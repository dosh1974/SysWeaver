namespace SysWeaver.Media
{
    public class Svg3dParams
    {
        public SvgColorStyle Face = new SvgColorStyle
        {
            StrokeWidth = 0.5,
            FillColor = "#4ff",
            StrokeColor = "#aff",
        };
        public SvgShadowParams Shadow = new SvgShadowParams();
        public SvgExtrudeParams Extrude = new SvgExtrudeParams();
        public bool IncludeExtrusionInShadow = true;
        public int MaxDecimals = 3;
    }


}