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
    /// TTL applied to per-user UserInfo cache entries (stored by <c>PutUserInfo</c> and read
    /// back by <c>CheckFileInfo</c>). Defaults to 24 hours.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolves #425 item 2.7: pre-fix the entries were stored with
    /// <see cref="Microsoft.Extensions.Caching.Memory.CacheItemPriority.NeverRemove"/> and no
    /// expiry, pinning per-user data indefinitely — unbounded memory in any host that sees
    /// many distinct users. With an absolute expiry, total memory is bounded by
    /// <c>UserInfoCacheLifetime × distinct-active-users</c>.
    /// </para>
    /// <para>
    /// Tighten for memory-constrained hosts with many users; extend for hosts with stable
    /// user populations where users would prefer their <c>UserInfo</c> to survive longer
    /// idle gaps. 24 hours is a balance: long enough that mid-session re-entry isn't a
    /// concern, short enough that an idle user's row eventually falls off.
    /// </para>
    /// </remarks>
    public TimeSpan UserInfoCacheLifetime { get; set; } = TimeSpan.FromHours(24);

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
