using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Extension methods for adding WOPI proof validation services.
/// </summary>
public static class WopiProofValidationExtensions
{
    /// <summary>
    /// Adds WOPI proof validation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWopiProofValidation(this IServiceCollection services)
    {
        services.AddScoped<WopiProofValidator>();
        services.AddScoped<WopiOriginValidationMiddleware>();
        
        return services;
    }

    /// <summary>
    /// Adds WOPI proof validation middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder to add the middleware to.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseWopiProofValidation(this IApplicationBuilder app)
    {
        app.UseMiddleware<WopiOriginValidationMiddleware>();
        
        return app;
    }
} 