using System;
using System.Collections.Concurrent;

namespace SysWeaver.Net
{
    sealed class WebFolder
    {
        public override string ToString() => String.Concat('"', Url, "\" from [", String.Join("], [", DiscFolders), ']');

        public readonly String Url;

        public readonly ConcurrentBag<DiscFolder> DiscFolders = new();

        public WebFolder(string webFolder)
        {
            Url = webFolder;
        }
    }


}
