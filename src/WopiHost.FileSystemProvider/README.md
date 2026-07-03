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
    "RootPath": "./wopi-docs",          // absolute or relative to ContentRootPath
    "WatchForExternalChanges": true     // default; see "How identifiers work"
  }
}
```

`RootPath` is bound from the `Wopi:StorageProvider` section (`WopiFileSystemProviderOptions.SectionName`). Set `WatchForExternalChanges` to `false` on storage where change notifications are unreliable — network shares (SMB/NFS) and some container bind mounts — to skip the `FileSystemWatcher` and rely on the fallback resolution described below.

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

Identifiers are deterministic 64-character SHA-256 hex hashes of the canonical (case-folded) path, computed by [`InMemoryFileIds`](InMemoryFileIds.cs) via `WopiResourceId.FromCanonicalPath`. Consumers treat them as opaque. (The WOPI validator's `test.wopitest` file is the one exception — it's given the fixed id `WOPITEST` by every registration flow.) Lookup is `O(1)` in both directions; the in-memory map is rebuilt at startup and kept converged with the tree after that by [`FileIdMapSynchronizer`](FileIdMapSynchronizer.cs), in three layers:

1. **`FileSystemWatcher` (primary, on by default).** Create/delete/rename events update the map as they happen. The rename event carries both paths, so the file's *existing* id is repointed to the new path — including every child when a directory is renamed — rather than a new id being derived for what looks like a new file.
2. **Lazy registration.** Enumeration and by-name lookups register on-disk entries the map hasn't seen; ids derive deterministically from paths, so every process derives the same id.
3. **Reconciliation sweep (recovery).** An id absent from the map triggers a debounced full-tree sweep that registers the derived id of every entry — this covers events the watcher lost (buffer overflow, races) or never got (`WatchForExternalChanges=false`). The sweep enumerates and hashes outside the map's write lock, so lookups and mutations aren't blocked while it runs, and malformed ids (anything that isn't a 64-char lower-hex digest) are rejected without touching the disk. Tree walks skip reparse points (junctions/symlinks): they can introduce cycles, and entries outside the root must not become addressable through a link inside it.

A consequence: because an id derives purely from the path, a file's id is **stable across process restarts** and across hosts pointing at the same tree, so long-lived WOPI URLs keep working without persisting a separate mapping. Renaming or moving a file changes its path-derived id, but the provider re-points the retained id on rename — and the watcher replays that same repoint in every other process over the tree — so an in-progress edit's URL keeps working everywhere and all processes keep addressing the file through **one id, one lock domain**. Only when the rename event is lost (or the watcher is off) does another process fall back to deriving a fresh id from the new path; both ids then resolve (the retained id stays canonical, the derived one registers as an alias on first use), at the cost that the two ids lock independently until the processes restart — acceptable for the dev/single-instance deployments this provider targets.

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
