using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// End-to-end coverage of <c>WopiProofValidator</c> against the production filter pipeline.
/// Every other integration test class swaps the validator for <see cref="AlwaysValidProofValidator"/>,
/// leaving the proof-validation surface without integration coverage. This
/// suite wires the real validator (via <see cref="WopiBackendFactory"/>'s
/// <c>useRealProofValidator</c> flag) and a discoverer that publishes a known RSA key pair
/// (<see cref="FakeDiscovererWithProofKeys"/>), so each test exercises the same code path
/// production hosts run on every WOPI request.
/// </summary>
/// <remarks>
/// <para>
/// Why dedicated and not folded into the existing read-only suite: the read-only fixture is
/// shared across endpoint test classes for boot-time amortisation. Threading the real proof
/// validator through it would force every existing test to sign each request, ballooning their
/// per-test work; isolating the validation coverage here keeps the proof-gate the only thing
/// under test in this class.
/// </para>
/// <para>
/// Test base URL: <c>WebApplicationFactory.CreateClient()</c> defaults to
/// <c>http://localhost/</c>; the validator reads the framework-built request URL via
/// <c>WopiRequestInfo.RequestUrl</c> and case-folds it for signing, so signing the same
/// <c>http://localhost/...</c> URL the framework hands the filter is what makes verification
/// land. <c>BaseAddress</c> is pinned explicitly in <see cref="CreateClient"/> so a future
/// default change in the test host doesn't silently invalidate signatures.
/// </para>
/// </remarks>
public sealed class ProofValidationIntegrationTests : IAsyncLifetime, IDisposable
{
    private const string SigningSecret = "proof-validation-integration-key-32by";

    private readonly WopiBackendFactory _factory;
    private string _fileId = null!;
    private string _accessToken = null!;
    private FakeDiscovererWithProofKeys _keys = null!;

    public ProofValidationIntegrationTests()
    {
        _factory = new WopiBackendFactory(SigningSecret, useRealProofValidator: true);
    }

    public async ValueTask InitializeAsync()
    {
        // Touch CreateClient() to build the host so the in-process discoverer is materialised.
        using var _ = _factory.CreateClient();
        _keys = _factory.ProofKeys
            ?? throw new InvalidOperationException("Factory was built without useRealProofValidator=true");
        using var scope = _factory.Services.CreateScope();

        var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        await foreach (var f in storage.GetWopiFiles(storage.RootContainer.Identifier))
        {
            _fileId = f.Identifier;
            break;
        }

        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "proof-user",
            UserDisplayName = "Proof User",
            UserEmail = "proof@example.com",
            ResourceId = _fileId,
            ResourceType = WopiResourceType.File,
            FilePermissions = WopiFilePermissions.UserCanWrite,
        });
        _accessToken = token.Token;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ProofValidator_AcceptsRequest_SignedWithCurrentKey()
    {
        // The golden path: client signs with X-WOPI-Proof using the current key the discoverer
        // advertises. Validator must accept and route through to the endpoint.
        using var client = CreateClient();
        var requestUrl = BuildRequestUrl();
        var ticks = DateTime.UtcNow.Ticks;
        using var request = BuildSignedRequest(HttpMethod.Get, requestUrl, _keys.CurrentKey, ticks);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProofValidator_AcceptsRequest_SignedWithOldKey_DuringRotation()
    {
        // Spec: during key rotation, clients may still sign with the previous key (which the
        // host has cached as WopiProofKeys.OldValue). Validator must accept.
        using var client = CreateClient();
        var requestUrl = BuildRequestUrl();
        var ticks = DateTime.UtcNow.Ticks;
        using var request = BuildSignedRequest(HttpMethod.Get, requestUrl, _keys.OldKey, ticks);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProofValidator_Rejects_RequestSignedWithAttackerKey()
    {
        // Negative path: a request signed with a key the discoverer doesn't know fails
        // validation. Filter responds 500 per WOPI spec for signature failure (NOT 401 — 401 is
        // reserved for unauthenticated requests; an authenticated request with a bad signature
        // is the "untrusted origin" 500 path).
        using var attacker = new RSACryptoServiceProvider(2048);
        using var client = CreateClient();
        var ticks = DateTime.UtcNow.Ticks;
        using var request = BuildSignedRequest(HttpMethod.Get, BuildRequestUrl(), attacker, ticks);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task ProofValidator_Rejects_RequestMissingProofHeader()
    {
        // No X-WOPI-Proof / X-WOPI-TimeStamp headers — the validator's first guard fails and the
        // request 500s. The unit tests cover ValidateProofAsync directly; this asserts the
        // filter wires it correctly at the integration level.
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUrl());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task ProofValidator_Rejects_RequestWithStaleTimestamp()
    {
        // Spec: requests older than 20 minutes are stale. The validator rejects them even when
        // the signature is otherwise correct — pinning the spec's anti-replay window.
        using var client = CreateClient();
        var requestUrl = BuildRequestUrl();
        var ticks = DateTime.UtcNow.AddMinutes(-21).Ticks;
        using var request = BuildSignedRequest(HttpMethod.Get, requestUrl, _keys.CurrentKey, ticks);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost/"),
            AllowAutoRedirect = false,
        });

    private string BuildRequestUrl()
        => $"http://localhost/wopi/files/{_fileId}?access_token={Uri.EscapeDataString(_accessToken)}";

    private HttpRequestMessage BuildSignedRequest(HttpMethod method, string url, RSACryptoServiceProvider signer, long ticks)
    {
        var request = new HttpRequestMessage(method, url);
        // Match the validator: case-fold the URL to upper-invariant before hashing — same
        // contract WOPI clients sign against.
        var canonical = BuildCanonicalProof(_accessToken, url.ToUpperInvariant(), ticks);
        var signature = signer.SignData(canonical, "SHA256");
        request.Headers.Add(WopiHeaders.Proof, Convert.ToBase64String(signature));
        request.Headers.Add(WopiHeaders.Timestamp, ticks.ToString(CultureInfo.InvariantCulture));
        return request;
    }

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
}
