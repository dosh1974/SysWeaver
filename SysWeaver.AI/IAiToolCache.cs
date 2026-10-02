using System;
using System.Reflection;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public interface IAiToolCache
    {
        AiTool GetRegisteredTool(String fn);
        AiTool GetTool(String apiName, String fn = null);
        AiTool GetTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix);
    }


}
