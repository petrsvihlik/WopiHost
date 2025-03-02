using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WopiHost.Abstractions;

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

    /// <summary>
    /// Callback for the CheckFileInfo operation.
    /// </summary>
    public Func<WopiCheckFileInfoContext, Task<WopiCheckFileInfo>> OnCheckFileInfo { get; set; } = c => Task.FromResult(c.CheckFileInfo);
}

/// <summary>
/// Context for the CheckFileInfo operation.
/// </summary>
/// <param name="User">the current user.</param>
/// <param name="File">the current file resource.</param>
/// <param name="CheckFileInfo">the default created <see cref="WopiCheckFileInfo"/></param>
public record WopiCheckFileInfoContext(ClaimsPrincipal? User, IWopiFile File, WopiCheckFileInfo CheckFileInfo);