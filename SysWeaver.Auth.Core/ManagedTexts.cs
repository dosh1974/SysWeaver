using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SysWeaver
{
    /// <summary>
    /// Holds a set of localized text templates, one "key: text" file (see <see cref="ManagedTextLookup"/>) per language.
    /// </summary>
    /// <remarks>
    /// For language codes with a region (ex: "en-GB"), the neutral language ("en") is also mapped to the first (alphabetically) regional variant if no neutral folder exists.
    /// </remarks>
    public sealed class ManagedTexts
    {
        /// <summary>
        /// Load localized texts for all language folders found.
        /// </summary>
        /// <param name="sourceFile">A source file pattern.
        /// "*" is used to specify where the subfolders are.".
        /// Ex: "C:\locale\*\MyTexts.txt".".
        /// "C:\Locale" should then include sub-folders named using a language code like: "en-US", "en-GB", "es-MX", "es-ES", "de", "sv".
        /// </param>
        /// <param name="defaultLang">The default language if the supplied language isn't found, null uses "en"</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceFile"/> doesn't contain a '*'.</exception>
        public ManagedTexts(String sourceFile, String defaultLang)
        {
            var languages = new ConcurrentDictionary<String, ManagedLanguageTexts>(StringComparer.Ordinal);
            var i = sourceFile.IndexOf('*');
            String searchFolder = sourceFile.Substring(0, i).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar);
            foreach (var ff in Directory.GetDirectories(searchFolder))
            {
                var lang = Path.GetFileName(ff);
                var f = PathExt.GetFullDirectoryName(sourceFile.Replace("*", lang));
                languages.TryAdd(lang.FastToLower(), new ManagedLanguageTexts(lang, f));
            }
            ManagedLanguageTexts def = null;
            if (languages.Count > 0)
            {
                var langs = languages.OrderBy(x => x.Key).ToList();
                foreach (var lang in langs)
                {
                    lang.Value.GetTemplates();
                    var x = lang.Key.Split('-');
                    if (x.Length <= 1)
                        continue;
                    var key = x[0];
                    if (languages.ContainsKey(key))
                        continue;
                    languages.TryAdd(key, lang.Value);
                }
                var dl = (defaultLang ?? "en").FastToLower();
                foreach (var x in languages)
                {
                    if (x.Key.FastEquals(dl))
                    {
                        def = x.Value;
                        break;
                    }
                }
                if (def == null)
                    languages.TryGetValue("en", out def);
                if (def == null)
                    def = langs.First().Value;
            }
            Default = def == null ? null : new ManagedLanguageTexts(def);
            Languages = languages;
        }

        /// <inheritdoc/>
        public override string ToString() => String.Concat("Default: ", Default, ", languages: ", Languages.Count);

        readonly ConcurrentDictionary<String, ManagedLanguageTexts> Languages = new ConcurrentDictionary<String, ManagedLanguageTexts>(StringComparer.Ordinal);
        
        /// <summary>
        /// Add a language (typically machine translated at runtime) if it doesn't exist.
        /// </summary>
        /// <param name="language">The language code, should be lower case to be found by <see cref="GetLang(string)"/></param>
        /// <param name="data">The texts</param>
        /// <returns>True if added, false if the language already exists</returns>
        public bool TryAddTranslatedLanguage(String language, ManagedLanguageTexts data)
            => Languages.TryAdd(language, data);


        /// <summary>
        /// All languages, key is the lower cased language code (including neutral language aliases).
        /// </summary>
        public IEnumerable<KeyValuePair<String, ManagedLanguageTexts>> AllLanguages => Languages;

        /// <summary>
        /// The language used if the supplied language isn't found (null if there are no languages)
        /// </summary>
        public readonly ManagedLanguageTexts Default;

        /// <summary>
        /// Get the texts for a language, falling back to the neutral language (ex: "en" for "en-US") and then to <see cref="Default"/>.
        /// </summary>
        /// <param name="language">The language code (case insensitive), null returns <see cref="Default"/></param>
        /// <returns>The texts, null only if no languages exist</returns>
        public ManagedLanguageTexts GetLang(String language)
        {
            if (language == null)
                return Default;
            var l = Languages;
            language = language.FastToLower();
            if (l.TryGetValue(language, out var x))
                return x;
            var s = language.Split('-');
            if (s.Length > 1)
                if (l.TryGetValue(s[0], out x))
                    return x;
            return Default;
        }


        /// <summary>
        /// Get the texts for a language, falling back to the neutral language (ex: "en" for "en-US"), without using the default language.
        /// </summary>
        /// <param name="language">The language code (case insensitive)</param>
        /// <returns>The texts, or null if the language isn't available</returns>
        public ManagedLanguageTexts TryGetLang(String language)
        {
            if (language == null)
                return null;
            var l = Languages;
            language = language.FastToLower();
            if (l.TryGetValue(language, out var x))
                return x;
            var s = language.Split('-');
            if (s.Length > 1)
                if (l.TryGetValue(s[0], out x))
                    return x;
            return null;
        }

        /// <summary>
        /// Get the text for a given key and language.
        /// If the language can't be found the default language is used.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="language">The language, ex: "en-US", "en-GB", "es-MX", "es-ES", "de", "sv"</param>
        /// <returns>null if not found, else the text template</returns>
        public TextTemplate GetText(String key, String language)
        {
            var l = GetLang(language);
            return l.GetTemplate(key);
        }

    }
    
}
