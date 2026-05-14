# WopiHost.Core

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core)

The WOPI server: ASP.NET Core controllers, authentication, authorization, proof-key validation, and the access-token pipeline. Reference this package in your host along with a storage provider (e.g. [WopiHost.FileSystemProvider](../WopiHost.FileSystemProvider/README.md)) and a lock provider (e.g. [WopiHost.MemoryLockProvider](../WopiHost.MemoryLockProvider/README.md)).

## Install

```bash
dotnet add package WopiHost.Core
```

## Quick start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWopi(o =>
{
    o.ClientUrl = new Uri("https://your-office-online-server.com");
    o.StorageProviderAssemblyName = "WopiHost.FileSystemProvider";
    o.LockProviderAssemblyName    = "WopiHost.MemoryLockProvider";
});

// Required in production: pin the access-token signing key.
builder.Services.ConfigureWopiSecurity(o =>
{
    o.SigningKey = Convert.FromBase64String(
        builder.Configuration[$"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.SigningKey)}"]!);
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

`AddWopi()` registers controllers, the access-token authentication scheme, the proof-key validator, default `IWopiAccessTokenService` (signed JWT), and default `IWopiPermissionProvider` (reads from token claims). Override either by registering your own implementation — the defaults are added with `TryAdd*` so a custom registration wins regardless of order. See [the runnable sample](../../sample/WopiHost/Program.cs).

## Configuration

```jsonc
{
  "Wopi": {
    "ClientUrl": "https://your-office-online-server.com",
    "StorageProviderAssemblyName": "WopiHost.FileSystemProvider",
    "LockProviderAssemblyName":    "WopiHost.MemoryLockProvider",
    "UseCobalt": false,
    "Discovery": {
      "NetZone":         "ExternalHttps",
      "RefreshInterval": "12:00:00"
    }
  }
}
```

`WopiHostOptions` ([source](Models/WopiHostOptions.cs)) implements `IDiscoveryOptions` so a single config section feeds Core, Discovery, and Url.

## Endpoints

WOPI uses a small set of routes; verb + `X-WOPI-Override` header pick the operation. Don't expect REST-style `/lock` / `/unlock` paths — they don't exist.

| Route | Verb | Override header | Operation |
|---|---|---|---|
| `/wopi/files/{id}` | `GET` | — | [`CheckFileInfo`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo) |
| `/wopi/files/{id}/contents` | `GET` | — | [`GetFile`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getfile) |
| `/wopi/files/{id}/contents` | `POST` | — | [`PutFile`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile) |
| `/wopi/files/{id}` | `POST` | `LOCK` / `UNLOCK` / `REFRESH_LOCK` / `GET_LOCK` | Lock operations |
| `/wopi/files/{id}` | `POST` | `PUT_RELATIVE` | [`PutRelativeFile`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile) |
| `/wopi/files/{id}` | `POST` | `RENAME_FILE` | [`RenameFile`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile) |
| `/wopi/files/{id}` | `POST` | `DELETE` | [`DeleteFile`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile) |
| `/wopi/files/{id}` | `POST` | `PUT_USER_INFO` | [`PutUserInfo`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo) |
| `/wopi/files/{id}` | `POST` | `COBALT` | MS-FSSHTTP request batch (when [WopiHost.Cobalt](../WopiHost.Cobalt/README.md) is registered) |
| `/wopi/files/{id}/ancestry` | `GET` | — | [`EnumerateAncestors`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors) |
| `/wopi/files/{id}/ecosystem_pointer` | `GET` | — | Ecosystem pointer for the file |
| `/wopi/containers/{id}` | `GET` | — | [`CheckContainerInfo`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo) |
| `/wopi/containers/{id}/children` | `GET` | — | [`EnumerateChildren`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren) |
| `/wopi/containers/{id}/ancestry` | `GET` | — | [`EnumerateAncestors`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors) |
| `/wopi/containers/{id}/ecosystem_pointer` | `GET` | — | Ecosystem pointer for the container |
| `/wopi/containers/{id}` | `POST` | `CREATE_CHILD_CONTAINER` / `CREATE_CHILD_FILE` / `RENAME_CONTAINER` / `DELETE_CONTAINER` | Container mutations |
| `/wopi/folders/{id}` | `GET` | — | [`CheckFolderInfo`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/folders/checkfolderinfo) (legacy OneNote) |
| `/wopi/folders/{id}/children` | `GET` | — | [`EnumerateChildren (folders)`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/folders/enumeratechildren) |
| `/wopi/ecosystem` | `GET` | — | [`CheckEcosystem`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem) |
| `/wopi/ecosystem/root_container_pointer` | `GET` | — | [`GetRootContainer`](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer) |
| `/wopibootstrapper` | `GET` / `POST` | `GET_ROOT_CONTAINER` / `GET_NEW_ACCESS_TOKEN` | Office mobile bootstrapper (separate auth scheme — see below) |

## Security

```
┌─────────────┐    1. who is the user?       ┌──────────────────────────┐
│ Frontend    │  ─────────────────────────►  │ Your ACL store           │
│ (mints      │  2. perms?                   │ (IWopiPermissionProvider)│
│  WOPI URL)  │  ◄─────────────────────────  └──────────────────────────┘
│             │  3. issue token              ┌──────────────────────────┐
│             │  ─────────────────────────►  │ IWopiAccessTokenService  │
│             │  ◄ JWT (signed, perms baked) │ (default: JWT-based)     │
└──────┬──────┘                              └──────────────────────────┘
       │ 4. URL?WOPISrc=...&access_token=<JWT>
       ▼
┌─────────────┐
│ Office      │ 5. GET /wopi/files/{id}?access_token=<JWT> + X-WOPI-Proof
│ Online      │
└──────┬──────┘
       ▼
┌────────────────────────────────────────────────────────────────────────┐
│  WopiHost.Core pipeline                                                │
│                                                                        │
│  AccessTokenHandler  → IWopiAccessTokenService.ValidateAsync(token)    │
│                       (re-materializes ClaimsPrincipal from JWT)       │
│                                                                        │
│  WopiProofValidator → verifies X-WOPI-Proof against discovery keys     │
│                                                                        │
│  WopiAuthorizationHandler                                              │
│      └ permission check: token's wopi:fperms grants required Permission│
│        (route {id} vs token wopi:rid is logged on mismatch but not     │
│         enforced — WOPI tokens are session-scoped; layer a custom      │
│         IAuthorizationHandler if you need strict per-resource binding) │
│                                                                        │
│  Controller runs (IWopiPermissionProvider also called for CheckFileInfo│
│      to populate UserCan* response flags)                              │
└────────────────────────────────────────────────────────────────────────┘
```

### What you implement

In almost all cases the only seam you need is `IWopiPermissionProvider`:

```csharp
public class MyAclPermissionProvider : IWopiPermissionProvider
{
    public Task<WopiFilePermissions> GetFilePermissionsAsync(
        ClaimsPrincipal user, IWopiFile file, CancellationToken ct = default) { /* your ACL lookup */ }

    public Task<WopiContainerPermissions> GetContainerPermissionsAsync(
        ClaimsPrincipal user, IWopiContainer container, CancellationToken ct = default) { /* ... */ }
}

services.AddWopi(o => { o.ClientUrl = ...; o.StorageProviderAssemblyName = ...; });
services.AddSingleton<IWopiPermissionProvider, MyAclPermissionProvider>();
services.ConfigureWopiSecurity(o =>
{
    o.SigningKey = Convert.FromBase64String(Configuration[$"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.SigningKey)}"]!);
    o.DefaultTokenLifetime = TimeSpan.FromMinutes(10);
});
```

Without `ConfigureWopiSecurity`, the host generates a random signing key on first use and logs a warning. Fine for local dev; never ship that.

### What you can override (rarely)

| Service | Default | Override when |
|---|---|---|
| `IWopiPermissionProvider` | `DefaultWopiPermissionProvider` (reads from token claims, falls back to `WopiHostOptions.DefaultFilePermissions`/`DefaultContainerPermissions`) | You have a real ACL model — almost always. |
| `IWopiAccessTokenService` | `JwtAccessTokenService` (HMAC-SHA256, configurable issuer/audience/lifetime/key rotation) | You need opaque revocable tokens or external token issuance. |

### Issuing a WOPI URL from the frontend

The host's frontend (often a separate process) hands the user a URL embedding an `access_token`:

```csharp
public async Task<IActionResult> Open(string fileId)
{
    var file  = await _storage.GetWopiFile(fileId);
    var perms = await _permissions.GetFilePermissionsAsync(User, file);
    var token = await _tokens.IssueAsync(new WopiAccessTokenRequest
    {
        UserId          = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
        UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
        UserEmail       = User.FindFirstValue(ClaimTypes.Email),
        ResourceId      = file.Identifier,
        ResourceType    = WopiResourceType.File,
        FilePermissions = perms,
    });
    return Redirect($"{wopiSrcUrl}?access_token={Uri.EscapeDataString(token.Token)}&access_token_ttl={token.ExpiresAt.ToUnixTimeMilliseconds()}");
}
```

If the frontend lives in a different process from the WOPI server, both must be configured with the **same `WopiSecurityOptions.SigningKey`**. The OIDC sample wires this end-to-end — see [`sample/WopiHost.Web.Oidc`](../../sample/WopiHost.Web.Oidc/README.md).

### Claim layout (`WopiClaimTypes`)

Tokens issued by the default `JwtAccessTokenService` carry these custom claims, read back by `WopiAuthorizationHandler` and `DefaultWopiPermissionProvider`:

| Claim | Meaning |
|---|---|
| `wopi:rid` | Resource id the token was issued for. Used for audit; not enforced as a route binding because Office uses one token to navigate file → ancestor container. |
| `wopi:rtype` | `"File"` or `"Container"`. |
| `wopi:fperms` | Comma-separated `WopiFilePermissions` flags (when `wopi:rtype` is `File`). |
| `wopi:cperms` | Comma-separated `WopiContainerPermissions` flags (when `wopi:rtype` is `Container`). |
| `wopi:uname` | Friendly display name (mirrors `ClaimTypes.Name`). |

### Key rotation

```csharp
services.ConfigureWopiSecurity(o =>
{
    o.SigningKey = newKey;                   // signs new tokens, validates new + old
    o.AdditionalValidationKeys.Add(oldKey);  // accepted for validation only
});
```

Keep the old key in `AdditionalValidationKeys` for at least one full token TTL, then drop it.

### Bootstrapper (Office mobile)

`/wopibootstrapper` uses OAuth2 Bearer from your IdP — not the `access_token` query parameter. Register a JWT-bearer handler under `WopiAuthenticationSchemes.Bootstrap`:

```csharp
services.AddAuthentication()
    .AddJwtBearer(WopiAuthenticationSchemes.Bootstrap, o =>
    {
        o.Authority = "https://login.example.com/";
        o.Audience  = "wopi-bootstrap";
    });
```

Skip this section if you are not exposing the bootstrapper. See [Bootstrap endpoint](https://github.com/petrsvihlik/WopiHost/wiki/Bootstrap-Endpoint) on the wiki for the WWW-Authenticate challenge and the three operations.

## Customizing CheckFileInfo / CheckContainerInfo / CheckFolderInfo / CheckEcosystem

`IWopiHostExtensions` is the single host-customization seam. The shipped [`WopiHostExtensions`](../WopiHost.Abstractions/WopiHostExtensions.cs) base class is pass-through — subclass it and override only the hooks you care about, then register your subclass with DI.

```csharp
public class MyHostExtensions(IAuditLog audit, ITelemetry telemetry) : WopiHostExtensions
{
    public override Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext ctx, CancellationToken ct = default)
    {
        ctx.CheckFileInfo.IsAnonymousUser = false;
        return Task.FromResult(ctx.CheckFileInfo);
    }

    // Spec: SupportsContainers should match the value returned from CheckFileInfo.
    public override Task<WopiCheckEcosystem> OnCheckEcosystemAsync(WopiCheckEcosystemContext ctx, CancellationToken ct = default)
    {
        ctx.CheckEcosystem.SupportsContainers = false;
        return Task.FromResult(ctx.CheckEcosystem);
    }

    // Fired after a successful PutFile. Editors comes from X-WOPI-Editors —
    // a comma-delimited list of UserId values for users who contributed
    // changes in this PutFile request.
    public override async Task OnPutFileAsync(WopiPutFileContext ctx, CancellationToken ct = default)
    {
        await audit.RecordEditAsync(ctx.File, ctx.Editors, ctx.User, ct);
    }

    // Fired after a successful PutRelativeFile. IsFileConversion reflects
    // X-WOPI-FileConversion (presence-only); DeclaredSize reflects X-WOPI-Size.
    public override Task OnPutRelativeFileAsync(WopiPutRelativeFileContext ctx, CancellationToken ct = default)
    {
        if (ctx.IsFileConversion) telemetry.Conversion(ctx.NewFile);
        return Task.CompletedTask;
    }
}

// Register BEFORE or AFTER AddWopi — TryAddSingleton respects either order.
services.AddSingleton<IWopiHostExtensions, MyHostExtensions>();
services.AddWopi(o =>
{
    o.ClientUrl                   = ...;
    o.StorageProviderAssemblyName = "...";
});
```

Throwing inside any hook turns the response into a 500 — for best-effort bookkeeping (audit log, last-edit telemetry), catch exceptions inside the override.

If the customization needs scoped services that don't fit the hook's context shape — or you want to replace the entire response generation — register a custom [`ICheckFileInfoBuilder`](../WopiHost.Abstractions/ICheckFileInfoBuilder.cs) / [`ICheckContainerInfoBuilder`](../WopiHost.Abstractions/ICheckContainerInfoBuilder.cs) / [`ICheckFolderInfoBuilder`](../WopiHost.Abstractions/ICheckFolderInfoBuilder.cs). The default builders in `WopiHost.Core` fire the matching `IWopiHostExtensions` hook before returning; custom builders own that responsibility themselves.

## Upload-size budget

Reject oversize uploads at the controller before the body is read:

```csharp
services.AddWopi(o =>
{
    o.MaxFileSize = 50 * 1024 * 1024;  // 50 MB; null (default) = no WOPI-level limit
});
```

`PutFile` checks `Content-Length`; `PutRelativeFile` also honors the declared `X-WOPI-Size`. When the budget is exceeded the controller returns `413 Request Entity Too Large` (a valid response per the WOPI spec) without invoking the storage provider. The underlying server's request-size limits still apply on top.

## Empty `X-WOPI-Lock` placeholder

The WOPI spec for `GetLock` (on an unlocked file) and `PutFile` (on a non-empty unlocked file) requires `X-WOPI-Lock` to be present and set to the empty string. That's the default. IIS in-process hosting strips empty header values before they hit the wire (issue #208), so opt back into the historic single-space workaround on that path:

```csharp
services.AddWopi(o =>
{
    o.EmptyLockHeaderValue = " ";   // default is "" (spec)
});
```

Kestrel, IIS out-of-process, and any reverse proxy that preserves empty headers (NGINX, Caddy, YARP) need no override.

## Lock-id comparison

By default the host compares lock ids with byte-exact ordinal equality (`OrdinalWopiLockComparer`), which is what the WOPI spec implies. If you observe the Office Online Server / Microsoft 365-for-the-Web quirk where JSON-shaped lock ids round-trip with extra properties added — `cs3org/wopiserver` and SenseNet both ship a tolerant fallback for the same reason — swap in the included `JsonShapedWopiLockComparer`:

```csharp
services.AddWopi(...);
services.Replace(ServiceDescriptor.Singleton<IWopiLockComparer, JsonShapedWopiLockComparer>());
```

Or register your own `IWopiLockComparer` implementation tailored to the specific mutation you observe. Tolerance carries its own correctness risk — distinct locks that happen to share a `S` field are treated as equivalent, which can mask lost updates — so don't relax the strict default speculatively.

## Lock-aware writable storage (defense in depth)

`services.AddWopiLockAwareWritableStorage()` wraps the registered `IWopiWritableStorageProvider` so that the delete/rename pairs (`DeleteWopiFile`, `DeleteWopiContainer`, `RenameWopiFile`, `RenameWopiContainer`) consult `IWopiLockProvider` first and throw `WopiResourceLockedException` when the target is locked. The WOPI controllers already short-circuit on locks before reaching the storage layer, so on the hot path this decorator is redundant — it earns its keep when:

- non-WOPI code paths in the same host (admin tools, batch jobs, REST APIs) resolve `IWopiWritableStorageProvider` directly and would otherwise clobber a locked file
- a future controller refactor accidentally drops the lock check

```csharp
services.AddWopi(...);
services.AddStorageProvider("WopiHost.AzureStorageProvider");
services.AddLockProvider("WopiHost.AzureLockProvider");
services.AddWopiLockAwareWritableStorage();   // must run after the storage + lock providers are registered
```

The decorator only guards single-resource mutations; the create methods (`CreateWopiChildFile`, `CreateWopiChildContainer` — no prior lock to check) and the read-only members pass through unchanged.

## License

See the [repo README](../../README.md#license).
