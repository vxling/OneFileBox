#nullable enable
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Timers;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using OneFileBox.ViewModels;
using CommunityToolkit.Mvvm.Input;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly SvnCliService _svnService;
    private readonly RepoGlobalManager _globalManager;

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

    [ObservableProperty]
    private bool _showSyncRecords;

    [ObservableProperty]
    private bool _isExiting;

    [ObservableProperty]
    private string _backButtonText = "← 返回";

    [ObservableProperty]
    private ObservableCollection<SyncRecordDisplay> _syncRecords = new();

    // Context menu enable flags
    [ObservableProperty]
    private bool _canOperate;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _canRename;

    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _selectedFiles = new();

    [ObservableProperty]
    private bool _canPaste;

    [ObservableProperty]
    private bool _canCopyPath;

    public enum SyncStatusType { Idle, Syncing, Success, Failed }

    [ObservableProperty]
    private SyncStatusType _syncStatus = SyncStatusType.Idle;

    private string _savedStatus = "就绪";
    private System.Timers.Timer? _transientTimer;



    // FileCopier for copy/paste
    private readonly FileCopier _fileCopier = new();

    public event Action<string>? ShowAddLocalRepoDialog;
    public event Action<string>? ShowCheckoutDialog;
    public event Action<string>? ShowSettingsDialog;
    public event Action<string>? ShowError;
    public event Action? ShowWindowRequested;
    public event Action? ShowAboutRequested;
    public event Action<List<ConflictedFileInfo>>? ShowConflictDialog;

    public RepoGlobalManager GlobalManager => _globalManager;
    public SvnCliService SvnService => _svnService;
    public FileCopier GetFileCopier() => _fileCopier;

    public MainWindowViewModel()
    {
        _configService = ConfigService.Instance;
        _svnService = new SvnCliService();
        _globalManager = new RepoGlobalManager();
        _fileCopier.SetSvnService(_svnService);

        // Bridge CredentialExpired to UI error
        _svnService.CredentialExpired += (url) => {
            ShowError?.Invoke($"凭据已过期，请重新输入密码: {url}");
        };

        // Bridge RepoGlobalManager events to ViewModel events
        _globalManager.FilesChanged += (s, e) => { _ = RefreshAsync(); };
        _globalManager.SyncNotification += (s, msg) => {
            StatusText = msg;
            if (msg != null && (msg.Contains("完成") || msg.Contains("成功")))
                SyncStatus = SyncStatusType.Success;
            else if (msg != null && (msg.Contains("失败") || msg.Contains("错误")))
                SyncStatus = SyncStatusType.Failed;
            else
                SyncStatus = SyncStatusType.Syncing;
        };

        // Transient status timer for auto-clear messages
        _transientTimer = new System.Timers.Timer(3000);
        _transientTimer.AutoReset = false;
        _transientTimer.Elapsed += (s, e) => {
            StatusText = _savedStatus;
            SyncStatus = SyncStatusType.Idle;
        };

    }

    /// <summary>Set a transient status message that auto-clears after 3 seconds.</summary>
    public void SetTransientStatus(string message, SyncStatusType status = SyncStatusType.Syncing)
    {
        _savedStatus = StatusText;
        StatusText = message;
        SyncStatus = status;
        _transientTimer?.Stop();
        _transientTimer?.Start();
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
                IsActive = repo.IsActive,
                Url = repo.Url ?? "",
                RepositoryType = (int)repo.RepositoryType
            });
        }
        if (Repositories.Count > 0 && SelectedRepository == null)
            SelectedRepository = Repositories[0];
    }

    partial void OnSelectedRepositoryChanged(RepositoryItemViewModel? value)
    {
        if (value == null || string.IsNullOrEmpty(value.Path)) return;

        // Mark as active in config
        foreach (var r in Repositories)
            r.IsActive = r.Path == value.Path;
        _configService.Config.ActiveRepositoryName = value.Name;

        CurrentPath = value.Path;
        CanOperate = true;
        _ = RefreshAsync();

        // Switch to the RepoManager for this repository
        var manager = _globalManager.Managers.FirstOrDefault(m => m.Repository.Path == value.Path);
        if (manager != null)
        {
            _ = _globalManager.SwitchToAsync(manager);
        }
        else
        {
            // First time selecting this repo — create a new RepoManager
            var repo = new Repository
            {
                Name = value.Name,
                Path = value.Path,
                IsActive = true,
                RepositoryType = RepositoryType.Local
            };
            manager = _globalManager.CreateLocal(repo);
            _ = _globalManager.SwitchToAsync(manager);
        }
    }

    partial void OnSelectedFileChanged(FileItemViewModel? value)
    {
        CanDelete = value != null && !value.IsParentDirectory;
        CanRename = value != null && !value.IsParentDirectory;
        CanCopyPath = value != null;
        CanPaste = _copiedPaths.Count > 0 && CanOperate;
    }

    partial void OnShowSyncRecordsChanged(bool value)
    {
        if (value)
            LoadSyncRecords();
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
        if (repo != null && parent.Length > repo.Path.Length)
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
        var manager = _globalManager.Managers.FirstOrDefault(m => m.Repository.Path == repo.Path);
        try
        {
            if (manager != null)
                await manager.DismissAsync();
        }
        catch (Exception ex)
        {
            ShowError?.Invoke($"关闭仓库失败: {ex.Message}");
        }

        _configService.RemoveRepository(repo.Name);
        await _configService.SaveAsync();
        Repositories.Remove(repo);
        if (SelectedRepository == repo)
            SelectedRepository = Repositories.FirstOrDefault();
    }

    [RelayCommand]
    public async Task AddLocalRepoConfirmed(string path)
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

        try
        {
            _configService.AddRepository(repo);
            await _configService.SaveAsync();
            Repositories.Add(new RepositoryItemViewModel { Name = name, Path = path, IsActive = true });
            SelectedRepository = Repositories.Last();
        }
        catch (Exception ex)
        {
            _configService.Config.Repositories.Remove(repo);
            ShowError?.Invoke("添加仓库失败: " + ex.Message);
        }
    }

    [RelayCommand]
    private void ShowWindow() => ShowWindowRequested?.Invoke();

    [RelayCommand]
    private void ShowAbout() => ShowAboutRequested?.Invoke();

    [RelayCommand]
    private async Task SyncNow()
    {
        await _configService.SaveAsync();
        StatusText = "已触发立即同步";
    }

    [RelayCommand]
    private void Exit()
    {
        IsExiting = true;
        Environment.Exit(0);
    }

    public async Task ShowConflictWindowAsync(List<ConflictedFileInfo> conflicts)
    {
        ShowConflictDialog?.Invoke(conflicts);
    }

    // ─── Context Menu Commands ───────────────────────────────────────

    [RelayCommand]
    private void OpenFile()
    {
        // Open selected file with system default app
        if (SelectedFile == null || SelectedFile.IsParentDirectory) return;
        if (SelectedFile.IsDirectory)
        {
            CurrentPath = SelectedFile.FullPath;
            _ = RefreshAsync();
        }
        else
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = SelectedFile.FullPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    [RelayCommand]
    private async Task DeleteFile()
    {
        if (SelectedFile == null || SelectedFile.IsParentDirectory) return;
        try
        {
            await _svnService.DeleteAsync(SelectedFile.FullPath);
            await RefreshAsync();
            StatusText = "已删除";
        }
        catch (Exception ex)
        {
            ShowError?.Invoke("删除失败: " + ex.Message);
        }
    }

    [RelayCommand]
    private void RenameFile()
    {
        // Raise an event for MainWindow to show an InputDialog
        ShowRenameDialog?.Invoke(SelectedFile?.FullPath ?? "");
    }

    public event Action<string>? ShowRenameDialog;

    [RelayCommand]
    private void ToggleSyncRecordsView()
    {
        ShowSyncRecords = !ShowSyncRecords;
        if (ShowSyncRecords && SelectedRepository != null)
            LoadSyncRecords();
    }

    [RelayCommand]
    private void CloseSyncRecordsView()
    {
        ShowSyncRecords = false;
    }

    private void LoadSyncRecords()
    {
        if (SelectedRepository == null) return;
        SyncRecords.Clear();
        var records = SyncRecordService.Instance.Records;
        foreach (var r in records.Where(r => r.RepoName == SelectedRepository.Name))
            SyncRecords.Add(SyncRecordDisplay.FromRecord(r));
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedFile == null) return;
        // Copy path to clipboard — handled by MainWindow.axaml.cs via event
        CopyPathRequested?.Invoke(SelectedFile.FullPath);
        SetTransientStatus("路径已复制");
    }

    public event Action<string>? CopyPathRequested;

    [RelayCommand]
    private async Task ManualSync()
    {
        if (!CanOperate) return;
        // Trigger immediate sync on current RepoManager
        if (_globalManager.ActiveManager != null)
        {
            StatusText = "正在同步...";
            await _globalManager.ActiveManager.SyncNowAsync();
            StatusText = "同步完成";
        }
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (SelectedFile == null) return;
        var path = SelectedFile.IsDirectory
            ? SelectedFile.FullPath
            : Path.GetDirectoryName(SelectedFile.FullPath);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    // New file commands — path is derived from CurrentPath
    [RelayCommand]
    private async Task NewFolder()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke("folder");
    }

    [RelayCommand]
    private async Task NewTextFile()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".txt");
    }

    [RelayCommand]
    private async Task NewWordDoc()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".docx");
    }

    [RelayCommand]
    private async Task NewExcelSheet()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".xlsx");
    }

    [RelayCommand]
    private async Task NewPowerPoint()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".pptx");
    }

    [RelayCommand]
    private async Task NewPngImage()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".png");
    }

    [RelayCommand]
    private async Task NewBmpImage()
    {
        if (!CanOperate) return;
        ShowNewItemDialog?.Invoke(".bmp");
    }

    public event Action<string>? ShowNewItemDialog;

    // ─── Copy/Paste ─────────────────────────────────────────────────
    private readonly List<string> _copiedPaths = new();

    [RelayCommand]
    private void Copy()
    {
        if (SelectedFile == null) return;
        _copiedPaths.Clear();
        _copiedPaths.Add(SelectedFile.FullPath);
        CanPaste = true;
        StatusText = "已复制到剪贴板";
    }


    public event Action<List<string>, string>? ShowCopyDialog;

    [RelayCommand]
    private async Task Paste()
    {
        if (!CanOperate || _copiedPaths.Count == 0) return;
        ShowCopyDialog?.Invoke(_copiedPaths.ToList(), CurrentPath);
    }

    // ─── Multi-select commands ─────────────────────────────────────
}
