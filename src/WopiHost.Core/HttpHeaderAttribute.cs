using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace WopiHost.Core;

/// <summary>
/// A header-based constraint for HTTP actions.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HttpHeaderAttribute : Attribute, IActionConstraint
{
    private string Header { get; set; }

    private string[] Values { get; set; }

    /// <summary>
    /// Creates an instance of a constraint based on a header name and allowed values.
    /// </summary>
    /// <param name="header">Header name to check.</param>
    /// <param name="values">Accepted header values.</param>
    public HttpHeaderAttribute(string header, params string[] values)
    {
        Header = header;
        Values = values;
    }

    /// <inheritdoc />
    public bool Accept(ActionConstraintContext context)
    {
        return (context is not null) && context.RouteContext.HttpContext.Request.Headers.TryGetValue(Header, out var value) && Values.Contains(value[0]);
    }

    /// <inheritdoc />
    public int Order => 0;
}
