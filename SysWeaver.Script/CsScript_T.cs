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
                throw new ObjectDisposedException(nameof(CsScript<T, R>), "Script have been disposed!");
            return (R)mi.Invoke(null, [p]);
        }


        internal CsScript(SimpleUnloadableAssemblyLoadContext lc, MethodInfo mi)
            : base(lc, mi)
        {
            Fn = CsScriptBase.TryCreateDelegate<Func<T, R>>(mi);
        }

        /// <summary>
        /// A typed delegate to Main (null when disposed, or when Main's parameter type needs a conversion of the input, then Mi is invoked)
        /// </summary>
        Func<T, R> Fn;

        /// <summary>
        /// Release the delegate (it references the compiled assembly, keeping it would prevent the unloading)
        /// </summary>
        protected override void OnDispose()
        {
            Fn = null;
        }

    }
}
