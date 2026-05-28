# WopiHost.Url

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url)

Resolves the URL templates exposed in the WOPI client's [discovery XML](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery) into the concrete URLs you embed in iframes / `<form>` posts. Pairs with [WopiHost.Discovery](../WopiHost.Discovery/README.md).

## Install

```bash
dotnet add package WopiHost.Url
```

## Use

```csharp
var urlBuilder = new WopiUrlBuilder(discoverer, new WopiUrlSettings
{
    UiLlcc = CultureInfo.CurrentUICulture,
});

var fileUrl = new Uri("https://your-host.example/wopi/files/AbC123");
var editUrl = await urlBuilder.GetFileUrlAsync("docx", fileUrl, WopiActionEnum.Edit);
```

`GetFileUrlAsync` looks up the template for `(extension, action)` in the discovery XML, substitutes the placeholders that you populated in `WopiUrlSettings`, drops the placeholders you didn't, and appends the mandatory `WOPISrc=<wopiFileUrl>` parameter. It returns `null` if the WOPI client doesn't advertise a template for that combination.

You can also pass per-call settings; they are merged on top of the constructor defaults:

```csharp
var businessUrl = await urlBuilder.GetFileUrlAsync(
    "xlsx", fileUrl, WopiActionEnum.Edit,
    new WopiUrlSettings { BusinessUser = 1, SessionContext = "user-42" });
```

## Settings

[`WopiUrlSettings`](WopiUrlSettings.cs) is a `Dictionary<string, string>` with strongly-typed accessors for each placeholder defined by the [WOPI spec](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values):

| Property | Placeholder | Type |
|---|---|---|
| `UiLlcc` | `UI_LLCC` | `CultureInfo` |
| `DcLlcc` | `DC_LLCC` | `CultureInfo` |
| `Embedded` | `EMBEDDED` | `bool` |
| `DisableAsync` | `DISABLE_ASYNC` | `bool` |
| `DisableBroadcast` | `DISABLE_BROADCAST` | `bool` |
| `Fullscreen` | `FULLSCREEN` | `bool` |
| `Recording` | `RECORDING` | `bool` |
| `ThemeId` | `THEME_ID` | `int` (1 = light, 2 = dark) |
| `BusinessUser` | `BUSINESS_USER` | `int` |
| `DisableChat` | `DISABLE_CHAT` | `int` |
| `Perfstats` | `PERFSTATS` | `int` |
| `HostSessionId` | `HOST_SESSION_ID` | `string` |
| `SessionContext` | `SESSION_CONTEXT` | `string` |
| `WopiSource` | `WOPI_SOURCE` | `string` (set automatically — usually leave alone) |
| `ValidatorTestCategory` | `VALIDATOR_TEST_CATEGORY` | `ValidatorTestCategoryEnum` |

Add raw `key/value` pairs directly via the dictionary indexer for placeholders not yet covered by a typed property.

## DI

```csharp
services.AddWopiDiscovery<WopiHostOptions>(o => Configuration.GetSection("Wopi:Discovery").Bind(o));
services.AddSingleton(sp => new WopiUrlBuilder(sp.GetRequiredService<IDiscoverer>()));
```

`WopiUrlBuilder` is cheap to construct but the underlying `IDiscoverer` holds the discovery-XML cache, so register it as a singleton. Pass per-call settings via the `urlSettings` argument on `GetFileUrlAsync` instead of constructing a new builder.

## License

See the [repo README](../../README.md#license).
