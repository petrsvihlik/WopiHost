namespace WopiHost.AzureLockProvider;

/// <summary>
/// Represents the configuration for <see cref="WopiAzureLockProvider"/>.
/// </summary>
/// <remarks>
/// Bind to the <c>Wopi:LockProvider</c> configuration section. Authentication mirrors the storage
/// provider: prefer <see cref="ConnectionString"/> if set, otherwise <see cref="ServiceUri"/> +
/// <see cref="Azure.Core.TokenCredential"/> from DI (default: <see cref="Azure.Identity.DefaultAzureCredential"/>).
/// </remarks>
public class WopiAzureLockProviderOptions
{
    /// <summary>
    /// The configuration section path that <see cref="ServiceCollectionExtensions.AddAzureLockProvider"/>
    /// binds these options from.
    /// </summary>
    public const string SectionName = "Wopi:LockProvider";

    /// <summary>
    /// Gets or sets the storage account connection string. Takes precedence over <see cref="ServiceUri"/>
    /// when set. Use <c>UseDevelopmentStorage=true</c> for Azurite.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the blob service endpoint, used with a <see cref="Azure.Core.TokenCredential"/>
    /// when <see cref="ConnectionString"/> is not set.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Gets or sets the name of the blob container that holds the per-file lock placeholder blobs.
    /// Created on first use if missing. Defaults to <c>wopi-locks</c>. A dedicated container keeps
    /// locks isolated from content blobs and allows a separate storage account for lock state.
    /// </summary>
    public string ContainerName { get; set; } = "wopi-locks";
}
