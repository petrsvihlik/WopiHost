namespace WopiHost.Core.Models;

/// <summary>
/// Represents a pointer to a child container.
/// </summary>
/// <param name="Name">The name of the container.</param>
/// <param name="Url">The WopiSrc URL of the container.</param>
public record ChildContainer(string Name, Uri Url) : AbstractChildBase(Name, Url);
