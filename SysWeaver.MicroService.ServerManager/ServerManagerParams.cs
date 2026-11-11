using System;

namespace SysWeaver.MicroService
{

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
        /// The auth required to view this file
        /// </summary>
        public String Auth = Roles.AdminOps;

        /// <summary>
        /// If true, the file way be deleted
        /// </summary>
        public bool AllowDelete;

        /// <summary>
        /// If true, the file way be edited (and saved)
        /// </summary>
        public bool AllowEdit;


        /// <summary>
        /// If true, scroll to the end of the file by default
        /// </summary>
        public bool ScrollToEnd = true;

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



    public sealed class ServerManagerParams
    {
        /// <summary>
        /// If true, service versions are compressed, saving disc space and transfer size at the cost of slower switching
        /// </summary>
        public bool CompressServices = true;

        /// <summary>
        /// Where managed services are located
        /// </summary>
        public String ServiceFolder;

        /// <summary>
        /// The number of days to keep backup's
        /// </summary>
        public int RemoveServiceBackupsDays = 365;

        /// <summary>
        /// Default auth for folders and services
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
        /// If true, the hosts file is readable
        /// </summary>
        public bool AllowHosts = true;

        /// <summary>
        /// If true, the hosts file may be edited
        /// </summary>
        public bool AllowHostsEdit = true;

        /// <summary>
        /// The maximum number of seconds that a service verb (start, stop, restart, ...) may take before it's process is killed (the service itself isn't killed)
        /// </summary>
        public int VerbTimeoutSeconds = 120;

        /// <summary>
        /// When activating a version: the maximum number of seconds to wait for the running service to uninstall (stop),
        /// after that every process running from the active folder is killed and the activation continues
        /// </summary>
        public int SwapStopTimeoutSeconds = 10;

        /// <summary>
        /// When the server manager activates a version of itself (a self update, done by a detached script):
        /// the number of seconds that the new version must keep running, else the previous version is restored and started
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
