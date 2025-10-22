using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections;


using SysWeaver.Docs;
using SysWeaver.Parser;

namespace SysWeaver
{

    public sealed class CommandLine
    {
        public static String DefaultOptionsPrefix = "-";
        public static String RequiredArgumentStart = "";
        public static String RequiredArgumentEnd = "";
        public static String OptionalArgumentStart = "<";
        public static String OptionalArgumentEnd = ">";
        public static String OptionalTag = "optional";

        public static String TagStart = "[";
        public static String TagEnd = "]";

        public static String MakeTag(String v) => String.Concat(TagStart, v, TagEnd);

        public static readonly IReadOnlySet<String> AdditionalOptionPrefixes = ReadOnlyData.Set(StringComparer.Ordinal,
                "--", "/"
            );

        public static StringComparer DefaultOptionsComparer = StringComparer.OrdinalIgnoreCase;


        public static SByte ParseSByte(String value) => (SByte)(Int64)ExpressionEvaluator.Get(typeof(SByte)).ToValue(value.Trim());
        public static Int16 ParseInt16(String value) => (Int16)(Int64)ExpressionEvaluator.Get(typeof(Int16)).ToValue(value.Trim());
        public static Int32 ParseInt32(String value) => (Int32)(Int64)ExpressionEvaluator.Get(typeof(Int32)).ToValue(value.Trim());
        public static Int64 ParseInt64(String value) => (Int64)ExpressionEvaluator.Get(typeof(Int64)).ToValue(value.Trim());

        public static Byte ParseByte(String value) => (Byte)(UInt64)ExpressionEvaluator.Get(typeof(Byte)).ToValue(value.Trim());
        public static UInt16 ParseUInt16(String value) => (UInt16)(UInt64)ExpressionEvaluator.Get(typeof(UInt16)).ToValue(value.Trim());
        public static UInt32 ParseUInt32(String value) => (UInt32)(UInt64)ExpressionEvaluator.Get(typeof(UInt32)).ToValue(value.Trim());
        public static UInt64 ParseUInt64(String value) => (UInt64)ExpressionEvaluator.Get(typeof(UInt64)).ToValue(value.Trim());

        public static Single ParseSingle(String value) => (Single)(Double)ExpressionEvaluator.Get(typeof(Single)).ToValue(value.Trim());
        public static Double ParseDouble(String value) => (Double)ExpressionEvaluator.Get(typeof(Double)).ToValue(value.Trim());
        public static Decimal ParseDecimal(String value) => (Decimal)ExpressionEvaluator.Get(typeof(Decimal)).ToValue(value.Trim());

        public static String ParseString(String value) => value;
        public static Boolean ParseBoolean(String value) => Boolean.Parse(value);

        public static DateTime ParseDateTime(String value) => DateTime.Parse(value.Trim());
        public static TimeSpan ParseTimeSpan(String value) => TimeSpan.Parse(value.Trim());
        public static Guid ParseGuid(String value) => Guid.Parse(value.Trim());

        public static Char ParseChar(String value)
        {
            value = value.Trim();
            if (value.StartsWith('\''))
            {
                if (value.Length != 3)
                    throw new ArgumentException("Expected a char, ex: 'x'!, found " + value, nameof(value));
                if (!value.EndsWith('\''))
                    throw new ArgumentException("Expected a char, ex: 'x'!, found " + value, nameof(value));
                return value[1];
            }
            if (value.StartsWith('"'))
            {
                if (value.Length != 3)
                    throw new ArgumentException("Expected a char, ex: \"x\"!, found " + value, nameof(value));
                if (!value.EndsWith('"'))
                    throw new ArgumentException("Expected a char, ex: \"x\"!, found " + value, nameof(value));
                return value[1];
            }
            return (Char)(((UInt32)(UInt64)ExpressionEvaluator.Get(typeof(UInt32)).ToValue(value.Trim())) & 0xffff);
        }


