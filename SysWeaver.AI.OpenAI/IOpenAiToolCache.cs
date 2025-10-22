using System;
using System.Reflection;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public interface IOpenAiToolCache
    {
        OpenAiTool GetRegisteredTool(String fn);
        OpenAiTool GetTool(String apiName, String fn = null);
        OpenAiTool GetTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix);
    }


}
