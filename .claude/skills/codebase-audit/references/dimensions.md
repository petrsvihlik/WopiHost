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
- **Path traversal in the file-system provider (highest-severity class here).** A resource id or
  relative/suggested target that resolves outside the configured root (`..`, absolute paths, UNC,
  alternate data streams). Confirm the provider canonicalizes and re-roots (`Path.GetFullPath` +
  `StartsWith(root)`) before any `File.*`/`Directory.*` call — a WOPI host acts on client-supplied
  targets, so an unrooted path is arbitrary file read/write.
- **Non-constant-time comparison of secrets.** `==` / `string.Equals` / `SequenceEqual` on a proof
  signature, HMAC, access token, or lock id is a timing side-channel; crypto/auth comparisons use
  `CryptographicOperations.FixedTimeEquals`.
- **Secrets in logs / exceptions.** Access tokens, signing/proof keys, `Authorization` or
  `X-WOPI-Proof` header values, or file contents passed to `ILogger` or an exception message. Redact.
- **Weak randomness for security values.** `System.Random` (or `Guid.NewGuid()`) minting a token,
  nonce, or anything that must be unguessable → `RandomNumberGenerator`.
- **Token/JWT validation that doesn't validate.** `TokenValidationParameters` with
  `ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`/signature checks disabled, outside the one
  documented dev no-op proof validator.

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

## 12. Over-engineering & simplification (Medium/Low — the subtractive dimension)

