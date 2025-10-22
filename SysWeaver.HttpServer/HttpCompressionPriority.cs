using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    public sealed class HttpCompressionPriority
    {
        public override string ToString() => String.Join(", ", Encoders.Select(x => String.Join(":", x.Item1.HttpCode, x.Item2)));

        HttpCompressionPriority(IReadOnlyList<Tuple<ICompEncoder, CompEncoderLevels>> encoders)
        {
            Encoders = encoders;
        }

        /// <summary>
        /// The encoders to use, most preferable first etc
        /// </summary>
        public readonly IReadOnlyList<Tuple<ICompEncoder, CompEncoderLevels>> Encoders;
        /// The encoder compression level to use, most preferable first etc


        readonly ConcurrentDictionary<String, Tuple<ICompEncoder, CompEncoderLevels>> Matches = new ConcurrentDictionary<string, Tuple<ICompEncoder, CompEncoderLevels>>(StringComparer.Ordinal);


        /// <summary>
        /// Given the supported encodings (usually from the Accept-Encoding header), return the encoder and compression level
        /// </summary>
        /// <param name="supported"></param>
        /// <returns></returns>
        public Tuple<ICompEncoder, CompEncoderLevels> GetEncoder(String supported) => GetEncoder(supported, GetAcceptedEncoders(supported));

        /// <summary>
        /// Given the supported encodings (usually from the Accept-Encoding header), return the encoder and compression level
        /// </summary>
        /// <param name="supported"></param>
        /// <param name="sup"></param>
        /// <returns></returns>
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

        public const String DefaultMethods = "br:Fast, deflate:Fast, gzip:Fast";

        /// <summary>
        /// Get a list of desired encoders and their levels given a string of priorities
        /// </summary>
        /// <param name="methodsAndPerformance">Compresson method and level, ex: "br:Fast, deflate:Balanced"</param>
        /// <returns></returns>
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
                var e = kv[0].Trim().FastToLower();
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
        static readonly ConcurrentDictionary<String, HttpCompressionPriority> InitInstances = new ConcurrentDictionary<string, HttpCompressionPriority>(StringComparer.Ordinal);

        static readonly ConcurrentDictionary<String, HttpCompressionPriority> Instances = new ConcurrentDictionary<string, HttpCompressionPriority>(StringComparer.Ordinal);


        static readonly ConcurrentDictionary<String, IReadOnlySet<String>> EncoderSets = new ConcurrentDictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        static readonly IReadOnlySet<String> Empty = new HashSet<String>().Freeze();

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
                sup.Add(x.Split(';')[0].Trim().FastToLower());
            cs = sup.Freeze();
            s[supported] = cs;
            return cs;
        }


        public static readonly HttpCompressionPriority Default = GetSupportedEncoders(DefaultMethods);



    }
}
