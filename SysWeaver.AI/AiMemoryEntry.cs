using System;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// The header of a memory (everything but the value)
    /// </summary>
    public sealed class AiMemoryEntry
    {

        /// <inheritdoc />
        public override string ToString() => MdTableRow;

        internal String MdTableRow => String.Concat(
            "| ", StringTools.EscapeMD(Key), " | ", StringTools.EscapeMD(new DateTime(Interlocked.Read(ref Last), DateTimeKind.Utc).ToString("yy-MM-dd HH:mm:ss")), " | ", StringTools.EscapeMD(Desc ?? ""), " |");

        /// <summary>
        /// The unique key of the memory
        /// </summary>
        public volatile String Key;

        /// <summary>
        /// When the memory was last accessed (UTC ticks)
        /// </summary>
        public long Last;

        /// <summary>
        /// A short description of the memory
        /// </summary>
        public volatile String Desc;


    }

}
