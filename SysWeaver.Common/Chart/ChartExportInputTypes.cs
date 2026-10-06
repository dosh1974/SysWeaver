namespace SysWeaver.Chart
{
    /// <summary>
    /// The type of data that the <see cref="IChartExporter.Export"/> method expects
    /// </summary>
    public enum ChartExportInputTypes
    {
        /// <summary>
        /// Expects an object of type ChartJsConfig (from SysWeaver.MicroService.ChartJs) describing the chart
        /// </summary>
        Data = 0,
        /// <summary>
        /// Expects a string containing a base64 encoded data url ("data:image/png;base64,...") with a rendered png image of the chart
        /// </summary>
        Png,
        /// <summary>
        /// Expects a string containing a base64 encoded data url ("data:image/svg+xml;base64,...") with a rendered svg image of the chart
        /// </summary>
        Svg,
    }


}
