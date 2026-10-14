using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SpanJson;
using SpanJson.Formatters;
using SpanJson.Resolvers;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializer for the "json" extension backed by the SpanJson library.
    /// Has a low priority (-5) so it never displaces the other JSON serializers when they are registered.
    /// </summary>
    /// <remarks>
    /// Serialization and deserialization use the same custom resolver (nulls included, byte arrays as number arrays, enums as integers, original member name casing).
    /// The <see cref="SerializerOptions"/> are ignored (output is always compact).
    /// </remarks>
    public sealed class SpanJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "SpanJson";

        /// <inheritdoc/>
        public string Extension => "json";

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/json";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        /// <inheritdoc/>
        public Encoding Encoding => Encoding.UTF8;

        /// <inheritdoc/>
        public int Prio => -5;

        SpanJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new SpanJsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        sealed class CustomResolver : ResolverBase<Byte, CustomResolver>
        {
            static readonly SpanJsonOptions Options = new SpanJsonOptions
            {
                NullOption = NullOptions.IncludeNulls,
                ByteArrayOption = ByteArrayOptions.Array,
                EnumOption = EnumOptions.Integer,
                NamingConvention = NamingConventions.OriginalCase
            };

            public CustomResolver() : base(Options)
            {
            }
        }

        /// <summary>
        /// The UTF-16 version of <see cref="CustomResolver"/> (same options), used by <see cref="ToString{T}(T, SerializerOptions)"/>.
        /// Uri values are written like the UTF-8 writer does (see <see cref="Utf16UriFormatter"/>).
        /// </summary>
        sealed class CustomUtf16Resolver : ResolverBase<Char, CustomUtf16Resolver>
        {
            static readonly SpanJsonOptions Options = new SpanJsonOptions
            {
                NullOption = NullOptions.IncludeNulls,
                ByteArrayOption = ByteArrayOptions.Array,
                EnumOption = EnumOptions.Integer,
                NamingConvention = NamingConventions.OriginalCase
            };

            public CustomUtf16Resolver() : base(Options)
            {
            }

            /// <summary>
            /// Uri, Uri[] and List&lt;Uri&gt; (the types with built in UTF-16 Uri formatters) use <see cref="Utf16UriFormatter"/>
            /// </summary>
            public override IJsonFormatter GetFormatter(Type type)
            {
                if (type == typeof(Uri))
                    return Utf16UriFormatter.Default;
                if (type == typeof(Uri[]))
                    return ArrayFormatter<Uri, Char, CustomUtf16Resolver>.Default;
                if (type == typeof(List<Uri>))
                    return ListFormatter<List<Uri>, Uri, Char, CustomUtf16Resolver>.Default;
                return base.GetFormatter(type);
            }
        }

        /// <summary>
        /// Writes a Uri as its original string like the UTF-8 writer of SpanJson (the UTF-16 writer of SpanJson writes Uri.ToString(), the unescaped form)
        /// </summary>
        sealed class Utf16UriFormatter : IJsonFormatter<Uri, Char>
        {
            public static readonly Utf16UriFormatter Default = new Utf16UriFormatter();

            public void Serialize(ref JsonWriter<Char> writer, Uri value)
            {
                if (value == null)
                    writer.WriteUtf16Null();
                else
                    writer.WriteUtf16String(value.OriginalString);
            }

            public Uri Deserialize(ref JsonReader<Char> reader)
                => reader.ReadUtf16Uri();
        }

        /// <summary>
        /// Replace every lone (unpaired) surrogate with U+FFFD, like a UTF-8 encode and decode does (the UTF-16 writer of SpanJson keeps them).
        /// </summary>
        /// <param name="s">The text</param>
        /// <returns>The text (the same instance if it has no lone surrogates)</returns>
        static String ReplaceLoneSurrogates(String s)
        {
            var t = s.AsSpan();
            var i = t.IndexOfAnyInRange('\ud800', '\udfff');
            if (i < 0)
                return s;
            if (FirstLoneSurrogate(t, i) < 0)
                return s;
            return String.Create(s.Length, s, static (d, src) =>
            {
                var t = src.AsSpan();
                t.CopyTo(d);
                var i = 0;
                for (; ; )
                {
                    i = FirstLoneSurrogate(t, i);
                    if (i < 0)
                        return;
                    d[i] = '�';
                    ++i;
                }
            });
        }

        /// <summary>
        /// Find the first lone surrogate
        /// </summary>
        /// <param name="t">The text</param>
        /// <param name="i">The index to start at</param>
        /// <returns>The index of the first lone surrogate at or after i, -1 if none</returns>
        static int FirstLoneSurrogate(ReadOnlySpan<Char> t, int i)
        {
            var l = t.Length;
            while (i < l)
            {
                var n = t.Slice(i).IndexOfAnyInRange('\ud800', '\udfff');
                if (n < 0)
                    return -1;
                i += n;
                //  A high surrogate followed by a low surrogate is a valid pair
                if ((t[i] <= '\udbff') && ((i + 1) < l) && Char.IsLowSurrogate(t[i + 1]))
                {
                    i += 2;
                    continue;
                }
                return i;
            }
            return -1;
        }


        /// <inheritdoc/>
        public T Create<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(data);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(data.Span);
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.Generic.Utf8.Serialize<T, CustomResolver>(obj);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Serialized as UTF-16 directly, the same text as decoding the UTF-8 output of <see cref="Serialize{T}(T, SerializerOptions)"/>
        /// (Uri values as their original string, lone surrogates replaced with U+FFFD).
        /// </remarks>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return ReplaceLoneSurrogates(JsonSerializer.Generic.Utf16.Serialize<T, CustomUtf16Resolver>(obj));
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return DeserializeChars<T>(text);
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            //  Null throws as before
            if (text == null)
                return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(Encoding.UTF8.GetBytes(text));
            return DeserializeChars<T>(text);
        }

        /// <inheritdoc/>
        /// <remarks>The same as <see cref="FromString{T}(String)"/> of a (non null) string with this text.</remarks>
        public T FromSpan<T>(ReadOnlySpan<char> text)
        {
            return DeserializeChars<T>(text);
        }

        /// <summary>
        /// The maximum number of UTF-8 bytes to encode on the stack
        /// </summary>
        const int MaxStackBytes = 4096;

        /// <summary>
        /// Deserialize some json text, the text is UTF-8 encoded on the stack if it's small (else to a new array), same bytes as Encoding.UTF8.GetBytes(new String(text))
        /// </summary>
        /// <typeparam name="T">The type to deserialize</typeparam>
        /// <param name="text">The json text</param>
        /// <returns>The deserialized object</returns>
        static T DeserializeChars<T>(ReadOnlySpan<char> text)
        {
            var enc = Encoding.UTF8;
            var n = enc.GetByteCount(text);
            if (n > MaxStackBytes)
            {
                var a = new Byte[n];
                enc.GetBytes(text, a);
                return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(a);
            }
            Span<Byte> b = stackalloc Byte[n];
            enc.GetBytes(text, b);
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(b);
        }
    }
}
