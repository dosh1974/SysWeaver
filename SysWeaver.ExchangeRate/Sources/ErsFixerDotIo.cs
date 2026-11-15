using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.ExchangeRate.Sources
{

    /// <summary>
    /// A rate source that gets the rates from fixer.io
    /// </summary>
    public sealed class ErsFixerDotIo : IRateSource, IDisposable
    {

        /// <summary>
        /// Create the source
        /// </summary>
        /// <param name="p">Parameters (the reference currency must be a valid currency)</param>
        /// <exception cref="Exception">If the reference currency isn't valid</exception>
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

        /// <inheritdoc />
        public void Dispose()
        {
            Interlocked.Exchange(ref Client, null)?.Dispose();
        }

        readonly ISerializerType S;
        readonly ErsFixerDotIoParams P;

        /// <inheritdoc />
        public string Source => "https://fixer.io/";

        /// <summary>
        /// The reference currency (a three letter ISO 4217 code)
        /// </summary>
        public readonly String Reference;

        /// <inheritdoc />
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

        /// <inheritdoc />
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
