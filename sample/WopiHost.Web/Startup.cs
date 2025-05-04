using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Models;

namespace WopiHost.Web;

public class Startup(IConfiguration configuration)
{
    /// <summary>
    /// Sets up the DI container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole(); //Configuration.GetSection("Logging")
            loggingBuilder.AddDebug();
        });

        services.AddControllersWithViews()
            .AddRazorRuntimeCompilation(); // Add browser link

        // Configuration
        services
            .AddOptionsWithValidateOnStart<WopiOptions>()
            .Bind(configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT))
            .ValidateDataAnnotations();
        services.AddOptions<DiscoveryOptions>()
            .Bind(configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS));

        services.AddHttpClient<IDiscoveryFileProvider, HttpDiscoveryFileProvider>((sp, client) =>
        {
            var wopiOptions = sp.GetRequiredService<IOptions<WopiOptions>>();
            client.BaseAddress = wopiOptions.Value.ClientUrl;
        });

        services.AddSingleton<IDiscoverer, WopiDiscoverer>();
        services.AddSingleton<InMemoryFileIds>();
        services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
    }

    /// <summary>
    /// Configure is called after ConfigureServices is called.
    /// </summary>
    public static void Configure(IApplicationBuilder app)
    {
        app.UseDeveloperExceptionPage();

        //app.UseHttpsRedirection();

        // Add static files to the request pipeline.
        app.UseStaticFiles();

        app.UseRouting();

        //Add MVC to the request pipeline.
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}