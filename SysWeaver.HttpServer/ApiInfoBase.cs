using SysWeaver.Data;
using System;

namespace SysWeaver.Net
{

    /// <summary>
    /// Describes a registered API end point, used as the row type of the API debug table and as the base of richer API descriptions.
    /// </summary>
    [TableDataPrimaryKey(nameof(Uri))]

    public class ApiInfoBase
    {
        /// <summary>
        /// Copy the values of this instance to another instance.
        /// </summary>
        /// <param name="dest">The instance to copy to.</param>
        /// <remarks>Note: <see cref="PerSession"/> and <see cref="Assembly"/> are currently NOT copied.</remarks>
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
        /// The local url of the end point (no leading slash).
        /// </summary>
        [TableDataUrl(null, "*../explore/api.html?q={0}", "Click to show the API details")]
        public String Uri;

        /// <summary>
        /// Auth information: null = open, empty = any logged in user, else comma separated tokens where at least one is required.
        /// </summary>
        [TableDataTags("{^0}", null, "{0}", true)]
        public String Auth;

        /// <summary>
        /// The mime type (without parameters) of the return value if it's raw data, null if the result is a serialized object.
        /// </summary>
        [TableDataMime]
        public String Mime;

        /// <summary>
        /// API description (the title of the method's XML doc summary).
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
        /// If true, the request is cached per session else it's cached globally (only relevant if <see cref="RequestCacheDuration"/> is positive).
        /// </summary>
        public bool PerSession;

        /// <summary>
        /// The compression methods to use (in order of preference) or null if no compression should be applied.
        /// </summary>
        [TableDataTags("{^1}", "Compression quality: {2}", "{0}", true)]
        public String CompPreference;

        /// <summary>
        /// The name of the assembly that declares the API method.
        /// </summary>
        public String Assembly;

        /// <summary>
        /// If true, the response contains data that will be translated to the session language.
        /// </summary>
        public bool Translated;

    }





}
