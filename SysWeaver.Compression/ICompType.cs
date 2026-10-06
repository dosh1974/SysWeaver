using System;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A complete compression implementation (encoder and decoder) that can be registered in the <see cref="CompManager"/>.
    /// </summary>
    /// <remarks>
    /// Implementations are stateless singletons that are safe to use concurrently from multiple threads.
    /// Plug-ins are typically registered by type name in a service manifest, the service manager then invokes the public static parameterless <c>Register</c> method using reflection.
    /// </remarks>
    public interface ICompType : ICompEncoder, ICompDecoder
    {
        /// <summary>
        /// Register the implementation in the <see cref="CompManager"/> (implementations call <see cref="CompManager.AddType(ICompType)"/> with their singleton instance).
        /// </summary>
        /// <exception cref="NotImplementedException">The implementing type doesn't provide a static <c>Register</c> method.</exception>
        static virtual void Register() => throw new NotImplementedException();

        /// <summary>
        /// The singleton instance of the implementation.
        /// </summary>
        /// <remarks>
        /// The built-in implementations expose <c>Instance</c> as a static field, which does not implement this static virtual property,
        /// so accessing it through a generic type parameter constrained to <see cref="ICompType"/> throws; use the concrete type's field instead.
        /// </remarks>
        /// <exception cref="NotImplementedException">The implementing type doesn't override this property.</exception>
        static virtual ICompType Instance { get => throw new NotImplementedException(); } 
    }

}
