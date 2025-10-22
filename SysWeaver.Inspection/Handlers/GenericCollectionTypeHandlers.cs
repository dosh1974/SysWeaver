using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Inspection.Implementation
{

    static class GenericCollectionTypeHandlers<T>
    {

        public static void Describe_IList<E>(IInspectorImplementation i, ref T value, RegFieldDelegate<E> regValue)
        {
            IList<E> list = value as IList<E>;
            int orgCount = list.Count;
            int[] count = new int[] { orgCount };
            i.Array_Begin(count);
            int c = count[0];
            int minC = Math.Min(c, orgCount);
            for (int e = 0; e < minC; ++e)
            {
                var el = list[e];
                regValue(i, ref el);
                if (!Object.Equals(el, list[e]))
                    list[e] = el;
            }
            for (int e = minC; e < c; ++e)
            {
                E el = default(E);
                regValue(i, ref el);
                list.Add(el);
            }
            while (orgCount > c)
            {
                --orgCount;
                list.RemoveAt(orgCount);
            }
        }
        public static void Describe_ICollection<E>(IInspectorImplementation i, ref T value, RegFieldDelegate<E> regValue)
        {
            ICollection<E> col = value as ICollection<E>;
            if (col.IsReadOnly)
                throw new Exception("Can't describe readonly collections!\nType \"" + value.GetType().FullName + "\"");
            var list = col.ToArray();
            col.Clear();
            int orgCount = list.Length;
            int[] count = new int[] { orgCount };
            i.Array_Begin(count);
            int c = count[0];
            int minC = Math.Min(c, orgCount);
            for (int e = 0; e < minC; ++e)
            {
                regValue(i, ref list[e]);
                col.Add(list[e]);
            }
            for (int e = minC; e < c; ++e)
            {
                E el = default(E);
                regValue(i, ref el);
                col.Add(el);
            }
        }
    }

}

