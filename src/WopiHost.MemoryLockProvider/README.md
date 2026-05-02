# WopiHost.MemoryLockProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)

Process-local in-memory implementation of [`IWopiLockProvider`](../WopiHost.Abstractions/IWopiLockProvider.cs).
Backed by a static `ConcurrentDictionary<string, WopiLockInfo>`. Locks auto-expire after 30 minutes per the [WOPI spec](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock); expired entries are evicted on read.

Use it for development, single-instance hosts, and tests. **Don't** use it for multi-instance deployments — locks are not shared across processes and are lost on restart.

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

The interface is **synchronous** (lock state is in-process; there's nothing to await):

```csharp
bool          TryGetLock(string fileId, out WopiLockInfo? lockInfo);
WopiLockInfo? AddLock(string fileId, string lockId);
bool          RefreshLock(string fileId, string? lockId = null);
bool          RemoveLock(string fileId);
```

`WopiLockInfo` carries `LockId`, `FileId`, `DateCreated`, and a computed `Expired` flag.

See [WopiHost.Abstractions](../WopiHost.Abstractions/IWopiLockProvider.cs) for the full contract.

## License

See the [repo README](../../README.md#license).
