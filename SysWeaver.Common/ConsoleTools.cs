using System;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    public static class ConsoleTools
    {

        /// <summary>
        /// True if a console window is available
        /// </summary>
        public static readonly bool IsConsoleAvailable;


        /// <summary>
        /// True if a console window that supports ansi escape codes is available
        /// </summary>
        public static readonly bool IsAnsiConsoleAvailable;

        /// <summary>
        /// Set console progress indicator
        /// </summary>
        public static readonly Action<ConsoleProgressDisplays, int> SetProgress;



        static ConsoleTools()
        {
            bool isC;
            if (OperatingSystem.IsWindows())
            {
                isC = Environment.UserInteractive;
            }else
            {
                isC = !(Console.IsOutputRedirected && Console.IsErrorRedirected);
            }
            IsConsoleAvailable = isC;
            var isA = isC && SupportsAnsi();
            IsAnsiConsoleAvailable = isA;
            if (isA)
            {
                SetProgress = (m, t) => Console.Write(String.Concat("\x1b]9;4;", (int)m, ';',t, "\x07"));
            }
            else
            {
                SetProgress = (m, t) => { };
            }
        }

        // Windows API-konstanter nödvändiga för ANSI/VT-processering
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        static bool SupportsAnsi()
        {
            // 1. Om utmatningen är omdirigerad (t.ex. till en fil) ska ANSI döljas
            if (Console.IsOutputRedirected)
                return false;

            // 2. Följ industristandarden NO_COLOR (https://no-color.org)
            if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
                return false;

            // 3. Plattformsspecifik logik
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CheckAndEnableWindowsAnsi();
            }
            else
            {
                // Unix/Linux/macOS använder nästan alltid ANSI, såvida inte TERM=dumb
                string? term = Environment.GetEnvironmentVariable("TERM");
                return term != null && term.ToLower() != "dumb";
            }
        }

        static bool CheckAndEnableWindowsAnsi()
        {
            IntPtr stdout = GetStdHandle(STD_OUTPUT_HANDLE);
            if (stdout == IntPtr.Zero || stdout == new IntPtr(-1))
                return false;

            if (!GetConsoleMode(stdout, out uint mode))
                return false;

            // Om flaggan redan är aktiv stödjer terminalen ANSI
            if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == ENABLE_VIRTUAL_TERMINAL_PROCESSING)
                return true;

            // Försök att aktivera flaggan (krävs för äldre Windows Console Host / CMD)
            uint requestedMode = mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            return SetConsoleMode(stdout, requestedMode);
        }



    }

}
