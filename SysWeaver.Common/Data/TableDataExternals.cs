using System;

namespace SysWeaver.Data
{

    #region Text 

    /// <summary>
    /// Format values (ISO 3166 alpha-2 country codes) as a link to information about the country.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCountryAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values (ISO 3166 alpha-2 country codes) as a link to information about the country.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataIsoCountryAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "country/{0}",
                  "Click show information about the country with the ISO 3166 Alpha 2 country code: {0}"
            )
        {
        }
    }

    /// <summary>
    /// Format values (ISO 4217 currency codes) as a link to information about the currency.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCurrencyAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values (ISO 4217 currency codes) as a link to information about the currency.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataIsoCurrencyAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "currency/{0}",
                  "Click show information about the currency with the ISO 4217 currency code: {0}"
            )
        {
        }
    }

    /// <summary>
    /// Format values (user agent strings) as a link to information about the user agent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataUserAgentAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values (user agent strings) as a link to information about the user agent.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataUserAgentAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "useragent/{0}",
                  "Click show information about this user agent"
            )
        {
        }
    }
   

    /// <summary>
    /// Format values as a link to a wikipedia page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWikipediaAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values as a link to a wikipedia page.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        /// <param name="searchFormat">The wikipedia page name format, {0} = This value.</param>
        public TableDataWikipediaAttribute(String textFormat = "{0}", String searchFormat = "{0}")
            : base(
                  textFormat,
                  String.Format(TableDataConsts.WikipediaFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.WikipediaTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    /// <summary>
    /// Format values as a link to a google search.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataGoogleSearchAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values as a link to a google search.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        /// <param name="searchFormat">The search term format, {0} = This value.</param>
        public TableDataGoogleSearchAttribute(String textFormat = "{0}", String searchFormat = "{0}")
            : base(
                  textFormat,
                  String.Format(TableDataConsts.GoogleSearchFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    /// <summary>
    /// Format values (file extensions without the leading dot) as a link to a google search about the file extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataFileExtensionAttribute : TableDataGoogleSearchAttribute
    {
        /// <summary>
        /// Format values (file extensions without the leading dot) as a link to a google search about the file extension.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataFileExtensionAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.FileExtensionSearchFormat
            )
        {
        }
    }


    /// <summary>
    /// Format values (mime types) as a link to a google search about the mime type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataMimeAttribute : TableDataGoogleSearchAttribute
    {
        /// <summary>
        /// Format values (mime types) as a link to a google search about the mime type.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataMimeAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.MimeSearchFormat
            )
        {
        }
    }


    /// <summary>
    /// Format values (text encoding names) as a link to a google search about the text encoding.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataEncodingAttribute : TableDataGoogleSearchAttribute
    {
        /// <summary>
        /// Format values (text encoding names) as a link to a google search about the text encoding.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataEncodingAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.EncodingSearchFormat
            )
        {
        }
    }


    /// <summary>
    /// Format values (IP addresses) as a link to information about the IP address.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIpAttribute : TableDataUrlAttribute
    {
        /// <summary>
        /// Format values (IP addresses) as a link to information about the IP address.
        /// </summary>
        /// <param name="textFormat">The text to display, {0} = This value.</param>
        public TableDataIpAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "ip/{0}",
                  "Click show information about the IP \"{0}\""
            )
        {
        }
    }

    #endregion// Text

    #region Image

    /// <summary>
    /// Format values (ISO 3166 alpha-2 country codes) as a country flag linking to information about the country.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCountryImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values (ISO 3166 alpha-2 country codes) as a country flag linking to information about the country.
        /// </summary>
        public TableDataIsoCountryImageAttribute()
            : base(
                  "../iso_data/country/{_0}.svg",
                  TableDataConsts.ExternalInfoRoot + "country/{0}",
                  "Click show information about the country with the ISO 3166 Alpha 2 country code: {0}"
            )
        {
        }
    }

    /// <summary>
    /// Format values (ISO language codes) as a language flag.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoLanguageImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values (ISO language codes) as a language flag.
        /// </summary>
        public TableDataIsoLanguageImageAttribute()
            : base(
                  "../iso_data/language/{_0}.svg",
                  null,
                  null
            )
        {
        }
    }



    /// <summary>
    /// Format values as a wikipedia icon linking to a wikipedia page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWikipediaImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values as a wikipedia icon linking to a wikipedia page.
        /// </summary>
        /// <param name="searchFormat">The wikipedia page name format, {0} = This value.</param>
        public TableDataWikipediaImageAttribute(String searchFormat = "{0}")
            : base(
                  "../icons/external/Wikipedia.svg",
                  String.Format(TableDataConsts.WikipediaFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.WikipediaTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    /// <summary>
    /// Format values as a google search icon linking to a google search.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataGoogleSearchImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values as a google search icon linking to a google search.
        /// </summary>
        /// <param name="searchFormat">The search term format, {0} = This value.</param>
        public TableDataGoogleSearchImageAttribute(String searchFormat = "{0}")
            : base(
                  "../icons/external/GoogleSearch.svg",
                  String.Format(TableDataConsts.GoogleSearchFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }


    /// <summary>
    /// Format values (file extensions without the leading dot) as a file type icon linking to a google search about the file extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataFileExtensionImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values (file extensions without the leading dot) as a file type icon linking to a google search about the file extension.
        /// </summary>
        public TableDataFileExtensionImageAttribute()
            : base(
                  "../icons/ext/{_0}.svg",
                  String.Format(TableDataConsts.GoogleSearchFormat, TableDataConsts.FileExtensionSearchFormat),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, TableDataConsts.FileExtensionSearchFormat)
            )
        {
        }
    }


    /// <summary>
    /// Format values as a google maps icon linking to a google maps place page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataGoogleMapsPlaceImageAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// Format values as a google maps icon linking to a google maps place page.
        /// </summary>
        /// <param name="searchFormat">The place format, {0} = This value.</param>
        public TableDataGoogleMapsPlaceImageAttribute(String searchFormat = "{0}")
            : base(
                  "../icons/external/GoogleMaps.svg",
                  String.Format(TableDataConsts.GoogleMapsPlaceFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.GoogleMapsPlaceTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }


    #endregion//Image


}



