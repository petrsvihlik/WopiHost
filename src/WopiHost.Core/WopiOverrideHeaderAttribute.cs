namespace WopiHost.Core;

/// <summary>
/// An action constraint based on the X-WOPI-Override header.
/// </summary>
/// <remarks>
/// Creates an instance of the header-based constraint based on allowed values.
/// </remarks>
/// <param name="values">Accepted header values.</param>
[AttributeUsage(AttributeTargets.Method)]
public class WopiOverrideHeaderAttribute(string[] values) : HttpHeaderAttribute(WopiHeaders.WOPI_OVERRIDE, values)
{
}
