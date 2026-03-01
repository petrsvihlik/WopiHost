using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
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
        var id = IdFromPath(path);
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

        fileIds[IdFromPath(rootPath)] = rootPath;

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
        {
            fileIds[IdFromPath(directory)] = directory;
        }

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var newId = file.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
                ? "WOPITEST"
                : IdFromPath(file);
            fileIds[newId] = file;
        }

        logger.LogInformation("Scanned {total} items", fileIds.Count);
    }

    /// <summary>
    /// Creates a deterministic identifier from a file path so that the same path always
    /// produces the same identifier, even across process restarts or separate services.
    /// </summary>
    private static string IdFromPath(string path) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(path).ToUpperInvariant()))).ToLowerInvariant();
}
