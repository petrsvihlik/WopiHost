using System.Text.Json.Serialization;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the response body of the WOPI bootstrapper operations.
/// </summary>
/// <remarks>
/// <para>
/// Spec sample responses:
/// </para>
/// <list type="bullet">
///   <item><description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap#sample-response"/> — bare <c>Bootstrap</c></description></item>
///   <item><description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response"/> — <c>Bootstrap</c> + <c>RootContainerInfo</c></description></item>
///   <item><description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getnewaccesstoken#sample-response"/> — <c>Bootstrap</c> + <c>AccessTokenInfo</c></description></item>
/// </list>
/// <para>
/// The two operation-specific properties are omitted from the serialized JSON when <see langword="null"/>, so
/// the <c>GET /wopibootstrapper</c> response is the bare <c>{ "Bootstrap": {...} }</c> the
/// spec calls for.
/// </para>
/// </remarks>
public class BootstrapRootContainerInfo
{
    /// <summary>
    /// Gets or sets the object describing the root container. Populated for <c>GET_ROOT_CONTAINER</c> only.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RootContainerInfo? RootContainerInfo { get; set; }

    /// <summary>
    /// Gets or sets the object with the properties necessary for the bootstrap exchange.
    /// </summary>
    public BootstrapInfo? Bootstrap { get; set; }

    /// <summary>
    /// Gets or sets the new WOPI access token. Populated for <c>GET_NEW_ACCESS_TOKEN</c> only.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AccessTokenInfo? AccessTokenInfo { get; set; }
}
