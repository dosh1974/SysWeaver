using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// The context passed to a compiled workflow delegate
    /// </summary>
    readonly struct ComfyUiRunContext
    {
        public ComfyUiRunContext(ComfyUiService service, CancellationToken cancel)
        {
            Service = service;
            Cancel = cancel;
        }

        public readonly ComfyUiService Service;
        public readonly CancellationToken Cancel;
    }


    /// <summary>
    /// Cache of compiled workflow delegates for some input and output types.
    /// The key is the workflow's cache key (identifies the workflow file and version).
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <typeparam name="R">The output type</typeparam>
    static class WorkflowCache<T, R> where R : new()
    {
        static readonly ConcurrentDictionary<String, Func<T, ComfyUiRunContext, Task<R>>> Cache = new(StringComparer.Ordinal);

        /// <summary>
        /// Get (or create) the delegate that runs a workflow
        /// </summary>
        /// <param name="wf">The workflow</param>
        /// <param name="msg">Where to report mapping warnings (only reported when created)</param>
        /// <returns>The delegate</returns>
        public static Func<T, ComfyUiRunContext, Task<R>> Get(ComfyUiWorkflow wf, IMessageHost msg)
        {
            var c = Cache;
            var key = wf.CacheKey;
            if (c.TryGetValue(key, out var f))
                return f;
            //  Lock so that warnings are only reported once
            lock (c)
            {
                if (c.TryGetValue(key, out f))
                    return f;
                f = WorkflowMapper.Build<T, R>(wf, msg);
                c[key] = f;
                return f;
            }
        }
    }


    /// <summary>
    /// Builds delegates (using Linq expressions) that maps typed inputs and outputs to a workflow
    /// </summary>
    static class WorkflowMapper
    {
        sealed record InputMap(String Tag, String Input, MemberInfo Member, Type Type);
        sealed record TagMap(String Tag, MemberInfo Member, Type Type);

        /// <summary>
        /// A candidate input to match, Tag and Input are the names used for matching, PTag and PInput are the actual tag and input to set
        /// </summary>
        sealed record Cand(String Tag, String Input, JsonValueKind Kind, String Target, String PTag, String PInput);

        public static Func<T, ComfyUiRunContext, Task<R>> Build<T, R>(ComfyUiWorkflow wf, IMessageHost msg) where R : new()
        {
            var prefix = String.Concat("ComfyUi workflow ", wf.Name.ToQuoted(), ": ");
            var writer = BuildWriter<T>(wf, msg, prefix);
            var reader = BuildReader<R>(wf, msg, prefix);
            //  (t, ctx) => ctx.Service.Execute<T, R>(ctx.Cancel, wf, t, writer, reader)
            var tp = Expression.Parameter(typeof(T), "t");
            var cp = Expression.Parameter(typeof(ComfyUiRunContext), "ctx");
            var exec = typeof(ComfyUiService).GetMethod(nameof(ComfyUiService.Execute), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(T), typeof(R));
            var body = Expression.Call(
                Expression.Field(cp, nameof(ComfyUiRunContext.Service)),
                exec,
                Expression.Field(cp, nameof(ComfyUiRunContext.Cancel)),
                Expression.Constant(wf),
                tp,
                Expression.Constant(writer),
                Expression.Constant(reader));
            return Expression.Lambda<Func<T, ComfyUiRunContext, Task<R>>>(body, tp, cp).Compile();
        }

        #region Members

        /// <summary>
        /// Normalize a name for matching, only letters and digits are used and case is ignored, "my-sampler" and "MySampler" are equal
        /// </summary>
        static String Norm(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if (Char.IsLetterOrDigit(c))
                    sb.Append(Char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static IEnumerable<(MemberInfo Member, Type Type)> GetMembers(Type t, bool forWrite)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var p in t.GetProperties(flags))
            {
                if (p.GetIndexParameters().Length > 0)
                    continue;
                if (forWrite ? (p.SetMethod?.IsPublic != true) : (p.GetMethod?.IsPublic != true))
                    continue;
                if (p.IsDefined(typeof(ComfyUiIgnoreAttribute), true))
                    continue;
                yield return (p, p.PropertyType);
            }
            foreach (var f in t.GetFields(flags))
            {
                if (forWrite && f.IsInitOnly)
                    continue;
                if (f.IsDefined(typeof(ComfyUiIgnoreAttribute), true))
                    continue;
                yield return (f, f.FieldType);
            }
        }

        static String MemberName(Type t, MemberInfo m) => String.Concat(t.Name, '.', m.Name).ToQuoted();

        static bool IsBool(Type t) => (t == typeof(bool)) || (t == typeof(bool?));

        static bool IsNumeric(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return !t.IsEnum;
            }
            return false;
        }

        static bool IsFile(Type t) => (t == typeof(byte[])) || (t == typeof(ComfyUiFile)) || (t == typeof(Uri));

        /// <summary>
        /// Check if a member type is compatible with the type of the workflow (default) value
        /// </summary>
        static String GetTypeWarning(Type t, JsonValueKind kind)
        {
            var u = Nullable.GetUnderlyingType(t) ?? t;
            switch (kind)
            {
                case JsonValueKind.Number:
                    if (!IsNumeric(u))
                        return "the workflow value is a number";
                    break;
                case JsonValueKind.True:
                    if (u != typeof(bool))
                        return "the workflow value is a bool";
                    break;
                case JsonValueKind.String:
                    if (IsNumeric(u) || (u == typeof(bool)))
                        return "the workflow value is a string";
                    break;
            }
            if (IsFile(u) && (kind != JsonValueKind.String))
                return "files can only be used for string (file name) inputs";
            return null;
        }

        #endregion//Members

        #region Inputs

        static Action<T, Utf8JsonWriter> BuildWriter<T>(ComfyUiWorkflow wf, IMessageHost msg, String prefix)
        {
            var type = typeof(T);
            var inputs = wf.Inputs;
            var targets = wf.InputTargets;
            String TargetOf(String key) => targets.TryGetValue(key, out var target) ? target : key;
            //  The actual tag inputs
            var allInputs = inputs.SelectMany(x => x.Value.Select(y =>
            {
                var key = String.Concat(x.Key, '.', y.Key);
                return new Cand(x.Key, y.Key, y.Value, TargetOf(key), x.Key, y.Key);
            })).ToList();
            //  Subgraph inputs, the subgraph node tag and input label (behaves like a normal annotated node)
            foreach (var sg in wf.SubgraphInputs)
            {
                foreach (var si in sg.Value)
                {
                    var dot = si.Value.IndexOf('.');
                    var pTag = si.Value.Substring(0, dot);
                    var pInput = si.Value.Substring(dot + 1);
                    if (inputs.TryGetValue(pTag, out var ti) && ti.TryGetValue(pInput, out var kind))
                        allInputs.Add(new Cand(sg.Key, si.Key, kind, TargetOf(si.Value), pTag, pInput));
                }
            }
            //  Number of distinct inputs per (logical) tag
            var tagInputCount = allInputs.GroupBy(x => x.Tag, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Select(y => y.Target).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
            var inputMaps = new List<InputMap>();
            var tagMaps = new List<TagMap>();
            var used = new Dictionary<String, MemberInfo>(StringComparer.Ordinal);

            String Available() => allInputs.Count == 0 ? "none" : String.Join(", ", allInputs.Select(x => String.Concat(x.Tag, '.', x.Input)).Distinct(StringComparer.Ordinal));

            //  Different tags can expose the same node input (ex: a manual and a subgraph annotation), these are not ambiguous
            List<Cand> Distinct(IEnumerable<Cand> c)
                => c.GroupBy(x => x.Target, StringComparer.Ordinal).Select(x => x.First()).ToList();

            void AddInput(MemberInfo m, Type t, Cand c)
            {
                var key = String.Concat(c.Tag, '.', c.Input);
                if (used.TryGetValue(c.Target, out var prev))
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to ", key.ToQuoted(), " that is already mapped by ", MemberName(type, prev), ", ignored!"), MessageLevels.Warning);
                    return;
                }
                used.Add(c.Target, m);
                var w = GetTypeWarning(t, c.Kind);
                if (w != null)
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " of type ", t.Name.ToQuoted(), " maps to ", key.ToQuoted(), " but ", w, ", may fail!"), MessageLevels.Warning);
                inputMaps.Add(new InputMap(c.PTag, c.PInput, m, t));
            }

            void AddTag(MemberInfo m, Type t, String tag)
            {
                var key = "!" + tag;
                if (used.TryGetValue(key, out var prev))
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to the node bypass of ", tag.ToQuoted(), " that is already mapped by ", MemberName(type, prev), ", ignored!"), MessageLevels.Warning);
                    return;
                }
                used.Add(key, m);
                tagMaps.Add(new TagMap(tag, m, t));
            }

            foreach (var (m, t) in GetMembers(type, false))
            {
                var attr = m.GetCustomAttribute<ComfyUiNameAttribute>(true);
                if (attr != null)
                {
                    var name = attr.Name ?? "";
                    var dot = name.IndexOf('.');
                    if (dot < 0)
                    {
                        if (!IsBool(t))
                        {
                            msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to the tag ", name.ToQuoted(), " but only bool's can be mapped to a tag (to bypass the node), ignored!"), MessageLevels.Warning);
                            continue;
                        }
                        var tag = wf.Tags.FirstOrDefault(x => String.Equals(x, name, StringComparison.OrdinalIgnoreCase));
                        if (tag == null)
                        {
                            msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to the tag ", name.ToQuoted(), " that doesn't exist in the workflow, ignored! Available tags: ", String.Join(", ", wf.Tags)), MessageLevels.Warning);
                            continue;
                        }
                        AddTag(m, t, tag);
                    }
                    else
                    {
                        var tagName = name.Substring(0, dot);
                        var inputName = name.Substring(dot + 1);
                        var match = allInputs.FirstOrDefault(x => String.Equals(x.Tag, tagName, StringComparison.OrdinalIgnoreCase) && String.Equals(x.Input, inputName, StringComparison.OrdinalIgnoreCase));
                        if (match == null)
                        {
                            msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to ", name.ToQuoted(), " that doesn't exist in the workflow, ignored! Available inputs: ", Available()), MessageLevels.Warning);
                            continue;
                        }
                        AddInput(m, t, match);
                    }
                    continue;
                }
                var n = Norm(m.Name);
                //  1. Tag and input name, ex: "SamplerSeed" or "Sampler_Seed" => "sampler.seed"
                var c = Distinct(allInputs.Where(x => (Norm(x.Tag) + Norm(x.Input)) == n));
                if (c.Count == 1)
                {
                    AddInput(m, t, c[0]);
                    continue;
                }
                if (c.Count > 1)
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is ambiguous, matches: ", String.Join(", ", c.Select(x => String.Concat(x.Tag, '.', x.Input))), ", ignored! Use the [ComfyUiName] attribute"), MessageLevels.Warning);
                    continue;
                }
                //  2. A tag with a single input, ex: "Seed" => "seed.seed" (subgraph inputs are annotated like this) or "Sampler" => "sampler.sampler_name"
                c = Distinct(allInputs.Where(x => (Norm(x.Tag) == n) && (tagInputCount[x.Tag] == 1) && (!IsBool(t) || (x.Kind == JsonValueKind.True))));
                if (c.Count == 1)
                {
                    AddInput(m, t, c[0]);
                    continue;
                }
                if (c.Count > 1)
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is ambiguous, matches: ", String.Join(", ", c.Select(x => String.Concat(x.Tag, '.', x.Input))), ", ignored! Use the [ComfyUiName] attribute"), MessageLevels.Warning);
                    continue;
                }
                //  3. A bool matching a tag name, false => bypass the node(s)
                if (IsBool(t))
                {
                    var tc = wf.Tags.Where(x => Norm(x) == n).ToList();
                    if (tc.Count == 1)
                    {
                        AddTag(m, t, tc[0]);
                        continue;
                    }
                    if (tc.Count > 1)
                    {
                        msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is ambiguous, matches the tags: ", String.Join(", ", tc), ", ignored! Use the [ComfyUiName] attribute"), MessageLevels.Warning);
                        continue;
                    }
                }
                //  4. A (unique) input name, ex: "Seed" => "sampler.seed"
                c = Distinct(allInputs.Where(x => Norm(x.Input) == n));
                if (c.Count == 1)
                {
                    AddInput(m, t, c[0]);
                    continue;
                }
                if (c.Count > 1)
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is ambiguous, matches: ", String.Join(", ", c.Select(x => String.Concat(x.Tag, '.', x.Input))), ", ignored! Prefix the name with the tag name or use the [ComfyUiName] attribute"), MessageLevels.Warning);
                    continue;
                }
                msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " can't be mapped to any input, ignored! Available inputs: ", Available()), MessageLevels.Warning);
            }
            if (msg != null)
            {
                foreach (var x in inputMaps)
                    msg.AddMessage(String.Concat(prefix, "Input ", (x.Tag + '.' + x.Input).ToQuoted(), " <= ", MemberName(type, x.Member)), MessageLevels.Debug);
                foreach (var x in tagMaps)
                    msg.AddMessage(String.Concat(prefix, "Bypass ", x.Tag.ToQuoted(), " <= ", MemberName(type, x.Member)), MessageLevels.Debug);
            }

            //  Build the writer, the payload is: { "tag": { "input": value, .. } | false, .. }
            var tp = Expression.Parameter(typeof(T), "t");
            var wp = Expression.Parameter(typeof(Utf8JsonWriter), "w");
            var writePropertyName = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WritePropertyName), [typeof(String)]);
            var writeStartObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartObject), Type.EmptyTypes);
            var writeEndObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndObject), Type.EmptyTypes);
            var writeBoolean = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteBoolean), [typeof(String), typeof(bool)]);
            var body = new List<Expression>();
            var inputsByTag = inputMaps.GroupBy(x => x.Tag, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
            var tagsByTag = tagMaps.ToDictionary(x => x.Tag, StringComparer.Ordinal);
            foreach (var tag in inputsByTag.Keys.Union(tagsByTag.Keys))
            {
                Expression inputsBlock = null;
                if (inputsByTag.TryGetValue(tag, out var tagInputs))
                {
                    var b = new List<Expression>
                    {
                        Expression.Call(wp, writePropertyName, Expression.Constant(tag)),
                        Expression.Call(wp, writeStartObject),
                    };
                    foreach (var x in tagInputs)
                        b.Add(GetWrite(wp, x.Input, Expression.MakeMemberAccess(tp, x.Member)));
                    b.Add(Expression.Call(wp, writeEndObject));
                    inputsBlock = Expression.Block(b);
                }
                if (!tagsByTag.TryGetValue(tag, out var tm))
                {
                    body.Add(inputsBlock);
                    continue;
                }
                var v = Expression.MakeMemberAccess(tp, tm.Member);
                Expression bypass = tm.Type == typeof(bool)
                    ? Expression.Not(v)
                    : Expression.AndAlso(Expression.Property(v, "HasValue"), Expression.Not(Expression.Property(v, "Value")));
                var writeBypass = Expression.Call(wp, writeBoolean, Expression.Constant(tag), Expression.Constant(false));
                body.Add(inputsBlock == null
                    ? Expression.IfThen(bypass, writeBypass)
                    : Expression.IfThenElse(bypass, writeBypass, inputsBlock));
            }
            Expression lb = body.Count == 0 ? Expression.Empty() : Expression.Block(body);
            if (!type.IsValueType && (body.Count > 0))
                lb = Expression.IfThen(Expression.NotEqual(tp, Expression.Constant(null, type)), lb);
            return Expression.Lambda<Action<T, Utf8JsonWriter>>(lb, tp, wp).Compile();
        }

        static readonly IReadOnlyDictionary<Type, MethodInfo> InputWriters =
            typeof(ComfyUiJson).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.Name == nameof(ComfyUiJson.WriteInput))
            .ToDictionary(x => x.GetParameters()[2].ParameterType);

        static readonly MethodInfo WriteEnumMethod = typeof(ComfyUiJson).GetMethod(nameof(ComfyUiJson.WriteEnum));
        static readonly MethodInfo WriteObjectMethod = typeof(ComfyUiJson).GetMethod(nameof(ComfyUiJson.WriteObject));

        static Expression GetWrite(ParameterExpression w, String input, Expression v)
        {
            var t = v.Type;
            var u = Nullable.GetUnderlyingType(t);
            if (u != null)
                return Expression.IfThen(Expression.Property(v, "HasValue"), GetWrite(w, input, Expression.Property(v, "Value")));
            var name = Expression.Constant(input);
            if (InputWriters.TryGetValue(t, out var m))
                return Expression.Call(m, w, name, v);
            if (t.IsEnum)
                return Expression.Call(WriteEnumMethod.MakeGenericMethod(t), w, name, v);
            return Expression.Call(WriteObjectMethod.MakeGenericMethod(t), w, name, v);
        }

        #endregion//Inputs

        #region Outputs

        static Func<JsonElement, R> BuildReader<R>(ComfyUiWorkflow wf, IMessageHost msg, String prefix) where R : new()
        {
            var type = typeof(R);
            var ep = Expression.Parameter(typeof(JsonElement), "e");
            var rv = Expression.Variable(type, "r");
            var body = new List<Expression>
            {
                Expression.Assign(rv, Expression.New(type)),
            };
            var used = new Dictionary<String, MemberInfo>(StringComparer.Ordinal);
            String Available() => wf.Outputs.Count == 0 ? "none" : String.Join(", ", wf.Outputs);
            foreach (var (m, t) in GetMembers(type, true))
            {
                var attr = m.GetCustomAttribute<ComfyUiNameAttribute>(true);
                String tag;
                if (attr != null)
                {
                    tag = wf.Outputs.FirstOrDefault(x => String.Equals(x, attr.Name, StringComparison.OrdinalIgnoreCase));
                    if (tag == null)
                    {
                        msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to the output ", attr.Name.ToQuoted(), " that doesn't exist in the workflow, ignored! Available outputs: ", Available()), MessageLevels.Warning);
                        continue;
                    }
                }
                else
                {
                    var n = Norm(m.Name);
                    var c = wf.Outputs.Where(x => Norm(x) == n).ToList();
                    if (c.Count > 1)
                    {
                        msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is ambiguous, matches the outputs: ", String.Join(", ", c), ", ignored! Use the [ComfyUiName] attribute"), MessageLevels.Warning);
                        continue;
                    }
                    if (c.Count == 0)
                    {
                        msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " can't be mapped to any output, ignored! Available outputs: ", Available()), MessageLevels.Warning);
                        continue;
                    }
                    tag = c[0];
                }
                var read = GetRead(t, ep, tag);
                if (read == null)
                {
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " is of type ", t.Name.ToQuoted(), " that is not supported for outputs (use byte[], byte[][], List<byte[]>, String, String[] or List<String>), ignored!"), MessageLevels.Warning);
                    continue;
                }
                if (used.TryGetValue(tag, out var prev))
                    msg?.AddMessage(String.Concat(prefix, "Property ", MemberName(type, m), " maps to the output ", tag.ToQuoted(), " that is also mapped by ", MemberName(type, prev)), MessageLevels.Info);
                else
                    used.Add(tag, m);
                msg?.AddMessage(String.Concat(prefix, "Output ", tag.ToQuoted(), " => ", MemberName(type, m)), MessageLevels.Debug);
                body.Add(Expression.Assign(Expression.MakeMemberAccess(rv, m), read));
            }
            if (msg != null)
            {
                foreach (var x in wf.Outputs)
                    if (!used.ContainsKey(x))
                        msg.AddMessage(String.Concat(prefix, "The output ", x.ToQuoted(), " isn't used by ", type.Name.ToQuoted()), MessageLevels.Debug);
            }
            body.Add(rv);
            return Expression.Lambda<Func<JsonElement, R>>(Expression.Block([rv], body), ep).Compile();
        }

        static Expression GetRead(Type t, ParameterExpression e, String tag)
        {
            var tc = Expression.Constant(tag);
            Expression Call(String name) => Expression.Call(typeof(ComfyUiJson).GetMethod(name), e, tc);
            if (t == typeof(byte[]))
                return Call(nameof(ComfyUiJson.ReadBytes));
            if (t == typeof(String))
                return Call(nameof(ComfyUiJson.ReadString));
            if (t == typeof(List<byte[]>))
                return Call(nameof(ComfyUiJson.ReadBytesList));
            if (t == typeof(List<String>))
                return Call(nameof(ComfyUiJson.ReadStringList));
            if (t == typeof(Object))
                return null;
            if (t.IsAssignableFrom(typeof(byte[][])))
                return Expression.Convert(Call(nameof(ComfyUiJson.ReadBytesArray)), t);
            if (t.IsAssignableFrom(typeof(String[])))
                return Expression.Convert(Call(nameof(ComfyUiJson.ReadStringArray)), t);
            return null;
        }

        #endregion//Outputs
    }

}
