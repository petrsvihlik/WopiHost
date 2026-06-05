---
name: prepare-release
description: >-
  Turn the merged-PR changelog since the last release into polished, consolidated GitHub release
  notes (Highlights, ✨ New, 🐛 Fixes, 🧹 Refactors, 🔧 Maintenance/CI/deps, 💥 Breaking changes,
  and a verified Migration guide) modeled on this repo's prior releases, then open a **draft**
  GitHub release for review. Use whenever the user wants to cut, prep, draft, or write notes for a
  release. Trigger on phrasings like "prep a release", "prepare the 9.0.0 release", "cut a
  release", "draft a release", "release X.Y.Z", "write release notes", or "generate the changelog
  for the next version" — even when the version number isn't given.
---

# Prepare release

Produces a release description in the house style and creates a **draft** GitHub release. The draft
is the safety boundary: publishing a release fires `.github/workflows/release.yml`
(`release: types: [published]`) which pushes packages to NuGet.org — so this skill **never
publishes**. The user reviews the draft in the GitHub UI and clicks Publish themselves.

Repo: `petrsvihlik/WopiHost`. Tags are bare semver (`8.0.0`, not `v8.0.0`); the release target is
`master`.

## Inputs

- **New version** (required) — e.g. `9.0.0`. If the user didn't say it, ask. Pick the bump per
  semver from the actual change surface: any public-API break → major; new back-compatible API →
  minor; fixes only → patch. The repo is in API-stabilization, so breaking changes are expected
  and land as majors.
- **Previous tag** (auto) — the latest published release (`gh release list -L 1` /
  `gh release view --json tagName`). Override only if the user names a different base.

## Step 1 — Gather the raw changelog

Let GitHub assemble the merged-PR list rather than hand-collecting it:

```bash
bash .claude/skills/prepare-release/scripts/gather-changelog.sh <NEW_VERSION> <PREV_TAG>
```

It calls the `releases/generate-notes` API (the same engine behind GitHub's "Generate release
notes" button) and prints the raw `## What's Changed` list plus the `Full Changelog` compare link.
If the tag doesn't exist yet, generate-notes targets `master` HEAD — which is what you want when
prepping ahead of tagging. Keep that raw list; it's the input you transform, and the
`Full Changelog` line is pasted verbatim at the very end of the notes.

## Step 2 — Study the house format

Read the most recent prior release as the canonical template — match its section order, heading
style, table shape, and migration-guide depth:

```bash
gh release view <PREV_TAG> --repo petrsvihlik/WopiHost --json body -q .body
```

`references/format.md` distills that format and the consolidation/labeling rules. Read it before
writing.

## Step 3 — Categorize and consolidate

Map every raw PR into one section, collapsing noise (see `references/format.md` for the full
rules). In short:

- **Highlights** — a 2–3 paragraph lede naming the release's theme (the one architectural headline
  + the secondary themes), plus an upfront ⚠️ requirements/breaking call-out.
- **✨ New** — user-visible features and new packages/capabilities.
- **🐛 Fixes** — bug fixes and spec-correctness.
- **🧹 Refactors & internals** — internal cleanups worth noting (cross-link anything that's also a
  breaking change).
- **🔧 Maintenance, CI & dependencies** — **consolidate hard**: collapse every Dependabot bump of
  the same package into one `pkg X → Y` line spanning the whole range; group the re-applied /
  baseline / framework-targeting chores. Don't list 30 individual bump PRs.
- Group **related PRs that share an issue tracker** (e.g. several PRs all closing items of one
  audit issue) into a single bullet citing all of them.
- Drop pure-noise PRs (a bump later reverted/superseded within the same range) — but never silently
  drop a real change.

## Step 4 — Breaking changes + migration guide (verify against code)

This is the part that must not be hallucinated. For each PR that changes public API, **read the
actual change** before writing the migration step:

- `gh pr view <N> --repo petrsvihlik/WopiHost --json title,body`
- `gh pr diff <N> --repo petrsvihlik/WopiHost` (or read the current source — the renamed symbol,
  the new signature, the new registration call).

Then:

- Build a **💥 Breaking changes** table: `| | Before (Nx) | After (N+1.0) | PR |`.
- Write a numbered **Migration guide** with copy-pasteable ` ```diff ` before/after blocks for each
  break (target framework, registration/wiring, renamed symbols, changed signatures, DI lifetime,
  data/runtime breaks). Show the real old and new names/signatures, not invented ones.
- A rename with unchanged values → say "recompile, not a behavior change".

If there are no public-API breaks, omit both sections (a minor/patch release).

## Step 5 — Assemble the notes

Follow the skeleton in `references/format.md`. End with the verbatim `Full Changelog` compare link
from Step 1. Write the whole thing to a file so it can feed `--notes-file` and be shown to the user:

```
artifacts/release-notes-<NEW_VERSION>.md
```

(`artifacts/` is git-ignored, so the file isn't committed.)

## Step 6 — Create the draft release

```bash
gh release create <NEW_VERSION> \
  --repo petrsvihlik/WopiHost \
  --draft \
  --target master \
  --title "<NEW_VERSION>" \
  --notes-file artifacts/release-notes-<NEW_VERSION>.md
```

`--draft` is mandatory — it creates the tag-less draft without firing the NuGet publish workflow.
Print the resulting draft URL. Then show the user the full notes as a copy-pasteable fenced block
(wrap in a 4-backtick ```` ```` ```` fence so the inner ` ```diff ` blocks survive) and tell them:
review in the UI, edit if needed, and click **Publish** to tag + ship to NuGet.

## Guardrails

- **Never** run `gh release create` without `--draft`, and never `gh release edit --draft=false` /
  publish. Tagging + publish is the user's call because it triggers NuGet push.
- Don't invent PRs, issue numbers, or API shapes — every cited `#N` comes from the raw changelog or
  a real `gh pr` lookup; every migration diff reflects code you actually read.
- Keep prose in the repo's comment/voice style: concrete, third-person, no meta-narration.
