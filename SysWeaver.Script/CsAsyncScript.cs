using System.Reflection;
using System;
using System.Threading.Tasks;


namespace SysWeaver.Script
{
    /// <summary>
    /// A compiled async script, see <see cref="CsScript.CreateAsync{T, R}(string, Microsoft.CodeAnalysis.CSharp.LanguageVersion)"/>
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <typeparam name="R">The result type (of the task)</typeparam>
    public sealed class CsAsyncScript<T, R> : CsScriptBase
    {
        /// <summary>
        /// Run the script (call its Main method), can be called concurrently (if the script code is thread safe)
        /// </summary>
        /// <param name="p">The input</param>
        /// <returns>The task returned by Main (exceptions thrown by an async Main are in the task)</returns>
        /// <exception cref="ObjectDisposedException">The script is disposed</exception>
        /// <exception cref="TargetInvocationException">The script threw an exception synchronously (it's the inner exception)</exception>
        public Task<R> Run(T p)
        {
            var mi = Mi;
            if (mi == null)
                throw new ObjectDisposedException(nameof(CsAsyncScript<T, R>), "Script have been disposed!");
            return (Task<R>)mi.Invoke(null, [p]);
        }


        internal CsAsyncScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
        }

    }
}
