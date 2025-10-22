using SysWeaver.Compression;
using SysWeaver.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Buffers;

namespace SysWeaver.Net
{
    public sealed class ApiIoParams
    {

        const String JsonMime = "application/json";
        const String XmlMime = "application/xml";
        const string UrlMime = "application/x-www-form-urlencoded";

        public readonly ISerializerType CopySerializer = SerManager.Get("json");


        /// <summary>
        /// Valid output serializers, the key is the mime type (all lowercased)
        /// </summary>
        public readonly IReadOnlyDictionary<String, ISerializer> OutputSerializers;

        /// <summary>
        /// Valid input serializers, the key is the extension, mime etc (all lowercased)
        /// </summary>
        public readonly IReadOnlyDictionary<String, IDeserializer> InputSerializers;

        public readonly ISerializer DefaultOutput;
        public readonly IDeserializer DefaultInput;

        public readonly IDeserializer JsonDeSer;
        public readonly IDeserializer UriDeSer;

        public readonly IReadOnlyDictionary<Char, IDeserializer> SerMapper;


        readonly ConcurrentDictionary<String, ISerializer> Ac = new ConcurrentDictionary<string, ISerializer>(StringComparer.Ordinal);

        public ISerializer GetSerializer(String accept)
        {
            if (accept == null)
                return DefaultOutput;
            var c = Ac;
            if (c.TryGetValue(accept, out var s))
                return s;
            var a = accept.FastToLower();
            var o = OutputSerializers;
            foreach (var x in a.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var k = x.SplitFirst(';');
                if (o.TryGetValue(k, out s))
                    break;
            }
            s = s ?? DefaultOutput;
            c.TryAdd(accept, s);
            return s;
        }

        static IReadOnlyDictionary<String, T> Get<T>(IReadOnlyList<T> l) where T : ISerializerInfo
        {
            var t = new Dictionary<String, T>(StringComparer.Ordinal);
            foreach (var s in l)
            {
                var mime = s.Mime;
                t[mime] = s;
                t[s.MimeHeader] = s;
                t.TryAdd(s.Extension, s);
            }
            return t.Freeze();

        }

        public ApiIoParams(
            IReadOnlyList<IDeserializer> inputSerializers,
            IReadOnlyList<ISerializer> outputSerializers,
            IDeserializer defaultInput = null,
            ISerializer defaultOutput = null)
            
        {
            OutputSerializers = Get(outputSerializers);
            if (defaultOutput == null)
                if (!OutputSerializers.TryGetValue(JsonMime, out defaultOutput))
                    defaultOutput = outputSerializers.FirstOrDefault();
            DefaultOutput = defaultOutput;

            var t = Get(inputSerializers);
            InputSerializers = t;
            t.TryGetValue(JsonMime, out JsonDeSer);
            t.TryGetValue(UrlMime, out UriDeSer);
            var serMapper = new Dictionary<Char, IDeserializer>();
            foreach (var x in Formats)
            {
                var mime = x.Value;
                t.TryGetValue(mime, out var ser);
                serMapper.Add(x.Key, ser);
            }
            SerMapper = serMapper.Freeze();
            DefaultInput = defaultInput ?? JsonDeSer ?? t.FirstOrDefault().Value;
        }


        static IReadOnlyDictionary<Char, ValueTuple<ICompType, bool>> GetDecomp()
        {
            Dictionary<Char, ValueTuple<ICompType, bool>> decomp = new Dictionary<char, ValueTuple<ICompType, bool>>();
            void Add(Char c, String http)
            {
                var s = CompManager.GetFromHttp(http);
                if (s == null)
                    return;
                decomp.Add(c, ValueTuple.Create(s, false));
                decomp.Add(Char.ToUpper(c), ValueTuple.Create(s, true));
            }
            Add('d', "deflate");
            Add('g', "gzip");
            Add('b', "br");
            Add('z', "zstd");
            decomp.Add('u', new ValueTuple<ICompType, bool>(null, false));
            decomp.Add('U', new ValueTuple<ICompType, bool>(null, true));
            return decomp.Freeze();
        }

        static readonly IReadOnlyDictionary<Char, ValueTuple<ICompType, bool>> Decomp = GetDecomp();

        static readonly IReadOnlyDictionary<Char, String> Formats = new Dictionary<Char, String>()
        {
            { '{', JsonMime },
            { '[', JsonMime },
            { '"', JsonMime },
            { '\'', JsonMime },
            { '-', JsonMime },
            { '+', JsonMime },
            { '.', JsonMime },
            { '0', JsonMime },
            { '1', JsonMime },
            { '2', JsonMime },
            { '3', JsonMime },
            { '4', JsonMime },
            { '5', JsonMime },
            { '6', JsonMime },
            { '7', JsonMime },
            { '8', JsonMime },
            { '9', JsonMime },
            { '<', XmlMime },
        }.Freeze();


