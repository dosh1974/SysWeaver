using SysWeaver.AI;

namespace SysWeaver.Map
{
    /// <summary>
    /// The base style that is applied to all regions that doesn't override it, also holds the shadow settings for extrusions.
    /// </summary>
    public class MapStyleBase : MapStyle
    {
        /// <summary>
        /// Create a base style with default colors (dark fill, light stroke, light text with a dark outline).
        /// </summary>
        public MapStyleBase()
        {
            FillColor = "#444";
            StrokeColor = "#ddd";
            StrokeWidth = 0.25;
            TextSize = "50%";
            Text = "{0}";
            TextOutlineColor = "rgba(0,0,0,0.8)";
            TextOutlineWidth = 2;
            TextColor = "#bbb";
            ToolTip = "{1}";
        }

        /// <summary>
        /// The target CSS color to use for shadowed part of any extrusion, ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [AiOptional]
        [EditColor(EditColorFormat.Css, true)]
        public string ShadowColor = "#001";

        /// <summary>
        /// [0, 1] The intensity of the shadow on the fill of extruded polygons.
        /// The final fill color is interpolated from the fill colour of the region and the shadow color (lerp), by this fraction.
        /// </summary>
        [AiOptional]
        public double ShadowFillStrength = 0.4;

        /// <summary>
        /// [0, 1] The intensity of the shadow on the stroke of extruded polygons.
        /// The final stroke color is interpolated from the fill colour of the region and the shadow color (lerp), by this fraction.
        /// </summary>
        [AiOptional]
        public double ShadowStrokeStrength = 0.3;

    }

}
