using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SysWeaver.Compression;
using SysWeaver.Serialization;

namespace SysWeaver.Map
{
    sealed class MapCache
    {
        public readonly ReadOnlyMemory<Byte> Svg;
        public readonly IReadOnlyDictionary<String, MapRegionInfo> Regions;

        MapCache(ReadOnlyMemory<Byte> svg, IReadOnlyDictionary<string, MapRegionInfo> regions)
        {
            Svg = svg;
            Regions = regions;
        }

        public static int CacheSize => Cache.Count;

        static readonly ConcurrentDictionary<String, MapCache> Cache = new ConcurrentDictionary<string, MapCache>(StringComparer.Ordinal);



        static readonly ICompType Comp = CompManager.GetFromHttp("br");
        static readonly ISerializerType Ser = SerManager.Get("json");
        static readonly Assembly Asm = typeof(MapGen).Assembly;
        static readonly String NsPrefix = typeof(MapGen).Namespace + ".data.";



        public static MapCache Get(String mapName)
        {
            var cache = Cache;
            if (cache.TryGetValue(mapName, out var m))
                return m;
            var rname = NsPrefix + mapName;
            MapRegionInfo[] ri;
            using (var s = Asm.GetManifestResourceStream(rname + ".json.br"))
                ri = Ser.Create<MapRegionInfo[]>(Comp.GetDecompressed(s));
            ReadOnlyMemory<Byte> svg;
            using (var s = Asm.GetManifestResourceStream(rname + ".svg.br"))
                svg = Comp.GetDecompressed(s);
            var d = DictionaryExt.Create(ri.Select(x => Tuple.Create(x.N, x))).Freeze();
            m = new MapCache(svg, d);
            if (!cache.TryAdd(mapName, m))
                m = cache[mapName];
            return m;
        }

        public static MapCache Get(MapSelect mapSelect)
            => Get(MapTools.GetMapName(mapSelect));

    }
}
