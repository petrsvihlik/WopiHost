using WopiHost.E2ETests.Fixtures;

namespace WopiHost.E2ETests.OnlyOffice;

/// <summary>
/// App fixture for the ONLYOFFICE lane: boots the AppHost with <c>AppHost:UseOnlyOffice=true</c>
/// (Collabora + Redis off, so the suite depends on a single container) and gates on ONLYOFFICE's
/// <c>/healthcheck</c> returning a literal <c>true</c>. Unlike <c>/hosting/discovery</c> (served by
/// nginx almost immediately), <c>/healthcheck</c> only answers true once the document-conversion
/// engine is actually ready — gating on it removes the "editor shell loads but document download
/// fails" cold-start race.
/// </summary>
/// <remarks>
/// The ONLYOFFICE image is heavyweight (bundles its own PostgreSQL / RabbitMQ), so the cold-start is
/// longer than Collabora's — the first CI run on a clean cache pays the ~4.3 GB pull plus the
/// engine warmup; the timeouts below are sized accordingly.
/// </remarks>
public sealed class OnlyOfficeAppFixture : WopiAppFixtureBase
{
    /// <inheritdoc />
    protected override string ClientResourceName => "onlyoffice";

    /// <inheritdoc />
    protected override string BackendResourceName => "wopihost-onlyoffice";

    /// <inheritdoc />
    protected override string FrontendResourceName => "wopihost-web-onlyoffice";

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string?> AppHostFlags { get; } = new Dictionary<string, string?>
    {
        ["AppHost:UseOnlyOffice"] = "true",
        // Collabora defaults on in the AppHost; keep it off here so this suite spins up only the
        // ONLYOFFICE container.
        ["AppHost:UseCollabora"] = "false",
        ["AppHost:UseRedisLocks"] = "false",
        // Runs the lane with REAL proof validation: ONLYOFFICE signs its WOPI callbacks
        // (X-WOPI-Proof over the spec's token/url/timestamp layout) and WopiProofValidator
        // accepts them now that CheckFileInfo no longer advertises a self-referential FileUrl
        // (clients fetch FileUrl unsigned per spec, so the old default broke the document
        // download). This suite is the end-to-end regression gate for that fix.
        ["AppHost:OnlyOfficeProofValidation"] = "true",
    };

    /// <inheritdoc />
    protected override string ReadinessProbePath => "/healthcheck";

    /// <inheritdoc />
    protected override TimeSpan StartupTimeout => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override TimeSpan ReadinessTimeout => TimeSpan.FromMinutes(3);

    /// <inheritdoc />
    protected override async Task<bool> IsReadyResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var body = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        return body.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Captures the ONLYOFFICE container's logs for failure diagnostics: the docker-logs tail plus a
    /// tail of the internal docservice/converter logs (where WOPI download / proof / edit-mode
    /// errors are actually written — they don't surface on the container's stdout).
    /// </summary>
    public override async Task<string> CaptureClientLogsAsync()
    {
        const int tailLines = 200;
        try
        {
            var (names, _) = await RunProcessAsync("docker", "ps --filter ancestor=onlyoffice/documentserver --format {{.Names}}").ConfigureAwait(false);
            var container = names.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(container))
            {
                return "<no running onlyoffice/documentserver container>";
            }

            var (dockerLogs, dockerLogsErr) = await RunProcessAsync("docker", $"logs --tail {tailLines} {container}").ConfigureAwait(false);
            // The interesting WOPI diagnostics live in the docservice logs inside the container.
            var (docservice, _) = await RunProcessAsync(
                "docker",
                $"exec {container} sh -lc \"tail -n {tailLines} /var/log/onlyoffice/documentserver/docservice/out.log /var/log/onlyoffice/documentserver/docservice/err.log /var/log/onlyoffice/documentserver/converter/err.log 2>/dev/null\"")
                .ConfigureAwait(false);

            static string Cap(string s, int max) => s.Length <= max ? s : "…(truncated)…\n" + s[^max..];

            return $"=== {container} docker logs (tail {tailLines}) ===\n{Cap(dockerLogs + dockerLogsErr, 6000)}\n" +
                   $"=== {container} docservice/converter logs (tail {tailLines}) ===\n{Cap(docservice, 8000)}";
        }
        catch (Exception ex)
        {
            return $"<CaptureClientLogsAsync threw: {ex.GetType().Name}: {ex.Message}>";
        }
    }
}
