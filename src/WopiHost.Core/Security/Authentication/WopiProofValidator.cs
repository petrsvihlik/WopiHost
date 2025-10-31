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
public class WopiProofValidator(IDiscoverer discoverer, ILogger<WopiProofValidator> logger, TimeProvider? timeProvider = null) : IWopiProofValidator
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
                !request.Headers.TryGetValue(WopiHeaders.TIMESTAMP, out var receivedTimeStamp))
            {
                logger.LogWarning("Missing required proof headers");
                return false;
            }
            
            var sourceProofKeys = await discoverer.GetProofKeysAsync();
            if (sourceProofKeys.Value is null || sourceProofKeys.OldValue is null)
            {
                return false;
            }
            
            var receivedProofOld = request.Headers[WopiHeaders.PROOF_OLD].FirstOrDefault() ?? String.Empty;
            var receivedAccessToken = accessToken;
            
            if (DateTime.UtcNow - new DateTime(Convert.ToInt64(receivedTimeStamp)) > TimeSpan.FromMinutes(20))
            {
                return false;
            }

            var hostUrl = request.GetProxyAwareRequestUrl().ToUpperInvariant();
            
            var hostUrlBytes = Encoding.UTF8.GetBytes(hostUrl.ToUpperInvariant());
            var accessTokenBytes = Encoding.UTF8.GetBytes(receivedAccessToken);
            var timeStampBytes = BitConverter.GetBytes(Convert.ToInt64(receivedTimeStamp)).Reverse().ToArray();
            
            var expectedProof = new List<byte>(
                4 + accessTokenBytes.Length +
                4 + hostUrlBytes.Length +
                4 + timeStampBytes.Length);
            
            expectedProof.AddRange([.. BitConverter.GetBytes(accessTokenBytes.Length).Reverse()]);
            expectedProof.AddRange(accessTokenBytes);
            expectedProof.AddRange([.. BitConverter.GetBytes(hostUrlBytes.Length).Reverse()]);
            expectedProof.AddRange(hostUrlBytes);
            expectedProof.AddRange([.. BitConverter.GetBytes(timeStampBytes.Length).Reverse()]);
            expectedProof.AddRange(timeStampBytes);
            byte[] expectedBytes = [.. expectedProof];
            
            return (VerifyProof(expectedBytes, receivedProof.ToString(), sourceProofKeys.Value) ||
                    VerifyProof(expectedBytes, receivedProof.ToString(), sourceProofKeys.OldValue) ||
                    VerifyProof(expectedBytes, receivedProofOld, sourceProofKeys.Value));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating WOPI proof");
            return false;
        }
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