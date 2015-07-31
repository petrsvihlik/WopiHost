Introduction
==========
[![Build status](https://ci.appveyor.com/api/projects/status/l7jn00f4fxydpbed?svg=true)](https://ci.appveyor.com/project/petrsvihlik/wopihost)

This project is an example implementation of a [WOPI host](http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx). It's based on [Marx Yu's project](https://github.com/marx-yu/WopiHost). WOPI host allows Office Web Apps or any other WOPI client to consume documents.
Basically, it allows developers to connect Office Web Apps to any thinkable source of data. (Requires implementation of a few interfaces.)

Motivation
-------------
The intention was to try out the new technologies like ASP.NET 5 and MVC 6 in some useful way.

Features / improvements
-----------------------
 - clean WebAPI built with MVC 6, no references to System.Web
 - automatically discovers capabilities of a WOPI client and acts accordingly
 - can be hosted as a web app as well as a windows application
 - file manipulation is extracted to own layer of abstraction (there is no dependency on System.IO)
   - example implementation included (provider for Windows file system)
   - file identifiers can be anything (doesn't have to correspond with the file's name in the file system)
 - generation/validation of access tokens is also extracted to its own layer
 - concrete implementations are loaded using Autofac
 - URL generator
 - Configuration done via Microsoft.Framework.ConfigurationModel
 - all references are NuGets
 
Usage
=====

Prerequisites
-------------

1. [Visual Studio 2015](https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx) + [ASP.NET 5 beta 6](http://blogs.msdn.com/b/webdev/archive/2015/07/27/announcing-availability-of-asp-net-5-beta-6.aspx)
2. Following NuGet sources:
  * api.nuget.org (https://api.nuget.org/v3/index.json)
    * default NuGet source
  * AspNetVNext (https://www.myget.org/F/aspnetvnext/api/v2)
    * contains the latest vNext libraries
  * Autofac (https://www.myget.org/F/autofac/)
    * latatest Autofac version compatible with vNext
  * Local (e.g. C:\Users\username\Documents\NuGet)
    * this will contain your Microsoft.CobaltCore.15.0.0.0.nupkg
3. Microsoft.CobaltCore.15.0.0.0.nupkg. One of the dependencies is Microsoft.CobaltCore.dll. This DLL is part of Office Web Apps 2013 and its license doesn't allow public distribution and therefore it's not part of this repository. Please make sure you have a valid license to OWA 2013 before you start using it.
 1. Locate Microsoft.CobaltCore.dll (you can find it in the GAC of the OWA server): `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Microsoft.CobaltCore\v4.0_15.0.0.0__71e9bce111e9429c`
 2. Install [NuGet Package Explorer](https://npe.codeplex.com/)
 3. Create new package, drop the dll in it and add some metadata
 4. Put the .nupkg to your local NuGet feed
 
Configuration
-----------
SampleWeb\Properties\launchSettings.json
- `WopiHostUrl` - used by URL generator
- `WopiClientUrl` - used by discovery module and for URL generation
- `WopiFileProviderAssemblyName` - name of assembly containing implementation of WopiHost.Contracts interfaces
- `WopiRootPath` - provider-specific setting used by WopiFileSystemProvider (which is an implementation of IWopiFileProvider working with System.IO)


WopiHost\Properties\launchSettings.json
- `WopiClientUrl` - used by discovery module and for URL generation
- `WopiFileProviderAssemblyName` - name of assembly containing implementation of WopiHost.Contracts interfaces
- `WopiRootPath` - provider-specific setting used by WopiFileSystemProvider (which is an implementation of IWopiFileProvider working with System.IO)

Running the application
-----------------------
Once you've successfully built the app you can:

- run it directly from the Visual Studio (in IIS Express or selfhosted `web` command)
  - make sure you set both `WopiHost` and `SampleWeb` as [startup projects](/img/multiple_projects.png?raw=true)
- run it from the `cmd`
  - navigate to the WopiHost folder and run `dnx . web`

Testing
-------
Testing the application requires an operational WOPI client. I use Office Web Apps and I'm not sure if there is any other client.
When deploying OWA 2013 please follow the [guidelines](https://technet.microsoft.com/en-us/library/jj219455.aspx). The deployment requires the server to be part of a domain. If your server is not part of any domain (e.g. you're running it in a VM sandbox) it can be overcame e.g. by installing [DC role](http://social.technet.microsoft.com/wiki/contents/articles/12370.windows-server-2012-set-up-your-first-domain-controller-step-by-step.aspx). After it's deployed you can remove the role and the OWA will remain functional.

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
 - [ORIGINAL_WORK_LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/ORIGINAL_WORK_LICENSE.txt) - License for Marx Yu's part of the project
 - [NOTICE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/NOTICE.txt) - additional notes to how the licenses are applied

Useful resources
=============
Building WOPI Host
-----------------------
http://blogs.msdn.com/b/officedevdocs/archive/2013/03/20/introducing-wopi.aspx
http://blogs.msdn.com/b/scicoria/archive/2013/07/22/building-an-office-web-apps-owa-wopi-host.aspx
https://code.msdn.microsoft.com/office/Building-an-Office-Web-f98650d6

Other relevant resources
-----------------------------
http://www.asp.net/web-api/overview/formats-and-model-binding/parameter-binding-in-aspnet-web-api
https://weblog.west-wind.com/posts/2009/Feb/05/Html-and-Uri-String-Encoding-without-SystemWeb
http://blogs.msdn.com/b/scicoria/archive/2013/06/24/office-web-apps-wopi-host-and-url-paths.aspx
http://weblogs.asp.net/imranbaloch/k-kvm-kpm-klr-kre-in-asp-net-vnext

Building WOPI Client
-------------------------
http://www.sharepointcolumn.com/edition-1-wopi-client-in-sharepoint-2013/
http://www.wictorwilen.se/sharepoint-2013-building-your-own-wopi-client-part-2
