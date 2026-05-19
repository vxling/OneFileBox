using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneFileBox_new.Views;
using OneFileBox_new.Models;
using OneFileBox_new.Services;

namespace OneFileBox_new.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<RepositoryItemViewModel> _repositories = [];

    [ObservableProperty]
    private RepositoryItemViewModel? _selectedRepository;

    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = [];

    [ObservableProperty]
    private FileItemViewModel? _selectedFile;

    [ObservableProperty]
    private string _currentPath = "No repository selected";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _itemCountText = "";

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 由 MainWindow 在构造后注入，避免 ViewModel 直接引用 View。
    /// </summary>
    public TopLevel? HostTopLevel { get; set; }

    public MainWindowViewModel()
    {
        GlobalSvnRepoManager.Instance.RepoSwitched += OnRepoSwitched;
    }

    /// <summary>
    /// 启动时调用：加载配置，注册所有仓库。
    /// </summary>
    public async Task InitializeAsync()
    {
        var repos = await ConfigService.Instance.LoadRepositoriesAsync();
        foreach (var cfg in repos)
        {
            var vm = new RepositoryItemViewModel
            {
                Key = cfg.Key,
                Name = cfg.Name,
                Path = cfg.LocalPath,
                SvnUrl = cfg.SvnUrl,
                UserName = cfg.UserName,
                Password = cfg.Password
            };
            Repositories.Add(vm);
            GlobalSvnRepoManager.Instance.RegisterRepo(cfg);
        }

        // 恢复上次激活的仓库
        var lastKey = ConfigService.Instance.Config.LastActiveRepoKey;
        if (!string.IsNullOrEmpty(lastKey))
        {
            var last = Repositories.FirstOrDefault(r => r.Key == lastKey);
            if (last != null) SelectedRepository = last;
        }

        if (Repositories.Count == 0)
            StatusText = "请添加本地仓库";
    }

    #region Repo Management

    [RelayCommand]
    private async Task AddLocalRepo()
    {
        if (HostTopLevel == null) return;

        var existing = Repositories.Select(r => new RepoConfig
        {
            Key = r.Key,
            LocalPath = r.Path
        }).ToList();

        var dialog = new AddLocalRepoWindow(existing, HostTopLevel);
        var win = HostTopLevel as Window;
        var result = win != null ? await dialog.ShowDialog<bool>(win) : false;

        if (!result || dialog.ResultRepo == null) return;

        var repo = dialog.ResultRepo;
        var vm = new RepositoryItemViewModel
        {
            Key = repo.Key,
            Name = repo.Name,
            Path = repo.LocalPath,
            SvnUrl = repo.SvnUrl,
            UserName = repo.UserName,
            Password = repo.Password
        };

        Repositories.Add(vm);
        GlobalSvnRepoManager.Instance.RegisterRepo(repo);
        await ConfigService.Instance.AddOrUpdateRepoAsync(repo);

        SelectedRepository = vm;
        StatusText = $"已添加仓库：{repo.Name}";
    }

    [RelayCommand]
    private async Task Checkout()
    {
        StatusText = "Checkout from network...";
        await Task.Delay(100);
    }

    [RelayCommand]
    private void Settings()
    {
        StatusText = "Settings...";
    }

    [RelayCommand]
    private async Task RemoveRepo(RepositoryItemViewModel? repo)
    {
        if (repo == null) return;
        Repositories.Remove(repo);
        await ConfigService.Instance.RemoveRepoAsync(repo.Key);
        if (SelectedRepository == repo)
        {
            SelectedRepository = Repositories.Count > 0 ? Repositories[0] : null;
        }
    }

    partial void OnSelectedRepositoryChanged(RepositoryItemViewModel? value)
    {
        if (value == null) return;
        _ = SwitchToRepoAsync(value);
    }

    private async Task SwitchToRepoAsync(RepositoryItemViewModel repoVm)
    {
        IsLoading = true;
        StatusText = $"切换到：{repoVm.Name}...";
        CurrentPath = repoVm.Path;

        // 注册（或更新）仓库配置
        var config = new RepoConfig
        {
            Key = repoVm.Key,
            Name = repoVm.Name,
            LocalPath = repoVm.Path,
            SvnUrl = repoVm.SvnUrl,
            UserName = repoVm.UserName,
            Password = repoVm.Password
        };

        GlobalSvnRepoManager.Instance.RegisterRepo(config);
        await GlobalSvnRepoManager.Instance.SwitchActiveRepoAsync(repoVm.Key);
        await ConfigService.Instance.SetLastActiveRepoAsync(repoVm.Key);

        // 切换文件监听
        FolderFileWatcher.Instance.SetWatchPath(repoVm.Path);

        // 刷新文件列表
        await RefreshCurrentDirectoryFileList();
        IsLoading = false;
    }

    private void OnRepoSwitched(string repoKey)
    {
        StatusText = $"已切换到仓库：{repoKey}";
    }

    #endregion

    #region File List Refresh

    [RelayCommand]
    private async Task Refresh()
    {
        if (SelectedRepository == null) return;
        await RefreshCurrentDirectoryFileList();
    }

    private async Task RefreshCurrentDirectoryFileList()
    {
        if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath))
        {
            Files.Clear();
            ItemCountText = "";
            return;
        }

        IsLoading = true;
        StatusText = "正在加载文件列表...";

        try
        {
            var svnStates = await GlobalSvnRepoManager.Instance.GetCurrentRepoDirectoryFiles(CurrentPath);

            Files.Clear();
            foreach (var state in svnStates)
            {
                var vm = new FileItemViewModel();
                vm.SyncFrom(state);
                Files.Add(vm);
            }

            ItemCountText = $"{Files.Count} items";
            StatusText = SelectedRepository != null ? $"当前仓库：{SelectedRepository.Name}" : "Ready";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region File Operations

    [RelayCommand]
    private void Commit()
    {
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.EnqueueBatchCommit();
        StatusText = "提交已加入队列";
    }

    [RelayCommand]
    private void Update()
    {
        GlobalSvnRepoManager.Instance.CurrentActiveRepo?.EnqueueBatchUpdate();
        StatusText = "更新已加入队列";
    }

    [RelayCommand]
    private async Task NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Directory.GetParent(CurrentPath);
        if (parent == null) return;
        CurrentPath = parent.FullName;
        await RefreshCurrentDirectoryFileList();
    }

    [RelayCommand]
    private async Task NavigateInto(FileItemViewModel? item)
    {
        if (item == null || !item.IsDirectory) return;
        CurrentPath = item.FullPath;
        await RefreshCurrentDirectoryFileList();
    }

    #endregion

    #region Drag & Drop (programmatic file copy)

    public async Task HandleDroppedFiles(string[] paths)
    {
        if (SelectedRepository == null)
        {
            StatusText = "请先选择一个仓库";
            return;
        }

        var targetRoot = SelectedRepository.Path;
        if (string.IsNullOrEmpty(targetRoot) || !Directory.Exists(targetRoot))
        {
            StatusText = "仓库目录无效";
            return;
        }

        IsLoading = true;
        StatusText = "正在导入文件...";

        try
        {
            foreach (var sourcePath in paths)
            {
                if (Directory.Exists(sourcePath))
                {
                    string destDir = Path.Combine(targetRoot, Path.GetFileName(sourcePath));
                    CopyDirectoryRecursive(sourcePath, destDir);
                }
                else if (File.Exists(sourcePath))
                {
                    string destFile = Path.Combine(targetRoot, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destFile, true);
                    GlobalSvnRepoManager.Instance.CurrentActiveRepo?.DebounceFileChanged(destFile);
                }
            }

            await RefreshCurrentDirectoryFileList();
            StatusText = "导入完成";
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
            GlobalSvnRepoManager.Instance.CurrentActiveRepo?.DebounceFileChanged(destFile);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            string subTarget = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, subTarget);
        }
    }

    #endregion

    public async Task ShutdownAsync()
    {
        FolderFileWatcher.Instance.StopWatch();
        await GlobalSvnRepoManager.Instance.ShutdownAllAsync();
    }
}
