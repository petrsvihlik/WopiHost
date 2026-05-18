using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Events;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Endpoints;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security;
using WopiHost.Core.Security.Authentication;
using WopiHost.Discovery;
using WopiHost.ServiceDefaults;
using Scalar.AspNetCore;

namespace WopiHost;

public partial class Program
{
    /// <summary>Marker for <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
    private Program() { }

    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();

        try
        {
            Log.Information("Starting WOPI host");

            var builder = WebApplication.CreateBuilder(args);

            // Add Serilog
            builder.Host.UseSerilog();

            // Add service defaults from Aspire
            builder.AddServiceDefaults();

            // Add services to the container
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });

            // Configuration
            // this makes sure that the configuration exists and is valid
            var wopiHostOptionsSection = builder.Configuration.GetRequiredSection(WopiHostOptions.SectionName);
            builder.Services
                .AddOptionsWithValidateOnStart<WopiHostOptions>()
                .BindConfiguration(wopiHostOptionsSection.Path)
                .ValidateDataAnnotations();

            var wopiHostOptions = wopiHostOptionsSection.Get<WopiHostOptions>();

            // Provider selection lives in the sample's own config section (Sample:*), not in
            // WopiHost.Core's WopiHostOptions — choosing between bundled providers is composition-
            // root concern. Real hosts reference one provider package and call its typed extension
            // directly (services.AddFileSystemStorageProvider(cfg), etc.); the sample retains a
            // small switch so the AppHost flag flow can flip providers at runtime.
            var sampleStorage = builder.Configuration.GetValue("Sample:StorageProvider", ServiceCollectionExtensions.SampleStorageProvider.FileSystem);
            var sampleLock = builder.Configuration.GetValue("Sample:LockProvider", ServiceCollectionExtensions.SampleLockProvider.Memory);
            builder.Services.AddSampleStorageProvider(builder.Configuration, sampleStorage);
            builder.Services.AddSampleLockProvider(builder.Configuration, sampleLock);

            // Add Discovery services
            builder.Services.AddWopiDiscovery<WopiHostOptions>(
                options => builder.Configuration.GetSection(DiscoveryOptions.SectionName).Bind(options));

            // Add Cobalt support
            if (wopiHostOptions.UseCobalt)
            {
                // Add cobalt
                builder.Services.AddCobalt();
            }

            // Add OpenAPI
            builder.Services.AddOpenApi();

            // Add WOPI
            builder.Services.AddWopi();

            // Signing key for WOPI access tokens. MUST match whatever the URL-generating
            // frontend (sample/WopiHost.Web) uses or every token will fail validation.
            // In production: load from a managed secret store, never hard-code.
            //
            // Two-step binding: first read the configured values via Bind (gets SigningKey,
            // DisableProofValidation, etc.), then PostConfigure to apply the dev-key fallback
            // *after* configuration has been read — keeps the fallback out of the hot path of
            // every option read and makes the precedence explicit.
            builder.Services
                .AddOptions<WopiSecurityOptions>()
                .Bind(builder.Configuration.GetSection(WopiSecurityOptions.SectionName));

            builder.Services.PostConfigure<WopiSecurityOptions>(o =>
            {
                if (o.SigningKey is null || o.SigningKey.Length == 0)
                {
                    o.SigningKey = JwtAccessTokenService.DeriveHmacKey("wopi-sample-shared-dev-key");
                }
            });

            // Dev-only escape hatch for WOPI clients that do not sign requests with proof keys
            // (e.g. Collabora Online — proof keys are an OOS / M365-for-the-Web feature). Without
            // this, every WOPI callback 500s in WopiOriginValidationActionFilter because
            // sourceProofKeys.Value is null. Refuses to enable outside Development so a stray
            // production config cannot silently disable signature checking.
            var bootSecurityOptions = builder.Configuration
                .GetSection(WopiSecurityOptions.SectionName)
                .Get<WopiSecurityOptions>();
            if (bootSecurityOptions?.DisableProofValidation == true)
            {
                if (!builder.Environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        $"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.DisableProofValidation)} " +
                        "can only be enabled in the Development environment.");
                }
                Log.Warning("WOPI proof validation is DISABLED. This is a development-only setting.");
                builder.Services.RemoveAll<IWopiProofValidator>();
                builder.Services.AddSingleton<IWopiProofValidator, NoOpProofValidator>();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // Map OpenAPI endpoint (only in development)
                app.MapOpenApi();

                app.MapScalarApiReference(options => options.WithTitle(nameof(WopiHost)).WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
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

            app.MapWopiEndpoints();
            app.MapGet("/", () => "This is just a WOPI server. You need a WOPI client to access it...").ShortCircuit(404);

            // Map default endpoints from Aspire — owns /health and /alive (Development only,
            // per the security note in WopiHost.ServiceDefaults.MapDefaultEndpoints). Hosts that
            // need health endpoints in non-Development should add their own MapHealthChecks call
            // here, gated by authn/authz appropriate to the deployment environment.
            app.MapDefaultEndpoints();

            app.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WOPI Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

/// <summary>
/// Dev-only no-op proof validator used when <see cref="WopiSecurityOptions.DisableProofValidation"/>
/// is set (typically with the AppHost's <c>UseCollabora</c> flag, since Collabora does not sign
/// WOPI callbacks with proof keys). Real hosts must keep <c>WopiProofValidator</c>.
/// </summary>
internal sealed class NoOpProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(HttpContext httpContext, string accessToken)
        => Task.FromResult(true);
}
