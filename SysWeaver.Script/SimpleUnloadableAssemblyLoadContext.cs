using System.Reflection;
using System.Runtime.Loader;


namespace SysWeaver.Script
{
    /// <summary>
    /// A collectible assembly load context, dependencies are resolved by the default context
    /// </summary>
    public sealed class SimpleUnloadableAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Create a collectible load context
        /// </summary>
        public SimpleUnloadableAssemblyLoadContext()
            : base(true)
        {
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
