using System;
using System.Collections.Generic;
using System.Buffers;
using System.Linq;
using System.Web;

namespace SysWeaver
{


    /// <summary>
    /// Creates a text template, that can be used to replace variable (or any string) in a text quickly.
    /// Variables can either be known up front by supplying a set or dictionary, or be defined by a start and end token.
    /// Variable values can be transformed by prepending the variable name with one of the following:
    ///   _ = Make lower case, ex: "hello world!".
    ///   ^ = Make upper case, ex: "HELLO WORLD!".
    ///   @ = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
    ///   # = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
    ///   % = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
    ///   $ = Make javascript string safe (HttpUtility.JavaScriptStringEncode), ex: "Hello world!".
    ///   £ = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = $(£Var); => var x = "Hello world!".
    ///   ¤ = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = $(¤Var); => var x = "12 &lt; 42".
    ///   * = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
    /// Only one of _ , ^ and ~ may be used.
    /// Only one of @, #, %, $, £, ¤ and * may be used.
    /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: _#, ^$
    /// </summary>
    public sealed class TextTemplate
    {

        /// <summary>
        /// Create a text template where variables have a start and end token
        /// </summary>
        /// <param name="text">The original text</param>
        /// <param name="varBegin">Variable begin with this</param>
        /// <param name="varEnd">Variables end with this</param>
        /// <param name="caseInSensitive">If true, the variable is case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed according to:
        /// $(Var) = Variable, ex: "Hello world!".
        /// $(_Var) = Make lower case, ex: "hello world!".
        /// $(^Var) = Make upper case, ex: "HELLO WORLD!".
        /// $(~Var) = Remove camel case, ex: "MyCoolType" => "My cool type".
        /// $(@Var) = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
        /// $(#Var) = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
        /// $(%Var) = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
        /// $($Var) = Make javascript string safe (HttpUtility.JavaScriptStringEncode ), ex: "Hello world!".
        /// $(£Var) = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = $(£Var); => var x = "Hello world!".
        /// $(¤Var) = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = $(¤Var); => var x = "12 &lt; 42".
        /// $(*Var) = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤ and * may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: $(_#Var)
        /// </param>/// 
        public TextTemplate(String text, String varBegin = "$(", String varEnd = ")", bool caseInSensitive = false, bool allowTransforms = true)
        {
            var beginLen = varBegin.Length;
            var endLen = varEnd.Length;

            List<Block> blocks = new ();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool>>(StringComparer.Ordinal);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            if (allowTransforms)
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
            var fmt = Transforms;
            while (start < len)
            {
                var f = text.IndexOf(varBegin, start, StringComparison.Ordinal);
                if (f < 0)
                    break;
                var e = text.IndexOf(varEnd, f + beginLen, StringComparison.Ordinal);
                if (e < 0)
                    break;
                if (f > start)
                {
                    var flen = f - start;
                    blocks.Add(new Block(start, flen));
                    staticLen += flen;
                }
                f += beginLen;
                var key = text.Substring(f, e - f);
                start = e + endLen;
                Func<String, String> format = null;
                if (allowTransforms)
                {
                    var kl = key.Length;
                    if (kl > 1)
                    {
                        if (fmt.TryGetValue(key.Substring(0, 2), out format))
                        {
                            transformedVars[key] = Tuple.Create(key.Substring(2), format);
                        }
                        else
                        {
                            if (fmt.TryGetValue(key.Substring(0, 1), out format))
                            {
                                transformedVars[key] = Tuple.Create(key.Substring(1), format);
                            }
                        }
                    }else
                    {
                        if (kl > 0)
                        {
                            if (fmt.TryGetValue(key.Substring(0, 1), out format))
                            {
                                transformedVars[key] = Tuple.Create(key.Substring(1), format);
                            }
                        }
                    }
                }
                bool isTransformed = format != null;
                blocks.Add(new Block(key, isTransformed));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool>(1 + (v?.Item1 ?? 0), null, isTransformed);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            foreach (var v in vars.ToList())
            {
                var k = v.Key;
                vars[k] = new Tuple<int, String, bool>(v.Value.Item1, String.Join(k, varBegin, varEnd), v.Value.Item3);
            }
            BuildAction = Build;
            Template = text;
            TransformedVars = transformedVars.Freeze();
            VarsAndFrequency = vars.Freeze();
            Blocks = blocks;
            StaticLen = staticLen;
        }


        /// <summary>
        /// Create a text template where the specified values can be replaced
        /// </summary>
        /// <param name="text">The original text</param>
        /// <param name="replace">A set of strings that can be replaced</param>
        /// <param name="caseInSensitive">If true, the variable is case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed according to:
        /// [Var] = Variable, ex: "Hello world!".
        /// [_Var] = Make lower case, ex: "hello world!".
        /// [^Var] = Make upper case, ex: "HELLO WORLD!".
        /// [~Var] = Remove camel case, ex: "MyCoolType" => "My cool type".
        /// [@Var] = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
        /// [#Var] = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
        /// [%Var] = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
        /// [$Var] = Make javascript string safe (HttpUtility.JavaScriptStringEncode ), ex: "Hello world!".
        /// [£Var] = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = [£Var]; => var x = "Hello world!".
        /// [¤Var] = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = [¤Var]; => var x = "12 &lt; 42".
        /// [*Var] = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤ and * may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: [_#Var]
        /// </param>
        public TextTemplate(String text, IReadOnlySet<String> replace, bool caseInSensitive = false, bool allowTransforms = true)
        {
            var tree = StringTree.Build(replace, caseInSensitive);
            var transforms = AddTransforms(ref tree, replace, caseInSensitive, allowTransforms);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            if (allowTransforms)
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
            BuildAction = Build;
            Template = text;

            List<Block> blocks = new();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool>>(StringComparer.Ordinal);
            while (start < len)
            {
                var f = tree.IndexOfAny(out var keyN, text, start);
                if (f < 0)
                    break;
                var key = keyN ?? throw new NullReferenceException();
                if (f > start)
                {
                    var flen = f - start;
                    blocks.Add(new Block(start, flen));
                    staticLen += flen;
                }
                start = f + key.Length;
                if (transforms.TryGetValue(key, out var transform))
                    transformedVars[key] = transform;
                bool isTransformed = transform != null;
                blocks.Add(new Block(key, isTransformed));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool>(1 + (v?.Item1 ?? 0), null, isTransformed);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            foreach (var v in vars.ToList())
            {
                var k = v.Key;
                vars[k] = new Tuple<int, String, bool>(v.Value.Item1, k, v.Value.Item3);
            }
            TransformedVars = transformedVars.Freeze();
            BuildAction = Build;
            Template = text;
            VarsAndFrequency = vars.Freeze();
            Blocks = blocks;
            StaticLen = staticLen;
        }

        /// <summary>
        /// Create a text template where the specified values can be replaced
        /// </summary>
        /// <param name="text">The original text</param>
        /// <param name="varsWithDefaults">A dictionary with the values that can be replaced and their specified defaults</param>
        /// <param name="caseInSensitive">If true, the variable is case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed according to:
        /// [Var] = Variable, ex: "Hello world!".
        /// [_Var] = Make lower case, ex: "hello world!".
        /// [^Var] = Make upper case, ex: "HELLO WORLD!".
        /// [~Var] = Remove camel case, ex: "MyCoolType" => "My cool type".
        /// [@Var] = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
        /// [#Var] = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
        /// [%Var] = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
        /// [$Var] = Make javascript string safe (HttpUtility.JavaScriptStringEncode ), ex: "Hello world!".
        /// [£Var] = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = [£Var]; => var x = "Hello world!".
        /// [¤Var] = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = [¤Var]; => var x = "12 &lt; 42".
        /// [*Var] = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤ and * may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: [_#Var]
        /// </param>
        public TextTemplate(String text, IReadOnlyDictionary<String, String> varsWithDefaults, bool caseInSensitive = false, bool allowTransforms = true)
        {
            var tree = StringTree.Build(varsWithDefaults.Keys, caseInSensitive);
            var transforms = AddTransforms(ref tree, varsWithDefaults.Keys, caseInSensitive, allowTransforms);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            if (allowTransforms)
            {
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
                TransformedVars = transformedVars.Freeze();
            }

            BuildAction = Build;
            Template = text;

            List<Block> blocks = new();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool>>(StringComparer.Ordinal);
            while (start < len)
            {
                var f = tree.IndexOfAny(out var key, text, start);
                if ((f < 0) || (key == null))
                    break;
                if (f > start)
                {
                    var flen = f - start;
                    blocks.Add(new Block(start, flen));
                    staticLen += flen;
                }
                start = f + key.Length;
                if (transforms.TryGetValue(key, out var transform))
                    transformedVars[key] = transform;
                bool isTransformed = transform != null;
                blocks.Add(new Block(key, isTransformed));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool>(1 + (v?.Item1 ?? 0), null, isTransformed);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            BuildAction = Build;
            Template = text;
            VarsAndFrequency = vars.Freeze();
            Blocks = blocks;
            StaticLen = staticLen;
        }

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="vars">A dictionary containing the variables to replace and the value to replace it with</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(IReadOnlyDictionary<String, String> vars) => Get(x => vars.TryGetValue(x, out var v) ? v : null);

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="first">A dictionary containing the variables to replace and the value to replace it with</param>
        /// <param name="second">If the variable isn't found in the first dictionary, try with this</param>
        /// <param name="rest">If the variable isn't found in the second dictionary, try these in order</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(IReadOnlyDictionary<String, String> first, IReadOnlyDictionary<String, String> second, params IReadOnlyDictionary<String, String>[] rest)
        {
            var l = rest?.Length ?? 0;
            Func<String, String> f = k =>
            {
                if (first.TryGetValue(k, out var v))
                    return v;
                if (second.TryGetValue(k, out v))
                    return v;
                for (int i = 0; i <l; ++ i)
                {
                    if (rest[i].TryGetValue(k, out v))
                        return v;
                }
                return null;
            };
            return Get(f);
        }

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="getVars">A function that returns the value for the given key, if not found return null</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(Func<String, String> getVars)
        {
            var eVars = VarsAndFrequency;
            int len = StaticLen;
            Dictionary<String, String> transformed = null;
            Func<String, String> getTrans = null;
            var tr = TransformedVars;
            if (tr != null)
            {
                transformed = new Dictionary<string, string>(tr.GetComparer());
                getTrans = kk => transformed.TryGetValue(kk, out var t) ? t : null;
                foreach (var x in tr)
                {
                    var v = getVars(x.Value.Item1) ?? x.Key;
                    v = x.Value.Item2(v);
                    transformed[x.Key] = v;
                }
            }
            foreach (var x in eVars)
            {
                var k = x.Key;
                var v = (x.Value.Item3 ? getTrans : getVars)(k) ?? x.Value.Item2;
                var val = v ?? String.Empty;
                len += (x.Value.Item1 * val.Length);
            }
            var str = String.Create(len, new ValueTuple<Func<String, String>, Func<String, String>>(getVars, getTrans), BuildAction);
#if DEBUG
            if (str.Length != len)
                throw new Exception("Internal error!");
#endif//DEBUG
            return str;
        }

        /// <summary>
        /// The original template text
        /// </summary>
        public readonly String Template;

        /// <summary>
        /// All variables found/used in the text
        /// </summary>
        public IEnumerable<String> Vars
        {
            get
            {
                foreach (var x in VarsAndFrequency.Where(x => !x.Value.Item3))
                    yield return x.Key;
                foreach (var x in TransformedVars)
                    yield return x.Value.Item1;
            }
        }

        /// <summary>
        /// True if the text contains variables
        /// </summary>
        public bool HaveVars => VarsAndFrequency.Count > 0;

        /// <summary>
        /// Replaces a bunch of key value pairs in a text
        /// </summary>
        /// <param name="text">The text to replace data in</param>
        /// <param name="values">A key value dictionary with replacements to be made</param>
        /// <param name="caseInSensitive">true to make the search case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed according to:
        /// [Var] = Variable, ex: "Hello world!".
        /// [_Var] = Make lower case, ex: "hello world!".
        /// [^Var] = Make upper case, ex: "HELLO WORLD!".
        /// [~Var] = Remove camel case, ex: "MyCoolType" => "My cool type".
        /// [@Var] = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
        /// [#Var] = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
        /// [%Var] = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
        /// [$Var] = Make javascript string safe (HttpUtility.JavaScriptStringEncode), ex: "Hello world!".
        /// [£Var] = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = [£Var]; => var x = "Hello world!".
        /// [¤Var] = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = [¤Var]; => var x = "12 &lt; 42".
        /// [*Var] = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤ and * may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: [_#Var]
        /// </param>
        /// <returns>The text with all replacements made</returns>
        public static String SearchAndReplace(String text, IReadOnlyDictionary<String, String> values, bool caseInSensitive = false, bool allowTransforms = false) 
            => new TextTemplate(text, values, caseInSensitive, allowTransforms).Get(values);


        /// <summary>
        /// Replaces a bunch of key value pairs in a text
        /// </summary>
        /// <param name="text">The text to replace data in</param>
        /// <param name="varBegin">Variable begin with this</param>
        /// <param name="varEnd">Variables end with this</param>
        /// <param name="values">A key value dictionary with replacements to be made</param>
        /// <param name="caseInSensitive">true to make the search case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed according to:
        /// [Var] = Variable, ex: "Hello world!".
        /// [_Var] = Make lower case, ex: "hello world!".
        /// [^Var] = Make upper case, ex: "HELLO WORLD!".
        /// [~Var] = Remove camel case, ex: "MyCoolType" => "My cool type".
        /// [@Var] = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
        /// [#Var] = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
        /// [%Var] = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
        /// [$Var] = Make javascript string safe (HttpUtility.JavaScriptStringEncode), ex: "Hello world!".
        /// [£Var] = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = [£Var]; => var x = "Hello world!".
        /// [¤Var] = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = [¤Var]; => var x = "12 &lt; 42".
        /// [*Var] = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤ and * may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $ and *, ex: [_#Var]
        /// </param>
        /// <returns>The text with all replacements made</returns>
        public static String SearchAndReplaceVars(String text, IReadOnlyDictionary<String, String> values, String varBegin = "${", String varEnd = "}", bool caseInSensitive = false, bool allowTransforms = false) 
            => new TextTemplate(text, varBegin, varEnd, caseInSensitive, allowTransforms).Get(values);


        readonly IReadOnlyDictionary<String, Tuple<int, String, bool>> VarsAndFrequency;

        readonly IReadOnlyDictionary<String, Tuple<String, Func<String, String>>> TransformedVars;

        static readonly IReadOnlyDictionary<String, Func<String, String>> Transforms = new Dictionary<String, Func<String, String>>(StringComparer.Ordinal)
        {
            { "_", new Func<String, String>(x => StringExt.FastLower(x)) }, 
            { "^", new Func<String, String>(x => StringExt.FastToUpper(x)) },
            { "~", new Func<String, String>(x => x.RemoveCamelCase()) },

            { "@", HttpUtility.HtmlAttributeEncode }, 
            { "#", HttpUtility.HtmlEncode }, 
            { "%", HttpUtility.UrlEncode }, 
            { "$", x => HttpUtility.JavaScriptStringEncode(x, false) },
            { "£", x => HttpUtility.JavaScriptStringEncode(x, true) },
            { "¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(x), true) },
            { "*", PathExt.SafeFilename },


            { "_@", x => HttpUtility.HtmlAttributeEncode(StringExt.FastLower(x)) }, 
            { "_#", x => HttpUtility.HtmlEncode(StringExt.FastLower(x)) }, 
            { "_%", x => HttpUtility.UrlEncode(StringExt.FastLower(x)) }, 
            { "_$", x => HttpUtility.JavaScriptStringEncode(StringExt.FastLower(x), false) },
            { "_£", x => HttpUtility.JavaScriptStringEncode(StringExt.FastLower(x), true) },
            { "_¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(StringExt.FastLower(x)), true) },
            { "_*", x => PathExt.SafeFilename(StringExt.FastLower(x)) },


            { "^@", x => HttpUtility.HtmlAttributeEncode(StringExt.FastUpper(x)) }, 
            { "^#", x => HttpUtility.HtmlEncode(StringExt.FastUpper(x)) }, 
            { "^%", x => HttpUtility.UrlEncode(StringExt.FastUpper(x)) }, 
            { "^$", x => HttpUtility.JavaScriptStringEncode(StringExt.FastUpper(x), false) },
            { "^£", x => HttpUtility.JavaScriptStringEncode(StringExt.FastUpper(x), true) },
            { "^¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(StringExt.FastUpper(x)), true) },
            { "^*", x => PathExt.SafeFilename(StringExt.FastUpper(x)) },

            { "~@", x => HttpUtility.HtmlAttributeEncode(x.RemoveCamelCase()) },
            { "~#", x => HttpUtility.HtmlEncode(x.RemoveCamelCase()) },
            { "~%", x => HttpUtility.UrlEncode(x.RemoveCamelCase()) },
            { "~$", x => HttpUtility.JavaScriptStringEncode(x.RemoveCamelCase(), false) },
            { "~£", x => HttpUtility.JavaScriptStringEncode(x.RemoveCamelCase(), true) },
            { "~¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(x.RemoveCamelCase()), true) },
            { "~*", x => PathExt.SafeFilename(x.RemoveCamelCase()) },
        }.Freeze();

        static Dictionary<String, Tuple<String, Func<String, String>>> AddTransforms(ref StringTree tree, IEnumerable<String> replace, bool caseInSensitive, bool allowFormatting)
        {
            Dictionary<String, Tuple<String, Func<String, String>>> replaces = new Dictionary<String, Tuple<String, Func<String, String>>>(caseInSensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            if (!allowFormatting)
                return replaces;
            foreach (var x in replace)
            {
                var l = x.Length;
                int k = -1;
                for (int i = 0; i < l; ++i)
                {
                    if (Char.IsLetterOrDigit(x[i]))
                    {
                        k = i;
                        break;
                    }
                }
                if (k < 0)
                    continue;
                var pre = x.Substring(0, k);
                var post = x.Substring(k);
                foreach (var c in Transforms)
                {
                    var vn = String.Join(c.Key, pre, post);
                    tree = StringTree.Add(vn, caseInSensitive, tree);
                    replaces[vn] = Tuple.Create(x, c.Value);
                }
            }
            return replaces;
        }


        sealed class Block
        {
#if DEBUG
            public override string ToString() => Var == null ? ("Text of length " + Count) : ("Variable " + Var);
#endif//DEBUG

            public readonly String Var;
            public readonly int Start;
            public readonly int Count;
            public readonly bool Transform;

            public Block(int start, int count)
            {
                Start = start;
                Count = count;
            }
            public Block(String var, bool haveTransform)
            {
                Var = var;
                Transform = haveTransform;
            }

        }

        readonly int StaticLen;

        readonly IReadOnlyList<Block> Blocks;
        readonly SpanAction<Char, ValueTuple<Func<String, String>, Func<String, String>>> BuildAction;

        void Build(Span<Char> dest, ValueTuple<Func<String, String>, Func<String, String>> vars)
        {
            var blocks = Blocks;
            var bc = blocks.Count;
            var temp = Template.AsSpan();
            var def = VarsAndFrequency;
            int offset = 0;
            for (int i = 0; i < bc; ++ i)
            {
                var block = blocks[i];
                var v = block.Var;
                if (v != null)
                {
                    var val = (block.Transform ? vars.Item2 : vars.Item1)(v) ?? def[v].Item2;
                    if (String.IsNullOrEmpty(val))
                        continue;
                    val.AsSpan().CopyTo(dest.Slice(offset));
                    offset += val.Length;
                    continue;
                }
                var l = block.Count;
                temp.Slice(block.Start, l).CopyTo(dest.Slice(offset));
                offset += l;
            }
        }



    }




}
