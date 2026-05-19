using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OneFileBox_new.Services;
using OneFileBox_new.ViewModels;
using OneFileBox_new.Views;

namespace OneFileBox_new;

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
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };

            // 启动时加载配置
            _ = vm.InitializeAsync();

            desktop.ShutdownRequested += async (s, e) =>
            {
                await vm.ShutdownAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
