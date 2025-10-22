using SysWeaver.Serialization;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public interface IHttpApiAudit : IHaveUri
    {
        String AuditGroup { get; }
    }

    public interface IApiHttpServerEndPoint : IHttpServerEndPoint, IHttpApiAudit
    {
        Object Instance { get; }
        MethodInfo MethodInfo { get; }
        void GetDesc(out Type arg, out Type ret, out String methodDesc, out String argDesc, out String retDesc, out String argName);
        ValueTask<ReadOnlyMemory<Byte>> InvokeAsync(HttpServerRequest request, ReadOnlyMemory<Byte> data);

    }



    public sealed class HttpApiAudit : IHttpApiAudit
    {
        public HttpApiAudit(String uri, String auditGroup)
        {
            Uri = uri;
            AuditGroup = auditGroup;
        }

#if DEBUG
        public override string ToString() => AuditGroup == null ? Uri : String.Concat(Uri, " in ", AuditGroup);
#endif//DEBUG

        public string Uri { get; init; }
        public string AuditGroup { get; init;  }

    }



}
