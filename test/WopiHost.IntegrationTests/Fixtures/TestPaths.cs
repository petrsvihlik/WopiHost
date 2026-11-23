using System.Reflection;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>Provides file-system paths used by integration test factories.</summary>
internal static class TestPaths
{
    /// <summary>Gets the absolute path to the <c>sample/wopi-docs</c> directory at the repo root, used as the WOPI storage root.</summary>
    public static string WopiDocsRoot { get; } = ResolveWopiDocsRoot();

    private static string ResolveWopiDocsRoot()
    {
        // Walk up from the test bin directory to the repo root, then into sample/wopi-docs.
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var dir = new DirectoryInfo(assemblyDir);
        while (dir is not null && !File.Exists(Path.Join(dir.FullName, "WOPI.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repo root (WOPI.slnx) walking up from test bin.");
        }
        return Path.Join(dir.FullName, "sample", "wopi-docs");
    }
}
