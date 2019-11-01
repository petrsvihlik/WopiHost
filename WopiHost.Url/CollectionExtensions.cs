using System.Collections.Generic;
using System.Linq;

namespace WopiHost.Url
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Merges two dictionaries. If duplicate occurs, <param name="dictA"></param> wins over <param name="dictB" />.
        /// </summary>
        /// <returns></returns>
        public static IDictionary<TKey, TValue> Merge<TKey, TValue>(this IDictionary<TKey, TValue> dictA, IDictionary<TKey, TValue> dictB) where TValue : class
        {
            if (dictA == null)
            {
                return dictB;
            }
            if (dictB == null)
            {
                return dictA;
            }
            return dictA.Keys.Union(dictB.Keys).ToDictionary(k => k, k => dictA.ContainsKey(k) ? dictA[k] : dictB[k]);
        }
    }
}
