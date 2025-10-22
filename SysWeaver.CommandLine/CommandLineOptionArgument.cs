using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SysWeaver
{
    /// <summary>
    /// Represents a valid command line option
    /// </summary>
    public class CommandLineOptionArgument
    {
        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="type">Type of the option</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOptionArgument Make(String name, Type type, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, type, null, defaultValue, helpText);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOptionArgument Make<T>(String name, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), null, defaultValue, helpText);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="minValue">The minimum allowed value of the option</param>
        /// <param name="maxValue">The maximum allowed value of the option</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOptionArgument Make<T>(String name, T minValue, T maxValue, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), null, minValue, maxValue, defaultValue, helpText);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="type">Type of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOptionArgument Make(String name, Type type, Func<String, Object> parser, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, type, parser, defaultValue, helpText);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOptionArgument Make<T>(String name, Func<String, T> parser, String defaultValue = null, String helpText = null) => new CommandLineOptionArgument(name, typeof(T), x => parser(x.Trim()), defaultValue, helpText);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="minValue">The minimum allowed value of the option</param>
        /// <param name="maxValue">The maximum allowed value of the option</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An option</returns>
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
        /// List of tags
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
        /// Name
        /// </summary>
        public readonly String Name;
        /// <summary>
        /// Type 
        /// </summary>
        public readonly Type Type;
        /// <summary>
        /// Type tag
        /// </summary>
        public readonly String TypeTag;
        /// <summary>
        /// Help text
        /// </summary>
        public readonly String HelpText;
        /// <summary>
        /// True if the valus has numerical limits
        /// </summary>
        public readonly bool Limit;
        /// <summary>
        /// The tag to display if the value have a default
        /// </summary>
        public readonly String DefaultTag;
        /// <summary>
        /// 
        /// </summary>
        public readonly String ValidTag;
        /// <summary>
        /// The tag to indicate the numerical limits
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
        /// Parse a value from a string
        /// </summary>
        /// <param name="value">The string that represents this value</param>
        /// <returns>The object that was represented in the string</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
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
        public override string ToString() => String.Join(String.IsNullOrEmpty(HelpText) ? "" : " - ", String.Concat(Type.Name, ' ', Name, ' ', Tags), HelpText ?? "");

    }


}
