using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OneFileBox.Services;
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
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            _ = vm.InitializeAsync();

            desktop.ShutdownRequested += async (s, e) =>
            {
                await vm.ShutdownAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}