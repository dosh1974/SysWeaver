using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Sources
{
    public sealed class AppIpgeolocationIoSource : IIpLocationSource, IDisposable
    {
        public AppIpgeolocationIoSource(ApiKeyParams p = null)
        {
            p = p ?? new ApiKeyParams();
            Api = "https://api.ipgeolocation.io/ipgeo?apiKey=" + p.GetApiKey() + "&ip=";
            S = SerManager.Get("json");
            Client = new HttpClient();
        }

        readonly ISerializerType S;

        HttpClient Client;

        public void Dispose()
        {
            Interlocked.Exchange(ref Client, null)?.Dispose();
        }

        readonly String Api;

        public override string ToString() => "Ip Location source: " + Name;

        public string Name => "api.ipgeolocation.io";


#pragma warning disable CS0649

        sealed class Data
        {
            public String hostname; // "dns.google",
            public String continent_code; // "NA",
            public String continent_name; // "North America",
            public String country_code2; // "US",
            public String country_code3; // "USA",
            public String country_name; // "United States",
            public String country_name_official; // "United States of America",
            public String country_capital; // "Washington, D.C.",
            public String state_prov; // "California",
            public String state_code; // "US-CA",
            public String district; // "Santa Clara",
            public String city; // "Mountain View",
            public String zipcode; // "94043-1351",
            public String latitude; // "37.42240",
            public String longitude; // "-122.08421",
            public bool is_eu; // false,
            public String calling_code; // "+1",
            public String country_tld; // ".us",
            public String languages; // "en-US,es-US,haw,fr",
            public String country_flag; // "https://ipgeolocation.io/static/flags/us_64.png",
            public String geoname_id; // "6301403",
            public String isp; // "Google LLC",
            public String connection_type; // "",
            public String organization; // "Google LLC",
            public String country_emoji; // "🇺🇸",
            public String asn; // "AS15169",
        }

#pragma warning restore CS0649

        public async Task<IpLocation> LookUp(string ip)
        {
            var url = Api + ip;
            byte[] data = await Client.GetByteArrayAsync(url).ConfigureAwait(false);
            var d = S.Create<Data>(data.AsSpan());
            return new IpLocation(d.country_code2,
                IpLocationTools.ParseNumber(d.latitude),
                IpLocationTools.ParseNumber(d.longitude),
                StringExt.JoinNonEmpty("\n", StringExt.JoinNonEmpty(" ", d.zipcode, d.city), d.district, d.state_prov, d.country_name, d.continent_name),
                d.state_code,
                d.organization,
                Name, 
                DateTime.UtcNow);
        }


    }


}
