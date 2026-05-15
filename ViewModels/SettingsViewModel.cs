using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;

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

    [ObservableProperty]
    private bool _isDirty = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event EventHandler? SettingsSaved;

    partial void OnStartOnBootChanged(bool value) => IsDirty = true;
    partial void OnMinimizeToTrayChanged(bool value) => IsDirty = true;
    partial void OnShowNotificationsChanged(bool value) => IsDirty = true;
    partial void OnSyncIntervalMinutesChanged(int value) => IsDirty = true;
    partial void OnMaxBandwidthChanged(int value) => IsDirty = true;
    partial void OnShowHiddenFilesChanged(bool value) => IsDirty = true;
    partial void OnConfirmBeforeCommitChanged(bool value) => IsDirty = true;
    partial void OnProxyUrlChanged(string value) => IsDirty = true;
    partial void OnProxyPortChanged(int value) => IsDirty = true;

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // TODO: 保存设置到配置文件
            await Task.Delay(100);
            StatusMessage = "设置已保存";
            IsDirty = false;
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        StartOnBoot = false;
        MinimizeToTray = true;
        ShowNotifications = true;
        SyncIntervalMinutes = 5;
        MaxBandwidth = 0;
        ShowHiddenFiles = false;
        ConfirmBeforeCommit = true;
        ProxyUrl = string.Empty;
        ProxyPort = 0;
        IsDirty = true;
        StatusMessage = "设置已重置为默认值";
    }

    [RelayCommand]
    private void TestProxy()
    {
        if (string.IsNullOrWhiteSpace(ProxyUrl))
        {
            StatusMessage = "请输入代理服务器地址";
            return;
        }

        // TODO: 实际测试代理连接
        StatusMessage = $"正在测试代理 {ProxyUrl}:{ProxyPort}...";
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OneFileBox"
            );

            if (!Directory.Exists(settingsPath))
                Directory.CreateDirectory(settingsPath);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "explorer.exe" : "open",
                Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"\"{settingsPath}\"" : settingsPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            StatusMessage = "已打开配置文件夹";
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件夹失败: {ex.Message}";
        }
    }
}
