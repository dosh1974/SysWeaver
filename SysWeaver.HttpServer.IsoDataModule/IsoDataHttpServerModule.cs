using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.IsoData;
using SysWeaver.MicroService;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.Net.IsoDataModule
{
    public sealed class IsoDataHttpServerModule : IHttpServerModule
    {
        public IsoDataHttpServerModule(StaticDataHttpServerModule data)
        {
            if (data == null)
                return;
            data.AddText("iso_data/languages.js",
                "[IsoData]",
                GetLanguageCode(),
                "text/javascript; charset=UTF-8",
                null,
                30,
                HttpCompressionPriority.GetSupportedEncoders("br:Balanced,deflate:Balanced,gzip:Balanced"));

            data.AddText("iso_data/countries.js",
                "[IsoData]",
                GetCountryCode(),
                "text/javascript; charset=UTF-8",
                null,
                30,
                HttpCompressionPriority.GetSupportedEncoders("br:Balanced,deflate:Balanced,gzip:Balanced"));

            data.AddText("iso_data/currencies.js",
                "[IsoData]",
                GetCurrencyCode(),
                "text/javascript; charset=UTF-8",
                null,
                30,
                HttpCompressionPriority.GetSupportedEncoders("br:Balanced,deflate:Balanced,gzip:Balanced"));

            data.AddText("iso_data/phone_prefixes.js",
                "[IsoData]",
                GetPhonePrefixCodes(),
                "text/javascript; charset=UTF-8",
                null,
                30,
                HttpCompressionPriority.GetSupportedEncoders("br:Balanced,deflate:Balanced,gzip:Balanced"));



            IntUnknown = data.TryGetHandler("iso_data/icons/unknown.svg");
            Data = data;
        }

        IHttpRequestHandler IntUnknown;

        IHttpRequestHandler Unknown
        {
            get
            {
                var u = IntUnknown;
                if (u != null)
                    return u;
                u = Data.TryGetHandler("iso_data/icons/unknown.svg");
                IntUnknown = u;
                return u;
            }
        }

        readonly StaticDataHttpServerModule Data;

        static String GetLanguageCode()
        {
            var sb = new StringBuilder();
            sb.Append(
@"class IsoLanguage
{
    constructor(n,i1,i2)
    {
        this.Name = n;
        this.Iso639_1 = i1;
        this.Iso639_2 = i2;
    }
    static Languages = [
");

            var c = IsoLanguage.Languages;
            var l = c.Count;
            for (int i = 0; i < l; ++ i)
            {
                var data = c[i];
                sb.Append("      new IsoLanguage('").Append(data.Name).Append("','").Append(data.Iso639_1).Append("','").Append(data.Iso639_2);
                if ((i+ 1) >= l)
                    sb.AppendLine("')");
                else
                    sb.AppendLine("'),");
            }
            sb.Append(
@"      ];  
    static Lookup = function()
        {
            const l = IsoLanguage.Languages;
            const c = l.length;
            const map = new Map();
            for (let i = 0; i < c; ++ i)
            {
                const la = l[i];
                map.set(la.Name.toLowerCase(), la);
                map.set(la.Iso639_1.toLowerCase(), la);
                map.set(la.Iso639_2.toLowerCase(), la);
            }
            return map;
        }();

    static Get(languageCode)
    {
        if (!languageCode)
            return null;
        return IsoLanguage.Lookup.get(languageCode.toLowerCase());
    };
}
");
            var code = sb.ToString();
            return code;
        }


        static String GetCountryCode()
        {
            var sb = new StringBuilder();
            sb.Append(
@"class IsoCountry
{
    constructor(i,o,c,cc)
    {
        this.Iso3166a2 = i;
        this.OfficialName = o;
        this.CommonName = c;
        this.Currency = cc;
    }
    static Countries = [
");

            var c = IsoCountry.Countries;
            var l = c.Count;
            for (int i = 0; i < l; ++i)
            {
                var data = c[i];
                sb.Append("      new IsoCountry(\"").Append(data.Iso3166a2).Append("\",\"").Append(data.OfficialName).Append("\",\"").Append(data.CommonName).Append("\",\"").Append(data.Currency);
                if ((i + 1) >= l)
                    sb.AppendLine("\")");
                else
                    sb.AppendLine("\"),");
            }
            sb.Append(
@"      ];  
    static Lookup = function()
        {
            const l = IsoCountry.Countries;
            const c = l.length;
            const map = new Map();
            for (let i = 0; i < c; ++ i)
            {
                const la = l[i];
                map.set(la.Iso3166a2.toLowerCase(), la);
                map.set(la.CommonName.toLowerCase(), la);
                map.set(la.OfficialName.toLowerCase(), la);
            }
            return map;
        }();

    static Get(countryCode)
    {
        if (!countryCode)
            return null;
        return IsoCountry.Lookup.get(countryCode.toLowerCase());
    };
}
");
            var code = sb.ToString();
            return code;
        }

        static String GetCurrencyCode()
        {
            var sb = new StringBuilder();
            sb.Append(
@"class IsoCurrency
{
    constructor(i,c,n,s,u,dn,dd,sp,ss,t,d,nn)
    {
        this.Iso4217 = i;
        this.Country = c;
        this.Number = n;
        this.Symbol = s;
        this.UnderUnits = u;
        this.DecimalNumbers = dn;
        this.DecimalDigits = dd;
        this.SymbolPrefix = sp;
        this.SymbolSuffix = ss;
        this.ThousandsSeparator = t;
        this.DecimalSeparator = d;
        this.Name = nn;
    }
    static Currencies = [
");

            var c = IsoCurrency.Currencies;
            var l = c.Count;
            for (int i = 0; i < l; ++i)
            {
                var data = c[i];
                sb.Append("      new IsoCurrency('").Append(data.Iso4217).Append("','").Append(data.Country).Append("','").Append(data.Number)
                            .Append("','").Append(data.Symbol).Append("','").Append(data.UnderUnits).Append("','").Append(data.DecimalNumbers)
                            .Append("','").Append(data.DecimalDigits).Append("','").Append(data.SymbolPrefix).Append("','").Append(data.SymbolSuffix)
                            .Append("','").Append(data.ThousandsSeparator).Append("','").Append(data.DecimalSeparator).Append("','").Append(data.Name)
                    ;
                if ((i + 1) >= l)
                    sb.AppendLine("')");
                else
                    sb.AppendLine("'),");
            }
            sb.Append(
@"      ];  
    static Lookup = function()
        {
            const l = IsoCurrency.Currencies;
            const c = l.length;
            const map = new Map();
            for (let i = 0; i < c; ++ i)
            {
                const la = l[i];
                map.set(la.Iso4217.toLowerCase(), la);
                map.set(('' + la.Number), la);
                map.set(la.Name.toLowerCase(), la);
            }
            return map;
        }();

    static Get(currencyCode)
    {
        if (!currencyCode)
            return null;
        return IsoCurrency.Lookup.get(('' + currencyCode).toLowerCase());
    };
}
");
            var code = sb.ToString();
            return code;
        }

        static String GetPhonePrefixCodes()
        {
            var sb = new StringBuilder();
            sb.Append(
@"class PhonePrefix
{
    constructor(c,n,i,ip,np,rp,l,r)
    {
        this.CountryCode = c;
        this.Name = n;
        this.IsoCountry = i;
        this.IntPrefix = ip;
        this.NatPrefix = np;
        this.RegionPrefix = rp;
        this.LocalCounts = l;
        this.Rank = r;
    }
    static Codes = [
");

            var c = PhonePrefix.Codes;
            var l = c.Count;
            for (int i = 0; i < l; ++i)
            {
                var data = c[i];
                sb.Append("      new PhonePrefix('").Append(data.CountryCode).Append("','").Append(data.Name).Append("','").Append(data.IsoCountry)
                            .Append("','").Append(data.IntPrefix).Append("','").Append(data.NatPrefix).Append("','").Append(data.RegionPrefix)
                            .Append("',[").Append(String.Join(',', data.LocalCounts)).Append("],").Append(data.Rank)
                    ;
                if ((i + 1) >= l)
                    sb.AppendLine(")");
                else
                    sb.AppendLine("),");
            }
            sb.Append(
@"      ];  
}
");
            var code = sb.ToString();
            return code;
        }


        public override string ToString() => String.Concat(
            "Currencies: ", IsoCurrency.Currencies.Count
            , ", Countries: ", IsoCountry.Countries.Count
            , ", Languages: ", IsoLanguage.Languages.Count
            , ", Phone prefixes: ", PhonePrefix.Codes.Count
            );

        /// <summary>
        /// Get information about all currencies known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic(WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Currencies", null, "IconTableCurrency")]
        public TableData IsoCurrencyTable(TableDataRequest r) => TableDataTools.Get(r, 30000, IsoCurrency.Currencies);

        /// <summary>
        /// Get information about all countries known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic(WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Countries", null, "IconTableCountry")]
        public TableData IsoCountryTable(TableDataRequest r) => TableDataTools.Get(r, 30000, IsoCountry.Countries);

        /// <summary>
        /// Get information about all languages known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic(WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Languages", null, "IconTableLanguage")]
        public Task<TableData> IsoLanguageTable(TableDataRequest r, HttpServerRequest context) => TableDataTools.Get(context, r, 30000, IsoLanguage.Languages);


        /// <summary>
        /// Get information about all international phone number prefixes known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic(WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Phone number prefixes", null, "IconTablePhone")]
        public TableData PhonePrefixTable(TableDataRequest r) => TableDataTools.Get(r, 30000, PhonePrefix.Codes);

        IHttpRequestHandler HandleCountry(String name)
        {
            if (name.FastEquals("explore"))
                return null;
            var i = name.IndexOf('.');
            if (i >= 0)
                name = name.Substring(0, i);
            var ci = IsoCountry.TryGetName(name);
            if (ci == null)
            {
                var t = Data.TryGetHandler("icons/flags/" + name.FastToLower() + ".svg");
                return t ?? Unknown;
            }
            return Data.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
        }

        IEnumerable<IHttpServerEndPoint> EnumCountries()
        {
            var d = Data;
            foreach (var x in IsoCountry.Aliases)
            {
                IHttpServerEndPoint t = d.TryGetHandler("icons/flags/" + x.Value.Iso3166a2.FastToLower() + ".svg");
                if (t != null)
                    yield return new HttpServerEndPoint(
                        String.Concat("iso_data/country/", x.Key, ".svg"),
                        t.Method,
                        t.ClientCacheDuration,
                        t.RequestCacheDuration,
                        t.UseStream,
                        t.CompPreference,
                        t.PreCompressed,
                        t.Auth,
                        t.Type,
                        String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                        t.LastModified,
                        t.Mime,
                        null
                        );
            }
        }

        static readonly IReadOnlyDictionary<String, String> LangMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {  "en", "GB" },
            {  "ko", "KR" },
        }.Freeze();

        IHttpRequestHandler HandleLanguage(String name)
        {
            if (name.FastEquals("explore"))
                return null;
            var i = name.IndexOf('.');
            if (i >= 0)
                name = name.Substring(0, i);
            var lang = IsoLanguage.TryGet(out var ci, name);
            if (ci != null)
                return Data.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
            if (lang == null)
            {
                lang = IsoLanguage.TryGetName(name);
                if (lang == null)
                    return Unknown;
            }
            name = lang.Iso639_1;
            //  Same iso
            ci = IsoCountry.TryGet(name);
            if (ci != null)
            {
                if (!ci.Languages.Split(',').Contains(name))
                    ci = null;
            }
        //  Hardcoded map
            if ((ci == null) && LangMap.TryGetValue(name, out var cname))
                ci = IsoCountry.TryGet(cname);
            if (ci == null)
            {
                foreach (var x in lang.GetCountries())
                {
                    ci = IsoCountry.TryGet(x);
                    if (ci != null)
                        break;
                }
            }
            if (ci == null)
                return Unknown;
            return Data.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
        }


        IEnumerable<IHttpServerEndPoint> EnumLanguages()
        {
            var d = Data;
            foreach (var x in IsoCountry.Countries)
            {
                var ll = x.Languages;
                if (ll == null)
                    continue;
                var cc = x.Iso3166a2;
                IHttpServerEndPoint t = d.TryGetHandler("icons/flags/" + cc.FastToLower() + ".svg");
                if (t == null)
                    continue;
                foreach (var lang in ll.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    yield return new HttpServerEndPoint(
                        String.Concat("iso_data/language/", lang, '-', cc, ".svg"),
                        t.Method,
                        t.ClientCacheDuration,
                        t.RequestCacheDuration,
                        t.UseStream,
                        t.CompPreference,
                        t.PreCompressed,
                        t.Auth,
                        t.Type,
                        String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                        t.LastModified,
                        t.Mime,
                        null
                        );
            }
            foreach (var ln in IsoLanguage.Aliases)
            {
                var lang = ln.Value;
                var name = lang.Iso639_1;
                //  Same iso
                var ci = IsoCountry.TryGet(name);
                if (ci != null)
                {
                    if (!ci.Languages.Split(',').Contains(name))
                        ci = null;
                }
                if ((ci == null) && LangMap.TryGetValue(name, out var cname))
                    ci = IsoCountry.TryGet(cname);
                if (ci == null)
                {
                    foreach (var x in lang.GetCountries())
                    {
                        ci = IsoCountry.TryGet(x);
                        if (ci != null)
                            break;
                    }
                }
                if (ci != null)
                {
                    var cc = ci.Iso3166a2;
                    IHttpServerEndPoint t = d.TryGetHandler("icons/flags/" + cc.FastToLower() + ".svg");
                    if (t == null)
                        continue;
                    yield return new HttpServerEndPoint(
                        String.Concat("iso_data/language/", ln.Key, ".svg"),
                        t.Method,
                        t.ClientCacheDuration,
                        t.RequestCacheDuration,
                        t.UseStream,
                        t.CompPreference,
                        t.PreCompressed,
                        t.Auth,
                        t.Type,
                        String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                        t.LastModified,
                        t.Mime,
                        null
                        );
                }
            }
        }



        IHttpRequestHandler HandlePhonePrefix(String name)
        {
            if (name.FastEquals("explore"))
                return null;
            var i = name.IndexOf('.');
            if (i >= 0)
                name = name.Substring(0, i);
            try
            {
                var countryName = PhonePrefix.Identify(out var p, name)?.Where(x => x.IsoCountry != null)?.FirstOrDefault()?.IsoCountry;
                var ci = IsoCountry.TryGet(countryName);
                if (ci == null)
                {
                    var t = Data.TryGetHandler("icons/flags/" + name.FastToLower() + ".svg");
                    return t ?? Unknown;
                }
                return Data.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
            }catch
            {
                var t = Data.TryGetHandler("icons/flags/" + name.FastToLower() + ".svg");
                return t ?? Unknown;
            }
        }

        IEnumerable<IHttpServerEndPoint> EnumPhonePrefixes()
        {
            var d = Data;
            foreach (var x in PhonePrefix.Codes)
            {
                var name = x.CountryCode;
                var countryName = PhonePrefix.Identify(out var p, name)?.Where(x => x.IsoCountry != null)?.FirstOrDefault()?.IsoCountry;
                var ci = IsoCountry.TryGet(countryName);
                if (ci != null)
                {
                    IHttpServerEndPoint t = d.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
                    if (t != null)
                    {
                        yield return new HttpServerEndPoint(
                            String.Concat("iso_data/phone_prefix/", name, ".svg"),
                            t.Method,
                            t.ClientCacheDuration,
                            t.RequestCacheDuration,
                            t.UseStream,
                            t.CompPreference,
                            t.PreCompressed,
                            t.Auth,
                            t.Type,
                            String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                            t.LastModified,
                            t.Mime,
                            null
                            );
                        yield return new HttpServerEndPoint(
                            String.Concat("iso_data/phone_prefix/+", name, ".svg"),
                            t.Method,
                            t.ClientCacheDuration,
                            t.RequestCacheDuration,
                            t.UseStream,
                            t.CompPreference,
                            t.PreCompressed,
                            t.Auth,
                            t.Type,
                            String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                            t.LastModified,
                            t.Mime,
                            null
                            );
                    }
                }
                var rp = x.RegionPrefix;
                if (String.IsNullOrEmpty(rp))
                    continue;
                foreach (var rs in rp.Split(','))
                {
                    name = x.CountryCode + rs;
                    countryName = PhonePrefix.Identify(out p, name)?.Where(x => x.IsoCountry != null)?.FirstOrDefault()?.IsoCountry;
                    ci = IsoCountry.TryGet(countryName);
                    if (ci != null)
                    {
                        IHttpServerEndPoint t = d.TryGetHandler("icons/flags/" + ci.Iso3166a2.FastToLower() + ".svg");
                        if (t != null)
                        {
                            yield return new HttpServerEndPoint(
                                String.Concat("iso_data/phone_prefix/", name, ".svg"),
                                t.Method,
                                t.ClientCacheDuration,
                                t.RequestCacheDuration,
                                t.UseStream,
                                t.CompPreference,
                                t.PreCompressed,
                                t.Auth,
                                t.Type,
                                String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                                t.LastModified,
                                t.Mime,
                                null
                                );
                            yield return new HttpServerEndPoint(
                                String.Concat("iso_data/phone_prefix/+", name, ".svg"),
                                t.Method,
                                t.ClientCacheDuration,
                                t.RequestCacheDuration,
                                t.UseStream,
                                t.CompPreference,
                                t.PreCompressed,
                                t.Auth,
                                t.Type,
                                String.Concat("[Internal redirect] => ", t.Uri, " @ ", t.Location), t.Size,
                                t.LastModified,
                                t.Mime,
                                null
                                );
                        }
                    }
                }
            }
        }


        public String[] OnlyForPrefixes { get; } = ["iso_data/"];


        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            //if (!context.LocalUrl.FastStartsWith("iso_data/"))
                //return null;
            var url = context.LocalUrl.Substring(9);
            if (url.FastStartsWith("country/"))
                return HandleCountry(url.Substring(8));
            if (url.FastStartsWith("language/"))
                return HandleLanguage(url.Substring(9));
            if (url.FastStartsWith("phone_prefix/"))
                return HandlePhonePrefix(url.Substring(13));
            return null;
        }

        static readonly String ImpLocation = "[Implicit Folder] from " + typeof(IsoDataHttpServerModule).Name;

        static readonly IHttpServerEndPoint CountryFolder = new HttpServerEndPoint("iso_data/country", ImpLocation, HttpServerTools.StartedTime);
        static readonly IHttpServerEndPoint LanguageFolder = new HttpServerEndPoint("iso_data/language", ImpLocation, HttpServerTools.StartedTime);
        static readonly IHttpServerEndPoint PhonePrefixFolder = new HttpServerEndPoint("iso_data/phone_prefix", ImpLocation, HttpServerTools.StartedTime);





        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            if (root == null)
            {
                yield return CountryFolder;
                yield return LanguageFolder;
                yield return PhonePrefixFolder;
                foreach (var x in EnumCountries())
                    yield return x;
                foreach (var x in EnumLanguages())
                    yield return x;
                foreach (var x in EnumPhonePrefixes())
                    yield return x;
            }
            else
            {
                if (root.FastStartsWith("iso_data/"))
                {
                    if (root.Length == 9)
                    {
                        yield return CountryFolder;
                        yield return LanguageFolder;
                        yield return PhonePrefixFolder;
                    }
                    else
                    {
                        root = root.Substring(9);
                        if (root.FastEquals("country/"))
                        {
                            foreach (var x in EnumCountries())
                                yield return x;
                        }
                        else
                        {
                            if (root.FastEquals("language/"))
                            {
                                foreach (var x in EnumLanguages())
                                    yield return x;
                            }else
                            {
                                if (root.FastEquals("phone_prefix/"))
                                {
                                    foreach (var x in EnumPhonePrefixes())
                                        yield return x;
                                }
                            }
                        }
                    }

                }
            }


        }


    }

}
