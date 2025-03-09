namespace WopiHost.Core.Models;

/// <summary>
/// Object containing information pointing to a container.
/// </summary>
/// <param name="Name">Name of the object.</param>
/// <param name="Url">URL pointing to the object.</param>
public record ChildContainer(string Name, string Url) : AbstractChildBase(Name, Url);
