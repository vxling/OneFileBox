using CommunityToolkit.Mvvm.ComponentModel;

namespace OneFileBox.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private double _navWidth = 200;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isPinned = false;

    [ObservableProperty]
    private int _navSelectedIndex = 1;

    // 页面 ViewModels
    [ObservableProperty]
    private HomeViewModel _homeVm = new();

    [ObservableProperty]
    private RepositoriesViewModel _repositoriesVm = new();

    [ObservableProperty]
    private SettingsViewModel _settingsVm = new();

    public string PinIcon => IsPinned ? "📌" : "📍";
    public string PinTooltip => IsPinned ? "取消固定" : "固定导航栏";

    public void Expand()
    {
        if (!IsExpanded)
        {
            IsExpanded = true;
            NavWidth = 200;
        }
    }

    public void CollapseIfNotPinned()
    {
        if (!IsPinned && IsExpanded)
        {
            IsExpanded = false;
            NavWidth = 56;
        }
    }

    public void TogglePin()
    {
        IsPinned = !IsPinned;
        OnPropertyChanged(nameof(PinIcon));
        OnPropertyChanged(nameof(PinTooltip));
    }
}
