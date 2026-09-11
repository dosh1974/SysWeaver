using OpenAI.Realtime;
using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    public interface IAiMemory
    {
        Task<AiMemoryEntry[]> GetMemoryEntries(HttpSession session);

        Task<bool> AddMemory(HttpSession session, String key, String desc, String value);

        Task<String> GetMemory(HttpSession session, String key);

        Task<bool> SetMemory(HttpSession session, String key, String value);

        Task<bool> RemoveMemory(HttpSession session, String key);
    }

}
