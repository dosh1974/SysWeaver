using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SC_ACTION structure, a failure action.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct ScAction
    {
        /// <summary>
        /// The action to perform.
        /// </summary>
        public ScActionTypes Type;
        /// <summary>
        /// Delay in milli seconds
        /// </summary>
        public uint Delay;
    }

}
