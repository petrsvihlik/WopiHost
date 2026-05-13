namespace WopiHost.FileSystemProvider;

/// <summary>
/// Configuration object for <see cref="WopiFileSystemProvider"/>.
/// </summary>
public class WopiFileSystemProviderOptions
{
    /// <summary>
    /// Default configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(WopiFileSystemProviderOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi:StorageProvider";

    /// <summary>
    /// File system path indicating the root of the folder hierarchy considered by the <see cref="WopiFileSystemProvider"/>.
    /// </summary>
    public required string RootPath { get; set; }
}
