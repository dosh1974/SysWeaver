using System;
using System.Globalization;

namespace SysWeaver
{
    public static class DateTimeExt
    {
        /// <summary>
        /// Return the time stamp that happened first, aka Min(a, b)
        /// </summary>
        /// <param name="a">First time stamp</param>
        /// <param name="b">Second time stamp</param>
        /// <returns>The first time stamp (has the lowest value)</returns>
        public static DateTime First(this DateTime a, DateTime b)
            => a < b ? a : b;

        /// <summary>
        /// Return the time stamp that happened last, aka Max(a, b)
        /// </summary>
        /// <param name="a">First time stamp</param>
        /// <param name="b">Second time stamp</param>
        /// <returns>The last time stamp (has the highest value)</returns>
        public static DateTime Last(this DateTime a, DateTime b)
            => a > b ? a : b;

        /// <summary>
        /// Return a new DateTime with a specific kind 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="kind">New kind</param>
        /// <returns>New DateTime with the specific kind</returns>
        public static DateTime AsKind(this DateTime t, DateTimeKind kind)
        {
            if (kind == t.Kind)
                return t;
            var y = t.Year;
            var m = t.Month;
            var d = t.Day;
            var hh = t.Hour;
            var mm = t.Minute;
            var ss = t.Second;
            var ms = t.Millisecond;
            var mq = t.Microsecond;
            return new DateTime(y, m, d, hh, mm, ss, ms, mq, kind);
        }


        /// <summary>
        /// Return a new DateTime with a specific time set
        /// </summary>
        /// <param name="t"></param>
        /// <param name="hour">[0, 24) New hour</param>
        /// <param name="minute">[0, 60) New minute</param>
        /// <param name="second">[0, 60) New second</param>
        /// <param name="millisecond">[0, 1000) New millisecond</param>
        /// <param name="microsecond">[0, 1000) New microsecond</param>
        /// <returns>New DateTime with the specific time set</returns>
        public static DateTime ChangeTime(this DateTime t, int hour = 0, int minute = 0, int second = 0, int millisecond = 0, int microsecond = 0)
        {
            var kind = t.Kind;
            var y = t.Year;
            var m = t.Month;
            var d = t.Day;
            return new DateTime(y, m, d, hour, minute, second, millisecond, microsecond, kind);
        }


        /// <summary>
        /// Return a new DateTime with a specific day of month
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newDay">[1, 31) New day of month</param>
        /// <returns>New DateTime with the day of month</returns>
        public static DateTime ChangeDay(this DateTime t, int newDay = 1)
        {
            var kind = t.Kind;
            var y = t.Year;
            var m = t.Month;
            var d = newDay;
            var hh = t.Hour;
            var mm = t.Minute;
            var ss = t.Second;
            var ms = t.Millisecond;
            var mq = t.Microsecond;
            return new DateTime(y, m, d, hh, mm, ss, ms, mq, kind);
        }

        /// <summary>
        /// Return a new DateTime with a specific month of the year
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newMonth">[1, 12) New month of the year</param>
        /// <returns>New DateTime with the month of the year</returns>
        public static DateTime ChangeMonth(this DateTime t, int newMonth = 1)
        {
            var kind = t.Kind;
            var y = t.Year;
            var m = newMonth;
            var d = t.Day;
            var hh = t.Hour;
            var mm = t.Minute;
            var ss = t.Second;
            var ms = t.Millisecond;
            var mq = t.Microsecond;
            return new DateTime(y, m, d, hh, mm, ss, ms, mq, kind);
        }

        /// <summary>
        /// Get the ISO 8601 week of a given date
        /// </summary>
        /// <param name="time">The timestamp</param>
        /// <returns>[1, 53] week number</returns>
        public static int GetIso8601WeekOfYear(this DateTime time)
        {
            // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
            // be the same week# as whatever Thursday, Friday or Saturday are,
            // and we always get those right
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
                time = time.AddDays(3);
            // Return the week of our adjusted day
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }


        /// <summary>
        /// Get a human friendly text from a time span
        /// </summary>
        /// <param name="value">The time span</param>
        /// <param name="zeroAsThis">If exactly zerom return this value</param>
        /// <returns></returns>
        public static String ElapsedTime(this TimeSpan value, String zeroAsThis = null)
        {
            var days = value.TotalDays;
            if (days >= (3 * 365))
                return (days / 365.242374).ToString("0") + " years";
            if (days >= 3)
                return days.ToString("0") + " days";
            var hours = value.TotalHours;
            if (hours >= 10)
                return hours.ToString("0") + " hours";
            var minutes = value.TotalMinutes;
            if (minutes >= 10)
                return minutes.ToString("0") + " minutes";
            var seconds = value.TotalSeconds;
            if (seconds == 0)
                return zeroAsThis ?? "0 seconds";
            if (seconds >= 10)
                return seconds.ToString("0.0") + " seconds";
            var ms = value.TotalMilliseconds;
            if (ms >= 10)
                return ms.ToString("0.0") + " ms";
            var us = value.TotalMicroseconds;
            if (us >= 10)
                return us.ToString("0.0") + " µs";
            var ns = value.TotalNanoseconds;
            return ns.ToString("0.0") + " ns";
        }


        /// <summary>
        /// Given a time stamp, get the start of the year that includes the time stamp
        /// Example: 2024-03-15 11:05:33 => 2024-01-01 00:00:00.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraMonths">Number of months to add to the start of the year</param>
        /// <param name="extraDays">Number of days to add to the start of the month</param>
        /// <returns>A new time stamp</returns>
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
        /// <returns>A new time stamp</returns>
        public static DateTime ToStartOfMonth(this DateTime value, double extraDays = 0)
        {
            var n = new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind);
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
        /// <returns>A new time stamp</returns>
        public static DateTime ToStartOfDay(this DateTime value, double extraHours = 0)
        {
            var n = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, value.Kind);
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
        /// <returns>A new time stamp</returns>
        public static DateTime ToStartOfHour(this DateTime value, double extraMinutes = 0)
        {
            var n = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind);
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
        /// <returns>A new time stamp</returns>
        public static DateTime ToStartOfMinute(this DateTime value, double extraSeconds = 0)
        {
            var n = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
            if (extraSeconds != 0)
                n = n.AddSeconds(extraSeconds);
            return n;
        }

        /// <summary>
        /// Given a time stamp, get the start of the seconds that includes the time stamp.
        /// Example: 2024-03-15 11:05:33,123 => 2024-03-15 11:05:33,000.
        /// </summary>
        /// <param name="value">The time stamp</param>
        /// <param name="extraMs">Number of millie seconds to add to the start of the second</param>
        /// <returns>A new time stamp</returns>
        public static DateTime ToStartOSecond(this DateTime value, double extraMs = 0)
        {
            var n = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
            if (extraMs != 0)
                n = n.AddMilliseconds(extraMs);
            return n;
        }

    }

}
