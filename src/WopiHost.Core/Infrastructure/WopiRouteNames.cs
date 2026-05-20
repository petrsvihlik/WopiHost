using WopiHost.Core.Endpoints;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Named-route constants for the Minimal-API endpoints registered by
/// <see cref="WopiEndpointRouteBuilderExtensions.MapWopiEndpoints"/>. The strings are passed to
/// <c>.WithName(...)</c> at registration and consumed by callers that need to construct WOPI
/// URLs via <c>LinkGenerator</c>.
/// </summary>
public static class WopiRouteNames
{
    /// <summary>Endpoint name for the ecosystem capability probe (<c>GET /wopi/ecosystem</c>).</summary>
    public const string CheckEcosystem = nameof(CheckEcosystem);

    /// <summary>Endpoint name for the <c>CheckFileInfo</c> handler (<c>GET /wopi/files/{id}</c>).</summary>
    public const string CheckFileInfo = nameof(CheckFileInfo);

    /// <summary>Endpoint name for the <c>GetFile</c> handler (<c>GET /wopi/files/{id}/contents</c>).</summary>
    public const string GetFile = nameof(GetFile);

    /// <summary>Endpoint name for the <c>CheckContainerInfo</c> handler (<c>GET /wopi/containers/{id}</c>).</summary>
    public const string CheckContainerInfo = nameof(CheckContainerInfo);

    /// <summary>Endpoint name for the <c>CheckFolderInfo</c> handler (<c>GET /wopi/folders/{id}</c>).</summary>
    public const string CheckFolderInfo = nameof(CheckFolderInfo);
}
