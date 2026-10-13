using System.Reflection;
using System;
using System.Threading;


namespace SysWeaver.Script
{
    /// <summary>
    /// Base class of the compiled scripts
    /// </summary>
    public abstract class CsScriptBase : IDisposable
    {
        /// <summary>
        /// Release the script and unload its load context (the assembly is collected when it's no longer used).
        /// The script can't be run after this
        /// </summary>
        public void Dispose()
        {
            Mi = null;
            OnDispose();
            Interlocked.Exchange(ref Lc, null)?.Unload();
        }

        /// <summary>
        /// Called by Dispose before the load context is unloaded, a subclass must release everything that references the compiled assembly (ex: a delegate to Main)
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        /// <summary>
        /// A typed delegate to a static method, null if the method can't be bound to the delegate type without a conversion (ex: a value type input to a wider parameter type)
        /// </summary>
        internal static D TryCreateDelegate<D>(MethodInfo mi) where D : Delegate
        {
            try
            {
                return mi.CreateDelegate<D>();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// The load context of the compiled assembly (null when disposed)
        /// </summary>
        protected SimpleUnloadableAssemblyLoadContext Lc;
        /// <summary>
        /// The Main method of the script (null when disposed)
        /// </summary>
        protected MethodInfo Mi;


        /// <summary>
        /// Create a script
        /// </summary>
        /// <param name="lc">The load context of the compiled assembly</param>
        /// <param name="mi">The Main method of the script</param>
        protected CsScriptBase(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
        {
            Lc = lc;
            Mi = mi;
        }

    }
}
