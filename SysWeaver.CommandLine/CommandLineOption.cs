using System;
using System.Linq;
using System.Text;

namespace SysWeaver
{
    public sealed class CommandLineOption
    {
        public override string ToString()
        {
            return Args.Length > 0 ? String.Concat(Name, '[', String.Join(", ", Args.Select(x => x.Name)), ']') : Name;
        }

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
        /// <param name="args">optional paramters for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, params CommandLineOptionArgument[] args) => new CommandLineOption(name, helpText, args);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="args">optional paramters for this option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, params CommandLineOptionArgument[] args) => new CommandLineOption(name, null, args);

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <param name="valueType">The type of the option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, Type valueType) => new CommandLineOption(name, helpText, CommandLineOptionArgument.Make("value", valueType));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="valueType">The type of the option</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, Type valueType) => new CommandLineOption(name, null, CommandLineOptionArgument.Make("value", valueType));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="helpText">Optional help text for this option</param>
        /// <param name="value">Default value</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, String helpText, Object value) => new CommandLineOption(name, helpText, CommandLineOptionArgument.Make("value", value.GetType(), CommandLine.FormatValueText(value)));

        /// <summary>
        /// Make an option
        /// </summary>
        /// <param name="name">Name of the option</param>
        /// <param name="value">Default value</param>
        /// <returns>An option</returns>
        public static CommandLineOption Make(String name, Object value) => new CommandLineOption(name, null, CommandLineOptionArgument.Make("value", value.GetType(), CommandLine.FormatValueText(value)));

        CommandLineOption(String name, String helpText, params CommandLineOptionArgument[] args)
        {
            Name = name;
            Args = args;
            HelpText = helpText;
        }
        public readonly String Name;
        public readonly String HelpText;
        public readonly CommandLineOptionArgument[] Args;

        /// <summary>
        /// Get the syntax for this option
        /// </summary>
        public String Syntax => Args.Length > 0 ? String.Concat(Name, ' ', String.Join(' ', Args.Select(x => x.Name))) : Name;

        /// <summary>
        /// Get a descirption for this option
        /// </summary>
        /// <param name="newLinePrefix"></param>
        /// <param name="argPrefix"></param>
        /// <returns></returns>
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
