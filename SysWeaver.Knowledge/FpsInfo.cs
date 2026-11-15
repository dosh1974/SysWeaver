using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Knowledge
{


    /// <summary>
    /// Keywords for frame rates
    /// </summary>
    public static class FpsInfo
    {
        /// <summary>
        /// Get keywords describing a frame rate
        /// </summary>
        /// <param name="fps">The frame rate in frames per second</param>
        /// <returns>"30 fps", "60 fps" or "120 fps" if the rate is approximately that, else an empty string</returns>
        public static String GetKeyWords(double fps)
        {
            if (fps <= 0)
                return "";
            if (ScreenResolutionInfo.ApproxEqual(fps, 29.9, 30.0))
                return "30 fps";
            if (ScreenResolutionInfo.ApproxEqual(fps, 59.9, 60.0))
                return "60 fps";
            if (ScreenResolutionInfo.ApproxEqual(fps, 119.9, 120.0))
                return "120 fps";
            return "";
        }
    }


}
