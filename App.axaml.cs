using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using OneFileBox.Services;
using OneFileBox.ViewModels;
using OneFileBox.Views;

namespace OneFileBox;

public partial class App : Application
{
    private SplashWindow? _splash;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _splash = new SplashWindow();
            _splash.Show();

            var vm = new MainWindowViewModel();
            _ = InitializeWithSplashAsync(vm, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeWithSplashAsync(MainWindowViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            _splash?.SetStatus("Loading configuration...");
            SplashLog.Info("Starting InitializeAsync");
            await vm.InitializeAsync();
            SplashLog.Info("InitializeAsync completed, showing main window");
            _splash?.SetStatus("Ready to start");

            await Task.Delay(300);

            var mainWindow = new MainWindow { DataContext = vm };
            desktop.MainWindow = mainWindow;
            _splash?.Close();
            _splash = null;

            SetupTrayIcon(vm, desktop);

            desktop.ShutdownRequested += async (s, e) =>
            {
                await vm.ShutdownAsync();
            };
        }
        catch (Exception ex)
        {
            SplashLog.Error(ex, "Startup FAILED");
            SvnCliLog.Error(ex, "Startup failed");
            _splash?.ShowErrorAndClose(ex.Message);
        }
    }

    private void SetupTrayIcon(MainWindowViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var trayMenu = new NativeMenu();

        var showItem = new NativeMenuItem { Header = "显示主窗口" };
        showItem.Command = vm.ShowWindowCommand;
        trayMenu.Add(showItem);

        trayMenu.Add(new NativeMenuItemSeparator());

        var syncItem = new NativeMenuItem { Header = "立即同步" };
        syncItem.Command = vm.SyncNowCommand;
        trayMenu.Add(syncItem);

        trayMenu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem { Header = "退出" };
        exitItem.Command = vm.ExitCommand;
        trayMenu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://OneFileBox/Assets/avalonia-logo.ico"))),
            ToolTipText = "OneFileBox — SVN 文件管理器",
            Menu = trayMenu
        };

        _trayIcon.Clicked += (s, e) => vm.ShowWindowCommand.Execute(null);

        desktop.ShutdownRequested += (s, e) =>
        {
            if (ConfigService.Instance.Config.MinimizeToTray && !vm.IsExiting)
            {
                e.Cancel = true;
                desktop.MainWindow?.Hide();
            }
        };
    }

internal static class SplashLog
{
    internal static void Info(string msg, params object[] args)
    {
        var full = args.Length > 0 ? $"[Splash] {msg}: {string.Join(", ", args)}" : $"[Splash] {msg}";
        Console.WriteLine(full);
    }
    internal static void Error(Exception? ex, string msg, params object[] args)
    {
        var full = ex != null ? $"[Splash] ERROR {msg}: {ex.Message}" : $"[Splash] ERROR {msg}";
        Console.WriteLine(full);
    }
}

}