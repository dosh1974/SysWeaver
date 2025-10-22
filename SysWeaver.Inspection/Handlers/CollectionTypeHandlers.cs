using System;
using System.Collections;

namespace SysWeaver.Inspection.Implementation
{
    static class CollectionTypeHandlers
    {

        public static void Describe_List(IInspectorImplementation i, ref IList list)
        {
            int orgCount = list.Count;
            int[] count = new int[] { orgCount };
            i.Array_Begin(count);
            int c = count[0];
            int minC = Math.Min(c, orgCount);
            for (int e = 0; e < minC; ++e)
            {
                var el = list[e];
                i.Field(ref el);
                if (!Object.Equals(el, list[e]))
                    list[e] = el;
            }
            for (int e = minC; e < c; ++e)
            {
                Object el = null;
                i.Field(ref el);
                list.Add(el);
            }
            while (orgCount > c)
            {
                --orgCount;
                list.RemoveAt(orgCount);
            }
        }

    }

}

