# WopiHost.Cobalt

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Cobalt.svg)](https://www.nuget.org/packages/WopiHost.Cobalt)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Cobalt.svg)](https://www.nuget.org/packages/WopiHost.Cobalt)

[MS-FSSHTTP](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/) ("Cobalt") implementation of [`ICobaltProcessor`](../WopiHost.Abstractions/ICobaltProcessor.cs). Office 2013+ uses Cobalt for delta-based file updates when both sides advertise it; modern Office Online Server falls back to plain `PutFile` if it is absent. Most hosts can leave this package out.

## Why you might (not) want this

In Office Web Apps 2013, several actions required Cobalt. Office Online Server 2016+ no longer requires it, but will use it opportunistically (signalled by the [`SupportsCobalt`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#supportscobalt) flag in `CheckFileInfo`) for more efficient large-document updates.

> [!IMPORTANT]
> This package depends on `Microsoft.CobaltCore.dll`, which ships with Office Online Server / Office Web Apps and **cannot be redistributed**. To consume `WopiHost.Cobalt` you must build your own `Microsoft.CobaltCore` NuGet package from a licensed installation. See the [step-by-step wiki guide](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package).

## Install

```bash
dotnet add package WopiHost.Cobalt
```

You also need a `Microsoft.CobaltCore` package available to the build (see the wiki link above).

## Wire it up

The sample server registers Cobalt by reflection-loading the assembly when `Wopi:UseCobalt` is `true`. The relevant snippet from [`sample/WopiHost/ServiceCollectionExtensions.cs`](../../sample/WopiHost/ServiceCollectionExtensions.cs):

```csharp
public static void AddCobalt(this IServiceCollection services)
{
    var assemblyPath = Path.Combine(AppContext.BaseDirectory, "WopiHost.Cobalt.dll");
    var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    services.Scan(s => s
        .FromAssemblies(asm)
        .AddClasses(c => c.AssignableTo<ICobaltProcessor>())
        .AsImplementedInterfaces());
}
```

Then in `Program.cs`:

```csharp
if (builder.Configuration.GetValue<bool>("Wopi:UseCobalt"))
{
    builder.Services.AddCobalt();
}
```

If you prefer direct DI, the package exposes a single concrete: register `CobaltProcessor` as `ICobaltProcessor`.

```csharp
services.AddSingleton<ICobaltProcessor, CobaltProcessor>();
```

When `ICobaltProcessor` is in the container, `WopiHost.Core`'s `FilesController` flips `WopiHostCapabilities.SupportsCobalt = true` automatically, so the WOPI client knows to use it.

## Configuration

```jsonc
{
  "Wopi": {
    "UseCobalt": true,
    "ClientUrl": "https://your-office-online-server.com"
  }
}
```

`UseCobalt` is read by the sample's `Program.cs` to decide whether to call `AddCobalt()`. The library itself takes no configuration.

## API

```csharp
public interface ICobaltProcessor
{
    Task<byte[]> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent);
}
```

`FilesController` invokes this from the [`POST /wopi/files/{id}` with `X-WOPI-Override: COBALT`](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/) handler. The processor reads the file via `IWopiFile.GetReadStream()`, applies the request batch, writes back through `GetWriteStream()` if content changed, and returns the response batch as bytes.

## License

This package contains code originally written by [Marx Yu](https://github.com/marx-yu) — see [ORIGINAL_WORK_LICENSE.txt](ORIGINAL_WORK_LICENSE.txt). For everything else see the [repo README](../../README.md#license).
