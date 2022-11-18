using Autofac;
using Autofac.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using WopiHost.Core;
using WopiHost.Core.Models;
using Serilog;

namespace WopiHost;

public class Startup
{
    public IConfiguration Configuration { get; set; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        var config = Configuration.GetSection(WopiConfigurationSections.WOPI_ROOT).Get<WopiHostOptions>();
        // Add file provider
        builder.AddFileProvider(config.StorageProviderAssemblyName);

        if (config.UseCobalt)
        {
            // Add cobalt
            builder.AddCobalt();
        }
    }

    /// <summary>
    /// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers(); //.AddControllersAsServices(); https://autofaccn.readthedocs.io/en/latest/integration/aspnetcore.html#controllers-as-services

        // Ideally, pass a persistent dictionary implementation
        services.AddSingleton<IDictionary<string, LockInfo>>(d => new Dictionary<string, LockInfo>());

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole(); //Configuration.GetSection("Logging")
            loggingBuilder.AddDebug();
        });

        // Configuration
        services.AddOptions();

        var config = Configuration.GetSection(WopiConfigurationSections.WOPI_ROOT);

        services.Configure<WopiHostOptions>(config);

        // Add WOPI (depends on file provider)
        services.AddWopi(GetSecurityHandler(services, config.Get<WopiHostOptions>().StorageProviderAssemblyName));
    }

    private IWopiSecurityHandler GetSecurityHandler(IServiceCollection services, string storageProviderAssemblyName)
    {
        var providerBuilder = new ContainerBuilder();
        // Add file provider implementation
        providerBuilder.AddFileProvider(storageProviderAssemblyName); //TODO: why?
        providerBuilder.Populate(services);
        var providerContainer = providerBuilder.Build();
        return providerContainer.Resolve<IWopiSecurityHandler>();
    }

    /// <summary>
    /// Configure is called after ConfigureServices is called.
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        //app.UseHttpsRedirection();
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = LogHelper.EnrichWithWopiDiagnostics;
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} with [WOPI CorrelationID: {" + nameof(WopiHeaders.CORRELATION_ID) + "}, WOPI SessionID: {" + nameof(WopiHeaders.SESSION_ID) + "}] responded {StatusCode} in {Elapsed:0.0000} ms";
        });

        app.UseRouting();

        // Automatically authenticate
        app.UseAuthentication();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
