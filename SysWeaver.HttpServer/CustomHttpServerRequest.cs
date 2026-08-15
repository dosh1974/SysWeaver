using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SysWeaver.Net
{
    public sealed class CustomHttpServerRequest : HttpServerRequest
    {

        public CustomHttpServerRequest(String httpMethod = null, String url = "", String prefix = "", HttpServerBase server = null, HttpServerHostInfo host = null, int queryStart = -1, bool didIndex = false)
            : base(httpMethod, url, prefix, server, host, queryStart, didIndex)
        { 
        }

        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders => throw new NotImplementedException();
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders => throw new NotImplementedException();


        public override String IfNoneMatch => null;
        public override string AcceptEncoding => null;


        public override Stream InputStream => throw new NotImplementedException();

        public override Stream OutputStream => throw new NotImplementedException();

        public override long ReqContentLength => throw new NotImplementedException();

        public override string ProtocolVersion => throw new NotImplementedException();

        public override void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore)
        {
            throw new NotImplementedException();
        }

        public override IPAddress GetIP()
        {
            throw new NotImplementedException();
        }

        public override string GetReqCookie(string name, String cookieString = null)
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

        public override void SetResBody(Byte[] data, int offset, int length)
        {
            throw new NotImplementedException();
        }
        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
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

        public override int GetResStatusCode()
        {
            throw new NotImplementedException();
        }

        public override void UpdateCookie(string str)
        {
            throw new NotImplementedException();
        }

        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            throw new NotImplementedException();
        }

    }


}
