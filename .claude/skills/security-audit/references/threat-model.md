# WopiHost threat model & security checklist

The boundaries each security run walks, the attacker model, and the concrete per-boundary checks —
drawn from this repo's real security-relevant code so a run knows where the sharp edges are. Every
candidate still needs source verification, a `file:line` anchor, and a traced attack path.

## Attacker model

A WopiHost deployment sits between three actors, each a distinct trust level:

- **The WOPI client** (Office for the web / M365, Collabora, ONLYOFFICE) — semi-trusted. It is
  authenticated by the **access token** it presents and (for mutation) by the **X-WOPI-Proof**
  signature. A malicious or compromised client, or an attacker who can replay/forge a request, is in
  scope: the host must not let a token do more than it was scoped for, must verify proof signatures,
  and must treat every client-supplied id / target name / header as untrusted input.
- **The browser user** of the sample frontends — semi-trusted. Drives the host pages; receives an
  access token in a URL; exchanges `postMessage` with the embedded client iframe. In scope: token
  over-scoping (a view link minting write), `postMessage` to/from the wrong origin, token leakage.
- **The host operator / config** — trusted, but fallible. In scope: a dev-only escape hatch reaching
  production, an insecure default, a secret committed or logged.

Out of scope (documented accepted risk — see the ledger): the sample frontends' anonymous access
model, the single-process MemoryLockProvider's lack of cross-instance exclusion, and the FS
provider's "sample purposes only" local-disk TOCTOU.

## Boundary 1 — WOPI client ↔ host: token & proof

The load-bearing authentication boundary. Two independent mechanisms.

- **Access-token validation (JWT).** `TokenValidationParameters` must validate signature + lifetime,
  and issuer/audience when configured. A disabled check (`ValidateIssuer=false`,
  `ValidateLifetime=false`, `RequireSignedTokens=false`, `SignatureValidator` that returns without
  verifying) is a finding. The ephemeral dev signing key must come from `RandomNumberGenerator`, not
  `Guid`/`Random`. Verify `JwtAccessTokenService` and `AccessTokenHandler`.
- **Proof validation (X-WOPI-Proof).** On mutation the host verifies the client's RSA signature over
  the spec's byte layout (access token + URL + timestamp) using the **discovery** proof keys
  (current + old, for rotation). Checks: the signature is actually *verified* (RSA `VerifyData`, not
  skipped); both current and old keys are tried; the timestamp is range-checked against replay; the
  failure path **fails closed** (returns false / 401) and filters only async-rude exceptions
  (OOM/SO/ThreadAbort rethrow). This is **not** a secret-byte compare — constant-time does not apply.
  Verify `WopiProofValidator`.
- **Authorization — permission scoping.** Every mutating endpoint carries
  `RequireWopiPermission(resourceType, Permission.*)`; the handler maps the token's `wopi:fperms` /
  `wopi:cperms` flags to the required permission and **fails closed** (an authenticated-but-
  unauthorized token → 403, not 401). Checks: a `ReadOnly` token is denied every edit permission; no
  endpoint that mutates is missing its `RequireWopiPermission`; `Read` is the only implied grant.
- **Token-trading prevention.** A freshly minted child / relative / ancestor resource gets a token
  **bound to the new resource id** (via `IWopiResourceTokenMinter`), never the inbound token — so a
  client can't trade a token for one scoped to a resource it shouldn't reach.
- **Technique that pays off:** the resource-binding is *intentionally* loose (one token navigates
  file→ancestors), documented in `WopiAuthorizationHandler` — don't file that as a missing check; do
  confirm the *permission* gate still fires.

## Boundary 2 — browser ↔ frontend: origin, postMessage, token-in-URL

- **`postMessage` target origin.** The host page must post to the **configured client origin**, never
  `'*'`, and must **verify `event.origin`** on inbound messages. A wildcard target leaks the access
  token to any origin that ends up framed; an unchecked inbound origin lets any page drive the host.
  Verify the extracted postMessage module in `sample/WopiHost.Web` and any host page.
- **Token scoping by action (the invisible one).** A **view** link must not mint a token carrying
  `UserCanWrite`/`UserCanRename`. Collabora derives view-vs-edit from CheckFileInfo permission flags
  off a single URL, so an over-permissive token silently opens edit mode — invisible against OOS/M365
  (which ship distinct view/edit URLs). Check **every** token-mint site in all three sample frontends
  (`WopiHost.Web`, `WopiHost.Web.Oidc`, `WopiHost.Validator`): non-edit actions strip write+rename,
  keep interaction-only flags (Attend/Present). This is a real, recurring finding here.
- **Info disclosure.** Developer exception pages / stack traces gated behind `IsDevelopment()`; no
  token or proof header echoed into HTML or logs.

## Boundary 3 — host ↔ storage provider: path traversal & reserved names

The highest-impact class for the FS provider; a flat-namespace variant for Azure.

- **FS path traversal (potential arbitrary read/write).** A client-controlled relative/suggested
  target or id that resolves outside the configured root (`..`, absolute path, UNC, alternate data
  stream). Confirm the provider rejects non-single-segment names (`CheckValidFileName` /
  `IsValidSingleSegmentName`) **and/or** canonicalises + re-roots (`Path.GetFullPath` +
  `StartsWith(root)`) before any `File.*`/`Directory.*`. The endpoint layer also validates the name
  *before* the provider — so trace both layers and state which one blocks it.
