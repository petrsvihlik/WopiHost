namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child object of an arbitrary type.
/// </summary>
/// <param name="Name">Name of the object.</param>
/// <param name="Url">URL pointing to the object.</param>
#pragma warning disable CA1056 // URI-like properties should not be strings
public abstract record AbstractChildBase(string Name, string Url);
#pragma warning restore CA1056 // URI-like properties should not be strings
