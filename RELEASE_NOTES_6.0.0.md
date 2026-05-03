# WopiHost 6.0.0

A milestone release that hardens the Ecosystem surface to spec, opens the door to distributed deployments with new Azure providers, and ships a production-style OIDC sample. Several long-standing concurrency bugs in discovery and Cobalt are fixed along the way.

> **Heads up — breaking changes**
> - `IWopiLockProvider` is now async (see *Async lock provider* below).
> - WOPI bootstrapper response shape changed to match the spec.
> - Ecosystem endpoints now reject token-trading and enforce stricter scopes.

---

## Highlights

### Spec-compliant WOPI bootstrapper ([#308](https://github.com/petrsvihlik/WopiHost/pull/308))
The bootstrapper endpoint has been rewritten to follow the WOPI bootstrapper specification end-to-end — correct response envelope, correct error codes, and correct claim handling for the federated-identity hand-off. Hosts that relied on the old response shape will need to refresh any client-side parsing.

### Hardened Ecosystem endpoints ([#306](https://github.com/petrsvihlik/WopiHost/pull/306), [#307](https://github.com/petrsvihlik/WopiHost/pull/307))
`CheckEcosystem` and `GetRootContainer` now validate inputs and apply scopes consistently with the rest of the Files/Containers surface. `GetEcosystem` no longer permits token-trading (a class of attack where a low-privilege token is exchanged for a higher-privilege one), and `GetFileWopiSrc` is stubbed out as an explicit not-implemented response so callers fail fast instead of silently misbehaving.

### Azure Blob storage + Azure Blob lock providers ([#323](https://github.com/petrsvihlik/WopiHost/pull/323))
Two new provider assemblies — `WopiHost.AzureStorageProvider` and `WopiHost.AzureLockProvider` — let you back a WOPI host with Azure Blob Storage instead of the local filesystem and the in-process lock dictionary. The lock provider uses blob leases for cross-instance coordination, which makes multi-replica / load-balanced deployments viable for the first time. As part of this work, `IWopiLockProvider` was made fully async — this is a breaking change for any custom lock provider implementation.

### OIDC frontend sample ([#316](https://github.com/petrsvihlik/WopiHost/pull/316), closes [#198](https://github.com/petrsvihlik/WopiHost/issues/198))
A new `WopiHost.Web.Oidc` sample shows how to wire a real identity provider into the frontend that mints WOPI access tokens. Per-resource permissions are derived from OIDC role claims via the new `OidcRolePermissionMapper` and baked into the token at mint time, with the WOPI backend reading them back at `CheckFileInfo` time — the canonical pattern production hosts should follow when integrating an ACL store.

### Optional Collabora Online for end-to-end editing ([#327](https://github.com/petrsvihlik/WopiHost/pull/327), [#328](https://github.com/petrsvihlik/WopiHost/pull/328), [#329](https://github.com/petrsvihlik/WopiHost/pull/329))
The Aspire AppHost now has an `AppHost:UseCollabora` flag that spins up a `collabora/code` container and rewires the sample projects to talk to it, giving you a fully working WOPI client locally with no Office Online Server license required. The flag also handles two well-known gotchas automatically: forcing `NetZone=ExternalHttp` so discovery picks the right action URLs, and disabling proof-key validation since Collabora doesn't sign callbacks.

### Cobalt: GitHub Packages migration + per-file caching ([#320](https://github.com/petrsvihlik/WopiHost/pull/320), [#322](https://github.com/petrsvihlik/WopiHost/pull/322))
`Microsoft.CobaltCore` was migrated off the (now-defunct) MyGet feed to GitHub Packages and bumped from 15 → 16. On the runtime side, `CobaltFile` instances are now cached per file rather than per request, and the user principal is flowed through `AsyncLocal` so co-authoring sessions see the right identity across awaits. This fixes a class of intermittent permission-denied errors during multi-user editing.

---

## Bug fixes

### View tokens are now read-only ([#330](https://github.com/petrsvihlik/WopiHost/pull/330))
View links were minting tokens that still carried `UserCanWrite`. Against Office Online Server this was invisible because the URL itself (`wv/...` vs `we/...`) decides view-vs-edit, but Collabora ships a single URL for both modes and derives view-vs-edit from `CheckFileInfo` permission flags — meaning an over-permissive token silently opened files in edit mode. View-action tokens now strip `UserCanWrite | UserCanRename` while keeping interaction-only flags like `UserCanAttend` and `UserCanPresent`. A close button was also added to the editor.

### Discovery concurrency + Linux Owner support ([#315](https://github.com/petrsvihlik/WopiHost/pull/315), [#317](https://github.com/petrsvihlik/WopiHost/pull/317), [#318](https://github.com/petrsvihlik/WopiHost/pull/318))
`AsyncExpiringLazy` had a race where two concurrent callers could both end up refreshing the cached value, and its semaphores weren't being disposed. Both are fixed, lazies are now eager-initialized so Infer# can track ownership, and the file-system provider's owner-resolution path now works on Linux (previously Windows-only via WMI).

### Other fixes
- File-icon fallback URL is now root-relative, so it resolves correctly when the frontend is hosted under a path prefix ([#326](https://github.com/petrsvihlik/WopiHost/pull/326)).

---

## Build, CI & infrastructure

- **Centralized output under `artifacts/`** ([#313](https://github.com/petrsvihlik/WopiHost/pull/313)) — `bin/`, `obj/`, and package output now live under a single `artifacts/` folder at the repo root, simplifying clean operations and CI caching.
- **Infer# static analysis workflow** ([#314](https://github.com/petrsvihlik/WopiHost/pull/314)) — runs on every PR to catch null-deref, resource-leak, and concurrency issues before they ship.
- **Validator regression pinning** ([#312](https://github.com/petrsvihlik/WopiHost/pull/312)) — known-failing ProofKeys cases are now hard-pinned so any regression breaks CI instead of being silently absorbed into the failure baseline.
- **TODO cleanup, sync-over-async fix, baseline bump to 5.0, README accuracy pass** ([#319](https://github.com/petrsvihlik/WopiHost/pull/319)).
- **Roslyn analyzer cleanup** ([#325](https://github.com/petrsvihlik/WopiHost/pull/325)).

## Docs

- **IIS app-pool recycling warning for the in-memory lock provider** ([#332](https://github.com/petrsvihlik/WopiHost/pull/332)) — `WopiHost.MemoryLockProvider` loses all locks when the app pool recycles; if you're hosting on IIS with a non-trivial editing load, switch to `WopiHost.AzureLockProvider` (or another out-of-process provider).

## Dependencies

- `Microsoft.IdentityModel.Tokens` 8.17.0 → 8.18.0 ([#309](https://github.com/petrsvihlik/WopiHost/pull/309))
- `System.IdentityModel.Tokens.Jwt` 8.17.0 → 8.18.0 ([#311](https://github.com/petrsvihlik/WopiHost/pull/311))
- `Scalar.AspNetCore` 2.14.8 → 2.14.9 ([#310](https://github.com/petrsvihlik/WopiHost/pull/310))

---

**Full Changelog**: https://github.com/petrsvihlik/WopiHost/compare/5.1.0...6.0.0
