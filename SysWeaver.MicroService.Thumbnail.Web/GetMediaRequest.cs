using System;

namespace SysWeaver.MicroService
{
    public abstract class GetMediaRequest : GetDataRequestBase
    {
        /// <summary>
        /// "When" to take the screen shot (time in seconds)
        /// </summary>
        [EditMin(0)]
        [EditDefault(0.5)]
        public double Pos = 0.5;

        internal abstract int Type { get; }
        internal abstract Object Params { get; }
    }


}
