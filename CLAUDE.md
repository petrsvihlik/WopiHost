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
- **Comment style** (applies to `//` comments and the prose inside `///` XML docs):
  - Write in third person. Never "we"/"our"/"let's" — describe what the code does ("Resolves the owner"), not what the author did.
  - Comment only what is genuinely non-obvious or surprising to a new reader: a gotcha, an ABI/spec quirk, a non-local invariant. If the code is self-explanatory, leave no comment.
  - No meta-commentary: don't justify a choice to a reviewer, don't narrate how the code used to be, don't say "matching X" / "changed to Y".
  - Never reference GitHub issues or PRs by number (`#123`). If the context matters, inline the explanation itself.
  - Keep comments short and concrete; prefer deleting a comment to padding it.
  - Keep the `///` XML doc tags on public APIs (warnings-as-errors require them), but trim their wording to the same bar.
- .NET analyzers and code style enforcement are on (`EnforceCodeStyleInBuild`).
- **Nullable reference types** are enabled solution-wide from the root `Directory.Build.props`. Don't re-declare `<Nullable>` in individual projects — keep the single source of truth.
- Follow [.NET Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/).
- Centralized package management via `Directory.Packages.props` — specify versions there, not in individual `.csproj` files.
- Single-targeted on `net10.0` (libraries, samples, infra, tests). The v8 line was the last to support `net8.0` / `net9.0`; v9 onward is net10 only. The `tools/wopi-validator/` helper project stays on `net8.0` because the upstream `Microsoft.Office.WopiValidator` NuGet does.
- Build output is centralized under `artifacts/` at the repo root (`UseArtifactsOutput=true` in `Directory.Build.props`) — there are no per-project `bin/`/`obj/` folders.
- Package validation is enabled — avoid breaking public API changes in NuGet-packaged libraries. The baseline tracks the latest stable release (auto-bumped by `.github/workflows/release.yml`; see `PackageValidationBaselineVersion` in `Directory.Build.props` for the current value). Packages without a prior release on NuGet.org opt out via `EnablePackageValidation=false` in their `.csproj`.
- `InternalsVisibleTo` is auto-configured for `*.Tests` assemblies.

## Codebase audit skill

`.claude/skills/codebase-audit/` is the repo's standing health-check — a repeatable, multi-dimension
code-quality / architecture / WOPI-spec-compliance audit modeled on the prior audit trackers. Invoke
it (or just ask to "audit the codebase" / "find code smells") to sweep for duplication, leaky
abstractions, architectural drift, non-idiomatic patterns, security/performance smells, tech debt,
and spec non-compliance. It only reports **clear net wins** and respects a `do-not-re-file` ledger of
verified-intentional decisions so repeat runs stay quiet. After a run, fold any newly-confirmed
intentional decisions back into that ledger.

## Architecture

This is a modular **WOPI protocol host** implementation that integrates custom data sources with Office Online Server / Microsoft 365 for the Web.

### Core Libraries (src/)

