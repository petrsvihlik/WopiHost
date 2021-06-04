# ![Logo](img/logo48.png) WopiHost

[![Build & Test](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml/badge.svg)](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml)
[![codecov](https://codecov.io/gh/petrsvihlik/WopiHost/branch/master/graph/badge.svg)](https://codecov.io/gh/petrsvihlik/WopiHost) 
[![Maintainability](https://api.codeclimate.com/v1/badges/369f0a7ff28279088d9c/maintainability)](https://codeclimate.com/github/petrsvihlik/WopiHost/maintainability)
[![CodeFactor](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/badge/master)](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/overview/master)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_shield)
[![Gitter](https://img.shields.io/gitter/room/nwjs/nw.js.svg)](https://gitter.im/ms-wopi/)
[![.NET Core](https://img.shields.io/badge/net-5-692079.svg)](https://dotnet.microsoft.com/download/dotnet/5.0)

| Package | Version | Downloads |
| ------------- | :-------------: | :-------------: | 
| `WopiHost.Abstractions` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) |
| `WopiHost.Core` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) |
| `WopiHost.Discovery` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) |
| `WopiHost.FileSystemProvider` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) |
| `WopiHost.Url` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) |

Introduction
==========
This project is a sample implementation of a [WOPI host](http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx). Basically, it allows developers to integrate custom datasources with Office Online Server (formerly Office Web Apps) or any other WOPI client by implementing a bunch of interfaces.

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
 - [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)
 - Recommended: [VS Code](https://code.visualstudio.com/) or [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)

Building the app
----------------
The WopiHost app targets `net5.0` exclusively. This is due to the [announced discontinuation of .NET Framework and .NET Core](https://devblogs.microsoft.com/dotnet/introducing-net-5/) and [replacement of .NET Standard](https://devblogs.microsoft.com/dotnet/the-future-of-net-standard/).

If you need a version that's targeting an older version of .NET, check out the releases:
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
|`Wopi:StorageProviderOptions:RootPath` | [`".\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost/wwwroot/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
|`Wopi:UseCobalt`| `true`| Whether or not to use [MS-FSSHTTP](https://docs.microsoft.com/en-us/openspecs/sharepoint_protocols/ms-fsshttp/) for file synchronization. More details at [Cobalt](#cobalt)|

### WopiHost.Web
[WopiHost.Web\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Web/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
| `Wopi:HostUrl` | `"http://wopihost:5000"` | URL pointing to a WopiHost instance (above). It's used by the URL generator. |
| `Wopi:ClientUrl` | ` "http://owaserver"` | Base URL of your WOPI client - typically, [Office Online Server](#compatible-wopi-clients) - used by the discovery module to load WOPI client URL templates |
|`Wopi:StorageProviderOptions:RootPath` | [`"..\\..\\WopiHost\\wwwroot\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost/wwwroot/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |


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

 - Office Online Server 2016 ([deployment guidelines](https://technet.microsoft.com/en-us/library/jj219455(v=office.16).aspx))
 - Office Online https://wopi.readthedocs.io/en/latest/

Note that WopiHost will always be compatible only with the latest version of OOS because Microsoft also [supports only the latest version](https://blogs.office.com/2016/11/18/office-online-server-november-release/).

The deployment of OOS/OWA requires the server to be part of a domain. If your server is not part of any domain (e.g. you're running it in a VM sandbox) it can be overcame e.g. by installing [DC role](http://social.technet.microsoft.com/wiki/contents/articles/12370.windows-server-2012-set-up-your-first-domain-controller-step-by-step.aspx). After it's deployed you can safely remove the role and the OWA server will remain functional.
To test your OWA server [follow the instructions here](https://blogs.technet.microsoft.com/office_web_apps_server_2013_support_blog/2013/12/27/how-to-test-viewing-office-documents-using-the-office-web-apps-2013-viewer/).
To remove the OWA instance use [`Remove-OfficeWebAppsMachine`](http://sharepointjack.com/2014/fun-configuring-office-web-apps-2013-owa/).

Cobalt
------
In the past (in Office Web Apps 2013), some HTTP actions required support of MS-FSSHTTP protocol (also known as "cobalt"). This is no longer true with Office Online Server 2016.
However, if the WOPI client discovers (via [SupportsCobalt](http://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html#term-supportscobalt) property) that the WOPI host supports cobalt, it'll take advantage of it as it's more efficient.

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
 - [ORIGINAL_WORK_LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/ORIGINAL_WORK_LICENSE.txt) - License for Marx Yu's part of the project. This project is based on [Marx Yu's project](https://github.com/marx-yu/WopiHost).
 - [NOTICE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/NOTICE.txt) - additional notes to how the licenses are applied

Supporters
===================
If this project helped you and you want to support its further development, please consider donating an amount of your choice. Thank you!


<table>
<tr><td colspan="2"><a href="https://www.paypal.me/Svihlik"><img src="https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif" alt="Donate with PayPal" /></a></td></tr>
<tr><td><img src="https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Bitcoin_logo.svg/2000px-Bitcoin_logo.svg.png" width="100" alt="Donate with BitCoin" /></td><td>3PuqLrSsV4EFFr55brj9cSJVoaRoc23b3p</td></tr>
</table>

Useful resources
=============
Building WOPI Host
-----------------------
 - [Official WOPI Documentation](https://wopi.readthedocs.io)
 - [Official WOPI REST API Reference](https://wopi.readthedocs.io/projects/wopirest/en/latest/)
 - [Introducing WOPI by S. D. Oliver](http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx)
 - [Building an Office Web Apps (OWA) WOPI Host by Shawn Cicoria](https://code.msdn.microsoft.com/office/Building-an-Office-Web-f98650d6) + [WOPI Host and url paths](https://www.cicoria.com/office-web-appswopi-host-and-url-paths/)
 - [Office Online integration via WOPI Host by Richard diZerega](https://github.com/OfficeDev/PnP-WOPI) + [video](https://www.youtube.com/watch?v=9lGonu0eoGA)

MS-FSSHTTP (Cobalt)
-------
 - https://msdn.microsoft.com/en-us/library/dd956775(v=office.12).aspx
 - https://channel9.msdn.com/Events/Open-Specifications-Plugfests/Redmond-Interoperability-Protocols-Plugfest-2015/FSSHTTP-File-Synchronization-over-HTTP

Building WOPI Client
-------------------------
 - http://www.wictorwilen.se/sharepoint-2013-building-your-own-wopi-client-part-1
 - http://www.wictorwilen.se/sharepoint-2013-building-your-own-wopi-client-part-2


[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_large)
