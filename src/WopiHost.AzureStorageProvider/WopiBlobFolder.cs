using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Virtual folder backed by a blob-name prefix. Plain Blob Storage has no folder objects, so a folder
/// is just a logical grouping of blobs that share a <c>/</c>-delimited prefix.
/// </summary>
public class WopiBlobFolder(string prefix, string identifier) : IWopiFolder
{
    /// <summary>Blob-name prefix this folder represents (no leading or trailing slash).</summary>
    public string Prefix { get; } = prefix;

    /// <inheritdoc/>
    public string Identifier { get; } = identifier;

    /// <inheritdoc/>
    public string Name => string.IsNullOrEmpty(Prefix)
        ? string.Empty
        : Prefix[(Prefix.LastIndexOf('/') + 1)..];
}
