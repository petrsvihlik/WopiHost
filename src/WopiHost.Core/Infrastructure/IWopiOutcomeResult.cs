namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Defines an <see cref="Microsoft.AspNetCore.Http.IResult"/> that carries a
/// specific WOPI telemetry outcome distinct from what the HTTP status code alone would suggest.
/// The canonical example is the lock-mismatch case: <c>409 Conflict</c> is shared by generic
/// conflicts and lock mismatches, but dashboards need them counted separately.
/// </summary>
/// <remarks>
/// Consulted by the telemetry endpoint filter before falling back to status-code-based
/// classification. Implement this on custom <c>IResult</c> types only when status code alone
/// loses information needed downstream.
/// </remarks>
public interface IWopiOutcomeResult
{
    /// <summary>Gets the telemetry outcome: one of the constants on <see cref="WopiTelemetry.Outcomes"/>.</summary>
    string Outcome { get; }
}
