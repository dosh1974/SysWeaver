using System;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    /// <summary>
    /// Console capability detection (console / ANSI escape code support) and helpers for colors and the terminal progress indicator.
    /// </summary>
    /// <remarks>
    /// Detection is done once in the static constructor. On Windows, detecting ANSI support also tries to enable virtual terminal processing on the
    /// console output handle (a process wide side effect). Setting the "NO_COLOR" environment variable disables ANSI support.
    /// </remarks>
    public static class ConsoleTools
    {

        /// <summary>
        /// True if a console window is available.
        /// On Windows this is <see cref="Environment.UserInteractive"/>, on other platforms it's true unless both stdout and stderr are redirected.
        /// </summary>
        public static readonly bool IsConsoleAvailable;


        /// <summary>
        /// True if a console window that supports ansi escape codes is available
        /// </summary>
        public static readonly bool IsAnsiConsoleAvailable;

        /// <summary>
        /// Set console progress indicator (taskbar / tab progress in terminals such as Windows Terminal and ConEmu), using the "ESC ] 9 ; 4" escape sequence.
        /// Arguments are the display state and the progress in percent (0 to 100).
        /// A no-op if <see cref="IsAnsiConsoleAvailable"/> is false. Never null.
        /// </summary>
        public static readonly Action<ConsoleProgressDisplays, int> SetProgress;


        /// <summary>
        /// The console foreground color.
        /// The value is only applied to <see cref="Console.ForegroundColor"/> if <see cref="IsAnsiConsoleAvailable"/> is true (then the last set value is returned),
        /// else the setter only records the value and the getter returns <see cref="Console.ForegroundColor"/>.
        /// </summary>
        public static ConsoleColor ForegroundColor
        {
            get => IsAnsiConsoleAvailable ? InternalForegroundColor : Console.ForegroundColor;
            set
            {
                InternalForegroundColor = value;
                if (IsAnsiConsoleAvailable)
                    Console.ForegroundColor = value;
            }
        }
        static ConsoleColor InternalForegroundColor = ConsoleColor.White;


        /// <summary>
        /// The console background color.
        /// The value is only applied to <see cref="Console.BackgroundColor"/> if <see cref="IsAnsiConsoleAvailable"/> is true (then the last set value is returned),
        /// else the setter only records the value and the getter returns <see cref="Console.BackgroundColor"/>.
        /// </summary>
        public static ConsoleColor BackgroundColor
        {
            get => IsAnsiConsoleAvailable ? InternalBackgroundColor : Console.BackgroundColor;
            set
            {
                InternalBackgroundColor = value;
                if (IsAnsiConsoleAvailable)
                    Console.BackgroundColor = value;
            }
        }
        static ConsoleColor InternalBackgroundColor = ConsoleColor.Black;


        /// <summary>
        /// Reset the recorded colors to white on black and, if <see cref="IsAnsiConsoleAvailable"/> is true, call <see cref="Console.ResetColor"/>.
        /// </summary>
        public static void ResetColor()
        {
            InternalBackgroundColor = ConsoleColor.Black;
            InternalForegroundColor = ConsoleColor.White;
            if (IsAnsiConsoleAvailable)
                Console.ResetColor();
        }

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
            if (Console.IsOutputRedirected)
                return false;

            if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
                return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CheckAndEnableWindowsAnsi();
            }
            else
            {
                if (Environment.ProcessId == 1) // Running inside docker container or similar, no terminal available
                    return false;
                var term = Environment.GetEnvironmentVariable("TERM");
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
