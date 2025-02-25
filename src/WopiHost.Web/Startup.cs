using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Models;

namespace WopiHost.Web;

public class Startup
{
    protected Startup()
    {
    }

    /// <summary>
    /// Sets up the DI container.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews()
            .AddRazorRuntimeCompilation(); // Add browser link

        // Configuration
        services.AddOptions();
        services.AddOptions<WopiOptions>(WopiConfigurationSections.WOPI_ROOT);

        services.AddHttpClient<IDiscoveryFileProvider, HttpDiscoveryFileProvider>((sp, client) =>
        {
            var wopiOptions = sp.GetRequiredService<IOptions<WopiOptions>>();
            client.BaseAddress = wopiOptions.Value.ClientUrl;
        });
        services.AddOptions<DiscoveryOptions>(WopiConfigurationSections.DISCOVERY_OPTIONS);
        services.AddSingleton<IDiscoverer, WopiDiscoverer>();

        services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();//Configuration.GetSection("Logging")
            loggingBuilder.AddDebug();
        });
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

        // Add MVC to the request pipeline.
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}