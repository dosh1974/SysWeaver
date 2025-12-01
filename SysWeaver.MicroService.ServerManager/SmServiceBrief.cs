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
        [TableDataUrl("{0}", "*../ServerManager/serviceInfo.html?p={0}", "Click to view service details \"{3}\".")]
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
        /// Number of versions that this service have
        /// </summary>
        public int VersionCount;

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

        public void CopyTo(SmServiceBrief to)
        {
            to.Name = Name;
            to.Status = Status;
            to.MemUsage = MemUsage;
            to.CpuUsage = CpuUsage;
            to.VersionCount = VersionCount;
            to.Uploaded = Uploaded;
            to.User = User;
            to.Machine = Machine;
            to.Comment = Comment;
            to.Folder = Folder;
            to.Auth = Auth;
        }

        internal SmServiceBrief(SmServiceInfo info, IReadOnlyList<FolderSyncService.Data> data)
        {
            var s = info.Service;
            var f = info.Syncher;
            Name = s.Name;
            VersionCount = data.Count;
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


    public class SmServiceBriefActions : SmServiceBrief
    {
        public SmServiceBriefActions(SmServiceBrief b)
        {
            b.CopyTo(this);
        }

        /// <summary>
        /// Actions that can be performed
        /// </summary>
        [TableDataActions(
            "Remove",
            "Remove management of the service, this will not Stop or Disable the service, nor remove any files",
            "../ServerManager/RemoveService?\"{0}\"", "../icons/close.svg"
            )]
        public String Actions => Name;

    }
}
