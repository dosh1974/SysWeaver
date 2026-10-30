using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IFilePermissions"/> for Linux: owner, group and the permission bits ("stat", chown / chgrp / chmod, using sudo when needed).
    /// POSIX ACL's are shown (getfacl, if installed) but not edited.
    /// </summary>
    sealed class UnixFilePermissions : IFilePermissions
    {
        public bool UseAcl => false;

        public IReadOnlyList<String> Rights { get; } = [];

        public IReadOnlyList<String> AppliesTo { get; } = [];

        static String Rwx(int bits) => String.Concat((bits & 4) != 0 ? "r" : "-", (bits & 2) != 0 ? "w" : "-", (bits & 1) != 0 ? "x" : "-");

        static String Full(String path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("\"" + path + "\" doesn't exist!");
            return path;
        }

        public OsFilePermissions GetPermissions(String path)
        {
            path = Full(path);
            var isFolder = Directory.Exists(path);
            var stat = UnixCommand.Get(UnixCommand.Find("stat") ?? "/usr/bin/stat", "-c", "%U:%G:%a", "--", path).Trim().Split(':');
            var owner = stat.Length > 0 ? stat[0] : "";
            var group = stat.Length > 1 ? stat[1] : "";
            var mode = (int)File.GetUnixFileMode(path);
            var entries = new List<OsFileAccessEntry>();
            String text = "";
            var getfacl = UnixCommand.Find("getfacl");
            if (getfacl != null)
            {
                try
                {
                    text = UnixCommand.Get(getfacl, "-p", "--", path);
                    foreach (var raw in text.Split('\n'))
                    {
                        var l = raw.Trim();
                        if ((l.Length <= 0) || (l[0] == '#'))
                            continue;
                        var c = l.IndexOf('#');
                        if (c > 0)
                            l = l.Substring(0, c).Trim();
                        var p = l.Split(':');
                        var isDefault = p[0] == "default";
                        if (isDefault)
                            p = p.Skip(1).ToArray();
                        if (p.Length < 3)
                            continue;
                        var who = p[0] switch
                        {
                            "user" => p[1].Length > 0 ? ("User " + p[1]) : ("Owner (" + owner + ")"),
                            "group" => p[1].Length > 0 ? ("Group " + p[1]) : ("Owning group (" + group + ")"),
                            "mask" => "Mask (max rights of groups and named users)",
                            "other" => "Others",
                            _ => p[0] + " " + p[1],
                        };
                        entries.Add(new OsFileAccessEntry
                        {
                            Identity = who,
                            Allow = true,
                            Rights = p[2],
                            Inherited = false,
                            AppliesTo = isDefault ? "New files and folders (default)" : (isFolder ? "This folder" : ""),
                        });
                    }
                }
                catch
                {
                    entries.Clear();
                }
            }
            if (entries.Count <= 0)
            {
                entries.Add(new OsFileAccessEntry { Identity = "Owner (" + owner + ")", Allow = true, Rights = Rwx(mode >> 6), AppliesTo = "" });
                entries.Add(new OsFileAccessEntry { Identity = "Owning group (" + group + ")", Allow = true, Rights = Rwx(mode >> 3), AppliesTo = "" });
                entries.Add(new OsFileAccessEntry { Identity = "Others", Allow = true, Rights = Rwx(mode), AppliesTo = "" });
            }
            return new OsFilePermissions
            {
                Path = path,
                IsFolder = isFolder,
                Owner = owner,
                Group = group,
                Mode = mode,
                InheritanceEnabled = false,
                Entries = entries.ToArray(),
                Text = text,
            };
        }

        static void CheckName(String n, String what)
        {
            if (String.IsNullOrWhiteSpace(n) || n.StartsWith('-') || n.Any(c => Char.IsWhiteSpace(c) || Char.IsControl(c) || (c == ':')))
                throw new ArgumentException("Invalid " + what + " \"" + n + "\"!");
        }

        static void Run(String program, bool recursive, String arg, String path)
        {
            var exe = UnixCommand.Find(program) ?? ("/bin/" + program);
            if (recursive)
                UnixCommand.Change(exe, "-R", arg, "--", path);
            else
                UnixCommand.Change(exe, arg, "--", path);
        }

        public void SetOwner(String path, String owner, bool recursive)
        {
            path = Full(path);
            owner = (owner ?? "").Trim();
            CheckName(owner, "owner");
            Run("chown", recursive, owner, path);
        }

        public void SetGroup(String path, String group, bool recursive)
        {
            path = Full(path);
            group = (group ?? "").Trim();
            CheckName(group, "group");
            Run("chgrp", recursive, group, path);
        }

        public void SetMode(String path, int mode, bool recursive)
        {
            path = Full(path);
            if ((mode < 0) || (mode > 0xFFF))
                throw new ArgumentException("Invalid mode " + mode + "!");
            if (!recursive)
            {
                try
                {
                    File.SetUnixFileMode(path, (UnixFileMode)mode);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            Run("chmod", recursive, Convert.ToString(mode, 8).PadLeft(4, '0'), path);
        }

        public void AddEntry(String path, String identity, bool allow, String rights, String appliesTo)
            => throw new PlatformNotSupportedException("Editing ACL entries isn't supported on Linux, change the owner, group or mode (or use setfacl)!");

        public void RemoveEntry(String path, int index)
            => throw new PlatformNotSupportedException("Editing ACL entries isn't supported on Linux, change the owner, group or mode (or use setfacl)!");

        public void SetInheritance(String path, bool enabled, bool keepCopies)
            => throw new PlatformNotSupportedException("Linux permissions aren't inherited!");
    }
}
