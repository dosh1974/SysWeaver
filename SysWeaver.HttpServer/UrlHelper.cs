using System;
using System.Buffers;

namespace SysWeaver.Net
{
    public static class UrlHelper
    {

        static void BuildParent(Span<Char> dest, int count)
        {
            int o = 0;
            for (int i = 0; i < count; ++ i)
            {
                dest[o] = '.';
                ++o;
                dest[o] = '.';
                ++o;
                dest[o] = '/';
                ++o;
            }
        }

        static UrlHelper()
        {
            BuildParentAction = BuildParent;
            CacheParentFolders = [
                String.Empty,
                String.Create(3, 1, BuildParentAction),
                String.Create(6, 2, BuildParentAction),
                String.Create(9, 3, BuildParentAction),
            ];
        }

        static readonly SpanAction<Char, int> BuildParentAction;
        static readonly String[] CacheParentFolders;


        /// <summary>
        /// Create a string containg parent folders, level 0 = "", level 1 = "../", level 2 = "../../" and so on.
        /// </summary>
        /// <param name="levels">The level to get a parent folder for</param>
        /// <returns>A prefix that can be used for referencing in a parent folder</returns>
        public static String ParentFolderRef(int levels) => (levels < 4) ? CacheParentFolders[levels] : String.Create(levels * 3, levels, BuildParentAction);

    }
}
