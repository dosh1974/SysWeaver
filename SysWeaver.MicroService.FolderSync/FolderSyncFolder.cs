using System;

namespace SysWeaver.MicroService
{
    public sealed class FolderSyncFolder
    {
        public String Name;
        public String DiscFolder;
        public String Auth = Roles.Debug;
        public int RemoveBackupsDays = 30;
    }
}
