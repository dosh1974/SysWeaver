using System;

namespace SysWeaver.MicroService
{
    public class SmVersionDetail : SmVersionBrief
    {
        /// <summary>
        /// Name of repository, used when synching
        /// </summary>
        public String ServiceName;

        /// <summary>
        /// Number of files in the folder
        /// </summary>
        public long Count;

        /// <summary>
        /// The number of bytes (sum of all file sizes)
        /// </summary>
        public long Size;

        /// <summary>
        /// True if compressed
        /// </summary>
        public bool Comp;

        /// <summary>
        /// Full path
        /// </summary>
        public String FullPath;


        /// <summary>
        /// 
        /// </summary>
        public bool IsRunning;


        public bool IsCompressedService;


        public SmVersionDetail()
        {
        }

        internal SmVersionDetail(SmServiceInfo info, FolderSyncService.ManagedFolderData s) : base(s)
        {
            ServiceName = info.Syncher.Name;
            Count = s.Count;
            Size = s.Size;
            Comp = s.Comp;
            FullPath = s.FullPath;
            IsRunning = (info.Process?.Id ?? 0) != 0;
            IsCompressedService = info.Syncher.Compress;
        }

    }
}
