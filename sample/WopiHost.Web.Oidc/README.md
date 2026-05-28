# WopiHost.Web.Oidc — OIDC business-flow sample

A frontend host that signs users in via **any OpenID Connect provider** and feeds the resulting identity into the WOPI access token consumed by the backend WOPI server (`sample/WopiHost`).

This sample exists to demonstrate the [M365 business flow](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business) end-to-end without locking WopiHost into a specific identity provider. The WOPI core libraries (`src/WopiHost.*`) take no dependency on any IdP — sign-in is a sample concern.

## What's different from `WopiHost.Web`

| | `WopiHost.Web` | `WopiHost.Web.Oidc` |
|---|---|---|
| Authentication | None (anonymous) | Cookie + OpenID Connect |
| User identity in WOPI token | Hardcoded `"Anonymous"` | OIDC `sub` / `name` / `email` claims |
| File permissions | All-write | Resolved from OIDC role claims at mint time |
| Hostpage | Consumer flow | Business flow (`business_user=1`) |
| New endpoints | — | `/account/login`, `/account/logout`, `/account/denied` |

The WOPI access-token format is unchanged — both samples sign with the same shared HMAC key, and the backend WOPI server (`sample/WopiHost`) accepts tokens from either frontend.

## Run it

1. Start the WOPI backend (`sample/WopiHost`) — defaults to `http://localhost:5000`.
2. Configure your IdP (see provider sections below) and put the values into `appsettings.Development.json` or `dotnet user-secrets`.
3. `dotnet run --project sample/WopiHost.Web.Oidc`
4. Browse to `https://localhost:6101` — you should bounce to your IdP, sign in, return, and see the file list.

## Swap your IdP

Configuration lives in the `Oidc` section of `appsettings.json`. The shape is the same for every provider; only values change.

### Microsoft Entra (Azure AD)

```json
"Oidc": {
  "Authority": "https://login.microsoftonline.com/{tenantId}/v2.0",
  "ClientId": "<app registration client id>",
  "ClientSecret": "<client secret>",
  "Scopes": [ "openid", "profile", "email" ],
  "RoleClaimType": "roles"
}
```

App registration:
- Redirect URI (Web): `https://localhost:6101/signin-oidc`
- Front-channel logout URL: `https://localhost:6101/signout-callback-oidc`
- Add app roles `wopi.editor` and `wopi.viewer` (Expose an API → App roles), assign them to test users in Enterprise applications.

See [`appsettings.Entra.sample.json`](appsettings.Entra.sample.json).

### Auth0

```json
"Oidc": {
  "Authority": "https://{tenant}.auth0.com/",
  "ClientId": "<auth0 client id>",
  "ClientSecret": "<auth0 client secret>",
  "Scopes": [ "openid", "profile", "email" ],
  "RoleClaimType": "https://your-app.example.com/roles"
}
```

Auth0 namespaces custom claims; add an Action that reads the user's roles and writes them to a namespaced claim. Set `RoleClaimType` to that exact namespace.

See [`appsettings.Auth0.sample.json`](appsettings.Auth0.sample.json).

### Keycloak

```json
"Oidc": {
  "Authority": "https://{host}/realms/{realm}",
  "ClientId": "<keycloak client id>",
  "ClientSecret": "<keycloak client secret>",
  "Scopes": [ "openid", "profile", "email" ],
  "RoleClaimType": "roles"
}
```

Keycloak emits realm roles inside `realm_access.roles` by default. Add a token mapper of type "User Realm Role" with `Token Claim Name` = `roles` so the JWT carries a flat `roles` array.

See [`appsettings.Keycloak.sample.json`](appsettings.Keycloak.sample.json).

### Anything else

Any spec-compliant OIDC provider works. You need:
- An OIDC discovery document at `{Authority}/.well-known/openid-configuration`.
- A confidential or public+PKCE client.
- A claim that carries roles (configure its name via `RoleClaimType`).

## Permission model

`OidcRolePermissionMapper` ([`Infrastructure/OidcRolePermissionMapper.cs`](Infrastructure/OidcRolePermissionMapper.cs)) translates OIDC roles into `WopiFilePermissions` at token-mint time:

| Role claim value | WOPI permissions |
|---|---|
| `wopi.editor` | UserCanWrite, UserCanRename, UserCanAttend, UserCanPresent |
| `wopi.viewer` | UserCanAttend (read-only) |
| (none / unknown) | None |

This is intentionally simple. Real hosts replace this with a database/ACL lookup keyed off `sub` and the resource id — the seam is `Resolve(ClaimsPrincipal, string)`.

## Cookie + cross-origin caveat

The WOPI client (Office Online) calls the backend WOPI server (`sample/WopiHost`, port 5000) — **not** this frontend (port 6101). The OIDC cookie set by this frontend is **never** seen by the backend; the backend only sees the WOPI access token in the iframe URL. This is by design: the access token is the trust boundary between frontend and backend. Don't try to share cookies cross-origin.

## Limitations / what this sample does not do

- No refresh-token handling. Cookie expiry is set to 8 hours; users sign in again after that.
- No `Microsoft.Identity.Web` or any Microsoft-specific package. Pure OIDC by design.
- No real ACL — `OidcRolePermissionMapper` is a demonstration. Plug in your own.
