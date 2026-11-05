using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Kill tokens: a process can only be killed (<see cref="ServerManagerService.KillProcess(string, HttpServerRequest)"/>) with a token
    /// that was sent with the process list (<see cref="SmProcessMetrics.Actions"/>).
    /// A crafted link, a deep link scan or an image (a plain GET, CSRF style) can't kill a process since the token can't be guessed,
    /// it can only be read from the (authenticated, same origin) process list.
    /// </summary>
    /// <remarks>
    /// Token format: "pid-expires-mac", where expires is the expire time in minutes since the unix epoch and
    /// mac is the first 128 bits (base64url) of a HMAC-SHA256 of the pid, the start time of the process and the expire time,
    /// using a random key that is created once per process (so tokens never survive a restart of the server manager).
    /// Binding the start time means that a token can't kill another process that reuses the same pid.
    /// </remarks>
    static class SmProcessKillToken
    {
        /// <summary>
        /// For how long a token is at least valid (15 to 20 minutes, the process list is refreshed every few seconds, so tokens are renewed all the time).
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

        static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

        static String Mac(long pid, DateTime start, long expires)
        {
            var data = Encoding.UTF8.GetBytes(String.Join('|', pid.ToString(CultureInfo.InvariantCulture), start.Ticks.ToString(CultureInfo.InvariantCulture), expires.ToString(CultureInfo.InvariantCulture)));
            var h = HMACSHA256.HashData(Key, data);
            return Convert.ToBase64String(h, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static long NowMinutes() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;

        /// <summary>
        /// Create a kill token for a process.
        /// </summary>
        /// <param name="pid">The process id</param>
        /// <param name="start">The start time of the process</param>
        /// <returns>The token (only contains [0-9a-zA-Z_-])</returns>
        public static String Create(long pid, DateTime start)
        {
            //  The expire time is rounded up to 5 minutes, so the token (and the rendered action) stays the same between refreshes of the list
            var expires = ((NowMinutes() + (long)Lifetime.TotalMinutes) / 5 + 1) * 5;
            return String.Join('-', pid.ToString(CultureInfo.InvariantCulture), expires.ToString(CultureInfo.InvariantCulture), Mac(pid, start, expires));
        }

        /// <summary>
        /// Get the process id from a token (without validating it).
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="pid">The process id</param>
        /// <returns>True if the token is well formed</returns>
        public static bool TryGetPid(String token, out long pid)
        {
            pid = 0;
            if (String.IsNullOrEmpty(token) || token.Length > 64)
                return false;
            var p = token.Split('-', 3);
            if (p.Length != 3)
                return false;
            return long.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out pid);
        }

        /// <summary>
        /// Validate a kill token.
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="pid">The process id (must match the token)</param>
        /// <param name="start">The start time of the process (must match the time used when the token was created)</param>
        /// <returns>True if the token is valid (and hasn't expired)</returns>
        public static bool IsValid(String token, long pid, DateTime start)
        {
            if (String.IsNullOrEmpty(token) || token.Length > 64)
                return false;
            var p = token.Split('-', 3);
            if (p.Length != 3)
                return false;
            if (!long.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out var tpid) || (tpid != pid))
                return false;
            if (!long.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) || (expires < NowMinutes()))
                return false;
            var a = Encoding.ASCII.GetBytes(Mac(pid, start, expires));
            var b = Encoding.ASCII.GetBytes(p[2]);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        /// <summary>
        /// The "start time" used for reboot tokens (pid 0, never a valid kill token since pid 0 can't be killed).
        /// </summary>
        static readonly DateTime RebootStamp = new DateTime(1, DateTimeKind.Utc);

        /// <summary>
        /// Create a reboot token, required by <see cref="ServerManagerService.RebootComputer(string)"/> (sent with <see cref="SmServerInfo.RebootToken"/>),
        /// so a crafted link or a plain GET request can't reboot the computer.
        /// </summary>
        /// <returns>The token</returns>
        public static String CreateReboot() => Create(0, RebootStamp);

        /// <summary>
        /// Validate a reboot token.
        /// </summary>
        /// <param name="token">The token</param>
        /// <returns>True if the token is valid (and hasn't expired)</returns>
        public static bool IsValidReboot(String token) => IsValid(token, 0, RebootStamp);

        /// <summary>
        /// True if a process may be killed at all: not the idle pseudo process (pid 0), not the Windows "System" process (pid 4)
        /// and not the process running the server manager itself.
        /// </summary>
        /// <param name="pid">The process id</param>
        /// <param name="name">The process name</param>
        /// <returns>True if the process may be killed</returns>
        public static bool CanKill(long pid, String name)
        {
            if (pid <= 0)
                return false;
            if (pid == Environment.ProcessId)
                return false;
            if ((pid == 4) && OperatingSystem.IsWindows() && String.Equals(name, "System", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
    }

}
