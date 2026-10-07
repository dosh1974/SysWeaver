using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Describes a positional command line argument (name, type, parser, help text and whether it is optional).
    /// </summary>
    /// <remarks>
    /// Pass a list of these to <see cref="CommandLine.ParseOptions(string[], IEnumerable{CommandLineArgument}, IEnumerable{CommandLineOption}, StringComparer)"/>
    /// or <c>CommandLine.ParseObject</c>; a null last element allows any number of extra arguments.
    /// Instances are immutable.
    /// </remarks>
    public sealed class CommandLineArgument : CommandLineOptionArgument
    {

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="type">Type of the argument, must have a built-in or registered parser (see <see cref="CommandLine.GetParser(Type, bool)"/>)</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make(String name, Type type, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, type, null, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the argument</typeparam>
        /// <param name="name">Name of the argument</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), null, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the argument</typeparam>
        /// <param name="name">Name of the argument</param>
        /// <param name="minValue">The minimum allowed value , enforced by <see cref="CommandLineOptionArgument.ParseValue(string)"/></param>
        /// <param name="maxValue">The maximum allowed value , enforced by <see cref="CommandLineOptionArgument.ParseValue(string)"/></param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, T minValue, T maxValue, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), null, minValue, maxValue, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="type">Type of the argument</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make(String name, Type type, Func<String, Object> parser, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, type, parser, optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the argument</typeparam>
        /// <param name="name">Name of the argument</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, Func<String, T> parser, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), x => parser(x.Trim()), optional, defaultValue, helpText);

        /// <summary>
        /// Make an argument
        /// </summary>
        /// <typeparam name="T">Type of the argument</typeparam>
        /// <param name="name">Name of the argument</param>
        /// <param name="parser">The parser to use (string to value), the input is trimmed for the generic overloads</param>
        /// <param name="minValue">The minimum allowed value , enforced by <see cref="CommandLineOptionArgument.ParseValue(string)"/></param>
        /// <param name="maxValue">The maximum allowed value , enforced by <see cref="CommandLineOptionArgument.ParseValue(string)"/></param>
        /// <param name="optional">Set to true if the argument is optional, false to require it</param>
        /// <param name="defaultValue">Text describing the default value, shown as a "default: ..." tag in help (not used when parsing), or null for no tag.</param>
        /// <param name="helpText">Optional help text for this argument</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument Make<T>(String name, Func<String, T> parser, T minValue, T maxValue, bool optional = false, String defaultValue = null, String helpText = null) => new CommandLineArgument(name, typeof(T), x => parser(x.Trim()), minValue, maxValue, optional, defaultValue, helpText);

        /// <summary>
        /// Get a string that represents the value of this argument, "name = value" (formatted with <see cref="CommandLine.FormatValueText(object)"/>)
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <param name="prefix">An optional prefix to add to the string</param>
        /// <returns>A string that represents the value of this argument, name=val</returns>
        public String ValueText(Object value, String prefix = "") => String.Concat(prefix, Name, " = ", CommandLine.FormatValueText(value));


        CommandLineArgument(String name, Type type, Func<String, Object> parser, bool optional, String defaultValue, String helpText) : base(name, type, parser, defaultValue, helpText)
        {
            Optional = optional;
        }

        CommandLineArgument(String name, Type type, Func<String, Object>parser, Object minValue, Object maxValue, bool optional, String defaultValue, String helpText) : base(name, type, parser, minValue, maxValue, defaultValue, helpText)
        {
            Optional = optional;
        }

        /// <summary>
        /// True if this argument may be omitted, false if it is required
        /// </summary>
        public readonly bool Optional;
        
        /// <summary>
        /// The tags shown in help: the <see cref="CommandLine.OptionalTag"/> (if optional) followed by the base tags
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
