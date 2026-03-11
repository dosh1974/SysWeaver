using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    sealed class MediaTransformCacheEntry
    {
        public volatile bool Completed;
        public FileHttpRequestHandler[] Files;

    }


}
