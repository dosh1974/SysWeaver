using GemBox.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SysWeaver.Chart;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Service that provides the Excel chart exporter
    /// </summary>
    public sealed class ChartJsExcelService : IHaveChartExporters
    {

        /// <inheritdoc />
        public override string ToString() => "Margins (inch): " + ChartMarginInch.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="p">Parameters, null uses defaults (negative margins are clamped to 0)</param>
        public ChartJsExcelService(ChartJsExcelParams p)
        {
            p = p ?? new ChartJsExcelParams();
            ChartMarginInch = Math.Max(0.0, p.ChartMarginInch);
        }


        /// <summary>
        /// Margin for chart pages in inches (set by the last created service)
        /// </summary>
        public static double ChartMarginInch { get; private set; } = 0.2;

        /// <inheritdoc />
        public IReadOnlyList<IChartExporter> ChartExporters { get; } = 
            [
                ChartJsExcelExporter.Instance,
                //ChartJsPdfExporter.Instance,
            ];
    }

}
