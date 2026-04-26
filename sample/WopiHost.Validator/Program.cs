using WopiHost.Core.Security.Authentication;
using WopiHost.Validator.Infrastructure;

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

await app.RunAsync();
