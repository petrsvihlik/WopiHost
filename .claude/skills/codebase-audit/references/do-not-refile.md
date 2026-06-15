# Do not re-file — verified false-positives & intentional design

Every entry here was investigated by a past audit (or a focused fix) and **deliberately rejected**
as a finding: it's either a false positive or an intentional design decision with a documented
reason. A new audit run must treat these as out-of-bounds and must not re-file them — unless the
surrounding code has materially changed, in which case re-verify before resurfacing.

Keep this file current: when a run investigates a new candidate and concludes it's intentional,
add it here with the reason. This ledger is what keeps repeat audits quiet and fast.

## Concurrency / async

- **`CobaltSession.cs` `.Result` access is not a sync-over-async deadlock.** Both call sites guard
  on `task.Status == TaskStatus.RanToCompletion` before touching `.Result` — correct sync-unwrap of
  an already-completed task.
- **`HashingBlobWriteStream` sync `SetMetadata` in `Dispose` is intentional.** `Stream.Dispose`
  can't be async (documented inline); the `DisposeAsync` path uses `SetMetadataAsync`. This is the
  one necessary sync-over-async boundary.

## Spec / wire format

- **`UtfString` keeps `#pragma warning disable SYSLIB0001` (UTF-7).** UTF-7 is mandated by the WOPI
  spec for `X-WOPI-SuggestedTarget` / `X-WOPI-RelativeTarget`; the framework deprecation can't be
  honored. The suppression carries a spec-linked rationale.

## Abstractions / API surface

- **`using Microsoft.AspNetCore.Mvc;` in `*Endpoints.cs` is required.** `[FromHeader]` /
  `[FromRoute]` / `[FromBody]` / `[FromServices]` live there and bind the `[AsParameters]`
  record-struct properties. Removing it breaks the build.
- **`ICheckFolderInfoBuilder.Build` staying synchronous is intentional.** Documented on the
  interface; making it async would regress an Infer# null-deref false positive.
- **`WopiSecurityOptions.SigningKey` stays `byte[]?` (CA1819 suppressed).** The `IConfiguration`
  `BinaryConverter` only targets `byte[]`, and `SymmetricSecurityKey(byte[])` consumes it directly.
  The suppression has a documented rationale next to the field.
