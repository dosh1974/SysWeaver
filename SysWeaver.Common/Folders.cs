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
    /// /// </summary>
    public static class Folders
    {

        /// <summary>
        /// Folders to use for shared data between all SysWeaver apps, shared for all OS users.
        /// </summary>
        public static readonly IReadOnlyList<String> AllSharedFolders;

        /// <summary>
        /// Folders to use for private data for this app, shared for all OS users.
        /// </summary>
        public static readonly IReadOnlyList<String> AllAppFolders;

        /// <summary>
        /// Folders to use for shared data between all SysWeaver apps, unique to the running user.
        /// </summary>
        public static readonly IReadOnlyList<String> UserSharedFolders;

        /// <summary>
        /// Folders to use for private data for this app, unique to the running user.
        /// </summary>
        public static readonly IReadOnlyList<String> UserAppFolders;

        /// <summary>
        /// The folder to use for key files.
        /// </summary>
        public static readonly String KeyFolder;

        /// <summary>
        /// Append a path to a set of root paths
        /// </summary>
        /// <param name="roots">Root paths</param>
        /// <param name="paths">The path(s) to append (using Path.Combine)</param>
        /// <param name="allowAll">Allow all users</param>
        /// <returns>Resulting paths</returns>
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
        /// <param name="keyName">The config key to use, can contains any number of folders separated by a ;, is resolved using the PathTemplate.Resolve methods so any variables can be used</param>
        /// <param name="defaultRoots">If the key is not in the config use these root paths</param>
        /// <param name="defaultPath">If the key is not in the config, append this path to the roots</param>
        /// <param name="allowAll">Allow all users</param>
        /// <returns></returns>
        public static String[] FromConfig(String keyName, IReadOnlyList<String> defaultRoots, String defaultPath, bool allowAll = false)
        {
            Config.TryGetString(keyName, out var x);
            var f = SplitFolders(x) ?? Append(defaultRoots, defaultPath);
            Validate(f, allowAll);
            return f;
        }

        /// <summary>
        /// Get a set of folders from config or using defaults.
        /// </summary>
        /// <param name="paths">The paths to use, can be null or empty to use defaults, can contains any number of folders separated by a ;, is resolved using the PathTemplate.Resolve methods so any variables can be used</param>
        /// <param name="defaultRoots">If the key is not in the config use these root paths</param>
        /// <param name="defaultPath">If the key is not in the config, append this path to the roots</param>
        /// <param name="allowAll">Allow all users</param>
        /// <returns></returns>
        public static String[] FromString(String paths, IReadOnlyList<String> defaultRoots, String defaultPath, bool allowAll = false)
        {
            var f = SplitFolders(paths) ?? Append(defaultRoots, defaultPath);
            Validate(f, allowAll);
            return f;
        }

        /// <summary>
        /// Select one folder of possible many, using hashing of a key for "balancing"
        /// </summary>
        /// <param name="folders">The folders to choose from</param>
        /// <param name="key">A key, like a filename for instance</param>
        /// <returns>The chosen folder</returns>
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


        static readonly ConcurrentDictionary<String, int> Seen = new(StringComparer.Ordinal);

        static void Validate(String[] paths, bool allowAll)
        {
            var ti = paths.Length;
            var seen = Seen;
            for (int i = 0; i < ti; ++i)
            {
                var fp = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(paths[i]));
                if (!seen.TryAdd(fp, 0))
                    continue;
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
                paths[i] = fp;
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
            //  Derived folders
            AllSharedFolders = FromConfig("AllSharedFolders", allFolders, "Shared", true);
            AllAppFolders = FromConfig("AllAppFolders", allFolders, "$(*AppAssemblyName)_$(*AppGuid)", true);
            UserSharedFolders = FromConfig("UserFolders", userFolders, "Shared");
            UserAppFolders = FromConfig("UserAppFolders", userFolders, "$(*AppAssemblyName)_$(*AppGuid)");

        }


    }

}
