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

            // 设置托盘图标
            SetupTrayIcon(desktop, mainWindow);

            // Avalonia 12: DevTools via DiagnosticsSupport
            this.AttachDeveloperTools();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
    {
        // 创建托盘图标菜单
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
            // 真正退出：先显示窗口再关闭，让 Closing 事件正常处理
            mainWindow.Show();
            desktop.TryShutdown();
        };

        var menu = new NativeMenu();
        menu.Add(showWindowItem);
        menu.Add(separator);
        menu.Add(quitItem);

        // 创建托盘图标
        var trayIcon = new TrayIcon
        {
            ToolTipText = "OneFileBox",
            Menu = menu,
            IsVisible = true,
        };

        // 点击托盘图标也显示窗口（macOS 不触发，但 Win/Linux 可以）
        trayIcon.Clicked += (s, e) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };

        // 窗口关闭时隐藏到托盘，而不是退出
        mainWindow.Closing += (s, e) =>
        {
            if (e.CloseReason == WindowCloseReason.WindowClosing)
            {
                e.Cancel = true; // 阻止关闭
                mainWindow.Hide(); // 隐藏到托盘
            }
        };

        // 设置托盘图标到 Application
        TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
    }
}