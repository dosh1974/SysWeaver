namespace SysWeaver.Inspection
{
    /// <summary>
    /// How a type name is qualified, see StaticTypeHandler.GetTypename (generic arguments are qualified the same way)
    /// </summary>
    public enum TypenameQualifications
    {
        /// <summary>
        /// The full name without an assembly, ex: "System.Int32", "Ns.Outer+Inner", "System.Collections.Generic.List`1[[System.String]]"
        /// </summary>
        Simple = 0,
        /// <summary>
        /// The full name and the simple assembly name, ex: "System.Int32, System.Private.CoreLib"
        /// </summary>
        Assembly,
        /// <summary>
        /// The assembly qualified name (<see cref="System.Type.AssemblyQualifiedName"/>, including the assembly version, culture and public key token)
        /// </summary>
        Full,
    }

}

