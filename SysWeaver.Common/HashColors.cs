using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// A deterministic color theme (web colors) derived from a name and/or seed.
    /// The same name and seed always yields the same colors, used for application themes, favicons, avatars and chart colors.
    /// </summary>
    /// <remarks>
    /// The theme is built from a main hue (one of 18 steps of 20 degrees) and a saturation (0.2 - 0.65),
    /// a complement hue (roughly opposite the main hue) and two accent hues (the complement hue -/+ a spread of 5, 10 or 15 degrees).
    /// All color strings are lower case "#rrggbb" values.
    /// Instances are immutable and thread safe.
    /// </remarks>
    public sealed class HashColors
    {

        /// <summary>
        /// Compute a deterministic seed from some text (the first 4 bytes of the MD5 hash of the UTF-8 encoded text, machine endian).
        /// </summary>
        /// <param name="text">The text to compute a seed from, must not be null</param>
        /// <returns>A seed value (can be any int, including 0 and negative values)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null</exception>
        public static int SeedFromString(String text)
            => BitConverter.ToInt32(MD5.HashData(Encoding.UTF8.GetBytes(text)));

        /// <summary>
        /// The colors for the current application (based on <see cref="EnvInfo.AppName"/> and <see cref="EnvInfo.AppSeed"/>).
        /// The instance is cached and recreated if the app name or seed changes.
        /// </summary>
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

        /// <summary>
        /// The random theme properties (hues and saturation) derived from a seed
        /// </summary>
        public sealed class Props
        {
            /// <summary>
            /// Derive theme properties from a name and / or a seed
            /// </summary>
            /// <param name="name">The name to compute a seed from (only used if <paramref name="seed"/> is 0)</param>
            /// <param name="seed">The seed to use, if 0 the seed is computed from <paramref name="name"/> using <see cref="SeedFromString(string)"/></param>
            /// <exception cref="ArgumentNullException"><paramref name="seed"/> is 0 and <paramref name="name"/> is null</exception>
            public Props(String name, int seed)
            {
                if (seed == 0)
                    seed = SeedFromString(name);
                Seed = GetRandom(out Hue, out Saturation, out ComplementHue, out AccentHue1, out AccentHue2, seed);
            }

            /// <summary>
            /// Derive theme properties from a seed
            /// </summary>
            /// <param name="seed">The seed to use (0 is a valid seed here)</param>
            public Props(int seed)
            {
                Seed = GetRandom(out Hue, out Saturation, out ComplementHue, out AccentHue1, out AccentHue2, seed);
            }
            /// <summary>
            /// The main hue in degrees [0, 340] (multiples of 20)
            /// </summary>
            public readonly double Hue;
            /// <summary>
            /// The base saturation [0.2, 0.65]
            /// </summary>
            public readonly double Saturation;
            /// <summary>
            /// The complement hue in degrees [0, 360)
            /// </summary>
            public readonly double ComplementHue;
            /// <summary>
            /// The first accent hue in degrees [0, 360)
            /// </summary>
            public readonly double AccentHue1;
            /// <summary>
            /// The second accent hue in degrees [0, 360)
            /// </summary>
            public readonly double AccentHue2;
            /// <summary>
            /// A new seed generated from the input seed (NOT the input seed), can be used to derive further random values
            /// </summary>
            public readonly int Seed;
        }


        /// <summary>
        /// Get a deterministic random hue and saturation from a seed (same hue and saturation as the other <see cref="GetRandom(out double, out double, out double, out double, out double, int)"/> overload).
        /// </summary>
        /// <param name="hue">The hue in degrees [0, 340] (multiples of 20)</param>
        /// <param name="saturation">The saturation [0.2, 0.65]</param>
        /// <param name="seed">The seed</param>
        public static void GetRandom(out double hue, out double saturation, int seed = 0)
        {
            var rng = new Random(seed);
            int spread = (rng.Next(3) * 5) + 5;
            hue = rng.Next(18) * 360.0 / 18.0;
            saturation = rng.Next(10) * 0.05 + 0.2;
        }

        /// <summary>
        /// Get deterministic random theme hues and saturation from a seed.
        /// </summary>
        /// <param name="hue">The main hue in degrees [0, 340] (multiples of 20)</param>
        /// <param name="saturation">The saturation [0.2, 0.65]</param>
        /// <param name="complementHue">The complement hue, the main hue + 170 to 190 degrees (in steps of 5)</param>
        /// <param name="accentHue1">The first accent hue, the complement hue - 5, 10 or 15 degrees</param>
        /// <param name="accentHue2">The second accent hue, the complement hue + the same spread</param>
        /// <param name="seed">The seed</param>
        /// <returns>A new seed (the next value of the seeded random generator)</returns>
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

        /// <summary>
        /// Create a color theme from a name and / or a seed
        /// </summary>
        /// <param name="name">The name to compute a seed from (only used if <paramref name="seed"/> is 0)</param>
        /// <param name="seed">The seed to use, if 0 the seed is computed from <paramref name="name"/></param>
        /// <exception cref="ArgumentNullException"><paramref name="seed"/> is 0 and <paramref name="name"/> is null</exception>
        public HashColors(String name, int seed = 0) : this(new Props(name, seed))
        {
        }

        /// <summary>
        /// Create a color theme from some theme properties
        /// </summary>
        /// <param name="p">The theme properties, must not be null</param>
        public HashColors(Props p) : this(p.Hue, p.Saturation, p.ComplementHue, p.AccentHue1, p.AccentHue2, p.Seed)
        {
        }


        /// <summary>
        /// Create a new theme with all hues rotated (saturation and seed are kept)
        /// </summary>
        /// <param name="angle">The angle in degrees to rotate the hues with, should be greater than -720</param>
        /// <returns>A new color theme</returns>
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

        /// <summary>
        /// Create a color theme from explicit hues and saturation (all color strings are computed here)
        /// </summary>
        /// <param name="hue">The main hue in degrees</param>
        /// <param name="saturation">The base saturation [0, 1] (the main colors use this + 0.2)</param>
        /// <param name="complementHue">The complement hue in degrees (used for the background colors)</param>
        /// <param name="accentHue1">The first accent hue in degrees</param>
        /// <param name="accentHue2">The second accent hue in degrees</param>
        /// <param name="newSeed">The value to store in <see cref="Seed"/></param>
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

        /// <summary>
        /// Get a web color from HSV
        /// </summary>
        /// <param name="hue">The hue in degrees</param>
        /// <param name="saturation">The saturation [0, 1]</param>
        /// <param name="value">The value (brightness) [0, 1], larger values are clamped per channel</param>
        /// <returns>A lower case "#rrggbb" color</returns>
        public static String GetWeb(double hue, double saturation, double value)
            => "#" + ColorTools.HsvToRgb(hue, saturation, value).ToString("x").PadLeft(6, '0');

        /// <summary>
        /// Get a web color from HSV and alpha
        /// </summary>
        /// <param name="hue">The hue in degrees</param>
        /// <param name="saturation">The saturation [0, 1]</param>
        /// <param name="value">The value (brightness) [0, 1], larger values are clamped per channel</param>
        /// <param name="alpha">The opacity [0, 1]</param>
        /// <returns>A "#rrggbb" color if alpha &gt;= 1, "transparent" if alpha &lt;= 0, else an "rgba(r,g,b,a)" color (invariant culture)</returns>
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




        /// <summary>
        /// Get a deterministic web color from some text (the hue and saturation are derived from the text)
        /// </summary>
        /// <param name="text">The text to compute the color from, must not be null</param>
        /// <param name="value">The value (brightness) [0, 1]</param>
        /// <returns>A lower case "#rrggbb" color</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null</exception>
        public static String GetWeb(String text, double value = 0.8)
        {
            GetRandom(out var h, out var s, SeedFromString(text));
            return "#" + ColorTools.HsvToRgb(h, s, value).ToString("x").PadLeft(6, '0');
        }

        /// <summary>
        /// Get a deterministic web color with alpha from some text (the hue and saturation are derived from the text)
        /// </summary>
        /// <param name="text">The text to compute the color from, must not be null</param>
        /// <param name="alpha">The opacity [0, 1]</param>
        /// <param name="value">The value (brightness) [0, 1]</param>
        /// <returns>A "#rrggbb" color if alpha &gt;= 1, "transparent" if alpha &lt;= 0, else an "rgba(r,g,b,a)" color (invariant culture)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null and <paramref name="alpha"/> is greater than 0</exception>
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

        /// <summary>
        /// The main hue in degrees
        /// </summary>
        public readonly double Hue;
        /// <summary>
        /// The base saturation
        /// </summary>
        public readonly double Saturation;
        /// <summary>
        /// The complement hue in degrees
        /// </summary>
        public readonly double ComplementHue;
        /// <summary>
        /// The first accent hue in degrees
        /// </summary>
        public readonly double AccentHue1;
        /// <summary>
        /// The second accent hue in degrees
        /// </summary>
        public readonly double AccentHue2;
        /// <summary>
        /// The seed generated when the theme was created (see <see cref="Props.Seed"/>), can be used to derive further random values
        /// </summary>
        public readonly int Seed;

        /// <summary>
        /// First accent color (value 0.9)
        /// </summary>
        public readonly String Acc1;
        /// <summary>
        /// Bright first accent color (value 1.1, clamped)
        /// </summary>
        public readonly String Acc1Bright;
        /// <summary>
        /// Dark first accent color (value 0.45)
        /// </summary>
        public readonly String Acc1Dark1;
        /// <summary>
        /// Darkest first accent color (value 0.3)
        /// </summary>
        public readonly String Acc1Dark0;

        /// <summary>
        /// Second accent color (value 0.9)
        /// </summary>
        public readonly String Acc2;
        /// <summary>
        /// Bright second accent color (value 1.1, clamped)
        /// </summary>
        public readonly String Acc2Bright;

        /// <summary>
        /// Dark second accent color (value 0.4)
        /// </summary>
        public readonly String Acc2Dark1;
        /// <summary>
        /// Darkest second accent color (value 0.15)
        /// </summary>
        public readonly String Acc2Dark0;

        /// <summary>
        /// Main color (main hue, saturation + 0.2, value 0.9)
        /// </summary>
        public readonly String Main;
        /// <summary>
        /// Bright main color (value 1.1, clamped)
        /// </summary>
        public readonly String MainBright;
        /// <summary>
        /// Darkest main color (value 0.2)
        /// </summary>
        public readonly String MainDark0;
        /// <summary>
        /// Dark main color (value 0.6)
        /// </summary>
        public readonly String MainDark1;

        /// <summary>
        /// Background color (complement hue, value 0.1)
        /// </summary>
        public readonly String Background;
        /// <summary>
        /// Dark background color (complement hue, value 0.05)
        /// </summary>
        public readonly String BackgroundDark;

        /// <summary>
        /// Third accent color (main hue + 30 degrees, value 0.7)
        /// </summary>
        public readonly String Acc3;
        /// <summary>
        /// Fourth accent color (main hue - 30 degrees, value 0.7)
        /// </summary>
        public readonly String Acc4;

    }

}
