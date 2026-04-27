using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.FolderSync;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        sealed class Sync
        {
            public readonly ConcurrentDictionary<String, FileSync> Files = new ConcurrentDictionary<string, FileSync>();
            public readonly ConcurrentDictionary<ReadOnlyMemory<Byte>, int> MissingChunks = new(ReadOnlyMemoryComparer.GetEqualityComparer<Byte>());
            public readonly ManagedFolder Target;
            public readonly String DestPath;
            public readonly bool UseFolder;
            public readonly IDisposable D;

            public long FileInProgess;



            public void Touch()
            {
                Interlocked.Exchange(ref LastUsed, DateTime.UtcNow.Ticks);
            }

            static readonly long ExpirationTime = TimeSpan.FromMinutes(5).Ticks;

            public bool IsOld
            {
                get
                {
                    return (DateTime.UtcNow.Ticks - Interlocked.Read(ref LastUsed)) > ExpirationTime;
                }
            }

            long LastUsed;

            public int DoExit;

            public readonly long CopyCount;
            public readonly long CopySize;
            public readonly String User;
            public readonly DateTime Start;
            public readonly ManagedFolderSyncRequest R;

            public long UploadCount;
            public long UploadSize;
            public long NetworkSize;

            /// <summary>
            /// Number of file chunks sent (all chunks in all missing files)
            /// </summary>
            public long ChunkCount = 0;
            /// <summary>
            /// Number of new chunks that was sent (all missing chunks in all missing files)
            /// </summary>
            public long NewChunkCount = 0;
            /// <summary>
            /// Total number of compressed bytes that was sent (all missing chunk data in all missing files)
            /// </summary>
            public long NewChunkSize = 0;


            public Sync(ManagedFolderSyncRequest r, IEnumerable<FileSync> files, string destPath, ManagedFolder target, bool activate, IDisposable d, long copyCount, long copySize, String user, DateTime start)
            {
                R = r;
                var fs = Files;
                foreach (var f in files)
                    fs.TryAdd(f.Name.FastToLower().Replace('\\', '/'), f);
                Target = target;
                DestPath = destPath;
                UseFolder = activate;
                D = d;
                CopyCount = copyCount;
                CopySize = copySize;
                User = user;
                Start = start;
                LastUsed = DateTime.UtcNow.Ticks;

            }
        }


    }

}
