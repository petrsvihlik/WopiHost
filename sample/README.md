# ![Logo](../img/logo48.png) WopiHost samples

Three runnable samples + a folder of test documents. The recommended way to launch them is via .NET Aspire — see [Running with .NET Aspire](../README.md#running-with-net-aspire) in the top-level README.

## Layout

| Folder | What it is |
|---|---|
| [`WopiHost`](WopiHost/) | Backend WOPI server. Implements the WOPI REST endpoints. |
| [`WopiHost.Web`](WopiHost.Web/) | Frontend host page (anonymous user). Lists files and embeds the WOPI client. |
| [`WopiHost.Web.Oidc`](WopiHost.Web.Oidc/) | Frontend host page with **OIDC sign-in**. Same role as `WopiHost.Web` but with real users. See [its README](WopiHost.Web.Oidc/README.md). |
| [`WopiHost.Validator`](WopiHost.Validator/) | Combined backend + host page used to run the official WOPI Validator suite. |
| [`wopi-docs`](wopi-docs/) | Sample documents served by the file-system storage provider. |

## Configuration

### `WopiHost` ([appsettings.json](WopiHost/appsettings.json))

| Key | Sample value | Purpose |
|---|---|---|
| `Wopi:StorageProviderAssemblyName` | `"WopiHost.FileSystemProvider"` | Assembly providing `IWopiStorageProvider`. Loaded reflectively at startup. See [src/WopiHost.FileSystemProvider](../src/WopiHost.FileSystemProvider/README.md). |
| `Wopi:StorageProvider:RootPath` | `"../wopi-docs"` | Root of the directory tree the file-system provider exposes. |
| `Wopi:LockProviderAssemblyName` | `"WopiHost.MemoryLockProvider"` | Assembly providing `IWopiLockProvider`. See [src/WopiHost.MemoryLockProvider](../src/WopiHost.MemoryLockProvider/README.md). |
| `Wopi:UseCobalt` | `false` | Reflectively register [WopiHost.Cobalt](../src/WopiHost.Cobalt/README.md) when `true`. Requires a private `Microsoft.CobaltCore` package. |
| `Wopi:ClientUrl` | `"https://ffc-onenote.officeapps.live.com"` | Base URI of the WOPI client (Office Online Server / M365 for the Web / Collabora). Used by discovery. The AppHost overrides this to `http://localhost:9980` when run with `AppHost:UseCollabora=true` — see [End-to-end editing with Collabora Online](../README.md#end-to-end-editing-with-collabora-online). |

### `WopiHost.Web` ([appsettings.json](WopiHost.Web/appsettings.json))

| Key | Sample value | Purpose |
|---|---|---|
| `Wopi:HostUrl` | `"http://wopihost"` | Base URI of the backend (above). Used to construct WopiSrc URLs. AppHost overrides this to `http://host.docker.internal:5000` when `AppHost:UseCollabora=true`, so callbacks from the Collabora container resolve to the host machine. |
| `Wopi:ClientUrl` | `"https://ffc-onenote.officeapps.live.com"` | Base URI of the WOPI client. Used by the discovery cache. |
| `Wopi:Discovery:NetZone` | `"ExternalHttps"` | Which discovery zone to read URL templates from. Values: see [`NetZoneEnum`](../src/WopiHost.Discovery/NetZoneEnum.cs). |
| `Wopi:UiCulture` | (unset) | Optional IETF BCP 47 tag (e.g. `en-US`) for the `UI_LLCC` placeholder. Defaults to `CultureInfo.CurrentUICulture`. |

`WopiHost.Web.Oidc` adds an `Oidc:*` section — see [its README](WopiHost.Web.Oidc/README.md).

`WopiHost.Validator` is a combined host that needs both sets of values plus `Wopi:UserId` (default: `Anonymous`) which it bakes into the access token used by the validator.

You can also use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for any of the above.

## Running individually (without Aspire)

```bash
# Terminal 1 — backend
dotnet run --project sample/WopiHost

# Terminal 2 — frontend
dotnet run --project sample/WopiHost.Web
```

Set both as startup projects in Visual Studio for an F5-driven flow.

## Running the WOPI Validator

After cloning [Microsoft/wopi-validator-core](https://github.com/Microsoft/wopi-validator-core):

```bash
dotnet run --project src/WopiValidator/WopiValidator.csproj --framework net10.0 \
  -s -e OfficeOnline \
  -w http://localhost:28752/wopi/files/Llx0ZXN0LndvcGl0ZXN0 \
  -t Anonymous \
  -l 0
```

`28752` is the validator's [launchSettings](WopiHost.Validator/Properties/launchSettings.json) port for `dotnet run`. Under Aspire orchestration it is `7000` instead — adjust `-w` accordingly.

## Hosting in IIS / Docker

See [Hosting Options](../README.md#hosting-options) in the top-level README.
