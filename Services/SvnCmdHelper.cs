using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OneFileBox_new.Models;

namespace OneFileBox_new.Services;

public static partial class SvnCmdHelper
{
    public static int IdleHeartbeatTimeoutMs { get; set; } = 8000;
    public static int NormalMaxTimeoutMs { get; set; } = 30000;
    public static int BigFileMaxTimeoutMs { get; set; } = 600000;
    public static int RetryTimes { get; set; } = 2;
    public static int RetrySleepMs { get; set; } = 300;
    public static long BigFileSizeThreshold { get; set; } = 104857600;
    public static long LongTimeWarnMs { get; set; } = 300000;

    public static async Task<SvnExecuteResult> ExecuteAsyncWithProgress(
        string args,
        string workDir,
        string user,
        string pwd,
        string opName,
        SvnSyncManager manager,
        bool isBigTask = false)
    {
        var result = new SvnExecuteResult();
        var progress = new SvnProgressInfo { OperateName = opName, TaskStartTime = DateTime.Now };
        int maxTotalTimeout = isBigTask ? BigFileMaxTimeoutMs : NormalMaxTimeoutMs;

        if (!Directory.Exists(workDir))
        {
            result.Success = false;
            result.ErrorMsg = $"工作目录不存在：{workDir}";
            manager.RaiseLog(SvnLogLevel.Error, result.ErrorMsg);
            return result;
        }

        string fullArg = $"{args} --username {user} --password-from-stdin --no-auth-cache --non-interactive -v --progress";
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = fullArg,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? proc = null;
        try
        {
            proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var ctsTotal = new CancellationTokenSource(maxTotalTimeout);

            proc.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                outputBuilder.AppendLine(e.Data);
                ParseProgressLine(e.Data, progress);
                progress.LastActiveTime = DateTime.Now;
                progress.IsActiveWorking = true;
                manager.RaiseFileProgress(progress);
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                errorBuilder.AppendLine(e.Data);
            };

            if (!proc.Start())
            {
                result.Success = false;
                result.ErrorMsg = "启动 svn 进程失败，请检查环境变量";
                manager.RaiseLog(SvnLogLevel.Error, result.ErrorMsg);
                return result;
            }

            await proc.StandardInput.WriteLineAsync(pwd);
            await proc.StandardInput.FlushAsync();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            bool exited = false;
            while (!exited && !ctsTotal.Token.IsCancellationRequested)
            {
                exited = proc.WaitForExit(200);
                if (exited) break;

                var idleSpan = DateTime.Now - progress.LastActiveTime;
                if (idleSpan.TotalMilliseconds > IdleHeartbeatTimeoutMs)
                {
                    proc.Kill();
                    result.ErrorMsg = "任务长时间无响应，已终止";
                    manager.RaiseLog(SvnLogLevel.Warn, result.ErrorMsg);
                    return result;
                }

                long elapsed = (long)(DateTime.Now - progress.TaskStartTime).TotalMilliseconds;
                if (elapsed >= LongTimeWarnMs && !string.IsNullOrEmpty(progress.CurrentFile))
                {
                    var warnInfo = new SvnLongTaskInfo
                    {
                        FileName = progress.CurrentFile,
                        FileSizeBytes = progress.TotalBytes,
                        CurrentPercent = progress.Percent,
                        ElapsedMs = elapsed,
                        OperateName = progress.OperateName
                    };
                    if (manager.RaiseLongTaskConfirm(warnInfo))
                    {
                        proc.Kill();
                        result.UserCanceled = true;
                        result.ErrorMsg = "用户取消长任务";
                        manager.RaiseLog(SvnLogLevel.Warn, result.ErrorMsg);
                        return result;
                    }
                }
            }

            if (!exited)
            {
                proc.Kill();
                result.ErrorMsg = "任务执行超时强制终止";
                manager.RaiseLog(SvnLogLevel.Error, result.ErrorMsg);
                return result;
            }

            proc.WaitForExit();
            result.ExitCode = proc.ExitCode;
            result.Output = outputBuilder.ToString().Trim();
            result.ErrorMsg = errorBuilder.ToString().Trim();
            result.Success = result.ExitCode == 0 && !result.UserCanceled;

            if (!result.Success && !result.UserCanceled)
                manager.RaiseLog(SvnLogLevel.Error, $"SVN 执行失败：{result.ErrorMsg}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMsg = $"执行异常：{ex.Message}";
            manager.RaiseLog(SvnLogLevel.Error, result.ErrorMsg);
        }
        finally
        {
            proc?.Dispose();
        }

        return result;
    }

