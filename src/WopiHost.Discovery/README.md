# WopiHost.Discovery

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery)

Parses the [WOPI client discovery XML](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery) to determine which file extensions and actions a WOPI client (Office Online Server / Microsoft 365 for the Web) supports, and returns the corresponding URL templates. Used by [WopiHost.Url](../WopiHost.Url/README.md) and consumed indirectly by host pages.

## Install

```bash
dotnet add package WopiHost.Discovery
```

## Use

```csharp
services.AddWopiDiscovery<WopiHostOptions>(o =>
{
    o.NetZone         = NetZoneEnum.ExternalHttps;
    o.RefreshInterval = TimeSpan.FromHours(12);
});
```

`AddWopiDiscovery<TOptions>` registers an `HttpClient`-backed [`IDiscoveryFileProvider`](IDiscoveryFileProvider.cs) and the singleton [`IDiscoverer`](IDiscoverer.cs). The generic parameter is the *host's* options type (must implement `IDiscoveryOptions`) — it is the source of `ClientUrl`, the WOPI client's base URI.

```csharp
public class DiscoveryController(IDiscoverer discoverer) : ControllerBase
{
    [HttpGet("capabilities/{extension}")]
    public async Task<IActionResult> Capabilities(string extension) => Ok(new
    {
        Supported    = await discoverer.SupportsExtensionAsync(extension),
        CanEdit      = await discoverer.SupportsActionAsync(extension, WopiActionEnum.Edit),
        CanView      = await discoverer.SupportsActionAsync(extension, WopiActionEnum.View),
        Requirements = await discoverer.GetActionRequirementsAsync(extension, WopiActionEnum.Edit),
        EditTemplate = await discoverer.GetUrlTemplateAsync(extension, WopiActionEnum.Edit),
    });
}
```

## Configuration

```jsonc
"Wopi": {
  "ClientUrl": "https://your-office-online-server.com",
  "Discovery": {
    "NetZone":         "ExternalHttps",  // InternalHttp | InternalHttps | ExternalHttp | ExternalHttps
    "RefreshInterval": "12:00:00"
  }
}
```

`RefreshInterval` controls how often the discoverer refetches `/hosting/discovery` from the client. The result is cached in-memory; a single fetch failure does not invalidate the prior result.

## API surface

```csharp
public interface IDiscoverer
{
    Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action);
    Task<bool>    SupportsExtensionAsync(string extension);
    Task<bool>    SupportsActionAsync(string extension, WopiActionEnum action);
    Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action);
    Task<bool>    RequiresCobaltAsync(string extension, WopiActionEnum action);
    Task<string?> GetApplicationNameAsync(string extension);
    Task<Uri?>    GetApplicationFavIconAsync(string extension);
    Task<WopiProofKeys> GetProofKeysAsync();
}
```

`GetProofKeysAsync` returns the public keys used by [`WopiProofValidator`](../WopiHost.Core/Security/WopiProofValidator.cs) to verify the `X-WOPI-Proof` header. Don't call it directly unless you're implementing your own proof validation.

## Custom discovery sources

The default `HttpDiscoveryFileProvider` fetches discovery over HTTP. To use a fixed XML (testing) or a custom transport, register your own implementation. `FileSystemDiscoveryFileProvider` ships in this package and is what the discovery tests use:

```csharp
services.AddSingleton<IDiscoveryFileProvider>(_ =>
    new FileSystemDiscoveryFileProvider("path/to/discovery.xml"));
services.AddSingleton<IDiscoverer, WopiDiscoverer>();
services.Configure<DiscoveryOptions>(o => o.NetZone = NetZoneEnum.ExternalHttps);
```

Note: the `AddWopiDiscovery<>` extension registers `HttpDiscoveryFileProvider` unconditionally. If you replace the provider, register the discoverer manually as shown above.

## License

See the [repo README](../../README.md#license).
