using System.Diagnostics;

namespace WopiHost.E2ETests.Collabora.Fixtures;

/// <summary>
/// Probe for whether a usable Docker engine is reachable from this process. The Collabora e2e
/// fixture short-circuits with a skip when this returns <see langword="false"/> — contributor
/// machines without Docker Desktop running shouldn't see test failures, just a clean skip.
/// </summary>
internal static class DockerCheck
{
    /// <summary>
    /// Runs <c>docker info --format {{.ServerVersion}}</c> with a short timeout and treats a
    /// non-empty server version as "Docker is available". A missing CLI, a Windows-only client
    /// without a running engine, or a context pointing at an offline daemon all surface here.
    /// </summary>
    /// <remarks>
    /// The check is intentionally minimal — it only answers "can Aspire start containers right
    /// now". It does <em>not</em> verify Linux-container mode, Docker Compose, image pull
    /// permissions, or any other capability. A more specific path failing downstream should
    /// surface the real error rather than be masked by a coarser preflight.
    /// </remarks>
    public static bool IsDockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format {{.ServerVersion}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }
            if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }
            if (process.ExitCode != 0)
            {
                return false;
            }
            return !string.IsNullOrWhiteSpace(process.StandardOutput.ReadToEnd());
        }
        catch
        {
            // FileNotFoundException for missing CLI, Win32Exception for ACL issues — either way,
            // "Docker not usable". The defensive catch keeps the test class from blowing up on
            // environments where the probe itself throws.
            return false;
        }
    }
}
