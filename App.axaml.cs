using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OneFileBox.ViewModels;
using OneFileBox.Views;

namespace OneFileBox;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow = mainWindow;

            // Setup system tray icon (Windows only for system-level balloon tips)
            SetupTrayIcon(desktop, mainWindow);

            // Avalonia 12: DevTools via DiagnosticsSupport
            this.AttachDeveloperTools();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
    {
        // Build tray menu
        var showWindowItem = new NativeMenuItem("显示窗口");
        showWindowItem.Click += (s, e) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };

        var separator = new NativeMenuItemSeparator();
        var quitItem = new NativeMenuItem("退出");
        quitItem.Click += (s, e) =>
        {
            // Show window before shutdown to allow Closing event to process normally
            mainWindow.Show();
            desktop.TryShutdown();
        };

        var menu = new NativeMenu();
        menu.Add(showWindowItem);
        menu.Add(separator);
        menu.Add(quitItem);

        // Create single TrayIcon using Avalonia 12 TrayIcons API (plural SetIcons)
        // Note: TrayIcon tooltips are platform-dependent; balloon tips only work on Windows
        var trayIcon = new TrayIcon
        {
            ToolTipText = "OneFileBox",
            Menu = menu,
            IsVisible = true,
        };

        // Click tray icon to show window (works on Win/Linux; macOS may vary)
        trayIcon.Clicked += (s, e) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };

        // When window closes, hide to tray instead of exiting
        mainWindow.Closing += (s, e) =>
        {
            if (e.CloseReason == WindowCloseReason.WindowClosing)
            {
                e.Cancel = true;  // Prevent actual close
                mainWindow.Hide(); // Minimize to tray
            }
        };

        // Set tray icon on the app using Avalonia 12 plural SetIcons API
        TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
    }
}
