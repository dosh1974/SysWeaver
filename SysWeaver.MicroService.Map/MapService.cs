using System;
using System.Linq;
using System.Text;
using SysWeaver.AI;
using SysWeaver.Map;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    [OpenAiToolPrefix("")]
    public sealed class MapService : IHaveOpenAiTools
    {
        public MapService() 
        { 
        }

        public override string ToString() => "Cached maps: " + MapGen.CacheSize;

        /// <summary>
        /// Generate a map
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebApiRaw("image/svg+xml")]
        [WebApiClientCacheStatic]
        [WebApiRequestCache(30)]
        public ReadOnlyMemory<Byte> GenerateMap(MapGenParams p)
            => Encoding.UTF8.GetBytes(MapGen.Generate(p));

        /// <summary>
        /// Get an url (svg image) to a customizable map, with the specified paramateres.
        /// Always use the GetMapRegions first to discover what regions can be marked.
        /// </summary>
        /// <param name="map">The data required to generate the map</param>
        /// <param name="request"></param>
        /// <returns>An url to a svg image with the generated map</returns>
        [OpenAiUse]
        [OpenAiTool("🗺️")]
        public String BuildMap(MapGenParams map, HttpServerRequest request)
        {
            var c = request.Properties[OpenAiToolExt.RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return null;
            var data = MapGen.Generate(map);
            return c.AddMessageFile("image/svg+xml", data, map.Title ?? "Map");
        }


        /// <summary>
        /// Get a list of all regions in a map
        /// </summary>
        /// <param name="map">The map to get information about</param>
        /// <returns>An array of all region names (that can be stylized)</returns>
        [OpenAiUse]
        [OpenAiTool("ℹ️")]
        public String[] GetMapRegions(MapSelect map)
            => MapTools.GetRegions(map).Select(x => x.N).ToArray();


    }


}
