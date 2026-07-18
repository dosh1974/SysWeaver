using Newtonsoft.Json;
using System;
using System.IO;

namespace SysWeaver.Serialization
{
    sealed class ExtendedJsonTextWriter : JsonTextWriter
    {
        public ExtendedJsonTextWriter(TextWriter textWriter) : base(textWriter)
        {
            ExtraIndent = new String(IndentChar, Indentation);
        }

        public readonly String ExtraIndent;


        public new void WriteIndent()
            => base.WriteIndent();
    }
}
