using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using WaterTrans.GlyphLoader;

namespace SysWeaver.Media
{
    public static class SvgPath
    {
        public static void UpdateMinMax(SvgMinMaxState state, String svgPath)
            => Process(svgPath, state.Update);


        /// <summary>
        /// Currently only supports L and Q
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="numDecimals"></param>
        /// <param name="doSort"></param>
        /// <returns>A tuple for each path with: The new path, the diffuse lighing value, the sort value</returns>
        public static Tuple<String, double, double>[] Extrude(String path, double dx, double dy, int numDecimals = 3, bool doSort = true)
        {
            var fmt = SvgTools.GetFormat(numDecimals);
            var dlen = Math.Sqrt(dx * dx + dy * dy);
            if (dlen <= 0)
                throw new Exception("Invalid dx or dy");

            StringBuilder svg = new StringBuilder();
            double pDefault = Double.MinValue;
            double pMax = pDefault;
            List<Tuple<String, double, double>> shapes = new List<Tuple<string, double, double>>();

            void Add(double x0, double y0, double x1, double y1)
            {
                double p = x0 * dx + y0 * dy;
                if (p > pMax)
                    pMax = p;
                p = x1 * dx + y1 * dy;
                if (p > pMax)
                    pMax = p;

                double vx = x1 - x0;
                double vy = y1 - y0;
                double len = Math.Sqrt(vx * vx + vy * vy);
                if (len >= 0.000001)
                {
                    var nx = -vy;
                    var ny = vx;
                    var d = nx * dx + ny * dy;
                    if (len > 0)
                    {
                        d /= (len * dlen);
                        if (d < 0)
                            d = -d;
                    }
                    shapes.Add(Tuple.Create(svg.ToString(), d, pMax));
                }
                svg.Clear();
                pMax = pDefault;
            }

            Process(path, null, (x, y, cmd, nums) =>
            {
                var nl = nums.Count;
                int i;
                switch (cmd)
                {
                    case 'L':
                        for (i = 0; i < nl; i += 2)
                        {
                            svg.Append('M');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            var nx = nums[i];
                            var ny = nums[i + 1];
                            svg.Append('L');
                            svg.Append(fmt(nx));
                            svg.Append(' ');
                            svg.Append(fmt(ny));
                            svg.Append(' ');
                            svg.Append(fmt(nx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(ny + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x + dx));
                            svg.Append(' ');
                            svg.Append(fmt(y + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            svg.Append('z');
                            Add(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'M':
                        x = nums[0];
                        y = nums[1];
                        for (i = 2; i < nl; i += 2)
                        {
                            svg.Append('M');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            var nx = nums[i];
                            var ny = nums[i + 1];
                            svg.Append('L');
                            svg.Append(fmt(nx));
                            svg.Append(' ');
                            svg.Append(fmt(ny));
                            svg.Append(' ');
                            svg.Append(fmt(nx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(ny + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x + dx));
                            svg.Append(' ');
                            svg.Append(fmt(y + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            svg.Append('z');
                            Add(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'H':
                        for (i = 0; i < nl; ++ i)
                        {
                            svg.Append('M');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            var nx = nums[i];
                            var ny = y;
                            svg.Append('L');
                            svg.Append(fmt(nx));
                            svg.Append(' ');
                            svg.Append(fmt(ny));
                            svg.Append(' ');
                            svg.Append(fmt(nx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(ny + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x + dx));
                            svg.Append(' ');
                            svg.Append(fmt(y + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            svg.Append('z');
                            Add(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'V':
                        for (i = 0; i < nl; ++i)
                        {
                            svg.Append('M');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            var nx = x;
                            var ny = nums[i];
                            svg.Append('L');
                            svg.Append(fmt(nx));
                            svg.Append(' ');
                            svg.Append(fmt(ny));
                            svg.Append(' ');
                            svg.Append(fmt(nx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(ny + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x + dx));
                            svg.Append(' ');
                            svg.Append(fmt(y + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            svg.Append('z');
                            Add(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'Q':
                        for (i = 0; i < nl; i += 4)
                        {
                            svg.Append('M');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            var cx = nums[i];
                            var cy = nums[i + 1];
                            var nx = nums[i + 2];
                            var ny = nums[i + 3];

                            svg.Append('Q');
                            svg.Append(fmt(cx));
                            svg.Append(' ');
                            svg.Append(fmt(cy));
                            svg.Append(' ');
                            svg.Append(fmt(nx));
                            svg.Append(' ');
                            svg.Append(fmt(ny));

                            svg.Append('L');
                            svg.Append(fmt(nx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(ny + dy));

                            svg.Append('Q');
                            svg.Append(fmt(cx + dx));
                            svg.Append(' ');
                            svg.Append(fmt(cy + dy));
                            svg.Append(' ');
                            svg.Append(fmt(x + dx));
                            svg.Append(' ');
                            svg.Append(fmt(y + dy));


                            svg.Append('L');
                            svg.Append(fmt(x));
                            svg.Append(' ');
                            svg.Append(fmt(y));
                            svg.Append('z');
                            Add(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        throw new NotImplementedException("Unsupported command '" + cmd + "'");

                }

            });
            if (doSort)
                shapes.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return shapes.Select(x => Tuple.Create(x.Item1, x.Item3, x.Item2)).ToArray();
        }

        /// <summary>
        /// Process a path and perform some actions on all points / and or commands
        /// </summary>
        /// <param name="path"></param>
        /// <param name="onPoint"></param>
        /// <param name="onCommand"></param>
        public static void Process(String path, Action<double, double> onPoint, Action<double, double, Char, IList<double>> onCommand = null)
        {
            double x = 0;
            double y = 0;
            onPoint = onPoint ?? new Action<double, double>((xx, yy) => { });
            onCommand = onCommand ?? new Action<double, double, Char, IList<double>>((xx, yy, cc, vv) => { });
            List<double> nums = new List<double>();
            void processCommand(Char c)
            {
                if (c == 0)
                    return;
                onCommand(x, y, c, nums);
                var nl = nums.Count;
                switch (c)
                {
                    case 'm':
                        for (int i = 0; i < nl; i += 2)
                        {
                            x += nums[i + 0];
                            y += nums[i + 1];
                            onPoint(x, y);
                        }
                        break;
                    case 'M':
                        for (int i = 0; i < nl; i += 2)
                        {
                            x = nums[i + 0];
                            y = nums[i + 1];
                            onPoint(x, y);
                        }
                        break;
                    case 'L':
                    case 'T':
                        for (int i = 0; i < nl; i += 2)
                        {
                            x = nums[i + 0];
                            y = nums[i + 1];
                            onPoint(x, y);
                        }
                        break;
                    case 'l':
                    case 't':
                        for (int i = 0; i < nl; i += 2)
                        {
                            x += nums[i + 0];
                            y += nums[i + 1];
                            onPoint(x, y);
                        }
                        break;
                    case 'H':
                        for (int i = 0; i < nl; ++i)
                        {
                            x = nums[i];
                            onPoint(x, y);
                        }
                        break;
                    case 'h':
                        for (int i = 0; i < nl; ++i)
                        {
                            x += nums[i];
                            onPoint(x, y);
                        }
                        break;
                    case 'V':
                        for (int i = 0; i < nl; ++i)
                        {
                            y = nums[i];
                            onPoint(x, y);
                        }
                        break;
                    case 'v':
                        for (int i = 0; i < nl; ++i)
                        {
                            y += nums[i];
                            onPoint(x, y);
                        }
                        break;
                    case 'C':
                        for (int i = 0; i < nl; i += 6)
                        {
                            onPoint(nums[i + 0], nums[i + 1]);
                            onPoint(nums[i + 2], nums[i + 3]);
                            x = nums[i + 4];
                            y = nums[i + 5];
                            onPoint(x, y);
                        }
                        break;
                    case 'c':
                        for (int i = 0; i < nl; i += 6)
                        {
                            onPoint(x + nums[i + 0], y + nums[i + 1]);
                            onPoint(x + nums[i + 2], y + nums[i + 3]);
                            x += nums[i + 4];
                            y += nums[i + 5];
                            onPoint(x, y);
                        }
                        break;
                    case 'S':
                    case 'Q':
                        for (int i = 0; i < nl; i += 4)
                        {
                            onPoint(nums[i + 0], nums[i + 1]);
                            x = nums[i + 2];
                            y = nums[i + 3];
                            onPoint(x, y);
                        }
                        break;
                    case 's':
                    case 'q':
                        for (int i = 0; i < nl; i += 4)
                        {
                            onPoint(x + nums[i + 0], y + nums[i + 1]);
                            x += nums[i + 2];
                            y += nums[i + 3];
                            onPoint(x, y);
                        }
                        break;
                    case 'A':
                        for (int i = 0; i < nl; i += 7)
                        {
                            x = nums[i + 5];
                            y = nums[i + 6];
                            onPoint(x, y);
                        }
                        break;
                    case 'a':
                        for (int i = 0; i < nl; i += 7)
                        {
                            x += nums[i + 5];
                            y += nums[i + 6];
                            onPoint(x, y);
                        }
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        throw new Exception("Path command not understood!");
                }
            }
            path = path.Trim();
            var l = path.Length;
            Char code = (Char)0;
            int start = 0;
            bool havePoint = false;
            for (int i = 0; i < l; ++i)
            {
                char c = path[i];
                if (Char.IsLetter(c))
                {
                    if (start < i)
                    {
                        var num = double.Parse(path.Substring(start, i - start).Trim(), CultureInfo.InvariantCulture);
                        nums.Add(num);
                    }
                    processCommand(code);
                    code = c;
                    start = i + 1;
                    nums.Clear();
                    havePoint = false;
                    continue;
                }
                var isP = c == '.';
                if ((c == ' ') || (c == ',') || (c == '-') || (isP && havePoint))
                {
                    if (start < i)
                    {
                        var num = double.Parse(path.Substring(start, i - start).Trim(), CultureInfo.InvariantCulture);
                        nums.Add(num);
                    }
                    start = i;
                    if (c != '-')
                        if (!isP)
                            ++start;
                    havePoint = isP;
                    continue;
                }
                havePoint |= isP;

            }
            processCommand(code);
        }


        /// <summary>
        /// Get min/max for some paths
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static SvgMinMaxState GetMinMaxForPaths(IList<String> paths)
        {
            var mm = new SvgMinMaxState();
            var gl = paths.Count;
            for (int i = 0; i < gl; ++i)
                UpdateMinMax(mm, paths[i]);
            return mm;
        }

        /// <summary>
        /// Apply a scale and translate for a graph
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="scaleX"></param>
        /// <param name="scaleY"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="maxDecimals"></param>
        public static void TransformPaths(IList<String> paths, double scaleX, double scaleY, double offsetX, double offsetY, int maxDecimals = 4)
        {
            var gl = paths.Count;
            Action<IList<double>, int> ScaleX = (IList<double> p, int o) =>
            {
                var v = p[o];
                v *= scaleX;
                v += offsetX;
                p[o] = v;
            };

            Action<IList<double>, int> ScaleY = (IList<double> p, int o) =>
            {
                var v = p[o];
                v *= scaleY;
                v += offsetY;
                p[o] = v;
            };

            Action<IList<double>, int> ScaleRelX = (IList<double> p, int o) =>
            {
                var v = p[o];
                v *= scaleX;
                p[o] = v;
            };

            Action<IList<double>, int> ScaleRelY = (IList<double> p, int o) =>
            {
                var v = p[o];
                v *= scaleY;
                p[o] = v;
            };

            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            void onCmd(double px, double py, Char code, IList<double> nums)
            {
                var isRel = Char.IsLower(code);
                var scaleX = isRel ? ScaleRelX : ScaleX;
                var scaleY = isRel ? ScaleRelY : ScaleY;
                var c = nums.Count;
                switch (code)
                {
                    case 'h':
                    case 'H':
                        for (int i = 0; i < c; ++i)
                            scaleX(nums, i);
                        break;
                    case 'v':
                    case 'V':
                        for (int i = 0; i < c; ++i)
                            scaleY(nums, i);
                        break;
                    case 'a':
                    case 'A':
                        for (int i = 0; i < c; i += 7)
                        {
                            ScaleRelX(nums, i);
                            ScaleRelY(nums, i + 1);
                            scaleX(nums, i + 5);
                            scaleY(nums, i + 6);
                        }
                        break;
                    case 'm':
                    case 'M':
                    case 'l':
                    case 'L':
                    case 'c':
                    case 'C':
                    case 'q':
                    case 'Q':
                    case 't':
                    case 'T':
                        for (int i = 0; i < c; i += 2)
                        {
                            scaleX(nums, i);
                            scaleY(nums, i + 1);
                        }
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        break;
                }
                b.Append(code);
                for (int i = 0; i < c; ++i)
                {
                    if (i != 0)
                        b.Append(' ');
                    b.Append(fmt(nums[i]));
                }
            }
            List<String> o = new List<string>(gl);
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }



        public sealed class Point
        {
            public double X;
            public double Y;
        }

        /// <summary>
        /// Apply a custom function on an absolute path
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="onPoint"></param>
        /// <param name="maxDecimals"></param>
        public static void ModifyAbsolutePaths(IList<String> paths, Action<Point> onPoint, int maxDecimals = 4)
        {
            var gl = paths.Count;
            Point point = new Point();

            Action<IList<double>, int> ScaleX = (IList<double> p, int o) =>
            {
                point.X = p[o];
                onPoint(point);
                p[o] = point.X;
            };

            Action<IList<double>, int> ScaleY = (IList<double> p, int o) =>
            {
                point.Y = p[o];
                onPoint(point);
                p[o] = point.Y;
            };

            Action<IList<double>, int> ScaleXY = (IList<double> p, int o) =>
            {
                point.X = p[o];
                point.Y = p[o + 1];
                onPoint(point);
                p[o] = point.X;
                p[o + 1] = point.Y;
            };


            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            void onCmd(double px, double py, Char code, IList<double> nums)
            {
                if (Char.IsLower(code))
                    throw new Exception("May only use absolute paths!");
                point.X = px;
                point.Y = py;
                var c = nums.Count;
                switch (code)
                {
                    case 'h':
                    case 'H':
                        for (int i = 0; i < c; ++i)
                            ScaleX(nums, i);
                        break;
                    case 'v':
                    case 'V':
                        for (int i = 0; i < c; ++i)
                            ScaleY(nums, i);
                        break;
                    case 'a':
                    case 'A':
                        for (int i = 0; i < c; i += 7)
                        {
                            ScaleXY(nums, i);
                            ScaleXY(nums, i + 5);
                        }
                        break;
                    case 'm':
                    case 'M':
                    case 'l':
                    case 'L':
                    case 'c':
                    case 'C':
                    case 'q':
                    case 'Q':
                    case 't':
                    case 'T':
                        for (int i = 0; i < c; i += 2)
                        {
                            ScaleXY(nums, i);
                        }
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        break;
                }
                b.Append(code);
                for (int i = 0; i < c; ++i)
                {
                    if (i != 0)
                        b.Append(' ');
                    b.Append(fmt(nums[i]));
                }
            }
            List<String> o = new List<string>(gl);
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }


        /// <summary>
        /// Optimize a bunch of paths
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="maxDecimals"></param>
        public static void OptimizePaths(IList<String> paths, int maxDecimals = 4)
        {
            var gl = paths.Count;
            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            void onCmd(double px, double py, Char code, IList<double> nums)
            {
                int c = nums.Count;
                b.Append(code);
                for (int i = 0; i < c; ++i)
                {
                    var str = fmt(nums[i]);
                    if (i != 0)
                        if (str[0] != '-')
                            b.Append(' ');
                    b.Append(str);
                }
            }
            List<String> o = new List<string>(gl);
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }



        static readonly Char[] SplitZ = "zZ".ToCharArray();
        public static double ComputeApproxCentroid(out double x, out double y, String path)
        {
            var regions = path.Split(SplitZ, StringSplitOptions.RemoveEmptyEntries);
            var c = regions.Length;
            double maxArea = 0;
            double maxX = 0;
            double maxY = 0;
            double totArea = 0;
            for (int i = 0; i < c; ++ i)
            {
                var p = regions[i] + 'Z';
                int j = 0;
                double ox = 0;
                double oy = 0;
                double px = 0;
                double py = 0;
                double ccx = 0;
                double ccy = 0;
                double area = 0;
                Process(p, (x, y) =>
                {
                    switch (j)
                    {
                        case 0:
                            ox = x;
                            oy = y;
                            ++j;
                            break;
                        case 1:
                            px = x;
                            py = y;
                            ++j;
                            break;
                        case 2:
                            var dx0 = px - ox;
                            var dy0 = py - oy;
                            var dx1 = x - ox;
                            var dy1 = y - oy;
                            var cx = ox + px + x;
                            var cy = oy + py + y;
                            var a = dx0 * dy1 - dx1 * dy0;
                            area += a;
                            cx *= a;
                            cy *= a;
                            ccx += cx;
                            ccy += cy;
                            px = x;
                            py = y;
                            break;
                    }
                });
                bool isNeg = area < 0;
                double absArea = isNeg ? -area : area;
                totArea += absArea;
                if (absArea > maxArea)
                {
                    maxArea = absArea;
                    maxX = ccx / area;
                    maxY = ccy / area;
                }
            }
            x = maxX / 3;
            y = maxY / 3;
            return totArea * 0.5;
        }


        const double C = 0.70710678118;

        static readonly double[] Dirs = [
            -C, -C,     0, -1,          C, -C,
            -1, 0,                      1, 0,
            -C, C,      0, 1,           C, C,
        ];

        public static void FindInnerMostPoint(out double x, out double y, String path, double xPrio = 1)
        {
            var regions = path.Split(SplitZ, StringSplitOptions.RemoveEmptyEntries);
            var c = regions.Length;

            /*
            double maxA = 0;
            for (int i = 0; i < c; ++i)
            {
                var p = regions[i] + 'Z';
                var a = ComputeApproxCentroid(out var _, out var __, p);
                if (a > maxA)
                {
                    maxA = a;
                    regions[0] = regions[i];
                }
            }
            c = 1;
            */

            //  Find bounds
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            List<double>[] points = new List<double>[c];
            for (int i = 0; i < c; ++i)
            {
                var p = regions[i] + 'Z';
                var pp = new List<double>();
                points[i] = pp;
                Process(p, (tx, ty) =>
                {
                    tx /= xPrio;

                    if (tx < minX)
                        minX = tx;
                    if (ty < minY)
                        minY = ty;

                    if (tx > maxX)
                        maxX = tx;
                    if (ty > maxY)
                        maxY = ty;
                    var prev = pp.Count - 2;
                    if (prev >= 0)
                    {
                        if ((tx == pp[prev]) && (ty == pp[prev + 1]))
                            return;
                    }
                    pp.Add(tx);
                    pp.Add(ty);
                });
                var pl = pp.Count - 2;
                if (pl > 0)
                {
                    if ((pp[pl] == pp[0]) && (pp[pl + 1] == pp[1]))
                        pp.RemoveRange(pl, 2);
                }
            }

            bool IsInside(double pX, double pY, List<double> polygon)
            {
                bool inside = false;
                var pc = polygon.Count;

                int j = pc - 2;
                for (int i = 0; i < pc; i += 2)
                {
                    if ((polygon[i + 1] > pY) != (polygon[j + 1] > pY) &&
                         pX < (polygon[j] - polygon[i]) * (pY - polygon[i + 1]) / (polygon[j + 1] - polygon[i + 1]) + polygon[i])
                    {
                        inside ^= true;
                    }
                    j = i;
                }
                return inside;
            }



            double MinDist(double px, double py)
            {
                double m = double.MaxValue;
                bool isInside = false;
                for (int j = 0; j < c; ++j)
                {
                    var pp = points[j];
                    if (!IsInside(px, py, pp))
                        continue;
                    isInside = true;
                    var pc = pp.Count;
                    var prevx = pp[pc - 2];
                    var prevy = pp[pc - 1];
                    for (int i = 0; i < pc; i += 2)
                    {
                        var tx = pp[i];
                        var ty = pp[i + 1];
                        var lx = tx - prevx;
                        var ly = ty - prevy;
                        double dx = px - prevx;
                        double dy = py - prevy;

                        double t = (lx * dx + ly * dy) / (lx * lx + ly * ly);
                        if (t < 0)
                            t = 0;
                        if (t > 1)
                            t = 1;

                        lx *= t;
                        ly *= t;
                        lx -= dx;
                        ly -= dy;
                        var dd = lx * lx + ly * ly;

                        prevx = tx;
                        prevy = ty;

                        if (dd < m)
                            m = dd;
                    }
                }
                return isInside ? m : -1;
            }

        //  Try grid to find a starting point
            maxX -= minX;
            maxY -= minY;
            double m = 0;
            x = 0;
            y = 0;
            for (int xx = 1; xx < 50; ++ xx)
            {
                var tx = minX + (maxX * xx) / 50.0;
                for (int yy = 1; yy < 50; ++yy)
                {
                    var ty = minY + (maxY * yy) / 50.0;
                    var t = MinDist(tx, ty);
                    if (t > m)
                    {
                        m = t;
                        x = tx;
                        y = ty;
                    }
                }
            }
        //  Iterative refinement

            var step = Math.Max(maxX, maxY) / 70.0;
            var d = Dirs;
            for (int i = 0; i < 200; ++ i, step *= 0.97)
            {
                var ox = x;
                var oy = y;
                for (int j = 0; j < 16; j += 2)
                {
                    var tx = ox + step * d[j];
                    var ty = oy + step * d[j + 1];
                    double t = MinDist(tx, ty);
                    if (t > m)
                    {
                        m = t;
                        x = tx;
                        y = ty;
                    }
                }
            }
            

            x *= xPrio;

        }


        /// <summary>
        /// Change all relative positions to absolute
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="maxDecimals"></param>
        public static void MakeAbsolute(IList<String> paths, int maxDecimals = 4)
        {
            var gl = paths.Count;
            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            void writeNum(double val)
            {
                var str = fmt(val);
                if (Char.IsNumber(b[b.Length - 1]))
                    if (str[0] != '-')
                        b.Append(' ');
                b.Append(str);
            }

            void onCmd(double x, double y, Char code, IList<double> nums)
            {
                int c = nums.Count;
                if (Char.IsLower(code))
                {
                    b.Append(code.FastToUpper());
                    switch (code)
                    {
                        case 'm':
                        case 'l':
                        case 't':
                            break;
                        case 'h':
                            for (int i = 0; i < c; ++ i)
                            {
                                x += nums[i];
                                writeNum(x);
                            }
                            return;
                        case 'v':
                            for (int i = 0; i < c; ++i)
                            {
                                y += nums[i];
                                writeNum(y);
                            }
                            return;
                        case 'z':
                            break;
                        default:
                            throw new Exception("Not yet implemented!");
                    }
                    for (int i = 0; i < c; i += 2)
                    {
                        x += nums[i];
                        y += nums[i + 1];
                        writeNum(x);
                        writeNum(y);
                    }
                    return;
                }
                b.Append(code);
                for (int i = 0; i < c; ++i)
                {
                    var str = fmt(nums[i]);
                    if (i != 0)
                        if (str[0] != '-')
                            b.Append(' ');
                    b.Append(str);
                }
            }
            List<String> o = new List<string>(gl);
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }



        /// <summary>
        /// Fit some path(s) within an area
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="maxWidth"></param>
        /// <param name="maxHeigth"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="maxDecimals"></param>
        public static void FitPaths(IList<String> paths, double maxWidth, double maxHeigth, double offsetX = 0, double offsetY = 0, int maxDecimals = 4)
            =>
            FitPaths(paths, GetMinMaxForPaths(paths), maxWidth, maxHeigth, offsetX, offsetY, maxDecimals);

        public static void FitPaths(IList<String> paths, SvgMinMaxState mm, double width, double height, double offsetX = 0, double offsetY = 0, int maxDecimals = 4)
        {
            var w = mm.Width;
            var h = mm.Height;
            var sx = width / w;
            var sy = height / h;
            var scale = Math.Min(sx, sy);
            var preX = -mm.MinX;
            var preY = -mm.MinY;
            w *= scale;
            h *= scale;
            var postX = (width - w) * 0.5 + offsetX + preX * scale;
            var postY = (height - h) * 0.5 + offsetY + preY * scale;
            TransformPaths(paths, scale, scale, postX, postY, maxDecimals);
        }

        public static Tuple<double, double> NormalizeInto(IList<String> paths, double maxWidth, double maxHeigth, double marginTop = 4, double marginRight = 4, double marginBottom = 4, double marginLeft = 4, int maxDecimals = 4)
            =>
            NormalizeInto(paths, GetMinMaxForPaths(paths), maxWidth, maxHeigth, marginTop, marginRight, marginBottom, marginLeft, maxDecimals);



        public static Tuple<double, double> NormalizeInto(out double scale, out double postX, out double postY, IList<String> paths, SvgMinMaxState mm, double maxWidth, double maxHeigth, double marginTop = 4, double marginRight = 4, double marginBottom = 4, double marginLeft = 4, int maxDecimals = 4, bool round = true)
        {
            var w = mm.Width;
            var h = mm.Height;
            var sx = maxWidth / w;
            var sy = maxHeigth / h;
            bool useX = sx < sy;
            scale = useX ? sx : sy;
            var width = useX ? maxWidth : (w * scale);
            var height = useX ? (h * scale) : maxHeigth;
            if (round)
            {
                width = Math.Ceiling(width);
                height = Math.Ceiling(height);
            }
            var preX = -mm.MinX;
            var preY = -mm.MinY;
            w *= scale;
            h *= scale;
            postX = (width - w) * 0.5 + marginLeft + preX * scale;
            postY = (height - h) * 0.5 + marginTop + preY * scale;
            TransformPaths(paths, scale, scale, postX, postY, maxDecimals);
            return Tuple.Create(width + marginLeft + marginRight, height + marginTop + marginBottom);
        }

        public static Tuple<double, double> NormalizeInto(IList<String> paths, SvgMinMaxState mm, double maxWidth, double maxHeigth, double marginTop = 4, double marginRight = 4, double marginBottom = 4, double marginLeft = 4, int maxDecimals = 4, bool round = true)
            => NormalizeInto(out var s, out var x, out var y, paths, mm, maxWidth, maxHeigth, marginTop, marginRight, marginBottom, marginLeft, maxDecimals, round);


        /// <summary>
        /// Generate svg paths from text
        /// </summary>
        /// <param name="text"></param>
        /// <param name="font"></param>
        /// <param name="size"></param>
        /// <param name="maxDecimals"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<String> GetSvgTextPaths(String text, SvgFont font, double size = 12, int maxDecimals = 3)
        {
            Typeface tf = font?.TF;
            if (tf == null)
                throw new Exception("No font supplied!");

            double x = 0, y = 0;
            var cult = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            List<String> paths = new List<String>(text.Length);
            var dec = maxDecimals;
            foreach (char c in text)
            {
                // Get glyph index
                if (!tf.CharacterToGlyphMap.TryGetValue((int)c, out var glyphIndex))
                    continue;

                // Get glyph outline
                var geometry = tf.GetGlyphOutline(glyphIndex, size);

                // Get advanced width
                var advanceWidth = tf.AdvanceWidths[glyphIndex] * size;

                // Get advanced height
                var advanceHeight = tf.AdvanceHeights[glyphIndex] * size;

                // Get baseline
                double baseline = tf.Baseline * size;

                // Convert to path mini-language
                var figs = geometry.Figures;
                var fillRule = geometry.FillRule.ToString().FastToLower();
                string miniLanguage = figs.ToString(x, y + baseline, dec);
                if (!String.IsNullOrEmpty(miniLanguage))
                    paths.Add(miniLanguage);
                x += advanceWidth;
            }
            return paths;
        }


        /// <summary>
        /// Create svg paths from some text
        /// </summary>
        /// <param name="text"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<String> GetSvgTextPaths(String text, SvgTextPathParams p)
        {
            var fitWidth = p.FitWidth;
            var fitHeight = p.FitHeight;
            var marginX = p.MarginX;
            var marginY = p.MarginY;
            double unit = Math.Max(fitWidth, fitHeight) * 0.5;
            var paths = GetSvgTextPaths(text, p.Font, unit, Math.Max(15, p.MaxDecimals));
            FitPaths(paths, fitWidth, fitHeight, marginX, marginY, p.MaxDecimals);
            return paths;
        }

        /// <summary>
        /// Create a svg path in the shape of a regular N-gon
        /// </summary>
        /// <param name="n"></param>
        /// <param name="size">The size (width and height) to fit the shape inside</param>
        /// <param name="maxDecimals"></param>
        /// <param name="angleAdjust"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static String GetNGonPath(int n, double size = 224, int maxDecimals = 3, double angleAdjust = 0, double offsetX = 0, double offsetY = 0)
        {
            if (n <= 2)
                throw new ArgumentOutOfRangeException(nameof(n));
            double[] x = new double[n];
            double[] y = new double[n];
            double mx = 0;
            double my = 0;
            double scale = Math.PI * 2 / n;
            double rad = 0.5 * size;
            double offset = 0;
            if ((n & 1) == 0)
                offset = Math.PI / n;
            offset += (angleAdjust * Math.PI / 180.0);
            for (int i = 0; i < n; i++)
            {
                var a = scale * i + offset;
                var xp = Math.Sin(a);
                var yp = -Math.Cos(a);
                x[i] = xp;
                y[i] = yp;
                if (xp < 0)
                    xp = -xp;
                if (yp < 0)
                    yp = -yp;
                if (xp > mx)
                    mx = xp;
                if (yp > my)
                    my = yp;
            }
            size = rad / Math.Max(mx, my);
            offsetX += rad;
            offsetY += rad;
            String[] c = new String[n];
            var fmt = SvgTools.GetFormat(maxDecimals);
            for (int i = 0; i < n; i++)
            {
                var xp = x[i] * size + offsetX;
                var yp = y[i] * size + offsetY;
                c[i] = String.Join(' ', fmt(xp), fmt(yp));
            }
            StringBuilder b = new StringBuilder();
            b.Append('M').Append(c[n - 1]);
            b.Append('L');
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                    b.Append(' ');
                b.Append(c[i]);
            }
            b.Append('z');
            return b.ToString();
        }


        /// <summary>
        /// Create a svg path in the shape of a rounded rectangle
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="radius"></param>
        /// <param name="maxDecimals"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        public static String GetRoundedRect(double width, double height, double radius, int maxDecimals = 3, double offsetX = 0, double offsetY = 0)
        {
            var maxRad = Math.Min(width, height) * 0.5;
            if (radius > maxRad)
                radius = maxRad;
            bool haveX = width > (radius * 2);
            bool haveY = height > (radius * 2);

            var x1 = offsetX + width;
            var y1 = offsetY + height;

            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            b.Append('M');
            b.Append(fmt(offsetX + radius));
            b.Append(' ');
            b.Append(fmt(offsetY));
            if (haveX)
            {
                b.Append('H');
                b.Append(fmt(x1 - radius));
            }
            b.Append('Q');
            b.Append(fmt(x1));
            b.Append(' ');
            b.Append(fmt(offsetY));
            b.Append(' ');
            b.Append(fmt(x1));
            b.Append(' ');
            b.Append(fmt(offsetY + radius));
            if (haveY)
            {
                b.Append('V');
                b.Append(fmt(y1 - radius));
            }
            b.Append('Q');
            b.Append(fmt(x1));
            b.Append(' ');
            b.Append(fmt(y1));
            b.Append(' ');
            b.Append(fmt(x1 - radius));
            b.Append(' ');
            b.Append(fmt(y1));
            if (haveX)
            {
                b.Append('H');
                b.Append(fmt(offsetX + radius));
            }
            b.Append('Q');
            b.Append(fmt(offsetX));
            b.Append(' ');
            b.Append(fmt(y1));
            b.Append(' ');
            b.Append(fmt(offsetX));
            b.Append(' ');
            b.Append(fmt(y1 - radius));
            if (haveY)
            {
                b.Append('V');
                b.Append(fmt(offsetY + radius));
            }
            b.Append('Q');
            b.Append(fmt(offsetX));
            b.Append(' ');
            b.Append(fmt(offsetY));
            b.Append(' ');
            b.Append(fmt(offsetX + radius));
            b.Append(' ');
            b.Append(fmt(offsetY));

            b.Append('z');
            return b.ToString();

        }


        /// <summary>
        /// Join multiple paths into one
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static String JoinPaths(IEnumerable<String> paths)
        {
            bool needZ = false;
            StringBuilder b = new StringBuilder();
            foreach (var p in paths)
            {
                if (String.IsNullOrEmpty(p))
                    continue;
                if (needZ)
                    b.Append('z');
                b.Append(p);
                needZ = !p.EndsWith('z');
            }
            return b.ToString();
        }




        public static void VerticalBend(IList<String> paths, double width, double amplitude, double periods = 0.5, double offset = 0)
        {
            MakeAbsolute(paths);

            double toA(double x)
            {
                double rx = x / width + offset;
                return rx * periods * Math.PI * 2;
            }
            ModifyAbsolutePaths(paths, point =>
            {
                var a = toA(point.X);
                point.Y += Math.Sin(a) * amplitude;
            }, 4);
        }


    }


}