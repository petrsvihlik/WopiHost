using Microsoft.Playwright;
using WopiHost.SmokeTests.Fixtures;
using Xunit;

namespace WopiHost.SmokeTests;

/// <summary>
/// DOM-only smoke tests for <c>sample/WopiHost.Web</c>. Asserts the Index page renders the
/// expected structure (breadcrumb, file table, action links with the right route values,
/// disabled-icon styling on unsupported extensions) and that no console errors fire on load.
/// Intentionally avoids visual / pixel diffing — those tests rot fast.
/// </summary>
[Collection(nameof(SmokeTestCollection))]
public sealed class WebSampleSmokeTests(PlaywrightFixture playwright) : IAsyncLifetime
{
    private readonly WebSampleFactory _factory = new();
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
    public async Task Index_renders_breadcrumb_and_file_table()
    {
        var page = await _context.NewPageAsync();
        var response = await page.GotoAsync(_factory.ServerUrl.AbsoluteUri);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected 2xx, got {response.Status}");

        // Breadcrumb: starts at "Root" with the current container marked aria-current.
        var breadcrumbItems = await page.Locator("nav.breadcrumb ol li").AllTextContentsAsync();
        Assert.Contains("Root", breadcrumbItems);

        // File table renders with both folder rows (subfolder) and file rows (test.docx etc.).
        var rows = page.Locator("table.files tbody tr");
        Assert.True(await rows.CountAsync() > 0, "Expected at least one row in the file table.");

        // The fixed test docs all show up as named cells (folder + 5 files in sample/wopi-docs).
        var bodyText = await page.Locator("table.files tbody").InnerTextAsync();
        Assert.Contains("subfolder", bodyText);
        Assert.Contains("test.docx", bodyText);
        Assert.Contains("test.html", bodyText);
    }

    [Fact]
    public async Task Action_links_carry_id_and_wopiaction_route_values()
    {
        var page = await _context.NewPageAsync();
        await page.GotoAsync(_factory.ServerUrl.AbsoluteUri);

        // The View / Edit columns render as <a href="/Home/Detail/{id}?wopiaction=View|Edit">.
        var editAnchor = page.Locator("table.files tbody tr a[title='Edit']").First;
        var href = await editAnchor.GetAttributeAsync("href")
            ?? throw new InvalidOperationException("Edit anchor has no href.");

        Assert.Contains("/Home/Detail/", href);
        Assert.Contains("wopiaction=Edit", href);

        var viewAnchor = page.Locator("table.files tbody tr a[title='View']").First;
        var viewHref = await viewAnchor.GetAttributeAsync("href")
            ?? throw new InvalidOperationException("View anchor has no href.");

        Assert.Contains("/Home/Detail/", viewHref);
        Assert.Contains("wopiaction=View", viewHref);
    }

    [Fact]
    public async Task Disabled_icon_class_appears_for_unsupported_extension()
    {
        // FakeDiscoverer reports docx/xlsx/pptx as supported but rejects html/wopitest. The
        // controller stamps the "disabledIcon" class on action anchors whose action isn't
        // supported for that file's extension.
        var page = await _context.NewPageAsync();
        await page.GotoAsync(_factory.ServerUrl.AbsoluteUri);

        // Find the row for test.html and assert both action anchors have disabledIcon.
        var htmlRow = page.Locator("table.files tbody tr").Filter(new() { HasText = "test.html" }).First;
        await Assertions.Expect(htmlRow).ToBeVisibleAsync();

        var disabledCount = await htmlRow.Locator("a.disabledIcon").CountAsync();
        Assert.Equal(2, disabledCount); // both View and Edit should be disabled

        // And docx, which IS supported, should have neither anchor disabled.
        var docxRow = page.Locator("table.files tbody tr").Filter(new() { HasText = "test.docx" }).First;
        var docxDisabledCount = await docxRow.Locator("a.disabledIcon").CountAsync();
        Assert.Equal(0, docxDisabledCount);
    }

    [Fact]
    public async Task Index_loads_with_no_javascript_errors()
    {
        // Asserts only on JS errors (PageError) — resource load failures (favicon, fake-discoverer
        // icon URIs, the ~/css/site.css that StaticWebAssets disablement skips) come through as
        // Console errors and are noise from the test setup, not bugs in the sample.
        var page = await _context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, error) => pageErrors.Add(error);

        await page.GotoAsync(_factory.ServerUrl.AbsoluteUri, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        Assert.Empty(pageErrors);
    }
}
