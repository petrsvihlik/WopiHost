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
    o.SigningKey = Convert.FromBase64String(builder.Configuration["Wopi:Security:SigningKey"]!);
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
        ClaimsPrincipal user, IWopiFolder container, CancellationToken ct = default) { /* ... */ }
}

services.AddWopi(o => { o.ClientUrl = ...; o.StorageProviderAssemblyName = ...; });
services.AddSingleton<IWopiPermissionProvider, MyAclPermissionProvider>();
services.ConfigureWopiSecurity(o =>
{
    o.SigningKey = Convert.FromBase64String(Configuration["Wopi:Security:SigningKey"]!);
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
    var file  = await _storage.GetWopiResource<IWopiFile>(fileId);
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

Skip this section if you are not exposing the bootstrapper. See the top-level [README](../../README.md#bootstrap-endpoint-office-for-mobile) for the WWW-Authenticate challenge and the three operations.

## Customizing CheckFileInfo / CheckContainerInfo / CheckEcosystem

Use the option callbacks to layer your own properties or override defaults:

```csharp
services.AddWopi(o =>
{
    o.ClientUrl                  = ...;
    o.StorageProviderAssemblyName = "...";

    o.OnCheckFileInfo = async ctx =>
    {
        ctx.CheckFileInfo.IsAnonymousUser = false;
        return ctx.CheckFileInfo;
    };

    // Spec: SupportsContainers should match the value returned from CheckFileInfo.
    o.OnCheckEcosystem = ctx =>
    {
        ctx.CheckEcosystem.SupportsContainers = false;
        return Task.FromResult(ctx.CheckEcosystem);
    };
});
```

## License

See the [repo README](../../README.md#license).
