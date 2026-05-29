#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OneFileBox.Models;

namespace OneFileBox.Services;

internal static class SvnCliLog
{
    internal static void Information(string msg, params object[] args) =>
        Console.WriteLine(args.Length > 0 ? $"[SvnCli] {msg}, args" : $"[SvnCli] {msg}");
    internal static void Error(Exception ex, string msg, params object[] args) =>
        Console.WriteLine($"[SvnCli] ERROR {msg}: {ex.Message}");
    internal static void Warning(string msg, params object[] args) =>
        Console.WriteLine($"[SvnCli] WARN {msg}");
    internal static void Debug(string msg, params object[] args) { }
}

/// <summary>
/// SVN CLI wrapper — implements the same interface as SharpSvn-based SvnService
/// using `svn` command-line with XML output parsing.
/// </summary>
public class SvnCliService : IDisposable
{
    // ── Concurrency (same as original SharpSvn SvnService) ──────────────────

    private static readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _readSemaphore = new(10, 10);

    private static readonly Dictionary<string, Task<int>> _headRevisionCache = new();
    private static readonly SemaphoreSlim _headRevisionLock = new(1, 1);
    private static readonly TimeSpan HeadRevisionCacheTtl = TimeSpan.FromSeconds(30);

    private const int LockWaitTimeoutMs = 30_000;
    private const int SafetyNetTimeoutMs = 600_000;

    private static int _fileTransferTimeoutMs = 120_000;
    public static int FileTransferTimeoutMs
    {
        get => _fileTransferTimeoutMs;
        set => _fileTransferTimeoutMs = Math.Clamp(value, 30_000, 600_000);
    }
    public static event Action? FileTransferTimeoutChanged;
    public static void NotifyFileTransferTimeoutChanged() => FileTransferTimeoutChanged?.Invoke();

    public event Action<string, string>? FileTransferActivity;

    // ── Credential helpers ──────────────────────────────────────────────────

    private static readonly Dictionary<string, string> _passwordCache = new();

    private static string GetPasswordForRepo(string workingCopyPath)
    {
        lock (_passwordCache)
        {
            return _passwordCache.TryGetValue(workingCopyPath, out var pwd) ? pwd : "";
        }
    }

    public void CacheCredential(string workingCopyPath, string? password)
    {
        lock (_passwordCache)
        {
            if (password != null)
                _passwordCache[workingCopyPath] = password;
            else
                _passwordCache.Remove(workingCopyPath);
        }
    }

    public event Action<string>? CredentialExpired;

