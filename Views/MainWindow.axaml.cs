using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneFileBox.ViewModels;

namespace OneFileBox.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Register pointer events on NavBorder using AddHandler
        // Border does NOT support PointerEnter/PointerLeave directly in Avalonia 12
        var navBorder = this.FindControl<Border>("NavBorder");
        if (navBorder != null)
        {
            navBorder.AddHandler(InputElement.PointerEnteredEvent, NavPanel_PointerEnter, RoutingStrategies.Bubble);
            navBorder.AddHandler(InputElement.PointerExitedEvent, NavPanel_PointerLeave, RoutingStrategies.Bubble);
        }

        // Toolbar button event bindings
        this.FindControl<Button>("OpenWorkingCopyBtn")!.Click += OpenWorkingCopy_Click;
        this.FindControl<Button>("SettingsBtn")!.Click += Settings_Click;
        this.FindControl<Button>("AddRepoBtn")!.Click += AddRepository_Click;
        this.FindControl<Button>("RefreshBtn")!.Click += Refresh_Click;

        // File list double-click to enter directory
        var fileListBox = this.FindControl<ListBox>("FileListBox");
        if (fileListBox != null)
        {
            fileListBox.DoubleTapped += FileListBox_DoubleTapped;
        }
    }

    /// <summary>
    /// Pointer entered nav panel - expand if collapsed and not pinned
    /// </summary>
    private void NavPanel_PointerEnter(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Expand();
    }

    /// <summary>
    /// Pointer left nav panel - collapse if not pinned
    /// </summary>
    private void NavPanel_PointerLeave(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CollapseIfNotPinned();
    }

    private void PinButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.TogglePin();
    }

    private async void OpenWorkingCopy_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择工作副本目录",
            AllowMultiple = false,
        });

        if (result.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            vm.HomeVm.CurrentPath = result[0].Path.LocalPath;
        }
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.NavSelectedIndex = 0;
    }

    private void AddRepository_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Show add repository dialog
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.HomeVm.SyncNowCommand.CanExecute(null))
        {
            await vm.HomeVm.SyncNowCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// ListBox double-click handler - enter directory if item is a folder
    /// </summary>
    private void FileListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.HomeVm.SelectedFile is FileItemViewModel file)
        {
            if (file.IsDirectory)
            {
                // Navigate into directory via HomeViewModel.NavigateIntoCommand
                vm.HomeVm.NavigateIntoCommand.Execute(file);
            }
            else
            {
                // Open file
                vm.HomeVm.OpenItemCommand.Execute(file);
            }
        }
    }
}
