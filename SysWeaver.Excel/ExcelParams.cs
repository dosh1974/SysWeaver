namespace SysWeaver.Excel
{
    /// <summary>
    /// Parameters for the <see cref="ExcelService"/>, the inherited api key (or credentials file) is the GemBox.Spreadsheet license.
    /// A key is required (a missing key or key file throws when the service is created); use the api key "FREE-LIMITED-KEY" for GemBox's free, limited version
    /// </summary>
    public sealed class ExcelParams : ApiKeyParams
    {
        /// <summary>
        /// Margin for table pages in inches
        /// </summary>
        public double TableMarginInch = 0.2;
    }

}
