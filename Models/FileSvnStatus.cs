namespace OneFileBox.Models;

/// <summary>
/// SVN file status enumeration with emoji-friendly display names.
/// </summary>
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
    Unknown,
    Hidden  // Used for ".." parent directory row — hides the status badge
}
