namespace WopiHost.Core.Models;

/// <summary>
/// Configuration class for WopiHost.Core
/// </summary>
public class WopiHostOptions
{
    /// <summary>
    /// Determines whether the MS-FSSHTTP should be enabled or not.
    /// </summary>
    public bool UseCobalt { get; set; }

    /// <summary>
    /// Name of the assembly containing the implementation of the <see cref="Abstractions.IWopiStorageProvider"/>.
    /// </summary>
    public string StorageProviderAssemblyName { get; set; }
}
