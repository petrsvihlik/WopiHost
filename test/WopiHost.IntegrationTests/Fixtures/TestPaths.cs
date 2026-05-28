using System.Reflection;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>Filesystem paths used by integration test factories.</summary>
internal static class TestPaths
{
    /// <summary>Absolute path to the sample/wopi-docs directory at the repo root, used as the WOPI storage root.</summary>
    public static string WopiDocsRoot { get; } = ResolveWopiDocsRoot();

    private static string ResolveWopiDocsRoot()
    {
        // Walk up from the test bin directory to the repo root, then into sample/wopi-docs.
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var dir = new DirectoryInfo(assemblyDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WOPI.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repo root (WOPI.slnx) walking up from test bin.");
        }
        return Path.Combine(dir.FullName, "sample", "wopi-docs");
    }
}
