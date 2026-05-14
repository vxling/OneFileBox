using CommunityToolkit.Mvvm.ComponentModel;

namespace OneFileBox.ViewModels;

public partial class FileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private SvnSyncState _syncState = SvnSyncState.Synced;

    [ObservableProperty]
    private string _lastSyncTime = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string StateIcon => SyncState switch
    {
        SvnSyncState.Synced => "✓",
        SvnSyncState.Syncing => "↻",
        SvnSyncState.Modified => "✏",
        SvnSyncState.Pending => "↑",
        SvnSyncState.Conflict => "⚠",
        SvnSyncState.Error => "✗",
        _ => "—"
    };

    public string StateColor => SyncState switch
    {
        SvnSyncState.Synced => "#22C55E",
        SvnSyncState.Syncing => "#3B82F6",
        SvnSyncState.Modified => "#F59E0B",
        SvnSyncState.Pending => "#6B7280",
        SvnSyncState.Conflict => "#EF4444",
        SvnSyncState.Error => "#EF4444",
        _ => "#6B7280"
    };

    public string FileIcon => IsDirectory ? "📁" : "📄";
}

public enum SvnSyncState
{
    Synced,
    Syncing,
    Modified,
    Pending,
    Conflict,
    Ignored,
    Locked,
    Error,
}
