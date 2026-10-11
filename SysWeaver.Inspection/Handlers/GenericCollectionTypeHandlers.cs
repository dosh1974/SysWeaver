using System;
using System.Collections.Generic;

namespace SysWeaver.Inspection.Implementation
{

    /// <summary>
    /// Describes generic collections.
    /// A writer (an <see cref="IWriteInspector"/>, it never changes the values) only reads the collection, so several writers may write the same collection at the same time.
    /// Other inspectors only modify the collection if the count or an item changed.
    /// </summary>
    static class GenericCollectionTypeHandlers<T>
    {

        public static void Describe_IList<E>(IInspectorImplementation i, ref T value, RegFieldDelegate<E> regValue)
        {
            IList<E> list = value as IList<E>;
            int orgCount = list.Count;
            int[] count = new int[] { orgCount };
            i.Array_Begin(count);
            int c = count[0];
            if (i is IWriteInspector)
            {
                //  Writing: read only (no compare, no boxing)
                for (int e = 0; e < c; ++e)
                {
                    var el = list[e];
                    regValue(i, ref el);
                }
                return;
            }
            int minC = Math.Min(c, orgCount);
            var cmp = EqualityComparer<E>.Default;
            for (int e = 0; e < minC; ++e)
            {
                var org = list[e];
                var el = org;
                regValue(i, ref el);
                if (!cmp.Equals(el, org))
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
            int orgCount = col.Count;
            int[] count = new int[] { orgCount };
            i.Array_Begin(count);
            int c = count[0];
            if (i is IWriteInspector)
            {
                //  Writing: the items are only read (no copy, the collection isn't modified, so read-only collections can be written too)
                foreach (var item in col)
                {
                    var el = item;
                    regValue(i, ref el);
                }
                return;
            }
            //  The items are described as copies, the collection is only cleared and refilled if the count or an item changed (when reading into a non-empty collection)
            E[] list;
            if (orgCount > 0)
            {
                list = new E[orgCount];
                col.CopyTo(list, 0);
            }
            else
            {
                list = Array.Empty<E>();
            }
            int minC = Math.Min(c, orgCount);
            bool changed = c != orgCount;
            var cmp = EqualityComparer<E>.Default;
            for (int e = 0; e < minC; ++e)
            {
                var el = list[e];
                regValue(i, ref el);
                if (!changed)
                    changed = !cmp.Equals(el, list[e]);
                list[e] = el;
            }
            if (!changed)
                return;
            if (col.IsReadOnly)
                throw new NotSupportedException("Can't change a readonly collection!\nType \"" + value.GetType().FullName + "\"");
            col.Clear();
            for (int e = 0; e < minC; ++e)
                col.Add(list[e]);
            for (int e = minC; e < c; ++e)
            {
                E el = default(E);
                regValue(i, ref el);
                col.Add(el);
            }
        }
    }

}

