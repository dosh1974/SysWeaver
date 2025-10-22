using System;
using System.Collections.Generic;


namespace SysWeaver.MicroService
{


    /// <summary>
    /// Indicated that a type can be used as a micro service
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IsMicroServiceAttribute : Attribute
    {
    }


    /// <summary>
    /// Add to a micro service class to show an optional dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class OptionalDepAttribute : Attribute
    {
        public OptionalDepAttribute(params Type[] types)
        {
            Types = types;
        }


        public readonly IReadOnlyList<Type> Types;
    }


    /// <summary>
    /// Add to a micro service class to show an optional dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class OptionalDepAttribute<T> : OptionalDepAttribute where T : class
    {
        public OptionalDepAttribute() : base(typeof(T))
        {
        }
    }

    /// <summary>
    /// Add to a micro service class to show an optional dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class OptionalDepAttribute<T0, T1> : OptionalDepAttribute where T0 : class where T1 : class
    {
        public OptionalDepAttribute() : base(typeof(T0), typeof(T1))
        {
        }
    }


    /// <summary>
    /// Add to a micro service class to show a required dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class RequiredDepAttribute : Attribute
    {
        public RequiredDepAttribute(params Type[] types)
        {
            Types = types;
        }


        public readonly IReadOnlyList<Type> Types;
    }


    /// <summary>
    /// Add to a micro service class to show a required dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class RequiredDepAttribute<T> : RequiredDepAttribute where T : class
    {
        public RequiredDepAttribute() : base(typeof(T))
        {
        }
    }

    /// <summary>
    /// Add to a micro service class to show a required dependency
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class RequiredDepAttribute<T0, T1> : RequiredDepAttribute where T0 : class where T1 : class
    {
        public RequiredDepAttribute() : base(typeof(T0), typeof(T1))
        {
        }
    }



            


}
