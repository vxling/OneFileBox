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
        var border = this.FindControl<Border>("NavBorder");
        if (border != null)
        {
            border.AddHandler(InputElement.PointerEnteredEvent, NavPanel_PointerEnter, RoutingStrategies.Bubble);
            border.AddHandler(InputElement.PointerExitedEvent, NavPanel_PointerLeave, RoutingStrategies.Bubble);
        }

        // 绑定工具栏按钮点击
        this.FindControl<Button>("OpenWorkingCopyBtn")!.Click += OpenWorkingCopy_Click;
        this.FindControl<Button>("SettingsBtn")!.Click += Settings_Click;
        this.FindControl<Button>("AddRepoBtn")!.Click += AddRepository_Click;
    }

    private void NavPanel_PointerEnter(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Expand();
    }

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
            vm.HomeVm.LocalWorkingCopyPath = result[0].Path.LocalPath;
        }
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.NavSelectedIndex = 0;
    }

    private void AddRepository_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 弹出添加仓库对话框
    }
}
