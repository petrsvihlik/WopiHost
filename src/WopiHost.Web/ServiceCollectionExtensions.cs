namespace WopiHost.Web;

public static class ServiceCollectionExtensions
{
    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration) where TConfig : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var config = new TConfig();
        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }

    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration, Func<TConfig> pocoProvider) where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(pocoProvider);

        var config = pocoProvider();
        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }

    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration, TConfig config) where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(config);

        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }
}
