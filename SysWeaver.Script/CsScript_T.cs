using System.Reflection;
using System;


namespace SysWeaver.Script
{

    public sealed class CsScript<T, R> : CsScriptBase
    {
        public R Run(T p)
        {
            var mi = Mi;
            if (mi == null)
                throw new Exception("Script have been disposed!");
            return (R)mi.Invoke(null, [p]);
        }


        internal CsScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
        }

    }
}
