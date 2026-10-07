using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;


namespace SysWeaver
{
    /// <summary>
    /// Use this to get base folders for application data.
    /// Folders can be configured in the ApplicationName.Config.json config file, using the keys:
    /// - "AllFolders" for folders not specific to the running user.
    /// - "UserFolders" for folders specific to the running user (either use the "$(LocalApplicationData)" to use the OS users home folder, or the "$(UserName)").
    /// You can optionally or additionally override any of the derived folders using the keys:
    /// - "AllSharedFolders".
    /// - "AllAppFolders".
    /// - "UserSharedFolders".
    /// - "UserAppFolders".
    /// Multiple folders can be specified, separated by the platform path separator (';' on Windows, ':' on Unix), and may contain <see cref="PathTemplate"/> variables.
    /// When multiple folders are available, use <see cref="SelectFolder(IReadOnlyList{string}, string)"/> to distribute data among them.
    /// The key folder is configured using the "KeyFolder" key (defaults to <see cref="IPlatformTools.DefaultKeyDir"/>).
    /// </summary>
    /// <remarks>
    /// All folders are resolved and created (if missing) in the static constructor, which also marks them as not content indexed.
    /// Folders shared by all users are made accessible to everyone (using <see cref="IPlatformTools.MakeDirectoryAccessableToEveryOne(string)"/>),
    /// so data written by a service (ex: running as SYSTEM or root) stays modifiable by users and vice versa.
    /// On Linux "$(CommonApplicationData)" is "/var/lib" (see <see cref="PathTemplate.CommonApplicationData"/>), where only root can create folders:
    /// when root runs first, the "SysWeaver" root folder is created with rwx for everyone and the sticky bit (so any user can create application folders in it).
    /// If an all user folder doesn't exist and can't be created (ex: a normal user runs before root), the matching user folders are used instead
    /// (<see cref="AllSharedFolders"/> = <see cref="UserSharedFolders"/>, <see cref="AllAppFolders"/> = <see cref="UserAppFolders"/>) and a warning is written to the console.
    /// </remarks>
    public static class Folders
    {

        /// <summary>
        /// Folders to use for shared data between all SysWeaver apps, shared for all OS users.
        /// Default: "$(CommonApplicationData)/SysWeaver/Shared" (Windows: "C:\ProgramData\SysWeaver\Shared", Linux: "/var/lib/SysWeaver/Shared").
        /// </summary>
        public static readonly IReadOnlyList<String> AllSharedFolders;

        /// <summary>
        /// Folders to use for private data for this app, shared for all OS users.
        /// Default: "$(CommonApplicationData)/SysWeaver/[AppAssemblyName]_[AppGuid]" (Windows: "C:\ProgramData\SysWeaver\...", Linux: "/var/lib/SysWeaver/...").
        /// </summary>
        public static readonly IReadOnlyList<String> AllAppFolders;

        /// <summary>
        /// Folders to use for shared data between all SysWeaver apps, unique to the running user.
        /// Default: "$(LocalApplicationData)/SysWeaver/Shared".
        /// </summary>
        public static readonly IReadOnlyList<String> UserSharedFolders;

        /// <summary>
        /// Folders to use for private data for this app, unique to the running user.
        /// Default: "$(LocalApplicationData)/SysWeaver/[AppAssemblyName]_[AppGuid]".
        /// </summary>
        public static readonly IReadOnlyList<String> UserAppFolders;

        /// <summary>
        /// The folder to use for key files, from the "KeyFolder" config key or <see cref="IPlatformTools.DefaultKeyDir"/>, without a trailing directory separator.
        /// Not resolved, created or validated.
        /// </summary>
        public static readonly String KeyFolder;

        /// <summary>
        /// Append a path to a set of root paths, the resulting folders are resolved, made absolute and created (if missing)
        /// </summary>
        /// <param name="roots">Root paths</param>
        /// <param name="paths">The (single) relative path to append to each root (using Path.Combine), null or empty to use the roots as is. May contain <see cref="PathTemplate"/> variables.</param>
        /// <param name="allowAll">Make newly validated folders accessible to all users</param>
        /// <returns>Resulting paths (a new array)</returns>
        /// <remarks>
        /// Paths are always resolved and made absolute, but a folder is only created the first time it's seen (process wide).
        /// </remarks>
        /// <exception cref="IOException">A folder couldn't be created.</exception>
        public static String[] Append(IReadOnlyList<String> roots, String paths, bool allowAll = false)
        {
            var ti = roots.Count;
            var d = new String[ti];
            if (String.IsNullOrEmpty(paths))
            {
                for (int i = 0; i < ti; ++i)
                    d[i] = roots[i];
            }
            else
            {
                for (int i = 0; i < ti; ++i)
                    d[i] = Path.Combine(roots[i], paths);
            }
            Validate(d, allowAll);
            return d;
        }

