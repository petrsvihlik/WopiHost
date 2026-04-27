using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Validator.Infrastructure;
using WopiHost.Validator.Models;

var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks();

// Add service discovery
builder.Services.AddServiceDiscovery();

// standard
builder.Services.AddControllers();
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
builder.Services.AddRazorPages();

// --------- Add Wopi Server and Host pages
builder.Services.AddWopiLogging();
builder.Services.AddWopiServer(builder.Configuration);
builder.Services.AddWopiHostPages(builder.Configuration);

// Replace the proof validator: the Microsoft WOPI validator does not sign
// requests, so the default WopiProofValidator would reject every call.
builder.Services.AddScoped<IWopiProofValidator, NoOpProofValidator>();

// ---------
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();
// HostPages
app.MapRazorPages();
// WopiServer Controllers
app.MapControllers();
// Map health checks
app.MapHealthChecks("/health");

// Test-only token-issuance endpoint used by the WOPI validator harness.
// Mints a real signed access token for {fileId} so the Microsoft WOPI validator
// CLI can authenticate. NOT a pattern to copy into a production WOPI host —
// in real deployments tokens are minted server-side from an authenticated
// user session, never exposed via an unauthenticated endpoint.
app.MapGet("/_test/issue-token/{fileId}", async (
    string fileId,
    IWopiStorageProvider storage,
    IWopiAccessTokenService tokens,
    IWopiPermissionProvider permissions,
    IOptions<WopiOptions> options,
    CancellationToken ct) =>
{
    var file = await storage.GetWopiResource<IWopiFile>(fileId, ct);
    if (file is null)
    {
        return Results.NotFound();
    }
    var anonymous = new System.Security.Claims.ClaimsPrincipal();
    var filePerms = await permissions.GetFilePermissionsAsync(anonymous, file, ct);
    // The Microsoft WOPI validator uses a single token for both file and container ops, so
    // we mint one with both surfaces pre-authorized. Real hosts typically issue narrower
    // tokens per session.
    var rootContainer = storage.RootContainerPointer;
    var containerPerms = await permissions.GetContainerPermissionsAsync(anonymous, rootContainer, ct);
    var token = await tokens.IssueAsync(new WopiAccessTokenRequest
    {
        UserId = options.Value.UserId,
        UserDisplayName = options.Value.UserId,
        ResourceId = file.Identifier,
        ResourceType = WopiResourceType.File,
        FilePermissions = filePerms,
        ContainerPermissions = containerPerms,
    }, ct);
    return Results.Text(token.Token);
});

await app.RunAsync();
