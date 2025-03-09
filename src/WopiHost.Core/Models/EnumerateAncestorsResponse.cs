namespace WopiHost.Core.Models;

/// <summary>
/// EnumerateAncestors operation response.
/// </summary>
/// <param name="AncestorsWithRootFirst"></param>
public record EnumerateAncestorsResponse(IEnumerable<ChildContainer> AncestorsWithRootFirst);