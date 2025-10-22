using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Knowledge
{


    public static class FpsInfo
    {
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
