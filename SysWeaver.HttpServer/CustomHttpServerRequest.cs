using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SysWeaver.Net
{
    /// <summary>
    /// A minimal request without any connection, only the url related members (and the members of the base class that don't touch the connection) can be used.
    /// Useful as a context when calling code that expects an <see cref="HttpServerRequest"/> outside of a real request.
    /// </summary>
    /// <remarks>
    /// All abstract members, except <see cref="IfNoneMatch"/> and <see cref="AcceptEncoding"/> (always null), throw <see cref="NotImplementedException"/>.
    /// </remarks>
    public sealed class CustomHttpServerRequest : HttpServerRequest
    {

        /// <summary>
        /// Create a request.
        /// </summary>
        /// <param name="httpMethod">The http method, ex: "GET"</param>
        /// <param name="url">The absolute url (used as both raw and decoded url)</param>
        /// <param name="prefix">The prefix of the url (the local url is the url after the prefix), must not be longer than the url</param>
        /// <param name="server">The server, may be null if no server functionality is used</param>
        /// <param name="host">The host, may be null</param>
        /// <param name="queryStart">The index of the '?' in the url, -1 if there is no query string</param>
        /// <param name="didIndex">True if "index.html" was added to the url</param>
        public CustomHttpServerRequest(String httpMethod = null, String url = "", String prefix = "", HttpServerBase server = null, HttpServerHostInfo host = null, int queryStart = -1, bool didIndex = false)
            : base(httpMethod, url, url, prefix, server, host, queryStart, didIndex)
        { 
        }

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders => throw new NotImplementedException();
        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders => throw new NotImplementedException();


        /// <inheritdoc/>
        public override String IfNoneMatch => null;
        /// <inheritdoc/>
        public override string AcceptEncoding => null;


        /// <inheritdoc/>
        public override Stream InputStream => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Stream OutputStream => throw new NotImplementedException();

        /// <inheritdoc/>
        public override long ReqContentLength => throw new NotImplementedException();

        /// <inheritdoc/>
        public override string ProtocolVersion => throw new NotImplementedException();

        /// <inheritdoc/>
        public override void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override IPAddress GetIP()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override string GetReqCookie(string name, String cookieString = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override string GetReqHeader(string name)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override string GetResHeader(string name)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override string GetResMime()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override bool IsDead()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResBody(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task SetResBodyAsync(ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResBody(Byte[] data, int offset, int length)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResContentLength(long length)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResHeader(string header, string value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResMime(string mime)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetResStatusCode(int statusCode)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int GetResStatusCode()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void UpdateCookie(string str)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            throw new NotImplementedException();
        }

    }


}
