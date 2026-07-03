namespace WopiHost.FileSystemProvider;

/// <summary>
/// The single enumeration policy for full-tree walks — the startup scan and the reconciliation
/// sweep share it so both walk the same set of entries.
/// </summary>
internal static class FileSystemEnumeration
{
    // Hidden and system entries are included: they carry WOPI content like any other file and the
    // startup scan has always registered them. Reparse points (junctions, symlinks) are skipped —
    // following them risks cycles during recursion, and entries outside the root must not become
    // addressable through a link planted inside it.
    private static readonly EnumerationOptions s_treeWalkOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>
    /// Yields <paramref name="rootPath"/> itself, then every file and directory beneath it.
    /// </summary>
    internal static IEnumerable<string> EnumerateTree(string rootPath)
    {
        yield return rootPath;
        foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath, "*", s_treeWalkOptions))
        {
            yield return entry;
        }
    }
}
