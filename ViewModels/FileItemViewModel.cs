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

    public string TypeDisplay
    {
        get
        {
            if (IsParentDirectory) return "";
            if (IsDirectory) return "📂";
            var ext = System.IO.Path.GetExtension(Name ?? "").ToLowerInvariant();
            return ext switch
            {
                ".cs" or ".fs" or ".vb" or ".java" or ".py" or ".go" or ".rs" or ".c" or ".cpp" or ".h" or ".hpp" => "💻",
                ".xlsx" or ".xls" or ".xlsm" or ".csv" => "📊",
                ".docx" or ".doc" or ".odt" or ".rtf" => "📝",
                ".pptx" or ".ppt" or ".odp" => "📽️",
                ".pdf" => "📕",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" or ".svg" or ".tiff" => "🖼️",
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => "🎵",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "📦",
                ".txt" or ".md" or ".log" or ".ini" or ".cfg" or ".conf" => "📄",
                ".json" or ".xml" or ".yaml" or ".yml" or ".toml" => "⚙️",
                ".html" or ".htm" or ".css" or ".js" or ".ts" or ".jsx" or ".tsx" or ".vue" or ".sass" or ".scss" => "🌐",
                ".exe" or ".msi" or ".dll" or ".sys" or ".bat" or ".cmd" or ".ps1" => "⚙️",
                _ => "📄"
            };
        }
    }

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