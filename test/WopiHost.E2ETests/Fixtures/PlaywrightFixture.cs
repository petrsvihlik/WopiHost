using Microsoft.Playwright;

namespace WopiHost.E2ETests.Fixtures;

/// <summary>
/// Provides the xUnit collection fixture that owns a single <see cref="IPlaywright"/> and
/// <see cref="IBrowser"/> for the lifetime of an e2e test collection. Each test creates a fresh
/// <see cref="IBrowserContext"/> for cookie / storage isolation.
/// </summary>
/// <remarks>
/// <para>
/// Initialization calls <c>Playwright.Program.Main(["install", "chromium"])</c>,
/// which is idempotent — fast when browsers are already cached, downloads them once otherwise.
/// The nightly workflows install Chromium explicitly via <c>playwright.ps1 install --with-deps chromium</c>
/// so the per-test-run check is a no-op in CI.
/// </para>
/// <para>
/// Mirrors <c>test/WopiHost.SmokeTests/Fixtures/PlaywrightFixture.cs</c>; the two are kept
/// separate because the SmokeTests project has lighter dependencies (no Aspire.Hosting.Testing).
/// </para>
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    /// <summary>Gets the Playwright driver owned by the collection.</summary>
    public IPlaywright Playwright { get; private set; } = null!;

    /// <summary>Gets the headless Chromium instance shared by every test in the collection.</summary>
    public IBrowser Browser { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright browser install failed with exit code {exitCode}. " +
                "Run 'pwsh artifacts/bin/WopiHost.E2ETests/debug/playwright.ps1 install chromium' manually.");
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }
}