- **Azure flat-namespace variant (lower impact, real gap).** Azure blob names are flat: `..` is a
  literal, so there's no container escape — but an unguarded `name` still lets a client create a blob
  at an unintended nested path or collide with the reserved folder marker (`.wopi.folder`). The guard
  is the same `CheckValidFileName`/`CheckValidContainerName`. **Sibling-diff technique:** a guard the
  FS provider applies and Azure omits (or that one Azure method applies and its twin doesn't) is a
  likely finding — this is exactly how the Azure `CreateWopiChild*` backstop gap surfaced.
- **Reachability note:** because the endpoint layer sanitises first, a provider-level miss is usually
  a *defense-in-depth / library-consumer* gap (Medium), not direct RCE — say so explicitly.

## Boundary 4 — host ↔ lock provider: atomicity & DoS

- **Lock atomicity (correctness/integrity).** RefreshLock / UnlockAndRelock must be a true
  compare-and-swap **inside** the provider (ConcurrentDictionary.TryUpdate / ETag- or lease-
  conditional write / Redis WATCH-MULTI-EXEC), not a check-then-act in the endpoint — a race lets one
  client steal/extend another's lock. Verify all three providers.
- **Lock ids are NOT secrets.** They're opaque client-chosen identifiers — `==`/`Ordinal` comparison
  is correct, not a timing channel. Do not file constant-time-compare on lock ids.
- **DoS / exhaustion.** Locks auto-expire (spec 30 min) so a crashed client can't wedge a file
  forever; expiry is enforced on read. A `MaxFileSize` bounds PutFile. Note (don't file) that the
  in-memory provider is single-process by design.

## Boundary 5 — host ↔ discovery: XML & transport

- **XXE / DTD.** `XElement.Load`/`Parse` on modern .NET default to `DtdProcessing.Prohibit` with no
  external resolver, so external-entity expansion is already off — **only** a finding if an
  `XmlReaderSettings`/`XmlResolver` is explicitly set to re-enable DTD/entity resolution. The
  discovery endpoint is also a host-trusted client.
- **Transport.** Discovery and client URLs should be HTTPS in production; the discovery cache honours
  the configured NetZone. The proof-key element is read from the document root (net-zone-independent)
  — pin that invariant in tests, don't "fix" it.

## Boundary 6 — config & secrets

- **Dev-only escape hatches refused in production.** `Wopi:Security:DisableProofValidation` swaps in a
  no-op proof validator and **must throw on startup outside Development**. Confirm the guard exists;
  its presence is correct design, not a hole.
- **Secrets never logged or thrown.** Access tokens, signing/proof keys, `Authorization` /
  `X-WOPI-Proof` header values, file contents — never passed to `ILogger` or an exception message.
  Lock ids and resource ids are not secrets (fine to log).
- **Weak randomness only for security values.** `RandomNumberGenerator` for tokens/nonces/keys;
  `Guid.NewGuid()` is fine for a non-security value (a fallback file-name stem, a test container).
- **Secure defaults.** `DefaultFilePermissions`/`DefaultContainerPermissions` and any
  `Validate*=false` default scrutinised; a dependency CVE (`NU1903`/`NU1901`) is a supply-chain
  finding.

## Noisy leads — interpret, don't auto-file

The mechanical scan casts wide; these are mostly false positives here (mirror of `codebase-audit`'s
list, security-framed):

- `==`/`SequenceEqual`/`string.Equals` **on a lock id or resource id** — not secrets, not a timing
  channel.
- `catch (Exception)` in `WopiProofValidator` / telemetry filter — already filtered with a `when
  (!IsCritical…)` and fails closed; read the filter before filing.
- `Guid.NewGuid()` — only a finding if it mints something that must be unguessable; the name-stem and
  per-test-container uses are fine.
- `RSACryptoServiceProvider` / `ImportCspBlob` — required for the WOPI proof byte layout; cross-
  platform in modern .NET; not a CA1416/security issue.
- `DisableProofValidation` hits — the flag is dev-only *and guarded*; only a finding if the
  throw-outside-Development guard is gone.
- `XElement.Load`/`Parse` — XXE is off by platform default; not a finding absent an explicit resolver.

## Techniques that pay off here

- **Sibling-provider guard diff.** Diff the FS vs Azure storage providers (and a provider's own
  create vs rename methods) for a validation guard one applies and another omits — the Azure
  `CreateWopiChild*` path-traversal backstop gap was found this way.
- **Trace endpoint → provider for every client-supplied name/id.** The endpoint layer validates a lot
  up front; a provider "miss" is often already blocked there. Decide direct-exploit vs defense-in-
  depth and state which.
- **Walk each WOPI mutation's auth metadata.** Grep `RequireWopiPermission` and confirm every
  mutating route carries one and maps to the right `Permission.*` — a missing one is an authz bypass.
- **Token-mint sweep across the three frontends.** Grep every place a token is minted; confirm
  non-edit actions strip write/rename. Recurring real finding.
