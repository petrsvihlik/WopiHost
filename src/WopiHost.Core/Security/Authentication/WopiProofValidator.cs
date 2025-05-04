using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Infrastructure;
using WopiHost.Discovery;
using WopiHost.Discovery.Models;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Service for validating WOPI proof headers to ensure requests come from a trusted WOPI client.
/// </summary>
public class WopiProofValidator : IWopiProofValidator
{
    private readonly IDiscoverer _discoverer;
    private readonly ILogger<WopiProofValidator> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new instance of the <see cref="WopiProofValidator"/> class.
    /// </summary>
    /// <param name="discoverer">Service for retrieving WOPI discovery information, including proof keys.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Provider for time operations (defaults to system time if not provided).</param>
    public WopiProofValidator(IDiscoverer discoverer, ILogger<WopiProofValidator> logger, TimeProvider? timeProvider = null)
    {
        _discoverer = discoverer;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Validates the WOPI proof headers on the given request.
    /// </summary>
    /// <param name="request">The HTTP request to validate.</param>
    /// <param name="accessToken">The access token from the request.</param>
    /// <returns>True if the request's proof headers are valid, false otherwise.</returns>
    public async Task<bool> ValidateProofAsync(HttpRequest request, string accessToken)
    {
        try
        {
            if (!request.Headers.TryGetValue(WopiHeaders.PROOF, out var proofHeader) ||
                !request.Headers.TryGetValue(WopiHeaders.TIMESTAMP, out var timestampHeader))
            {
                _logger.LogWarning("Missing required proof headers");
                return false;
            }

            // Get the proof keys from discovery
            var proofKeys = await _discoverer.GetProofKeysAsync();
            var currentProofKey = GetCurrentKey(proofKeys);
            var oldProofKey = GetOldKey(proofKeys);

            if (currentProofKey == null)
            {
                _logger.LogWarning("Failed to retrieve proof keys from discovery");
                return false;
            }

            // Get the proof headers
            var proof = proofHeader.ToString();
            var proofOld = request.Headers.TryGetValue(WopiHeaders.PROOF_OLD, out var proofOldHeader)
                ? proofOldHeader.ToString()
                : null;
            var timestamp = timestampHeader.ToString();

            // Check if the timestamp is too old (> 20 minutes)
            if (!ValidateTimestamp(timestamp))
            {
                _logger.LogWarning("Timestamp is too old or invalid");
                return false;
            }

            // Build the expected proof for validation
            var expectedProof = BuildExpectedProof(request, accessToken, timestamp);

            // Try all valid combinations of proofs and keys
            if (VerifyProof(expectedProof, proof, currentProofKey) ||
                (proofOld != null && VerifyProof(expectedProof, proofOld, currentProofKey)) ||
                (oldProofKey != null && VerifyProof(expectedProof, proof, oldProofKey)))
            {
                return true;
            }

            _logger.LogWarning("All proof validations failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating WOPI proof");
            return false;
        }
    }

    internal bool ValidateTimestamp(string timestamp)
    {
        if (!long.TryParse(timestamp, out var timestampValue))
        {
            return false;
        }

        // Convert timestamp to DateTimeOffset (WOPI timestamp is in ticks)
        var timestampDate = DateTimeOffset.FromUnixTimeMilliseconds(timestampValue);
        
        // Use the time provider to get the current time
        var now = _timeProvider.GetUtcNow();
        
        // Check if timestamp is no more than 20 minutes old (inclusive)
        return now.Subtract(timestampDate).TotalMinutes <= 20;
    }

    private byte[] BuildExpectedProof(HttpRequest request, string accessToken, string timestamp)
    {
        var accessTokenBytes = Encoding.UTF8.GetBytes(accessToken);
        var hostUrl = GetRequestUrl(request).ToUpperInvariant();
        var hostUrlBytes = Encoding.UTF8.GetBytes(hostUrl);
        var timestampBytes = BitConverter.GetBytes(long.Parse(timestamp));

        using var ms = new MemoryStream();
        
        // Write access token length and data
        WriteInt32ToStream(ms, accessTokenBytes.Length);
        ms.Write(accessTokenBytes, 0, accessTokenBytes.Length);

        // Write host URL length and data
        WriteInt32ToStream(ms, hostUrlBytes.Length);
        ms.Write(hostUrlBytes, 0, hostUrlBytes.Length);

        // Write timestamp length and data
        WriteInt32ToStream(ms, timestampBytes.Length);
        ms.Write(timestampBytes, 0, timestampBytes.Length);

        return ms.ToArray();
    }

    private void WriteInt32ToStream(Stream stream, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        // Ensure little-endian byte order
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, bytes.Length);
    }

