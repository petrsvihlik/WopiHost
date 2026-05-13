using Microsoft.Playwright;
using WopiHost.SmokeTests.Fixtures;
using Xunit;

namespace WopiHost.SmokeTests;

/// <summary>
/// Single smoke test for <c>sample/WopiHost.Validator</c> — the combined host+frontend used to
/// drive Microsoft's WOPI validator. Asserts the index page renders without throwing; deeper
/// validator-flow assertions belong with the validator suite itself, not Playwright.
/// </summary>
[Collection(nameof(SmokeTestCollection))]
public sealed class ValidatorSampleSmokeTests(PlaywrightFixture playwright) : IAsyncLifetime
{
    private readonly ValidatorSampleFactory _factory = new();
    private IBrowserContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        _context = await playwright.Browser.NewContextAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CloseAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Index_renders()
    {
        var page = await _context.NewPageAsync();
        var response = await page.GotoAsync(_factory.ServerUrl.AbsoluteUri);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected 2xx, got {response.Status}");

        // Page renders to a non-empty body — minimal sanity check.
        var bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }
}
