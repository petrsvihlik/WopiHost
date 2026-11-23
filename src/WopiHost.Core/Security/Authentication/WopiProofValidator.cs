using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Discovery;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Provides the default <see cref="IWopiProofValidator"/>, which verifies WOPI proof headers against
/// the proof keys from WOPI discovery.
/// </summary>
/// <param name="discoverer">The discovery service that supplies the current and old proof keys.</param>
/// <param name="logger">The logger.</param>
/// <param name="timeProvider">The clock for timestamp checks, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
/// <remarks>
/// Requests whose <c>X-WOPI-TimeStamp</c> is more than 20 minutes old or more than 5 minutes in the
/// future are rejected. Any non-fatal exception during validation fails closed (returns <see langword="false"/>).
/// </remarks>
public partial class WopiProofValidator(IDiscoverer discoverer, ILogger<WopiProofValidator> logger, TimeProvider? timeProvider = null) : IWopiProofValidator
{
    /// <summary>
    /// The maximum age, in minutes, of a request timestamp before it is rejected as stale, per the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts/proof-keys">proof-keys spec</see>.
    /// </summary>
    private const int MaxTimestampAgeMinutes = 20;

    /// <summary>
    /// The maximum tolerated future skew, in minutes, of a request timestamp, to absorb drift between
    /// the WOPI client's clock and the host's clock.
    /// </summary>
    private const int MaxFutureSkewMinutes = 5;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<bool> ValidateProofAsync(WopiRequestInfo request, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var receivedProof = request.GetHeader(WopiHeaders.Proof);
            var receivedTimeStamp = request.GetHeader(WopiHeaders.Timestamp);
            if (string.IsNullOrEmpty(receivedProof)
                || string.IsNullOrEmpty(receivedTimeStamp)
                || !long.TryParse(receivedTimeStamp, CultureInfo.InvariantCulture, out var ticks))
            {
                LogProofHeadersMissing(logger);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", "missing_or_invalid_headers"));
                return false;
            }

            var sourceProofKeys = await discoverer.GetProofKeysAsync().ConfigureAwait(false);

            // Per WOPI spec: X-WOPI-TimeStamp is .NET DateTime.Ticks (UTC).
            // Reject stale timestamps; allow a small future skew.
            var age = _timeProvider.GetUtcNow().UtcDateTime - new DateTime(ticks, DateTimeKind.Utc);
            if (sourceProofKeys.Value is null
                || sourceProofKeys.OldValue is null
                || age > TimeSpan.FromMinutes(MaxTimestampAgeMinutes)
                || age < TimeSpan.FromMinutes(-MaxFutureSkewMinutes))
            {
                var reason = sourceProofKeys.Value is null || sourceProofKeys.OldValue is null
                    ? "missing_discovery_keys"
                    : "timestamp_outside_window";
                LogProofTimestampOrKeysInvalid(logger, age, reason);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", reason));
                return false;
            }

            // WOPI spec signs the EXACT URL the client called, case-sensitive on path/query.
            // request.RequestUrl comes from the proxy-aware reconstruction (X-Forwarded-Proto /
            // X-Forwarded-Host honoured); .OriginalString preserves it byte-for-byte. The
            // .ToUpperInvariant() that follows matches WOPI's signature contract — case-folded
            // uniformly across host case quirks.
            //
            // Null RequestUrl: the adapter couldn't reconstruct a usable URL (synthesised
            // context, blank scheme/host). Without a URL there's nothing to sign against —
            // fail validation.
            if (request.RequestUrl is null)
            {
                LogProofHeadersMissing(logger);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", "missing_request_url"));
                return false;
            }
            var hostUrl = request.RequestUrl.OriginalString.ToUpperInvariant();
            var hostUrlBytes = Encoding.UTF8.GetBytes(hostUrl);
            var accessTokenBytes = Encoding.UTF8.GetBytes(accessToken);
            var timeStampBytes = BitConverter.GetBytes(ticks);
            Array.Reverse(timeStampBytes);

            var expectedProof = new List<byte>(
                4 + accessTokenBytes.Length +
                4 + hostUrlBytes.Length +
                4 + timeStampBytes.Length);

            expectedProof.AddRange(ToBigEndian(accessTokenBytes.Length));
            expectedProof.AddRange(accessTokenBytes);
            expectedProof.AddRange(ToBigEndian(hostUrlBytes.Length));
            expectedProof.AddRange(hostUrlBytes);
            expectedProof.AddRange(ToBigEndian(timeStampBytes.Length));
            expectedProof.AddRange(timeStampBytes);
            byte[] expectedBytes = [.. expectedProof];

            var receivedProofOld = request.GetHeader(WopiHeaders.ProofOld) ?? string.Empty;
            var verified = VerifyProof(expectedBytes, receivedProof, sourceProofKeys.Value)
                || VerifyProof(expectedBytes, receivedProof, sourceProofKeys.OldValue)
                || VerifyProof(expectedBytes, receivedProofOld, sourceProofKeys.Value);
            if (!verified)
            {
                LogProofSignatureMismatch(logger);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", "signature_mismatch"));
            }
            return verified;
        }
        // Filter critical async-rude exceptions out of the fail-closed catch: swallowing an
        // OutOfMemoryException / StackOverflowException / ThreadAbortException to return false
        // hides a process-level fault and lets the request continue against a host that's
        // already torn. Let those bubble. Any other Exception is treated as "proof validation
        // failed" — fail-closed is the right default for a security gate.
        catch (Exception ex) when (!IsCriticalUnwindException(ex))
        {
            LogProofValidationError(logger, ex);
            WopiTelemetry.ProofValidationFailures.Add(1,
                new KeyValuePair<string, object?>("reason", "exception"));
            return false;
        }
    }

    /// <summary>
    /// Determines whether <paramref name="ex"/> is a process-fatal exception that must not be coerced
    /// into "validation failed".
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="ex"/> must be rethrown; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Catching these would mask a torn process state and let the request continue against a broken host.
    /// </remarks>
    private static bool IsCriticalUnwindException(Exception ex) => ex is
        OutOfMemoryException or
        StackOverflowException or
        ThreadAbortException;

    private static byte[] ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }

    private bool VerifyProof(byte[] expectedProof, string proofFromRequest, string proofFromDiscovery)
    {
        using var rsaProvider = new RSACryptoServiceProvider();
        try
        {
            rsaProvider.ImportCspBlob(Convert.FromBase64String(proofFromDiscovery));
            return rsaProvider.VerifyData(expectedProof, "SHA256", Convert.FromBase64String(proofFromRequest));
        }
        // The original catches stayed silent — a malformed Base64 input or a busted CSP blob
        // looked identical to a normal "signature didn't match" mismatch in the logs, so a
        // misconfigured discovery key (e.g. truncated, swapped) was indistinguishable from a
        // legitimate spoofing attempt. Debug-level structured logging surfaces the distinction
        // without leaking the key material in production INFO logs.
        catch (FormatException ex)
        {
            LogProofVerifyMalformedBase64(logger, ex);
            return false;
        }
        catch (CryptographicException ex)
        {
            LogProofVerifyCryptoFailure(logger, ex);
            return false;
        }
    }
}