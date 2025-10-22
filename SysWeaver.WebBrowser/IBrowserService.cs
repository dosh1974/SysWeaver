
using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{

    public interface IBrowserService 
    {
        Task<IBrowserWindow> OpenWindow();
    }


}
