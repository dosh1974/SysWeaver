using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{

    /// <summary>
    /// The commands (first command line argument) understood by <see cref="ServiceHost.Run"/>.
    /// </summary>
    public enum ServiceVerbs
    {
        /// <summary>
        /// No verb.
        /// </summary>
        None = 0,
        /// <summary>
        /// "help" (also -h, /h, -help, /help, -?, /? and ?): display usage, the default when no arguments are given and a console is available.
        /// </summary>
        Help,
        /// <summary>
        /// "status": display the current service status.
        /// </summary>
        Status,
        /// <summary>
        /// "install": install the service (no start).
        /// </summary>
        Install,
        /// <summary>
        /// "uninstall": stop (if running) and uninstall the service.
        /// </summary>
        Uninstall,
        /// <summary>
        /// "reinstall": uninstall and install, or start if the service was running.
        /// </summary>
        Reinstall,
        /// <summary>
        /// "start": install (if not installed) and start the service, the default when no arguments are given and no console is available.
        /// </summary>
        Start,
        /// <summary>
        /// "stop": stop the service.
        /// </summary>
        Stop,
        /// <summary>
        /// "pause": pause the service.
        /// </summary>
        Pause,
        /// <summary>
        /// "continue": resume a paused service.
        /// </summary>
        Continue,
        /// <summary>
        /// "restart": stop and start the service.
        /// </summary>
        Restart,
        /// <summary>
        /// "debug": run as a console program with all message levels displayed.
        /// </summary>
        Debug,
        /// <summary>
        /// "execute": run as a console program.
        /// </summary>
        Execute,
        /// <summary>
        /// "daemon": run as a service, used by the OS service system.
        /// </summary>
        Daemon,
        /// <summary>
        /// "hash [user] [password]": compute and display a simple password hash.
        /// </summary>
        Hash,
    }


    /// <summary>
    /// Extensions for <see cref="ServiceVerbs"/>.
    /// </summary>
    public static class ServiceVerbHelper
    {
        static readonly IReadOnlyDictionary<ServiceVerbs, String> IntActions = new Dictionary<ServiceVerbs, string>()
        {
            { ServiceVerbs.Status, "Checking status: " },
            { ServiceVerbs.Install, "Installing: " },
            { ServiceVerbs.Uninstall, "Un-installing: " },
            { ServiceVerbs.Reinstall, "Re-installing: " },
            { ServiceVerbs.Start, "Starting: " },
            { ServiceVerbs.Stop, "Stopping: " },
            { ServiceVerbs.Pause, "Pausing: " },
            { ServiceVerbs.Continue, "Continuing (resuming): " },
            { ServiceVerbs.Restart, "Restaring: " },
            { ServiceVerbs.Hash, "Computing hash: " },

        }.Freeze();


        /// <summary>
        /// Get the action text displayed when executing a verb, ex: "Installing: ".
        /// </summary>
        /// <param name="verb">The verb.</param>
        /// <returns>The action text, or null for verbs without one (help, debug, execute, daemon, none).</returns>
        public static String Action(this ServiceVerbs verb) => IntActions.TryGetValue(verb, out var action) ? action : null;


    }



}
