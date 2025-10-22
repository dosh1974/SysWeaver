using System;
using System.Collections.Generic;

namespace SysWeaver.Map
{
    public static class MapTools
    {


        /// <summary>
        /// Get the actual map resource name for a given map selection
        /// </summary>
        /// <param name="map">The selected map to use</param>
        /// <returns>The map resource name</returns>
        public static String GetMapName(MapSelect map)
            => String.Format(MapNames[(int)map.Map], map.MapCountry);


        /// <summary>
        /// Get all regions of a map
        /// </summary>
        /// <param name="map">The map to get regions for</param>
        /// <returns>An enumerable of regions</returns>
        public static IEnumerable<MapRegionInfo> GetRegions(MapSelect map)
        {
            var m = MapCache.Get(map);
            return m?.Regions?.Values.Nullable();
        }

        static readonly String[] MapNames = [
                "world",
                "africa",
                "europe",
                "north-america",
                "{0}",
                "zones_{0}",
            ];

    }
}
