using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extensions for registering WOPI into the application pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register services required by the WOPI host server.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    public static IServiceCollection AddWopi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAuthorizationCore();

        // Add authorization handler
        services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();
        
        services.AddScoped<IWopiProofValidator, WopiProofValidator>();
        services.AddScoped<WopiOriginValidationActionFilter>();
        services.AddControllers()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).GetTypeInfo().Assembly) // Add controllers from this assembly
            .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null); // Ensure PascalCase property name-style

        services.AddAuthentication(o => { o.DefaultScheme = AccessTokenDefaults.AUTHENTICATION_SCHEME; })
            .AddTokenAuthentication(
                AccessTokenDefaults.AUTHENTICATION_SCHEME,
                AccessTokenDefaults.AUTHENTICATION_SCHEME,
                options => { });

        // default options
        services.AddOptions<WopiHostOptions>()
            .Configure(o =>
            {
                o.UseCobalt = false;
            });

        return services;
    }

    /// <summary>
    /// Register services required by the WOPI host server.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="WopiHostOptions"/>.</param>
    public static IServiceCollection AddWopi(this IServiceCollection services, Action<WopiHostOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddWopi();
        services.Configure(configureOptions);
        return services;
    }
}