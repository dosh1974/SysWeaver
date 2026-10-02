using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsGridOptions
    {
        /// <summary>
        /// The color of the grid lines
        /// </summary>
        [AiOptional]
        public String color;

        /// <summary>
        /// If false, do not display grid lines for this axis.
        /// </summary>
        [AiOptional]
        public bool display = true;
    }


}
