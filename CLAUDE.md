# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build the entire solution
dotnet build WOPI.slnx

# Run all tests
dotnet test WOPI.slnx

# Run tests for a specific project
dotnet test test/WopiHost.Core.Tests

# Run a single test by fully qualified name
dotnet test test/WopiHost.Core.Tests --filter "FullyQualifiedName~TestMethodName"

# Run the sample WOPI host via Aspire orchestration
dotnet run --project infra/WopiHost.AppHost

# Run the sample WOPI host directly (port 5000)
dotnet run --project sample/WopiHost
```

## Code Style & Build Rules

- **Warnings as errors** is enabled globally (`TreatWarningsAsErrors`).
- .NET analyzers and code style enforcement are on (`EnforceCodeStyleInBuild`).
- Follow [.NET Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/).
- Centralized package management via `Directory.Packages.props` — specify versions there, not in individual `.csproj` files.
- Multi-targeting: libraries target `net8.0;net9.0;net10.0`; sample apps target `net10.0` only.
- Package validation baseline is `4.0.0` — avoid breaking public API changes in NuGet-packaged libraries.
- `InternalsVisibleTo` is auto-configured for `*.Tests` assemblies.

## Architecture

This is a modular **WOPI protocol host** implementation that integrates custom data sources with Office Online Server / Microsoft 365 for the Web.

### Core Libraries (src/)

- **WopiHost.Abstractions** — Interfaces and contracts: `IWopiStorageProvider`, `IWopiWritableStorageProvider`, `IWopiLockProvider`, `IWopiSecurityHandler`, `IDiscoverer`. All other projects depend on this.
- **WopiHost.Core** — WOPI REST endpoint controllers (`FilesController`, `ContainersController`, `FoldersController`), security (JWT auth, WOPI proof validation), and request infrastructure. This is the main server library.
- **WopiHost.Discovery** — Discovers WOPI client capabilities from Office Online Server XML.
- **WopiHost.Url** — Generates WOPI URLs for file/container actions.
- **WopiHost.Cobalt** — Optional MS-FSSHTTP (Cobalt) protocol support for co-authoring.
- **WopiHost.FileSystemProvider** — Default file-system-based storage provider (uses base64-encoded paths as identifiers).
- **WopiHost.MemoryLockProvider** — Default in-memory lock provider (ConcurrentDictionary, 30-min expiry).

### Dependency Chain

```
Abstractions ← Discovery ← Core
Abstractions ← Url ← (depends on Discovery)
Abstractions ← FileSystemProvider
Abstractions ← MemoryLockProvider
Abstractions ← Cobalt
```

### Provider Model

Storage and lock providers are loaded **dynamically by assembly name** from configuration (`StorageProviderAssemblyName`, `LockProviderAssemblyName` in `WopiHostOptions`). Custom providers implement `IWopiStorageProvider`/`IWopiLockProvider` and are registered via `services.AddStorageProvider(assemblyName)` / `services.AddLockProvider(assemblyName)`.

### Infrastructure (infra/)

- **WopiHost.AppHost** — .NET Aspire orchestrator for local development (backend :5000, frontend :6000, validator :7000).
- **WopiHost.ServiceDefaults** — Shared service configuration: OpenTelemetry, health checks, HTTP resilience, service discovery.

### Sample Apps (sample/)

- **WopiHost** — Backend WOPI server sample.
- **WopiHost.Web** — Frontend that integrates with a WOPI client (Office Online).
- **WopiHost.Validator** — WOPI protocol validation/testing tool.

## Key WOPI Concepts

- WOPI operations are distinguished by `X-WOPI-Override` header values (LOCK, UNLOCK, PUT, DELETE, RENAME, etc.).
- `X-WOPI-Proof` headers provide cryptographic request validation from the WOPI client.
- File/container identifiers are opaque strings (the filesystem provider uses base64-encoded paths).
- Locks auto-expire after 30 minutes per the WOPI spec.
