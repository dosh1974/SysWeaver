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

    /// <summary>
    /// Command line parser and help (syntax) generator.
    /// <para>An instance is the result of a parse: the positional <see cref="Arguments"/> and the <see cref="Options"/> that were found.</para>
    /// <para>Options can be declared explicitly (<see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>)
    /// or generated from the public members of an options object (<see cref="ParseObject{T}(out T, string[], IEnumerable{CommandLineArgument}, T, StringComparer, OptionMembers)"/>),
    /// in which case the XML documentation summary of each member (via SysWeaver.Docs) becomes its help text.</para>
    /// </summary>
    /// <remarks>
    /// The static formatting fields (prefixes, tags, comparer) are global mutable settings shared by the whole process; change them once at startup.
    /// </remarks>
    public sealed class CommandLine
    {
        /// <summary>
        /// The primary option prefix, accepted by <see cref="IsOption(ref string)"/> and used when generating help text. Default is "-".
        /// </summary>
        /// <remarks>
        /// When several prefixes match a token, the longest one is removed (so "--help" is the option "help" with the default settings).
        /// </remarks>
        public static String DefaultOptionsPrefix = "-";
        /// <summary>
        /// Text written before a required argument name in the syntax line. Default is empty.
        /// </summary>
        public static String RequiredArgumentStart = "";
        /// <summary>
        /// Text written after a required argument name in the syntax line. Default is empty.
        /// </summary>
        public static String RequiredArgumentEnd = "";
        /// <summary>
        /// Text written before an optional argument name (and "Options") in the syntax line. Default is "&lt;".
        /// </summary>
        public static String OptionalArgumentStart = "<";
        /// <summary>
        /// Text written after an optional argument name (and "Options") in the syntax line. Default is "&gt;".
        /// </summary>
        public static String OptionalArgumentEnd = ">";
        /// <summary>
        /// The tag text used to mark optional arguments and the options section in help output. Default is "optional".
        /// </summary>
        public static String OptionalTag = "optional";

        /// <summary>
        /// Text written before a tag in help output (see <see cref="MakeTag(string)"/>). Default is "[".
        /// </summary>
        public static String TagStart = "[";
        /// <summary>
        /// Text written after a tag in help output (see <see cref="MakeTag(string)"/>). Default is "]".
        /// </summary>
        public static String TagEnd = "]";

        /// <summary>
        /// Wraps a tag text in <see cref="TagStart"/> and <see cref="TagEnd"/>, ex: "[optional]".
        /// </summary>
        /// <param name="v">The tag text.</param>
        /// <returns>The decorated tag.</returns>
        public static String MakeTag(String v) => String.Concat(TagStart, v, TagEnd);

        /// <summary>
        /// Other option prefixes accepted by <see cref="IsOption(ref string)"/>, checked (ordinal) together with <see cref="DefaultOptionsPrefix"/>: "--" and "/".
        /// </summary>
        /// <remarks>
        /// The "/" prefix means that any token starting with "/" (ex: an absolute Unix path) is treated as an option.
        /// </remarks>
        public static readonly IReadOnlySet<String> AdditionalOptionPrefixes = ReadOnlyData.Set(StringComparer.Ordinal,
                "--", "/"
            );

        /// <summary>
        /// The comparer used to match option names when no comparer is supplied. Default is <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        public static StringComparer DefaultOptionsComparer = StringComparer.OrdinalIgnoreCase;


        /// <summary>
        /// Parses a <see cref="SByte"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static SByte ParseSByte(String value) => (SByte)(Int64)ExpressionEvaluator.Get(typeof(SByte)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="Int16"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Int16 ParseInt16(String value) => (Int16)(Int64)ExpressionEvaluator.Get(typeof(Int16)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="Int32"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Int32 ParseInt32(String value) => (Int32)(Int64)ExpressionEvaluator.Get(typeof(Int32)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="Int64"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Int64 ParseInt64(String value) => (Int64)ExpressionEvaluator.Get(typeof(Int64)).ToValue(value.Trim());

        /// <summary>
        /// Parses a <see cref="Byte"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Byte ParseByte(String value) => (Byte)(UInt64)ExpressionEvaluator.Get(typeof(Byte)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="UInt16"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static UInt16 ParseUInt16(String value) => (UInt16)(UInt64)ExpressionEvaluator.Get(typeof(UInt16)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="UInt32"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static UInt32 ParseUInt32(String value) => (UInt32)(UInt64)ExpressionEvaluator.Get(typeof(UInt32)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="UInt64"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static UInt64 ParseUInt64(String value) => (UInt64)ExpressionEvaluator.Get(typeof(UInt64)).ToValue(value.Trim());

        /// <summary>
        /// Parses a <see cref="Single"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <remarks>The evaluated value is converted with an unchecked cast, so an out of range value may be truncated rather than rejected.</remarks>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Single ParseSingle(String value) => (Single)(Double)ExpressionEvaluator.Get(typeof(Single)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="Double"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Double ParseDouble(String value) => (Double)ExpressionEvaluator.Get(typeof(Double)).ToValue(value.Trim());
        /// <summary>
        /// Parses a <see cref="Decimal"/> from a trimmed string using the SysWeaver expression evaluator, so simple expressions (ex: "4*1024") are accepted as well as plain numbers.
        /// </summary>
        /// <param name="value">The text to parse, leading/trailing whitespace is ignored.</param>
        /// <returns>The parsed value.</returns>
        public static Decimal ParseDecimal(String value) => (Decimal)ExpressionEvaluator.Get(typeof(Decimal)).ToValue(value.Trim());

        /// <summary>
        /// Returns the string as is (no trimming).
        /// </summary>
        /// <param name="value">The text.</param>
        /// <returns><paramref name="value"/>.</returns>
        public static String ParseString(String value) => value;
        /// <summary>
        /// Parses a <see cref="Boolean"/> using <see cref="Boolean.Parse(string)"/> ("true"/"false", case insensitive).
        /// </summary>
        /// <param name="value">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="value"/> is not a valid boolean.</exception>
        public static Boolean ParseBoolean(String value) => Boolean.Parse(value);

        /// <summary>
        /// Parses a <see cref="DateTime"/> from a trimmed string using <see cref="DateTime.Parse(string)"/> (current culture).
        /// </summary>
        /// <param name="value">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="value"/> is not a valid date/time.</exception>
        public static DateTime ParseDateTime(String value) => DateTime.Parse(value.Trim());
        /// <summary>
        /// Parses a <see cref="TimeSpan"/> from a trimmed string using <see cref="TimeSpan.Parse(string)"/>.
        /// </summary>
        /// <param name="value">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="value"/> is not a valid time span.</exception>
        public static TimeSpan ParseTimeSpan(String value) => TimeSpan.Parse(value.Trim());
        /// <summary>
        /// Parses a <see cref="Guid"/> from a trimmed string using <see cref="Guid.Parse(string)"/>.
        /// </summary>
        /// <param name="value">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="value"/> is not a valid guid.</exception>
        public static Guid ParseGuid(String value) => Guid.Parse(value.Trim());

        /// <summary>
        /// Parses a <see cref="Char"/> from a trimmed string.
        /// Accepts a quoted character ('x' or "x") or a numeric expression giving the character code (only the low 16 bits are used).
        /// </summary>
        /// <param name="value">The text to parse.</param>
        /// <returns>The parsed character.</returns>
        /// <exception cref="ArgumentException">A quoted value is not exactly one character between matching quotes.</exception>
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


        /// <summary>
        /// Registers a parser for a type that has no built-in parser, so it can be used for arguments, options and generated option members.
        /// </summary>
        /// <param name="type">The type that the parser produces.</param>
        /// <param name="parser">Function converting the command line text to a boxed value of <paramref name="type"/>.</param>
        /// <remarks>
        /// The parser table is a plain <see cref="Dictionary{TKey, TValue}"/> (not thread safe): register parsers at startup, before any parsing happens.
        /// Enums, arrays, <see cref="List{T}"/> and <see cref="IEnumerable{T}"/> are handled automatically and need no registration.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="parser"/> is null, or <paramref name="type"/> is null (thrown by the dictionary, with parameter name "key").</exception>
        /// <exception cref="ArgumentException">A parser for <paramref name="type"/> is already registered.</exception>
        public static void AddParser(Type type, Func<String, Object> parser)
        {
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

        /// <summary>
        /// Gets a function that converts command line text into a value of the given type.
        /// </summary>
        /// <param name="type">The type to parse into.</param>
        /// <param name="throwOnError">True to throw if no parser is available, false to return null.</param>
        /// <returns>The parser, or null if none is available and <paramref name="throwOnError"/> is false.</returns>
        /// <remarks>
        /// Enums use <see cref="Enum.Parse(Type, string)"/> (case sensitive, also accepts numbers and comma separated flags).
        /// Arrays, <see cref="List{T}"/> and <see cref="IEnumerable{T}"/> take a ';' separated list of element values (empty entries are skipped).
        /// Other types use the registered parsers (see <see cref="AddParser(Type, Func{string, object})"/>).
        /// </remarks>
        /// <exception cref="InvalidCastException">No parser exists for <paramref name="type"/> and <paramref name="throwOnError"/> is true.</exception>
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
                throw new InvalidCastException(String.Concat("Do not know how parse a string into ", type.CleanTypename()));
            return null;
        }

        static IEnumerable<Object> ToObjectEnumerable(IEnumerable e)
        {
            foreach (var x in e)
                yield return x;
        }

        /// <summary>
        /// Formats a parsed value for display: "null", quoted strings, chars with their code, and enumerables as "[a; b]".
        /// </summary>
        /// <param name="o">The value to format, may be null.</param>
        /// <returns>A human readable representation of the value.</returns>
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

        /// <summary>
        /// Tests if a command line token is an option (starts with <see cref="DefaultOptionsPrefix"/> or one of <see cref="AdditionalOptionPrefixes"/>), and if so strips the prefix.
        /// </summary>
        /// <param name="v">The token, replaced with the option name (prefix removed) if it is an option.</param>
        /// <returns>True if the token is an option.</returns>
        /// <remarks>Only the longest matching prefix is removed. Tokens such as negative numbers ("-5") or paths starting with "/" are also treated as options.</remarks>
        public static bool IsOption(ref String v)
        {
            int prefixLen = -1;
            if (v.StartsWith(DefaultOptionsPrefix, StringComparison.Ordinal))
                prefixLen = DefaultOptionsPrefix.Length;
            foreach (var x in AdditionalOptionPrefixes)
            {
                if ((x.Length > prefixLen) && v.StartsWith(x, StringComparison.Ordinal))
                    prefixLen = x.Length;
            }
            if (prefixLen < 0)
                return false;
            v = v.Substring(prefixLen);
            return true;
        }

        #region Type options

        /// <summary>
        /// Selects which public instance members of an options object are turned into command line options.
        /// </summary>
        public enum OptionMembers
        {
            /// <summary>
            /// Only properties (readable and writable).
            /// </summary>
            Properties = 0,
            /// <summary>
            /// Only fields (that are not read-only).
            /// </summary>
            Fields = 1,
            /// <summary>
            /// Both properties and fields.
            /// </summary>
            All = 2,
        }

        /// <summary>
        /// Generates command line options from the public instance members of an object, each paired with a setter that assigns the parsed value.
        /// </summary>
        /// <param name="o">The object whose members define the options; its current member values become the displayed defaults.</param>
        /// <param name="members">Which members to include.</param>
        /// <returns>
        /// The help options (<see cref="HelpOption1"/>, <see cref="HelpOption2"/>, with null setters) followed by one option per supported member.
        /// The setter takes (instance, value).
        /// </returns>
        /// <remarks>
        /// <para>Option names are the member names. Members whose type has no parser are treated as nested option objects (created with the default constructor if null)
        /// and their members are exposed as "Member.SubMember".</para>
        /// <para>A boolean member that is currently false becomes a flag option (no value, sets it to true); other members take a single "value" argument.</para>
        /// <para>The help text is the member's XML documentation summary. Setters are compiled expressions, so this is relatively expensive; the result is lazily enumerated and not cached.</para>
        /// </remarks>
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


        /// <summary>
        /// The positional arguments found, in command line order, each paired with its parsed value.
        /// Extra arguments (when any number is allowed) are paired with a generated string argument named "Arg{index}".
        /// </summary>
        /// <remarks>Null when parsing stopped because a help option was found.</remarks>
        public readonly Tuple<CommandLineArgument, Object>[] Arguments;
        /// <summary>
        /// The options found, in command line order, each paired with its parsed option argument values (null if the option takes no arguments).
        /// </summary>
        public readonly Tuple<CommandLineOption, Object[]>[] Options;

        /// <summary>
        /// The "?" help option, automatically added by <see cref="GetOptions(object, OptionMembers)"/>.
        /// </summary>
        public static readonly CommandLineOption HelpOption1 = CommandLineOption.Make("?", "Show help");
        /// <summary>
        /// The "help" help option, automatically added by <see cref="GetOptions(object, OptionMembers)"/>.
        /// </summary>
        public static readonly CommandLineOption HelpOption2 = CommandLineOption.Make("help", "Show help");

        static readonly Tuple<CommandLineOption, Action<Object, Object>> HelpAction1 = new Tuple<CommandLineOption, Action<object, object>>(HelpOption1, null);
        static readonly Tuple<CommandLineOption, Action<Object, Object>> HelpAction2 = new Tuple<CommandLineOption, Action<object, object>>(HelpOption2, null);

        CommandLine(Tuple<CommandLineArgument, Object>[] arguments, Tuple<CommandLineOption, Object[]>[] options)
        {
            Options = options;
            Arguments = arguments;
        }

        /// <summary>
        /// Parses command line tokens against a set of positional arguments and options.
        /// </summary>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">
        /// The valid positional arguments, in order. A null last element means any number of extra arguments is allowed.
        /// Null means any number of arguments (all kept as strings).
        /// </param>
        /// <param name="options">The valid options, may be null.</param>
        /// <param name="optionsComparer">Comparer used to match option names.</param>
        /// <returns>The parse result. If one of the help options is found, parsing stops and the result has null <see cref="Arguments"/>.</returns>
        /// <remarks>
        /// Options may appear anywhere; each option consumes the following tokens as its arguments.
        /// When fewer arguments than declared are given, optional arguments before the last required one are skipped so that required arguments get values.
        /// </remarks>
        /// <exception cref="ArgumentException">An option is unknown, an option is missing parameters, there are too few/many arguments, or two options have the same name.</exception>
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
            var validArgs = arguments?.ToList() ?? [];
            var maxArgCount = validArgs.Count;
            bool anyNumberOfArgs = arguments == null || ((maxArgCount > 0) && (validArgs[maxArgCount - 1] == null));
            if (anyNumberOfArgs)
            {
                --maxArgCount;
                if (maxArgCount >= 0)
                    validArgs.RemoveAt(maxArgCount);
                maxArgCount = int.MaxValue;
            }
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
            var maxDef = validArgs.Count;
            var aa = new Tuple<CommandLineArgument, Object>[argCount];
            for (int s = 0; s < argCount; ++s)
            {
                if (so < maxDef)
                {
                    var arg = validArgs[so];
                    while ((skip > 0) && arg.Optional)
                    {
                        ++so;
                        arg = validArgs[so];
                        --skip;
                    }
                    ++so;
                    aa[s] = new Tuple<CommandLineArgument, object>(arg, arg.ParseValue(outArgs[s]));
                }else
                {
                    aa[s] = new Tuple<CommandLineArgument, object>(CommandLineArgument.Make<String>("Arg" + s), outArgs[s]);
                }
            }
            return new CommandLine(aa, usedOptions.ToArray());
        }
        /// <summary>
        /// Parses command line tokens against a set of positional arguments and options, matching option names with <see cref="DefaultOptionsComparer"/>.
        /// </summary>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">The valid positional arguments, see <see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>.</param>
        /// <param name="options">The valid options, may be null.</param>
        /// <returns>The parse result.</returns>
        /// <exception cref="ArgumentException">Invalid command line, see the other overload.</exception>
        public static CommandLine ParseOptions(String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, IEnumerable<CommandLineOption> options) => ParseOptions(commandLineArgs, arguments, options, DefaultOptionsComparer);

        /// <summary>
        /// Parses the command line into a new instance of <typeparamref name="T"/>, using its public members as options (see <see cref="GetOptions(object, OptionMembers)"/>).
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="options">Receives the populated options object, or null if help was requested.</param>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">The valid positional arguments, see <see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>.</param>
        /// <param name="optionsComparer">Comparer used to match option names.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <returns>The parse result, or null if a help option ("-?" or "-help") was given (the caller should then display the syntax).</returns>
        /// <exception cref="ArgumentException">Invalid command line (unknown option, wrong argument count, parse failure etc).</exception>
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, StringComparer optionsComparer, OptionMembers members = OptionMembers.Properties) where T : class, new() => ParseObject<T>(out options, commandLineArgs, arguments, new T(), optionsComparer, members);
        /// <summary>
        /// Parses the command line into a new instance of <typeparamref name="T"/>, using its public members as options and <see cref="DefaultOptionsComparer"/> to match names.
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="options">Receives the populated options object, or null if help was requested.</param>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">The valid positional arguments, see <see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <returns>The parse result, or null if a help option ("-?" or "-help") was given (the caller should then display the syntax).</returns>
        /// <exception cref="ArgumentException">Invalid command line (unknown option, wrong argument count, parse failure etc).</exception>
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, OptionMembers members = OptionMembers.Properties) where T : class, new() => ParseObject<T>(out options, commandLineArgs, arguments, new T(), DefaultOptionsComparer, members);
        /// <summary>
        /// Parses the command line into an existing instance of <typeparamref name="T"/> (modified in place), using <see cref="DefaultOptionsComparer"/> to match names.
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="options">Receives the populated options object, or null if help was requested.</param>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">The valid positional arguments, see <see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>.</param>
        /// <param name="current">The instance to update; its current values are shown as defaults.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <returns>The parse result, or null if a help option ("-?" or "-help") was given (the caller should then display the syntax).</returns>
        /// <exception cref="ArgumentException">Invalid command line (unknown option, wrong argument count, parse failure etc).</exception>
        public static CommandLine ParseObject<T>(out T options, String[] commandLineArgs, IEnumerable<CommandLineArgument> arguments, T current, OptionMembers members = OptionMembers.Properties) where T : class => ParseObject<T>(out options, commandLineArgs, arguments, current, DefaultOptionsComparer, members);
        /// <summary>
        /// Parses the command line into an existing instance of <typeparamref name="T"/> (modified in place), using its public members as options (see <see cref="GetOptions(object, OptionMembers)"/>).
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="options">Receives the populated options object, or null if help was requested.</param>
        /// <param name="commandLineArgs">The tokens (as passed to Main).</param>
        /// <param name="arguments">The valid positional arguments, see <see cref="ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>.</param>
        /// <param name="current">The instance to update; its current values are shown as defaults.</param>
        /// <param name="optionsComparer">Comparer used to match option names.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <returns>The parse result, or null if a help option ("-?" or "-help") was given (the caller should then display the syntax).</returns>
        /// <exception cref="ArgumentException">Invalid command line (unknown option, wrong argument count, parse failure etc).</exception>
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
        /// Full path of the entry assembly (falls back to this assembly), used as the program name in generated help.
        /// </summary>
        /// <remarks>Empty for single-file published applications, where <see cref="Assembly.Location"/> is empty.</remarks>
        public static readonly String Executable = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location;
        
        /// <summary>
        /// Folder of <see cref="Executable"/>.
        /// </summary>
        public static readonly String ExecutableFolder = Path.GetDirectoryName(Executable);

        /// <summary>
        /// Joins the values that are not null or empty.
        /// </summary>
        /// <param name="separator">The separator to insert between values.</param>
        /// <param name="values">The values.</param>
        /// <returns>The joined string.</returns>
        public static String JoinNonEmpty(String separator, params String[] values) => String.Join(separator, values.Where(x => !String.IsNullOrEmpty(x)));

        static String FormatHelp(String help, int pad)
        {
            if (help == null)
                return "";
            var ps = new String(' ', pad);
            var lines = help.Split('\n', StringSplitOptions.TrimEntries);
            var c = lines.Length;
            if (c < 2)
                return help;
            for (int i = 1; i < c; ++ i)
            {
                var t = lines[i];
                lines[i] = String.IsNullOrEmpty(t) ? "" : (ps + t);
            }
            return String.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Generates help text lines: a syntax line, the arguments with their tags and help, and the options with their arguments.
        /// </summary>
        /// <param name="arguments">The valid positional arguments (a null element means "any number of arguments"), null means any number of arguments.</param>
        /// <param name="options">The valid options.</param>
        /// <param name="commandPrefix">Text starting the syntax line, null for "Use: {executable name} ".</param>
        /// <param name="linePrefix">Text prefixed to every line.</param>
        /// <param name="levelInset">Indentation for each nesting level.</param>
        /// <returns>The help lines, to be written one per line (lazily generated).</returns>
        public static IEnumerable<String> SyntaxOptions(IEnumerable<CommandLineArgument> arguments, IEnumerable<CommandLineOption> options, String commandPrefix = null, String linePrefix = "", String levelInset = "  ")
        {
            commandPrefix = commandPrefix ?? String.Concat("Use: ", Path.GetFileNameWithoutExtension(Executable), ' ');
            levelInset = levelInset ?? "";
            linePrefix = linePrefix ?? "";
            var baseLen = levelInset.Length + linePrefix.Length;
            yield return String.Concat(linePrefix, commandPrefix, OptionalArgumentStart, "Options", OptionalArgumentEnd, ' ', String.Join(' ', (arguments ?? new CommandLineArgument[] { null }).Select(x => x == null ? String.Concat(OptionalArgumentStart + "..." + OptionalArgumentEnd) : String.Concat(x.Optional ? OptionalArgumentStart : RequiredArgumentStart, x.Name, x.Optional ? OptionalArgumentEnd : RequiredArgumentEnd))));
            yield return String.Concat(linePrefix, "Arguments:");
            var validArgs = arguments?.ToList() ?? [];
            var maxArgCount = validArgs.Count;
            bool anyNumberOfArgs = arguments == null || ((maxArgCount > 0) && (validArgs[maxArgCount - 1] == null));
            int pad;
            if (arguments == null)
            {
                pad = 4;
                yield return String.Concat(linePrefix, levelInset, "...".PadRight(pad), "Optional arguments");
            }
            else
            {
                pad = validArgs.Select(x => x == null ? 3 : x.Name.Length).DefaultIfEmpty(0).Max() + 1;
                foreach (var x in validArgs)
                    yield return x == null ?
                        String.Concat(linePrefix, levelInset, "...".PadRight(pad), "Optional arguments")
                        :
                        String.Concat(linePrefix, levelInset, x.Name.PadRight(pad), String.Join(' ', x.Tags.Select(z => MakeTag(z))), ' ', FormatHelp(x.HelpText, baseLen + pad))
                        ;
            }
            yield return String.Concat(linePrefix, "Options ", MakeTag(OptionalTag), ':');
            pad = options.Select(x => x.Syntax.Length).DefaultIfEmpty(0).Max() + 1;
            ++baseLen;
            var baseLen2 = levelInset.Length + baseLen;
            foreach (var x in options)
            {
                var args = x.Args;
                var argLen = (args?.Length ?? 0);
//                if ((argLen == 1) && String.IsNullOrEmpty(x.HelpText))
                if ((argLen == 1) && (String.IsNullOrEmpty(x.HelpText) || String.IsNullOrEmpty(args[0].HelpText)))
                {
                    var y = args[0];
                    yield return String.Concat(linePrefix, levelInset, DefaultOptionsPrefix, x.Syntax.PadRight(pad), String.Join(' ', y.Tags.Select(z => MakeTag(z))), ' ', FormatHelp(y.HelpText ?? x.HelpText, baseLen + pad));
                }
                else {
                    yield return String.Concat(linePrefix, levelInset, DefaultOptionsPrefix, x.Syntax.PadRight(pad), FormatHelp(x.HelpText, baseLen + pad));
                    if (argLen > 0)
                    {
                        var pad2 = args.Select(y => y.Name.Length).Max() + 1;
                        foreach (var y in args)
                            yield return String.Concat(linePrefix, levelInset, levelInset, y.Name.PadRight(pad2), String.Join(' ', y.Tags.Select(z => MakeTag(z))), ' ', FormatHelp(y.HelpText, baseLen2 + pad2));
                    }
                }
            }
        }

        /// <summary>
        /// Generates help text lines for an options type, using a new default instance of <typeparamref name="T"/> for the defaults (see <see cref="SyntaxOptions(IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, string, string, string)"/>).
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="arguments">The valid positional arguments.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <param name="commandPrefix">Text starting the syntax line, null for "Use: {executable name} ".</param>
        /// <param name="linePrefix">Text prefixed to every line.</param>
        /// <param name="levelInset">Indentation for each nesting level.</param>
        /// <returns>The help lines, to be written one per line (lazily generated).</returns>
        public static IEnumerable<String> SyntaxObject<T>(IEnumerable<CommandLineArgument> arguments, OptionMembers members = OptionMembers.Properties, String commandPrefix = null, String linePrefix = "", String levelInset = "  ") where T : new() => SyntaxOptions(arguments, GetOptions(new T(), members).Select(x => x.Item1), commandPrefix, linePrefix, levelInset);
        /// <summary>
        /// Generates help text lines for an options object, showing its current member values as defaults (see <see cref="SyntaxOptions(IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, string, string, string)"/>).
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="arguments">The valid positional arguments.</param>
        /// <param name="current">The options object.</param>
        /// <param name="members">Which members of <typeparamref name="T"/> become options.</param>
        /// <param name="commandPrefix">Text starting the syntax line, null for "Use: {executable name} ".</param>
        /// <param name="linePrefix">Text prefixed to every line.</param>
        /// <param name="levelInset">Indentation for each nesting level.</param>
        /// <returns>The help lines, to be written one per line (lazily generated).</returns>
        public static IEnumerable<String> SyntaxObject<T>(IEnumerable<CommandLineArgument> arguments, T current, OptionMembers members = OptionMembers.Properties, String commandPrefix = null, String linePrefix = "", String levelInset = "  ") => SyntaxOptions(arguments, GetOptions(current, members).Select(x => x.Item1), commandPrefix, linePrefix, levelInset);

    }


}
