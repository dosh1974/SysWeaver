using System;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        sealed class FileSync
        {
            public override string ToString() => Name;

            public readonly String Name;

            public readonly DateTime LastModified;

            public int InProgress;

            public FileSync(string name, DateTime lastModified)
            {
                Name = name;
                LastModified = lastModified;
            }

            public ReadOnlyMemory<Byte> CdcChunks;

        }


    }

}
