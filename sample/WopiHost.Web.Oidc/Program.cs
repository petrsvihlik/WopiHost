using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.ServiceDefaults;
using WopiHost.Web.Oidc.Components;
using WopiHost.Web.Oidc.Endpoints;
using WopiHost.Web.Oidc.Infrastructure;
using WopiHost.Web.Oidc.Models;
using WopiHost.Web.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Razor Components: static SSR only (no .AddInteractiveServerComponents()) — the sample is a
// thin viewer and the WOPI client owns the editor experience inside an iframe, so there's
// nothing to gain from SignalR connections per request.
builder.Services.AddRazorComponents();

// MainLayout reads HttpContext.User for the auth-aware nav; Detail.razor sets WOPI hostpage
// cache-control headers and reads the captured exception in Error.razor. All three need
// direct HttpContext access from component code, which is only available via the accessor.
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddOptionsWithValidateOnStart<WopiOptions>()
    .Bind(builder.Configuration.GetRequiredSection(WopiOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<OidcOptions>()
    .Bind(builder.Configuration.GetRequiredSection(OidcOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddWopiDiscovery<WopiOptions>(
    options => builder.Configuration.GetSection(DiscoveryOptions.SectionName).Bind(options));

builder.Services.AddSingleton<InMemoryFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

// WOPI access-token signer must use the same key as the WOPI backend. Bind a project-local
// WopiSigningOptions pointed at the same configuration path (Wopi:Security) — pure frontends
// don't take a project reference on WopiHost.Core, so we keep our own typed shape for the
// shared key.
builder.Services
    .AddOptions<WopiSigningOptions>()
    .Bind(builder.Configuration.GetSection(WopiSigningOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<WopiSigningOptions>>().Value;
    return opts.SigningKey is { Length: > 0 } key
        ? new WopiAccessTokenMinter(key)
        : WopiAccessTokenMinter.FromSecret(WopiAccessTokenMinter.DefaultDevSecret);
});

// Don't rename inbound JWT claim names — keeps the role/email/name claims usable by their
// short OIDC names (sub, name, roles, email) regardless of which IdP is configured.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddOpenIdConnect();

// Bind OIDC handler options from OidcOptions lazily — this fires AFTER all configuration sources
// (including the in-memory overrides used by integration tests) are layered in, so the test
// factories can swap Authority/ClientId without rewriting Program.cs.
builder.Services
    .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<IOptions<OidcOptions>>((options, accessor) =>
    {
        var oidc = accessor.Value;
        options.Authority = oidc.Authority;
        options.ClientId = oidc.ClientId;
        options.ClientSecret = oidc.ClientSecret;
        options.CallbackPath = oidc.CallbackPath;
        options.SignedOutCallbackPath = oidc.SignedOutCallbackPath;
        options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
        options.UsePkce = oidc.UsePkce;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.ResponseType = "code";
        options.Scope.Clear();
        foreach (var scope in oidc.Scopes)
        {
            options.Scope.Add(scope);
        }
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = oidc.RoleClaimType;
    });

builder.Services.AddAuthorization();

if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
}

var app = builder.Build();

// Gate the developer exception page behind the Development environment — it leaks stack
// traces and configuration values. Production falls through to the /Error handler.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Auth middleware now has to be wired explicitly — AddRazorComponents doesn't add it the way
// AddControllersWithViews did via MapControllerRoute.
app.UseAuthentication();
app.UseAuthorization();

// Razor Components stamp anti-forgery metadata on each endpoint, so app.UseAntiforgery() must
// be in the pipeline before MapRazorComponents runs. The MainLayout's sign-out POST form
// honours this via <AntiforgeryToken />.
app.UseAntiforgery();

app.MapRazorComponents<App>();
AccountEndpoints.MapAccountEndpoints(app);

app.MapDefaultEndpoints();

app.Run();

namespace WopiHost.Web.Oidc
{
    /// <summary>Test-only marker so <c>WebApplicationFactory&lt;OidcSampleEntryPoint&gt;</c> resolves unambiguously to this sample's assembly (the global <c>Program</c> name conflicts with the WOPI backend's <c>WopiHost.Program</c>).</summary>
    public partial class OidcSampleEntryPoint;
}
