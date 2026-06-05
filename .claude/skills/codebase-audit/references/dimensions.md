# Audit dimensions

The checklist each area reviewer works through. Patterns below are drawn from this repo's real past
findings — they tell a new run what kinds of things have genuinely been wrong here, so it can spot
the next instance instead of inventing generic advice. Every candidate still needs source
verification and a `file:line` anchor.

**Apply the net-value bar (see SKILL.md) to everything here:** only report a finding when its fix
would make the codebase unambiguously better. A pattern matching a checklist item is necessary but
not sufficient — if the "fix" would be a lateral move, add ceremony, fight an intentional decision,
or trade clarity for purity, it is not a finding. When in doubt, leave it out.

## 1. WOPI spec compliance (usually High)

The protocol is exacting; a single wrong header or status code silently breaks real clients
(Collabora, M365, the WOPI Validator suite). Check each against Microsoft Learn's WOPI REST docs.

- **Exact response headers.** No stray spaces in header *names* or *values* (a past bug shipped
  `"X-WOPI-InvalidFileNameError "` and `EMPTY_LOCK_VALUE = " "` instead of the empty string the spec
  mandates). On a 409 lock-mismatch / GetLock / unlocked file, `X-WOPI-Lock` must be present and set
  to the current lock id or the empty string per spec.
- **Status-code branching.** PutRelativeFile uses 409 vs 501 for distinct failures; over-length lock
  ids are 400; missing-vs-conflict is 404 vs 409. Confirm the handler returns the spec's code.
- **Lock atomicity.** RefreshLock / UnlockAndRelock MUST be a true compare-and-swap inside the
  provider (not a check-then-act in the endpoint). A past High was a non-atomic `Get`+`TryUpdate`.
- **Required request headers honored.** PutFile should read `X-WOPI-Editors`; PutRelativeFile
  `X-WOPI-FileConversion` / `X-WOPI-Size`; lock ops require `X-WOPI-Lock`.
- **Token scoping.** A freshly-minted child/relative resource gets a token bound to the NEW resource
  id (token-trading prevention), not the inbound token.
- **Sample token scoping.** A "view" link must not mint a token with write flags — Collabora derives
  view-vs-edit from `CheckFileInfo` permission flags, so an over-permissive token opens edit mode.

## 2. Leaky abstractions & contracts (High/Medium)

- **Framework types in `WopiHost.Abstractions`.** `HttpContext`, MVC/ASP.NET types leaking into the
  abstraction layer (the adapter `WopiRequestInfo` exists precisely to keep HTTP out of it).
- **Undefined / surprising contracts.** A property or method whose null/throw/empty behavior isn't
  documented but whose callers assume one (e.g. an interface member that throws on some platforms).
- **Read/write seam.** The read interface (`IWopiStorageProvider`) must not expose write affordances;
  writes go through `IWopiWritableStorageProvider` / `IWopiWritableFile`.
- **Sync-over-async at a boundary** that isn't a deliberate, documented `Dispose` case.

## 3. Architectural inconsistencies (Medium)

These are "two things that should match but don't." The providers are the usual offenders.

- **DI lifetime drift.** Equivalent providers registered with different lifetimes (a past fix aligned
  FileSystem `Scoped` → `Singleton` to match Azure). Singleton is safe only if the type is stateless
  and captures no scoped dependency.
- **Override semantics.** Storage providers register with `TryAdd*` so a host can pre-register its
  own; lock providers throw if one is already registered (exactly one per process). A plain
  `Add*` that should be `TryAdd*` is the smell.
- **Options-validation parity.** Every options-bound provider chains `.ValidateOnStart()`.
- **Config access pattern.** Implementations inject `IOptions<T>`, not `IConfiguration` — binding +
  validation belong at the composition root. An `IConfiguration` constructor param in `src/` is the
  smell (the FileSystem provider was migrated for exactly this).
- **Conformance-test coverage.** Every `IWopiLockProvider` / `IWopiStorageProvider` runs the shared
  `*ConformanceTests`. A provider without a conformance subclass is drift.
- **Provider conventions** (per CLAUDE.md): `Add{Name}{Storage|Lock}Provider` naming; an options
  class only when there are settings (Memory lock provider deliberately has none — see ledger).

## 4. Code duplication (Medium)

- **Repeated literals/logic.** The same forbidden-filename character set or sanitization in multiple
  handlers; the same id↔path map logic; copy-pasted validation.
- **Duplicated test fixtures.** Near-identical `AzuriteFixture` / `PlaywrightFixture` / token-minting
  helpers across test projects (some are intentionally left — check the ledger).
- **`csproj` / `Program.cs` drift.** Repeated `PropertyGroup` settings that belong in a
  `Directory.Build.props`; sample frontends independently re-wiring the same boilerplate.

## 5. Refactoring opportunities (Medium/Low)

- **Long handlers.** Endpoint bodies that interleave validate / resolve / write — extract named
  helpers (but watch for side effects: accessing `file.Length` before a write can prime a stale
  version cache).
- **Primitive obsession.** Bare `string` ids / lock tokens threaded everywhere. **Do not file** —
  typed `WopiResourceId` / `WopiLockToken` value types were evaluated and rejected (#514/#515 closed
  *not planned*: preventive-only, generalizes to every string, the proposed design didn't deliver the
  safety). See the ledger; only revisit if a concrete id-mix-up bug surfaces.
