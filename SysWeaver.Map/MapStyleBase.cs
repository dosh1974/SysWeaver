using SysWeaver.AI;

namespace SysWeaver.Map
{
    public class MapStyleBase : MapStyle
    {
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
        [OpenAiOptional]
        public string ShadowColor = "#001";

        /// <summary>
        /// [0, 1] The intensity of the shadows.
        /// The final color is interpolated from the fill colour of the region and the shadow color (lerp), by this fraction.
        /// </summary>
        [OpenAiOptional]
        public double ShadowFillStrength = 0.4;

        /// <summary>
        /// [0, 1] The intensity of the shadows.
        /// The final color is interpolated from the fill colour of the region and the shadow color (lerp), by this fraction.
        /// </summary>
        [OpenAiOptional]
        public double ShadowStrokeStrength = 0.3;

    }

}
