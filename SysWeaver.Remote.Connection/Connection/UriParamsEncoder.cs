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
    /// if it's an array of a supported type, the variable name is the element index (ex: "{0}"); otherwise the variable names a public instance property or field.
    /// Use "{{" to emit a literal '{'. Only string values are url escaped.
    /// Compiled functions are cached per template (thread safe, a template may be compiled more than once under contention).
    /// </remarks>
    public static class UriParamsEncoder<T>
    {
        static readonly ConcurrentDictionary<string, Func<T, string>> Cache = new ConcurrentDictionary<string, Func<T, string>>();

        /// <summary>
        /// Get (or compile and cache) the formatting function for a template.
        /// </summary>
        /// <param name="textTemplate">The url template, variables are enclosed in { and }.</param>
        /// <returns>A function that formats a value into a url.</returns>
        /// <exception cref="Exception">Nested '{', a variable that names a missing member, or a member of an unsupported type.</exception>
        public static Func<T, string> Get(string textTemplate)
        {
            var cache = Cache;
            if (cache.TryGetValue(textTemplate, out var con))
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
                        throw new Exception("Nested { not allowed!");
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
                if (toStrings.TryGetValue(t.GetElementType(), out var tsa))
                    toIndex = s => tsa(Expression.ArrayAccess(p, Expression.Constant(int.Parse(s))));
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
                    throw new Exception("Members of type \"" + pi.PropertyType.FullName + "\" isn't supported!");
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
                    throw new Exception("Members of type \"" + fi.FieldType.FullName + "\" isn't supported!");
                }
                throw new Exception("Member \"" + token.Item2 + "\" isn't found in type \"" + t.FullName + "\"");
            }
            program.Add(Expression.Call(sb, EncoderHelper.SbToString));
            var block = Expression.Block(EncoderHelper.SbBlock, program.ToArray());
            var lam = Expression.Lambda<Func<T, string>>(block, p);
            con = lam.Compile();
            cache.TryAdd(textTemplate, con);
            return con;
        }
    }

}
