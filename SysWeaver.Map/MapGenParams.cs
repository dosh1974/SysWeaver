using System;
using SysWeaver.AI;

namespace SysWeaver.Map
{

    public sealed class MapGenParams : MapSelect
    {

        /// <summary>
        /// The style to apply for all regions that is not specially handled.
        /// </summary>
        [OpenAiOptional]
        public MapStyleBase Base = new MapStyleBase();

        /// <summary>
        /// Regions that should be rendered differently may be specified here.
        /// </summary>
        [OpenAiOptional]
        public MapRegion[] Regions;

        /// <summary>
        /// The horizontal extrude direction and maximum magnitude of any extrusion in pixels.
        /// </summary>
        [OpenAiOptional]
        public double MaxExtrudeX = -10;

        /// <summary>
        /// The vertical extrude direction and maximum magnitude of any extrusion in pixels.
        /// </summary>
        [OpenAiOptional]
        public double MaxExtrudeY = -30;

        /// <summary>
        /// Crop the map to just include the highlighted regions.
        /// If null or no regions are present - no cropping will be done.
        /// If the value is positive, this is the number of pixels for the margin, if it less than zero, it's the percentage of the maximum of the width or height.
        /// </summary>
        [OpenAiOptional]
        public double? CropToRegions;

        /// <summary>
        /// If non-null, a background rectangle with this CSS color is added, ex: ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [OpenAiOptional]
        public String BackgroundColor;


        /// <summary>
        /// The title of this map, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

    }
}
