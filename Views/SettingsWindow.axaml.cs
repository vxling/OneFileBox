#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;

    public SettingsWindow()
    {
        InitializeComponent();
        _configService = ConfigService.Instance;
        LoadSettings();
    }

    private void LoadSettings()
    {
        AutoSyncCheckBox.IsChecked = _configService.Config.AutoSyncEnabled;
        SyncIntervalSlider.Value = _configService.Config.SyncIntervalMinutes;
        SyncIntervalText.Text = $"{_configService.Config.SyncIntervalMinutes} min";

        MinimizeToTrayCheckBox.IsChecked = _configService.Config.MinimizeToTray;
        AutoStartCheckBox.IsChecked = _configService.Config.AutoStart;
        ProxyUrlBox.Text = _configService.Config.ProxyUrl;

        LanguageComboBox.SelectedIndex = _configService.Config.Language switch
        {
            "zh" => 1,
            "en" => 2,
            _ => 0
        };

        ThemeComboBox.SelectedIndex = _configService.Config.Theme switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0
        };

        AutoStartCheckBox.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "IsChecked")
            {
                // AutoStartMinimize would be enabled here if we had that control
            }
        };
    }

    private async void OK_Click(object? sender, RoutedEventArgs e)
    {
        _configService.Config.AutoSyncEnabled = AutoSyncCheckBox.IsChecked == true;
        _configService.Config.SyncIntervalMinutes = (int)SyncIntervalSlider.Value;

        _configService.Config.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _configService.Config.AutoStart = AutoStartCheckBox.IsChecked == true;
        _configService.Config.ProxyUrl = ProxyUrlBox.Text?.Trim() ?? "";

        _configService.Config.Language = LanguageComboBox.SelectedIndex switch
        {
            1 => "zh",
            2 => "en",
            _ => "auto"
        };
        LocalizationService.Instance.SetLanguage(_configService.Config.Language);

        _configService.Config.Theme = ThemeComboBox.SelectedIndex switch
        {
            1 => "light",
            2 => "dark",
            _ => "system"
        };

        _configService.Config.LocalCommandTimeoutSeconds = (int)LocalCommandTimeoutSlider.Value;
        SvnCliService.LocalCommandTimeoutMs = _configService.Config.LocalCommandTimeoutSeconds * 1000;

        // Auto start registration (Windows only)
        if (OperatingSystem.IsWindows())
            UpdateAutoStart(_configService.Config.AutoStart);

        await _configService.SaveAsync();
        SvnCliLog.Information("Settings saved");
        Close();
    }

    private void UpdateAutoStart(bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            if (enable)
                key.SetValue("OneFileBox", $"\"{exePath}\" --autostart");
            else
                key.DeleteValue("OneFileBox", false);
        }
        catch (Exception ex)
        {
            SvnCliLog.Warning("Failed to update auto start: {0}", ex.Message);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}