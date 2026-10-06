namespace SysWeaver
{
    /// <summary>
    /// Indicate that a class can be cloned (deep) using the Clone method.
    /// </summary>
    /// <typeparam name="T">The type being cloned (typically the implementing type itself).</typeparam>
    public interface ICloneable<T> where T : class
    {
        /// <summary>
        /// Create a deep copy of the object.
        /// Implementations MUST handle the case where this is null (and return null), which can happen when called as an extension-like helper on a null reference.
        /// </summary>
        /// <returns>A clone of the object or null if the object is null</returns>
        T Clone();

        /// <summary>
        /// Copy data from some other instance (none may be null)
        /// </summary>
        /// <param name="other">The instance to copy data from</param>
        void CopyFrom(T other);


    }


}
