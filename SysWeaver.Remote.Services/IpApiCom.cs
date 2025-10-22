using System;
using System.Threading.Tasks;

using SysWeaver.MicroService;

namespace SysWeaver.Remote.Services
{
    /// <summary>
    /// Information
    /// </summary>
    public sealed class IpApiResponse
    {

        /// <summary>
        /// "success" or "fail", ex: "success"
        /// </summary>
        public string status;

        /// <summary>
        /// Included only when status is "fail".
        /// Can be one of the following: 
        ///     "private range"
        ///     "reserved range"
        ///     "invalid query"
        /// </summary>
        public string message;

        /// <summary>
        /// Continent name, ex: "North America"
        /// </summary>
        public string continent;

        /// <summary>
        /// Two-letter continent code, ex: "NA"
        /// </summary>
        public string continentCode;

        /// <summary>
        /// Country name, ex: "United States"
        /// </summary>
        public string country;

        /// <summary>
        /// Two-letter country code ISO 3166-1 alpha-2, ex: "US"
        /// </summary>
        [EditType(EditTypes.Country)]
        public string countryCode;

        /// <summary>
        /// Region/state short code (FIPS or ISO), ex: "CA or 10"
        /// </summary>
        public string region;

        /// <summary>
        /// Region/state, ex: "California"
        /// </summary>
        public string regionName;

        /// <summary>
        /// City, ex: "Mountain View"
        /// </summary>
        public string city;

        /// <summary>
        /// District (subdivision of city), ex: "Old Farm District"
        /// </summary>
        public string district;

        /// <summary>
        /// Zip code, ex: "94043"
        /// </summary>
        public string zip;

        /// <summary>
        /// Latitude, ex: "37.4192"
        /// </summary>
        [EditRange(-90, 90)]
        [EditSlider]
        public float lat;

        /// <summary>
        /// Longitude, ex: "-122.0574"
        /// </summary>
        [EditRange(0, 360)]
        [EditSlider]
        public float lon;

        /// <summary>
        /// Timezone (tz), ex: "America/Los_Angeles"
        /// </summary>
        public string timezone;

        /// <summary>
        /// Timezone UTC DST offset in seconds, ex: "-25200"
        /// </summary>
        [EditRange(-86400, 86400)]
        public int offset;

        /// <summary>
        /// National currency, ex: "USD"
        /// </summary>
        [EditType(EditTypes.Currency)]
        public string currency;

        /// <summary>
        /// ISP name, ex: "Google"
        /// </summary>
        public string isp;

        /// <summary>
        /// Organization name, ex: "Google"
        /// </summary>
        public string org;

        /// <summary>
        /// AS number and organization, separated by space (RIR). 
        /// Empty for IP blocks not being announced in BGP tables, ex: "AS15169 Google Inc."
        /// </summary>
        public string @as;

        /// <summary>
        /// AS name (RIR). Empty for IP blocks not being announced in BGP tables, ex: "GOOGLE"
        /// </summary>
        public string asname;

        /// <summary>
        /// Reverse DNS of the IP (can delay response), ex: "wi-in-f94.1e100.net"
        /// </summary>
        public string reverse;

        /// <summary>
        /// Mobile (cellular) connection, ex: "true"
        /// </summary>
        public bool mobile;

        /// <summary>
        /// Proxy, VPN or Tor exit address, ex: "true"
        /// </summary>
        public bool proxy;

        /// <summary>
        /// Hosting, colocated or data center, ex: "true"
        /// </summary>
        public bool hosting;

        /// <summary>
        /// IP used for the query, ex: "173.194.67.94"
        /// </summary>
        public string query;

    }

    [RemoteSerializer(RemoteParam.JsonSerializer)]
    public interface IIpApiCom : IRemoteApi
    {
        /// <summary>
        /// Get information about an ip address
        /// </summary>
        /// <param name="address">The ip address to lookup, can be a dns name or empty</param>
        /// <returns>Information about the supplied address</returns>
        [RemoteEndPoint(HttpEndPointTypes.Get, "json/{ip}")]
        [RemoteCache(15)]
        [WebApi]
        Task<IpApiResponse> GetIpInfo(String address = null);
    }

}
