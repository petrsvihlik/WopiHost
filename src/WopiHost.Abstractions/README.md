# WopiHost.Abstractions

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions)

The contracts a WOPI host implementation has to satisfy. Reference this package if you are writing a custom storage provider, lock provider, or permission provider; everything else in the repo depends on it.

## Install

```bash
dotnet add package WopiHost.Abstractions
```

## The interfaces

| Interface | Purpose | Bundled implementations | When to implement |
|---|---|---|---|
| [`IWopiStorageProvider`](IWopiStorageProvider.cs) | Read access to files and containers (folders). | `WopiFileSystemProvider` (local disk), `WopiAzureStorageProvider` (Azure Blob) | When neither bundled provider fits. |
| [`IWopiWritableStorageProvider`](IWopiWritableStorageProvider.cs) | Create / delete / rename / suggest names. Optional. | Same providers above also implement this. | If you want WOPI clients to be able to mutate the store. |
| [`IWopiLockProvider`](IWopiLockProvider.cs) | Editing locks. Asynchronous so out-of-process stores (Azure Blob, Redis, SQL) can plug in without sync-over-async. | `MemoryLockProvider` (in-process), `WopiAzureLockProvider` (blob leases) | Replace for multi-instance hosts. |
| [`IWopiLockComparer`](IWopiLockComparer.cs) | Compares two lock ids for equivalence — used by every controller validation and by both providers' atomic compare-and-swap. | `OrdinalWopiLockComparer` (default, byte-exact), `JsonShapedWopiLockComparer` (opt-in, OOS-tolerant) | Replace only if you observe a specific WOPI client mutating lock ids between round-trips. |
| [`IWopiPermissionProvider`](IWopiPermissionProvider.cs) | What a `ClaimsPrincipal` can do with a file/container. | `DefaultWopiPermissionProvider` (reads from token claims) | Almost always — this is where your ACL model plugs in. |
| [`IWopiAccessTokenService`](IWopiAccessTokenService.cs) | Issue and validate access tokens. | `JwtAccessTokenService` (signed JWT) | Rarely — only if you need opaque/revocable tokens or external issuance. |
| [`IWopiHostCapabilities`](IWopiHostCapabilities.cs) | Feature flags reported in `CheckFileInfo`. | `WopiHostCapabilities` | Override individual flags by extending [`WopiHostExtensions`](WopiHostExtensions.cs) and overriding `OnCheckFileInfoAsync`. |
| [`IWopiHostExtensions`](IWopiHostExtensions.cs) | Single host-customization seam for `CheckFileInfo` / `CheckContainerInfo` / `CheckFolderInfo` / `CheckEcosystem` / `PutFile` / `PutRelativeFile`. | `WopiHostExtensions` (pass-through) | Subclass when you want to layer custom properties, rewrite URLs, or hook write-completion telemetry. |
| [`ICheckFileInfoBuilder`](ICheckFileInfoBuilder.cs) / [`ICheckContainerInfoBuilder`](ICheckContainerInfoBuilder.cs) / [`ICheckFolderInfoBuilder`](ICheckFolderInfoBuilder.cs) | Build the corresponding response shapes. The default implementations fire the matching `IWopiHostExtensions` hook. | `DefaultCheck*InfoBuilder` (in `WopiHost.Core`) | Replace when you need scoped services that don't fit in the host-extensions seam, or to short-circuit the default population entirely. |

The defaults ship in `WopiHost.Core`, `WopiHost.FileSystemProvider`, `WopiHost.MemoryLockProvider`, `WopiHost.AzureStorageProvider`, and `WopiHost.AzureLockProvider` — selected via `WopiHostOptions.StorageProviderAssemblyName` / `LockProviderAssemblyName`.

## Resource model

```csharp
public interface IWopiResource
{
    string Name { get; }
    string Identifier { get; }
}

public interface IWopiFile : IWopiResource
{
    string Owner { get; }
    bool Exists { get; }
    long Length { get; }
    long Size { get; }
    DateTime LastWriteTimeUtc { get; }
    string Extension { get; }            // without leading dot
    string? Version { get; }
    byte[]? Checksum { get; }            // SHA-256

    Task<Stream> GetReadStream(CancellationToken cancellationToken = default);
    Task<Stream> GetWriteStream(CancellationToken cancellationToken = default);
}

public interface IWopiFolder : IWopiResource { }
```

## Permission model

`IWopiPermissionProvider` is the single seam for "what is this user allowed to do here?". The provider is consulted at two points:

1. **Token issuance** by host code that builds the WOPI URL — to decide what permissions to bake into the token.
2. **CheckFileInfo / CheckContainerInfo** — to populate the `UserCan*` response flags Office reads.

```csharp
public class MyAclPermissionProvider(IAclStore acls) : IWopiPermissionProvider
{
    public async Task<WopiFilePermissions> GetFilePermissionsAsync(
        ClaimsPrincipal user, IWopiFile file, CancellationToken ct = default)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var entry = await acls.GetEntriesAsync(sub, file.Identifier, ct);

        var perms = WopiFilePermissions.None;
        if (entry.CanWrite)  perms |= WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename;
        else                 perms |= WopiFilePermissions.ReadOnly;
        if (!entry.CanCreate) perms |= WopiFilePermissions.UserCanNotWriteRelative;
        return perms;
    }

    public Task<WopiContainerPermissions> GetContainerPermissionsAsync(
        ClaimsPrincipal user, IWopiFolder container, CancellationToken ct = default)
        => /* ... */;
}

// Register AFTER AddWopi() so it overrides the default.
services.AddWopi();
services.AddSingleton<IWopiPermissionProvider, MyAclPermissionProvider>();
```

