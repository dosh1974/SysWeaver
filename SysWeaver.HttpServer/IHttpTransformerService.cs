using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public interface IHttpTransformerService
    {
        IEnumerable<KeyValuePair<String, Func<HttpRequestTransformerState, Task<bool>>>> GetTransformers();
    }


}
