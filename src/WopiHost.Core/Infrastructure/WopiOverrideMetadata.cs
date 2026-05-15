using System.Collections.Frozen;
using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Endpoint metadata declaring that an endpoint participates in <see cref="WopiHeaders.WOPI_OVERRIDE"/>-based
/// dispatch. Only requests carrying one of <see cref="Values"/> as the header value reach the
/// endpoint; all others are invalidated as candidates by <see cref="WopiOverrideMatcherPolicy"/>.
/// Endpoints without this metadata pass through the policy untouched (e.g., GET endpoints sharing
/// the same route template).
/// </summary>
/// <remarks>
/// <para>
/// The WOPI protocol multiplexes mutating operations through a single <c>POST {id}</c> route on
/// both the Files and Containers controllers, distinguished only by the <c>X-WOPI-Override</c>
/// header (<c>LOCK</c>, <c>UNLOCK</c>, <c>PUT</c>, <c>RENAME_FILE</c>, <c>DELETE</c>, …). This
/// metadata is the Minimal API equivalent of the <see cref="HttpHeaderAttribute"/>
/// <see cref="Microsoft.AspNetCore.Mvc.ActionConstraints.IActionConstraint"/> currently used on
/// the MVC controllers — see <see cref="WopiOverrideHeaderAttribute"/>.
/// </para>
/// <para>
/// String comparison is <see cref="StringComparer.Ordinal"/> to match the existing controller
/// behaviour (<see cref="HttpHeaderAttribute.Accept"/> uses default string equality). The WOPI
/// spec defines override values as upper-case constants in <see cref="WopiFileOperations"/> /
/// <see cref="WopiContainerOperations"/> / <see cref="WopiEcosystemOperations"/>.
/// </para>
/// </remarks>
public sealed class WopiOverrideMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WopiOverrideMetadata"/> class with the
    /// supplied set of accepted <c>X-WOPI-Override</c> header values.
    /// </summary>
    /// <param name="values">The header values that should dispatch to the endpoint this metadata
    /// is attached to. At least one value is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public WopiOverrideMetadata(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }
        Values = values.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>The header values that route requests to this endpoint.</summary>
    public FrozenSet<string> Values { get; }
}
