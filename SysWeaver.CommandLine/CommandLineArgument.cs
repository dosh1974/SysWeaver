using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Represents a valid command argument
    /// </summary>
    public sealed class CommandLineArgument : CommandLineOptionArgument
    {

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="type">Type of the option</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make(String name, Type type, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, type, null, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), null, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="minValue">The minimum allowed value of the option</param>
        /// <param name="maxValue">The maximum allowed value of the option</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, T minValue, T maxValue, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), null, minValue, maxValue, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="type">Type of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make(String name, Type type, Func<String, Object> parser, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, type, parser, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, Func<String, T> parser, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), x => parser(x.Trim()), optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the option</typeparam>
        /// <param name="name">Name of the option</param>
        /// <param name="parser">The parser to use (string to object)</param>
        /// <param name="minValue">The minimum allowed value of the option</param>
        /// <param name="maxValue">The maximum allowed value of the option</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">The default value of the option or null if it's a required option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, Func<String, T> parser, T minValue, T maxValue, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), x => parser(x.Trim()), minValue, maxValue, optional, defaultValue, helpText);

        /// <summary>
        /// Get a string that represents the value of this argument, name=val
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <param name="prefix">An optional prefix to add to the string</param>
        /// <returns>A string that represents the value of this argument, name=val</returns>
        public String ValueText(Object value, String prefix = "") => String.Concat(prefix, Name, " = ", CommandLine.FormatValueText(value));


        CommandLineArgument(String name, Type type, Func<String, Object> parser, bool optional, String defaultValue, String helpText) : base(name, type, parser, defaultValue, helpText)
        {
            Optional = optional;
        }

        CommandLineArgument(String name, Type type, Func<String, Object>parser, Object minValue, Object maxValue, bool optional, String defaultValue, String helpText) : base(name, type, parser, defaultValue, helpText)
        {
            Optional = optional;
        }

        /// <summary>
        /// True if this is an optional argument (option), else false
        /// </summary>
        public readonly bool Optional;
        
        /// <summary>
        /// List of tags
        /// </summary>
        public override IEnumerable<String> Tags
        {
            get
            {
                if (Optional)
                    yield return CommandLine.OptionalTag;
                foreach (var x in base.Tags)
                    yield return x;
            }
        }
    }


}
