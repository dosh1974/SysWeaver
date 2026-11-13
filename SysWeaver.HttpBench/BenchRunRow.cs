using System;
using SysWeaver.Data;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// One test (the list of tests).
    /// </summary>
    [TableDataDefaultOrder("-Id")]
    public sealed class BenchRunRow
    {
        [TableDataKey]
        [TableDataUrl("Test {0}", "*../httpbench/bench.html?run={0}", "Show the report of test {0}")]
        [TableDataTitle("Test")]
        public int Id;

        [TableDataDesc("When the test started (utc)")]
        public DateTime Started;

        public String State;

        public String Target;

        [TableDataNumber(0, "{0} s")]
        [TableDataTitle("Duration")]
        public double Seconds;

        [TableDataNumber]
        public int Workers;

        [TableDataNumber]
        [TableDataTitle("End points")]
        public int EndPoints;

        [TableDataTitle("Values as")]
        [TableDataDesc("How the api values were sent: POST (the body) or GET (the query string)")]
        public String ValueMethod;

        [TableDataTitle("Value compression")]
        public String RequestCompression;

        [TableDataTitle("Accept-Encoding")]
        public String ResponseCompression;

        [TableDataNumber]
        public long Requests;

        [TableDataNumber(1)]
        [TableDataTitle("Requests/s")]
        public double Rps;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("Avg")]
        public double AvgMs;

        [TableDataNumber(3, "{0} ms")]
        [TableDataTitle("P95")]
        public double P95Ms;

        [TableDataNumber]
        public long Errors;
    }
}
