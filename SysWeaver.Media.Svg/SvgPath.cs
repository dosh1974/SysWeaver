using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using WaterTrans.GlyphLoader;

namespace SysWeaver.Media
{

    /// <summary>
    /// The shape of a corner, used by <see cref="SvgPath.GetFancyRect(double, double, SvgCornerShape, SvgCornerShape, SvgCornerShape, SvgCornerShape, int, double, double)"/>
    /// </summary>
    public class SvgCornerShape
    {
        /// <summary>
        /// Create a corner shape with the same horizontal and vertical radius
        /// </summary>
        /// <param name="rad">The radius</param>
        /// <param name="exp">The shape: 1 = Rounded, 0 = Line (bevel), -1 = Scooped (values in between blends the quadratic control point)</param>
        public SvgCornerShape(double rad, double exp = 1)
        {
            RadX = rad;
            RadY = rad;
            Exp = exp;
        }

        /// <summary>
        /// Create a corner shape
        /// </summary>
        /// <param name="radX">The horizontal radius</param>
        /// <param name="radY">The vertical radius</param>
        /// <param name="exp">The shape: 1 = Rounded, 0 = Line (bevel), -1 = Scooped (values in between blends the quadratic control point)</param>
        public SvgCornerShape(double radX, double radY, double exp)
        {
            RadX = radX;
            RadY = radY;
            Exp = exp;
        }

        /// <summary>
        /// The horizontal radius (negative values are treated as zero)
        /// </summary>
        public double RadX;
        /// <summary>
        /// The vertical radius (negative values are treated as zero)
        /// </summary>
        public double RadY;
        /// <summary>
        /// The shape: 1 = Rounded, 0 = Line (bevel), -1 = Scooped
        /// </summary>
        public double Exp; // 0 = Line, 1 = Rounded, -1 = Scooped
    }

    /// <summary>
    /// SVG path helpers: parsing (<see cref="Process"/>), bounds, transforms, text to paths, shape generators (n-gons, rounded / fancy rectangles) and extrusion.
    /// The parser supports the commands M, L, H, V, C, S, Q, T, A and Z (absolute and relative) with implicit repeats; numbers are parsed with the invariant culture
    /// and may have an exponent ("1e-3"), they may be separated by any white space, commas or a sign, compact numbers such as "-.5.5" and arc flags without separators ("a1 1 0 015 5") are supported.
    /// The generated paths are formatted with the invariant culture (see <see cref="SvgTools.GetFormat(int)"/>).
    /// </summary>
    public static class SvgPath
    {
        /// <summary>
        /// Update a min/max state with all points of a path (including control points of curves and the arc end points, so the bounds may be larger than the tight bounds)
        /// </summary>
        /// <param name="state">The state to update</param>
        /// <param name="svgPath">The path data</param>
        /// <exception cref="FormatException">The path contains an unsupported command or an invalid number</exception>
        public static void UpdateMinMax(SvgMinMaxState state, String svgPath)
            => Process(svgPath, state.Update);


