using System.Reflection;
using System;


namespace SysWeaver.Script
{

    /// <summary>
    /// A compiled script, see <see cref="CsScript.Create{T, R}(string, Microsoft.CodeAnalysis.CSharp.LanguageVersion)"/>
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <typeparam name="R">The result type</typeparam>
    public sealed class CsScript<T, R> : CsScriptBase
    {
        /// <summary>
        /// Run the script (call its Main method), can be called concurrently (if the script code is thread safe)
        /// </summary>
        /// <param name="p">The input</param>
        /// <returns>The result of Main</returns>
        /// <exception cref="ObjectDisposedException">The script is disposed</exception>
        /// <exception cref="TargetInvocationException">The script threw an exception (it's the inner exception)</exception>
        public R Run(T p)
        {
            var mi = Mi;
            if (mi == null)
                throw new ObjectDisposedException(nameof(CsScript<T, R>), "Script have been disposed!");
            return (R)mi.Invoke(null, [p]);
        }


        internal CsScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
        }

    }
}
