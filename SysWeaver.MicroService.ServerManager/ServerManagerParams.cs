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
        public FolderSyncFolder[] Folders;

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
    }




}
