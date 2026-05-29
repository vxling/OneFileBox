using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using OneFileBox.ViewModels;
using OneFileBox.Services;
using OneFileBox.Models;

namespace OneFileBox.Views;

public partial class MainWindow : Window
{
    private bool _isExiting;
    private readonly ConfigService _configService = ConfigService.Instance;
    private static readonly List<string> _systemCopiedPaths = new();
    private int _isCopying;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ShowAddLocalRepoDialog += OnShowAddLocalRepo;
                vm.ShowCheckoutDialog += OnShowCheckout;
                vm.ShowSettingsDialog += OnShowSettings;
                vm.ShowAboutRequested += OnShowAbout;
                vm.ShowError += OnShowError;
                vm.ShowWindowRequested += () => ShowAndActivate();
                vm.ShowConflictDialog += OnShowConflict;
                vm.ShowRenameDialog += OnShowRename;
                vm.ShowNewItemDialog += OnShowNewItem;
                vm.CopyPathRequested += OnCopyPath;
                vm.ShowCopyDialog += OnShowCopyDialog;
            }

            // Keyboard shortcuts
            AddHandler(KeyDownEvent, OnKeyDown);

            // Drag and drop
            FileDataGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            FileDataGrid.AddHandler(DragDrop.DropEvent, OnDrop);
            FileDataGrid.SelectionChanged += OnSelectionChanged;
        };
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ForceClose()
    {
        _isExiting = true;
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_isExiting && ConfigService.Instance.Config.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private async void OnShowAddLocalRepo(string _)
    {
        var dialog = new AddLocalRepoWindow();
        try
        {
            var result = await dialog.ShowDialog<Models.Repository?>(this);
            if (result != null && DataContext is MainWindowViewModel vm)
            {
                await vm.AddLocalRepoConfirmed(result.Path);
            }
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "OnShowAddLocalRepo failed");
        }
    }

    private void OnShowAbout()
    {
        var win = new AboutWindow();
        win.ShowDialog(this);
    }

    private void OnShowSettings(string _)
    {
        var dialog = new SettingsWindow();
        dialog.ShowDialog(this);
    }

    private async void OnShowConflict(List<ConflictedFileInfo> conflicts)
    {
        var dialog = new ConflictWindow();
        dialog.SetConflicts(conflicts);
        await dialog.ShowDialog(this);
    }

    private async void OnShowCheckout(string _)
    {
        var dialog = new CheckoutWindow();
        var result = await dialog.ShowDialog<Models.Repository?>(this);
        if (result != null && DataContext is MainWindowViewModel vm)
        {
            _configService.AddRepository(result);
            await _configService.SaveAsync();
            vm.Repositories.Add(new RepositoryItemViewModel
            {
                Name = result.Name,
                Path = result.Path,
                IsActive = true
            });
            vm.SelectedRepository = vm.Repositories.Last();
        }
    }

    private void OnShowError(string msg) { }

    private void OnCopyPath(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo { FileName = "bash", Arguments = $"-c \"echo -n '{path}' | xclip -selection clipboard\"" });
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = "powershell", Arguments = $"-Command \"Set-Clipboard -Value '{path}'\"" });
            }
        }
        catch { }

        if (DataContext is MainWindowViewModel vm)
            vm.SetTransientStatus("路径已复制");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!vm.CanOperate) return;

        // Backspace = navigate up to parent directory
        if (e.Key == Key.Back)
        {
            _ = vm.NavigateUpCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        // Enter = open/enter selected file
        if (e.Key == Key.Enter)
        {
            if (vm.SelectedFile == null) return;
            _ = vm.NavigateToCommand.ExecuteAsync(vm.SelectedFile);
            e.Handled = true;
            return;
        }

        // Ctrl+C = Copy
        if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            var file = vm.SelectedFile;
            if (file == null) return;
            _systemCopiedPaths.Clear();
            _systemCopiedPaths.Add(file.FullPath);
            vm.CanPaste = true;
            vm.SetTransientStatus($"已复制：{file.Name}");
            e.Handled = true;
            return;
        }

        // Ctrl+V = Paste
        if (e.Key == Key.V && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            if (_systemCopiedPaths.Count == 0 || string.IsNullOrEmpty(vm.CurrentPath)) return;
            _ = DoExecuteCopyAsync(_systemCopiedPaths.ToList(), vm.CurrentPath);
            e.Handled = true;
            return;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(Avalonia.Input.DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CurrentPath)) return;
        if (!e.DataTransfer.Contains(Avalonia.Input.DataFormat.File)) return;

        // Get file paths from drag data (IStorageItem[] -> string[])
        var items = e.DataTransfer.TryGetFiles();
        if (items == null || items.Length == 0) return;
        var files = items.Select(i => i.Path.LocalPath).ToList();

        await DoExecuteCopyAsync(files, vm.CurrentPath);
        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SelectedFiles = new ObservableCollection<FileItemViewModel>(
                FileDataGrid.SelectedItems.Cast<FileItemViewModel>());
    }

    private async Task DoExecuteCopyAsync(List<string> sourcePaths, string targetDir)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (Interlocked.CompareExchange(ref _isCopying, 1, 0) == 1) return;

        var analyzer = new FileAnalyzer();
        var plan = analyzer.Analyze(sourcePaths, targetDir);
        if (plan == null) { Interlocked.Exchange(ref _isCopying, 0); return; }
        if (plan.IsSameLocation) { Interlocked.Exchange(ref _isCopying, 0); return; }

        var progressWindow = new FileCopyProgressWindow();
        var copier = vm.GetFileCopier();
        progressWindow.SetCopier(copier);
        progressWindow.Show(this);

        var progress = new Progress<CopyProgress>(p => progressWindow.UpdateProgress(p));
        var result = await copier.CopyAsync(plan, progress);

        if (result.WasCancelled)
            progressWindow.SetCompleted("已取消复制");
        else if (result.HasError)
            progressWindow.SetError(result.ErrorMessage ?? "未知错误");
        else
            progressWindow.SetCompleted($"复制完成：{result.CopiedCount} 文件");

        Interlocked.Exchange(ref _isCopying, 0);
        await vm.RefreshCommand.ExecuteAsync(null);
    }

    private async void OnShowRename(string currentPath)
    {
        if (string.IsNullOrEmpty(currentPath)) return;
        var fileName = System.IO.Path.GetFileName(currentPath);
        var dialog = new InputDialog
        {
            Title = "重命名",
            InputText = fileName,
            LabelText = "新文件名："
        };
        var result = await dialog.ShowDialog<string>(this);
        if (!string.IsNullOrEmpty(result) && result != fileName)
        {
            var dir = System.IO.Path.GetDirectoryName(currentPath) ?? "";
            var newPath = System.IO.Path.Combine(dir, result);
            if (DataContext is MainWindowViewModel vm)
            {
                try
                {
                    await vm.SvnService.MoveAsync(currentPath, newPath);
                    await vm.RefreshCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    OnShowError("重命名失败: " + ex.Message);
                }
            }
        }
    }


    private async void OnShowBatchRename(List<string> paths)
    {
        var dialog = new BatchRenameWindow(paths);
        var baseName = await dialog.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(baseName)) return;

        if (DataContext is not MainWindowViewModel vm) return;

        var ext = System.IO.Path.GetExtension(paths.FirstOrDefault() ?? "");
        var results = new List<(string oldPath, string newPath, bool ok)>();

        for (int i = 0; i < paths.Count; i++)
        {
            var dir = System.IO.Path.GetDirectoryName(paths[i]) ?? "";
            var newPath = System.IO.Path.Combine(dir, $"{baseName} ({i + 1}){ext}");
            try
            {
                await vm.SvnService.MoveAsync(paths[i], newPath);
                results.Add((paths[i], newPath, true));
            }
            catch
            {
                results.Add((paths[i], newPath, false));
            }
        }

        var success = results.Count(r => r.ok);
        vm.SetTransientStatus($"批量重命名：{success}/{results.Count} 成功");
        await vm.RefreshCommand.ExecuteAsync(null);
    }

    private async void OnShowCopyDialog(List<string> sourcePaths, string destDir)
    {
        await DoExecuteCopyAsync(sourcePaths, destDir);
    }

    private async void OnShowNewItem(string itemType)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CurrentPath)) return;

        if (itemType == "folder")
        {
            var dialog = new InputDialog { Title = "新建文件夹", InputText = "", LabelText = "文件夹名称：" };
            var result = await dialog.ShowDialog<string>(this);
            if (!string.IsNullOrEmpty(result))
            {
                var newPath = System.IO.Path.Combine(vm.CurrentPath, result);
                try
                {
                    System.IO.Directory.CreateDirectory(newPath);
                    await vm.SvnService.AddPathAsync(newPath);
                    await vm.RefreshCommand.ExecuteAsync(null);
                }
                catch (Exception ex) { OnShowError("创建文件夹失败: " + ex.Message); }
            }
        }
        else
        {
            var dialog = new InputDialog { Title = "新建文件", InputText = "新建文件" + itemType, LabelText = "文件名：" };
            var result = await dialog.ShowDialog<string>(this);
            if (!string.IsNullOrEmpty(result))
            {
                var fileName = result.EndsWith(itemType) ? result : result + itemType;
                var newPath = System.IO.Path.Combine(vm.CurrentPath, fileName);
                try
                {
                    System.IO.File.WriteAllText(newPath, "");
                    await vm.SvnService.AddPathAsync(newPath);
                    await vm.RefreshCommand.ExecuteAsync(null);
                }
                catch (Exception ex) { OnShowError("创建文件失败: " + ex.Message); }
            }
        }
    }
}