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
        /// <typeparam name="T">The expected type</typeparam>
        /// <returns>The object read</returns>
        T Read<T>();
    }

}

