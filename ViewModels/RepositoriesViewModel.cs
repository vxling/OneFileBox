using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace OneFileBox.ViewModels;

public partial class RepositoriesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<RepositoryItemViewModel> _repositories = new();

    [ObservableProperty]
    private RepositoryItemViewModel? _selectedRepository;
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
}
