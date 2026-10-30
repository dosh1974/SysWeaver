using System;
using System.Collections;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Where an environment variable is defined.
    /// </summary>
    public enum OsEnvironmentScopes
    {
        /// <summary>
        /// The current process only (inherited by processes started by it)
        /// </summary>
        Process,

        /// <summary>
        /// The current user (Windows: HKCU\Environment, Linux: ~/.config/environment.d)
        /// </summary>
        User,

        /// <summary>
        /// All users (Windows: HKLM\...\Session Manager\Environment, Linux: /etc/environment)
        /// </summary>
        Machine,
    }


    /// <summary>
    /// An environment variable.
    /// </summary>
    public sealed class OsEnvironmentVariable
    {
        /// <summary>
        /// The name
        /// </summary>
        public String Name;

        /// <summary>
        /// The value (as stored, not expanded)
        /// </summary>
        public String Value;

        /// <summary>
        /// Where the variable is defined
        /// </summary>
        public OsEnvironmentScopes Scope;

        /// <summary>
        /// True if references to other variables are expanded (Windows REG_EXPAND_SZ)
        /// </summary>
        public bool Expandable;

        /// <summary>
        /// Where the value is stored, ex: a registry key or a file name (empty for the process scope)
        /// </summary>
        public String Source;
    }


    /// <summary>
    /// Lists and changes environment variables of the process, the current user and the machine.
    /// Get an instance using <see cref="OsSystem.Environment"/> (created by <see cref="IServiceHostFactory.CreateEnvironment"/>).
    /// User and machine changes only affects processes started after the change (and only after a new log on for some programs).
    /// Machine changes requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface IEnvironmentVariables
    {
        /// <summary>
        /// The scopes that can be read and changed
        /// </summary>
        IReadOnlyList<OsEnvironmentScopes> Scopes { get; }

        /// <summary>
        /// True if variable names are case sensitive
        /// </summary>
        bool CaseSensitive { get; }

        /// <summary>
        /// The char that separates the entries in a list variable such as PATH (';' on Windows, ':' on Linux)
        /// </summary>
        char ListSeparator { get; }

        /// <summary>
        /// Get the variables of a scope.
        /// </summary>
        /// <param name="scope">The scope</param>
        /// <returns>The variables</returns>
        IReadOnlyList<OsEnvironmentVariable> GetVariables(OsEnvironmentScopes scope);

        /// <summary>
        /// Set (add or change) a variable.
        /// </summary>
        /// <param name="scope">The scope</param>
        /// <param name="name">The name</param>
        /// <param name="value">The value (not empty)</param>
        void SetVariable(OsEnvironmentScopes scope, String name, String value);

        /// <summary>
        /// Delete a variable.
        /// </summary>
        /// <param name="scope">The scope</param>
        /// <param name="name">The name</param>
        void DeleteVariable(OsEnvironmentScopes scope, String name);
    }


    /// <summary>
    /// Helpers for <see cref="IEnvironmentVariables"/> implementations.
    /// </summary>
    public static class OsEnvironmentHelper
    {
        /// <summary>
        /// Get the variables of the current process.
        /// </summary>
        public static List<OsEnvironmentVariable> GetProcessVariables()
        {
            var res = new List<OsEnvironmentVariable>();
            foreach (DictionaryEntry e in System.Environment.GetEnvironmentVariables())
                res.Add(new OsEnvironmentVariable
                {
                    Name = e.Key?.ToString() ?? "",
                    Value = e.Value?.ToString() ?? "",
                    Scope = OsEnvironmentScopes.Process,
                    Source = "",
                });
            return res;
        }

        /// <summary>
        /// Validate a variable name and value, throws an exception if invalid.
        /// </summary>
        public static void Validate(String name, String value, bool needValue)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("No variable name!");
            foreach (var c in name)
                if ((c == '=') || (c == '\0') || Char.IsControl(c))
                    throw new ArgumentException("The variable name \"" + name + "\" contains an invalid char!");
            if (needValue && String.IsNullOrEmpty(value))
                throw new ArgumentException("No value (delete the variable instead)!");
            if ((value != null) && (value.IndexOf('\0') >= 0))
                throw new ArgumentException("The value contains a null char!");
        }
    }
}
