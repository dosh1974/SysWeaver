using System;

namespace SysWeaver.FolderSync
{
    public sealed class FolderSyncResult
    {
        public long SourceFiles;
        public long SourceBytes;
        public long Uploaded;
        public long UploadedSourceBytes;
        public long UploadedNetworkBytes;
        public Exception[] Errors;
    }
}
