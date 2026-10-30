using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IStartupItems"/> for Windows: the Run / RunOnce registry keys (HKCU, HKLM and the 32-bit view) and the startup folders (user and all users).
    /// Enabled / disabled is stored like Task Manager does (Explorer\StartupApproved), so both shows the same state.
    /// </summary>
    sealed class Win32Startup : IStartupItems
    {
        public IReadOnlyList<String> AddScopes { get; } = ["User", "Machine"];

        const String RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const String RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        const String Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        const String RunOnce32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce";
        const String ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        sealed class RegLocation
        {
            public bool Machine;
            public String Key;
            public String Type;
            /// <summary>
            /// The StartupApproved sub key (null if it can't be disabled)
            /// </summary>
            public String Approved;
        }

        static readonly RegLocation[] RegLocations =
        [
            new RegLocation { Machine = false, Key = RunKey, Type = "Run key", Approved = "Run" },
            new RegLocation { Machine = false, Key = RunOnceKey, Type = "RunOnce key" },
            new RegLocation { Machine = true, Key = RunKey, Type = "Run key", Approved = "Run" },
            new RegLocation { Machine = true, Key = Run32Key, Type = "Run key (32-bit)", Approved = "Run32" },
            new RegLocation { Machine = true, Key = RunOnceKey, Type = "RunOnce key" },
            new RegLocation { Machine = true, Key = RunOnce32Key, Type = "RunOnce key (32-bit)" },
        ];

        static RegistryKey Hive(bool machine) => machine ? Registry.LocalMachine : Registry.CurrentUser;

        static String HiveName(bool machine) => machine ? "HKLM" : "HKCU";

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        /// <summary>
        /// Read the StartupApproved state, true if enabled (no value = enabled)
        /// </summary>
        static bool IsApproved(bool machine, String approved, String name)
        {
            try
            {
                using var k = Hive(machine).OpenSubKey(ApprovedKey + approved, false);
                if (k?.GetValue(name) is byte[] b && (b.Length > 0))
                    return (b[0] & 1) == 0;
            }
            catch
            {
            }
            return true;
        }

        static void SetApproved(bool machine, String approved, String name, bool enabled)
        {
            var b = new byte[12];
            b[0] = (byte)(enabled ? 2 : 3);
            if (!enabled)
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(b, 4);
            try
            {
                using var k = Hive(machine).CreateSubKey(ApprovedKey + approved, true);
                k.SetValue(name, b, RegistryValueKind.Binary);
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Access denied, changing items for all users requires admin!");
            }
        }

        static void RemoveApproved(bool machine, String approved, String name)
        {
            try
            {
                using var k = Hive(machine).OpenSubKey(ApprovedKey + approved, true);
                k?.DeleteValue(name, false);
            }
            catch
            {
            }
        }

        static String FolderOf(bool machine) => Environment.GetFolderPath(machine ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup);

        /// <summary>
        /// Get the target, arguments and working folder of a shortcut (null if it can't be read)
        /// </summary>
        static (String Target, String Args, String Dir) Shortcut(String file)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null)
                    return default;
                dynamic shell = Activator.CreateInstance(t);
                try
                {
                    dynamic lnk = shell.CreateShortcut(file);
                    return ((String)lnk.TargetPath ?? "", (String)lnk.Arguments ?? "", (String)lnk.WorkingDirectory ?? "");
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                return default;
            }
        }

        static String RegId(RegLocation l, String name) => String.Concat("reg|", HiveName(l.Machine), "|", l.Key, "|", name);

        static String FileId(String file) => "file|" + file;

        static OsStartupItem RegItem(RegLocation l, RegistryKey k, String name)
        {
            var v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
            return new OsStartupItem
            {
                Id = RegId(l, name),
                Name = name,
                Command = v,
                Location = String.Concat(HiveName(l.Machine), "\\", l.Key),
                Type = l.Type,
                Scope = l.Machine ? "Machine" : "User",
                Enabled = l.Approved == null || IsApproved(l.Machine, l.Approved, name),
                CanDisable = l.Approved != null,
                CanRemove = true,
                File = "",
                Properties = l.Type.StartsWith("RunOnce", StringComparison.Ordinal) ? [P("Note", "Runs once at the next log on and is then removed")] : [],
            };
        }

        static OsStartupItem FileItem(bool machine, String file)
        {
            var name = Path.GetFileName(file);
            var props = new List<KeyValuePair<String, String>>();
            String cmd = file;
            if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var (target, args, dir) = Shortcut(file);
                if (!String.IsNullOrEmpty(target))
                {
                    cmd = target.IndexOf(' ') >= 0 ? ("\"" + target + "\"") : target;
                    if (!String.IsNullOrEmpty(args))
                        cmd += " " + args;
                    props.Add(P("Target", target));
                    if (!String.IsNullOrEmpty(args))
                        props.Add(P("Arguments", args));
                    if (!String.IsNullOrEmpty(dir))
                        props.Add(P("Start in", dir));
                }
                name = Path.GetFileNameWithoutExtension(file);
            }
            return new OsStartupItem
            {
                Id = FileId(file),
                Name = name,
                Command = cmd,
                Location = Path.GetDirectoryName(file),
                Type = "Startup folder",
                Scope = machine ? "Machine" : "User",
                Enabled = IsApproved(machine, "StartupFolder", Path.GetFileName(file)),
                CanDisable = true,
                CanRemove = true,
                File = file,
                Properties = props.ToArray(),
            };
        }

        public IReadOnlyList<OsStartupItem> GetItems()
        {
            var res = new List<OsStartupItem>();
            foreach (var l in RegLocations)
            {
                try
                {
                    using var k = Hive(l.Machine).OpenSubKey(l.Key, false);
                    if (k == null)
                        continue;
                    foreach (var n in k.GetValueNames())
                        if (n.Length > 0)
                            res.Add(RegItem(l, k, n));
                }
                catch
                {
                }
            }
            foreach (var machine in new[] { false, true })
            {
                try
                {
                    var f = FolderOf(machine);
                    if (String.IsNullOrEmpty(f) || !Directory.Exists(f))
                        continue;
                    foreach (var file in Directory.EnumerateFiles(f))
                    {
                        if (String.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                            continue;
                        res.Add(FileItem(machine, file));
                    }
                }
                catch
                {
                }
            }
            return res;
        }

        /// <summary>
        /// Parse an id
        /// </summary>
        static (RegLocation Loc, String Name, String File) Parse(String id)
        {
            id ??= "";
            if (id.StartsWith("file|", StringComparison.Ordinal))
            {
                var file = id.Substring(5);
                var dir = Path.GetDirectoryName(file) ?? "";
                if ((!String.Equals(dir, FolderOf(false), StringComparison.OrdinalIgnoreCase)) && (!String.Equals(dir, FolderOf(true), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Not a startup folder item \"" + id + "\"!");
                return (null, null, file);
            }
            var p = id.Split('|', 4);
            if ((p.Length == 4) && (p[0] == "reg"))
            {
                var machine = p[1] == "HKLM";
                var l = RegLocations.FirstOrDefault(x => (x.Machine == machine) && String.Equals(x.Key, p[2], StringComparison.OrdinalIgnoreCase));
                if (l != null)
                    return (l, p[3], null);
            }
            throw new ArgumentException("Invalid startup item id \"" + id + "\"!");
        }

        public OsStartupItem GetItem(String id)
        {
            var (l, name, file) = Parse(id);
            if (file != null)
                return File.Exists(file) ? FileItem(String.Equals(Path.GetDirectoryName(file), FolderOf(true), StringComparison.OrdinalIgnoreCase), file) : null;
            using var k = Hive(l.Machine).OpenSubKey(l.Key, false);
            if ((k == null) || !Array.Exists(k.GetValueNames(), n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                return null;
            return RegItem(l, k, name);
        }

        public void SetEnabled(String id, bool enabled)
        {
            var (l, name, file) = Parse(id);
            if (file != null)
            {
                SetApproved(String.Equals(Path.GetDirectoryName(file), FolderOf(true), StringComparison.OrdinalIgnoreCase), "StartupFolder", Path.GetFileName(file), enabled);
                return;
            }
            if (l.Approved == null)
                throw new Exception("RunOnce items can't be disabled (remove it instead)!");
            SetApproved(l.Machine, l.Approved, name, enabled);
        }

        public void Remove(String id)
        {
            var (l, name, file) = Parse(id);
            try
            {
                if (file != null)
                {
                    File.Delete(file);
                    RemoveApproved(String.Equals(Path.GetDirectoryName(file), FolderOf(true), StringComparison.OrdinalIgnoreCase), "StartupFolder", Path.GetFileName(file));
                    return;
                }
                using (var k = Hive(l.Machine).OpenSubKey(l.Key, true) ?? throw new Exception("The key doesn't exist!"))
                    k.DeleteValue(name, true);
                if (l.Approved != null)
                    RemoveApproved(l.Machine, l.Approved, name);
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Access denied, changing items for all users requires admin!");
            }
            catch (ArgumentException)
            {
                throw new Exception("The item \"" + name + "\" doesn't exist!");
            }
        }

        public String Add(String scope, String name, String command)
        {
            name = (name ?? "").Trim();
            command = (command ?? "").Trim();
            if (name.Length <= 0)
                throw new ArgumentException("No name!");
            if (command.Length <= 0)
                throw new ArgumentException("No command!");
            if ((name.IndexOf('\\') >= 0) || name.Any(Char.IsControl))
                throw new ArgumentException("Invalid name \"" + name + "\"!");
            var machine = String.Equals(scope, "Machine", StringComparison.OrdinalIgnoreCase);
            if ((!machine) && (!String.Equals(scope, "User", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Invalid scope \"" + scope + "\", use \"User\" or \"Machine\"!");
            var l = RegLocations.First(x => (x.Machine == machine) && (x.Key == RunKey));
            try
            {
                using var k = Hive(machine).CreateSubKey(RunKey, true);
                if (Array.Exists(k.GetValueNames(), n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("An item named \"" + name + "\" already exists!");
                k.SetValue(name, command, command.IndexOf('%') >= 0 ? RegistryValueKind.ExpandString : RegistryValueKind.String);
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Access denied, adding items for all users requires admin!");
            }
            return RegId(l, name);
        }
    }
}
