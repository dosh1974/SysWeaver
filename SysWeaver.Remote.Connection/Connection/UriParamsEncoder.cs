using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;

namespace SysWeaver.Remote.Connection
{
    /// <summary>
    /// Compiles url templates such as <c>"Get?id={Id}&amp;name={Name}"</c> into functions that format a value of type <typeparamref name="T"/> into a url.
    /// Used by the generated remote API classes to build GET/DELETE urls from method parameters.
    /// </summary>
    /// <typeparam name="T">The type of the value that is used to resolve the template variables.</typeparam>
    /// <remarks>
    /// Variable resolution: if <typeparamref name="T"/> is a supported primitive (see <see cref="EncoderHelper.ToStrings"/>), every variable is replaced by the value itself;
    /// if it's an array of a supported type or of <see cref="object"/>, the variable is the element index (ex: "{0}") or an element name (see <see cref="Get(string, IReadOnlyList{string})"/>);
    /// otherwise the variable names a public instance property or field.
    /// Use "{{" to emit a literal '{'. Strings, floating point values and <see cref="DateTime"/> values are url escaped.
    /// Elements of an <see cref="object"/> array are formatted at runtime based on their type, like the typed values (invariant culture), other types (ex: enums) use <see cref="Convert.ToString(object, IFormatProvider)"/>;
    /// they are always url escaped and null becomes an empty string.
    /// Compiled functions are cached per template (thread safe, a template may be compiled more than once under contention).
    /// </remarks>
    public static class UriParamsEncoder<T>
    {
        static readonly ConcurrentDictionary<string, Func<T, string>> Cache = new ConcurrentDictionary<string, Func<T, string>>();
        static readonly ConcurrentDictionary<string, Func<T, string>> NamedCache = new ConcurrentDictionary<string, Func<T, string>>();

        /// <summary>
        /// Get (or compile and cache) the formatting function for a template.
        /// </summary>
        /// <param name="textTemplate">The url template, variables are enclosed in { and }.</param>
        /// <returns>A function that formats a value into a url.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textTemplate"/> is null.</exception>
        /// <exception cref="FormatException">Nested '{', or a variable that names a missing member (or an invalid index for array types).</exception>
        /// <exception cref="NotSupportedException">A variable names a member of an unsupported type.</exception>
        public static Func<T, string> Get(string textTemplate) => Get(textTemplate, null);

