using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    [StructLayout(LayoutKind.Sequential)]
    struct ScAction
    {
        public ScActionTypes Type;
        /// <summary>
        /// Delay in milli seconds
        /// </summary>
        public uint Delay;
    }

}
