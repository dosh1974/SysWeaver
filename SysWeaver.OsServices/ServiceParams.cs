
using System;

namespace SysWeaver.OsServices
{
    public sealed class ServiceParams
    {
        /// <summary>
        /// Name of the service (id)
        /// </summary>
        public String Name;
        
        /// <summary>
        /// The display name of the service (visible in service managers etc)
        /// </summary>
        public String DisplayName;
        
        /// <summary>
        /// Description of the service
        /// </summary>
        public String Description;

        /// <summary>
        /// Set to true if the process has to run elevated
        /// </summary>
        public bool NeedToRunElevated;

        /// <summary>
        /// The default service start up mode
        /// </summary>
        public ServiceStarts Start = ServiceStarts.Normal;

        /// <summary>
        /// Try to restart service on failure (Windows only for now)
        /// </summary>
        public bool RestartOnFail = true;
        
        /// <summary>
        /// Number of seconds to wait on first and second fails
        /// </summary>
        public int RestartDelaySeconds = 2 * 60;
        
        /// <summary>
        /// Number of seconds to wait on the third fail
        /// </summary>
        public int RestartDelayLastSeconds = 5 * 50;
        
        /// <summary>
        /// Number of seconds before resetting the failure counter
        /// </summary>
        public int ResetSeconds = 24 * 60 * 60;
        
        /// <summary>
        /// Packed ascii logo, rendered using:
        /// AsciiTools.RenderColor(AsciiLogo, AsciiTools.ConsolePalette);
        /// </summary>
        public Byte[] AsciiLogo;

        /// <summary>
        /// If enabled:
        /// - If services are loaded from the manifest correctly, that manifest is saved (last working).
        /// - If services fails to load AND a last working manifest exists, the current manifest file is replaced by the last working manifest file and the process restarted.
        /// </summary>
        public bool AutoRecover = true;
    }

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
