using Microsoft.AspNetCore.Http;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Validator.Infrastructure;

/// <summary>
/// Permissive proof validator used by the validator sample.
/// </summary>
/// <remarks>
/// The Microsoft WOPI validator (https://github.com/microsoft/wopi-validator-core)
/// is not an Office Online client and does not sign requests with the
/// X-WOPI-Proof / X-WOPI-ProofOld / X-WOPI-TimeStamp headers. Replacing
/// <see cref="IWopiProofValidator"/> with this no-op implementation lets the
/// validator hit this sample's WOPI endpoints. Production hosts must keep
/// the default <see cref="WopiProofValidator"/>.
/// </remarks>
public sealed class NoOpProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(HttpContext httpContext, string accessToken) => Task.FromResult(true);
}
