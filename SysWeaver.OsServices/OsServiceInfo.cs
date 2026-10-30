using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// How (or if) an OS service is started.
    /// </summary>
    public enum OsServiceStartModes
    {
        /// <summary>
        /// The start mode is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// A device driver started by the boot loader (Windows).
        /// </summary>
        Boot,
        /// <summary>
        /// A device driver started during kernel initialization (Windows).
        /// </summary>
        System,
        /// <summary>
        /// Started automatically when the system starts (Windows "Automatic", systemd "enabled").
        /// </summary>
        Automatic,
        /// <summary>
        /// Started automatically shortly after the system has started (Windows only).
        /// </summary>
        AutomaticDelayed,
        /// <summary>
        /// Only started on demand (Windows "Manual", systemd "disabled").
        /// </summary>
        Manual,
        /// <summary>
        /// Can't be started (Windows "Disabled", systemd "masked").
        /// </summary>
        Disabled,
        /// <summary>
        /// Can't be enabled or disabled, started by other units (systemd "static", "indirect", "generated" etc).
        /// </summary>
        Static,
    }

    /// <summary>
    /// The actions that can be performed on an OS service.
    /// </summary>
    [Flags]
    public enum OsServiceActions
    {
        /// <summary>
        /// No action is possible.
        /// </summary>
        None = 0,
        /// <summary>
        /// The service can be started.
        /// </summary>
        Start = 1,
        /// <summary>
        /// The service can be stopped.
        /// </summary>
        Stop = 2,
        /// <summary>
        /// The service can be restarted.
        /// </summary>
        Restart = 4,
        /// <summary>
        /// The service can be paused (Windows: pause, systemd: freeze).
        /// </summary>
        Pause = 8,
        /// <summary>
        /// The service can be resumed (Windows: continue, systemd: thaw).
        /// </summary>
        Continue = 16,
        /// <summary>
        /// The service can reload its configuration (systemd only).
        /// </summary>
        Reload = 32,
        /// <summary>
        /// The start mode of the service can be changed.
        /// </summary>
        SetStartMode = 64,
        /// <summary>
        /// The service can be deleted (uninstalled).
        /// </summary>
        Delete = 128,
    }


    /// <summary>
    /// Information about an OS service (a Windows service, a systemd unit or a SysVinit script).
    /// </summary>
    public sealed class OsServiceInfo
    {
        /// <summary>
        /// The (unique) name of the service, ex: "Spooler" or "ssh.service".
        /// </summary>
        public String Name;

        /// <summary>
        /// The display name of the service, ex: "Print Spooler".
        /// </summary>
        public String DisplayName;

        /// <summary>
        /// The description of the service (may be empty).
        /// </summary>
        public String Description;

        /// <summary>
        /// The current status.
        /// </summary>
        public ServiceStatus Status;

        /// <summary>
        /// The OS specific status text, ex: "running", "exited", "failed" (systemd sub state) or the Windows status name.
        /// </summary>
        public String StatusText;

        /// <summary>
        /// How (or if) the service is started.
        /// </summary>
        public OsServiceStartModes StartMode;

        /// <summary>
        /// The OS specific start mode text, ex: "enabled", "static", "Auto (delayed)".
        /// </summary>
        public String StartModeText;

        /// <summary>
        /// The type of the service, ex: "Own process", "Shared process" (Windows) or "simple", "forking", "oneshot" (systemd).
        /// </summary>
        public String ServiceType;

        /// <summary>
        /// The id of the (main) process of the service, 0 if not running (or unknown).
        /// </summary>
        public int ProcessId;

        /// <summary>
        /// The account that the service runs as, ex: "LocalSystem" or "root" (empty if unknown).
        /// </summary>
        public String Account;

        /// <summary>
        /// The command line that starts the service (empty if unknown).
        /// </summary>
        public String Command;

        /// <summary>
        /// The file that defines the service, ex: the unit file (empty if unknown or not applicable).
        /// </summary>
        public String File;

        /// <summary>
        /// The actions that can currently be performed on the service.
        /// </summary>
        public OsServiceActions Actions;

        /// <summary>
        /// Exit code of the last run (0 if none / unknown).
        /// </summary>
        public int ExitCode;

        /// <summary>
        /// The names of the services that this service depends on (only filled by <see cref="ISystemServices.GetService"/>).
        /// </summary>
        public String[] Dependencies;

        /// <summary>
        /// The names of the services that depends on this service (only filled by <see cref="ISystemServices.GetService"/>).
        /// </summary>
        public String[] Dependents;

        /// <summary>
        /// More OS specific properties (only filled by <see cref="ISystemServices.GetService"/>), ex: the failure actions or all systemd unit properties.
        /// </summary>
        public KeyValuePair<String, String>[] Properties;

        /// <summary>
        /// Test if an action can be performed.
        /// </summary>
        /// <param name="action">The action</param>
        /// <returns>True if the action can be performed</returns>
        public bool Can(OsServiceActions action) => (Actions & action) == action;

        public override string ToString() => String.Concat(Name, " (", Status, ")");
    }
}
