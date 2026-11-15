using SysWeaver.Minifier;

namespace SysWeaver.HttpTransformer
{
    /// <summary>
    /// Parameters for <see cref="SvgTransformer"/>.
    /// </summary>
    public class SvgTransformerParams : CachedTransformerParams
    {
        /// <summary>
        /// If true, missing variants are built while the request waits, else they are built in the background (the original is served meanwhile).
        /// </summary>
        public bool BuildDirect = true;

        /// <summary>
        /// The svg minifier options, null to use <see cref="GetDefault"/>.
        /// </summary>
        public SvgMinifierParams Svg = GetDefault();


        /// <summary>
        /// Get the default minifier options (1 decimal, no bitmap validation).
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static SvgMinifierParams GetDefault() => new SvgMinifierParams
        {
            MaxDecimals = 1,
            BitmapValidation = false,
        };
    
    }


}
