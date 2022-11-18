namespace WopiHost.Url;

/// <summary>
/// Provides helper methods for collections of objects.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Merges two dictionaries. If duplicate occurs, <param name="dictA"></param> wins over <param name="dictB" />.
    /// </summary>
    /// <returns></returns>
    public static IDictionary<TKey, TValue> Merge<TKey, TValue>(this IDictionary<TKey, TValue> dictA, IDictionary<TKey, TValue> dictB) where TValue : class
    {
        if (dictA is null)
        {
            return dictB;
        }
        if (dictB is null)
        {
            return dictA;
        }
        return dictA.Keys.Union(dictB.Keys).ToDictionary(k => k, k => dictA.TryGetValue(k, out TValue value) ? value : dictB[k]);
    }
}
