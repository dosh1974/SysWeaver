using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represent one data set (a series of data)
    /// </summary>
    public sealed class ChartSeries
    {
        /// <summary>
        /// The name of this data series
        /// </summary>
        public String Name;
        /// <summary>
        /// For individual color per label, one for each label.
        /// Color for label x is: Colors[x] ?? Color ?? "#888". 
        /// Colors[x] returns null if Colors are null or x is out of bounds or Colors[x] == null.
        /// </summary>
        [OpenAiOptional]
        public String[] Colors;
        /// <summary>
        /// Color of values (unless overrideen in the Colors array).
        /// Color for label x is: Colors[x] ?? Color ?? "#888". 
        /// Colors[x] returns null if Colors are null or x is out of bounds or Colors[x] == null.
        /// </summary>
        [OpenAiOptional]
        public String Color;
        
        /// <summary>
        /// The values (y-axis), one for each label.
        /// </summary>
        public double[] Values;

        /// <summary>
        /// The opacity of the fill area (not border).
        /// </summary>
        [OpenAiOptional]
        public double FillOpacity = 0.6;

        /// <summary>
        /// Width of the border
        /// </summary>
        [OpenAiOptional]
        public double BorderWidth = 2.0;

    }
}
