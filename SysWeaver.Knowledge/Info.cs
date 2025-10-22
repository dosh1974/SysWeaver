using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SysWeaver.Knowledge
{
    
    public class Info
    {
#if DEBUG || VERBOSE
        public override string ToString() => String.Concat(Name, " [", Category, "] ", Desc.Trim());
#endif

        public readonly String Name;
        public readonly String Desc;
        public readonly String Category;
        public readonly Info[] Parents;
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


        public static Info GetInfo(out bool wasNew, string name, string desc, string category, Info[] parent = null, bool isName = false)
        {
            var key = name.FastToLower();
            var c = Cache;
            if (c.TryGetValue(key, out var i))
            {
                wasNew = false;
                return i;
            }
            i = new Info(name, desc, category, parent, isName);
            wasNew = c.TryAdd(key, i);
            if (wasNew)
                return i;
            c.TryGetValue(key, out i);
            return i;
        }



    }


}
