using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IEnvironmentVariables"/> for Linux.
    /// Machine: /etc/environment (read by pam_env at log on), User: ~/.config/environment.d/*.conf (read by systemd for the user session, new variables are written to 60-sysweaver.conf).
    /// Shell startup files (.profile, .bashrc etc) are not parsed.
    /// </summary>
    sealed class UnixEnvironment : IEnvironmentVariables
    {
        public IReadOnlyList<OsEnvironmentScopes> Scopes { get; } = [OsEnvironmentScopes.Process, OsEnvironmentScopes.User, OsEnvironmentScopes.Machine];

        public bool CaseSensitive => true;

        public char ListSeparator => ':';

        const String MachineFile = "/etc/environment";

        const String UserFileName = "60-sysweaver.conf";

        static String UserFolder
        {
            get
            {
                var c = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (String.IsNullOrEmpty(c))
                    c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(c, "environment.d");
            }
        }

        /// <summary>
        /// Parse a "NAME=value" line, null if it's not an assignment
        /// </summary>
        static (String Name, String Value)? ParseLine(String line)
        {
            var l = line.Trim();
            if ((l.Length <= 0) || (l[0] == '#'))
                return null;
            if (l.StartsWith("export ", StringComparison.Ordinal))
                l = l.Substring(7).TrimStart();
            var i = l.IndexOf('=');
            if (i <= 0)
                return null;
            var name = l.Substring(0, i).Trim();
            if (name.Any(c => !(Char.IsLetterOrDigit(c) || (c == '_'))))
                return null;
            var v = l.Substring(i + 1).Trim();
            if ((v.Length >= 2) && (((v[0] == '"') && (v[^1] == '"')) || ((v[0] == '\'') && (v[^1] == '\''))))
                v = v.Substring(1, v.Length - 2);
            return (name, v);
        }

        static String FormatLine(String name, String value)
        {
            if (value.IndexOfAny(['"', '\n', '\r']) >= 0)
                throw new ArgumentException("The value can't contain double quotes or line feeds!");
            return value.Any(c => Char.IsWhiteSpace(c) || (c == '#') || (c == '\'')) ? String.Concat(name, "=\"", value, "\"") : String.Concat(name, "=", value);
        }

        static void ValidateName(String name)
        {
            if (name.Any(c => !(Char.IsAsciiLetterOrDigit(c) || (c == '_'))) || Char.IsDigit(name[0]))
                throw new ArgumentException("The variable name \"" + name + "\" is invalid (letters, digits and _ only)!");
        }

        static String[] ReadLines(String file)
        {
            try
            {
                return File.Exists(file) ? File.ReadAllLines(file) : [];
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// The user files, in the order they are read (later ones overrides)
        /// </summary>
        static String[] UserFiles()
        {
            try
            {
                var d = UserFolder;
                return Directory.Exists(d) ? Directory.GetFiles(d, "*.conf").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToArray() : [];
            }
            catch
            {
                return [];
            }
        }

        public IReadOnlyList<OsEnvironmentVariable> GetVariables(OsEnvironmentScopes scope)
        {
            if (scope == OsEnvironmentScopes.Process)
                return OsEnvironmentHelper.GetProcessVariables();
            var res = new Dictionary<String, OsEnvironmentVariable>(StringComparer.Ordinal);
            var files = scope == OsEnvironmentScopes.Machine ? [MachineFile] : UserFiles();
            foreach (var f in files)
            {
                foreach (var line in ReadLines(f))
                {
                    var p = ParseLine(line);
                    if (p == null)
                        continue;
                    res[p.Value.Name] = new OsEnvironmentVariable
                    {
                        Name = p.Value.Name,
                        Value = p.Value.Value,
                        Scope = scope,
                        Expandable = (scope == OsEnvironmentScopes.User) && (p.Value.Value.IndexOf('$') >= 0),
                        Source = f,
                    };
                }
            }
            return res.Values.ToList();
        }

        /// <summary>
        /// Set or remove (value = null) a variable in a file, returns true if it was found
        /// </summary>
        static bool Update(String file, String name, String value, bool addIfMissing)
        {
            var lines = ReadLines(file).ToList();
            bool found = false;
            for (int i = lines.Count - 1; i >= 0; --i)
            {
                var p = ParseLine(lines[i]);
                if ((p == null) || (p.Value.Name != name))
                    continue;
                if (found || (value == null))
                    lines.RemoveAt(i);
                else
                    lines[i] = FormatLine(name, value);
                found = true;
            }
            if ((!found) && (value != null) && addIfMissing)
                lines.Add(FormatLine(name, value));
            if (found || ((value != null) && addIfMissing))
            {
                var sb = new StringBuilder();
                foreach (var l in lines)
                    sb.Append(l).Append('\n');
                if (file == MachineFile)
                    UnixCommand.WriteFile(file, sb.ToString());
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, sb.ToString());
                }
            }
            return found;
        }

        public void SetVariable(OsEnvironmentScopes scope, String name, String value)
        {
            OsEnvironmentHelper.Validate(name, value, true);
            if (scope == OsEnvironmentScopes.Process)
            {
                Environment.SetEnvironmentVariable(name, value);
                return;
            }
            ValidateName(name);
            if (scope == OsEnvironmentScopes.Machine)
            {
                Update(MachineFile, name, value, true);
                return;
            }
            //  Change it in the (last) file that defines it, else add it to our own file
            var files = UserFiles();
            for (int i = files.Length - 1; i >= 0; --i)
                if (Update(files[i], name, value, false))
                    return;
            Update(Path.Combine(UserFolder, UserFileName), name, value, true);
        }

        public void DeleteVariable(OsEnvironmentScopes scope, String name)
        {
            OsEnvironmentHelper.Validate(name, null, false);
            if (scope == OsEnvironmentScopes.Process)
            {
                Environment.SetEnvironmentVariable(name, null);
                return;
            }
            bool found = false;
            if (scope == OsEnvironmentScopes.Machine)
                found = Update(MachineFile, name, null, false);
            else
                foreach (var f in UserFiles())
                    found |= Update(f, name, null, false);
            if (!found)
                throw new Exception("The variable \"" + name + "\" doesn't exist in the " + scope + " environment!");
        }
    }
}
