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
- **`WopiResourceId` being a `static` helper (not a value type) is the current state, not a bug.**
  Turning it into a typed id is a deliberate, API-breaking design task — tracked, not a quick win.
  Don't file "primitive obsession: string ids" as a fresh smell; link the design issue instead.

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

## Intentionally-kept duplication

- **`AzuriteFixture` / `PlaywrightFixture` duplication across test projects is accepted.** Sharing
  would force heavier test dependencies (`Aspire.Hosting.Testing`) onto lighter projects; the
  duplication is small and the consumers pin versions in lockstep. (Revisit only if it starts
  drifting.)
- **Sample `Program.cs` initialization overlap is accepted.** The only genuinely shared middleware
  (`MapDefaultEndpoints`) already lives in `ServiceDefaults`; folding logging setup into
  `AddServiceDefaults` would change logging for every consumer, including the Serilog backend and
  test hosts.