        /// <summary>
        /// Create the side faces of an extrusion of a path in the direction (dx, dy), one closed face per line / curve segment.
        /// Supports the absolute commands M (with implicit line to's), L, H, V, Q and Z.
        /// A Z extrudes the closing segment (from the current point back to the start of the sub path).
        /// Segments shorter than 0.000001 are skipped (ex: the closing segment of a path that ends with an explicit line to its start point).
        /// </summary>
        /// <param name="path">The path data (absolute commands only)</param>
        /// <param name="dx">The x extrusion delta</param>
        /// <param name="dy">The y extrusion delta</param>
        /// <param name="numDecimals">The maximum number of decimals in the output</param>
        /// <param name="doSort">If true the faces are sorted on the lighting value (ascending)</param>
        /// <returns>A tuple for each face with: The face path, the max projection of the segment end points on (dx, dy) and the lighting value
        /// [0, 1] (the absolute sine of the angle between the segment and the extrusion direction, 1 = perpendicular)</returns>
        /// <exception cref="ArgumentException">Both <paramref name="dx"/> and <paramref name="dy"/> are zero</exception>
        /// <exception cref="NotSupportedException">The path contains a command that is not supported</exception>
        public static Tuple<String, double, double>[] Extrude(String path, double dx, double dy, int numDecimals = 3, bool doSort = true)
        {
            var fmt = SvgTools.GetFormat(numDecimals);
            var dlen = Math.Sqrt(dx * dx + dy * dy);
            if (dlen <= 0)
                throw new ArgumentException("Invalid dx or dy, the extrusion length must be greater than zero", nameof(dx));

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

            //  The face of a line segment
            void AddLine(double x, double y, double nx, double ny)
            {
                svg.Append('M');
                svg.Append(fmt(x));
                svg.Append(' ');
                svg.Append(fmt(y));
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
            }

            //  Start of the current sub path
            double sx = 0;
            double sy = 0;

            Process(path, null, (x, y, cmd, nums) =>
            {
                var nl = nums.Count;
                int i;
                switch (cmd)
                {
                    case 'L':
                        for (i = 0; i < nl; i += 2)
                        {
                            var nx = nums[i];
                            var ny = nums[i + 1];
                            AddLine(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'M':
                        x = nums[0];
                        y = nums[1];
                        sx = x;
                        sy = y;
                        for (i = 2; i < nl; i += 2)
                        {
                            var nx = nums[i];
                            var ny = nums[i + 1];
                            AddLine(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'H':
                        for (i = 0; i < nl; ++ i)
                        {
                            var nx = nums[i];
                            var ny = y;
                            AddLine(x, y, nx, ny);
                            x = nx;
                            y = ny;
                        }
                        break;
                    case 'V':
                        for (i = 0; i < nl; ++i)
                        {
                            var nx = x;
                            var ny = nums[i];
                            AddLine(x, y, nx, ny);
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
                        //  The closing segment (skipped if the current point is the start point)
                        AddLine(x, y, sx, sy);
                        break;
                    default:
                        throw new NotSupportedException("Unsupported command '" + cmd + "'");

                }

            });
            if (doSort)
                shapes.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return shapes.Select(x => Tuple.Create(x.Item1, x.Item3, x.Item2)).ToArray();
        }

        /// <summary>
        /// Parse a path and perform some actions on all points and / or commands.
        /// For every command letter, <paramref name="onCommand"/> is called once (with all numbers, implicit repeats included), then <paramref name="onPoint"/> is called for each point of the command.
        /// A close path (Z / z) moves the current point back to the start of the sub path.
        /// Numbers before the first command are ignored.
        /// Numbers follow the svg path grammar: an optional sign, digits, an optional '.' with digits and an optional exponent ("1e-3");
        /// they are separated by any white space, ',' or the start of the next number ("1-2+3.5.5" is 1, -2, 3.5 and .5).
        /// The large-arc-flag and sweep-flag of arcs may be written as single chars without a separator ("a5 5 0 0110 10").
        /// </summary>
        /// <param name="path">The path data</param>
        /// <param name="onPoint">Called with the absolute coordinates of every end point and of the explicit control points of C, S and Q (not for arc radii), may be null</param>
        /// <param name="onCommand">Called with the current (absolute) point before the command, the command char and the numbers of the command.
        /// The numbers may be modified in place by the callback, the current point tracking (and <paramref name="onPoint"/>) then uses the modified values. May be null</param>
        /// <exception cref="FormatException">The path contains an unsupported command or an invalid number</exception>
        /// <exception cref="ArgumentOutOfRangeException">A command has too few numbers</exception>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null (DEBUG builds only)</exception>
        public static void Process(String path, Action<double, double> onPoint, Action<double, double, Char, IList<double>> onCommand = null)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(path);
#endif//DEBUG
            double x = 0;
            double y = 0;
            //  Start of the current sub path (a close path command moves the current point here)
            double sx = 0;
            double sy = 0;
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
                            if (i == 0)
                            {
                                sx = x;
                                sy = y;
                            }
                            onPoint(x, y);
                        }
                        break;
                    case 'M':
                        for (int i = 0; i < nl; i += 2)
                        {
                            x = nums[i + 0];
                            y = nums[i + 1];
                            if (i == 0)
                            {
                                sx = x;
                                sy = y;
                            }
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
                        x = sx;
                        y = sy;
                        break;
                    default:
                        throw new FormatException("Path command '" + c + "' not understood!");
                }
            }
            var l = path.Length;
            Char code = (Char)0;
            int i = 0;
            while (i < l)
            {
                char c = path[i];
                //  Separators: any white space and ','
                if ((c == ',') || Char.IsWhiteSpace(c))
                {
                    ++i;
                    continue;
                }
                //  Commands (an 'e' / 'E' is only valid as an exponent)
                if (Char.IsLetter(c) && ((c | 0x20) != 'e'))
                {
                    processCommand(code);
                    code = c;
                    nums.Clear();
                    ++i;
                    continue;
                }
                //  The large-arc-flag and sweep-flag of an arc are single chars, they don't need a separator ("0110 10")
                if (((c == '0') || (c == '1')) && (((code | 0x20) == 'a')))
                {
                    var ai = nums.Count % 7;
                    if ((ai == 3) || (ai == 4))
                    {
                        nums.Add(c - '0');
                        ++i;
                        continue;
                    }
                }
                //  A number: optional sign, digits, optional '.' + digits, optional exponent.
                //  A '.' after the decimals or a sign starts a new number ("1.5.5" is 1.5 and .5, "1-2" is 1 and -2)
                var start = i;
                if ((c == '-') || (c == '+'))
                    ++i;
                while ((i < l) && Char.IsAsciiDigit(path[i]))
                    ++i;
                if ((i < l) && (path[i] == '.'))
                {
                    ++i;
                    while ((i < l) && Char.IsAsciiDigit(path[i]))
                        ++i;
                }
                if ((i < l) && ((path[i] | 0x20) == 'e'))
                {
                    var e = i + 1;
                    if ((e < l) && ((path[e] == '-') || (path[e] == '+')))
                        ++e;
                    if ((e < l) && Char.IsAsciiDigit(path[e]))
                    {
                        i = e + 1;
                        while ((i < l) && Char.IsAsciiDigit(path[i]))
                            ++i;
                    }
                }
                if (i == start)
                    throw new FormatException("Invalid char '" + c + "' at position " + i.ToString(CultureInfo.InvariantCulture) + " in the path");
                if (!double.TryParse(path.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    throw new FormatException("Invalid number \"" + path.Substring(start, i - start) + "\" at position " + start.ToString(CultureInfo.InvariantCulture) + " in the path");
                nums.Add(num);
            }
            processCommand(code);
        }


        /// <summary>
        /// Get min/max for some paths (all points of all paths, see <see cref="UpdateMinMax(SvgMinMaxState, string)"/>)
        /// </summary>
        /// <param name="paths">The paths</param>
        /// <returns>A new min/max state (<see cref="SvgMinMaxState.IsEmpty"/> is true if there are no points)</returns>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
        public static SvgMinMaxState GetMinMaxForPaths(IList<String> paths)
        {
            var mm = new SvgMinMaxState();
            var gl = paths.Count;
            for (int i = 0; i < gl; ++i)
                UpdateMinMax(mm, paths[i]);
            return mm;
        }

        /// <summary>
        /// Scale and then translate paths in place: x' = x * scaleX + offsetX, y' = y * scaleY + offsetY.
        /// Relative coordinates (lower case commands) are only scaled, except the first pair of a relative 'm' that starts a path (it's absolute, as in the svg spec).
        /// Arcs are transformed exactly: the radii and the x-axis-rotation describe the transformed ellipse (for a non-uniform scale of a rotated ellipse the radii and the rotation change)
        /// and the sweep flag is flipped if the transform mirrors (scaleX * scaleY &lt; 0).
        /// The paths are reformatted (numbers separated by a single space, implicit repeats are kept).
        /// </summary>
        /// <param name="paths">The paths to transform (replaced in the list)</param>
        /// <param name="scaleX">The x scale</param>
        /// <param name="scaleY">The y scale</param>
        /// <param name="offsetX">The x offset (added after scaling)</param>
        /// <param name="offsetY">The y offset (added after scaling)</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
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
            bool isFirst = true;
            void onCmd(double px, double py, Char code, IList<double> nums)
            {
                var isRel = Char.IsLower(code);
                var tx = isRel ? ScaleRelX : ScaleX;
                var ty = isRel ? ScaleRelY : ScaleY;
                //  The first pair of a relative move to that starts a path is absolute
                var absFirst = isFirst && (code == 'm');
                isFirst = false;
                var c = nums.Count;
                switch (code)
                {
                    case 'h':
                    case 'H':
                        for (int i = 0; i < c; ++i)
                            tx(nums, i);
                        break;
                    case 'v':
                    case 'V':
                        for (int i = 0; i < c; ++i)
                            ty(nums, i);
                        break;
                    case 'a':
                    case 'A':
                        for (int i = 0; i < c; i += 7)
                        {
                            TransformArc(nums, i, scaleX, scaleY);
                            tx(nums, i + 5);
                            ty(nums, i + 6);
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
                    case 's':
                    case 'S':
                    case 't':
                    case 'T':
                        for (int i = 0; i < c; i += 2)
                        {
                            if ((i == 0) && absFirst)
                            {
                                ScaleX(nums, i);
                                ScaleY(nums, i + 1);
                                continue;
                            }
                            tx(nums, i);
                            ty(nums, i + 1);
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
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                isFirst = true;
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }

        /// <summary>
        /// Transform the radii, x-axis-rotation and sweep flag of an arc (at offset o in nums) by a scale
        /// </summary>
        static void TransformArc(IList<double> nums, int o, double scaleX, double scaleY)
        {
            var rx = Math.Abs(nums[o]);
            var ry = Math.Abs(nums[o + 1]);
            var phiDeg = nums[o + 2];
            var mirror = (scaleX * scaleY) < 0;
            //  A mirror reverses the direction of the arc
            if (mirror)
                nums[o + 4] = nums[o + 4] != 0 ? 0 : 1;
            var ax = Math.Abs(scaleX);
            if (ax == Math.Abs(scaleY))
            {
                //  Uniform: the radii are scaled, a mirror negates the rotation
                nums[o] = rx * ax;
                nums[o + 1] = ry * ax;
                if (mirror && (phiDeg != 0))
                    nums[o + 2] = -phiDeg;
                return;
            }
            //  The ellipse is the unit circle transformed by M = Scale * Rotate(phi) * Scale(rx, ry) = [a b; c d],
            //  the axes of the transformed ellipse are the eigen vectors of M * transpose(M) = [p q; q r] (the radii are the square roots of the eigen values)
            var phi = phiDeg * (Math.PI / 180.0);
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            var a = scaleX * cos * rx;
            var b = -scaleX * sin * ry;
            var c = scaleY * sin * rx;
            var d = scaleY * cos * ry;
            var p = a * a + b * b;
            var q = a * c + b * d;
            var r = c * c + d * d;
            var h = 0.5 * (p + r);
            var k = Math.Sqrt(0.25 * (p - r) * (p - r) + q * q);
            var l1 = h + k;
            var l2 = Math.Max(0, h - k);
            //  The direction of the axis with the eigen value l1
            var theta = 0.5 * Math.Atan2(2.0 * q, p - r);
            //  Keep rx on the axis closest to the transformed x-axis of the original ellipse
            var alpha = Math.Atan2(scaleY * sin, scaleX * cos);
            double newPhi, newRx, newRy;
            if (Math.Abs(Math.IEEERemainder(theta - alpha, Math.PI)) <= (Math.PI * 0.25))
            {
                newPhi = theta;
                newRx = Math.Sqrt(l1);
                newRy = Math.Sqrt(l2);
            }
            else
            {
                newPhi = theta + Math.PI * 0.5;
                newRx = Math.Sqrt(l2);
                newRy = Math.Sqrt(l1);
            }
            //  An ellipse is the same when rotated by 180 degrees, use the angle closest to the original
            newPhi = newPhi * (180.0 / Math.PI);
            newPhi = phiDeg + Math.IEEERemainder(newPhi - phiDeg, 180.0);
            //  Avoid "-0" (and rounding noise) in the output
            if (Math.Abs(newPhi) < 1e-9)
                newPhi = 0;
            nums[o] = newRx;
            nums[o + 1] = newRy;
            nums[o + 2] = newPhi;
        }

        /// <summary>
        /// Convert an svg arc (endpoint parameterization) to cubic bezier segments (at most 90 degrees each), see https://www.w3.org/TR/SVG/implnote.html#ArcImplementationNotes.
        /// Six numbers (control point 1, control point 2 and end point) are added per segment.
        /// Nothing is added if the end points are equal, a straight line (as one cubic) is added if a radius is zero.
        /// </summary>
        static void ArcToCubics(List<double> output, double x1, double y1, double rx, double ry, double phiDeg, bool largeArc, bool sweep, double x2, double y2)
        {
            if ((x1 == x2) && (y1 == y2))
                return;
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if ((rx == 0) || (ry == 0))
            {
                var tdx = (x2 - x1) * (1.0 / 3.0);
                var tdy = (y2 - y1) * (1.0 / 3.0);
                output.Add(x1 + tdx);
                output.Add(y1 + tdy);
                output.Add(x2 - tdx);
                output.Add(y2 - tdy);
                output.Add(x2);
                output.Add(y2);
                return;
            }
            var phi = phiDeg * (Math.PI / 180.0);
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            //  Step 1: the start point in the ellipse coordinate system (centered between the end points)
            var hx = 0.5 * (x1 - x2);
            var hy = 0.5 * (y1 - y2);
            var x1p = cos * hx + sin * hy;
            var y1p = -sin * hx + cos * hy;
            //  Too small radii are scaled up
            var lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
            if (lambda > 1)
            {
                var ls = Math.Sqrt(lambda);
                rx *= ls;
                ry *= ls;
            }
            //  Step 2: the center in the ellipse coordinate system
            var rx2 = rx * rx;
            var ry2 = ry * ry;
            var den = rx2 * y1p * y1p + ry2 * x1p * x1p;
            var num = rx2 * ry2 - den;
            var coef = den > 0 ? Math.Sqrt(Math.Max(0, num / den)) : 0;
            if (largeArc == sweep)
                coef = -coef;
            var cxp = coef * rx * y1p / ry;
            var cyp = -coef * ry * x1p / rx;
            //  Step 3: the center
            var cx = cos * cxp - sin * cyp + 0.5 * (x1 + x2);
            var cy = sin * cxp + cos * cyp + 0.5 * (y1 + y2);
            //  Step 4: the start angle and the sweep angle
            var t1 = Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
            var t2 = Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
            var dt = t2 - t1;
            if (sweep && (dt < 0))
                dt += 2.0 * Math.PI;
            else if ((!sweep) && (dt > 0))
                dt -= 2.0 * Math.PI;
            //  The segments
            var n = Math.Max(1, (int)Math.Ceiling(Math.Abs(dt) / (Math.PI * 0.5) - 1e-9));
            var st = dt / n;
            var kk = (4.0 / 3.0) * Math.Tan(st * 0.25);
            var t = t1;
            var ct = Math.Cos(t);
            var stt = Math.Sin(t);
            var px = x1;
            var py = y1;
            for (int i = 1; i <= n; ++i)
            {
                //  The derivative at the start of the segment
                var dx0 = -rx * cos * stt - ry * sin * ct;
                var dy0 = -rx * sin * stt + ry * cos * ct;
                t = t1 + st * i;
                ct = Math.Cos(t);
                stt = Math.Sin(t);
                double ex, ey;
                if (i == n)
                {
                    ex = x2;
                    ey = y2;
                }
                else
                {
                    ex = cx + rx * cos * ct - ry * sin * stt;
                    ey = cy + rx * sin * ct + ry * cos * stt;
                }
                //  The derivative at the end of the segment
                var dx1 = -rx * cos * stt - ry * sin * ct;
                var dy1 = -rx * sin * stt + ry * cos * ct;
                output.Add(px + kk * dx0);
                output.Add(py + kk * dy0);
                output.Add(ex - kk * dx1);
                output.Add(ey - kk * dy1);
                output.Add(ex);
                output.Add(ey);
                px = ex;
                py = ey;
            }
        }



        /// <summary>
        /// A mutable point, used by <see cref="ModifyAbsolutePaths"/>
        /// </summary>
        public sealed class Point
        {
            /// <summary>
            /// The x coordinate
            /// </summary>
            public double X;
            /// <summary>
            /// The y coordinate
            /// </summary>
            public double Y;
        }

        /// <summary>
        /// Apply a custom function on all points of absolute paths (in place), the paths are reformatted (numbers separated by a single space).
        /// The function is called (in path order) with the original coordinates of every end point and every control point of C, S, Q and T.
        /// H / V segments are kept if the modified end point is still axis aligned with the modified start point, else the command is written as an L.
        /// Arcs are converted to cubic beziers (at most 90 degrees per segment), the function is called for their control points and end points
        /// (so a bend or any other non-linear function bends arcs too, a degenerate arc with a zero radius becomes a straight cubic, an arc to the current point is removed).
        /// The same <see cref="Point"/> instance is reused for all calls.
        /// </summary>
        /// <param name="paths">The paths to modify (replaced in the list), may only contain absolute commands (and 'z')</param>
        /// <param name="onPoint">Called for each point, change the point to change the path</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="ArgumentException">A path contains a relative command</exception>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
        public static void ModifyAbsolutePaths(IList<String> paths, Action<Point> onPoint, int maxDecimals = 4)
        {
            var gl = paths.Count;
            Point point = new Point();
            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
            //  The modified points
            List<double> temp = new List<double>();
            //  The modified current point and start of the sub path
            double ox = 0;
            double oy = 0;
            double osx = 0;
            double osy = 0;

            void Modify(double x, double y)
            {
                point.X = x;
                point.Y = y;
                onPoint(point);
                temp.Add(point.X);
                temp.Add(point.Y);
            }

            void Append(Char code, int start, int step)
            {
                b.Append(code);
                var c = temp.Count;
                for (int i = start; i < c; i += step)
                {
                    if (i != start)
                        b.Append(' ');
                    b.Append(fmt(temp[i]));
                }
            }

            //  The nums are not modified, so px, py is the original current point
            void onCmd(double px, double py, Char code, IList<double> nums)
            {
                if (Char.IsLower(code) && (code != 'z'))
                    throw new ArgumentException("May only use absolute paths!", nameof(paths));
                var c = nums.Count;
                temp.Clear();
                switch (code)
                {
                    case 'H':
                    case 'V':
                        {
                            var isH = code == 'H';
                            //  Still axis aligned if the other coordinate is unchanged
                            bool aligned = true;
                            var prev = isH ? oy : ox;
                            for (int i = 0; i < c; ++i)
                            {
                                if (isH)
                                    Modify(nums[i], py);
                                else
                                    Modify(px, nums[i]);
                                var other = isH ? point.Y : point.X;
                                aligned &= other == prev;
                                prev = other;
                            }
                            if (aligned)
                                Append(code, isH ? 0 : 1, 2);
                            else
                                Append('L', 0, 1);
                        }
                        break;
                    case 'A':
                        {
                            var x = px;
                            var y = py;
                            var arcs = new List<double>();
                            for (int i = 0; i < c; i += 7)
                            {
                                var ex = nums[i + 5];
                                var ey = nums[i + 6];
                                ArcToCubics(arcs, x, y, nums[i], nums[i + 1], nums[i + 2], nums[i + 3] != 0, nums[i + 4] != 0, ex, ey);
                                x = ex;
                                y = ey;
                            }
                            var ac = arcs.Count;
                            for (int i = 0; i < ac; i += 2)
                                Modify(arcs[i], arcs[i + 1]);
                            if (ac > 0)
                                Append('C', 0, 1);
                        }
                        break;
                    case 'M':
                    case 'L':
                    case 'C':
                    case 'Q':
                    case 'S':
                    case 'T':
                        for (int i = 0; i < c; i += 2)
                            Modify(nums[i], nums[i + 1]);
                        Append(code, 0, 1);
                        if ((code == 'M') && (c >= 2))
                        {
                            osx = temp[0];
                            osy = temp[1];
                        }
                        break;
                    case 'z':
                    case 'Z':
                        b.Append(code);
                        ox = osx;
                        oy = osy;
                        return;
                    default:
                        for (int i = 0; i < c; ++i)
                            temp.Add(nums[i]);
                        Append(code, 0, 1);
                        return;
                }
                var tc = temp.Count;
                if (tc >= 2)
                {
                    ox = temp[tc - 2];
                    oy = temp[tc - 1];
                }
            }
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                ox = 0;
                oy = 0;
                osx = 0;
                osy = 0;
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }


        /// <summary>
        /// Reformat paths in place: numbers are rounded to <paramref name="maxDecimals"/> decimals and separated by a space (no space before a '-').
        /// The commands are kept as is (no conversion between relative and absolute).
        /// </summary>
        /// <param name="paths">The paths to optimize (replaced in the list)</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
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
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }



        static readonly Char[] SplitZ = "zZ".ToCharArray();

        /// <summary>
        /// Compute the approximate area of a path and the centroid of its largest region.
        /// The path is split into regions at every Z, each region is treated as a polygon of all its points (curve control points included),
        /// so a region after a Z should start with an absolute move to.
        /// </summary>
        /// <param name="x">The x coordinate of the centroid of the region with the largest area (0 if there are no regions with an area)</param>
        /// <param name="y">The y coordinate of the centroid of the region with the largest area (0 if there are no regions with an area)</param>
        /// <param name="path">The path data</param>
        /// <returns>The sum of the (absolute) areas of all regions</returns>
        /// <exception cref="FormatException">The path contains an unsupported command or an invalid number</exception>
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

        /// <summary>
        /// Find an approximate "pole of inaccessibility": a point inside the path that is as far as possible from the edges of the region (polygon) that contains it.
        /// The path is split into regions at every Z (each treated as a polygon of all its points), a 49x49 grid search over the bounds is refined iteratively.
        /// Useful for placing a label inside a shape.
        /// </summary>
        /// <param name="x">The x coordinate of the found point (0 if no grid point is inside the path)</param>
        /// <param name="y">The y coordinate of the found point (0 if no grid point is inside the path)</param>
        /// <param name="path">The path data</param>
        /// <param name="xPrio">The x coordinates are divided by this value during the search, a value greater than 1 makes horizontal distances count less (prefers wide areas)</param>
        /// <exception cref="FormatException">The path contains an unsupported command or an invalid number</exception>
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
        /// Convert the relative commands m, l, t, h, v and z of paths to absolute commands (in place), the numbers are reformatted.
        /// </summary>
        /// <param name="paths">The paths to convert (replaced in the list)</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="NotSupportedException">A path contains a relative c, s, q or a command</exception>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
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
                            throw new NotSupportedException("Converting the relative command '" + code + "' to absolute is not implemented!");
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
            for (int i = 0; i < gl; ++i)
            {
                b.Clear();
                Process(paths[i], null, onCmd);
                paths[i] = b.ToString();
            }

        }



        /// <summary>
        /// Fit some path(s) within an area (in place): uniformly scaled so that the bounds (see <see cref="GetMinMaxForPaths"/>) fit, and centered in the area
        /// </summary>
        /// <param name="paths">The paths to fit (replaced in the list)</param>
        /// <param name="maxWidth">The width of the area</param>
        /// <param name="maxHeigth">The height of the area</param>
        /// <param name="offsetX">The left position of the area</param>
        /// <param name="offsetY">The top position of the area</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
        public static void FitPaths(IList<String> paths, double maxWidth, double maxHeigth, double offsetX = 0, double offsetY = 0, int maxDecimals = 4)
            =>
            FitPaths(paths, GetMinMaxForPaths(paths), maxWidth, maxHeigth, offsetX, offsetY, maxDecimals);

        /// <summary>
        /// Fit some path(s) within an area (in place): uniformly scaled so that the supplied bounds fit, and centered in the area
        /// </summary>
        /// <param name="paths">The paths to fit (replaced in the list)</param>
        /// <param name="mm">The bounds of the paths (or any other bounds to fit)</param>
        /// <param name="width">The width of the area</param>
        /// <param name="height">The height of the area</param>
        /// <param name="offsetX">The left position of the area</param>
        /// <param name="offsetY">The top position of the area</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
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

        /// <summary>
        /// Uniformly scale paths (in place) to fit within maxWidth x maxHeight and place them inside the margins.
        /// The content size gets the max size in the limiting dimension and the scaled size (rounded up) in the other, the paths are centered in it.
        /// </summary>
        /// <param name="paths">The paths to normalize (replaced in the list)</param>
        /// <param name="maxWidth">The max content width</param>
        /// <param name="maxHeigth">The max content height</param>
        /// <param name="marginTop">The top margin</param>
        /// <param name="marginRight">The right margin</param>
        /// <param name="marginBottom">The bottom margin</param>
        /// <param name="marginLeft">The left margin</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <returns>The total size (content size + margins) as (width, height)</returns>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
        public static Tuple<double, double> NormalizeInto(IList<String> paths, double maxWidth, double maxHeigth, double marginTop = 4, double marginRight = 4, double marginBottom = 4, double marginLeft = 4, int maxDecimals = 4)
            =>
            NormalizeInto(paths, GetMinMaxForPaths(paths), maxWidth, maxHeigth, marginTop, marginRight, marginBottom, marginLeft, maxDecimals);


        /// <summary>
        /// Uniformly scale paths (in place) so that the supplied bounds fit within maxWidth x maxHeight and place them inside the margins.
        /// The content size gets the max size in the limiting dimension and the scaled size in the other, the paths are centered in it.
        /// The applied transform is x' = x * scale + postX, y' = y * scale + postY.
        /// </summary>
        /// <param name="scale">The applied scale</param>
        /// <param name="postX">The applied x offset (after scaling)</param>
        /// <param name="postY">The applied y offset (after scaling)</param>
        /// <param name="paths">The paths to normalize (replaced in the list)</param>
        /// <param name="mm">The bounds of the paths</param>
        /// <param name="maxWidth">The max content width</param>
        /// <param name="maxHeigth">The max content height</param>
        /// <param name="marginTop">The top margin</param>
        /// <param name="marginRight">The right margin</param>
        /// <param name="marginBottom">The bottom margin</param>
        /// <param name="marginLeft">The left margin</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="round">If true, the content size is rounded up to whole units</param>
        /// <returns>The total size (content size + margins) as (width, height)</returns>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
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

        /// <summary>
        /// Uniformly scale paths (in place) so that the supplied bounds fit within maxWidth x maxHeight and place them inside the margins,
        /// see <see cref="NormalizeInto(out double, out double, out double, IList{string}, SvgMinMaxState, double, double, double, double, double, double, int, bool)"/>
        /// </summary>
        /// <param name="paths">The paths to normalize (replaced in the list)</param>
        /// <param name="mm">The bounds of the paths</param>
        /// <param name="maxWidth">The max content width</param>
        /// <param name="maxHeigth">The max content height</param>
        /// <param name="marginTop">The top margin</param>
        /// <param name="marginRight">The right margin</param>
        /// <param name="marginBottom">The bottom margin</param>
        /// <param name="marginLeft">The left margin</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="round">If true, the content size is rounded up to whole units</param>
        /// <returns>The total size (content size + margins) as (width, height)</returns>
        /// <exception cref="FormatException">A path contains an unsupported command or an invalid number</exception>
        public static Tuple<double, double> NormalizeInto(IList<String> paths, SvgMinMaxState mm, double maxWidth, double maxHeigth, double marginTop = 4, double marginRight = 4, double marginBottom = 4, double marginLeft = 4, int maxDecimals = 4, bool round = true)
            => NormalizeInto(out var s, out var x, out var y, paths, mm, maxWidth, maxHeigth, marginTop, marginRight, marginBottom, marginLeft, maxDecimals, round);


        /// <summary>
        /// Generate svg paths from text (one path per glyph that has an outline, laid out on a single line starting at x = 0).
        /// Characters that are missing in the font are skipped (they don't advance the position), there is no kerning or line breaking.
        /// </summary>
        /// <param name="text">The text to convert</param>
        /// <param name="font">The font to use</param>
        /// <param name="size">The font size (em size in output units)</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output coordinates</param>
        /// <returns>The glyph paths (absolute coordinates, y grows downwards), may be empty</returns>
        /// <exception cref="ArgumentNullException"><paramref name="font"/> is null, or <paramref name="text"/> is null (DEBUG builds only)</exception>
        public static List<String> GetSvgTextPaths(String text, SvgFont font, double size = 12, int maxDecimals = 3)
        {
            Typeface tf = font?.TF;
            if (tf == null)
                throw new ArgumentNullException(nameof(font), "No font supplied!");
#if DEBUG
            ArgumentNullException.ThrowIfNull(text);
#endif//DEBUG
            double x = 0, y = 0;
            List<String> paths = new List<String>(text.Length);
            var dec = maxDecimals;
            //  The glyph outline formatting is culture dependent, so use the invariant culture while formatting (restored when done)
            var cult = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                foreach (char c in text)
                {
                    // Get glyph index
                    if (!tf.CharacterToGlyphMap.TryGetValue((int)c, out var glyphIndex))
                        continue;

                    // Get glyph outline
                    var geometry = tf.GetGlyphOutline(glyphIndex, size);

                    // Get advanced width
                    var advanceWidth = tf.AdvanceWidths[glyphIndex] * size;

                    // Get baseline
                    double baseline = tf.Baseline * size;

                    // Convert to path mini-language
                    var figs = geometry.Figures;
                    string miniLanguage = figs.ToString(x, y + baseline, dec);
                    if (!String.IsNullOrEmpty(miniLanguage))
                        paths.Add(miniLanguage);
                    x += advanceWidth;
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = cult;
            }
            return paths;
        }


        /// <summary>
        /// Create svg paths from some text (one path per glyph).
        /// If both <see cref="SvgTextPathParams.FitWidth"/> and <see cref="SvgTextPathParams.FitHeight"/> are greater than zero the text is fitted (and centered)
        /// in that area at (<see cref="SvgTextPathParams.MarginX"/>, <see cref="SvgTextPathParams.MarginY"/>), else <see cref="SvgTextPathParams.Size"/> is used as the font size (margins are ignored).
        /// </summary>
        /// <param name="text">The text to convert</param>
        /// <param name="p">The parameters</param>
        /// <returns>The glyph paths</returns>
        /// <exception cref="ArgumentNullException">The font is null</exception>
        public static List<String> GetSvgTextPaths(String text, SvgTextPathParams p)
        {
            var fitWidth = p.FitWidth;
            var fitHeight = p.FitHeight;
            var fit = fitWidth > 0 && fitHeight > 0;
            var marginX = p.MarginX;
            var marginY = p.MarginY;
            double unit = fit ? Math.Max(fitWidth, fitHeight) * 0.5 : p.Size;
            var paths = GetSvgTextPaths(text, p.Font, unit, Math.Min(15, p.MaxDecimals));
            if (fit)
                FitPaths(paths, fitWidth, fitHeight, marginX, marginY, p.MaxDecimals);
            return paths;
        }

        /// <summary>
        /// Create a svg path in the shape of a regular N-gon, centered in a size x size square.
        /// Odd n have a vertex at the top, even n have a flat top edge (before applying <paramref name="angleAdjust"/>).
        /// The shape is centered on the square center and scaled so that the vertex farthest from the center (horizontally or vertically) touches the square,
        /// so odd n only touch the top (and the sides if wider than high).
        /// </summary>
        /// <param name="n">The number of vertices, must be at least 3</param>
        /// <param name="size">The size (width and height) to fit the shape inside</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="angleAdjust">Rotation in degrees (clockwise, y down)</param>
        /// <param name="offsetX">The left position of the square</param>
        /// <param name="offsetY">The top position of the square</param>
        /// <returns>A closed path: "M" + the last vertex, "L" + all vertices and "z"</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is less than 3</exception>
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
        /// Create a svg path in the shape of a rounded rectangle (the corners are quadratic curves with the control point in the corner)
        /// </summary>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="radius">The corner radius, clamped to half of the smallest side</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="offsetX">The left position</param>
        /// <param name="offsetY">The top position</param>
        /// <returns>A closed path (ends with "z")</returns>
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
        /// Create a svg path in the shape of a rectangle with the same shape for all corners
        /// </summary>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="shape">Shape of the corners</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="offsetX">The left position</param>
        /// <param name="offsetY">The top position</param>
        /// <returns>A closed path (ends with "z")</returns>
        /// <exception cref="ArgumentNullException"><paramref name="shape"/> is null (DEBUG builds only)</exception>
        public static String GetFancyRect(double width, double height, SvgCornerShape shape, int maxDecimals = 3, double offsetX = 0, double offsetY = 0)
            => GetFancyRect(width, height, shape, shape, shape, shape, maxDecimals, offsetX, offsetY);


        /// <summary>
        /// Create a svg path in the shape of a rectangle with specific corner shapes.
        /// Negative radii are treated as zero (a sharp corner), if the radii of two corners on the same side are larger than that side they are scaled down to fit.
        /// </summary>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="topLeft">Shape of top left corner</param>
        /// <param name="topRight">Shape of top right corner</param>
        /// <param name="bottomRight">Shape of bottom right corner</param>
        /// <param name="bottomLeft">Shape of bottom left corner</param>
        /// <param name="maxDecimals">The maximum number of decimals in the output</param>
        /// <param name="offsetX">The left position</param>
        /// <param name="offsetY">The top position</param>
        /// <returns>A closed path (ends with "z")</returns>
        /// <exception cref="ArgumentNullException">A corner shape is null (DEBUG builds only)</exception>
        public static String GetFancyRect(double width, double height, SvgCornerShape topLeft, SvgCornerShape topRight, SvgCornerShape bottomRight, SvgCornerShape bottomLeft, int maxDecimals = 3, double offsetX = 0, double offsetY = 0)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(topLeft);
            ArgumentNullException.ThrowIfNull(topRight);
            ArgumentNullException.ThrowIfNull(bottomRight);
            ArgumentNullException.ThrowIfNull(bottomLeft);
#endif//DEBUG
            var radTopLeftX = Math.Max(0, topLeft.RadX);
            var radTopLeftY = Math.Max(0, topLeft.RadY);

            var radTopRightX = Math.Max(0, topRight.RadX);
            var radTopRightY = Math.Max(0, topRight.RadY);

            var radBottomRightX = Math.Max(0, bottomRight.RadX);
            var radBottomRightY = Math.Max(0, bottomRight.RadY);

            var radBottomLeftX = Math.Max(0, bottomLeft.RadX);
            var radBottomLeftY = Math.Max(0, bottomLeft.RadY);

            //  Validate radie and determine what edges are available

            var sum = radTopLeftX + radTopRightX;
            var haveTop = sum < width;
            if (sum > width)
            {
                radTopLeftX *= (width / sum);
                radTopRightX = width - radTopLeftX;
            }

            sum = radBottomLeftX + radBottomRightX;
            var haveBottom = sum < width;
            if (sum > width)
            {
                radBottomLeftX *= (width / sum);
                radBottomRightX = width - radBottomLeftX;
            }

            sum = radTopLeftY + radBottomLeftY;
            var haveLeft = sum < height;
            if (sum > height)
            {
                radTopLeftY *= (height / sum);
                radBottomLeftY = height - radTopLeftY;
            }

            sum = radTopRightY + radBottomRightY;
            var haveRight = sum < height;
            if (sum > height)
            {
                radTopRightY *= (height / sum);
                radBottomRightY = height - radTopRightY;
            }

            var x1 = offsetX + width;
            var y1 = offsetY + height;

            var fmt = SvgTools.GetFormat(maxDecimals);
            StringBuilder b = new StringBuilder();
        //  Top
            b.Append('M');
            b.Append(fmt(offsetX + radTopLeftX));
            b.Append(' ');
            b.Append(fmt(offsetY));
            if (haveTop)
            {
                b.Append('H');
                b.Append(fmt(x1 - radTopRightX));
            }
            //  Top right
            if ((radTopRightX > 0) || (radTopRightY > 0))
            {
                var exp = topRight.Exp;
                if (exp != 0)
                {
                    var scale = exp * 0.5 + 0.5;
                    b.Append('Q');
                    b.Append(fmt(x1 - radTopRightX + scale * radTopRightX));
                    b.Append(' ');
                    b.Append(fmt(offsetY + radTopRightY - scale * radTopRightY));
                    b.Append(' ');
                }else
                {
                    b.Append('L');
                }
                b.Append(fmt(x1));
                b.Append(' ');
                b.Append(fmt(offsetY + radTopRightY));
            }
            //  Right
            if (haveRight)
            {
                b.Append('V');
                b.Append(fmt(y1 - radBottomRightY));
            }
            //  Bottom right
            if ((radBottomRightX > 0) || (radBottomRightY > 0))
            {
                var exp = bottomRight.Exp;
                if (exp != 0)
                {
                    var scale = exp * 0.5 + 0.5;
                    b.Append('Q');
                    b.Append(fmt(x1 - radBottomRightX + scale * radBottomRightX));
                    b.Append(' ');
                    b.Append(fmt(y1 - radBottomRightY + scale * radBottomRightY));
                    b.Append(' ');
                }
                else
                {
                    b.Append('L');
                }
                b.Append(fmt(x1 - radBottomRightX));
                b.Append(' ');
                b.Append(fmt(y1));
            }
            //  Bottom
            if (haveBottom)
            {
                b.Append('H');
                b.Append(fmt(offsetX + radBottomLeftX));
            }
            //  Bottom left
            if ((radBottomLeftX > 0) || (radBottomLeftY > 0))
            {
                var exp = bottomLeft.Exp;
                if (exp != 0)
                {
                    var scale = exp * 0.5 + 0.5;
                    b.Append('Q');
                    b.Append(fmt(offsetX + radBottomLeftX - scale * radBottomLeftX));
                    b.Append(' ');
                    b.Append(fmt(y1 - radBottomLeftY + scale * radBottomLeftY));
                    b.Append(' ');
                }
                else
                {
                    b.Append('L');
                }
                b.Append(fmt(offsetX));
                b.Append(' ');
                b.Append(fmt(y1 - radBottomLeftY));
            }
            //  Left
            if (haveLeft)
            {
                b.Append('V');
                b.Append(fmt(offsetY + radTopLeftY));
            }
            //  Top left
            if ((radTopLeftX > 0) || (radTopLeftY > 0))
            {
                var exp = topLeft.Exp;
                if (exp != 0)
                {
                    var scale = exp * 0.5 + 0.5;
                    scale = topLeft.Exp * 0.5 + 0.5;
                    b.Append('Q');
                    b.Append(fmt(offsetX + radTopLeftX - scale * radTopLeftX));
                    b.Append(' ');
                    b.Append(fmt(offsetY + radTopLeftY - scale * radTopLeftY));
                    b.Append(' ');
                }
                else
                {
                    b.Append('L');
                }
                b.Append(fmt(offsetX + radTopLeftX));
                b.Append(' ');
                b.Append(fmt(offsetY));
            }
            b.Append('z');
            return b.ToString();
        }




        /// <summary>
        /// Join multiple paths into one, a "z" is inserted between two paths if the first one isn't closed (doesn't end with 'z' or 'Z').
        /// The last path is not closed.
        /// </summary>
        /// <param name="paths">The paths to join, null and empty paths are skipped</param>
        /// <returns>The joined path (empty if there are no non-empty paths)</returns>
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
                needZ = !(p.EndsWith('z') || p.EndsWith('Z'));
            }
            return b.ToString();
        }



        /// <summary>
        /// Bend paths vertically (in place) along a sine wave: y' = y + sin(2π * periods * (x / width + offset)) * amplitude.
        /// The paths are first made absolute (see <see cref="MakeAbsolute"/>), then the points are modified (see <see cref="ModifyAbsolutePaths"/>:
        /// only end points and control points are moved, so straight lines stay straight; bent H / V segments become L segments, arcs become cubic beziers).
        /// </summary>
        /// <param name="paths">The paths to bend (replaced in the list)</param>
        /// <param name="width">The width that corresponds to one unit of the wave phase</param>
        /// <param name="amplitude">The amplitude of the bend</param>
        /// <param name="periods">The number of periods over <paramref name="width"/></param>
        /// <param name="offset">The phase offset (as a fraction of <paramref name="width"/>)</param>
        /// <exception cref="NotSupportedException">A path contains a relative c, s, q or a command</exception>
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