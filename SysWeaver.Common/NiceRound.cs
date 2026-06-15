using System.Collections.Generic;

namespace SysWeaver
{
    public static class NiceRound
    {
        /// <summary>
        /// Nice numbers in the [1, 1000) intervall.
        /// </summary>
        public static IReadOnlyList<int> Nice1000 = new int[]
        {
            1,
            2,
            3,
            4,
            5,
            10,
            15,
            20,
            25,
            30,
            40,
            50,
            60,
            70,
            75,
            80,
            90,
            100,
            125,
            150,
            175,
            200,
            225,
            250,
            300,
            350,
            375,
            400,
            450,
            500,
            550,
            600,
            700,
            750,
            800,
            900,
            1000,
        };


        public static long Round(double value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            nice = nice ?? Nice1000;
            var neg = value < 0;
            if (neg)
                value = -value;
            if (value < 0.5)
                return 0;
            int scale = 1;
            double nmd = niceMax;
            while ((value / scale) > nmd)
                scale *= 10;
            double minErr = double.MaxValue;
            long minRes = 0;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long v = nice[i];
                v *= scale;
                var d = value - v;
                if (d < 0)
                    d = -d;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = v;
            }
            return neg ? -minRes : minRes;
        }


        public static long Round(decimal value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            nice = nice ?? Nice1000;
            var neg = value < 0;
            if (neg)
                value = -value;
            if (value < 0.5M)
                return 0;
            int scale = 1;
            decimal nmd = niceMax;
            while ((value / scale) > nmd)
                scale *= 10;
            decimal minErr = decimal.MaxValue;
            long minRes = 0;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long v = nice[i];
                v *= scale;
                var d = value - v;
                if (d < 0)
                    d = -d;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = v;
            }
            return neg ? -minRes : minRes;
        }

        public static long Round(long value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            nice = nice ?? Nice1000;
            var neg = value < 0;
            if (neg)
                value = -value;
            if (value < 1)
                return 0;
            int scale = 1;
            double nmd = niceMax;
            while ((value / scale) > nmd)
                scale *= 10;
            long minErr = long.MaxValue;
            long minRes = 0;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long v = nice[i];
                v *= scale;
                var d = value - v;
                if (d < 0)
                    d = -d;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = v;
            }
            return neg ? -minRes : minRes;
        }




        public static double Uggliness(long value)
        {
            if (value <= 0)
                return 0;

            int count = 0;
            int firstNonZero = -1;
            bool lastIsFive = false;
            while (value > 0)
            {
                if (firstNonZero < 0)
                {
                    var dec = value % 10;
                    if (dec != 0)
                    {
                        lastIsFive = dec == 5;
                        firstNonZero = count;
                    }
                }
                ++count;
                value /= 10;
            }
            return count - firstNonZero - (lastIsFive ? 0.5 : 0);
        }



    }



}


 