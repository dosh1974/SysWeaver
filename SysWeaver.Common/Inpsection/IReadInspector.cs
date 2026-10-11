namespace SysWeaver.Inspection
{
    /// <summary>
    /// An inspector that reads (creates) objects from some source.
    /// </summary>
    public interface IReadInspector : IInspector
    {
        /// <summary>
        /// Read the next object.
        /// </summary>
        /// <typeparam name="T">The expected type: the type it was written as (see <see cref="IWriteInspector.Write{T}(T, bool)"/>), or <see cref="object"/> if it was written as an object</typeparam>
        /// <returns>The object read</returns>
        T Read<T>();
    }

}

