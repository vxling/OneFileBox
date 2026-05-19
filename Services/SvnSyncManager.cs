using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using OneFileBox_new.Models;

namespace OneFileBox_new.Services;

public class SvnSyncManager : IDisposable
{
    #region Events

    internal event Action<SvnLogLevel, string>? LogReceived;
    internal event Action<SvnProgressInfo>? FileProgressChanged;
    internal event Func<SvnLongTaskInfo, bool>? LongTaskConfirmRequired;
    internal event Action<List<SvnConflictFileInfo>, Action<SvnConflictResolveMode>>? ConflictResolvedRequired;
    internal event Action<SvnBatchOperateProgress>? BatchCommitProgress;
    internal event Action<SvnBatchOperateProgress>? BatchUpdateProgress;
    internal event Action<SvnSyncRecordItem>? SingleSyncCompleted;

    // 供外部类（如 SvnCmdHelper）触发事件的公共包装方法
    public void RaiseLog(SvnLogLevel level, string msg) => LogReceived?.Invoke(level, msg);
    public void RaiseFileProgress(SvnProgressInfo info) => FileProgressChanged?.Invoke(info);
    public bool RaiseLongTaskConfirm(SvnLongTaskInfo info) => LongTaskConfirmRequired?.Invoke(info) ?? false;
    public void RaiseConflictResolved(List<SvnConflictFileInfo> conflicts, Action<SvnConflictResolveMode> resolver) => ConflictResolvedRequired?.Invoke(conflicts, resolver);
    public void RaiseBatchCommitProgress(SvnBatchOperateProgress p) => BatchCommitProgress?.Invoke(p);
    public void RaiseBatchUpdateProgress(SvnBatchOperateProgress p) => BatchUpdateProgress?.Invoke(p);
    public void RaiseSingleSyncCompleted(SvnSyncRecordItem item) => SingleSyncCompleted?.Invoke(item);

    #endregion

    #region Shutdown State

    public bool IsShuttingDown { get; private set; }
    public bool IsDisposedComplete { get; private set; }
    private const int ShutdownWaitTimeoutMs = 15000;

    #endregion

    #region Config

    public string LocalSyncRoot { get; set; } = string.Empty;
    public string RemoteSvnUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int BatchCommitFileCount { get; set; } = 30;
    public int BatchIntervalMs { get; set; } = 400;

    private const int DebounceMs = 2000;
    private const int AutoUpdateIntervalSec = 60;
    private const int FileStableDelayMs = 3000;

    #endregion

    #region Queue & Lock

    private readonly Channel<SvnSyncTask> _highTaskChannel;
    private readonly Channel<SvnSyncTask> _lowTaskChannel;
    private readonly object _svnWriteOperateLock = new();
    private bool _isWriteTaskRunning;
    private readonly ConcurrentDictionary<string, byte> _pendingCommitFiles = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _fileDebounceDict = new();
    private Timer? _autoUpdateTimer;
    private bool _disposed;

    #endregion

    public SvnSyncManager() { }

    public SvnSyncManager(string localRoot, string remoteUrl, string user, string pwd)
    {
        LocalSyncRoot = localRoot;
        RemoteSvnUrl = remoteUrl;
        UserName = user;
        Password = pwd;
        _highTaskChannel = Channel.CreateBounded<SvnSyncTask>(1000);
        _lowTaskChannel = Channel.CreateBounded<SvnSyncTask>(1000);
    }

    #region Start / Stop

    public void StartSyncService()
    {
        _ = Task.Run(ConsumePriorityWriteTaskLoop);
        _autoUpdateTimer = new Timer(_ => EnqueueBatchUpdate(), null, 5000, AutoUpdateIntervalSec * 1000);
    }

    public void StopSyncService()
    {
        _autoUpdateTimer?.Dispose();
        _highTaskChannel.Writer.Complete();
        _lowTaskChannel.Writer.Complete();
    }

    #endregion

