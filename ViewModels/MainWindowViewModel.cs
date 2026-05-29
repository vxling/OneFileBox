#nullable enable
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly SvnCliService _svnService;

    [ObservableProperty]
    private ObservableCollection<RepositoryItemViewModel> _repositories = new();

    [ObservableProperty]
    private RepositoryItemViewModel? _selectedRepository;

    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private FileItemViewModel? _selectedFile;

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _itemCountText = "";

    [ObservableProperty]
    private bool _isLoading;

    public event Action<string>? ShowAddLocalRepoDialog;
    public event Action<string>? ShowCheckoutDialog;
    public event Action<string>? ShowSettingsDialog;
    public event Action<string>? ShowError;

    public MainWindowViewModel()
    {
        _configService = ConfigService.Instance;
        _svnService = new SvnCliService();
    }

    public async Task InitializeAsync()
    {
        await _configService.LoadAsync();
        LoadRepositoriesFromConfig();
        SvnCliLog.Information("MainWindowViewModel initialized with {0} repositories", _repositories.Count);
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private void LoadRepositoriesFromConfig()
    {
        Repositories.Clear();
        foreach (var repo in _configService.Config.Repositories)
        {
            Repositories.Add(new RepositoryItemViewModel
            {
                Name = repo.Name,
                Path = repo.Path,
                IsActive = repo.IsActive
            });
        }
        if (Repositories.Count > 0 && SelectedRepository == null)
            SelectedRepository = Repositories[0];
    }

    partial void OnSelectedRepositoryChanged(RepositoryItemViewModel? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.Path))
        {
            CurrentPath = value.Path;
            _ = RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        IsLoading = true;
        StatusText = "正在加载...";
        Files.Clear();

        try
        {
            var fileInfos = new DirectoryInfo(CurrentPath).GetFileSystemInfos();
            var parentDir = new FileItemViewModel
            {
                Name = "..",
                IsDirectory = true,
                IsParentDirectory = true,
                FullPath = Path.GetDirectoryName(CurrentPath) ?? ""
            };
            Files.Add(parentDir);

            var statuses = await _svnService.GetStatusAsync(CurrentPath, depth: false);

            foreach (var info in fileInfos.OrderByDescending(f => f is DirectoryInfo).ThenBy(f => f.Name))
            {
                var isDir = info is DirectoryInfo;
                var fullPath = info.FullName;
                var name = info.Name;

                statuses.TryGetValue(fullPath, out var svnStatus);
                statuses.TryGetValue(fullPath + Path.DirectorySeparatorChar.ToString(), out var svnStatusDir);

                var status = isDir ? svnStatusDir : svnStatus;

                var vm = new FileItemViewModel
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = isDir,
                    LastModified = info.LastWriteTime,
                    FileSize = isDir ? 0 : (info as FileInfo)?.Length ?? 0,
                    SvnStatus = status.ToString(),
                    StatusIcon = GetStatusIcon(status)
                };
                Files.Add(vm);
            }

            var count = Files.Count - 1;
            ItemCountText = $"{count} 项";
            StatusText = "就绪";
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "RefreshAsync failed for {Path}", CurrentPath);
            StatusText = "加载失败: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string GetStatusIcon(FileSvnStatus status) => status switch
    {
        FileSvnStatus.Modified => "✏️",
        FileSvnStatus.Added => "➕",
        FileSvnStatus.Deleted => "❌",
        FileSvnStatus.Conflicted => "⚠️",
        FileSvnStatus.TreeConflicted => "⚠️",
        FileSvnStatus.Unversioned => "❓",
        FileSvnStatus.Missing => "❓",
        FileSvnStatus.Replaced => "🔄",
        _ => ""
    };

    [RelayCommand]
    private async Task NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Path.GetDirectoryName(CurrentPath);
        if (string.IsNullOrEmpty(parent)) return;

        // Find repo root
        var repo = Repositories.FirstOrDefault(r => CurrentPath.StartsWith(r.Path));
        if (repo != null && parent.Length >= repo.Path.Length)
        {
            CurrentPath = parent;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task NavigateTo(FileItemViewModel item)
    {
        if (item.IsParentDirectory)
        {
            await NavigateUp();
            return;
        }

        if (item.IsDirectory)
        {
            CurrentPath = item.FullPath;
            await RefreshAsync();
            return;
        }
    }

    [RelayCommand]
    private void AddLocalRepo()
    {
        ShowAddLocalRepoDialog?.Invoke("");
    }

    [RelayCommand]
    private void Checkout()
    {
        ShowCheckoutDialog?.Invoke("");
    }

    [RelayCommand]
    private async Task Commit()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        IsLoading = true;
        StatusText = "正在提交...";

        try
        {
            var result = await _svnService.CommitAsync(CurrentPath, "OneFileBox commit");
            if (result)
            {
                StatusText = "提交成功";
                await RefreshAsync();
            }
            else
            {
                StatusText = "提交失败";
                ShowError?.Invoke("Commit failed — check repository status");
            }
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "Commit failed for {Path}", CurrentPath);
            StatusText = "提交失败: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Update()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        IsLoading = true;
        StatusText = "正在更新...";

        try
        {
            var result = await _svnService.UpdateAsync(CurrentPath);
            if (result)
            {
                StatusText = "更新成功";
                await RefreshAsync();
            }
            else
            {
                StatusText = "更新失败";
            }
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "Update failed for {Path}", CurrentPath);
            StatusText = "更新失败: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Settings()
    {
        ShowSettingsDialog?.Invoke("");
    }

    [RelayCommand]
    private async Task RemoveRepo(RepositoryItemViewModel repo)
    {
        if (repo == null) return;
        _configService.RemoveRepository(repo.Name);
        await _configService.SaveAsync();
        Repositories.Remove(repo);
        if (SelectedRepository == repo)
            SelectedRepository = Repositories.FirstOrDefault();
    }

    [RelayCommand]
    private void AddLocalRepoConfirmed(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        if (!_svnService.IsValidWorkingCopy(path))
        {
            ShowError?.Invoke("不是有效的 SVN 工作副本（没有 .svn 目录）");
            return;
        }

        var name = new DirectoryInfo(path).Name;
        var repo = new Repository
        {
            Name = name,
            Path = path,
            IsActive = true,
            RepositoryType = RepositoryType.Local
        };

        _configService.AddRepository(repo);
        _ = _configService.SaveAsync();

        Repositories.Add(new RepositoryItemViewModel { Name = name, Path = path, IsActive = true });
        SelectedRepository = Repositories.Last();
    }
}