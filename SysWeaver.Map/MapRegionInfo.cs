using System;

namespace SysWeaver.Map
{
    /// <summary>
    /// Information about a single region of a map (bounding box, label position etc).
    /// </summary>
    public sealed class MapRegionInfo
    {
        /// <summary>
        /// Name (id) of the region, typically the ISO 3166 country code
        /// </summary>
        public String N;
        
        /// <summary>
        /// Left position of the bounding box
        /// </summary>
        public double X;
        
        /// <summary>
        /// Top position of the bounding box
        /// </summary>
        public double Y;
        
        /// <summary>
        /// Width of the bounding box
        /// </summary>
        public double W;
        
        /// <summary>
        /// Height of the bounding box
        /// </summary>
        public double H;

        /// <summary>
        /// Index of the region in the original map data (original order)
        /// </summary>
        public int I;
        
        /// <summary>
        /// Text horizontal center 
        /// </summary>
        public double CX;
        
        /// <summary>
        /// Text vertical center 
        /// </summary>
        public double CY;
    }
}
