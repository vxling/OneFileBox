#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class CheckoutWindow : Window
{
    public Repository? ResultRepository { get; private set; }

    private readonly ConfigService _configService;
    private string? _generatedLocalPath;

    public CheckoutWindow()
    {
        InitializeComponent();
        _configService = ConfigService.Instance;
    }

    private void RepoName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var name = RepoNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            LocalPathBox.Text = "";
            _generatedLocalPath = null;
            return;
        }
        _generatedLocalPath = Path.Combine(AppPaths.WorkCopies, name);
        LocalPathBox.Text = _generatedLocalPath;
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Local Path",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            _generatedLocalPath = folder[0].Path.LocalPath;
            LocalPathBox.Text = _generatedLocalPath;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private async void OK_Click(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        StatusText.IsVisible = false;

        var repoName = RepoNameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(repoName))
        {
            ShowError("Repository name is required");
            return;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (repoName.IndexOfAny(invalidChars) >= 0)
        {
            ShowError("Repository name contains invalid characters");
            return;
        }

        var repoUrl = RepoUrlBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            ShowError("Repository URL is required");
            return;
        }

        var username = string.IsNullOrWhiteSpace(UsernameBox.Text) ? null : UsernameBox.Text.Trim();
        var password = string.IsNullOrWhiteSpace(PasswordBox.Text) ? null : PasswordBox.Text;

        if (string.IsNullOrEmpty(_generatedLocalPath))
        {
            _generatedLocalPath = Path.Combine(AppPaths.WorkCopies, repoName);
        }

        if (Directory.Exists(_generatedLocalPath))
        {
            ShowError("Local path already exists. Choose a different name or delete the existing folder.");
            return;
        }

        if (_configService.Config.Repositories.Any(r => r.Url.Equals(repoUrl, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError("A repository with this URL already exists");
            return;
        }

        StatusText.Text = "Validating connection...";
        StatusText.IsVisible = true;
        OkBtn.IsEnabled = false;

        var svnSvc = new SvnCliService();
        var (connResult, connError) = await svnSvc.TestConnectionAsync(repoUrl!, username, password);

        if (connResult != SvnCliService.SvnConnectResult.Success)
        {
            ShowError($"{connResult}: {connError ?? "unknown error"}");
            StatusText.IsVisible = false;
            OkBtn.IsEnabled = true;
            return;
        }

        StatusText.Text = "Checking out...";
        CheckoutProgress.IsIndeterminate = true;
        CheckoutProgress.IsVisible = true;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_generatedLocalPath)!);
            var checkoutResult = await svnSvc.CheckoutAsync(repoUrl!, _generatedLocalPath!, username, password);

            if (checkoutResult.exitCode != 0)
            {
                ShowError($"Checkout failed: {checkoutResult.error}");
                try { Directory.Delete(_generatedLocalPath, recursive: true); } catch { }
                OkBtn.IsEnabled = true;
                return;
            }

            ResultRepository = new Repository
            {
                Name = repoName,
                Path = _generatedLocalPath,
                Url = repoUrl!,
                Username = username ?? "",
                Password = password ?? "",
                IsActive = true,
                RepositoryType = RepositoryType.Network
            };

            SvnCliLog.Information("Checkout successful: {Url} -> {Path}", repoUrl, _generatedLocalPath);
            Close();
        }
        catch (Exception ex)
        {
            SvnCliLog.Error(ex, "Checkout failed");
            ShowError($"Checkout failed: {ex.Message}");
            try { Directory.Delete(_generatedLocalPath!, recursive: true); } catch { }
            OkBtn.IsEnabled = true;
        }
        finally
        {
            CheckoutProgress.IsVisible = false;
            StatusText.IsVisible = false;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}