    public SvnCliService()
    {
        SvnCliLog.Information("[SvnCli] Initialized — SVN CLI wrapper using SharpSvn-compatible interface");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Tier 1 — ReadOnly
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<Dictionary<string, FileSvnStatus>> GetStatusAsync(
        string workingCopyPath, bool depth)
    {
        return await ExecuteRead(token =>
        {
            var statuses = new Dictionary<string, FileSvnStatus>();
            var depthArg = depth ? "--depth infinity" : "--depth immediates";
            var result = RunSvn($"status --xml {depthArg}", workingCopyPath);
            if (result.exitCode != 0) return statuses;

            try
            {
                var doc = XDocument.Parse(result.output);
                foreach (var target in doc.Descendants("target"))
                {
                    foreach (var entry in target.Elements("entry"))
                    {
                        var path = entry.Attribute("path")?.Value ?? "";
                        if (string.IsNullOrEmpty(path)) continue;

                        FileSvnStatus svnStatus = entry.Element("wc-status")?.Attribute("item")?.Value switch
                        {
                            "modified"    => FileSvnStatus.Modified,
                            "added"       => FileSvnStatus.Added,
                            "deleted"     => FileSvnStatus.Deleted,
                            "conflicted"  => FileSvnStatus.Conflicted,
                            "unversioned" => FileSvnStatus.Unversioned,
                            "missing"     => FileSvnStatus.Missing,
                            "replaced"    => FileSvnStatus.Replaced,
                            "obstructed"  => FileSvnStatus.Obstructed,
                            "external"    => FileSvnStatus.External,
                            "incomplete"  => FileSvnStatus.Incomplete,
                            _             => Models.FileSvnStatus.Normal
                        };

                        // Tree conflict check
                        var treeConflicted = entry.Element("wc-status")?.Attribute("tree-conflicted")?.Value;
                        if (svnStatus == FileSvnStatus.Normal && treeConflicted == "true")
                            svnStatus = FileSvnStatus.TreeConflicted;

                        if (svnStatus != Models.FileSvnStatus.Normal)
                            statuses[path] = svnStatus;
                    }
                }
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetStatusAsync XML parse error for {Path}", workingCopyPath);
            }
            return statuses;
        });
    }

    public async Task<List<string>> GetServerUpdatePathsAsync(string workingCopyPath)
    {
        return await ExecuteRead(token =>
        {
            var paths = new List<string>();
            // --show-updates (-u) contacts server; without it we get local-only
            var result = RunSvn("status --xml --show-updates", workingCopyPath);
            if (result.exitCode != 0) return paths;

            try
            {
                var doc = XDocument.Parse(result.output);
                foreach (var entry in doc.Descendants("entry"))
                {
                    var remoteStatus = entry.Element("remote-status");
                    if (remoteStatus != null)
                    {
                        var path = entry.Attribute("path")?.Value ?? "";
                        if (!string.IsNullOrEmpty(path))
                            paths.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetServerUpdatePathsAsync parse error for {Path}", workingCopyPath);
            }
            return paths;
        });
    }

    public async Task<string> GetRepoUrlAsync(string workingCopyPath)
    {
        return await ExecuteRead(token =>
        {
            var result = RunSvn("info --xml", workingCopyPath);
            if (result.exitCode != 0) return "";

            try
            {
                var doc = XDocument.Parse(result.output);
                var url = doc.Descendants("url").FirstOrDefault()?.Value ?? "";
                return url;
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetRepoUrlAsync error for {Path}", workingCopyPath);
                return "";
            }
        });
    }

    public async Task<int> GetWorkingCopyRevisionAsync(string workingCopyPath)
    {
        return await ExecuteRead(token =>
        {
            var result = RunSvn("info --xml", workingCopyPath);
            if (result.exitCode != 0) return -1;

            try
            {
                var doc = XDocument.Parse(result.output);
                var revStr = doc.Descendants("commit").FirstOrDefault()?.Attribute("revision")?.Value ?? "";
                return int.TryParse(revStr, out var rev) ? rev : -1;
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetWorkingCopyRevisionAsync error for {Path}", workingCopyPath);
                return -1;
            }
        });
    }

    public async Task<int> GetHeadRevisionAsync(string repoUrl, string? username = null, string? password = null)
    {
        var newTask = DoGetHeadRevisionAsync(repoUrl, username, password);
        lock (_headRevisionLock)
        {
            if (_headRevisionCache.TryGetValue(repoUrl, out var inFlight))
            {
                if (inFlight != newTask) return inFlight.Result;
            }
            _headRevisionCache[repoUrl] = newTask;
        }

        try { return await newTask; }
        finally
        {
            var captured = newTask;
            _ = Task.Delay(HeadRevisionCacheTtl).ContinueWith(_ =>
            {
                lock (_headRevisionLock)
                {
                    if (_headRevisionCache.TryGetValue(repoUrl, out var cached) && cached == captured)
                        _headRevisionCache.Remove(repoUrl);
                }
            });
        }
    }

    private Task<int> DoGetHeadRevisionAsync(string repoUrl, string? username, string? password)
    {
        return ExecuteRead(token =>
        {
            var result = RunSvn("info -r HEAD --xml", repoUrl, username: username, password: password);
            if (result.exitCode != 0)
            {
                if (result.exitCode == -1 || result.error.Contains("authentication"))
                    CredentialExpired?.Invoke(repoUrl);
                return -1;
            }

            try
            {
                var doc = XDocument.Parse(result.output);
                var revStr = doc.Descendants("commit").FirstOrDefault()?.Attribute("revision")?.Value ?? "";
                return int.TryParse(revStr, out var rev) ? rev : -1;
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] DoGetHeadRevisionAsync error for {Url}", repoUrl);
                return -1;
            }
        });
    }

    public async Task<(bool success, int revision)> ValidateCredentialsAsync(
        string repoUrl, string? username = null, string? password = null)
    {
        var result = RunSvn("info --xml", repoUrl, username: username, password: password);
        if (result.exitCode == 0)
        {
            try
            {
                var doc = XDocument.Parse(result.output);
                var revStr = doc.Descendants("commit").FirstOrDefault()?.Attribute("revision")?.Value ?? "";
                var rev = int.TryParse(revStr, out var r) ? r : -1;
                return (true, rev);
            }
            catch { return (true, -1); }
        }
        // Try once more after clearing
        result = RunSvn("info --xml", repoUrl, username: username, password: password, clearCache: true);
        if (result.exitCode == 0)
        {
            try
            {
                var doc = XDocument.Parse(result.output);
                var revStr = doc.Descendants("commit").FirstOrDefault()?.Attribute("revision")?.Value ?? "";
                var rev = int.TryParse(revStr, out var r) ? r : -1;
                return (true, rev);
            }
            catch { return (true, -1); }
        }
        return (false, -1);
    }

    public async Task<List<string>> GetConflictedFilesAsync(string workingCopyPath)
    {
        return await ExecuteRead(token =>
        {
            var files = new List<string>();
            var result = RunSvn("status --xml --depth infinity", workingCopyPath);
            if (result.exitCode != 0) return files;

            try
            {
                var doc = XDocument.Parse(result.output);
                foreach (var entry in doc.Descendants("entry"))
                {
                    var wcStatus = entry.Element("wc-status");
                    if (wcStatus == null) continue;

                    var item = wcStatus.Attribute("item")?.Value ?? "";
                    var treeConflict = wcStatus.Attribute("tree-conflicted")?.Value;
                    var path = entry.Attribute("path")?.Value ?? "";

                    if ((item == "conflicted" || treeConflict == "true") && !string.IsNullOrEmpty(path))
                        files.Add(path);
                }
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetConflictedFilesAsync error for {Path}", workingCopyPath);
            }
            return files;
        });
    }

    public async Task<DateTime> GetLastChangedTimeAsync(string filePath)
    {
        return await ExecuteRead(token =>
        {
            var result = RunSvn("info --xml", filePath);
            if (result.exitCode != 0) return DateTime.MinValue;

            try
            {
                var doc = XDocument.Parse(result.output);
                var dateStr = doc.Descendants("date").FirstOrDefault()?.Value ?? "";
                if (DateTime.TryParse(dateStr, out var dt))
                    return dt.ToUniversalTime();
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "[SvnCli] GetLastChangedTimeAsync error for {Path}", filePath);
            }
            return DateTime.MinValue;
        });
    }

    public bool IsVersioned(string path) => IsValidWorkingCopy(path);

    public bool IsValidWorkingCopy(string path)
    {
        var result = RunSvn("info --xml", path);
        return result.exitCode == 0;
    }

    public bool IsCredentialValid(string workingCopyPath)
    {
        var result = RunSvn("info --xml", workingCopyPath);
        if (result.exitCode == 0) return true;
        if (result.error.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            result.error.Contains("authorization", StringComparison.OrdinalIgnoreCase))
            return false;
        return result.exitCode != 0; // local error means it's a valid wc but creds might be bad
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Tier 2 — LocalWrite
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<bool> AddPathAsync(string path)
    {
        return await ExecuteLocalWrite(token =>
        {
            TryCleanStaleLocks(Path.GetDirectoryName(path) ?? path);
            var result = RunSvn("add", path);
            return result.exitCode == 0;
        });
    }

    public async Task<bool> DeleteAsync(string path)
    {
        return await ExecuteLocalWrite(token =>
        {
            TryCleanStaleLocks(Path.GetDirectoryName(path) ?? path);
            var result = RunSvn("delete", path);
            // Idempotent — already deleted is OK
            return result.exitCode == 0 || result.error.Contains("not found");
        });
    }

    public async Task<bool> MoveAsync(string fromPath, string toPath)
    {
        return await ExecuteLocalWrite(token =>
        {
            TryCleanStaleLocks(Path.GetDirectoryName(fromPath) ?? fromPath);
            var result = RunSvn($"move \"{fromPath}\" \"{toPath}\"", workingDirectory: Path.GetDirectoryName(fromPath) ?? "");
            return result.exitCode == 0;
        });
    }

    public async Task<bool> RevertAsync(string path, bool recursive = true)
    {
        return await ExecuteLocalWrite(token =>
        {
            TryCleanStaleLocks(Path.GetDirectoryName(path) ?? path);
            var recursiveArg = recursive ? "--recursive" : "";
            var result = RunSvn($"revert {recursiveArg}", path);
            return result.exitCode == 0;
        });
    }

    public async Task<bool> ResolveAsync(string path, SvnAccept accept)
    {
        return await ExecuteLocalWrite(token =>
        {
            TryCleanStaleLocks(Path.GetDirectoryName(path) ?? path);
            var acceptStr = accept switch
            {
                SvnAccept.TheirsFull => "theirs-full",
                SvnAccept.MineFull    => "mine-full",
                SvnAccept.Working     => "working",
                SvnAccept.Base       => "base",
                _                            => "working"
            };
            var result = RunSvn($"resolve --accept {acceptStr}", path);
            return result.exitCode == 0;
        });
    }

    public async Task<bool> BreakWriteLockAsync(string path)
    {
        return await ExecuteLocalWrite(token =>
        {
            var result = RunSvn("lock --force", path);
            return result.exitCode == 0;
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Tier 3 — HeavyWrite
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<bool> CommitAsync(string workingCopyPath, string message)
    {
        try
        {
            return await ExecuteHeavyWrite(token =>
            {
                TryCleanStaleLocks(workingCopyPath);
                return ExecuteSvnWithNotify(() =>
                {
                    var result = RunSvn($"commit -m \"{EscapeForSvn(message)}\"", workingCopyPath,
                        progressCallback: (line) =>
                    {
                        FileTransferActivity?.Invoke("", "commit:" + line);
                    });
                    return result.exitCode == 0;
                }, token);
            }, workingCopyPath);
        }
        catch (Exception ex)
        {
            if (IsAuthError(ex))
            {
                CredentialExpired?.Invoke(workingCopyPath);
                SvnCliLog.Error(ex, "[SvnCli] CommitAsync auth error for {Path}", workingCopyPath);
            }
            SvnCliLog.Error(ex, "[SvnCli] CommitAsync failed for {Path}", workingCopyPath);
            return false;
        }
    }

    public async Task<bool> UpdateAsync(string workingCopyPath)
        => await UpdateAsync(new[] { workingCopyPath });

    public async Task<bool> UpdateAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return true;
        var topDir = paths.Count == 1 ? paths[0] : Path.GetDirectoryName(paths[0]) ?? paths[0];

        try
        {
            return await ExecuteHeavyWrite(token =>
            {
                TryCleanStaleLocks(topDir);
                return ExecuteSvnWithNotify(() =>
                {
                    var pathArgs = string.Join(" ", paths.Select(p => $"\"{p}\""));
                    var result = RunSvn($"update {pathArgs} --verbose --progress", topDir,
                        progressCallback: (line) =>
                    {
                        // Parse "   U   /path/to/file" update progress
                        var parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                            FileTransferActivity?.Invoke(parts[1], parts[0]);
                    });
                    return result.exitCode == 0;
                }, token);
            }, topDir);
        }
        catch (Exception ex)
        {
            if (IsAuthError(ex))
            {
                CredentialExpired?.Invoke(topDir);
                SvnCliLog.Error(ex, "[SvnCli] UpdateAsync auth error for {TopDir}", topDir);
            }
            SvnCliLog.Error(ex, "[SvnCli] UpdateAsync failed for {TopDir}", topDir);
            return false;
        }
    }

    public async Task<(string output, int exitCode, string error)> CheckoutAsync(
        string repoUrl, string workingCopyPath, string? username = null, string? password = null)
    {
        try
        {
            TryCleanStaleLocks(workingCopyPath);
            return await ExecuteHeavyWrite(token =>
            {
                return ExecuteSvnWithNotify(() =>
                {
                    var result = RunSvn($"checkout \"{repoUrl}\" \"{workingCopyPath}\" --verbose --progress",
                        workingDirectory: Path.GetDirectoryName(workingCopyPath) ?? "",
                        username: username, password: password,
                        progressCallback: (line) =>
                        {
                            var parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                FileTransferActivity?.Invoke(parts[1], parts[0]);
                        });
                    return (result.output, result.exitCode, result.error);
                }, token);
            }, workingCopyPath);
        }
        catch (Exception ex)
        {
            if (IsAuthError(ex))
            {
                CredentialExpired?.Invoke(workingCopyPath);
                SvnCliLog.Error(ex, "[SvnCli] CheckoutAsync auth error for {Url}", repoUrl);
            }
            SvnCliLog.Error(ex, "[SvnCli] CheckoutAsync failed for {Url}", repoUrl);
            return ("", 1, ex.Message);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Connection test
    // ══════════════════════════════════════════════════════════════════════════

    public enum SvnConnectResult
    {
        Success, AuthFailed, AccessDenied, RepoNotFound,
        NetworkError, SslCertError, Timeout, Unknown
    }

    public async Task<(SvnConnectResult result, string? errorMessage)> TestConnectionAsync(
        string url, string? username = null, string? password = null)
    {
        return await ExecuteRead(token =>
        {
            var result = RunSvn("list --xml", url, username: username, password: password);
            if (result.exitCode == 0)
                return (SvnConnectResult.Success, (string?)null);

            // Categorize error
            var err = result.error;
            if (err.Contains("E170001") || err.Contains("authentication") || err.Contains("authorization"))
                return (SvnConnectResult.AuthFailed, result.error);
            if (err.Contains("E230001") || err.Contains("SSL") || err.Contains("certificate"))
                return (SvnConnectResult.SslCertError, result.error);
            if (err.Contains("E175002") || err.Contains("E170013") || err.Contains("path not found"))
                return (SvnConnectResult.RepoNotFound, result.error);
            if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                err.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                err.Contains("no route", StringComparison.OrdinalIgnoreCase) ||
                err.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
                err.Contains("network", StringComparison.OrdinalIgnoreCase))
                return (SvnConnectResult.NetworkError, result.error);
            if (err.Contains("access denied") || err.Contains("Forbidden"))
                return (SvnConnectResult.AccessDenied, result.error);

            return (SvnConnectResult.Unknown, result.error);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Internal helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<T> ExecuteRead<T>(Func<CancellationToken, T> operation)
    {
        if (!await _readSemaphore.WaitAsync(LockWaitTimeoutMs))
            throw new TimeoutException($"SVN read timed out waiting for a read slot after {LockWaitTimeoutMs / 1000}s.");
        try
        {
            using var safetyCts = new CancellationTokenSource(SafetyNetTimeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(safetyCts.Token);
            return await Task.Run(() => operation(linked.Token), linked.Token);
        }
        catch (OperationCanceledException) {
            throw new TimeoutException($"SVN read timed out after {SafetyNetTimeoutMs / 1000}s.");
        }
        finally { _readSemaphore.Release(); }
    }

    private async Task<T> ExecuteLocalWrite<T>(Func<CancellationToken, T> operation)
    {
        if (!await _writeSemaphore.WaitAsync(LockWaitTimeoutMs))
            throw new TimeoutException($"SVN write timed out waiting for write lock after {LockWaitTimeoutMs / 1000}s.");
        try
        {
            using var safetyCts = new CancellationTokenSource(LockWaitTimeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(safetyCts.Token);
            return await Task.Run(() => operation(linked.Token), linked.Token);
        }
        finally { _writeSemaphore.Release(); }
    }

    private async Task<T> ExecuteHeavyWrite<T>(
        Func<CancellationToken, T> operation,
        string workingCopyPath = "",
        Action<string>? onAuthFailed = null)
    {
        if (!await _writeSemaphore.WaitAsync(LockWaitTimeoutMs))
            throw new TimeoutException($"SVN operation timed out waiting for write lock after {LockWaitTimeoutMs / 1000}s.");
        try
        {
            return await Task.Run(() => operation(default));
        }
        catch (Exception ex) when (IsAuthError(ex))
        {
            if (onAuthFailed != null && !string.IsNullOrEmpty(workingCopyPath))
                onAuthFailed(workingCopyPath);
            throw;
        }
        finally { _writeSemaphore.Release(); }
    }

    private T ExecuteSvnWithNotify<T>(Func<T> operation, CancellationToken token)
    {
        return operation();
    }

    private static bool IsAuthError(Exception ex)
    {
        var err = ex?.ToString() ?? "";
        return err.Contains("E170001") || err.Contains("authentication") || err.Contains("authorization failed");
    }

    private void TryCleanStaleLocks(string workingCopyPath)
    {
        try
        {
            RunSvn("cleanup", workingCopyPath);
        }
        catch { /* swallow */ }
    }

    private static string EscapeForSvn(string message)
    {
        // Escape double quotes and newlines for svn commit -m "..."
        return message.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }

    /// <summary>
    /// Runs an `svn` command and returns (stdout, exitCode, stderr).
    /// </summary>
    private static (string output, int exitCode, string error) RunSvn(
        string args,
        string? workingDirectory = null,
        string? username = null,
        string? password = null,
        bool clearCache = false,
        Action<string>? progressCallback = null)
    {
        var fullArgs = new StringBuilder();
        if (!string.IsNullOrEmpty(username))
        {
            fullArgs.Append($"--username \"{username}\" ");
            if (!string.IsNullOrEmpty(password))
                fullArgs.Append("--password-from-stdin ");
            else
                fullArgs.Append("--password \"\" ");
        }
        fullArgs.Append("--non-interactive ");
        if (clearCache) fullArgs.Append("--no-auth-cache ");
        fullArgs.Append(args);

        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = fullArgs.ToString(),
            WorkingDirectory = workingDirectory ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Password via stdin if needed
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            psi.RedirectStandardInput = true;
        }

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return ("", -1, "Failed to start process");

            string? passwordInput = null;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                passwordInput = password;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (passwordInput != null)
            {
                process.StandardInput.WriteLine(passwordInput);
                process.StandardInput.Close();
            }

            var timedOut = !process.WaitForExit(300_000);
            if (timedOut)
            {
                try { process.Kill(); } catch { }
                return ("", -1, "svn command timed out after 300s");
            }

            var output = outputTask.Result;
            var error = errorTask.Result;

            return (output, process.ExitCode, error);
        }
        catch (Exception ex)
        {
            return ("", -1, ex.Message);
        }
    }

    public void Dispose()
    {
        // Static semaphores are shared — do not dispose
    }
}