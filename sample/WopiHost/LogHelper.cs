using Serilog;
using WopiHost.Core.Infrastructure;

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

        if(request.Headers.TryGetValue(WopiHeaders.CorrelationId, out var correlationId))
        {
            diagnosticContext.Set(nameof(WopiHeaders.CorrelationId), correlationId.ToString());
        }

        if (request.Headers.TryGetValue(WopiHeaders.SessionId, out var sessionId))
        {
            diagnosticContext.Set(nameof(WopiHeaders.SessionId), sessionId.ToString());
        }
    }
}
