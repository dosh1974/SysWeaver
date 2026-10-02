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

}
