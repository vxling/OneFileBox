#nullable enable
using System.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.Services;

/// <summary>
/// Bidirectional sync engine: local changes → SVN server (upload)
/// and SVN server → local working copy (download).
///
/// Uses SvnCliService directly (no executor layer).
/// FileWatcher detects local changes → enqueues operations.
/// Poll timer checks server revisions → pulls updates.
/// </summary>
public class SyncService : IDisposable
{
    private readonly SvnCliService _svnService;
    private readonly FileWatcherService _fileWatcher;
    private readonly System.Timers.Timer _pollTimer;
    private readonly System.Timers.Timer _fullSyncTimer;
    private Repository? _repository;

    private int _isPolling;
    private int _isSyncing;
    private int _staleCounter;

    public event EventHandler? FilesChanged;
    public event EventHandler<string>? SyncNotification;
    public event EventHandler<List<ConflictedFileInfo>>? ConflictDetected;

    public SyncService(SvnCliService svnService, FileWatcherService fileWatcher)
    {
        _svnService = svnService;
        _fileWatcher = fileWatcher;
        _fileWatcher.FilesChanged += OnFilesChanged;

        _pollTimer = new System.Timers.Timer(60_000);
        _pollTimer.Elapsed += OnPollTimerElapsed;
        _pollTimer.AutoReset = true;

        _fullSyncTimer = new System.Timers.Timer(15 * 60 * 1000);
        _fullSyncTimer.Elapsed += OnFullSyncTimerElapsed;
        _fullSyncTimer.AutoReset = true;

        SvnCliLog.Information("[SyncService] Created");
    }

    public void SetRepository(Repository repo) => _repository = repo;

    public void StartSync(Repository repo)
    {
        _repository = repo;
        _pollTimer.Start();
        _fullSyncTimer.Start();
        SvnCliLog.Information("[SyncService] Started for {Name}", repo.Name);
        _ = ScanAndCommitAsync();
    }

    public void StopSync()
    {
        _pollTimer.Stop();
        _fullSyncTimer.Stop();
        SvnCliLog.Information("[SyncService] Stopped");
    }

