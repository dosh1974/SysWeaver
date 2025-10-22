using System;

namespace SysWeaver.Data
{

    #region Text 

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCountryAttribute : TableDataUrlAttribute
    {
        public TableDataIsoCountryAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "country/{0}",
                  "Click show information about the country with the ISO 3166 Alpha 2 country code: {0}"
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCurrencyAttribute : TableDataUrlAttribute
    {
        public TableDataIsoCurrencyAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "currency/{0}",
                  "Click show information about the currency with the ISO 4217 currency code: {0}"
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataUserAgentAttribute : TableDataUrlAttribute
    {
        public TableDataUserAgentAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.ExternalInfoRoot + "useragent/{0}",
                  "Click show information about this user agent"
            )
        {
        }
    }
   

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWikipediaAttribute : TableDataUrlAttribute
    {
        public TableDataWikipediaAttribute(String textFormat = "{0}", String searchFormat = "{0}")
            : base(
                  textFormat,
                  String.Format(TableDataConsts.WikipediaFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.WikipediaTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataGoogleSearchAttribute : TableDataUrlAttribute
    {
        public TableDataGoogleSearchAttribute(String textFormat = "{0}", String searchFormat = "{0}")
            : base(
                  textFormat,
                  String.Format(TableDataConsts.GoogleSearchFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataFileExtensionAttribute : TableDataGoogleSearchAttribute
    {
        public TableDataFileExtensionAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.FileExtensionSearchFormat
            )
        {
        }
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataMimeAttribute : TableDataGoogleSearchAttribute
    {
        public TableDataMimeAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.MimeSearchFormat
            )
        {
        }
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataEncodingAttribute : TableDataGoogleSearchAttribute
    {
        public TableDataEncodingAttribute(String textFormat = "{0}")
            : base(
                  textFormat,
                  TableDataConsts.EncodingSearchFormat
            )
        {
        }
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIpAttribute : TableDataUrlAttribute
    {
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

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoCountryImageAttribute : TableDataImgAttribute
    {
        public TableDataIsoCountryImageAttribute()
            : base(
                  "../iso_data/country/{_0}.svg",
                  TableDataConsts.ExternalInfoRoot + "country/{0}",
                  "Click show information about the country with the ISO 3166 Alpha 2 country code: {0}"
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIsoLanguageImageAttribute : TableDataImgAttribute
    {
        public TableDataIsoLanguageImageAttribute()
            : base(
                  "../iso_data/language/{_0}.svg",
                  null,
                  null
            )
        {
        }
    }



    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWikipediaImageAttribute : TableDataImgAttribute
    {
        public TableDataWikipediaImageAttribute(String searchFormat = "{0}")
            : base(
                  "../icons/external/Wikipedia.svg",
                  String.Format(TableDataConsts.WikipediaFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.WikipediaTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataGoogleSearchImageAttribute : TableDataImgAttribute
    {
        public TableDataGoogleSearchImageAttribute(String searchFormat = "{0}")
            : base(
                  "../icons/external/GoogleSearch.svg",
                  String.Format(TableDataConsts.GoogleSearchFormat, searchFormat ?? "{0}"),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, searchFormat ?? "{0}")
            )
        {
        }
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataFileExtensionImageAttribute : TableDataImgAttribute
    {
        public TableDataFileExtensionImageAttribute()
            : base(
                  "../icons/ext/{_0}.svg",
                  String.Format(TableDataConsts.GoogleSearchFormat, TableDataConsts.FileExtensionSearchFormat),
                  String.Format(TableDataConsts.GoogleSearchTitleFormat, TableDataConsts.FileExtensionSearchFormat)
            )
        {
        }
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataGoogleMapsPlaceImageAttribute : TableDataImgAttribute
    {
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



