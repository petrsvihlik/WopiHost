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
    /// Callback for the CheckFolderInfo operation.
    /// </summary>
    public Func<WopiCheckFolderInfoContext, Task<WopiCheckFolderInfo>> OnCheckFolderInfo { get; set; } = c => Task.FromResult(c.CheckFolderInfo);

    /// <summary>
    /// Callback for the CheckEcosystem operation.
    /// </summary>
    public Func<WopiCheckEcosystemContext, Task<WopiCheckEcosystem>> OnCheckEcosystem { get; set; } = c => Task.FromResult(c.CheckEcosystem);

    /// <summary>
    /// Callback raised after a successful <c>PutFile</c> write. Receives the updated file plus
    /// the user ids parsed from the optional <c>X-WOPI-Editors</c> request header. Use this for
    /// audit trails and last-touch metadata; the default is a no-op.
    /// </summary>
    /// <remarks>
    /// Throwing inside this callback turns the response into a 500. For best-effort bookkeeping,
    /// catch exceptions inside the handler.
    /// </remarks>
    public Func<WopiPutFileContext, Task> OnPutFile { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Callback raised after a successful <c>PutRelativeFile</c> write. Surfaces the optional
    /// <c>X-WOPI-FileConversion</c> (presence flag) and <c>X-WOPI-Size</c> headers alongside
    /// both the original and newly-created files. Hosts that want to flag conversion-context
    /// uploads or record declared-size telemetry plug in here; the default is a no-op.
    /// </summary>
    /// <remarks>
    /// Throwing inside this callback turns the response into a 500. For best-effort bookkeeping,
    /// catch exceptions inside the handler.
    /// </remarks>
    public Func<WopiPutRelativeFileContext, Task> OnPutRelativeFile { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Base URI of the WOPI Client server (Office Online Server / Office Web Apps).
    /// </summary>
    [Required]
    public required Uri ClientUrl { get; set; }

    /// <summary>
    /// Default file permissions used by <see cref="Security.Authorization.DefaultWopiPermissionProvider"/>
    /// when no <see cref="Abstractions.WopiClaimTypes.FilePermissions"/> claim is present on the
    /// principal (typically pre-issuance, when a host is computing what to bake into a token).
    /// </summary>
    /// <remarks>
    /// Replace <see cref="Abstractions.IWopiPermissionProvider"/> in DI to compute permissions
    /// from your own ACL store instead of using these defaults.
    /// </remarks>
    public WopiFilePermissions DefaultFilePermissions { get; set; } =
        WopiFilePermissions.UserCanWrite |
        WopiFilePermissions.UserCanRename |
        WopiFilePermissions.UserCanAttend |
        WopiFilePermissions.UserCanPresent;

    /// <summary>
    /// Default container permissions used by <see cref="Security.Authorization.DefaultWopiPermissionProvider"/>
    /// when no <see cref="Abstractions.WopiClaimTypes.ContainerPermissions"/> claim is present on
    /// the principal.
    /// </summary>
    public WopiContainerPermissions DefaultContainerPermissions { get; set; } =
        WopiContainerPermissions.UserCanCreateChildContainer |
        WopiContainerPermissions.UserCanCreateChildFile |
        WopiContainerPermissions.UserCanDelete |
        WopiContainerPermissions.UserCanRename;
}