using Serilog;
using Serilog.Events;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.FileSystemProvider;
using Microsoft.Extensions.Options;
using WopiHost.Discovery;

namespace WopiHost;

public static class Program
{
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();

        try
        {
            Log.Information("Starting WOPI host");
            
            var builder = WebApplication.CreateBuilder(args);
            
            // Add Serilog
            builder.Host.UseSerilog();
            
            // Add service defaults from Aspire
            builder.AddServiceDefaults();
            
            // Add services to the container
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });
            
            // Configuration
            // this makes sure that the configuration exists and is valid
            var wopiHostOptionsSection = builder.Configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT);
            builder.Services
                .AddOptionsWithValidateOnStart<WopiHostOptions>()
                .BindConfiguration(wopiHostOptionsSection.Path) 
                .ValidateDataAnnotations();
            
            var wopiHostOptions = wopiHostOptionsSection.Get<WopiHostOptions>();
            
            // Register InMemoryFileIds - needed by WopiFileSystemProvider
            builder.Services.AddSingleton<InMemoryFileIds>();
            
            // Add file provider
            builder.Services.AddStorageProvider(wopiHostOptions.StorageProviderAssemblyName);
            // Add lock provider
            builder.Services.AddLockProvider(wopiHostOptions.LockProviderAssemblyName);
            
            // Add Discovery services
            builder.Services.AddWopiDiscovery<WopiHostOptions>(
                options => builder.Configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS).Bind(options));
            
            // Add Cobalt support
            if (wopiHostOptions.UseCobalt)
            {
                // Add cobalt
                builder.Services.AddCobalt();
            }
            
            builder.Services.AddControllers();
            
            // Add WOPI
            builder.Services.AddWopi();
            
            var app = builder.Build();
            
            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = LogHelper.EnrichWithWopiDiagnostics;
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} with [WOPI CorrelationID: {" + nameof(WopiHeaders.CORRELATION_ID) + "}, WOPI SessionID: {" + nameof(WopiHeaders.SESSION_ID) + "}] responded {StatusCode} in {Elapsed:0.0000} ms";
            });
            
            app.UseRouting();
            
            // Automatically authenticate
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.MapControllers();
            app.MapGet("/", () => "This is just a WOPI server. You need a WOPI client to access it...").ShortCircuit(404);
            
            // Map health check endpoints
            app.MapHealthChecks("/health");
            
            // Map default endpoints from Aspire
            app.MapDefaultEndpoints();
            
            app.Run();
            
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WOPI Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
