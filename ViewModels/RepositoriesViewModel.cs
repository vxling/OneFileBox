using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OneFileBox.ViewModels;

public partial class RepositoriesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<RepositoryItemViewModel> _repositories = new();

    [ObservableProperty]
    private RepositoryItemViewModel? _selectedRepository;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    partial void OnSelectedRepositoryChanged(RepositoryItemViewModel? oldValue, RepositoryItemViewModel? newValue)
    {
        // 当选择仓库时，更新 IsConnected 状态
        if (newValue != null)
        {
            foreach (var repo in Repositories)
                repo.IsConnected = repo == newValue;
        }
    }

    [RelayCommand]
    private async Task AddLocalRepositoryAsync()
    {
        // TODO: 弹出添加本地仓库对话框
        // 这里添加一个占位仓库
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CheckoutRepositoryAsync()
    {
        // TODO: 弹出 SVN Checkout 对话框
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void RemoveRepository(RepositoryItemViewModel? repo)
    {
        if (repo == null) return;

        var result = Repositories.Remove(repo);
        if (result && SelectedRepository == repo)
            SelectedRepository = null;
    }

    [RelayCommand]
    private void SelectRepository(RepositoryItemViewModel? repo)
    {
        if (repo != null)
            SelectedRepository = repo;
    }

    [RelayCommand]
    private async Task RefreshRepositoryAsync(RepositoryItemViewModel? repo)
    {
        if (repo == null) return;

        IsLoading = true;
        StatusText = $"正在刷新 {repo.Name}...";

        try
        {
            // TODO: 实际刷新仓库内容
            await Task.Delay(500);
            StatusText = $"{repo.Name} 刷新完成";
        }
        catch (Exception ex)
        {
            StatusText = $"刷新失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public partial class RepositoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _localPath = string.Empty;

    [ObservableProperty]
    private string _remoteUrl = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private string _lastSyncTime = "从未同步";

    [ObservableProperty]
    private RepositoryType _repositoryType = RepositoryType.Local;

    [ObservableProperty]
    private string _username = string.Empty;
}

public enum RepositoryType
{
    Local,
    Network
}
