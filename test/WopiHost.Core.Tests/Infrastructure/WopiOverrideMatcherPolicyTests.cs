using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// End-to-end exercise of <see cref="WopiOverrideMatcherPolicy"/> against an in-memory
/// <see cref="TestServer"/>. Maps synthetic POST endpoints sharing the same route template and
/// HTTP verb, each tagged with a distinct <see cref="WopiOverrideMetadata"/>, and asserts the
/// behaviours the Minimal-API migration depends on (issue #430):
/// dispatch by header, per-endpoint authorization, pass-through for metadata-less endpoints,
/// fallback to 404/405 when no candidate matches.
/// </summary>
public sealed class WopiOverrideMatcherPolicyTests : IAsyncLifetime
{
    private const string AuthScheme = "test";
    private const string AuthHeader = "X-Test-Auth";

    private WebApplication? _app;
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddSingleton<MatcherPolicy, WopiOverrideMatcherPolicy>();
        builder.Services
            .AddAuthentication(AuthScheme)
            .AddScheme<AuthenticationSchemeOptions, HeaderTriggerAuthHandler>(AuthScheme, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();

        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();

        // Two POST endpoints sharing route + verb, distinguished only by the override header.
        _app.MapPost("/test/{id}", (string id) => TypedResults.Ok($"A:{id}"))
            .WithMetadata(new WopiOverrideMetadata("OP_A"));

        // Second POST endpoint additionally requires authentication — proves that authorization
        // is evaluated against the endpoint selected by the matcher policy, not all candidates.
        _app.MapPost("/test/{id}", (string id) => TypedResults.Ok($"B:{id}"))
            .WithMetadata(new WopiOverrideMetadata("OP_B"))
            .RequireAuthorization();

        // Endpoint without override metadata — must pass through the policy untouched so that
        // GET handlers continue to work on routes shared with override-multiplexed POSTs.
        _app.MapGet("/test/{id}", (string id) => TypedResults.Ok($"GET:{id}"));

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("OP_A", "\"A:abc\"")]
    [InlineData("OP_B", "\"B:abc\"")]
    public async Task Dispatches_To_Endpoint_With_Matching_Override(string headerValue, string expectedBody)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        req.Headers.Add(WopiHeaders.WOPI_OVERRIDE, headerValue);
        // OP_B requires authentication — supply the trigger header.
        req.Headers.Add(AuthHeader, "1");

        var resp = await _client!.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(expectedBody, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Endpoints_Without_Metadata_Pass_Through()
    {
        var resp = await _client!.GetAsync("/test/abc");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("\"GET:abc\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_Override_On_Override_Only_Route_Returns_NotFound()
    {
        // No X-WOPI-Override header. All POST endpoints declare the metadata, so every POST
        // candidate is invalidated. The router returns 404 (not 405) — the 405 hint belongs
        // to HttpMethodMatcherPolicy alone; custom matcher policies that nullify candidates
        // cannot re-emit it. The MVC HttpHeaderAttribute action constraint falls through the
        // same way today. See WopiOverrideMatcherPolicy's "Selection failure mode" remarks.
        var req = new HttpRequestMessage(HttpMethod.Post, "/test/abc");

        var resp = await _client!.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Unknown_Override_Value_Returns_NotFound()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        req.Headers.Add(WopiHeaders.WOPI_OVERRIDE, "BOGUS_VALUE");

        var resp = await _client!.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Authorization_Is_Evaluated_Against_Selected_Endpoint()
    {
        // OP_A endpoint does not require authentication: anonymous succeeds.
        var anonA = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        anonA.Headers.Add(WopiHeaders.WOPI_OVERRIDE, "OP_A");
        var anonAResp = await _client!.SendAsync(anonA);
        Assert.Equal(HttpStatusCode.OK, anonAResp.StatusCode);

        // OP_B endpoint requires authentication: anonymous → 401.
        var anonB = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        anonB.Headers.Add(WopiHeaders.WOPI_OVERRIDE, "OP_B");
        var anonBResp = await _client!.SendAsync(anonB);
        Assert.Equal(HttpStatusCode.Unauthorized, anonBResp.StatusCode);

        // OP_B with the auth trigger header → 200. Confirms the auth policy belongs to the
        // endpoint the matcher selected (not the alphabetically first or some merged set).
        var authB = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        authB.Headers.Add(WopiHeaders.WOPI_OVERRIDE, "OP_B");
        authB.Headers.Add(AuthHeader, "1");
        var authBResp = await _client!.SendAsync(authB);
        Assert.Equal(HttpStatusCode.OK, authBResp.StatusCode);
    }

    /// <summary>Test authentication handler that succeeds only when <c>X-Test-Auth</c> is present.</summary>
    private sealed class HeaderTriggerAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Context.Request.Headers.ContainsKey(AuthHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user")],
                AuthScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
