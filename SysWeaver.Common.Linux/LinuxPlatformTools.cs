using System;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    public sealed class WindowsPlatformTools : IPlatformTools
    {
        public string Name => "Linux";

        public bool FlushToDisc(SafeHandle h)
        {
            //  TODO: What?
            return true;
        }

    }


}
