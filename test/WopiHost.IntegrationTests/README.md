# WopiHost.IntegrationTests

End-to-end tests that exercise [`WopiHost.Web.Oidc`](../../sample/WopiHost.Web.Oidc/README.md) and the WOPI backend (`sample/WopiHost`) together, including a real OIDC server when Docker is available.

## What's covered

| Test class | What it verifies | Docker required |
|---|---|---|
| `OidcRolePermissionMapperTests` | OIDC role → `WopiFilePermissions` mapping across claim shapes | No |
| `WopiAccessTokenMinterTests` | WOPI access-token format, claims, signing-key round-trip | No |
| `OidcStartupTests` | Anonymous request redirects to the configured authority; discovery document is reachable; health endpoint is anonymous | **Yes** |
| `WopiTokenRoundTripTests` | Authenticated user → minted token contains OIDC identity → backend `CheckFileInfo` reflects that identity | No (uses `TestAuthHandler`) |

## What's not covered (and why)

- **Real Entra/Auth0/Okta tenants.** Only the OIDC handshake against a Navikt `mock-oauth2-server` is automated. The sample's [README](../../sample/WopiHost.Web.Oidc/README.md) walks through pointing the same code at a real IdP — that is a manual smoke test, not an automated one.
- **MSAL.js / MSAL.NET client flows.** The sample is pure server-side OIDC; no Microsoft-Identity-Web dependency.
- **Conditional Access, guest users, B2B.** Real-tenant only.

## Running

```bash
# Full suite (requires Docker for the OIDC startup tests)
dotnet test test/WopiHost.IntegrationTests

# Skip Docker-backed tests (e.g. on a machine without Docker Desktop / Colima)
dotnet test test/WopiHost.IntegrationTests --filter "FullyQualifiedName!~OidcStartupTests"
```

The Docker-backed tests use [`Xunit.SkippableFact`](https://github.com/AArnott/Xunit.SkippableFact) to mark themselves as **skipped** (not failed) when Docker is unavailable, so the build stays green.

## How the test factories fit together

```
┌─────────────────────────────┐         ┌──────────────────────────┐
│ MockOidcServerFixture       │◀────────│ OidcWebAppFactory        │
│ (Testcontainers, Docker)    │ OIDC    │ (real OIDC handshake)    │
│ ghcr.io/navikt/             │ flow    │ Used by OidcStartupTests │
│ mock-oauth2-server          │         └──────────────────────────┘
└─────────────────────────────┘
                                          ┌──────────────────────────┐
                                          │ TestAuthOidcWebAppFactory│
                                          │ (TestAuthHandler stand-in│
                                          │  for OIDC; no Docker)    │
                                          │ Used by WopiTokenRoundTrip│
                                          └─────────────┬────────────┘
                                                        │
                                                        │ minted WOPI
                                                        │ access token
                                                        ▼
                                          ┌──────────────────────────┐
                                          │ WopiBackendFactory       │
                                          │ (sample/WopiHost; proof  │
                                          │  validation stubbed)     │
                                          └──────────────────────────┘
```

Both factories wire the same WOPI signing secret, so a token minted by the frontend factory validates on the backend factory.
