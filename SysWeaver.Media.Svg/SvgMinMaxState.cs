using System;
using System.Globalization;

namespace SysWeaver.Media
{
    public sealed class SvgMinMaxState
    {
        static String F(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);
        public override string ToString() => IsEmpty ? "Empty" : String.Concat('[', F(MinX), ", ", F(MinY), "] - [", F(MaxX), ", ", F(MaxY), "] ", F(MaxX - MinX), 'x', F(MaxY - MinY));

        public bool IsEmpty { get; private set; }

        public double Width => IsEmpty ? 0 : (MaxX - MinX);
        public double Height => IsEmpty ? 0 : (MaxY - MinY);

        public double MinX = double.MaxValue;
        public double MinY = double.MaxValue;
        public double MaxX = double.MinValue;
        public double MaxY = double.MinValue;

        public readonly Action<double, double> Update;

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