using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver
{
    public class ManagedTextLookup
    {


        /// <summary>
        /// This can be an embedded resource name or a filename
        /// </summary>
        public String Text { get; set; }

        /// <summary>
        /// Get text
        /// </summary>
        /// <param name="key">The text key</param>
        /// <param name="vars">The variables</param>
        public String GetText(String key, IReadOnlyDictionary<String, String> vars = null)
        { 
            if (vars == null)
            {
                if (Overrides.TryGetValue(key, out var o))
                    return o;
            }
            return GetTemplate(key)?.Get(vars);
        }

        public readonly ConcurrentDictionary<String, String> Overrides = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);


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
        /// Get the text template for the body
        /// </summary>
        /// <param name="key">The text key</param>
        /// <returns>A text template</returns>
        public TextTemplate GetTemplate(String key)
        {
            var t = GetTemplates();
            if (t == null)
                return null;
            return t.TryGetValue(key.FastToLower(), out var s) ? s : null;
        }

        /// <summary>
        /// Get all texts
        /// </summary>
        /// <returns></returns>
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
                    var key = l.Substring(0, kv).TrimEnd().FastToLower();
                    var value = l.Substring(kv + 1).Replace("\\n", "\n").TrimStart();
                    map[key] = new TextTemplate(value, ManagedVars.TextVars, true);
                }
                t = map.Freeze();
                Templates = t;
                return t;
            }

        }

        volatile IReadOnlyDictionary<String, TextTemplate> Templates;


        public ManagedTextLookup()
        {
        }

        public ManagedTextLookup(String text)
        {
            Text = text;
            Templates = null;
            GetTemplates();
        }

        protected ManagedTextLookup(ManagedTextLookup copy)
        {
            Text = copy.Text;
            Templates = copy.Templates;
        }

    }


}
