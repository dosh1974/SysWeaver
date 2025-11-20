using System;
using System.Collections.Generic;

namespace SysWeaver.MicroService
{


    public sealed class SmFileInfo
    {
        public String Name;
        public long Size;
        public DateTime LastModified;
    }


    public class SmVersionBrief : IComparable<SmVersionBrief>
    {
        public SmVersionBrief()
        {
        }

        internal SmVersionBrief(FolderSyncService.Data s)
        {
            Name = s.DiscFolder;
            IsActive = s.IsActive;
            Uploaded = s.Uploaded;
            User = s.User;
            Machine = s.Machine;
            Comment = s.Comment;
            LastUsed = s.LastUsed;
        }

        public String Name;

        /// <summary>
        /// True if active
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// Folder creation time
        /// </summary>
        public DateTime Uploaded;

        /// <summary>
        /// The service user that uploaded this
        /// </summary>
        public String User;

        /// <summary>
        /// The name of the source machine (this can be anything)
        /// </summary>
        public String Machine;

        /// <summary>
        /// Optional comment supplied when uploading this folder
        /// </summary>
        public String Comment;

        /// <summary>
        /// When folder was last used (as active)
        /// </summary>
        public DateTime LastUsed;

        public int CompareTo(SmVersionBrief other)
        {
            var i = other.Uploaded.CompareTo(Uploaded);
            if (i != 0)
                return i;
            return other.LastUsed.CompareTo(LastUsed);
        }
    }


    public class SmVersionDetail : SmVersionBrief
    {
        public SmVersionDetail() : base()
        {
        }

        internal SmVersionDetail(FolderSyncService.Data s) : base(s)
        {
        }

    }

    public sealed class SmServiceDetail : SmServiceBrief
    {

        public long ProcId;
        public TimeSpan TotalProcessorTime;
        public String ExeName;
        public SmFileInfo Log;
        public SmFileInfo[] Configs;
        public SmFileInfo[] MasterConfigs;
        public SmVersionBrief[] Versions;

        public SmServiceDetail()
        {
        }

        internal SmServiceDetail(SmServiceInfo info, IReadOnlyList<FolderSyncService.Data> data, String exeName, SmFileInfo log, SmFileInfo[] configs, SmFileInfo[] masterConfigs) : base(info, data)
        {
            var m = info.Metrics;
            ProcId = m.ProcessHandle;
            TotalProcessorTime = m.TotalProcessorTime;
            ExeName = exeName;
            Log = log;
            Configs = configs;
            MasterConfigs = masterConfigs;
            Versions = data.Convert(x => new SmVersionBrief(x));
            Array.Sort(Versions);

        }

    }
}
