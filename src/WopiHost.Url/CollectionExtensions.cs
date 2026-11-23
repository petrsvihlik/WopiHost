namespace WopiHost.Url;

/// <summary>
/// Provides helper methods for collections of objects.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Merges two dictionaries. When a key occurs in both, the value from <paramref name="dictA"/> wins.
    /// </summary>
    /// <typeparam name="TKey">The type of the dictionary keys.</typeparam>
    /// <typeparam name="TValue">The type of the dictionary values.</typeparam>
    /// <param name="dictA">The dictionary whose values take precedence.</param>
    /// <param name="dictB">The dictionary that supplies values for keys missing from <paramref name="dictA"/>.</param>
    /// <returns>
    /// A new dictionary with the keys of both inputs; the other input itself if one of them is <see langword="null"/>;
    /// or <see langword="null"/> if both are <see langword="null"/>.
    /// </returns>
    public static IDictionary<TKey, TValue>? Merge<TKey, TValue>(this IDictionary<TKey, TValue>? dictA, IDictionary<TKey, TValue>? dictB)
        where TKey : notnull
        where TValue : class
    {
        if (dictA is null)
        {
            return dictB;
        }
        if (dictB is null)
        {
            return dictA;
        }
        return dictA.Keys.Union(dictB.Keys).ToDictionary(k => k, k => dictA.TryGetValue(k, out TValue? value) ? value : dictB[k]);
    }
}
