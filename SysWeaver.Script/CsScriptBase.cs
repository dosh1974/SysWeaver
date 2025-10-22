using System.Reflection;
using System;


namespace SysWeaver.Script
{
    public abstract class CsScriptBase : IDisposable
    {
        public void Dispose()
        {
            Mi = null;
            Lc = null;
        }

        protected SimpleUnloadableAssemblyLoadContext Lc;
        protected MethodInfo Mi;


        protected CsScriptBase(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
        {
            Lc = lc;
            Mi = mi;
        }

    }
}
