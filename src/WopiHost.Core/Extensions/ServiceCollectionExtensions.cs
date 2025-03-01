using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extensions for registering WOPI into the application pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core WOPI services and controllers to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">Service collection to add WOPI services to.</param>
    public static void AddWopi(this IServiceCollection services)
    {
        services.AddAuthorizationCore();

        // Add authorization handler
        services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();

        services.AddControllers()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).GetTypeInfo().Assembly) // Add controllers from this assembly
            .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null); // Ensure PascalCase property name-style

        services.AddAuthentication(o => { o.DefaultScheme = AccessTokenDefaults.AUTHENTICATION_SCHEME; })
            .AddTokenAuthentication(
                AccessTokenDefaults.AUTHENTICATION_SCHEME, 
                AccessTokenDefaults.AUTHENTICATION_SCHEME, 
                options => { });
    }
}
