# WopiHost.SmokeTests

DOM-only Playwright smoke tests for the sample frontends (`sample/WopiHost.Web`, `sample/WopiHost.Validator`). Tracking issue: [#357](https://github.com/petrsvihlik/WopiHost/issues/357) — Approach A.

## What's covered

- `WopiHost.Web` index page renders, breadcrumb shows `Root`, file table populates from `sample/wopi-docs/`.
- `View` and `Edit` action links carry the right `id` and `wopiaction` route values.
- `disabledIcon` styling appears on action links for extensions the (faked) discoverer reports as unsupported (`.html`, `.wopitest`).
- No JavaScript / page errors fire on load (favicon-resource errors from the unreachable fake `ClientUrl` are ignored).
- `WopiHost.Validator` index page renders.

## What's not covered

- Visual regression / pixel diffs.
- End-to-end editor flow through Collabora — that's [Approach B](https://github.com/petrsvihlik/WopiHost/issues/357#approach-b--end-to-end-edit-flow-through-collabora-nightly-not-per-pr) and lives in a separate test project once it lands.
- Layout cross-equivalence between `WopiHost.Web` and `WopiHost.Web.Oidc`. Their `_Layout.cshtml` files have legitimately diverged (the OIDC sample's nav is auth-aware), so byte-equality assertions wouldn't hold.

## How it's wired

| Piece | What it does |
|---|---|
| [`WebSampleFactory`](Fixtures/WebSampleFactory.cs) / [`ValidatorSampleFactory`](Fixtures/ValidatorSampleFactory.cs) | Hosts the sample over **real Kestrel** on a random loopback port (the default `WebApplicationFactory` uses an in-memory `TestServer` Playwright cannot reach). The base `TestServer` is still created so `WebApplicationFactory`'s lazy lifecycle stays happy; a parallel Kestrel host is started alongside it for the browser to drive. |
| [`FakeDiscoverer`](Fixtures/FakeDiscoverer.cs) | Stand-in `IDiscoverer` so the controller renders without an actual WOPI client to query. Reports `docx`, `xlsx`, `pptx` as supported and rejects everything else — that's how the `disabledIcon` assertion exercises a real path. |
| [`PlaywrightFixture`](Fixtures/PlaywrightFixture.cs) | xUnit `IAsyncLifetime` that owns one `IPlaywright` + one `IBrowser` per test collection. Each test gets its own `IBrowserContext` for cookie/storage isolation. |

## Running locally

```bash
# 1. Build (this also makes the playwright.ps1 helper script available under artifacts/bin/)
dotnet build test/WopiHost.SmokeTests

# 2. Install Chromium (one-off; idempotent)
pwsh artifacts/bin/WopiHost.SmokeTests/debug/playwright.ps1 install chromium

# 3. Run
dotnet test test/WopiHost.SmokeTests
```

The `PlaywrightFixture` will auto-install Chromium on first run if step 2 was skipped — works fine locally but slows the first CI run, which is why the workflow installs it explicitly.
