using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver.IsoData
{

    [TableDataPrimaryKey(nameof(CommonName))]
    public sealed class IsoCountry
    {
        /// <summary>
        /// Flag
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
        /// ISO 4217 currency code of the most common currency used in the country
        /// </summary>
        [TableDataIsoCurrency]
        public readonly String Currency;

        /// <summary>
        /// The population estimate (2020) of the country, a zero means no information
        /// </summary>
        public readonly long Population;

        /// <summary>
        /// The land area estimate in km² (2020) of the country, a zero means no information
        /// </summary>
        [TableDataNumber(0, "{0} km²")]
        public readonly int LandArea;

        /// <summary>
        /// Population density in people per km² (2020) of the country, a zero means no information
        /// </summary>
        [TableDataNumber(1, "{0} p/km²")]
        [TableDataOrder(1)]
        public decimal PopDense => LandArea <= 0 ? 0M : ((Decimal)Population / (Decimal)LandArea);

        /// <summary>
        /// The languages spoken in the country as a comma seprated list of ISO 639-1 language codes.
        /// </summary>
        [TableDataOrder(2)]
        public readonly String Languages;

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

        public readonly String Nicks;
        
        /// <summary>
        /// Get information about a country from a two letter ISO 3166-A2 country code.
        /// Ex:
        ///   "GB" => "UNITED KINGDOM"
        ///   "SE" => "SWEDEN"
        /// </summary>
        /// <param name="iso3166a2">A two letter ISO 3166-A2 country code</param>
        /// <returns>Information about the country if it's known, or null if it's unknown</returns>
        public static IsoCountry TryGet(String iso3166a2) => IsoToInfo.TryGetValue(iso3166a2?.FastToLower() ?? "", out var i) ? i : null;

        /// <summary>
        /// Get information about a country from a country name, iso code and so on
        /// Ex:
        ///   "UNITED KINGDOM" => "UK"
        ///   "SWEDEN" => "SE"
        /// </summary>
        /// <param name="name">The name of the country</param>
        /// <returns>Information about the country if it's known, or null if it's unknown</returns>
        public static IsoCountry TryGetName(String name)
        {
            var ni = NameToInfo;
            name = name?.FastToLower() ?? "";
            if (ni.TryGetValue(FixName(name), out var i))
                return i;
            return null;
        }


        public static String FixName(String name)
        {
            name = name.RemoveDiacritics();
            name = name.Replace('-', ' ');
            name = name.Replace(".", "");
            return name;
        }


        /// <summary>
        /// Enumerates all aliases for a country
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
        /// All known countries
        /// </summary>
        public static readonly IReadOnlyList<IsoCountry> Countries = new IsoCountry[]
        {
new IsoCountry("AD", "Andorra", "Andorra", "EUR", 77265, 470, "ca"),
new IsoCountry("AE", "United Arab Emirates", "United Arab Emirates", "AED", 9890402, 836, "ar", "UAE"),
new IsoCountry("AF", "Afghanistan", "Afghanistan", "AFN", 38928346, 65286, "fa,ps"),
new IsoCountry("AG", "Antigua and Barbuda", "Antigua and Barbuda", "XCD", 97929, 440, "en", "Antigua and Barb."),
new IsoCountry("AI", "Anguilla", "Anguilla", "XCD", 15003, 90, ""),
new IsoCountry("AL", "Albania", "Albania", "ALL", 2877797, 274, "sq"),
new IsoCountry("AM", "Armenia", "Armenia", "AMD", 2963243, 2847, "hy"),
new IsoCountry("AO", "Angola", "Angola", "AOA", 32866272, 1246700, "pt,hz,kg,kj,ng"),
new IsoCountry("AQ", "Antarctica", "Antarctica", null, 0, 0, "", "Fr S Antarctic Lands"),
new IsoCountry("AR", "Argentina", "Argentina", "ARS", 45195774, 2736690, "es,cy"),
new IsoCountry("AS", "American Samoa", "American Samoa", "USD", 55191, 200, ""),
new IsoCountry("AT", "Austria", "Austria", "EUR", 9006398, 82409, "de,sl"),
new IsoCountry("AU", "Australia", "Australia", "AUD", 25499884, 7682300, "en", "Ashmore and Cartier Is"),
new IsoCountry("AW", "Aruba", "Aruba", "AWG", 106766, 180, ""),
new IsoCountry("AX", "Åland islands", "Åland islands", "EUR", 0, 0, ""),
new IsoCountry("AZ", "Azerbaijan", "Azerbaijan", "AZN", 10139177, 82658, "az"),
new IsoCountry("BA", "Bosnia and Herzegovina", "Bosnia and Herzegovina", "BAM", 3280819, 51, "bs", "Bosnia and Herz"),
new IsoCountry("BB", "Barbados", "Barbados", "BBD", 287375, 430, "en"),
new IsoCountry("BD", "Bangladesh", "Bangladesh", "BDT", 164689383, 13017, "bn"),
new IsoCountry("BE", "Belgium", "Belgium", "EUR", 11589623, 3028, "nl,fr,de,li,wa"),
new IsoCountry("BF", "Burkina Faso", "Burkina Faso", "XOF", 20903273, 2736, "fr"),
new IsoCountry("BG", "Bulgaria", "Bulgaria", "BGN", 6948445, 10856, "bg"),
new IsoCountry("BH", "Bahrain", "Bahrain", "BHD", 1701575, 760, "ar"),
new IsoCountry("BI", "Burundi", "Burundi", "BIF", 11890784, 2568, "fr,rn,en"),
new IsoCountry("BJ", "Benin", "Benin", "XOF", 12123200, 11276, "fr,ee,yo"),
new IsoCountry("BL", "Saint Barthélemy", "Saint Barthélemy", "EUR", 9877, 21, "", "St-Barthélemy"),
new IsoCountry("BM", "Bermuda", "Bermuda", "BMD", 62278, 50, ""),
new IsoCountry("BN", "Brunei Darussalam", "Brunei", "BND", 437479, 527, "ms"),
new IsoCountry("BO", "Plurinational state of Bolivia", "Bolivia", "BOB", 11673021, 1083300, "es,ay,gn,qu"),
new IsoCountry("BQ", "Sint Eustatius and Saba Bonaire", "Saba", "USD", 0, 0, "", "Saint Eustatius"),
new IsoCountry("BR", "Brazil", "Brazil", "BRL", 212559417, 8358140, "pt", "Brazilian I"),
new IsoCountry("BS", "The Bahamas", "Bahamas", "BSD", 393244, 1001, "en"),
new IsoCountry("BT", "Bhutan", "Bhutan", "BTN", 771608, 38117, "dz"),
new IsoCountry("BW", "Botswana", "Botswana", "BWP", 2351627, 56673, "en,hz"),
new IsoCountry("BV", "Bouvet island", "Bouvet island", "NOK", 0, 0, ""),
new IsoCountry("BY", "Belarus", "Belarus", "BYN", 9449323, 20291, "be,ru"),
new IsoCountry("BZ", "Belize", "Belize", "BZD", 397628, 2281, "en"),
new IsoCountry("CA", "Canada", "Canada", "CAD", 37742154, 9093510, "en,fr,cr,gd,iu,ik,oj"),
new IsoCountry("CC", "Cocos (Keeling) islands", "Cocos islands", "AUD", 0, 0, "en"),
new IsoCountry("CD", "Democratic republic of the Congo", "DR Congo", "CDF", 89561403, 2267050, "fr,kg,ln,lu", "Dem Rep Congo"),
new IsoCountry("CF", "Central African republic", "Central African republic", "XAF", 4829767, 62298, "fr,sg", "Central African Rep"),
new IsoCountry("CG", "Republic of the Congo", "Congo", "XAF", 5518087, 3415, "fr,kg,ln", "Republic of Congo"),
new IsoCountry("CH", "Switzerland", "Switzerland", "CHE", 8654622, 39516, "rm"),
new IsoCountry("CI", "Côte d'Ivoire", "Ivory coast", "XOF", 26378274, 318, "fr", "Cote DIvoire"),
new IsoCountry("CK", "Cook islands", "Cook islands", "NZD", 17564, 240, "en", "Cook Is."),
new IsoCountry("CL", "Chile", "Chile", "CLP", 19116201, 743532, "es"),
new IsoCountry("CM", "Cameroon", "Cameroon", "XAF", 26545863, 47271, "en,fr,kr"),
new IsoCountry("CN", "China", "China", "CNY", 1439323776, 9388211, "zh,ii,bo,ug,za"),
new IsoCountry("CO", "Colombia", "Colombia", "COP", 50882891, 1109500, "es"),
new IsoCountry("CR", "Costa Rica", "Costa Rica", "CRC", 5094118, 5106, "es"),
new IsoCountry("CU", "Cuba", "Cuba", "CUP", 11326616, 10644, "es"),
new IsoCountry("CV", "Cabo Verde", "Cape Verde", "CVE", 555987, 403, "pt"),
new IsoCountry("CW", "Curaçao", "Curaçao", "ANG", 164093, 444, ""),
new IsoCountry("CX", "Christmas island", "Christmas island", "AUD", 0, 0, "en,ms"),
new IsoCountry("CY", "Cyprus", "Cyprus", "EUR", 1207359, 924, "el,tr"),
new IsoCountry("CZ", "Czech republic", "Czechia", "CZK", 10708981, 7724, "cs,sk"),
new IsoCountry("DE", "Germany", "Germany", "EUR", 83783942, 34856, "de,li"),
new IsoCountry("DJ", "Djibouti", "Djibouti", "DJF", 988, 2318, "ar,fr"),
new IsoCountry("DK", "Denmark", "Denmark", "DKK", 5792202, 4243, "da,fo,kl"),
new IsoCountry("DM", "Dominica", "Dominica", "XCD", 71986, 750, "en"),
new IsoCountry("DO", "Dominican republic", "Dominican republic", "DOP", 10847910, 4832, "es"),
new IsoCountry("DZ", "Algeria", "Algeria", "DZD", 43851044, 2381740, "ar"),
new IsoCountry("EC", "Ecuador", "Ecuador", "USD", 17643054, 24836, "es"),
new IsoCountry("EE", "Estonia", "Estonia", "EUR", 1326535, 4239, "et"),
new IsoCountry("EG", "Egypt", "Egypt", "EGP", 102334404, 99545, "ar"),
new IsoCountry("EH", "Western Sahara", "Western Sahara", "MAD", 597339, 266, "", "W Sahara"),
new IsoCountry("ER", "Eritrea", "Eritrea", "ERN", 3546421, 101, "ti"),
new IsoCountry("ES", "Spain", "Spain", "EUR", 46754778, 4988, "es,an,eu,gl,oc", "Canary islands"),
new IsoCountry("ET", "Ethiopia", "Ethiopia", "ETB", 114963588, 1000000, "aa,am,om,so,ti"),
new IsoCountry("FI", "Finland", "Finland", "EUR", 5540720, 30389, "fi,sv,se"),
new IsoCountry("FJ", "Fiji", "Fiji", "FJD", 896445, 1827, "en,fj"),
new IsoCountry("FK", "Falkland islands (Malvinas)", "Falkland islands", "FKP", 348, 1217, ""),
new IsoCountry("FM", "Federated states of Micronesia", "Micronesia", "USD", 115023, 700, "en"),
new IsoCountry("FO", "Faroe islands", "Faroe islands", "DKK", 48863, 1396, "", "Faeroe islands"),
new IsoCountry("FR", "France", "France", "EUR", 65273511, 547557, "fr,br,co,oc,wa"),
new IsoCountry("GA", "Gabon", "Gabon", "XAF", 2225734, 25767, "fr,kg"),
new IsoCountry("GB", "United Kingdom of Great Britain and Northern Ireland", "United kingdom", "GBP", 67886011, 24193, "en,kw,gd,cy", "UK,England,Falkland islands"),
new IsoCountry("GD", "Grenada", "Grenada", "XCD", 112523, 340, "en"),
new IsoCountry("GE", "Georgia", "Georgia", "GEL", 3989167, 6949, "ka,ab,os"),
new IsoCountry("GF", "French Guiana", "French Guiana", "EUR", 298682, 822, ""),
new IsoCountry("GG", "Guernsey", "Guernsey", "GBP", 0, 0, ""),
new IsoCountry("GH", "Ghana", "Ghana", "GHS", 31072940, 22754, "en,ak,ee,tw"),
new IsoCountry("GI", "Gibraltar", "Gibraltar", "GIP", 33691, 10, ""),
new IsoCountry("GL", "Greenland", "Greenland", "DKK", 5677, 41045, ""),
new IsoCountry("GM", "The Gambia", "Gambia", "GMD", 2416668, 1012, "en,wo"),
new IsoCountry("GN", "Guinea", "Guinea", "GNF", 13132795, 24572, "fr"),
new IsoCountry("GP", "Guadeloupe", "Guadeloupe", "EUR", 400124, 169, ""),
new IsoCountry("GQ", "Equatorial Guinea", "Equatorial Guinea", "XAF", 1402985, 2805, "fr,pt,es", "Eq Guinea"),
new IsoCountry("GR", "Greece", "Greece", "EUR", 10423054, 1289, "el"),
new IsoCountry("GS", "South Georgia and the south Sandwich islands", "South Georgia and the south Sandwich islands", "GEL", 0, 0, "", "S Geo and the islands"),
new IsoCountry("GT", "Guatemala", "Guatemala", "GTQ", 17915568, 10716, "es"),
new IsoCountry("GU", "Guam", "Guam", "USD", 168775, 540, "ch"),
new IsoCountry("GW", "Guinea-Bissau", "Guinea-Bissau", "XOF", 1968001, 2812, "pt"),
new IsoCountry("GY", "Guyana", "Guyana", "GYD", 786552, 19685, "en"),
new IsoCountry("HK", "Hong Kong", "Hong Kong", "HKD", 7496981, 105, ""),
new IsoCountry("HM", "Heard island and McDonald islands", "Heard island and McDonald islands", "AUD", 0, 0, "", "Heard I and McDonald Islands"),
new IsoCountry("HN", "Honduras", "Honduras", "HNL", 9904607, 11189, "es"),
new IsoCountry("HR", "Croatia", "Croatia", "HRK", 4105267, 5596, "hr"),
new IsoCountry("HT", "Haiti", "Haiti", "HTG", 11402528, 2756, "fr,ht"),
new IsoCountry("HU", "Hungary", "Hungary", "HUF", 9660351, 9053, "hu,sl"),
new IsoCountry("ID", "Indonesia", "Indonesia", "IDR", 273523615, 1811570, "id,jv,su"),
new IsoCountry("IE", "Ireland", "Ireland", "EUR", 4937786, 6889, "ga,en"),
new IsoCountry("IL", "Israel", "Israel", "ILS", 8655535, 2164, "he,yi"),
new IsoCountry("IM", "Isle of Man", "Isle of Man", "GBP", 85033, 570, "gv,en"),
new IsoCountry("IN", "India", "India", "INR", 1380004385, 2973190, "hi,en,as,gu,kn,ks,ml,mr,or,pa,sa,sd,te"),
new IsoCountry("IO", "British Indian ocean territory", "British Indian ocean territory", "USD", 0, 0, "", "Indian Ocean Ter,Br Indian Ocean Ter"),
new IsoCountry("IQ", "Iraq", "Iraq", "IQD", 40222493, 43432, "ar,ku"),
new IsoCountry("IR", "Islamic republic of Iran", "Iran", "IRR", 83992949, 1628550, "fa"),
new IsoCountry("IS", "Iceland", "Iceland", "ISK", 341243, 10025, "is"),
new IsoCountry("IT", "Italy", "Italy", "EUR", 60461826, 29414, "it,oc,sc,sl"),
new IsoCountry("JE", "Jersey", "Jersey", "GBP", 0, 0, ""),
new IsoCountry("JM", "Jamaica", "Jamaica", "JMD", 2961167, 1083, "en"),
new IsoCountry("JO", "Jordan", "Jordan", "JOD", 10203134, 8878, "ar"),
new IsoCountry("JP", "Japan", "Japan", "JPY", 126476461, 364555, "ja"),
new IsoCountry("KE", "Kenya", "Kenya", "KES", 53771296, 56914, "en,sw,ki"),
new IsoCountry("KG", "Kyrgyzstan", "Kyrgyzstan", "KGS", 6524195, 1918, "ky,ru,ug"),
new IsoCountry("KH", "Cambodia", "Cambodia", "KHR", 16718965, 17652, "km"),
new IsoCountry("KI", "Kiribati", "Kiribati", "AUD", 119449, 810, "en"),
new IsoCountry("KM", "Comoros", "Comoros", "KMF", 869601, 1861, "ar,fr"),
new IsoCountry("KN", "Saint Kitts and Nevis", "Saint Kitts and Nevis", "XCD", 53199, 260, "en"),
new IsoCountry("KP", "Democratic people's republic of Korea", "North Korea", "KPW", 51269185, 9723, "ko", "Dem. Rep. Korea"),
new IsoCountry("KR", "Republic of Korea", "South Korea", "KRW", 25778816, 12041, "ko"),
new IsoCountry("KW", "Kuwait", "Kuwait", "KWD", 4270571, 1782, "ar"),
new IsoCountry("KY", "Cayman islands", "Cayman islands", "KYD", 65722, 240, ""),
new IsoCountry("KZ", "Kazakhstan", "Kazakhstan", "KZT", 18776707, 2699700, "kk,ru,ug"),
new IsoCountry("LA", "Lao people's Democratic republic", "Laos", "LAK", 7275560, 2308, "lo", "Lao PDR"),
new IsoCountry("LB", "Lebanon", "Lebanon", "LBP", 6825445, 1023, "ar"),
new IsoCountry("LC", "Saint Lucia", "Saint Lucia", "XCD", 183627, 610, "en"),
new IsoCountry("LI", "Liechtenstein", "Liechtenstein", "CHF", 38128, 160, "de"),
new IsoCountry("LK", "Sri Lanka", "Sri Lanka", "LKR", 21413249, 6271, "si,ta"),
new IsoCountry("LR", "Liberia", "Liberia", "LRD", 5057681, 9632, "en"),
new IsoCountry("LS", "Lesotho", "Lesotho", "LSL", 2142249, 3036, "st,en"),
new IsoCountry("LT", "Lithuania", "Lithuania", "EUR", 2722289, 62674, "lt"),
new IsoCountry("LU", "Luxembourg", "Luxembourg", "EUR", 625978, 259, "fr,de,lb"),
new IsoCountry("LV", "Latvia", "Latvia", "EUR", 1886198, 622, "lv"),
new IsoCountry("LY", "Libya", "Libya", "LYD", 6871292, 1759540, "ar"),
new IsoCountry("MA", "Morocco", "Morocco", "MAD", 36910560, 4463, "ar"),
new IsoCountry("MC", "Monaco", "Monaco", "EUR", 39242, 1, "fr,oc"),
new IsoCountry("MD", "Republic of Moldova", "Moldova", "MDL", 4033963, 3285, "ro"),
new IsoCountry("ME", "Montenegro", "Montenegro", "EUR", 628066, 1345, ""),
new IsoCountry("MF", "Saint Martin (French part)", "Saint Martin", "EUR", 38666, 53, ""),
new IsoCountry("MG", "Madagascar", "Madagascar", "MGA", 27691018, 581795, "fr,mg"),
new IsoCountry("MH", "Marshall islands", "Marshall islands", "USD", 5919, 180, "en,mh"),
new IsoCountry("MK", "Republic of Macedonia", "North Macedonia", "MKD", 2083374, 2522, "mk,sq"),
new IsoCountry("ML", "Mali", "Mali", "XOF", 20250833, 1220190, "bm,ff"),
new IsoCountry("MM", "Union of Burma", "Burma", "MMK", 54409800, 65329, "my", "Myanmar"),
new IsoCountry("MN", "Mongolia", "Mongolia", "MNT", 3278290, 1553560, "mn"),
new IsoCountry("MO", "Macau", "Macao", "MOP", 649335, 30, ""),
new IsoCountry("MP", "Northern Mariana islands", "Northern Marianas islands", "USD", 57559, 460, "", "N Mariana islands,Northern Marianas"),
new IsoCountry("MQ", "Martinique", "Martinique", "EUR", 375265, 106, ""),
new IsoCountry("MR", "Mauritania", "Mauritania", "MRO", 4649658, 1030700, "ar,wo"),
new IsoCountry("MS", "Montserrat", "Montserrat", "XCD", 4992, 100, ""),
new IsoCountry("MT", "Malta", "Malta", "EUR", 441543, 320, "mt,en"),
new IsoCountry("MU", "Mauritius", "Mauritius", "MUR", 1271768, 203, "en"),
new IsoCountry("MW", "Malawi", "Malawi", "MWK", 19129952, 9428, "en,ny"),
new IsoCountry("MV", "Maldives", "Maldives", "MVR", 540544, 300, "dv"),
new IsoCountry("MX", "Mexico", "Mexico", "MXN", 128932753, 1943950, "es"),
new IsoCountry("MY", "Malaysia", "Malaysia", "MYR", 32365999, 32855, "ms"),
new IsoCountry("MZ", "Mozambique", "Mozambique", "MZN", 31255435, 78638, "pt"),
new IsoCountry("NA", "Namibia", "Namibia", "NAD", 2540905, 82329, "en,hz,kj,ng"),
new IsoCountry("NC", "New Caledonia", "New Caledonia", "XPF", 285498, 1828, ""),
new IsoCountry("NE", "Niger", "Niger", "XOF", 24206644, 1266700, "fr,ha,kr"),
new IsoCountry("NF", "Norfolk island", "Norfolk island", "AUD", 0, 0, "en"),
new IsoCountry("NG", "Nigeria", "Nigeria", "NGN", 206139589, 91077, "en,ha,ig,kr,yo"),
new IsoCountry("NI", "Nicaragua", "Nicaragua", "NIO", 6624554, 12034, "es"),
new IsoCountry("NL", "Netherlands", "Netherlands", "EUR", 17134872, 3372, "nl,fy,li"),
new IsoCountry("NO", "Norway", "Norway", "NOK", 5421241, 365268, "no,nn,nb,se"),
new IsoCountry("NP", "Nepal", "Nepal", "NPR", 29136808, 14335, "ne,bo"),
new IsoCountry("NR", "Nauru", "Nauru", "AUD", 10824, 20, "en,na"),
new IsoCountry("NU", "Niue", "Niue", "NZD", 1626, 260, "en"),
new IsoCountry("NZ", "New Zealand", "New Zealand", "NZD", 4822233, 26331, "en,mi"),
new IsoCountry("OM", "Oman", "Oman", "OMR", 5106626, 3095, "ar"),
new IsoCountry("PA", "Panama", "Panama", "PAB", 4314767, 7434, "es"),
new IsoCountry("PE", "Peru", "Peru", "PEN", 32971854, 1280000, "es"),
new IsoCountry("PF", "French Polynesia", "French Polynesia", "XPF", 280908, 366, "ty", "Fr Polynesia"),
new IsoCountry("PG", "Papua new Guinea", "Papua new Guinea", "PGK", 8947024, 45286, "en,ho"),
new IsoCountry("PH", "Philippines", "Philippines", "PHP", 109581078, 29817, "en,tl"),
new IsoCountry("PK", "Pakistan", "Pakistan", "PKR", 220892340, 77088, "ur,en,ks,pa,sd"),
new IsoCountry("PL", "Poland", "Poland", "PLN", 37846611, 30623, "pl"),
new IsoCountry("PM", "Saint Pierre and Miquelon", "Saint Pierre and Miquelon", "EUR", 5794, 230, ""),
new IsoCountry("PN", "Pitcairn islands", "Pitcairn", "NZD", 0, 0, ""),
new IsoCountry("PR", "Puerto Rico", "Puerto Rico", "USD", 2860853, 887, ""),
new IsoCountry("PS", "State of Palestine", "Palestine", "ILS", 5101414, 602, "ar"),
new IsoCountry("PT", "Portugal", "Portugal", "EUR", 10196709, 9159, "pt"),
new IsoCountry("PW", "Palau", "Palau", "USD", 18094, 460, "en"),
new IsoCountry("PY", "Paraguay", "Paraguay", "PYG", 7132538, 3973, "es"),
new IsoCountry("QA", "Qatar", "Qatar", "QAR", 2881053, 1161, "ar"),
new IsoCountry("RE", "Réunion", "Réunion", "EUR", 895312, 25, ""),
new IsoCountry("RO", "Romania", "Romania", "RON", 19237691, 23017, "ro"),
new IsoCountry("RS", "Serbia", "Serbia", "RSD", 8737371, 8746, "sr"),
new IsoCountry("RU", "Russian Federation", "Russia", "RUB", 145934462, 16376870, "ru,av,ba,ce,cv,kv,os,tt,yi"),
new IsoCountry("RW", "Rwanda", "Rwanda", "RWF", 12952218, 2467, "en,fr,rw,sw"),
new IsoCountry("SA", "Saudi Arabia", "Saudi Arabia", "SAR", 34813871, 2149690, "ar"),
new IsoCountry("SB", "Solomon islands", "Solomon islands", "SBD", 686884, 2799, "en"),
new IsoCountry("SC", "Seychelles", "Seychelles", "SCR", 98347, 460, "en,fr"),
new IsoCountry("SD", "Sudan", "Sudan", "SDG", 43849260, 1765048, "ar,en"),
new IsoCountry("SE", "Sweden", "Sweden", "SEK", 10099265, 41034, "sv,se"),
new IsoCountry("SG", "Singapore", "Singapore", "SGD", 5850342, 700, "en,ms,ta"),
new IsoCountry("SH", "Ascension and Tristan Da Cunha Saint Helena", "Saint Helena", "SHP", 6077, 390, ""),
new IsoCountry("SI", "Slovenia", "Slovenia", "EUR", 2078938, 2014, "sl,hu,it,hr"),
new IsoCountry("SJ", "Svalbard and Jan Mayen", "Svalbard and Jan Mayen", "NOK", 0, 0, ""),
new IsoCountry("SK", "Slovakia", "Slovakia", "EUR", 5459642, 48088, "sk"),
new IsoCountry("SL", "Sierra Leone", "Sierra Leone", "SLL", 7976983, 7218, "en"),
new IsoCountry("SM", "San Marino", "San Marino", "EUR", 33931, 60, "it"),
new IsoCountry("SN", "Senegal", "Senegal", "XOF", 16743927, 19253, "fr,wo"),
new IsoCountry("SO", "Somalia", "Somalia", "SOS", 15893222, 62734, "so"),
new IsoCountry("SR", "Suriname", "Suriname", "SRD", 586632, 156, "nl"),
new IsoCountry("SS", "South Sudan", "South Sudan", "SDG", 11193725, 610952, "en", "S Sudan"),
new IsoCountry("ST", "São Tomé and Príncipe", "São Tomé and Príncipe", "STN", 219159, 960, "pt"),
new IsoCountry("SV", "El Salvador", "El Salvador", "SVC", 6486205, 2072, "es"),
new IsoCountry("SX", "Sint Maarten (Dutch part)", "Sint Maarten", "ANG", 42876, 34, ""),
new IsoCountry("SY", "Syrian Arab republic", "Syria", "SYP", 17500658, 18363, "ar"),
new IsoCountry("SZ", "Eswatini", "Swaziland", "SZL", 1160164, 172, "en,ss"),
new IsoCountry("TC", "Turks and Caicos islands", "Turks and Caicos islands", "USD", 38717, 950, ""),
new IsoCountry("TD", "Chad", "Chad", "XAF", 16425864, 1259200, "ar,fr,kr"),
new IsoCountry("TF", "French southern territories", "French southern territories", "EUR", 0, 0, ""),
new IsoCountry("TG", "Togo", "Togo", "XOF", 8278724, 5439, "fr,ee,yo"),
new IsoCountry("TH", "Thailand", "Thailand", "THB", 69799978, 51089, "th"),
new IsoCountry("TJ", "Tajikistan", "Tajikistan", "TJS", 9537645, 13996, "tg,ug"),
new IsoCountry("TK", "Tokelau", "Tokelau", "NZD", 1357, 10, "en"),
new IsoCountry("TL", "Timor-Leste", "East Timor", "USD", 1318445, 1487, "pt"),
new IsoCountry("TM", "Turkmenistan", "Turkmenistan", "TMT", 6031200, 46993, "tk"),
new IsoCountry("TN", "Tunisia", "Tunisia", "TND", 11818619, 15536, "ar"),
new IsoCountry("TO", "Tonga", "Tonga", "TOP", 105695, 720, "en,to"),
new IsoCountry("TR", "Turkey", "Turkey", "TRY", 84339067, 76963, "tr"),
new IsoCountry("TT", "Trinidad and Tobago", "Trinidad and Tobago", "TTD", 1399488, 513, "en"),
new IsoCountry("TW", "Province of China Taiwan", "Taiwan", "TWD", 23816775, 3541, "zh"),
new IsoCountry("TV", "Tuvalu", "Tuvalu", "AUD", 11792, 30, "en"),
new IsoCountry("TZ", "United republic of Tanzania", "Tanzania", "TZS", 59734218, 8858, "sw,en"),
new IsoCountry("UA", "Ukraine", "Ukraine", "UAH", 43733762, 57932, "uk"),
new IsoCountry("UG", "Uganda", "Uganda", "UGX", 45741007, 19981, "en,sw,lg"),
new IsoCountry("UM", "United states minor outlying islands", "United states minor outlying islands", "USD", 0, 0, "en", "US Minor Outlying Islands"),
new IsoCountry("US", "United states of America", "United states", "USD", 331002651, 9147420, "en,ik,nv"),
new IsoCountry("UY", "Uruguay", "Uruguay", "UYU", 3473730, 17502, "es"),
new IsoCountry("UZ", "Uzbekistan", "Uzbekistan", "UZS", 33469203, 4254, "uz"),
new IsoCountry("VA", "Holy See (Vatican City state)", "Vatican City", "EUR", 801, 0, "it,la", "Holy See,Vatican"),
new IsoCountry("VC", "Saint Vincent and the Grenadines", "Saint Vincent and the Grenadines", "XCD", 11094, 390, "en", "Saint Vin and Gren"),
new IsoCountry("VE", "Bolivarian republic of Venezuela", "Venezuela", "VEF", 28435940, 88205, "es"),
new IsoCountry("WF", "Wallis and Futuna", "Wallis and Futuna", "XPF", 11239, 140, "", "Wallis and Futuna islands"),
new IsoCountry("VG", "British Virgin islands", "British Virgin islands", "USD", 30231, 150, ""),
new IsoCountry("VI", "United States Virgin islands", "U.S. Virgin islands", "USD", 104425, 350, ""),
new IsoCountry("VN", "The socialist republic of VietNam", "Vietnam", "VND", 97338579, 31007, "vi"),
new IsoCountry("WS", "Samoa", "Samoa", "WST", 198414, 283, "en,sm"),
new IsoCountry("VU", "Vanuatu", "Vanuatu", "VUV", 307145, 1219, "en,fr,bi"),
new IsoCountry("YE", "Yemen", "Yemen", "YER", 29825964, 52797, "ar"),
new IsoCountry("YT", "Mayotte", "Mayotte", "EUR", 272815, 375, ""),
new IsoCountry("XK", "Republic of Kosovo", "Kosovo", "RSD", 1761985, 10887, "sq,sr"),
new IsoCountry("ZA", "South Africa", "South Africa", "ZAR", 59308690, 1213090, "af,en,nr,st,ss,ts,tn,ve,xh,zu"),
new IsoCountry("ZM", "Zambia", "Zambia", "ZMW", 18383955, 74339, "en"),
new IsoCountry("ZW", "Zimbabwe", "Zimbabwe", "ZWD", 14862924, 38685, "ny,en,nd,ts,sn,st,tn,ve,xh"),
        };


    }

}
