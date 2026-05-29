#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneFileBox.Models;

namespace OneFileBox.Services;

public enum RepoState { None, Focused, Dismissed }

public class RepoManager : IDisposable
{
    private readonly object _lock = new();
    private bool _isDisposed;
    private SyncService? _syncService;

    public Repository Repository { get; }
    public SvnCliService SvnService { get; }
    public FileWatcherService FileWatcher { get; }
    public SyncService SyncService => _syncService!;
    public RepoState State { get; private set; } = RepoState.None;

    public event EventHandler? FilesChanged;
    public event EventHandler<string>? SyncNotification;
    public event EventHandler<string>? CredentialExpired;
public event EventHandler<List<ConflictedFileInfo>>? ConflictDetected;

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

            // Create and start SyncService for this repo
            _syncService = new SyncService(SvnService, FileWatcher);
            _syncService.SetRepository(Repository);

            // Bridge SyncService events → RepoManager events
            _syncService.FilesChanged += (s, _) =>
            {
                FilesChanged?.Invoke(this, EventArgs.Empty);
            };
            _syncService.SyncNotification += (s, msg) =>
            {
                SyncNotification?.Invoke(this, msg);
            };
            _syncService.ConflictDetected += (s, conflicts) =>
            {
                ConflictDetected?.Invoke(this, conflicts);
            };

            // Start FileWatcher
            FileWatcher.FilesChanged += OnFileChanged;
            FileWatcher.StartWatching(Repository.Path);

            // Start sync engine
            _syncService.StartSync(Repository);

            State = RepoState.Focused;
        }
    }

    public async Task DismissAsync()
    {
        lock (_lock)
        {
            if (_isDisposed || State == RepoState.Dismissed || State == RepoState.None) return;
            SvnCliLog.Information("[RepoManager] Dismissing {Name}", Repository.Name);
            State = RepoState.Dismissed;
        }

        // Stop sync but keep FileWatcher alive
        _syncService?.StopSync();
        await (_syncService?.DrainAsync() ?? Task.CompletedTask);

        FileWatcher.StopWatching();
        FileWatcher.FilesChanged -= OnFileChanged;

        SvnCliLog.Information("[RepoManager] Dismissed {Name}", Repository.Name);
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            SvnCliLog.Information("[RepoManager] Shutting down {Name}", Repository.Name);
        }
        _syncService?.Cancel();
        _syncService?.Dispose();
        _syncService = null;
        FileWatcher.StopWatching();
        lock (_lock) { State = RepoState.None; _isDisposed = true; }
    }

    private void OnFileChanged(object? sender, string[] files)
    {
        if (State != RepoState.Focused) return;
        SyncNotification?.Invoke(this, $"本地变更: {files.Length} 个文件");
        FilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateCredential(string username, string password)
    {
        Repository.Username = username;
        Repository.Password = password;
        SvnCliLog.Information("[RepoManager] Credential updated for {Name}", Repository.Name);
    }

    public async Task SyncNowAsync() => await (_syncService?.SyncNowAsync() ?? Task.CompletedTask);

    public void Dispose()
    {
        Shutdown();
        FileWatcher.Dispose();
        GC.SuppressFinalize(this);
    }
}