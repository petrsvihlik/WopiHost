using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Discovery;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the configuration of the WOPI host.
/// </summary>
/// <remarks>
/// Pure data. Host-customization behavior lives on <see cref="IWopiHostExtensions"/>; per-resource
/// response shaping is replaced by registering custom <see cref="ICheckFileInfoBuilder"/> /
/// <see cref="ICheckContainerInfoBuilder"/> / <see cref="ICheckFolderInfoBuilder"/> implementations.
/// </remarks>
public class WopiHostOptions : IDiscoveryOptions
{
    /// <summary>
    /// The default configuration section path this options class binds to, for use with
    /// <c>builder.Configuration.GetSection(WopiHostOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi";

    /// <summary>
    /// Gets or sets a value indicating whether MS-FSSHTTP (Cobalt) support is enabled.
    /// </summary>
    public bool UseCobalt { get; set; }

    /// <summary>
    /// Gets or sets the value written to the <c>X-WOPI-Lock</c> response header when the file is unlocked but the
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
    /// Set to <c>" "</c> (a single space) when hosting under <i>IIS in-process</i>, which strips
    /// empty header values before they reach the wire. The space is technically
    /// non-spec but is the established workaround and what every WOPI client treats as "no lock". It also
    /// helps behind any other downstream component that drops empty headers.
    /// </para>
    /// </remarks>
    public string EmptyLockHeaderValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional upper bound (in bytes) on the size of files accepted via <c>PutFile</c> and
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
    /// Gets or sets the absolute expiry of per-user <c>UserInfo</c> cache entries (stored by <c>PutUserInfo</c> and read
    /// back by <c>CheckFileInfo</c>). Defaults to 24 hours.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Total cache memory is bounded by <c>UserInfoCacheLifetime × distinct-active-users</c>.
    /// </para>
    /// <para>
    /// Shorten for memory-constrained hosts with many users; lengthen for hosts with stable
    /// user populations whose <c>UserInfo</c> should survive longer idle gaps.
    /// </para>
    /// </remarks>
    public TimeSpan UserInfoCacheLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the base URI of the WOPI client server (for example, Office Online Server).
    /// </summary>
    [Required]
    public required Uri ClientUrl { get; set; }

    /// <summary>
    /// Gets or sets the default file permissions used by <see cref="Security.Authorization.DefaultWopiPermissionProvider"/>
    /// when no <see cref="Abstractions.WopiClaimTypes.FilePermissions"/> claim is present on the
    /// principal (typically pre-issuance, when a host is computing what to bake into a token).
    /// </summary>
    /// <remarks>
    /// Replace <see cref="Abstractions.IWopiPermissionProvider"/> in DI to compute permissions
    /// from an ACL store instead of using these defaults.
    /// </remarks>
    public WopiFilePermissions DefaultFilePermissions { get; set; } =
        WopiFilePermissions.UserCanWrite |
        WopiFilePermissions.UserCanRename |
        WopiFilePermissions.UserCanAttend |
        WopiFilePermissions.UserCanPresent;

    /// <summary>
    /// Gets or sets the default container permissions used by <see cref="Security.Authorization.DefaultWopiPermissionProvider"/>
    /// when no <see cref="Abstractions.WopiClaimTypes.ContainerPermissions"/> claim is present on
    /// the principal.
    /// </summary>
    public WopiContainerPermissions DefaultContainerPermissions { get; set; } =
        WopiContainerPermissions.UserCanCreateChildContainer |
        WopiContainerPermissions.UserCanCreateChildFile |
        WopiContainerPermissions.UserCanDelete |
        WopiContainerPermissions.UserCanRename;
}
