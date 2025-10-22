using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Knowledge
{
    public sealed class InfoSearcher
    {
        public InfoSearcher(IReadOnlyDictionary<String, Info> infoCollection)
        {
            InfoLookup = infoCollection;
            T = StringTree.Build(infoCollection.Select(y => y.Key), true);
            var k = KeywordLookup;
            foreach (var x in infoCollection)
            {
                var key = x.Value;
                if (!k.TryGetValue(key, out var l))
                {
                    l = new List<string>();
                    k.Add(key, l);
                }
                l.Add(x.Key);
            }
        }

        readonly IReadOnlyDictionary<String, Info> InfoLookup;
        readonly Dictionary<Info, List<String>> KeywordLookup = new Dictionary<Info, List<string>>();

        /// <summary>
        /// Number of keywords that the searcher is looking for
        /// </summary>
        public long KeywordCount => InfoLookup.Count;
        
        /// <summary>
        /// Number of unique information pieces (not counting parents that aren't directly linked to a keyword)
        /// </summary>
        public long InfoCount => KeywordLookup.Count;


        /// <summary>
        /// Get the keywords that lead to the specified info
        /// </summary>
        /// <param name="i">The information, the keywords that maps to this information</param>
        /// <returns>List of keywords</returns>
        public IReadOnlyList<String> GetKeyWords(Info i) => KeywordLookup.TryGetValue(i, out var l) ? l : [];


        /// <summary>
        /// Get all information found for a given text, roughly in the order of importance
        /// </summary>
        /// <param name="text">The text to search for information</param>
        /// <param name="expand">If true, the information is expanded (i.e parent information is included)</param>
        /// <param name="custom">Optionally search for key phrases in this tree too</param>
        /// <param name="getCustomInfo">If the custom tree returns a key phrase, this function is used to get the associated information</param>
        /// <returns>All information found</returns>
        public IEnumerable<Info> GetInfo(String text, bool expand = true, StringTree custom = null, Func<String, Info> getCustomInfo = null)
        {
            if (!String.IsNullOrEmpty(text))
            {
                bool prevIsLetter = false;
                var l = text.Length;
                var t = T;
                var il = InfoLookup;
                HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
                HashSet<Info> seenInfo = new HashSet<Info>();
                for (int i = 0; i < l; ++ i)
                {
                    var c = text[i];
                    var isP = Char.IsLetterOrDigit(c);
                    if (!prevIsLetter)
                    {
                        if (isP)
                        {
                            var all = t.AllStartsWithAny(text, i);
                            all.Reverse();
                            foreach (var s in all)
                            {
                                var sl = s.Length;
                                var p = i + sl;
                                if ((p >= l) || (!Char.IsLetterOrDigit(text[p])))
                                {
                                    var key = s.FastToLower();
                                    if (seen.Add(key))
                                    {
                                        if (il.TryGetValue(key, out var info))
                                        {
                                            if ((!info.IsName) || Char.IsUpper(text[i]))
                                            {
                                                if (seenInfo.Add(info))
                                                {
                                                    yield return info;
                                                    if (expand)
                                                    {
                                                        var pp = info.Parents;
                                                        if (pp != null)
                                                        {
                                                            foreach (var pi in pp)
                                                            {
                                                                if (seenInfo.Add(pi))
                                                                    yield return pi;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (custom != null)
                            {
                                all = custom.AllStartsWithAny(text, i);
                                foreach (var s in all)
                                {
                                    var sl = s.Length;
                                    var p = i + sl;
                                    if ((p >= l) || (!Char.IsLetterOrDigit(text[p])))
                                    {
                                        var key = s.FastToLower();
                                        if (seen.Add(key))
                                        {
                                            var info = getCustomInfo(key);
                                            if (info != null)
                                            {
                                                if ((!info.IsName) || Char.IsUpper(text[i]))
                                                {
                                                    if (seenInfo.Add(info))
                                                    {
                                                        yield return info;
                                                        if (expand)
                                                        {
                                                            var pp = info.Parents;
                                                            if (pp != null)
                                                            {
                                                                foreach (var pi in pp)
                                                                {
                                                                    if (seenInfo.Add(pi))
                                                                        yield return pi;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    prevIsLetter = isP;
                }

            }
        }


        public IEnumerable<Info> FindFirst(out int pos, String text)
        {
            pos = -1;
            if (String.IsNullOrEmpty(text))
                return [];
            var t = T;
            List<String> f = null;
            int pp = -1;
            StringTools.OnWordStart(text, i =>
            {
                f = t.AllStartsWithAny(text, i);
                if (f.Count > 0)
                {
                    pp = i;
                    return false;
                }
                return true;
            });
            if (f == null)
                return [];
            f.Reverse();
            pos = pp;
            var il = InfoLookup;
            return f.Select(x =>
            {
                il.TryGetValue(x, out var p);
                return p;
            }).Where(x => x != null);
        }

        public Info FindFirst(out int pos, String text, Func<Info, bool> predicate)
        {
            pos = -1;
            if (String.IsNullOrEmpty(text))
                return null;
            var t = T;
            Info res = null;
            int pp = -1;
            var il = InfoLookup;
            StringTools.OnWordStart(text, i =>
            {
                foreach (var x in t.AllStartsWithAny(text, i))
                {
                    if (!il.TryGetValue(x, out var p))
                        continue;
                    if (predicate(p))
                    {
                        res = p;
                        pp = i;
                        return false;
                    }
                }
                return true;
            });
            pos = pp;
            return res;
        }

        public readonly StringTree T;

    }


}
