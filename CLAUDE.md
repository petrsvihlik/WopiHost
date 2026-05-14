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

# Sample-app smoke tests (Playwright). First time only: install Chromium.
pwsh artifacts/bin/WopiHost.SmokeTests/debug/playwright.ps1 install chromium
dotnet test test/WopiHost.SmokeTests

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
- Build output is centralized under `artifacts/` at the repo root (`UseArtifactsOutput=true` in `Directory.Build.props`) — there are no per-project `bin/`/`obj/` folders.
- Package validation baseline is `6.0.0` — avoid breaking public API changes in NuGet-packaged libraries. Bump in lockstep with releases. The two newest packages (`WopiHost.AzureStorageProvider`, `WopiHost.AzureLockProvider`) opt out via `EnablePackageValidation=false` in their `.csproj` until they have a prior release to validate against.
- `InternalsVisibleTo` is auto-configured for `*.Tests` assemblies.

## Architecture

This is a modular **WOPI protocol host** implementation that integrates custom data sources with Office Online Server / Microsoft 365 for the Web.

### Core Libraries (src/)

- **WopiHost.Abstractions** — Interfaces and contracts: `IWopiStorageProvider`, `IWopiWritableStorageProvider`, `IWopiLockProvider`, `IWopiAccessTokenService`, `IWopiProofValidator`, `IWopiPermissionProvider`, `IDiscoverer`. All other projects depend on this.
- **WopiHost.Core** — WOPI REST endpoint controllers (`FilesController`, `ContainersController`, `FoldersController`), security (JWT auth, WOPI proof validation), and request infrastructure. This is the main server library.
- **WopiHost.Discovery** — Discovers WOPI client capabilities from Office Online Server XML.
- **WopiHost.Url** — Generates WOPI URLs for file/container actions.
- **WopiHost.Cobalt** — Optional MS-FSSHTTP (Cobalt) protocol support for co-authoring.
- **WopiHost.FileSystemProvider** — Default file-system-based storage provider. Resource identifiers are deterministic SHA-256 hashes of the path (see `WopiResourceId.FromCanonicalPath`); the in-memory id↔path map is rebuilt on startup.
- **WopiHost.MemoryLockProvider** — Default in-memory lock provider (per-instance `ConcurrentDictionary`, 30-min expiry). Single-process only.
- **WopiHost.AzureStorageProvider** — Azure Blob storage provider (alternative to FileSystemProvider). See its README for the connection-string config flow.
- **WopiHost.AzureLockProvider** — Azure-blob-backed distributed lock provider (alternative to MemoryLockProvider). Strongest cross-instance exclusion via Azure blob leases.
- **WopiHost.RedisLockProvider** — Redis-backed distributed lock provider. Best-effort, single-Redis (does not implement Redlock — see its README for rationale). Atomicity via Lua scripts; TTL-driven WOPI expiry.
- **WopiHost.Abstractions.Testing** — Shared `LockProviderConformanceTests` xUnit class that every `IWopiLockProvider` implementation runs through. Provider-specific test projects derive a sealed subclass and supply a factory; xUnit picks up the inherited `[Fact]` tests automatically. Adding a future provider = one more conformance subclass.

### Dependency Chain

```
Abstractions ← Discovery ← Core
Abstractions ← Url ← (depends on Discovery)
Abstractions ← FileSystemProvider
Abstractions ← AzureStorageProvider
Abstractions ← MemoryLockProvider
Abstractions ← AzureLockProvider
Abstractions ← RedisLockProvider
Abstractions ← Cobalt
Abstractions ← Abstractions.Testing (test-helper library; depends on xunit)
```

### Provider Model

Storage and lock providers are loaded **dynamically by assembly name** from configuration (`StorageProviderAssemblyName`, `LockProviderAssemblyName` in `WopiHostOptions`). Custom providers implement `IWopiStorageProvider`/`IWopiLockProvider` and are registered via `services.AddStorageProvider(assemblyName)` / `services.AddLockProvider(assemblyName)`.

### Infrastructure (infra/)

