namespace WopiHost.Web;

public static class ServiceCollectionExtensions
{
    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration) where TConfig : class, new()
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var config = new TConfig();
        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }

    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration, Func<TConfig> pocoProvider) where TConfig : class
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (pocoProvider == null) throw new ArgumentNullException(nameof(pocoProvider));

        var config = pocoProvider();
        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }

    public static TConfig Configure<TConfig>(this IServiceCollection services, IConfiguration configuration, TConfig config) where TConfig : class
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (config == null) throw new ArgumentNullException(nameof(config));

        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }
}
