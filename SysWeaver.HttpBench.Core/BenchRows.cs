using System;
using SysWeaver.Data;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The latency and traffic columns that the end point, module, second and worker tables share.
    /// </summary>
    public abstract class BenchLatencyRow
    {
        [TableDataNumber]
        [TableDataDesc("Completed requests (including errors)")]
        [TableDataOrder(1)]
        public long Requests;

        [TableDataNumber]
        [TableDataDesc("Requests that failed: no response, a time out or an http status of 400 or more")]
        [TableDataOrder(2)]
        public long Errors;

        [TableDataNumber(2, "{0} %")]
        [TableDataTitle("Error rate")]
        [TableDataDesc("Errors / requests")]
        [TableDataOrder(3)]
        public double ErrorPercent;

        [TableDataNumber(1)]
        [TableDataTitle("Requests/s")]
        [TableDataDesc("Requests completed per second")]
        [TableDataOrder(4)]
        public double Rps;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Avg")]
        [TableDataDesc("Average response time: from sending the request to the last byte of the response")]
        [TableDataOrder(5)]
        public double AvgMs;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Min")]
        [TableDataDesc("Shortest response time")]
        [TableDataOrder(6)]
        public double MinMs;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Max")]
        [TableDataDesc("Longest response time")]
        [TableDataOrder(11)]
        public double MaxMs;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Avg TTFB")]
        [TableDataDesc("Average time to the first byte: from sending the request to the response headers")]
        [TableDataOrder(12)]
        public double AvgTtfbMs;

        [TableDataByteSize]
        [TableDataTitle("Sent")]
        [TableDataDesc("Bytes sent on the wire: request lines, headers, bodies and TLS")]
        [TableDataOrder(13)]
        public long WireSent;

        [TableDataByteSize]
        [TableDataTitle("Received")]
        [TableDataDesc("Bytes received on the wire: status lines, headers, bodies and TLS")]
        [TableDataOrder(14)]
        public long WireRecv;

        [TableDataByteSize]
        [TableDataTitle("Body received")]
        [TableDataDesc("Response body bytes as received (compressed when the server compressed them)")]
        [TableDataOrder(15)]
        public long BodyRecv;

        [TableDataByteSpeed]
        [TableDataTitle("Receive rate")]
        [TableDataDesc("Bytes received on the wire per second")]
        [TableDataOrder(17)]
        public double RecvPerSecond;

        internal void Set(BenchCounters c, double seconds)
        {
            Requests = c.Count;
            Errors = c.Errors;
            ErrorPercent = c.ErrorPercent;
            Rps = seconds > 0 ? c.Count / seconds : 0;
            AvgMs = c.AvgMs;
            MinMs = c.MinMs;
            MaxMs = c.MaxMs;
            AvgTtfbMs = c.AvgTtfbMs;
            WireSent = c.WireSent;
            WireRecv = c.WireRecv;
            BodyRecv = c.BodyRecv;
            RecvPerSecond = seconds > 0 ? c.WireRecv / seconds : 0;
        }
    }

    /// <summary>
    /// The results of one end point.
    /// </summary>
    [TableDataDefaultOrder("Order")]
    public sealed class BenchEndPointRow : BenchLatencyRow
    {
        [TableDataHide]
        [TableDataDesc("The order in which the workers call the end points")]
        [TableDataOrder(-20)]
        public int Order;

        [TableDataKey]
        [TableDataOrder(-19)]
        public String Module;

        [TableDataKey]
        [TableDataTitle("End point")]
        [TableDataOrder(-18)]
        public String EndPoint;

        [TableDataOrder(-17)]

        public String Method;

        [TableDataDesc("The url, relative to the target")]
        [TableDataOrder(-16)]
        public String Path;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Median")]
        [TableDataDesc("50 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(7)]
        public double P50Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P90")]
        [TableDataDesc("90 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(8)]
        public double P90Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P95")]
        [TableDataDesc("95 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(9)]
        public double P95Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P99")]
        [TableDataDesc("99 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(10)]
        public double P99Ms;

        [TableDataByteSize]
        [TableDataTitle("Avg body")]
        [TableDataDesc("Average response body size as received")]
        [TableDataOrder(16)]
        public double AvgBody;

        [TableDataNumber]
        [TableDataTitle("New connections")]
        [TableDataDesc("Requests that had to open a new connection (keep alive was not used)")]
        [TableDataOrder(18)]
        public long Connections;

        [TableDataHide]
        [TableDataOrder(20)]
        public String Desc;
    }

    /// <summary>
    /// The results of one module (group of end points).
    /// </summary>
    [TableDataDefaultOrder("Order")]
    public sealed class BenchModuleRow : BenchLatencyRow
    {
        [TableDataHide]
        [TableDataOrder(-20)]
        public int Order;

        [TableDataKey]
        [TableDataOrder(-19)]
        public String Module;

        [TableDataNumber]
        [TableDataTitle("End points")]
        [TableDataOrder(-12)]
        public int EndPoints;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Median")]
        [TableDataDesc("50 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(7)]
        public double P50Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P95")]
        [TableDataDesc("95 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(9)]
        public double P95Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P99")]
        [TableDataDesc("99 % of the requests were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(10)]
        public double P99Ms;

        [TableDataNumber(2, "{0} %")]
        [TableDataTitle("Share")]
        [TableDataDesc("Share of all requests")]
        [TableDataOrder(19)]
        public double SharePercent;
    }

    /// <summary>
    /// The results of one second (all end points).
    /// </summary>
    [TableDataDefaultOrder("Second")]
    public sealed class BenchSecondRow : BenchLatencyRow
    {
        [TableDataKey]
        [TableDataNumber]
        [TableDataDesc("Seconds since the start (the second in which the requests completed)")]
        [TableDataOrder(-15)]
        public int Second;

        [TableDataDesc("The start of the second (utc)")]
        [TableDataOrder(-14)]
        public DateTime Time;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Median")]
        [TableDataDesc("50 % of the requests of this second were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(7)]
        public double P50Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P95")]
        [TableDataDesc("95 % of the requests of this second were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(9)]
        public double P95Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P99")]
        [TableDataDesc("99 % of the requests of this second were faster (from a histogram, about ±6 %)")]
        [TableDataOrder(10)]
        public double P99Ms;

        [TableDataNumber]
        [TableDataTitle("New connections")]
        [TableDataOrder(18)]
        public long Connections;
    }

    /// <summary>
    /// The results of one end point in one second.
    /// </summary>
    [TableDataDefaultOrder("Second", "Order")]
    public sealed class BenchSecondEndPointRow : BenchLatencyRow
    {
        [TableDataKey]
        [TableDataNumber]
        [TableDataDesc("Seconds since the start (the second in which the requests completed)")]
        [TableDataOrder(-15)]
        public int Second;

        [TableDataHide]
        [TableDataOrder(-20)]
        public int Order;

        [TableDataOrder(-19)]

        public String Module;

        [TableDataKey]
        [TableDataTitle("End point")]
        [TableDataOrder(-18)]
        public String EndPoint;
    }

    /// <summary>
    /// The results of one worker.
    /// </summary>
    [TableDataDefaultOrder("Worker")]
    public sealed class BenchWorkerRow : BenchLatencyRow
    {
        [TableDataKey]
        [TableDataNumber]
        [TableDataDesc("The worker (one client: one connection at a time and one session)")]
        [TableDataOrder(-13)]
        public int Worker;

        [TableDataNumber]
        [TableDataTitle("Connections")]
        [TableDataDesc("Connections the worker opened (1 when keep alive worked all the time)")]
        [TableDataOrder(18)]
        public long Connections;
    }

    /// <summary>
    /// The responses of one end point with one status code.
    /// </summary>
    [TableDataDefaultOrder("Order", "Status")]
    public sealed class BenchStatusRow
    {
        [TableDataHide]
        public int Order;

        public String Module;

        [TableDataKey]
        [TableDataTitle("End point")]
        public String EndPoint;

        [TableDataKey]
        [TableDataNumber]
        [TableDataDesc("The http status code, 0 = no response (a connection error or a time out)")]
        public int Status;

        [TableDataDesc("What the status means")]
        public String Meaning;

        [TableDataNumber]
        public long Count;

        [TableDataNumber(2, "{0} %")]
        [TableDataTitle("Share")]
        [TableDataDesc("Share of the requests of the end point")]
        public double SharePercent;
    }

    /// <summary>
    /// One kind of error (end point, status and message).
    /// </summary>
    [TableDataDefaultOrder("-Count")]
    public sealed class BenchErrorRow
    {
        public String Module;

        [TableDataKey]
        [TableDataTitle("End point")]
        public String EndPoint;

        [TableDataNumber]
        [TableDataDesc("The http status code, 0 = no response (a connection error or a time out)")]
        public int Status;

        [TableDataKey]
        [TableDataWordWrap]
        public String Message;

        [TableDataNumber]
        public long Count;

        [TableDataNumber(3, "{0} s")]
        [TableDataTitle("First")]
        [TableDataDesc("When the first request with this error was sent (seconds since the start)")]
        public double FirstSecond;

        [TableDataNumber(3, "{0} s")]
        [TableDataTitle("Last")]
        [TableDataDesc("When the last request with this error was sent (seconds since the start)")]
        public double LastSecond;
    }

    /// <summary>
    /// One request (a sample of all requests when there were many).
    /// </summary>
    [TableDataDefaultOrder("Seq")]
    public sealed class BenchRequestRow
    {
        [TableDataKey]
        [TableDataNumber]
        [TableDataTitle("Request")]
        [TableDataDesc("The number of the request, in the order the kept requests completed")]
        public long Seq;

        [TableDataNumber(3, "{0} s")]
        [TableDataTitle("Start")]
        [TableDataDesc("When the request was sent (seconds since the start)")]
        public double StartSecond;

        [TableDataNumber]
        public int Worker;

        public String Module;

        [TableDataTitle("End point")]
        public String EndPoint;

        [TableDataNumber]
        [TableDataDesc("The http status code, 0 = no response (a connection error or a time out)")]
        public int Status;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Time")]
        [TableDataDesc("From sending the request to the last byte of the response")]
        public double Ms;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("TTFB")]
        [TableDataDesc("From sending the request to the response headers")]
        public double TtfbMs;

        [TableDataByteSize]
        [TableDataTitle("Sent")]
        [TableDataDesc("Bytes sent on the wire (request line, headers, body, TLS)")]
        public int WireSent;

        [TableDataByteSize]
        [TableDataTitle("Received")]
        [TableDataDesc("Bytes received on the wire (status line, headers, body, TLS)")]
        public int WireRecv;

        [TableDataByteSize]
        [TableDataTitle("Value sent")]
        [TableDataDesc("The api value as sent: the (compressed) POST body or the GET query string")]
        public int BodySent;

        [TableDataByteSize]
        [TableDataTitle("Body received")]
        public int BodyRecv;

        [TableDataTitle("New connection")]
        [TableDataDesc("The request opened a new connection (connect and TLS hand shake included in its time)")]
        public bool NewConnection;

        [TableDataWordWrap]
        public String Error;
    }

    /// <summary>
    /// One bucket of the latency histogram.
    /// </summary>
    [TableDataDefaultOrder("FromMs")]
    public sealed class BenchHistogramRow
    {
        [TableDataKey]
        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("From")]
        public double FromMs;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("To")]
        public double ToMs;

        [TableDataNumber]
        public long Count;

        [TableDataNumber(3, "{0} %")]
        [TableDataTitle("Share")]
        public double SharePercent;

        [TableDataNumber(3, "{0} %")]
        [TableDataTitle("Cumulative")]
        [TableDataDesc("Share of the requests that were at most this fast")]
        public double CumulativePercent;
    }
}
