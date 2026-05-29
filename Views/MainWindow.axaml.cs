using System;
using Avalonia.Controls;
using Avalonia.Input;
using OneFileBox.ViewModels;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class MainWindow : Window
{
    private DateTime _lastClickTime;
    private FileItemViewModel? _lastClickedItem;

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
                vm.ShowError += OnShowError;
            }
        };
    }

    private async void OnShowAddLocalRepo(string _)
    {
        var dialog = new AddLocalRepoWindow();
        var result = await dialog.ShowDialog<Models.Repository?>(this);
        if (result != null && DataContext is MainWindowViewModel vm)
        {
            vm.AddLocalRepoConfirmed(result.Path);
        }
    }

    private void OnShowCheckout(string _) { }
    private void OnShowSettings(string _) { }
    private void OnShowError(string msg) { }
}