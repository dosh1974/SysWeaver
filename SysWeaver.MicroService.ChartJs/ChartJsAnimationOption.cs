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
        [OpenAiOptional]
        public double duration = 200;

        /// <summary>
        /// Easing method
        /// </summary>
        [OpenAiOptional]
        public String easing = "easeOutQuart";

        /// <summary>
        /// Delay before starting the animations in milliseconds.
        /// </summary>
        [OpenAiOptional]
        public double? delay;
    }


}
