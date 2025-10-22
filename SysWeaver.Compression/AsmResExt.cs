using System;
using System.IO;
using System.Reflection;

namespace SysWeaver.Compression
{
    public static class AsmResExt
    {

        /// <summary>
        /// Given an uncompressed resource name, find the compressed version (if any) and modify to the true resource name
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if a compressed version is found it's modified to that resource</param>
        /// <returns>The compression type or null</returns>
        public static ICompType FindResource(this Assembly asm, ref String uncompressedName)
        {
            foreach (var t in asm.GetManifestResourceNames())
            {
                if (!t.StartsWith(uncompressedName, StringComparison.Ordinal))
                    continue;
                if (t == uncompressedName)
                    return null;
                var comp = CompManager.GetFromExt(t.Substring(uncompressedName.Length + 1));
                if (comp == null)
                    continue;
                uncompressedName = t;
                return comp;
            }
            uncompressedName = null;
            return null;
        }


        /// <summary>
        /// Determine the compression method used in a resource based on it's extension
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="compressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The compression type or null</returns>
        public static ICompType GetResourceCompression(this Assembly asm, ref String compressedName)
        {
            var f = compressedName.LastIndexOf('.');
            if (f < 0)
                return null;
            var comp = CompManager.GetFromExt(compressedName.Substring(f + 1));
            if (comp != null)
                compressedName = compressedName.Substring(0, f);
            return comp;
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="compressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, ref String compressedName)
        {
            var o = compressedName;
            var comp = GetResourceCompression(asm, ref compressedName);
            using var s = asm.GetManifestResourceStream(o);
            if (comp == null)
            {
                using var ms = new MemoryStream((int)s.Length);
                s.CopyTo(ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }else
            {
                using var ms = new MemoryStream((int)s.Length * 4);
                comp.Decompress(s, ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }


        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, String uncompressedName)
        {
            var comp = FindResource(asm, ref uncompressedName);
            using var s = asm.GetManifestResourceStream(uncompressedName);
            if (comp == null)
            {
                using var ms = new MemoryStream((int)s.Length);
                s.CopyTo(ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
            else
            {
                using var ms = new MemoryStream((int)s.Length * 4);
                comp.Decompress(s, ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get the data of an embedded resource
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The name of the resource</param>
        /// <returns>The data of the resource</returns>
        public static ReadOnlyMemory<Byte> GetResourceData(this Assembly asm, String name)
        {
            using var s = asm.GetManifestResourceStream(name);
            using var ms = new MemoryStream((int)s.Length);
            s.CopyTo(ms);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Get the data of an embedded resource as a byte array
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The name of the resource</param>
        /// <returns>The data of the resource</returns>
        public static Byte[] GetResourceDataBytes(this Assembly asm, String name)
        {
            using var s = asm.GetManifestResourceStream(name);
            using var ms = new MemoryStream((int)s.Length);
            s.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static ReadOnlyMemory<Byte> GetUncompressedResourceData(this Type asmType, String uncompressedName)
        {
            var asm = asmType.Assembly;
            var t = uncompressedName;
            var comp = FindResource(asm, ref t);
            if (t == null)
            {
                t = String.Concat(asmType.Namespace, '.', uncompressedName);
                comp = FindResource(asm, ref t);
            }
            using var s = asm.GetManifestResourceStream(t);
            using var ms = new MemoryStream((int)s.Length);
            if (comp == null)
                s.CopyTo(ms);
            else
                comp.Decompress(s, ms);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static Byte[] GetUncompressedResourceDataBytes(this Type asmType, String uncompressedName)
        {
            var asm = asmType.Assembly;
            var t = uncompressedName;
            var comp = FindResource(asm, ref t);
            if (t == null)
            {
                t = String.Concat(asmType.Namespace, '.', uncompressedName);
                comp = FindResource(asm, ref t);
            }
            using var s = asm.GetManifestResourceStream(t);
            using var ms = new MemoryStream((int)s.Length);
            if (comp == null)
                s.CopyTo(ms);
            else
                comp.Decompress(s, ms);
            return ms.ToArray();
        }

    }


}
