using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class StreamExt
    {
        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        public static IEnumerable<String> ReadAllLines(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            for (; ;)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                yield return line;
            }
        }


        /// <summary>
        /// Read all text of a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        public static String ReadAllText(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns></returns>
        public static IEnumerable<String> ReadAllLines(this Stream stream, bool leaveOpen = false, Encoding encoding = null)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                yield return line;
            }
        }


        public static async ValueTask<Memory<Byte>> ReadAllMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return new Memory<Byte>(ms.GetBuffer(), 0, (int)ms.Position);
        }

        public static async ValueTask<Byte[]> ReadAllBytesAsync(this Stream stream, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }



    }

}
