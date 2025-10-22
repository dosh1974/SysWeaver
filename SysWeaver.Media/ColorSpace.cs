using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{
    public static class ColorSpace
    {


        /// <summary>
        /// Convert from OkLch to a CSS color.
        /// </summary>
        /// <param name="il">[0, 1] Luminosity</param>
        /// <param name="ic">[0, 1] Chroma</param>
        /// <param name="ih">[0, 2pi) Hue</param>
        /// <returns>A css color, ex: #12aa42</returns>
        public static String LightnessChromaHueToCss(double il, double ic, double ih)
        {
            return "#" + LightnessChromaHueToUInt32(il, ic, ih).ToString("x").PadLeft(6, '0');
        }

        /// <summary>
        /// Convert from OkLch to a 0x00rrggbb value encoded in an UInt32
        /// </summary>
        /// <param name="il">[0, 1] Luminosity</param>
        /// <param name="ic">[0, 1] Chroma</param>
        /// <param name="ih">[0, 2pi) Hue</param>
        /// <returns></returns>
        public static UInt32 LightnessChromaHueToUInt32(double il, double ic, double ih)
        {
            LightnessChromaHueToSrgb(out var r, out var g, out var b, il, ic, ih);
            r *= 255;
            g *= 255;
            b *= 255;
            if (r < 0)
                r = 0;
            if (g < 0)
                g = 0;
            if (b < 0)
                b = 0;
            uint rr = (uint)Math.Round(r);
            uint gg = (uint)Math.Round(g);
            uint bb = (uint)Math.Round(b);
            if (rr > 255)
                rr = 255;
            rr <<= 16;
            if (gg > 255)
                gg = 255;
            gg <<= 8;
            if (bb > 255)
                bb = 255;
            rr |= gg;
            rr |= bb;
            return rr;
        }

        /// <summary>
        /// Convert from OkLch to srgb components.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="l">[0, 1] Luminosity</param>
        /// <param name="c">[0, 1] Chroma</param>
        /// <param name="h">[0, 2pi) Hue</param>
        public static void LightnessChromaHueToSrgb(out double r, out double g, out double b, double l, double c, double h)
        {
            OkLchToOkLab(out r, out g, out b, l, c, h);
            OkLabToLinearRgb(out r, out g, out b, r, g, b);
            r = LinearRgbToSrgbComponent(r);
            g = LinearRgbToSrgbComponent(g);
            b = LinearRgbToSrgbComponent(b);
        }


        /// <summary>
        /// Convert from OkHsv to a CSS color.
        /// </summary>
        /// <param name="h">[0, 2pi) Hue</param>
        /// <param name="s">[0, 1] Saturation</param>
        /// <param name="v">[0, 1] Value</param>
        /// <returns>A css color, ex: #12aa42</returns>
        public static String OkHsvToCss(double h, double s, double v)
        {
            return "#" + OkHsvToUInt32(h, s, v).ToString("x").PadLeft(6, '0');
        }

        /// <summary>
        /// Convert from OkHsv to a 0x00rrggbb value encoded in an UInt32
        /// </summary>
        /// <param name="h">[0, 2pi) Hue</param>
        /// <param name="s">[0, 1] Saturation</param>
        /// <param name="v">[0, 1] Value</param>
        /// <returns></returns>
        public static UInt32 OkHsvToUInt32(double h, double s, double v)
        {
            OkHsvToSrgb(out var r, out var g, out var b, h, s, v);
            r *= 255;
            g *= 255;
            b *= 255;
            if (r < 0)
                r = 0;
            if (g < 0)
                g = 0;
            if (b < 0)
                b = 0;
            uint rr = (uint)Math.Round(r);
            uint gg = (uint)Math.Round(g);
            uint bb = (uint)Math.Round(b);
            if (rr > 255)
                rr = 255;
            rr <<= 16;
            if (gg > 255)
                gg = 255;
            gg <<= 8;
            if (bb > 255)
                bb = 255;
            rr |= gg;
            rr |= bb;
            return rr;
        }


        /// <summary>
        /// Convert from OkLch to srgb components.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="h">[0, 2pi) Hue</param>
        /// <param name="s">[0, 1] Saturation</param>
        /// <param name="v">[0, 1] Value</param>
        public static void OkHsvToSrgb(out double r, out double g, out double b, double h, double s, double v)
        {
            OkHsvToOkLab(out r, out g, out b, h, s, v);
            OkLabToLinearRgb(out r, out g, out b, r, g, b);
            r = LinearRgbToSrgbComponent(r);
            g = LinearRgbToSrgbComponent(g);
            b = LinearRgbToSrgbComponent(b);
        }


        /// <summary>
        /// Convert from OkLch to OkLab
        /// </summary>
        /// <param name="ol">Luminosity</param>
        /// <param name="oa">a (how green/red the color is)</param>
        /// <param name="ob">b (how blue/yellow the color is)</param>
        /// <param name="il">[0, 1] Luminosity</param>
        /// <param name="ic">[0, 1] Chroma</param>
        /// <param name="ih">[0, 2pi) Hue</param>
        public static void OkLchToOkLab(out double ol, out double oa, out double ob, double il, double ic, double ih)
        {
            ol = il;
            oa = Math.Cos(ih) * ic;
            ob = Math.Sin(ih) * ic;
        }


        public static void OkHsvToOkLab(out double ol, out double oa, out double ob, double h, double s, double v)
        {
            var a_ = Math.Cos(h);
            var b_ = Math.Sin(h);
            find_cusp(out var cl, out var cc, a_, b_);
            var S_max = cc / cl;
            var T_max = cc / (1.0 - cl);
            double S_0 = 0.5;
            var k = 1.0 - S_0 / S_max;

            // first we compute L and V as if the gamut is a perfect triangle:

            // L, C when v==1:
            var L_v = 1.0 - s * S_0 / (S_0 + T_max - T_max * k * s);
            var C_v = s * T_max * S_0 / (S_0 + T_max - T_max * k * s);

            var L = v * L_v;
            var C = v * C_v;

            // then we compensate for both toe and the curved top part of the triangle:
            var L_vt = toe_inv(L_v);
            var C_vt = C_v * L_vt / L_v;

            var L_new = toe_inv(L);
            C = C * L_new / L;
            L = L_new;

            ol = L;
            oa = a_ * C_vt;
            ob = b_ * C_vt;
        }


        public static void OkLabToOkHsl(out double oh, out double os, out double ol, double l, double a, double b)
        {
            var C = Math.Sqrt(a * a + b * b);
            var a_ = a / C;
            var b_ = b / C;

            oh = 0.5 + 0.5 * Math.Atan2(-b, -a) / Math.PI;

            get_Cs(out var C_0, out var C_mid, out var C_max, l, a_, b_);

            // Inverse of the interpolation in okhsl_to_srgb:

            var mid = 0.8;
            var mid_inv = 1.25;

            if (C < C_mid)
            {
                var k_1 = mid * C_0;
                var k_2 = (1.0 - k_1 / C_mid);

                var t = C / (k_1 + k_2 * C);
                os = t * mid;
            }
            else
            {
                var k_0 = C_mid;
                var k_1 = (1.0 - mid) * C_mid * C_mid * mid_inv * mid_inv / C_0;
                var k_2 = (1.0 - (k_1) / (C_max - C_mid));
                var t = (C - k_0) / (k_1 + k_2 * (C - k_0));
                os = mid + (1.0 - mid) * t;
            }
            ol = toe(l);
        }




        /// <summary>
        /// Convert from linear rgb to OkLab
        /// </summary>
        /// <param name="ol">Luminosity</param>
        /// <param name="oa">a (how green/red the color is)</param>
        /// <param name="ob">b (how blue/yellow the color is)</param>
        /// <param name="ir">[0, 1] linear space red</param>
        /// <param name="ig">[0, 1] linear space green</param>
        /// <param name="ib">[0, 1] linear space blue</param>
        public static void LinearRgbToOkLab(out double ol, out double oa, out double ob, double ir, double ig, double ib)
        {
            var l = 0.4122214708 * ir + 0.5363325363 * ig + 0.0514459929 * ib;
            var m = 0.2119034982 * ir + 0.6806995451 * ig + 0.1073969566 * ib;
            var s = 0.0883024619 * ir + 0.2817188376 * ig + 0.6299787005 * ib;

            var l_ = Math.Pow(l, 1.0 / 3.0);
            var m_ = Math.Pow(m, 1.0 / 3.0);
            var s_ = Math.Pow(s, 1.0 / 3.0);

            ol = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
            oa = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
            ob = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;
        }

        /// <summary>
        /// Convert from OkLab to linear rgb
        /// </summary>
        /// <param name="or">Linear space red</param>
        /// <param name="og">Linear space green</param>
        /// <param name="ob">Linear space blue</param>
        /// <param name="il">Luminosity</param>
        /// <param name="ia">a (how green/red the color is)</param>
        /// <param name="ib">b (how blue/yellow the color is)</param>
        public static void OkLabToLinearRgb(out double or, out double og, out double ob, double il, double ia, double ib)
        {

            var l_ = il + 0.3963377774 * ia + 0.2158037573 * ib;
            var m_ = il - 0.1055613458 * ia - 0.0638541728 * ib;
            var s_ = il - 0.0894841775 * ia - 1.2914855480 * ib;
            var l = l_ * l_ * l_;
            var m = m_ * m_ * m_;
            var s = s_ * s_ * s_;
            or = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            og = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            ob = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        }


        /// <summary>
        /// Convert one of the rgb components from linear rgb to srgb
        /// </summary>
        /// <param name="x">[0, 1] The linear rgb component value</param>
        /// <returns>The srgb component value</returns>
        public static double LinearRgbToSrgbComponent(double x)
        {
            return (x >= 0.0031308) ? (1.055 * Math.Pow(x, 1.0 / 2.4) - 0.055) : (12.92 * x);
        }

        /// <summary>
        /// Convert one of the rgb components from srgb to linear rgb
        /// </summary>
        /// <param name="x">[0, 1] The srgb component value</param>
        /// <returns>The linear rgb component value</returns>
        public static double SrgbToLinearRgbComponent(double x)
        {
            return x >= 0.04045 ? Math.Pow((x + 0.055) * (1.0 / 1.055), 2.4) : (x * (1.0 / 12.92));
        }


        #region Helpers

        // Finds the maximum saturation possible for a given hue that fits in sRGB
        // Saturation here is defined as S = C/L
        // a and b must be normalized so a^2 + b^2 == 1
        static double compute_max_saturation(double a, double b)
        {
            // Max saturation will be when one of r, g or b goes below zero.

            // Select different coefficients depending on which component goes below zero first
            double k0, k1, k2, k3, k4, wl, wm, ws;

            if (-1.88170328 * a - 0.80936493 * b > 1)
            {
                // Red component
                k0 = +1.19086277; k1 = +1.76576728; k2 = +0.59662641; k3 = +0.75515197; k4 = +0.56771245;
                wl = +4.0767416621; wm = -3.3077115913; ws = +0.2309699292;
            }
            else if (1.81444104 * a - 1.19445276 * b > 1)
            {
                // Green component
                k0 = +0.73956515; k1 = -0.45954404; k2 = +0.08285427; k3 = +0.12541070; k4 = +0.14503204;
                wl = -1.2684380046; wm = +2.6097574011; ws = -0.3413193965;
            }
            else
            {
                // Blue component
                k0 = +1.35733652; k1 = -0.00915799; k2 = -1.15130210; k3 = -0.50559606; k4 = +0.00692167;
                wl = -0.0041960863; wm = -0.7034186147; ws = +1.7076147010;
            }

            // Approximate max saturation using a polynomial:
            var S = k0 + k1 * a + k2 * b + k3 * a * a + k4 * a * b;

            // Do one step Halley's method to get closer
            // this gives an error less than 10e6, except for some blue hues where the dS/dh is close to infinite
            // this should be sufficient for most applications, otherwise do two/three steps 

            var k_l = +0.3963377774 * a + 0.2158037573 * b;
            var k_m = -0.1055613458 * a - 0.0638541728 * b;
            var k_s = -0.0894841775 * a - 1.2914855480 * b;

            {
                var l_ = 1.0 + S * k_l;
                var m_ = 1.0 + S * k_m;
                var s_ = 1.0 + S * k_s;

                var l = l_ * l_ * l_;
                var m = m_ * m_ * m_;
                var s = s_ * s_ * s_;

                var l_dS = 3.0 * k_l * l_ * l_;
                var m_dS = 3.0 * k_m * m_ * m_;
                var s_dS = 3.0 * k_s * s_ * s_;

                var l_dS2 = 6.0 * k_l * k_l * l_;
                var m_dS2 = 6.0 * k_m * k_m * m_;
                var s_dS2 = 6.0 * k_s * k_s * s_;

                var f = wl * l + wm * m + ws * s;
                var f1 = wl * l_dS + wm * m_dS + ws * s_dS;
                var f2 = wl * l_dS2 + wm * m_dS2 + ws * s_dS2;

                S = S - f * f1 / (f1 * f1 - 0.5 * f * f2);
            }

            return S;
        }

        // finds L_cusp and C_cusp for a given hue
        // a and b must be normalized so a^2 + b^2 == 1
        static void find_cusp(out double l, out double c, double a, double b)
        {
            // First, find the maximum saturation (saturation S = C/L)
            var S_cusp = compute_max_saturation(a, b);
            // Convert to linear sRGB to find the first point where at least one of r,g or b >= 1:
            OkLabToLinearRgb(out var maxR, out var maxG, out var maxB, 1.0, S_cusp * a, S_cusp * b);
            l = Math.Pow(1.0 / Math.Max(Math.Max(maxR, maxG), maxB), 1.0 / 3.0);
            c = l * S_cusp;
        }

        static double toe(double x)
        {
            const double k_1 = 0.206;
            const double k_2 = 0.03;
            const double k_3 = (1.0 + k_1) / (1.0 + k_2);
            return 0.5 * (k_3 * x - k_1 + Math.Sqrt((k_3 * x - k_1) * (k_3 * x - k_1) + 4.0 * k_2 * k_3 * x));
        }

        static double toe_inv(double x)
        {
            const double k_1 = 0.206;
            const double k_2 = 0.03;
            const double k_3 = (1.0 + k_1) / (1.0 + k_2);
            return (x * x + k_1 * x) / (k_3 * (x + k_2));
        }


        static void to_ST(out double s, out double t, double cuspL, double cuspC)
        {
            s = cuspC / cuspL;
            t = cuspC / (1.0 - cuspL);
        }

        static void get_Cs(out double C_0, out double C_mid, out double C_max, double L, double a_, double b_)
        {
            find_cusp(out var cuspL, out var cuspC, a_, b_);
            C_max = find_gamut_intersection(a_, b_, L, 1, L, cuspL, cuspC);
            to_ST(out var stS, out var stT, cuspL, cuspC);
            // Scale factor to compensate for the curved part of gamut shape:
            var k = C_max / Math.Min(L * stS, (1.0 - L) * stT);

            get_ST_mid(out var S, out var T, a_, b_);

            // Use a soft minimum function, instead of a sharp triangle shape to get a smooth value for chroma.
            var C_a = L * S;
            var C_b = (1.0 - L) * T;
            C_mid = 0.9 * k * Math.Sqrt(Math.Sqrt(1.0 / (1.0 / (C_a * C_a * C_a * C_a) + 1.0 / (C_b * C_b * C_b * C_b))));
            // for C_0, the shape is independent of hue, so ST are constant. Values picked to roughly be the average values of ST.

            C_a = L * 0.4;
            C_b = (1.0 - L) * 0.8;

            // Use a soft minimum function, instead of a sharp triangle shape to get a smooth value for chroma.
            C_0 = Math.Sqrt(1.0 / (1.0 / (C_a * C_a) + 1.0 / (C_b * C_b)));
        }


        // Returns a smooth approximation of the location of the cusp
        // This polynomial was created by an optimization process
        // It has been designed so that S_mid < S_max and T_mid < T_max
        static void get_ST_mid(out double S, out double T, double a_, double b_)
        {
            S = 0.11516993 + 1/ (
                +7.44778970 + 4.15901240 * b_
                + a_ * (-2.19557347 + 1.75198401 * b_
                    + a_ * (-2.13704948 - 10.02301043 * b_
                        + a_ * (-4.24894561 + 5.38770819 * b_ + 4.69891013 * a_
                            )))
                );

            T = 0.11239642 + 1 / (
                +1.61320320 - 0.68124379 * b_
                + a_ * (+0.40370612 + 0.90148123 * b_
                    + a_ * (-0.27087943 + 0.61223990 * b_
                        + a_ * (+0.00299215 - 0.45399568 * b_ - 0.14661872 * a_
                            )))
                );
        }


        // Finds intersection of the line defined by 
        // L = L0 * (1 - t) + t * L1;
        // C = t * C1;
        // a and b must be normalized so a^2 + b^2 == 1
        static double find_gamut_intersection(double a, double b, double L1, double C1, double L0, double cuspL, double cuspC)
        {
            // Find the intersection for upper and lower half seprately
            double t;
            if (((L1 - L0) * cuspC - (cuspL - L0) * C1) <= 0)
            {
                // Lower half

                t = cuspC * L0 / (C1 * cuspL + cuspC * (L0 - L1));
            }
            else
            {
                // Upper half

                // First intersect with triangle
                t = cuspC * (L0 - 1) / (C1 * (cuspL - 1) + cuspC * (L0 - L1));

                // Then one step Halley's method
                {
                    double dL = L1 - L0;
                    double dC = C1;

                    double k_l = +0.3963377774 * a + 0.2158037573 * b;
                    double k_m = -0.1055613458 * a - 0.0638541728 * b;
                    double k_s = -0.0894841775 * a - 1.2914855480 * b;

                    double l_dt = dL + dC * k_l;
                    double m_dt = dL + dC * k_m;
                    double s_dt = dL + dC * k_s;


                    // If higher accuracy is required, 2 or 3 iterations of the following block can be used:
                    {
                        double L = L0 * (1.0 - t) + t * L1;
                        double C = t * C1;

                        double l_ = L + C * k_l;
                        double m_ = L + C * k_m;
                        double s_ = L + C * k_s;

                        double l = l_ * l_ * l_;
                        double m = m_ * m_ * m_;
                        double s = s_ * s_ * s_;

                        double ldt = 3 * l_dt * l_ * l_;
                        double mdt = 3 * m_dt * m_ * m_;
                        double sdt = 3 * s_dt * s_ * s_;

                        double ldt2 = 6 * l_dt * l_dt * l_;
                        double mdt2 = 6 * m_dt * m_dt * m_;
                        double sdt2 = 6 * s_dt * s_dt * s_;

                        double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s - 1;
                        double r1 = 4.0767416621 * ldt - 3.3077115913 * mdt + 0.2309699292 * sdt;
                        double r2 = 4.0767416621 * ldt2 - 3.3077115913 * mdt2 + 0.2309699292 * sdt2;

                        double u_r = r1 / (r1 * r1 - 0.5 * r * r2);
                        double t_r = -r * u_r;

                        double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s - 1;
                        double g1 = -1.2684380046 * ldt + 2.6097574011 * mdt - 0.3413193965 * sdt;
                        double g2 = -1.2684380046 * ldt2 + 2.6097574011 * mdt2 - 0.3413193965 * sdt2;

                        double u_g = g1 / (g1 * g1 - 0.5 * g * g2);
                        double t_g = -g * u_g;

                        b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s - 1;
                        double b1 = -0.0041960863 * ldt - 0.7034186147 * mdt + 1.7076147010 * sdt;
                        double b2 = -0.0041960863 * ldt2 - 0.7034186147 * mdt2 + 1.7076147010 * sdt2;

                        double u_b = b1 / (b1 * b1 - 0.5 * b * b2);
                        double t_b = -b * u_b;

                        t_r = u_r >= 0.0 ? t_r : double.MaxValue;
                        t_g = u_g >= 0.0 ? t_g : double.MaxValue;
                        t_b = u_b >= 0.0 ? t_b : double.MaxValue;

                        t += Math.Min(t_r, Math.Min(t_g, t_b));
                    }
                }
            }

            return t;
        }


        #endregion//Helpers

    }
}
