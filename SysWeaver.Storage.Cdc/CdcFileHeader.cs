using System;

namespace SysWeaver
{
    sealed class CdcFileHeader
    {
        public readonly long MinTime;
        public readonly String[] Files;

        public CdcFileHeader(long minTime, string[] files)
        {
            MinTime = minTime;
            Files = files;
        }
    }
}
