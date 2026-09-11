using System;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed class AiMemoryEntry
    {

        public override string ToString() => MdTableRow;

        internal String MdTableRow => String.Concat(
            "| ", StringTools.EscapeMD(Key), " | ", StringTools.EscapeMD(new DateTime(Interlocked.Read(ref Last), DateTimeKind.Utc).ToString("yy-MM-dd HH:mm:ss")), " | ", StringTools.EscapeMD(Desc ?? ""), " |");

        public volatile String Key;
        public long Last;
        public volatile String Desc;


    }


    public static class AiMemoryLimits
    {
        public const int MaxRecords = 64;
        public const int MaxKeyLen = 32;
        public const int MaxDescLen = 128;
        public const int MaxContentLen = 4096;
    }

    public class AiMemorySet
    {
        /// <summary>
        /// The unqiue key used to manage this memory.
        /// This is listed in the system prompt.
        /// Max length is 32.
        /// </summary>
        public String Key;
        /// <summary>
        /// The value of this memory, typically MD text.
        /// Max length is 4096.
        /// </summary>
        public String Value;
    }

    public class AiMemoryAdd : AiMemorySet
    {
        /// <summary>
        /// A short textual description of this memory.
        /// This is listed in the system prompt.
        /// Max length is 128.
        /// </summary>
        public String Desc;
    }

}
