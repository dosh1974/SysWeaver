using System;

namespace SysWeaver
{
    public interface IMessageHost
    {
        MessageLevels AcceptMessageAbove { get; set; }
        void AddMessage(String message, MessageLevels level = MessageLevels.Info);
        void AddMessage(String message, Exception ex, MessageLevels level = MessageLevels.Error);

        IDisposable Tab(int count = 1);

        void Flush();
    }

}
