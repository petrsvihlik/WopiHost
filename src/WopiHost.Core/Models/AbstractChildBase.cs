namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child object of an arbitrary type.
/// </summary>
/// <param name="Name">Name of the object.</param>
/// <param name="Url">URL pointing to the object.</param>
public abstract record AbstractChildBase(string Name, Uri Url);
