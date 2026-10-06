
namespace SysWeaver.Inspection
{

    /// <summary>
    /// Object than can be described (saved, loaded, copied etc) has to implement this interface.
    /// Optionally a constructor (private is preferred) with the ClassName(IInspector i, int version) might have to be implemented for readonly fields.
    /// Version handling of the type is done by adding a type attribute <see cref="DescVersionAttribute"/> to the type.
    /// </summary>
    /// <remarks>
    /// The same Describe method is used for both reading and writing, members are passed by reference (or with a setter) so that the inspector can read or update them.
    /// Members must be described in the same order every time.
    /// Handlers are compiled and cached per type by SysWeaver.Inspection.
    /// </remarks>
    public interface IDescribable
    {
        /// <summary>
        /// Describe this type to an inspector at the current version
        /// </summary>
        /// <param name="i">The inspector that should be given the description of this object</param>
        void Describe(IInspector i);
        
        /// <summary>
        /// Describe this type to an inspector at a specified version (the version of this type is less than the current version).
        /// Called when reading data that was written by an older version of the type.
        /// </summary>
        /// <param name="i">The inspector that should be given the description of this object</param>
        /// <param name="version">The version that should be described </param>
        void Describe(IInspector i, int version);
    }

}

