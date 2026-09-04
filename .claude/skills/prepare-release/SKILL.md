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

- **New version** (optional) — e.g. `9.0.0`. `draft-release.yml` computes the minimum the public
  API surface allows (ApiCompat run in both directions against the last NuGet release: removals or
  changes → major, additions only → minor, neither → patch) and uses it when no version is given.
  Supply one only to go *above* that floor — the case ApiCompat cannot see: a behavioural break
  with an unchanged surface, like 9.2.0's new "requires Redis 8.4+" runtime requirement. The
  workflow refuses anything below the floor.
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
from Step 1. The first line must be `# <NEW_VERSION>` — the drafting workflow warns when the
heading and the version disagree, which catches notes pasted from the previous release.

Write them to a scratch file so they can be shown to the user and fed to the dispatch:

```
artifacts/release-notes-<NEW_VERSION>.md
```

`artifacts/` is git-ignored, deliberately. The notes are not committed: once the release is
published, GitHub Releases is their single source of truth, and a copy in the repo would go stale
the first time someone edits the draft in the UI before publishing.

## Step 6 — Create the draft release

The draft is created by the `draft-release.yml` workflow rather than by a local `gh` invocation,
because the notes are usually written in a session that has no GitHub CLI credentials — and giving
one a release-scoped token would put a credential that can publish to NuGet.org somewhere it is
stored in plaintext. The workflow authenticates with its own `GITHUB_TOKEN` instead.

Dispatch it with the notes content, and a version only to override the computed floor. From a
Claude session that is the GitHub MCP server's `actions_run_trigger` on `draft-release.yml` with
`ref: master`; from a shell:

```bash
# Version computed from the API surface:
gh workflow run draft-release.yml --repo petrsvihlik/WopiHost --ref master \
  -F notes=@artifacts/release-notes-<NEW_VERSION>.md

# Or pinned, to go above the floor:
gh workflow run draft-release.yml --repo petrsvihlik/WopiHost --ref master \
  -f version=<NEW_VERSION> \
  -F notes=@artifacts/release-notes-<NEW_VERSION>.md
```

Then read the run's job summary for the draft URL and report it.

The workflow only ever creates drafts, and refuses to run if the version is below the computed
floor or malformed, the notes are empty, the tag or a release for that version already exists, or
it is dispatched from anything but master.

**Regenerating a draft.** A draft is pinned to the commit it was created from, so once master
moves past it — a merge that belongs in the release, or notes that have gone stale behind a
dependency bump — publishing it would tag the older commit and ship without those changes.
Re-dispatch with `replace=true` (`-f replace=true`) to discard the existing draft and cut a new
one from the current HEAD. It refuses to touch a *published* release: that tag exists, its
packages are on NuGet.org, and consumers may already have resolved them. Notes travel as a `workflow_dispatch` input, which
caps the whole inputs payload at 65,535 characters — far above any release so far, and a breach
fails the dispatch loudly rather than truncating.

**Never publish.** `release.yml` triggers on `release: types: [published]`, so publishing is what
pushes packages to NuGet.org — a draft emits no such event. Tell the user to review the draft in
the UI, edit if needed, and click **Publish** themselves.

## Guardrails

- **Never** publish. `draft-release.yml` can only create drafts; if you fall back to a local
  `gh release create`, `--draft` is mandatory. Never `gh release edit --draft=false`, and never
  add a publish step to the workflow. Tagging + publish is the user's call because it triggers
  the NuGet push.
- Don't invent PRs, issue numbers, or API shapes — every cited `#N` comes from the raw changelog or
  a real `gh pr` lookup; every migration diff reflects code you actually read.
- Keep prose in the repo's comment/voice style: concrete, third-person, no meta-narration.