        /// <summary>
        /// Get a set of folders from config or using defaults.
        /// </summary>
        /// <param name="keyName">The config key to use (see <see cref="Config"/>), can contains any number of folders separated by the platform path separator (';' on Windows, ':' on Unix), is resolved using the PathTemplate.Resolve methods so any variables can be used</param>
        /// <param name="defaultRoots">If the key is not in the config use these root paths</param>
        /// <param name="defaultPath">If the key is not in the config, append this path to the roots</param>
        /// <param name="allowAll">Make newly validated folders accessible to all users</param>
        /// <returns>The absolute folder paths (folders are created if missing)</returns>
        public static String[] FromConfig(String keyName, IReadOnlyList<String> defaultRoots, String defaultPath, bool allowAll = false)
        {
            Config.TryGetString(keyName, out var x);
            var f = SplitFolders(x);
            //  Append validates (a folder is only set up the first time it's seen, so allowAll must be passed on)
            if (f == null)
                return Append(defaultRoots, defaultPath, allowAll);
            Validate(f, allowAll);
            return f;
        }

        /// <summary>
        /// Get a set of folders from a string or using defaults.
        /// </summary>
        /// <param name="paths">The paths to use, can be null or empty to use defaults, can contains any number of folders separated by the platform path separator (';' on Windows, ':' on Unix), is resolved using the PathTemplate.Resolve methods so any variables can be used</param>
        /// <param name="defaultRoots">If <paramref name="paths"/> contains no folders, use these root paths</param>
        /// <param name="defaultPath">If <paramref name="paths"/> contains no folders, append this path to the roots</param>
        /// <param name="allowAll">Make newly validated folders accessible to all users</param>
        /// <returns>The absolute folder paths (folders are created if missing)</returns>
        public static String[] FromString(String paths, IReadOnlyList<String> defaultRoots, String defaultPath, bool allowAll = false)
        {
            var f = SplitFolders(paths);
            //  Append validates (a folder is only set up the first time it's seen, so allowAll must be passed on)
            if (f == null)
                return Append(defaultRoots, defaultPath, allowAll);
            Validate(f, allowAll);
            return f;
        }

        /// <summary>
        /// Select one folder of possible many, using hashing of a key for "balancing"
        /// </summary>
        /// <param name="folders">The folders to choose from</param>
        /// <param name="key">A key, like a filename for instance (case sensitive)</param>
        /// <returns>The chosen folder, the same key always maps to the same folder as long as the folder list is unchanged</returns>
        /// <remarks><paramref name="folders"/> may not be empty (an index out of range exception is thrown).</remarks>
        public static String SelectFolder(IReadOnlyList<String> folders, String key)
        {
            var fl = folders.Count;
            if (fl <= 1)
                return folders[0];
            return folders[(int)(QuickHash.Hash(key) % fl)];
        }

        /// <summary>
        /// Get the base folder based on the use case
        /// </summary>
        /// <param name="perUser">Get a unique path per user or not</param>
        /// <param name="perApp">Get a unique path per application</param>
        /// <returns>The base paths to use</returns>
        public static IReadOnlyList<String> GetBase(bool perUser, bool perApp)
            => perApp
                ?
                    (perUser ? UserAppFolders : AllAppFolders)
                :
                    (perUser ? UserSharedFolders : AllSharedFolders)
                ;



        static String[] SplitFolders(String paths)
        {
            if (paths == null)
                return null;
            var t = paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ti = t.Length;
            if (ti <= 0)
                return null;
            for (int i = 0; i < ti; ++i)
                t[i] = t[i].RemoveQuotes();
            return t;
        }


        /// <summary>
        /// Folders that have been validated, the value is true if the folder was made accessible to everyone
        /// </summary>
        static readonly ConcurrentDictionary<String, bool> Seen = new(StringComparer.Ordinal);

