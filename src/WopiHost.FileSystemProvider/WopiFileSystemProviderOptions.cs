namespace WopiHost.FileSystemProvider;

/// <summary>
/// Represents the configuration for <see cref="WopiFileSystemProvider"/>.
/// </summary>
public class WopiFileSystemProviderOptions
{
    /// <summary>
    /// The configuration section path that <see cref="ServiceCollectionExtensions.AddFileSystemStorageProvider"/>
    /// binds these options from.
    /// </summary>
    public const string SectionName = "Wopi:StorageProvider";

    /// <summary>
    /// Gets or sets the root of the folder hierarchy that <see cref="WopiFileSystemProvider"/> exposes.
    /// A relative path resolves against the host's content root.
    /// </summary>
    public required string RootPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to watch <see cref="RootPath"/> for changes made
    /// outside this process. Defaults to <see langword="true"/>. The watcher keeps the id map converged proactively — in
    /// particular, a rename observed in another process repoints the file's existing identifier
    /// instead of minting a new one, so all processes over one tree share a single lock domain
    /// per file. Disable this on storage where change notifications are unreliable (network
    /// shares, some container bind mounts); the provider then falls back to lazy id registration
    /// plus an on-demand reconciliation sweep.
    /// </summary>
    public bool WatchForExternalChanges { get; set; } = true;
}
