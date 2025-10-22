using System;


namespace SysWeaver.Geometry
{

    public static class Earth
    {

        public const double EquatorCircumferenceKm = 40_075.017;
        public const double PoleCircumferenceKm = 40_007.863;

        public const double EquatorRadieKm = EquatorCircumferenceKm / (Math.PI * 2);
        public const double PoleRadieKm = PoleCircumferenceKm / (Math.PI * 2);

        public const double PoleRatio = PoleCircumferenceKm / EquatorCircumferenceKm;


        public static void ToCartesian(out double x, out double y, out double z, double latitude, double longitude)
        {
            var toRad = Math.PI / 180;
            var p = toRad * longitude;
            var t = toRad * latitude;
            var cp = Math.Cos(p);
            var sp = Math.Sin(p);
            var ct = Math.Cos(t);
            var st = Math.Sin(t);
            x = cp * ct;
            y = st * (-PoleRatio);
            z = sp * ct;
        }


        public static void ToLatLong(out double latitude, out double longitude, double x, double y, double z)
        {
            var theta = Math.Atan2(y / (-PoleRatio), Math.Sqrt(x * x + z * z));
            var phi = Math.Atan2(z, x);
            var fromRad = 180 / Math.PI;
            latitude = theta * fromRad;
            longitude = phi * fromRad;
        }


        /// <summary>
        ///     Gets the difference between two angles in radians.
        /// </summary>
        /// <param name="phi">The first angle.</param>
        /// <param name="psi">The second angle.</param>
        /// <returns>The difference between the angles.</returns>
        public static double AngleDifference(this double phi, double psi)
        {
            var diff = Math.Abs(phi - psi);
            return diff <= Math.PI ? diff : 2 * Math.PI - diff;
        }


        public static double DistanceKm(
            double latitudeA, double longitudeA, 
            double latitudeB, double longitudeB)
        {
            var toRad = Math.PI / 180;
            latitudeA *= toRad;
            longitudeA *= toRad;
            latitudeB *= toRad;
            longitudeB *= toRad;

            double longitudeDelta = longitudeA.AngleDifference(longitudeB);
            return EquatorRadieKm * Math.Acos(
                Math.Sin(latitudeA) * Math.Sin(latitudeB) +
                Math.Cos(latitudeA) * Math.Cos(latitudeB) * Math.Cos(longitudeDelta));



        }

    }






}
