using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// General animation options
    /// </summary>
    public sealed class ChartJsAnimationOption
    {
        /// <summary>
        /// The number of milliseconds an animation takes.
        /// </summary>
        [AiOptional]
        public double duration = 200;

        /// <summary>
        /// Easing method
        /// </summary>
        [AiOptional]
        public String easing = "easeOutQuart";

        /// <summary>
        /// Delay before starting the animations in milliseconds.
        /// </summary>
        [AiOptional]
        public double? delay;
    }


}
