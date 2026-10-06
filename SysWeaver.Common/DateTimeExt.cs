using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// DateTime and TimeSpan extensions
    /// </summary>
    public static class DateTimeExt
    {
        /// <summary>
        /// Return the time stamp that happened first, aka Min(a, b)
        /// </summary>
        /// <param name="a">First time stamp</param>
        /// <param name="b">Second time stamp</param>
        /// <returns>The first time stamp (has the lowest value)</returns>
        /// <remarks>Only the ticks are compared, the kind is ignored (no time zone conversions are made)</remarks>
        public static DateTime First(this DateTime a, DateTime b)
            => a < b ? a : b;

        /// <summary>
        /// Return the time stamp that happened last, aka Max(a, b)
        /// </summary>
        /// <param name="a">First time stamp</param>
        /// <param name="b">Second time stamp</param>
        /// <returns>The last time stamp (has the highest value)</returns>
        /// <remarks>Only the ticks are compared, the kind is ignored (no time zone conversions are made)</remarks>
        public static DateTime Last(this DateTime a, DateTime b)
            => a > b ? a : b;

        /// <summary>
        /// Return a new DateTime with a specific kind (no conversion is made, the ticks are unchanged)
        /// </summary>
        /// <param name="t">The time stamp</param>
        /// <param name="kind">New kind</param>
        /// <returns>New DateTime with the specific kind</returns>
        /// <exception cref="ArgumentException"><paramref name="kind"/> is not a valid DateTimeKind</exception>
        public static DateTime AsKind(this DateTime t, DateTimeKind kind)
            => DateTime.SpecifyKind(t, kind);


        /// <summary>
        /// Return a new DateTime with a specific time set (the date and kind are unchanged)
        /// </summary>
        /// <param name="t">The time stamp</param>
        /// <param name="hour">[0, 24) New hour</param>
        /// <param name="minute">[0, 60) New minute</param>
        /// <param name="second">[0, 60) New second</param>
        /// <param name="millisecond">[0, 1000) New millisecond</param>
        /// <param name="microsecond">[0, 1000) New microsecond</param>
        /// <returns>New DateTime with the specific time set</returns>
        /// <exception cref="ArgumentOutOfRangeException">Any of the time components are out of range</exception>
        public static DateTime ChangeTime(this DateTime t, int hour = 0, int minute = 0, int second = 0, int millisecond = 0, int microsecond = 0)
        {
            var time = new TimeOnly(hour, minute, second, millisecond, microsecond).Ticks;
            var ticks = t.Ticks;
            return new DateTime(ticks - (ticks % TimeSpan.TicksPerDay) + time, t.Kind);
        }


        /// <summary>
        /// Return a new DateTime with a specific day of month (everything else is unchanged)
        /// </summary>
        /// <param name="t">The time stamp</param>
        /// <param name="newDay">[1, 31] New day of month (must exist in the month)</param>
        /// <returns>New DateTime with the day of month</returns>
        /// <exception cref="ArgumentOutOfRangeException">The day doesn't exist in the month</exception>
        public static DateTime ChangeDay(this DateTime t, int newDay = 1)
        {
            t.Deconstruct(out int y, out int m, out _);
            var ticks = t.Ticks;
            return new DateTime(new DateTime(y, m, newDay).Ticks + (ticks % TimeSpan.TicksPerDay), t.Kind);
        }

        /// <summary>
        /// Return a new DateTime with a specific month of the year (everything else is unchanged)
        /// </summary>
        /// <param name="t">The time stamp</param>
        /// <param name="newMonth">[1, 12] New month of the year</param>
        /// <returns>New DateTime with the month of the year</returns>
        /// <exception cref="ArgumentOutOfRangeException">The month is out of range, or the day of the time stamp doesn't exist in the new month</exception>
        public static DateTime ChangeMonth(this DateTime t, int newMonth = 1)
        {
            t.Deconstruct(out int y, out _, out int d);
            var ticks = t.Ticks;
            return new DateTime(new DateTime(y, newMonth, d).Ticks + (ticks % TimeSpan.TicksPerDay), t.Kind);
        }

        /// <summary>
        /// Get the ISO 8601 week of a given date
        /// </summary>
        /// <param name="time">The timestamp</param>
        /// <returns>[1, 53] week number</returns>
        /// <remarks>Same as <see cref="ISOWeek.GetWeekOfYear(DateTime)"/></remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIso8601WeekOfYear(this DateTime time)
            => ISOWeek.GetWeekOfYear(time);


        /// <summary>
        /// Get a human friendly text from a time span, ex: "3 years", "12 days", "15 hours", "34 minutes", "12.3 seconds", "543.2 ms", "12.0 µs" or "800.0 ns".
        /// The largest unit giving a value of at least 10 is used (at least 3 for days, at least 3 * 365 days for years), years / days / hours / minutes are rounded to whole numbers.
        /// Negative time spans are prefixed with a '-', ex: "-3 days".
        /// </summary>
        /// <param name="value">The time span</param>
        /// <param name="zeroAsThis">If exactly zero return this value (null = "0 seconds")</param>
        /// <returns>A human friendly text (always using '.' as the decimal separator)</returns>
        [SkipLocalsInit]
        public static String ElapsedTime(this TimeSpan value, String zeroAsThis = null)
        {
            var ticks = value.Ticks;
            if (ticks == 0)
                return zeroAsThis ?? "0 seconds";
            var isNeg = ticks < 0;
            // Can't overflow (TimeSpan.MinValue)
            double t = isNeg ? -(double)ticks : ticks;
            String suffix;
            String format = "0";
            double v;
            var days = t / TimeSpan.TicksPerDay;
            if (days >= (3 * 365))
            {
                v = days / 365.242374;
                suffix = " years";
            }
            else if (days >= 3)
            {
                v = days;
                suffix = " days";
            }
            else
            {
                var hours = t / TimeSpan.TicksPerHour;
                if (hours >= 10)
                {
                    v = hours;
                    suffix = " hours";
                }
                else
                {
                    var minutes = t / TimeSpan.TicksPerMinute;
                    if (minutes >= 10)
                    {
                        v = minutes;
                        suffix = " minutes";
                    }
                    else
                    {
                        format = "0.0";
                        var seconds = t / TimeSpan.TicksPerSecond;
                        if (seconds >= 10)
                        {
                            v = seconds;
                            suffix = " seconds";
                        }
                        else
                        {
                            var ms = t / TimeSpan.TicksPerMillisecond;
                            if (ms >= 10)
                            {
                                v = ms;
                                suffix = " ms";
                            }
                            else
                            {
                                var us = t / TimeSpan.TicksPerMicrosecond;
                                if (us >= 10)
                                {
                                    v = us;
                                    suffix = " µs";
                                }
                                else
                                {
                                    v = t * TimeSpan.NanosecondsPerTick;
                                    suffix = " ns";
                                }
                            }
                        }
                    }
                }
            }
            Span<char> temp = stackalloc char[64];
            int o = 0;
            if (isNeg)
                temp[o++] = '-';
            v.TryFormat(temp.Slice(o), out var w, format, CultureInfo.InvariantCulture);
            o += w;
            suffix.CopyTo(temp.Slice(o));
            o += suffix.Length;
            return new string(temp.Slice(0, o));
        }


        /// <summary>
        /// Given a time stamp, get the start of the year that includes the time stamp
        /// Example: 2024-03-15 11:05:33 => 2024-01-01 00:00:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraMonths">Number of months to add to the start of the year</param>
        /// <param name="extraDays">Number of days to add (after <paramref name="extraMonths"/> has been added)</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOfYear(this DateTime value, int extraMonths = 0, double extraDays = 0)
        {
            var n = new DateTime(value.Year, 1, 1, 0, 0, 0, value.Kind);
            if (extraMonths != 0)
                n = n.AddMonths(extraMonths);
            if (extraDays != 0)
                n = n.AddDays(extraDays);
            return n;
        }

        /// <summary>
        /// Given a time stamp, get the start of the month that includes the time stamp
        /// Example: 2024-03-15 11:05:33 => 2024-03-01 00:00:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraDays">Number of days to add to the start of the month</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOfMonth(this DateTime value, double extraDays = 0)
        {
            value.Deconstruct(out _, out _, out int d);
            var ticks = value.Ticks;
            var n = new DateTime(ticks - (ticks % TimeSpan.TicksPerDay) - (d - 1) * TimeSpan.TicksPerDay, value.Kind);
            if (extraDays != 0)
                n = n.AddDays(extraDays);
            return n;
        }

        /// <summary>
        /// Given a time stamp, get the start of the day that includes the time stamp.
        /// Example: 2024-03-15 11:05:33 => 2024-03-15 00:00:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraHours">Number of hours to add to the start of the day</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOfDay(this DateTime value, double extraHours = 0)
        {
            var ticks = value.Ticks;
            var n = new DateTime(ticks - (ticks % TimeSpan.TicksPerDay), value.Kind);
            if (extraHours != 0)
                n = n.AddHours(extraHours);
            return n;
        }

        /// <summary>
        /// Given a time stamp, get the start of the hour that includes the time stamp.
        /// Example: 2024-03-15 11:05:33 => 2024-03-15 11:00:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraMinutes">Number of minutes to add to the start of the hour</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOfHour(this DateTime value, double extraMinutes = 0)
        {
            var ticks = value.Ticks;
            var n = new DateTime(ticks - (ticks % TimeSpan.TicksPerHour), value.Kind);
            if (extraMinutes != 0)
                n = n.AddMinutes(extraMinutes);
            return n;
        }


        /// <summary>
        /// Given a time stamp, get the start of the minute that includes the time stamp.
        /// Example: 2024-03-15 11:05:33 => 2024-03-15 11:05:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraSeconds">Number of seconds to add to the start of the minute</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOfMinute(this DateTime value, double extraSeconds = 0)
        {
            var ticks = value.Ticks;
            var n = new DateTime(ticks - (ticks % TimeSpan.TicksPerMinute), value.Kind);
            if (extraSeconds != 0)
                n = n.AddSeconds(extraSeconds);
            return n;
        }

        /// <summary>
        /// Given a time stamp, get the start of the seconds that includes the time stamp.
        /// Example: 2024-03-15 11:05:33,123 => 2024-03-15 11:05:33,000.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraMs">Number of milliseconds to add to the start of the second</param>
        /// <returns>A new time stamp (with the same kind)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The resulting time stamp is outside of the range of a DateTime</exception>
        public static DateTime ToStartOSecond(this DateTime value, double extraMs = 0)
        {
            var ticks = value.Ticks;
            var n = new DateTime(ticks - (ticks % TimeSpan.TicksPerSecond), value.Kind);
            if (extraMs != 0)
                n = n.AddMilliseconds(extraMs);
            return n;
        }

    }

}