        static readonly IReadOnlyDictionary<String, String> Consts = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { "null", "null" },
            { "nan", "0" },
            { "undefined", "null" },
            { "true", "true" },
            { "false", "false" },

        }.Freeze();


        struct FixBase64State
        {
            public readonly String Src;
            public readonly int Start;

            public FixBase64State(string src, int start)
            {
                Src = src;
                Start = start;
            }
        }


        static void WriteFixBase64(Span<Char> to, FixBase64State data)
        {
            var str = data.Src;
            var start = data.Start;
            var strLen = str.Length;
            int d = 0; 
            for (int i = start; i < strLen; ++ i)
            {
                var c = str[i];
                if (c == '-')
                    c = '+';
                if (c == '_')
                    c = '/';
                to[d] = c;
                ++d;
            }
            var len = to.Length;
            while (d < len)
            {
                to[d] = '=';
                ++d;
            }
        }

        static readonly SpanAction<Char, FixBase64State> WriteFixBase64Action = WriteFixBase64;




        static ReadOnlyMemory<Byte> FromText(String text, int start)
        {
            var ttl = text.Length;
            var tl = text.Length - start;
            bool needPadding = (tl & 3) != 0;
            bool needFix = false;
            if (!needPadding)
            {
                for (int i = start; i < ttl; ++i)
                {
                    var c = text[i];
                    needFix |= (c == '-');
                    needFix |= (c == '_');
                    if (needFix)
                        break;
                }
            }
            if (needPadding || needFix)
            {
                var nl = needPadding ? ((tl + 3) & ~3) : tl;
                text = String.Create(nl, new FixBase64State(text, start), WriteFixBase64Action);
                tl = nl;
                start = 0;
            }
            tl += (tl << 1);
            tl += 7;
            tl >>= 2;
            var temp = GC.AllocateUninitializedArray<Byte>(tl);
            Convert.TryFromBase64Chars(text.AsSpan().Slice(start), temp.AsSpan(), out var b);
            return new ReadOnlyMemory<Byte>(temp, 0, b);
        }

        T GetBinary<T>(String text)
        {
            if (!Decomp.TryGetValue(text[1], out var z))
                throw new Exception(String.Concat("Don't know how to handle binary data of type '", text[1], '\''));
            int i = 2;
            IDeserializer ser;
            if (z.Item2)
            {
                i = text.IndexOf(',', i);
                if (i < 0)
                    throw new Exception(String.Concat("Don't know how to handle binary data \"", text.LimitLength(20), '"'));
                var ext = text.Substring(2, i - 2).FastToLower();
                if (!InputSerializers.TryGetValue(ext, out ser))
                    throw new Exception(String.Concat("Can't find an enabled serializer named \"", ext, '"'));
                ++i;
            }else
            {
                ser = JsonDeSer;
                if (ser == null)
                    throw new Exception(String.Concat("Can't find an enabled json serializer"));
            }
            var data = FromText(text, i);
            var comp = z.Item1;
            if (comp != null)
                data = comp.GetDecompressed(data.Span);
            var vd = ser.Create<T>(data);
            return vd;
        }

        T GetText<T>(String text, IDeserializer ser)
        {
            var ts = ser as ITextDeserializer;
#if DEBUG
            if (ts == null)
                throw new Exception(String.Concat("The \"", ser.Name, "\" isn't a ITextDeserializer which is required"));
#endif//DEBUG
            var v = ts.FromString<T>(text);
            return v;
        }

        public T Get<T>(String text)
        {
            var tl = text.Length;
            if (tl <= 0)
                return default(T);
            text = Uri.UnescapeDataString(text);
            var c = text[0];
            if (c == '_')
                return GetBinary<T>(text);
            if (!SerMapper.TryGetValue(c, out var ser))
            {
                if (Consts.TryGetValue(text, out var val))
                {
                    ser = JsonDeSer;
                    if (ser == null)
                        throw new Exception("No enabled json deserializer");
                    text = val;
                }else
                {
                    if (!Char.IsLetter(c))
                        throw new Exception(String.Concat("Don't know how to handle API parameters \"", text, '"'));
                    ser = UriDeSer;
                    if (ser == null)
                        throw new Exception("No enabled uri deserializer");
                }
            }
            if (ser == null)
            { 
                if (Formats.TryGetValue(c, out var mime))
                    throw new Exception(String.Concat("No enabled serializer for \"", mime, '"'));
                else
                    throw new Exception(String.Concat("No serializer defined for '", c, '\''));
            }
            return GetText<T>(text, ser);
        }

    }





}
