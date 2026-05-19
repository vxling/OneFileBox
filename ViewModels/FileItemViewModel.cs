using System;
using OneFileBox_new.Models;

namespace OneFileBox_new.ViewModels;

public class FileItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _typeIcon = "📄";
    private long _fileSize;
    private DateTime _lastModified;
    private bool _isDirectory;
    private string _fullPath = string.Empty;
    private string _statusText = "";
    private string _statusIcon = "";
    private SvnItemState _svnState = SvnItemState.None;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string TypeIcon
    {
        get => _typeIcon;
        set => SetProperty(ref _typeIcon, value);
    }

    public long FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    public string FileSizeDisplay => IsDirectory ? "--" : FormatSize(FileSize);

    public DateTime LastModified
    {
        get => _lastModified;
        set => SetProperty(ref _lastModified, value);
    }

    public string LastModifiedDisplay => LastModified.ToString("yyyy-MM-dd HH:mm");

    public bool IsDirectory
    {
        get => _isDirectory;
        set
        {
            if (SetProperty(ref _isDirectory, value))
            {
                TypeIcon = value ? "📁" : "📄";
                OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string StatusIcon
    {
        get => _statusIcon;
        set => SetProperty(ref _statusIcon, value);
    }

    public SvnItemState SvnState
    {
        get => _svnState;
        set
        {
            if (SetProperty(ref _svnState, value))
            {
                (StatusIcon, StatusText) = value switch
                {
                    SvnItemState.Added => ("🟢", "新增"),
                    SvnItemState.Modified => ("🟡", "已修改"),
                    SvnItemState.Deleted => ("🔴", "已删除"),
                    SvnItemState.Conflicted => ("⚠️", "冲突"),
                    SvnItemState.Unversioned => ("⬜", "未受控"),
                    _ => ("✅", "正常")
                };
            }
        }
    }

    /// <summary>从 SvnFileItemState 同步到 FileItemViewModel</summary>
    public void SyncFrom(SvnFileItemState state)
    {
        Name = state.Name;
        FullPath = state.FullPath;
        IsDirectory = state.IsFolder;
        FileSize = state.FileSize;
        LastModified = state.ModifyTime;
        SvnState = state.State;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}
