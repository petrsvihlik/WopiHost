using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Discovery;

namespace WopiHost.Core.Models;

/// <summary>
/// Configuration class for WopiHost.Core. Pure data — host-customization behavior lives on
/// <see cref="IWopiHostExtensions"/>; replace per-resource response shaping by registering
/// custom <see cref="ICheckFileInfoBuilder"/> / <see cref="ICheckContainerInfoBuilder"/> /
/// <see cref="ICheckFolderInfoBuilder"/> implementations.
/// </summary>
public class WopiHostOptions : IDiscoveryOptions
{
    /// <summary>
    /// Default configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(WopiHostOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi";

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
    /// Value written to the <c>X-WOPI-Lock</c> response header when the file is unlocked but the
    /// spec requires the header to be present (notably <c>GetLock</c> on an unlocked file and
    /// <c>PutFile</c> on a non-empty unlocked file).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WOPI spec mandates the empty string. This is the default and works under Kestrel, Linux
    /// IIS out-of-process hosting, and any reverse proxy (NGINX, Caddy, YARP) that preserves empty
    /// header values.
    /// </para>
    /// <para>
    /// Set to <c>" "</c> (a single space) when hosting under <em>IIS in-process</em>, which strips
    /// empty header values before they reach the wire (see issue #208). The space is technically
    /// non-spec but is the historic workaround and what every WOPI client treats as "no lock". Also
    /// useful as an escape hatch behind any other downstream component that drops empty headers.
    /// </para>
    /// </remarks>
    public string EmptyLockHeaderValue { get; set; } = string.Empty;

    /// <summary>
    /// Optional upper bound (in bytes) on the size of files accepted via <c>PutFile</c> and
    /// <c>PutRelativeFile</c>. When set, requests whose <c>Content-Length</c> or — for
    /// <c>PutRelativeFile</c> — declared <c>X-WOPI-Size</c> exceed this value short-circuit with
    /// <see cref="StatusCodes.Status413PayloadTooLarge"/> before any body is read. Defaults to
    /// <see langword="null"/> (no WOPI-level limit; the host's underlying server still applies
    /// its own request-size limits).
    /// </summary>
    /// <remarks>
    /// Returning 413 is explicitly listed as a valid response in the WOPI <c>PutFile</c> and
    /// <c>PutRelativeFile</c> specs (<i>"File is too large. The maximum file size is host-specific"</i>).
    /// </remarks>
    public long? MaxFileSize { get; set; }

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
