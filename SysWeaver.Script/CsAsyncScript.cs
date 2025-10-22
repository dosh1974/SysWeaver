using System.Reflection;
using System;
using System.Threading.Tasks;


namespace SysWeaver.Script
{
    public sealed class CsAsyncScript<T, R> : CsScriptBase
    {
        public Task<R> Run(T p)
        {
            var mi = Mi;
            if (mi == null)
                throw new Exception("Script have been disposed!");
            return (Task<R>)mi.Invoke(null, [p]);
        }


        internal CsAsyncScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
        }

    }
}
