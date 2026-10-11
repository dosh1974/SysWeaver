namespace SysWeaver.Inspection
{
    /// <summary>
    /// An inspector that writes objects to some destination.
    /// </summary>
    public interface IWriteInspector : IInspector
    {
        /// <summary>
        /// Write an object.
        /// Read it with <see cref="IReadInspector.Read{T}"/> with the same <typeparamref name="T"/>, or with <see cref="object"/> if <paramref name="saveAsObject"/> is true.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="obj">The object to write</param>
        /// <param name="saveAsObject">True to write it as an <see cref="object"/> (including the runtime type, so it can be read polymorphically), false (the default) to write it as <typeparamref name="T"/></param>
        void Write<T>(T obj, bool saveAsObject = false);
    }

}

