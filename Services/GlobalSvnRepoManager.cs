using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneFileBox_new.Models;

namespace OneFileBox_new.Services;

public class GlobalSvnRepoManager
{
    public static GlobalSvnRepoManager Instance { get; } = new();

    private readonly Dictionary<string, SvnSyncManager> _repoDict = new();

    public SvnSyncManager? CurrentActiveRepo { get; private set; }
    public string CurrentRepoKey { get; private set; } = string.Empty;

    /// <summary>仓库切换完成后通知（UI刷新用）</summary>
    public event Action<string>? RepoSwitched;

    private GlobalSvnRepoManager() { }

    /// <summary>注册仓库配置，不启动</summary>
    public SvnSyncManager RegisterRepo(RepoConfig config)
    {
        if (_repoDict.TryGetValue(config.Key, out var existing)) return existing;
        var mgr = new SvnSyncManager(config.LocalPath, config.SvnUrl, config.UserName, config.Password);
        _repoDict[config.Key] = mgr;
        return mgr;
    }

    /// <summary>切换仓库：旧仓库优雅停止，新仓库启动</summary>
    public async Task<bool> SwitchActiveRepoAsync(string repoKey)
    {
        if (CurrentRepoKey == repoKey && CurrentActiveRepo != null) return true;

        if (CurrentActiveRepo != null)
        {
            await CurrentActiveRepo.ShutdownAndWaitFinishAsync();
            CurrentActiveRepo.StopSyncService();
        }

        if (!_repoDict.TryGetValue(repoKey, out var newRepo)) return false;

        CurrentActiveRepo = newRepo;
        CurrentRepoKey = repoKey;
        newRepo.StartSyncService();
        RepoSwitched?.Invoke(repoKey);
        return true;
    }

    /// <summary>UI调用：查询当前仓库目录文件状态</summary>
    public async Task<List<SvnFileItemState>> GetCurrentRepoDirectoryFiles(string folderPath)
    {
        if (CurrentActiveRepo == null) return new();
        return await CurrentActiveRepo.GetDirectoryFileSvnStateAsync(folderPath);
    }

    /// <summary>程序全部退出</summary>
    public async Task ShutdownAllAsync()
    {
        if (CurrentActiveRepo != null)
        {
            await CurrentActiveRepo.ShutdownAndWaitFinishAsync();
            CurrentActiveRepo.Dispose();
        }
        foreach (var repo in _repoDict.Values)
        {
            repo.StopSyncService();
            repo.Dispose();
        }
        _repoDict.Clear();
        CurrentActiveRepo = null;
        CurrentRepoKey = string.Empty;
    }

    public SvnSyncManager? GetRepo(string key) =>
        _repoDict.TryGetValue(key, out var r) ? r : null;

    public IEnumerable<string> GetAllRepoKeys() => _repoDict.Keys;
}
