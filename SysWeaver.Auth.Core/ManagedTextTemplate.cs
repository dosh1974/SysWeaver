using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver
{

    /// <summary>
    /// A class that managed a text template, the text template can be located on disc or as an embedded resource.
    /// The template is updated if the file is changed.
    /// </summary>
    public sealed class ManagedTextTemplate
    {
        /// <summary>
        /// Create a managed text template
        /// </summary>
        /// <param name="location">Can be a file path, the name of a resource or literal text.
        /// The location is evaluated in the following order:
        /// - If it points to a valid local file, it's read using UTF8 encoding.
        /// - If a type is supplied and an embedded resource exist (raw or compressed using any of the supported compressors), the embedded data is read as UTF8.
        /// - The string is the input string.
        /// </param>
        /// <param name="embeddedResourceType">A type in the assembly where the embedded resource exists</param>
        /// <param name="vars">The variables that the template may use, if null, the EnvInfo.TextVarsCaseInsensitive keys surrounded by [], ex: [AppDisplayName]</param>
        public ManagedTextTemplate(String location, Type embeddedResourceType = null, IReadOnlySet<String> vars = null)
        {
            Location = location ?? "";
            Vars = (vars ?? GetDefVarKeys()).Freeze();
        }

        /// <summary>
        /// Get the current EnvInfo.TextVarsCaseInsensitive variables surrounded by [], ex: [AppDisplayName]
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<String, String> GetDefVars()
        {
            var t = DefVars;
            var src = EnvInfo.TextVarsCaseInsensitive;
            if (t != null)
            {
                if (src == DefVarSource)
                    return t;
            }
            t = new Dictionary<string, string>(src.Select(x => new KeyValuePair<String, String>(String.Join(x.Key, '[', ']'), x.Value)), StringComparer.OrdinalIgnoreCase).Freeze();
            DefVarKeys = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase, t.Select(x => x.Key));
            DefVars = t;
            DefVarSource = src;
            return t;
        }

        /// <summary>
        /// Get the current EnvInfo.TextVarsCaseInsensitive keys surrounded by [], ex: [AppDisplayName]
        /// </summary>
        /// <returns></returns>
        public static IReadOnlySet<String> GetDefVarKeys()
        {
            GetDefVars();
            return DefVarKeys;
        }


        static IReadOnlyDictionary<String, String> DefVarSource;
        static IReadOnlySet<String> DefVarKeys;
        static IReadOnlyDictionary<String, String> DefVars;


        /// <summary>
        /// Resolve a text with the given variables
        /// </summary>
        /// <param name="vars">The variables, if null, the EnvInfo.TextVarsCaseInsensitive keys surrounded by [], ex: [AppDisplayName]</param>
        /// <returns>The resolved text</returns>
        public String GetText(IReadOnlyDictionary<String, String> vars = null)
            => GetTemplate().Get(vars ?? GetDefVars());

        TextTemplate GetTemplate()
        {
            var t = CachedTemplate;
            if (t != null)
                return t;
            lock (this)
            {
                t = CachedTemplate;
                if (t != null)
                    return t;
                t = ManagedTools.GetTemplate(Location, Vars, GetType(), () => CachedTemplate = null);
                CachedTemplate = t;
                return t;
            }
        }
        volatile TextTemplate CachedTemplate;


        public override string ToString() => String.Concat(Location.ToQuoted(), " with vars: ", String.Join(", ", Vars));

        public readonly String Location;

        /// <summary>
        /// All avaiable locations
        /// </summary>
        public readonly IReadOnlySet<String> Vars;

    }


}
