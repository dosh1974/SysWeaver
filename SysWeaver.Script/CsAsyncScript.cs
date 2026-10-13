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
            var fn = Fn;
            if (fn != null)
            {
                try
                {
                    return fn(p);
                }
                catch (Exception ex)
                {
                    //  Same contract as MethodInfo.Invoke
                    throw new TargetInvocationException(ex);
                }
            }
            var mi = Mi;
            if (mi == null)
                throw new ObjectDisposedException(nameof(CsAsyncScript<T, R>), "Script have been disposed!");
            return (Task<R>)mi.Invoke(null, [p]);
        }


        internal CsAsyncScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
            Fn = CsScriptBase.TryCreateDelegate<Func<T, Task<R>>>(mi);
        }

        /// <summary>
        /// A typed delegate to Main (null when disposed, or when Main's parameter type needs a conversion of the input, then Mi is invoked)
        /// </summary>
        Func<T, Task<R>> Fn;

        /// <summary>
        /// Release the delegate (it references the compiled assembly, keeping it would prevent the unloading)
        /// </summary>
        protected override void OnDispose()
        {
            Fn = null;
        }

    }
}
