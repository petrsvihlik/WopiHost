# ![Logo](img/logo48.png) WopiHost

[![Build & Test](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml/badge.svg)](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml)
[![codecov](https://codecov.io/gh/petrsvihlik/WopiHost/branch/master/graph/badge.svg)](https://codecov.io/gh/petrsvihlik/WopiHost) 
[![Maintainability](https://api.codeclimate.com/v1/badges/369f0a7ff28279088d9c/maintainability)](https://codeclimate.com/github/petrsvihlik/WopiHost/maintainability)
[![CodeFactor](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/badge/master)](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/overview/master)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_shield)
[![.NET Core](https://img.shields.io/badge/net-7-692079.svg)](https://dotnet.microsoft.com/download/dotnet/7.0)

| Package | Version | Downloads |
| ------------- | :-------------: | :-------------: | 
| `WopiHost.Abstractions` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) |
| `WopiHost.Core` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) |
| `WopiHost.Discovery` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) |
| `WopiHost.FileSystemProvider` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) |
| `WopiHost.Url` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) |

Supporters
==========

Sponsors
---------
<a href="https://github.com/scottgal"><img src="https://github.com/scottgal.png" width="80" alt="scottgal" /></a>

<!-- sponsors --><!-- sponsors -->

Contributors
---------
[![Contributors](https://contrib.rocks/image?repo=petrsvihlik/wopihost)](https://github.com/petrsvihlik/wopihost/graphs/contributors)



Introduction
==========
This project is a sample implementation of a WOPI host. Basically, it allows developers to integrate custom datasources with Office Online Server (formerly Office Web Apps) or any other WOPI client by implementing a bunch of interfaces.

Features / improvements compared to existing samples on the web
-----------------------
 - clean WebAPI built with ASP.NET Core MVC (no references to System.Web)
 - uses new ASP.NET Core features (configuration, etc.)
 - can be self-hosted or run under IIS
 - file manipulation is extracted to own layer of abstraction (there is no dependency on System.IO)
   - example implementation included (provider for Windows file system)
   - file identifiers can be anything (doesn't have to correspond with the file's name in the file system)
 - custom token authentication middleware
 - DI used everywhere
 - URL generator
   - based on a WOPI discovery module
 - all references are NuGets
 
Usage
=====

Prerequisites
-------------
 - [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
 - Recommended: [VS Code](https://code.visualstudio.com/) or [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)

Building the app
----------------
The WopiHost app targets `net7.0` exclusively.

If you need a version that's targeting an older version of .NET, check out the releases:
- [.NET 6](TBD)
- [.NET 5](https://github.com/petrsvihlik/WopiHost/releases/tag/3.0.0)
- [.NET Core 2.1 + .NET Framewokr 4.6](https://github.com/petrsvihlik/WopiHost/releases/tag/1.0.0)
- [.NET Core 3.1 + .NET Standard 2.1](https://github.com/petrsvihlik/WopiHost/releases/tag/2.0.0)

If you get errors saying that Microsoft.CobaltCore.*.nupkg can't be found, then just remove the reference or see the chapter [Cobalt](#Cobalt) below.
 
Configuration
-----------

### WopiHost
[WopiHost\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
|`Wopi:StorageProviderAssemblyName`| [`"WopiHost.FileSystemProvider"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost.FileSystemProvider) | Name of assembly containing implementation of `WopiHost.Abstractions` interfaces |
|`Wopi:StorageProvider:RootPath` | [`".\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost/wwwroot/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
|`Wopi:UseCobalt`| `true`| Whether or not to use [MS-FSSHTTP](https://docs.microsoft.com/en-us/openspecs/sharepoint_protocols/ms-fsshttp/) for file synchronization. More details at [Cobalt](#cobalt)|

### WopiHost.Web
[WopiHost.Web\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Web/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
| `Wopi:HostUrl` | `"http://wopihost:5000"` | URL pointing to a WopiHost instance (above). It's used by the URL generator. |
| `Wopi:ClientUrl` | ` "http://owaserver"` | Base URL of your WOPI client - typically, [Office Online Server](#compatible-wopi-clients) - used by the discovery module to load WOPI client URL templates |
| `Wopi:StorageProvider:RootPath` | [`"..\\..\\WopiHost\\wwwroot\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost/wwwroot/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
| `Wopi:Discovery:NetZone` | `"InternalHttp"` | Determines the target zone configuration of your [OOS Deployment](https://docs.microsoft.com/en-us/officeonlineserver/deploy-office-online-server). Values correspond with the [`NetZoneEnum`](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Discovery/NetZoneEnum.cs). |


Additionally, you can use the [secret storage](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-2.2&tabs=windows) to configure both of the apps.

Running the application
-----------------------
Once you've successfully built the app you can:

- run it directly from the Visual Studio using [IIS Express or self-hosted](/img/debug.png?raw=true).
  - make sure you run both `WopiHost` and `WopiHost.Web`. You can set them both as [startup projects](/img/multiple_projects.png?raw=true)
- run it from the `cmd`
  - navigate to the WopiHost folder and run `dotnet run`
- run it in IIS (tested in IIS 8.5)
  - TODO

Compatible WOPI Clients
-------
Running the application only makes sense with a WOPI client as its counterpart. WopiHost is compatible with the following clients:

 - Office Online Server 2016 ([deployment guidelines](https://docs.microsoft.com/en-us/officeonlineserver/deploy-office-online-server))
 - Office Online https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/

Note that WopiHost will always be compatible only with the latest version of OOS because Microsoft also [supports only the latest version](https://docs.microsoft.com/en-us/officeonlineserver/office-online-server-release-schedule).

The deployment of OOS/OWA requires the server to be part of a domain. If your server is not part of any domain (e.g. you're running it in a VM sandbox) it can be overcome by promoting your machine to a [Domain Controller](https://social.technet.microsoft.com/wiki/contents/articles/12370.windows-server-2012-set-up-your-first-domain-controller-step-by-step.aspx).
To test your OWA server [follow the instructions here](https://docs.microsoft.com/en-us/office/troubleshoot/administration/test-viewing-documents-by-using-office-online-server-viewer).
To remove the OWA instance use [`Remove-OfficeWebAppsMachine`](http://sharepointjack.com/2014/fun-configuring-office-web-apps-2013-owa/).

Cobalt
------
In the past (in Office Web Apps 2013), some HTTP actions required support of MS-FSSHTTP protocol (also known as "cobalt"). This is no longer true with Office Online Server 2016.
However, if the WOPI client discovers (via [SupportsCobalt](https://docs.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo#supportscobalt) property) that the WOPI host supports cobalt, it'll take advantage of it as it's more efficient.

If you need or want your project to use Cobalt, you'll need to [create a NuGet package called Microsoft.CobaltCore.nupkg](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package) containing Microsoft.CobaltCore.dll. This DLL is part of Office Web Apps 2013 / Office Online Server 2016 and its license doesn't allow public distribution and therefore it's not part of this repository. Please always make sure your OWA/OOS server and user connecting to it have valid licenses before you start using it.


Using in your web project
-------------------------
TODO

Extending
=========
TODO

Known issues / TODOs
==================
There is plenty of space for improvements in the overall architecture, implementation of the [MS-*] protocols or just finishing the TODOs in the code. Lot of refactoring still needs to be done and also the code style has to be unified. So please feel free to help me out with it :)

 - Check out [open issues](https://github.com/petrsvihlik/WopiHost/issues?q=is%3Aopen)

Contributing
==========
https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/

License
=======
 - [LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/LICENSE.txt) - License for my part of the project
 - [ORIGINAL_WORK_LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Cobalt/ORIGINAL_WORK_LICENSE.txt) - License for Marx Yu's part of the project. This project is based on [Marx Yu's project](https://github.com/marx-yu/WopiHost).
 - [NOTICE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/NOTICE.txt) - additional notes to how the licenses are applied


Useful resources
=============
Building WOPI Host
-----------------------
 - [Official WOPI Documentation](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/)
 - [Official WOPI REST API Reference](https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/6a8bb410-68ad-47e4-9dc3-6cf29c6b046b)
 - [Building an Office Web Apps (OWA) WOPI Host by Shawn Cicoria](https://code.msdn.microsoft.com/office/Building-an-Office-Web-f98650d6) + [WOPI Host and url paths](https://www.cicoria.com/office-web-appswopi-host-and-url-paths/)
 - [Office Online integration via WOPI Host by Richard diZerega](https://github.com/OfficeDev/PnP-WOPI) + [video](https://www.youtube.com/watch?v=9lGonu0eoGA)

MS-FSSHTTP (Cobalt)
-------
 - https://docs.microsoft.com/en-us/openspecs/sharepoint_protocols/ms-fsshttp/6d078cbe-2651-43a0-b460-685ac3f14c45
 - https://channel9.msdn.com/Events/Open-Specifications-Plugfests/Redmond-Interoperability-Protocols-Plugfest-2015/FSSHTTP-File-Synchronization-over-HTTP

Building WOPI Client
-------------------------
 - [SharePoint 2013: Building your own WOPI Client, part 1](https://www.wictorwilen.se/blog/sharepoint-2013-building-your-own-wopi-client-part-1/)
 - [SharePoint 2013: Building your own WOPI Client, part 2](https://www.wictorwilen.se/blog/sharepoint-2013-building-your-own-wopi-client-part-2/)


[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_large)
