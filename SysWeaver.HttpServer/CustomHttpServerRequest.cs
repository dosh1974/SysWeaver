using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class CustomHttpServerRequest : HttpServerRequest
    {

        public CustomHttpServerRequest(String httpMethod = null, String ifNoneMatch = null, String acceptEncoding = null, String url = "", String prefix = "", HttpServerBase server = null, Uri uri = null, HttpServerHostInfo host = null)
            : base(httpMethod, ifNoneMatch, acceptEncoding, url, prefix, server, uri, host)
        { 
        }

        public override Stream InputStream => throw new NotImplementedException();

        public override Stream OutputStream => throw new NotImplementedException();

        public override long ReqContentLength => throw new NotImplementedException();

        public override string ProtocolVersion => throw new NotImplementedException();

        public override void CopyHeaders(HttpServerRequest to)
        {
            throw new NotImplementedException();
        }

        public override IPAddress GetIP()
        {
            throw new NotImplementedException();
        }

        public override string GetReqCookie(string name)
        {
            throw new NotImplementedException();
        }

        public override string GetReqHeader(string name)
        {
            throw new NotImplementedException();
        }

        public override string GetResHeader(string name)
        {
            throw new NotImplementedException();
        }

        public override string GetResMime()
        {
            throw new NotImplementedException();
        }

        public override bool IsDead()
        {
            throw new NotImplementedException();
        }

        public override void SetResBody(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        public override ValueTask SetResBodyAsync(ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public override void SetResContentLength(long length)
        {
            throw new NotImplementedException();
        }

        public override void SetResHeader(string header, string value)
        {
            throw new NotImplementedException();
        }

        public override void SetResMime(string mime)
        {
            throw new NotImplementedException();
        }

        public override void SetResStatusCode(int statusCode)
        {
            throw new NotImplementedException();
        }

        public override void UpdateCookie(string name, string value, DateTime exp, string path = "/;HttpOnly")
        {
            throw new NotImplementedException();
        }
    }


}
