using CommunityToolkit.Mvvm.ComponentModel;

namespace OneFileBox.ViewModels;

public partial class RepositoryItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _url = "";
}