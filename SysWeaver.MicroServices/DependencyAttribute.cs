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
        /// <summary>
        /// Add to a micro service class to show optional dependencies
        /// </summary>
        /// <param name="types">The types of the optional dependencies</param>
        public OptionalDepAttribute(params Type[] types)
        {
            Types = types;
        }


        /// <summary>
        /// The types of the optional dependencies
        /// </summary>
        public readonly IReadOnlyList<Type> Types;
    }


    /// <summary>
    /// Add to a micro service class to show an optional dependency
    /// </summary>
    /// <typeparam name="T">The type of the optional dependency</typeparam>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class OptionalDepAttribute<T>: OptionalDepAttribute where T : class
    {
        public OptionalDepAttribute() : base(typeof(T))
        {
        }
    }

    /// <summary>
    /// Add to a micro service class to show two optional dependencies
    /// </summary>
    /// <typeparam name="T0">The type of the first optional dependency</typeparam>
    /// <typeparam name="T1">The type of the second optional dependency</typeparam>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class OptionalDepAttribute<T0, T1>: OptionalDepAttribute where T0 : class where T1 : class
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
        /// <summary>
        /// Add to a micro service class to show required dependencies
        /// </summary>
        /// <param name="types">The types of the required dependencies</param>
        public RequiredDepAttribute(params Type[] types)
        {
            Types = types;
        }


        /// <summary>
        /// The types of the required dependencies
        /// </summary>
        public readonly IReadOnlyList<Type> Types;
    }


    /// <summary>
    /// Add to a micro service class to show a required dependency
    /// </summary>
    /// <typeparam name="T">The type of the required dependency</typeparam>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class RequiredDepAttribute<T>: RequiredDepAttribute where T : class
    {
        public RequiredDepAttribute() : base(typeof(T))
        {
        }
    }

    /// <summary>
    /// Add to a micro service class to show two required dependencies
    /// </summary>
    /// <typeparam name="T0">The type of the first required dependency</typeparam>
    /// <typeparam name="T1">The type of the second required dependency</typeparam>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
    public class RequiredDepAttribute<T0, T1>: RequiredDepAttribute where T0 : class where T1 : class
    {
        public RequiredDepAttribute() : base(typeof(T0), typeof(T1))
        {
        }
    }



            


}
