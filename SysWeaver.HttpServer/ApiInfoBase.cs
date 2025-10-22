using SysWeaver.Data;
using System;

namespace SysWeaver.Net
{

    [TableDataPrimaryKey(nameof(Uri))]

    public class ApiInfoBase
    {
        public void CopyTo(ApiInfoBase dest)
        {
            dest.Uri = Uri;
            dest.Auth = Auth;
            dest.Mime = Mime;
            dest.Desc = Desc;
            dest.ClientCacheDuration = ClientCacheDuration;
            dest.RequestCacheDuration = RequestCacheDuration;
            dest.CompPreference = CompPreference;
            dest.Translated = Translated;
        }


        /// <summary>
        /// The Uri of the end point
        /// </summary>
        [TableDataUrl(null, "*../explore/api.html?q={0}")]
        public String Uri;

        /// <summary>
        /// Auth information, null = open, empty = auth required or comma separted tokens that are required
        /// </summary>
        [TableDataTags("{^0}", null, "{0}", true)]
        public String Auth;

        /// <summary>
        /// The mime type of the return value if it's not a serialized object
        /// </summary>
        [TableDataMime]
        public String Mime;

        /// <summary>
        /// API description (code comments)
        /// </summary>
        [TableDataText(60)]
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the description on an API endpoint")]
        public String Desc;

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        [TableDataNumber(0, "{0} s")]
        public int ClientCacheDuration;

        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// </summary>
        [TableDataNumber(0, "{0} s")]
        public int RequestCacheDuration;

        /// <summary>
        /// If true, the request is cached per session else it's cached globally
        /// </summary>
        public bool PerSession;

        /// <summary>
        /// The compression method to use (in order of preferens) or null if no compression should be applied
        /// </summary>
        [TableDataTags("{^1}", "Compression quality: {2}", "{0}", true)]
        public String CompPreference;

        /// <summary>
        /// The assembly that defined the API
        /// </summary>
        public String Assembly;

        /// <summary>
        /// If true, the request response contains data that will be translated
        /// </summary>
        public bool Translated;

    }





}
