using System;

namespace SysWeaver.Map
{
    /// <summary>
    /// A style that is applied to a single region of a map (overrides the base style).
    /// </summary>
    public sealed class MapRegion : MapStyle
    {
        /// <summary>
        /// The Id of the region to highlight, typically this is the ISO 3166 country code.
        /// </summary>
        public String Id;



    }
}
