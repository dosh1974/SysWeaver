using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// Optional input of a browser window (implemented by browsers that can send real input events, ex: Playwright): a person can operate a
    /// window that runs on a server (a check that needs a person, ex: a captcha) through a remote view: a screenshot (<see cref="IBrowserWindow.CaptureJpeg"/>)
    /// and the clicks / keys relayed here. Coordinates are css pixels of the window's viewport.
    /// </summary>
    public interface IBrowserWindowInput
    {
        /// <summary>The viewport size in css pixels (the coordinate space of <see cref="Click"/> and <see cref="Move"/>)</summary>
        Task<(int Width, int Height)> ViewportSize();

        /// <summary>Move the mouse to a point without pressing a button (the person's own cursor position, relayed as they move it, so a click lands after a real trajectory instead of teleporting there)</summary>
        Task Move(double x, double y);

        /// <summary>Click at a point</summary>
        Task Click(double x, double y);

        /// <summary>Type text into the focused element (key by key)</summary>
        Task Type(String text);

        /// <summary>Press a key (names like "Enter", "Tab", "Backspace", "Escape", "ArrowDown")</summary>
        Task Press(String key);
    }
}
