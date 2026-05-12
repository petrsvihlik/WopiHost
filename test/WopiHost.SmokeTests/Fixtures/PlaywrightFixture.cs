using Microsoft.Playwright;
using Xunit;

namespace WopiHost.SmokeTests.Fixtures;

/// <summary>
/// xUnit class fixture that owns a single <see cref="IPlaywright"/> + <see cref="IBrowser"/>
/// for the lifetime of a test class. Each test creates a fresh <see cref="IBrowserContext"/>
/// (cheap; sub-second) for isolation, so tests don't share cookies / storage.
/// </summary>
/// <remarks>
/// On first construction we shell out to <c>Playwright.Program.Main(["install", "chromium"])</c>,
/// which is idempotent — it is fast when browsers are already cached, and downloads them once
/// otherwise. CI workflows can install upfront via <c>pwsh playwright.ps1 install --with-deps chromium</c>
/// to avoid the per-test-run check.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // Returns 0 immediately if browsers are already installed.
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright browser install failed with exit code {exitCode}. " +
                "Run 'pwsh test/WopiHost.SmokeTests/bin/Debug/net10.0/playwright.ps1 install chromium' manually.");
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
