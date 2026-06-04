using Microsoft.Playwright;
using WopiHost.E2ETests.Collabora.Fixtures;

namespace WopiHost.E2ETests.Collabora;

/// <summary>
/// End-to-end happy-path tests for the WopiHost ↔ Collabora editing loop. Two tests:
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
/// between releases. The tests pin only the markers that have stayed stable across at least three
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

    /// <summary>How long to wait for Collabora's iframe to render the document area. Generous
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
            // Chromium's trust store, so the test-only cert is accepted explicitly.
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
        // if the save test mutated it. Skip the rewrite when the file is byte-identical to avoid
        // churning the working tree's modified-time for diagnostics.
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

    /// <summary>
    /// Init script installed on every test page to capture Collabora's postMessage stream.
    /// Unwraps <c>e.data</c> (always a JSON string from Collabora) into structured fields
    /// so the subsequent C# searches don't have to deal with JSON-in-JSON escaping.
    /// Without the unwrap, <c>JSON.stringify(window.__collaboraMessages)</c> turns
    /// <c>{"MessageId":"Action_Load_Resp"}</c> into <c>"{\"MessageId\":\"Action_Load_Resp\"}"</c>
    /// in C# memory — every backslash-escaped — and the test's <c>Contains</c> on a literal
    /// <c>"MessageId":"Action_Load_Resp"</c> silently never matches.
    /// </summary>
    private const string CollaboraMessageCaptureScript = @"
        window.__collaboraMessages = [];
        window.addEventListener('message', (e) => {
            try {
                var raw = typeof e.data === 'string' ? e.data : null;
                var parsed = null;
                if (raw) { try { parsed = JSON.parse(raw); } catch { /* not JSON */ } }
                window.__collaboraMessages.push({
                    t: Date.now(),
                    origin: e.origin,
                    messageId: parsed && parsed.MessageId,
                    status:    parsed && parsed.Values && parsed.Values.Status,
                    success:   parsed && parsed.Values && parsed.Values.success,
                    errorType: parsed && parsed.Values && parsed.Values.errorType,
                    errorMsg:  parsed && parsed.Values && parsed.Values.errorMsg,
                    raw: raw
                });
            } catch { /* ignore */ }
        });
    ";

    [Fact]
    public async Task OpensDocxInCollabora_RendersDocumentArea()
    {
        Assert.SkipUnless(app.IsDockerAvailable, "Docker is not available — skipping Collabora e2e.");

        var page = await _context.NewPageAsync();

        // Listen for Collabora's postMessages from the very first navigation. Critical because
        // #document-container being visible is NOT proof the document loaded — Collabora paints
        // the shell even when its WebSocket auth fails (errorType:"websocketunauthorized"). The
        // only durable proof of a successful handshake is Collabora emitting
        // Action_Load_Resp{success:true} OR App_LoadingStatus{Status:"Document_Loaded"}.
        await page.AddInitScriptAsync(CollaboraMessageCaptureScript);

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
            // Surface the actual page to disambiguate a missing/empty table from an error /
            // auth-redirect page. Without this the failure message is just "selector not found",
            // which is useless for triage.
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

        // Step 3: get the iframe handle and wait for Collabora's editor shell to attach. Note
        // that #document-container is visible even when the WebSocket auth fails (the shell
        // paints, the document area shows an error overlay) — so seeing it visible is necessary
        // but NOT sufficient.
        var officeFrame = page.FrameLocator("iframe[name='office_frame']");
        await Assertions.Expect(officeFrame.Locator("#document-container")).ToBeVisibleAsync(
            new() { Timeout = (float)s_iframeReadyTimeout.TotalMilliseconds });

        // Step 4: opt in to Collabora's postMessage protocol. Collabora is silent until the
        // host page sends Host_PostmessageReady; once that lands it starts emitting
        // App_LoadingStatus, Action_Load_Resp, Document_LoadedSuccessfully, etc.
        await page.EvaluateAsync(@"() => {
            var frame = document.querySelector('iframe[name=""office_frame""]');
            frame.contentWindow.postMessage(JSON.stringify({
                MessageId: 'Host_PostmessageReady',
                SendTime: Date.now(),
                Values: {}
            }), '*');
        }");

        // Step 5: poll the captured postMessages until there's proof the document actually
        // loaded. Action_Load_Resp{success:false} is the failure-mode signal checked explicitly
        // to fast-fail with a useful message instead of timing out blind.
        //
        // The capture script unwraps Collabora's JSON-in-JSON envelope into top-level
        // `messageId`/`status`/`success` fields on each entry, so the JS-side filtering uses
        // straight property comparisons (no string-escape gymnastics). The find runs on the
        // JS side and passes back a single boolean per loop iteration to keep the round-trip
        // tiny and the C# code obvious.
        var loadDeadline = DateTime.UtcNow + s_iframeReadyTimeout;
        while (DateTime.UtcNow < loadDeadline)
        {
            var verdict = await page.EvaluateAsync<string>(@"
                () => {
                    var arr = window.__collaboraMessages || [];
                    if (arr.some(m => m.messageId === 'Action_Load_Resp' && m.success === true)) return 'success';
                    if (arr.some(m => m.messageId === 'Action_Load_Resp' && m.success === false)) return 'failed';
                    if (arr.some(m => m.messageId === 'App_LoadingStatus' && m.status === 'Document_Loaded')) return 'success';
                    return 'pending';
                }
            ");
            if (verdict == "success")
            {
                return; // GREEN — handshake completed.
            }
            if (verdict == "failed")
            {
                break;
            }
            await Task.Delay(500);
        }

        var finalMessages = await page.EvaluateAsync<string>(
            "() => JSON.stringify(window.__collaboraMessages || [], null, 2)");
        var collaboraLogs = await app.CaptureCollaboraLogsAsync();
        throw new Xunit.Sdk.XunitException(
            $"Collabora did not emit a successful load event within {s_iframeReadyTimeout.TotalSeconds}s.\n" +
            $"Captured postMessages:\n{finalMessages}\n" +
            $"\nCollabora container logs (last 400 lines):\n{collaboraLogs}");
    }

    [Fact]
    public async Task SaveAfterEdit_WritesNewBytesToDisk()
    {
        Assert.SkipUnless(app.IsDockerAvailable, "Docker is not available — skipping Collabora e2e.");

        var docxPath = Path.Combine(app.WopiDocsPath, SampleDocxName);
        var sizeBefore = new FileInfo(docxPath).Length;
        var modifiedBefore = File.GetLastWriteTimeUtc(docxPath);

        var page = await _context.NewPageAsync();

        // Install a postMessage capture on EVERY page (host page + iframe contentWindow) before
        // any navigation, to capture Collabora's host-integration events (App_LoadingStatus,
        // Document_LoadedSuccessfully, Action_Save_Resp, etc.) from their first emission. The
        // events are JSON-encoded; the shared capture script parses them into structured fields
        // so subsequent C# searches don't have to deal with JSON-in-JSON escaping.
        await page.AddInitScriptAsync(CollaboraMessageCaptureScript);

        await page.GotoAsync(app.WebFrontendUrl.AbsoluteUri);
        await page.Locator($"table.files tbody tr:has-text('{SampleDocxName}') a[title='Edit']").First.ClickAsync();

        var officeFrame = page.FrameLocator("iframe[name='office_frame']");
        // Wait for the editor shell — same selector as the handshake test, for the same
        // version-stability reason.
        await Assertions.Expect(officeFrame.Locator("#document-container")).ToBeVisibleAsync(
            new() { Timeout = (float)s_iframeReadyTimeout.TotalMilliseconds });

        // Step 0: send Host_PostmessageReady to opt the host page into Collabora's postMessage
        // protocol. Without this, CODE 25.04 sends nothing back to the parent window — including
        // App_LoadingStatus, Document_LoadedSuccessfully, and (critically) Action_Save_Resp.
        // The host MUST initiate, per
        // https://sdk.collaboraonline.com/docs/postmessage_api.html.
        await page.EvaluateAsync(@"() => {
            var frame = document.querySelector('iframe[name=""office_frame""]');
            frame.contentWindow.postMessage(JSON.stringify({
                MessageId: 'Host_PostmessageReady',
                SendTime: Date.now(),
                Values: {}
            }), '*');
        }");

        // Step 1: dismiss Collabora's "session will expire" modal. It appears asynchronously a
        // few seconds after the editor renders, so a one-shot IsVisible check at this point can
        // race the modal's arrival. Use Playwright's auto-wait by trying to click the OK button
        // with a short timeout; on miss (modal genuinely didn't show), swallow the timeout and
        // proceed. The button id is well-named and stable across CODE versions.
        await TryDismissAsync(officeFrame.Locator("#response-ok-button"), timeoutMs: 5_000);

        // Step 1a: dismiss Collabora's "welcome" iframe overlay (CODE 25.04+ shows it on first
        // open). The wrapper is a <div class="iframe-welcome-wrap"> with an .iframe-welcome
        // child whose pointer-events sit ON TOP of the notebookbar. Until it's dismissed, every
        // click into the toolbar (including the view-mode dropdown below) gets swallowed with
        // "subtree intercepts pointer events" — Playwright then times out at 5 s.
        //
        // The dialog's close affordance has varied across CODE versions: 25.04 ships an
        // <button class="iframe-welcome-close">, older versions had a "Close" link. Each is
        // tried in turn, falling back to pressing Escape on the iframe contentWindow (Collabora's
        // dialog manager closes on Esc), and finally just removing the wrapper from the DOM
        // (defensive against further selector drift).
        await TryDismissAsync(officeFrame.Locator(".iframe-welcome-close"), timeoutMs: 3_000);
        await TryDismissAsync(officeFrame.Locator(".iframe-welcome-wrap button:has-text('Close')"), timeoutMs: 1_500);
        await page.EvaluateAsync(@"() => {
            try {
                var frame = document.querySelector('iframe[name=""office_frame""]');
                var wrap = frame && frame.contentDocument && frame.contentDocument.querySelector('.iframe-welcome-wrap');
                if (wrap) { wrap.remove(); }
            } catch (_) { /* same-origin policy or no wrap — both fine, fall through */ }
        }");

        // Step 2: Collabora opens the doc in "Viewing" mode by default even when the WOPI host
        // sends UserCanWrite=true (#document-container carries class="readonly"). The view-mode
        // dropdown in the notebookbar has the toggle; clicking it once and then picking the
        // "Editing" entry switches the doc into edit mode so subsequent typing actually
        // mutates content. Wrapped to swallow any failure (selector drift, leftover overlay,
        // timeout) because the save assertion below is the real test — if Collabora kept the
        // doc read-only, the write won't happen and the diagnostic dump explains it.
        // Catches the broad Exception bucket because Playwright's timeout surfaces as
        // System.TimeoutException (from WrapApiCallAsync), NOT as a PlaywrightException.
        try
        {
            await officeFrame.Locator("#viewModeDropdownButton-button").ClickAsync(new() { Timeout = 5_000 });
            await officeFrame.Locator("text=Editing").First.ClickAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            // View-mode dropdown not where expected, or the click was intercepted. Fall
            // through — the save assertion below will catch any resulting no-op write.
        }

        // Step 3: focus the document area. The actual document tiles are rendered to internal
        // canvas / <div> elements that swallow pointer events; clicking the container is
        // enough to put input focus on the editor (Collabora intercepts the synthetic mouse
        // event and routes it through loolwsd's tile pipeline).
        // Force=true skips the visible/enabled/stable check so a *second* modal popping up
        // mid-click (e.g. the "Welcome" dialog on a fresh session) doesn't make the click
        // retry-loop and time out. Pointer-event semantics aren't needed here — only focus +
        // subsequent keyboard input, both of which work under Force.
        await officeFrame.Locator("#document-container").ClickAsync(new() { Force = true });

        // Step 2: type a marker into the document. The actual text doesn't matter — what
        // matters is that something was inserted. Typing routes through Collabora's normal
        // input path: keystroke → loolwsd → tile invalidate → write-on-save.
        await page.Keyboard.TypeAsync("wopihost-e2e-marker", new KeyboardTypeOptions { Delay = 30 });

        // Give Collabora's WebSocket pipeline a moment to settle so the document model is
        // marked dirty before the save request. Without this, Action_Save can race the typing
        // and be a no-op (loolwsd hasn't yet processed the keystrokes into a model change).
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Step 3: trigger save via the host-postMessage API. Collabora documents this as a
        // first-class integration channel (https://sdk.collaboraonline.com/docs/postmessage_api.html#Action_Save),
        // so it's much more stable than poking the toolbar Save icon — that icon's DOM id
        // has changed at least once between CODE majors. Action_Save with default options
        // does a synchronous save back to the host via PutFile.
        // Dispatched from the parent page because that's what a real WOPI host frontend does
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
        // take a few seconds. Allow up to 60 s with a 250 ms poll cadence — a shorter budget can
        // be too tight given Collabora's autosave debouncing on CI.
        var saveDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
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

        static async Task TryDismissAsync(ILocator locator, int timeoutMs)
        {
            try
            {
                await locator.ClickAsync(new() { Timeout = timeoutMs });
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                // Modal genuinely didn't appear (or its selector drifted across CODE versions).
                // Caller treats this as a best-effort dismissal — proceed regardless.
            }
        }

        if (!changed)
        {
            // Pull the postMessage trail + the editor's read-only state to disambiguate WHY the
            // save was a no-op. Three classes of failure mode readable off the captured events:
            //   - Document_LoadedSuccessfully with permissions != edit  → CheckFileInfo wire content not granting UserCanWrite.
            //   - Action_Save_Resp with success: false                  → host-side save handler failed (lock conflict, IO error).
            //   - No Action_Save_Resp at all                            → Collabora ignored the save trigger (doc in view mode).
            // Stringify on the JS side. `EvaluateAsync<object>` over an array returns a boxed
            // JS array whose .ToString() is "System.Object[]" — useless. JSON.stringify with
            // indentation gives a readable, copy-pasteable diagnostic.
            var messages = await page.EvaluateAsync<string>(
                "() => JSON.stringify(window.__collaboraMessages || [], null, 2)");
            string? containerClass = null;
            try
            {
                containerClass = await officeFrame.Locator("#document-container").GetAttributeAsync("class");
            }
            catch { /* selector may have died if Collabora unmounted */ }

            var collaboraLogs = await app.CaptureCollaboraLogsAsync();
            throw new Xunit.Sdk.XunitException(
                $"Expected {SampleDocxName} to be re-written by Collabora's Action_Save within 60s.\n" +
                $"  LastWrite before: {modifiedBefore:O}\n" +
                $"  LastWrite after:  {File.GetLastWriteTimeUtc(docxPath):O}\n" +
                $"  Size before: {sizeBefore}\n" +
                $"  Size after:  {new FileInfo(docxPath).Length}\n" +
                $"  #document-container class: {containerClass ?? "<unresolved>"}\n" +
                $"  Captured Collabora postMessages (newest last):\n{messages}\n" +
                $"\nCollabora container logs (last 400 lines):\n{collaboraLogs}");
        }
    }
}
