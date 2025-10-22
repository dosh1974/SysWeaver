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
    /// Additional information about the runtime environment
    /// </summary>
    public static class EnvInfo
    {


        /// <summary>
        /// True if the process is running with a console window
        /// </summary>
        public static readonly bool HaveConsole = Console.LargestWindowWidth > 0;



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
        /// The processor architecture:
        /// "x86"
        /// "x64"
        /// "arm"
        /// "arm64"
        /// </summary>
        public static readonly String ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().FastToLower();


        static String GetOS()
        {
            foreach (var x in typeof(OSPlatform).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (x.PropertyType != typeof(OSPlatform))
                    continue;
                var val = (OSPlatform)x.GetValue(null);
                if (RuntimeInformation.IsOSPlatform(val))
                    return val.ToString().FastToLower();
            }
            return "unknown";
        }

        /// <summary>
        /// The operative system platform:
        /// "freebsd"
        /// "linux"
        /// "osx"
        /// "windows"
        /// </summary>
        public static readonly String OsPlatform = GetOS();


        /// <summary>
        /// Full path to the actual executable
        /// </summary>
        public static readonly String HostExecutable = GetHostExecutable();

        /// <summary>
        /// The executable command ("host.exe" file or "host.exe asm.dll")
        /// </summary>
        public static readonly String ExecCommand = GetExecCommand();

        /// <summary>
        /// The command line (including host)
        /// </summary>
        public static readonly String CommandLine = GetCommandLine();

        /// <summary>
        /// Full path to the executable
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
        /// ExecutableBase + "\runtimes\" + OsPlatform + "_" + ProcessorArchitecture.
        /// Ex:
        /// "D:\MyApp\runtimes\linux_x64"
        /// "D:\MyApp\runtimes\windows_arm64"
        /// </summary>
        public static readonly String NativePath = Path.Combine(ExecutableDir, "runtimes", String.Join('_', OsPlatform, ProcessorArchitecture));


        static readonly String ExeAppName = Path.GetFileNameWithoutExtension(Executable);
        static readonly String ExeAppDisplayName = StringTools.RemoveCamelCase(ExeAppName, ' ', true).Replace(".", "").Replace("  ", " ");
        static readonly String ExeAppDescription = "This is the " + ExeAppDisplayName + " application.";

        /// <summary>
        /// Application name
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
        /// Application display name
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
        /// Application description
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
        /// Application start time
        /// </summary>
        public static readonly DateTime AppStart = DateTime.UtcNow;

        /// <summary>
        /// Application CC tick
        /// </summary>
        public static readonly long Cc = AppStart.Ticks - new DateTime(2023, 11, 1).Ticks;

        /// <summary>
        /// "Unique" instance id as a string
        /// </summary>
        public static readonly String AppInstance = Cc.ToString("x");

        /// <summary>
        /// Assembly name, should stay the same independent of filename
        /// </summary>
        public static readonly String AppAssemblyName = Assembly.GetEntryAssembly()?.GetName()?.Name ?? AppName;

        /// <summary>
        /// A guid (as a string), based on AppAssemblyName
        /// </summary>
        public static readonly String AppGuid = CreateHashGuid(AppAssemblyName);

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
        ///             "AppStart" = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             "AppAssemblyName" = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             "AppGuid" = A unique guid for this appllication, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             "UserName" = The environment user name, ex: "John Doe".
        ///             "Is64BitProcess" = "True" if the process is running as a 64-bit process, else "False"
        ///             "OSVersion" = The version of the OS
        ///             "Platform" = The platform, ex "WinNT", "Unix".
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
        ///             "appstart" = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             "appassemblyname" = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             "appguid" = A unique guid for this appllication, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             "username" = The environment user name, ex: "John Doe".
        ///             "is64bitprocess" = "True" if the process is running as a 64-bit process, else "False"
        ///             "osversion" = The version of the OS
        ///             "platform" = The platform, ex "WinNT", "Unix".
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
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(AppAssemblyName) = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             $(AppGuid) = A unique guid for this appllication, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             $(UserName) = The environment user name, ex: "John Doe".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False".
        ///             $(OSVersion) = The version of the OS.
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>
        /// <param name="template">The template, variables start with "$(" and ends with ")".
        ///             $(AppName) = Application name.
        ///             $(AppDisplayName) = Application display name.
        ///             $(AppDescription) = Application description name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(AppAssemblyName) = Application assembly name (typically the exe name), "ExchangeRateService".
        ///             $(AppGuid) = A unique guid for this appllication, ex: "{CFBEDD92-341E-4EDB-96EB-C8305974EE29}".
        ///             $(UserName) = The environment user name, ex: "John Doe".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False".
        ///             $(OSVersion) = The version of the OS.
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </param>
        /// <param name="caseInSensitive">If true the variable names is case in-sensitive</param>
        /// <param name="extra">Optional extra variables</param>
        /// <returns>The resolved text</returns>
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
        /// If the path already is absolute, nothing will be changed.
        /// </summary>
        /// <param name="path">Relative or absolue path</param>
        /// <param name="useCurrentDir">Make relative to the current path instead of the executable path</param>
        /// <returns>An absolute path</returns>
        public static String MakeAbsoulte(String path, bool useCurrentDir = false)
        {
            if (path == null)
                return path;
            if (path.IndexOf("://") >= 0)
                return path;
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
            new Stats(StatsSystem , nameof(Environment.OSVersion), Environment.OSVersion.ToString(), "The operation system"),
            new Stats(StatsSystem , nameof(Environment.OSVersion.Platform), Environment.OSVersion.Platform.ToString(), "The OS platform"),
            new Stats(StatsSystem , nameof(ProcessorArchitecture), ProcessorArchitecture, "The CPU in the machine"),
            new Stats(StatsSystem , nameof(OsPlatform), OsPlatform, "The general OS type"),
        ];

        public static IEnumerable<Stats> GetStats()
        {
            foreach (var x in StaticStats)
                yield return x;
            yield return new Stats(StatsSystem, nameof(AppName), AppName, "The name of the application");
            yield return new Stats(StatsSystem, nameof(AppDisplayName), AppDisplayName, "The display name of the application");
            yield return new Stats(StatsSystem, nameof(Environment.WorkingSet), Environment.WorkingSet, "Amount of memory mapped to the physical process", TableDataByteSizeAttribute.Instance);
        }



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

        public static readonly string RumtimeID = $"{OSIdentifier}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        public static readonly string RuntimeFolder = Path.Combine(ExecutableDir, "runtimes", RumtimeID);
        public static readonly string RuntimeFolderNative = Path.Combine(RuntimeFolder, "native");
        public static readonly string RuntimeFolderLib = Path.Combine(RuntimeFolder, "lib");


        /// <summary>
        /// Seed to use for generating data (app specific)
        /// </summary>
        public static int AppSeed { get; internal set; }

        /// <summary>
        /// The default language to use, system should try to localize according to this.
        /// The two letter ISO 639-1 language code of the language, ex: "en", "es", "de".
        /// Can optionally have an ISO 3166 Alpha 2 country code appended, ex: "en-GB", "en-US", "es-MX", "es-ES".
        /// </summary>
        public static string AppLanguage { get; internal set; } = "en-US";

    }




    public sealed class AppInfoParams
    {
        /// <summary>
        /// The name of the application (can be used for file names etc)
        /// </summary>
        public String AppName;

        /// <summary>
        /// The display name of the application (can be used for texts etc, displayed to end users)
        /// </summary>
        public String AppDisplayName;

        /// <summary>
        /// The description of the application
        /// </summary>
        public String AppDescription;

        /// <summary>
        /// A seed (changes the automatically generated logo)
        /// </summary>
        public int AppSeed;

        /// <summary>
        /// The default language to use, system should try to localize according to this.
        /// The two letter ISO 639-1 language code of the language, ex: "en", "es", "de".
        /// Can optionally have an ISO 3166 Alpha 2 country code appended, ex: "en-GB", "en-US", "es-MX", "es-ES".
        /// </summary>
        public String AppLanguage = "en-US";

    }

    /// <summary>
    /// Service used to change the application information such as display name etc
    /// </summary>
    public sealed class AppInfo
    {
        public AppInfo(IMessageHost m, AppInfoParams p = null)
        {
            if (p == null)
            {
                m?.AddMessage("No app info parameters supplied!", MessageLevels.Warning);
                return;
            }
            var name = p.AppName;
            var dispName = p.AppDisplayName;
            var desc = p.AppDescription;
            var lang = p.AppLanguage;
            if (name != null)
            {
                if (name != EnvInfo.AppName)
                {
                    m?.AddMessage(String.Concat(nameof(EnvInfo), '.', nameof(EnvInfo.AppName), " changed to ", name.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppName = name;
                    dispName = dispName ?? StringTools.RemoveCamelCase(name, ' ', true);
                }
            }
            if (dispName != null)
            {
                if (dispName != EnvInfo.AppDisplayName)
                {
                    m?.AddMessage(String.Concat(nameof(EnvInfo), '.', nameof(EnvInfo.AppDisplayName), " changed to ", dispName.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppDisplayName = dispName;
                }
            }
            if (desc != null)
            {
                if (desc != EnvInfo.AppDescription)
                {
                    m?.AddMessage(String.Concat(nameof(EnvInfo), '.', nameof(EnvInfo.AppDescription), " changed to ", desc.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppDescription = desc;
                }
            }
            if (lang != null)
            {
                if (lang != EnvInfo.AppLanguage)
                {
                    m?.AddMessage(String.Concat(nameof(EnvInfo), '.', nameof(EnvInfo.AppLanguage), " changed to ", lang.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppLanguage = lang;
                }
            }
            EnvInfo.AppSeed = p.AppSeed;
        }

        public override string ToString() => String.Concat(
            "Name: ", EnvInfo.AppName
            , ", Display name: ", EnvInfo.AppDisplayName
            , ", Seed: ", EnvInfo.AppSeed
            , ", Description: ", EnvInfo.AppDescription
            );
    }


}
