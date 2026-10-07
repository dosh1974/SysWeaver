
using System;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Parameters describing the OS service, passed to <see cref="ServiceHost.Run"/>.
    /// </summary>
    public sealed class ServiceParams
    {
        /// <summary>
        /// Name of the service (id), if null or empty the application name is used
        /// </summary>
        public String Name;
        
        /// <summary>
        /// The display name of the service (visible in service managers etc), if null the <see cref="Name"/> is used
        /// </summary>
        public String DisplayName;
        
        /// <summary>
        /// Description of the service
        /// </summary>
        public String Description;

        /// <summary>
        /// Set to true if the process has to run elevated (the "daemon", "execute" and "debug" verbs will request elevation)
        /// </summary>
        public bool NeedToRunElevated;

        /// <summary>
        /// The default service start up mode (used when installing)
        /// </summary>
        public ServiceStarts Start = ServiceStarts.Normal;

        /// <summary>
        /// Try to restart service on failure (Windows only for now)
        /// </summary>
        public bool RestartOnFail = true;
        
        /// <summary>
        /// Number of seconds to wait before restarting on the first and second fails (Windows only)
        /// </summary>
        public int RestartDelaySeconds = 2 * 60;
        
        /// <summary>
        /// Number of seconds to wait before restarting on the third (and later) fail (Windows only), the default is 5 minutes
        /// </summary>
        public int RestartDelayLastSeconds = 5 * 60;
        
        /// <summary>
        /// Number of seconds without failures before resetting the failure counter (Windows only)
        /// </summary>
        public int ResetSeconds = 24 * 60 * 60;
        
        /// <summary>
        /// Optional packed ascii logo displayed in the console header, rendered using:
        /// AsciiTools.RenderColor(AsciiLogo, AsciiTools.ConsolePalette);
        /// </summary>
        public Byte[] AsciiLogo;

        /// <summary>
        /// If enabled:
        /// - If services are loaded from the manifest correctly, that manifest is saved (last working).
        /// - If an unhandled exception occurs while services are loading AND a last working manifest exists (that differs from the current), the current manifest file is replaced by the last working manifest file and the process restarted.
        /// The last working manifest is named "[Manifest].LastGood.json", the replaced one "[Manifest].Replace.json", backups are named "Bak_[Now]_[LastWrite].[Manifest].json".
        /// </summary>
        public bool AutoRecover = true;
    }

    /// <summary>
    /// How (and if) the OS should start an installed service.
    /// </summary>
    public enum ServiceStarts
    {
        /// <summary>
        /// Service may not be started
        /// </summary>
        Disabled,
        /// <summary>
        /// Service should not start when OS start, must be started manually
        /// </summary>
        Manual,
        /// <summary>
        /// Service should start when other services start
        /// </summary>
        Normal,
        /// <summary>
        /// Service should start after other services started
        /// </summary>
        Delayed,
    }

}
