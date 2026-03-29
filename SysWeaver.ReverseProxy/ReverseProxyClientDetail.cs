using System;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyClientDetail
    {

        public ReverseProxyClientDetail()
        {
        }

        internal ReverseProxyClientDetail(KeyValuePair<String, ReverseProxyServer.Client> d, String baseUrl)
        {
            var c = d.Value;
            BaseUrl = baseUrl;
            Client = d.Key;
            Ip = c.Ip;
            LastConnected = new DateTime(Interlocked.Read(ref c.LastConnection), DateTimeKind.Utc);
            Connections = Interlocked.Read(ref c.Connections);
            Pending = Interlocked.Read(ref c.PendingCount);
            InProgress = Interlocked.Read(ref c.InProgress);
            Sent = Interlocked.Read(ref c.TotalSent);
            Completed = Interlocked.Read(ref c.TotalCompleted);

            var cc = c.Cache;
            cc.GetGetStats(out GetHitRatio, out GetSemiHitRatio, out GetMissRatio, out GetHitCount, out GetSemiHitCount, out GetMissCount, out GetSize);
            cc.GetHeadStats(out HeadHitRatio, out HeadSemiHitRatio, out HeadMissRatio, out HeadHitCount, out HeadSemiHitCount, out HeadMissCount, out HeadSize);
            GetHitRatio *= 100;
            GetSemiHitRatio *= 100;
            GetMissRatio *= 100;
            HeadHitRatio *= 100;
            HeadSemiHitRatio *= 100;
            HeadMissRatio *= 100;
        }

        /// <summary>
        /// Name of the client (typically machine name)
        /// </summary>
        [TableDataUrl("{0}", "../{1}{0}/")]
        public String Client;

        /// <summary>
        /// The base url to access a client.
        /// </summary>
        [TableDataHide]
        public String BaseUrl;

        /// <summary>
        /// IP of the client (of last the connection made)
        /// </summary>
        [TableDataIp]
        public String Ip;

        /// <summary>
        /// When a connection from the client was last made
        /// </summary>
        public DateTime LastConnected;

        /// <summary>
        /// Number of client connections (ready to process requests)
        /// </summary>
        public long Connections;

        /// <summary>
        /// Number of pending requests (queued, waiting for a connection to process them)
        /// </summary>
        public long Pending;

        /// <summary>
        /// Number of client requests in progress
        /// </summary>
        public long InProgress;

        /// <summary>
        /// Total number of requests sent to the client
        /// </summary>
        public long Sent;

        /// <summary>
        /// Total number of completed client requests
        /// </summary>
        public long Completed;

        /// <summary>
        /// The ratio of cache hits
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double GetHitRatio;

        /// <summary>
        /// The ratio of semi cache hits (returned cached, but waited for pending result, so not optimal performance)
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double GetSemiHitRatio;

        /// <summary>
        /// The ratio of cache misses 
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double GetMissRatio;


        /// <summary>
        /// The ratio of cache hits
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double HeadHitRatio;

        /// <summary>
        /// The ratio of semi cache hits (returned cached, but waited for pending result, so not optimal performance)
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double HeadSemiHitRatio;

        /// <summary>
        /// The ratio of cache misses 
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double HeadMissRatio;


        /// <summary>
        /// Number of cache hits 
        /// </summary>
        public long GetHitCount;
        
        /// <summary>
        /// Number of semi cache hits (returned cached, but waited for pending result, so not optimal performance)
        /// </summary>
        public long GetSemiHitCount;
        
        /// <summary>
        /// Number of cache misses 
        /// </summary>
        public long GetMissCount;
        
        /// <summary>
        /// Number of items in the cache
        /// </summary>
        public long GetSize;



        /// <summary>
        /// Number of cache hits 
        /// </summary>
        public long HeadHitCount;

        /// <summary>
        /// Number of semi cache hits (returned cached, but waited for pending result, so not optimal performance)
        /// </summary>
        public long HeadSemiHitCount;

        /// <summary>
        /// Number of cache misses 
        /// </summary>
        public long HeadMissCount;

        /// <summary>
        /// Number of items in the cache
        /// </summary>
        public long HeadSize;

    }

}
