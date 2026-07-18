using Newtonsoft.Json;
using System;

namespace SysWeaver.Serialization
{
    sealed class JsonByteArrayConverter : JsonConverter
    {

        public static readonly JsonByteArrayConverter Instance = new JsonByteArrayConverter();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override bool CanRead => false;

        public override bool CanConvert(Type t) 
            =>typeof(byte[]).IsAssignableFrom(t);


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
