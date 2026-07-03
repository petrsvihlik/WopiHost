---
name: security-audit
description: >-
  Run a repeatable, threat-model-driven security audit of the WopiHost codebase and produce a
  tracker-ready findings report. Use this whenever the user wants a security-focused sweep — a
  WOPI threat-model review, "is this safe to ship", token/JWT and WOPI-proof validation, access-token
  scoping and authorization-bypass, path traversal in a storage provider, secret handling (keys,
  tokens, proof headers in logs), constant-time comparison, weak randomness, postMessage / origin
  validation, XXE in discovery-XML parsing, dependency CVEs, dev-only escape hatches, or info
  disclosure. Trigger on phrasings like "security audit", "security review", "threat model",
  "pen-test the host", "check for auth bypass / path traversal / secret leakage", "is the token
  handling safe", "harden the host", or "find security holes" — even when the user doesn't say the
  word "audit". This is the security counterpart to `codebase-audit` (which is the broad
  code-quality sweep); reach for this one when the lens is specifically security.
---

# Security audit

A standing, repeatable **security** health-check for WopiHost — the security-specialized sibling of
`codebase-audit`. Where that skill sweeps broad code quality, this one walks the WopiHost **threat
model** boundary by boundary and holds each trust transition to a security bar. It reproduces the
methodology behind this repo's prior security work (the proof-validation hardening, the per-resource
token scoping, the path-traversal guards, the dev-only proof-disable gate) so the host stays safe
across many runs — without re-flagging decisions already verified as intentional.

## What makes this repeatable (read first)

The single thing that keeps a recurring security audit useful instead of noisy is a **do-not-re-file
ledger**: items earlier runs investigated and deliberately rejected as non-issues or accepted risk
with a documented rationale. Every run MUST load it and suppress anything on it.

1. Read `references/do-not-refile.md` — the verified-intentional / accepted-risk ledger. Treat each
   entry as out-of-bounds unless the surrounding code has materially changed, in which case
   re-verify before resurfacing.
2. Read `references/threat-model.md` — the WopiHost trust boundaries, attacker model, and the
   per-dimension security checklist with the concrete patterns that have genuinely been wrong (or
   genuinely right) here. This is the skill's how-to-look memory.
3. Read the root `CLAUDE.md` — several "smells" are documented security conventions (the dev-only
   `DisableProofValidation` flag that throws outside Development, the action-scoped `wopi:fperms`
   token rule, the SHA-256 id scheme, the per-OS path canonicalization). Findings are measured
   against these.
4. Check open issues/PRs and the repo's `SECURITY.md` / security issue template so a finding
   cross-references an existing tracker rather than duplicating it.

Then run the scan, verify each lead by building the concrete attack, and report.

## The bar: a real security win, proven — never a hypothetical

A security finding ships only if it is **a concrete weakness an attacker (or a careless caller)
could exploit, or a real defense-in-depth gap on a trust boundary**, verified against the source —
not a generic "could be hardened." Apply this test before filing:

> Can I name the actor, the entry point, the input, and the impact? Is there a path from
> attacker-controlled data to the bad outcome that the current code does **not** already block?

If the honest answer is "the framework already blocks this," "nothing untrusted reaches here," "the
upstream layer already validates," or "this is theoretical with no reachable path," **drop it** — or,
if it's a meaningful belt-and-suspenders gap on a security boundary (a shipped library with no
backstop, a guard one sibling applies and another doesn't), file it at the right (usually Medium)
severity and **say explicitly why it isn't directly exploitable**. Honesty about reachability is the
whole game: a security report that cries wolf trains the maintainer to ignore it. A tight list of
true issues, each with a named attack path, is the goal.

**Reachability is mandatory.** For every candidate, trace the data flow from the trust boundary to
the sink. WopiHost's endpoints validate a lot up front (token auth, name sanitisation in the
endpoint layer before the provider, proof on mutation) — so a provider-level "missing check" is
often already covered on the HTTP path and is only a *library-consumer* / defense-in-depth issue.
Distinguish the two; never imply direct exploitability you didn't trace.

### Do NOT file — security anti-findings

