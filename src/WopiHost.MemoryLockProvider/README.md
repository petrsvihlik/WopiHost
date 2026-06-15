# WopiHost.MemoryLockProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)

Process-local in-memory implementation of [`IWopiLockProvider`](../WopiHost.Abstractions/IWopiLockProvider.cs).
Backed by a static `ConcurrentDictionary<string, WopiLockInfo>`. Locks auto-expire after 30 minutes per the [WOPI spec](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock); expired entries are evicted lazily — on read, or when a new lock is added over an expired entry for the same file (a takeover, matching the Azure/Redis providers).

Use it for development, single-instance hosts, and tests. **Don't** use it for multi-instance deployments — locks are not shared across processes and are lost on restart.

> **IIS / app-pool recycling.** "Single-instance" includes single-worker IIS sites only as long as the app pool stays warm. Recycles, idle shutdowns, and overlapped recycling all wipe the in-memory store, which surfaces as *"Sorry, you cannot edit this document with others"* on the next open of a previously-locked file (the WOPI client still holds the old lock id, the host no longer recognises it). For IIS — and any host with a non-trivial recycle policy — use a distributed provider such as [`WopiHost.AzureLockProvider`](../WopiHost.AzureLockProvider/README.md).

## Install

```bash
dotnet add package WopiHost.MemoryLockProvider
```

## Register

```csharp
services.AddMemoryLockProvider();
```

`AddMemoryLockProvider` registers `MemoryLockProvider` as the singleton `IWopiLockProvider`. Singleton lifetime ensures every request in the process shares the same in-memory lock dictionary — anything narrower would let concurrent requests see independent stores and silently break exclusion.

## API

The interface is **asynchronous** so out-of-process implementations (Azure Blob, Redis, SQL) can plug in without sync-over-async. This in-memory provider just wraps its synchronous logic in `Task.FromResult`:

```csharp
Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken ct = default);
Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken ct = default);
Task<bool>          RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken ct = default);
Task<bool>          RemoveLockAsync(string fileId, CancellationToken ct = default);
Task<bool>          TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken ct = default);
```

`GetLockAsync` returns `null` when the lock isn't present (or has expired and was evicted). `WopiLockInfo` carries `LockId`, `FileId`, `DateCreated`, and a computed `IsExpiredAt(DateTimeOffset now)` method.

`TryUnlockAndRelockAsync` is implemented with `ConcurrentDictionary.TryUpdate` against a snapshot — a true compare-and-swap, so a concurrent `UnlockAndRelock` from another caller correctly loses the race instead of silently overwriting.

See [WopiHost.Abstractions](../WopiHost.Abstractions/IWopiLockProvider.cs) for the full contract.

## Lock-id comparison

The provider takes an optional `IWopiLockComparer` constructor parameter, defaulting to `OrdinalWopiLockComparer.Instance` (byte-exact). Plug in a custom comparer either via DI (`services.Replace(ServiceDescriptor.Singleton<IWopiLockComparer, ...>())`) or by passing it explicitly when constructing the provider. See the [WopiHost.Abstractions README](../WopiHost.Abstractions/README.md#lock-comparison) for the trade-offs (the bundled `JsonShapedWopiLockComparer` covers the OOS / M365-for-the-Web JSON-mutation quirk).

## License

See the [repo README](../../README.md#license).
