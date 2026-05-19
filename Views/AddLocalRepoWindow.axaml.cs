using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneFileBox_new.Models;
using OneFileBox_new.Services;

namespace OneFileBox_new.Views;

public partial class AddLocalRepoWindow : Window
{
    private readonly List<RepoConfig> _existingRepos;
    private readonly TopLevel? _topLevel;

    public RepoConfig? ResultRepo { get; private set; }

    public AddLocalRepoWindow() : this([], null) { }

    public AddLocalRepoWindow(IEnumerable<RepoConfig> existingRepos, TopLevel? topLevel)
    {
        _existingRepos = existingRepos != null ? new List<RepoConfig>(existingRepos) : [];
        _topLevel = topLevel;
        InitializeComponent();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (_topLevel == null) return;

        var result = await _topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择本地 SVN 工作副本目录",
            AllowMultiple = false
        });

        if (result.Count == 0) return;

        var path = result[0].Path.LocalPath;
        PathBox.Text = path;

        // 清除旧状态
        ClearMessages();
        UrlBox.IsVisible = false;
        OkBtn.IsEnabled = false;

        // 异步校验 + 获取 URL，超时 10 秒
        await ValidateAndFetchUrlAsync(path);
    }

    private async Task ValidateAndFetchUrlAsync(string path)
    {
        // 基础校验
        if (_existingRepos.Exists(r => r.LocalPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError("该仓库已添加");
            return;
        }

        var svnDir = Path.Combine(path, ".svn");
        if (!Directory.Exists(svnDir))
        {
            ShowError("不是有效的 SVN 工作副本（缺少 .svn 目录）");
            return;
        }

        // 显示正在获取
        ShowStatus("正在获取仓库信息...");
        OkBtn.IsEnabled = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var url = await SvnCmdHelper.GetRepoUrlAsync(path);

            if (string.IsNullOrEmpty(url))
            {
                ShowError("无法获取仓库地址，请确认网络连接正常");
                return;
            }

            UrlBox.Text = url;
            UrlBox.IsVisible = true;
            HideStatus();
            OkBtn.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            ShowError("获取仓库信息超时（10秒），请检查网络");
        }
        catch (Exception ex)
        {
            ShowError($"获取仓库信息失败：{ex.Message}");
        }
    }

    private async void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowError("请先选择目录");
            return;
        }

        if (_existingRepos.Exists(r => r.LocalPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError("该仓库已添加");
            return;
        }

        OkBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        ShowStatus("正在保存...");

        try
        {
            // 如果 URL 还没抓（罕见情况），这里补一次
            var url = UrlBox.Text;
            if (string.IsNullOrEmpty(url))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                url = await SvnCmdHelper.GetRepoUrlAsync(path) ?? "";
            }

            var name = new DirectoryInfo(path).Name;
            ResultRepo = new RepoConfig
            {
                Key = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                LocalPath = path,
                SvnUrl = url,
                UserName = "",
                Password = ""
            };

            Close(true);
        }
        catch (Exception ex)
        {
            ShowError($"保存失败：{ex.Message}");
            OkBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            HideStatus();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.IsVisible = true;
        OkBtn.IsEnabled = false;
        HideStatus();
    }

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
        StatusText.IsVisible = true;
        ErrorText.IsVisible = false;
    }

    private void HideStatus()
    {
        StatusText.IsVisible = false;
    }

    private void ClearMessages()
    {
        ErrorText.IsVisible = false;
        StatusText.IsVisible = false;
    }
}
