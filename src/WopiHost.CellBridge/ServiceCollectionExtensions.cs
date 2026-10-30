using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;

namespace WopiHost.CellBridge;

/// <summary>
/// Registration for the cellbridge-backed MS-FSSHTTP processor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CellBridgeProcessor"/> as the host's <see cref="ICobaltProcessor"/>, binding
    /// <see cref="CellBridgeProcessorOptions"/> from <c>Wopi:CellBridge</c>. Exactly one MS-FSSHTTP
    /// backend can be active per host, so a second registration is a configuration error.
    /// </summary>
    public static IServiceCollection AddCellBridgeProcessor(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(d => d.ServiceType == typeof(ICobaltProcessor)))
        {
            throw new InvalidOperationException(
                $"An {nameof(ICobaltProcessor)} is already registered. Exactly one MS-FSSHTTP backend (Microsoft.CobaltCore or cellbridge) can be active per host.");
        }

        services.AddOptions<CellBridgeProcessorOptions>()
            .Bind(configuration.GetSection(CellBridgeProcessorOptions.SectionName))
            .ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddSingleton<ICobaltProcessor, CellBridgeProcessor>();
        return services;
    }
}
