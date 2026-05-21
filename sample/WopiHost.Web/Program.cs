using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.ServiceDefaults;
using WopiHost.Web.Components;
using WopiHost.Web.Models;
using WopiHost.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults from Aspire
builder.AddServiceDefaults();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Razor Components: static SSR only (no .AddInteractiveServerComponents()) — the sample is a
// thin viewer and the WOPI client owns the editor experience inside an iframe, so there's
// nothing to gain from SignalR connections per request.
builder.Services.AddRazorComponents();

// Detail.razor sets cache-control / Pragma response headers per the WOPI hostpage spec and
// reads the captured exception in Error.razor — both need direct access to HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<WopiAccessTokenMinter>();

// Configuration
builder.Services
    .AddOptionsWithValidateOnStart<WopiOptions>()
    .Bind(builder.Configuration.GetRequiredSection(WopiOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddWopiDiscovery<WopiOptions>(
    options => builder.Configuration.GetSection(DiscoveryOptions.SectionName).Bind(options));

builder.Services.AddSingleton<InMemoryFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline. The developer exception page leaks stack traces and
// configuration values, so gate it behind the Development environment — production deployments
// fall through to the /Error handler below.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

//app.UseHttpsRedirection();

// Add static files to the request pipeline
app.UseStaticFiles();

// Razor Components stamp anti-forgery metadata on each endpoint, so app.UseAntiforgery() must
// be in the pipeline before the endpoint middleware runs. The Razor Components routing layer
// rejects unsafe verbs (POST etc.) when the anti-forgery cookie/token pair is missing — this
// sample is GET-only so the gate is a no-op at runtime, but the middleware must still be wired.
app.UseAntiforgery();

app.MapRazorComponents<App>();

// Map health check endpoints
app.MapHealthChecks("/health");

// Map default endpoints from Aspire
app.MapDefaultEndpoints();

app.Run();

namespace WopiHost.Web
{
    /// <summary>Test-only marker so <c>WebApplicationFactory&lt;WebSampleEntryPoint&gt;</c> resolves unambiguously to this sample's assembly (the global <c>Program</c> name conflicts with the WOPI backend's <c>WopiHost.Program</c>).</summary>
    public partial class WebSampleEntryPoint;
}
