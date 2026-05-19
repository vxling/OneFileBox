using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            desktop.ShutdownRequested += async (s, e) =>
            {
                if (desktop.MainWindow?.DataContext is MainWindowViewModel vm)
                    await vm.ShutdownAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
