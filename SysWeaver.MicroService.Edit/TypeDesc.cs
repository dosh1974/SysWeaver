using System;

namespace SysWeaver.MicroService
{

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
