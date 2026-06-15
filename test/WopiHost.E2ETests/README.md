# WopiHost.E2ETests

End-to-end tests that exercise the full WopiHost ↔ editor loop against the real WOPI clients the
AppHost orchestrates. One project, one suite per client — shared scaffolding, selected by trait.
Tracked under [#357 Approach B](https://github.com/petrsvihlik/WopiHost/issues/357#approach-b--end-to-end-edit-flow-through-collabora-nightly-not-per-pr).

## Suites

| Suite | Trait | Tests | Asserts |
|---|---|---|---|
| [`Collabora/`](Collabora) | `Client=Collabora` | `OpensDocxInCollabora_RendersDocumentArea` | The frontend's Edit link mints a WOPI URL + access token; Collabora's iframe loads; the CheckFileInfo + GetFile handshake completes (proven by Collabora's `Action_Load_Resp`/`Document_Loaded` postMessages). |
| | | `SaveAfterEdit_WritesNewBytesToDisk` | Typing into Collabora and triggering `Action_Save` via the host-postMessage API causes Collabora to call PutFile, and the WopiHost backend writes the new bytes to `sample/wopi-docs/test.docx`. |
| [`OnlyOffice/`](OnlyOffice) | `Client=OnlyOffice` | `OpensDocxInOnlyOffice_LoadsDocument` | Same handshake proof for ONLYOFFICE, read from the editor's own `window.Asc.editor.isDocumentLoadComplete` flag (ONLYOFFICE has no Collabora-style postMessage protocol). Runs with **real proof validation** (`AppHost:OnlyOfficeProofValidation=true`), so it also verifies that `WopiProofValidator` accepts ONLYOFFICE's signed callbacks. |

Every test also carries `Category=E2E`, which is what keeps the whole project out of per-PR runs
(see below).

## What's NOT tested

- **ONLYOFFICE save round-trip (PutFile).** ONLYOFFICE saves on its own autosave/forcesave cadence
  rather than a deterministic host-triggered call, so a save assertion would be flaky. The Collabora
  suite covers the PutFile path.
- **Proof validation against Collabora.** Collabora does not sign WOPI callbacks with proof keys
  (an OOS / M365 feature it never implemented), so its lane necessarily runs proof-off. The
  ONLYOFFICE suite is the one that exercises the real `WopiProofValidator` path.
- **Multi-user / co-authoring** — the Cobalt path is its own scoping problem ([#321](https://github.com/petrsvihlik/WopiHost/issues/321)).
- **Visual regression / pixel diffs** — rot fast, low signal.
- **WOPI protocol conformance** — that's what [`microsoft/wopi-validator-core`](https://github.com/microsoft/wopi-validator-core) is for.
- **Office Online Server / M365 for the Web** — CODE and ONLYOFFICE are *development substitutes*
  with different feature surfaces. A green run here is not M365 conformance.

## How the slnx / runsettings / filter split works

This project IS in `WOPI.slnx` so PR builds compile it (build-error coverage), but it's filtered out
of `dotnet test` execution by default. The filter lives in [`/.runsettings`](../../.runsettings):

```xml
<TestCaseFilter>Category!=E2E</TestCaseFilter>
```

…auto-loaded by [`Directory.Build.props`](../../Directory.Build.props) via `<RunSettingsFilePath>`
(dotnet test does NOT auto-discover runsettings by convention). All tests here carry
`[Trait("Category", "E2E")]`, so `dotnet test` and `dotnet test WOPI.slnx` skip them.

The dedicated workflows opt back in by passing `-p:RunSettingsFilePath=` to bypass the autoloaded
runsettings entirely, then select their suite with a **`Client` trait filter**:

```bash
dotnet test test/WopiHost.E2ETests -p:RunSettingsFilePath= --filter "Client=Collabora"
dotnet test test/WopiHost.E2ETests -p:RunSettingsFilePath= --filter "Client=OnlyOffice"
```

The `Client` trait is deliberately distinct from `Category`: a CLI `--filter` ANDs with the
runsettings filter (`(Category!=E2E)&(Category=E2E)` matches zero tests), so the runsettings must be
cleared first — and once it is, the `Client` filter applies alone.

## Where this runs

- **Nightly**: [`e2e-collabora.yml`](../../.github/workflows/e2e-collabora.yml) at 03:17 UTC and
  [`e2e-onlyoffice.yml`](../../.github/workflows/e2e-onlyoffice.yml) at 03:37 UTC (offset so the two
  heavy container jobs don't contend). Both run this project with their suite's `Client` filter.
- **On-demand**: `workflow_dispatch` from the Actions tab. Use after touching
  `infra/WopiHost.AppHost/`, `sample/WopiHost.Web/`, anything in `src/WopiHost.Core/`, or after a
  client image bump.
- **Never per-PR.** By design — Docker + container cold-start + an integration that fails mostly for
  upstream reasons.

## Running locally

Requires Docker (engine running, Linux containers, internet for the first image pull).

```bash
# 1. Build (also drops the playwright.ps1 helper next to the test assembly)
dotnet build test/WopiHost.E2ETests

# 2. Install Chromium for Playwright (one-off; idempotent)
pwsh artifacts/bin/WopiHost.E2ETests/debug/playwright.ps1 install chromium

# 3. (Optional, speeds up the first run) Pre-pull the client image(s)
docker pull collabora/code
docker pull onlyoffice/documentserver

# 4. Run one suite — -p:RunSettingsFilePath= clears the repo-root .runsettings autoload. Without it,
#    dotnet test inherits the Category!=E2E filter and runs zero tests in this project.
dotnet test test/WopiHost.E2ETests -p:RunSettingsFilePath= --filter "Client=Collabora"

# …or run everything (both suites, sequentially — each boots its own Aspire stack)
dotnet test test/WopiHost.E2ETests -p:RunSettingsFilePath=
```

Without Docker, the tests log `Docker is not available — skipping …` and pass-as-skipped. No red on
contributor machines that just want to run the unit suite.

## How the fixtures are wired

| Piece | What it does |
|---|---|
| [`WopiAppFixtureBase`](Fixtures/WopiAppFixtureBase.cs) | Shared scaffolding: boots the AppHost via `DistributedApplicationTestingBuilder<Projects.WopiHost_AppHost>` with the lane's `AppHost:*` flags (fed through the `configureBuilder` seam — the only one that runs before the AppHost reads them), waits for the lane's resources, polls the client's readiness endpoint via the Aspire-discovered URL (DCP can remap host ports in test mode), exposes `WebFrontendUrl` / `WopiDocsPath`. |
| [`CollaboraAppFixture`](Collabora/CollaboraAppFixture.cs) | Collabora lane: `UseCollabora=true`, others off; readiness = `/hosting/discovery` 200; keyword-filtered coolwsd log capture for failure diagnostics. |
| [`OnlyOfficeAppFixture`](OnlyOffice/OnlyOfficeAppFixture.cs) | ONLYOFFICE lane: `UseOnlyOffice=true` + `OnlyOfficeProofValidation=true`, others off; readiness = `/healthcheck` body `true` (the document engine, not just nginx); docker-logs + in-container docservice log capture. |
| [`PlaywrightFixture`](Fixtures/PlaywrightFixture.cs) | One `IPlaywright` + Chromium `IBrowser` per collection; each test gets a fresh `IBrowserContext`. Mirrors `test/WopiHost.SmokeTests`'s fixture deliberately — sharing would force Aspire's dependencies onto SmokeTests. |
| [`DockerCheck`](Fixtures/DockerCheck.cs) | Probes `docker info`. Tests `Assert.SkipUnless(...)` when the engine is unreachable. |
| [`CollaboraFixtureCollection`](Collabora/CollaboraFixtureCollection.cs) / [`OnlyOfficeFixtureCollection`](OnlyOffice/OnlyOfficeFixtureCollection.cs) | Pair each app fixture with the Playwright fixture, `DisableParallelization = true` — so an unfiltered run boots the two Aspire stacks **sequentially**, never concurrently (they pin the same host ports). |

## Known flakiness sources

1. **Cold-start time.** Image pulls dominate on a clean cache (Collabora ~1 GB, ONLYOFFICE ~4.3 GB —
   the workflows pre-pull to keep the fixture timeouts bounding runtime, not download). If you see a
   readiness timeout in CI, the most likely cause is upstream image-pull slowness, not WOPI breakage.
2. **Canvas-based editors.** Both render documents to `<canvas>`, so Playwright's text assertions
   don't work inside the editor. Collabora is driven via its documented host-postMessage API;
   ONLYOFFICE via its `Asc.editor` state flags. The *DOM ids* are the brittle part.
3. **Selector / API drift.** Both images float on `:latest`. When a new client major ships, expect to
   revisit the load-signal markers before assuming the test caught a real bug.

## Triaging a failing run

| Symptom | First-line hypothesis |
|---|---|
| Readiness timeout on `collabora` / `onlyoffice` | Image-pull slowness or engine warmup (ONLYOFFICE's first boot initialises Postgres). Re-run; if persistent, check image-tag drift. |
| Iframe never appears | The Detail form failed to submit, or the frontend returned a 4xx. Inspect the Playwright trace artifact. |
| Collabora: `#document-container` ok but no `Action_Load_Resp{success:true}` | Collabora loaded the shell but couldn't fetch the document — CheckFileInfo / GetFile non-200, or proof validation refusing the callback. |
| ONLYOFFICE: `isDocumentLoadComplete` never true | Same class of failure — the dump includes the editor state (`openResult`, dialogs) + docservice logs. |
| Bytes-changed assertion times out (Collabora save test) | PutFile didn't fire (doc in view mode) or the storage provider isn't writing. The diagnostic dump disambiguates via the postMessage trail. |
| Passes locally, fails in CI | The image's `latest` tag changed between local and CI pull. Pin the tag in [`infra/WopiHost.AppHost/Program.cs`](../../infra/WopiHost.AppHost/Program.cs) and re-evaluate. |