    public async Task DrainAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (_isSyncing != 0 || _isPolling != 0)
        {
            if (DateTime.UtcNow > deadline) break;
            await Task.Delay(200);
        }
    }

    public void Cancel()
    {
        StopSync();
        SvnCliLog.Information("[SyncService] Cancel requested");
    }

    public async Task SyncNowAsync() => await ScanAndCommitAsync();

    public void DisableFileWatcher() => _fileWatcher.Disable();
    public void ReEnableFileWatcher() => _fileWatcher.Enable();

    private void OnFilesChanged(object? sender, string[] files)
    {
        if (files.Length == 0) return;
        SyncNotification?.Invoke(this, $"本地变更: {files.Length} 个文件");
        _ = ScanAndCommitAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // ScanAndCommit: local changes → commit to server
    // ═══════════════════════════════════════════════════════════════

    private async Task ScanAndCommitAsync()
    {
        var repo = _repository;
        if (repo == null) return;
        if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) != 0) return;

        try
        {
            var statuses = await _svnService.GetStatusAsync(repo.Path, depth: true);
            if (statuses.Count == 0)
            {
                SvnCliLog.Debug("[ScanAndCommit] No changes to commit");
                return;
            }

            var unversioned = statuses.Where(kv => kv.Value == FileSvnStatus.Unversioned).ToList();
            var changed = statuses.Where(kv =>
                kv.Value != FileSvnStatus.Conflicted && kv.Value != FileSvnStatus.Unversioned).ToList();

            // Add unversioned files
            foreach (var (filePath, _) in unversioned)
            {
                if (!IsTempFile(filePath))
                    await _svnService.AddPathAsync(filePath);
            }

            // Group by directory for commit
            var normalizedRepoPath = repo.Path.Replace('\\', '/').TrimEnd('/');
            var dirGroups = changed
                .Select(kv =>
                {
                    var key = kv.Key.Replace('\\', '/');
                    var lastSeg = Path.GetFileName(key);
                    var parent = lastSeg.Contains('.') ? Path.GetDirectoryName(key)?.Replace('\\', '/') : key;
                    return (key: parent ?? normalizedRepoPath, kv);
                })
                .GroupBy(x => x.key)
                .OrderByDescending(g => g.Key.Split('/').Length)
                .ToList();

            SvnCliLog.Information("[ScanAndCommit] Committing {DirCount} dirs, {FileCount} changed (+ {AddCount} unversioned)",
                dirGroups.Count, changed.Count, unversioned.Count);

            foreach (var group in dirGroups)
            {
                var dirPath = string.IsNullOrEmpty(group.Key) ? repo.Path : group.Key;
                var fileCount = group.Count();
                var message = fileCount == 1
                    ? $"Auto-sync: {Path.GetFileName(group.First().kv.Key)}"
                    : $"Auto-sync: {fileCount} files in {Path.GetFileName(dirPath)}";

                await _svnService.CommitAsync(dirPath, message);
            }

            SyncNotification?.Invoke(this, "批量同步完成");
            FilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "[ScanAndCommit] failed");
            SyncNotification?.Invoke(this, $"批量同步失败: {ex.Message}");
            SyncRecordService.Instance.AddRecord(_repository?.Name ?? "", "", "Commit", "Failed", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _isSyncing, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Poll: check server for updates (downward sync)
    // ═══════════════════════════════════════════════════════════════

    private async void OnPollTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try { await PollCoreAsync(); }
        catch (Exception ex) { SvnCliLog.Error(ex, "[PollTimer] Unhandled"); }
    }

    private async void OnFullSyncTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_repository == null) return;
        if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) != 0) return;
        try
        {
            SvnCliLog.Information("[FullSync] Starting full sync for {Name}", _repository.Name);
            await ScanAndCommitAsync();
            SyncNotification?.Invoke(this, "定时全量同步完成");
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "[FullSync] Full sync failed");
        }
        finally
        {
            Interlocked.Exchange(ref _isSyncing, 0);
        }
    }

    private async Task PollCoreAsync()
    {
        var repo = _repository;
        if (repo == null) return;

        if (Interlocked.CompareExchange(ref _isPolling, 1, 0) == 1) return;
        if (Interlocked.CompareExchange(ref _isSyncing, 0, 0) != 0)
        {
            Interlocked.Exchange(ref _isPolling, 0);
            return;
        }

        try
        {
            var localRev = await _svnService.GetWorkingCopyRevisionAsync(repo.Path);
            var serverRev = string.IsNullOrEmpty(repo.Url)
                ? -1
                : await _svnService.GetHeadRevisionAsync(repo.Url, repo.Username, repo.Password);

            SvnCliLog.Debug("[PollCore] Revisions: local={Local}, server={Server}", localRev, serverRev);

            _staleCounter++;
            if (_staleCounter > 35)
            {
                SvnCliLog.Information("[PollCore] Stale for {Count} polls, forcing full update", _staleCounter);
                var result = await _svnService.UpdateAsync(repo.Path);
                _staleCounter = 1;
                if (result)
                {
                    var conflicts = await BuildConflictInfoListAsync(repo.Path);
                    if (conflicts.Count > 0)
                        ConflictDetected?.Invoke(this, conflicts);
                    else
                        FilesChanged?.Invoke(this, EventArgs.Empty);
                }
                SyncNotification?.Invoke(this, result ? "强制全量更新完成" : "强制更新失败");
                return;
            }

            // Check for incomplete wc
            if (await HasIncompleteWorkingCopyAsync(repo.Path))
            {
                SvnCliLog.Warning("[PollCore] Incomplete working copy detected, repairing…");
                await _svnService.UpdateAsync(repo.Path);
                SyncNotification?.Invoke(this, "已修复 working copy 中的 incomplete 状态");
                var repairConflicts = await BuildConflictInfoListAsync(repo.Path);
                if (repairConflicts.Count > 0)
                    ConflictDetected?.Invoke(this, repairConflicts);
                FilesChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Server has newer revision → pull changes
            if (serverRev > localRev && localRev > 0)
            {
                _staleCounter = 1;
                SvnCliLog.Information("[PollCore] Server has updates: {Local} → {Server}", localRev, serverRev);

                var result = await _svnService.UpdateAsync(repo.Path);
                SyncNotification?.Invoke(this, $"已更新 r{localRev} → r{serverRev}");
                if (result)
                {
                    var normalConflicts = await BuildConflictInfoListAsync(repo.Path);
                    if (normalConflicts.Count > 0)
                        ConflictDetected?.Invoke(this, normalConflicts);
                    else
                        FilesChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            SvnCliLog.Debug("[PollCore] No action: revisions match, working copy clean");
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "[PollCore] Unexpected error");
            SyncNotification?.Invoke(this, $"轮询失败: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private async Task<List<ConflictedFileInfo>> BuildConflictInfoListAsync(string repoPath)
    {
        try
        {
            return await _svnService.GetConflictedFilesAsync(repoPath);
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "[SyncService] BuildConflictInfoListAsync failed");
            return new List<ConflictedFileInfo>();
        }
    }

    private async Task<bool> HasIncompleteWorkingCopyAsync(string repoPath)
    {
        var statuses = await _svnService.GetStatusAsync(repoPath, depth: true);
        return statuses.Values.Any(s => s == FileSvnStatus.Incomplete);
    }

    private static bool IsTempFile(string path)
    {
        var fileName = path.Replace('\\', '/').Split('/').Last();
        return fileName.StartsWith("~$") || fileName.StartsWith("~")
            || fileName.EndsWith(".tmp") || fileName.EndsWith(".temp")
            || fileName.Equals(".DS_Store");
    }

    public void Dispose()
    {
        StopSync();
        _fileWatcher.FilesChanged -= OnFilesChanged;
        _pollTimer.Dispose();
        _fullSyncTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}