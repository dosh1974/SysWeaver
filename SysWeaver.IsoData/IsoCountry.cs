using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver.IsoData
{

    /// <summary>
    /// Contains information about a country or territory (ISO 3166 Alpha 2 code, names, currency, population, area and languages).
    /// Use <see cref="TryGet(string)"/> to look up by ISO code, <see cref="TryGetName(string)"/> to look up by (fuzzy) name, or enumerate <see cref="Countries"/>.
    /// </summary>
    /// <remarks>
    /// All instances are created once from built-in static data and are immutable, so the type is thread safe.
    /// Population and land area figures are estimates and zero when unknown.
    /// Source: World Bank Open Data API (fetched 2026-10-07), indicators SP.POP.TOTL (population, total; 2025 values) and
    /// AG.LND.TOTL.K2 (land area in km², rounded; 2023 values). Countries and territories that the World Bank doesn't cover keep their older
    /// (around 2020) curated figures: AI, AQ, AX, BL, BQ, BV, CC, CK, CX, EH, FK, GF, GG, GP, GS, HM, IO, JE, MQ, MS, NF, NU, PM, PN, RE, SH, SJ, TF, TK, TW, UM, VA, WF, YT
    /// (and the land area of XK).
    /// </remarks>
    [TableDataPrimaryKey(nameof(CommonName))]
    public sealed class IsoCountry
    {
        /// <summary>
        /// The flag of the country (same value as <see cref="Iso3166a2"/>, rendered as a flag in table data views).
        /// </summary>
        [TableDataIsoCountryImage]
        [TableDataOrder(-1)]
        public String Flag => Iso3166a2;

        /// <summary>
        /// The ISO 3166 Alpha 2 country code of the country.
        /// </summary>
        [TableDataIsoCountry]
        [TableDataKey]
        public readonly String Iso3166a2;
        /// <summary>
        /// The official name of the country
        /// </summary>
        [TableDataWikipedia]
        [TableDataKey]
        public readonly String OfficialName;
        /// <summary>
        /// The common name of the country
        /// </summary>
        [TableDataWikipedia]
        public readonly String CommonName;
        /// <summary>
        /// ISO 4217 currency code of the most common currency used in the country, or null if none (ex: Antarctica).
        /// </summary>
        [TableDataIsoCurrency]
        public readonly String Currency;

        /// <summary>
        /// The population estimate (number of people, World Bank 2025 where available, else around 2020) of the country, a zero means no information
        /// </summary>
        public readonly long Population;

        /// <summary>
        /// The land area estimate in km² (World Bank 2023 where available, else around 2020) of the country, a zero means no information
        /// </summary>
        [TableDataNumber(0, "{0} km²")]
        public readonly int LandArea;

        /// <summary>
        /// Population density in people per km² (<see cref="Population"/> / <see cref="LandArea"/>) of the country, a zero means no information
        /// </summary>
        [TableDataNumber(1, "{0} p/km²")]
        [TableDataOrder(1)]
        public decimal PopDense => LandArea <= 0 ? 0M : ((Decimal)Population / (Decimal)LandArea);

        /// <summary>
        /// The languages spoken in the country as a comma separated list of ISO 639-1 language codes (most important first), empty if unknown.
        /// </summary>
        [TableDataOrder(2)]
        public readonly String Languages;

        /// <summary>
        /// Returns a debug friendly description, ex: "SE Sweden [SEK]".
        /// </summary>
        /// <returns>The ISO code, official name and currency (if any) of the country.</returns>
        public override string ToString()
        {
            if (String.IsNullOrEmpty(Currency))
                return String.Concat(Iso3166a2, ' ', OfficialName);
            return String.Concat(Iso3166a2, ' ', OfficialName, " [" + Currency + "]");
        }

        IsoCountry(String iso3166a2, String officialName, String commonName, String currency, int pop, int size, String langs, String nicks = null)
        {
            Iso3166a2 = iso3166a2;
            OfficialName = officialName;
            CommonName = commonName;
            Currency = String.IsNullOrEmpty(currency) ? null : currency;
            Population = pop;
            LandArea = size;
            Languages = langs;
            Nicks = nicks;
        }

        /// <summary>
        /// Optional comma separated list of alternative names (nick names, abbreviations) for the country, used by <see cref="TryGetName(string)"/>.
        /// Null if the country has no alternative names.
        /// </summary>
        public readonly String Nicks;
        
        /// <summary>
        /// Get information about a country from a two letter ISO 3166-A2 country code (case insensitive, no trimming).
        /// Ex:
        ///   "GB" =&gt; United Kingdom
        ///   "se" =&gt; Sweden
        /// </summary>
        /// <param name="iso3166a2">A two letter ISO 3166-A2 country code, may be null.</param>
        /// <returns>Information about the country if it's known, or null if it's unknown</returns>
        public static IsoCountry TryGet(String iso3166a2) => IsoToInfo.TryGetValue(iso3166a2?.FastToLower() ?? "", out var i) ? i : null;

        /// <summary>
        /// Get information about a country from a country name, nick name, ISO code or a distinctive word of the name (case and diacritics insensitive).
        /// Ex:
        ///   "United Kingdom" =&gt; "GB"
        ///   "UK" =&gt; "GB"
        ///   "SWEDEN" =&gt; "SE"
        /// </summary>
        /// <param name="name">The name of the country, may be null.</param>
        /// <returns>Information about the country if it's known, or null if it's unknown</returns>
        /// <remarks>
        /// The lookup table is built once and contains full names, nick names, ISO codes, single words (4+ letters) of names, and abbreviated variants
        /// (ex: "st" for "saint", "rep" for "republic", "is" for "island(s)", "n"/"s"/"e"/"w" for directions). When a single word matches multiple countries,
        /// the most populous country wins. The input is not trimmed.
        /// </remarks>
        public static IsoCountry TryGetName(String name)
        {
            var ni = NameToInfo;
            name = name?.FastToLower() ?? "";
            if (ni.TryGetValue(FixName(name), out var i))
                return i;
            return null;
        }


        /// <summary>
        /// Normalizes a (lower cased) country name for lookup: removes diacritics and periods, and replaces hyphens with spaces.
        /// </summary>
        /// <param name="name">The name to normalize, must not be null.</param>
        /// <returns>The normalized name.</returns>
        public static String FixName(String name)
        {
            name = name.RemoveDiacritics();
            name = name.Replace('-', ' ');
            name = name.Replace(".", "");
            return name;
        }


        /// <summary>
        /// Enumerates all aliases (lower case lookup keys used by <see cref="TryGetName(string)"/>) and the country they map to.
        /// </summary>
        public static IEnumerable<KeyValuePair<String, IsoCountry>> Aliases => NameToInfo;

        static readonly IReadOnlySet<String> Ignore = ReadOnlyData.Set(StringComparer.Ordinal,
            "states", "state", "sint", "saint", "african", "the", "democratic", "united", "south", "republic", "hong", "kong", "mcdonald", "isle", "man", "north", "south", "west", "east", "sri", "new", "rico", "island", "islands", "de", "da", "city", "state", "states", "africa"
        );

        static IsoCountry()
        {
            var t = new Dictionary<string, IsoCountry>(StringComparer.Ordinal);
            foreach (var c in Countries)
                t.Add(c.Iso3166a2.FastToLower(), c);
            IsoToInfo = t.Freeze();
            t = new Dictionary<string, IsoCountry>(StringComparer.Ordinal);
            
            void AddOne(String s, IsoCountry c)
            {
                var cc = s[0];
                if (!Char.IsLetter(cc))
                    return;
                if (!Char.IsUpper(cc))
                    return;
                if (s.Length < 4)
                    return;
                s = s.FastToLower();
                if (Ignore.Contains(s))
                    return;
                if ((!t.TryGetValue(s, out var e)) || (c.Population > e.Population))
                    t[s] = c;
            }

            void AddSplit(String s, IsoCountry c, String split)
            {
                for (; ; )
                {
                    var i = s.IndexOf(split);
                    if (i < 0)
                        return;
                    AddOne(s.Substring(0, i), c);
                    s = s.Substring(i + split.Length);
                    AddOne(s, c);
                }
            }
            foreach (var c in Countries)
            {
                AddSplit(c.CommonName, c, " and ");
                AddSplit(c.OfficialName, c, " and ");
                AddSplit(c.CommonName, c, "-");
                AddSplit(c.OfficialName, c, "-");
                AddSplit(c.CommonName, c, " ");
                AddSplit(c.OfficialName, c, " ");
            }
            foreach (var c in Countries)
            {
                t[c.Iso3166a2.FastToLower()] = c;
                t[c.CommonName.FastToLower()] = c;
                t[c.OfficialName.FastToLower()] = c;
            }
            foreach (var c in Countries)
            {
                var p = c.Nicks;
                if (p == null)
                    continue;
                foreach (var x in p.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    t.TryAdd(x.FastToLower(), c);
            }
            foreach (var x in t.ToList())
            {
                var b = FixName(x.Key);
                t.TryAdd(b, x.Value);
                b = StringTools.RemoveGroup(b);
                t.TryAdd(b, x.Value);
            }
            foreach (var x in t.ToList())
            {
                var b = x.Key;
                t.TryAdd(b.Replace("sint", "saint"), x.Value);
                b = b.Replace("saint", "st");
                b = b.Replace("sint", "st");
                t.TryAdd(b, x.Value);
            }
            foreach (var x in t.ToList())
            {
                var b = x.Key;
                t.TryAdd(b.Replace("republic", "rep"), x.Value);
            }
            foreach (var x in t.ToList())
            {
                var b = x.Key;
                t.TryAdd(b.Replace("democratic", "dem"), x.Value);
            }
            foreach (var x in t.ToList())
            {
                var b = x.Key;
                b = b.Replace("islands", "is");
                b = b.Replace("island", "is");
                t.TryAdd(b, x.Value);
            }
            foreach (var x in t.ToList())
            {
                var b = x.Key;
                b = b.Replace("south", "s");
                b = b.Replace("southern", "s");
                b = b.Replace("north", "n");
                b = b.Replace("northern", "n");
                b = b.Replace("west", "w");
                b = b.Replace("western", "w");
                b = b.Replace("east", "e");
                b = b.Replace("eastern", "e");
                t.TryAdd(b, x.Value);
            }
            NameToInfo = t.Freeze();
        }

        static readonly IReadOnlyDictionary<String, IsoCountry> IsoToInfo;
        static readonly IReadOnlyDictionary<String, IsoCountry> NameToInfo;


        /// <summary>
        /// All known countries and territories, ordered (mostly) alphabetically by ISO 3166 Alpha 2 code.
        /// </summary>
        public static readonly IReadOnlyList<IsoCountry> Countries = new IsoCountry[]
        {
new IsoCountry("AD", "Andorra", "Andorra", "EUR", 82904, 470, "ca"),
new IsoCountry("AE", "United Arab Emirates", "United Arab Emirates", "AED", 11513149, 71020, "ar", "UAE"),
new IsoCountry("AF", "Afghanistan", "Afghanistan", "AFN", 43844111, 652230, "fa,ps"),
new IsoCountry("AG", "Antigua and Barbuda", "Antigua and Barbuda", "XCD", 94209, 440, "en", "Antigua and Barb."),
new IsoCountry("AI", "Anguilla", "Anguilla", "XCD", 15003, 90, ""),
new IsoCountry("AL", "Albania", "Albania", "ALL", 2349580, 27400, "sq"),
new IsoCountry("AM", "Armenia", "Armenia", "AMD", 3086700, 28199, "hy"),
new IsoCountry("AO", "Angola", "Angola", "AOA", 39040039, 1246700, "pt,hz,kg,kj,ng"),
new IsoCountry("AQ", "Antarctica", "Antarctica", null, 0, 0, "", "Fr S Antarctic Lands"),
new IsoCountry("AR", "Argentina", "Argentina", "ARS", 45851378, 2736690, "es,cy"),
new IsoCountry("AS", "American Samoa", "American Samoa", "USD", 46029, 200, ""),
new IsoCountry("AT", "Austria", "Austria", "EUR", 9208163, 82520, "de,sl"),
new IsoCountry("AU", "Australia", "Australia", "AUD", 27614411, 7692020, "en", "Ashmore and Cartier Is"),
new IsoCountry("AW", "Aruba", "Aruba", "AWG", 108785, 180, ""),
new IsoCountry("AX", "Åland islands", "Åland islands", "EUR", 0, 0, ""),
new IsoCountry("AZ", "Azerbaijan", "Azerbaijan", "AZN", 10246996, 82650, "az"),
new IsoCountry("BA", "Bosnia and Herzegovina", "Bosnia and Herzegovina", "BAM", 3140095, 51200, "bs", "Bosnia and Herz"),
new IsoCountry("BB", "Barbados", "Barbados", "BBD", 282623, 430, "en"),
new IsoCountry("BD", "Bangladesh", "Bangladesh", "BDT", 175686899, 129980, "bn"),
new IsoCountry("BE", "Belgium", "Belgium", "EUR", 11941781, 30494, "nl,fr,de,li,wa"),
new IsoCountry("BF", "Burkina Faso", "Burkina Faso", "XOF", 24074580, 273600, "fr"),
new IsoCountry("BG", "Bulgaria", "Bulgaria", "BGN", 6433302, 108560, "bg"),
new IsoCountry("BH", "Bahrain", "Bahrain", "BHD", 1600366, 800, "ar"),
new IsoCountry("BI", "Burundi", "Burundi", "BIF", 14390003, 25680, "fr,rn,en"),
new IsoCountry("BJ", "Benin", "Benin", "XOF", 14814460, 112760, "fr,ee,yo"),
new IsoCountry("BL", "Saint Barthélemy", "Saint Barthélemy", "EUR", 9877, 21, "", "St-Barthélemy"),
new IsoCountry("BM", "Bermuda", "Bermuda", "BMD", 64555, 54, ""),
new IsoCountry("BN", "Brunei Darussalam", "Brunei", "BND", 466330, 5270, "ms"),
new IsoCountry("BO", "Plurinational state of Bolivia", "Bolivia", "BOB", 12581843, 1083300, "es,ay,gn,qu"),
new IsoCountry("BQ", "Sint Eustatius and Saba Bonaire", "Saba", "USD", 0, 0, "", "Saint Eustatius"),
new IsoCountry("BR", "Brazil", "Brazil", "BRL", 212812405, 8358140, "pt", "Brazilian I"),
new IsoCountry("BS", "The Bahamas", "Bahamas", "BSD", 403033, 10010, "en"),
new IsoCountry("BT", "Bhutan", "Bhutan", "BTN", 796682, 38140, "dz"),
new IsoCountry("BW", "Botswana", "Botswana", "BWP", 2562122, 566730, "en,hz"),
new IsoCountry("BV", "Bouvet island", "Bouvet island", "NOK", 0, 0, ""),
new IsoCountry("BY", "Belarus", "Belarus", "BYN", 9085991, 202983, "be,ru"),
new IsoCountry("BZ", "Belize", "Belize", "BZD", 422924, 22810, "en"),
new IsoCountry("CA", "Canada", "Canada", "CAD", 41651653, 8788700, "en,fr,cr,gd,iu,ik,oj"),
new IsoCountry("CC", "Cocos (Keeling) islands", "Cocos islands", "AUD", 0, 0, "en"),
new IsoCountry("CD", "Democratic republic of the Congo", "DR Congo", "CDF", 112832473, 2267050, "fr,kg,ln,lu", "Dem Rep Congo"),
new IsoCountry("CF", "Central African republic", "Central African republic", "XAF", 5513282, 622980, "fr,sg", "Central African Rep"),
new IsoCountry("CG", "Republic of the Congo", "Congo", "XAF", 6484437, 341500, "fr,kg,ln", "Republic of Congo"),
new IsoCountry("CH", "Switzerland", "Switzerland", "CHF", 9092436, 39510, "rm"),
new IsoCountry("CI", "Côte d'Ivoire", "Ivory coast", "XOF", 32711547, 318000, "fr", "Cote DIvoire"),
new IsoCountry("CK", "Cook islands", "Cook islands", "NZD", 17564, 240, "en", "Cook Is."),
new IsoCountry("CL", "Chile", "Chile", "CLP", 19859921, 742832, "es"),
new IsoCountry("CM", "Cameroon", "Cameroon", "XAF", 29879337, 472710, "en,fr,kr"),
new IsoCountry("CN", "China", "China", "CNY", 1406585000, 9388210, "zh,ii,bo,ug,za"),
new IsoCountry("CO", "Colombia", "Colombia", "COP", 53425635, 1109500, "es"),
new IsoCountry("CR", "Costa Rica", "Costa Rica", "CRC", 5152950, 51060, "es"),
new IsoCountry("CU", "Cuba", "Cuba", "CUP", 10937203, 103800, "es"),
new IsoCountry("CV", "Cabo Verde", "Cape Verde", "CVE", 527326, 4030, "pt"),
new IsoCountry("CW", "Curaçao", "Curaçao", "ANG", 156263, 444, ""),
new IsoCountry("CX", "Christmas island", "Christmas island", "AUD", 0, 0, "en,ms"),
new IsoCountry("CY", "Cyprus", "Cyprus", "EUR", 1370754, 9240, "el,tr"),
new IsoCountry("CZ", "Czech republic", "Czechia", "CZK", 10886878, 77167, "cs,sk"),
new IsoCountry("DE", "Germany", "Germany", "EUR", 83491249, 349430, "de,li"),
new IsoCountry("DJ", "Djibouti", "Djibouti", "DJF", 1184076, 23180, "ar,fr"),
new IsoCountry("DK", "Denmark", "Denmark", "DKK", 6009169, 40000, "da,fo,kl"),
new IsoCountry("DM", "Dominica", "Dominica", "XCD", 65871, 750, "en"),
new IsoCountry("DO", "Dominican republic", "Dominican republic", "DOP", 11520487, 48198, "es"),
new IsoCountry("DZ", "Algeria", "Algeria", "DZD", 47435312, 2381740, "ar"),
new IsoCountry("EC", "Ecuador", "Ecuador", "USD", 18289896, 248360, "es"),
new IsoCountry("EE", "Estonia", "Estonia", "EUR", 1366475, 42730, "et"),
new IsoCountry("EG", "Egypt", "Egypt", "EGP", 118365995, 995450, "ar"),
new IsoCountry("EH", "Western Sahara", "Western Sahara", "MAD", 597339, 266, "", "W Sahara"),
new IsoCountry("ER", "Eritrea", "Eritrea", "ERN", 3607003, 121178, "ti"),
new IsoCountry("ES", "Spain", "Spain", "EUR", 49355143, 499697, "es,an,eu,gl,oc", "Canary islands"),
new IsoCountry("ET", "Ethiopia", "Ethiopia", "ETB", 135472051, 1128499, "aa,am,om,so,ti"),
new IsoCountry("FI", "Finland", "Finland", "EUR", 5646436, 303960, "fi,sv,se"),
new IsoCountry("FJ", "Fiji", "Fiji", "FJD", 933154, 18270, "en,fj"),
new IsoCountry("FK", "Falkland islands (Malvinas)", "Falkland islands", "FKP", 348, 1217, ""),
new IsoCountry("FM", "Federated states of Micronesia", "Micronesia", "USD", 113683, 700, "en"),
new IsoCountry("FO", "Faroe islands", "Faroe islands", "DKK", 54900, 1370, "", "Faeroe islands"),
new IsoCountry("FR", "France", "France", "EUR", 68720337, 538950, "fr,br,co,oc,wa"),
new IsoCountry("GA", "Gabon", "Gabon", "XAF", 2593130, 257670, "fr,kg"),
new IsoCountry("GB", "United Kingdom of Great Britain and Northern Ireland", "United kingdom", "GBP", 69487000, 241930, "en,kw,gd,cy", "UK,England,Falkland islands"),
new IsoCountry("GD", "Grenada", "Grenada", "XCD", 117303, 340, "en"),
new IsoCountry("GE", "Georgia", "Georgia", "GEL", 3935766, 69490, "ka,ab,os"),
new IsoCountry("GF", "French Guiana", "French Guiana", "EUR", 298682, 822, ""),
new IsoCountry("GG", "Guernsey", "Guernsey", "GBP", 0, 0, ""),
new IsoCountry("GH", "Ghana", "Ghana", "GHS", 35064272, 227533, "en,ak,ee,tw"),
new IsoCountry("GI", "Gibraltar", "Gibraltar", "GIP", 40126, 10, ""),
new IsoCountry("GL", "Greenland", "Greenland", "DKK", 56831, 410450, ""),
new IsoCountry("GM", "The Gambia", "Gambia", "GMD", 2822093, 10120, "en,wo"),
new IsoCountry("GN", "Guinea", "Guinea", "GNF", 15099727, 245720, "fr"),
new IsoCountry("GP", "Guadeloupe", "Guadeloupe", "EUR", 400124, 169, ""),
new IsoCountry("GQ", "Equatorial Guinea", "Equatorial Guinea", "XAF", 1938431, 28050, "fr,pt,es", "Eq Guinea"),
new IsoCountry("GR", "Greece", "Greece", "EUR", 10413962, 128900, "el"),
new IsoCountry("GS", "South Georgia and the south Sandwich islands", "South Georgia and the south Sandwich islands", "GBP", 0, 0, "", "S Geo and the islands"),
new IsoCountry("GT", "Guatemala", "Guatemala", "GTQ", 18687881, 107160, "es"),
new IsoCountry("GU", "Guam", "Guam", "USD", 168999, 540, "ch"),
new IsoCountry("GW", "Guinea-Bissau", "Guinea-Bissau", "XOF", 2249515, 28120, "pt"),
new IsoCountry("GY", "Guyana", "Guyana", "GYD", 835986, 211140, "en"),
new IsoCountry("HK", "Hong Kong", "Hong Kong", "HKD", 7498900, 1050, ""),
new IsoCountry("HM", "Heard island and McDonald islands", "Heard island and McDonald islands", "AUD", 0, 0, "", "Heard I and McDonald Islands"),
new IsoCountry("HN", "Honduras", "Honduras", "HNL", 11005850, 111890, "es"),
new IsoCountry("HR", "Croatia", "Croatia", "HRK", 3876200, 55960, "hr"),
new IsoCountry("HT", "Haiti", "Haiti", "HTG", 11906095, 27560, "fr,ht"),
new IsoCountry("HU", "Hungary", "Hungary", "HUF", 9514251, 91260, "hu,sl"),
new IsoCountry("ID", "Indonesia", "Indonesia", "IDR", 285721236, 1892555, "id,jv,su"),
new IsoCountry("IE", "Ireland", "Ireland", "EUR", 5484367, 68890, "ga,en"),
new IsoCountry("IL", "Israel", "Israel", "ILS", 10122800, 21640, "he,yi"),
new IsoCountry("IM", "Isle of Man", "Isle of Man", "GBP", 84118, 570, "gv,en"),
new IsoCountry("IN", "India", "India", "INR", 1463865525, 2973190, "hi,en,as,gu,kn,ks,ml,mr,or,pa,sa,sd,te"),
new IsoCountry("IO", "British Indian ocean territory", "British Indian ocean territory", "USD", 0, 0, "", "Indian Ocean Ter,Br Indian Ocean Ter"),
new IsoCountry("IQ", "Iraq", "Iraq", "IQD", 47020774, 434130, "ar,ku"),
new IsoCountry("IR", "Islamic republic of Iran", "Iran", "IRR", 92417681, 1622500, "fa"),
new IsoCountry("IS", "Iceland", "Iceland", "ISK", 392404, 100830, "is"),
new IsoCountry("IT", "Italy", "Italy", "EUR", 58915656, 295720, "it,oc,sc,sl"),
new IsoCountry("JE", "Jersey", "Jersey", "GBP", 0, 0, ""),
new IsoCountry("JM", "Jamaica", "Jamaica", "JMD", 2837077, 10830, "en"),
new IsoCountry("JO", "Jordan", "Jordan", "JOD", 11520684, 88794, "ar"),
new IsoCountry("JP", "Japan", "Japan", "JPY", 123366734, 364569, "ja"),
new IsoCountry("KE", "Kenya", "Kenya", "KES", 57532493, 580876, "en,sw,ki"),
new IsoCountry("KG", "Kyrgyzstan", "Kyrgyzstan", "KGS", 7343064, 191800, "ky,ru,ug"),
new IsoCountry("KH", "Cambodia", "Cambodia", "KHR", 17847982, 176520, "km"),
new IsoCountry("KI", "Kiribati", "Kiribati", "AUD", 136488, 810, "en"),
new IsoCountry("KM", "Comoros", "Comoros", "KMF", 882847, 1861, "ar,fr"),
new IsoCountry("KN", "Saint Kitts and Nevis", "Saint Kitts and Nevis", "XCD", 46922, 260, "en"),
new IsoCountry("KP", "Democratic people's republic of Korea", "North Korea", "KPW", 26571036, 120410, "ko", "Dem. Rep. Korea"),
new IsoCountry("KR", "Republic of Korea", "South Korea", "KRW", 51684564, 97600, "ko"),
new IsoCountry("KW", "Kuwait", "Kuwait", "KWD", 4865298, 17820, "ar"),
new IsoCountry("KY", "Cayman islands", "Cayman islands", "KYD", 75844, 240, ""),
new IsoCountry("KZ", "Kazakhstan", "Kazakhstan", "KZT", 20843754, 2699700, "kk,ru,ug"),
new IsoCountry("LA", "Lao people's Democratic republic", "Laos", "LAK", 7873046, 230800, "lo", "Lao PDR"),
new IsoCountry("LB", "Lebanon", "Lebanon", "LBP", 5849421, 10230, "ar"),
new IsoCountry("LC", "Saint Lucia", "Saint Lucia", "XCD", 180149, 610, "en"),
new IsoCountry("LI", "Liechtenstein", "Liechtenstein", "CHF", 41024, 160, "de"),
new IsoCountry("LK", "Sri Lanka", "Sri Lanka", "LKR", 21756000, 61860, "si,ta"),
new IsoCountry("LR", "Liberia", "Liberia", "LRD", 5731206, 96320, "en"),
new IsoCountry("LS", "Lesotho", "Lesotho", "LSL", 2363325, 30360, "st,en"),
new IsoCountry("LT", "Lithuania", "Lithuania", "EUR", 2888774, 62600, "lt"),
new IsoCountry("LU", "Luxembourg", "Luxembourg", "EUR", 686970, 2574, "fr,de,lb"),
new IsoCountry("LV", "Latvia", "Latvia", "EUR", 1847785, 62230, "lv"),
new IsoCountry("LY", "Libya", "Libya", "LYD", 7458555, 1759540, "ar"),
new IsoCountry("MA", "Morocco", "Morocco", "MAD", 38430770, 446300, "ar"),
new IsoCountry("MC", "Monaco", "Monaco", "EUR", 38341, 2, "fr,oc"),
new IsoCountry("MD", "Republic of Moldova", "Moldova", "MDL", 2360527, 32930, "ro"),
new IsoCountry("ME", "Montenegro", "Montenegro", "EUR", 623129, 13450, ""),
new IsoCountry("MF", "Saint Martin (French part)", "Saint Martin", "EUR", 24941, 50, ""),
new IsoCountry("MG", "Madagascar", "Madagascar", "MGA", 32740678, 581800, "fr,mg"),
new IsoCountry("MH", "Marshall islands", "Marshall islands", "USD", 36282, 180, "en,mh"),
new IsoCountry("MK", "Republic of Macedonia", "North Macedonia", "MKD", 1820909, 25220, "mk,sq"),
new IsoCountry("ML", "Mali", "Mali", "XOF", 25198821, 1220190, "bm,ff"),
new IsoCountry("MM", "Union of Burma", "Burma", "MMK", 54850648, 652670, "my", "Myanmar"),
new IsoCountry("MN", "Mongolia", "Mongolia", "MNT", 3568978, 1558500, "mn"),
new IsoCountry("MO", "Macau", "Macao", "MOP", 685900, 33, ""),
new IsoCountry("MP", "Northern Mariana islands", "Northern Marianas islands", "USD", 43541, 460, "", "N Mariana islands,Northern Marianas"),
new IsoCountry("MQ", "Martinique", "Martinique", "EUR", 375265, 106, ""),
new IsoCountry("MR", "Mauritania", "Mauritania", "MRO", 5315065, 1030700, "ar,wo"),
new IsoCountry("MS", "Montserrat", "Montserrat", "XCD", 4992, 100, ""),
new IsoCountry("MT", "Malta", "Malta", "EUR", 579704, 320, "mt,en"),
new IsoCountry("MU", "Mauritius", "Mauritius", "MUR", 1243741, 1997, "en"),
new IsoCountry("MW", "Malawi", "Malawi", "MWK", 22216120, 94280, "en,ny"),
new IsoCountry("MV", "Maldives", "Maldives", "MVR", 529676, 298, "dv"),
new IsoCountry("MX", "Mexico", "Mexico", "MXN", 131946900, 1943950, "es"),
new IsoCountry("MY", "Malaysia", "Malaysia", "MYR", 35977838, 328550, "ms"),
new IsoCountry("MZ", "Mozambique", "Mozambique", "MZN", 35631653, 786380, "pt"),
new IsoCountry("NA", "Namibia", "Namibia", "NAD", 3092816, 823290, "en,hz,kj,ng"),
new IsoCountry("NC", "New Caledonia", "New Caledonia", "XPF", 295333, 18280, ""),
new IsoCountry("NE", "Niger", "Niger", "XOF", 27917831, 1266700, "fr,ha,kr"),
new IsoCountry("NF", "Norfolk island", "Norfolk island", "AUD", 0, 0, "en"),
new IsoCountry("NG", "Nigeria", "Nigeria", "NGN", 237527782, 910770, "en,ha,ig,kr,yo"),
new IsoCountry("NI", "Nicaragua", "Nicaragua", "NIO", 7007502, 120340, "es"),
new IsoCountry("NL", "Netherlands", "Netherlands", "EUR", 18087633, 33670, "nl,fy,li"),
new IsoCountry("NO", "Norway", "Norway", "NOK", 5610870, 364270, "no,nn,nb,se"),
new IsoCountry("NP", "Nepal", "Nepal", "NPR", 29618118, 143350, "ne,bo"),
new IsoCountry("NR", "Nauru", "Nauru", "AUD", 12025, 20, "en,na"),
new IsoCountry("NU", "Niue", "Niue", "NZD", 1626, 260, "en"),
new IsoCountry("NZ", "New Zealand", "New Zealand", "NZD", 5324700, 263310, "en,mi"),
new IsoCountry("OM", "Oman", "Oman", "OMR", 5494691, 309500, "ar"),
new IsoCountry("PA", "Panama", "Panama", "PAB", 4571189, 74180, "es"),
new IsoCountry("PE", "Peru", "Peru", "PEN", 34576665, 1280000, "es"),
new IsoCountry("PF", "French Polynesia", "French Polynesia", "XPF", 282465, 3471, "ty", "Fr Polynesia"),
new IsoCountry("PG", "Papua new Guinea", "Papua new Guinea", "PGK", 10762817, 452860, "en,ho"),
new IsoCountry("PH", "Philippines", "Philippines", "PHP", 116786962, 298170, "en,tl"),
new IsoCountry("PK", "Pakistan", "Pakistan", "PKR", 255219554, 770880, "ur,en,ks,pa,sd"),
new IsoCountry("PL", "Poland", "Poland", "PLN", 36435861, 306270, "pl"),
new IsoCountry("PM", "Saint Pierre and Miquelon", "Saint Pierre and Miquelon", "EUR", 5794, 230, ""),
new IsoCountry("PN", "Pitcairn islands", "Pitcairn", "NZD", 0, 0, ""),
new IsoCountry("PR", "Puerto Rico", "Puerto Rico", "USD", 3184835, 8870, ""),
new IsoCountry("PS", "State of Palestine", "Palestine", "ILS", 5413596, 6025, "ar"),
new IsoCountry("PT", "Portugal", "Portugal", "EUR", 10804871, 91606, "pt"),
new IsoCountry("PW", "Palau", "Palau", "USD", 17663, 460, "en"),
new IsoCountry("PY", "Paraguay", "Paraguay", "PYG", 7013078, 396012, "es"),
new IsoCountry("QA", "Qatar", "Qatar", "QAR", 2972215, 11490, "ar"),
new IsoCountry("RE", "Réunion", "Réunion", "EUR", 895312, 25, ""),
new IsoCountry("RO", "Romania", "Romania", "RON", 19020271, 230080, "ro"),
new IsoCountry("RS", "Serbia", "Serbia", "RSD", 6549143, 84090, "sr"),
new IsoCountry("RU", "Russian Federation", "Russia", "RUB", 143513328, 16376870, "ru,av,ba,ce,cv,kv,os,tt,yi"),
new IsoCountry("RW", "Rwanda", "Rwanda", "RWF", 14569341, 24670, "en,fr,rw,sw"),
new IsoCountry("SA", "Saudi Arabia", "Saudi Arabia", "SAR", 36973555, 2149690, "ar"),
new IsoCountry("SB", "Solomon islands", "Solomon islands", "SBD", 838645, 27990, "en"),
new IsoCountry("SC", "Seychelles", "Seychelles", "SCR", 122730, 460, "en,fr"),
new IsoCountry("SD", "Sudan", "Sudan", "SDG", 51662147, 1868000, "ar,en"),
new IsoCountry("SE", "Sweden", "Sweden", "SEK", 10596620, 407270, "sv,se"),
new IsoCountry("SG", "Singapore", "Singapore", "SGD", 6111175, 718, "en,ms,ta"),
new IsoCountry("SH", "Ascension and Tristan Da Cunha Saint Helena", "Saint Helena", "SHP", 6077, 390, ""),
new IsoCountry("SI", "Slovenia", "Slovenia", "EUR", 2130986, 20135, "sl,hu,it,hr"),
new IsoCountry("SJ", "Svalbard and Jan Mayen", "Svalbard and Jan Mayen", "NOK", 0, 0, ""),
new IsoCountry("SK", "Slovakia", "Slovakia", "EUR", 5413813, 48080, "sk"),
new IsoCountry("SL", "Sierra Leone", "Sierra Leone", "SLL", 8819794, 72180, "en"),
new IsoCountry("SM", "San Marino", "San Marino", "EUR", 34109, 60, "it"),
new IsoCountry("SN", "Senegal", "Senegal", "XOF", 18931966, 192530, "fr,wo"),
new IsoCountry("SO", "Somalia", "Somalia", "SOS", 19654739, 627340, "so"),
new IsoCountry("SR", "Suriname", "Suriname", "SRD", 639850, 160508, "nl"),
new IsoCountry("SS", "South Sudan", "South Sudan", "SDG", 12188788, 631930, "en", "S Sudan"),
new IsoCountry("ST", "São Tomé and Príncipe", "São Tomé and Príncipe", "STN", 240254, 960, "pt"),
new IsoCountry("SV", "El Salvador", "El Salvador", "SVC", 6365503, 20720, "es"),
new IsoCountry("SX", "Sint Maarten (Dutch part)", "Sint Maarten", "ANG", 43923, 34, ""),
new IsoCountry("SY", "Syrian Arab republic", "Syria", "SYP", 25620427, 183630, "ar"),
new IsoCountry("SZ", "Eswatini", "Swaziland", "SZL", 1256174, 17200, "en,ss"),
new IsoCountry("TC", "Turks and Caicos islands", "Turks and Caicos islands", "USD", 46855, 950, ""),
new IsoCountry("TD", "Chad", "Chad", "XAF", 21003705, 1259200, "ar,fr,kr"),
new IsoCountry("TF", "French southern territories", "French southern territories", "EUR", 0, 0, ""),
new IsoCountry("TG", "Togo", "Togo", "XOF", 8591626, 54390, "fr,ee,yo"),
new IsoCountry("TH", "Thailand", "Thailand", "THB", 71619863, 510890, "th"),
new IsoCountry("TJ", "Tajikistan", "Tajikistan", "TJS", 10786734, 138790, "tg,ug"),
new IsoCountry("TK", "Tokelau", "Tokelau", "NZD", 1357, 10, "en"),
new IsoCountry("TL", "Timor-Leste", "East Timor", "USD", 1418517, 14870, "pt"),
new IsoCountry("TM", "Turkmenistan", "Turkmenistan", "TMT", 7618847, 469930, "tk"),
new IsoCountry("TN", "Tunisia", "Tunisia", "TND", 12348573, 155360, "ar"),
new IsoCountry("TO", "Tonga", "Tonga", "TOP", 103742, 720, "en,to"),
new IsoCountry("TR", "Turkey", "Turkey", "TRY", 85878556, 769630, "tr"),
new IsoCountry("TT", "Trinidad and Tobago", "Trinidad and Tobago", "TTD", 1367764, 5130, "en"),
new IsoCountry("TW", "Province of China Taiwan", "Taiwan", "TWD", 23816775, 3541, "zh"),
new IsoCountry("TV", "Tuvalu", "Tuvalu", "AUD", 9492, 30, "en"),
new IsoCountry("TZ", "United republic of Tanzania", "Tanzania", "TZS", 70545865, 885800, "sw,en"),
new IsoCountry("UA", "Ukraine", "Ukraine", "UAH", 38980376, 579400, "uk"),
new IsoCountry("UG", "Uganda", "Uganda", "UGX", 51384894, 200520, "en,sw,lg"),
new IsoCountry("UM", "United states minor outlying islands", "United states minor outlying islands", "USD", 0, 0, "en", "US Minor Outlying Islands"),
new IsoCountry("US", "United states of America", "United states", "USD", 341784857, 9147420, "en,ik,nv"),
new IsoCountry("UY", "Uruguay", "Uruguay", "UYU", 3384688, 175020, "es"),
new IsoCountry("UZ", "Uzbekistan", "Uzbekistan", "UZS", 37053428, 440650, "uz"),
new IsoCountry("VA", "Holy See (Vatican City state)", "Vatican City", "EUR", 801, 0, "it,la", "Holy See,Vatican"),
new IsoCountry("VC", "Saint Vincent and the Grenadines", "Saint Vincent and the Grenadines", "XCD", 99924, 390, "en", "Saint Vin and Gren"),
new IsoCountry("VE", "Bolivarian republic of Venezuela", "Venezuela", "VEF", 28516896, 882050, "es"),
new IsoCountry("WF", "Wallis and Futuna", "Wallis and Futuna", "XPF", 11239, 140, "", "Wallis and Futuna islands"),
new IsoCountry("VG", "British Virgin islands", "British Virgin islands", "USD", 39732, 150, ""),
new IsoCountry("VI", "United States Virgin islands", "U.S. Virgin islands", "USD", 103792, 350, ""),
new IsoCountry("VN", "The socialist republic of VietNam", "Vietnam", "VND", 101598527, 313429, "vi"),
new IsoCountry("WS", "Samoa", "Samoa", "WST", 219306, 2780, "en,sm"),
new IsoCountry("VU", "Vanuatu", "Vanuatu", "VUV", 335169, 12190, "en,fr,bi"),
new IsoCountry("YE", "Yemen", "Yemen", "YER", 41773878, 527970, "ar"),
new IsoCountry("YT", "Mayotte", "Mayotte", "EUR", 272815, 375, ""),
new IsoCountry("XK", "Republic of Kosovo", "Kosovo", "RSD", 1576876, 10887, "sq,sr"),
new IsoCountry("ZA", "South Africa", "South Africa", "ZAR", 64747319, 1213090, "af,en,nr,st,ss,ts,tn,ve,xh,zu"),
new IsoCountry("ZM", "Zambia", "Zambia", "ZMW", 21913874, 743390, "en"),
new IsoCountry("ZW", "Zimbabwe", "Zimbabwe", "ZWD", 16950795, 386850, "ny,en,nd,ts,sn,st,tn,ve,xh"),
        };


    }

}
