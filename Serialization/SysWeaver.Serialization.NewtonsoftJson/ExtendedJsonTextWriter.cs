using Newtonsoft.Json;
using System;
using System.IO;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// A <see cref="JsonTextWriter"/> that exposes indentation helpers, used by <see cref="JsonByteArrayConverter"/> to write raw, aligned byte array rows.
    /// </summary>
    sealed class ExtendedJsonTextWriter : JsonTextWriter
    {
        /// <summary>
        /// Create a writer.
        /// </summary>
        /// <param name="textWriter">The text writer to write to.</param>
        public ExtendedJsonTextWriter(TextWriter textWriter) : base(textWriter)
        {
            ExtraIndent = new String(IndentChar, Indentation);
        }

        /// <summary>
        /// One indentation level (computed from the indentation settings at construction time).
        /// </summary>
        public readonly String ExtraIndent;


        /// <summary>
        /// Write a new line followed by the indentation of the current nesting level.
        /// </summary>
        public new void WriteIndent()
            => base.WriteIndent();
    }
}
