using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IFilePermissions"/> for Windows (NTFS ACL's).
    /// </summary>
    sealed class Win32FilePermissions : IFilePermissions
    {
        public bool UseAcl => true;

        static readonly (String Name, FileSystemRights Rights)[] RightsMap =
        [
            ("FullControl", FileSystemRights.FullControl),
            ("Modify", FileSystemRights.Modify | FileSystemRights.Synchronize),
            ("ReadAndExecute", FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize),
            ("ListFolderContents", FileSystemRights.ListDirectory | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Traverse | FileSystemRights.Synchronize),
            ("Read", FileSystemRights.Read | FileSystemRights.Synchronize),
            ("Write", FileSystemRights.Write | FileSystemRights.Synchronize),
        ];

        public IReadOnlyList<String> Rights { get; } = RightsMap.Select(x => x.Name).ToArray();

        static readonly (String Name, InheritanceFlags Inherit, PropagationFlags Propagate)[] AppliesToMap =
        [
            ("This folder, subfolders and files", InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None),
            ("This folder only", InheritanceFlags.None, PropagationFlags.None),
            ("This folder and subfolders", InheritanceFlags.ContainerInherit, PropagationFlags.None),
            ("This folder and files", InheritanceFlags.ObjectInherit, PropagationFlags.None),
            ("Subfolders and files only", InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly),
            ("Subfolders only", InheritanceFlags.ContainerInherit, PropagationFlags.InheritOnly),
            ("Files only", InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly),
        ];

        public IReadOnlyList<String> AppliesTo { get; } = AppliesToMap.Select(x => x.Name).ToArray();

        static String RightsText(FileSystemRights r)
        {
            var v = (long)r;
            //  Generic rights (on inherit only entries, ex: CREATOR OWNER)
            if ((v & 0x10000000) != 0)
                return "Full control (generic)";
            var g = new List<String>();
            if ((v & 0x80000000) != 0)
                g.Add("Read (generic)");
            if ((v & 0x40000000) != 0)
                g.Add("Write (generic)");
            if ((v & 0x20000000) != 0)
                g.Add("Execute (generic)");
            if (g.Count > 0)
                return String.Join(", ", g);
            var s = r & ~FileSystemRights.Synchronize;
            if (s == (FileSystemRights.FullControl & ~FileSystemRights.Synchronize))
                return "Full control";
            if (s == FileSystemRights.Modify)
                return "Modify";
            if (s == FileSystemRights.ReadAndExecute)
                return "Read & execute";
            if (s == FileSystemRights.Read)
                return "Read";
            if (s == FileSystemRights.Write)
                return "Write";
            if (s == (FileSystemRights.Read | FileSystemRights.Write))
                return "Read, write";
            if (s == (FileSystemRights.ReadAndExecute | FileSystemRights.Write))
                return "Read & execute, write";
            return s.ToString();
        }

        static String AppliesText(FileSystemAccessRule r, bool isFolder)
        {
            if (!isFolder)
                return "";
            var m = AppliesToMap.FirstOrDefault(x => (x.Inherit == r.InheritanceFlags) && (x.Propagate == (r.PropagationFlags & PropagationFlags.InheritOnly)));
            var s = m.Name ?? (r.InheritanceFlags + ", " + r.PropagationFlags);
            if ((r.PropagationFlags & PropagationFlags.NoPropagateInherit) != 0)
                s += " (one level only)";
            return s;
        }

        static String Name(IdentityReference id)
        {
            try
            {
                return id.Translate(typeof(NTAccount)).Value;
            }
            catch
            {
                return id.Value;
            }
        }

        static FileSystemSecurity Get(String path, out bool isFolder, AccessControlSections sections)
        {
            isFolder = Directory.Exists(path);
            if (isFolder)
                return new DirectoryInfo(path).GetAccessControl(sections);
            if (!File.Exists(path))
                throw new FileNotFoundException("\"" + path + "\" doesn't exist!");
            return new FileInfo(path).GetAccessControl(sections);
        }

        static void Set(String path, bool isFolder, FileSystemSecurity sec)
        {
            try
            {
                if (isFolder)
                    new DirectoryInfo(path).SetAccessControl((DirectorySecurity)sec);
                else
                    new FileInfo(path).SetAccessControl((FileSecurity)sec);
            }
            catch (PrivilegeNotHeldException ex)
            {
                throw new UnauthorizedAccessException("A privilege is missing (run the server as admin): " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("Access denied, changing the permissions of \"" + path + "\" requires the \"change permissions\" right (or ownership): " + ex.Message);
            }
        }

        static List<FileSystemAccessRule> Rules(FileSystemSecurity sec)
            => sec.GetAccessRules(true, true, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>().ToList();

        public OsFilePermissions GetPermissions(String path)
        {
            path = Path.GetFullPath(path);
            var sec = Get(path, out var isFolder, AccessControlSections.Access | AccessControlSections.Owner);
            String owner = "";
            try
            {
                var o = sec.GetOwner(typeof(SecurityIdentifier));
                owner = o == null ? "" : Name(o);
            }
            catch
            {
            }
            String sddl = "";
            try
            {
                sddl = sec.GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);
            }
            catch
            {
            }
            return new OsFilePermissions
            {
                Path = path,
                IsFolder = isFolder,
                Owner = owner,
                Group = "",
                Mode = -1,
                InheritanceEnabled = !sec.AreAccessRulesProtected,
                Entries = Rules(sec).Select(r => new OsFileAccessEntry
                {
                    Identity = Name(r.IdentityReference),
                    Allow = r.AccessControlType == AccessControlType.Allow,
                    Rights = RightsText(r.FileSystemRights),
                    Inherited = r.IsInherited,
                    AppliesTo = AppliesText(r, isFolder),
                }).ToArray(),
                Text = sddl,
            };
        }

        static SecurityIdentifier Sid(String identity)
        {
            identity = (identity ?? "").Trim();
            if (identity.Length <= 0)
                throw new ArgumentException("No user or group!");
            try
            {
                if (identity.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
                    return new SecurityIdentifier(identity);
                return (SecurityIdentifier)new NTAccount(identity).Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                throw new ArgumentException("The user or group \"" + identity + "\" doesn't exist!");
            }
        }

        public void SetOwner(String path, String owner, bool recursive)
        {
            path = Path.GetFullPath(path);
            var sid = Sid(owner);
            //  Setting another owner than yourself requires these (admins have them, disabled)
            Win32Privileges.Enable("SeTakeOwnershipPrivilege");
            Win32Privileges.Enable("SeRestorePrivilege");
            void one(String p)
            {
                var sec = Get(p, out var isFolder, AccessControlSections.Owner);
                sec.SetOwner(sid);
                Set(p, isFolder, sec);
            }
            one(path);
            if (recursive && Directory.Exists(path))
            {
                int failed = 0;
                String first = null;
                foreach (var p in Walk(path))
                {
                    try
                    {
                        one(p);
                    }
                    catch (Exception ex)
                    {
                        ++failed;
                        first ??= p + ": " + ex.Message;
                    }
                }
                if (failed > 0)
                    throw new Exception(String.Concat("The owner of ", failed, " items couldn't be changed, ex: ", first));
            }
        }

        /// <summary>
        /// All files and folders in a folder (recursive, links are not followed)
        /// </summary>
        static IEnumerable<String> Walk(String folder)
        {
            var o = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return Directory.EnumerateFileSystemEntries(folder, "*", o);
        }

        public void SetGroup(String path, String group, bool recursive) => throw new PlatformNotSupportedException("Windows files don't have a group (add an entry instead)!");

        public void SetMode(String path, int mode, bool recursive) => throw new PlatformNotSupportedException("Windows files don't have permission bits (add or remove entries instead)!");

        public void AddEntry(String path, String identity, bool allow, String rights, String appliesTo)
        {
            path = Path.GetFullPath(path);
            var sid = Sid(identity);
            var rm = RightsMap.FirstOrDefault(x => String.Equals(x.Name, rights, StringComparison.OrdinalIgnoreCase));
            if (rm.Name == null)
                throw new ArgumentException("Unknown rights \"" + rights + "\", use one of: " + String.Join(", ", Rights));
            var sec = Get(path, out var isFolder, AccessControlSections.Access);
            var inh = InheritanceFlags.None;
            var prop = PropagationFlags.None;
            if (isFolder)
            {
                var am = AppliesToMap.FirstOrDefault(x => String.Equals(x.Name, appliesTo, StringComparison.OrdinalIgnoreCase));
                if (am.Name == null)
                    am = AppliesToMap[0];
                inh = am.Inherit;
                prop = am.Propagate;
            }
            sec.AddAccessRule(new FileSystemAccessRule(sid, rm.Rights, inh, prop, allow ? AccessControlType.Allow : AccessControlType.Deny));
            Set(path, isFolder, sec);
        }

        public void RemoveEntry(String path, int index)
        {
            path = Path.GetFullPath(path);
            var sec = Get(path, out var isFolder, AccessControlSections.Access);
            var rules = Rules(sec);
            if ((index < 0) || (index >= rules.Count))
                throw new ArgumentException("The entry doesn't exist (the permissions may have changed, reload)!");
            var r = rules[index];
            if (r.IsInherited)
                throw new Exception("Inherited entries can't be removed (change them on the parent folder, or disable inheritance)!");
            sec.RemoveAccessRuleSpecific(r);
            Set(path, isFolder, sec);
        }

        public void SetInheritance(String path, bool enabled, bool keepCopies)
        {
            path = Path.GetFullPath(path);
            var sec = Get(path, out var isFolder, AccessControlSections.Access);
            sec.SetAccessRuleProtection(!enabled, keepCopies);
            Set(path, isFolder, sec);
        }
    }
}
