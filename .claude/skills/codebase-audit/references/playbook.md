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
