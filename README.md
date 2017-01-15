Introduction
==========
[![Build status](https://ci.appveyor.com/api/projects/status/l7jn00f4fxydpbed?svg=true)](https://ci.appveyor.com/project/petrsvihlik/wopihost) [![codecov](https://codecov.io/gh/petrsvihlik/WopiHost/branch/master/graph/badge.svg)](https://codecov.io/gh/petrsvihlik/WopiHost)


This project is an example implementation of a [WOPI host](http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx). Basically, it allows developers to integrate custom datasources with Office Web Apps / Office Online Server (or any other WOPI client app) by implementing a few of interfaces.

Features / improvements
-----------------------
 - clean WebAPI built with ASP.NET Core MVC, no references to System.Web
 - automatically discovers capabilities of a WOPI client and acts accordingly
 - can be self-hosted or run under IIS
 - file manipulation is extracted to own layer of abstraction (there is no dependency on System.IO)
   - example implementation included (provider for Windows file system)
   - file identifiers can be anything (doesn't have to correspond with the file's name in the file system)
 - generation/validation of access tokens is also extracted to its own layer
 - concrete implementations are loaded using Autofac
 - URL generator
 - Configuration done via Microsoft.Framework.Configuration
 - all references are NuGets
 
Usage
=====

Prerequisites
-------------

1. [Visual Studio 2015](https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx) + [ASP.NET Core 1.0](https://blogs.msdn.microsoft.com/webdev/2016/06/27/announcing-asp-net-core-1-0/)
2. Following NuGet sources:
  * api.nuget.org (https://api.nuget.org/v3/index.json)
    * default NuGet source
  * Local (e.g. C:\Users\username\Documents\NuGet)
    * this will contain your Microsoft.CobaltCore.15.0.0.0.nupkg
3. Microsoft.CobaltCore.15.0.0.0.nupkg. One of the dependencies is Microsoft.CobaltCore.dll. This DLL is part of Office Web Apps 2013 / Office Online Server 2016 and its license doesn't allow public distribution and therefore it's not part of this repository. Please make sure your OWA/OOS server and user connecting to it have valid licenses before you start using it.
 1. Locate Microsoft.CobaltCore.dll (you can find it in the GAC of the OWA server): `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Microsoft.CobaltCore\`
 2. Install [NuGet Package Explorer](https://npe.codeplex.com/)
 3. Create new package, drop the dll in it and add some metadata
 4. Put the .nupkg to your local NuGet feed
 
Configuration
-----------
WopiHost.Web\Properties\launchSettings.json
- `WopiHostUrl` - used by URL generator
- `WopiClientUrl` - used by the discovery module to load WOPI client URL templates

WopiHost\Properties\launchSettings.json
- `WopiClientUrl` - used by the discovery module to identify capabilities of WOPI client
- `WopiFileProviderAssemblyName` - name of assembly containing implementation of WopiHost.Abstractions interfaces
- `WopiRootPath` - provider-specific setting used by WopiFileSystemProvider (which is an implementation of IWopiFileProvider working with System.IO)
- `server.urls` - hosting URL(s) used by Kestrel. [Read more...](http://andrewlock.net/configuring-urls-with-kestrel-iis-and-iis-express-with-asp-net-core/)

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

Testing & Compatibility
-------
Testing the application requires an operational WOPI client. 
WOPIHost is tested and compatible with:
 - Office Web Apps 2013 SP1 ([deployment guidelines](https://technet.microsoft.com/en-us/library/jj219455(v=office.15).aspx))
 - Office Online Server 2016 ([deployment guidelines](https://technet.microsoft.com/en-us/library/jj219455(v=office.16).aspx))

The deployment requires the server to be part of a domain. If your server is not part of any domain (e.g. you're running it in a VM sandbox) it can be overcame e.g. by installing [DC role](http://social.technet.microsoft.com/wiki/contents/articles/12370.windows-server-2012-set-up-your-first-domain-controller-step-by-step.aspx). After it's deployed you can safely remove the role and the OWA server will remain functional.
To test your OWA server [follow the instructions here](https://blogs.technet.microsoft.com/office_web_apps_server_2013_support_blog/2013/12/27/how-to-test-viewing-office-documents-using-the-office-web-apps-2013-viewer/).
To remove the OWA instance use [`Remove-OfficeWebAppsMachine`](http://sharepointjack.com/2014/fun-configuring-office-web-apps-2013-owa/).

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
