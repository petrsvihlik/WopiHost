using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides unique file identifiers for files.
/// </summary>
/// <remarks>very basic in-memory for sample purposes only</remarks>
public class InMemoryFileIds(ILogger<InMemoryFileIds> logger)
{
    private readonly Dictionary<string, string> fileIds = [];

    /// <summary>
    /// Gets a value indicating whether any files have been scanned.
    /// </summary>
    public bool WasScanned => fileIds.Count > 0;

    /// <summary>
    /// Gets the file identifier for the specified path.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileId"></param>
    /// <returns></returns>
    public bool TryGetFileId(string path, [NotNullWhen(true)] out string? fileId)
    {
        fileId = fileIds.FirstOrDefault(x => x.Value == path).Key;
        return fileId != null;
    }

    /// <summary>
    /// Gets the file identifier for the specified path.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? path)
    {
        return fileIds.TryGetValue(fileId, out path);
    }

    /// <summary>
    /// Gets the path for the specified file identifier.
    /// </summary>
    /// <param name="fileId"></param>
    /// <returns></returns>
    public string? GetPath(string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        if (TryGetPath(fileId, out var path))
        {
            return path;
        }
        return null;
    }

    /// <summary>
    /// Adds a file to the collection and returns its identifier.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public string AddFile(string path)
    {
        var id = NewId();
        fileIds[id] = path;
        return id;
    }

    /// <summary>
    /// Removes the specified file identifier.
    /// </summary>
    /// <param name="fileId"></param>
    public void RemoveId(string fileId)
    {
        fileIds.Remove(fileId);
    }

    /// <summary>
    /// Updates the file path for the specified file identifier.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="path"></param>
    public void UpdateFile(string id, string path)
    {
        fileIds[id] = path;
    }

    /// <summary>
    /// Scans all files and directories in the specified root path.
    /// </summary>
    /// <param name="rootPath"></param>
    public void ScanAll(string rootPath)
    {
        fileIds.Clear();

        fileIds[NewId()] = rootPath;

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
        {
            fileIds[NewId()] = directory;
        }

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var newId = file.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
                ? "WOPITEST"
                : NewId();
            fileIds[newId] = file;
        }

        logger.LogInformation("Scanned {total} items", fileIds.Count);
    }

    /// <summary>
    /// Creates a unique identifier.
    /// </summary>
    /// <returns></returns>
    private static string NewId() => Guid.NewGuid().ToString("N");
}
