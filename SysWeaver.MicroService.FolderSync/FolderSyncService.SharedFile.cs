using System;
using SysWeaver.FolderSync;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        internal sealed class SharedFile
        {
            public override string ToString() => File.Name;
            public readonly FolderSyncFile File;
            public readonly ReadOnlyMemory<Byte> ChunkHashes;

            public SharedFile(FolderSyncFile file, ReadOnlyMemory<byte> chunkHashes)
            {
                File = file;
                ChunkHashes = chunkHashes;
            }
        }
    }

}
