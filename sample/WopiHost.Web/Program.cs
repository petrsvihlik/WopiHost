using Microsoft.AspNetCore.Http.HttpResults;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.ServiceDefaults;
using WopiHost.Web.Components;
using WopiHost.Web.Services;
using WopiHost.Web.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

builder.Services
    .AddOptionsWithValidateOnStart<WopiOptions>()
    .Bind(builder.Configuration.GetRequiredSection(WopiOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddWopiDiscovery<WopiOptions>(
    options => builder.Configuration.GetSection(DiscoveryOptions.SectionName).Bind(options));

builder.Services.AddFileSystemStorageProvider(builder.Configuration);

var app = builder.Build();

// The developer exception page leaks stack traces and configuration values, so gate it behind
// the Development environment — production deployments fall through to the /Error handler below.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

// Razor Components stamp anti-forgery metadata on each endpoint, so app.UseAntiforgery() must
// be in the pipeline before the endpoint middleware runs. The Razor Components routing layer
// rejects unsafe verbs (POST etc.) when the anti-forgery cookie/token pair is missing — this
// sample is GET-only so the gate is a no-op at runtime, but the middleware must still be wired.
app.UseAntiforgery();

app.MapRazorComponents<App>();

// Sample-only host-app action backing the editor toolbar's Download button. Streams the file via
// the storage provider directly (not a WOPI protocol operation) and, like the rest of this sample,
// is anonymous: fine for a demo, not a template for production access control.
app.MapGet("/files/{id}/content", async Task<Results<NotFound, FileStreamHttpResult>> (string id, IWopiStorageProvider storage, CancellationToken ct) =>
{
    var file = await storage.GetWopiFile(id, ct);
    // Exists guards a stale id→path map entry (e.g. after an editor-driven rename moved the file):
    // the frontend's map still resolves the id, but the path is gone. 404 rather than faulting in
    // OpenReadAsync — the listing re-enumerates live, so a reload picks the file up under its new id.
    if (file is null || !file.Exists)
    {
        return TypedResults.NotFound();
    }
    var downloadName = $"{file.Name}.{file.Extension.TrimStart('.')}";
    var stream = await file.OpenReadAsync(ct);
    return TypedResults.File(stream, "application/octet-stream", downloadName);
});

app.MapHealthChecks("/health");

app.MapDefaultEndpoints();

app.Run();

namespace WopiHost.Web
{
    /// <summary>Test-only marker so <c>WebApplicationFactory&lt;WebSampleEntryPoint&gt;</c> resolves unambiguously to this sample's assembly (the global <c>Program</c> name conflicts with the WOPI backend's <c>WopiHost.Program</c>).</summary>
    public partial class WebSampleEntryPoint;
}
