using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SysWeaver.Knowledge
{

    public static class AllInfo
    {
        /// <summary>
        /// Try to get information given a keyword
        /// </summary>
        /// <param name="keyword">The keyword, preferable lowercased</param>
        /// <param name="info">Information if any is found</param>
        /// <returns>True if any information is found about the keyword</returns>
        public static bool TryGet(String keyword, out Info info) => Tags.TryGetValue(keyword.FastToLower(), out info);


        /// <summary>
        /// Try to get information with the supplied name
        /// </summary>
        /// <param name="name">The information name</param>
        /// <param name="info">Information if any is found</param>
        /// <returns>True if any information was found with the supplied name</returns>
        public static bool TryGetExactInfo(String name, out Info info) => UniqueTag.TryGetValue(name, out info);

        /// <summary>
        /// All keyword/info pairs
        /// </summary>
        public static IEnumerable<KeyValuePair<String, Info>> All => Tags;

        /// <summary>
        /// Number of keyword/info pairs
        /// </summary>
        public static int Count => Tags.Count;

        /// <summary>
        /// Number of info records
        /// </summary>
        public static int InfoCount => UniqueTag.Count;

        static AllInfo()
        {
            Sink.Data += InfoCommon.TagRetro.GetHashCode();
            Sink.Data += Years.Count;
            Sink.Data += Countries.Count;
            Sink.Data += Cities.Count;
            Sink.Data += Sports.Count;
            Sink.Data += Places.Count;
            Sink.Data += Artists.Count;
            Sink.Data += Persons.Count;
            Sink.Data += HistoricalEvents.Count;
            Sink.Data += Movies.Count;
            Sink.Data += Series.Count;
            Sink.Data += ArtStyles.Count;
            Sink.Data += Animals.Count;
            Sink.Data += CarBrands.Count;

        }


        static volatile InfoSearcher Sr;
        static readonly Object Lock = new object();

        /// <summary>
        /// A searcher containing everything
        /// </summary>
        public static InfoSearcher Searcher
        {
            get
            {
                var s = Sr;
                if (s != null)
                    return s;
                lock (Lock)
                {
                    s = Sr;
                    if (s != null)
                        return s;
                    s = new InfoSearcher(Tags.Freeze(Tags.Comparer));
                    Sr = s;
                }
                return s;
            }
        }


        public static bool IsMultiple(String s)
        {
            var l = s.Length - 1;
            if (s[l] != 's')
                return false;
            var p = s[l - 1];
            switch (p)
            {
                case 's':
                case 'u':
                case 'i':
                case 'o':
                    return false;
            }
            return true;
        }

        internal static void TryAdd(String tagName, Info tag, bool canBeSpecific, bool canBeMultiple)
        {
            var key = tagName.FastToLower();
            var kl = key.Length;
            if (kl < 2)
                return;
            InternalAddOne(key, tag);
            --kl;
            var last = key[kl];
            if (canBeSpecific)
            {
                if (last != 's')
                    InternalAddOne(key + "s", tag);
            }
            if (canBeMultiple)
            {
                if (last != 's')
                {
                    switch (last)
                    {
                        case 'x':
                            InternalAddOne(key + "es", tag);
                            break;
                        case 'y':
                            InternalAddOne(key.Substring(0, kl) + "ies", tag);
                            break;
                        default:
                            InternalAddOne(key + "s", tag);
                            break;
                    }
                }
                else
                {
                    var pp = key[kl - 1];
                    switch (pp)
                    {
                        case 'e':
                        case 'a':
                            break;
                        default:
                            InternalAddOne(key + "es", tag);
                            break;
                    }
                }
            }
        }

        static void InternalAddOne(String key, Info tag)
        {
            var tags = Tags;
            if (tags.TryGetValue(key, out var _))
                return;
            tags.TryAdd(key, tag);
            UniqueTag.TryAdd(tag.Name, tag);
        }




        static readonly ConcurrentDictionary<String, Info> Tags = new ConcurrentDictionary<string, Info>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, Info> UniqueTag = new ConcurrentDictionary<string, Info>(StringComparer.Ordinal);

    }


}
