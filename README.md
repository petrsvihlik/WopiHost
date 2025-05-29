# ![Logo](img/logo48.png) WopiHost

[![Build & Test](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml/badge.svg)](https://github.com/petrsvihlik/WopiHost/actions/workflows/integrate.yml)
[![codecov](https://codecov.io/gh/petrsvihlik/WopiHost/branch/master/graph/badge.svg)](https://codecov.io/gh/petrsvihlik/WopiHost) 
[![Code Coverage](https://qlty.sh/badges/43534b35-fa0c-4a2d-bd02-17802842b9c5/test_coverage.svg)](https://qlty.sh/gh/petrsvihlik/projects/WopiHost)
[![Maintainability](https://api.codeclimate.com/v1/badges/369f0a7ff28279088d9c/maintainability)](https://codeclimate.com/github/petrsvihlik/WopiHost/maintainability)
[![Maintainability](https://qlty.sh/badges/43534b35-fa0c-4a2d-bd02-17802842b9c5/maintainability.svg)](https://qlty.sh/gh/petrsvihlik/projects/WopiHost)
[![CodeFactor](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/badge/master)](https://www.codefactor.io/repository/github/petrsvihlik/wopihost/overview/master)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_shield)
[![.NET Core](https://img.shields.io/badge/net-9-692079.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)

| Package | Version | Downloads |
| ------------- | :-------------: | :-------------: | 
| `WopiHost.Abstractions` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions) |
| `WopiHost.Core` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core) |
| `WopiHost.Discovery` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery) |
| `WopiHost.FileSystemProvider` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider) |
| `WopiHost.Url` | [![NuGet](https://img.shields.io/nuget/v/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) | [![NuGet](https://img.shields.io/nuget/dt/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url) |


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
 - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
 - Recommended: [VS Code](https://code.visualstudio.com/) or [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)

Building the app
----------------
The WopiHost app targets both `net8.0` and `net9.0`.

If you need a version that targets an older version of .NET, check out the releases:
- [.NET 6](TBD)
- [.NET 5](https://github.com/petrsvihlik/WopiHost/releases/tag/3.0.0)
- [.NET Core 2.1 + .NET Framework 4.6](https://github.com/petrsvihlik/WopiHost/releases/tag/1.0.0)
- [.NET Core 3.1 + .NET Standard 2.1](https://github.com/petrsvihlik/WopiHost/releases/tag/2.0.0)

If you get errors saying that Microsoft.CobaltCore.*.nupkg can't be found, then just remove the reference or see the chapter [Cobalt](#Cobalt) below.

Running with .NET Aspire
------------------------
This project includes a .NET Aspire orchestration for easy development and deployment. .NET Aspire provides a comprehensive developer experience for building cloud-native applications with .NET.

### Prerequisites for .NET Aspire
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (required for the AppHost project)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for containerization support)
- Recommended: [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the .NET Aspire workload, or [VS Code](https://code.visualstudio.com/) with the C# Dev Kit extension

### Running the application with .NET Aspire

1. **Clone the repository:**
   ```bash
   git clone https://github.com/petrsvihlik/WopiHost.git
   cd WopiHost
   ```

2. **Run the Aspire AppHost:**
   ```bash
   dotnet run --project infra/WopiHost.AppHost
   ```

   This will start all three components of the application:
   - **WopiHost** (Backend service) - `http://localhost:5000`
     - API endpoints for WOPI operations
     - Swagger UI available at `/scalar`
   - **WopiHost.Web** (Frontend) - `http://localhost:6000` (HTTP) / `https://localhost:6001` (HTTPS)
     - Web interface for file management and WOPI client integration
   - **WopiHost.Validator** (Testing tool) - `http://localhost:7000`
     - WOPI protocol validation and testing interface

3. **Access the Aspire Dashboard:**
   When you run the AppHost, .NET Aspire will automatically open the Aspire dashboard in your browser. The dashboard provides:
   - Real-time application status and health monitoring
   - Structured logging and trace visualization
   - Resource management and configuration
   - Inter-service communication monitoring

![image](https://github.com/user-attachments/assets/438cf17b-36f2-4d5f-adb5-6003314d17c3)

### Aspire Benefits

Using .NET Aspire with WopiHost provides several advantages:

- **Service Orchestration**: Automatically manages dependencies between WopiHost, Web frontend, and Validator
- **Configuration Management**: Centralized configuration through the AppHost
- **Observability**: Built-in logging, metrics, and distributed tracing
- **Development Experience**: Simplified local development with automatic service discovery
- **Production Ready**: Easy deployment to cloud environments with container support

### Configuration

The Aspire configuration can be customized in `infra/WopiHost.AppHost/Program.cs`. The current setup includes:

- Service references and dependencies
- Port assignments for each service
- External endpoint configuration for web access
- Health monitoring and readiness checks

You can also customize application settings through:
- `infra/WopiHost.AppHost/appsettings.json`
- `infra/WopiHost.AppHost/appsettings.Development.json`

### Alternative: Running individual projects

If you prefer to run the projects individually without Aspire:

```bash
# Terminal 1 - Backend
dotnet run --project sample/WopiHost

# Terminal 2 - Frontend  
dotnet run --project sample/WopiHost.Web

# Terminal 3 - Validator (optional)
dotnet run --project sample/WopiHost.Validator
```
 
Samples
-----------

See [Samples](https://github.com/petrsvihlik/WopiHost/blob/master/sample/README.md) for all samples.

Compatible WOPI Clients
-------
Running the application only makes sense with a WOPI client as its counterpart. WopiHost is compatible with the following clients:

 #### Office Online Server 2016 
 
 [deployment guidelines](https://learn.microsoft.com/officeonlineserver/deploy-office-online-server)

Note that WopiHost will always be compatible only with the latest version of OOS because Microsoft also [supports only the latest version](https://learn.microsoft.com/officeonlineserver/office-online-server-release-schedule).

The deployment of OOS/OWA requires the server to be part of a domain. If your server is not part of any domain (e.g. you're running it in a VM sandbox) it can be overcome by promoting your machine to a [Domain Controller](https://social.technet.microsoft.com/wiki/contents/articles/12370.windows-server-2012-set-up-your-first-domain-controller-step-by-step.aspx).
To test your OWA server [follow the instructions here](https://learn.microsoft.com/office/troubleshoot/administration/test-viewing-documents-by-using-office-online-server-viewer).
To remove the OWA instance use [`Remove-OfficeWebAppsMachine`](http://sharepointjack.com/2014/fun-configuring-office-web-apps-2013-owa/).

#### Microsoft 365 for the Web 

You can [use WopiHost to integrate with Microsoft 365 for the web](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online) which will require:
- onboarding - [apply for CSPP](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/apply-for-cspp-program)
- extending the provided interfaces to support the required features by Microsoft; we provide a sample implementation of the interfaces that pass the interactive [WOPI-Validator](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/build-test-ship/validator) tests
- [Test Microsoft 365 for the web integration](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/build-test-ship/testing)

Cobalt
------
In the past (in Office Web Apps 2013), some HTTP actions required the support of MS-FSSHTTP protocol (also known as "cobalt"). This is no longer true with Office Online Server 2016.
However, if the WOPI client discovers (via [SupportsCobalt](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo#supportscobalt) property) that the WOPI host supports cobalt, it'll take advantage of it as it's more efficient.

If you need or want your project to use Cobalt, you'll need to [create a NuGet package called Microsoft.CobaltCore.nupkg](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package) containing Microsoft.CobaltCore.dll. This DLL is part of Office Web Apps 2013 / Office Online Server 2016, and its license doesn't allow public distribution and therefore, it's not part of this repository. Please always make sure your OWA/OOS server and the user connecting to it have valid licenses before you start using it.


Using in your web project
-------------------------
TODO

Extending
=========

### IWopiStorageProvider

The `IWopiStorageProvider` interface is the main interface that needs to be implemented to provide access to the files. It's up to you how you implement it. One sample implementation is in the `WopiHost.FileSystemProvider` project.

### IWopiSecurityHandler

The `IWopiSecurityHandler` interface is used to authenticate and authorize resource requests. One sample implementation is in the `WopiHost.FileSystemProvider` project.

### IWopiLockProvider

The `IWopiLockProvider` interface is used to handle file locks. One sample implementation is in the `WopiHost.MemoryLockProvider` project.

### CheckFileInfo

The [CheckFileInfo](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo) includes not only details about the file but also some additional properties that can be used by the WOPI client. You can either completely customize the response (by adding your own / missing properties), or update any properties before returning them by registering for the OnCheckFileInfo event.

### CheckContainerInfo

The [CheckContainerInfo](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo) includes also some security related properties (that are checked by IWopiSecurityHandler), but you can still customize the response using the OnCheckContainerInfo event.


TODO additional details

Known issues / TODOs
==================
There is plenty of space for improvements in the overall architecture, implementation of the [MS-*] protocols, or just finishing the TODOs in the code. A lot of refactoring still needs to be done and also the code style has to be unified. So please feel free to help me out with it :)

 - Check out [open issues](https://github.com/petrsvihlik/WopiHost/issues?q=is%3Aopen)

Contributing
==========
https://learn.microsoft.com/dotnet/standard/design-guidelines/

License
=======
 - [LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/LICENSE.txt) - License for my part of the project
 - [ORIGINAL_WORK_LICENSE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/src/WopiHost.Cobalt/ORIGINAL_WORK_LICENSE.txt) - License for Marx Yu's part of the project. This project is based on [Marx Yu's project](https://github.com/marx-yu/WopiHost).
 - [NOTICE.txt](https://github.com/petrsvihlik/WopiHost/blob/master/NOTICE.txt) - additional notes to how the licenses are applied


Useful resources
=============
Building WOPI Host
-----------------------
 - [Official WOPI Documentation](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/)
 - [Official WOPI REST API Reference](https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/6a8bb410-68ad-47e4-9dc3-6cf29c6b046b)
 - [WOPI Host and url paths](https://www.cicoria.com/office-web-appswopi-host-and-url-paths/)
 - [Office Online integration via WOPI Host by Richard diZerega](https://github.com/OfficeDev/PnP-WOPI) + [video](https://www.youtube.com/watch?v=9lGonu0eoGA)

MS-FSSHTTP (Cobalt)
-------
 - https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/6d078cbe-2651-43a0-b460-685ac3f14c45

Building WOPI Client
-------------------------
 - [SharePoint 2013: Building your own WOPI Client, part 1](https://www.wictorwilen.se/blog/sharepoint-2013-building-your-own-wopi-client-part-1/)
 - [SharePoint 2013: Building your own WOPI Client, part 2](https://www.wictorwilen.se/blog/sharepoint-2013-building-your-own-wopi-client-part-2/)


[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fpetrsvihlik%2FWopiHost?ref=badge_large)
