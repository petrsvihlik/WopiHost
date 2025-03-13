using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Models;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWopiLogging(this IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();

        services.AddSerilog(Log.Logger);
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
        });
        return services;
    }
    public static IServiceCollection AddWopiServer(this IServiceCollection services, IConfiguration configuration)
    {
        // ------ add Wopi Server .Core services

        // Configuration
        services.Configure<WopiHostOptions>(configuration.GetSection(WopiConfigurationSections.WOPI_ROOT));
        // Add file provider
        services.AddSingleton<InMemoryFileIds>();
        services.AddSingleton<IWopiStorageProvider, WopiFileSystemProvider>();
        services.AddSingleton<IWopiWritableStorageProvider, WopiFileSystemProvider>();
        // Add lock provider
        services.AddSingleton<IWopiLockProvider, MemoryLockProvider.MemoryLockProvider>();
        services.AddSingleton<IWopiSecurityHandler, WopiSecurityHandler>();
        // Add WOPI
        services.AddWopi(o =>
        {
            o.OnCheckFileInfo = WopiEvents.OnGetWopiCheckFileInfo;
        });
        return services;
    }

    public static IServiceCollection AddWopiHostPages(this IServiceCollection services, IConfiguration configuration)
    {
        // ------- add Wopi Host services
        services.Configure<DiscoveryOptions>(configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS));
        services.Configure<WopiOptions>(configuration.GetSection(WopiConfigurationSections.WOPI_ROOT));

        services.AddHttpClient<IDiscoveryFileProvider, HttpDiscoveryFileProvider>((sp, client) =>
        {
            var wopiOptions = sp.GetRequiredService<IOptions<WopiOptions>>();
            client.BaseAddress = wopiOptions.Value.ClientUrl;
        });

        services.AddSingleton<IDiscoverer, WopiDiscoverer>();
        return services;
    }
}
