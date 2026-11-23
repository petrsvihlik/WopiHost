namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Represents the configuration for <see cref="WopiAzureStorageProvider"/>.
/// </summary>
/// <remarks>
/// Bind to the <c>Wopi:StorageProvider</c> configuration section. Authentication is determined by which
/// fields are populated, in order of precedence:
/// <list type="number">
///   <item><description><see cref="ConnectionString"/> set → use it (typical for Azurite/dev or shared-key prod).</description></item>
///   <item><description><see cref="ServiceUri"/> set → use the <c>TokenCredential</c> registered in DI; fall back to
///   <see cref="Azure.Identity.DefaultAzureCredential"/> if none was supplied.</description></item>
/// </list>
/// </remarks>
public class WopiAzureStorageProviderOptions
{
    /// <summary>
    /// The configuration section path that <see cref="ServiceCollectionExtensions.AddAzureStorageProvider"/>
    /// binds these options from.
    /// </summary>
    public const string SectionName = "Wopi:StorageProvider";

    /// <summary>
    /// Gets or sets the storage account connection string. Use <c>UseDevelopmentStorage=true</c> for
    /// Azurite, or a full shared-key connection string for production. Takes precedence over
    /// <see cref="ServiceUri"/> when both are set.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the blob service endpoint (e.g. <c>https://my-account.blob.core.windows.net</c>) used together with a
    /// <see cref="Azure.Core.TokenCredential"/> from DI (typically <see cref="Azure.Identity.DefaultAzureCredential"/>)
    /// for managed-identity / service-principal auth.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Gets or sets the name of the blob container that holds WOPI files. Created on first use if missing.
    /// </summary>
    public required string ContainerName { get; set; }
}
