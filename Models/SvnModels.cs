using System;

namespace OneFileBox_new.Models;

public enum SvnLogLevel { Info, Warn, Error }

public enum SvnOperateType { AddFile, DeleteFile, CleanLock, BatchCommit, BatchUpdate, CommitAll, UpdateRemote }

public enum SvnConflictResolveMode { UseRemote, UseLocal, KeepAll }

public enum SvnItemState
{
    Normal,
    Added,
    Modified,
    Deleted,
    Conflicted,
    Unversioned,
    None
}

public class SvnFileItemState
{
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public SvnItemState State { get; set; } = SvnItemState.Normal;
    public bool IsVersionControl { get; set; }
    public long FileSize { get; set; }
    public DateTime ModifyTime { get; set; }
}

public class SvnSyncRecordItem
{
    public string SyncPath { get; set; } = string.Empty;
    public string OperateName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime OperateTime { get; set; } = DateTime.Now;
}

public class SvnSyncTask
{
    public SvnOperateType OpType { get; set; }
    public string TargetPath { get; set; } = string.Empty;
}

public class SvnFileDiffInfo
{
    public string FullLocalPath { get; set; } = string.Empty;
    public string RelativeRemotePath { get; set; } = string.Empty;
    public long LocalRevision { get; set; }
    public long RemoteRevision { get; set; }
    public bool IsLocalMissing { get; set; }
    public bool NeedUpdate => IsLocalMissing || RemoteRevision > LocalRevision;
}

public class SvnExecuteResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorMsg { get; set; } = string.Empty;
    public bool UserCanceled { get; set; }
}

public class SvnProgressInfo
{
    public string CurrentFile { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long DoneBytes { get; set; }
    public double Percent { get; set; }
    public bool IsActiveWorking { get; set; }
    public string OperateName { get; set; } = string.Empty;
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    public DateTime TaskStartTime { get; set; } = DateTime.Now;
}

public class SvnLongTaskInfo
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public double CurrentPercent { get; set; }
    public long ElapsedMs { get; set; }
    public string OperateName { get; set; } = string.Empty;
}

public class SvnConflictFileInfo
{
    public string FullPath { get; set; } = string.Empty;
    public long LocalSize { get; set; }
    public DateTime LocalModifyTime { get; set; }
    public long RemoteSize { get; set; }
    public DateTime RemoteModifyTime { get; set; }
    public string ConflictType { get; set; } = string.Empty;
}

public class SvnBatchOperateProgress
{
    public string CurrentPath { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public double TotalProgress => TotalCount == 0 ? 0 : Math.Round(CompletedCount * 100.0 / TotalCount, 1);
    public string TipText { get; set; } = string.Empty;
    public bool IsFinished { get; set; }
    public bool IsError { get; set; }
}

public class RepoConfig
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string SvnUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