        public static void AddParser(Type type, Func<String, Object> parser)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));
            Parsers.Add(type, parser);
        }

        static Object MakeTypedArray(Type type, IEnumerable<Object> objects)
        {
            var values = objects.Select(x => Expression.Convert(Expression.Constant(x), type)).ToArray();
            var body = Expression.Convert(Expression.NewArrayInit(type, values), typeof(Object));
            var lambda = Expression.Lambda<Func<Object>>(body).Compile();
            return lambda();
        }

        static Object MakeListT(Type type, IEnumerable<Object> objects)
        {
            var values = MakeTypedArray(type, objects);
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(type), values);
        }

        static IEnumerable<Object> GetObjects(String t, Func<String, Object> parser)
        {
            return t.Split(';').Where(x => !String.IsNullOrEmpty(x)).Select(x => parser(x.Trim()));
        }

        public static Func<String, Object> GetParser(Type type, bool throwOnError = true)
        {
            if (type.IsEnum)
                return value => Enum.Parse(type, value.Trim());
            if (type.IsArray)
            {
                var et = type.GetElementType();
                var ep = GetParser(et, throwOnError);
                return value => MakeTypedArray(et, GetObjects(value, ep));
            }
            if (type.IsGenericType)
            {
                var bt = type.GetGenericTypeDefinition();
                var et = type.GetGenericArguments().FirstOrDefault();
                if (bt == typeof(List<>))
                {
                    var ep = GetParser(et, throwOnError);
                    return value => MakeListT(et, GetObjects(value, ep));
                }
                if (bt == typeof(IEnumerable<>))
                {
                    var ep = GetParser(et, throwOnError);
                    return value => MakeTypedArray(et, GetObjects(value, ep));
                }
            }
            if (Parsers.TryGetValue(type, out var p))
                return p;
            if (throwOnError)
                throw new InvalidCastException(String.Concat("Do not know how parse a string into ", type.FullName));
            return null;
        }

        static IEnumerable<Object> ToObjectEnumerable(IEnumerable e)
        {
            foreach (var x in e)
                yield return x;
        }

        public static String FormatValueText(Object o)
        {
            if (o == null)
                return "null";
            var ot = o.GetType();
            if (ot == typeof(String))
                return o.ToString().ToQuoted();
            if (ot == typeof(Char))
                return String.Concat('\'', o, "' ", (int)(Char)o, " 0x", ((int)(Char)o).ToString("x"));
            var oa = o as IEnumerable;
            if (oa != null)
                return String.Concat('[', String.Join("; ", ToObjectEnumerable(oa).Select(x => FormatValueText(x))), ']');
            return o.ToString();
        }

        static readonly Dictionary<Type, Func<String, Object>> Parsers = new Dictionary<Type, Func<string, object>>()
        {
            { typeof(SByte), s => (Object)ParseSByte(s) },
            { typeof(Int16), s => (Object)ParseInt16(s) },
            { typeof(Int32), s => (Object)ParseInt32(s) },
            { typeof(Int64), s => (Object)ParseInt64(s) },
            { typeof(Byte), s => (Object)ParseByte(s) },
            { typeof(UInt16), s => (Object)ParseUInt16(s) },
            { typeof(UInt32), s => (Object)ParseUInt32(s) },
            { typeof(UInt64), s => (Object)ParseUInt64(s) },
            { typeof(Single), s => (Object)ParseSingle(s) },
            { typeof(Double), s => (Object)ParseDouble(s) },
            { typeof(Decimal), s => (Object)ParseDecimal(s) },
            { typeof(String), s => (Object)ParseString(s) },
            { typeof(Char), s => (Object)ParseChar(s) },
            { typeof(Boolean), s => (Object)ParseBoolean(s) },
            { typeof(DateTime), s => (Object)ParseDateTime(s) },
            { typeof(TimeSpan), s => (Object)ParseTimeSpan(s) },
            { typeof(Guid), s => (Object)ParseGuid(s) },
        };

        public static bool IsOption(ref String v)
        {
            if (v.StartsWith(DefaultOptionsPrefix, StringComparison.Ordinal))
            {
                v = v.Substring(DefaultOptionsPrefix.Length);
                return true;
            }
            foreach (var x in AdditionalOptionPrefixes)
            {
                if (v.StartsWith(x, StringComparison.Ordinal))
                {
                    v = v.Substring(x.Length);
                    return true;
                }
            }
            return false;
        }

        #region Type options

        public enum OptionMembers
        {
            Properties = 0,
            Fields = 1,
            All = 2,
        }

        public static IEnumerable<Tuple<CommandLineOption, Action<Object, Object>>> GetOptions(Object o, OptionMembers members = OptionMembers.All)
        {
            yield return HelpAction1;
            yield return HelpAction2;
            var obj = Expression.Parameter(typeof(Object), "obj");
            var value = Expression.Parameter(typeof(Object), "value");
            foreach (var x in InternalGetOptions(o, members, Expression.Convert(obj, o.GetType()), "", obj, value))
                yield return x;
        }

        #endregion //Type options


        public readonly Tuple<CommandLineArgument, Object>[] Arguments;
        public readonly Tuple<CommandLineOption, Object[]>[] Options;

        public static readonly CommandLineOption HelpOption1 = CommandLineOption.Make("?", "Show help");
        public static readonly CommandLineOption HelpOption2 = CommandLineOption.Make("help", "Show help");

        static readonly Tuple<CommandLineOption, Action<Object, Object>> HelpAction1 = new Tuple<CommandLineOption, Action<object, object>>(HelpOption1, null);
        static readonly Tuple<CommandLineOption, Action<Object, Object>> HelpAction2 = new Tuple<CommandLineOption, Action<object, object>>(HelpOption2, null);

        CommandLine(Tuple<CommandLineArgument, Object>[] arguments, Tuple<CommandLineOption, Object[]>[] options)
        {
            Options = options;
            Arguments = arguments;
        }

        public static CommandLine ParseOptions(String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, IEnumerable<CommandLineOption> options, StringComparer optionsComparer)
        {
            int l = commandLineArgs.Length;
            List<String> outArgs = new List<String>(l);
            Dictionary<String, CommandLineOption> sopt = new Dictionary<string, CommandLineOption>(optionsComparer);
            if (options != null)
            {
                foreach (var x in options)
                    sopt.Add(x.Name, x);
            }
            List<Tuple<CommandLineOption, Object[]>> usedOptions = new List<Tuple<CommandLineOption, object[]>>(l);
            var validArgs = arguments.ToList();
            var maxArgCount = validArgs.Count;
            var minArgCount = validArgs.Count(x => !x.Optional);
            int i = 0;
            while (i < l)
            {
                var vo = commandLineArgs[i];
                var v = vo;
                if (IsOption(ref v))
                {
                    if (sopt.TryGetValue(v, out var opt))
                    {
                        Object[] oas = null;
                        var oal = (opt.Args?.Length ?? 0);
                        if (oal > 0)
                        {
                            oas = GC.AllocateUninitializedArray<Object>(oal);
                            for (int j = 0; j < oal; ++j)
                            {
                                ++i;
                                var oa = opt.Args[j];
                                if (i >= l)
                                    throw new ArgumentException(String.Concat("Not enough parameters to option ", vo, " expected ", oa.Name, '!'), nameof(commandLineArgs));
                                oas[j] = oa.ParseValue(commandLineArgs[i]);
                            }
                        }
                        usedOptions.Add(Tuple.Create(opt, oas));
                        if ((opt == HelpOption1) || (opt == HelpOption2))
                            return new CommandLine(null, usedOptions.ToArray());
                    }
                    else
                    {
                        throw new ArgumentException(String.Concat("Option ", vo, " is unknown!"), nameof(commandLineArgs));
                    }
                }
                else
                {
                    if (outArgs.Count >= maxArgCount)
                        throw new ArgumentException(String.Concat(minArgCount == maxArgCount ? "Too many arguments! Expected " : "Too many arguments! Expected at most ", maxArgCount, maxArgCount == 1 ? " argument" : " arguments"), nameof(commandLineArgs));
                    outArgs.Add(vo);
                }
                ++i;
            }
            int argCount = outArgs.Count;
            if (argCount < minArgCount)
                throw new ArgumentException(String.Concat(minArgCount == maxArgCount ? "Too few arguments! Expected " : "Too few arguments! Expected at least ", minArgCount, minArgCount == 1 ? " argument" : " arguments"), nameof(commandLineArgs));
            int lastRequired = validArgs.LastIndexOf(x => !x.Optional) + 1;
            int skip = Math.Max(0, lastRequired - argCount);
            int so = 0;
            var aa = new Tuple<CommandLineArgument, Object>[argCount];
            for (int s = 0; s < argCount; ++s)
            {
                var arg = validArgs[so];
                while ((skip > 0) && arg.Optional)
                {
                    ++so;
                    arg = validArgs[so];
                    --skip;
                }
                --skip;
                ++so;
                aa[s] = new Tuple<CommandLineArgument, object>(arg, arg.ParseValue(outArgs[s]));
            }
            return new CommandLine(aa, usedOptions.ToArray());
        }
        public static CommandLine ParseOptions(String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, IEnumerable<CommandLineOption> options) => ParseOptions(commandLineArgs, arguments, options, DefaultOptionsComparer);

        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, StringComparer optionsComparer, OptionMembers members = OptionMembers.Properties) where T : class, new() => ParseObject<T>(out options, commandLineArgs, arguments, new T(), optionsComparer, members);
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, OptionMembers members = OptionMembers.Properties) where T : class, new() => ParseObject<T>(out options, commandLineArgs, arguments, new T(), DefaultOptionsComparer, members);
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, T current, OptionMembers members = OptionMembers.Properties) where T : class => ParseObject<T>(out options, commandLineArgs, arguments, current, DefaultOptionsComparer, members);
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, T current, StringComparer optionsComparer, OptionMembers members = OptionMembers.Properties) where T : class
        {
            List<CommandLineOption> genOpts = new List<CommandLineOption>(commandLineArgs.Length);
            Dictionary<String, Action<Object, Object>> optSetters = new Dictionary<string, Action<object, object>>(optionsComparer);
            foreach (var opt in GetOptions(current, members))
            {
                genOpts.Add(opt.Item1);
                optSetters.Add(opt.Item1.Name, opt.Item2);
            }
            var t = ParseOptions(commandLineArgs, arguments, genOpts, optionsComparer);
            foreach (var v in t.Options)
            {
                var ck = v.Item1;
                if ((ck == HelpOption1) || (ck == HelpOption2))
                {
                    options = null;
                    return null;
                }
                optSetters[ck.Name](current, (v.Item2?.Length ?? 0) > 0 ? v.Item2[0] : null);
            }
            options = current;
            return t;
        }

        static IEnumerable<Tuple<CommandLineOption, Action<Object, Object>>> InternalGetOptions(Object o, OptionMembers members, Expression instance, String prefix, ParameterExpression obj, ParameterExpression value)
        {
            var type = o.GetType();
            foreach (var p in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                Object nested = null;
                Type nestedType = null;
                Expression nestedValue = null;
                {
                    var mi = p as PropertyInfo;
                    if ((mi != null) && (members != OptionMembers.Fields))
                    {
                        if (mi.CanRead)
                        {
                            var val = mi.GetValue(o);
                            var mt = mi.PropertyType;
                            var acc = Expression.Property(instance, mi);
                            var parser = GetParser(mt, false);
                            if (parser == null)
                            {
                                nested = val;
                                nestedType = mt;
                                nestedValue = acc;
                            }
                            else
                            {
                                if (mi.CanWrite)
                                {
                                    if ((mt == typeof(Boolean)) && (val != null) && (!((Boolean)val)))
                                    {
                                        var setter = Expression.Lambda<Action<Object, Object>>(Expression.Assign(acc, Expression.Constant(true)), obj, value).Compile();
                                        var desc = mi.XmlDoc()?.Summary;
                                        var cmd = CommandLineOption.Make(prefix + mi.Name, desc);
                                        yield return Tuple.Create(cmd, setter);
                                    }else
                                    {
                                        var setter = Expression.Lambda<Action<Object, Object>>(Expression.Assign(acc, Expression.Convert(value, mt)), obj, value).Compile();
                                        var desc = mi.XmlDoc()?.Summary;
                                        var cmd = val == null ? CommandLineOption.Make(prefix + mi.Name, desc, mt) : CommandLineOption.Make(prefix + mi.Name, desc, val);
                                        yield return Tuple.Create(cmd, setter);
                                    }
                                }
                            }
                        }
                    }
                }
                {
                    var mi = p as FieldInfo;
                    if ((mi != null) && (members != OptionMembers.Properties))
                    {
                        var val = mi.GetValue(o);
                        var mt = mi.FieldType;
                        var acc = Expression.Field(instance, mi);
                        var parser = GetParser(mt, false);
                        if (parser == null)
                        {
                            nested = val;
                            nestedType = mt;
                            nestedValue = acc;
                        }
                        else
                        {
                            if (!mi.IsInitOnly)
                            {
                                if ((mt == typeof(Boolean)) && (val != null) && (!((Boolean)val)))
                                {
                                    var setter = Expression.Lambda<Action<Object, Object>>(Expression.Assign(acc, Expression.Constant(true)), obj, value).Compile();
                                    var desc = mi.XmlDoc()?.Summary;
                                    var cmd = CommandLineOption.Make(prefix + mi.Name, desc);
                                    yield return Tuple.Create(cmd, setter);
                                }
                                else
                                {
                                    var setter = Expression.Lambda<Action<Object, Object>>(Expression.Assign(acc, Expression.Convert(value, mt)), obj, value).Compile();
                                    var desc = mi.XmlDoc()?.Summary;
                                    var cmd = val == null ? CommandLineOption.Make(prefix + mi.Name, desc, mt) : CommandLineOption.Make(prefix + mi.Name, desc, val);
                                    yield return Tuple.Create(cmd, setter);
                                }
                            }
                        }
                    }
                }
                if (nestedType != null)
                {
                    if (nested == null)
                    {
                        if (nestedType.GetConstructor([]) != null)
                            nested = Activator.CreateInstance(nestedType);
                    }
                    if (nested != null)
                    {
                        foreach (var x in InternalGetOptions(nested, members, nestedValue, String.Concat(prefix, p.Name, '.'), obj, value))
                            yield return x;
                    }
                }
            }
        }

        /// <summary>
        /// Name of the executable filename
        /// </summary>
        public static readonly String Executable = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location;
        
        /// <summary>
        /// Directory name where the executable exists
        /// </summary>
        public static readonly String ExecutableFolder = Path.GetDirectoryName(Executable);

        public static String JoinNonEmpty(String separator, params String[] values) => String.Join(separator, values.Where(x => !String.IsNullOrEmpty(x)));

        public static IEnumerable<String> SyntaxOptions(IEnumerable<CommandLineArgument> arguments, IEnumerable<CommandLineOption> options, String commandPrefix = null, String linePrefix = "", String levelInset = "  ")
        {
            commandPrefix = commandPrefix ?? String.Concat("Use: ", Path.GetFileNameWithoutExtension(Executable), ' ');
            levelInset = levelInset ?? "";
            linePrefix = linePrefix ?? "";
            yield return String.Concat(linePrefix, commandPrefix, OptionalArgumentStart, "Options", OptionalArgumentEnd, ' ', String.Join(' ', arguments.Select(x => String.Concat(x.Optional ? OptionalArgumentStart : RequiredArgumentStart, x.Name, x.Optional ? OptionalArgumentEnd : RequiredArgumentEnd))));
            yield return String.Concat(linePrefix, "Arguments:");
            var pad = arguments.Select(x => x.Name.Length).Max() + 1;
            foreach (var x in arguments)
                yield return String.Concat(linePrefix, levelInset, x.Name.PadRight(pad), String.Join(' ', x.Tags.Select(z => MakeTag(z))), ' ', x.HelpText ?? "");
            yield return String.Concat(linePrefix, "Options ", MakeTag(OptionalTag), ':');
            pad = options.Select(x => x.Syntax.Length).Max() + 1;
            foreach (var x in options)
            {
                var args = x.Args;
                var argLen = (args?.Length ?? 0);
//                if ((argLen == 1) && String.IsNullOrEmpty(x.HelpText))
                if ((argLen == 1) && (String.IsNullOrEmpty(x.HelpText) || String.IsNullOrEmpty(args[0].HelpText)))
                {
                    var y = args[0];
                    yield return String.Concat(linePrefix, levelInset, DefaultOptionsPrefix, x.Syntax.PadRight(pad), String.Join(' ', y.Tags.Select(z => MakeTag(z))), ' ', y.HelpText ?? x.HelpText ?? "");
                }
                else {
                    yield return String.Concat(linePrefix, levelInset, DefaultOptionsPrefix, x.Syntax.PadRight(pad), x.HelpText ?? "");
                    if (argLen > 0)
                    {
                        var pad2 = args.Select(y => y.Name.Length).Max() + 1;
                        foreach (var y in args)
                            yield return String.Concat(linePrefix, levelInset, levelInset, y.Name.PadRight(pad2), String.Join(' ', y.Tags.Select(z => MakeTag(z))), ' ', y.HelpText ?? "");
                    }
                }
            }
        }

        public static IEnumerable<String> SyntaxObject<T>(IEnumerable<CommandLineArgument> arguments, OptionMembers members = OptionMembers.Properties, String commandPrefix = null, String linePrefix = "", String levelInset = "  ") where T : new() => SyntaxOptions(arguments, GetOptions(new T(), members).Select(x => x.Item1), commandPrefix, linePrefix, levelInset);
        public static IEnumerable<String> SyntaxObject<T>(IEnumerable<CommandLineArgument> arguments, T current, OptionMembers members = OptionMembers.Properties, String commandPrefix = null, String linePrefix = "", String levelInset = "  ") => SyntaxOptions(arguments, GetOptions(current, members).Select(x => x.Item1), commandPrefix, linePrefix, levelInset);

    }


}
