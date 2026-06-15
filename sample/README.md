# ![Logo](../img/logo48.png) WopiHost samples

Three runnable samples + a folder of test documents. The recommended way to launch them is via .NET Aspire — see [Quick start](../README.md#quick-start) in the top-level README.

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
| `Sample:StorageProvider` | `"FileSystem"` | Sample-local discriminator picking which storage provider's typed extension to call. Values: `FileSystem` ([src/WopiHost.FileSystemProvider](../src/WopiHost.FileSystemProvider/README.md)) or `Azure` ([src/WopiHost.AzureStorageProvider](../src/WopiHost.AzureStorageProvider/README.md)). Lives in the sample only — real hosts reference one provider package and call its typed extension directly. |
| `Wopi:StorageProvider:RootPath` | `"../wopi-docs"` | Root of the directory tree the file-system provider exposes. |
| `Sample:LockProvider` | `"Memory"` | Sample-local lock-provider discriminator. Values: `Memory`, `Azure`, `Redis` — see the per-provider READMEs. |
| `Wopi:UseCobalt` | `false` | Registers [WopiHost.Cobalt](../src/WopiHost.Cobalt/README.md) when `true`. Requires a private `Microsoft.CobaltCore` package. |
| `Wopi:ClientUrl` | `"https://ffc-onenote.officeapps.live.com"` | Base URI of the WOPI client (Office Online Server / M365 for the Web / Collabora). Used by discovery. The AppHost overrides this to `http://localhost:9980` when run with `AppHost:UseCollabora=true` — see [End-to-end editing with Collabora Online](https://github.com/petrsvihlik/WopiHost/wiki/Collabora-Online) on the wiki. |

### `WopiHost.Web` ([appsettings.json](WopiHost.Web/appsettings.json))

| Key | Sample value | Purpose |
|---|---|---|
| `Wopi:HostUrl` | `"http://wopihost"` | Base URI of the backend (above). Used to construct WopiSrc URLs. AppHost overrides this to `http://host.docker.internal:5050` when `AppHost:UseCollabora=true`, so callbacks from the Collabora container resolve to the host machine. |
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
  -w http://localhost:28752/wopi/files/WOPITEST \
  -t Anonymous \
  -l 0
```

`28752` is the validator's [launchSettings](WopiHost.Validator/Properties/launchSettings.json) port for `dotnet run`. Under Aspire orchestration the port is allocated dynamically (the validator is registered with `WithHttpsEndpoint()`); read it off the Aspire dashboard and adjust `-w` accordingly.

## Hosting in IIS / Docker

See [Hosting](https://github.com/petrsvihlik/WopiHost/wiki/Hosting) on the wiki.
