namespace WopiHost.Abstractions;

/// <summary>
/// Model according to <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#wopi-host-capabilities-properties">WOPI host capabilities properties</see>
/// </summary>
public class WopiHostCapabilities : IWopiHostCapabilities
{
    /// <inheritdoc/>
    public bool SupportsCoauth { get; set; }

    /// <inheritdoc/>
    /// <remarks>set by FilesController.ctor depending on registered DI services</remarks>
    public bool SupportsCobalt { get; set; }

    /// <inheritdoc/>
    public bool SupportsFolders { get; set; } = true;

    /// <inheritdoc/>
    public bool SupportsContainers { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>set by FilesController.ctor depending on registered DI services</remarks>
    public bool SupportsLocks { get; set; }

    /// <inheritdoc/>
    /// <remarks>set by FilesController.ctor depending on registered DI services</remarks>
    public bool SupportsGetLock { get; set; }

    /// <inheritdoc/>
    public bool SupportsExtendedLockLength { get; set; } = true;

    /// <inheritdoc/>
    public bool SupportsEcosystem { get; set; } = true;

    /// <inheritdoc/>
    public bool SupportsGetFileWopiSrc { get; set; }

    /// <inheritdoc/>
    public IEnumerable<string> SupportedShareUrlTypes { get; set; } = [];

    /// <inheritdoc/>
    public bool SupportsScenarioLinks { get; set; }

    /// <inheritdoc/>
    public bool SupportsSecureStore { get; set; }

    /// <inheritdoc/>
    public bool SupportsFileCreation { get; set; }

    /// <inheritdoc/>
    public bool SupportsUpdate { get; set; } = true;

    /// <inheritdoc/>
    public bool SupportsRename { get; set; } = true;

    /// <inheritdoc/>
    public bool SupportsDeleteFile { get; set; } = true;

    /// <inheritdoc/>    
    public bool SupportsUserInfo { get; set; } = true;
}