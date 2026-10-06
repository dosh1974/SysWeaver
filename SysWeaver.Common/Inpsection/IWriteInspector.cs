namespace SysWeaver.Inspection
{
    /// <summary>
    /// An inspector that writes objects to some destination.
    /// </summary>
    public interface IWriteInspector : IInspector
    {
        /// <summary>
        /// Write an object.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="obj">The object to write</param>
        /// <param name="saveAsObject">True to write it as an <see cref="object"/> (including the runtime type, so it can be read polymorphically), false to write it as <typeparamref name="T"/></param>
        void Write<T>(T obj, bool saveAsObject = true);
    }

}

