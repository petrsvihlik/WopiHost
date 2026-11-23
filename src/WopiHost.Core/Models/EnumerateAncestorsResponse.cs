namespace WopiHost.Core.Models;

/// <summary>
/// Represents the response body of the <c>EnumerateAncestors</c> operation.
/// </summary>
/// <param name="AncestorsWithRootFirst">The ancestor containers, with the root container first.</param>
public record EnumerateAncestorsResponse(IEnumerable<ChildContainer> AncestorsWithRootFirst);