    public static async Task<SvnExecuteResult> ExecuteWithRetryAsync(
        string args, string workDir, string user, string pwd,
        string opName, SvnSyncManager manager, bool isBig = false)
    {
        SvnExecuteResult? res = null;
        for (int i = 0; i <= RetryTimes; i++)
        {
            res = await ExecuteAsyncWithProgress(args, workDir, user, pwd, opName, manager, isBig);
            if (res.Success || res.UserCanceled) break;
            if (i >= RetryTimes) break;
            manager.RaiseLog(SvnLogLevel.Warn, $"第{i + 1}次失败，稍后重试");
            await Task.Delay(RetrySleepMs);
        }
        return res!;
    }

    public static XDocument? QueryRemoteListXml(string svnUrl, string user, string pwd)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = $"list -R --xml {svnUrl} --username {user} --password-from-stdin --no-auth-cache --non-interactive",
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var p = Process.Start(psi);
            if (p == null) return null;
            p.StandardInput.WriteLine(pwd);
            p.StandardInput.Flush();
            p.WaitForExit(10000);
            string xml = p.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrEmpty(xml) ? null : XDocument.Parse(xml);
        }
        catch { return null; }
    }

    public static List<(string RelPath, long Revision)> ParseRemoteEntries(XDocument? xml)
    {
        var list = new List<(string, long)>();
        if (xml == null) return list;
        var ns = xml.Root?.Name.Namespace!;
        foreach (var entry in xml.Descendants(ns + "entry"))
        {
            string name = entry.Element(ns + "name")?.Value ?? "";
            if (string.IsNullOrEmpty(name)) continue;
            var commit = entry.Element(ns + "commit");
            if (commit == null) continue;
            if (!long.TryParse(commit.Element(ns + "revision")?.Value, out long rev)) continue;
            list.Add((name, rev));
        }
        return list;
    }

    public static long QueryLocalFileRev(string filePath, string user, string pwd)
    {
        if (!File.Exists(filePath)) return 0;
        string dir = Path.GetDirectoryName(filePath) ?? "";
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = $"info --xml \"{filePath}\" --username {user} --password-from-stdin --no-auth-cache --non-interactive",
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        try
        {
            using var p = Process.Start(psi);
            if (p == null) return 0;
            p.StandardInput.WriteLine(pwd);
            p.StandardInput.Flush();
            p.WaitForExit(8000);
            var doc = XDocument.Parse(p.StandardOutput.ReadToEnd().Trim());
            var ns = doc.Root?.Name.Namespace!;
            var revStr = doc.Descendants(ns + "revision").FirstOrDefault()?.Value;
            return long.TryParse(revStr, out long r) ? r : 0;
        }
        catch { return 0; }
    }

    public static List<SvnConflictFileInfo> ScanAllConflictFiles(string rootDir, string user, string pwd)
    {
        var list = new List<SvnConflictFileInfo>();
        string statusXml = QueryLocalStatus(rootDir, user, pwd);
        if (string.IsNullOrWhiteSpace(statusXml)) return list;
        XDocument doc;
        try { doc = XDocument.Parse(statusXml); }
        catch { return list; }

        var ns = doc.Root?.Name.Namespace!;
        var conflictItems = doc.Descendants(ns + "entry")
            .Where(e => e.Element(ns + "status")?.Attribute("item")?.Value == "conflicted");

        foreach (var item in conflictItems)
        {
            string relPath = item.Attribute("path")?.Value ?? "";
            string fullPath = Path.Combine(rootDir, relPath);
            if (!File.Exists(fullPath)) continue;
            var fiLocal = new FileInfo(fullPath);
            list.Add(new SvnConflictFileInfo
            {
                FullPath = fullPath,
                LocalSize = fiLocal.Length,
                LocalModifyTime = fiLocal.LastWriteTime
            });
        }
        return list;
    }

    public static string QueryLocalStatus(string dir, string user, string pwd)
    {
        if (!Directory.Exists(dir)) return string.Empty;
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = $"status --xml --username {user} --password-from-stdin --no-auth-cache --non-interactive",
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi);
        if (p == null) return "";
        p.StandardInput.WriteLine(pwd);
        p.StandardInput.Flush();
        p.WaitForExit(5000);
        return p.StandardOutput.ReadToEnd().Trim();
    }

    public static async Task ResolveConflictFile(
        string filePath, SvnConflictResolveMode mode,
        string user, string pwd, SvnSyncManager manager)
    {
        string arg = mode == SvnConflictResolveMode.UseRemote ? "theirs-full" : "mine-full";
        await ExecuteWithRetryAsync(
            $"resolve --accept {arg} \"{filePath}\"",
            Path.GetDirectoryName(filePath)!, user, pwd, "冲突处理", manager);
    }

    public static async Task<bool> SafeAddFileAsync(
        string path, string user, string pwd, SvnSyncManager manager)
    {
        var record = new SvnSyncRecordItem { SyncPath = path, OperateName = "新增文件" };
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                record.IsSuccess = false;
                record.Message = "文件不存在";
                manager.RaiseSingleSyncCompleted(record);
                return false;
            }

            var res = await ExecuteWithRetryAsync(
                $"add \"{path}\" --force",
                Path.GetDirectoryName(path)!, user, pwd, "新增", manager);

            if (res.UserCanceled)
            {
                record.IsSuccess = false;
                manager.RaiseSingleSyncCompleted(record);
                return false;
            }

            if (!res.Success)
            {
                if (res.ErrorMsg.Contains("already under version"))
                {
                    record.IsSuccess = true;
                    record.Message = "已受控";
                    manager.RaiseSingleSyncCompleted(record);
                    return true;
                }
                record.IsSuccess = false;
                record.Message = res.ErrorMsg;
                manager.RaiseSingleSyncCompleted(record);
                return false;
            }

            record.IsSuccess = true;
            manager.RaiseSingleSyncCompleted(record);
            return true;
        }
        catch
        {
            record.IsSuccess = false;
            manager.RaiseSingleSyncCompleted(record);
            return false;
        }
    }

    public static async Task<bool> SafeDeleteFileAsync(
        string path, string user, string pwd, SvnSyncManager manager)
    {
        var record = new SvnSyncRecordItem { SyncPath = path, OperateName = "删除文件" };
        try
        {
            var res = await ExecuteWithRetryAsync(
                $"delete \"{path}\"",
                Path.GetDirectoryName(path)!, user, pwd, "删除", manager);

            if (res.UserCanceled)
            {
                record.IsSuccess = false;
                manager.RaiseSingleSyncCompleted(record);
                return false;
            }

            if (!res.Success)
            {
                if (res.ErrorMsg.Contains("not found"))
                {
                    record.IsSuccess = true;
                    manager.RaiseSingleSyncCompleted(record);
                    return true;
                }
                record.IsSuccess = false;
                manager.RaiseSingleSyncCompleted(record);
                return false;
            }

            record.IsSuccess = true;
            manager.RaiseSingleSyncCompleted(record);
            return true;
        }
        catch
        {
            record.IsSuccess = false;
            manager.RaiseSingleSyncCompleted(record);
            return false;
        }
    }

    public static async Task<bool> CommitDirAsync(
        string dir, string msg, string user, string pwd, SvnSyncManager manager)
    {
        var res = await ExecuteWithRetryAsync($"commit -m \"{msg}\"", dir, user, pwd, "提交", manager);
        if (res.ErrorMsg.Contains("nothing to commit")) return true;
        return res.Success;
    }

    public static async Task<bool> UpdateDirAsync(
        string dir, string user, string pwd, SvnSyncManager manager)
    {
        var res = await ExecuteWithRetryAsync("update --accept postpone", dir, user, pwd, "更新", manager);
        return res.Success;
    }

    public static async Task<string?> GetRepoUrlAsync(string localPath)
    {
        try
        {
            var res = await ExecuteWithRetryAsync(
                $"info --xml \"{localPath}\"",
                localPath, null!, null!, null!, null!);

            if (res.ExitCode != 0 || string.IsNullOrEmpty(res.Output))
                return null;

            var doc = XDocument.Parse(res.Output);
            var ns = doc.Root?.Name.Namespace!;
            return doc.Descendants(ns + "url").FirstOrDefault()?.Value;
        }
        catch { return null; }
    }

    public static async Task<bool> CleanUpAsync(
        string dir, string user, string pwd, SvnSyncManager manager)
    {
        var res = await ExecuteWithRetryAsync("cleanup", dir, user, pwd, "清理锁定", manager);
        return res.Success;
    }

    private static void ParseProgressLine(string line, SvnProgressInfo progress)
    {
        var fileMatch = FileReg().Match(line);
        if (fileMatch.Success) progress.CurrentFile = fileMatch.Groups[2].Value.Trim();

        var progMatch = ProgressReg().Match(line);
        if (progMatch.Success)
        {
            if (long.TryParse(progMatch.Groups[2].Value, out long d)) progress.DoneBytes = d;
            if (long.TryParse(progMatch.Groups[3].Value, out long t)) progress.TotalBytes = t;
            if (double.TryParse(progMatch.Groups[1].Value, out double p)) progress.Percent = p;
        }
    }

    [GeneratedRegex(@"^(Sending|Receiving|Adding|Deleting)\s+(.+)$")]
    private static partial Regex FileReg();

    [GeneratedRegex(@"(\d+)%\s*\((\d+)/(\d+)\s*bytes\)")]
    private static partial Regex ProgressReg();
}
