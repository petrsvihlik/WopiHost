# WopiHost.AzureLockProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.AzureLockProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureLockProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.AzureLockProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureLockProvider)

Distributed [`IWopiLockProvider`](../WopiHost.Abstractions/IWopiLockProvider.cs) backed by **Azure Blob leases**. Use this in place of [WopiHost.MemoryLockProvider](../WopiHost.MemoryLockProvider/README.md) when you run more than one WopiHost instance and need them to agree on who is currently editing a file.

## Install

```bash
dotnet add package WopiHost.AzureLockProvider
```

## Configure

```jsonc
"Wopi": {
  "LockProviderAssemblyName": "WopiHost.AzureLockProvider",
  "LockProvider": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "wopi-locks"
  }
}
```

```jsonc
"Wopi": {
  "LockProviderAssemblyName": "WopiHost.AzureLockProvider",
  "LockProvider": {
    "ServiceUri": "https://my-account.blob.core.windows.net",
    "ContainerName": "wopi-locks"
  }
}
```

The lock container is dedicated and separate from your content blobs — that keeps lock churn out of the hot data path and lets you put locks in a different storage account if you want.

## Register

```csharp
builder.Services.AddAzureLockProvider(builder.Configuration);
builder.Services.AddWopi(o =>
{
    o.LockProviderAssemblyName = "WopiHost.AzureLockProvider";
});
```

The sample's [`AddLockProvider`](../../sample/WopiHost/ServiceCollectionExtensions.cs) helper recognises `"WopiHost.AzureLockProvider"` and dispatches to the call above.

## How it works

For every `fileId`, the provider holds a **placeholder blob** named `SHA256(fileId)` in the configured container. Two pieces of state coexist:

1. **An infinite-duration Azure blob lease** — provides true cross-instance mutual exclusion. Only one WopiHost instance can hold the lease at a time.
2. **Blob metadata** — carries the WOPI-level state visible to any instance that can read the blob:

| Metadata key | Meaning |
|---|---|
| `wopi_lock_id` | The client-supplied WOPI lock id (any string). |
| `wopi_lease_id` | The Azure lease GUID — needed by remote instances to renew or release the lease. |
| `wopi_created` | ISO-8601 timestamp; honours the WOPI 30-minute auto-expiry. |

Why both? The lease provides "is this lock physically held right now"; the metadata provides "who claims it and when did the claim start". An instance handling a `RefreshLock` or `Unlock` reads the metadata to recover the lease GUID stored by whichever instance originally created the lock.

### Crash recovery

If the WopiHost instance that created a lock dies without releasing, the infinite lease persists. The next `GetLock` or `AddLock` call against that fileId notices the metadata indicates a >30-minute-old claim, breaks the lease, and either evicts (`GetLock`) or takes over (`AddLock`). This matches the WOPI specification's 30-minute lock auto-expiry without requiring a background sweeper.

## Caveats

- **Latency**: every WOPI lock op is one or two round-trips to Azure Blob. For high-volume editing scenarios, measure carefully.
- **Costs**: each lock acquire/refresh/release is a billable Azure Storage operation.
- **Storage cleanup**: `RemoveLock` deletes the placeholder blob; expired locks are also evicted on observation. There is no separate cleanup process.

## Local development

Works against [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite). The repo's tests use Testcontainers to spin up Azurite per test run; production hosts will typically use Azurite via Aspire's `AddAzureStorage().RunAsEmulator()` integration (see [WopiHost.AppHost](../../infra/WopiHost.AppHost/Program.cs)).

## License

See the [repo README](../../README.md#license).
