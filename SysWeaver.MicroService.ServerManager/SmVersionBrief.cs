using System;

namespace SysWeaver.MicroService
{
    public class SmVersionBrief : IComparable<SmVersionBrief>
    {
#if DEBUG
        public override string ToString() => String.Concat('"', Name, "\" @ ", LastUsed);
#endif//DEBUG

        public SmVersionBrief()
        {
        }

        internal SmVersionBrief(FolderSyncService.PushData s)
        {
            Name = s.DiscFolder;
            IsActive = s.IsActive;
            Uploaded = s.Uploaded;
            User = s.User;
            Machine = s.Machine;
            Comment = s.Comment;
            LastUsed = s.LastUsed;
        }

        /// <summary>
        /// Folder name on disc (this will change when activate / deactivated)
        /// </summary>
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
}
