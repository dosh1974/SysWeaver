using System;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Describes a type and its members
    /// </summary>
    public sealed class TypeDesc : TypeDescBase
    {

        /// <summary>
        /// The assembly that defined the type (requires the Debug token)
        /// </summary>
        public String Asm;
        /// <summary>
        /// Members in the type
        /// </summary>
        public TypeMemberDesc[] Members;
    }


}