    private string GetRequestUrl(HttpRequest request)
    {
        var scheme = request.Headers.ContainsKey("X-Forwarded-Proto") 
            ? request.Headers["X-Forwarded-Proto"].ToString() 
            : request.Scheme;
        
        var host = request.Headers.ContainsKey("X-Forwarded-Host") 
            ? request.Headers["X-Forwarded-Host"].ToString() 
            : request.Host.Value;
        
        var pathBase = request.Headers.ContainsKey("X-Forwarded-PathBase") 
            ? request.Headers["X-Forwarded-PathBase"].ToString() 
            : request.PathBase.Value;
        
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;
        
        return $"{scheme}://{host}{pathBase}{path}{queryString}";
    }

    private bool VerifyProof(byte[] expectedProof, string signedProof, RSA publicKey)
    {
        try
        {
            var signedProofBytes = Convert.FromBase64String(signedProof);
            return publicKey.VerifyData(expectedProof, signedProofBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying proof signature");
            return false;
        }
    }

    private RSA? GetCurrentKey(WopiProofKeys proofKeys)
    {
        try
        {
            // For modulus/exponent approach (more portable)
            if (!string.IsNullOrEmpty(proofKeys.Modulus) && !string.IsNullOrEmpty(proofKeys.Exponent))
            {
                var rsa = RSA.Create();
                var modulusBytes = Convert.FromBase64String(proofKeys.Modulus);
                var exponentBytes = Convert.FromBase64String(proofKeys.Exponent);
                
                var rsaParameters = new RSAParameters
                {
                    Modulus = modulusBytes,
                    Exponent = exponentBytes
                };
                
                rsa.ImportParameters(rsaParameters);
                return rsa;
            }

            // Try to get .NET specific key format
            if (!string.IsNullOrEmpty(proofKeys.Value))
            {
                // We'll try to use the most compatible approach
                try
                {
                    var rsa = RSA.Create();
                    // Different frameworks have different methods
                    var keyBytes = Convert.FromBase64String(proofKeys.Value);
                    rsa.ImportRSAPublicKey(keyBytes, out _);
                    return rsa;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import key using ImportRSAPublicKey, falling back to parameters if possible");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current proof key");
            return null;
        }
    }

    private RSA? GetOldKey(WopiProofKeys proofKeys)
    {
        try
        {
            // For modulus/exponent approach (more portable)
            if (!string.IsNullOrEmpty(proofKeys.OldModulus) && !string.IsNullOrEmpty(proofKeys.OldExponent))
            {
                var rsa = RSA.Create();
                var modulusBytes = Convert.FromBase64String(proofKeys.OldModulus);
                var exponentBytes = Convert.FromBase64String(proofKeys.OldExponent);
                
                var rsaParameters = new RSAParameters
                {
                    Modulus = modulusBytes,
                    Exponent = exponentBytes
                };
                
                rsa.ImportParameters(rsaParameters);
                return rsa;
            }

            // Try to get .NET specific key format
            if (!string.IsNullOrEmpty(proofKeys.OldValue))
            {
                // We'll try to use the most compatible approach
                try
                {
                    var rsa = RSA.Create();
                    // Different frameworks have different methods
                    var keyBytes = Convert.FromBase64String(proofKeys.OldValue);
                    rsa.ImportRSAPublicKey(keyBytes, out _);
                    return rsa;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import key using ImportRSAPublicKey, falling back to parameters if possible");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving old proof key");
            return null;
        }
    }
} 