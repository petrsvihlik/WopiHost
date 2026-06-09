# WopiHost.E2ETests.OnlyOffice

End-to-end test that exercises the full WopiHost ↔ ONLYOFFICE editing loop. Sibling of
[`WopiHost.E2ETests.Collabora`](../WopiHost.E2ETests.Collabora) — same harness pattern, a second real
WOPI client.

## What's tested

| Test | Asserts |
|---|---|
| `OpensDocxInOnlyOffice_LoadsDocument` | The frontend's Edit link mints a WOPI URL + access token; ONLYOFFICE's editor loads in the iframe; the CheckFileInfo + GetFile handshake completes — proven by reading ONLYOFFICE's own `window.Asc.editor.isDocumentLoadComplete` flag, which only flips true after the document is fetched via WOPI and rendered. |

## What's NOT tested

- **Save round-trip (PutFile).** The Collabora suite covers `Action_Save`; ONLYOFFICE saves on its own
  autosave/forcesave cadence rather than a deterministic host-triggered call, so a save assertion would
  be flaky. The open/load test already proves the read side of the handshake end-to-end.
- **Proof validation.** The ONLYOFFICE lane runs with proof validation off
  (`AppHost:OnlyOfficeProofValidation=false`) because ONLYOFFICE's signed callbacks are currently
  rejected by `WopiProofValidator` — tracked in [#545](https://github.com/petrsvihlik/WopiHost/issues/545).
- **Office Online Server / M365 for the Web.** ONLYOFFICE Docs is a *development substitute* with a
  different feature surface. A green run is not M365 conformance.

## How the slnx / runsettings split works

This project IS in `WOPI.slnx` so PR builds compile it (build-error coverage), but it's filtered out of
`dotnet test` execution by default. The filter lives in [`/.runsettings`](../../.runsettings):

```xml
<TestCaseFilter>Category!=E2E</TestCaseFilter>
```

…auto-loaded by [`Directory.Build.props`](../../Directory.Build.props) via `<RunSettingsFilePath>`. The
test carries `[Trait("Category", "E2E")]`, so `dotnet test` and `dotnet test WOPI.slnx` skip it. The
dedicated [`e2e-onlyoffice.yml`](../../.github/workflows/e2e-onlyoffice.yml) workflow opts back in by
passing `-p:RunSettingsFilePath=` to bypass the autoloaded runsettings entirely. (CLI `--filter` would
seem like the obvious answer, but `dotnet test` ANDs the CLI filter with the runsettings filter —
`(Category!=E2E)&(Category=E2E)` matches zero tests.)

## Where this runs

- **Nightly**: [`.github/workflows/e2e-onlyoffice.yml`](../../.github/workflows/e2e-onlyoffice.yml) at
  03:37 UTC (offset from Collabora's 03:17 so the two heavy container jobs don't contend).
- **On-demand**: `workflow_dispatch` from the Actions tab. Use after touching
  `infra/WopiHost.AppHost/`, `sample/WopiHost.Web/`, anything in `src/WopiHost.Core/`, or after an
  ONLYOFFICE image bump.
- **Never per-PR.** By design — the ONLYOFFICE image is ~4.3 GB and bundles its own Postgres/RabbitMQ;
  gating PRs on it would burn CI minutes for an integration that fails mostly for upstream reasons.

## Running locally

Requires Docker (engine running, Linux containers, internet for the first image pull).

```bash
# 1. Build (also drops the playwright.ps1 helper next to the test assembly)
dotnet build test/WopiHost.E2ETests.OnlyOffice

# 2. Install Chromium for Playwright (one-off; idempotent)
pwsh artifacts/bin/WopiHost.E2ETests.OnlyOffice/debug/playwright.ps1 install chromium

# 3. (Optional, speeds up the first run) Pre-pull ONLYOFFICE
docker pull onlyoffice/documentserver

# 4. Run — -p:RunSettingsFilePath= clears the repo-root .runsettings autoload. Without it,
#    dotnet test inherits the Category!=E2E filter and runs zero tests in this project.
dotnet test test/WopiHost.E2ETests.OnlyOffice -p:RunSettingsFilePath=
```

Without Docker, the test logs `Docker is not available — skipping ONLYOFFICE e2e` and passes-as-skipped.

## How the fixture is wired

| Piece | What it does |
|---|---|
| [`OnlyOfficeAppFixture`](Fixtures/OnlyOfficeAppFixture.cs) | xUnit collection fixture. Boots the AppHost via `DistributedApplicationTestingBuilder<Projects.WopiHost_AppHost>`, sets `AppHost:UseOnlyOffice=true` + `AppHost:UseCollabora=false` + `AppHost:UseRedisLocks=false`, waits for `onlyoffice` / `wopihost-onlyoffice` / `wopihost-web-onlyoffice` to be healthy (gating on ONLYOFFICE's `/healthcheck` returning `true`, not just `/hosting/discovery`, so the document engine is actually ready), and exposes the frontend HTTPS URL. |
| [`PlaywrightFixture`](Fixtures/PlaywrightFixture.cs) | Owns one `IPlaywright` + one Chromium `IBrowser` for the whole collection. Each test gets a fresh `IBrowserContext`. |
| [`DockerCheck`](Fixtures/DockerCheck.cs) | Probes `docker info`. The test `Assert.SkipUnless(...)` when the engine is unreachable. |
| [`OnlyOfficeFixtureCollection`](OnlyOfficeFixtureCollection.cs) | Pairs the two fixtures with `DisableParallelization = true`. |

## Why a separate project (not in `WopiHost.E2ETests.Collabora`)

Each client needs its own AppHost config (`UseOnlyOffice` vs `UseCollabora`) and its own load-signal
logic. Keeping them in sibling projects mirrors how the AppHost models each client as its own lane, and
lets the two heavy suites run as independent nightly workflows.

## Triaging a failing run

| Symptom | First-line hypothesis |
|---|---|
| Healthcheck timeout on `onlyoffice` | Image-pull slowness / engine warmup. ONLYOFFICE's first boot is slow (Postgres init). Re-run; if persistent, check image-tag drift. |
| Iframe never appears | The Detail form failed to submit, or the frontend returned a 4xx. Inspect the Playwright trace artifact. |
| `isDocumentLoadComplete` never true | ONLYOFFICE loaded the shell but couldn't fetch the document. Almost always CheckFileInfo / GetFile returning non-200, or proof validation refusing the callback. The failure dumps the editor state (`openResult`, dialogs) + the ONLYOFFICE container's docservice logs. |
| Passes locally, fails in CI | The image's `latest` tag changed between local pull and CI pull. Pin the tag in [`infra/WopiHost.AppHost/Program.cs`](../../infra/WopiHost.AppHost/Program.cs) and re-evaluate. |
