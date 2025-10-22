using System;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    public sealed class WindowsPlatformTools : IPlatformTools
    {
        public string Name => "Windows";

        public bool FlushToDisc(SafeHandle h)
        {
            return FlushFileBuffers(h.DangerousGetHandle());
        }

        #region Imports

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FlushFileBuffers(IntPtr handle);

        #endregion//Imports

    }


}
