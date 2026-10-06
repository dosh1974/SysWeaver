using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
        /// How unknown variables are handled is defined by <see cref="TextTemplate"/>.
        /// Parsed templates are cached per template string (thread safe, unbounded), the variables are evaluated on every call.
        /// </remarks>
        public static String Resolve(String template, IReadOnlyDictionary<String, String> extra = null, bool caseInSensitive = true, bool useEnv = true)
        {
            if (String.IsNullOrEmpty(template))
                return template;
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
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        var lk = key.FastToLower();
                        if (extra.TryGetValue(lk, out var val))
                            return val;
                        if (env.TryGetValue(lk, out val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (extra.TryGetValue(key, out var val))
                            return val;
                        if (env.TryGetValue(key, out val))
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
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        if (env.TryGetValue(key.FastToLower(), out var val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (env.TryGetValue(key, out var val))
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
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, true, out var folder))
                            return folder;
                        if (extra.TryGetValue(key.FastToLower(), out var val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(key =>
                    {
                        if (TryGetFolder(key, false, out var folder))
                            return folder;
                        if (extra.TryGetValue(key, out var val))
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
            if (!Enum.TryParse<Environment.SpecialFolder>(key, ignoreCase, out var e))
                return false;
            if (!Enum.IsDefined(e))
                return false;
            folder = Environment.GetFolderPath(e);
            return true;
        }

        static readonly ConcurrentDictionary<String, TextTemplate> Cache = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, TextTemplate> CachInSens = new ConcurrentDictionary<String, TextTemplate>(StringComparer.OrdinalIgnoreCase);
    }

}
