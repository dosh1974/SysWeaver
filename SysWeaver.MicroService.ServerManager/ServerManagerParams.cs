using System;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// A text file that can be viewed (and optionally edited or deleted) through the server manager
    /// </summary>
    public class SmTextFile
    {
        /// <summary>
        /// The full path to the text file on disc
        /// </summary>
        public String Filename;

        /// <summary>
        /// An optional description
        /// </summary>
        public String Desc;

        /// <summary>
        /// The auth required to view this file.
        /// Defaults to <see cref="Roles.AdminOps"/>.
        /// </summary>
        public String Auth = Roles.AdminOps;

        /// <summary>
        /// If true, the file may be deleted
        /// </summary>
        public bool AllowDelete;

        /// <summary>
        /// If true, the file may be edited (and saved)
        /// </summary>
        public bool AllowEdit;


        /// <summary>
        /// If true, scroll to the end of the file by default.
        /// Defaults to true.
        /// </summary>
        public bool ScrollToEnd = true;

        /// <summary>
        /// Copy all settings from another text file
        /// </summary>
        /// <param name="s">The text file to copy from</param>
        public void CopyFrom(SmTextFile s)
        {
            Filename = s.Filename;
            Desc = s.Desc;
            Auth = s.Auth;
            AllowDelete = s.AllowDelete;
            ScrollToEnd = s.ScrollToEnd;
            AllowEdit = s.AllowEdit;
        }
    }



    /// <summary>
    /// Parameters for the <see cref="ServerManagerService"/>
    /// </summary>
    public sealed class ServerManagerParams
    {
        /// <summary>
        /// If true, service versions are compressed, saving disc space and transfer size at the cost of slower switching.
        /// Defaults to true.
        /// </summary>
        public bool CompressServices = true;

        /// <summary>
        /// Where managed services are located, multiple folders can be separated by ";" (path templates are resolved).
        /// If null or empty, "$(CommonApplicationData)/SysWeaver/ManagedServices" is used.
        /// </summary>
        public String ServiceFolder;

        /// <summary>
        /// The number of days to keep backup's (at least 3).
        /// Defaults to 365.
        /// </summary>
        public int RemoveServiceBackupsDays = 365;

        /// <summary>
        /// Default auth for folders and services.
        /// Synced folders (managed folders and the folders of managed services) also accept the "service_sync" token (<see cref="Roles.ServiceSync"/>),
        /// so a sync client can push without access to the other server manager API's.
        /// Defaults to <see cref="Roles.Admin"/>.
        /// </summary>
        public String SyncAuth = Roles.Admin;

        /// <summary>
        /// SysWeaver services that should be managed
        /// </summary>
        public ManagedService[] Services;

        /// <summary>
        /// Other folders that should be managed
        /// </summary>
        public FsManagedFolder[] Folders;

        /// <summary>
        /// List if text files that can be viewed
        /// </summary>
        public SmTextFile[] TextFiles;

        /// <summary>
        /// If true, the hosts file is readable.
        /// Defaults to true.
        /// </summary>
        public bool AllowHosts = true;

        /// <summary>
        /// If true, the hosts file may be edited (only used if <see cref="AllowHosts"/> is true).
        /// Defaults to true.
        /// </summary>
        public bool AllowHostsEdit = true;

        /// <summary>
        /// The maximum number of seconds that a service verb (start, stop, restart, ...) may take before it's process is killed (the service itself isn't killed), at least 5.
        /// Defaults to 120.
        /// </summary>
        public int VerbTimeoutSeconds = 120;

        /// <summary>
        /// When activating a version: the maximum number of seconds to wait for the running service to uninstall (stop),
        /// after that every process running from the active folder is killed and the activation continues (at least 1).
        /// Defaults to 10.
        /// </summary>
        public int SwapStopTimeoutSeconds = 10;

        /// <summary>
        /// When the server manager activates a version of itself (a self update, done by a detached script):
        /// the number of seconds that the new version must keep running, else the previous version is restored and started (at least 4).
        /// Defaults to 30.
        /// </summary>
        public int SelfUpdateStableSeconds = 30;

        /// <summary>
        /// When the server manager activates a version of itself (a self update, done by a detached script):
        /// an optional url that the new version must answer (any 2xx / 3xx response, the certificate isn't validated) within <see cref="SelfUpdateStableSeconds"/>,
        /// else the previous version is restored and started.
        /// Use a url that is only answered when the web server is up, ex: "https://localhost:443/ServerManager/".
        /// The process status alone can't tell if the new version is usable (ex: running but the web server failed to bind the port).
        /// </summary>
        public String SelfUpdateHealthUrl;
    }




}
