#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class AddLocalRepoWindow : Window
{
    public Repository? ResultRepository { get; private set; }

    public AddLocalRepoWindow()
    {
        InitializeComponent();
    }

    public async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 SVN 工作副本目录",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            PathBox.Text = folders[0].Path.LocalPath;
            await ValidatePathAsync();
        }
    }

    private async Task ValidatePathAsync()
    {
        ErrorText.IsVisible = false;
        StatusText.IsVisible = false;
        OkBtn.IsEnabled = false;

        var path = PathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowError("请选择目录");
            return;
        }

        var svnSvc = new SvnCliService();
        if (!svnSvc.IsValidWorkingCopy(path))
        {
            ShowError("所选目录不是有效的 SVN 工作副本（没有 .svn 目录）");
            return;
        }

        var existing = ConfigService.Instance.Config.Repositories;
        if (existing.Exists(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError("本地路径已存在，不能重复添加");
            return;
        }

        OkBtn.IsEnabled = true;
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.IsVisible = true;
    }

    private async void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowError("请选择目录");
            return;
        }

        var svnSvc = new SvnCliService();
        if (!svnSvc.IsValidWorkingCopy(path))
        {
            ShowError("所选目录不是有效的 SVN 工作副本");
            return;
        }

        StatusText.Text = "正在获取仓库信息...";
        StatusText.IsVisible = true;

        try
        {
            var url = await svnSvc.GetRepoUrlAsync(path);
            var name = new DirectoryInfo(path).Name;

            ResultRepository = new Repository
            {
                Name = name,
                Path = path,
                Url = url,
                IsActive = false,
                RepositoryType = RepositoryType.Local
            };

            SvnCliLog.Information("AddLocalRepo: added {Name} at {Path}", name, path);
            Close();
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "AddLocalRepoWindow failed");
            ShowError("获取仓库信息失败: " + ex.Message);
        }
        finally
        {
            StatusText.IsVisible = false;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}