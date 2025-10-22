using System;

namespace SysWeaver.Map
{
    public sealed class MapRegionInfo
    {
        /// <summary>
        /// Name of the region
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
        /// Original order
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
