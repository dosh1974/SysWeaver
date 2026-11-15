using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Sources
{
    /// <summary>
    /// An ip location source that uses ip2location.io
    /// </summary>
    public sealed class Ip2locationIoSource : IIpLocationSource, IDisposable
    {
        /// <summary>
        /// Create the source
        /// </summary>
        /// <param name="p">The api key, null to use the default <see cref="ApiKeyParams"/></param>
        public Ip2locationIoSource(ApiKeyParams p = null)
        {
            p = p ?? new ApiKeyParams();
            Api = "https://api.ip2location.io/?key=" + p.GetApiKey() + "&ip=";
            S = SerManager.Get("json");
            Client = new HttpClient();
        }

        readonly ISerializerType S;

        HttpClient Client;

        /// <inheritdoc />
        public void Dispose()
        {
            Interlocked.Exchange(ref Client, null)?.Dispose();
        }

        readonly String Api;

        /// <inheritdoc />
        public override string ToString() => "Ip Location source: " + Name;

        /// <inheritdoc />
        public string Name => "ip2location.io";

#pragma warning disable CS0649

        sealed class Data
        {
            public String ip; //"8.8.8.8",
            public String country_code; //"US",
            public String country_name; //"United States of America",
            public String region_name; //"California",
            public String city_name; //"Mountain View",
            public double latitude; //37.405992,
            public double longitude; //-122.078515,
            public String zip_code; //"94043",
            public String time_zone; //"-07:00",
            public String asn; //"15169",
	        public String @as; //"Google LLC",
	        public bool is_proxy; //false
        }

#pragma warning restore CS0649

        /// <inheritdoc />
        public async Task<IpLocation> LookUp(string ip)
        {
            var url = Api + ip;
            byte[] data = await Client.GetByteArrayAsync(url).ConfigureAwait(false);
            var d = S.Create<Data>(data.AsSpan());
            return new IpLocation(d.country_code,
                d.latitude,
                d.longitude,
                StringExt.JoinNonEmpty("\n", StringExt.JoinNonEmpty(" ", d.zip_code, d.city_name), d.region_name, d.country_name),
                null,
                d.@as,
                Name,
                DateTime.UtcNow,
                d.city_name);
        }


    }

}
