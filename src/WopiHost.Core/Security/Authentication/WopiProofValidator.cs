using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Discovery;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Service for validating WOPI proof headers to ensure requests come from a trusted WOPI client.
/// </summary>
/// <remarks>
/// Creates a new instance of the <see cref="WopiProofValidator"/> class.
/// </remarks>
/// <param name="discoverer">Service for retrieving WOPI discovery information, including proof keys.</param>
/// <param name="logger">Logger instance.</param>
/// <param name="timeProvider">Provider for time operations (defaults to system time if not provided).</param>
public partial class WopiProofValidator(IDiscoverer discoverer, ILogger<WopiProofValidator> logger, TimeProvider? timeProvider = null) : IWopiProofValidator
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Validates the WOPI proof headers on the given request.
    /// </summary>
    /// <param name="httpContext">The HTTP context to validate.</param>
    /// <param name="accessToken">The access token from the request.</param>
    /// <returns>True if the request's proof headers are valid, false otherwise.</returns>
    public async Task<bool> ValidateProofAsync(HttpContext httpContext, string accessToken)
    {
        try
        {
            var request = httpContext.Request;
            if (!request.Headers.TryGetValue(WopiHeaders.PROOF, out var receivedProof) ||
                !request.Headers.TryGetValue(WopiHeaders.TIMESTAMP, out var receivedTimeStamp) ||
                !long.TryParse(receivedTimeStamp.ToString(), CultureInfo.InvariantCulture, out var ticks))
            {
                LogProofHeadersMissing(logger);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", "missing_or_invalid_headers"));
                return false;
            }

            var sourceProofKeys = await discoverer.GetProofKeysAsync();

            // Per WOPI spec: X-WOPI-TimeStamp is .NET DateTime.Ticks (UTC).
            // Reject timestamps older than 20 minutes; allow a small future skew.
            var age = _timeProvider.GetUtcNow().UtcDateTime - new DateTime(ticks, DateTimeKind.Utc);
            if (sourceProofKeys.Value is null
                || sourceProofKeys.OldValue is null
                || age > TimeSpan.FromMinutes(20)
                || age < TimeSpan.FromMinutes(-5))
            {
                var reason = sourceProofKeys.Value is null || sourceProofKeys.OldValue is null
                    ? "missing_discovery_keys"
                    : "timestamp_outside_window";
                LogProofTimestampOrKeysInvalid(logger, age, reason);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", reason));
                return false;
            }

            var hostUrl = request.GetProxyAwareRequestUrl().ToUpperInvariant();
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

            var receivedProofOld = request.Headers[WopiHeaders.PROOF_OLD].FirstOrDefault() ?? string.Empty;
            var verified = VerifyProof(expectedBytes, receivedProof.ToString(), sourceProofKeys.Value)
                || VerifyProof(expectedBytes, receivedProof.ToString(), sourceProofKeys.OldValue)
                || VerifyProof(expectedBytes, receivedProofOld, sourceProofKeys.Value);
            if (!verified)
            {
                LogProofSignatureMismatch(logger);
                WopiTelemetry.ProofValidationFailures.Add(1,
                    new KeyValuePair<string, object?>("reason", "signature_mismatch"));
            }
            return verified;
        }
        catch (Exception ex)
        {
            LogProofValidationError(logger, ex);
            WopiTelemetry.ProofValidationFailures.Add(1,
                new KeyValuePair<string, object?>("reason", "exception"));
            return false;
        }
    }

    private static byte[] ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }

    private static bool VerifyProof(byte[] expectedProof, string proofFromRequest, string proofFromDiscovery)
    {
        using var rsaProvider = new RSACryptoServiceProvider();
        try
        {
            rsaProvider.ImportCspBlob(Convert.FromBase64String(proofFromDiscovery));
            return rsaProvider.VerifyData(expectedProof, "SHA256", Convert.FromBase64String(proofFromRequest));
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
} 