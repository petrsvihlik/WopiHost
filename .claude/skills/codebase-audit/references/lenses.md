# Review lenses — distilled from Microsoft Amplifier's persona skills

Orthogonal **perspective** lenses that complement the audit's **area** fan-out. Area reviewers
slice the repo by directory; a lens is a load-bearing question every reviewer holds up to whatever
it reads. Distilled from
[microsoft/amplifier-bundle-skills](https://github.com/microsoft/amplifier-bundle-skills) (MIT,
upstream commit `92e4e0e`) per the evaluation in #571 — the persona voice was dropped, the method
kept, and each lens specialized to this repo. The ledger at the bottom records what was
deliberately *not* taken, so a future run doesn't redo the assessment.

## Subtraction lens — dimension 12's method (from `cranky-old-sam`)

The load-bearing question: **"what breaks if this doesn't exist?"** The burden of proof is on the
complexity — every abstraction, layer, and indirection must justify itself against the alternative
of not having it. `dimensions.md` §12 lists the *patterns* (wrappers that only forward,
single-implementation indirection, dead configurability…); this is the *interrogation* that turns
a pattern hit into a verified finding:

- What concretely breaks if the construct is deleted? "Nothing, today" **is** the finding.
- Is it solving a problem the repo has, or one it might have? Speculative generality is debt, not
  investment — adding it back later, when a real second case exists, is almost always cheaper than
  owning it now.
- Could it be a smaller thing — a function instead of a class, a value instead of a function,
  nothing instead of a value?
- How many things must a new reader understand to change it? Count the hops.

Evidence moves (do these; don't eyeball):

- **Count implementations.** For each interface / abstract member / `virtual`: exactly one
  implementation, no test double, no DI substitution → inline candidate. (Caveat: most
  `Abstractions` interfaces are deliberate provider seams — see §12's caveat and the ledger.)
- **Grep usages.** An abstraction consumed in exactly one place is an inline candidate; an options
  property or flag nothing reads is a delete candidate.
- **Check the removal cost** before filing — packaged public API only breaks at a major bump
  (§12's removal-cost guard).

**"What stays" discipline:** a subtraction finding names what it examined and *kept*, not just
what it wants deleted. Complexity that earns its keep is fine — saying so keeps the lens honest
and prevents the over-reach §12 already warns about (not everything indirect is over-engineered).

## Breaker lens — verifying High correctness/security candidates (from `tester-breaker`)

The load-bearing question: **"what concrete input makes this fail?"** A candidate verified this
way ships with the exact request/input that triggers it — never a category ("malformed input") —
and that input doubles as the regression test the fix must pass.

The drill:

1. **Find the input surface** the candidate sits on — where client-controlled data enters.
2. **Manufacture exact inputs**, not categories: empty, missing, single, huge, reversed,
   off-by-one, exactly-at-the-limit, wrong-encoding.
3. **Run the attack where cheap** — a failing test or a request against the sample host beats a
   hypothesized failure read off the source. When running it isn't practical, report the break as
   *traced*, not *demonstrated*.
4. **For races, name the interleaving** — the two callers and the order that corrupts state — not
   "there might be a race." If no shared mutable state is in play, say so and drop the concern.

WopiHost's standing input surfaces, and boundary values worth throwing at them:

- **Lock ids** — empty vs missing `X-WOPI-Lock`; exactly 1024 chars vs 1025 (over-length → 400);
  a lock id belonging to a different file (mismatch → 409 with the current lock echoed in
  `X-WOPI-Lock`).
- **Client-supplied names** (PutRelativeFile suggested/relative targets, RENAME) — `..\`, rooted
  paths, UNC, alternate data streams (`doc.docx:hidden`), Windows reserved device names (`CON`,
  `NUL`), trailing dot/space, illegal chars, the empty string, a 300-char name. All must fail
  single-segment validation *before* reaching any path-building call.
- **`X-WOPI-Override`** — unknown value, wrong casing, empty; each must map to the spec's status
  code, never a 500.
- **Proof headers** — timestamp outside the acceptance window, current/old key permutations, a
  mutation of the signed byte layout; each must fail closed.
- **Resource ids** — a well-formed id for a nonexistent resource (404, not 500); an id of the
  wrong shape; a stale id after an out-of-band rename (the lazily refreshed id↔path map).
- **Race surfaces** — the lock CAS paths (RefreshLock / UnlockAndRelock must be atomic inside the
  provider), and the id↔path maps under concurrent rename/enumeration (where the `BlobIdMap`
  thread-safety bug lived).

Seam discipline: this lens owns *fails-now-on-this-input*. "Costs too much later" belongs to the
net-win bar; "shouldn't exist at all" belongs to the subtraction lens. A breaker finding that
can't name its input isn't verified yet.

## Panel discipline for the fan-out (from `council`)

Three rules that keep a multi-reviewer sweep honest, applied to SKILL.md's Method steps 2–4:

- **Coverage is reported, never implied.** The report states which areas/dimensions were actually
  swept. A reviewer that fails, times out, or is skipped leaves its area **not covered** — name it
  in the scope line. "Reviewed, nothing found" and "not reviewed" must never be conflated; a
  silent gap reads as a clean bill.
- **No silent verdict downgrades.** Every High/Medium candidate a reviewer returns is either
  verified into the report or explicitly rejected into the do-not-re-file section with the reason.
  Synthesis may re-rank candidates; it may not make one disappear.
- **Resolve conflicts in source; keep the losing argument.** When two reviewers reach opposing
  conclusions on the same construct ("delete it" vs "it guards an edge case"), the arbiter is the
  source-verification step, and the rejected position goes into the ledger so the next run doesn't
  re-litigate it. (This replaces the upstream debate-to-consensus loop — see below.)

## Evaluated and not taken (the ledger for this distillation)

Recorded so future runs — and future readers of #571 — don't redo the assessment:

- **Running Amplifier itself as a separate audit pipeline** — rejected. It's a full Python
  runtime/bundle framework; this audit already fans out via subagents, and the bundle's skills are
  Amplifier-native (`delegate` tool, `context_depth`, `foundation:` refs) so they don't run here
  as-is. A second pipeline would add a heavy dependency to get a worse copy of what the Method
  already does.
- **Copying persona skills wholesale / adding them as standalone skills** — rejected. The tone
  theater is most of their text, a standalone lens skill would compete with this audit as an entry
  point, and the audit already owns the machinery they would duplicate (ledger, bar, dimensions).
- **`council`'s debate-to-consensus loop** — skipped as overkill; source verification is the
  arbiter here. Its durable ideas (coverage manifest, no silent downgrades, conflict recording)
  are the panel discipline above.
- **`crusty-old-engineer`** ("what will this cost to own later?") — already embodied in the
  net-win bar's "would a maintainer thank you" test; nothing new to import.
- **`intent-keeper`, `user-advocate`** — goal/product lenses; an audit's goal is fixed by the
  skill itself. Not applicable.
- **`verification-discipline`** — the repo already encodes the gradient (unit → provider
  conformance → Docker-gated integration → Playwright smoke); nothing to add.
- **The rest of the bundle** (domain pattern libraries, `skillify` / `personafy` / `adapt-skill`,
  `session-debug`, `mass-change`, `image-vision`) — Amplifier-platform tooling or unrelated
  domains; nothing applicable to auditing this repo.

Re-evaluation trigger: upstream is pinned at `92e4e0e`. If the bundle later grows a genuinely new
review *method* (not a new persona voice), a future run may re-distill — the bar is the same
net-win test the audit applies to code.
