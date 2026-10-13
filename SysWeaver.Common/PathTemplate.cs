using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Resolve a "path" template to a full path.
    /// Variables in the template starts with "$(" and ends with ")".
    /// Variables can be any value of the Environment.SpecialFolder enum, or any of the ones in the supplied dictionary.
    /// Ex:
    /// "$(LocalApplicationData)\MyAppsData\StateBlockUntil.json"
    /// Some common folder variables:
    ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
    ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
    ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
    ///             $(MyPictures) = The My Pictures folder.
    /// Env info variables:
    ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
    ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
    ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
    ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
    ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
    ///             $(AppGuid) = A guid derived from the application assembly name (stable between runs)
    ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
    ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
    ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
    /// See <see cref="EnvInfo.TextVars"/> for all env info variables.
    /// </summary>
    public static class PathTemplate
    {
        /// <summary>
        /// The folder for application data shared by all users, this is what "$(CommonApplicationData)" resolves to.
        /// Windows (and other platforms): <see cref="Environment.SpecialFolder.CommonApplicationData"/>, ex: "C:\ProgramData".
        /// Linux: "/var/lib" (.NET returns "/usr/share", which is meant for read only data).
        /// </summary>
        /// <remarks>
        /// On Linux only root can create folders in "/var/lib", see <see cref="Folders"/> for how the SysWeaver folder is set up.
        /// </remarks>
        public static readonly String CommonApplicationData = OperatingSystem.IsLinux() ? "/var/lib" : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        /// <summary>
        /// Resolve a "path" template to a full path.
        /// Variables in the template starts with "$(" and ends with ")".
        /// Variables can be any value of the Environment.SpecialFolder enum, or any of the ones in the supplied dictionary.
        /// Formatting as described in the TextTemplate type can be used ("$(*Var)" is useful for paths).
        /// Ex:
        /// "$(LocalApplicationData)\MyAppsData\StateBlockUntil.json"
        /// Some common folders:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        /// </summary>
        /// <param name="template">The template, variables start with "$(" and ends with ")".</param>
        /// <param name="extra">Optional extra variables. If case insensitive is specified, the keys in this dictionary must be lower-cased (names are lower cased before the lookup)</param>
        /// <param name="caseInSensitive">If true the variable names is case in-sensitive</param>
        /// <param name="useEnv">If true, variables from EnvInfo.TextVars are available, examples:
        ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
        ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
        ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
        ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
        ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
        ///             $(AppGuid) = A guid derived from the application assembly name (stable between runs)
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
        /// </param>
        /// <returns>The resolved path (null or empty if <paramref name="template"/> is null or empty). The result is not made absolute (see <see cref="EnvInfo.MakeAbsoulte(string, bool)"/>).</returns>
        /// <remarks>
        /// Lookup order: special folders (<see cref="Environment.SpecialFolder"/> names), then <paramref name="extra"/>, then env info variables.
        /// Only defined special folder names are resolved as folders (numeric values are not).
        /// The special folder paths are read once per process (cached on first use), a folder that is changed (redirected) while the process runs is not seen.
        /// How unknown variables are handled is defined by <see cref="TextTemplate"/>.
        /// Parsed templates are cached per template string (thread safe, unbounded), the variables are evaluated on every call.
        /// </remarks>
        public static String Resolve(String template, IReadOnlyDictionary<String, String> extra = null, bool caseInSensitive = true, bool useEnv = true)
        {
            if (String.IsNullOrEmpty(template))
                return template;
            //  Cached per exact template text (only the variable names are case in-sensitive)
            var cache = caseInSensitive ? CachInSens : Cache;
            if (!cache.TryGetValue(template, out var t))
            {
                t = new TextTemplate(template, "$(", ")", caseInSensitive);
                cache[template] = t;
            }
            if ((extra != null) && (extra.Count <= 0))
                extra = null;
            var env = useEnv ? (caseInSensitive ? EnvInfo.TextVarsCaseInsensitive : EnvInfo.TextVars) : null;
            if ((extra != null) && useEnv)
            {
            //  extra and env
                if (caseInSensitive)
                {
                    return t.Get((Extra: extra, Env: env), static (s, key) =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        var lk = key.FastToLower();
                        if (s.Extra.TryGetValue(lk, out var val))
                            return val;
                        if (s.Env.TryGetValue(lk, out val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get((Extra: extra, Env: env), static (s, key) =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (s.Extra.TryGetValue(key, out var val))
                            return val;
                        if (s.Env.TryGetValue(key, out val))
                            return val;
                        return null;
                    });
                }

            }
            if (env != null)
            {
                //  env
                if (caseInSensitive)
                {
                    return t.Get(env, static (vars, key) =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        if (vars.TryGetValue(key.FastToLower(), out var val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(env, static (vars, key) =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (vars.TryGetValue(key, out var val))
                            return val;
                        return null;
                    });
                }
            }
            if (extra != null)
            {
                //  extra
                if (caseInSensitive)
                {
                    return t.Get(extra, static (vars, key) =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        if (vars.TryGetValue(key.FastToLower(), out var val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(extra, static (vars, key) =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (vars.TryGetValue(key, out var val))
                            return val;
                        return null;
                    });
                }
            }
        //  Only folders
            if (caseInSensitive)
            {
                return t.Get(key =>
                {
                    if (TryGetFolder(key, true, out var folder))
                        return folder;
                    return null;
                });
            }
            else
            {
                return t.Get(key =>
                {
                    if (TryGetFolder(key, false, out var folder))
                        return folder;
                    return null;
                });
            }
        }

        static bool TryGetFolder(String key, bool ignoreCase, out String folder)
        {
            folder = null;
            //  Only accept names (Enum.TryParse also accepts numbers and comma separated combinations)
            var k = key.AsSpan().TrimStart();
            if ((k.Length <= 0) || !Char.IsLetter(k[0]))
                return false;
            //  A single name without surrounding white space: Enum.TryParse only accepts the exact name (ordinal, or ordinal ignore case), a frozen lookup gives the same result.
            //  Else (white space to trim, comma separated combinations) the original parse
            var kl = key.Length;
            if ((key.IndexOf(',') < 0) && !Char.IsWhiteSpace(key[0]) && !Char.IsWhiteSpace(key[kl - 1]))
            {
                if (!(ignoreCase ? FoldersIgnoreCase : Folders).TryGetValue(key, out var f))
                    return false;
                folder = GetFolder(f);
                return true;
            }
            if (!Enum.TryParse<Environment.SpecialFolder>(key, ignoreCase, out var e))
                return false;
            if (!Enum.IsDefined(e))
                return false;
            folder = GetFolder(e);
            return true;
        }

        /// <summary>
        /// Get the path of a special folder (<see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>, <see cref="CommonApplicationData"/> for CommonApplicationData), cached for the process.
        /// </summary>
        /// <remarks>
        /// The path is read at most once per folder (lock free after that): a folder that is changed (redirected) after its first use is not seen.
        /// An empty path (a folder that doesn't exist on this OS) is cached too.
        /// </remarks>
        /// <param name="f">A defined special folder</param>
        /// <returns>The path of the folder, empty if the folder doesn't exist on this OS</returns>
        static String GetFolder(Environment.SpecialFolder f)
        {
            var i = (uint)(int)f;
            var c = FolderPaths;
            String p;
            if (i >= (uint)c.Length)
            {
                var s = SparseFolderPaths;
                if (s.TryGetValue(f, out p))
                    return p;
                lock (c)
                {
                    if (!s.TryGetValue(f, out p))
                    {
                        p = ReadFolder(f);
                        s[f] = p;
                    }
                }
                return p;
            }
            p = Volatile.Read(ref c[i]);
            if (p != null)
                return p;
            //  Only one thread reads a path (GetFolderPath runs at most once per folder)
            lock (c)
            {
                p = c[i];
                if (p == null)
                {
                    p = ReadFolder(f);
                    Volatile.Write(ref c[i], p);
                }
            }
            return p;
        }

        static String ReadFolder(Environment.SpecialFolder f)
            => f == Environment.SpecialFolder.CommonApplicationData ? CommonApplicationData : Environment.GetFolderPath(f);

        /// <summary>
        /// The cached special folder paths, indexed by the enum value (null = not read yet)
        /// </summary>
        static readonly String[] FolderPaths = new String[Enum.GetValues<Environment.SpecialFolder>().Max(x => Math.Max((int)x, 0)) + 1];

        /// <summary>
        /// Cached paths of special folders with a value outside <see cref="FolderPaths"/> (not used by the current .NET values)
        /// </summary>
        static readonly ConcurrentDictionary<Environment.SpecialFolder, String> SparseFolderPaths = new();

        /// <summary>
        /// All special folder names (including names that share a value, ex: Personal / MyDocuments), case sensitive (as Enum.TryParse)
        /// </summary>
        static readonly FrozenDictionary<String, Environment.SpecialFolder> Folders = Enum.GetNames<Environment.SpecialFolder>().ToFrozenDictionary(x => x, Enum.Parse<Environment.SpecialFolder>, StringComparer.Ordinal);

        /// <summary>
        /// All special folder names, case in-sensitive (as Enum.TryParse with ignoreCase)
        /// </summary>
        static readonly FrozenDictionary<String, Environment.SpecialFolder> FoldersIgnoreCase = Folders.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        static readonly ConcurrentDictionary<String, TextTemplate> Cache = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, TextTemplate> CachInSens = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);
    }

}
