namespace SysWeaver.Media
{
    /// <summary>
    /// The style parameters of <see cref="SvgScene.AddPath"/>: a face, an extrusion and a drop shadow
    /// </summary>
    public class Svg3dParams
    {
        /// <summary>
        /// The style of the face (the path itself), null for no face
        /// </summary>
        public SvgColorStyle Face = new SvgColorStyle
        {
            StrokeWidth = 0.5,
            FillColor = "#4ff",
            StrokeColor = "#aff",
        };
        /// <summary>
        /// The drop shadow, null for no shadow
        /// </summary>
        public SvgShadowParams Shadow = new SvgShadowParams();
        /// <summary>
        /// The extrusion (3D sides), null for no extrusion
        /// </summary>
        public SvgExtrudeParams Extrude = new SvgExtrudeParams();
        /// <summary>
        /// If true (and there is an extrusion), the shadow includes the extrusion
        /// </summary>
        public bool IncludeExtrusionInShadow = true;
        /// <summary>
        /// The maximum number of decimals of the generated coordinates
        /// </summary>
        public int MaxDecimals = 3;
    }


}