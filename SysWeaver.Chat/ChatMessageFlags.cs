using System;

namespace SysWeaver.Chat
{
    [Flags]
    public enum ChatMessageFlags
    {
        IsWorking = 1,

        CanRemove = 256,
    }

}
