using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace SysWeaver.AI
{
    /// <summary>
    /// Helpers used by the compiled workflow delegates to write inputs and read outputs.
    /// Writers: All methods named "WriteInput" are used for the exact type of the third parameter.
    /// Writers ignore null values (the workflow default is used).
    /// </summary>
    static class ComfyUiJson
    {
        #region Write inputs

        public static void WriteInput(Utf8JsonWriter w, String name, String v)
        {
            if (v != null)
                w.WriteString(name, v);
        }

        public static void WriteInput(Utf8JsonWriter w, String name, bool v) => w.WriteBoolean(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, sbyte v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, byte v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, short v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, ushort v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, int v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, uint v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, long v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, ulong v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, float v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, double v) => w.WriteNumber(name, v);
        public static void WriteInput(Utf8JsonWriter w, String name, decimal v) => w.WriteNumber(name, v);

        public static void WriteInput(Utf8JsonWriter w, String name, byte[] v)
        {
            if (v != null)
                WriteFile(w, name, null, v, null);
        }

        public static void WriteInput(Utf8JsonWriter w, String name, ComfyUiFile v)
        {
            if (v != null)
                WriteFile(w, name, v.Name, v.Data, v.Url);
        }

        public static void WriteInput(Utf8JsonWriter w, String name, Uri v)
        {
            if (v != null)
                WriteFile(w, name, null, null, v.ToString());
        }

        public static void WriteEnum<E>(Utf8JsonWriter w, String name, E v) where E : struct, Enum
            => w.WriteString(name, v.ToString());

        public static void WriteObject<V>(Utf8JsonWriter w, String name, V v)
        {
            if (v == null)
                return;
            w.WritePropertyName(name);
            JsonSerializer.Serialize(w, v);
        }

        static void WriteFile(Utf8JsonWriter w, String name, String fileName, byte[] data, String url)
        {
            w.WriteStartObject(name);
            w.WriteString("type", "file");
            if (data != null)
            {
                w.WriteBase64String("content", data);
                w.WriteString("name", String.IsNullOrEmpty(fileName) ? GetFileName(data) : fileName);
            }
            else
            {
                w.WriteString("url", url);
                if (!String.IsNullOrEmpty(fileName))
                    w.WriteString("name", fileName);
            }
            w.WriteEndObject();
        }

        /// <summary>
        /// ComfyUI-Connect caches input files by name, so the name must be unique for the content
        /// </summary>
        static String GetFileName(byte[] data)
            => String.Concat("sw_", Convert.ToHexStringLower(SHA256.HashData(data), 0, 16), GetExt(data));

        static String GetExt(ReadOnlySpan<byte> d)
        {
            if (d.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4e, 0x47]))
                return ".png";
            if (d.StartsWith((ReadOnlySpan<byte>)[0xff, 0xd8, 0xff]))
                return ".jpg";
            if (d.StartsWith("GIF8"u8))
                return ".gif";
            if (d.StartsWith("BM"u8))
                return ".bmp";
            if (d.Length >= 12)
            {
                if (d.StartsWith("RIFF"u8))
                {
                    var t = d.Slice(8, 4);
                    if (t.SequenceEqual("WEBP"u8))
                        return ".webp";
                    if (t.SequenceEqual("WAVE"u8))
                        return ".wav";
                }
                if (d.Slice(4, 4).SequenceEqual("ftyp"u8))
                    return ".mp4";
            }
            if (d.StartsWith("fLaC"u8))
                return ".flac";
            if (d.StartsWith("OggS"u8))
                return ".ogg";
            if (d.StartsWith("ID3"u8))
                return ".mp3";
            return ".bin";
        }

        #endregion//Write inputs

        #region Read outputs

        //  An output is a single base64 string, or an array of base64 strings if there are multiple (or zero) results

        public static String ReadString(JsonElement r, String tag)
        {
            if (!r.TryGetProperty(tag, out var e))
                return null;
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    return e.GetString();
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray())
                        if (x.ValueKind == JsonValueKind.String)
                            return x.GetString();
                    return null;
            }
            return null;
        }

        public static String[] ReadStringArray(JsonElement r, String tag)
        {
            if (!r.TryGetProperty(tag, out var e))
                return null;
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    return [e.GetString()];
                case JsonValueKind.Array:
                    return e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray();
            }
            return null;
        }

        public static List<String> ReadStringList(JsonElement r, String tag)
        {
            var a = ReadStringArray(r, tag);
            return a == null ? null : new List<String>(a);
        }

        public static byte[] ReadBytes(JsonElement r, String tag)
        {
            if (!r.TryGetProperty(tag, out var e))
                return null;
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    return e.GetBytesFromBase64();
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray())
                        if (x.ValueKind == JsonValueKind.String)
                            return x.GetBytesFromBase64();
                    return null;
            }
            return null;
        }

        public static byte[][] ReadBytesArray(JsonElement r, String tag)
        {
            if (!r.TryGetProperty(tag, out var e))
                return null;
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    return [e.GetBytesFromBase64()];
                case JsonValueKind.Array:
                    return e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetBytesFromBase64()).ToArray();
            }
            return null;
        }

        public static List<byte[]> ReadBytesList(JsonElement r, String tag)
        {
            var a = ReadBytesArray(r, tag);
            return a == null ? null : new List<byte[]>(a);
        }

        #endregion//Read outputs
    }

}
