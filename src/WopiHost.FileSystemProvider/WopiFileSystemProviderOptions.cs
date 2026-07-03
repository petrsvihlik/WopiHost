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

    /// <summary>
    /// Whether to watch <see cref="RootPath"/> for changes made outside this process (defaults to
    /// <see langword="true"/>). The watcher keeps the id map converged proactively — in
    /// particular, a rename observed in another process repoints the file's existing identifier
    /// instead of minting a new one, so all processes over one tree share a single lock domain
    /// per file. Disable this on storage where change notifications are unreliable (network
    /// shares, some container bind mounts); the provider then falls back to lazy id registration
    /// plus an on-demand reconciliation sweep.
    /// </summary>
    public bool WatchForExternalChanges { get; set; } = true;
}
