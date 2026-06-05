#!/usr/bin/env bash
# Fetches the GitHub wiki (a separate git repo) so the documentation-accuracy dimension can verify
# its pages against the code. Prints the local path on the last line. Run from the repo root.
#
# The wiki lives at <origin>.wiki.git. This derives that from the repo's origin remote so it works
# for forks too.

set -u
cd "$(git rev-parse --show-toplevel 2>/dev/null || echo .)" || exit 1

origin="$(git remote get-url origin 2>/dev/null)"
# Turn git@github.com:owner/repo.git or https://github.com/owner/repo(.git) into the .wiki.git URL.
wiki_url="$(printf '%s' "$origin" | sed -E 's#\.git$##; s#$#.wiki.git#')"

dest="${TMPDIR:-/tmp}/wopihost-wiki"
rm -rf "$dest"
if git clone --depth 1 "$wiki_url" "$dest" >/dev/null 2>&1; then
  echo "Wiki pages:" >&2
  ls -1 "$dest"/*.md 2>/dev/null | xargs -n1 basename >&2
  echo "$dest"
else
  echo "Could not clone wiki from $wiki_url (no wiki, or no access)." >&2
  exit 1
fi
