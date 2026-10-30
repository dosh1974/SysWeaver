using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IStartupItems"/> for Linux:
    /// XDG autostart (~/.config/autostart and /etc/xdg/autostart, a user file with the same name overrides a system file, disabling a system item creates such an override with Hidden=true),
    /// cron @reboot jobs (the user crontab, /etc/crontab and /etc/cron.d, disabled jobs are commented out) and /etc/rc.local (enabled if executable).
    /// </summary>
    sealed class UnixStartup : IStartupItems
    {
        public IReadOnlyList<String> AddScopes { get; } = ["User", "Machine"];

        const String DisabledPrefix = "#SysWeaver-disabled# ";
        const String SystemAutostart = "/etc/xdg/autostart";
        const String RcLocal = "/etc/rc.local";

        static String UserAutostart
        {
            get
            {
                var c = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (String.IsNullOrEmpty(c))
                    c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(c, "autostart");
            }
        }

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        #region XDG autostart

        /// <summary>
        /// Read the [Desktop Entry] group of a .desktop file
        /// </summary>
        static Dictionary<String, String> ReadDesktop(String file)
        {
            var res = new Dictionary<String, String>(StringComparer.Ordinal);
            bool inEntry = false;
            foreach (var raw in File.ReadAllLines(file))
            {
                var l = raw.Trim();
                if (l.StartsWith('['))
                {
                    inEntry = l == "[Desktop Entry]";
                    continue;
                }
                if ((!inEntry) || (l.Length <= 0) || (l[0] == '#'))
                    continue;
                var i = l.IndexOf('=');
                if (i > 0)
                    res.TryAdd(l.Substring(0, i).Trim(), l.Substring(i + 1).Trim());
            }
            return res;
        }

        static bool IsTrue(Dictionary<String, String> d, String key) => d.TryGetValue(key, out var v) && String.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

        static bool IsFalse(Dictionary<String, String> d, String key) => d.TryGetValue(key, out var v) && String.Equals(v, "false", StringComparison.OrdinalIgnoreCase);

        static bool DesktopEnabled(Dictionary<String, String> d) => !IsTrue(d, "Hidden") && !IsFalse(d, "X-GNOME-Autostart-enabled");

        /// <summary>
        /// Set (or add) keys in the [Desktop Entry] group of a .desktop file (keeps everything else)
        /// </summary>
        static String SetKeys(String[] lines, Dictionary<String, String> values)
        {
            var sb = new StringBuilder();
            var left = new Dictionary<String, String>(values, StringComparer.Ordinal);
            bool inEntry = false;
            void flush()
            {
                foreach (var kv in left)
                    sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
                left.Clear();
            }
            foreach (var raw in lines)
            {
                var l = raw.Trim();
                if (l.StartsWith('['))
                {
                    if (inEntry)
                        flush();
                    inEntry = l == "[Desktop Entry]";
                    sb.Append(raw).Append('\n');
                    continue;
                }
                if (inEntry)
                {
                    var i = l.IndexOf('=');
                    if ((i > 0) && (l[0] != '#'))
                    {
                        var k = l.Substring(0, i).Trim();
                        if (values.TryGetValue(k, out var v))
                        {
                            if (left.Remove(k))
                                sb.Append(k).Append('=').Append(v).Append('\n');
                            continue;
                        }
                    }
                }
                sb.Append(raw).Append('\n');
            }
            if (inEntry)
                flush();
            if (left.Count > 0)
            {
                sb.Append("[Desktop Entry]\n");
                flush();
            }
            return sb.ToString();
        }

        IEnumerable<OsStartupItem> GetAutostart()
        {
            var user = UserAutostart;
            var names = new SortedSet<String>(StringComparer.Ordinal);
            foreach (var d in new[] { user, SystemAutostart })
            {
                try
                {
                    if (Directory.Exists(d))
                        foreach (var f in Directory.GetFiles(d, "*.desktop"))
                            names.Add(Path.GetFileName(f));
                }
                catch
                {
                }
            }
            foreach (var n in names)
            {
                var item = Autostart(n);
                if (item != null)
                    yield return item;
            }
        }

        OsStartupItem Autostart(String fileName)
        {
            var uf = Path.Combine(UserAutostart, fileName);
            var sf = Path.Combine(SystemAutostart, fileName);
            var isUser = File.Exists(uf);
            var file = isUser ? uf : sf;
            if (!File.Exists(file))
                return null;
            Dictionary<String, String> d;
            try
            {
                d = ReadDesktop(file);
            }
            catch
            {
                return null;
            }
            var props = new List<KeyValuePair<String, String>>();
            if (isUser && File.Exists(sf))
                props.Add(P("Overrides", sf));
            foreach (var k in new[] { "Comment", "OnlyShowIn", "NotShowIn", "TryExec", "X-GNOME-Autostart-Phase", "X-GNOME-Autostart-Delay" })
                if (d.TryGetValue(k, out var v) && (v.Length > 0))
                    props.Add(P(k, v));
            return new OsStartupItem
            {
                Id = "xdg|" + fileName,
                Name = d.TryGetValue("Name", out var name) && (name.Length > 0) ? name : Path.GetFileNameWithoutExtension(fileName),
                Command = d.TryGetValue("Exec", out var exec) ? exec : "",
                Location = Path.GetDirectoryName(file),
                Type = "Autostart",
                Scope = isUser ? "User" : "Machine",
                Enabled = DesktopEnabled(d),
                CanDisable = true,
                CanRemove = true,
                File = file,
                Properties = props.ToArray(),
            };
        }

        static void CheckFileName(String n)
        {
            if (String.IsNullOrEmpty(n) || (n.IndexOf('/') >= 0) || (n == ".") || (n == "..") || !n.EndsWith(".desktop", StringComparison.Ordinal))
                throw new ArgumentException("Invalid autostart file \"" + n + "\"!");
        }

        void SetAutostartEnabled(String fileName, bool enabled)
        {
            CheckFileName(fileName);
            var uf = Path.Combine(UserAutostart, fileName);
            var sf = Path.Combine(SystemAutostart, fileName);
            var values = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                ["Hidden"] = enabled ? "false" : "true",
            };
            String[] lines;
            if (File.Exists(uf))
            {
                lines = File.ReadAllLines(uf);
                if (enabled && ReadDesktop(uf).ContainsKey("X-GNOME-Autostart-enabled"))
                    values["X-GNOME-Autostart-enabled"] = "true";
            }
            else if (File.Exists(sf))
            {
                //  A user override of the system item
                lines = File.ReadAllLines(sf);
                if (enabled && ReadDesktop(sf).ContainsKey("X-GNOME-Autostart-enabled"))
                    values["X-GNOME-Autostart-enabled"] = "true";
                Directory.CreateDirectory(UserAutostart);
            }
            else
                throw new Exception("The autostart item \"" + fileName + "\" doesn't exist!");
            File.WriteAllText(uf, SetKeys(lines, values));
        }

        static void RemoveAutostart(String fileName)
        {
            CheckFileName(fileName);
            var uf = Path.Combine(UserAutostart, fileName);
            if (File.Exists(uf))
            {
                File.Delete(uf);
                return;
            }
            var sf = Path.Combine(SystemAutostart, fileName);
            if (!File.Exists(sf))
                throw new Exception("The autostart item \"" + fileName + "\" doesn't exist!");
            UnixCommand.DeleteFile(sf);
        }

        #endregion//XDG autostart

        #region Cron @reboot

        sealed class CronLine
        {
            /// <summary>
            /// "user" for the user crontab, else the file
            /// </summary>
            public String Source;
            public int Index;
            public String Line;
            public bool Enabled;
            public bool SystemFormat;
        }

        static String Hash(String s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)), 0, 6).ToLowerInvariant();

        static String UserCrontab()
        {
            var c = UnixCommand.Find("crontab");
            if (c == null)
                return null;
            //  Exit code 1 and "no crontab for user" if there is none
            return UnixCommand.Run(out var o, out _, c, ["-l"]) == 0 ? o : "";
        }

        static IEnumerable<CronLine> Reboots(String source, String[] lines, bool systemFormat)
        {
            for (int i = 0; i < lines.Length; ++i)
            {
                var l = lines[i].Trim();
                var enabled = true;
                if (l.StartsWith(DisabledPrefix.Trim(), StringComparison.Ordinal))
                {
                    l = l.Substring(DisabledPrefix.Trim().Length).Trim();
                    enabled = false;
                }
                if (l.StartsWith("@reboot", StringComparison.Ordinal) && ((l.Length == 7) || Char.IsWhiteSpace(l[7])))
                    yield return new CronLine { Source = source, Index = i, Line = lines[i], Enabled = enabled, SystemFormat = systemFormat };
            }
        }

        static IEnumerable<String> SystemCrontabs()
        {
            if (File.Exists("/etc/crontab"))
                yield return "/etc/crontab";
            String[] files = [];
            try
            {
                if (Directory.Exists("/etc/cron.d"))
                    files = Directory.GetFiles("/etc/cron.d");
            }
            catch
            {
            }
            foreach (var f in files.OrderBy(x => x, StringComparer.Ordinal))
                yield return f;
        }

        static IEnumerable<CronLine> GetCronLines()
        {
            var u = UserCrontab();
            if (!String.IsNullOrEmpty(u))
                foreach (var c in Reboots("user", u.Split('\n'), false))
                    yield return c;
            foreach (var f in SystemCrontabs())
            {
                String[] lines;
                try
                {
                    lines = File.ReadAllLines(f);
                }
                catch
                {
                    continue;
                }
                foreach (var c in Reboots(f, lines, true))
                    yield return c;
            }
        }

        static OsStartupItem CronItem(CronLine c)
        {
            var l = c.Line.Trim();
            if (l.StartsWith(DisabledPrefix.Trim(), StringComparison.Ordinal))
                l = l.Substring(DisabledPrefix.Trim().Length).Trim();
            var rest = l.Substring(7).Trim();
            var runAs = "";
            if (c.SystemFormat)
            {
                var i = rest.IndexOfAny([' ', '\t']);
                if (i > 0)
                {
                    runAs = rest.Substring(0, i);
                    rest = rest.Substring(i).Trim();
                }
            }
            var user = c.Source == "user";
            var props = new List<KeyValuePair<String, String>>
            {
                P("Line", c.Line.Trim()),
            };
            if (runAs.Length > 0)
                props.Add(P("Runs as", runAs));
            return new OsStartupItem
            {
                Id = String.Concat("cron|", c.Source, "|", c.Index, "|", Hash(c.Line)),
                Name = rest.Length > 60 ? (rest.Substring(0, 57) + "...") : rest,
                Command = rest,
                Location = user ? "User crontab" : c.Source,
                Type = "Cron @reboot",
                Scope = user ? "User" : "Machine",
                Enabled = c.Enabled,
                CanDisable = true,
                CanRemove = true,
                File = user ? "" : c.Source,
                Properties = props.ToArray(),
            };
        }

        /// <summary>
        /// Change (newLine != null) or remove (newLine == null) a cron line, the id must match the current content
        /// </summary>
        static void ChangeCron(String id, Func<String, String> change)
        {
            var p = id.Split('|');
            if ((p.Length != 4) || !int.TryParse(p[2], out var index))
                throw new ArgumentException("Invalid cron id \"" + id + "\"!");
            var source = p[1];
            String[] lines;
            if (source == "user")
                lines = (UserCrontab() ?? "").Split('\n');
            else
            {
                if (!SystemCrontabs().Contains(source))
                    throw new ArgumentException("Not a crontab \"" + source + "\"!");
                lines = File.ReadAllLines(source);
            }
            if ((index < 0) || (index >= lines.Length) || (Hash(lines[index]) != p[3]))
                throw new Exception("The cron job has changed, reload!");
            var list = lines.ToList();
            var n = change(lines[index]);
            if (n == null)
                list.RemoveAt(index);
            else
                list[index] = n;
            var text = String.Join('\n', list);
            if (!text.EndsWith('\n'))
                text += "\n";
            if (source == "user")
                UnixCommand.Run(out _, out var err, UnixCommand.Find("crontab"), ["-"], text).ThrowIfFailed("crontab -", err);
            else
                UnixCommand.WriteFile(source, text);
        }

        #endregion//Cron @reboot

        #region rc.local

        static OsStartupItem RcItem()
        {
            if (!File.Exists(RcLocal))
                return null;
            var mode = File.GetUnixFileMode(RcLocal);
            return new OsStartupItem
            {
                Id = "rc|" + RcLocal,
                Name = "rc.local",
                Command = RcLocal,
                Location = "/etc",
                Type = "rc.local",
                Scope = "Machine",
                Enabled = (mode & UnixFileMode.UserExecute) != 0,
                CanDisable = true,
                CanRemove = false,
                File = RcLocal,
                Properties = [P("Note", "Run by rc-local.service at boot if it's executable")],
            };
        }

        #endregion//rc.local

        public IReadOnlyList<OsStartupItem> GetItems()
        {
            var res = new List<OsStartupItem>();
            try
            {
                res.AddRange(GetAutostart());
            }
            catch
            {
            }
            try
            {
                res.AddRange(GetCronLines().Select(CronItem));
            }
            catch
            {
            }
            var rc = RcItem();
            if (rc != null)
                res.Add(rc);
            return res;
        }

        public OsStartupItem GetItem(String id)
        {
            id ??= "";
            if (id.StartsWith("xdg|", StringComparison.Ordinal))
            {
                CheckFileName(id.Substring(4));
                return Autostart(id.Substring(4));
            }
            if (id.StartsWith("cron|", StringComparison.Ordinal))
                return GetCronLines().Select(CronItem).FirstOrDefault(x => x.Id == id);
            if (id == "rc|" + RcLocal)
                return RcItem();
            return null;
        }

        public void SetEnabled(String id, bool enabled)
        {
            id ??= "";
            if (id.StartsWith("xdg|", StringComparison.Ordinal))
            {
                SetAutostartEnabled(id.Substring(4), enabled);
                return;
            }
            if (id.StartsWith("cron|", StringComparison.Ordinal))
            {
                ChangeCron(id, l =>
                {
                    var t = l.Trim();
                    var isDisabled = t.StartsWith(DisabledPrefix.Trim(), StringComparison.Ordinal);
                    if (enabled)
                        return isDisabled ? t.Substring(DisabledPrefix.Trim().Length).Trim() : l;
                    return isDisabled ? l : (DisabledPrefix + t);
                });
                return;
            }
            if (id == "rc|" + RcLocal)
            {
                UnixCommand.Change(UnixCommand.Find("chmod") ?? "/bin/chmod", enabled ? "+x" : "-x", RcLocal);
                return;
            }
            throw new ArgumentException("Invalid startup item id \"" + id + "\"!");
        }

        public void Remove(String id)
        {
            id ??= "";
            if (id.StartsWith("xdg|", StringComparison.Ordinal))
            {
                RemoveAutostart(id.Substring(4));
                return;
            }
            if (id.StartsWith("cron|", StringComparison.Ordinal))
            {
                ChangeCron(id, _ => null);
                return;
            }
            throw new ArgumentException("This item can't be removed!");
        }

        public String Add(String scope, String name, String command)
        {
            name = (name ?? "").Trim();
            command = (command ?? "").Trim();
            if (name.Length <= 0)
                throw new ArgumentException("No name!");
            if (command.Length <= 0)
                throw new ArgumentException("No command!");
            if (name.Any(Char.IsControl) || command.Any(Char.IsControl))
                throw new ArgumentException("The name and command can't contain control chars!");
            var machine = String.Equals(scope, "Machine", StringComparison.OrdinalIgnoreCase);
            if ((!machine) && (!String.Equals(scope, "User", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Invalid scope \"" + scope + "\", use \"User\" or \"Machine\"!");
            var baseName = new String(name.Select(c => Char.IsAsciiLetterOrDigit(c) || (c == '-') || (c == '_') ? c : '-').ToArray()).Trim('-');
            if (baseName.Length <= 0)
                baseName = "item";
            var fileName = baseName + ".desktop";
            var folder = machine ? SystemAutostart : UserAutostart;
            if (File.Exists(Path.Combine(folder, fileName)))
                throw new Exception("An autostart item with the file name \"" + fileName + "\" already exists!");
            var text = String.Concat("[Desktop Entry]\nType=Application\nName=", name, "\nExec=", command, "\nHidden=false\nX-GNOME-Autostart-enabled=true\nComment=Added by SysWeaver\n");
            if (machine)
                UnixCommand.WriteFile(Path.Combine(folder, fileName), text);
            else
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, fileName), text);
            }
            return "xdg|" + fileName;
        }
    }


    static class UnixStartupExt
    {
        /// <summary>
        /// Throw an exception if an exit code isn't zero
        /// </summary>
        public static void ThrowIfFailed(this int exitCode, String what, String err)
        {
            if (exitCode != 0)
                throw new Exception(String.Concat("\"", what, "\" failed: ", String.IsNullOrWhiteSpace(err) ? ("exit code " + exitCode) : err.Trim()));
        }
    }
}
