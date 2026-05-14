using Microsoft.Playwright;
using WopiHost.E2ETests.Collabora.Fixtures;

namespace WopiHost.E2ETests.Collabora;

/// <summary>
/// End-to-end happy-path tests for the WopiHost ↔ Collabora editing loop. Tracked under
/// <see href="https://github.com/petrsvihlik/WopiHost/issues/357">issue #357</see>
/// "Approach B". Two tests:
/// </summary>
/// <list type="bullet">
///   <item>
///     <see cref="OpensDocxInCollabora_RendersDocumentArea"/> — proves the WOPI handshake
///     completes: the frontend's edit page submits the office form, the iframe loads
///     Collabora, and Collabora successfully calls CheckFileInfo + GetFile against the
///     WopiHost backend (otherwise the document area wouldn't render).
///   </item>
///   <item>
///     <see cref="SaveAfterEdit_WritesNewBytesToDisk"/> — proves the PutFile callback path
///     works end-to-end: types into the document via the Collabora canvas, triggers Save,
///     and asserts the on-disk file's last-write timestamp has advanced. Restores the
///     original bytes on teardown so subsequent runs aren't dependent on the prior outcome.
///   </item>
/// </list>
/// <remarks>
/// <para>
/// <b>Why this lives in a separate test project</b> rather than alongside the DOM-only
/// Playwright suite in <c>test/WopiHost.SmokeTests</c>: Aspire.Hosting.Testing pulls in the
/// AppHost SDK + every Aspire resource type the AppHost references, and the cold-start
/// requires Docker. Keeping the dependency surface contained to this project means the
/// per-PR Build &amp; Test workflow doesn't pay the cost.
/// </para>
/// <para>
/// <b>Where this runs</b>: a dedicated <c>e2e-collabora.yml</c> workflow on a nightly cron
/// plus <c>workflow_dispatch</c>, NEVER on per-PR CI. The integration is by design flaky
/// (Collabora's canvas-based editor + WebSocket cold-start), and gating PRs on it would
/// cause more friction than it'd catch.
/// </para>
/// <para>
/// <b>Selector strategy</b>: Collabora's editor UI iterates rapidly and DOM selectors drift
/// between releases. We pin only the markers that have stayed stable across at least three
/// CODE versions: <c>iframe[name='office_frame']</c> (the WopiHost.Web sample's own iframe),
/// <c>#document-container</c> / <c>#document-canvas</c> (Collabora's main canvas area), and
/// the <c>postMessage</c> API surface (Collabora's documented host-protocol channel, much
/// less likely to change than DOM ids). When something breaks at the upstream version bump,
/// expect to revisit these selectors before investigating the WOPI layer itself.
/// </para>
/// </remarks>
[Collection(CollaboraFixtureCollection.Name)]
[Trait("Category", "E2E")]
public sealed class CollaboraEditDocxTests(CollaboraAppFixture app, PlaywrightFixture playwright) : IAsyncLifetime
{
    private const string SampleDocxName = "test.docx";

    /// <summary>How long we'll wait for Collabora's iframe to render the document area. Generous
    /// because CODE's startup + WebSocket handshake commonly takes 8–15 s on a cold cache.</summary>
    private static readonly TimeSpan s_iframeReadyTimeout = TimeSpan.FromSeconds(60);

    private IBrowserContext _context = null!;
    private byte[]? _originalDocxBytes;
    private DateTime _originalDocxWriteTime;

