using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
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
/// override-dispatch behaviours: dispatch by header, per-endpoint authorization, pass-through
/// for metadata-less endpoints, fallback to 404/405 when no candidate matches.
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
        req.Headers.Add(WopiHeaders.WopiOverride, headerValue);
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
        req.Headers.Add(WopiHeaders.WopiOverride, "BOGUS_VALUE");

        var resp = await _client!.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Authorization_Is_Evaluated_Against_Selected_Endpoint()
    {
        // OP_A endpoint does not require authentication: anonymous succeeds.
        var anonA = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        anonA.Headers.Add(WopiHeaders.WopiOverride, "OP_A");
        var anonAResp = await _client!.SendAsync(anonA);
        Assert.Equal(HttpStatusCode.OK, anonAResp.StatusCode);

        // OP_B endpoint requires authentication: anonymous → 401.
        var anonB = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        anonB.Headers.Add(WopiHeaders.WopiOverride, "OP_B");
        var anonBResp = await _client!.SendAsync(anonB);
        Assert.Equal(HttpStatusCode.Unauthorized, anonBResp.StatusCode);

        // OP_B with the auth trigger header → 200. Confirms the auth policy belongs to the
        // endpoint the matcher selected (not the alphabetically first or some merged set).
        var authB = new HttpRequestMessage(HttpMethod.Post, "/test/abc");
        authB.Headers.Add(WopiHeaders.WopiOverride, "OP_B");
        authB.Headers.Add(AuthHeader, "1");
        var authBResp = await _client!.SendAsync(authB);
        Assert.Equal(HttpStatusCode.OK, authBResp.StatusCode);
    }

    public sealed class MetadataConstructor
    {
        [Fact]
        public void Throws_When_Values_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new WopiOverrideMetadata(null!));
        }

        [Fact]
        public void Throws_When_Values_Is_Empty()
        {
            Assert.Throws<ArgumentException>(() => new WopiOverrideMetadata());
        }

        [Fact]
        public void Exposes_Supplied_Values_Case_Sensitively()
        {
            // Project to an array — Assert.Contains on FrozenSet<T> is ambiguous between
            // the ISet<T> and IReadOnlySet<T> overloads, and Assert.True(set.Contains(x)) trips
            // xUnit2017.
            var values = new WopiOverrideMetadata("Lock", "UNLOCK").Values.ToArray();

            Assert.Contains("Lock", values);
            Assert.Contains("UNLOCK", values);
            // Ordinal (case-sensitive) per WOPI spec — header values are upper-case constants.
            Assert.DoesNotContain("lock", values);
        }
    }

    /// <summary>
    /// Exercises the pass-through branch in <see cref="WopiOverrideMatcherPolicy"/>: a candidate
    /// without <see cref="WopiOverrideMetadata"/> sharing the route template with metadata-bearing
    /// siblings must remain valid when the request header invalidates the others. Models the
    /// "consumer mixes WOPI-style endpoints with their own metadata-less fallback" scenario.
    /// </summary>
    public sealed class MetadataLessSiblingPassThrough : IAsyncLifetime
    {
        private WebApplication? _app;
        private HttpClient? _client;

        public async ValueTask InitializeAsync()
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddSingleton<MatcherPolicy, WopiOverrideMatcherPolicy>();
            builder.Services.AddAuthorization();

            _app = builder.Build();
            _app.UseRouting();
            _app.UseAuthorization();

            // Override-bearing endpoint. Invalidated when the request header isn't OP_A.
            _app.MapPost("/sibling/{id}", (string id) => TypedResults.Ok($"OP_A:{id}"))
                .WithMetadata(new WopiOverrideMetadata("OP_A"));

            // Metadata-less sibling on the same route+verb. Pass-through (`metadata is null`)
            // leaves it valid; with OP_A invalidated, this is the only remaining candidate.
            _app.MapPost("/sibling/{id}", (string id) => TypedResults.Ok($"FALLBACK:{id}"));

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

        [Fact]
        public async Task Metadata_Less_Sibling_Wins_When_Override_Does_Not_Match()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/sibling/abc");
            // No X-WOPI-Override — invalidates the OP_A endpoint, leaves the metadata-less one.
            var resp = await _client!.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal("\"FALLBACK:abc\"", await resp.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Exercises the <c>IsValidCandidate</c> short-circuit in <see cref="WopiOverrideMatcherPolicy"/>:
    /// when an earlier <see cref="MatcherPolicy"/> has invalidated a candidate, the override policy
    /// must skip it rather than re-evaluate metadata. Models composition with custom downstream
    /// policies — the built-in <c>HttpMethodMatcherPolicy</c> doesn't trigger this branch because
    /// Minimal API's DFA pre-splits by verb before override dispatch runs.
    /// </summary>
    public sealed class PreInvalidatedCandidateSkip : IAsyncLifetime
    {
        private WebApplication? _app;
        private HttpClient? _client;

        public async ValueTask InitializeAsync()
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            // Order matters: invalidator runs first (Order 500), override policy second (Order 1000).
            builder.Services.AddSingleton<MatcherPolicy, FirstCandidateInvalidator>();
            builder.Services.AddSingleton<MatcherPolicy, WopiOverrideMatcherPolicy>();
            builder.Services.AddAuthorization();

            _app = builder.Build();
            _app.UseRouting();
            _app.UseAuthorization();

            // Two POST endpoints on the same route. The invalidator nullifies the first by index;
            // the override policy must skip it (covering `IsValidCandidate(i) continue`) and
            // dispatch the second based on the header.
            _app.MapPost("/pre/{id}", (string id) => TypedResults.Ok($"FIRST:{id}"))
                .WithMetadata(new WopiOverrideMetadata("OP_X"));
            _app.MapPost("/pre/{id}", (string id) => TypedResults.Ok($"SECOND:{id}"))
                .WithMetadata(new WopiOverrideMetadata("OP_X"));

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

        [Fact]
        public async Task Skips_Already_Invalidated_Candidate()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/pre/abc");
            req.Headers.Add(WopiHeaders.WopiOverride, "OP_X");
            var resp = await _client!.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal("\"SECOND:abc\"", await resp.Content.ReadAsStringAsync());
        }

        private sealed class FirstCandidateInvalidator : MatcherPolicy, IEndpointSelectorPolicy
        {
            public override int Order => 500;
            public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) => true;
            public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
            {
                if (candidates.Count > 0)
                {
                    candidates.SetValidity(0, false);
                }
                return Task.CompletedTask;
            }
        }
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
