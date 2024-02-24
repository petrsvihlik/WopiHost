using Serilog;
using WopiHost.Core;

namespace WopiHost;

/// <summary>
/// Contains logging helper methods.
/// </summary>
public static class LogHelper
{
    /// <summary>
    /// Adds WOPI diagnostic codes to the diagnostic context.
    /// </summary>
    /// <param name="diagnosticContext">Serilog's diagnostic context.</param>
    /// <param name="httpContext">HTTP context instance</param>
    public static void EnrichWithWopiDiagnostics(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(diagnosticContext);

        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;

        if(request.Headers.TryGetValue(WopiHeaders.CORRELATION_ID, out var correlationId))
        {
            diagnosticContext.Set(nameof(WopiHeaders.CORRELATION_ID), correlationId.First());
        }

        if (request.Headers.TryGetValue(WopiHeaders.SESSION_ID, out var sessionId))
        {
            diagnosticContext.Set(nameof(WopiHeaders.SESSION_ID), sessionId.First());
        }
    }
}
