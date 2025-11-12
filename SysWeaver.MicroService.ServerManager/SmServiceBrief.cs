using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public class SmServiceBrief
    {
        /// <summary>
        /// Name of the repo, use this when synchronizing a local folder.
        /// </summary>
        //[TableDataUrl("{0}", "*../FolderSync/Folders/{0}/explore", "Click to explore \"{3}\".")]
        [TableDataUrl("{0}", "*../ServerManager/ServiceInfo.html?p=\"{0}\"", "Click to view service details \"{3}\".")]
        public String Name;

        /// <summary>
        /// Status of the service
        /// </summary>
        public String Status;

        /// <summary>
        /// Amount of RAM used
        /// </summary>
        [TableDataByteSize]
        public long MemUsage;

        /// <summary>
        /// The current CPU usage
        /// </summary>
        [TableDataNumber(-2, "{0} %")]
        public double CpuUsage;

        /// <summary>
        /// Number of versions that this server have
        /// </summary>
        public int Versions;

        /// <summary>
        /// The time when the current version was uploaded
        /// </summary>
        public DateTime Uploaded;

        /// <summary>
        /// The user that uploaded the current version
        /// </summary>
        public String User;

        /// <summary>
        /// The name of the source machine that the current version originated from
        /// </summary>
        public String Machine;

        /// <summary>
        /// Optional comment supplied when uploading the current version
        /// </summary>
        [TableDataText]
        public String Comment;

        /// <summary>
        /// The base folder on disc
        /// </summary>
        [TableDataText(30, "{0}", "{0}", true)]
        public String Folder;

        /// <summary>
        /// Required auth
        /// </summary>
        [TableDataTags]
        public String Auth;

        public SmServiceBrief()
        {
        }

        internal SmServiceBrief(SmServiceInfo info, IReadOnlyList<FolderSyncService.Data> data)
        {
            var s = info.Service;
            var f = info.Syncher;
            Name = s.Name;
            Versions = data.Count;
            var x = data.FirstOrDefault(x => x.IsActive);
            Folder = Path.GetDirectoryName(f.DiscFolder);
            Uploaded = x?.Uploaded ?? DateTime.MinValue;
            User = x?.User;
            Machine = x?.Machine;
            Comment = x?.Comment;
            Auth = x?.Auth;
            var m = info.Metrics;
            Status = m.Status.ToString();
            MemUsage = m.MemUsage;
            CpuUsage = m.CpuUsage;
        }
    }
}
