using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    public interface IManagedFileSource : IDisposable
    {
        Task<ManagedFileData> TryGetNow();
    }

}
