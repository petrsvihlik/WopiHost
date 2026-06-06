# Continuous auditing — modes, scope, and safe automation

`SKILL.md` describes the **interactive deep-dive**: a human asks, the audit fans out across all
dimensions and the whole tree. That's the right shape when a person is driving. It's the *wrong*
shape for a scheduled or PR-triggered run, where re-sweeping everything every time is slow,
expensive, and noisy. This file adds the automation modes. The methodology, the net-win bar, the
do-not-refile ledger, and the anti-findings all still apply unchanged — only the **scope** and the
**side-effect discipline** change.

## Three run modes

| Mode | Trigger | Scope | Side effects |
|---|---|---|---|
| **Deep-dive** (default) | a human asks | whole tree, all 14 dimensions | report back to the human |
| **Scheduled sweep** | monthly cron | whole tree, all dimensions | one `audit` issue, or silence |
| **Incremental** | PR / since-last-audit | changed files + blast radius, mapped dimensions | comment on the PR or the audit issue |

Deep-dive is `SKILL.md` as written. The two automated modes add scoping plus a safe-output contract
on top of it.

## Incremental scope — audit the delta, not the world

A continuous audit earns its keep by being *cheap and relevant*. Don't re-sweep unchanged code.

1. **Establish the baseline.** Use the watermark recorded in the most recent `audit` issue (the
   `audited-sha: <40-hex>` line in its body); fall back to the previous release tag or
   `origin/master~N`. In PR mode the baseline is the PR's merge-base.
2. **Compute the delta.** `git diff --name-only <baseline>..HEAD` — the changed files are the seed.
3. **Expand to the blast radius.** A change rarely stands alone here:
   - a **public-API** file (`src/**/Abstractions`, any packaged `src/**`) → pull in dimension 2
     (contracts) and dimension 13 (design guidelines), and grep docs for the changed symbol (dim 11).
   - a **provider** file → run the sibling-drift technique against its twin (FS↔Azure id maps, the
     lock-provider trio) even if the twin didn't change.
   - a **doc / wiki** change, or a public rename anywhere → run dimension 11 over the docs.
   - a **DI / lifetime** change → re-check the singleton-mutable-state technique (playbook).
4. **Map files → dimensions.** Run only the dimensions a change can plausibly affect; skip the rest.
   A README-only PR doesn't need the security or performance passes.

Record the new HEAD sha as the watermark when you file or update the issue, so the next incremental
run starts where this one stopped.

## Safe-output contract (what automated runs may touch)

Borrowed from agentic-workflow "safe outputs": an unattended run gets a **read-only** view of the
code and may produce only these side effects — nothing else, ever:

- **Create** exactly one issue labelled `audit`, titled `[Audit] Codebase health — <YYYY-MM>`, **or**
- **Comment** on the existing open `audit` issue with net-new findings, **or**
- **Nothing** (a quiet run is a success, not a failure).

Hard prohibitions in automated mode: no editing code, no pushing branches, no opening pull requests,
no touching secrets/tokens, no network writes. "Proactive" fix-PRs are a separate, explicitly
opted-in mode (below) — never the default.

### Idempotency

Before filing: `gh issue list --label audit --state open`. If a matching issue exists, **comment the
delta** rather than open a duplicate; if there's no net-new finding since its watermark, do nothing.
Two runs over the same HEAD must converge on the same issue, not a pile of duplicates.

### Noise budget

The channel only stays useful while it stays high-signal. Per automated run:

- Cap at the **top ~10 findings** by (severity × confidence). If more survive verification, file the
  top ones and note "+N more lower-confidence — run a deep-dive to see them."
- Drop anything below firm confidence. Automated mode has no human to sanity-check a hedge, so the
  bar is *higher* than interactive, not lower.
- A run that finds nothing clear files nothing. Don't manufacture a finding to look busy.

## Read-only vs proactive

- **Report mode** (every automated trigger): findings only, per the safe-output contract.
- **Fix mode** (opt-in — a human asks, or a dedicated workflow with write scope): may open a
  *single-finding* PR, and only for a slam-dunk **subtractive** win (delete dead code, drop a flag
  nothing reads). Never bundle findings, never open a fix-PR for anything on the anti-findings list,
  never refactor for taste. One finding, one small PR, or nothing.

## Specialization — run one dimension

The dimensions are stably numbered (`references/dimensions.md`, 1–14). A focused workflow can invoke
a single slice instead of the whole sweep — e.g. "run dimension 11 over the wiki" as a docs-drift
check on every doc/public-API change, or "run dimension 7" as a security pass on `src/**` changes.
Specialized, frequent, single-dimension runs catch drift earlier than a monolithic monthly sweep and
cost a fraction as much. When invoked this way, load only that dimension plus the ledger and the
anti-findings, and apply the same safe-output contract.

## Meta-audit — keep the auditor honest (≈quarterly, or when noise creeps in)

A self-calibration pass, in the spirit of a meta-agent that watches other agents:

- **Measure signal quality.** Across recent `audit` issues, what share of filed findings were acted
  on versus closed *not planned* / dismissed? A rising dismissal rate means the bar has slipped —
  tighten it, and move the recurring false positives into `do-not-refile.md`.
- **Close the loop automatically.** When a filed finding is later dismissed or closed *not planned*,
  fold it into `do-not-refile.md` with the reason — the learning loop the skill already runs for
  interactive findings, applied to the issue automation.
- **Watch the watchers.** If the scheduled workflow itself starts failing, timing out, or going
  silent for months, that's a finding about the *automation*, not the code — surface it.
