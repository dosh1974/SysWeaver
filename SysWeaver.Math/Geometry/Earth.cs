using System;


namespace SysWeaver.Geometry
{

    /// <summary>
    /// Simple geometric calculations on the Earth (spherical model for distances).
    /// </summary>
    public static class Earth
    {

        /// <summary>
        /// The circumference of the Earth around the equator in km.
        /// </summary>
        public const double EquatorCircumferenceKm = 40_075.017;
        /// <summary>
        /// The circumference of the Earth around the poles (meridional) in km.
        /// </summary>
        public const double PoleCircumferenceKm = 40_007.863;

        /// <summary>
        /// The radius of a circle with the circumference <see cref="EquatorCircumferenceKm"/> in km.
        /// </summary>
        public const double EquatorRadieKm = EquatorCircumferenceKm / (Math.PI * 2);
        /// <summary>
        /// The radius of a circle with the circumference <see cref="PoleCircumferenceKm"/> in km.
        /// </summary>
        public const double PoleRadieKm = PoleCircumferenceKm / (Math.PI * 2);

        /// <summary>
        /// <see cref="PoleCircumferenceKm"/> / <see cref="EquatorCircumferenceKm"/>, used to flatten the y axis in <see cref="ToCartesian"/>.
        /// </summary>
        public const double PoleRatio = PoleCircumferenceKm / EquatorCircumferenceKm;


        /// <summary>
        /// Convert a latitude / longitude to cartesian coordinates on a unit sphere with a flattened (and flipped) y axis:
        /// x = cos(lat) * cos(lon), y = -sin(lat) * <see cref="PoleRatio"/>, z = cos(lat) * sin(lon).
        /// The inverse is <see cref="ToLatLong"/>.
        /// </summary>
        /// <param name="x">The x coordinate (towards latitude 0, longitude 0).</param>
        /// <param name="y">The y coordinate (negative towards the north pole).</param>
        /// <param name="z">The z coordinate (towards latitude 0, longitude 90).</param>
        /// <param name="latitude">The latitude in degrees.</param>
        /// <param name="longitude">The longitude in degrees.</param>
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


        /// <summary>
        /// Convert cartesian coordinates (as computed by <see cref="ToCartesian"/>) to a latitude / longitude.
        /// The vector doesn't have to be normalized (only the direction is used).
        /// </summary>
        /// <param name="latitude">The latitude in degrees [-90, 90].</param>
        /// <param name="longitude">The longitude in degrees [-180, 180].</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        public static void ToLatLong(out double latitude, out double longitude, double x, double y, double z)
        {
            var theta = Math.Atan2(y / (-PoleRatio), Math.Sqrt(x * x + z * z));
            var phi = Math.Atan2(z, x);
            var fromRad = 180 / Math.PI;
            latitude = theta * fromRad;
            longitude = phi * fromRad;
        }


        /// <summary>
        ///     Gets the (absolute) difference between two angles in radians, taking the wrap around at 2π into account.
        ///     The angles are expected to be within 2π of each other (ex: both in [0, 2π) or both in [-π, π]).
        /// </summary>
        /// <param name="phi">The first angle.</param>
        /// <param name="psi">The second angle.</param>
        /// <returns>The difference between the angles [0, π].</returns>
        public static double AngleDifference(this double phi, double psi)
        {
            var diff = Math.Abs(phi - psi);
            return diff <= Math.PI ? diff : 2 * Math.PI - diff;
        }


        /// <summary>
        /// Compute the great circle distance between two points (spherical law of cosines using the radius <see cref="EquatorRadieKm"/>).
        /// </summary>
        /// <param name="latitudeA">The latitude of the first point in degrees.</param>
        /// <param name="longitudeA">The longitude of the first point in degrees.</param>
        /// <param name="latitudeB">The latitude of the second point in degrees.</param>
        /// <param name="longitudeB">The longitude of the second point in degrees.</param>
        /// <returns>The distance in km [0, π * <see cref="EquatorRadieKm"/>].</returns>
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
            return EquatorRadieKm * Math.Acos(Math.Clamp(
                Math.Sin(latitudeA) * Math.Sin(latitudeB) +
                Math.Cos(latitudeA) * Math.Cos(latitudeB) * Math.Cos(longitudeDelta), -1.0, 1.0));



        }

    }






}
