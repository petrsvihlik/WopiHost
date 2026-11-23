namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Provides the diagnostic identifiers used by <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>
/// to mark WopiHost APIs that are intentionally unstable or reserved for future use.
/// </summary>
/// <remarks>
/// To call an API marked with one of these IDs, callers must opt in by suppressing the
/// corresponding compiler warning, e.g. <c>#pragma warning disable WOPIHOST001</c>.
/// </remarks>
public static class WopiDiagnostics
{
    /// <summary>
    /// <c>WOPIHOST001</c>: reserved for the <c>GetFileWopiSrc</c> ecosystem operation, which the WOPI
    /// spec says clients should not call at this time. This host does not implement it, and no API
    /// currently carries this ID.
    /// </summary>
    public const string GetFileWopiSrcReserved = "WOPIHOST001";
}