- **Many-parameter helpers.** A private helper taking 9–10 pass-through params → bundle into a
  record (done for `ProcessLockCore` → `LockOperationRequest`).

## 6. .NET 10 / Minimal-API idiom (Medium/Low)

- `TypedResults.*` over `Results.*` in endpoints (typed unions); consistent in the samples too.
- Redundant `[FromServices]` annotations (inferred in .NET 10); unnecessary `app.UseRouting()`.
- `[AsParameters]` request records for large handlers; `IParsable` for typed route params.
- File-scoped namespaces (IDE0161); no unused usings (IDE0005); source-generated `[LoggerMessage]`
  over direct `ILogger.LogX` calls.
- Endpoint filters / route groups over per-handler boilerplate.

## 7. Security smells (High)

- `postMessage(..., '*')` wildcard target origin in sample host pages — use the configured client
  origin.
- Stack-trace / exception-detail leakage not gated behind `IsDevelopment()`.
- `Enum.Parse` / other throwing parses on untrusted route/header input → 500 instead of 400; use
  `TryParse`.
- Broad `catch (Exception)` that swallows without filtering async-rude exceptions
  (OOM/SO/ThreadAbort) or without structured logging — especially in auth/proof validation.
- A dev-only escape hatch (e.g. proof-validation disable) that isn't refused outside Development.

## 8. Performance (Medium/Low)

- O(n) scans on a hot path (a past fix added a reverse `_pathToId` dict to `BlobIdMap` to kill a
  linear `TryGetFileId`). Mirror what the FS provider's `InMemoryFileIds` already does.
- TOCTOU between an existence check and a mutating call — use conditional headers/ETags
  (`IfNoneMatch`) instead of check-then-act.
- Avoidable allocations on cached/hot paths (weigh against readability — a cold 12h-cached path is
  not worth contorting).

## 9. Tech debt (Low)

- `//TODO` / `//HACK` comments with no tracking issue — convert or remove.
- `#pragma warning disable` without an adjacent rationale comment (warnings-as-errors is on, so each
  suppression should explain why it can't be honored — e.g. UTF-7 mandated by the spec).
- Hardcoded constants that should be named/centralized; magic strings for headers.
- Inconsistent option defaults (some `GetValue("X", defaultValue: ...)` explicit, some implicit).

## 10. Test-coverage gaps (Medium)

- Security/contract round-trips with unit tests on each side but none end-to-end (a past fix added a
  permission-claim round-trip integration test; another added real-proof-key validation coverage).
- A provider lacking the shared conformance subclass.
- Docker-gated suites (Azurite/Redis/Collabora) — note when a path is CI-only-verifiable.

## 11. Documentation accuracy — wiki + READMEs (High when actively misleading)

The docs must match the current API/behavior exactly — a wrong type name or a sample that won't
compile sends users down a dead end. Scope: the GitHub **wiki** (fetch it with
`scripts/fetch-wiki.sh` — it's a separate repo) AND every `README.md` (root, `sample/`, each
`src/*`, each `test/*`). The big risk is **post-migration drift**: docs that still describe the
pre-v9 world (MVC controllers, old method names, old id scheme).

For each doc claim, verify against source before trusting it:

- **Removed / renamed types referenced.** Grep the codebase for every type, interface, method, and
  namespace a doc names — an unresolved reference is drift. Prime suspects after the controller→
  Minimal-API move: `FilesController` / `ContainersController` / `FoldersController` /
  `EcosystemController` / `WopiBootstrapperController` (gone → `MapWopiEndpoints()` + `Endpoints/*`),
  and the renamed stream methods `IWopiFile.GetReadStream` / `GetWriteStream`
  (gone → `IWopiFile.OpenReadAsync` / `IWopiWritableFile.OpenWriteAsync`).
- **Wrong member types/shapes.** e.g. `Checksum` is `ReadOnlyMemory<byte>?` not `byte[]?`; `IWopiFile`
  has `Length` and no `Size`; lock expiry is the method `IsExpiredAt(now)`, not an `Expired` flag.
- **Code samples that wouldn't compile** against the current API (registration calls, ctor args,
  `using`s, signatures). `WopiFileSystemProvider`'s ctor takes `IOptions<…>`, not `IConfiguration`.
- **Config keys / section names** that don't exist on the bound options class — verify each
  `Wopi:StorageProvider` / `Wopi:LockProvider` / `Wopi:Security` / `Wopi:Discovery` /
  `Sample:*` / `AppHost:*` key against its options type.
- **ID-scheme claims.** Ids are deterministic SHA-256 hex of a canonical path (stable across
  restarts), case-folded on the file system, case-sensitive on Azure — not base64 paths, not MD5,
  not "short tokens", not "unstable across restarts".
- **Package names/IDs**, install snippets, project names, and **target framework** (`net10.0` only).
- **Broken internal links** between wiki pages or to repo paths that no longer exist.

Report each as `Doc page:line | what the doc says → what the code is → fix`, with the source
file:line you verified against. Wiki fixes are pushed to the `.wiki.git` repo; README fixes go
through the normal repo PR flow.
