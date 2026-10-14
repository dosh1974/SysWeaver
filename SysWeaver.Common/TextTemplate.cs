using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace SysWeaver
{


    /// <summary>
    /// Creates a text template, that can be used to replace variable (or any string) in a text quickly.
    /// Variables can either be known up front by supplying a set or dictionary, or be defined by a start and end token.
    /// Variable values can be transformed by prepending the variable name with one of the following:
    ///   _ = Make lower case, ex: "hello world!".
    ///   ^ = Make upper case, ex: "HELLO WORLD!".
    ///   ~ = Remove camel case, ex: "MyCoolType" => "My cool type".
    ///   @ = Make html attribute safe (HttpUtility.HtmlAttributeEncode), ex: "Hello world!".
    ///   # = Make html value safe (HttpUtility.HtmlEncode), ex: "Hello world!".
    ///   % = Make URL safe (HttpUtility.UrlEncode), ex: "Hello+world!".
    ///   $ = Make javascript string safe (HttpUtility.JavaScriptStringEncode), ex: "Hello world!".
    ///   £ = Make javascript string safe with quotes (HttpUtility.JavaScriptStringEncode), ex: var x = $(£Var); => var x = "Hello world!".
    ///   ¤ = Make javascript string safe with quotes of html content (HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode()), ex: var x = $(¤Var); => var x = "12 &lt; 42".
    ///   * = Make file/path safe (PathExt.SafeFilename), ex: "C:\Apa" => "C__Apa".
    ///   ! = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
    ///   / = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
    /// Only one of _ , ^ and ~ may be used.
    /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
    /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
    /// Example: _#, ^$
    /// </summary>
    /// <remarks>
    /// The template text is parsed once (in the constructor), <see cref="Get(Func{string, string})"/> then builds the result with a single string allocation.
    /// Instances are immutable and <see cref="Get(Func{string, string})"/> is thread safe (as long as the value provider is).
    /// The value provider is called once per variable name and call (the values are read into a pooled array when the length is computed and written from it), every occurrence of a variable gets the same value.
    /// How unresolved variables (the provider returns null) are rendered depends on the constructor, see each constructor.
    /// Typically used for path templates (<see cref="PathTemplate"/>), environment variables (<see cref="EnvInfo.ResolveText(string, bool, IReadOnlyDictionary{string, string})"/>) and HTML / text templates in the HTTP server.
    /// </remarks>
    public sealed class TextTemplate
    {

        /// <summary>
        /// Create a text template where variables have a start and end token.
        /// Unresolved variables are left as is in the output (ex: "$(Unknown)"), unresolved transformed variables are rendered as the transformed variable name without the tokens.
        /// A begin token without a matching end token is treated as plain text.
        /// </summary>
        /// <param name="text">The original text (may not be null)</param>
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
        /// $(!Var) = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
        /// $(/Var) = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
        /// Example: $(_#Var)
        /// </param>
        /// <exception cref="NullReferenceException"><paramref name="text"/>, <paramref name="varBegin"/> or <paramref name="varEnd"/> is null.</exception>
        public TextTemplate(String text, String varBegin = "$(", String varEnd = ")", bool caseInSensitive = false, bool allowTransforms = true)
            : this(text, varBegin, varEnd, null, caseInSensitive, allowTransforms)
        {
        }

        /// <summary>
        /// Creates a text template, only the variables accepted by <paramref name="isVariable"/> are replaced, other tokens are kept as is (static text).
        /// Useful when the text can contain the variable syntax for other purposes, ex: javascript template literals "${x}".
        /// </summary>
        /// <param name="text">The original text (may not be null)</param>
        /// <param name="varBegin">Variable begin with this</param>
        /// <param name="varEnd">Variables end with this</param>
        /// <param name="isVariable">Called with the variable name (without any transform prefix), return true if it's a variable, false to keep the token as static text. Null accepts all names</param>
        /// <param name="caseInSensitive">If true, the variable is case insensitive</param>
        /// <param name="allowTransforms">If true, the variable can be transformed, see <see cref="TextTemplate(string, string, string, bool, bool)"/></param>
        /// <exception cref="NullReferenceException"><paramref name="text"/>, <paramref name="varBegin"/> or <paramref name="varEnd"/> is null.</exception>
        public TextTemplate(String text, String varBegin, String varEnd, Func<String, bool> isVariable, bool caseInSensitive = false, bool allowTransforms = true)
        {
            var beginLen = varBegin.Length;
            var endLen = varEnd.Length;

            List<Block> blocks = new ();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool, int>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.Ordinal);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            if (allowTransforms)
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
            var fmt = TransformsLookup;
            int search = 0;
            while (search < len)
            {
                var f = text.IndexOf(varBegin, search, StringComparison.Ordinal);
                if (f < 0)
                    break;
                var e = text.IndexOf(varEnd, f + beginLen, StringComparison.Ordinal);
                if (e < 0)
                    break;
                var key = text.Substring(f + beginLen, e - f - beginLen);
                Func<String, String> format = null;
                String name = key;
                if (allowTransforms)
                {
                    //  Look up the transform prefixes without allocating them
                    var kl = key.Length;
                    if (kl > 1)
                    {
                        if (fmt.TryGetValue(key.AsSpan(0, 2), out format))
                        {
                            name = key.Substring(2);
                        }
                        else
                        {
                            if (fmt.TryGetValue(key.AsSpan(0, 1), out format))
                            {
                                name = key.Substring(1);
                            }
                        }
                    }else
                    {
                        if (kl > 0)
                        {
                            if (fmt.TryGetValue(key.AsSpan(0, 1), out format))
                            {
                                name = key.Substring(1);
                            }
                        }
                    }
                }
                //  Not a variable, keep the token as static text (continue searching after the begin token)
                if ((isVariable != null) && !isVariable(name))
                {
                    search = f + beginLen;
                    continue;
                }
                if (format != null)
                    transformedVars[key] = Tuple.Create(name, format);
                if (f > start)
                {
                    var flen = f - start;
                    blocks.Add(new Block(start, flen));
                    staticLen += flen;
                }
                start = e + endLen;
                search = start;
                bool isTransformed = format != null;
                var tokenLen = start - f;
                blocks.Add(new Block(key, isTransformed, f, tokenLen));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool, int>(1 + (v?.Item1 ?? 0), null, isTransformed, (v?.Item4 ?? 0) + tokenLen);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            KeepUnresolved = true;
            Template = text;
            TransformedVars = transformedVars.Freeze();
            VarsAndFrequency = vars.Freeze();
            VarList = VarsAndFrequency.ToArray();
            TransformedList = (TransformedVars?.Count ?? 0) > 0 ? TransformedVars.ToArray() : null;
            Blocks = blocks.ToArray();
            (Lookups, Slots) = CreateSlots(VarList, vars.Comparer, TransformedList, transformedVars?.Comparer, TransformDefaults, Blocks);
            StaticLen = staticLen;
        }


        /// <summary>
        /// Create a text template where the specified strings can be replaced (any occurrence of the strings in the text, no begin / end tokens).
        /// Unresolved variables are left as is in the output.
        /// When transforms are allowed, a transform prefix is inserted before the first letter or digit of each string, ex: "${Name}" can be used as "${_Name}".
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
        /// [!Var] = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
        /// [/Var] = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
        /// Example: [_#Var]
        /// </param>
        public TextTemplate(String text, IReadOnlySet<String> replace, bool caseInSensitive = false, bool allowTransforms = true)
        {
            var tree = StringTree.Build(replace, caseInSensitive);
            var transforms = AddTransforms(ref tree, replace, caseInSensitive, allowTransforms);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            if (allowTransforms)
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
            Template = text;

            List<Block> blocks = new();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool, int>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.Ordinal);
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
                blocks.Add(new Block(key, isTransformed, f, key.Length));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool, int>(1 + (v?.Item1 ?? 0), null, isTransformed, (v?.Item4 ?? 0) + key.Length);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            KeepUnresolved = true;
            TransformedVars = transformedVars.Freeze();
            Template = text;
            VarsAndFrequency = vars.Freeze();
            VarList = VarsAndFrequency.ToArray();
            TransformedList = (TransformedVars?.Count ?? 0) > 0 ? TransformedVars.ToArray() : null;
            Blocks = blocks.ToArray();
            (Lookups, Slots) = CreateSlots(VarList, vars.Comparer, TransformedList, transformedVars?.Comparer, TransformDefaults, Blocks);
            StaticLen = staticLen;
        }

        /// <summary>
        /// Create a text template where the specified strings (the keys of the dictionary) can be replaced (any occurrence of the strings in the text, no begin / end tokens).
        /// Unresolved variables (including transformed ones) are rendered using the default value from the dictionary (a null default is rendered as an empty string).
        /// When transforms are allowed, a transform prefix is inserted before the first letter or digit of each string, ex: "[Name]" can be used as "[_Name]".
        /// </summary>
        /// <param name="text">The original text</param>
        /// <param name="varsWithDefaults">A dictionary whose keys are the strings that can be replaced, and whose values are the default values to use if a variable is unresolved</param>
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
        /// [!Var] = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
        /// [/Var] = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
        /// Example: [_#Var]
        /// </param>
        public TextTemplate(String text, IReadOnlyDictionary<String, String> varsWithDefaults, bool caseInSensitive = false, bool allowTransforms = true)
        {
            var tree = StringTree.Build(varsWithDefaults.Keys, caseInSensitive);
            var transforms = AddTransforms(ref tree, varsWithDefaults.Keys, caseInSensitive, allowTransforms);
            Dictionary<String, Tuple<String, Func<String, String>>> transformedVars = null;
            Dictionary<String, String> transformDefaults = null;
            if (allowTransforms)
            {
                transformedVars = caseInSensitive ? new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<string, Func<string, string>>>(StringComparer.Ordinal);
                transformDefaults = new Dictionary<string, string>(caseInSensitive ? StringComparer.InvariantCultureIgnoreCase : StringComparer.Ordinal);
            }

            Template = text;

            List<Block> blocks = new();
            int start = 0;
            int len = text.Length;
            int staticLen = 0;
            Dictionary<String, Tuple<int, String, bool, int>> vars = caseInSensitive ? new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, Tuple<int, string, bool, int>>(StringComparer.Ordinal);
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
                String defValue;
                if (transforms.TryGetValue(key, out var transform))
                {
                    transformedVars[key] = transform;
                    varsWithDefaults.TryGetValue(transform.Item1, out defValue);
                    transformDefaults[transform.Item1] = defValue;
                }
                else
                {
                    varsWithDefaults.TryGetValue(key, out defValue);
                }
                bool isTransformed = transform != null;
                blocks.Add(new Block(key, isTransformed, f, key.Length));
                vars.TryGetValue(key, out var v);
                vars[key] = new Tuple<int, String, bool, int>(1 + (v?.Item1 ?? 0), defValue, isTransformed, 0);
            }
            if (start < len)
            {
                var flen = len - start;
                blocks.Add(new Block(start, flen));
                staticLen += flen;
            }
            Template = text;
            TransformedVars = transformedVars.Freeze();
            TransformDefaults = transformDefaults.Freeze();
            VarsAndFrequency = vars.Freeze();
            VarList = VarsAndFrequency.ToArray();
            TransformedList = (TransformedVars?.Count ?? 0) > 0 ? TransformedVars.ToArray() : null;
            Blocks = blocks.ToArray();
            (Lookups, Slots) = CreateSlots(VarList, vars.Comparer, TransformedList, transformedVars?.Comparer, TransformDefaults, Blocks);
            StaticLen = staticLen;
        }

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="vars">A dictionary containing the variables to replace and the value to replace it with.
        /// For templates with begin / end tokens the keys are the names without the tokens, else the full strings.</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(IReadOnlyDictionary<String, String> vars) => Get(vars, static (d, x) => d.TryGetValue(x, out var v) ? v : null);

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="first">A dictionary containing the variables to replace and the value to replace it with</param>
        /// <param name="second">If the variable isn't found in the first dictionary, try with this</param>
        /// <param name="rest">If the variable isn't found in the second dictionary, try these in order</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(IReadOnlyDictionary<String, String> first, IReadOnlyDictionary<String, String> second, params IReadOnlyDictionary<String, String>[] rest)
            => Get((first, second, rest), static (s, k) =>
            {
                if (s.first.TryGetValue(k, out var v))
                    return v;
                if (s.second.TryGetValue(k, out v))
                    return v;
                var rest = s.rest;
                var l = rest?.Length ?? 0;
                for (int i = 0; i <l; ++ i)
                {
                    if (rest[i].TryGetValue(k, out v))
                        return v;
                }
                return null;
            });

        /// <summary>
        /// Get a string from the template with the specified replacements
        /// </summary>
        /// <param name="getVars">A function that returns the value for the given key, if not found return null.
        /// Called once per variable name (every occurrence of the variable gets that value).</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get(Func<String, String> getVars) => Get(getVars, static (f, k) => f(k));

        /// <summary>
        /// Get a string from the template with the specified replacements, the values are looked up using a state (use a static lambda to avoid allocating a closure per call)
        /// </summary>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="state">The state passed to <paramref name="getVars"/></param>
        /// <param name="getVars">A function that returns the value for the given state and key, if not found return null.
        /// Called once per variable name (every occurrence of the variable gets that value).</param>
        /// <returns>A string with the specified replacements done</returns>
        public String Get<TState>(TState state, Func<TState, String, String> getVars)
        {
            var count = Lookups.Length + Slots.Length;
            if (count <= 0)
                return String.Create(StaticLen, new BuildState(this, null), static (dest, s) => s.Template.Build(dest, s.Values));
            var values = ArrayPool<String>.Shared.Rent(count);
            try
            {
                var len = Resolve(values, state, getVars);
                var str = String.Create(len, new BuildState(this, values), static (dest, s) => s.Template.Build(dest, s.Values));
#if DEBUG
                if (str.Length != len)
                    throw new Exception("Internal error!");
#endif//DEBUG
                return str;
            }
            finally
            {
                Array.Clear(values, 0, count);
                ArrayPool<String>.Shared.Return(values);
            }
        }

        /// <summary>
        /// Append the text from the template with the specified replacements to a string builder (the same text as <see cref="Get{TState}(TState, Func{TState, string, string})"/>, without creating a string).
        /// </summary>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="sb">The string builder to append the text to</param>
        /// <param name="state">The state passed to <paramref name="getVars"/></param>
        /// <param name="getVars">A function that returns the value for the given state and key, if not found return null.
        /// Called once per variable name (every occurrence of the variable gets that value), use a static lambda to avoid allocating a closure per call.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sb"/> is null.</exception>
        public void AppendTo<TState>(StringBuilder sb, TState state, Func<TState, String, String> getVars)
        {
            ArgumentNullException.ThrowIfNull(sb);
            var count = Lookups.Length + Slots.Length;
            if (count <= 0)
            {
                Append(sb, null);
                return;
            }
            var values = ArrayPool<String>.Shared.Rent(count);
            try
            {
                Resolve(values, state, getVars);
                Append(sb, values);
            }
            finally
            {
                Array.Clear(values, 0, count);
                ArrayPool<String>.Shared.Return(values);
            }
        }

        /// <summary>
        /// Append the text from the template with the specified replacements to a string builder, the values are read as memory (ex: a part of a string or a reused buffer, no string per value).
        /// The same text as <see cref="AppendTo{TState}(StringBuilder, TState, Func{TState, string, string})"/> with the values converted to strings.
        /// </summary>
        /// <remarks>
        /// A transformed variable (ex: <c>${#Var}</c>) needs a string: a value that isn't a whole string is copied to a new string, except for transforms that leave the value unchanged
        /// (<c>@</c> when the value doesn't contain any of the chars encoded by <see cref="HttpUtility.HtmlAttributeEncode(string)"/>).
        /// </remarks>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="sb">The string builder to append the text to</param>
        /// <param name="state">The state passed to <paramref name="getVars"/></param>
        /// <param name="getVars">A function that returns the value for the given state and key, if not found return <c>default</c> (a memory without a backing string or array, the same as a null string;
        /// an empty string's memory is a found empty value).
        /// Called once per variable name (every occurrence of the variable gets that value), use a static lambda to avoid allocating a closure per call.
        /// The memory must be valid until this method returns.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sb"/> is null.</exception>
        public void AppendMemoryValuesTo<TState>(StringBuilder sb, TState state, Func<TState, String, ReadOnlyMemory<Char>> getVars)
        {
            ArgumentNullException.ThrowIfNull(sb);
            var count = Lookups.Length + Slots.Length;
            if (count <= 0)
            {
                Append(sb, null);
                return;
            }
            var values = ArrayPool<ReadOnlyMemory<Char>>.Shared.Rent(count);
            try
            {
                Resolve(values, state, getVars);
                var blocks = Blocks;
                var temp = Template;
                var nl = Lookups.Length;
                foreach (var block in blocks)
                {
                    if (block.Var != null)
                    {
                        var val = values[nl + block.Slot];
                        //  Unresolved: left as is (the original text)
                        if (IsNotFound(val))
                        {
                            sb.Append(temp, block.Start, block.Count);
                            continue;
                        }
                        sb.Append(val.Span);
                        continue;
                    }
                    sb.Append(temp, block.Start, block.Count);
                }
            }
            finally
            {
                Array.Clear(values, 0, count);
                ArrayPool<ReadOnlyMemory<Char>>.Shared.Return(values);
            }
        }

        /// <summary>
        /// True if a memory value means "not found" (no backing object, like a null string)
        /// </summary>
        static bool IsNotFound(ReadOnlyMemory<Char> value) => value.Equals(default(ReadOnlyMemory<Char>));

        /// <summary>
        /// Get a memory value as a string, without a copy if the memory is a whole string
        /// </summary>
        static String AsString(ReadOnlyMemory<Char> value)
        {
            if (IsNotFound(value))
                return null;
            if (MemoryMarshal.TryGetString(value, out var s, out var start, out var length) && (start == 0) && (length == s.Length))
                return s;
            return new String(value.Span);
        }

        /// <summary>
        /// The chars that <see cref="HttpUtility.HtmlAttributeEncode(string)"/> encodes, a text without any of them is returned as is
        /// </summary>
        static readonly SearchValues<Char> HtmlAttributeEncodedChars = SearchValues.Create("\"&'<");

        /// <summary>
        /// The "@" transform (HtmlAttributeEncode)
        /// </summary>
        static readonly Func<String, String> HtmlAttributeEncodeTransform = HttpUtility.HtmlAttributeEncode;

        /// <summary>
        /// Test if a transform leaves a text unchanged (only known for some transforms, false if unknown)
        /// </summary>
        static bool IsUnchangedBy(Func<String, String> transform, ReadOnlySpan<Char> text)
            => ReferenceEquals(transform, HtmlAttributeEncodeTransform) && (text.IndexOfAny(HtmlAttributeEncodedChars) < 0);

        /// <summary>
        /// Read the values of all variables as memory (one call of <paramref name="getVars"/> per name in <see cref="Lookups"/>), see <see cref="Resolve{TState}(string[], TState, Func{TState, string, string})"/>.
        /// </summary>
        /// <param name="values">Receives the looked up values (the first <see cref="Lookups"/> elements), then the value to write for each slot (<see cref="Slots"/>, not found = write the original text of each occurrence)</param>
        /// <param name="state">The state passed to <paramref name="getVars"/></param>
        /// <param name="getVars">The value provider</param>
        void Resolve<TState>(ReadOnlyMemory<Char>[] values, TState state, Func<TState, String, ReadOnlyMemory<Char>> getVars)
        {
            var lookups = Lookups;
            var nl = lookups.Length;
            for (int i = 0; i < nl; ++i)
                values[i] = getVars(state, lookups[i]);
            var slots = Slots;
            var ns = slots.Length;
            var keepUnresolved = KeepUnresolved;
            for (int i = 0; i < ns; ++i)
            {
                ref readonly var slot = ref slots[i];
                var li = slot.Lookup;
                ReadOnlyMemory<Char> v = default;
                if (li >= 0)
                {
                    v = values[li];
                    var tr = slot.Transform;
                    //  Unresolved transformed variables: use the default (dictionary constructor) or the variable name
                    if ((tr != null) && (IsNotFound(v) || !IsUnchangedBy(tr, v.Span)))
                        v = tr(AsString(v) ?? slot.TransformDefault).AsMemory();
                }
                //  Unresolved: the original text of each occurrence, or the default
                if (IsNotFound(v))
                {
                    if (keepUnresolved)
                    {
                        values[nl + i] = default;
                        continue;
                    }
                    v = (slot.Default ?? String.Empty).AsMemory();
                }
                values[nl + i] = v;
            }
        }

        /// <summary>
        /// Read the values of all variables (one call of <paramref name="getVars"/> per name in <see cref="Lookups"/>) and compute the length of the result.
        /// </summary>
        /// <param name="values">Receives the looked up values (the first <see cref="Lookups"/> elements), then the value to write for each slot (<see cref="Slots"/>, null = write the original text of each occurrence)</param>
        /// <param name="state">The state passed to <paramref name="getVars"/></param>
        /// <param name="getVars">The value provider</param>
        /// <returns>The length of the result</returns>
        int Resolve<TState>(String[] values, TState state, Func<TState, String, String> getVars)
        {
            var lookups = Lookups;
            var nl = lookups.Length;
            for (int i = 0; i < nl; ++i)
                values[i] = getVars(state, lookups[i]);
            var slots = Slots;
            var ns = slots.Length;
            var keepUnresolved = KeepUnresolved;
            int len = StaticLen;
            for (int i = 0; i < ns; ++i)
            {
                ref readonly var slot = ref slots[i];
                var li = slot.Lookup;
                String v = null;
                if (li >= 0)
                {
                    v = values[li];
                    var tr = slot.Transform;
                    //  Unresolved transformed variables: use the default (dictionary constructor) or the variable name
                    if (tr != null)
                        v = tr(v ?? slot.TransformDefault);
                }
                //  Unresolved: the original text of each occurrence, or the default
                if ((v == null) && keepUnresolved)
                {
                    len += slot.TokenLen;
                    values[nl + i] = null;
                    continue;
                }
                v = v ?? slot.Default ?? String.Empty;
                len += (slot.Count * v.Length);
                values[nl + i] = v;
            }
            return len;
        }

        /// <summary>
        /// The original template text
        /// </summary>
        public readonly String Template;

        /// <summary>
        /// All variables found/used in the text (names without begin / end tokens and transform prefixes), may contain duplicates if a variable is used both with and without transforms
        /// </summary>
        public IEnumerable<String> Vars
        {
            get
            {
                foreach (var x in VarsAndFrequency.Where(x => !x.Value.Item3))
                    yield return x.Key;
                var t = TransformedVars;
                if (t != null)
                {
                    foreach (var x in t)
                        yield return x.Value.Item1;
                }
            }
        }

        /// <summary>
        /// True if the text contains variables
        /// </summary>
        public bool HaveVars => VarsAndFrequency.Count > 0;

        /// <summary>
        /// Replaces a bunch of key value pairs in a text (every occurrence of a key is replaced by its value)
        /// </summary>
        /// <remarks>Parses a new template on every call, cache a <see cref="TextTemplate"/> instance if the same text is used repeatedly.</remarks>
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
        /// [!Var] = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
        /// [/Var] = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
        /// Example: [_#Var]
        /// </param>
        /// <returns>The text with all replacements made</returns>
        public static String SearchAndReplace(String text, IReadOnlyDictionary<String, String> values, bool caseInSensitive = false, bool allowTransforms = false) 
            => new TextTemplate(text, values, caseInSensitive, allowTransforms).Get(values);


        /// <summary>
        /// Replaces variables delimited by begin and end tokens in a text, ex: "${Name}" (unknown variables are left as is)
        /// </summary>
        /// <remarks>Parses a new template on every call, cache a <see cref="TextTemplate"/> instance if the same text is used repeatedly.</remarks>
        /// <param name="text">The text to replace data in</param>
        /// <param name="varBegin">Variable begin with this</param>
        /// <param name="varEnd">Variables end with this</param>
        /// <param name="values">A key value dictionary with replacements to be made (keys are variable names without the tokens)</param>
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
        /// [!Var] = Make mark down (.md) safe (StringTools.EscapeMD), ex: "** Hello **" => "\*\* Hello \*\*".
        /// [/Var] = Make mark down (.md) safe and prevent word wrapping (StringTools.EscapeMD), ex: "** Hello **" => "\*\*\u00A0Hello\u00A0\*\*".
        /// Only one of _ , ^ and ~ may be used.
        /// Only one of @, #, %, $, £, ¤, *, ! and / may be used.
        /// _ , ^ and ~ can be combined with one of @, #, %, $, £, ¤, *, ! and /. 
        /// Example: [_#Var]
        /// </param>
        /// <returns>The text with all replacements made</returns>
        public static String SearchAndReplaceVars(String text, IReadOnlyDictionary<String, String> values, String varBegin = "${", String varEnd = "}", bool caseInSensitive = false, bool allowTransforms = false) 
            => new TextTemplate(text, varBegin, varEnd, caseInSensitive, allowTransforms).Get(values);


        readonly IReadOnlyDictionary<String, Tuple<int, String, bool, int>> VarsAndFrequency;

        /// <summary>
        /// The <see cref="VarsAndFrequency"/> (in the same order), enumerated by Get without an enumerator allocation
        /// </summary>
        readonly KeyValuePair<String, Tuple<int, String, bool, int>>[] VarList;

        /// <summary>
        /// The <see cref="TransformedVars"/> (in the same order), enumerated by Get without an enumerator allocation, null if there are none (no dictionary of transformed values is needed)
        /// </summary>
        readonly KeyValuePair<String, Tuple<String, Func<String, String>>>[] TransformedList;

        readonly IReadOnlyDictionary<String, Tuple<String, Func<String, String>>> TransformedVars;

        /// <summary>
        /// True if unresolved variables are rendered as their original text (token and set constructors), false to use the defaults (dictionary constructor)
        /// </summary>
        readonly bool KeepUnresolved;

        /// <summary>
        /// Default values of the (untransformed) variables used by transformed variables, only set by the dictionary constructor (else null).
        /// </summary>
        readonly IReadOnlyDictionary<String, String> TransformDefaults;

        static readonly IReadOnlyDictionary<String, Func<String, String>> Transforms = new Dictionary<String, Func<String, String>>(StringComparer.Ordinal)
        {
            { "_", new Func<String, String>(x => x.FastToLower()) }, 
            { "^", new Func<String, String>(x => StringExt.FastToUpper(x)) },
            { "~", new Func<String, String>(x => x.RemoveCamelCase()) },

            { "@", HtmlAttributeEncodeTransform },
            { "#", HttpUtility.HtmlEncode },
            { "%", HttpUtility.UrlEncode }, 
            { "$", x => HttpUtility.JavaScriptStringEncode(x, false) },
            { "£", x => HttpUtility.JavaScriptStringEncode(x, true) },
            { "¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(x), true) },
            { "*", PathExt.SafeFilename },
            { "!", x => StringTools.EscapeMD(x, false, true) },
            { "/", x => StringTools.EscapeMD(x, true, true) },



            { "_@", x => HttpUtility.HtmlAttributeEncode(x.FastToLower()) }, 
            { "_#", x => HttpUtility.HtmlEncode(x.FastToLower()) }, 
            { "_%", x => HttpUtility.UrlEncode(x.FastToLower()) }, 
            { "_$", x => HttpUtility.JavaScriptStringEncode(x.FastToLower(), false) },
            { "_£", x => HttpUtility.JavaScriptStringEncode(x.FastToLower(), true) },
            { "_¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(x.FastToLower()), true) },
            { "_*", x => PathExt.SafeFilename(x.FastToLower()) },
            { "_!", x => StringTools.EscapeMD(x.FastToLower(), false, true) },
            { "_/", x => StringTools.EscapeMD(x.FastToLower(), true, true) },


            { "^@", x => HttpUtility.HtmlAttributeEncode(StringExt.FastToUpper(x)) }, 
            { "^#", x => HttpUtility.HtmlEncode(StringExt.FastToUpper(x)) }, 
            { "^%", x => HttpUtility.UrlEncode(StringExt.FastToUpper(x)) }, 
            { "^$", x => HttpUtility.JavaScriptStringEncode(StringExt.FastToUpper(x), false) },
            { "^£", x => HttpUtility.JavaScriptStringEncode(StringExt.FastToUpper(x), true) },
            { "^¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(StringExt.FastToUpper(x)), true) },
            { "^*", x => PathExt.SafeFilename(StringExt.FastToUpper(x)) },
            { "^!", x => StringTools.EscapeMD(x.FastToUpper(), false, true) },
            { "^/", x => StringTools.EscapeMD(x.FastToUpper(), true, true) },

            { "~@", x => HttpUtility.HtmlAttributeEncode(x.RemoveCamelCase()) },
            { "~#", x => HttpUtility.HtmlEncode(x.RemoveCamelCase()) },
            { "~%", x => HttpUtility.UrlEncode(x.RemoveCamelCase()) },
            { "~$", x => HttpUtility.JavaScriptStringEncode(x.RemoveCamelCase(), false) },
            { "~£", x => HttpUtility.JavaScriptStringEncode(x.RemoveCamelCase(), true) },
            { "~¤", x => HttpUtility.JavaScriptStringEncode(HttpUtility.HtmlEncode(x.RemoveCamelCase()), true) },
            { "~*", x => PathExt.SafeFilename(x.RemoveCamelCase()) },
            { "~!", x => StringTools.EscapeMD(x.RemoveCamelCase(), false, true) },
            { "~/", x => StringTools.EscapeMD(x.RemoveCamelCase(), true, true) },
        }.Freeze();

        /// <summary>
        /// The <see cref="Transforms"/>, looked up using a span (ordinal)
        /// </summary>
        static readonly FrozenDictionary<String, Func<String, String>>.AlternateLookup<ReadOnlySpan<Char>> TransformsLookup = Transforms.ToFrozenDictionary(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<Char>>();

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
            /// <summary>
            /// The index of the variable in <see cref="Slots"/> (set after parsing, only used by variable blocks)
            /// </summary>
            public int Slot;

            public Block(int start, int count)
            {
                Start = start;
                Count = count;
            }
            public Block(String var, bool haveTransform, int start, int count)
            {
                Var = var;
                Transform = haveTransform;
                Start = start;
                Count = count;
            }

        }

        readonly int StaticLen;

        readonly Block[] Blocks;

        /// <summary>
        /// The variable names passed to the value provider, once each (ordinal): the names used by the transformed variables (in <see cref="TransformedList"/> order), then the untransformed variables (in <see cref="VarList"/> order)
        /// </summary>
        readonly String[] Lookups;

        /// <summary>
        /// How to compute the value of each variable (in <see cref="VarList"/> order)
        /// </summary>
        readonly Slot[] Slots;

        /// <summary>
        /// How to compute the value of a variable (an element of <see cref="VarList"/>)
        /// </summary>
        readonly struct Slot
        {
            public Slot(int lookup, Func<String, String> transform, String transformDefault, int count, String def, int tokenLen)
            {
                Lookup = lookup;
                Transform = transform;
                TransformDefault = transformDefault;
                Count = count;
                Default = def;
                TokenLen = tokenLen;
            }
            /// <summary>
            /// The index of the value in <see cref="Lookups"/>, -1 if the value is always unresolved (null)
            /// </summary>
            public readonly int Lookup;
            /// <summary>
            /// The transform to apply to the value, null if the variable isn't transformed
            /// </summary>
            public readonly Func<String, String> Transform;
            /// <summary>
            /// The value to transform if the variable is unresolved (the default of the dictionary constructor, or the variable name)
            /// </summary>
            public readonly String TransformDefault;
            /// <summary>
            /// The number of occurrences of the variable
            /// </summary>
            public readonly int Count;
            /// <summary>
            /// The default value (dictionary constructor)
            /// </summary>
            public readonly String Default;
            /// <summary>
            /// The total length of the variable tokens (written as is if the variable is unresolved)
            /// </summary>
            public readonly int TokenLen;
        }

        /// <summary>
        /// Create the <see cref="Lookups"/> and <see cref="Slots"/> and set the <see cref="Block.Slot"/> of the variable blocks
        /// </summary>
        static (String[], Slot[]) CreateSlots(KeyValuePair<String, Tuple<int, String, bool, int>>[] varList, IEqualityComparer<String> varComparer,
            KeyValuePair<String, Tuple<String, Func<String, String>>>[] trList, IEqualityComparer<String> trComparer, IReadOnlyDictionary<String, String> defs, Block[] blocks)
        {
            var lookups = new List<String>();
            var lookupIndex = new Dictionary<String, int>(StringComparer.Ordinal);
            int GetLookup(String name)
            {
                if (!lookupIndex.TryGetValue(name, out var index))
                {
                    index = lookups.Count;
                    lookups.Add(name);
                    lookupIndex.Add(name, index);
                }
                return index;
            }
            //  The transformed variables, the value provider is called with the name without the transform prefix
            Dictionary<String, int> trIndex = null;
            var trCount = trList?.Length ?? 0;
            if (trCount > 0)
            {
                trIndex = new Dictionary<String, int>(trComparer);
                for (int i = 0; i < trCount; ++i)
                {
                    trIndex[trList[i].Key] = i;
                    GetLookup(trList[i].Value.Item1);
                }
            }
            var vl = varList.Length;
            var slots = new Slot[vl];
            var slotIndex = new Dictionary<String, int>(varComparer);
            for (int i = 0; i < vl; ++i)
            {
                var x = varList[i];
                var k = x.Key;
                var v = x.Value;
                slotIndex[k] = i;
                if (v.Item3)
                {
                    if ((trIndex != null) && trIndex.TryGetValue(k, out var ti))
                    {
                        var t = trList[ti];
                        var name = t.Value.Item1;
                        var trDef = defs == null ? t.Key : (defs.TryGetValue(name, out var dv) ? dv : null) ?? String.Empty;
                        slots[i] = new Slot(GetLookup(name), t.Value.Item2, trDef, v.Item1, v.Item2, v.Item4);
                    }
                    else
                    {
                        slots[i] = new Slot(-1, null, null, v.Item1, v.Item2, v.Item4);
                    }
                    continue;
                }
                slots[i] = new Slot(GetLookup(k), null, null, v.Item1, v.Item2, v.Item4);
            }
            foreach (var b in blocks)
            {
                var v = b.Var;
                if (v != null)
                    b.Slot = slotIndex[v];
            }
            return (lookups.ToArray(), slots);
        }

        /// <summary>
        /// The state of <see cref="Build(Span{char}, String[])"/>
        /// </summary>
        readonly struct BuildState
        {
            public BuildState(TextTemplate template, String[] values)
            {
                Template = template;
                Values = values;
            }
            public readonly TextTemplate Template;
            /// <summary>
            /// The values computed by <see cref="Resolve{TState}(string[], TState, Func{TState, string, string})"/> (null if there are no variables)
            /// </summary>
            public readonly String[] Values;
        }

        void Build(Span<Char> dest, String[] values)
        {
            var blocks = Blocks;
            var temp = Template.AsSpan();
            var nl = Lookups.Length;
            int offset = 0;
            foreach (var block in blocks)
            {
                if (block.Var != null)
                {
                    var val = values[nl + block.Slot];
                    //  Unresolved: left as is (the original text)
                    if (val == null)
                    {
                        var cl = block.Count;
                        temp.Slice(block.Start, cl).CopyTo(dest.Slice(offset));
                        offset += cl;
                        continue;
                    }
                    val.AsSpan().CopyTo(dest.Slice(offset));
                    offset += val.Length;
                    continue;
                }
                var l = block.Count;
                temp.Slice(block.Start, l).CopyTo(dest.Slice(offset));
                offset += l;
            }
        }

        void Append(StringBuilder sb, String[] values)
        {
            var blocks = Blocks;
            var temp = Template;
            var nl = Lookups.Length;
            foreach (var block in blocks)
            {
                if (block.Var != null)
                {
                    var val = values[nl + block.Slot];
                    //  Unresolved: left as is (the original text)
                    if (val == null)
                    {
                        sb.Append(temp, block.Start, block.Count);
                        continue;
                    }
                    sb.Append(val);
                    continue;
                }
                sb.Append(temp, block.Start, block.Count);
            }
        }



    }




}
