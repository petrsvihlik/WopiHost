using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Validator.Infrastructure;

/// <summary>
/// Permissive proof validator used by the validator sample.
/// </summary>
/// <remarks>
/// <para>
/// The <see href="https://github.com/microsoft/wopi-validator-core">Microsoft WOPI validator</see>
/// is not an Office Online client and does not sign requests with the
/// <c>X-WOPI-Proof</c> / <c>X-WOPI-ProofOld</c> / <c>X-WOPI-TimeStamp</c> headers. Replacing
/// <see cref="IWopiProofValidator"/> with this no-op implementation lets the
/// validator hit this sample's WOPI endpoints. Production hosts must keep
/// the default <see cref="WopiProofValidator"/>.
/// </para>
/// <para>
/// As a side effect, three tests in the validator's ProofKeys group expect HTTP 500
/// (mutated current/old keys, ancient timestamp) but receive HTTP 200 because
/// this validator accepts every request. Those three are pinned as a
/// known-failure baseline in <c>.github/workflows/wopi-validator.yml</c> and
/// <c>scripts/run-validator.sh</c> so CI hard-fails only on regressions. Once upstream
/// <see href="https://github.com/microsoft/wopi-validator-core/pull/145">proof-key support</see>
/// ships in a WopiValidator NuGet release, the validator's public keys can be registered via
/// <c>IDiscoverer</c>, <see cref="WopiProofValidator"/> restored, and the pin retired.
/// </para>
/// </remarks>
public sealed class NoOpProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(WopiRequestInfo request, string accessToken) => Task.FromResult(true);
}
