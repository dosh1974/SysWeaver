using System;

namespace SysWeaver.AI
{
#pragma warning disable CS0649

    sealed class AiTimeInterval
    {
        /// <summary>
        /// The start time
        /// </summary>
        public DateTime From;

        /// <summary>
        /// The end time
        /// </summary>
        public DateTime To;

        /// <summary>
        /// What to return
        /// </summary>
        [AiOptional]
        public AiTimeIntervalUnits ReturnUnit;
    }

#pragma warning restore CS0649


}
