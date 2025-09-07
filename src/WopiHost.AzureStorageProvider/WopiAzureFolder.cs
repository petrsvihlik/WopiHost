using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Azure Blob Storage container implementation of <see cref="IWopiFolder"/>.
/// </summary>
/// <param name="containerName">Name of the Azure Blob Storage container</param>
/// <param name="folderIdentifier">Unique identifier of the folder</param>
/// <param name="blobPath">Path within the container (for subfolders)</param>
public class WopiAzureFolder(string containerName, string folderIdentifier, string? blobPath = null) : IWopiFolder
{
    /// <summary>
    /// Name of the Azure Blob Storage container.
    /// </summary>
    public string ContainerName { get; } = containerName ?? throw new ArgumentNullException(nameof(containerName));

    /// <summary>
    /// Path within the container for this folder.
    /// </summary>
    public string? BlobPath { get; } = blobPath;

    /// <inheritdoc/>
    public string Name
    {
        get
        {
            if (string.IsNullOrEmpty(BlobPath))
            {
                return ContainerName;
            }
            return Path.GetFileName(BlobPath.TrimEnd('/'));
        }
    }

    /// <inheritdoc/>
    public string Identifier { get; } = folderIdentifier ?? throw new ArgumentNullException(nameof(folderIdentifier));
}
