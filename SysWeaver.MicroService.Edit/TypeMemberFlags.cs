using System;

namespace SysWeaver.MicroService
{
    [Flags]
    public enum TypeMemberFlags
    {
        /// <summary>
        /// String is allowed to be multi line
        /// </summary>
        Multiline = 1,
        /// <summary>
        /// String input should be masked and non-copyable
        /// </summary>
        Password = 2,
        /// <summary>
        /// null is an acceptable value
        /// </summary>
        AcceptNull = 4,
        /// <summary>
        /// Use a slider if supported
        /// </summary>
        Slider = 8,
        /// <summary>
        /// Member is read only 
        /// </summary>
        ReadOnly = 16,
        /// <summary>
        /// Member is a collection
        /// </summary>
        Collection = 32,
        /// <summary>
        /// Member is an indexed collection (ordered)
        /// </summary>
        Indexed = 64,
        /// <summary>
        /// Member is an enum
        /// </summary>
        IsEnum = 128,
        /// <summary>
        /// Member is primitive
        /// </summary>
        IsPrimitive = 256,
        /// <summary>
        /// Member is an object
        /// </summary>
        IsObject = 512,
        /// <summary>
        /// Member is hidden (when editing)
        /// </summary>
        Hide = 1024,
        /// <summary>
        /// Member is an enum with the [Flags] attribute set
        /// </summary>
        IsFlags = 2048,
        /// <summary>
        /// Can be set on DateTime and DateOnly type to indicate that the the date should be unspecified (not local to user transformed to UTC).
        /// </summary>
        DateUnspecified = 2048,


    }


}
