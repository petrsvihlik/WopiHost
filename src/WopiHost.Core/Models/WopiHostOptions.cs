using System.ComponentModel.DataAnnotations;

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
    [Required]
    public required string StorageProviderAssemblyName { get; set; }

    /// <summary>
    /// Name of the assembly containing the implementation of the <see cref="Abstractions.IWopiLockProvider"/>
    /// </summary>
    public string? LockProviderAssemblyName { get; set; }
}
