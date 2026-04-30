namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Diagnostic identifiers used by <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>
/// to mark WopiHost APIs that are intentionally unstable or reserved for future use.
/// </summary>
/// <remarks>
/// To call an API marked with one of these IDs, callers must opt in by suppressing the
/// corresponding compiler warning, e.g. <c>#pragma warning disable WOPIHOST001</c>.
/// </remarks>
public static class WopiDiagnostics
{
    /// <summary>
    /// <c>WOPIHOST001</c> — marks the <c>GetFileWopiSrc</c> endpoint as reserved for future use.
    /// Microsoft's spec says clients should not call this operation at this time, so the host
    /// returns <c>501 Not Implemented</c> by default.
    /// </summary>
    public const string GetFileWopiSrcReserved = "WOPIHOST001";
}
