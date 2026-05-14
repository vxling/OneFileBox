namespace OneFileBox.Models;

using System;

/// <summary>
/// SVN 工作副本中的文件/目录项
/// </summary>
public class FileItem
{
    /// <summary>相对工作副本根目录的路径</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>完整本地路径</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>是否为目录</summary>
    public bool IsDirectory { get; set; }

    /// <summary>SVN 状态</summary>
    public SvnStatus Status { get; set; } = SvnStatus.None;

    /// <summary>本地最后修改时间</summary>
    public DateTime LocalModified { get; set; }

    /// <summary>SVN 版本号（如果是版本化文件）</summary>
    public long Revision { get; set; }

    /// <summary>最后提交作者</summary>
    public string? Author { get; set; }

    /// <summary>最后提交时间</summary>
    public DateTime? CommittedDate;

    /// <summary>文件大小（字节）</summary>
    public long Size { get; set; }

    /// <summary>URL（从 SVN 列表获取）</summary>
    public string? Url { get; set; }

    /// <summary>锁信息</summary>
    public string? LockOwner { get; set; }
    public DateTime? LockDate;
    public bool IsLocked => !string.IsNullOrEmpty(LockOwner);
}

/// <summary>
/// SVN 文件状态枚举
/// </summary>
public enum SvnStatus
{
    None,
    Normal,
    Modified,
    Added,
    Deleted,
    Unversioned,
    Conflicted,
    Ignored,
    External,
    Incomplete,
    Merged,
}