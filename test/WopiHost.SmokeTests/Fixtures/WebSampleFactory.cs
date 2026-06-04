using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Components;
using WopiHost.Web.Services;
using WopiHost.Web.Shared;

namespace WopiHost.SmokeTests.Fixtures;

/// <summary>
/// Hosts <c>sample/WopiHost.Web</c> behind a real Kestrel listener on a random localhost port so
/// Playwright (which drives a browser) can reach it.
/// </summary>
/// <remarks>
/// <c>WebApplicationFactory&lt;T&gt;</c> is avoided here because its <c>StartServer</c>
/// hard-casts <c>IServer</c> to <c>TestServer</c>, which crashes once Kestrel is swapped in.
/// Instead this replicates the relevant pieces of <c>sample/WopiHost.Web/Program.cs</c>
/// (Razor Components, discovery, storage provider) and binds Kestrel directly. The DI wiring is
/// intentionally minimal — just enough to make the Browse page render — and the
/// <see cref="IDiscoverer"/> registration is replaced with a <see cref="FakeDiscoverer"/> so
/// the page doesn't try to fetch discovery XML from an unreachable test hostname.
/// </remarks>
public sealed class WebSampleFactory : IDisposable, IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>Loopback URL the listener bound to.</summary>
    public Uri ServerUrl { get; }

    public WebSampleFactory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        // Mirror sample/WopiHost.Web/Program.cs config — minus AddServiceDefaults (Aspire-only).
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wopi:HostUrl"] = "http://wopihost.test",
            ["Wopi:ClientUrl"] = "https://office.example.test",
            ["Wopi:Discovery:NetZone"] = "ExternalHttps",
            ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
            ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
        });

        builder.Services.AddRazorComponents();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<WopiAccessTokenMinter>();

        builder.Services
            .AddOptionsWithValidateOnStart<WopiOptions>()
            .Bind(builder.Configuration.GetRequiredSection(WopiOptions.SectionName))
            .ValidateDataAnnotations();

        // Faked discoverer instead of AddWopiDiscovery — avoids HTTP fetch from ClientUrl.
        builder.Services.AddSingleton<IDiscoverer, FakeDiscoverer>();

        builder.Services.AddFileSystemStorageProvider(builder.Configuration);

        var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseStaticFiles();
        // UseAntiforgery is required by Razor Components even on a read-only sample.
        app.UseAntiforgery();
        app.MapRazorComponents<App>();

        _app = app;
        _app.StartAsync().GetAwaiter().GetResult();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var bound = addresses?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("No address bound — Kestrel didn't start as expected.");
        ServerUrl = new Uri(bound);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
