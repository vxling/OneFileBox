using System.IO;

namespace OneFileBox_new.Services;

public class FolderFileWatcher
{
    private FileSystemWatcher? _watcher;
    public static FolderFileWatcher Instance { get; } = new();

    private FolderFileWatcher() { }

    /// <summary>切换监听根目录（切仓库时自动调用）</summary>
    public void SetWatchPath(string rootFolder)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (!Directory.Exists(rootFolder)) return;

        _watcher = new FileSystemWatcher
        {
            Path = rootFolder,
            IncludeSubdirectories = true,
            Filter = "*.*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 65536
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;

        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        string lower = e.FullPath.ToLower();
        if (lower.EndsWith(".tmp") || lower.Contains("~$") || lower.EndsWith(".crdownload")) return;
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.DebounceFileChanged(e.FullPath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        string lower = e.FullPath.ToLower();
        if (lower.EndsWith(".tmp") || lower.Contains("~$")) return;
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.DebounceFileChanged(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.EnqueueDeleteFile(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.EnqueueDeleteFile(e.OldFullPath);
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.DebounceFileChanged(e.FullPath);
    }

    public void StopWatch()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
