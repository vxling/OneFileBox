using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneFileBox.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _startOnBoot = false;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private int _syncIntervalMinutes = 5;

    [ObservableProperty]
    private int _maxBandwidth = 0; // 0 = unlimited

    [ObservableProperty]
    private bool _showHiddenFiles = false;

    [ObservableProperty]
    private bool _confirmBeforeCommit = true;

    [ObservableProperty]
    private string _proxyUrl = string.Empty;

    [ObservableProperty]
    private int _proxyPort = 0;
}
