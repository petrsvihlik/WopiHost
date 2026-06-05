# WopiHost.Cobalt

[MS-FSSHTTP](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/) ("Cobalt") implementation of [`ICobaltProcessor`](../WopiHost.Abstractions/ICobaltProcessor.cs). Office 2013+ uses Cobalt for delta-based file updates when both sides advertise it; modern Office Online Server falls back to plain `PutFile` if it is absent. Most hosts can leave this package out.

## Why you might (not) want this

In Office Web Apps 2013, several actions required Cobalt. Office Online Server 2016+ no longer requires it, but will use it opportunistically (signalled by the [`SupportsCobalt`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#supportscobalt) flag in `CheckFileInfo`) for more efficient large-document updates.

> [!IMPORTANT]
> This package depends on `Microsoft.CobaltCore.dll`, which ships with Office Online Server / Office Web Apps and **cannot be redistributed**. To consume `WopiHost.Cobalt` you must build your own `Microsoft.CobaltCore` NuGet package from a licensed installation. See the [step-by-step wiki guide](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package).

## Install

This package is **not published on NuGet.org** — `Microsoft.CobaltCore.dll` is proprietary and cannot be redistributed, so a public NuGet would never restore on someone else's machine. To consume `WopiHost.Cobalt`:

1. Build a `Microsoft.CobaltCore` NuGet from a licensed installation (see the [wiki guide](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package)) and put it on a private feed.
2. Reference `WopiHost.Cobalt` directly as a `<ProjectReference>` from this repo, or build and pack it from source into the same private feed.

## Wire it up

The sample server registers Cobalt by reflection-loading the assembly when `Wopi:UseCobalt` is `true`. The relevant snippet from [`sample/WopiHost/ServiceCollectionExtensions.cs`](../../sample/WopiHost/ServiceCollectionExtensions.cs):

```csharp
public static void AddCobalt(this IServiceCollection services)
{
    var assemblyPath = Path.Combine(AppContext.BaseDirectory, "WopiHost.Cobalt.dll");
    if (!File.Exists(assemblyPath))
    {
        throw new InvalidProgramException($"Cobalt Assembly {assemblyPath} not found.");
    }
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

When `ICobaltProcessor` is in the container, `WopiHost.Core`'s file endpoints flip `WopiHostCapabilities.SupportsCobalt = true` automatically, so the WOPI client knows to use it.

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
    Task<byte[]> ProcessCobalt(
        IWopiWritableFile file,
        ClaimsPrincipal principal,
        byte[] newContent,
        CancellationToken cancellationToken = default);
}
```

The `COBALT`-override file endpoint ([`POST /wopi/files/{id}` with `X-WOPI-Override: COBALT`](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/)) invokes this. Each call deserializes the request batch, executes it against the `CobaltFile` for that WOPI file, flushes content via `IWopiWritableFile.OpenWriteAsync()` when there's a `PutChangesRequest` for the content partition, and returns the serialized response batch.

The Cobalt protocol is stateful (schema/exclusive locks, edit deltas, co-authoring metadata), so `CobaltProcessor` keeps a long-lived `CobaltFile` per WOPI file id in a `ConcurrentDictionary` and evicts idle sessions on a periodic timer (60-minute idle timeout). The first call for a file id reads the file content via `IWopiFile.OpenReadAsync()` to seed the session; subsequent calls reuse it.

## License

This package contains code originally written by [Marx Yu](https://github.com/marx-yu) — see [ORIGINAL_WORK_LICENSE.txt](ORIGINAL_WORK_LICENSE.txt). For everything else see the [repo README](../../README.md#license).
