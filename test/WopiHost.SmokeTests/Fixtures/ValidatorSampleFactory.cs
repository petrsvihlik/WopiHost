using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Validator;
using WopiHost.Validator.Infrastructure;

namespace WopiHost.SmokeTests.Fixtures;

/// <summary>
/// Hosts <c>sample/WopiHost.Validator</c> behind a real Kestrel listener for browser-driven
/// smoke tests. Mirrors the relevant pieces of <c>sample/WopiHost.Validator/Program.cs</c>
/// — controllers, Razor Pages, the WOPI server bits, the no-op proof validator the validator
/// sample needs — and replaces <see cref="IDiscoverer"/> with <see cref="FakeDiscoverer"/> so
/// the Index page renders without a reachable WOPI client.
/// </summary>
public sealed class ValidatorSampleFactory : IDisposable, IAsyncDisposable
{
    private readonly WebApplication _app;

    public string ServerUrl { get; }

    public ValidatorSampleFactory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wopi:UseCobalt"] = "false",
            ["Wopi:StorageProviderAssemblyName"] = "WopiHost.FileSystemProvider",
            ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
            ["Wopi:LockProviderAssemblyName"] = "WopiHost.MemoryLockProvider",
            ["Wopi:HostUrl"] = "http://localhost",
            ["Wopi:ClientUrl"] = "https://office.example.test",
            ["Wopi:Discovery:NetZone"] = "ExternalHttps",
            ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
            ["Wopi:UserId"] = "Anonymous",
            ["Wopi:Security:DisableProofValidation"] = "true",
            ["Wopi:Security:SigningKey"] = Convert.ToBase64String(new byte[64]),
        });

        // Same as WebSampleFactory: register the sample's assembly with MVC so its controllers /
        // Razor pages are discovered when the test project is the entry assembly.
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ValidatorSampleEntryPoint).Assembly);
        builder.Services.AddRazorPages();

        // Mirror sample/WopiHost.Validator/Program.cs: WopiServer + HostPages + no-op proof validator.
        builder.Services.AddWopiLogging();
        builder.Services.AddWopiServer(builder.Configuration);
        builder.Services.AddWopiHostPages(builder.Configuration);
        builder.Services.AddScoped<IWopiProofValidator, NoOpProofValidator>();

        // Faked discoverer instead of the HTTP-backed one — registered LAST so it wins over
        // whatever AddWopiServer / AddWopiHostPages registered.
        builder.Services.RemoveAll<IDiscoverer>();
        builder.Services.AddSingleton<IDiscoverer, FakeDiscoverer>();

        var app = builder.Build();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapControllers();

        _app = app;
        _app.StartAsync().GetAwaiter().GetResult();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        ServerUrl = addresses?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("No address bound — Kestrel didn't start as expected.");
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
