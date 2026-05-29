using CommunityToolkit.Mvvm.ComponentModel;
using OneFileBox.Models;

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

    [ObservableProperty]
    private int _repositoryType;

    public string TypeIcon => RepositoryType == (int)OneFileBox.Models.RepositoryType.Local ? "📂" : "🌐";
}