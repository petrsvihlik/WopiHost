using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Security;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extensions for registering WOPI into the application pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WOPI host services. Defaults are wired with <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService,TImplementation}(IServiceCollection)"/>
    /// so consumers can override individual services (notably <see cref="IWopiPermissionProvider"/>
    /// and <see cref="IWopiAccessTokenService"/>) before or after this call.
    /// </summary>
    public static IServiceCollection AddWopi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddAuthorizationCore();

        services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();

        services.AddScoped<IWopiProofValidator, WopiProofValidator>();
        services.AddScoped<WopiOriginValidationActionFilter>();
        services.AddControllers()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).GetTypeInfo().Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

        services.AddAuthentication(o => { o.DefaultScheme = WopiAuthenticationSchemes.AccessToken; })
            .AddTokenAuthentication(
                WopiAuthenticationSchemes.AccessToken,
                WopiAuthenticationSchemes.AccessToken,
                _ => { });

        // Security pipeline defaults — overridable via DI.
        services.AddOptions<WopiSecurityOptions>();
        services.TryAddSingleton<IWopiAccessTokenService, JwtAccessTokenService>();
        services.TryAddSingleton<IWopiPermissionProvider, DefaultWopiPermissionProvider>();

        services.AddOptions<WopiHostOptions>()
            .Configure(o =>
            {
                o.UseCobalt = false;
            });

        return services;
    }

    /// <summary>
    /// Registers the WOPI host services and applies the supplied <see cref="WopiHostOptions"/>
    /// configuration delegate.
    /// </summary>
    public static IServiceCollection AddWopi(this IServiceCollection services, Action<WopiHostOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddWopi();
        services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// Configures the WOPI access-token signing pipeline.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>services.Configure&lt;WopiSecurityOptions&gt;(...)</c> but available
    /// as a one-line option alongside <see cref="AddWopi(IServiceCollection)"/>.
    /// </remarks>
    public static IServiceCollection ConfigureWopiSecurity(this IServiceCollection services, Action<WopiSecurityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        return services;
    }
}