- **The committed `<NoWarn>` suppressions are intentional (dimension 13 lead, but justified).**
  `CS1591` in the root `Directory.Build.props` silences missing-XML-doc warnings on non-packable
  projects (samples/infra/tests don't ship docs); `xUnit1051` in `test/Directory.Build.props` is a
  test-only analyzer relaxation. Both are documented inline. Don't file them as suppressed-rule
  findings unless a *packaged* library starts carrying a broad `<NoWarn>`.
- **Typed id/token value types (`WopiResourceId`, `WopiLockToken`) were considered and decided
  against.** Bare `string` resource ids and lock tokens are the intended state. The two design
  issues (#514, #515) were closed as *not planned*: the payoff is purely preventive (no known
  fileId/lockId mix-up bug), the move generalizes to every string in the codebase, and the proposed
  `implicit operator string` wouldn't even stop the mix-up it targeted. Don't re-file "primitive
  obsession: string ids" as a fresh smell. Only revisit if a concrete mix-up bug actually surfaces.

## DI / provider model

- **`MemoryLockProvider` deliberately has no options class.** It has no configurable settings — the
  lock expiry is the spec-fixed 30 minutes. Don't add an empty marker options class for symmetry.
- **`DefaultCheckFileInfoBuilder` (and the other CheckInfo builders) are registered `Scoped` on
  purpose.** They capture the writable storage provider, which may be scoped; promoting to singleton
  would risk a captive dependency. The registration site documents this.

## Validation framework

- **`Microsoft.Extensions.Validation` (`AddValidation()`) was evaluated and rejected for the WOPI
  surface.** Three structural mismatches (501-vs-400 status branching, override-conditional header
  requirements, spec-mandated empty-body + failure headers). Header mutual-exclusivity is handled
  the lightweight way via `EndpointHelpers.EnsureExactlyOneOf`.

## Samples / frontends

- **Server-rendered views in the `WopiHost.Web` frontends are appropriate.** It's a view-heavy
  sample; Minimal APIs aren't designed to serve views, so "migrate to minimal-API-served views" is
  not an improvement.
- **`Wopi:Security:DisableProofValidation` is a dev-only flag, by design.** It swaps in a no-op
  proof validator for clients (Collabora) that don't sign callbacks, and throws on startup outside
  Development. Not a security hole.
- **`AppHost:UseCollabora` defaulting `true` (code default in `infra/WopiHost.AppHost/Program.cs`)
  is intentional.** Collabora is the owner-chosen out-of-box VS "Play" / `dotnet run` experience —
  it joins Redis locks as a Docker-backed AppHost default. A *code* default is deliberately used (not
  a committed `appsettings.Development.json` flag, nor a `launchSettings.json` env var) precisely
  because it's the lowest-precedence config source: `appsettings.*` / env var / cmdline all override
  it off, so a Docker-less contributor can opt out locally. Don't re-file as "forces a Docker
  dependency on every contributor" — that was the old *committed-flag* finding (#517), resolved.

## Intentionally-kept duplication

- **`AzuriteFixture` / `PlaywrightFixture` duplication across test projects is accepted.** Sharing
  would force heavier test dependencies (`Aspire.Hosting.Testing`) onto lighter projects; the
  duplication is small and the consumers pin versions in lockstep. (Revisit only if it starts
  drifting.)
- **Sample `Program.cs` initialization overlap is accepted.** The only genuinely shared middleware
  (`MapDefaultEndpoints`) already lives in `ServiceDefaults`; folding logging setup into
  `AddServiceDefaults` would change logging for every consumer, including the Serilog backend and
  test hosts.

## Spec / provider behavior (added 2026-06-05)

- **`RenameContainer` returns 400 on an invalid name without `RenameFile`'s sanitise-retry — and
  that's spec-correct.** The RenameContainer spec omits the "host should try to generate a different
  name" language that RenameFile carries, so the divergence is intentional, not drift.
- **Redis lock options chain only `.ValidateOnStart()` (no `.Validate` for `ConnectionString`).**
  The connection string is optional on the DI-supplied `IConnectionMultiplexer` / Aspire path; a
  `.Validate` requiring it would break that path.
- **`CollectionExtensions.Merge` (`WopiHost.Url`) being unused in production is not a removal
  candidate.** It's `public` API in a NuGet-packaged library (package-validation baseline) — deletion
  is a breaking change.
- **`WopiHost.ServiceDefaults` OTel meters/sources for `WopiHost.Web` / `Discovery` / `FileSystem`
  that nothing emits to are harmless aspirational placeholders.** Registering a non-existent
  meter/source is a no-op; removing them is consistency-for-its-own-sake.
- **`ProcessLock` assigning `X-WOPI-ItemVersion = file.Version` unconditionally is fine.** Assigning
  a null string to `Headers[...]` yields `StringValues.Empty`, which suppresses the header — no need
  to null-guard it the way `GetFile` does.

## Verified this run (added 2026-06-15)

- **CA2007 / CA1707 / CA1056 disabled in the root `.editorconfig` are deliberately re-enabled at
  `warning` for `src/` via `src/.editorconfig`.** The root disables them so sample/test/infra stay
  quiet; `src/.editorconfig` turns them back on so packaged libraries ARE held to
  `ConfigureAwait(false)`, underscore-free public names, and `Uri`-typed URL properties. Don't
  re-file "CA2007 disabled globally → library code missing ConfigureAwait" — the split is intentional
  and the analyzer enforces it in `src/` (warnings-as-errors makes a genuine miss a build break).
- **`CobaltSession.cs` `catch (Exception)` in the faulted-session-cache path is correct.** It evicts
  the faulted `Lazy<Task<>>` entry (race-safe KeyValuePair overload), logs, and `throw;` — a fail-safe
  rethrow that prevents a permanently-cached faulted task, not a swallow. The mechanical scan flags
  it; interpret, don't file.
- **Azure provider scans lazily (`EnsureInitializedAsync`) where the FS provider scans in its
  constructor.** Acceptable design difference — the Azure scan is async network I/O that can't run in
  a ctor. Not sibling drift.
