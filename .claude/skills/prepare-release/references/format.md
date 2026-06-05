# Release-notes format

The canonical exemplar is the most recent prior release (`gh release view <PREV_TAG> --json body`).
This file distills its structure and the rules for turning a raw merged-PR list into it.

## Section skeleton

```
# <VERSION>

## Highlights
<2–3 short paragraphs: the one headline theme, then secondary themes. Last paragraph is an
 ⚠️ requirements/breaking call-out (target framework, "see Migration guide below").>

## ✨ New
- **<feature>** ([#NN](pr-link)[, closes #II]) — one-line what + why it matters.

## 🐛 Fixes
- **<fix>** ([#NN](pr-link)) — what was wrong / what's correct now.

## 🧹 Refactors & internals
- **<change>** ([#NN](pr-link)) — note; cross-reference if also breaking.

## 🔧 Maintenance, CI & dependencies
- **<grouped chore>** (...).
- **Dependency bumps** (consolidated): pkgA `x → y`, pkgB `x → y`, ...

---

## 💥 Breaking changes
| | Before (Nx) | After (N+1.0) | PR |
|---|---|---|---|
| **<area>** | <old> | <new> | [#NN](pr-link) |

---

## Migration guide

### 1. <break>
<prose> +
```diff
- old
+ new
```

---

**Full Changelog**: https://github.com/petrsvihlik/WopiHost/compare/<PREV_TAG>...<VERSION>
```

Omit 💥 + Migration guide entirely for a release with no public-API breaks.

## Voice

- Bold the subject of each bullet; keep to one or two lines.
- Every bullet cites its PR(s) as `[#NN](https://github.com/petrsvihlik/WopiHost/pull/NN)`; cite the
  closed issue too when it frames the work (`closes #II`).
- Concrete and third-person — describe what the release does, not the work that went into it.
- Emoji headers exactly as above (they're part of the house style).

## Consolidation rules (the part that makes notes readable)

1. **Dependabot bumps → one line per package, full range.** If `pkg` went `1.0 → 1.1 → 1.2 → 1.3`
   across four PRs, write `pkg 1.0 → 1.3` once under Maintenance. Never enumerate the intermediate
   bump PRs. Aspire's family of packages (`Aspire.Hosting.*`) collapses to a single
   `Aspire.Hosting.* X → Y` entry.
2. **Re-applied / superseded chores fold in.** "re-apply bumps Dependabot wrongly closed",
   baseline reverts/bumps, and test-package consolidation are one or two Maintenance bullets, not
   one each.
3. **PRs sharing an issue tracker group together.** Several PRs that each close items of one audit
   issue → a single bullet listing all of them and the issue.
4. **A feature delivered across multiple PRs is one bullet.** e.g. an action wired in one PR and
   fixed/hardened in two follow-ups → one bullet citing all three.
5. **Drop within-range churn.** A bump added then reverted inside the same release window doesn't
   appear. But a genuine change never disappears silently — if coverage is bounded, say so.
6. **Classify by user impact, not by commit type prefix.** A `refactor(...)` PR that removes
   `HttpContext` from a public seam is a *breaking change*, not a refactor footnote — surface it in
   both places.

## Migration guide must be verified

Each migration step mirrors code actually read in Step 4 (`gh pr diff` or the current source):
the real renamed symbol, the real new signature, the real new registration call. A `diff` block
that doesn't compile against the shipped API is worse than no block. When a symbol was renamed but
its value/behavior is unchanged, say so explicitly so consumers know it's a mechanical recompile.
