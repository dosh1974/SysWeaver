using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver
{
    /// <summary>
    /// A set of named text templates read from "key: text" lines (one per line, lines starting with '#' are ignored, "\n" is replaced with a new line).
    /// The source can be a file, an embedded resource or the lines themselves, see <see cref="ManagedTools.GetLines(string, System.Type, System.Action)"/>.
    /// </summary>
    /// <remarks>
    /// Keys are case insensitive. The templates are parsed lazily and reloaded if the source file changes.
    /// Templates can use <see cref="ManagedVars.TextVars"/>.
    /// </remarks>
    public class ManagedTextLookup
    {


        /// <summary>
        /// This can be an embedded resource name or a filename
        /// </summary>
        public String Text { get; set; }

        /// <summary>
        /// Get a text, evaluated with the given variables.
        /// </summary>
        /// <param name="key">The text key</param>
        /// <param name="vars">The variables, if null an <see cref="Overrides"/> value (exact key match) is returned if present</param>
        /// <returns>The text, or null if the key isn't found</returns>
        public String GetText(String key, IReadOnlyDictionary<String, String> vars = null)
        { 
            if (vars == null)
            {
                if (Overrides.TryGetValue(key, out var o))
                    return o;
            }
            return GetTemplate(key)?.Get(vars);
        }

        /// <summary>
        /// Texts that override the templates (case sensitive keys), only used by <see cref="GetText(string, IReadOnlyDictionary{string, string})"/> when no variables are supplied.
        /// </summary>
        public readonly ConcurrentDictionary<String, String> Overrides = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);


        /// <summary>
        /// All texts: the overrides followed by the template texts (unevaluated) of keys that are not overridden.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>> AllTexts
        {
            get
            {
                var seen = new HashSet<String>(StringComparer.Ordinal);
                foreach (var x in Overrides)
                {
                    if (seen.Add(x.Key))
                        yield return x;
                }
                foreach (var x in GetTemplates())
                {
                    if (seen.Add(x.Key))
                        yield return new KeyValuePair<String, String>(x.Key, x.Value.Template);
                }
            }
        }

        /// <summary>
        /// Get the text template for a key
        /// </summary>
        /// <param name="key">The text key (case insensitive)</param>
        /// <returns>A text template, or null if not found</returns>
        public TextTemplate GetTemplate(String key)
        {
            var t = GetTemplates();
            if (t == null)
                return null;
            return t.TryGetValue(key.FastToLower(), out var s) ? s : null;
        }

        /// <summary>
        /// Get all text templates, loading them if needed
        /// </summary>
        /// <returns>The templates, key is the lower cased text key</returns>
        public IReadOnlyDictionary<String, TextTemplate> GetTemplates()
        {
            var t = Templates;
            if (t != null)
                return t;
            lock (this)
            {
                t = Templates;
                if (t != null)
                    return t;
                var lines = ManagedTools.GetLines(Text, GetType(), () => Templates = null);
                Dictionary<String, TextTemplate> map = new Dictionary<string, TextTemplate>(StringComparer.Ordinal);
                foreach (var line in lines)
                {
                    var l = line.Trim();
                    if (l.Length <= 0) 
                        continue;
                    if (l[0] == '#')
                        continue;
                    var kv = l.IndexOf(':');
                    if (kv < 0)
                        continue;
                    var key = l.Substring(0, kv).FastTrimEndToLower();
                    var value = l.Substring(kv + 1).Replace("\\n", "\n").TrimStart();
                    map[key] = new TextTemplate(value, ManagedVars.TextVars, true);
                }
                t = map.Freeze();
                Templates = t;
                return t;
            }

        }

        volatile IReadOnlyDictionary<String, TextTemplate> Templates;


        /// <summary>
        /// Create an empty lookup, set <see cref="Text"/> before use.
        /// </summary>
        public ManagedTextLookup()
        {
        }

        /// <summary>
        /// Create a lookup and load the templates immediately.
        /// </summary>
        /// <param name="text">A file name, embedded resource name or the actual "key: text" lines</param>
        public ManagedTextLookup(String text)
        {
            Text = text;
            Templates = null;
            GetTemplates();
        }

        /// <summary>
        /// Create a copy that shares the source and the currently loaded templates (but not the overrides).
        /// </summary>
        /// <param name="copy">The lookup to copy</param>
        protected ManagedTextLookup(ManagedTextLookup copy)
        {
            Text = copy.Text;
            Templates = copy.Templates;
        }

    }


}
