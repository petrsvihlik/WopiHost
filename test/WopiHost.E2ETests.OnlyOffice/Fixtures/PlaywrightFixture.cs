using Microsoft.Playwright;

namespace WopiHost.E2ETests.OnlyOffice.Fixtures;

/// <summary>
/// xUnit class fixture that owns a single <see cref="IPlaywright"/> + <see cref="IBrowser"/>
/// for the lifetime of the e2e test collection. Each test creates a fresh
/// <see cref="IBrowserContext"/> for cookie / storage isolation.
/// </summary>
/// <remarks>
/// First-run behaviour: shells out to <c>Playwright.Program.Main(["install", "chromium"])</c>,
/// which is idempotent — fast when browsers are already cached, downloads them once otherwise.
/// The nightly workflow installs Chromium explicitly via <c>playwright.ps1 install --with-deps chromium</c>
/// so the per-test-run check is a no-op in CI. Mirrors the Collabora e2e fixture.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright browser install failed with exit code {exitCode}. " +
                "Run 'pwsh test/WopiHost.E2ETests.OnlyOffice/bin/Debug/net10.0/playwright.ps1 install chromium' manually.");
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }
}
