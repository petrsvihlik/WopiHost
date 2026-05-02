namespace WopiHost.AzureLockProvider;

/// <summary>
/// Configuration for <see cref="WopiAzureLockProvider"/>.
/// </summary>
/// <remarks>
/// Bind to the <c>Wopi:LockProvider</c> configuration section. Authentication mirrors the storage
/// provider: prefer <see cref="ConnectionString"/> if set, otherwise <see cref="ServiceUri"/> +
/// <see cref="Azure.Core.TokenCredential"/> from DI (default: <see cref="Azure.Identity.DefaultAzureCredential"/>).
/// </remarks>
public class WopiAzureLockProviderOptions
{
    /// <summary>Storage account connection string. Use <c>UseDevelopmentStorage=true</c> for Azurite.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Blob service endpoint when using <see cref="Azure.Core.TokenCredential"/>.</summary>
    public string? ServiceUri { get; set; }

    /// <summary>
    /// Name of the blob container that holds the per-fileId lock placeholder blobs. Will be created
    /// on first use if missing. A dedicated container is recommended so locks are isolated from
    /// content blobs (and so a different storage account can be used for lock state if desired).
    /// </summary>
    public string ContainerName { get; set; } = "wopi-locks";
}
