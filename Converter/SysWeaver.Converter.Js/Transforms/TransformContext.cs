using System;
using System.Collections.Generic;
using System.Text;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// State shared by all transform passes of one conversion
    /// </summary>
    sealed class TransformContext
    {
        public readonly JsConverterParams Params;
        public readonly Diagnostics Diag;
        public readonly JsTarget Target;
        public readonly bool Loose;
        readonly HashSet<JsFeature> Features;
        /// <summary>All identifier names that are in use (source + generated)</summary>
        readonly HashSet<String> Names;
        /// <summary>Helper default name (without _) to the name used in the output</summary>
        public readonly Dictionary<String, String> HelperNames = new Dictionary<String, String>(StringComparer.Ordinal);
        /// <summary>The helpers that are used, in the order they were first requested</summary>
        public readonly List<String> UsedHelpers = new List<String>();
        /// <summary>The features that were actually transformed (something changed)</summary>
        public readonly HashSet<JsFeature> Transformed = new HashSet<JsFeature>();
        /// <summary>The polyfills to add (by name)</summary>
        public readonly List<String> Polyfills = new List<String>();

        public TransformContext(JsConverterParams p, Diagnostics diag, JsTarget target, HashSet<JsFeature> features, HashSet<String> names)
        {
            Params = p;
            Diag = diag;
            Target = target;
            Loose = p.Loose;
            Features = features;
            Names = names;
        }

        /// <summary>
        /// True if the feature should be transformed
        /// </summary>
        public bool Needs(JsFeature f) => Features.Contains(f);

        /// <summary>
        /// Mark a feature as transformed
        /// </summary>
        public void Did(JsFeature f) => Transformed.Add(f);

        /// <summary>
        /// True if a name is in use
        /// </summary>
        public bool IsUsed(String name) => Names.Contains(name);

        /// <summary>
        /// Reserve a name (so that it isn't generated)
        /// </summary>
        public void Reserve(String name) => Names.Add(name);

        /// <summary>
        /// Generate a unique identifier name (not used anywhere in the file), ex: "_ref", "_ref2"
        /// </summary>
        public String UniqueName(String hint)
        {
            var sb = new StringBuilder("_");
            if (!String.IsNullOrEmpty(hint))
            {
                foreach (var c in hint)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '$')
                        sb.Append(c);
                }
                //  Strip leading _ (we add one)
                while (sb.Length > 1 && sb[1] == '_')
                    sb.Remove(1, 1);
            }
            if (sb.Length == 1)
                sb.Append("ref");
            if (sb.Length > 21)
                sb.Length = 21;
            var b = sb.ToString();
            if (Names.Add(b))
                return b;
            for (int i = 2; ; ++i)
            {
                var n = b + i;
                if (Names.Add(n))
                    return n;
            }
        }

        /// <summary>
        /// Get an identifier for a helper function (the helper is added to the output)
        /// </summary>
        /// <param name="name">The helper name, ex: "defineProperty"</param>
        public Identifier Helper(String name)
        {
            if (!HelperNames.TryGetValue(name, out var actual))
            {
                var def = "_" + name;
                if (Params.Helpers == JsHelperMode.External)
                {
                    actual = def;
                    Names.Add(def);
                }
                else
                    actual = UniqueName(name);
                HelperNames[name] = actual;
                UsedHelpers.Add(name);
                foreach (var d in Helpers.JsHelpers.Dependencies(name))
                    Helper(d);
            }
            return new Identifier(actual);
        }
    }
}
