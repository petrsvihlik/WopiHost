using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
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
        services.AddScoped<WopiTelemetryActionFilter>();
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

        // Lock-id comparison: strict by default. Hosts that need tolerant comparison (e.g. for
        // OOS-style JSON-shaped lock mutations) replace this registration with their own
        // IWopiLockComparer (JsonShapedWopiLockComparer ships as one option).
        services.TryAddSingleton<IWopiLockComparer, OrdinalWopiLockComparer>();

        // Host-customization seam. Pass-through default; hosts register a subclass of
        // WopiHostExtensions to plug in audit, telemetry, response mutations, etc.
        services.TryAddSingleton<IWopiHostExtensions, WopiHostExtensions>();

        // CheckXxxInfo response builders. Scoped to match controller lifetime — the writable
        // storage provider may be scoped, and the builders capture it.
        services.TryAddScoped<ICheckFileInfoBuilder, DefaultCheckFileInfoBuilder>();
        services.TryAddScoped<ICheckContainerInfoBuilder, DefaultCheckContainerInfoBuilder>();
        services.TryAddScoped<ICheckFolderInfoBuilder, DefaultCheckFolderInfoBuilder>();

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

    /// <summary>
    /// Decorates the registered <see cref="IWopiWritableStorageProvider"/> with
    /// <see cref="WopiLockAwareWritableStorageProvider"/> so delete/rename operations consult
    /// <see cref="IWopiLockProvider"/> first and throw <see cref="WopiResourceLockedException"/>
    /// when the target is locked. Defense in depth — the controllers already short-circuit on
    /// locks; this decorator catches non-WOPI code paths (admin tools, batch jobs) and future
    /// controller regressions before they corrupt locked state.
    /// </summary>
    /// <remarks>
    /// Must be called <em>after</em> the storage provider is registered (typically via
    /// <c>AddStorageProvider(...)</c>) and after a lock provider is registered.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No <see cref="IWopiWritableStorageProvider"/>
    /// is registered when this method runs.</exception>
    public static IServiceCollection AddWopiLockAwareWritableStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var inner = services.LastOrDefault(d => d.ServiceType == typeof(IWopiWritableStorageProvider))
            ?? throw new InvalidOperationException(
                "AddWopiLockAwareWritableStorage requires an IWopiWritableStorageProvider to be registered first (typically via AddStorageProvider).");

        services.Remove(inner);
        services.Add(new ServiceDescriptor(
            typeof(IWopiWritableStorageProvider),
            sp =>
            {
                IWopiWritableStorageProvider innerInstance;
                if (inner.ImplementationInstance is not null)
                {
                    innerInstance = (IWopiWritableStorageProvider)inner.ImplementationInstance;
                }
                else if (inner.ImplementationFactory is not null)
                {
                    innerInstance = (IWopiWritableStorageProvider)inner.ImplementationFactory(sp);
                }
                else
                {
                    innerInstance = (IWopiWritableStorageProvider)ActivatorUtilities.CreateInstance(sp, inner.ImplementationType!);
                }
                return new WopiLockAwareWritableStorageProvider(
                    innerInstance,
                    sp.GetRequiredService<IWopiLockProvider>());
            },
            inner.Lifetime));
        return services;
    }
}
