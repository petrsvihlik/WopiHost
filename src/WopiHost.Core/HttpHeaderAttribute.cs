using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace WopiHost.Core;

/// <summary>
/// A header-based constraint for HTTP actions.
/// </summary>
/// <remarks>
/// Creates an instance of a constraint based on a header name and allowed values.
/// </remarks>
/// <param name="header">Header name to check.</param>
/// <param name="values">Accepted header values.</param>
[AttributeUsage(AttributeTargets.Method)]
public class HttpHeaderAttribute(string header, params string[] values) : Attribute, IActionConstraint
{
    private string Header { get; set; } = header;

    private string[] Values { get; set; } = values;

    /// <inheritdoc />
    public bool Accept(ActionConstraintContext context) => (context is not null) && context.RouteContext.HttpContext.Request.Headers.TryGetValue(Header, out var value) && Values.Contains(value[0]);

    /// <inheritdoc />
    public int Order => 0;
}
