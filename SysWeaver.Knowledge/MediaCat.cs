using System;


namespace SysWeaver.Knowledge
{
    /// <summary>
    /// Media categories (used as parents of movies, series etc)
    /// </summary>
    public static class MediaCat
    {
        /// <summary>
        /// The group (category) name of all media categories
        /// </summary>
        public const String Group = "Media";


        /// <summary>
        /// The root category, all things entertainment
        /// </summary>
        public static readonly Info Entertainment = Info.GetInfo(out var _, "Entertainment", "All things entertainment", Group);

        /// <summary>
        /// All things media (a child of <see cref="Entertainment"/>)
        /// </summary>
        public static readonly Info Media = Info.GetInfo(out var _, "Media", "All things media", Group, [Entertainment]);

        /// <summary>
        /// The parents of all media categories created by <see cref="Get(String)"/>
        /// </summary>
        public static readonly Info[] AllMedia = [Entertainment, Media];

        /// <summary>
        /// Get (or create) a media category
        /// </summary>
        /// <param name="name">The name of the category</param>
        /// <returns>The category info</returns>
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