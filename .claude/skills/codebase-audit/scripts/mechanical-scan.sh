#!/usr/bin/env bash
# Mechanical smell scan for the codebase-audit skill.
#
# Prints file:line LEADS for the stable, deterministic smells — not findings. Every hit still has to
# be read in context and verified before it earns a place in the audit report (some are intentional;
# cross-check references/do-not-refile.md). Run from the repo root.
#
# Prefers ripgrep (rg); falls back to grep. Excludes build output and generated files.

set -u
cd "$(git rev-parse --show-toplevel 2>/dev/null || echo .)" || exit 1

# --- search helper: scan(<regex>) over hand-written C# (+ a couple of other globs per call) -------
if command -v rg >/dev/null 2>&1; then
  GLOBS=(-g '!**/artifacts/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/*.g.cs')
  scan() { rg -n --no-heading "${GLOBS[@]}" "$@" 2>/dev/null; }
else
  scan() { # last arg is the path(s); flags before it
    grep -rnE --include='*.cs' --exclude-dir={artifacts,bin,obj} "$@" 2>/dev/null | grep -v '\.g\.cs:'; }
fi

section() { printf '\n========== %s ==========\n' "$1"; }

section "TODO / HACK / FIXME (tech debt — should be tracked or removed)"
scan -t cs 'TODO|HACK|FIXME|XXX' src sample infra test 2>/dev/null || scan 'TODO|HACK|FIXME|XXX' src sample infra test

section "#pragma warning disable (each needs an adjacent rationale comment)"
scan -t cs 'pragma warning disable' src sample infra 2>/dev/null || scan 'pragma warning disable' src sample infra

section "Provider DI lifetime — plain AddScoped/AddSingleton (want TryAdd*; watch lifetime drift)"
scan -t cs 'services\.Add(Scoped|Singleton|Transient)<' src 2>/dev/null || scan 'services\.Add(Scoped|Singleton|Transient)<' src

section "IConfiguration as a constructor param in src/ (prefer IOptions<T>)"
scan -t cs 'IConfiguration[[:space:]]+[a-z]' src 2>/dev/null || scan 'IConfiguration[[:space:]]+[a-z]' src

section "Broad catch (Exception) (filter async-rude exceptions; log structured detail)"
scan -t cs 'catch \(Exception' src sample 2>/dev/null || scan 'catch \(Exception' src sample

section "Sync-over-async (.Result / .Wait() / GetAwaiter().GetResult())"
scan -t cs '\.Result\b|\.Wait\(\)|GetAwaiter\(\)\.GetResult' src sample infra 2>/dev/null || scan '\.Result\b|\.Wait\(\)|GetAwaiter\(\)\.GetResult' src sample infra

section "Throwing parse on input — Enum.Parse (prefer Enum.TryParse for untrusted input)"
scan -t cs 'Enum\.Parse' src sample 2>/dev/null || scan 'Enum\.Parse' src sample

section "Wildcard postMessage origin in sample host pages (security)"
if command -v rg >/dev/null 2>&1; then
  rg -n --no-heading -g '!**/bin/**' -g '!**/obj/**' "postMessage\([^)]*['\"]\*['\"]" sample 2>/dev/null
else
  grep -rnE "postMessage\([^)]*['\"]\*['\"]" sample --exclude-dir={bin,obj} 2>/dev/null
fi

section "Direct ILogger.LogX calls (prefer source-generated [LoggerMessage])"
scan -t cs '\.(LogInformation|LogWarning|LogError|LogDebug|LogTrace|LogCritical)\(' src 2>/dev/null || scan '\.(LogInformation|LogWarning|LogError|LogDebug|LogTrace|LogCritical)\(' src

section "Block-scoped namespaces (prefer file-scoped: 'namespace X;')"
scan -t cs '^namespace [A-Za-z0-9_.]+$' src sample infra test 2>/dev/null || scan '^namespace [A-Za-z0-9_.]+$' src sample infra test

section "Header constants with a trailing space before the closing quote (wire-format bug)"
scan -t cs '"[A-Za-z-]+ "' src 2>/dev/null || scan '"[A-Za-z-]+ "' src

printf '\n--- end of mechanical scan. These are LEADS; verify each in source. ---\n'
