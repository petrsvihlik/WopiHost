#!/usr/bin/env bash
# Print the raw merged-PR changelog for an upcoming release, using GitHub's own
# release-notes generator (the engine behind the "Generate release notes" button).
#
# Usage: gather-changelog.sh <NEW_VERSION> [PREV_TAG]
#   NEW_VERSION  tag to generate for, e.g. 9.0.0 (need not exist yet — then master HEAD is the target)
#   PREV_TAG     base of the comparison; defaults to the latest published release
#
# Output: the raw "## What's Changed" list + the "Full Changelog" compare link, ready to be
# transformed into house-style notes. This does NOT create a tag or a release.
set -euo pipefail

REPO="petrsvihlik/WopiHost"
NEW_VERSION="${1:?usage: gather-changelog.sh <NEW_VERSION> [PREV_TAG]}"
PREV_TAG="${2:-}"

if [[ -z "$PREV_TAG" ]]; then
  PREV_TAG="$(gh release view --repo "$REPO" --json tagName -q .tagName 2>/dev/null || true)"
  if [[ -z "$PREV_TAG" ]]; then
    echo "Could not determine the previous release tag; pass it explicitly." >&2
    exit 1
  fi
fi

echo "# Generating changelog: ${PREV_TAG} -> ${NEW_VERSION}" >&2

gh api -X POST "repos/${REPO}/releases/generate-notes" \
  -f tag_name="${NEW_VERSION}" \
  -f previous_tag_name="${PREV_TAG}" \
  -f target_commitish="master" \
  -q .body
