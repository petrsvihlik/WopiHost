namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Configuration object for <see cref="WopiAzureStorageProvider"/>.
/// </summary>
public class WopiAzureStorageProviderOptions
{
    /// <summary>
    /// Azure Storage connection string. Required if UseManagedIdentity is false.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure Storage account name. Required if UseManagedIdentity is true.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Azure Storage account key. Optional if using connection string or managed identity.
    /// </summary>
    public string? AccountKey { get; set; }

    /// <summary>
    /// Name of the Azure Blob Storage container to use as the root container.
    /// </summary>
    public required string ContainerName { get; set; }

    /// <summary>
    /// Optional root path within the container to use as the base folder.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Whether to use Azure Managed Identity for authentication.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = false;

    /// <summary>
    /// Maximum length for file names. Defaults to 250 characters.
    /// </summary>
    public int FileNameMaxLength { get; set; } = 250;

    /// <summary>
    /// Whether to create the container if it doesn't exist.
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <summary>
    /// Public access level for the container if it needs to be created.
    /// </summary>
    public Azure.Storage.Blobs.Models.PublicAccessType ContainerPublicAccess { get; set; } = Azure.Storage.Blobs.Models.PublicAccessType.None;
}
