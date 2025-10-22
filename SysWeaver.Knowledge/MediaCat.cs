using System;


namespace SysWeaver.Knowledge
{
    public static class MediaCat
    {
        public const String Group = "Media";


        public static readonly Info Entertainment = Info.GetInfo(out var _, "Entertainment", "All things entertainment", Group);
        public static readonly Info Media = Info.GetInfo(out var _, "Media", "All things media", Group, [Entertainment]);

        public static readonly Info[] AllMedia = [Entertainment, Media];

        public static Info Get(String name)
        {
            var i = Info.GetInfo(out var wasNew, name, "The media cathegory " + name, Group, AllMedia);
            if (wasNew)
                AllInfo.TryAdd(name, i, false, true);
            return i;
        }


        static MediaCat()
        {
            AllInfo.TryAdd(Entertainment.Name, Entertainment, false, false);
            AllInfo.TryAdd(Media.Name, Media, false, false);
        }
    }

}