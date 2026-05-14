using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OneFileBox.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private FileItemViewModel? _selectedFile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SyncStatus _selectedFilter = SyncStatus.All;

    [ObservableProperty]
    private string _connectionStatus = "○ 已断开";

    [ObservableProperty]
    private string _syncProgress = "0 of 0 文件已同步";

    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _lastSyncTime = "从未同步";

    [ObservableProperty]
    private string _localWorkingCopyPath = string.Empty;

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        try
        {
            await Task.Delay(1000);
        }
        finally
        {
            IsSyncing = false;
        }
    }
}

public enum SyncStatus
{
    All,
    Syncing,
    Modified,
    Conflict,
}
