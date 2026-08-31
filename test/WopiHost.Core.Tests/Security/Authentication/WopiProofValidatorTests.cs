using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using WopiHost.Abstractions.Testing;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Tests.Infrastructure;
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

    // The default request timestamp derives from the same instant, so a negative test rejects
    // only on the branch it targets — a wall-clock default would trip the staleness window and
    // mask the branch under test (the validator ORs its rejection reasons).
    private static readonly DateTimeOffset s_now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IDiscoverer> _discoverer = new();
    private readonly FixedTimeProvider _time = new(s_now);

    [Fact]
    public async Task Returns_false_when_proof_header_missing()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(includeProof: false);
        using var failures = CaptureFailures();

        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("missing_or_invalid_headers", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_timestamp_header_missing()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(includeTimestamp: false);
        using var failures = CaptureFailures();

        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("missing_or_invalid_headers", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_timestamp_is_not_a_number()
    {
        SetupDiscoveryWithRandomKeys();
        var validator = CreateValidator();
        var ctx = BuildHttpContext(timestampOverride: "not-a-number", proofOverride: "AAAA");
        using var failures = CaptureFailures();

        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("missing_or_invalid_headers", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_discovery_returns_null_keys()
    {
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ReturnsAsync(new WopiProofKeys { Value = null, OldValue = null });
        var validator = CreateValidator();
        var ctx = BuildHttpContext(proofOverride: "AAAA");
        using var failures = CaptureFailures();

        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("missing_discovery_keys", SingleFailureReason(failures));
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
        using var failures = CaptureFailures();

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("timestamp_outside_window", SingleFailureReason(failures));
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
        using var failures = CaptureFailures();

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("timestamp_outside_window", SingleFailureReason(failures));
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
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

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
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

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

        var canonical = WopiProofPayload.Build(AccessToken, BuildExpectedHostUrl(), ticks);
        // X-WOPI-Proof bogus, but X-WOPI-ProofOld signed with current key
        ctx.Request.Headers[WopiHeaders.Proof] = Convert.ToBase64String(new byte[256]);
        ctx.Request.Headers[WopiHeaders.ProofOld] =
            Convert.ToBase64String(current.SignData(canonical, "SHA256"));

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

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
        using var failures = CaptureFailures();

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("signature_mismatch", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_discoverer_throws()
    {
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ThrowsAsync(new InvalidOperationException("discovery offline"));
        var validator = CreateValidator();
        var ctx = BuildHttpContext(proofOverride: "AAAA");
        using var failures = CaptureFailures();

        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("exception", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_request_proof_is_not_base64()
    {
        // VerifyProof Convert.FromBase64String's the request-side proof. Non-base64 input
        // throws FormatException, which the validator's defensive catch maps to "not valid".
        // Hosts must not 500 on malformed headers — just reject the request.
        using var current = new RSACryptoServiceProvider(2048);
        using var old = new RSACryptoServiceProvider(2048);
        SetupDiscovery(current, old);

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));
        // Embedded space + '@' is not valid base64 → FormatException inside VerifyProof.
        ctx.Request.Headers[WopiHeaders.Proof] = "not valid base64@@@@";
        using var failures = CaptureFailures();

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("signature_mismatch", SingleFailureReason(failures));
    }

    [Fact]
    public async Task Returns_false_when_discovery_key_is_garbage_csp_blob()
    {
        // VerifyProof calls ImportCspBlob on the discovery key. A blob that starts like a real
        // PUBLICKEYBLOB (bType 0x06, RSA1 magic) but is truncated raises CryptographicException
        // on every platform; the defensive catch maps it to "not valid". Covers the second catch
        // arm of VerifyProof. (An all-zero blob is no good here: its unknown bType raises
        // PlatformNotSupportedException on Linux, which escapes VerifyProof's catches into the
        // validator's outer catch — a different branch, reason "exception".)
        byte[] truncatedCspBlob = [0x06, 0x02, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x52, 0x53, 0x41, 0x31];
        _discoverer
            .Setup(d => d.GetProofKeysAsync())
            .ReturnsAsync(new WopiProofKeys
            {
                Value = Convert.ToBase64String(truncatedCspBlob),
                OldValue = Convert.ToBase64String(truncatedCspBlob),
            });

        var ticks = _time.GetUtcNow().UtcDateTime.Ticks;
        var ctx = BuildHttpContext(timestampOverride: ticks.ToString(CultureInfo.InvariantCulture));
        ctx.Request.Headers[WopiHeaders.Proof] = Convert.ToBase64String(new byte[256]);
        using var failures = CaptureFailures();

        var validator = CreateValidator();
        var result = await validator.ValidateProofAsync(ctx.ToWopiRequestInfo(), AccessToken);

        Assert.False(result);
        Assert.Equal("signature_mismatch", SingleFailureReason(failures));
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
            ctx.Request.Headers[WopiHeaders.Timestamp] =
                new StringValues(timestampOverride ?? s_now.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
        }
        if (includeProof)
        {
            ctx.Request.Headers[WopiHeaders.Proof] = new StringValues(proofOverride ?? string.Empty);
        }
        return ctx;
    }

    /// <summary>
    /// Captures the <see cref="WopiTelemetry.ProofValidationFailures"/> counter so a negative
    /// test can pin WHICH rejection branch fired. The validator's boolean return cannot — any
    /// rejection reason yields <c>false</c>, so without the reason tag a deleted guard would
    /// pass every negative test via a different branch.
    /// </summary>
    private static MeterCapture CaptureFailures()
        => new(WopiTelemetry.ProofValidationFailures.Name);

    private static string? SingleFailureReason(MeterCapture failures)
    {
        var (_, tags) = Assert.Single(failures.Measurements);
        return Assert.Single(tags, t => t.Key == "reason").Value as string;
    }

    private static string BuildExpectedHostUrl()
        => $"{Scheme}://{Host}{Path}{QueryString}".ToUpperInvariant();

    private static void SignAndApply(HttpRequest request, RSACryptoServiceProvider signer, long ticks)
    {
        var canonical = WopiProofPayload.Build(AccessToken, BuildExpectedHostUrl(), ticks);
        var signature = signer.SignData(canonical, "SHA256");
        request.Headers[WopiHeaders.Proof] = Convert.ToBase64String(signature);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
