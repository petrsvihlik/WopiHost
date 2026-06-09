using Microsoft.Playwright;
using WopiHost.E2ETests.OnlyOffice.Fixtures;

namespace WopiHost.E2ETests.OnlyOffice;

/// <summary>
/// End-to-end happy-path test for the WopiHost ↔ ONLYOFFICE editing loop. Proves the WOPI handshake
/// completes: the frontend's edit page submits the office form, ONLYOFFICE's editor loads in the
/// iframe, and ONLYOFFICE successfully calls CheckFileInfo + GetFile against the WopiHost backend
/// (otherwise the document would never finish loading).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a separate test project</b> (mirroring <c>WopiHost.E2ETests.Collabora</c>):
/// Aspire.Hosting.Testing pulls in the AppHost SDK + every Aspire resource type the AppHost
/// references, and the cold-start requires Docker. Keeping that dependency surface contained means
/// the per-PR Build &amp; Test workflow doesn't pay the cost.
/// </para>
/// <para>
/// <b>Where this runs</b>: a dedicated <c>e2e-onlyoffice.yml</c> workflow on a nightly cron plus
/// <c>workflow_dispatch</c>, NEVER on per-PR CI ([Trait Category=E2E] is filtered out by the
/// repo-root <c>.runsettings</c>). The integration is by design heavy — the ONLYOFFICE image is
/// ~4.3 GB and bundles its own Postgres/RabbitMQ — and gating PRs on it would cause more friction
/// than it'd catch.
/// </para>
/// <para>
/// <b>Load signal</b>: ONLYOFFICE does not expose Collabora's host-postMessage protocol, so the test
/// reads the editor's own <c>window.Asc.editor.isDocumentLoadComplete</c> flag (a boolean property,
/// NOT a method). It flips true only once the document has been fetched via WOPI and rendered;
/// a failed host callback (e.g. proof rejection → "Download failed") leaves it false, which the test
/// surfaces as a timeout with the editor state + ONLYOFFICE container logs attached.
/// </para>
/// </remarks>
[Collection(OnlyOfficeFixtureCollection.Name)]
[Trait("Category", "E2E")]
public sealed class OnlyOfficeOpenDocxTests(OnlyOfficeAppFixture app, PlaywrightFixture playwright) : IAsyncLifetime
{
    private const string SampleDocxName = "test.docx";

    /// <summary>How long to wait for ONLYOFFICE to finish loading the document. Generous because the
    /// document engine's first open after a cold container start can take 20–40 s on CI.</summary>
    private static readonly TimeSpan s_documentReadyTimeout = TimeSpan.FromSeconds(90);

    private IBrowserContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        if (!app.IsDockerAvailable)
        {
            return; // Skip path — tests check IsDockerAvailable before running.
        }

        _context = await playwright.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // The frontend ships HTTPS only with a dev cert; Aspire's per-run cert isn't in
            // Chromium's trust store, so the test-only cert is accepted explicitly.
            IgnoreHTTPSErrors = true,
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
        }
    }

    [Fact]
    public async Task OpensDocxInOnlyOffice_LoadsDocument()
    {
        Assert.SkipUnless(app.IsDockerAvailable, "Docker is not available — skipping ONLYOFFICE e2e.");

        var page = await _context.NewPageAsync();

        // Step 1: navigate to the frontend Index and click the Edit link for test.docx. Going via the
        // UI rather than constructing the /Home/Detail URL directly means the file-id (SHA-256 hash)
        // and access-token are produced by the actual sample code path.
        var response = await page.GotoAsync(app.WebFrontendUrl.AbsoluteUri, new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (response is null || !response.Ok)
        {
            var body = await page.ContentAsync();
            throw new Xunit.Sdk.XunitException(
                $"GET {app.WebFrontendUrl.AbsoluteUri} returned {response?.Status.ToString() ?? "<null>"}. Body:\n{body}");
        }
        var editLink = page.Locator($"table.files tbody tr:has-text('{SampleDocxName}') a[title='Edit']");
        try
        {
            await Assertions.Expect(editLink).ToBeVisibleAsync();
        }
        catch (PlaywrightException)
        {
            var body = await page.ContentAsync();
            throw new Xunit.Sdk.XunitException(
                $"Edit link for {SampleDocxName} not visible at {app.WebFrontendUrl.AbsoluteUri}. Page content was:\n{body}");
        }
        await editLink.ClickAsync();

        // Step 2: the Detail view auto-submits a form into iframe[name='office_frame']; ONLYOFFICE's
        // WOPI editor page loads there and in turn nests the real editor in
        // .../documenteditor/main/index.html. Wait for the office_frame to attach.
        await Assertions.Expect(page.Locator("iframe[name='office_frame']")).ToBeAttachedAsync(new() { Timeout = 15_000 });

        // Step 3: poll for ONLYOFFICE's own "document finished loading" flag inside the nested editor
        // frame. isDocumentLoadComplete flips true only after CheckFileInfo + GetFile succeed and the
        // .docx is parsed/rendered, so it's the durable proof of a completed WOPI handshake.
        var deadline = DateTime.UtcNow + s_documentReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var editorFrame = page.Frames.FirstOrDefault(
                f => f.Url.Contains("documenteditor/main/index.html", StringComparison.Ordinal));
            if (editorFrame is not null)
            {
                bool loaded;
                try
                {
                    loaded = await editorFrame.EvaluateAsync<bool>(
                        "() => !!(window.Asc && window.Asc.editor && window.Asc.editor.isDocumentLoadComplete === true)");
                }
                catch (PlaywrightException)
                {
                    // Frame navigated / context not ready for eval yet — keep polling.
                    loaded = false;
                }
                if (loaded)
                {
                    return; // GREEN — ONLYOFFICE finished loading the document.
                }
            }
            await Task.Delay(500);
        }

        // Timed out — gather diagnostics that disambiguate "engine slow" from "WOPI callback failed".
        var editorState = await CaptureEditorStateAsync(page);
        var logs = await app.CaptureOnlyOfficeLogsAsync();
        throw new Xunit.Sdk.XunitException(
            $"ONLYOFFICE did not finish loading {SampleDocxName} within {s_documentReadyTimeout.TotalSeconds}s.\n" +
            $"{editorState}\n\nONLYOFFICE container logs:\n{logs}");
    }

    private static async Task<string> CaptureEditorStateAsync(IPage page)
    {
        var frameUrls = string.Join("\n  ", page.Frames.Select(f => f.Url));
        var editorFrame = page.Frames.FirstOrDefault(
            f => f.Url.Contains("documenteditor/main/index.html", StringComparison.Ordinal));
        var editorProbe = "<editor frame not found>";
        if (editorFrame is not null)
        {
            try
            {
                editorProbe = await editorFrame.EvaluateAsync<string>(@"() => {
                    const ed = window.Asc && window.Asc.editor;
                    const dialogs = Array.from(document.querySelectorAll('.asc-window, .info-box'))
                        .map(e => (e.innerText || '').replace(/\s+/g, ' ').slice(0, 160)).filter(Boolean);
                    return JSON.stringify({
                        isDocumentLoadComplete: ed ? ed.isDocumentLoadComplete : null,
                        openResult: ed ? ed.openResult : null,
                        hasViewer: !!document.querySelector('#id_viewer'),
                        dialogs
                    });
                }");
            }
            catch (PlaywrightException ex)
            {
                editorProbe = "<editor probe failed: " + ex.Message + ">";
            }
        }
        return $"Frames:\n  {frameUrls}\nEditor probe: {editorProbe}";
    }
}
