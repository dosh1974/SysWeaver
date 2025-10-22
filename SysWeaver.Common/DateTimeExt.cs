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


    }

}
