using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OneFileBox.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private FileItemViewModel? _selectedFile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SyncStatus _selectedFilter = SyncStatus.All;

    [ObservableProperty]
    private string _connectionStatus = "○ 已断开";

    [ObservableProperty]
    private string _syncProgress = "0 of 0 文件已同步";

    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _lastSyncTime = "从未同步";

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _canOperate = false;

    // 右键菜单状态
    [ObservableProperty]
    private bool _canCopy = false;

    [ObservableProperty]
    private bool _canDelete = false;

    [ObservableProperty]
    private bool _canRename = false;

    [ObservableProperty]
    private bool _canPaste = false;

    public string BackButtonText => SelectedFilter == SyncStatus.SyncRecords
        ? "返回"
        : "返回上级目录";

    // 同步状态枚举
    public SyncStatus CurrentFilter => SelectedFilter;

    partial void OnSelectedFileChanged(FileItemViewModel? oldValue, FileItemViewModel? newValue)
    {
        CanCopy = newValue != null;
        CanDelete = newValue != null && newValue.Name != "..";
        CanRename = newValue != null && newValue.Name != "..";
    }

    partial void OnSelectedFilterChanged(SyncStatus value)
    {
        OnPropertyChanged(nameof(BackButtonText));
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        try
        {
            await Task.Delay(1000);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(CurrentPath))
            await LoadDirectoryAsync(CurrentPath);
    }

    [RelayCommand]
    private void NavigateInto(FileItemViewModel? item)
    {
        if (item == null) return;

        if (item.Name == "..")
        {
            var parentPath = Path.GetDirectoryName(CurrentPath);
            if (!string.IsNullOrEmpty(parentPath))
                _ = LoadDirectoryAsync(parentPath);
        }
        else if (item.IsDirectory || Directory.Exists(item.FullPath))
        {
            _ = LoadDirectoryAsync(item.FullPath);
        }
    }

    [RelayCommand]
    private void OpenItem(FileItemViewModel? item)
    {
        if (item == null) return;

        if (item.Name == "..")
        {
            NavigateInto(item);
            return;
        }

        if (item.IsDirectory || Directory.Exists(item.FullPath))
        {
            NavigateInto(item);
        }
        else if (File.Exists(item.FullPath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open file: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        try
        {
            var topLevel = (Avalonia.Application.Current?.ApplicationLifetime)
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var window = topLevel?.MainWindow;
            if (window?.Clipboard != null)
            {
                await ClipboardExtensions.SetTextAsync(window.Clipboard!, CurrentPath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CopyPath failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyFileAsync(FileItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var topLevel = (Avalonia.Application.Current?.ApplicationLifetime)
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var window = topLevel?.MainWindow;
            if (window?.Clipboard != null)
            {
                await ClipboardExtensions.SetTextAsync(window.Clipboard!, item.FullPath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CopyFile failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        if (!CanOperate || string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath))
            return;

        // TODO: 弹出输入对话框获取文件夹名称
        // 暂时用时间戳作为示例名称
        var newFolderName = $"新建文件夹_{DateTime.Now:HHmmss}";
        var newFolderPath = Path.Combine(CurrentPath, newFolderName);

        try
        {
            if (Directory.Exists(newFolderPath))
                return;

            Directory.CreateDirectory(newFolderPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"NewFolder failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NewFileAsync()
    {
        if (!CanOperate || string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath))
            return;

        var newFileName = $"新建文本文档_{DateTime.Now:HHmmss}.txt";
        var newFilePath = Path.Combine(CurrentPath, newFileName);

        try
        {
            if (File.Exists(newFilePath))
                return;

            await File.WriteAllTextAsync(newFilePath, string.Empty);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"NewFile failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(FileItemViewModel? item)
    {
        if (item == null || item.Name == "..") return;

        try
        {
            if (item.IsDirectory || Directory.Exists(item.FullPath))
                Directory.Delete(item.FullPath, recursive: true);
            else if (File.Exists(item.FullPath))
                File.Delete(item.FullPath);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Delete failed: {ex.Message}");
        }
    }

    // RenameAsync has 2 parameters so cannot be a relay command — call directly from View
    private async Task RenameAsync(FileItemViewModel? item, string? newName)
    {
        if (item == null || item.Name == ".." || string.IsNullOrWhiteSpace(newName))
            return;

        var parentDir = Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrEmpty(parentDir)) return;

        var newPath = Path.Combine(parentDir, newName.Trim());

        try
        {
            if (Directory.Exists(newPath) || File.Exists(newPath))
                return;

            if (item.IsDirectory || Directory.Exists(item.FullPath))
                Directory.Move(item.FullPath, newPath);
            else
                File.Move(item.FullPath, newPath);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rename failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RevertAsync(FileItemViewModel? item)
    {
        if (item == null || item.Name == "..") return;

        // SVN revert: restore file to last committed version
        // For now, just refresh
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        if (!CanOperate || string.IsNullOrEmpty(CurrentPath))
            return;

        // SVN add: mark unversioned files for addition
        await RefreshAsync();
    }

    [RelayCommand]
    private void ShowInExplorer(FileItemViewModel? item)
    {
        var path = item?.FullPath ?? CurrentPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var targetDir = File.Exists(path) || Directory.Exists(path)
                ? Path.GetDirectoryName(path) : path;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "explorer.exe" : "open",
                Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"\"{targetDir}\"" : targetDir,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ShowInExplorer failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenWith(FileItemViewModel? item)
    {
        if (item == null || item.IsDirectory) return;

        // 打开方式对话框 - 使用系统默认
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.FullPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"OpenWith failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (!CanOperate || string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath))
            return;

        try
        {
            var topLevel = (Avalonia.Application.Current?.ApplicationLifetime)
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var window = topLevel?.MainWindow;

            if (window == null) return;

            // 获取剪贴板内容
            if (window.Clipboard == null) return;
            var clipboardText = await ClipboardExtensions.TryGetTextAsync(window.Clipboard);
            if (string.IsNullOrEmpty(clipboardText))
                return;

            // 解析文件路径（每行一个）
            var lines = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var sourcePath = line.Trim();
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                    continue;

                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(CurrentPath, fileName);

                if (File.Exists(targetPath))
                    continue;

                File.Copy(sourcePath, targetPath);
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Paste failed: {ex.Message}");
        }
    }

    public async Task LoadDirectoryAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        IsLoading = true;
        try
        {
            CurrentPath = path;
            var items = new List<FileItemViewModel>();
            var dirInfo = new DirectoryInfo(path);
            var parentPath = dirInfo.Parent?.FullName;

            // Parent directory row - only show when not at root
            if (!string.IsNullOrEmpty(parentPath))
            {
                items.Add(new FileItemViewModel
                {
                    Name = "..",
                    FullPath = parentPath,
                    IsDirectory = true,
                    SyncState = SvnSyncState.Synced
                });
            }

            // Directories
            foreach (var dir in dirInfo.GetDirectories())
            {
                if (dir.Name.StartsWith(".")) continue;
                items.Add(new FileItemViewModel
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    SyncState = SvnSyncState.Synced
                });
            }

            // Files
            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Name.StartsWith(".")) continue;
                items.Add(new FileItemViewModel
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    SyncState = SvnSyncState.Synced
                });
            }

            Files = new ObservableCollection<FileItemViewModel>(items);
            CanOperate = !string.IsNullOrEmpty(CurrentPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LoadDirectory failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public enum SyncStatus
{
    All,
    Syncing,
    Modified,
    Conflict,
    SyncRecords,
}
