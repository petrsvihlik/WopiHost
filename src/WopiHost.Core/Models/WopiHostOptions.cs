using System.ComponentModel.DataAnnotations;
using WopiHost.Abstractions;
using WopiHost.Discovery;

namespace WopiHost.Core.Models;

/// <summary>
/// Configuration class for WopiHost.Core
/// </summary>
public class WopiHostOptions : IDiscoveryOptions
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

    /// <summary>
    /// Callback for the CheckFileInfo operation.
    /// </summary>
    public Func<WopiCheckFileInfoContext, Task<WopiCheckFileInfo>> OnCheckFileInfo { get; set; } = c => Task.FromResult(c.CheckFileInfo);

    /// <summary>
    /// Callback for the CheckContainerInfo operation.
    /// </summary>
    public Func<WopiCheckContainerInfoContext, Task<WopiCheckContainerInfo>> OnCheckContainerInfo { get; set; } = c => Task.FromResult(c.CheckContainerInfo);

    /// <summary>
    /// Base URI of the WOPI Client server (Office Online Server / Office Web Apps).
    /// </summary>
    [Required]
    public required Uri ClientUrl { get; set; }
}