- **WopiHost.AppHost** — .NET Aspire orchestrator for local development. The backend is pinned at `:5000` (the WOPI host URL is referenced by Collabora's `host.docker.internal:5000`); the frontend and validator use `WithHttpsEndpoint()` so Aspire allocates their ports from the OS's free pool — the dashboard shows whatever was bound.
- **WopiHost.ServiceDefaults** — Shared service configuration: OpenTelemetry, health checks, HTTP resilience, service discovery.

### Sample Apps (sample/)

- **WopiHost** — Backend WOPI server sample.
- **WopiHost.Web** — Frontend that integrates with a WOPI client (Office Online / Collabora). Anonymous access; folder browsing + tabular file listing.
- **WopiHost.Web.Oidc** — Same role as WopiHost.Web but with OIDC sign-in. Per-resource permissions baked into the WOPI access token are derived from OIDC role claims via `OidcRolePermissionMapper`.
- **WopiHost.Validator** — WOPI protocol validation/testing tool.

### AppHost opt-in flags

The Aspire AppHost reads a few `AppHost:*` flags from configuration so the default first-run flow stays minimal. Set them in `infra/WopiHost.AppHost/appsettings.Development.json`:

| Flag | Adds |
|---|---|
| `AppHost:UseAzureStorage` | Azurite emulator + `BlobStorage` connection string forwarded to the WOPI host. |
| `AppHost:UseRedisLocks` | **Default: `true` when launched via the AppHost.** Adds a Redis container; the WOPI host swaps `LockProviderAssemblyName` to `WopiHost.RedisLockProvider` and receives the Aspire-allocated connection string via `Wopi:LockProvider:ConnectionString`. Set to `false` to fall back to `WopiHost.MemoryLockProvider` (single-process) — useful on contributor machines without Docker. Aspire already manages Docker resources, so the realistic distributed-lock backend is the right default for the orchestrated dev loop. |
| `AppHost:UseCollabora` | `collabora/code` container as a real WOPI client for end-to-end editing. Auto-overrides `Wopi:ClientUrl`, `Wopi:HostUrl`, `Wopi:Discovery:NetZone`, and `Wopi:Security:DisableProofValidation` on the affected projects. See the **End-to-end editing with Collabora Online** section in the root README for the full wiring (`host.docker.internal:5000`, NetZone gotcha, proof-key gotcha). |
| `AppHost:IncludeOidcSample` | `WopiHost.Web.Oidc` frontend (requires IdP setup — see its README). |

Never commit `appsettings.Development.json` with these flags enabled — they impose external dependencies on every contributor.

## Key WOPI Concepts

- WOPI operations are distinguished by `X-WOPI-Override` header values (LOCK, UNLOCK, PUT, DELETE, RENAME, etc.).
- `X-WOPI-Proof` headers provide cryptographic request validation from the WOPI client.
- File/container identifiers are opaque strings. The filesystem and Azure providers both derive ids via `WopiResourceId.FromCanonicalPath` (SHA-256 of a provider-canonicalized path, lower-case hex). Each provider canonicalizes its input the way the underlying store compares names — FS case-folds with `Path.GetFullPath(...).ToUpperInvariant()` because filesystems on Windows/macOS are case-insensitive; Azure passes the blob path as-is because Azure Blob Storage is case-sensitive.
- Locks auto-expire after 30 minutes per the WOPI spec.

## Sample-frontend conventions

When working on any of the sample frontends (`WopiHost.Web`, `WopiHost.Web.Oidc`, `WopiHost.Validator`), keep these cross-cutting invariants in mind — they aren't enforced by the type system:

- **Token `wopi:fperms` MUST be scoped by the requested action.** A "view" link must not mint a token containing `UserCanWrite`. OOS / M365 ship distinct view (`wv/wopiviewerframe.aspx`) and edit (`we/wopieditorframe.aspx`) URLs and so the URL alone enforces the mode — the bug is invisible there. Collabora ships a single `cool.html?` URL for both and derives view-vs-edit from `CheckFileInfo` permission flags, so an over-permissive token silently opens the file in edit mode regardless of which link the user clicked. The pattern: strip `UserCanWrite | UserCanRename` when `actionEnum != WopiActionEnum.Edit`, keep `UserCanAttend`/`UserCanPresent` (interaction-only flags).
- **`Wopi:Security:DisableProofValidation` is dev-only.** The flag exists in `sample/WopiHost` to swap `IWopiProofValidator` for a no-op when running against WOPI clients (like Collabora) that don't sign callbacks with proof keys. It throws on startup outside the Development environment so a stray production config cannot silently disable signature checking. The AppHost flips it on automatically when `AppHost:UseCollabora=true`.
- **Permission resolution lives in two places by design.** The host frontend bakes per-resource permissions into the access token at mint time (claim `wopi:fperms`), and the WOPI backend's `IWopiPermissionProvider` reads them back at `CheckFileInfo` time. Production hosts replace the default mapper with an ACL-store lookup keyed off the user's `sub` and the resource id; see `OidcRolePermissionMapper` for the seam.
