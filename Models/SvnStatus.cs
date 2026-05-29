namespace OneFileBox.Models;

public enum FileSvnStatus
{
    Normal,
    Modified,
    Added,
    Deleted,
    Conflicted,
    Unversioned,
    Missing,
    Replaced,
    Obstructed,
    External,
    Incomplete,
    Hidden,         // Used for ".." parent directory row
    TreeConflicted  // Tree conflict
}

public enum SvnAccept
{
    Working,    // svn resolve --accept working (local version)
    MineFull,   // svn resolve --accept mine-full (local, full overwrite)
    TheirsFull, // svn resolve --accept theirs-full (server, full overwrite)
    Base        // svn resolve --accept base (original base version)
}