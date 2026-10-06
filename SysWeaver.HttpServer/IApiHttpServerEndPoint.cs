using SysWeaver.Serialization;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Identifies an audited operation (passed to <see cref="IApiAuditService"/> callbacks).
    /// </summary>
    public interface IHttpApiAudit : IHaveUri
    {
        /// <summary>
        /// The audit group (from <see cref="SysWeaver.MicroService.WebApiAuditAttribute.Group"/>, "Default" if not specified), null if not audited.
        /// </summary>
        String AuditGroup { get; }
    }

    /// <summary>
    /// A registered Web API end point (see <see cref="ApiHttpServerModule"/>).
    /// </summary>
    public interface IApiHttpServerEndPoint : IHttpServerEndPoint, IHttpApiAudit
    {
        /// <summary>
        /// The object instance that the API method is invoked on.
        /// </summary>
        Object Instance { get; }

        /// <summary>
        /// The method that implements the API.
        /// </summary>
        MethodInfo MethodInfo { get; }

        /// <summary>
        /// Get a description of the API (from the XML documentation of the method, if available).
        /// </summary>
        /// <param name="arg">The argument type, null if the API takes no argument.</param>
        /// <param name="ret">The return type, null if the API returns nothing.</param>
        /// <param name="methodDesc">The summary of the method.</param>
        /// <param name="argDesc">The documentation of the argument parameter.</param>
        /// <param name="retDesc">The documentation of the return value.</param>
        /// <param name="argName">The name of the argument parameter, null if none.</param>
        void GetDesc(out Type arg, out Type ret, out String methodDesc, out String argDesc, out String retDesc, out String argName);

        /// <summary>
        /// Invoke the API programmatically as if it was POST'ed (used by AI tools, chart exports, the API explorer etc).
        /// </summary>
        /// <param name="request">The request context to invoke the API with (its <see cref="HttpServerRequest.Custom"/> is overwritten with the input data).</param>
        /// <param name="data">The serialized input (in the request's content type, or the default serializer).</param>
        /// <returns>The serialized result.</returns>
        /// <remarks>
        /// No authorization is performed, the caller is responsible for checking <see cref="IHttpServerEndPoint.Auth"/> against the request's session.
        /// Auditing is performed as for a normal call.
        /// </remarks>
        Task<ReadOnlyMemory<Byte>> InvokeAsync(HttpServerRequest request, ReadOnlyMemory<Byte> data);

    }



    /// <summary>
    /// A simple <see cref="IHttpApiAudit"/> implementation, used to audit operations that aren't API end points.
    /// </summary>
    public sealed class HttpApiAudit : IHttpApiAudit
    {
        /// <summary>
        /// Create an audit identifier.
        /// </summary>
        /// <param name="uri">The uri (or other identifier) of the operation.</param>
        /// <param name="auditGroup">The audit group.</param>
        public HttpApiAudit(String uri, String auditGroup)
        {
            Uri = uri;
            AuditGroup = auditGroup;
        }

#if DEBUG
        public override string ToString() => AuditGroup == null ? Uri : String.Concat(Uri, " in ", AuditGroup);
#endif//DEBUG

        /// <inheritdoc/>
        public string Uri { get; init; }

        /// <inheritdoc/>
        public string AuditGroup { get; init;  }

    }



}
