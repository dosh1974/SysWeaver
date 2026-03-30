using System;

namespace SysWeaver
{
    public interface IStringTree
    {
        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The longest found match or null if no match is found</returns>
        String StartsWithAny(String text, int start = 0);

    }




}
