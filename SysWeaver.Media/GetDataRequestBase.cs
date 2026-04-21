using System;

namespace SysWeaver.MicroService
{
    public abstract class GetDataRequestBase
    {
        public override string ToString() => String.Concat(Width, 'x', Height, " from ", Url);

        /// <summary>
        /// The url to take a snap shot for
        /// </summary>
        public String Url;

        /// <summary>
        /// The width (can be modified if the page is "aware" and control is true)
        /// </summary>
        public int Width = 1920;

        /// <summary>
        /// The height (can be modified if the page is "aware" and control is true)
        /// </summary>
        public int Height = 1080;

    }




}
