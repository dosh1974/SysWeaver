using System;
using System.IO;

namespace SysWeaver
{
    sealed class CdcFileData
    {
        public readonly long CreationTimeUtc;
        public readonly long LastWriteTimeUtc;
        public readonly long LastAccessTimeUtc;
        public readonly int Attributes;
        public readonly ReadOnlyMemory<Byte> Chunks;

        public void SetFileInfo(String filename, long minTime)
        {
            var fi = new FileInfo(filename);
            fi.CreationTimeUtc = new DateTime(CreationTimeUtc + minTime, DateTimeKind.Utc);
            fi.LastWriteTimeUtc = new DateTime(LastWriteTimeUtc + minTime, DateTimeKind.Utc);
            fi.LastAccessTimeUtc = new DateTime(LastAccessTimeUtc + minTime, DateTimeKind.Utc);
            fi.Attributes = (FileAttributes)Attributes;
        }

        public CdcFileData(long creationTimeUtc, long lastWriteTimeUtc, long lastAccessTimeUtc, int attributes, ReadOnlyMemory<byte> chunks)
        {
            CreationTimeUtc = creationTimeUtc;
            LastWriteTimeUtc = lastWriteTimeUtc;
            LastAccessTimeUtc = lastAccessTimeUtc;
            Attributes = attributes;
            Chunks = chunks;
        }
    }
}
