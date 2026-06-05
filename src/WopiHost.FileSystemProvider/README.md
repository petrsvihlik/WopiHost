# WopiHost.FileSystemProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider)

Reference implementation of [`IWopiStorageProvider`](../WopiHost.Abstractions/IWopiStorageProvider.cs) and [`IWopiWritableStorageProvider`](../WopiHost.Abstractions/IWopiWritableStorageProvider.cs) backed by a local directory tree. Identifiers are deterministic SHA-256 hashes of the canonical path, cached in-memory and mapped back to absolute paths.

Suitable for development, validator runs, and single-instance deployments. Storage only — token issuance and ACLs live in [WopiHost.Core](../WopiHost.Core/README.md#security).

## Install

```bash
dotnet add package WopiHost.FileSystemProvider
```

## Configure

```jsonc
// appsettings.json
"Wopi": {
  "StorageProvider": {
    "RootPath": "./wopi-docs"   // absolute or relative to ContentRootPath
  }
}
```

`RootPath` is bound from the `Wopi:StorageProvider` section (`WopiFileSystemProviderOptions.SectionName`).

## Register

```csharp
builder.Services.AddFileSystemStorageProvider(builder.Configuration);
builder.Services.AddWopi(o =>
{
    o.ClientUrl = new Uri("https://your-office-online-server.com");
});
```

`AddFileSystemStorageProvider` registers `WopiFileSystemProvider` as both `IWopiStorageProvider` and `IWopiWritableStorageProvider` (one shared singleton instance) plus the singleton `InMemoryFileIds` map the provider uses for path↔id round-tripping. The runnable sample drives all of this through a sample-local `Sample:StorageProvider` discriminator — see [`sample/WopiHost/Program.cs`](../../sample/WopiHost/Program.cs).

## How identifiers work

Identifiers are deterministic 64-character SHA-256 hex hashes of the canonical (case-folded) path, computed by [`InMemoryFileIds`](InMemoryFileIds.cs) via `WopiResourceId.FromCanonicalPath`. Consumers treat them as opaque. Lookup is `O(1)` in both directions; the in-memory map is rebuilt at startup. (The WOPI validator's `test.wopitest` file is the one exception — it's given the fixed id `WOPITEST`.)

A consequence: because an id derives purely from the path, a file's id is **stable across process restarts** and across hosts pointing at the same tree, so long-lived WOPI URLs keep working without persisting a separate mapping. Renaming or moving a file changes its id (the path changed); the provider re-points the id on rename so an in-progress edit's URL doesn't break.

## Customize

To layer behavior — versioning, audit, soft-delete — wrap the provider via composition. The concrete class' interface methods are non-virtual, so a decorator is the cleanest seam:

```csharp
public class AuditingStorageProvider(IWopiWritableStorageProvider inner, IAuditLog audit)
    : IWopiWritableStorageProvider
{
    public async Task<bool> DeleteWopiFile(string id, CancellationToken ct = default)
    {
        var deleted = await inner.DeleteWopiFile(id, ct);
        if (deleted) audit.Log($"deleted file {id}");
        return deleted;
    }

    public async Task<bool> DeleteWopiContainer(string id, CancellationToken ct = default)
    {
        var deleted = await inner.DeleteWopiContainer(id, ct);
        if (deleted) audit.Log($"deleted container {id}");
        return deleted;
    }

    // Forward the rest of IWopiWritableStorageProvider to `inner`.
    public int FileNameMaxLength => inner.FileNameMaxLength;
    // ...
}
```

## License

See the [repo README](../../README.md#license).
