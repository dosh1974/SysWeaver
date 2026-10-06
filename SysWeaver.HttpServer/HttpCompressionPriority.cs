using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    /// <summary>
    /// An ordered list of compression encoders (and levels) that a handler supports, used to pick the encoder for a request's Accept-Encoding header.
    /// Instances are shared (one per distinct configuration), get them using <see cref="GetSupportedEncoders"/>.
    /// </summary>
    /// <remarks>
    /// Thread safe. The results are cached per distinct Accept-Encoding header value, without any bound.
    /// Quality values ("q=") are ignored, any listed encoding (even "gzip;q=0") is considered accepted, "*" is not supported.
    /// </remarks>
    public sealed class HttpCompressionPriority
    {
        /// <inheritdoc/>
        public override string ToString() => String.Join(", ", Encoders.Select(x => String.Join(":", x.Item1.HttpCode, x.Item2)));

        HttpCompressionPriority(IReadOnlyList<Tuple<ICompEncoder, CompEncoderLevels>> encoders)
        {
            Encoders = encoders;
        }

        /// <summary>
        /// The encoders and compression levels to use, most preferable first.
        /// </summary>
        public readonly IReadOnlyList<Tuple<ICompEncoder, CompEncoderLevels>> Encoders;
        /// The encoder compression level to use, most preferable first etc


        readonly SemiFrozenDictionary<String, Tuple<ICompEncoder, CompEncoderLevels>> Matches = new SemiFrozenDictionary<string, Tuple<ICompEncoder, CompEncoderLevels>>(StringComparer.Ordinal);


        /// <summary>
        /// Given the supported encodings (usually from the Accept-Encoding header), return the encoder and compression level.
        /// </summary>
        /// <param name="supported">The Accept-Encoding header value</param>
        /// <returns>The first of <see cref="Encoders"/> that the client accepts, or null if none is accepted or <paramref name="supported"/> is null or empty</returns>
        public Tuple<ICompEncoder, CompEncoderLevels> GetEncoder(String supported) => GetEncoder(supported, GetAcceptedEncoders(supported));

        /// <summary>
        /// Given the supported encodings (usually from the Accept-Encoding header), return the encoder and compression level.
        /// </summary>
        /// <param name="supported">The Accept-Encoding header value (used as cache key)</param>
        /// <param name="sup">The accepted encodings parsed from <paramref name="supported"/> (see <see cref="GetAcceptedEncoders"/>)</param>
        /// <returns>The first of <see cref="Encoders"/> that the client accepts, or null if none is accepted or <paramref name="supported"/> is null or empty</returns>
        public Tuple<ICompEncoder, CompEncoderLevels> GetEncoder(String supported, IReadOnlySet<String> sup)
        {
            if (String.IsNullOrEmpty(supported))
                return null;
            var m = Matches;
            if (m.TryGetValue(supported, out var val))
                return val;
            if (sup != null)
            {
                foreach (var x in Encoders)
                {
                    if (sup.Contains(x.Item1.HttpCode))
                    {
                        val = x;
                        break;
                    }
                }
            }
            m[supported] = val;
            return val;
        }

        /// <summary>
        /// The default compression preference.
        /// </summary>
        public const String DefaultMethods = "br:Fast, deflate:Fast, gzip:Fast";

        /// <summary>
        /// Get a (shared) instance for a comma separated list of compression methods and levels.
        /// </summary>
        /// <param name="methodsAndPerformance">Compression methods (http codes) and optional levels (<see cref="CompEncoderLevels"/> names, default Fast), ex: "br:Fast, deflate:Balanced".
        /// Unknown methods and duplicates are ignored.</param>
        /// <returns>The instance, or null if the value is null, empty or contains no known methods</returns>
        public static HttpCompressionPriority GetSupportedEncoders(String methodsAndPerformance = DefaultMethods)
        {
            if (String.IsNullOrEmpty(methodsAndPerformance))
                return null;
            var ii = InitInstances;
            if (ii.TryGetValue(methodsAndPerformance, out var inst))
                return inst;
            var m = methodsAndPerformance.Split(',');
            List<Tuple<ICompEncoder, CompEncoderLevels>> encoders = new List<Tuple<ICompEncoder, CompEncoderLevels>>();
            HashSet<ICompEncoder> seen = new HashSet<ICompEncoder>();
            foreach (var x in m)
            {
                var kv = x.Split(":");
                var e = kv[0].FastTrimToLower();
                var c = CompManager.GetFromHttp(e);
                if (c == null)
                    continue;
                if (!seen.Add(c))
                    continue;
                var l = CompEncoderLevels.Fast;
                if (kv.Length > 1)
                    Enum.TryParse<CompEncoderLevels>(kv[1].Trim(), true, out l);
                encoders.Add(Tuple.Create((ICompEncoder)c, l));
            }
            inst = encoders.Count > 0 ? new HttpCompressionPriority(encoders) : null;
            var key = String.Join(',', encoders.Select(x => String.Join(":", x.Item1.HttpCode, x.Item2)));
            var i = Instances;
            if (!i.TryAdd(key, inst))
                inst = i[key];
            ii[methodsAndPerformance] = inst;
            return inst;
        }
        static readonly SemiFrozenDictionary<String, HttpCompressionPriority> InitInstances = new SemiFrozenDictionary<string, HttpCompressionPriority>(StringComparer.Ordinal);

        static readonly SemiFrozenDictionary<String, HttpCompressionPriority> Instances = new SemiFrozenDictionary<string, HttpCompressionPriority>(StringComparer.Ordinal);


        static readonly SemiFrozenDictionary<String, IReadOnlySet<String>> EncoderSets = new SemiFrozenDictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        static readonly IReadOnlySet<String> Empty = new HashSet<String>().Freeze();

        /// <summary>
        /// Parse an Accept-Encoding header value into a set of lower cased encoding names (parameters are stripped, encodings with a zero quality value, ex: "gzip;q=0", are excluded).
        /// </summary>
        /// <param name="supported">The header value, may be null</param>
        /// <returns>The (cached, shared) set, empty if <paramref name="supported"/> is null</returns>
        public static IReadOnlySet<String> GetAcceptedEncoders(String supported)
        {
            if (supported == null)
                return Empty;
            var s = EncoderSets;
            if (s.TryGetValue(supported, out var cs))
                return cs;
            var d = supported.Split(',');
            if (d.Length <= 0)
                return Empty;
            HashSet<String> sup = new(StringComparer.Ordinal);
            foreach (var x in d)
            {
                var p = x.Split(';');
                // An encoding with a quality value of zero is not acceptable (ex: "gzip;q=0")
                bool notAccepted = false;
                for (int i = 1; i < p.Length; ++i)
                {
                    var param = p[i].AsSpan().Trim();
                    if ((param.Length < 2) || ((param[0] != 'q') && (param[0] != 'Q')) || (param[1] != '='))
                        continue;
                    if (Double.TryParse(param.Slice(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var q) && (q <= 0))
                        notAccepted = true;
                }
                if (notAccepted)
                    continue;
                sup.Add(p[0].FastTrimToLower());
            }
            cs = sup.Freeze();
            s[supported] = cs;
            return cs;
        }


        /// <summary>
        /// The instance for <see cref="DefaultMethods"/>.
        /// </summary>
        public static readonly HttpCompressionPriority Default = GetSupportedEncoders(DefaultMethods);



    }
}
