using SysWeaver.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{

    /// <summary>
    /// Additional information about the runtime environment: executable paths, OS / platform, application name and description,
    /// and text variables that can be used in templates (ex: "$(AppName)").
    /// </summary>
    /// <remarks>
    /// Most values are computed once when the class is first accessed.
    /// <see cref="AppName"/>, <see cref="AppDisplayName"/>, <see cref="AppDescription"/>, <see cref="AppSeed"/> and <see cref="AppLanguage"/>
    /// can only be changed internally, typically by <see cref="AppInfo"/> at startup.
    /// </remarks>
    public static class EnvInfo
    {

        static String GetHostExecutable()
        {
            var t = Process.GetCurrentProcess()?.MainModule?.FileName;
            if (!String.IsNullOrEmpty(t))
                return t;
            return Environment.GetCommandLineArgs()[0];
        }

        static String GetExecutable()
        {
            var t = Assembly.GetEntryAssembly()?.Location;
            if (!String.IsNullOrEmpty(t))
                return t;
            return GetHostExecutable();
        }

        static String GetExecCommand()
        {
            var ec = Environment.CommandLine;
            var t = SystemHelper.GetCommandAndArgs(out var args, ec);
            var e = HostExecutable;
            if (!String.Equals(t, e, StringComparison.OrdinalIgnoreCase))
            {
                var tt = Path.Combine(Path.GetDirectoryName(t), Path.GetFileNameWithoutExtension(t) + Path.GetExtension(e));
                if (!String.Equals(tt, e, StringComparison.OrdinalIgnoreCase))
                {
                    if (e.Contains(' '))
                        e = e.ToQuoted();
                    if (t.Contains(' '))
                        t = t.ToQuoted();
                    return String.Join(' ', e, t);
                }
            }
            if (e.Contains(' '))
                e = e.ToQuoted();
            return e;
        }

        static String GetCommandLine()
        {
            var ec = Environment.CommandLine;
            var t = SystemHelper.GetCommandAndArgs(out var args, ec);
            var e = HostExecutable;
            if (String.Equals(t, e, StringComparison.OrdinalIgnoreCase))
                return ec;
            t = Path.Combine(Path.GetDirectoryName(t), Path.GetFileNameWithoutExtension(t) + Path.GetExtension(e));
            if (String.Equals(t, e, StringComparison.OrdinalIgnoreCase))
            {
                if (e.Contains(' '))
                    e = e.ToQuoted();
                if (String.IsNullOrEmpty(args))
                    return e;
                return String.Join(' ', e, args);
            }
            if (e.Contains(' '))
                e = e.ToQuoted();
            return String.Join(' ', e, ec);
        }



        /// <summary>
        /// The processor architecture of the current process (lower cased <see cref="RuntimeInformation.ProcessArchitecture"/>), ex:
        /// "x86"
        /// "x64"
        /// "arm"
        /// "arm64"
        /// </summary>
        public static readonly String ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().FastToLower();


        static String GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return "freebsd";
            return "unknown";
        }

        /// <summary>
        /// The operative system platform:
        /// "freebsd"
        /// "linux"
        /// "osx"
        /// "windows"
        /// "unknown"
        /// </summary>
        public static readonly String OsPlatform = GetOS();

        /// <summary>
        /// The friendly OS name, as reported by <see cref="PlatformTools.Current"/>
        /// </summary>
        public static String OsName => PlatformTools.Current.OsFriendlyName;

        /// <summary>
        /// Version string of OS, ex: "Microsoft Windows NT 10.0.19045.0"
        /// </summary>
        public static readonly String OsVersion = Environment.OSVersion.ToString();

        /// <summary>
        /// Full path to the actual (host) executable of the process, ex: "C:\Program Files\dotnet\dotnet.exe" when started using "dotnet MyApp.dll"
        /// </summary>
        public static readonly String HostExecutable = GetHostExecutable();

        /// <summary>
        /// The executable command, i.e. what to run to start this application again ("host.exe" or "host.exe asm.dll"), paths containing spaces are quoted
        /// </summary>
        public static readonly String ExecCommand = GetExecCommand();

        /// <summary>
        /// The command line (including host and arguments)
        /// </summary>
        public static readonly String CommandLine = GetCommandLine();

        /// <summary>
        /// Full path to the executable (the entry assembly location, or the host executable if not available, ex: single file apps)
        /// </summary>
        public static readonly String Executable = GetExecutable();

        /// <summary>
        /// Full path to the folder where the application is
        /// </summary>
        public static readonly String ExecutableDir = Path.GetDirectoryName(Executable);

        /// <summary>
        /// Full path to the executable without the extension (append for log's etc)
        /// </summary>
        public static readonly String ExecutableBase = Path.Combine(ExecutableDir, Path.GetFileNameWithoutExtension(Executable));

        /// <summary>
        /// The "folder" to use when loading native dependencies.
        /// ExecutableDir + "\runtimes\" + OsPlatform + "_" + ProcessorArchitecture.
        /// Note: this differs from the .NET runtime identifier layout used by <see cref="RuntimeFolder"/>.
        /// Ex:
        /// "D:\MyApp\runtimes\linux_x64"
        /// "D:\MyApp\runtimes\windows_arm64"
        /// </summary>
        public static readonly String NativePath = Path.Combine(ExecutableDir, "runtimes", String.Join('_', OsPlatform, ProcessorArchitecture));


        static readonly String ExeAppName = Path.GetFileNameWithoutExtension(Executable);
        static readonly String ExeAppDisplayName = StringTools.RemoveCamelCase(ExeAppName, ' ', true).Replace(".", "").Replace("  ", " ");
        static readonly String ExeAppDescription = "This is the " + ExeAppDisplayName + " application.";

        /// <summary>
        /// Application name, defaults to the executable name without extension (can be changed using <see cref="AppInfoParams.AppName"/>).
        /// Setting null or empty restores the default.
        /// </summary>
        public static String AppName
        {
            get => InternalAppName;
            internal set
            {
                InternalAppName = String.IsNullOrEmpty(value) ? ExeAppName : value;
                InternalTextVarsCaseInsensitive = null;
                InternalTextVars = null;
            }
        }
        static String InternalAppName = ExeAppName;

        /// <summary>
        /// Application display name, defaults to the executable name with camel case split into words (can be changed using <see cref="AppInfoParams.AppDisplayName"/>).
        /// Setting null or empty restores the default.
        /// </summary>
        public static String AppDisplayName
        {
            get => InternalAppDisplayName;
            internal set
            {
                InternalAppDisplayName = String.IsNullOrEmpty(value) ? ExeAppDisplayName : value;
                InternalTextVarsCaseInsensitive = null;
                InternalTextVars = null;
            }
        }
        static String InternalAppDisplayName = ExeAppDisplayName;

        /// <summary>
        /// Application description, defaults to "This is the [display name] application." (can be changed using <see cref="AppInfoParams.AppDescription"/>).
        /// Setting null or empty restores the default.
        /// </summary>
        public static String AppDescription
        {
            get => InternalAppDescription;
            internal set
            {
                InternalAppDescription = String.IsNullOrEmpty(value) ? ExeAppDescription : value;
                InternalTextVarsCaseInsensitive = null;
                InternalTextVars = null;
            }
        }
        static String InternalAppDescription = ExeAppDescription;

        /// <summary>
        /// The current process Id
        /// </summary>
        public static readonly int ProcessId = Process.GetCurrentProcess().Id;


        /// <summary>
        /// Application start time (UTC), actually the time when this class was first accessed
        /// </summary>
        public static readonly DateTime AppStart = DateTime.UtcNow;

        /// <summary>
        /// The number of ticks (100 ns) between 2023-11-01 and <see cref="AppStart"/>, used as a "unique" instance number
        /// </summary>
        public static readonly long Cc = AppStart.Ticks - new DateTime(2023, 11, 1).Ticks;

        /// <summary>
        /// "Unique" instance id as a string (<see cref="Cc"/> in hex), changes every time the application is started
        /// </summary>
        public static readonly String AppInstance = Cc.ToString("x");

        /// <summary>
        /// Assembly name of the entry assembly, should stay the same independent of filename (falls back to <see cref="AppName"/>)
        /// </summary>
        public static readonly String AppAssemblyName = Assembly.GetEntryAssembly()?.GetName()?.Name ?? AppName;

        /// <summary>
        /// A guid (as a string, "{...}" format), based on AppAssemblyName, stable between runs and machines
        /// </summary>
        public static readonly String AppGuid = CreateHashGuid(AppAssemblyName);

        /// <summary>
        /// Create a deterministic guid from a text (MD5 hash of the UTF-16 bytes), not intended for security purposes.
        /// </summary>
        /// <param name="text">The text to hash (may not be null).</param>
        /// <returns>The guid in the "B" format, ex: "{cfbedd92-341e-4edb-96eb-c8305974ee29}".</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        public static String CreateHashGuid(String text)
        {
            var hash = MD5.HashData(Encoding.Unicode.GetBytes(text));
            return new Guid(hash).ToString("B");
        }


        /// <summary>
        /// Contains text variables, ex:
        ///             "AppName" = Application name.
        ///             "AppDisplayName" = Application display name.
        ///             "AppDescription" = Application description name.
        ///             "AppStart" = Application start time (UTC) formatted using "yyyy-MM-hh hh:mm:ss" (note: month followed by a 12-hour clock hour, not the day).
        ///             "AppAssemblyName" = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             "AppGuid" = A unique guid for this application, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             "UserName" = The environment user name, ex: "John Doe".
        ///             "Is64BitProcess" = "True" if the process is running as a 64-bit process, else "False".
        ///             "OSVersion" = The version of the OS.
        ///             "Platform" = The platform, ex "WinNT", "Unix".
        ///             "MachineName" = The computer name.
        ///             "MachineNameCased" = The computer name, lower cased with the first letter upper cased.
        ///             "ExeAppName", "Executable", "ExecutableDir", "ExecutableBase" = See the members with the same names.
        ///             "KeyFolder" = The folder where keys are stored (<see cref="Folders.KeyFolder"/>).
        /// Keys are case sensitive. The dictionary is rebuilt (lazily) when the app name, display name or description changes.
        /// </summary>
        public static IReadOnlyDictionary<String, String> TextVars
        {
            get
            {
                var v = InternalTextVars;
                if (v != null)
                    return v;
                v = new Dictionary<String, String>(StringComparer.Ordinal)
                {
                    { nameof(AppName), AppName },
                    { nameof(AppDisplayName), AppDisplayName },
                    { nameof(AppDescription), AppDescription },
                    { nameof(AppStart), AppStart.ToString("yyyy-MM-hh hh:mm:ss") },
                    { nameof(AppAssemblyName), AppAssemblyName },
                    { nameof(AppGuid), AppGuid },
                    { nameof(Environment.UserName), Environment.UserName },
                    { nameof(Environment.Is64BitProcess), Environment.Is64BitProcess.ToString() },
                    { nameof(Environment.OSVersion), Environment.OSVersion.ToString() },
                    { nameof(Environment.OSVersion.Platform), Environment.OSVersion.Platform.ToString() },
                    { nameof(Environment.MachineName), Environment.MachineName },
                    { (nameof(Environment.MachineName) + "Cased"), Environment.MachineName.FastToLower().MakeFirstUppercase() },
                    { nameof(ExeAppName), ExeAppName },
                    { nameof(Executable), Executable },
                    { nameof(ExecutableDir), ExecutableDir },
                    { nameof(ExecutableBase), ExecutableBase },
                    { nameof(Folders.KeyFolder), Folders.KeyFolder},
                }.Freeze();
                InternalTextVars = v;
                return v;
            }
        }

        static IReadOnlyDictionary<String, String> InternalTextVars;


        /// <summary>
        /// Contains text variables in a case insensitive dictionary where all keys are lowercased, ex:
        ///             "appname" = Application name.
        ///             "appdisplayname" = Application display name.
        ///             "appdescription" = Application description name.
        ///             "appstart" = Application start time (UTC) formatted using "yyyy-MM-hh hh:mm:ss" (note: month followed by a 12-hour clock hour, not the day).
        ///             "appassemblyname" = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             "appguid" = A unique guid for this application, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             "username" = The environment user name, ex: "John Doe".
        ///             "is64bitprocess" = "True" if the process is running as a 64-bit process, else "False".
        ///             "osversion" = The version of the OS.
        ///             "platform" = The platform, ex "WinNT", "Unix".
        ///             "machinename" = The computer name.
        ///             "keyfolder" = The folder where keys are stored.
        ///             "machinenamecased", "exeappname", "executable", "executabledir", "executablebase" = See <see cref="TextVars"/>.
        /// The dictionary uses a case insensitive comparer, so any casing of the keys can be used for lookups.
        /// </summary>
        public static IReadOnlyDictionary<String, String> TextVarsCaseInsensitive
        {
            get
            {
                var v = InternalTextVarsCaseInsensitive;
                if (v != null)
                    return v;
                v = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
                {
                    { nameof(AppName).FastToLower(), AppName },
                    { nameof(AppDisplayName).FastToLower(), AppDisplayName },
                    { nameof(AppDescription).FastToLower(), AppDescription },
                    { nameof(AppStart).FastToLower(), AppStart.ToString("yyyy-MM-hh hh:mm:ss") },
                    { nameof(AppAssemblyName).FastToLower(), AppAssemblyName },
                    { nameof(AppGuid).FastToLower(), AppGuid },
                    { nameof(Environment.UserName).FastToLower(), Environment.UserName },
                    { nameof(Environment.Is64BitProcess).FastToLower(), Environment.Is64BitProcess.ToString() },
                    { nameof(Environment.OSVersion).FastToLower(), Environment.OSVersion.ToString() },
                    { nameof(Environment.OSVersion.Platform).FastToLower(), Environment.OSVersion.Platform.ToString() },
                    { nameof(Environment.MachineName).FastToLower(), Environment.MachineName },
                    { (nameof(Environment.MachineName) + "Cased").FastToLower(), Environment.MachineName.FastToLower().MakeFirstUppercase() },
                    { nameof(ExeAppName).FastToLower(), ExeAppName },
                    { nameof(Executable).FastToLower(), Executable },
                    { nameof(ExecutableDir).FastToLower(), ExecutableDir },
                    { nameof(ExecutableBase).FastToLower(), ExecutableBase },
                    { nameof(Folders.KeyFolder).FastToLower(), Folders.KeyFolder},

                }.Freeze();
                InternalTextVarsCaseInsensitive = v;
                return v;
            }
        }

        static IReadOnlyDictionary<String, String> InternalTextVarsCaseInsensitive;


        /// <summary>
        /// Resolve a text template that may contain env info variables, ex: "$(AppName)".
        ///             $(AppName) = Application name.
        ///             $(AppDisplayName) = Application display name.
        ///             $(AppDescription) = Application description name.
        ///             $(AppStart) = Application start time (UTC) formatted using "yyyy-MM-hh hh:mm:ss" (note: month followed by a 12-hour clock hour, not the day).
        ///             $(AppAssemblyName) = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             $(AppGuid) = A unique guid for this application, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             $(UserName) = The environment user name, ex: "John Doe".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False".
        ///             $(OSVersion) = The version of the OS.
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        ///             $(MachineName) = The computer name.
        ///             $(KeyFolder) = The folder where keys are stored.
        /// </summary>
        /// <param name="template">The template, variables start with "$(" and ends with ")".
        ///             $(AppName) = Application name.
        ///             $(AppDisplayName) = Application display name.
        ///             $(AppDescription) = Application description name.
        ///             $(AppStart) = Application start time (UTC) formatted using "yyyy-MM-hh hh:mm:ss" (note: month followed by a 12-hour clock hour, not the day).
        ///             $(AppAssemblyName) = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             $(AppGuid) = A unique guid for this application, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             $(UserName) = The environment user name, ex: "John Doe".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False".
        ///             $(OSVersion) = The version of the OS.
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        ///             $(MachineName) = The computer name.
        ///             $(KeyFolder) = The folder where keys are stored.
        /// </param>
        /// <param name="caseInSensitive">If true the variable names is case in-sensitive</param>
        /// <param name="extra">Optional extra variables, used for names not found in <see cref="TextVars"/> (env info variables take precedence).
        /// When case insensitive, the lookup in <paramref name="extra"/> uses its own comparer.</param>
        /// <returns>The resolved text (null or empty if <paramref name="template"/> is null or empty)</returns>
        /// <remarks>
        /// Thread safe. When no <paramref name="extra"/> variables are given, the resolved result is cached per template (forever),
        /// so a later change of <see cref="AppName"/>, <see cref="AppDisplayName"/> or <see cref="AppDescription"/> is not reflected for templates resolved before the change.
        /// Caches are unbounded, so don't use with an unbounded set of templates.
        /// </remarks>
        public static String ResolveText(String template, bool caseInSensitive = true, IReadOnlyDictionary<String, String> extra = null)
        {
            if (String.IsNullOrEmpty(template))
                return template;
            if ((extra == null) || (extra.Count <= 0))
            {
                var cache = caseInSensitive ? StaticCachInSens : StaticCache;
                if (cache.TryGetValue(template, out var t))
                    return t;
                var x = caseInSensitive ? TextVarsCaseInsensitive : TextVars;
                t = new TextTemplate(template, "$(", ")", caseInSensitive, false).Get(x);
                cache[template] = t;
                return t;
            }
            else
            {
                var cache = caseInSensitive ? CachInSens : Cache;
                if (!cache.TryGetValue(template, out var t))
                {
                    t = new TextTemplate(template, "$(", ")", caseInSensitive, false);
                    cache[template] = t;
                }
                var x = caseInSensitive ? TextVarsCaseInsensitive : TextVars;
                return t.Get(v =>
                {
                    if (x.TryGetValue(v, out var value))
                        return value;
                    extra.TryGetValue(v, out value);
                    return value;
                });
            }
        }

        static readonly ConcurrentDictionary<String, String> StaticCache = new ConcurrentDictionary<String, String>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, String> StaticCachInSens = new ConcurrentDictionary<String, String>(StringComparer.Ordinal);


        static readonly ConcurrentDictionary<String, TextTemplate> Cache = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<String, TextTemplate> CachInSens = new ConcurrentDictionary<String, TextTemplate>(StringComparer.Ordinal);


        /// <summary>
        /// Make an absolute path from a relative path (not using current directory, but rather the executable directory).
        /// If the path already is absolute (rooted), or is an url (contains "://"), nothing will be changed (except for directory separators).
        /// </summary>
        /// <param name="path">Relative or absolute path (alt directory separators are replaced with the platform directory separator)</param>
        /// <param name="useCurrentDir">Make relative to the current path instead of the executable path</param>
        /// <returns>An absolute path, or null if <paramref name="path"/> is null</returns>
        /// <remarks>The result is not normalized, ".." segments are kept as is (no protection against path traversal).</remarks>
        public static String MakeAbsoulte(String path, bool useCurrentDir = false)
        {
            if (path == null)
                return path;
            if (path.FastIndexOf("://") >= 0)
                return path;
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(path))
                return path;
            return Path.Combine(useCurrentDir ? Environment.CurrentDirectory : ExecutableDir, path);
        }



        const String StatsSystem = nameof(EnvInfo);
        static readonly Stats[] StaticStats =
        [
            new Stats(StatsSystem , nameof(Executable), Path.GetFileName(Executable), "Name of the executable " + Executable.ToQuoted()),
            new Stats(StatsSystem , nameof(AppStart), AppStart, "When the application (process) started executing"),
            new Stats(StatsSystem , nameof(Environment.Is64BitProcess), Environment.Is64BitProcess, "True if the process runs as a 64-bit process"),
            new Stats(StatsSystem , nameof(Environment.MachineName), Environment.MachineName, "The computer name"),
            new Stats(StatsSystem , nameof(OsVersion), OsVersion, "The operation system"),
            new Stats(StatsSystem , nameof(Environment.OSVersion.Platform), Environment.OSVersion.Platform.ToString(), "The OS platform"),
            new Stats(StatsSystem , nameof(ProcessorArchitecture), ProcessorArchitecture, "The CPU in the machine"),
            new Stats(StatsSystem , nameof(OsPlatform), OsPlatform, "The general OS type"),
        ];

        /// <summary>
        /// Get statistics about the environment (executable, OS, machine, app name and current working set).
        /// </summary>
        /// <returns>The statistics.</returns>
        public static IEnumerable<Stats> GetStats()
        {
            foreach (var x in StaticStats)
                yield return x;
            yield return new Stats(StatsSystem, nameof(AppName), AppName, "The name of the application");
            yield return new Stats(StatsSystem, nameof(AppDisplayName), AppDisplayName, "The display name of the application");
            yield return new Stats(StatsSystem, nameof(Environment.WorkingSet), Environment.WorkingSet, "Amount of memory mapped to the physical process", TableDataByteSizeAttribute.Instance);
        }



        /// <summary>
        /// Get the two letter ISO 3166 region code of the current culture, ex: "US" (cached culture data is cleared first so OS changes are picked up).
        /// </summary>
        /// <returns>The two letter region code.</returns>
        public static String GetCurrentRegion()
        {
            CultureInfo.CurrentCulture.ClearCachedData();
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }


        static string OSIdentifier
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
                return "unknown";
            }
        }

        /// <summary>
        /// The .NET runtime identifier (RID) style name of the current platform, ex: "win-x64", "linux-arm64" (FreeBSD reports "unknown-..").
        /// </summary>
        public static readonly string RumtimeID = $"{OSIdentifier}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        /// <summary>
        /// The RID specific runtime folder, ExecutableDir + "/runtimes/" + <see cref="RumtimeID"/>, ex: "C:\MyApp\runtimes\win-x64".
        /// </summary>
        public static readonly string RuntimeFolder = Path.Combine(ExecutableDir, "runtimes", RumtimeID);
        /// <summary>
        /// The folder for native libraries, <see cref="RuntimeFolder"/> + "/native".
        /// </summary>
        public static readonly string RuntimeFolderNative = Path.Combine(RuntimeFolder, "native");
        /// <summary>
        /// The folder for RID specific managed libraries, <see cref="RuntimeFolder"/> + "/lib".
        /// </summary>
        public static readonly string RuntimeFolderLib = Path.Combine(RuntimeFolder, "lib");


        /// <summary>
        /// Seed to use for generating data (app specific, ex: the generated logo), set from <see cref="AppInfoParams.AppSeed"/>
        /// </summary>
        public static int AppSeed { get; internal set; }

        /// <summary>
        /// The default language to use, system should try to localize according to this.
        /// The two letter ISO 639-1 language code of the language, ex: "en", "es", "de".
        /// Can optionally have an ISO 3166 Alpha 2 country code appended, ex: "en-GB", "en-US", "es-MX", "es-ES".
        /// </summary>
        public static string AppLanguage { get; internal set; } = "en-US";

    }


}
