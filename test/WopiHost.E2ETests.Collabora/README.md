# WopiHost.E2ETests.Collabora

End-to-end tests that exercise the full WopiHost ↔ Collabora editing loop. Tracked under [#357 Approach B](https://github.com/petrsvihlik/WopiHost/issues/357#approach-b--end-to-end-edit-flow-through-collabora-nightly-not-per-pr).

## What's tested

| Test | Asserts |
|---|---|
| `OpensDocxInCollabora_RendersDocumentArea` | The frontend's Edit link mints a WOPI URL + access token; Collabora's iframe loads; the CheckFileInfo + GetFile handshake completes (proven by the document canvas rendering). |
| `SaveAfterEdit_WritesNewBytesToDisk` | Typing into Collabora and triggering `Action_Save` via the host-postMessage API causes Collabora to call PutFile, and the WopiHost backend writes the new bytes to `sample/wopi-docs/test.docx` within 30 s. |

## What's NOT tested

- **Multi-user / co-authoring** — Cobalt path is its own scoping problem (issue [#321](https://github.com/petrsvihlik/WopiHost/issues/321)).
- **Visual regression / pixel diffs** — rot fast, low signal.
- **WOPI protocol conformance** — that's what [`microsoft/wopi-validator-core`](https://github.com/microsoft/wopi-validator-core) is for. Issue #357 calls it out as a separate workstream.
- **Office Online Server / M365 for the Web** — CODE is a *development substitute* with a different feature surface. A green Collabora run is not M365 conformance.

## Why this is a separate project (not in `WopiHost.SmokeTests`)

Two reasons:

1. **Different cost.** Smoke tests run on every PR in ~30 s. Collabora e2e needs Docker + a real container cold-start (10–20 s) + Playwright. Per-PR is wrong for it; it'd burn CI minutes and gate PRs on a flaky integration that fails for upstream reasons most often.
2. **Different dependency surface.** This project transitively pulls in `Aspire.Hosting.Testing` + the full AppHost project graph. Keeping it isolated means the per-PR `Build & Test` workflow doesn't pay the cost.

## Where this runs

- **Nightly**: [`.github/workflows/e2e-collabora.yml`](../../.github/workflows/e2e-collabora.yml) at 03:17 UTC.
- **On-demand**: `workflow_dispatch` from the Actions tab. Use this after touching `infra/WopiHost.AppHost/`, `sample/WopiHost.Web/`, anything in `src/WopiHost.Core/Controllers/`, or after a Collabora version bump.
- **Never per-PR.** By design.

## Running locally

Requires Docker (engine running, Linux containers, internet for the first image pull).

```bash
# 1. Build (also drops the playwright.ps1 helper next to the test assembly)
dotnet build test/WopiHost.E2ETests.Collabora

# 2. Install Chromium for Playwright (one-off; idempotent)
pwsh artifacts/bin/WopiHost.E2ETests.Collabora/debug/playwright.ps1 install chromium

# 3. (Optional, speeds up the first run) Pre-pull Collabora
docker pull collabora/code

# 4. Run
dotnet test test/WopiHost.E2ETests.Collabora
```

Without Docker, both tests log `Docker is not available — skipping Collabora e2e` and pass-as-skipped. No red on contributor machines that just want to run the unit suite.

## How the fixture is wired

| Piece | What it does |
|---|---|
| [`CollaboraAppFixture`](Fixtures/CollaboraAppFixture.cs) | xUnit collection fixture. Boots the AppHost via `DistributedApplicationTestingBuilder<Projects.WopiHost_AppHost>`, sets `AppHost:UseCollabora=true` + `AppHost:UseRedisLocks=false`, waits for `collabora` / `wopihost` / `wopihost-web` to be healthy, exposes the frontend HTTPS URL. |
| [`PlaywrightFixture`](Fixtures/PlaywrightFixture.cs) | Owns one `IPlaywright` + one Chromium `IBrowser` for the whole collection. Each test gets a fresh `IBrowserContext` for cookie isolation. Mirrors `test/WopiHost.SmokeTests/Fixtures/PlaywrightFixture.cs` deliberately — sharing would force Aspire's dependencies onto SmokeTests. |
| [`DockerCheck`](Fixtures/DockerCheck.cs) | Probes `docker info`. Tests check the result and `Assert.SkipUnless(...)` when the engine is unreachable. |
| [`CollaboraFixtureCollection`](CollaboraFixtureCollection.cs) | Pairs the two fixtures with `DisableParallelization = true`. The Collabora container is a single shared resource for the test run; parallel test invocations against it produce ambiguous failures. |

## Local-environment caveats

This suite was developed against Windows + Docker Desktop and the scaffolding was verified to the point of:

- Config propagation (`AppHost:UseCollabora=true` flows through the `configureBuilder` callback and flips the resource graph).
- AppHost project resources (`wopihost`, `wopihost-web`) start and serve traffic — the Web frontend's HomeController is reached and errors *exactly as expected* when Collabora isn't available, proving the wiring is correct end-to-end up to that point.

What didn't reproduce locally: on Windows / Docker Desktop, Aspire reports the Collabora container resource as `Running` via `ResourceNotificationService`, but no actual container appears in `docker ps -a`. The `/hosting/discovery` poll in the fixture then times out with a clear message. This appears to be a DCP-on-Windows + test-mode interaction; the same AppHost runs the container correctly when launched normally via `dotnet run --project infra/WopiHost.AppHost`. Validation under real Linux containers (Linux CI runners, WSL2 Linux containers, native Linux dev box) is the path forward. If you reproduce locally on Linux and the tests fail, please open an issue — the failure messages are designed to be informative.

## Known flakiness sources

The issue text calls this out and it remains true:

1. **Cold-start time.** Collabora's `WaitForResourceHealthyAsync` timeout is 3 minutes — generous on a clean image cache, tight when GitHub Actions throttles outbound bandwidth. If you see a healthy-wait timeout in CI, the most likely cause is upstream image-pull slowness, not WOPI breakage.
2. **Canvas-based editor.** Collabora renders to `<canvas>`, so Playwright's text-content assertions don't work inside the editor. We focus the canvas, type via `page.Keyboard.TypeAsync`, then trigger Save via the documented host-postMessage API (`Action_Save`). Both calls have stayed stable across CODE versions; the *DOM ids* (`#document-container`, `#document-canvas`) are the brittle part.
3. **Selector drift.** When CODE ships a new major (image tag `latest` floats), expect to revisit the iframe selectors before assuming the test caught a real bug. The two assertions are at the top of [`CollaboraEditDocxTests.cs`](CollaboraEditDocxTests.cs) for ease of locating.

## Triaging a failing run

| Symptom | First-line hypothesis |
|---|---|
| Healthy-wait timeout on `collabora` | Image-pull slowness / GitHub Actions outbound throttle. Re-run; if persistent, check Collabora image tag drift. |
| Iframe never appears | The Detail.cshtml form is failing to submit, or the frontend is returning a 4xx. Inspect the Playwright trace artifact. |
| `#document-canvas` times out | Collabora loaded the shell but couldn't fetch the document. Almost always CheckFileInfo / GetFile returning non-200, or proof-validation refusing the callback (`Wopi:Security:DisableProofValidation` not flipped). |
| Bytes-changed assertion times out | PutFile didn't fire, or the writable storage provider isn't writing. Check the WOPI host logs in the Aspire dashboard. The save assertion polls for 30 s; bumping that is rarely the right fix. |
| Passes locally, fails in CI | The image's `latest` tag changed between local pull and CI pull. Pin the tag in [`infra/WopiHost.AppHost/Program.cs`](../../infra/WopiHost.AppHost/Program.cs) (`collabora/code:6.4`, etc.) and re-evaluate locally. |
