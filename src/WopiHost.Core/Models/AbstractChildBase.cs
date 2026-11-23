namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child object of an arbitrary type.
/// </summary>
/// <param name="Name">The name of the object.</param>
/// <param name="Url">The URL pointing to the object.</param>
public abstract record AbstractChildBase(string Name, Uri Url);
