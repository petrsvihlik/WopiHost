using Microsoft.AspNetCore.Http;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Validator.Infrastructure;

/// <summary>
/// Permissive proof validator used by the validator sample.
/// </summary>
/// <remarks>
/// <para>
/// The Microsoft WOPI validator (https://github.com/microsoft/wopi-validator-core)
/// is not an Office Online client and does not sign requests with the
/// X-WOPI-Proof / X-WOPI-ProofOld / X-WOPI-TimeStamp headers. Replacing
/// <see cref="IWopiProofValidator"/> with this no-op implementation lets the
/// validator hit this sample's WOPI endpoints. Production hosts must keep
/// the default <see cref="WopiProofValidator"/>.
/// </para>
/// <para>
/// Side-effect: three tests in the validator's ProofKeys group expect HTTP 500
/// (mutated current/old keys, ancient timestamp) but receive HTTP 200 because
/// this validator accepts every request. Those three are pinned as a
/// known-failure baseline in <c>.github/workflows/wopi-validator.yml</c> and
/// <c>scripts/run-validator.sh</c> so CI hard-fails only on regressions. The
/// upstream tracker is https://github.com/microsoft/wopi-validator-core/pull/145
/// (rebase of long-stalled https://github.com/microsoft/wopi-validator-core/pull/86) —
/// once that lands and a new WopiValidator NuGet release ships, register the validator's
/// public keys via <c>IDiscoverer</c> and switch back to
/// <see cref="WopiProofValidator"/>; the pin can then be retired.
/// </para>
/// </remarks>
public sealed class NoOpProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(HttpContext httpContext, string accessToken) => Task.FromResult(true);
}
