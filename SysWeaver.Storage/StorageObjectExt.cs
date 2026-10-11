using CommunityToolkit.HighPerformance;
using System;
using System.IO;
using System.Linq;
using System.Text;
using SysWeaver.Compression;
using SysWeaver.Serialization;

namespace SysWeaver
{
    /// <summary>
    /// Extensions for reading embedded resources relative to a type, transparently decompressing resources stored compressed (ex: "MyText.txt.br").
    /// </summary>
    public static class StorageTypeExt
    {
        /// <summary>
        /// Get a stream to an embedded resource in an assembly based of the type (uses the namespace of the type as a prefix).
        /// If the resource is stored compressed, the stream returned is the uncompressed data.
        /// Ex:
        /// Type namespace = "SysWeaver.Test"
        /// Compressed resource: "MyText.txt.br".
        /// 
        /// Use:
        /// using var s = type.GetManifestResourceStream("MyText.txt");
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return null if not found instead of throwing an error</param>
        /// <returns>A stream to the (uncompressed) resource data, the caller must dispose it. Null if not found and <paramref name="noThrow"/> is true.
        /// Compressed resources are fully decompressed into memory.</returns>
        /// <exception cref="FileNotFoundException">The resource wasn't found (neither uncompressed nor with the file extension of a registered compression method appended) and <paramref name="noThrow"/> is false.</exception>
        public static Stream GetManifestResourceStream(this Type t, string resourceName, bool noThrow = false)
        {
            var asm = t.Assembly;
            var n = String.Concat(t.Namespace, '.', resourceName);
            var res = asm.GetEmbeddedResource();
            if (res.Contains(n))
                return asm.GetManifestResourceStream(n);
            var nd = n + '.';
            foreach (var x in CompManager.Extensions)
            {
                var tn = nd + x;
                if (res.Contains(tn))
                {
                    ReadOnlyMemory<Byte> data;
                    using (var s = asm.GetManifestResourceStream(tn))
                        data = CompManager.GetFromExt(x).GetDecompressed(s);
                    return data.AsStream();
                }
            }
            if (noThrow)
                return null;
            throw new FileNotFoundException(String.Concat("No resource named \"", n, "\" found in assembly \"", asm.FullName, "\".\nResources found:\n", String.Join('\n', res.Select(x => x.ToQuoted()))));
        }

        /// <summary>
        /// Get the data contained in an embedded resource in an assembly based of the type (uses the namespace of the type as a prefix).
        /// If the resource is stored compressed, the data returned is the uncompressed data.
        /// Ex:
        /// Type namespace = "SysWeaver.Test"
        /// Compressed resource: "MyText.bin.br".
        /// 
        /// Use:
        /// var data = type.GetManifestResourceData("MyText.bin");
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return default if not found instead of throwing an error.</param>
        /// <returns>The (uncompressed) data of the embedded resource</returns>
        /// <exception cref="Exception">The resource wasn't found and <paramref name="noThrow"/> is false.</exception>
        public static ReadOnlyMemory<Byte> GetManifestResourceData(this Type t, string resourceName, bool noThrow = false)
        {
            using var s = GetManifestResourceStream(t, resourceName, noThrow);
            if (s == null)
                return default;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }



        /// <summary>
        /// Get the text contained in an embedded resource in an assembly based of the type (uses the namespace of the type as a prefix).
        /// 
        /// Ex:
        /// Type namespace = "SysWeaver.Test"
        /// Compressed resource: "MyText.txt.br".
        /// 
        /// Use:
        /// var text = type.GetManifestResourceText("MyText.txt");
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return null if not found instead of throwing an error</param>
        /// <param name="encoding">The encoding to use, default is to use UTF-8</param>
        /// <returns>The text of the embedded resource, without a leading byte order mark (the preamble of the encoding).
        /// Null if not found (or empty) and <paramref name="noThrow"/> is true.</returns>
        /// <exception cref="Exception">The resource wasn't found and <paramref name="noThrow"/> is false.</exception>
        public static String GetManifestResourceText(this Type t, string resourceName, bool noThrow = false, Encoding encoding = null)
        {
            var s = GetManifestResourceData(t, resourceName, noThrow);
            if (s.IsEmpty)
            {
                if (noThrow)
                    return null;
            }
            return (encoding ?? Encoding.UTF8).GetStringWithoutBom(s.Span);
        }


        /// <summary>
        /// Create an object from the data contained in an embedded resource in an assembly based of the type (uses the namespace of the type as a prefix).
        /// The deserializer to use is based on the file extension.
        /// 
        /// Ex:
        /// Type namespace = "SysWeaver.Test"
        /// Compressed resource: "MyData.json.br".
        /// 
        /// Use:
        /// var data = type.GetManifestResourceObject&lt;MyData&gt;("MyData.json");
        /// </summary>
        /// <typeparam name="T">The type of the stored object</typeparam>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return default if not found (or empty, or if no serializer exists for the extension) instead of throwing an error</param>
        /// <returns>The object stored in the embedded resource</returns>
        /// <exception cref="Exception">The resource wasn't found or no serializer exists for the file extension, and <paramref name="noThrow"/> is false, or deserialization failed.</exception>
        /// <remarks>A leading UTF-8 byte order mark is skipped for text serializers (ex: json).</remarks>
        public static T GetManifestResourceObject<T>(this Type t, string resourceName, bool noThrow = false)
        {
            var s = GetManifestResourceData(t, resourceName, noThrow);
            if (s.IsEmpty)
            {
                if (noThrow)
                    return default;
            }
            var ext = Path.GetExtension(resourceName);
            var ser = SerManager.Get(ext);
            if (ser == null)
            {
                if (noThrow)
                    return default;
                throw new Exception(String.Concat("No serializer could be found for the file extension \"", ext, "\", can't create an object from \"", resourceName, '"'));
            }
            var span = s.Span;
            //  Text files (ex: json) are often saved with a byte order mark that the deserializer doesn't accept
            if ((ser is ITextSerializerType) && span.StartsWith(Utf8Bom))
                span = span.Slice(Utf8Bom.Length);
            return ser.Create<T>(span);
        }

        static ReadOnlySpan<Byte> Utf8Bom => [0xef, 0xbb, 0xbf];

    }


}
