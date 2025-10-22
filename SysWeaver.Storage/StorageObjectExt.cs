using CommunityToolkit.HighPerformance;
using System;
using System.IO;
using System.Linq;
using System.Text;
using SysWeaver.Compression;
using SysWeaver.Serialization;

namespace SysWeaver
{
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
        /// <returns>A stream to the (uncompressed) resource data.</returns>
        /// <exception cref="Exception"></exception>
        public static Stream GetManifestResourceStream(this Type t, string resourceName, bool noThrow = false)
        {
            var asm = t.Assembly;
            var n = String.Concat(t.Namespace, '.', resourceName);
            var res = asm.GetEmbeddedResource();
            if (res.Contains(n))
                return asm.GetManifestResourceStream(n);
            n += '.';
            foreach (var x in CompManager.Extensions)
            {
                var tn = n + x;
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
            throw new Exception(String.Concat("No resource named \"", n, "\" found in assembly \"", asm.FullName, "\".\nResources found:\n", String.Join('\n', res.Select(x => x.ToQuoted()))));
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
        /// <param name="noThrow">If true, return default if not found instead of throwing an error</param>
        /// <returns>The (uncompressed) data of the embedded resource</returns>
        public static ReadOnlyMemory<Byte> GetManifestResourceData(this Type t, string resourceName, bool noThrow = false)
        {
            using var s = GetManifestResourceStream(t, resourceName, noThrow);
            if (noThrow)
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
        /// var text = type.GetManifestResourceData("MyText.txt");
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return default if not found instead of throwing an error</param>
        /// <param name="encoding">The encoding to use, default is to use UTF-8</param>
        /// <returns>The text of the embedded resource</returns>
        public static String GetManifestResourceText(this Type t, string resourceName, bool noThrow = false, Encoding encoding = null)
        {
            var s = GetManifestResourceData(t, resourceName, noThrow);
            if (s.IsEmpty)
            {
                if (noThrow)
                    return null;
            }
            return (encoding ?? Encoding.UTF8).GetString(s.Span);
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
        /// var data = type.GetManifestResourceData("MyData.json");
        /// </summary>
        /// <typeparam name="T">The type of the stored object</typeparam>
        /// <param name="t">The type</param>
        /// <param name="resourceName">Name of the resource (without name space)</param>
        /// <param name="noThrow">If true, return default if not found instead of throwing an error</param>
        /// <returns>The object stored in the embedded resource</returns>
        /// <exception cref="Exception"></exception>
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
            return ser.Create<T>(s.Span);
        }

    }


}
