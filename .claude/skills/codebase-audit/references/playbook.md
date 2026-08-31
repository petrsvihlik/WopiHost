# Audit playbook — techniques, noisy leads, and dead-ends

The skill's growing memory of **how to audit this repo well**. `do-not-refile.md` records *what* was
rejected; this records *how to look* and *what not to waste time on*. Update it at the end of every
run — add a technique that paid off, a lead category that wasted time, or a dead-end — so the next
run is faster and sharper. Keep entries earning their place; prune ones that stop being true.

## Techniques that pay off here

- **Sibling-implementation drift.** When two types play the same role, diff them — a one-directional
  divergence is often a latent bug or smell. *(The 2026-06-05 run found a thread-safety bug in
  `BlobIdMap` by diffing it against its file-system twin `InMemoryFileIds`, which deliberately uses
  `ConcurrentDictionary` + a `Lock`.)* Pairs worth diffing here: the storage providers (FS vs Azure)
  and their id↔path maps; the three lock providers; the provider `ServiceCollectionExtensions`; the
  CheckXxxInfo builders; near-identical test fixtures.
- **Singleton + mutable state ⇒ check thread safety.** Any type registered as a singleton (or
  injected into one) that holds mutable collections must be concurrency-safe — the request pipeline
  is concurrent. Grep registrations, then inspect the type's fields for unguarded `Dictionary`/
  `List`/counters.
- **Hold the code to CLAUDE.md's own invariants.** CLAUDE.md states hard rules ("never commit these
  flags", "scoped by design", "specify versions in Directory.Packages.props", "single source of
  truth for `<Nullable>`"). Grep for violations of the doc's own stated rules. *(The 2026-06-05 run
  found a committed `AppHost:UseCollabora=true` that the doc explicitly forbids.)*
- **Doc-vs-code drift.** Spot-check load-bearing claims in CLAUDE.md / READMEs against the code
  (ports, file paths, lifetimes). *(2026-06-05: CLAUDE.md said backend `:5000`, code uses `:5050`.)*
- **Spec cross-check by operation.** For each WOPI operation, open its Microsoft Learn page and walk
  the required headers / status codes against the handler — exact strings matter.
- **Doc-symbol grep (docs accuracy).** Pull every type/interface/method/namespace/config-key a doc
  (wiki page or README) names and grep the codebase for each — an unresolved reference is drift.
  Fast high-yield variant: grep all docs for known removed/renamed symbols at once. *(The 2026-06-05
  doc pass caught most issues with `FilesController`, `GetReadStream`, `GetWriteStream`, `Hex-MD5`,
  "not across restarts", and "`Expired` flag".)* After any breaking rename/removal, add the old
  symbol to this grep list so the docs are re-checked for it next run.

## Noisy leads — interpret, don't auto-file

The mechanical scan casts wide on purpose; these categories are mostly false positives in this repo.
Read them in context before filing:

- **`IConfiguration` constructor param.** The real smell is `IConfiguration` in a *provider /
  implementation* constructor. The scan also flags `Add{Name}Provider(this IServiceCollection,
  IConfiguration)` *extension methods*, which legitimately take config to bind options at the
  composition root. Distinguish the two.
- **Lock-provider `AddSingleton<…>(sp => …)`.** Lock providers register with a factory and
  *throw* if one is already registered (exactly one per process). That is the intended convention,
  not `TryAdd` drift. (Storage providers, by contrast, should use `TryAdd*`.)
- **`"Bearer "` trailing-space hits.** The header-trailing-space regex matches the
  `Authorization: Bearer ` scheme literal — not a wire-format header-name bug.
- **Guarded `.Result`.** `.Result` after a `TaskStatus.RanToCompletion` check is a correct
  sync-unwrap of a completed task (see `do-not-refile.md`), not sync-over-async.
- **`AsyncExpiringLazy{T}.cs` `.Result`.** Those are reads of the cached record's `.Result` *property*
  (a value), not a blocking `Task.Result`.

## Dead-ends

The running list of investigated-and-rejected items lives in `do-not-refile.md`. When a run rules a
new candidate out, add it there with the reason — that's what stops the next run re-investigating it.

## Added 2026-06-15

- **Diff a provider's OWN mutation methods against each other — not just against its sibling
  provider.** The Azure create-name path-traversal backstop gap (`CreateWopiChildFile`/
  `CreateWopiChildContainer` omitted the `CheckValidFileName`/`CheckValidContainerName` guard that the
  same class's `RenameWopiFile`/`RenameWopiContainer` and `GetSuggestedName*` all apply) was missed by
  a sibling-*provider* diff but caught from the test-coverage angle (the FS provider had the
  rejection test; Azure didn't). Generalize: for each provider, list every method that takes a
  client-controlled name/id and confirm they ALL run the same guard — an intra-class omission is as
  real as a cross-provider one. Cross-reference the test suite: "FS has this test, the twin doesn't"
  often points straight at an unguarded path.

## Added 2026-08-31 (test-quality audit, #677)

- **Sibling-oracle diff for tests.** The status-only-assertion findings all came from one move:
  when a file-side and a container-side test cover the same operation, diff their assertions — the
  five weak container/file oracles each had a twin that probed the side effect properly. Same
  technique as the provider-method diff above, applied to test oracles.
- **Reason-tag pinning is self-verifying.** Asserting the telemetry `reason` tag on the proof
  validator's negative tests didn't just close the over-determination hole — it immediately
  exposed that the "garbage CSP blob" test was exercising a different branch than its comment
  claimed (`PlatformNotSupportedException` escaping to the outer catch on Linux, not the
  `CryptographicException` arm). Adding branch discrimination to a fail-closed gate's tests is
  worth doing *because* it flushes out mislabeled coverage on the first run.
- **`cat -v` / hex-dump suspicious test literals.** A string literal in an edge-case test that
  looks like a *valid* input being rejected (`"foobar"` failing `CheckValidFileName`) usually
  hides an invisible control byte. Render, don't trust the on-screen text.
- **Probe platform-dependent crypto exceptions empirically before pinning them.** `ImportCspBlob`
  throws different exception types per blob shape and per OS (all-zero blob →
  `PlatformNotSupportedException` on Linux; truncated `PUBLICKEYBLOB` header →
  `CryptographicException` everywhere). A 10-line scratch console app settles it faster than
  reasoning from docs.
