# WopiHost.MemoryLockProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)

Process-local in-memory implementation of [`IWopiLockProvider`](../WopiHost.Abstractions/IWopiLockProvider.cs).
Backed by a static `ConcurrentDictionary<string, WopiLockInfo>`. Locks auto-expire after 30 minutes per the [WOPI spec](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock); expired entries are evicted on read.

Use it for development, single-instance hosts, and tests. **Don't** use it for multi-instance deployments — locks are not shared across processes and are lost on restart.

> **IIS / app-pool recycling.** "Single-instance" includes single-worker IIS sites only as long as the app pool stays warm. Recycles, idle shutdowns, and overlapped recycling all wipe the in-memory store, which surfaces as *"Sorry, you cannot edit this document with others"* on the next open of a previously-locked file (the WOPI client still holds the old lock id, the host no longer recognises it). For IIS — and any host with a non-trivial recycle policy — use a distributed provider such as [`WopiHost.AzureLockProvider`](../WopiHost.AzureLockProvider/README.md).

## Install

```bash
dotnet add package WopiHost.MemoryLockProvider
```

## Register

The provider is loaded by assembly name from `WopiHostOptions`, so the only thing the host has to do is reference the package and point at it:

```jsonc
// appsettings.json
"Wopi": {
  "LockProviderAssemblyName": "WopiHost.MemoryLockProvider"
}
```

If you prefer manual registration:

```csharp
services.AddSingleton<IWopiLockProvider, MemoryLockProvider>();
```

`MemoryLockProvider` keeps state in a `static` field — register it once, lifetime doesn't matter.

## API

The interface is **asynchronous** so out-of-process implementations (Azure Blob, Redis, SQL) can plug in without sync-over-async. This in-memory provider just wraps its synchronous logic in `Task.FromResult`:

```csharp
Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken ct = default);
Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken ct = default);
Task<bool>          RefreshLockAsync(string fileId, string? lockId = null, CancellationToken ct = default);
Task<bool>          RemoveLockAsync(string fileId, CancellationToken ct = default);
```

`GetLockAsync` returns `null` when the lock isn't present (or has expired and was evicted). `WopiLockInfo` carries `LockId`, `FileId`, `DateCreated`, and a computed `Expired` flag.

See [WopiHost.Abstractions](../WopiHost.Abstractions/IWopiLockProvider.cs) for the full contract.

## License

See the [repo README](../../README.md#license).
