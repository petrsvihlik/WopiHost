Introduction
==========
[![Build status](https://ci.appveyor.com/api/projects/status/l7jn00f4fxydpbed/branch/master?svg=true)](https://ci.appveyor.com/project/petrsvihlik/wopihost/branch/master)
[![codecov](https://codecov.io/gh/petrsvihlik/WopiHost/branch/master/graph/badge.svg)](https://codecov.io/gh/petrsvihlik/WopiHost) 
[![Maintainability](https://api.codeclimate.com/v1/badges/369f0a7ff28279088d9c/maintainability)](https://codeclimate.com/github/petrsvihlik/WopiHost/maintainability)
[![CodeFactor](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/badge/master)](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/overview/master)
[![Gitter](https://img.shields.io/gitter/room/nwjs/nw.js.svg)](https://gitter.im/ms-wopi/)
[![.NET Core](https://img.shields.io/badge/netcore-2.0-692079.svg)](https://www.microsoft.com/net/learn/get-started/windows)


This project is a sample implementation of a [WOPI host](http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx). Basically, it allows developers to integrate custom datasources with Office Online Server (formerly Office Web Apps) or any other WOPI client by implementing a bunch of interfaces.

Features / improvements compared to existing samples on the web
-----------------------
 - clean WebAPI built with ASP.NET Core MVC, no references to System.Web
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
 - [Visual Studio 2017 with the .NET Core workload](https://www.microsoft.com/net/core)

Building the app
----------------
The WopiHost app targets `net46` and `netcoreapp2.0`. You can choose which one you want to use. If you get errors that Microsoft.CobaltCore.15.0.0.0.nupkg can't be found then just remove the reference or see the chapter "Cobalt" below.
 
Configuration
-----------

### WopiHost
[WopiHost\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
|`WopiFileProviderAssemblyName`| [`"WopiHost.FileSystemProvider"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost.FileSystemProvider) | Name of assembly containing implementation of `WopiHost.Abstractions` interfaces |
|`WopiRootPath` | [`".\\wopi-docs"`](https://github.com/petrsvihlik/WopiHost/tree/master/WopiHost/wwwroot/wopi-docs) | Provider-specific setting used by `WopiHost.FileSystemProvider` (which is an implementation of `IWopiStorageProvider` working with System.IO) |
| `server.urls` | `"wopihost:5000"` | URL(s) at which the WopiHost will be hosted - used by [Kestrel](http://andrewlock.net/configuring-urls-with-kestrel-iis-and-iis-express-with-asp-net-core/) |

### WopiHost.Web
[WopiHost.Web\appSettings.json](https://github.com/petrsvihlik/WopiHost/blob/master/WopiHost.Web/appsettings.json)

| Parameter | Sample value | Description |
| :--- | :--- | :--- |
| `WopiHostUrl` | `"http://wopihost:5000"` | URL pointing to a WopiHost instance (above). It's used by the URL generator. |
| `WopiClientUrl` | ` "http://owaserver"` | Base URL of your WOPI client - typically, [Office Online Server](#compatible-wopi-clients) - used by the discovery module to load WOPI client URL templates |

Additionally, you can use the [secret storage](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-2.2&tabs=windows) to configure both of the apps.

Running the application
-----------------------
Once you've successfully built the app you can:

- run it directly from the Visual Studio using [IIS Express or selfhosted](/img/debug.png?raw=true).
  - make sure you run both `WopiHost` and `WopiHost.Web`. You can set them both as [startup projects](/img/multiple_projects.png?raw=true)
- run it from the `cmd`
  - navigate to the WopiHost folder and run `dotnet run`
- run it in IIS (tested in IIS 8.5)
  - navigate to the WopiHost folder and run `dnu publish --runtime active`
  - copy the files from WopiHost\bin\output to your desired web application directory
  - run the web.cmd file as administrator, wait for it to finish and close it (Ctrl+C and y)
  - create a new application in IIS and set the physical path to the wwwroot in the web application directory
  - make sure the site you're adding it to has a binding with port 5000
  - go to the application settings and change the value of `dnx clr` to `clr` and the value of `dnx-version` to `1.0.0-rc1`
  - in the same window, add all the configuration settings

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
In the past (in Office Web Apps 2013), some actions required support of MS-FSSHTTP protocol (also known as "cobalt"). This is no longer true with Office Online Server 2016.
However, if the WOPI client discovers (via [SupportsCobalt](http://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html#term-supportscobalt) property) that the WOPI host supports cobalt, it'll use it as it's more efficient.

If you want to make the project work with Office Web Apps 2013 SP1 ([deployment guidelines](https://technet.microsoft.com/en-us/library/jj219455(v=office.15).aspx)), you'll need to create a NuGet package called Microsoft.CobaltCore.15.0.0.0.nupkg containing Microsoft.CobaltCore.dll. This DLL is part of Office Web Apps 2013 / Office Online Server 2016 and its license doesn't allow public distribution and therefore it's not part of this repository. Please make sure your OWA/OOS server and user connecting to it have valid licenses before you start using it.

 1. Locate Microsoft.CobaltCore.dll (you can find it in the GAC of the OWA server): `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Microsoft.CobaltCore\`
 2. Install [NuGet Package Explorer](https://npe.codeplex.com/)
 3. Use .nuspec located in the [Microsoft.CobaltCore](https://github.com/petrsvihlik/WopiHost/tree/master/Microsoft.CobaltCore) folder to create new package
 4. Put the .nupkg to your local NuGet feed
 5. Configure Visual Studio to use your local NuGet feed

Note: the Microsoft.CobaltCore.dll targets the full .NET Framework, it's not possible to use it in an application that targets .NET Core.

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
https://msdn.microsoft.com/en-us/library/ms229042(v=vs.110).aspx

License
=======
 - [LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/LICENSE.txt) - License for my part of the project
 - [ORIGINAL_WORK_LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/ORIGINAL_WORK_LICENSE.txt) - License for Marx Yu's part of the project. This project is based on [Marx Yu's project](https://github.com/marx-yu/WopiHost).
 - [NOTICE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/NOTICE.txt) - additional notes to how the licenses are applied

Support the project
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
 - !!! NEW & SUPERCOOL Documentation: https://wopi.readthedocs.org/
 - http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx
 - http://blogs.msdn.com/b/scicoria/archive/2013/07/22/building-an-office-web-apps-owa-wopi-host.aspx
 - https://code.msdn.microsoft.com/office/Building-an-Office-Web-f98650d6
 - https://github.com/OfficeDev/PnP-WOPI + video https://www.youtube.com/watch?v=9lGonu0eoGA

FSSHTTP
-------
 - https://msdn.microsoft.com/en-us/library/dd956775(v=office.12).aspx
 - https://channel9.msdn.com/Events/Open-Specifications-Plugfests/Redmond-Interoperability-Protocols-Plugfest-2015/FSSHTTP-File-Synchronization-over-HTTP


Other relevant resources
-----------------------------
 - [Content roadmap for Office Web Apps Server](https://technet.microsoft.com/en-us/library/dn135237.aspx)
 - http://www.asp.net/web-api/overview/formats-and-model-binding/parameter-binding-in-aspnet-web-api
 - https://weblog.west-wind.com/posts/2009/Feb/05/Html-and-Uri-String-Encoding-without-SystemWeb
 - http://blogs.msdn.com/b/scicoria/archive/2013/06/24/office-web-apps-wopi-host-and-url-paths.aspx
 - http://weblogs.asp.net/imranbaloch/k-kvm-kpm-klr-kre-in-asp-net-vnext

Building WOPI Client
-------------------------
 - http://www.sharepointcolumn.com/edition-1-wopi-client-in-sharepoint-2013/
 - http://www.wictorwilen.se/sharepoint-2013-building-your-own-wopi-client-part-2
