using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneFileBox.ViewModels;

public partial class FileItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _fullPath = "";

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _svnStatus = "";

    [ObservableProperty]
    private string _statusIcon = "";

    [ObservableProperty]
    private bool _isParentDirectory;

    public string FileSizeDisplay => IsDirectory ? "" : FormatFileSize(FileSize);
    public string LastModifiedDisplay => LastModified == DateTime.MinValue ? "" : LastModified.ToString("yyyy-MM-dd HH:mm");

    public int SortGroup => IsParentDirectory ? 0 : 1;

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}