        static void Validate(String[] paths, bool allowAll)
        {
            var ti = paths.Length;
            var seen = Seen;
            for (int i = 0; i < ti; ++i)
            {
                var fp = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(paths[i]));
                paths[i] = fp;
                //  Only create and set attributes the first time a folder is seen (or the first time all access is requested)
                if (seen.TryGetValue(fp, out var wasAll) && (wasAll || !allowAll))
                    continue;
                seen[fp] = allowAll;
                PathExt.EnsureFolderExist(fp);
                if (allowAll)
                    PlatformTools.Current.MakeDirectoryAccessableToEveryOne(fp);
                var di = new DirectoryInfo(fp);
                if ((di.Attributes & FileAttributes.NotContentIndexed) == 0)
                {
                    try
                    {
                        di.Attributes |= FileAttributes.NotContentIndexed;
                    }
                    catch
                    {
                    }
                }
            }
        }



        static Folders()
        {
            Config.TryGetString("KeyFolder", out var x);
            x = x ?? PlatformTools.Current.DefaultKeyDir;
            KeyFolder = x.TrimEnd(Path.DirectorySeparatorChar);
            //  Base folders
            Config.TryGetString("AllFolders", out x);
            var allFolders = SplitFolders(x) ?? [@"$(CommonApplicationData)" + Path.DirectorySeparatorChar + "SysWeaver"];
            Config.TryGetString("UserFolders", out x);
            var userFolders = SplitFolders(x) ?? [@"$(LocalApplicationData)" + Path.DirectorySeparatorChar + "SysWeaver"];
            if (OperatingSystem.IsLinux())
                SetupLinuxRoots(allFolders);
            //  Derived folders
            var userShared = FromConfig("UserSharedFolders", userFolders, "Shared");
            var userApp = FromConfig("UserAppFolders", userFolders, "$(*AppAssemblyName)_$(*AppGuid)");
            UserSharedFolders = userShared;
            UserAppFolders = userApp;
            AllSharedFolders = AllOrUser("AllSharedFolders", FromConfig("AllSharedFolders", allFolders, "Shared", true), userShared);
            AllAppFolders = AllOrUser("AllAppFolders", FromConfig("AllAppFolders", allFolders, "$(*AppAssemblyName)_$(*AppGuid)", true), userApp);
        }

        /// <summary>
        /// Use the all user folders if they all exist, else fall back to the user folders (ex: on Linux only root can create folders in "/var/lib")
        /// </summary>
        static IReadOnlyList<String> AllOrUser(String name, String[] all, IReadOnlyList<String> user)
        {
            foreach (var x in all)
            {
                if (Directory.Exists(x))
                    continue;
                Console.WriteLine("Folders - Warning: The folder " + x.ToQuoted() + " couldn't be created, using " + String.Join(Path.PathSeparator, user).ToQuoted() + " for " + name + " (data isn't shared with other users)!");
                if (OperatingSystem.IsLinux())
                    Console.WriteLine("Folders - Warning: Run the application once as root, or create the folder using: sudo mkdir -p " + Path.GetDirectoryName(x).ToQuoted() + " && sudo chmod 1777 " + Path.GetDirectoryName(x).ToQuoted());
                return user;
            }
            return all;
        }

        /// <summary>
        /// On Linux the all user root folders (ex: "/var/lib/SysWeaver") must allow anyone to create entries (application folders),
        /// they get rwx for everyone and the sticky bit (like "/tmp": users can't delete or rename each others entries).
        /// The content (ex: the "Shared" and application folders) is made accessible to everyone by <see cref="Validate(string[], bool)"/>.
        /// Only root (or the owner) can create or change the folder, failures are ignored (see <see cref="AllOrUser(string, string[], IReadOnlyList{string})"/>).
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        static void SetupLinuxRoots(String[] roots)
        {
            const UnixFileMode all = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                | UnixFileMode.StickyBit;
            foreach (var r in roots)
            {
                try
                {
                    var fp = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(r));
                    if (PathExt.EnsureFolderExist(fp) != null)
                        continue;
                    var di = new DirectoryInfo(fp);
                    if (di.LinkTarget != null)
                        continue;
                    var m = di.UnixFileMode;
                    if ((m & all) != all)
                        di.UnixFileMode = m | all;
                }
                catch
                {
                }
            }
        }


    }

}