- **WopiHost.Abstractions** — Interfaces and contracts: `IWopiStorageProvider`, `IWopiWritableStorageProvider`, `IWopiLockProvider`, `IWopiAccessTokenService`, `IWopiProofValidator`, `IWopiPermissionProvider`, `IDiscoverer`. All other projects depend on this.
- **WopiHost.Core** — WOPI REST endpoints as Minimal APIs (`src/WopiHost.Core/Endpoints/*Endpoints.cs`, wired via `app.MapWopiEndpoints()`), security (JWT auth, WOPI proof validation), and request infrastructure. The override-multiplexed `POST {id}` routes dispatch through `WopiOverrideMatcherPolicy` so each `X-WOPI-Override` value keeps its own authorization policy. This is the main server library.
- **WopiHost.Discovery** — Discovers WOPI client capabilities from Office Online Server XML.
- **WopiHost.Url** — Generates WOPI URLs for file/container actions.
- **WopiHost.Cobalt** — Optional MS-FSSHTTP (Cobalt) protocol support for co-authoring. Depends on the proprietary `Microsoft.CobaltCore` package from a private feed (auth via `COBALT_PACKAGES_TOKEN`). Its build is gated by the `IncludeCobalt` property (root `Directory.Build.props`): on when the token is set (CI) or a user-level `NuGet.Config` declares the private feed (probed across the per-OS config locations — `%APPDATA%\NuGet`, `~/.nuget/NuGet`, `~/.config/NuGet`), off otherwise — so a fork / external clone with no feed access still builds the rest of the solution (`WopiHost.Cobalt` and its test project compile as no-ops). Force with `-p:IncludeCobalt=true|false`. Because `nuget.config` maps the cobalt feed to `Microsoft.CobaltCore` only, omitting the package means restore never touches the private feed.
- **WopiHost.FileSystemProvider** — Default file-system-based storage provider. Resource identifiers are deterministic SHA-256 hashes of the path (see `WopiResourceId.FromCanonicalPath`); the in-memory id↔path map is rebuilt on startup and lazily refreshed after that — enumeration registers on-disk paths the startup scan never saw, and an unknown id falls back to a hash-matching tree scan. That keeps several processes over one tree (the sample's frontend + backend lanes) converging after out-of-band renames/creates.
- **WopiHost.MemoryLockProvider** — Default in-memory lock provider (per-instance `ConcurrentDictionary`, 30-min expiry). Single-process only.
- **WopiHost.AzureStorageProvider** — Azure Blob storage provider (alternative to FileSystemProvider). See its README for the connection-string config flow.
- **WopiHost.AzureLockProvider** — Azure-blob-backed distributed lock provider (alternative to MemoryLockProvider). Strongest cross-instance exclusion via Azure blob leases.
- **WopiHost.RedisLockProvider** — Redis-backed distributed lock provider. Best-effort, single-Redis (does not implement Redlock — see its README for rationale). Atomicity via Redis transactions (`WATCH` + `MULTI`/`EXEC` conditions); TTL-driven WOPI expiry.
- **WopiHost.Abstractions.Testing** — Shared `LockProviderConformanceTests` and `StorageProviderConformanceTests` xUnit classes that every `IWopiLockProvider` / `IWopiStorageProvider` implementation runs through. Provider-specific test projects derive a sealed subclass and supply a factory; xUnit picks up the inherited `[Fact]` tests automatically. Adding a future provider = one more conformance subclass.

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

Each storage / lock provider package exposes a typed `services.Add{Provider}{StorageOrLock}Provider(...)` extension. The composition root references the provider package(s) it wants and calls the extension directly — no reflection, no assembly-name strings. Available extensions: `AddFileSystemStorageProvider(cfg)`, `AddAzureStorageProvider(cfg)`, `AddMemoryLockProvider()`, `AddAzureLockProvider(cfg)`, `AddRedisLockProvider(cfg)`. The sample retains a small sample-local discriminator (`Sample:StorageProvider`, `Sample:LockProvider`) so the AppHost flag flow can flip providers at runtime — see [sample/WopiHost/ServiceCollectionExtensions.cs](sample/WopiHost/ServiceCollectionExtensions.cs).

Conventions a new provider follows (keeps the set from drifting):

- **Naming.** `Add{Name}StorageProvider` / `Add{Name}LockProvider`. Takes `IConfiguration` only when the provider has settings to bind.
- **Options.** A provider with configurable settings has a `{Name}{Storage|Lock}ProviderOptions` class bound from a `Wopi:StorageProvider` / `Wopi:LockProvider` section via `AddOptions<T>().Bind(...).ValidateOnStart()`. A provider with **no** settings (e.g. `MemoryLockProvider` — purely in-memory, expiry fixed at the spec's 30 min) deliberately has **no** options class; don't add an empty one for symmetry.
- **Consume options, not raw config.** Provider implementations inject `IOptions<T>`, not `IConfiguration` — binding/validation stays at the composition root.
- **Registration.** Storage providers register with `TryAdd*` (a host pre-registering its own implementation wins; a repeat call no-ops). Lock providers throw if an `IWopiLockProvider` is already registered — exactly one lock backend per process, so a second registration is a configuration error, not a silent override.

### Infrastructure (infra/)

- **WopiHost.AppHost** — .NET Aspire orchestrator for local development. The backend is pinned at `:5050` (the WOPI host URL is referenced by Collabora's `host.docker.internal:5050`; the AppHost pins 5050 rather than 5000 because on Windows port 5000 sits in the kernel-excluded range); the frontend and validator use `WithHttpsEndpoint()` so Aspire allocates their ports from the OS's free pool — the dashboard shows whatever was bound.
- **WopiHost.ServiceDefaults** — Shared service configuration: OpenTelemetry, health checks, HTTP resilience, service discovery.

### Sample Apps (sample/)

- **WopiHost** — Backend WOPI server sample.
- **WopiHost.Web** — Frontend that integrates with a WOPI client (Office Online / Collabora). Anonymous access; folder browsing + tabular file listing.
- **WopiHost.Web.Oidc** — Same role as WopiHost.Web but with OIDC sign-in. Per-resource permissions baked into the WOPI access token are derived from OIDC role claims via `OidcRolePermissionMapper`.
- **WopiHost.Validator** — WOPI protocol validation/testing tool.

### AppHost opt-in flags

The Aspire AppHost reads a few `AppHost:*` flags from configuration. Override them locally in `infra/WopiHost.AppHost/appsettings.Development.json` or via environment variables:

| Flag | Adds |
|---|---|
| `AppHost:UseAzureStorage` | Azurite emulator + `BlobStorage` connection string forwarded to the WOPI host. |
| `AppHost:UseRedisLocks` | **Default: `true` when launched via the AppHost.** Adds a Redis container; the WOPI host swaps `Sample:LockProvider` to `Redis` (so `AddSampleLockProvider` dispatches to `AddRedisLockProvider`) and receives the Aspire-allocated connection string via `Wopi:LockProvider:ConnectionString`. Set to `false` to fall back to `Memory` (single-process) — useful on contributor machines without Docker. Aspire already manages Docker resources, so the realistic distributed-lock backend is the right default for the orchestrated dev loop. |
| `AppHost:UseCollabora` | **Default: `true` when launched via the AppHost** (a code default in `Program.cs`, mirroring `UseRedisLocks`), so VS F5 / `dotnet run` launches with Collabora out of the box (requires Docker). Adds a `collabora/code` container as a real WOPI client for end-to-end editing and auto-overrides `Wopi:ClientUrl`, `Wopi:HostUrl`, `Wopi:Discovery:NetZone`, and `Wopi:Security:DisableProofValidation` on the affected projects. Set `AppHost:UseCollabora=false` (in `appsettings.Development.json`, an env var, or the command line — all override the code default) to run without Docker/Collabora. See the **End-to-end editing with Collabora Online** section in the root README for the full wiring (`host.docker.internal:5050`, NetZone gotcha, proof-key gotcha). |
| `AppHost:UseOnlyOffice` | **Default: `true` when launched via the AppHost** (a code default in `Program.cs`, mirroring `UseCollabora`), so the dev loop brings up both editors for side-by-side testing. Adds a *second* WOPI-client lane — an `onlyoffice/documentserver` container (port `9981`, `WOPI_ENABLED=true`) plus its **own** `WopiHost` backend (pinned `5051`) and a second `WopiHost.Web` frontend (`wopihost-web-onlyoffice`) bound to it. Same frontend project as the default lane — no code duplication; only the injected `Wopi:ClientUrl`/`NetZone` differ. The separate backend exists so each lane's proof-validation mode is independent (one backend can't hold two clients' proof keys). Verified end-to-end: a document opens in ONLYOFFICE with proof off (see below). **This is the heaviest default** — the image is ~4.3 GB and bundles its own PostgreSQL/RabbitMQ, so it's slower to start than Collabora; set `AppHost:UseOnlyOffice=false` to opt out (no Docker, or a machine without the disk/RAM). |
| `AppHost:OnlyOfficeProofValidation` | **Default: `false`.** Set to `true` to run the ONLYOFFICE lane's backend with real WOPI proof validation — ONLYOFFICE signs its callbacks (`X-WOPI-Proof`/`X-WOPI-ProofOld` over the spec's byte layout) and `WopiProofValidator` accepts them; the nightly E2E ONLYOFFICE suite runs with this flag on as the regression gate. The historical "Download failed" with proof on was a host bug, not an ONLYOFFICE one: `DefaultCheckFileInfoBuilder` advertised a `FileUrl` pointing back at the proof-gated `GetFile` endpoint, and per the WOPI spec clients fetch `FileUrl` **without** proof headers — that default `FileUrl` has been removed. The dev-loop default stays `false` only to mirror the Collabora lane's posture (Collabora can't sign at all). No effect unless `AppHost:UseOnlyOffice=true`. |
| `AppHost:IncludeOidcSample` | `WopiHost.Web.Oidc` frontend (requires IdP setup — see its README). Shares the default lane's backend. |

The orchestrated dev loop intentionally defaults to Redis locks, Collabora, and ONLYOFFICE (all Docker-backed): all three are code defaults in `Program.cs` (`GetValue("AppHost:UseRedisLocks"/"AppHost:UseCollabora"/"AppHost:UseOnlyOffice", defaultValue: true)`), not committed `appsettings.Development.json` flags. A code default is the overridable kind — `appsettings.json`, `appsettings.Development.json`, an env var, or the command line all take precedence over it, so a contributor without Docker can flip any of them off locally. (A `launchSettings.json` env var would sit *above* `appsettings.*` in precedence and silently defeat that override, so the default deliberately lives in code instead.) ONLYOFFICE is the heaviest of the three (~4.3 GB image, bundled Postgres/RabbitMQ) — flip `AppHost:UseOnlyOffice=false` on a constrained machine. The remaining flags (`UseAzureStorage`, `IncludeOidcSample`) stay opt-in — don't commit `appsettings.Development.json` with those enabled, as they impose external dependencies on every contributor.

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
- **Rename is editor-driven, never host-page-driven.** The WOPI client owns the rename affordance (its editable title, surfaced by `SupportsRename` + the `UserCanRename` token flag scoped to edit sessions); the editor calls the backend's `RENAME_FILE` op, which renames under the same file id and keeps the live session working. A host-page rename button is the wrong shape: there's no host→editor "start rename" postMessage, a direct backend `RENAME_FILE` from the frontend collides with the editor's edit lock (409), and the frontend's per-process id↔path map would go stale (the failure that reverted the toolbar-rename attempt). The host page only *reflects* the result — Office and ONLYOFFICE post `File_Rename` (`Values.NewName`) which `WopiLayout.razor` folds into the toolbar label; Collabora renames via its own title bar but doesn't post it (label refreshes on next load). All three surface rename through their own title UI (verified live for Collabora and ONLYOFFICE).
