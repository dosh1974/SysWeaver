using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SysWeaver
{
    /// <summary>
    /// Describes a typed value on the command line: an argument that follows a <see cref="CommandLineOption"/>, and the base class of <see cref="CommandLineArgument"/>.
    /// Holds the name, type, parser, optional numerical limits and the tags shown in help.
    /// </summary>
    /// <remarks>
    /// Instances are immutable. If no parser is supplied, <see cref="CommandLine.GetParser(Type, bool)"/> is used (throws if the type is unsupported).
    /// </remarks>
    public class CommandLineOptionArgument
    {
        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="type">Type of the option</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make(String name, Type type, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, type, null, defaultValue, helpText);

        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make<T>(String name, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), null, defaultValue, helpText);

        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="minValue">The minimum allowed value, enforced by <see cref="ParseValue(string)"/></param>
        /// <param name="maxValue">The maximum allowed value, enforced by <see cref="ParseValue(string)"/></param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make<T>(String name, T minValue, T maxValue, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), null, minValue, maxValue, defaultValue, helpText);

        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="type">Type of the option</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make(String name, Type type, Func<String, Object> parser, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, type, parser, defaultValue, helpText);

        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make<T>(String name, Func<String, T> parser, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), x => parser(x.Trim()), defaultValue, helpText);

        /// <summary>
        /// Make an option argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="minValue">The minimum allowed value, enforced by <see cref="ParseValue(string)"/></param>
        /// <param name="maxValue">The maximum allowed value, enforced by <see cref="ParseValue(string)"/></param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument Make<T>(String name, Func<String, T> parser, T minValue, T maxValue, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), x => parser(x.Trim()), minValue, maxValue, defaultValue, helpText);

        static String GetTypeDesc(Type t)
        {
            if (t.IsEnum)
                return String.Concat("valid: ", String.Join(", ", Enum.GetNames(t)));
            if (t == typeof(Boolean))
                return "valid: False, True";
            return "";
        }

        static String GetTypeTag(Type t)
        {
            if (t.IsEnum)
                return "";
            if (t == typeof(Boolean))
                return "";
            return t.Name;
        }

        static String GetLimitTag(Object min, Object max)
        {
            return String.Concat(min, "; ", max);
        }

        static String GetDefaultTag(String defaultValue) => defaultValue == null ? "" : String.Concat("default: ", defaultValue);


        /// <summary>
        /// The tags shown in help, in order: default, valid values, limits, type (empty tags are skipped)
        /// </summary>
        public virtual IEnumerable<String> Tags
        {
            get
            {
                if (!String.IsNullOrEmpty(DefaultTag))
                    yield return DefaultTag;
                if (!String.IsNullOrEmpty(ValidTag))
                    yield return ValidTag;
                if (!String.IsNullOrEmpty(LimitTag))
                    yield return LimitTag;
                if (!String.IsNullOrEmpty(TypeTag))
                    yield return TypeTag;

            }
        }

        /// <summary>
        /// Create an option argument without limits
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="type">Type of the argument</param>
        /// <param name="parser">The parser to use, null to use <see cref="CommandLine.GetParser(Type, bool)"/></param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text</param>
        /// <exception cref="InvalidCastException"><paramref name="parser"/> is null and there is no parser for <paramref name="type"/></exception>
        protected CommandLineOptionArgument(String name, Type type, Func<String, Object> parser, String defaultValue, String helpText)
        {
            Name = name;
            Type = type;
            Parser = parser ?? CommandLine.GetParser(type);
            HelpText = helpText;
            ValidTag = GetTypeDesc(type);
            DefaultTag = GetDefaultTag(defaultValue);
            LimitTag = "";
            TypeTag = GetTypeTag(type);
        }

        /// <summary>
        /// Create an option argument with an inclusive value range
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="type">Type of the argument, must support the &lt; and &gt; operators</param>
        /// <param name="parser">The parser to use, null to use <see cref="CommandLine.GetParser(Type, bool)"/></param>
        /// <param name="minValue">The minimum allowed value</param>
        /// <param name="maxValue">The maximum allowed value</param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text</param>
        /// <exception cref="InvalidCastException"><paramref name="parser"/> is null and there is no parser for <paramref name="type"/></exception>
        protected CommandLineOptionArgument(String name, Type type, Func<String, Object> parser, Object minValue, Object maxValue, String defaultValue, String helpText)
        {
            Name = name;
            Type = type;
            Parser = parser ?? CommandLine.GetParser(type);
            HelpText = helpText;
            Limit = true;
            MinValue = minValue;
            MaxValue = maxValue;

            ValidTag = GetTypeDesc(type);
            DefaultTag = GetDefaultTag(defaultValue);
            LimitTag = GetLimitTag(minValue, maxValue);
            TypeTag = GetTypeTag(type);
        }

        readonly Func<String, Object> Parser;

        /// <summary>
        /// Name of the argument, shown in help
        /// </summary>
        public readonly String Name;
        /// <summary>
        /// The type of the parsed value
        /// </summary>
        public readonly Type Type;
        /// <summary>
        /// The type tag shown in help (the type name, empty for enums and booleans since they get a <see cref="ValidTag"/> instead)
        /// </summary>
        public readonly String TypeTag;
        /// <summary>
        /// Help text, may be null
        /// </summary>
        public readonly String HelpText;
        /// <summary>
        /// True if the value has limits (<see cref="MinValue"/> and <see cref="MaxValue"/>) that are checked by <see cref="ParseValue(string)"/>
        /// </summary>
        public readonly bool Limit;
        /// <summary>
        /// The tag to display if the value has a default ("default: ..."), else empty
        /// </summary>
        public readonly String DefaultTag;
        /// <summary>
        /// The tag listing the valid values for enums and booleans ("valid: A, B"), else empty
        /// </summary>
        public readonly String ValidTag;
        /// <summary>
        /// The tag to indicate the limits ("min; max"), empty if there are no limits
        /// </summary>
        public readonly String LimitTag;
        /// <summary>
        /// The min allowed value if it has numerical limits
        /// </summary>
        public readonly Object MinValue;
        /// <summary>
        /// The max allowed value if it has numerical limits
        /// </summary>
        public readonly Object MaxValue;

        /// <summary>
        /// Parse a value from a string, validating it against the limits (if any)
        /// </summary>
        /// <param name="value">The string that represents this value</param>
        /// <returns>The object that was represented in the string</returns>
        /// <exception cref="ArgumentOutOfRangeException">The parsed value is outside [<see cref="MinValue"/>, <see cref="MaxValue"/>]</exception>
        /// <remarks>Any exception thrown by the parser is propagated. The limit check compiles expressions on every call.</remarks>
        public Object ParseValue(String value)
        {
            var t = Parser(value);
            if (!Limit)
                return t;
            var type = Type;
            var te = Expression.Convert(Expression.Constant(t), type);
            if (Expression.Lambda<Func<bool>>(Expression.LessThan(te, Expression.Convert(Expression.Constant(MinValue), type))).Compile()())
                throw new ArgumentOutOfRangeException(Name, String.Concat("Option argument ", Name, " parsed from \"", value, "\" as ", t, " is to small! Valid value range is [", MinValue, "; ", MaxValue, ']'));
            if (Expression.Lambda<Func<bool>>(Expression.GreaterThan(te, Expression.Convert(Expression.Constant(MaxValue), type))).Compile()())
                throw new ArgumentOutOfRangeException(Name, String.Concat("Option argument ", Name, " parsed from \"", value, "\" as ", t, " is to big! Valid value range is [", MinValue, "; ", MaxValue, ']'));
            return t;
        }
        /// <summary>
        /// Returns "Type Name Tags - HelpText".
        /// </summary>
        /// <returns>A string describing the argument.</returns>
        /// <remarks>The tags are currently rendered as the enumerable's type name rather than the tag texts.</remarks>
        public override string ToString() => String.Join(String.IsNullOrEmpty(HelpText) ? "" : " - ", String.Concat(Type.Name, ' ', Name, ' ', Tags), HelpText ?? "");

    }


}
