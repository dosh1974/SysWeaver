using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SysWeaver.MicroService;

namespace SysWeaver.AI
{

    public sealed class OpenAiCallInstance
    {
        public readonly String Function;
        public readonly BinaryData Args;
        public readonly DateTime Start;
        public readonly DateTime End;
        public readonly String Ret;
        public readonly Exception Ex;

        public OpenAiCallInstance(string function, BinaryData args, DateTime start, DateTime end, string ret = null)
        {
            Function = function;
            Args = args;
            Start = start;
            End = end;
            Ret = ret;
        }

        public OpenAiCallInstance(string function, BinaryData args, DateTime start, DateTime end, Exception ex)
        {
            Function = function;
            Args = args;
            Start = start;
            End = end;
            Ex = ex;
        }


    }

    public sealed class OpenAiDebugBatch
    {
        public readonly DateTime Start;
        public DateTime End { get; internal set; }

        internal readonly List<OpenAiCallInstance> Calls = new List<OpenAiCallInstance>();

        public OpenAiDebugBatch(DateTime start)
        {
            Start = start;
        }
    }

    public sealed class OpenAiDebugMessage
    {

        internal void StartBatch()
        {
            var b = new OpenAiDebugBatch(DateTime.UtcNow);
            Batch = b;
            Batches.Add(b);
        }

        internal void EndBatch()
        {
            Batch.End = DateTime.UtcNow;
            Batch = null;
        }


        internal void AddCall(OpenAiCallInstance call)
        {
            Batch.Calls.Add(call);
        }

        OpenAiDebugBatch Batch;
        readonly List<OpenAiDebugBatch> Batches = new List<OpenAiDebugBatch>();


        public String Get(OpenAiDebugInfo debug)
        {
            var p = CultureInfo.InvariantCulture;
            var debugBuilder = new StringBuilder();
            int batchIndex = 0;
            const string I = "    ";
            int callIndex = 0;
            foreach (var b in Batches)
            {
                ++batchIndex;
                var dur = (b.End - b.Start).TotalMilliseconds.ToString("0.###", p);
                debugBuilder.Append(@"### Functions called in batch \#").Append(batchIndex).Append(" *(").Append(dur).AppendLine(" ms)*");
                foreach (var c in b.Calls)
                {
                    ++callIndex;
                    dur = (c.End - c.Start).TotalMilliseconds.ToString("0.###", p);
                    var ex = c.Ex;
                    debugBuilder.Append(callIndex).Append(". ").Append(OpenAiTools.MdEscape(c.Function)).Append(ex == null ? @" *\[ok\]* *(" : @" *\[failed\]* *(").Append(dur).AppendLine(" ms)*  ");
                    if (debug > OpenAiDebugInfo.Overview)
                    {
                        var ta = c.Args?.ToString();
                        if (ex != null)
                        {
                            if (!String.IsNullOrEmpty(ta))
                                debugBuilder.Append(I).AppendLine(@"##### Parameters").Append(I).AppendLine("```javascript").Append(I).AppendLine(OpenAiTools.BeautifyJson(ta, I)).Append(I).AppendLine("```  ");
                            if (debug > OpenAiDebugInfo.Parameters)
                                debugBuilder.Append(I).AppendLine(@"##### Exception").Append(I).AppendLine("```").Append(I).AppendLine(OpenAiTools.Intendent(ex.ToString(), I)).Append(I).AppendLine("```");
                            debugBuilder.AppendLine();
                        }
                        else
                        {
                            if (!String.IsNullOrEmpty(ta))
                                debugBuilder.Append(I).AppendLine(@"##### Parameters").Append(I).AppendLine("```javascript").Append(I).AppendLine(OpenAiTools.BeautifyJson(ta, I)).Append(I).AppendLine("```  ");
                            var res = c.Ret;
                            if ((debug > OpenAiDebugInfo.Parameters) && (!String.IsNullOrEmpty(res)))
                                debugBuilder.Append(I).AppendLine(@"##### Results").Append(I).AppendLine("```javascript").Append(I).AppendLine(OpenAiTools.BeautifyJson(res, I)).Append(I).AppendLine("```");
                            debugBuilder.AppendLine();
                        }
                    }
                }
            }
            if (debugBuilder.Length <= 0)
                return null;
            debugBuilder.AppendLine("### Summary").Append("  ").Append(callIndex).Append(callIndex == 1 ? " call in " : " calls in ").Append(batchIndex).AppendLine(batchIndex == 1 ? " batch." : " batches.");
            return debugBuilder.ToString();
        }



        public bool HaveInfo => Batches.Count > 0;

    }


}
