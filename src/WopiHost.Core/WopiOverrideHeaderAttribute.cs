namespace WopiHost.Core;

/// <summary>
/// An action constraint based on the X-WOPI-Override header.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class WopiOverrideHeaderAttribute : HttpHeaderAttribute
{
    /// <summary>
    /// Creates an instance of the header-based constraint based on allowed values.
    /// </summary>
    /// <param name="values">Accepted header values.</param>
    public WopiOverrideHeaderAttribute(string[] values) : base(WopiHeaders.WOPI_OVERRIDE, values)
    {
    }
}
