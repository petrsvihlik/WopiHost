using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults from Aspire
builder.AddServiceDefaults();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Configuration
builder.Services
    .AddOptionsWithValidateOnStart<WopiOptions>()
    .Bind(builder.Configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT))
    .ValidateDataAnnotations();

builder.Services.AddWopiDiscovery<WopiOptions>(
    options => builder.Configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS).Bind(options));

builder.Services.AddSingleton<InMemoryFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseDeveloperExceptionPage();

// Configure exception handler as a fallback for production
app.UseExceptionHandler("/Error");

//app.UseHttpsRedirection();

// Add static files to the request pipeline
app.UseStaticFiles();

app.UseRouting();

// Map endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map health check endpoints
app.MapHealthChecks("/health");

// Map default endpoints from Aspire
app.MapDefaultEndpoints();

app.Run();
