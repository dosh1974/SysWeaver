using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver
{
    public sealed class HashColors
    {

        public static int SeedFromString(String text)
            => BitConverter.ToInt32(MD5.HashData(Encoding.UTF8.GetBytes(text)));

        public static HashColors AppColors
        {
            get
            {
                var c = InternalAppCols;
                var seed = EnvInfo.AppSeed;
                var name = EnvInfo.AppName;
                if (c != null)
                    if ((name == InternalAppName) && (seed == InternalAppSeed))
                        return c;
                c = new HashColors(name, seed);
                InternalAppCols = c;
                InternalAppName = name;
                InternalAppSeed = seed;
                return c;
            }
        }

        volatile static HashColors InternalAppCols;
        volatile static String InternalAppName;
        volatile static int InternalAppSeed;

        public sealed class Props
        {
            public Props(String name, int seed)
            {
                if (seed == 0)
                    seed = SeedFromString(name);
                Seed = GetRandom(out Hue, out Saturation, out ComplementHue, out AccentHue1, out AccentHue2, seed);
            }

            public Props(int seed)
            {
                Seed = GetRandom(out Hue, out Saturation, out ComplementHue, out AccentHue1, out AccentHue2, seed);
            }
            public readonly double Hue;
            public readonly double Saturation;
            public readonly double ComplementHue;
            public readonly double AccentHue1;
            public readonly double AccentHue2;
            public readonly int Seed;
        }


        public static void GetRandom(out double hue, out double saturation, int seed = 0)
        {
            var rng = new Random(seed);
            int spread = (rng.Next(3) * 5) + 5;
            hue = rng.Next(18) * 360.0 / 18.0;
            saturation = rng.Next(10) * 0.05 + 0.2;
        }

        public static int GetRandom(out double hue, out double saturation, out double complementHue, out double accentHue1, out double accentHue2, int seed = 0)
        {
            var rng = new Random(seed);
            int spread = (rng.Next(3) * 5) + 5;
            hue = rng.Next(18) * 360.0 / 18.0;
            saturation = rng.Next(10) * 0.05 + 0.2;
            var c = 180 + (rng.Next(5) - 2) * 5;
            complementHue = (hue + c) % 360;
            accentHue1 = (hue + c - spread) % 360;
            accentHue2 = (hue + c + spread) % 360;
            return rng.Next();
        }

        public HashColors(String name, int seed = 0) : this(new Props(name, seed))
        {
        }

        public HashColors(Props p) : this(p.Hue, p.Saturation, p.ComplementHue, p.AccentHue1, p.AccentHue2, p.Seed)
        {
        }


        public HashColors RotateHue(double angle)
        {
            angle += 720;
            return new HashColors
            (
                (Hue + angle) % 360,
                Saturation,
                (ComplementHue + angle) % 360,
                (AccentHue1 + angle) % 360,
                (AccentHue2 + angle) % 360,
                Seed
            );
        }

        public HashColors(double hue, double saturation, double complementHue, double accentHue1, double accentHue2, int newSeed)
        {
            Hue = hue;
            Saturation = saturation;
            ComplementHue = complementHue;
            AccentHue1 = accentHue1;
            AccentHue2 = accentHue2;

            Acc1 = GetWeb(accentHue1, saturation, 0.9);
            Acc1Bright = GetWeb(accentHue1, saturation, 1.1);
            Acc1Dark0 = GetWeb(accentHue1, saturation, 0.3);
            Acc1Dark1 = GetWeb(accentHue1, saturation, 0.45);

            Acc2 = GetWeb(accentHue2, saturation, 0.9);
            Acc2Bright = GetWeb(accentHue2, saturation, 1.1);
            Acc2Dark0 = GetWeb(accentHue2, saturation, 0.15);
            Acc2Dark1 = GetWeb(accentHue2, saturation, 0.4);

            var mainSat = saturation + 0.2;
            Main = GetWeb(hue, mainSat, 0.9);
            MainBright = GetWeb(hue, mainSat, 1.1);
            MainDark0 = GetWeb(hue, mainSat, 0.2);
            MainDark1 = GetWeb(hue, mainSat, 0.6);

            Background = GetWeb(complementHue, saturation, 0.1);
            BackgroundDark = GetWeb(complementHue, saturation, 0.05);

            Acc3 = GetWeb((hue + 30) % 360, saturation, 0.7);
            Acc4 = GetWeb((hue + 330) % 360, saturation, 0.7);

            Seed = newSeed;
        }

        public static String GetWeb(double hue, double saturation, double value)
            => "#" + ColorTools.HsvToRgb(hue, saturation, value).ToString("x").PadLeft(6, '0');

        public static String GetWeb(double hue, double saturation, double value, double alpha)
        {
            if (alpha >= 1)
                return GetWeb(hue, saturation, value);
            if (alpha <= 0)
                return "transparent";
            var col = ColorTools.HsvToRgb(hue, saturation, value);
            var r = col >> 16;
            var g = (col >> 8) & 0xff;
            var b = col & 0xff;
            return String.Concat("rgba(", r, ',', g, ',', b, ',', alpha.ToString(CultureInfo.InvariantCulture), ')');
        }




        public static String GetWeb(String text, double value = 0.8)
        {
            GetRandom(out var h, out var s, SeedFromString(text));
            return "#" + ColorTools.HsvToRgb(h, s, value).ToString("x").PadLeft(6, '0');
        }

        public static String GetWeb(String text, double alpha, double value)
        {
            if (alpha >= 1)
                return GetWeb(text, value);
            if (alpha <= 0)
                return "transparent";
            GetRandom(out var h, out var s, SeedFromString(text));
            var col = ColorTools.HsvToRgb(h, s, value);
            var r = col >> 16;
            var g = (col >> 8) & 0xff;
            var b = col & 0xff;
            return String.Concat("rgba(", r, ',', g, ',', b, ',', alpha.ToString(CultureInfo.InvariantCulture), ')');
        }

        public readonly double Hue;
        public readonly double Saturation;
        public readonly double ComplementHue;
        public readonly double AccentHue1;
        public readonly double AccentHue2;
        public readonly int Seed;

        public readonly String Acc1;
        public readonly String Acc1Bright;
        public readonly String Acc1Dark1;
        public readonly String Acc1Dark0;

        public readonly String Acc2;
        public readonly String Acc2Bright;

        public readonly String Acc2Dark1;
        public readonly String Acc2Dark0;

        public readonly String Main;
        public readonly String MainBright;
        public readonly String MainDark0;
        public readonly String MainDark1;

        public readonly String Background;
        public readonly String BackgroundDark;

        public readonly String Acc3;
        public readonly String Acc4;

    }

}
