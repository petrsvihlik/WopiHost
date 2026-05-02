# WopiHost.AzureStorageProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.AzureStorageProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureStorageProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.AzureStorageProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureStorageProvider)

[`IWopiStorageProvider`](../WopiHost.Abstractions/IWopiStorageProvider.cs) + [`IWopiWritableStorageProvider`](../WopiHost.Abstractions/IWopiWritableStorageProvider.cs) backed by **Azure Blob Storage**. WOPI files map directly to blobs; folders are virtual prefixes with a hidden zero-byte marker so empty folders remain addressable.

Suitable for production multi-instance deployments. Use with [WopiHost.AzureLockProvider](../WopiHost.AzureLockProvider/README.md) for distributed locking.

## Install

```bash
dotnet add package WopiHost.AzureStorageProvider
```

## Configure

Two authentication modes — connection string (Azurite, dev) and `TokenCredential` (managed identity, service principal):

```jsonc
// appsettings.json — connection string
"Wopi": {
  "StorageProviderAssemblyName": "WopiHost.AzureStorageProvider",
  "StorageProvider": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "wopi-files"
  }
}
```

```jsonc
// appsettings.json — DefaultAzureCredential
"Wopi": {
  "StorageProviderAssemblyName": "WopiHost.AzureStorageProvider",
  "StorageProvider": {
    "ServiceUri": "https://my-account.blob.core.windows.net",
    "ContainerName": "wopi-files"
  }
}
```

When `ServiceUri` is used the provider resolves a `TokenCredential` from DI; if none is registered, [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential) is created automatically. Register your own to override:

```csharp
builder.Services.AddSingleton<TokenCredential>(new ManagedIdentityCredential(clientId));
```

The container is created on first use if it doesn't exist.

## Register

```csharp
builder.Services.AddAzureStorageProvider(builder.Configuration);
builder.Services.AddWopi(o =>
{
    o.ClientUrl                   = new Uri("https://your-office-online-server.com");
    o.StorageProviderAssemblyName = "WopiHost.AzureStorageProvider";
});
```

The sample's [`AddStorageProvider`](../../sample/WopiHost/ServiceCollectionExtensions.cs) helper recognises `"WopiHost.AzureStorageProvider"` and dispatches to the call above, so a single config-driven registration works there too.

## How it maps to Blob Storage

| WOPI concept | Azure Blob equivalent |
|---|---|
| File content | Blob bytes |
| `IWopiFile.Length`, `LastWriteTimeUtc` | Blob `ContentLength`, `LastModified` |
| `IWopiFile.Version` | Blob `ETag` (changes on every byte-level update) |
| `IWopiFile.Owner` | Blob metadata key `wopi_owner` (empty string when unset) |
| `IWopiFile.Checksum` (SHA-256) | Blob metadata key `wopi_sha256` (lowercase hex), computed during upload |
| Folder | Virtual blob-name prefix (`/` delimiter) + zero-byte marker `.wopi.folder` for materialising empty folders |
| Identifier | Hex-MD5 of the lowercased blob path (matches `WopiHost.FileSystemProvider`) |

The provider scans the container at first access to populate the in-memory id-to-path map. Identifiers are stable across process restarts (deterministic from path) but not across renames — a rename re-points the existing id to the new path so the WOPI URL doesn't break mid-edit.

## Caveats

- **Folder rename / delete-folder** are O(N) over the children — plain Blob has no atomic prefix rename. If you do this often, consider switching to ADLS Gen2 (not currently supported, see [issue #26](https://github.com/petrsvihlik/WopiHost/issues/26)) for atomic rename via the DFS endpoint.
- **Empty folders** are materialised by writing a zero-byte `.wopi.folder` blob. Listings filter it out; deleting an empty folder removes the marker and drops the id.
- **SHA-256** is computed streaming on upload via [`HashingBlobWriteStream`](HashingBlobWriteStream.cs) and stored as metadata. Pre-existing blobs that were not written through this provider will return `null` from `Checksum` until they are next written.
- **Owner** is read from blob metadata; the provider doesn't *set* an owner on its own. Hosts that care about ownership should set `wopi_owner` themselves (e.g. via a custom write pipeline that enriches metadata after `GetWriteStream`).

## Local development

The provider works against [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite). The repo's Aspire AppHost runs Azurite as a container resource when `AppHost:UseAzureStorage=true`:

```bash
AppHost__UseAzureStorage=true dotnet run --project infra/WopiHost.AppHost
```

The Aspire orchestrator forwards the emulator connection string into `WopiHost` as `ConnectionStrings:BlobStorage`, which you can map into `Wopi:StorageProvider:ConnectionString` via standard configuration substitution.

## License

See the [repo README](../../README.md#license).
