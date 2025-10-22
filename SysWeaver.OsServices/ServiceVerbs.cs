using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{

    public enum ServiceVerbs
    {
        None = 0,
        Help,
        Status,
        Install,
        Uninstall,
        Reinstall,
        Start,
        Stop,
        Pause,
        Continue,
        Restart,
        Debug,
        Execute,
        Daemon,
        Hash,
    }


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
        /// Get the action name, can be null
        /// </summary>
        /// <param name="verb"></param>
        /// <returns></returns>
        public static String Action(this ServiceVerbs verb) => IntActions.TryGetValue(verb, out var action) ? action : null;


    }



}
