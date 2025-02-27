using WopiHost.Abstractions;
using WopiHost.Core;
using WopiHost.Core.Models;
using Serilog;

namespace WopiHost;

public class Startup(IConfiguration configuration)
{
    /// <summary>
    /// Sets up the DI container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole(); //Configuration.GetSection("Logging")
            loggingBuilder.AddDebug();
        });

        // Configuration
        // this makes sure that the configuration exists and is valid
        var wopiHostOptionsSection = configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT);
        services
            .AddOptions<WopiHostOptions>()
            .BindConfiguration(wopiHostOptionsSection.Path) 
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var wopiHostOptions = wopiHostOptionsSection.Get<WopiHostOptions>();
        // Add file provider
        services.AddStorageProvider(wopiHostOptions.StorageProviderAssemblyName);
        // Add lock provider
        services.AddLockProvider(wopiHostOptions.LockProviderAssemblyName);
        // Add Cobalt support
        if (wopiHostOptions.UseCobalt)
        {
            // Add cobalt
            services.AddCobalt();
        }

        services.AddControllers();

        // Ideally, pass a persistent dictionary implementation
        services.AddSingleton<IDictionary<string, WopiLockInfo>>(d => new Dictionary<string, WopiLockInfo>());

        // Add WOPI
        services.AddWopi();
    }

    /// <summary>
    /// Configure is called after ConfigureServices is called.
    /// </summary>
    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
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

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => "This is just a WOPI server. You need a WOPI client to access it...").ShortCircuit(404);
        });
    }
}