These make the report worse, not safer. Each has a real reason it's not a finding here.

- **Constant-time comparison of a non-secret.** WOPI **lock ids are opaque client-chosen
  identifiers, not secrets** — comparing them with `==`/`Ordinal` is correct, not a timing channel.
  Constant-time matters only for proof signatures / HMACs / tokens / keys (and WOPI proof uses RSA
  signature *verification*, not a secret-byte compare, so it doesn't apply there either).
- **"Disable the dev escape hatch."** `Wopi:Security:DisableProofValidation` is dev-only **and already
  throws on startup outside Development** — that is the correct design (Collabora can't sign), not a
  hole. Only a finding if the throw-outside-Development guard is *missing*.
- **XXE on discovery XML.** `XElement.Load`/`Parse` on modern .NET default to `DtdProcessing.Prohibit`
  with no external resolver, so DTD/external-entity expansion is already disabled by the platform —
  and the discovery endpoint is a host-trusted WOPI client, not arbitrary input. Don't file unless an
  `XmlReaderSettings`/`XmlResolver` is explicitly configured to *re-enable* it.
- **Hardening with no reachable threat.** Adding rate limits, headers, or checks "for defense" where
  no attacker path exists and nothing in the threat model calls for it — that's churn dressed as
  security.
- **Re-flagging accepted, documented risk.** The sample frontends' anonymous access, the in-memory
  single-process lock provider's lack of cross-instance exclusion, the FS provider's "sample
  purposes only" TOCTOU — all documented trade-offs. Check the ledger.
- **Weak-RNG false alarms.** `Guid.NewGuid()` for a *non-security* value (a fallback file-name stem,
  a per-test container name) is fine; only flag `System.Random`/`Guid` minting something that must be
  *unguessable* (token, nonce, signing key — those already use `RandomNumberGenerator`).

When a candidate matches one of these, record it in the do-not-re-file section with the reason so the
next run doesn't re-investigate it.

## Method

Walk the threat model; don't grep blindly. Breadth comes from covering every boundary, depth from
proving reachability on the ones that matter.

1. **Mechanical security scan.** Run `.claude/skills/security-audit/scripts/security-scan.sh` (it
   `cd`s to the repo root itself via `git rev-parse --show-toplevel`, so it works from anywhere). It greps the
   stable security-relevant patterns (broad `catch`, `Enum.Parse` on input, `==`/`SequenceEqual`
   near "proof"/"token"/"hmac", `new Random(`, `postMessage(...,'*')`, `ValidateIssuer=false` &
   friends, secrets near `Log`, `DtdProcessing`/`XmlResolver`, `Path.Combine` with non-rooted input,
   `DisableProofValidation`). These are **leads, not findings** — every one is read in context and
   reachability-traced.

2. **Boundary-by-boundary walk** (see `references/threat-model.md` for each boundary's checklist).
   For each trust boundary — (a) WOPI client ↔ host (proof + token), (b) browser ↔ frontend
   (origin/postMessage/token-in-URL), (c) host ↔ storage provider (path traversal / reserved names),
   (d) host ↔ lock provider (atomicity / DoS), (e) host ↔ discovery (XML/HTTPS), (f) config & secrets
   — enumerate the inputs that cross it and confirm the code validates/authorizes/escapes before the
   sink. If subagents are available, fan out one reviewer per boundary; otherwise walk them in turn.
   The **sibling-implementation diff** is high-yield here too: a guard one provider applies and a
   twin omits is a likely real gap (this is how the Azure create-name path-traversal backstop gap was
   found — FS guarded, Azure didn't).

3. **Prove reachability, then apply the bar.** Open the file at each lead, trace attacker input →
   sink, and decide: directly exploitable (High+), defense-in-depth/library-consumer gap (usually
   Medium, say so), or already-blocked (drop). Mark survivors **[verified]** with the attack path.
   The breaker lens in `.claude/skills/codebase-audit/references/lenses.md` is the drill for this
   step: manufacture exact payloads (not categories), walk the boundary values, name race
   interleavings, and run the attack where cheap — a High/Critical ships with the concrete request
   that demonstrates it, which doubles as the regression test.

4. **Classify + anchor + attack path.** Every finding gets a severity, a boundary/dimension, a
   `file:line`, the **named attack path** (actor → input → sink → impact), and a remediation. Lead
   the report with the highest-severity, highest-confidence issues.

## Severity

Severity is exploitability × impact, security-framed:

- **Critical** — remotely exploitable, unauthenticated or trivially authenticated, high impact:
  auth bypass, signature/proof check that doesn't verify, arbitrary file read/write reachable from a
  WOPI request, token forgery, secret exfiltration.
- **High** — exploitable with some precondition (a specific config, an authenticated low-priv user),
  high impact: path traversal reachable through an endpoint, a view token that grants write a real
  client honors, JWT validation with a disabled check, a secret written to logs.
- **Medium** — defense-in-depth gap on a security boundary, not directly exploitable through the
  shipped surface (upstream already validates; a library-consumer-only path; a sibling-inconsistent
  guard); or info disclosure of limited value. **State why it isn't directly exploitable.**
- **Low** — hardening nicety with a plausible-but-narrow threat, or a security-relevant clarity/
  consistency fix.

When unsure between two levels, pick the lower and justify — under-claiming keeps trust; over-claiming
burns it.

## Output

Produce a single markdown report, ready to paste into a (private, if Critical/High) security tracker
issue. Use this structure:

```
## Security audit (<YYYY-MM-DD>)

<one-line scope: boundaries walked, how many findings, headline.>

### Critical
- [ ] **<title>** **[verified]** — [`path:line`](path#Lline). **Attack path:** <actor → input → sink → impact>. <why it matters>. **Fix:** <remediation>.

### High
- [ ] ...

### Medium
- [ ] ... (each notes why it is *not* directly exploitable)

### Low
- [ ] ...

---

### Threat-model coverage
| Boundary | Walked | Notes (what was verified safe) |
|---|---|---|
| WOPI client ↔ host (proof/token) | ✅ | |
| Browser ↔ frontend (origin/token) | ✅ | |
| Host ↔ storage (path traversal) | ✅ | |
| Host ↔ lock (atomicity/DoS) | ✅ | |
| Host ↔ discovery (XML/HTTPS) | ✅ | |
| Config & secrets | ✅ | |

---

### Do not re-file (verified non-issues / accepted risk)
<Carry forward every entry from references/do-not-refile.md, PLUS any new candidate this run
investigated and rejected, each with the reason. This regenerated section is what the next run folds
back into the ledger.>
```

Rules for the report:
- Phrase each finding in third person, concrete, with the attack path spelled out — no vague "could
  be insecure."
- Reference `file:line` with a real, confirmed anchor; never invent line numbers.
- For Medium/Low, explicitly state the reachability limit (what already blocks direct exploitation).
- Cross-reference existing issues/PRs and `SECURITY.md` when a finding is already tracked.
- **Disclosure discipline:** if a Critical/High is genuinely exploitable, recommend a *private*
  channel (the repo's security policy / a draft advisory) rather than a public issue, and keep the
  public-facing writeup free of a working exploit.

## Self-improvement (do this at the end of every run)

This skill sharpens each run. Whenever a run teaches you something that makes the next one faster,
sharper, or quieter, write it back into the skill's files — then commit. Capture, specifically:

- **A candidate that turned out a non-issue / accepted risk** → add it to `references/do-not-refile.md`
  with the reachability reason, so it isn't re-investigated.
- **A technique that found (or would have found) a real issue** → add it to `references/threat-model.md`
  under the relevant boundary (e.g. "diff sibling providers for an omitted guard").
- **A lead category the mechanical scan kept mis-firing on** → refine
  `.claude/skills/security-audit/scripts/security-scan.sh` and
  note it in the threat model's "noisy leads."
- **A new attack class** not yet covered → add a boundary/dimension to `references/threat-model.md`.
- **A finding you fixed this session** → note the resolving PR/commit next to the item.

Keep these files lean and true — a bloated ledger is as useless as an empty one. When you commit
skill updates, say what the run taught.
