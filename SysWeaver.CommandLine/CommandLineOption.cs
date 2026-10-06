using System;
using System.Linq;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// Describes a named command line option (ex: "-verbose" or "-level 3"), with optional arguments that follow it on the command line.
    /// </summary>
    /// <remarks>
    /// Instances are immutable; create them using the <c>Make</c> overloads.
    /// The name is matched without its prefix (see <see cref="CommandLine.IsOption(ref string)"/>).
    /// </remarks>
    public sealed class CommandLineOption
    {
        /// <summary>
        /// Returns the name, followed by the argument names in brackets if it has any.
        /// </summary>
        /// <returns>A string describing the option.</returns>
        public override string ToString()
        {
            return Args.Length > 0 ? String.Concat(Name, '[', String.Join(", ", Args.Select(x => x.Name)), ']') : Name;
        }

        /// <summary>
        /// Get a string that represents the value of this option: the name, or "name = value" if it has values (only the first value is shown).
        /// </summary>
        /// <param name="values">The parsed option argument values, may be null.</param>
        /// <param name="prefix">An optional prefix to add to the string.</param>
        /// <returns>A string that represents the option value.</returns>
        public String ValueText(Object[] values, String prefix = "")
        {
            if ((values == null) || (values.Length <= 0))
                return prefix + Name;
            return String.Concat(prefix, Name, " = ", CommandLine.FormatValueText(values[0]));
        }

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <param name="args">The arguments that must follow this option on the command line (none for a flag option)</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, params CommandLineOptionArgument[] args) => new CommandLineOption(name, helpText, args);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="args">The arguments that must follow this option on the command line (none for a flag option)</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, params CommandLineOptionArgument[] args) => new CommandLineOption(name, null, args);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <param name="valueType">The type of the single argument (named "value") that must follow the option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, Type valueType) => new CommandLineOption(name, helpText, CommandLineOptionArgument.Make("value", valueType));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="valueType">The type of the single argument (named "value") that must follow the option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, Type valueType) => new CommandLineOption(name, null, CommandLineOptionArgument.Make("value", valueType));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <param name="value">Default value, must not be null; its type becomes the type of the single argument (named "value") and its formatted text is shown as the default</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, Object value) => new CommandLineOption(name, helpText, CommandLineOptionArgument.Make("value", value.GetType(), CommandLine.FormatValueText(value)));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="value">Default value, must not be null; its type becomes the type of the single argument (named "value") and its formatted text is shown as the default</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, Object value) => new CommandLineOption(name, null, CommandLineOptionArgument.Make("value", value.GetType(), CommandLine.FormatValueText(value)));

        CommandLineOption(String name, String helpText, params CommandLineOptionArgument[] args)
        {
            Name = name;
            Args = args;
            HelpText = helpText;
        }
        /// <summary>
        /// The option name (without prefix).
        /// </summary>
        public readonly String Name;
        /// <summary>
        /// Help text, may be null.
        /// </summary>
        public readonly String HelpText;
        /// <summary>
        /// The arguments that follow the option on the command line (never null, may be empty).
        /// </summary>
        public readonly CommandLineOptionArgument[] Args;

        /// <summary>
        /// Get the syntax for this option: the name followed by the argument names (without prefix)
        /// </summary>
        public String Syntax => Args.Length > 0 ? String.Concat(Name, ' ', String.Join(' ', Args.Select(x => x.Name))) : Name;

        /// <summary>
        /// Get a multi-line description of this option: the prefixed syntax and help text, then one line per argument
        /// </summary>
        /// <param name="newLinePrefix">Text prefixed to every line</param>
        /// <param name="argPrefix">Additional indentation for argument lines</param>
        /// <returns>The description</returns>
        public String Desc(String newLinePrefix = "", String argPrefix = "  ")
        {
            StringBuilder b = new StringBuilder();
            b.Append(newLinePrefix).Append(CommandLine.DefaultOptionsPrefix).Append(Name);
            foreach (var x in Args)
                b.Append(' ').Append(x.Name);
            if (!String.IsNullOrEmpty(HelpText))
                b.Append(" - ").Append(HelpText);
            foreach (var x in Args)
                b.AppendLine().Append(newLinePrefix).Append(argPrefix).Append(x.ToString());
            return b.ToString();
        }

    }


}
