using System;
using System.Globalization;

namespace SysWeaver.Media
{
    /// <summary>
    /// Axis aligned bounds (min / max of points), updated using <see cref="Update"/>.
    /// A new instance is empty (<see cref="IsEmpty"/> is true, the min values are <see cref="double.MaxValue"/> and the max values <see cref="double.MinValue"/>).
    /// Not thread safe.
    /// </summary>
    public sealed class SvgMinMaxState
    {
        static String F(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>
        /// "Empty" or "[MinX, MinY] - [MaxX, MaxY] WidthxHeight" (2 decimals, invariant culture)
        /// </summary>
        /// <returns>A text representation</returns>
        public override string ToString() => IsEmpty ? "Empty" : String.Concat('[', F(MinX), ", ", F(MinY), "] - [", F(MaxX), ", ", F(MaxY), "] ", F(MaxX - MinX), 'x', F(MaxY - MinY));

        /// <summary>
        /// True if no point has been added, or if an <see cref="Expand(double, double, double, double)"/> resulted in a width or height that is zero or less
        /// </summary>
        public bool IsEmpty { get; private set; } = true;

        /// <summary>
        /// The width (MaxX - MinX), 0 if empty
        /// </summary>
        public double Width => IsEmpty ? 0 : (MaxX - MinX);
        /// <summary>
        /// The height (MaxY - MinY), 0 if empty
        /// </summary>
        public double Height => IsEmpty ? 0 : (MaxY - MinY);

        /// <summary>
        /// The min x value
        /// </summary>
        public double MinX = double.MaxValue;
        /// <summary>
        /// The min y value
        /// </summary>
        public double MinY = double.MaxValue;
        /// <summary>
        /// The max x value
        /// </summary>
        public double MaxX = double.MinValue;
        /// <summary>
        /// The max y value
        /// </summary>
        public double MaxY = double.MinValue;

        /// <summary>
        /// Expand the bounds (negative values shrinks the bounds).
        /// Note that the bounds of a single point (or a horizontal / vertical line) becomes empty when expanded by zero.
        /// </summary>
        /// <param name="top">Subtracted from MinY</param>
        /// <param name="right">Added to MaxX</param>
        /// <param name="bottom">Added to MaxY</param>
        /// <param name="left">Subtracted from MinX</param>
        /// <returns>This instance</returns>
        public SvgMinMaxState Expand(double top, double right, double bottom, double left)
        {
            MinY -= top;
            MaxX += right;
            MaxY += bottom;
            MinX -= left;
            IsEmpty = (MaxY <= MinY) || (MaxX <= MinX);
            return this;
        }

        /// <summary>
        /// Expand the bounds horizontally and vertically, see <see cref="Expand(double, double, double, double)"/>
        /// </summary>
        /// <param name="horizontal">Subtracted from MinX and added to MaxX</param>
        /// <param name="vertical">Subtracted from MinY and added to MaxY</param>
        /// <returns>This instance</returns>
        public SvgMinMaxState Expand(double horizontal, double vertical)
            => Expand(vertical, horizontal, vertical, horizontal);

        /// <summary>
        /// Expand the bounds in all directions, see <see cref="Expand(double, double, double, double)"/>
        /// </summary>
        /// <param name="d">The amount to expand in all directions</param>
        /// <returns>This instance</returns>
        public SvgMinMaxState Expand(double d)
            => Expand(d, d, d, d);

        /// <summary>
        /// Add a point (x, y) to the bounds (clears <see cref="IsEmpty"/>), can be used as a callback, ex: for <see cref="SvgPath.Process"/>
        /// </summary>
        public readonly Action<double, double> Update;

        /// <summary>
        /// Create an empty state
        /// </summary>
        public SvgMinMaxState()
        {
            Update = IntAction;
        }

        void IntAction(double x, double y)
        {
            if (x < MinX)
                MinX = x;
            if (x > MaxX)
                MaxX = x;

            if (y < MinY)
                MinY = y;
            if (y > MaxY)
                MaxY = y;
            IsEmpty = false;
        }
    }


}