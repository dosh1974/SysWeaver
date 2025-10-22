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

    public sealed class ChartJsExcelService : IHaveChartExporters
    {

        public override string ToString() => "Margins (inch): " + ChartMarginInch.ToString("0.####", CultureInfo.InvariantCulture);

        public ChartJsExcelService(ChartJsExcelParams p)
        {
            p = p ?? new ChartJsExcelParams();
            ChartMarginInch = Math.Max(0.0, p.ChartMarginInch);
        }


        /// <summary>
        /// Margin for table pages in inches
        /// </summary>
        public static double ChartMarginInch { get; private set; } = 0.2;

        public IReadOnlyList<IChartExporter> ChartExporters { get; } = 
            [
                ChartJsExcelExporter.Instance,
                //ChartJsPdfExporter.Instance,
            ];
    }

}
