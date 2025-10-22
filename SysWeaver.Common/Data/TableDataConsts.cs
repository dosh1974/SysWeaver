using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Helpers and constants for table data
    /// </summary>
    public static class TableDataConsts
    {
        /// <summary>
        /// String format for an url for searching the web
        /// </summary>
        public const String GoogleSearchFormat = "https://www.google.com/search?q={0}";

        /// <summary>
        /// String format for the title of a search
        /// </summary>
        public const String GoogleSearchTitleFormat = "Click to search google for \"{0}\".";


        /// <summary>
        /// String format for an url for viewing a wikipedia page
        /// </summary>
        public const String WikipediaFormat = "https://en.wikipedia.org/wiki/{0}";

        /// <summary>
        /// String format for the title of a wikipedia page
        /// </summary>
        public const String WikipediaTitleFormat = "Click to show wikipedia information for \"{0}\".";


        /// <summary>
        /// String format for an url for viewing a google maps place page
        /// </summary>
        public const String GoogleMapsPlaceFormat = "https://www.google.com/maps/place/{0}";

        /// <summary>
        /// String format for the title of a google maps place page
        /// </summary>
        public const String GoogleMapsPlaceTitleFormat = "Click to show google maps information for \"{0}\".";


        /// <summary>
        /// File extension search
        /// </summary>
        public const String FileExtensionSearchFormat = "Information about files with extension \".{0}\"";

        /// <summary>
        /// Mime search
        /// </summary>
        public const String MimeSearchFormat = "Information about the \".{0}\" mime type";

        /// <summary>
        /// Mime search
        /// </summary>
        public const String EncodingSearchFormat = "Information about the {0} text encoding format";


        /// <summary>
        /// Default max image width
        /// </summary>
        public const int ImgMaxWidth = 48;

        /// <summary>
        /// Default max image height
        /// </summary>
        public const int ImgMaxHeight = 24;

        /// <summary>
        /// Path used for external information redirects
        /// </summary>
        public const String ExternalInfoPath = "externalInfo/";

        /// <summary>
        /// Absolute root path for external information
        /// </summary>
        public const String ExternalInfoRoot = "https://ext.sysweaver.com/";
    }
}
