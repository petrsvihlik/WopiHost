---
name: codebase-audit
description: >-
  Run a repeatable, multi-dimension code-quality / architecture / WOPI-spec-compliance audit of
  the WopiHost codebase and produce a tracker-ready findings report. Use this whenever the user
  wants to sweep the codebase for smells — code duplication, leaky abstractions, architectural
  inconsistencies, non-idiomatic .NET / Minimal-API patterns, wrong patterns, refactoring
  opportunities, security smells, performance issues, tech debt (TODOs, `#pragma` suppressions,
  hardcoded constants), test-coverage gaps, or WOPI spec non-compliance. Trigger on phrasings like
  "audit the codebase", "find code smells", "tech-debt sweep", "is the codebase in good shape",
  "review the architecture", "find duplication / leaky abstractions / refactors", "check WOPI spec
  compliance", or "keep the codebase in top shape" — even when the user doesn't say the word
  "audit". This is the standing health-check for the repo; prefer it over an ad-hoc grep sweep.
---

# Codebase audit

A standing, repeatable health-check for WopiHost. It reproduces the methodology behind this repo's
past audit trackers (broad-sweep code-smell, leaky-abstraction, Minimal-API-idiom, and WOPI
spec-correctness audits) so the codebase stays in top shape across many runs — without resurfacing
decisions that were already verified as intentional.

## What makes this repeatable (read first)

The single thing that keeps a recurring audit useful instead of noisy is a **do-not-re-file
ledger**: a list of findings that earlier audits investigated and deliberately rejected as
false-positives or intentional design. Every run MUST load it and suppress anything on it.

1. Read `references/do-not-refile.md` — the verified-intentional ledger. Treat each entry as
   out-of-bounds unless the surrounding code has materially changed.
2. Read the root `CLAUDE.md` — the repo's own conventions (comment style, provider model, DI
   rules, nullable/warnings-as-errors, net10 targeting, centralized packages). Findings are
   measured against these, and several "smells" are actually documented conventions.
3. Check currently-open issues/PRs (`gh issue list`, `gh pr list`) so you cross-reference existing
   trackers rather than re-filing them.

Then run the scan, verify, and report.

## The bar: only clear net wins (and never make it worse)

The point of this audit is to make the codebase *better* — so a finding ships only if its
remediation is a **clear improvement**. Apply this test to every candidate before filing it:

> If a competent maintainer applied the suggested fix, would the codebase be unambiguously better —
> more correct, safer, clearer, or meaningfully simpler — with **no** offsetting loss?

If the honest answer is "it's a lateral move," "it trades clarity for theoretical purity," or "it's
correct in principle but nobody would thank you for it," **drop it**. A short list of real wins is
the goal; padding it with debatable nits trains the reader to ignore the whole report. When unsure
whether a fix is a net improvement, leave it out rather than hedge.

### Do NOT flag — "fixes" that would factually worsen the codebase

These are anti-findings: recommending them makes the code *worse*. Each has been correctly rejected
in this repo before — don't resurface them.

- **Annotating instead of improving.** Adding `// reserved` to an interface-mandated unused
  parameter, or any comment whose only job is to silence a non-warning. (The honest fix for an
  unused `CancellationToken` on an async seam is to *honor* it, not comment it.)
- **Micro-optimizing cold paths into contortions.** Avoiding a trivial allocation on a
  cached / startup / 12h-refresh path via span gymnastics or a clever one-liner. Clarity wins where
  performance doesn't matter.
- **Premature abstraction / wrapper types with no safety gain.** A value type, factory, or
  indirection that adds ceremony without preventing a real, plausible bug. (Typed ids here are a
  deliberate, separately-tracked design decision — not a drive-by smell.)
- **Code-moving extractions with no readability gain — and real risk.** Splitting a clear linear
  pipeline just to shorten a method. Extractions can introduce subtle bugs (eagerly touching
  `file.Length` before a write once primed a stale version cache and broke a validator test).
  Extract only when it genuinely clarifies or removes duplication.
- **Consistency for its own sake.** An empty options class "for symmetry," or forcing two
  legitimately-different things to look identical. Symmetry isn't a goal; correctness and clarity are.
- **Fighting intentional configuration.** Undoing a deliberate analyzer suppression, a documented
  `Scoped` lifetime, a spec-mandated UTF-7 pragma, or a raised qlty threshold. If `CLAUDE.md` or an
  inline rationale defends it, it is not a finding.
- **Cleverness over clarity.** Replacing readable code with a denser expression that's harder to read.
- **Churn for taste during stabilization.** A sweeping mechanical rewrite whose only benefit is
  preference, with real regression surface and little payoff.

