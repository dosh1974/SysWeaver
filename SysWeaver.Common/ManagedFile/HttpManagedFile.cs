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
    sealed class HttpManagedFile : IManagedFileSource
    {

        public HttpManagedFile(ManagedFile manager, String url, ManagedFileParams p, Func<ManagedFileData, Exception, Task> onChange, Func<Byte[], Byte[]> computeHash)
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

        readonly Func<Byte[], Byte[]> ConputeHash;

        HttpClient C;

        String LastTime;
        String ETag;

        public async Task<Tuple<ManagedFileData, Exception>> TryGetNow()
        {
            var f = Url;
            try
            {
                var r = new HttpRequestMessage(HttpMethod.Post, FileUrl);
                if (P.GetUserPassword(out var user, out var password, false))
                    r.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Join(":", user, password))));
                var lt = LastTime;
                if (lt != null)
                    r.Headers.Add("If-Modified-Since", lt);
                var et = ETag;
                if (et != null)
                    r.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(et));

                var res = await C.SendAsync(r).ConfigureAwait(false);
                var s = res.StatusCode;
                if (s == HttpStatusCode.NotModified)
                    return null;
                if (s != HttpStatusCode.OK)
                    return new Tuple<ManagedFileData, Exception>(null, new Exception("Http response was: " + (int)s + " - " + s));
                var h = res.Content.Headers;
                ETag = h.TryGetValues("ETag", out var v) ? v?.FirstOrDefault() : null;
                var d = DateTime.UtcNow;
                var lm = h.TryGetValues("Last-Modified", out v) ? v?.FirstOrDefault() : null;
                if (!String.IsNullOrEmpty(lm))
                {
                    if (DateTime.TryParseExact(lm, "r", null, DateTimeStyles.RoundtripKind, out var rt))
                    {
                        d = rt;
                        LastTime = lm;
                    }
                }
                var data = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var fd = new ManagedFileData(Url, data, d, ConputeHash(data), Manager);
                return new Tuple<ManagedFileData, Exception>(fd, null);

            }
            catch (Exception ex)
            {
                return new Tuple<ManagedFileData, Exception>(null, ex);
            }
        }

        readonly String Url;
        readonly Func<ManagedFileData, Exception, Task> A;

        PeriodicTask PollTask;

        async ValueTask<bool> Poll()
        {
            var res = await TryGetNow().ConfigureAwait(false);
            if (res == null)
                return true;
            await A(res.Item1, res.Item2).ConfigureAwait(false);
            return true;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref PollTask, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
        }


    }

}
