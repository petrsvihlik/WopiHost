using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;
using WopiHost.Discovery;
using WopiHost.Discovery.Models;

namespace WopiHost.Core.Tests.Security.Authentication;

public class WopiProofValidatorTests
{
    private const string AccessToken = "test-access-token";
    private const string Scheme = "https";
    private const string Host = "wopi.example.com";
    private const string Path = "/wopi/files/abc123";
    private const string QueryString = "?access_token=test-access-token";

    private readonly Mock<IDiscoverer> _discoverer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Returns_false_when_proof_header_missing()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(includeProof: false);

        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_timestamp_header_missing()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(includeTimestamp: false);

        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_timestamp_is_not_a_number()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(timestampOverride: "not-a-number", proofOverride: "AAAA");

        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_discovery_returns_null_keys()
    {
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ReturnsAsync(new WopiProofKeys { Value = null, OldValue = null });
        var validator = CreateValidator();
        var ctx = BuildHttpContext(proofOverride: "AAAA");

        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_timestamp_is_older_than_20_minutes()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var staleTime = _time.GetUtcNow().UtcDateTime - TimeSpan.FromMinutes(21);
        var ctx = BuildHttpContext(timestampOverride: staleTime.Ticks.ToString(CultureInfo.InvariantCulture));
        SignAndApply(ctx.Request, current, staleTime.Ticks);

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_timestamp_is_too_far_in_the_future()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var futureTime = _time.GetUtcNow().UtcDateTime + TimeSpan.FromMinutes(10);
        var ctx = BuildHttpContext(timestampOverride: futureTime.Ticks.ToString(CultureInfo.InvariantCulture));
        SignAndApply(ctx.Request, current, futureTime.Ticks);

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_true_for_valid_signature_with_current_key()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));
        SignAndApply(ctx.Request, current, ticks);

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.True(result);
    }

    [Fact]
    public async Task Returns_true_for_proof_signed_with_old_key_during_rotation()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));
        // X-WOPI-Proof signed with the OLD key (validator tries Value then OldValue)
        SignAndApply(ctx.Request, old, ticks);

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.True(result);
    }

    [Fact]
    public async Task Returns_true_when_only_old_proof_header_matches_current_key()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));

        var canonical = BuildCanonicalProof(AccessToken, BuildExpectedHostUrl(), ticks);
        // X-WOPI-Proof bogus, but X-WOPI-ProofOld signed with current key
        ctx.Request.Headers[WopiHeaders.PROOF] = Convert.ToBase64String(new byte[256]);
        ctx.Request.Headers[WopiHeaders.PROOF_OLD] =
            Convert.ToBase64String(current.SignData(canonical, "SHA256"));

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.True(result);
    }

    [Fact]
    public async Task Returns_false_for_tampered_signature()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        using var attacker = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));
        // signed with a key the discoverer doesn't know about
        SignAndApply(ctx.Request, attacker, ticks);

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task Returns_false_when_discoverer_throws()
    {
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ThrowsAsync(new InvalidOperationException("discovery offline"));
        var validator = CreateValidator();
        var ctx = BuildHttpContext(proofOverride: "AAAA");

        var result = await validator.ValidateProofAsync(ctx, AccessToken);

        Assert.False(result);
    }

    private WopiProofValidator CreateValidator()
        => new(_discoverer.Object, NullLogger<WopiProofValidator>.Instance, _time);

    private void SetupDiscoveryWithRandomKeys()
    {
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);
    }

    private void SetupDiscovery(RSACryptoServiceProvider current, RSACryptoServiceProvider old)
    {
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ReturnsAsync(new WopiProofKeys
            {
                Value = Convert.ToBase64String(current.ExportCspBlob(false)),
                OldValue = Convert.ToBase64String(old.ExportCspBlob(false))
            });
    }

    private static DefaultHttpContext BuildHttpContext(
        bool includeProof = true,
        bool includeTimestamp = true,
        string? timestampOverride = null,
        string? proofOverride = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = Scheme;
        ctx.Request.Host = new HostString(Host);
        ctx.Request.Path = Path;
        ctx.Request.QueryString = new QueryString(QueryString);

        if (includeTimestamp)
        {
            ctx.Request.Headers[WopiHeaders.TIMESTAMP] =
                new StringValues(timestampOverride ?? DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        }
        if (includeProof)
        {
            ctx.Request.Headers[WopiHeaders.PROOF] = new StringValues(proofOverride ?? string.Empty);
        }
        return ctx;
    }

    private static string BuildExpectedHostUrl()
        => $"{Scheme}://{Host}{Path}{QueryString}".ToUpperInvariant();

    private static byte[] BuildCanonicalProof(string accessToken, string hostUrl, long ticks)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(accessToken);
        var hostBytes = Encoding.UTF8.GetBytes(hostUrl);
        var tsBytes = BitConverter.GetBytes(ticks);
        Array.Reverse(tsBytes);

        var buffer = new List<byte>(4 + tokenBytes.Length + 4 + hostBytes.Length + 4 + tsBytes.Length);
        buffer.AddRange(BigEndian(tokenBytes.Length));
        buffer.AddRange(tokenBytes);
        buffer.AddRange(BigEndian(hostBytes.Length));
        buffer.AddRange(hostBytes);
        buffer.AddRange(BigEndian(tsBytes.Length));
        buffer.AddRange(tsBytes);
        return [.. buffer];
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }

    private static void SignAndApply(HttpRequest request, RSACryptoServiceProvider signer, long ticks)
    {
        var canonical = BuildCanonicalProof(AccessToken, BuildExpectedHostUrl(), ticks);
        var signature = signer.SignData(canonical, "SHA256");
        request.Headers[WopiHeaders.PROOF] = Convert.ToBase64String(signature);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
