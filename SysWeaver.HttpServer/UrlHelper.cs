using System;
using System.Buffers;

namespace SysWeaver.Net
{
    /// <summary>
    /// Url helpers.
    /// </summary>
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
        /// Create a string containing parent folder references, level 0 = "", level 1 = "../", level 2 = "../../" and so on.
        /// </summary>
        /// <param name="levels">The number of levels to go up, must not be negative</param>
        /// <returns>A prefix that can be used for referencing a file in a parent folder (levels 0 to 3 are cached, no allocation)</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="levels"/> is negative</exception>
        public static String ParentFolderRef(int levels) => (levels < 4) ? CacheParentFolders[levels] : String.Create(levels * 3, levels, BuildParentAction);

    }
}
