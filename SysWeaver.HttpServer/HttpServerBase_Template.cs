using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// The cached text template state of a local url (the parsed template, if it uses dynamic variables, and the etags it was built for).
        /// Lock free, the state is replaced atomically.
        /// </summary>
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

            /// <summary>
            /// Get the cached template if it was built from the same source data.
            /// </summary>
            /// <param name="createTemplate">True if the template must be (re)built from the source data</param>
            /// <param name="createTranslation">True if the template is valid but the translation for <paramref name="language"/> must be built</param>
            /// <param name="isDynamic">True if the template uses dynamic variables</param>
            /// <param name="languageTemplate">The language template, if any</param>
            /// <param name="lastModified">The etag of the source data (optionally followed by a space and the language)</param>
            /// <param name="language">The session language</param>
            /// <returns>The cached template, null if it must be created or the source isn't a template (no variables)</returns>
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

            /// <summary>
            /// Replace the cached template.
            /// </summary>
            /// <param name="temp">The template, null if the source has no variables</param>
            /// <param name="isDynamic">True if the template uses dynamic variables</param>
            /// <param name="lastModified">The etag of the source data (optionally followed by a space and the language)</param>
            /// <param name="languageTemplate">The language template, if any</param>
            /// <param name="language">The language the template was translated for</param>
            public void Set(TextTemplate temp, bool isDynamic, String lastModified, LanguageTemplate languageTemplate, String language)
            {
                Interlocked.Exchange(ref Current, new Data(isDynamic, temp, lastModified, languageTemplate, language));
            }

            /// <summary>
            /// Record that a translation for a language has been built for the current template.
            /// </summary>
            /// <param name="lastModified">The etag including the language</param>
            /// <param name="language">The language</param>
            public void SetLangLm(String lastModified, String language)
            {
                var clm = Current.LangLms;
                if (clm == null)
                    return;
                clm.TryAdd(language, lastModified);
            }
        }


        /// <summary>
        /// A reference counted template match pattern (compiled to a regular expression).
        /// </summary>
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
        /// Add a template match pattern (reference counted, adding the same pattern again increments the count).
        /// If a local url matches a template pattern, variable substitution ("${Name}") will be performed within the data (must be UTF-8 text).
        /// Patterns can use wildcards '*' (matches zero or more chars) or '?' (matches one).
        /// If the pattern starts with '$' the rest of the pattern is a regular expression.
        /// If the pattern starts with '#' the match should be case insensitive.
        /// If the pattern starts with '$#' the rest of the pattern is a regular expression matched case insensitive.
        /// </summary>
        /// <param name="matchPattern">The pattern</param>
        /// <remarks>Whether a url is a template is cached per local url the first time it's requested, so adding a pattern doesn't affect urls that have already been served.
        /// Query string parameters are available as variables in templates and take precedence over all other variables (see <see cref="GetVars"/>).</remarks>
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
        /// Remove a previously added template match pattern (decrements the reference count, removed when it reaches zero).
        /// </summary>
        /// <param name="matchPattern">The pattern</param>
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

        /// <summary>
        /// Get (or create) the template state for a local url, the result (including "not a template") is cached per local url forever.
        /// </summary>
        /// <param name="localUrl">The local url</param>
        /// <param name="varTemplateAllowed">Not used</param>
        /// <param name="force">True to create a template state even if no pattern matches (language templates)</param>
        /// <returns>The template state, null if the url isn't a template</returns>
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
