using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Input to <see cref="IWopiNewChildFileNegotiator.NegotiateAsync"/>. Captures the WOPI
/// PutRelativeFile / CreateChildFile request inputs as a near-POCO — only the authenticated
/// <see cref="ClaimsPrincipal"/> is carried (needed for per-operation authorization decisions
/// like <see cref="IWopiPermissionProvider.CanOverwriteFileAsync"/>); no HTTP context, no
/// ambient request state.
/// </summary>
/// <param name="ContainerId">
/// Parent container id. For PutRelativeFile pass the resolved parent-of-file container id;
/// for CreateChildFile pass the route container id.
/// </param>
/// <param name="SuggestedTarget">
/// Value of the <c>X-WOPI-SuggestedTarget</c> header (decoded) — either a file extension
/// or a full file name. Mutually exclusive with <paramref name="RelativeTarget"/>.
/// </param>
/// <param name="RelativeTarget">
/// Value of the <c>X-WOPI-RelativeTarget</c> header (decoded) — a full file name the host
/// must use verbatim. Mutually exclusive with <paramref name="SuggestedTarget"/>.
/// </param>
/// <param name="OverwriteRelativeTarget">
/// Value of the <c>X-WOPI-OverwriteRelativeTarget</c> header. Defaults to <see langword="false"/>.
/// Only meaningful when <paramref name="RelativeTarget"/> is set.
/// </param>
/// <param name="SuggestedExtensionFallbackStem">
/// Stem to prepend when <paramref name="SuggestedTarget"/> begins with a dot (extension-only
/// fallback). PutRelativeFile passes the original file's name; CreateChildFile has no
/// source file in scope so passes a fresh GUID. The only spec-mandated difference between
/// the two flows.
/// </param>
/// <param name="User">
/// The authenticated principal making the request. Carried into the request so the negotiator
/// can consult <see cref="IWopiPermissionProvider.CanOverwriteFileAsync"/> when the caller
/// asked to overwrite an existing target — that authorization decision is per-operation and
/// per-existing-file, so it can't be encoded into the access token at mint time.
/// </param>
/// <remarks>
/// Both target parameters are plain <see langword="string"/>s; controllers binding the
/// <c>UtfString</c>-typed WOPI headers can pass them in via the implicit
/// <c>UtfString → string?</c> conversion so the negotiator interface stays free of the
/// header-encoding wrapper type.
/// </remarks>
public sealed record WopiNewChildFileRequest(
    string ContainerId,
    string? SuggestedTarget,
    string? RelativeTarget,
    bool OverwriteRelativeTarget,
    string SuggestedExtensionFallbackStem,
    ClaimsPrincipal User);