    #region Background Task

    private async Task ConsumePriorityWriteTaskLoop()
    {
        while (!_disposed && !IsShuttingDown && !_highTaskChannel.Reader.Completion.IsCompleted)
        {
            SvnSyncTask? task = null;
            if (_highTaskChannel.Reader.TryRead(out var h))
                task = h;
            else if (_lowTaskChannel.Reader.TryRead(out var l))
                task = l;
            else
            {
                await Task.Delay(150);
                continue;
            }

            lock (_svnWriteOperateLock) _isWriteTaskRunning = true;
            try
            {
                await ExecuteSingleWriteTaskAsync(task);
            }
            catch
            {
                await SvnCmdHelper.CleanUpAsync(LocalSyncRoot, UserName, Password, this);
            }
            finally
            {
                lock (_svnWriteOperateLock) _isWriteTaskRunning = false;
            }
        }
    }

    #endregion

    #region File Debounce

    public void DebounceFileChanged(string fullFilePath)
    {
        if (IsShuttingDown || _disposed) return;
        string lower = fullFilePath.ToLower();
        if (lower.EndsWith(".tmp") || lower.Contains("~$") || lower.EndsWith(".crdownload")) return;

        if (_fileDebounceDict.TryRemove(fullFilePath, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        var cts = new CancellationTokenSource();
        _fileDebounceDict[fullFilePath] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(FileStableDelayMs, cts.Token);
                if (!cts.Token.IsCancellationRequested && !IsShuttingDown)
                    AddFileToPending(fullFilePath);
            }
            catch { }
            finally
            {
                _fileDebounceDict.TryRemove(fullFilePath, out _);
                cts.Dispose();
            }
        }, cts.Token);
    }

    #endregion

    #region Enqueue

    public void AddFileToPending(string filePath)
    {
        if (IsShuttingDown || _disposed) return;
        if (!File.Exists(filePath)) return;
        _pendingCommitFiles.TryAdd(filePath, 0);
        EnqueueLocalAdd(filePath);
        if (_pendingCommitFiles.Count >= BatchCommitFileCount) EnqueueBatchCommit();
    }

    public void EnqueueLocalAdd(string filePath)
    {
        if (IsShuttingDown) return;
        _ = _highTaskChannel.Writer.WriteAsync(new SvnSyncTask { OpType = SvnOperateType.AddFile, TargetPath = filePath });
    }

    public void EnqueueDeleteFile(string filePath)
    {
        if (IsShuttingDown) return;
        _ = _highTaskChannel.Writer.WriteAsync(new SvnSyncTask { OpType = SvnOperateType.DeleteFile, TargetPath = filePath });
    }

    public void EnqueueBatchCommit()
    {
        if (IsShuttingDown) return;
        _ = _lowTaskChannel.Writer.WriteAsync(new SvnSyncTask { OpType = SvnOperateType.BatchCommit, TargetPath = LocalSyncRoot });
    }

    public void EnqueueBatchUpdate()
    {
        if (IsShuttingDown) return;
        _ = _lowTaskChannel.Writer.WriteAsync(new SvnSyncTask { OpType = SvnOperateType.BatchUpdate, TargetPath = LocalSyncRoot });
    }

    public bool IsWriteTaskBusy()
    {
        lock (_svnWriteOperateLock) return _isWriteTaskRunning;
    }

    #endregion

    #region Task Execution

    private async Task ExecuteSingleWriteTaskAsync(SvnSyncTask task)
    {
        switch (task.OpType)
        {
            case SvnOperateType.AddFile:
                await SvnCmdHelper.SafeAddFileAsync(task.TargetPath, UserName, Password, this);
                break;
            case SvnOperateType.DeleteFile:
                await SvnCmdHelper.SafeDeleteFileAsync(task.TargetPath, UserName, Password, this);
                break;
            case SvnOperateType.BatchCommit:
                await ExecuteBatchCommitAsync();
                break;
            case SvnOperateType.BatchUpdate:
                await ExecuteIncrementalBatchUpdateAsync(task.TargetPath);
                break;
        }
    }

    private async Task ExecuteBatchCommitAsync()
    {
        if (_pendingCommitFiles.IsEmpty) return;
        var batch = _pendingCommitFiles.Keys.Take(BatchCommitFileCount).ToList();
        foreach (var f in batch) _pendingCommitFiles.TryRemove(f, out _);

        var dirs = batch.Select(Path.GetDirectoryName).Distinct()!.Where(d => !string.IsNullOrEmpty(d)).ToList();
        int idx = 0;
        foreach (var d in dirs)
        {
            BatchCommitProgress?.Invoke(new SvnBatchOperateProgress
            {
                CurrentPath = d!, CompletedCount = idx, TotalCount = dirs.Count, TipText = "提交中"
            });
            await SvnCmdHelper.CommitDirAsync(d!, DateTime.Now.ToString("yyyy-MM-dd HH:mm"), UserName, Password, this);
            idx++;
            await Task.Delay(BatchIntervalMs);
        }

        BatchCommitProgress?.Invoke(new SvnBatchOperateProgress
        {
            CompletedCount = dirs.Count, TotalCount = dirs.Count, TipText = "批量提交完成", IsFinished = true
        });
    }

    private async Task ExecuteIncrementalBatchUpdateAsync(string rootPath)
    {
        var diff = GetFileDifferenceList();
        if (!diff.Any())
        {
            BatchUpdateProgress?.Invoke(new SvnBatchOperateProgress { TipText = "无更新", IsFinished = true });
            return;
        }

        var dirs = diff.Select(x => Path.GetDirectoryName(x.FullLocalPath)).Distinct()!.Where(d => !string.IsNullOrEmpty(d)).ToList();
        int idx = 0;
        foreach (var d in dirs)
        {
            BatchUpdateProgress?.Invoke(new SvnBatchOperateProgress
            {
                CurrentPath = d!, CompletedCount = idx, TotalCount = dirs.Count
            });
            await SvnCmdHelper.UpdateDirAsync(d!, UserName, Password, this);
            idx++;
        }

        BatchUpdateProgress?.Invoke(new SvnBatchOperateProgress
        {
            CompletedCount = dirs.Count, TotalCount = dirs.Count, TipText = "更新完成", IsFinished = true
        });
    }

    #endregion

    #region Core: Directory File SVN State (UI calls this)

    public async Task<List<SvnFileItemState>> GetDirectoryFileSvnStateAsync(string dirPath)
    {
        var result = new List<SvnFileItemState>();
        if (!Directory.Exists(dirPath)) return result;

        string xmlStatus = await GetDirStatusXml(dirPath);
        var localFiles = ScanLocalDirectoryItems(dirPath);

        if (string.IsNullOrEmpty(xmlStatus)) return localFiles;

        var doc = XDocument.Parse(xmlStatus);
        var ns = doc.Root?.Name.Namespace;
        var entries = doc.Descendants(ns + "entry").ToList();

        foreach (var item in localFiles)
        {
            var svnNode = entries.FirstOrDefault(e =>
            {
                string p = e.Attribute("path")?.Value ?? "";
                string full = Path.Combine(dirPath, p);
                return full.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase);
            });

            if (svnNode == null)
            {
                item.IsVersionControl = false;
                item.StatusCode = "unversioned";
                item.StatusText = "未版本控制";
                item.State = SvnItemState.Unversioned;
                result.Add(item);
                continue;
            }

            item.IsVersionControl = true;
            string state = svnNode.Element(ns + "status")?.Attribute("item")?.Value ?? "";

            item.StatusCode = state;
            switch (state.ToLower())
            {
                case "added":
                    item.StatusText = "新增待提交";
                    item.State = SvnItemState.Added;
                    break;
                case "modified":
                    item.StatusText = "已修改";
                    item.State = SvnItemState.Modified;
                    break;
                case "deleted":
                    item.StatusText = "已删除";
                    item.State = SvnItemState.Deleted;
                    break;
                case "conflicted":
                    item.StatusText = "文件冲突";
                    item.State = SvnItemState.Conflicted;
                    break;
                default:
                    item.StatusText = "正常";
                    item.State = SvnItemState.Normal;
                    break;
            }

            result.Add(item);
        }

        return result;
    }

    private async Task<string> GetDirStatusXml(string dir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = $"status --depth immediates --xml --username {UserName} --password-from-stdin --no-auth-cache --non-interactive",
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var p = Process.Start(psi);
            if (p == null) return "";
            await p.StandardInput.WriteLineAsync(Password);
            await p.StandardInput.FlushAsync();
            using var cts = new CancellationTokenSource(4000);
            await p.WaitForExitAsync(cts.Token);
            return await p.StandardOutput.ReadToEndAsync();
        }
        catch { return ""; }
    }

    private List<SvnFileItemState> ScanLocalDirectoryItems(string dir)
    {
        var list = new List<SvnFileItemState>();
        foreach (var d in Directory.GetDirectories(dir))
        {
            var info = new DirectoryInfo(d);
            list.Add(new SvnFileItemState
            {
                FullPath = d,
                Name = info.Name,
                IsFolder = true,
                FileSize = 0,
                ModifyTime = info.LastWriteTime
            });
        }
        foreach (var f in Directory.GetFiles(dir))
        {
            var info = new FileInfo(f);
            list.Add(new SvnFileItemState
            {
                FullPath = f,
                Name = info.Name,
                IsFolder = false,
                FileSize = info.Length,
                ModifyTime = info.LastWriteTime
            });
        }
        return list;
    }

    #endregion

    #region Utilities

    public List<SvnFileDiffInfo> GetFileDifferenceList()
    {
        var xml = SvnCmdHelper.QueryRemoteListXml(RemoteSvnUrl, UserName, Password);
        var remote = SvnCmdHelper.ParseRemoteEntries(xml);
        var list = new List<SvnFileDiffInfo>();

        foreach (var (path, rev) in remote)
        {
            string local = Path.Combine(LocalSyncRoot, path);
            list.Add(new SvnFileDiffInfo
            {
                FullLocalPath = local,
                RelativeRemotePath = path,
                RemoteRevision = rev,
                IsLocalMissing = !File.Exists(local),
                LocalRevision = File.Exists(local) ? SvnCmdHelper.QueryLocalFileRev(local, UserName, Password) : 0
            });
        }
        return list;
    }

    public async Task CleanLocalSvnLockAsync()
    {
        await SvnCmdHelper.CleanUpAsync(LocalSyncRoot, UserName, Password, this);
    }

    #endregion

    #region Graceful Shutdown

    public async Task ShutdownAndWaitFinishAsync()
    {
        if (IsShuttingDown || _disposed) return;
        IsShuttingDown = true;
        LogReceived?.Invoke(SvnLogLevel.Info, "程序退出，开始收尾同步任务");
        _autoUpdateTimer?.Dispose();
        _highTaskChannel.Writer.Complete();
        _lowTaskChannel.Writer.Complete();

        if (!_pendingCommitFiles.IsEmpty) await ExecuteBatchCommitAsync();
        await WaitCurrentOperateIdleAsync();

        foreach (var cts in _fileDebounceDict.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _fileDebounceDict.Clear();
        IsDisposedComplete = true;
    }

    private async Task WaitCurrentOperateIdleAsync()
    {
        int count = 0;
        int max = ShutdownWaitTimeoutMs / 200;
        while (IsWriteTaskBusy() && count < max)
        {
            await Task.Delay(200);
            count++;
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            StopSyncService();
            foreach (var cts in _fileDebounceDict.Values) cts.Dispose();
        }
    }

    #endregion
}
