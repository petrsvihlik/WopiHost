using System.Diagnostics;

namespace WopiHost.E2ETests.Fixtures;

/// <summary>
/// Probes whether a usable Docker engine is reachable from this process. The e2e app fixtures
/// short-circuit with a skip when this returns <see langword="false"/> — contributor machines
/// without Docker Desktop running shouldn't see test failures, just a clean skip.
/// </summary>
internal static class DockerCheck
{
    /// <summary>
    /// Runs <c>docker info --format {{.ServerVersion}}</c> with a short timeout and treats a
    /// non-empty server version as "Docker is available". A missing CLI, a Windows-only client
    /// without a running engine, or a context pointing at an offline daemon all surface here.
    /// </summary>
    /// <returns><see langword="true"/> if the Docker engine reported a server version; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// The check is intentionally minimal — it only answers "can Aspire start containers right
    /// now". It does <i>not</i> verify Linux-container mode, Docker Compose, image pull
    /// permissions, or any other capability. A more specific path failing downstream should
    /// surface the real error rather than be masked by a coarser preflight.
    /// </remarks>
    public static bool IsDockerAvailable()
    {
        try
        {
            var result = Process.RunAndCaptureText("docker", ["info", "--format", "{{.ServerVersion}}"], TimeSpan.FromSeconds(5));
            return result.ExitStatus is { Canceled: false, ExitCode: 0 }
                && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            // FileNotFoundException for missing CLI, Win32Exception for ACL issues, TimeoutException
            // for a hung daemon — either way, "Docker not usable". The defensive catch keeps the test
            // class from blowing up on environments where the probe itself throws.
            return false;
        }
    }
}
