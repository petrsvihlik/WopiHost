using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
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
/// Provides extension methods that register the WOPI host services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WOPI host services: access-token authentication, proof validation,
    /// authorization, the endpoint filters and the <c>X-WOPI-Override</c> matcher policy.
    /// </summary>
    /// <remarks>
    /// Replaceable defaults (notably <see cref="IWopiPermissionProvider"/> and
    /// <see cref="IWopiAccessTokenService"/>) are registered with <c>TryAdd*</c>, so a host's own
    /// registration wins whether it is made before or after this call. The WOPI routes themselves
    /// are mapped by <see cref="Endpoints.WopiEndpointRouteBuilderExtensions.MapWopiEndpoints"/>.
    /// </remarks>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddWopi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.AddRouting(options => options.LowercaseUrls = true);
        // AddAuthorization (not AddAuthorizationCore) — brings in the middleware-tier services
        // that app.UseAuthorization() needs.
        services.AddAuthorization();

        services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();

        services.AddScoped<IWopiProofValidator, WopiProofValidator>();

        // Minimal-API endpoint filters + the override-header matcher policy. Consumers wire
        // the surface up via app.MapWopiEndpoints().
        services.AddSingleton<MatcherPolicy, WopiOverrideMatcherPolicy>();
        services.AddScoped<WopiOriginValidationEndpointFilter>();
        services.AddScoped<WopiTelemetryEndpointFilter>();
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(
            o => o.SerializerOptions.PropertyNamingPolicy = null);

        services.AddAuthentication(o => { o.DefaultScheme = WopiAuthenticationSchemes.AccessToken; })
            .AddTokenAuthentication(
                WopiAuthenticationSchemes.AccessToken,
                WopiAuthenticationSchemes.AccessToken,
                _ => { });

        // Security pipeline defaults — overridable via DI.
        services.AddOptions<WopiSecurityOptions>();
        services.TryAddSingleton<IWopiAccessTokenService, JwtAccessTokenService>();
        services.TryAddSingleton<IWopiPermissionProvider, DefaultWopiPermissionProvider>();

        // Per-resource token mint orchestrator used by every endpoint that surfaces a child or
        // ancestor URL in a response (EnumerateAncestors, EnumerateChildren, PutRelativeFile,
        // CreateChildFile, CreateChildContainer, ecosystem_pointer). Centralising the mint
        // (permission lookup + WopiAccessTokenRequest build + IssueAsync call) here keeps the
        // token-trading prevention shape in one place and lets a host override the policy.
        services.TryAddSingleton<IWopiResourceTokenMinter, ResourceTokenMinter>();

        // Lock-id comparison: strict by default. Hosts that need tolerant comparison (e.g. for
        // OOS-style JSON-shaped lock mutations) replace this registration with their own
        // IWopiLockComparer (JsonShapedWopiLockComparer ships as one option).
        services.TryAddSingleton<IWopiLockComparer, OrdinalWopiLockComparer>();

        // Host-customization seam. Pass-through default; hosts register a subclass of
        // WopiHostExtensions to plug in audit, telemetry, response mutations, etc.
        services.TryAddSingleton<IWopiHostExtensions, WopiHostExtensions>();

        // CheckXxxInfo response builders. Scoped because the writable storage provider may be
        // scoped, and the builders capture it.
        services.TryAddScoped<ICheckFileInfoBuilder, DefaultCheckFileInfoBuilder>();
        services.TryAddScoped<ICheckContainerInfoBuilder, DefaultCheckContainerInfoBuilder>();
        services.TryAddScoped<ICheckFolderInfoBuilder, DefaultCheckFolderInfoBuilder>();

        // Suggested-target / relative-target name-negotiation for PutRelativeFile and
        // CreateChildFile — the spec defines the protocol identically for both, so it lives
        // in one place.
        services.TryAddScoped<IWopiNewChildFileNegotiator, DefaultWopiNewChildFileNegotiator>();

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
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configureOptions">The delegate that configures <see cref="WopiHostOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.Services.AddWopi(o =&gt; o.ClientUrl = new Uri("https://your-office-online-server.com"));
    /// builder.Services.AddFileSystemStorageProvider(builder.Configuration);
    /// builder.Services.AddMemoryLockProvider();
    /// builder.Services.ConfigureWopiSecurity(o =&gt;
    ///     o.SigningKey = Convert.FromBase64String(
    ///         builder.Configuration[$"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.SigningKey)}"]!));
    ///
    /// var app = builder.Build();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapWopiEndpoints();
    /// app.Run();
    /// </code>
    /// </example>
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
    /// Equivalent to <c>services.Configure&lt;WopiSecurityOptions&gt;(...)</c>. Production hosts
    /// pin <see cref="WopiSecurityOptions.SigningKey"/> here.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The delegate that configures <see cref="WopiSecurityOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
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
    /// when the target is locked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defense in depth: the WOPI endpoints already short-circuit on locks; the decorator covers
    /// non-WOPI code paths (admin tools, batch jobs) that resolve the storage provider directly.
    /// </para>
    /// <para>
    /// Must be called <i>after</i> the storage provider is registered (for example, via
    /// <c>AddFileSystemStorageProvider(...)</c>). An <see cref="IWopiLockProvider"/> must also be
    /// registered by the time the decorated provider is first resolved.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection containing the storage provider registration.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
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