        /// <summary>
        /// Get (or compile and cache) the formatting function for a template, where <typeparamref name="T"/> is an array whose elements can also be named (ex: method parameters).
        /// </summary>
        /// <param name="textTemplate">The url template, variables are enclosed in { and }.</param>
        /// <param name="elementNames">For array types: the names of the elements (by index), a variable can be an element name (ex: "{a}", case sensitive) or an index (ex: "{0}").
        /// The array is expected to have (at least) this many elements. Null if the elements are only referenced by index. Ignored for other types.</param>
        /// <returns>A function that formats a value into a url.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textTemplate"/> is null.</exception>
        /// <exception cref="FormatException">Nested '{', or a variable that names a missing member (or an invalid index / unknown element name for array types).</exception>
        /// <exception cref="NotSupportedException">A variable names a member of an unsupported type.</exception>
        public static Func<T, string> Get(string textTemplate, IReadOnlyList<String> elementNames)
        {
            ArgumentNullException.ThrowIfNull(textTemplate);
            var isNamed = (elementNames != null) && typeof(T).IsArray;
            var cache = isNamed ? NamedCache : Cache;
            var key = isNamed ? String.Concat(textTemplate, "\0", String.Join('\0', elementNames)) : textTemplate;
            if (cache.TryGetValue(key, out var con))
                return con;
            List<Tuple<bool, string>> tokens = new List<Tuple<bool, string>>();
            var l = textTemplate.Length;
            StringBuilder tempB = new StringBuilder(l);
            bool inVar = false;
            for (int i = 0; i < l; ++i)
            {
                var c = textTemplate[i];
                if (c == '{')
                {
                    var n = i + 1;
                    if (n < l && textTemplate[n] == '{')
                    {
                        tempB.Append('{');
                        ++i;
                        continue;
                    }
                    if (inVar)
                        throw new FormatException("Nested { not allowed!");
                    inVar = true;
                    if (tempB.Length > 0)
                    {
                        tokens.Add(Tuple.Create(false, tempB.ToString()));
                        tempB.Clear();
                    }
                    continue;
                }
                if (c == '}' && inVar)
                {
                    if (tempB.Length > 0)
                    {
                        tokens.Add(Tuple.Create(true, tempB.ToString()));
                        tempB.Clear();
                    }
                    inVar = false;
                    continue;
                }
                tempB.Append(c);
            }
            if (tempB.Length > 0)
                tokens.Add(Tuple.Create(inVar, tempB.ToString()));

            var t = typeof(T);
            var sb = EncoderHelper.Sb;
            var p = Expression.Parameter(t, "src");
            List<Expression> program = new List<Expression>(50);
            program.Add(EncoderHelper.SbNew);
            var toStrings = EncoderHelper.ToStrings;
            Expression sv = toStrings.TryGetValue(t, out var tss) ? tss(p) : null;
            Func<string, Expression> toIndex = null;
            if (t.IsArray)
            {
                var et = t.GetElementType();
                Func<Expression, Expression> tsa;
                if (!toStrings.TryGetValue(et, out tsa) && (et == typeof(Object)))
                    tsa = EncoderHelper.ObjectToString;
                if (tsa != null)
                {
                    toIndex = s =>
                    {
                        int index = -1;
                        if (isNamed)
                        {
                            var c = elementNames.Count;
                            for (int i = 0; i < c; ++i)
                            {
                                if (String.Equals(elementNames[i], s, StringComparison.Ordinal))
                                {
                                    index = i;
                                    break;
                                }
                            }
                        }
                        if (index < 0)
                        {
                            if (!int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index))
                                throw new FormatException("Variable \"" + s + "\" isn't " + (isNamed ? "an element name or " : "") + "an index of type \"" + t.FullName + "\"");
                            if (isNamed && (index >= elementNames.Count))
                                throw new FormatException("Index " + index + " is out of range, there are " + elementNames.Count + " elements");
                        }
                        return tsa(Expression.ArrayAccess(p, Expression.Constant(index)));
                    };
                }
            }

            foreach (var token in tokens)
            {
                if (!token.Item1)
                {
                    program.Add(Expression.Call(sb, EncoderHelper.SbAppendString, Expression.Constant(token.Item2)));
                    continue;
                }
                if (sv != null)
                {
                    program.Add(Expression.Call(sb, EncoderHelper.SbAppendString, sv));
                    continue;
                }
                if (toIndex != null)
                {
                    program.Add(Expression.Call(sb, EncoderHelper.SbAppendString, toIndex(token.Item2)));
                    continue;
                }
                var pi = t.GetProperty(token.Item2, BindingFlags.Instance | BindingFlags.Public);
                if (pi != null)
                {
                    if (toStrings.TryGetValue(pi.PropertyType, out var ts))
                    {
                        var val = ts(Expression.Property(p, pi));
                        program.Add(Expression.Call(sb, EncoderHelper.SbAppendString, val));
                        continue;
                    }
                    throw new NotSupportedException("Members of type \"" + pi.PropertyType.FullName + "\" isn't supported!");
                }
                var fi = t.GetField(token.Item2, BindingFlags.Instance | BindingFlags.Public);
                if (fi != null)
                {
                    if (toStrings.TryGetValue(fi.FieldType, out var ts))
                    {
                        var val = ts(Expression.Field(p, fi));
                        program.Add(Expression.Call(sb, EncoderHelper.SbAppendString, val));
                        continue;
                    }
                    throw new NotSupportedException("Members of type \"" + fi.FieldType.FullName + "\" isn't supported!");
                }
                throw new FormatException("Member \"" + token.Item2 + "\" isn't found in type \"" + t.FullName + "\"");
            }
            program.Add(Expression.Call(sb, EncoderHelper.SbToString));
            var block = Expression.Block(EncoderHelper.SbBlock, program.ToArray());
            var lam = Expression.Lambda<Func<T, string>>(block, p);
            con = lam.Compile();
            cache.TryAdd(key, con);
            return con;
        }
    }

}
