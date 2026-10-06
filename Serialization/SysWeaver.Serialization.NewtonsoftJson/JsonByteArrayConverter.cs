using Newtonsoft.Json;
using System;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Write-only Newtonsoft converter that writes byte arrays as arrays of numbers, 32 per line with right aligned columns, instead of base64 strings.
    /// </summary>
    /// <remarks>
    /// Only works with an <see cref="ExtendedJsonTextWriter"/> (used by <see cref="NewtonsoftJsonSerializer.ToFormattedJson{T}(T)"/>).
    /// The numbers are written as raw text, so the writer state is not updated for each value.
    /// </remarks>
    sealed class JsonByteArrayConverter : JsonConverter
    {

        /// <summary>
        /// The shared instance.
        /// </summary>
        public static readonly JsonByteArrayConverter Instance = new JsonByteArrayConverter();

        /// <summary>
        /// Not supported (<see cref="CanRead"/> is false).
        /// </summary>
        /// <exception cref="NotImplementedException">Always thrown.</exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        /// <summary>
        /// Always false, this converter is write only.
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Returns true for byte arrays.
        /// </summary>
        /// <param name="t">The type to test.</param>
        /// <returns>True if <paramref name="t"/> is a byte array type.</returns>
        public override bool CanConvert(Type t) 
            =>typeof(byte[]).IsAssignableFrom(t);


        /// <summary>
        /// Write a byte array (or null) as a json array of numbers.
        /// Arrays with at most 32 elements are written on a single line, longer arrays are written with 32 numbers per line.
        /// </summary>
        /// <param name="jw">The writer, must be an <see cref="ExtendedJsonTextWriter"/>.</param>
        /// <param name="value">The byte array to write, may be null.</param>
        /// <param name="serializer">The calling serializer (unused).</param>
        public unsafe override void WriteJson(JsonWriter jw, object value, JsonSerializer serializer)
        {
            var writer = jw as ExtendedJsonTextWriter;
            var o = value as Byte[];
            if (o == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteStartArray();
            var l = o.LongLength;
            if (l > 0)
            {
                const int perLine = 32;
                int max = 0;
                for (long i = 0; i < l; ++ i)
                {
                    var v = o[i];
                    if (v <= max)
                        continue;
                    max = v;
                    if (max >= 100)
                        break;
                }
                int maxLen = 1;
                if (max >= 10)
                    maxLen = 2;
                if (max >= 100)
                    maxLen = 3;
                var extra = writer.ExtraIndent;
                bool singleLine = l <= (long)perLine;
                Span<char> temp = stackalloc char[(maxLen + 2) * perLine];
                fixed (char* w = temp)
                fixed (byte* s = o)
                {
                    var source = s;
                    for (long i = 0; i < l; i += perLine)
                    {
                        var take = l - i;
                        if (take > perLine)
                            take = perLine;
                        var dest = w;
                        switch (maxLen)
                        {
                            case 1:
                                for (int p = 0; p < (int)take; ++p)
                                {
                                    int v = *source;
                                    ++source;
                                    dest[0] = (Char)(v + '0');
                                    dest[1] = ',';
                                    dest[2] = ' ';
                                    dest += 3;
                                }
                                break;
                            case 2:
                                for (int p = 0; p < (int)take; ++p)
                                {
                                    int v = *source;
                                    ++source;
                                    dest[0] = ' ';
                                    if (v >= 10)
                                    {
                                        var v10 = v / 10;
                                        dest[0] = (Char)(v10 + '0');
                                        v -= (v10 * 10);
                                    }
                                    dest[1] = (Char)(v + '0');
                                    dest[2] = ',';
                                    dest[3] = ' ';
                                    dest += 4;
                                }
                                break;
                            default:
                                for (int p = 0; p < (int)take; ++p)
                                {
                                    int v = *source;
                                    ++source;
                                    dest[0] = ' ';
                                    dest[1] = ' ';
                                    if (v >= 100)
                                    {
                                        var v100 = v / 100;
                                        dest[0] = (Char)(v100 + '0');
                                        v -= (v100 * 100);
                                        dest[1] = '0';
                                    }
                                    if (v >= 10)
                                    {
                                        var v10 = v / 10;
                                        dest[1] = (Char)(v10 + '0');
                                        v -= (v10 * 10);
                                    }
                                    dest[2] = (Char)(v + '0');
                                    dest[3] = ',';
                                    dest[4] = ' ';
                                    dest += 5;
                                }
                                break;
                        }
                        --dest;
                        if ((i + take) >= l) // Last row: skip new line and comma
                            -- dest;
                        if (!singleLine)
                        {
                            writer.WriteIndent();
                            writer.WriteRaw(extra);
                        }
                        writer.WriteRaw(new string(w, 0, (int)(dest - w)));
                    }
                }
                if (!singleLine)
                    writer.WriteIndent();
            }
            writer.WriteEndArray();
        }
    }
}
