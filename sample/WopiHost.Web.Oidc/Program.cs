using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.ServiceDefaults;
using WopiHost.Web.Oidc.Infrastructure;
using WopiHost.Web.Oidc.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

builder.Services
    .AddOptionsWithValidateOnStart<WopiOptions>()
    .Bind(builder.Configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT))
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<OidcOptions>()
    .Bind(builder.Configuration.GetRequiredSection("Oidc"))
    .ValidateDataAnnotations();

builder.Services.AddWopiDiscovery<WopiOptions>(
    options => builder.Configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS).Bind(options));

builder.Services.AddSingleton<InMemoryFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

// WOPI access-token signer must use the same key as the WOPI backend.
builder.Services.AddSingleton(_ =>
{
    var configured = builder.Configuration["Wopi:Security:SigningKey"];
    return string.IsNullOrEmpty(configured)
        ? WopiAccessTokenMinter.FromSecret(WopiAccessTokenMinter.DefaultDevSecret)
        : new WopiAccessTokenMinter(Convert.FromBase64String(configured));
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

app.UseDeveloperExceptionPage();
app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapDefaultEndpoints();

app.Run();

namespace WopiHost.Web.Oidc
{
    /// <summary>Test-only marker so <c>WebApplicationFactory&lt;OidcSampleEntryPoint&gt;</c> resolves unambiguously to this sample's assembly (the global <c>Program</c> name conflicts with the WOPI backend's <c>WopiHost.Program</c>).</summary>
    public partial class OidcSampleEntryPoint;
}
