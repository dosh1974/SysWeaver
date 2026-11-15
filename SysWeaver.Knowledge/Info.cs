using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SysWeaver.Knowledge
{
    
    /// <summary>
    /// A piece of knowledge about a keyword
    /// </summary>
    public class Info
    {
#if DEBUG || VERBOSE
        /// <inheritdoc />
        public override string ToString() => String.Concat(Name, " [", Category, "] ", Desc.Trim());
#endif

        /// <summary>
        /// The name
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The description (always ends with a '.')
        /// </summary>
        public readonly String Desc;

        /// <summary>
        /// The category (group) of the info
        /// </summary>
        public readonly String Category;

        /// <summary>
        /// Parent info (more general info), or null if none
        /// </summary>
        public readonly Info[] Parents;

        /// <summary>
        /// True if the info is a proper name (a keyword is then only matched if it starts with an upper case letter)
        /// </summary>
        public readonly bool IsName;

        internal Info(string name, string desc, string category, Info[] parent = null, bool isName = false)
        {
            if (desc == null)
                desc = "";
            if (!desc.EndsWith("."))
                desc += '.';
            Name = name;
            Desc = desc;
            Category = category;
            Parents = parent;
            IsName = isName;
        }




        static readonly ConcurrentDictionary<String, Info> Cache = new ConcurrentDictionary<string, Info>(StringComparer.Ordinal);


        /// <summary>
        /// Get an info by name, if it doesn't exist it is created (names are case insensitive).
        /// </summary>
        /// <param name="wasNew">True if the info was created by this call</param>
        /// <param name="name">The name</param>
        /// <param name="desc">The description, only used if the info is created</param>
        /// <param name="category">The category, only used if the info is created</param>
        /// <param name="parent">Parent info, only used if the info is created</param>
        /// <param name="isName">True if the info is a proper name, only used if the info is created</param>
        /// <returns>The existing or created info</returns>
        public static Info GetInfo(out bool wasNew, string name, string desc, string category, Info[] parent = null, bool isName = false)
        {
            var c = Cache;
            //  The lower case key is only created on a miss
            if (c.TryGetValueLower(name, out var i))
            {
                wasNew = false;
                return i;
            }
            var key = name.FastToLower();
            i = new Info(name, desc, category, parent, isName);
            wasNew = c.TryAdd(key, i);
            if (wasNew)
                return i;
            c.TryGetValue(key, out i);
            return i;
        }



    }


}