    public async ValueTask InitializeAsync()
    {
        if (!app.IsDockerAvailable)
        {
            return; // Skip path — tests check IsDockerAvailable before running.
        }

        _context = await playwright.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // The frontend ships HTTPS only with a dev cert; Aspire's per-run cert isn't in
            // Chromium's trust store, so we accept the test-only cert explicitly.
            IgnoreHTTPSErrors = true,
        });

        // Snapshot the docx so the save test's mutation can be restored at teardown.
        var docxPath = Path.Combine(app.WopiDocsPath, SampleDocxName);
        _originalDocxBytes = await File.ReadAllBytesAsync(docxPath);
        _originalDocxWriteTime = File.GetLastWriteTimeUtc(docxPath);
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        // Restore the original docx so the next run starts from the same fixture state, even
        // if the save test mutated it. Skip the rewrite when the file is byte-identical so we
        // don't churn the working tree's modified-time for diagnostics.
        if (_originalDocxBytes is not null)
        {
            var docxPath = Path.Combine(app.WopiDocsPath, SampleDocxName);
            try
            {
                var current = await File.ReadAllBytesAsync(docxPath);
                if (!current.AsSpan().SequenceEqual(_originalDocxBytes))
                {
                    await File.WriteAllBytesAsync(docxPath, _originalDocxBytes);
                    File.SetLastWriteTimeUtc(docxPath, _originalDocxWriteTime);
                }
            }
            catch (FileNotFoundException)
            {
                // The save test could have caused a rename / delete in a future regression —
                // re-create the file from the snapshot.
                await File.WriteAllBytesAsync(docxPath, _originalDocxBytes);
                File.SetLastWriteTimeUtc(docxPath, _originalDocxWriteTime);
            }
        }
    }

    [Fact]
    public async Task OpensDocxInCollabora_RendersDocumentArea()
    {
        Assert.SkipUnless(app.IsDockerAvailable, "Docker is not available — skipping Collabora e2e.");

        var page = await _context.NewPageAsync();

        // Step 1: navigate to the frontend Index and click the Edit link for test.docx.
        // Going via the UI rather than constructing the /Home/Detail URL directly means the
        // file-id (SHA-256 hash) and access-token are produced by the actual sample code path
        // — no test-only knowledge of the id derivation leaks in.
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
            // Surface the actual page so we can tell whether the table is missing, empty, or
            // we landed on an error / auth-redirect page. Without this the failure message is
            // just "selector not found" which is useless for triage.
            var body = await page.ContentAsync();
            throw new Xunit.Sdk.XunitException(
                $"Edit link for {SampleDocxName} not visible at {app.WebFrontendUrl.AbsoluteUri}. Page content was:\n{body}");
        }
        await editLink.ClickAsync();

        // Step 2: the Detail.cshtml view auto-submits a form targeting office_frame. Wait for
        // the iframe to appear. The form auto-submits via inline JS, so the iframe is present
        // in DOM before WebSocket handshake completes.
        var officeFrameElement = page.Locator("iframe[name='office_frame']");
        await Assertions.Expect(officeFrameElement).ToBeAttachedAsync(new() { Timeout = 10_000 });

        // Step 3: get the iframe handle and wait inside it for Collabora's document area.
        // #document-container is Collabora's editor shell — appears after the WebSocket
        // handshake + initial render. Its presence proves the WOPI handshake worked end-to-end:
        // if CheckFileInfo / GetFile had failed Collabora would show an error overlay or stay
        // blank, and #document-container wouldn't be visible.
        //
        // Earlier iterations of this test also waited on #document-canvas, but CODE 25.04+ no
        // longer paints with that id — the actual rendering element name and presence varies by
        // editor mode. Asserting on #document-container is the most version-stable proof of
        // "WOPI handshake completed". The save round-trip test below proves the *editing* path
        // actually works.
        var officeFrame = page.FrameLocator("iframe[name='office_frame']");
        await Assertions.Expect(officeFrame.Locator("#document-container")).ToBeVisibleAsync(
            new() { Timeout = (float)s_iframeReadyTimeout.TotalMilliseconds });
    }

    [Fact]
    public async Task SaveAfterEdit_WritesNewBytesToDisk()
    {
        Assert.SkipUnless(app.IsDockerAvailable, "Docker is not available — skipping Collabora e2e.");

        var docxPath = Path.Combine(app.WopiDocsPath, SampleDocxName);
        var sizeBefore = new FileInfo(docxPath).Length;
        var modifiedBefore = File.GetLastWriteTimeUtc(docxPath);

        var page = await _context.NewPageAsync();
        await page.GotoAsync(app.WebFrontendUrl.AbsoluteUri);
        await page.Locator($"table.files tbody tr:has-text('{SampleDocxName}') a[title='Edit']").First.ClickAsync();

        var officeFrame = page.FrameLocator("iframe[name='office_frame']");
        // Wait for the editor shell — same selector as the handshake test, for the same
        // version-stability reason.
        await Assertions.Expect(officeFrame.Locator("#document-container")).ToBeVisibleAsync(
            new() { Timeout = (float)s_iframeReadyTimeout.TotalMilliseconds });

        // Step 1: dismiss Collabora's "session will expire" modal. It appears asynchronously a
        // few seconds after the editor renders, so a one-shot IsVisible check at this point can
        // race the modal's arrival. Use Playwright's auto-wait by trying to click the OK button
        // with a short timeout; on miss (modal genuinely didn't show), swallow the timeout and
        // proceed. The button id is well-named and stable across CODE versions.
        try
        {
            await officeFrame.Locator("#response-ok-button").ClickAsync(new() { Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
            // No modal — fine, proceed.
        }
        catch (PlaywrightException)
        {
            // Same idea — older Playwright wraps the timeout in PlaywrightException rather than
            // the .NET TimeoutException. Either way: no modal → proceed.
        }

        // Step 2: Collabora opens the doc in "Viewing" mode by default even when the WOPI host
        // sends UserCanWrite=true (#document-container carries class="readonly"). The view-mode
        // dropdown in the notebookbar has the toggle; clicking it once and then picking the
        // "Editing" entry switches the doc into edit mode so subsequent typing actually
        // mutates content. Wrapped in try/catch because the dropdown id has changed between
        // CODE versions before — on miss we just continue and let the save assertion below
        // surface the real symptom.
        try
        {
            await officeFrame.Locator("#viewModeDropdownButton-button").ClickAsync(new() { Timeout = 5_000 });
            await officeFrame.Locator("text=Editing").First.ClickAsync(new() { Timeout = 5_000 });
        }
        catch (PlaywrightException)
        {
            // View-mode dropdown not where we expect — fall through, the save assertion will
            // catch any resulting no-op write.
        }

        // Step 3: focus the document area. The actual document tiles are rendered to internal
        // canvas / <div> elements that swallow pointer events; clicking the container is
        // enough to put input focus on the editor (Collabora intercepts the synthetic mouse
        // event and routes it through loolwsd's tile pipeline).
        // Force=true skips the visible/enabled/stable check so a *second* modal popping up
        // mid-click (e.g. the "Welcome" dialog on a fresh session) doesn't make the click
        // retry-loop and time out. We don't need pointer-event semantics for this test — we
        // only need focus + subsequent keyboard input, both of which work under Force.
        await officeFrame.Locator("#document-container").ClickAsync(new() { Force = true });

        // Step 2: type a marker into the document. The actual text doesn't matter — what
        // matters is that something was inserted. Typing routes through Collabora's normal
        // input path: keystroke → loolwsd → tile invalidate → write-on-save.
        await page.Keyboard.TypeAsync("wopihost-e2e-marker", new KeyboardTypeOptions { Delay = 30 });

        // Step 3: trigger save via the host-postMessage API. Collabora documents this as a
        // first-class integration channel (https://sdk.collaboraonline.com/docs/postmessage_api.html#Action_Save),
        // so it's much more stable than poking the toolbar Save icon — that icon's DOM id
        // has changed at least once between CODE majors. Action_Save with default options
        // does a synchronous save back to the host via PutFile.
        // We dispatch from the parent page because that's what a real WOPI host frontend does
        // and what's authorised on Collabora's side (the iframe's window.parent).
        await page.EvaluateAsync(@"() => {
            const frame = document.querySelector(""iframe[name='office_frame']"");
            frame.contentWindow.postMessage(JSON.stringify({
                MessageId: 'Action_Save',
                SendTime: Date.now(),
                Values: { Notify: true, ExtendedData: '', DontTerminateEdit: false, DontSaveIfUnmodified: false }
            }), '*');
        }");

        // Step 4: poll for the on-disk write. The PutFile request travels Collabora → host
        // backend → file-system provider; even after Action_Save returns, the writeback can
        // take a few seconds. We give it up to 30 s with a 250 ms poll cadence.
        var saveDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var changed = false;
        while (DateTime.UtcNow < saveDeadline)
        {
            var info = new FileInfo(docxPath);
            info.Refresh();
            if (info.LastWriteTimeUtc > modifiedBefore || info.Length != sizeBefore)
            {
                changed = true;
                break;
            }
            await Task.Delay(250);
        }

        Assert.True(
            changed,
            $"Expected {SampleDocxName} to be re-written by Collabora's Action_Save within 30s. " +
            $"LastWrite before: {modifiedBefore:O}, after: {File.GetLastWriteTimeUtc(docxPath):O}; " +
            $"size before: {sizeBefore}, after: {new FileInfo(docxPath).Length}.");
    }
}
