# WopiHost.AppHost

The [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) orchestrator for local development. One
`dotnet run` here boots the whole topology — the WOPI backends, the sample frontends, and the real WOPI
client containers — and opens the Aspire dashboard.

```bash
dotnet run --project infra/WopiHost.AppHost
```

## What it starts

Each editor runs in its own **lane** — a dedicated WOPI backend + sample frontend — so the host can be
exercised against two independent real clients side by side. The resource names are suffixed by lane:

| Lane | Backend | Frontend | Client container |
|---|---|---|---|
| Collabora (default) | `wopihost-collabora` | `wopihost-web-collabora` | `collabora` (`collabora/code`) |
| ONLYOFFICE (default) | `wopihost-onlyoffice` | `wopihost-web-onlyoffice` | `onlyoffice` (`onlyoffice/documentserver`) |

Plus `wopihost-validator` (the WOPI protocol validator) and, when Redis locks are on, a `wopi-locks`
Redis container shared by both backends. (With Collabora disabled there's no in-Docker client, so the
default lane falls back to the plain `wopihost` / `wopihost-web` names.)

## Opting out of Docker clients

The orchestrated dev loop defaults to **both** Collabora and ONLYOFFICE, plus Redis — all Docker-backed.
That's the realistic multi-client setup, but the images are large (ONLYOFFICE alone is ~4.3 GB and bundles
its own Postgres/RabbitMQ). **If you don't want to pull or run an image, turn its flag off.** Each flag is
a code default, so any of the usual configuration sources overrides it — pick whichever is most convenient:

| To disable… | Set |
|---|---|
| ONLYOFFICE (skip the ~4.3 GB image) | `AppHost:UseOnlyOffice` → `false` |
| Collabora (skip the ~1 GB image) | `AppHost:UseCollabora` → `false` |
| Redis locks (use the in-memory provider) | `AppHost:UseRedisLocks` → `false` |

Turn **both** editor flags off to run with no Docker at all (the backends + frontends still start; the
frontends just have no live editor to embed).

Three ways to set them, in increasing precedence:

```bash
# 1. Local config file (not committed — see note below)
#    infra/WopiHost.AppHost/appsettings.Development.json
{ "AppHost": { "UseOnlyOffice": false } }

# 2. Environment variable (double underscore = config separator)
$env:AppHost__UseOnlyOffice = 'false'   # PowerShell
AppHost__UseOnlyOffice=false            # bash

# 3. Command line
dotnet run --project infra/WopiHost.AppHost -- --AppHost:UseOnlyOffice=false
```

> **Don't commit `appsettings.Development.json` with these flags flipped.** The defaults live in code
> precisely so a local override stays local — committing an opt-out file would impose your choice on
> everyone. (A `launchSettings.json` env var would also override the code default, but it sits *above*
> `appsettings.*` in precedence and is committed, so it's the wrong place for a personal preference.)

## All AppHost flags

| Flag | Default | Adds |
|---|---|---|
| `AppHost:UseCollabora` | `true` | `collabora/code` container + the Collabora lane. |
| `AppHost:UseOnlyOffice` | `true` | `onlyoffice/documentserver` container + the ONLYOFFICE lane. |
| `AppHost:OnlyOfficeProofValidation` | `false` | Runs the ONLYOFFICE lane's backend with WOPI proof validation on. ONLYOFFICE signs its callbacks and the validator accepts them (the nightly E2E ONLYOFFICE suite runs with this flag on); the dev-loop default just mirrors the Collabora lane's proof-off posture. |
| `AppHost:UseRedisLocks` | `true` | `wopi-locks` Redis container; both backends use the distributed lock provider against it. |
| `AppHost:UseAzureStorage` | `false` | Azurite emulator + `BlobStorage` connection string forwarded to the backends. |
| `AppHost:IncludeOidcSample` | `false` | The `WopiHost.Web.Oidc` frontend (requires IdP setup — see its README). |

## End-to-end tests

Each lane has a nightly E2E suite that boots this AppHost via `Aspire.Hosting.Testing` and drives the
editor with Playwright. Both live in [`test/WopiHost.E2ETests`](../../test/WopiHost.E2ETests) (one
project, suites selected by a `Client` trait):

- [`e2e-collabora.yml`](../../.github/workflows/e2e-collabora.yml) → `--filter "Client=Collabora"`
- [`e2e-onlyoffice.yml`](../../.github/workflows/e2e-onlyoffice.yml) → `--filter "Client=OnlyOffice"`

Neither gates per-PR CI (they're `[Trait("Category", "E2E")]`, filtered out by the repo-root
`.runsettings`); they run on a nightly cron + `workflow_dispatch`.
