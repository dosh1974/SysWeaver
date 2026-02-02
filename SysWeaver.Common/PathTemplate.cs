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
    ///             $(AppGuid) = A "unique" id for this process
    ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
    ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
    ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
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
        /// <param name="extra">Optional extra variables. If case insesitive is specified, all keys will be lower-cased</param>
        /// <param name="caseInSensitive">If true the variable names is case in-sensitive</param>
        /// <param name="useEnv">Variable from EnvInfo.TextVars is available, examples:
        ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
        ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
        ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
        ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
        ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
        ///             $(AppGuid) = A "unique" id for this process
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
        /// </param>
        /// <returns>The resolved path</returns>
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
                        if (Enum.TryParse<Environment.SpecialFolder>(key, true, out var e))
                            return Environment.GetFolderPath(e);
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
                        if (Enum.TryParse<Environment.SpecialFolder>(key, false, out var e))
                            return Environment.GetFolderPath(e);
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
                        if (Enum.TryParse<Environment.SpecialFolder>(key, true, out var e))
                            return Environment.GetFolderPath(e);
                        if (env.TryGetValue(key.FastToLower(), out var val))
                            return val;
                        return null;
                    });
                }
                else
                {
                    return t.Get(key =>
                    {
                        if (Enum.TryParse<Environment.SpecialFolder>(key, false, out var e))
                            return Environment.GetFolderPath(e);
                        if (env.TryGetValue(key, out var val))
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
                    if (Enum.TryParse<Environment.SpecialFolder>(key, true, out var e))
                        return Environment.GetFolderPath(e);
                    return null;
                });
            }
            else
            {
                return t.Get(key =>
                {
                    if (Enum.TryParse<Environment.SpecialFolder>(key, false, out var e))
                        return Environment.GetFolderPath(e);
                    return null;
                });
            }
        }

        static readonly ConcurrentDictionary<String, TextTemplate> Cache = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, TextTemplate> CachInSens = new ConcurrentDictionary<String, TextTemplate>(StringComparer.OrdinalIgnoreCase);
    }

}
