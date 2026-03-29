using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{
    public sealed partial class ReverseProxyServer 
    {
        internal sealed class Client
        {
            public String Ip;
            public long LastConnection;
            public long Connections;
            public long PendingCount;
            public long TotalSent;
            public long InProgress;
            public long TotalCompleted;
            public readonly ProxyRequestCache Cache = new ProxyRequestCache();

            public readonly BlockUntilChange Waiter = new BlockUntilChange();
            public readonly ConcurrentQueue<ReverseProxyRequest> Pending = new ();
            public readonly ConcurrentDictionary<String, ReverseProxyResponse> Responses = new(StringComparer.Ordinal);
            public readonly BlockUntilChange ResponseWaiter = new BlockUntilChange();



            public async ValueTask<ReverseProxyResponse> MakeRequest(ReverseProxyRequest r)
            {
                var p = Pending;
                if (Interlocked.Read(ref Connections) <= 0)
                {
                    var last = (DateTime.UtcNow - new DateTime(Interlocked.Read(ref LastConnection), DateTimeKind.Utc)).TotalSeconds;
                    if (last > 60)
                    {
                        while (p.TryDequeue(out var x)) ;
                        throw new HttpResponseException(503);
                    }
                }
                Interlocked.Increment(ref PendingCount);
                var rid = r.RequestId;
                var now = DateTime.UtcNow;
                var end = now.AddSeconds(7 * 60);
                try
                {
                    p.Enqueue(r);
                    Waiter.Change();
                    var responses = Responses;
                    var w = ResponseWaiter;
                    long cc = 0;
                    for (; ; )
                    {
                        if (responses.TryGetValue(rid, out var res))
                            return res;
                        var wait = (int)(end - DateTime.UtcNow).TotalMilliseconds;
                        if (wait < 1)
                            return null;
                        cc = await w.WaitForChange(cc, wait).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref PendingCount);
                }


            }


        }

    }

}
