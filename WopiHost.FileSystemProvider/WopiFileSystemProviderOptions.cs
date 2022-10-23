namespace WopiHost.FileSystemProvider;

/// <summary>
/// Configuration object for <see cref="WopiFileSystemProvider"/>.
/// </summary>
public class WopiFileSystemProviderOptions
{
    /// <summary>
    /// File system path indicating the root of the folder hierarchy considered by the <see cref="WopiFileSystemProvider"/>.
    /// </summary>
    public string RootPath { get; set; }
}
