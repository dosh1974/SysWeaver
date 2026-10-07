using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SysWeaver
{
    /// <summary>
    /// A managed file source for a http / https url, changes are detected by polling (every <see cref="ManagedFileParams.HttpPollFrequency"/> ms)
    /// using conditional requests (If-Modified-Since / If-None-Match).
    /// </summary>
    /// <remarks>
    /// The file is requested using a GET request, the ETag and Last-Modified of the last response are sent back as If-None-Match and If-Modified-Since.
    /// If credentials are specified in the parameters, basic authentication is used.
    /// </remarks>
    sealed class HttpManagedFile : IManagedFileSource
    {

        /// <summary>
        /// Start polling a remote file
        /// </summary>
        /// <param name="manager">The owning managed file (stored in the returned data)</param>
        /// <param name="url">The url of the file</param>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">Invoked with the new data (or an error) when a change is detected</param>
        /// <param name="computeHash">Computes the hash of the data (may return null if hashing is disabled)</param>
        public HttpManagedFile(ManagedFile manager, String url, ManagedFileParams p, Func<ManagedFileData, Task> onChange, Func<ReadOnlyMemory<Byte>, Byte[]> computeHash)
        {
            Manager = manager;
            P = p;
            Url = url;
            ConputeHash = computeHash;
            A = onChange;
            var c = WebTools.CreateHttpClient();
            var bi = url.LastIndexOf('/') + 1;
            FileUrl = url.Substring(bi);
            c.BaseAddress = new Uri(url.Substring(0, bi));
            C = c;
            PollTask = new PeriodicTask(Poll, p.HttpPollFrequency, true, true, true);
        }

        readonly ManagedFile Manager;
        readonly ManagedFileParams P;
        readonly String FileUrl;

        readonly Func<ReadOnlyMemory<Byte>, Byte[]> ConputeHash;

        HttpClient C;

        String LastTime;
        EntityTagHeaderValue ETag;

        /// <summary>
        /// Request the file now
        /// </summary>
        /// <returns>Null if the server responded with 304 (not modified), else the data or a data object with the exception (<see cref="ManagedFileData.Ex"/>) if the request failed (never throws)</returns>
        public async Task<ManagedFileData> TryGetNow()
        {
            var f = Url;
            try
            {
                using var r = new HttpRequestMessage(HttpMethod.Get, FileUrl);
                //  A configured credentials file must exist and contain valid credentials (reported as a failed request)
                if (P.GetUserPassword(out var user, out var password, !String.IsNullOrEmpty(P.CredFile)))
                    r.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Join(":", user, password))));
                var lt = LastTime;
                if (lt != null)
                    r.Headers.Add("If-Modified-Since", lt);
                var et = ETag;
                if (et != null)
                    r.Headers.IfNoneMatch.Add(et);

                using var res = await C.SendAsync(r).ConfigureAwait(false);
                var s = res.StatusCode;
                if (s == HttpStatusCode.NotModified)
                    return null;
                if (s != HttpStatusCode.OK)
                    return new ManagedFileData(Url, Memory<Byte>.Empty, DateTime.MinValue, null, Manager, new Exception("Http response was: " + (int)s + " - " + s));
                var h = res.Content.Headers;
                //  ETag is a response header (not a content header), keep the parsed value (it may be a weak tag)
                ETag = res.Headers.ETag;
                var d = DateTime.UtcNow;
                var lm = h.TryGetValues("Last-Modified", out var v) ? v?.FirstOrDefault() : null;
                if (!String.IsNullOrEmpty(lm))
                {
                    if (DateTime.TryParseExact(lm, "r", null, DateTimeStyles.RoundtripKind, out var rt))
                    {
                        d = rt;
                        LastTime = lm;
                    }
                }
                var data = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return new ManagedFileData(Url, data, d, ConputeHash(data), Manager, null);

            }
            catch (Exception ex)
            {
                return new ManagedFileData(Url, Memory<Byte>.Empty, DateTime.MinValue, null, Manager, ex);
            }
        }

        readonly String Url;
        readonly Func<ManagedFileData, Task> A;

        PeriodicTask PollTask;

        async Task<bool> Poll()
        {
            var res = await TryGetNow().ConfigureAwait(false);
            if (res == null)
                return true;
            await A(res).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Stop polling and dispose the http client
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref PollTask, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
        }


    }

}
