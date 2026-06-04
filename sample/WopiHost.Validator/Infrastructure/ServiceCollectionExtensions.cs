using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;
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
        services.Configure<WopiHostOptions>(configuration.GetSection(WopiHostOptions.SectionName));
        services.AddFileSystemStorageProvider(configuration);
        services.AddSingleton<IWopiLockProvider, MemoryLockProvider.MemoryLockProvider>();
        // The validator plugs in its own IWopiHostExtensions to populate the optional
        // CheckFileInfo URLs the Microsoft validator probes for.
        services.AddSingleton<IWopiHostExtensions, WopiValidatorExtensions>();
        services.AddWopi();
        // A fixed signing key keeps tests reproducible across restarts.
        services.ConfigureWopiSecurity(o =>
        {
            o.SigningKey = JwtAccessTokenService.DeriveHmacKey("wopi-validator-dev-key");
        });
        // Wrap the default JwtAccessTokenService so the Microsoft WOPI validator's literal-string
        // token is accepted alongside real JWTs.
        services.AddSingleton<JwtAccessTokenService>();
        services.AddSingleton<IWopiAccessTokenService, SentinelOrJwtAccessTokenService>();
        return services;
    }

    public static IServiceCollection AddWopiHostPages(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DiscoveryOptions>(configuration.GetSection(DiscoveryOptions.SectionName));
        services.Configure<WopiOptions>(configuration.GetSection(WopiOptions.SectionName));

        services.AddHttpClient<IDiscoveryFileProvider, HttpDiscoveryFileProvider>((sp, client) =>
        {
            var wopiOptions = sp.GetRequiredService<IOptions<WopiOptions>>();
            client.BaseAddress = wopiOptions.Value.ClientUrl;
        });

        services.AddSingleton<IDiscoverer, WopiDiscoverer>();
        return services;
    }
}
