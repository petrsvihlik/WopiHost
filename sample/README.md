# ![Logo](../img/logo48.png) WopiHost Samples

Introduction
==========
This folder includes samples for hosting the WopiHost and the Host pages.
 
wopi-docs
---

Sample documents used by our Host pages - referenced by the FileSystemProvider as configured in the samples.

WopiHost and WopiHost.Web
---

Two companion samples - the WopiHost is the actual Wopi Server host and the WopiHost.Web provides a sample Host page to actually view/edit the sample documents in the Wopi client (as configured in WopiHost.Web/Wopi:ClientUrl).

#### Configuration: WopiHost

[WopiHost\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/sample/WopiHost/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
|`Wopi:StorageProviderAssemblyName`| [`"WopiHost.FileSystemProvider"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost.FileSystemProvider) | Name of assembly containing implementation of the `IWopiStorageProvider` and `IWopiSecurityHandler` interfaces |
|`Wopi:StorageProvider:RootPath` | [`".\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/sample/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
|`Wopi:LockProviderAssemblyName`| [`"WopiHost.LockProvider"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost.MemoryLockProvider) | Name of assembly containing implementation of the `IWopiLockProvider` interface |
|`Wopi:UseCobalt`| `false`| Whether or not to use [MS-FSSHTTP](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/) for file synchronization. More details at [Cobalt](#cobalt)|

#### Configuration: WopiHost.Web
[WopiHost.Web\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/sample/WopiHost.Web/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
| `Wopi:HostUrl` | `"http://wopihost:52788"` | URL pointing to a WopiHost instance (above). It's used by the URL generator. |
| `Wopi:ClientUrl` | ` "http://owaserver"` | Base URL of your WOPI client - typically, [Office Online Server](#compatible-wopi-clients) - used by the discovery module to load WOPI client URL templates |
| `Wopi:StorageProvider:RootPath` | [`".\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/sample/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
| `Wopi:Discovery:NetZone` | `"InternalHttp"` | Determines the target zone configuration of your [OOS Deployment](https://learn.microsoft.com/officeonlineserver/deploy-office-online-server). Values correspond with the [`NetZoneEnum`](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Discovery/NetZoneEnum.cs). |


Additionally, you can use the [secret storage](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-2.2&tabs=windows) to configure both of the apps.

WopiHost.Validator
---

Single hosting for both the Wopi Server and the Host page server by a single .NET HTTP server.
This has been preconfigured to handle custom Wopi Events and is used to validate the Wopi Server implementation using the WOPI-Validator.

#### Configuration: WopiHost.Validator

This is a combination of the WopiHost and WopiHost.Web configurations, with the additional `Wopi:UserId` (default: Anonymous) setting to specify the hard-coded token to be used by the WOPI-Validator.

#### Running the WOPI-Validator

After cloning and building the [WOPI-Validator repository](https://github.com/Microsoft/wopi-validator-core) use the following command to run the `Office Online` suite of validations:

```
dotnet run --project src\WopiValidator\WopiValidator.csproj --framework net8.0 -s -e OfficeOnline -w http://localhost:28752/wopi/files/Llx0ZXN0LndvcGl0ZXN0 -t Anonymous -l 0
```


Running the samples
---
Once you've successfully built the app you can:

- run it directly from the Visual Studio using [IIS Express or self-hosted](/img/debug.png?raw=true).
  - make sure you run both `WopiHost` and `WopiHost.Web`. You can set them both as [startup projects](/img/multiple_projects.png?raw=true)
- run it from the `cmd`
  - navigate to the WopiHost folder and run `dotnet run`
- run it in IIS (tested in IIS 8.5)
  - TODO
