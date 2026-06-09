using WopiHost.E2ETests.Fixtures;

namespace WopiHost.E2ETests.Collabora;

/// <summary>
/// App fixture for the Collabora lane: boots the AppHost with <c>AppHost:UseCollabora=true</c>
/// (ONLYOFFICE + Redis off, so the suite depends on a single container) and gates on Collabora's
/// <c>/hosting/discovery</c> serving 200. Cold-start cost is on the order of 10–20 seconds once the
/// image is cached; the first CI run pays the pull (~1 GB).
/// </summary>
public sealed class CollaboraAppFixture : WopiAppFixtureBase
{
    /// <inheritdoc />
    protected override string ClientResourceName => "collabora";

    /// <inheritdoc />
    protected override string BackendResourceName => "wopihost-collabora";

    /// <inheritdoc />
    protected override string FrontendResourceName => "wopihost-web-collabora";

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string?> AppHostFlags { get; } = new Dictionary<string, string?>
    {
        ["AppHost:UseCollabora"] = "true",
        // ONLYOFFICE defaults on in the AppHost; keep it off here so this suite spins up only the
        // Collabora container, not the ~4.3 GB ONLYOFFICE one alongside it.
        ["AppHost:UseOnlyOffice"] = "false",
        ["AppHost:UseRedisLocks"] = "false",
    };

    /// <inheritdoc />
    protected override string ReadinessProbePath => "/hosting/discovery";

    /// <inheritdoc />
    protected override TimeSpan StartupTimeout => TimeSpan.FromMinutes(3);

    /// <inheritdoc />
    protected override TimeSpan ReadinessTimeout => TimeSpan.FromMinutes(2);

    /// <summary>
    /// Captures the most recent stdout/stderr from the running Collabora container so failure
    /// diagnostics include coolwsd's own logs (WOPI host-allow checks, WebSocket auth denials).
    /// </summary>
    /// <remarks>
    /// The output is shaped as two sections: a <b>filtered</b> view keeping only lines that match
    /// one of the diagnostic keywords (this answers "why was the request denied?" without being
    /// drowned out by Collabora's per-tile trace spam), and the <b>full tail</b> as a fallback for
    /// cases where the filter misses the signal.
    /// </remarks>
    public override async Task<string> CaptureClientLogsAsync()
    {
        const int tailLines = 2000;
        try
        {
            // Aspire names containers after the resource with a hash suffix; match any running
            // container from the collabora/code image.
            var (names, _) = await RunProcessAsync("docker", "ps --filter ancestor=collabora/code --format {{.Names}}").ConfigureAwait(false);
            var containerName = names.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(containerName))
            {
                return "<no running collabora/code container>";
            }

            // Collabora writes most diagnostics to stderr, so capture both streams.
            var (stdout, stderr) = await RunProcessAsync("docker", $"logs --tail {tailLines} {containerName}").ConfigureAwait(false);

            // Lines relevant to diagnosing websocket / WOPI-host rejection:
            //   - "WOPI" / "wopi" — host strings, allowed-host checks, WopiSrc parsing
            //   - "WebSocket" / "websocket" — handshake + upgrade
            //   - "Unauthorized" / "unauthoriz" — coolwsd's literal denial message
            //   - "ERR" / "WRN" — coolwsd's own severity tags
            //   - "Reject" / "reject" — generic denial
            //   - "isWopiHostAllowed" / "FrameAncestors" — specific check names from coolwsd
            // Kept narrow on purpose; the full tail below catches anything missed.
            var filterKeywords = new[]
            {
                "WOPI", "wopi", "WebSocket", "websocket", "Unauthorized", "unauthoriz",
                "ERR", "WRN", "Reject", "reject", "isWopiHostAllowed", "FrameAncestors",
                "frame-ancestors", "allowed", "Allow", "denied", "Denied",
            };

            static IEnumerable<string> Filter(string text, string[] keywords)
            {
                foreach (var line in text.Split('\n'))
                {
                    foreach (var kw in keywords)
                    {
                        if (line.Contains(kw, StringComparison.Ordinal))
                        {
                            yield return line;
                            break;
                        }
                    }
                }
            }

            var filteredStdout = string.Join("\n", Filter(stdout, filterKeywords));
            var filteredStderr = string.Join("\n", Filter(stderr, filterKeywords));

            // Truncate the full-tail dump to a budget that fits in xUnit's failure message without
            // truncating off the more useful filtered section (xUnit truncates around ~32 KB).
            static string TailBudget(string text, int maxChars)
            {
                if (text.Length <= maxChars) return text;
                return "…(truncated)…\n" + text[^maxChars..];
            }

            return $"=== {containerName} filtered stdout ===\n{filteredStdout}\n" +
                   $"=== {containerName} filtered stderr ===\n{filteredStderr}\n" +
                   $"=== {containerName} stdout tail ({tailLines} max) ===\n{TailBudget(stdout, 6000)}\n" +
                   $"=== {containerName} stderr tail ({tailLines} max) ===\n{TailBudget(stderr, 6000)}";
        }
        catch (Exception ex)
        {
            return $"<CaptureClientLogsAsync threw: {ex.GetType().Name}: {ex.Message}>";
        }
    }
}
