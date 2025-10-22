using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        sealed class Temp
        {
            public Temp(bool isVarTemplate)
            {
                IsVarTemplate = isVarTemplate;
            }

            /// <summary>
            /// Template is matching the pattern for file templates
            /// </summary>
            public readonly bool IsVarTemplate;


            sealed class Data
            {
                public readonly bool IsDynamic;
                public readonly TextTemplate T;
                public readonly String Lm;
                public readonly LanguageTemplate LanguageTemplate;
                public readonly ConcurrentDictionary<String, String> LangLms;



                public Data(bool isDynamic, TextTemplate t, string lm, LanguageTemplate languageTemplate, String language)
                {
                    IsDynamic = isDynamic;
                    T = t;
                    Lm = lm.SplitFirst(' ');
                    LanguageTemplate = languageTemplate;
                    if (languageTemplate != null)
                    {
                        var x = new ConcurrentDictionary<String, String>(StringComparer.Ordinal);
                        x.TryAdd(language, lm);
                        LangLms = x;
                    }
                }
            }

            volatile Data Current;

            public TextTemplate Get(out bool createTemplate, out bool createTranslation, out bool isDynamic, out LanguageTemplate languageTemplate, String lastModified, String language)
            {
                var c = Current;
                if (c != null)
                {
                    var r = c.T;
                    var lm = c.Lm;
                    var llms = c.LangLms;
                    if (llms != null)
                        llms.TryGetValue(language, out lm);
                    if (lastModified.FastEquals(lm))
                    {
                        isDynamic = c.IsDynamic;
                        createTemplate = false;
                        languageTemplate = c.LanguageTemplate;
                        createTranslation = false;
                        return r;
                    }
                    else
                    {
                        if (lastModified.SplitFirst(' ').FastEquals(c.Lm))
                        {
                            isDynamic = c.IsDynamic;
                            createTemplate = false;
                            languageTemplate = c.LanguageTemplate;
                            createTranslation = r != null;
                            return r;
                        }
                    }
                }
                createTemplate = true;
                createTranslation = true;
                languageTemplate = null;
                isDynamic = false;
                return null;
            }

            public void Set(TextTemplate temp, bool isDynamic, String lastModified, LanguageTemplate languageTemplate, String language)
            {
                Interlocked.Exchange(ref Current, new Data(isDynamic, temp, lastModified, languageTemplate, language));
            }

            public void SetLangLm(String lastModified, String language)
            {
                var c = Current;
                c.LangLms.TryAdd(language, lastModified);
            }
        }


        sealed class Matches
        {

            public override string ToString() => String.Concat('"', RegEx, "\" @ ", RefCount);


            public void IncRef() => Interlocked.Increment(ref RefCount);
            public bool DecRef() => Interlocked.Decrement(ref RefCount) == 0;

            int RefCount;

            public readonly Regex RegEx;

            public Matches(String value)
            {
                RefCount = 1;
                bool useRegEx = value.StartsWith('$');
                if (useRegEx)
                    value = value.Substring(1);
                bool caseInsensitive = value.StartsWith('#');
                if (caseInsensitive)
                    value = value.Substring(1);
                RegexOptions opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;
                if (caseInsensitive)
                    opt |= RegexOptions.IgnoreCase;
                if (!useRegEx)
                    value = "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
                RegEx = new Regex(value, opt);
            }
        }


        readonly ConcurrentDictionary<String, Temp> TextTemplates = new ConcurrentDictionary<string, Temp>(StringComparer.Ordinal);
        readonly ConcurrentDictionary<String, Matches> Templates = new ConcurrentDictionary<string, Matches>(StringComparer.Ordinal);

        /// <summary>
        /// Add a template match pattern.
        /// If a localurl matches a a template, variable substitution will be performed within the data.
        /// Patterns can use wildcards '*' (matches one or more) or '?' (matches one).
        /// If the pattern starts with '$' the rest of the pattern is a regular expression.
        /// If the pattern starts with '#' the match should be case insensitive.
        /// If the pattern starts with '$#' the rest of the pattern is a regular expression matched case insensitive.
        /// </summary>
        /// <param name="matchPattern"></param>
        public void AddTemplateMatch(String matchPattern)
        {
            var r = Templates;
            lock (r)
            {
                if (r.TryGetValue(matchPattern, out var x))
                {
                    x.IncRef();
                    return;
                }
                x = new Matches(matchPattern);
                r.TryAdd(matchPattern, x);
            }
        }

        /// <summary>
        /// Remove a previously added template match pattern.
        /// </summary>
        /// <param name="matchPattern"></param>
        public void RemoveTemplateMatch(String matchPattern)
        {
            var r = Templates;
            lock (r)
            {
                if (!r.TryGetValue(matchPattern, out var x))
                    return;
                if (!x.DecRef())
                    return;
                r.TryRemove(matchPattern, out x);
            }
        }

        Temp GetTextTemplate(String localUrl, bool varTemplateAllowed, bool force)
        {
            var c = TextTemplates;
            if (c.TryGetValue(localUrl, out var t))
                return t;
            foreach (var x in Templates.Values)
            {
                bool isVarTemplate = x.RegEx.Match(localUrl).Success;
                if (force || isVarTemplate)
                {
                    t = new Temp(isVarTemplate);
                    if (!c.TryAdd(localUrl, t))
                        if (!c.TryGetValue(localUrl, out t))
                            throw new Exception("Internal error!");
                    return t;
                }
            }
            c.TryAdd(localUrl, null);
            return null;
        }



    }
}
