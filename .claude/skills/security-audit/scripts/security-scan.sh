#!/usr/bin/env bash
# Mechanical SECURITY scan for the security-audit skill.
#
# Prints file:line LEADS for the stable, security-relevant patterns — NOT findings. Every hit must
# be read in context AND reachability-traced (attacker input → sink) before it earns a place in the
# report. Many are false positives here; cross-check references/do-not-refile.md and the threat
# model's "noisy leads". Run from the repo root. Prefers ripgrep (rg); falls back to grep.

set -u
cd "$(git rev-parse --show-toplevel 2>/dev/null || echo .)" || exit 1

if command -v rg >/dev/null 2>&1; then
  GLOBS=(-g '!**/artifacts/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/*.g.cs')
  scan() { rg -n --no-heading "${GLOBS[@]}" "$@" 2>/dev/null; }
else
  scan() { grep -rnE --include='*.cs' --exclude-dir={artifacts,bin,obj} "$@" 2>/dev/null | grep -v '\.g\.cs:'; }
fi
section() { printf '\n========== %s ==========\n' "$1"; }

section "JWT validation disabled (issuer/audience/lifetime/signature) — must all validate"
scan -t cs 'Validate(Issuer|Audience|Lifetime|IssuerSigningKey)[[:space:]]*=[[:space:]]*false|RequireSignedTokens[[:space:]]*=[[:space:]]*false|RequireExpirationTime[[:space:]]*=[[:space:]]*false|SignatureValidator[[:space:]]*=' src sample

section "Possible non-constant-time compare near a SECRET (proof/token/hmac/signature/key) — NOT lock/resource ids"
scan -t cs -i '(proof|token|hmac|signature|secret|signing).{0,40}(==|!=|\.Equals\(|SequenceEqual)|(==|!=|\.Equals\(|SequenceEqual).{0,40}(proof|token|hmac|signature|secret|signing)' src

section "Weak RNG for security values (System.Random / Guid for token/nonce/key) — verify it's a security value"
scan -t cs 'new Random\(|Guid\.NewGuid\(\)|Random\.Shared' src sample

section "Secrets possibly written to logs/exceptions (token/proof/key/Authorization near Log*/Exception)"
scan -t cs -i '(Log[A-Za-z]*|Exception)\(.{0,60}(token|proof|signingkey|authorization|x-wopi-proof)|(token|proof|signingkey|authorization)\b.{0,40}(Log[A-Za-z]*\()' src sample

section "XML parsing — XXE/DTD only if a resolver/DtdProcessing is explicitly enabled"
scan -t cs 'DtdProcessing|XmlResolver|XmlUrlResolver|XmlReaderSettings' src sample

section "Wildcard postMessage target origin (token leak) — host pages"
if command -v rg >/dev/null 2>&1; then
  rg -n --no-heading -g '!**/bin/**' -g '!**/obj/**' "postMessage\([^)]*['\"]\*['\"]" sample 2>/dev/null
else
  grep -rnE "postMessage\([^)]*['\"]\*['\"]" sample --exclude-dir={bin,obj} 2>/dev/null
fi

section "Inbound postMessage without an origin check (grep handlers; verify event.origin is validated)"
if command -v rg >/dev/null 2>&1; then
  rg -n --no-heading -g '!**/bin/**' -g '!**/obj/**' "addEventListener\(\s*['\"]message|onmessage" sample 2>/dev/null
else
  grep -rnE "addEventListener\(\s*['\"]message|onmessage" sample --exclude-dir={bin,obj} 2>/dev/null
fi

section "Token mint sites — confirm non-Edit actions strip UserCanWrite/UserCanRename (view-token scoping)"
scan -t cs -i 'FilePermissions[[:space:]]*=|GetFilePermissionsAsync|UserCanWrite|UserCanRename|Mint.*Token|IssueAsync' sample

section "Mutating endpoints — every one needs RequireWopiPermission (grep both to cross-check for a missing gate)"
scan -t cs 'MapPost|MapPut|MapDelete|RequireWopiPermission' src/WopiHost.Core/Endpoints

section "Path composition from a (possibly client-controlled) name/target — confirm a single-segment guard precedes it"
scan -t cs 'Path\.Combine\(|GetBlobClient\(|\+[[:space:]]*"/"[[:space:]]*\+[[:space:]]*name|parentPath[[:space:]]*\+' src

section "Dev-only proof-validation disable — confirm it throws outside Development"
scan -t cs -i 'DisableProofValidation|IsDevelopment|NoopProofValidator|disable.*proof' src sample

section "Stack-trace / detailed-error exposure — must be gated behind IsDevelopment()"
scan -t cs -i 'UseDeveloperExceptionPage|\.StackTrace|ex\.ToString\(\)|DeveloperExceptionPage' src sample

section "Dependency CVE suppressions / pins (verify the pin is to a patched version)"
if command -v rg >/dev/null 2>&1; then
  rg -n --no-heading 'NU1903|NU1901|NU1902|VulnerablePackage' Directory.Packages.props Directory.Build.props 2>/dev/null
else
  grep -rnE 'NU1903|NU1901|NU1902|VulnerablePackage' Directory.Packages.props Directory.Build.props 2>/dev/null
fi

printf '\n--- end of security scan. These are LEADS; trace attacker input -> sink and apply the bar. ---\n'
