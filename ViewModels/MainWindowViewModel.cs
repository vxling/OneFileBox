using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

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

    // 右键菜单状态
    [ObservableProperty]
    private bool _canCopy;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _canRename;

    [ObservableProperty]
    private bool _canPaste;

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

    [RelayCommand]
    private void TogglePinCommand()
    {
        TogglePin();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        NavSelectedIndex = 0;
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        NavSelectedIndex = 1;
    }

    [RelayCommand]
    private void NavigateToRepositories()
    {
        NavSelectedIndex = 2;
    }

    // 更新右键菜单可用状态
    public void UpdateContextMenuState(bool hasSelection, bool isNotParentDir, bool hasFilesToPaste)
    {
        CanCopy = hasSelection;
        CanDelete = hasSelection && isNotParentDir;
        CanRename = hasSelection && isNotParentDir;
        CanPaste = hasFilesToPaste;
    }
}
