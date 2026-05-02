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
| [`IWopiPermissionProvider`](IWopiPermissionProvider.cs) | What a `ClaimsPrincipal` can do with a file/container. | `DefaultWopiPermissionProvider` (reads from token claims) | Almost always — this is where your ACL model plugs in. |
| [`IWopiAccessTokenService`](IWopiAccessTokenService.cs) | Issue and validate access tokens. | `JwtAccessTokenService` (signed JWT) | Rarely — only if you need opaque/revocable tokens or external issuance. |
| [`IWopiHostCapabilities`](IWopiHostCapabilities.cs) | Feature flags reported in `CheckFileInfo`. | `WopiHostCapabilities` | Override individual flags via `WopiHostOptions.OnCheckFileInfo`. |

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

## Storage example sketch

A real storage provider implements the read interface and, if writable, the writable one:

```csharp
public class AzureBlobStorageProvider(BlobContainerClient container)
    : IWopiStorageProvider, IWopiWritableStorageProvider
{
    public IWopiFolder RootContainerPointer { get; } = new BlobFolder(container, "/");

    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken ct = default)
        where T : class, IWopiResource
    {
        // Resolve identifier → BlobClient or virtual folder, then return as T.
    }

    public IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null, string? searchPattern = null, CancellationToken ct = default) => /* ... */;

    public IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string? identifier = null, CancellationToken ct = default) => /* ... */;

    public Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(
        string identifier, CancellationToken ct = default) where T : class, IWopiResource => /* ... */;

    public Task<T?> GetWopiResourceByName<T>(
        string containerId, string name, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;

    // IWopiWritableStorageProvider
    public int FileNameMaxLength => 250;
    public Task<T?> CreateWopiChildResource<T>(string? containerId, string name, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;
    public Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;
    public Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;
    public Task<bool> CheckValidName<T>(string name, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;
    public Task<string> GetSuggestedName<T>(string containerId, string name, CancellationToken ct = default)
        where T : class, IWopiResource => /* ... */;
}
```

Once registered, `services.AddWopi()` discovers it via `WopiHostOptions.StorageProviderAssemblyName`. See [WopiHost.FileSystemProvider](../WopiHost.FileSystemProvider/README.md) for a working reference implementation.

## License

See the [repo README](../../README.md#license).
