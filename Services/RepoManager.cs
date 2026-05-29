#nullable enable
using System;
using System.Threading.Tasks;
using OneFileBox.Models;

namespace OneFileBox.Services;

public enum RepoState { None, Focused, Dismissed }

public class RepoManager : IDisposable
{
    private readonly object _lock = new();
    private bool _isDisposed;

    public Repository Repository { get; }
    public SvnCliService SvnService { get; }
    public FileWatcherService FileWatcher { get; }
    public RepoState State { get; private set; } = RepoState.None;

    public event EventHandler? FilesChanged;
    public event EventHandler<string>? SyncNotification;
    public event EventHandler<string>? CredentialExpired;

    public RepoManager(Repository repository)
    {
        Repository = repository;
        SvnService = new SvnCliService();
        FileWatcher = new FileWatcherService();
        SvnCliLog.Information("[RepoManager] Created for {Name} at {Path}", repository.Name, repository.Path);
    }

    public void Focus()
    {
        lock (_lock)
        {
            if (_isDisposed || State == RepoState.Focused) return;
            SvnCliLog.Information("[RepoManager] Focusing {Name}", Repository.Name);

            FileWatcher.FilesChanged += OnFileChanged;
            FileWatcher.StartWatching(Repository.Path);
            State = RepoState.Focused;
        }
    }

    public async Task DismissAsync()
    {
        lock (_lock)
        {
            if (State == RepoState.Dismissed || State == RepoState.None) return;
            State = RepoState.Dismissed;
        }
        FileWatcher.StopWatching();
        FileWatcher.FilesChanged -= OnFileChanged;
        SvnCliLog.Information("[RepoManager] Dismissed {Name}", Repository.Name);
        await Task.CompletedTask;
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            SvnCliLog.Information("[RepoManager] Shutting down {Name}", Repository.Name);
        }
        FileWatcher.StopWatching();
        lock (_lock) { State = RepoState.None; _isDisposed = true; }
    }

    private void OnFileChanged(object? sender, string[] files)
    {
        // File changed → trigger sync
        SyncNotification?.Invoke(this, $"File change detected: {files.Length} file(s)");
        FilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Shutdown();
        FileWatcher.Dispose();
        GC.SuppressFinalize(this);
    }
}