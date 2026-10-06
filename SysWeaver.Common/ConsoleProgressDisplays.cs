namespace SysWeaver
{
    /// <summary>
    /// The state of the console (terminal taskbar) progress indicator, see <see cref="ConsoleTools.SetProgress"/>.
    /// The numeric values match the state parameter of the "ESC ] 9 ; 4" (ConEmu / Windows Terminal) progress escape sequence.
    /// </summary>
    public enum ConsoleProgressDisplays
    {
        /// <summary>
        /// Disabled / Hide progress
        /// </summary>
        Disabled = 0,
        /// <summary>
        ///  Normal (Green/Blue fill based on theme)
        /// </summary>
        Normal,
        /// <summary>
        ///  Error (Red fill)
        /// </summary>
        Error,
        /// <summary>
        /// Indeterminate (Moving marquee/pulsing bar)
        /// </summary>
        Indeterminate,
        /// <summary>
        /// Warning / paused (Yellow/Orange fill)
        /// </summary>
        Warning,


    }

}
