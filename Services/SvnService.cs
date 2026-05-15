#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OneFileBox.Models;
using Serilog;

namespace OneFileBox.Services;

/// <summary>
/// SVN CLI wrapper using Process execution and XML output parsing.
/// All operations are serialized via SemaphoreSlim to prevent concurrent
/// SVN operations on the same working copy.
/// </summary>
public class SvnService : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private const int DefaultTimeoutMs = 120_000;

    public SvnService()
    {
        Log.Information("SvnService initialized — SVN CLI, operation timeout {Timeout}s",
            DefaultTimeoutMs / 1000);
    }

    /// <summary>
    /// Runs an SVN operation with exclusive access (serialized) and timeout.
    /// </summary>
    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, T> operation, CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromMilliseconds(DefaultTimeoutMs), cancellationToken))
        {
            throw new TimeoutException(
                $"SVN operation timed out waiting for lock after {DefaultTimeoutMs / 1000}s.");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(DefaultTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            return await Task.Run(() => operation(linkedCts.Token), linkedCts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"SVN operation timed out after {DefaultTimeoutMs / 1000}s.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Runs svn command line and returns (output, exitCode, error).
    /// </summary>
    private async Task<(string output, int exitCode, string error)> RunSvnAsync(
        string arguments,
        string? workingDir = null,
        string? username = null,
        string? password = null,
        int timeoutMs = 60000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(username))
        {
            psi.Environment["SVN_USERNAME"] = username;
        }
        if (!string.IsNullOrEmpty(password))
        {
            psi.Environment["SVN_PASSWORD"] = password;
        }

        using var cts = new CancellationTokenSource(timeoutMs);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cts.Token);

        return (outputBuilder.ToString(), process.ExitCode, errorBuilder.ToString());
    }

    /// <summary>
    /// Parses 'svn status --xml' output into a dictionary of path → FileSvnStatus.
    /// </summary>
    private FileSvnStatus ParseStatusChar(string wcStatus, string reposStatus)
    {
        if (wcStatus == "?") return FileSvnStatus.Unversioned;
        if (wcStatus == "!") return FileSvnStatus.Missing;
        if (wcStatus == "~") return FileSvnStatus.Obstructed;
        if (wcStatus == "C") return FileSvnStatus.Conflicted;
        if (wcStatus == "X") return FileSvnStatus.External;
        if (wcStatus == "I") return FileSvnStatus.Unknown; // ignored
        if (wcStatus == "M" || wcStatus == " ") return FileSvnStatus.Modified;
        if (wcStatus == "A") return FileSvnStatus.Added;
        if (wcStatus == "D") return FileSvnStatus.Deleted;
        if (wcStatus == "R") return FileSvnStatus.Replaced;
        return FileSvnStatus.Normal;
    }

    public async Task<Dictionary<string, FileSvnStatus>> GetStatusAsync(string workingCopyPath, bool recursive = true)
    {
        return await ExecuteAsync(token =>
        {
            var statuses = new Dictionary<string, FileSvnStatus>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var depth = recursive ? "--depth=infinity" : "--depth=children";
                var (output, exitCode, _) = RunSvnAsync($"status --xml {depth} \"{workingCopyPath}\"").GetAwaiter().GetResult();

                if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return statuses;

                var doc = XDocument.Parse(output);
                XNamespace ns = "urn:svn-command-line-output";

                foreach (var target in doc.Descendants("target"))
                {
                    var path = target.Attribute("path")?.Value ?? "";
                    if (string.IsNullOrEmpty(path)) continue;

                    foreach (var entry in target.Elements("entry"))
                    {
                        var entryPath = entry.Attribute("path")?.Value ?? "";
                        var fullPath = Path.IsPathRooted(entryPath) ? entryPath : Path.Combine(path, entryPath);

                        var wcStatusElem = entry.Element("wc-status");
                        var reposStatusElem = entry.Element("repos-status");
                        var wcStatus = wcStatusElem?.Attribute("item")?.Value ?? "";
                        var reposStatus = reposStatusElem?.Attribute("item")?.Value ?? "";

                        var svnStatus = ParseStatusChar(wcStatus, reposStatus);
                        if (svnStatus != FileSvnStatus.Normal)
                            statuses[fullPath] = svnStatus;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting SVN status for {Path}", workingCopyPath);
            }

            return statuses;
        });
    }

    public async Task<string> GetRepoUrlAsync(string workingCopyPath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"info --xml \"{workingCopyPath}\"").GetAwaiter().GetResult();
                if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return "";

                var doc = XDocument.Parse(output);
                var url = doc.Descendants("url").FirstOrDefault()?.Value ?? "";
                return url;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting repo URL for {Path}", workingCopyPath);
            }
            return "";
        });
    }

    public async Task<int> GetWorkingCopyRevisionAsync(string workingCopyPath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"info --xml \"{workingCopyPath}\"").GetAwaiter().GetResult();
                if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return -1;

                var doc = XDocument.Parse(output);
                var revision = doc.Descendants("entry").FirstOrDefault()?.Attribute("revision")?.Value;
                return int.TryParse(revision, out var rev) ? rev : -1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting revision for {Path}", workingCopyPath);
            }
            return -1;
        });
    }

    public async Task<bool> CommitAsync(string workingCopyPath, string message, string? username = null, string? password = null)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var userArgs = BuildAuthArgs(username, password);
                var (output, exitCode, _) = RunSvnAsync(
                    $"commit -m \"{Escape(message)}\" {userArgs} \"{workingCopyPath}\"",
                    username: username, password: password).GetAwaiter().GetResult();

                if (exitCode == 0)
                    Log.Information("[SvnService] Committed: {Path}", workingCopyPath);
                else
                    Log.Warning("[SvnService] Commit failed with exit {Code}: {Path}", exitCode, workingCopyPath);

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Commit failed for {Path}", workingCopyPath);
                return false;
            }
        });
    }

    public async Task<bool> UpdateAsync(string workingCopyPath, string? username = null, string? password = null)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var userArgs = BuildAuthArgs(username, password);
                var (output, exitCode, _) = RunSvnAsync(
                    $"update {userArgs} \"{workingCopyPath}\"",
                    username: username, password: password).GetAwaiter().GetResult();

                if (exitCode == 0)
                    Log.Information("[SvnService] Updated: {Path}", workingCopyPath);
                else
                    Log.Warning("[SvnService] Update failed with exit {Code}: {Path}", exitCode, workingCopyPath);

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Update failed for {Path}", workingCopyPath);
                return false;
            }
        });
    }

    public async Task<bool> AddFileAsync(string filePath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"add \"{filePath}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Add failed for {Path}", filePath);
                return false;
            }
        });
    }

    public async Task<bool> AddPathAsync(string path)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"add \"{path}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AddPath failed for {Path}", path);
                return false;
            }
        });
    }

    public async Task<bool> DeleteAsync(string path)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"delete \"{path}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Delete failed for {Path}", path);
                return false;
            }
        });
    }

    public async Task<bool> MoveAsync(string fromPath, string toPath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"move \"{fromPath}\" \"{toPath}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Move failed: {From} → {To}", fromPath, toPath);
                return false;
            }
        });
    }

    public async Task<bool> RevertAsync(string path, bool recursive = true)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var recurse = recursive ? "--depth=infinity" : "--depth=empty";
                var (output, exitCode, _) = RunSvnAsync($"revert {recurse} \"{path}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Revert failed for {Path}", path);
                return false;
            }
        });
    }

    public async Task<bool> CleanUpAsync(string workingCopyPath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"cleanup \"{workingCopyPath}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CleanUp failed for {Path}", workingCopyPath);
                return false;
            }
        });
    }

    public async Task<List<string>> GetConflictedFilesAsync(string workingCopyPath)
    {
        return await ExecuteAsync(token =>
        {
            var files = new List<string>();
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"status --xml --depth=infinity \"{workingCopyPath}\"").GetAwaiter().GetResult();
                if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return files;

                var doc = XDocument.Parse(output);
                foreach (var entry in doc.Descendants("entry"))
                {
                    var wcStatus = entry.Element("wc-status")?.Attribute("item")?.Value ?? "";
                    if (wcStatus == "C")
                    {
                        var path = entry.Attribute("path")?.Value ?? "";
                        if (!string.IsNullOrEmpty(path))
                            files.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting conflicted files for {Path}", workingCopyPath);
            }
            return files;
        });
    }

    public async Task<(string output, int exitCode, string error)> CheckoutAsync(
        string url, string localPath, string? username = null, string? password = null)
    {
        return await ExecuteAsync(token =>
        {
            var userArgs = BuildAuthArgs(username, password);
            return RunSvnAsync($"checkout \"{url}\" \"{localPath}\" {userArgs}", username: username, password: password).GetAwaiter().GetResult();
        });
    }

    public bool IsValidWorkingCopy(string path)
    {
        try
        {
            // Check for .svn directory
            var svnDir = Path.Combine(path, ".svn");
            return Directory.Exists(svnDir) || File.Exists(svnDir);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResolveAsync(string path, string resolution = "working")
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"resolve --accept={resolution} \"{path}\"").GetAwaiter().GetResult();
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Resolve failed for {Path}", path);
                return false;
            }
        });
    }

    public async Task<DateTime> GetLastChangedTimeAsync(string filePath)
    {
        return await ExecuteAsync(token =>
        {
            try
            {
                var (output, exitCode, _) = RunSvnAsync($"info --xml \"{filePath}\"").GetAwaiter().GetResult();
                if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return DateTime.MinValue;

                var doc = XDocument.Parse(output);
                var dateStr = doc.Descendants("date").FirstOrDefault()?.Value;
                if (DateTime.TryParse(dateStr, out var dt))
                    return dt.ToUniversalTime();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting last changed time for {Path}", filePath);
            }
            return DateTime.MinValue;
        });
    }

    private static string BuildAuthArgs(string? username, string? password)
    {
        var args = "";
        if (!string.IsNullOrEmpty(username))
            args += $" --username=\"{username}\"";
        if (!string.IsNullOrEmpty(password))
            args += $" --password=\"{password}\"";
        return args;
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"").Replace("\n", "\\n");

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
