using System;
using System.Collections.Generic;
using System.IO;
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

        PathBox.Text = result[0].Path.LocalPath;
        ValidatePath();
    }

    private void ValidatePath()
    {
        ErrorText.IsVisible = false;
        var path = PathBox.Text?.Trim();

        if (string.IsNullOrEmpty(path))
        {
            ShowError("请选择目录");
            return;
        }

        if (_existingRepos.Exists(r => r.LocalPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError("该仓库已添加");
            return;
        }

        // Quick check: is it a working copy?
        var svnDir = Path.Combine(path, ".svn");
        if (!Directory.Exists(svnDir))
        {
            ShowError("不是有效的 SVN 工作副本（缺少 .svn 目录）");
            return;
        }

        OkBtn.IsEnabled = true;
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.IsVisible = true;
        OkBtn.IsEnabled = false;
    }

    private async void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowError("请选择目录");
            return;
        }

        OkBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        StatusText.Text = "正在获取仓库信息...";
        StatusText.IsVisible = true;
        ErrorText.IsVisible = false;

        try
        {
            // Get repo URL via svn info
            var url = await SvnCmdHelper.GetRepoUrlAsync(path);

            var name = new DirectoryInfo(path).Name;
            ResultRepo = new RepoConfig
            {
                Key = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                LocalPath = path,
                SvnUrl = url ?? "",
                UserName = "",
                Password = ""
            };

            Close(true);
        }
        catch (Exception ex)
        {
            ShowError($"获取仓库信息失败：{ex.Message}");
            OkBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            StatusText.IsVisible = false;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