The counterpart to every other dimension: instead of asking "what's missing or wrong," ask **"what's
here that earns nothing and could be deleted?"** Every finding here *removes* code. The repo is in
API stabilization and values a small, honest surface, so an abstraction that doesn't pay for itself
is a liability. This is the dimension that would have flagged the typed-id wrappers — and it's the
one that must **never** recommend *adding* a wrapper/type/layer (that's the anti-finding in SKILL.md).

The net-value bar applies with full force, and the danger here is the *opposite* over-reach: not
everything indirect is over-engineered. A finding ships only if removing the construct is
**unambiguously safe and clearer** — nothing real depends on it, no plausible near-term need, and it
isn't load-bearing public contract. When in doubt, it's load-bearing; leave it.

Method: interrogate each candidate with the subtraction lens in `lenses.md` — what breaks if it's
deleted, count the implementations, grep the usages, check the removal cost, and note what stays.

Patterns that are genuinely over-engineered here:

- **Wrapper / value types that only forward.** A type around a primitive whose whole body is
  `ToString()` + an implicit operator, adding no validation and preventing no real bug → the construct
  itself is the smell; the fix is to keep the primitive. (Typed ids #514/#515 — closed not-planned;
  see ledger. Don't re-propose; *do* flag any such wrapper that ever lands.)
- **Single-implementation indirection with no seam need.** An interface / abstract base / factory /
  strategy with exactly one impl, no test double, and no DI substitution → inline it. **Caveat:** most
  interfaces here are deliberate DI seams or public `Abstractions` contracts (the provider model is
  intentionally swappable — see CLAUDE.md); only flag indirection that genuinely earns nothing.
- **Pass-through layers.** A class/method that only delegates to another with no added behavior,
  logging, or adaptation → collapse the layer.
- **Speculative generality.** A generic type param, `virtual`/`abstract` member, event, or extension
  point with zero or one user and no concrete second case in sight → make it concrete.
- **Dead configurability.** An options property / flag / setting that nothing reads, or is always set
  to one value in every path → delete it. (Distinguish from the ledger's *intentional* aspirational
  OTel placeholders.)
- **Defensive code for impossible states.** Null checks on a non-nullable the compiler already
  guarantees; re-validating input already validated upstream; catch-rethrow that changes nothing.
- **Construct heavier than the job.** A builder chain / reflection / dynamic dispatch where a literal
  or a direct call is clearer and equivalent.

**Removal-cost guard (do not skip):** before filing, confirm the deletion isn't a breaking change to
a NuGet-packaged library's public API (package-validation baseline `8.0.0`) — `CollectionExtensions.
Merge` is unused but unremovable for exactly this reason (see ledger). Unused public surface in
**non-packaged** assemblies (samples, `infra/`, tests) is freely deletable; in packaged `src/*`
libraries it is not. Note the constraint in the finding rather than blindly recommending `delete`.

Report each as: the construct, its `file:line`, why it earns nothing, and the subtractive remediation
(`inline` / `collapse` / `delete` / `replace with literal`) — plus the removal-cost note if it's
public API.

## 13. Public API & .NET design guidelines (Medium/High on packaged libraries)

This repo ships ~10 NuGet packages with a package-validation baseline, so the **public surface** of
the `src/*` libraries is a contract. Hold it to the
[.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) —
the rules the framework holds itself to. Scope this dimension to **public/protected** members of
packaged libraries (`Abstractions`, `Core`, `Discovery`, `Url`, the providers, `Cobalt`); internal,
sample, infra, and test code are held to a looser bar. Most items map to a `CAxxxx` analyzer rule, so
a *suppressed* one (`<NoWarn>` / `#pragma warning disable CAxxxx` / `.editorconfig severity = none`)
is a prime lead — open it and confirm the suppression is justified (some are; see the ledger).

- **Naming.** PascalCase types/members; `I`-prefixed interfaces; async methods end in `Async`; no
  abbreviations / Hungarian; types are nouns, methods verbs; enum names singular (flags plural).
- **Collections & arrays on the surface.** Return `IReadOnlyList<T>` / `IEnumerable<T>`, not `List<T>`
  or `T[]` (CA1819); accept the narrowest interface that works. (Accepted exception:
  `WopiSecurityOptions.SigningKey` stays `byte[]` — see ledger.)
- **Type design.** `struct` only for small, immutable value types with value semantics; `sealed` by
  default unless inheritance is designed-for; abstract base vs interface chosen deliberately; no
  public mutable static state.
- **Member design.** Properties are cheap and side-effect-free (else a method); avoid `ref`/`out`
  where a return value or a record/tuple is clearer; `CancellationToken` is the last parameter and
  defaulted; favour overloads over ambiguous optional args on the public surface.
- **Exceptions.** Throw the most specific type (`ArgumentNullException.ThrowIfNull`, `ArgumentException`,
  `InvalidOperationException`) — never bare `Exception`/`ApplicationException`/`SystemException`;
  preserve the stack with `throw;` not `throw ex;`.
- **Over-exposed surface.** A `public` type/member with no external consumer that could be `internal`
  — every public symbol is a maintenance + package-validation liability. (Lens overlaps dimension 12;
  here the question is "should this be in the contract at all," not "is the abstraction earning its
  keep.")
- **Nullability matches behavior.** A public member annotated non-null that can return null (or the
  reverse), or `!` null-forgiving used to paper over a real nullable on the contract.

Report each as `path:line | guideline → why it matters on the public API → fix`, and flag whether the
fix is itself a breaking change (package-validation baseline) so it can be timed with a major bump.

## 14. Library hygiene & runtime correctness (Medium)

Cross-cutting correctness for code that ships as a library and runs inside someone else's host.

- **`ConfigureAwait(false)` in library code.** Every `await` in a packaged `src/*` library that
  doesn't need the original sync context should use `ConfigureAwait(false)`, so a consumer that blocks
  on the call can't deadlock. `AsyncExpiringLazy` already does this — new awaits should match. (Don't
  flag sample/ASP.NET request paths, where the context is benign.)
- **Disposal & stream leaks.** Internally-created `IDisposable`/`IAsyncDisposable` is wrapped in
  `using`/`await using`; a stream opened then not disposed on an error path is a leak; the
  caller-owns-disposal contract on `OpenReadAsync`/`OpenWriteAsync` is documented.
- **Globalization / ordinal correctness.** Protocol strings (header names, `X-WOPI-Override` values,
  ids, lock tokens) compare and case-fold with `StringComparison.Ordinal[IgnoreCase]` and
  `ToUpperInvariant()` — never the current culture (`ToUpper()`, default `string.Equals`). A
  culture-sensitive compare on a wire value is a latent bug (Turkish-I etc.).
- **`async void`.** Only valid on event handlers; elsewhere it swallows exceptions and can crash the
  host — make it `async Task`.
- **`DateTimeOffset` for protocol time.** Lock expiry / WOPI timestamps use `DateTimeOffset` (the lock
  providers already do); a bare `DateTime.Now`/`UtcNow` on an expiry or wire path is a smell.
