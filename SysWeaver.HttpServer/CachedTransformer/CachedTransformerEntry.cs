using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public sealed class CachedTransformerEntry
    {
        public volatile bool Completed;
        public long OrgSize;
        public FileHttpRequestHandler[] Files;

    }


}
