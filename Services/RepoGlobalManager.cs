#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneFileBox.Models;

namespace OneFileBox.Services;

/// <summary>
/// Global multi-repo manager.
///
/// Responsibilities:
///   - Hold all RepoManager instances
///   - Create repo managers (from CheckoutWindow / AddLocalRepoWindow results)
///   - Switch between repos (Focus / Dismiss)
///   - Remove repos
///   - Forward events from RepoManager → MainWindowViewModel
/// </summary>
public class RepoGlobalManager : IDisposable
{
    private readonly List<RepoManager> _managers = new();
    private RepoManager? _activeManager;
    private bool _isDisposed;

    public IReadOnlyList<RepoManager> Managers => _managers.AsReadOnly();
    public RepoManager? ActiveManager => _activeManager;
    public bool IsEmpty => _managers.Count == 0;

    // Events forwarded from RepoManager → MainWindowViewModel
    public event EventHandler? FilesChanged;
    public event EventHandler<string>? SyncNotification;
    public event EventHandler<List<ConflictedFileInfo>>? ConflictDetected;
    public event EventHandler? SyncStatusChanged;

    public RepoGlobalManager()
    {
        SvnCliLog.Information("[RepoGlobalManager] Created");
    }

    /// <summary>
    /// Create a new RepoManager for an already-existing local working copy.
    /// Does not switch to it — caller decides when to SwitchToAsync.
    /// </summary>
    public RepoManager CreateLocal(Repository repo)
    {
        var manager = new RepoManager(repo);
        _managers.Add(manager);
        SvnCliLog.Information("[RepoGlobalManager] Created local manager for {Name}", repo.Name);
        return manager;
    }

    /// <summary>
    /// Switch to the given RepoManager:
    ///   1. Dismiss current active RepoManager (drain queue)
    ///   2. Focus newManager (start SyncService, bind events)
    /// </summary>
    public async Task SwitchToAsync(RepoManager newManager)
    {
        if (_isDisposed) return;
        if (!_managers.Contains(newManager)) return;

        // 1. Dismiss current
        if (_activeManager != null && _activeManager != newManager)
        {
            var previous = _activeManager;
            _activeManager = null;
            UnbindManagerEvents(previous);
            await previous.DismissAsync();
        }

        // 2. Focus new
        _activeManager = newManager;
        newManager.Focus();
        BindManagerEvents(newManager);

        SvnCliLog.Information("[RepoGlobalManager] Switched to {Name}", newManager.Repository.Name);
    }

    /// <summary>
    /// Restore all repos from ConfigService (local working copies only).
    /// Does NOT switch to any repo — user selects which to activate.
    /// </summary>
    public void RestoreFromConfig(IEnumerable<Repository> repositories)
    {
        foreach (var repo in repositories)
        {
            var manager = new RepoManager(repo);
            _managers.Add(manager);
            SvnCliLog.Information("[RepoGlobalManager] Restored repo from config: {Name}", repo.Name);
        }
    }

    /// <summary>
    /// Restore repos and switch to last active (or first if none specified).
    /// </summary>
    public async Task RestoreAndSwitchToLastActiveAsync(string? lastActiveName)
    {
        if (string.IsNullOrEmpty(lastActiveName))
        {
            if (_managers.Count > 0)
                await SwitchToAsync(_managers[0]);
            return;
        }

        var last = _managers.FirstOrDefault(m => m.Repository.Name == lastActiveName)
                   ?? _managers.FirstOrDefault();
        if (last != null)
            await SwitchToAsync(last);
    }

    /// <summary>
    /// Remove a RepoManager:
    ///   1. If it's active, dismiss + unbind
    ///   2. Remove from list
    ///   3. Switch to first remaining (if any)
    /// </summary>
    public async Task RemoveAsync(RepoManager manager)
    {
        if (!_managers.Contains(manager)) return;
        SvnCliLog.Information("[RepoGlobalManager] Removing {Name}", manager.Repository.Name);

        if (_activeManager == manager)
        {
            _activeManager = null;
            UnbindManagerEvents(manager);
            await manager.DismissAsync();
        }

        _managers.Remove(manager);
        manager.Dispose();

        if (_managers.Count > 0)
            await SwitchToAsync(_managers[0]);
    }

    private void BindManagerEvents(RepoManager manager)
    {
        manager.FilesChanged += (s, _) => FilesChanged?.Invoke(this, EventArgs.Empty);
        manager.SyncNotification += (s, msg) => SyncNotification?.Invoke(this, msg);
        manager.ConflictDetected += (s, conflicts) =>
        {
            var info = new List<ConflictedFileInfo>(conflicts);
            ConflictDetected?.Invoke(this, info);
        };
    }

    private void UnbindManagerEvents(RepoManager manager)
    {
        manager.FilesChanged -= (s, _) => FilesChanged?.Invoke(this, EventArgs.Empty);
        manager.SyncNotification -= (s, msg) => SyncNotification?.Invoke(this, msg);
        manager.ConflictDetected -= (s, conflicts) => ConflictDetected?.Invoke(this, new List<ConflictedFileInfo>(conflicts));
    }

    public void ShutdownAll()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var manager in _managers)
        {
            UnbindManagerEvents(manager);
            manager.Shutdown();
        }
        _managers.Clear();
        _activeManager = null;
        SvnCliLog.Information("[RepoGlobalManager] ShutdownAll complete");
    }

    public async Task SyncNowAsync() => await (_activeManager?.SyncNowAsync() ?? Task.CompletedTask);

    public void Dispose()
    {
        ShutdownAll();
        GC.SuppressFinalize(this);
    }
}