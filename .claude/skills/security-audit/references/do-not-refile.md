# Do not re-file — verified non-issues & accepted risk (security)

Every entry here was investigated by a security run (or a focused fix) and **deliberately rejected**:
either it's not reachable, the framework already blocks it, or it's a documented accepted risk. A new
run treats these as out-of-bounds and must not re-file them — unless the surrounding code has
materially changed, in which case re-verify before resurfacing. Keep it current: when a run rules a
new candidate out, add it here with the reachability reason.

## Authentication / proof

- **WOPI proof validation uses RSA signature verification, not a secret-byte compare** — so
  constant-time comparison does not apply. `WopiProofValidator` calls `RSACryptoServiceProvider.
  VerifyData`; there is no `==`/`SequenceEqual` of a secret to make constant-time.
- **`RSACryptoServiceProvider` / `ImportCspBlob` / `ExportCspBlob` are required and safe.** The WOPI
  proof byte layout mandates the CSP-blob key format; these members are cross-platform in modern .NET
  (production `WopiProofValidator` and the existing proof tests use them ungated under warnings-as-
  errors). Not a CA1416 or platform-security issue.
- **`catch (Exception)` in `WopiProofValidator.ValidateProofAsync` is correct fail-closed.** It's
  filtered `when (!IsCriticalUnwindException(ex))` (OOM/SO/ThreadAbort rethrow) and returns `false`
  (deny) on everything else — exactly what a security gate should do.
- **The loose resource-id ↔ token binding in `WopiAuthorizationHandler` is intentional.** A single
  WOPI token legitimately navigates from a file to its ancestor container / siblings (the protocol
  and the MS validator rely on it); a strict per-resource bind would break it. The id is logged on
  mismatch for audit. The *permission* gate still fires — that's the real control.

## Authorization / tokens

- **Lock ids and resource ids are not secrets.** Comparing them with `==`/`Ordinal` is correct; they
  are opaque client-chosen identifiers, not credentials — no timing side-channel.
- **`Guid.NewGuid()` for non-security values is fine.** The fallback file/container-name stems
  (`ContainerMutatingEndpoints`) and per-test container names are not tokens/nonces; only flag `Guid`/
  `System.Random` when the value must be unguessable. The dev signing key already uses
  `RandomNumberGenerator.GetBytes`.

## Input validation / storage

- **FS provider path traversal is guarded.** `CreateWopiChildFile`/`Rename*` reject non-single-
  segment names via `CheckValidFileName`/`IsValidSingleSegmentName` (`.`/`..`/separators/invalid
  chars), and the endpoint layer validates the name before the provider. No re-rooting gap.
- **Azure provider id scheme hashes the path as-is (case-sensitive).** This is correct (Azure blob
  names are case-sensitive); the FS provider case-folds because Win/macOS filesystems are case-
  insensitive. The divergence is intentional, not an injection/confusion bug.

## XML / transport

- **Discovery XML parsing is not XXE-exposed.** `XElement.Load`/`Parse` default to
  `DtdProcessing.Prohibit` with no external `XmlResolver` on modern .NET, so DTD/external-entity
  expansion is already disabled; the discovery doc is a host-trusted client. Only a finding if an
  `XmlReaderSettings`/`XmlResolver` is explicitly configured to re-enable it.
- **Proof-key is read from the discovery-document root, net-zone-independent — intentional.** Real
  OOS/M365 docs put `<proof-key>` as a root sibling of `<net-zone>`; scoping it under the selected
  zone would break proof validation. Pinned by a test; do not "fix" the root lookup.

## Config & secrets

- **`Wopi:Security:DisableProofValidation` is a dev-only flag and is guarded.** It swaps in a no-op
  proof validator for clients (Collabora) that can't sign, and **throws on startup outside
  Development** (`sample/WopiHost/Program.cs`). Correct design — only a finding if that guard is
  removed.
- **The sample frontends' anonymous access is an accepted, documented model.** They're samples; the
  security invariant that *does* apply is action-scoped token minting (a view link must not mint
  write) — that is enforced and tested, not waived.

## Accepted-risk (documented design trade-offs)

- **`MemoryLockProvider` is single-process** — no cross-instance exclusion by design; use the Azure/
  Redis providers for distributed deployments. Not a security finding.
- **The file-system provider is "for sample purposes"** and carries a benign local-disk TOCTOU
  (`File.Exists` → mutate). Documented; the Azure provider uses conditional ETag/lease writes.

## Verified this run (added 2026-06-15)

- **`PutUserInfo` carries no `RequireWopiPermission` — and that's fine.** Every endpoint under the
  `/wopi` group inherits the group-level `.RequireAuthorization()` (`WopiEndpointRouteBuilderExtensions`),
  so PutUserInfo still requires an authenticated token (anonymous → 401). PUT_USER_INFO is not
  permission-gated in the WOPI spec, it writes only the caller's own info to MemoryCache (capped 1024
  bytes), and `Permission.Read` is implied for any valid token anyway — so a per-endpoint permission
  would be cosmetic, not a control. Not an authz gap.
- **The Validator's `IssueValidatorToken` mints a full file+container token deliberately.** It's the
  Microsoft-WOPI-validator conformance harness (the `/_test` token route), which uses one token to
  exercise every operation; there is no view-vs-edit action to scope by. Distinct from the Validator's
  user-facing `HostPage` (which has a `WopiAction` and was scoped this run). Not a token-over-scoping
  finding.
- **`wopi-postmessage.js` validates the inbound origin.** `onMessage` short-circuits on
  `e.origin !== clientOrigin`, and `send` posts to the configured `clientOrigin` (never `'*'`). Both
  directions of the postMessage channel are origin-checked. Safe.
