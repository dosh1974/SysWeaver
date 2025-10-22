using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.ExchangeRate.Sources
{

    public sealed class ErsFixerDotIo : IRateSource, IDisposable
    {

        public ErsFixerDotIo(ErsFixerDotIoParams p)
        {
            P = p;
            RefreshMinutes = Math.Max(1, p.RefreshMinutes);
            var b = IsoData.IsoCurrency.TryGet(p.Reference)?.Iso4217;
            if (b == null)
                throw new Exception(p.Reference.ToQuoted() + " is not a valid reference currency!");
            Reference = b;
            S = SerManager.Get("json");
            Client = new HttpClient();
        }

        HttpClient Client;

        public void Dispose()
        {
            Interlocked.Exchange(ref Client, null)?.Dispose();
        }

        readonly ISerializerType S;
        readonly ErsFixerDotIoParams P;

        public string Source => "https://fixer.io/";

        public readonly String Reference;

        public int RefreshMinutes { get; init; }

#pragma warning disable CS0649

        sealed class Error
        {
            public string code;
            public string type;

        }

        sealed class Data
        {
            public bool success;
            public long timestamp;
            public string @base;
            public Dictionary<string, decimal> rates;
            public Error error;
        }

#pragma warning restore

        public async Task<Rates> GetRates()
        {
            var b = Reference;
            var key = P.GetApiKey();
            var url = String.Concat("https://data.fixer.io/api/latest?access_key=", key, "&base=", b);
            byte[] data = await Client.GetByteArrayAsync(url).ConfigureAwait(false);
            var d = S.Create<Data>(data.AsSpan());
            if (!d.success)
            {
                var e = d.error?.type;
                if (!string.IsNullOrEmpty(e))
                    throw new Exception(e);
                throw new Exception("Failed to get exchange rates!");
            }
            var time = DateTimeOffset.FromUnixTimeSeconds(d.timestamp).UtcDateTime;
            var rates = d.rates;
            return new Rates
            {
                LastUpdated = time,
                Source = Source,
                Reference = d.@base,
                From = rates.Select(x => new Rate { I = x.Key, R = x.Value }).ToArray(rates.Count),
            };
        }

    }




}
