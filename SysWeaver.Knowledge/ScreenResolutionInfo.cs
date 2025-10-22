using System;
using System.Collections.Generic;

namespace SysWeaver.Knowledge
{
    public static class ScreenResolutionInfo
    {



        public static bool ApproxEqual(double value, double min, double? max = null)
        {
            if (max == null)
                return Math.Abs(value - min) <= (min * 0.001);
            if (value < (min - min * 0.0001))
                return false;
            var m = max ?? 0;
            if (value > (m + m * 0.0001))
                return false;
            return true;
        }

        public static String GetKeyWords(int width, int height)
        {
            if ((width <= 0) || (height <= 0))
                return "";
            var key = String.Join("x", width, height);
            if (ScreenKeys.TryGetValue(key, out var t))
                key = String.Join(',', key, height + "p", t);
            double aspect = (double)width / (double)height;
            String at = null;
            if ((at == null) && ApproxEqual(aspect, 1.0))
                at = "square";
            if ((at == null) && ApproxEqual(aspect, 5.0 / 4.0))
                at = "5:4";
            if ((at == null) && ApproxEqual(aspect, 4.0 / 3.0))
                at = "4:3";
            if ((at == null) && ApproxEqual(aspect, 15.0 / 10.0))
                at = "15:10";
            if ((at == null) && ApproxEqual(aspect, 16.0 / 10.0))
                at = "16:10";
            if ((at == null) && ApproxEqual(aspect, 15.0 / 9.0))
                at = "15:9";
            if ((at == null) && ApproxEqual(aspect, 1.775, 1.8))
                at = "16:9";
            if ((at == null) && ApproxEqual(aspect, 2.0, 2.2))
                at = "18:9";
            if ((at == null) && ApproxEqual(aspect, 2.3, 2.4))
                at = "21:9"; 
            return at == null ? key : String.Join(',', key, at);
        }


        static readonly Dictionary<String, String> ScreenKeys = new Dictionary<String, string>(StringComparer.Ordinal)
        {
            { "640x360", "nHD,ninth HD" },
            { "960x540", "qHD,quarter HD" },
            { "1280x720", "HD" },
            { "1600x900", "HD+" },
            { "1920x1080", "Full HD,FHD,2K" },
            { "2048x1080", "Cinema 2K,2Kz1K,DCI 2K" },
            { "2560x1080", "Ultra wide full HD,UWFHD" },
            { "2560x1440", "Quad HD,QHD,WQHD" },
            { "3200x1800", "Quad HD+,QHD+" },
            { "3440x1440", "Ultra wide quad HD,Ultra wide QHD,UWQHD" },
            { "3840x2160", "Ultra HD,UHD,4K" },
            { "4096x2160", "Cinema 4K,4kx2K,DCI 4K" },
            { "5120x1440", "Dual quad HD,Dual QHD,DQHD" },
            { "5120x2880", "Ultra HD,UHD,5K" },
            { "7680x4320", "Ultra HD,UHD,8K" },
            { "15360x8640", "Ultra HD,UHD,16K" },
        };
    }


}
