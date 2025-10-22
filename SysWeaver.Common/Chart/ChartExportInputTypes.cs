namespace SysWeaver.Chart
{
    /// <summary>
    /// The type of data that the expåort function expects
    /// </summary>
    public enum ChartExportInputTypes
    {
        /// <summary>
        /// Expects an object of type ChartJsConfig 
        /// </summary>
        Data = 0,
        /// <summary>
        /// Expects a byte array with a Png
        /// </summary>
        Png,
        /// <summary>
        /// Expectes a string containing an svg
        /// </summary>
        Svg,
    }


}