For the full token pipeline (claim layout, key rotation, the bootstrapper authentication scheme), see the [WopiHost.Core README](../WopiHost.Core/README.md#security).

## Lock comparison

Every controller-level lock check and both providers' atomic compare-and-swap go through `IWopiLockComparer`. The default — `OrdinalWopiLockComparer` (byte-exact ordinal equality) — is the safe choice and matches the spec's implicit contract.

`JsonShapedWopiLockComparer` ships as an opt-in fallback for hosts that observe Office Online Server / Microsoft 365-for-the-Web round-tripping JSON-format lock ids with extra properties added (the same quirk that drives `cs3org/wopiserver`'s `wopilockstrictcheck=False` mode). It treats two JSON locks as equivalent when their `S` (sequence) field matches and falls back to ordinal for non-JSON inputs. Tolerance has its own correctness cost — distinct locks that share an `S` field are treated as equal, masking lost updates — so don't relax the strict default speculatively.

Wire either via DI:

```csharp
services.AddWopi(...);
services.Replace(ServiceDescriptor.Singleton<IWopiLockComparer, JsonShapedWopiLockComparer>());
```

Custom implementations are a single-method interface:

```csharp
public class MyLockComparer : IWopiLockComparer
{
    public bool AreEqual(string? storedLockId, string? candidateLockId) { /* ... */ }
}
```

## Lock model & limits

`WopiLockInfo` carries `LockId`, `FileId`, `DateCreated`, and a computed `Expired` flag. Two spec-mandated constants live alongside it:

- `WopiLockInfo.ExpirationMinutes = 30` — the WOPI spec requires locks auto-expire after 30 minutes if not refreshed.
- `WopiLockInfo.MaxLockIdLength = 1024` — WopiHost advertises `SupportsExtendedLockLength` in `CheckFileInfo`, so this is the contract limit; `ProcessLock` rejects oversize lock ids with 400.

`IWopiLockProvider` also exposes `TryUnlockAndRelockAsync(fileId, newLockId, expectedExistingLockId, ct)` — a required member that implements WOPI's `UnlockAndRelock` operation as a true compare-and-swap. Custom providers must implement this with their store's atomic primitive (`ConcurrentDictionary.TryUpdate`, ETag-conditional writes, optimistic-concurrency tokens, etc.). A naive Get-then-Refresh sequence is not sufficient.

`WopiResourceLockedException` is the contract type the lock-aware writable storage decorator (`WopiLockAwareWritableStorageProvider` in `WopiHost.Core`) throws when delete/rename hits a locked target.

## Storage example sketch

A real storage provider implements the read interface and, if writable, the writable one:

```csharp
public class AzureBlobStorageProvider(BlobContainerClient container)
    : IWopiStorageProvider, IWopiWritableStorageProvider
{
    public IWopiFolder RootContainer { get; } = new BlobFolder(container, "/");

    // IWopiStorageProvider — typed file/container pairs (no generic discriminator).
    public Task<IWopiFile?>    GetWopiFile     (string identifier, CancellationToken ct = default) => /* ... */;
    public Task<IWopiFolder?>  GetWopiContainer(string identifier, CancellationToken ct = default) => /* ... */;

    public IAsyncEnumerable<IWopiFile>   GetWopiFiles     (string identifier, IReadOnlyCollection<string>? fileExtensions = null, CancellationToken ct = default) => /* ... */;
    public IAsyncEnumerable<IWopiFolder> GetWopiContainers(string identifier, CancellationToken ct = default) => /* ... */;

    public Task<ReadOnlyCollection<IWopiFolder>> GetFileAncestors     (string fileId,      CancellationToken ct = default) => /* ... */;
    public Task<ReadOnlyCollection<IWopiFolder>> GetContainerAncestors(string containerId, CancellationToken ct = default) => /* ... */;

    public Task<IWopiFile?>   GetWopiFileByName     (string containerId, string name, CancellationToken ct = default) => /* ... */;
    public Task<IWopiFolder?> GetWopiContainerByName(string containerId, string name, CancellationToken ct = default) => /* ... */;

    // IWopiWritableStorageProvider — same pattern: file and container methods split apart so
    // implementations don't need to runtime-switch on a type parameter.
    public int FileNameMaxLength => 250;
    public Task<IWopiFile?>   CreateWopiChildFile     (string containerId, string name, CancellationToken ct = default) => /* ... */;
    public Task<IWopiFolder?> CreateWopiChildContainer(string containerId, string name, CancellationToken ct = default) => /* ... */;
    public Task<bool> DeleteWopiFile     (string identifier, CancellationToken ct = default) => /* ... */;
    public Task<bool> DeleteWopiContainer(string identifier, CancellationToken ct = default) => /* ... */;
    public Task<bool> RenameWopiFile     (string identifier, string requestedName, CancellationToken ct = default) => /* ... */;
    public Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken ct = default) => /* ... */;
    public Task<bool> CheckValidFileName     (string name, CancellationToken ct = default) => /* ... */;
    public Task<bool> CheckValidContainerName(string name, CancellationToken ct = default) => /* ... */;
    public Task<string> GetSuggestedFileName     (string containerId, string name, CancellationToken ct = default) => /* ... */;
    public Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken ct = default) => /* ... */;
}
```

Once registered, `services.AddWopi()` discovers it via `WopiHostOptions.StorageProviderAssemblyName`. See [WopiHost.FileSystemProvider](../WopiHost.FileSystemProvider/README.md) for a working reference implementation.

## License

See the [repo README](../../README.md#license).
