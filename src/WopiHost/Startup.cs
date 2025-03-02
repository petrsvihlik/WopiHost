using WopiHost.Abstractions;
using WopiHost.Core.Models;
using Serilog;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost;

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

        // Configuration
        // this makes sure that the configuration exists and is valid
        var wopiHostOptionsSection = configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT);
        services
            .AddOptionsWithValidateOnStart<WopiHostOptions>()
            .BindConfiguration(wopiHostOptionsSection.Path) 
            .ValidateDataAnnotations();

        var wopiHostOptions = wopiHostOptionsSection.Get<WopiHostOptions>();
        // Add file provider
        services.AddStorageProvider(wopiHostOptions.StorageProviderAssemblyName);
        // Add lock provider
        services.AddLockProvider(wopiHostOptions.LockProviderAssemblyName);
        // Add Cobalt support
        if (wopiHostOptions.UseCobalt)
        {
            // Add cobalt
            services.AddCobalt();
        }

        services.AddControllers();

        // Add WOPI
        services.AddWopi(o =>
        {
            o.OnCheckFileInfo = GetWopiCheckFileInfo;
        });
    }

    /// <summary>
    /// Configure is called after ConfigureServices is called.
    /// </summary>
    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = LogHelper.EnrichWithWopiDiagnostics;
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} with [WOPI CorrelationID: {" + nameof(WopiHeaders.CORRELATION_ID) + "}, WOPI SessionID: {" + nameof(WopiHeaders.SESSION_ID) + "}] responded {StatusCode} in {Elapsed:0.0000} ms";
        });

        app.UseRouting();

        // Automatically authenticate
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => "This is just a WOPI server. You need a WOPI client to access it...").ShortCircuit(404);
        });
    }

    /// <summary>
    /// Custom handling of CheckFileInfo results for WOPI-Validator
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private static Task<WopiCheckFileInfo> GetWopiCheckFileInfo(WopiCheckFileInfoContext context)
    {
        var wopiCheckFileInfo = context.CheckFileInfo;
        wopiCheckFileInfo.AllowAdditionalMicrosoftServices = true;
        wopiCheckFileInfo.AllowErrorReportPrompt = true;

        // ##183 required for WOPI-Validator
        if (wopiCheckFileInfo.BaseFileName == "test.wopitest")
        {
            wopiCheckFileInfo.CloseUrl = new("https://example.com/close");
            wopiCheckFileInfo.DownloadUrl = new("https://example.com/download");
            wopiCheckFileInfo.FileSharingUrl = new("https://example.com/share");
            wopiCheckFileInfo.FileUrl = new("https://example.com/file");
            wopiCheckFileInfo.FileVersionUrl = new("https://example.com/version");
            wopiCheckFileInfo.HostEditUrl = new("https://example.com/edit");
            wopiCheckFileInfo.HostEmbeddedViewUrl = new("https://example.com/embedded");
            wopiCheckFileInfo.HostEmbeddedEditUrl = new("https://example.com/embeddededit");
            wopiCheckFileInfo.HostRestUrl = new("https://example.com/rest");
            wopiCheckFileInfo.HostViewUrl = new("https://example.com/view");
            wopiCheckFileInfo.SignInUrl = new("https://example.com/signin");
            wopiCheckFileInfo.SignoutUrl = new("https://example.com/signout");

            wopiCheckFileInfo.ClientUrl = new("https://example.com/client");
            wopiCheckFileInfo.FileEmbedCommandUrl = new("https://example.com/embed");

            // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#breadcrumb-properties
            wopiCheckFileInfo.BreadcrumbBrandName = "WopiHost";
            wopiCheckFileInfo.BreadcrumbBrandUrl = new("https://example.com");
            wopiCheckFileInfo.BreadcrumbDocName = "test";
            wopiCheckFileInfo.BreadcrumbFolderName = "root";
            wopiCheckFileInfo.BreadcrumbFolderUrl = new("https://example.com/folder");
        }
        return Task.FromResult(wopiCheckFileInfo);
    }
}
