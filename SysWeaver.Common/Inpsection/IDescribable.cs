
namespace SysWeaver.Inspection
{

    /// <summary>
    /// Object than can be described (save, loaded, copied etc) has to implement this interface
    /// Optionally a constructor (private is preferred) with the ClassName(IInspector i, int version) might have to be implemented for readonly fields.
    /// Version handling of the type is done by adding a type attribute DescVersionAttribute to the type
    /// </summary>
    public interface IDescribable
    {
        /// <summary>
        /// Describe this type to an inspector at the current version
        /// </summary>
        /// <param name="i">The inspector that should be given the description of this object</param>
        void Describe(IInspector i);
        
        /// <summary>
        /// Describe this type to an inspector at a specified version (the version of this type is less than the current version)
        /// </summary>
        /// <param name="i">The inspector that should be given the description of this object</param>
        /// <param name="version">The version that should be described </param>
        void Describe(IInspector i, int version);
    }

}

