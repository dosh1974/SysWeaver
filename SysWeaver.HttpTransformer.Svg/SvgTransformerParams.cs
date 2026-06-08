using SysWeaver.Minifier;

namespace SysWeaver.HttpTransformer
{
    public class SvgTransformerParams : CachedTransformerParams
    {
        public bool BuildDirect = true;

        public SvgMinifierParams Svg = GetDefault();


        public static SvgMinifierParams GetDefault() => new SvgMinifierParams
        {
            MaxDecimals = 1,
            BitmapValidation = false,
        };
    
    }


}
