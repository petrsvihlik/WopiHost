using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Attribute for binding request body to a string (.NET FromBody binding only accepts json)
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class FromStringBodyAttribute : ModelBinderAttribute
{
    /// <summary>
    /// Creates a new instance of the <see cref="FromStringBodyAttribute"/> using our <see cref="FromStringBodyModelBinder"/>
    /// </summary>
    public FromStringBodyAttribute() : base(typeof(FromStringBodyModelBinder))
    {
    }
}