When a candidate matches one of these, don't discard it silently — record it in the do-not-re-file
section with the reason, so the next run doesn't re-propose it.

## Method

The audit fans out so breadth doesn't cost depth, then converges on verification. Don't try to hold
the whole codebase in one pass.

1. **Cheap mechanical pass.** Run `scripts/mechanical-scan.sh` (from the repo root). It greps the
   stable, deterministic smells (TODOs, `#pragma warning disable`, `AddScoped`/`AddSingleton` drift,
   `IConfiguration` constructor params, broad `catch (Exception)`, `postMessage(..., '*')`,
   `Enum.Parse` on input, etc.) and prints `file:line` hits. These are **leads, not findings** —
   every one still has to be read in context and verified.

2. **Parallel area scans.** Fan out one reviewer per area; each covers *all* dimensions for its
   slice. Areas: `src/WopiHost.Core` + `src/WopiHost.Abstractions`; the storage/lock providers
   (`*Provider`); `src/WopiHost.Discovery` + `Url` + `Cobalt`; `sample/` frontends; `infra/`;
   `test/`. Give each reviewer `references/dimensions.md` (the per-dimension checklist) and the
   do-not-re-file ledger, and have it return candidate findings with `file:line` anchors.
   - If subagents/Workflow are available, this is a natural fan-out (the past audits used "four
     parallel scans"). If not, walk the areas sequentially.

3. **Spot-verify in source AND pass the net-value bar.** Do NOT trust scan output. Open the file at
   the cited line and confirm the smell is real. Then apply the bar above: would the fix make the
   codebase unambiguously better? Past audits dropped items that "verification showed were false
   positives or already correctly handled" — also drop the ones whose fix would be a lateral move or
   a net loss. Mark survivors **[verified]**. A finding without a confirmed `file:line` anchor, or
   whose remediation isn't a clear win, does not ship.

4. **Classify + anchor + hint.** Every finding gets: a severity (High / Medium / Low), a dimension,
   a `file:line` link, and a one-line remediation hint. No vague "this could be cleaner." Lead the
   report with the highest-confidence clear wins — the slam-dunks a maintainer can apply without
   debate — so the most valuable work is unmissable.

See `references/dimensions.md` for the dimension checklist (WOPI spec compliance, leaky
abstractions, architectural inconsistencies, duplication, refactoring, .NET/Minimal-API idiom,
security, performance, tech debt, test-coverage gaps) — each with the concrete patterns earlier
audits actually found, so a new run knows what "good" looks like here.

## Severity

- **High** — wire-level / security / correctness: a WOPI spec violation a real client would hit, a
  data race or TOCTOU, a security leak (origin wildcard, stack traces, broad swallow of auth
  failures), a contract that throws where callers don't expect it.
- **Medium** — architectural drift, duplication, leaky abstractions, missing test coverage on a
  security/contract path, non-idiomatic patterns that will propagate.
- **Low** — quick wins: inconsistent defaults, stray allocations, unused params, untracked TODOs,
  tidy-ups.

When in doubt about whether something is a real problem, spend the verification time rather than
inflating the count — a tight, true list beats a long, hedged one.

## Output

Produce a single markdown report, ready to paste into a tracker issue. Use this structure:

```
## Codebase audit (<YYYY-MM-DD>)

<one-line scope: areas swept, how many findings.>

### High severity
- [ ] **<title>** **[verified]** — [`path:line`](path#Lline). <what's wrong + why it matters>. <remediation hint>.

### Medium severity
- [ ] ...

### Low severity
- [ ] ...

---

### Do not re-file (verified false positives / intentional design)
<Carry forward every entry from references/do-not-refile.md, PLUS any new candidate this run
investigated and rejected. This regenerated section is what you (or the next run) fold back into
the ledger.>

---

### Tally
| Dimension | High | Medium | Low |
|---|---:|---:|---:|
| ... | | | |
| **Total** | | | |
```

Rules for the report:
- Phrase each finding in third person, concrete, no meta-commentary (per CLAUDE.md comment style).
- Reference `file:line` with a real anchor; never invent line numbers — confirm them.
- If a candidate turns out intentional, don't silently drop it — move it to the do-not-re-file
  section with the reason, so the next run doesn't re-investigate it.
- Cross-reference existing issues/PRs by number when a finding is already tracked, instead of
  duplicating it.

## Closing the loop (so the ledger compounds)

After the user triages the report, append any newly-confirmed-intentional items to
`references/do-not-refile.md`. That file is the institutional memory; keeping it current is what
makes the next run faster and quieter. If you fix findings in the same session, note the resolving
PR next to the item (the past trackers used `→ #PR`).
