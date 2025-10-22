using System;
using System.Linq;
using System.Collections.Generic;
using SysWeaver.Data;


namespace SysWeaver.IsoData
{

    /// <summary>
    /// Contains information about a currency
    /// </summary>
    [TableDataPrimaryKey(nameof(Iso4217))]
    public sealed class IsoCurrency
    {
        /// <summary>
        /// The ISO 4217 currency code of the currency
        /// </summary>
        [TableDataIsoCurrency]
        [TableDataOrder(-3)]
        public readonly String Iso4217;

        /// <summary>
        /// The "biggest" country that is using this currency as ISO 3166 Alpha 2 country code, can be null or empty for special currencies
        /// </summary>
        [TableDataIsoCountry]
        [TableDataOrder(-2)]
        public readonly String Country;

        /// <summary>
        /// The flag of the "biggest" country that is using this currency.
        /// </summary>
        [TableDataIsoCountryImage]
        [TableDataOrder(-1)]
        public String Flag => Country;

        /// <summary>
        /// The ISO 4217 number of the currency
        /// </summary>
        public readonly int Number;
        
        /// <summary>
        /// The symbol of this currency (or null)
        /// </summary>
        public readonly String Symbol;
        
        /// <summary>
        /// The number of subdivisions for under units for this currency, ex: USD = 100, MGA = 2, CLF = 10000
        /// </summary>
        public readonly int UnderUnits;
        
        /// <summary>
        /// The number of decimal characters (string length) for this currency
        /// </summary>
        public readonly int DecimalNumbers;
        
        /// <summary>
        /// The decimal digits for under units for this currency, ex: USD = 2, MGA = 0.5, CLF = 4
        /// </summary>
        public readonly Double DecimalDigits;
        
        /// <summary>
        /// Symbol prefix to use when building a string, ex: AmountString = SymbolPrefix + ValueString + SymbolSuffix
        /// Will use the ISO 4217 currency code if no symbol is defined
        /// </summary>
        public readonly String SymbolPrefix;
        
        /// <summary>
        /// Symbol suffix to use when building a string, ex: AmountString = SymbolPrefix + ValueString + SymbolSuffix
        /// Will use the ISO 4217 currency code if no symbol is defined
        /// </summary>
        public readonly String SymbolSuffix;

        /// <summary>
        /// Thousands separator to use when formatting values as text
        /// </summary>
        public readonly String ThousandsSeparator;
        
        /// <summary>
        /// Decimal (under unit) separator to use when formatting values as text
        /// </summary>
        public readonly String DecimalSeparator;

        /// <summary>
        /// An example of the formatting
        /// </summary>
        public readonly String Example;

        /// <summary>
        /// Official name of the currency
        /// </summary>
        [TableDataWikipedia]
        public readonly String Name;

        /// <summary>
        /// Countries / regions where the currency is commonly used
        /// </summary>
        [TableDataGoogleSearch]
        public readonly String CommonlyUsed;


        readonly String ThousandsSeparatorFormat;
        readonly Char[] ThousandsSeparatorTrim;

        String ApplyThousandSeparator(Decimal a, String over)
        {
            var t = a.ToString(ThousandsSeparatorFormat).TrimStart(ThousandsSeparatorTrim);
            if (over != null)
                t = t.Replace(ThousandsSeparator, over);
            return String.IsNullOrEmpty(t) ? "0" : t;
        }

        /// <summary>
        /// Convert an amount to a displayable string
        /// </summary>
        /// <param name="amount">The valut to convert</param>
        /// <param name="options">Formatting options</param>
        /// <param name="decimalSeparatorOverride">Optionally override the default decimal separator</param>
        /// <param name="thousandSeparatorOverride">Optionally override the default thousand separator</param>
        /// <returns>A displayable string</returns>
        public String ToString(Decimal amount, CurrencyFormatOptions options = CurrencyFormatOptions.Default, String decimalSeparatorOverride = null, String thousandSeparatorOverride = null)
        {
            var decimalSeparator = decimalSeparatorOverride ?? DecimalSeparator;
            String value = null;
            if ((UnderUnits > 0) && ((options & CurrencyFormatOptions.ForceRounding) == 0))
            {
                var f = Math.Round(Math.Abs(amount) * UnderUnits);
                var sign = Math.Sign(amount);
                var a = Math.Truncate(f / UnderUnits);
                f %= UnderUnits;
                if ((f != 0) || ((options & CurrencyFormatOptions.AutomaticRounding) == 0))
                {
                    var fs = f.ToString(new string('0', DecimalNumbers));
                    a *= sign;
                    value = (options & CurrencyFormatOptions.ApplyThousandsSeparator) != 0 ? String.Join(decimalSeparator, ApplyThousandSeparator(a, thousandSeparatorOverride), fs) : String.Join(decimalSeparator, a, fs);
                }
            }
            if (value == null)
            {
                amount = Math.Round(amount);
                value = (options & CurrencyFormatOptions.ApplyThousandsSeparator) != 0 ? ApplyThousandSeparator(amount, thousandSeparatorOverride) : amount.ToString();
            }
            var style = options & CurrencyFormatOptions.Symbol;
            if (style == CurrencyFormatOptions.IsoPrefix)
                value = String.Join(' ', Iso4217, value);
            if (style == CurrencyFormatOptions.IsoSuffix)
                value = String.Join(' ', value, Iso4217);
            if (style == CurrencyFormatOptions.Symbol)
                value = String.Join(value, SymbolPrefix, SymbolSuffix);
            return value;
        }

        /// <summary>
        /// Get currency information given an ISO 4217 currency code, ex: "USD"
        /// </summary>
        /// <param name="iso4217">The ISO 4217 currency code, ex: "USD"</param>
        /// <returns>Currency information or null if an invalid currency code was specified</returns>
        public static IsoCurrency TryGet(String iso4217) => IsoToInfo.TryGetValue(iso4217?.FastToLower() ?? "", out var i) ? i : null;

        /// <summary>
        /// Get currency information given an ISO 4217 number, ex: 840
        /// </summary>
        /// <param name="number">The ISO 4217 number, ex: 840</param>
        /// <returns>Currency information or null if an invalid currency number specified</returns>
        public static IsoCurrency TryGet(int number) => NumberToInfo.TryGetValue(number, out var i) ? i : null;

        /// <summary>
        /// A list of all currencies known
        /// </summary>
        public static readonly IReadOnlyList<IsoCurrency> Currencies = new IsoCurrency[]
        {
            new IsoCurrency("AED", 784, 2, ".", " ", " \u062f\u002e\u0625", false, "United Arab Emirates dirham", "United Arab Emirates", "AE"),
            new IsoCurrency("AFN", 971, 2, ".", " ", " \u060b", false, "Afghan afghani", "Afghanistan", "AF"),
            new IsoCurrency("ALL", 008, 2, ".", " ", " Lek", false, "Albanian lek", "Albania", "AL"),
            new IsoCurrency("AMD", 051, 2, ".", " ", " \u058f", false, "Armenian dram", "Armenia", "AM"),
            new IsoCurrency("ANG", 532, 2, ".", " ", " \u0192", false, "Netherlands Antillean guilder", "Curaçao (CW), Sint Maarten (SX)", "CW"),
            new IsoCurrency("AOA", 973, 2, ".", " ", " Kz", false, "Angolan kwanza", "Angola", "AO"),
            new IsoCurrency("ARS", 032, 2, ".", " ", "$ ", false, "Argentine peso", "Argentina", "AR"),
            new IsoCurrency("AUD", 036, 2, ".", " ", "$ ", false, "Australian dollar", "Australia, Christmas Island (CX), Cocos (Keeling) Islands (CC), Heard Island and McDonald Islands (HM), Kiribati (KI), Nauru (NR), Norfolk Island (NF), Tuvalu (TV)", "AU"),
            new IsoCurrency("AWG", 533, 2, ".", " ", " \u0192", false, "Aruban florin", "Aruba", "AW"),
            new IsoCurrency("AZN", 944, 2, ".", " ", "\u20bc ", false, "Azerbaijani manat", "Azerbaijan", "AZ"),
            new IsoCurrency("BAM", 977, 2, ".", " ", " KM", false, "Bosnia and Herzegovina convertible mark", "Bosnia and Herzegovina", "BA"),
            new IsoCurrency("BBD", 052, 2, ".", " ", "$ ", false, "Barbados dollar", "Barbados", "BB"),
            new IsoCurrency("BDT", 050, 2, ".", " ", " Tk", false, "Bangladeshi taka", "Bangladesh", "BD"),
            new IsoCurrency("BGN", 975, 2, ".", " ", " \u043b\u0432", false, "Bulgarian lev", "Bulgaria", "BG"),
            new IsoCurrency("BHD", 048, 3, ".", " ", null, false, "Bahraini dinar", "Bahrain", "BH"),
            new IsoCurrency("BIF", 108, 2, ".", " ", null, false, "Burundian franc", "Burundi", "BI"),
            new IsoCurrency("BMD", 060, 2, ".", " ", "$ ", false, "Bermudian dollar", "Bermuda", "BM"),
            new IsoCurrency("BND", 096, 2, ".", " ", "$ ", false, "Brunei dollar", "Brunei", "BN"),
            new IsoCurrency("BOB", 068, 2, ".", " ", "$b ", false, "Boliviano", "Bolivia", "BO"),
            new IsoCurrency("BOV", 984, 2, ".", " ", null, false, "Bolivian Mvdol (funds code)", "Bolivia"),
            new IsoCurrency("BRL", 986, 2, ".", " ", "R$ ", false, "Brazilian real", "Brazil", "BR"),
            new IsoCurrency("BSD", 044, 2, ".", " ", "$ ", false, "Bahamian dollar", "Bahamas", "BS"),
            new IsoCurrency("BTN", 064, 2, ".", " ", null, false, "Bhutanese ngultrum", "Bhutan", "BT"),
            new IsoCurrency("BWP", 072, 2, ".", " ", " P", false, "Botswana pula", "Botswana", "BW"),
            new IsoCurrency("BYN", 933, 2, ".", " ", " Br", false, "Belarusian ruble", "Belarus", "BY"),
            new IsoCurrency("BZD", 084, 2, ".", " ", "BZ$ ", false, "Belize dollar", "Belize", "BZ"),
            new IsoCurrency("CAD", 124, 2, ".", " ", "$ ", false, "Canadian dollar", "Canada", "CA"),
            new IsoCurrency("CDF", 976, 2, ",", " ", " FC", false, "Congolese franc", "Democratic Republic of the Congo", "CD"),
            new IsoCurrency("CHE", 947, 2, ".", " ", null, false, "WIR Euro (complementary currency)", "Switzerland", "CH"),
            new IsoCurrency("CHF", 756, 2, ".", " ", " CHF", false, "Swiss franc", "Switzerland, Liechtenstein (LI)", "LI"),
            new IsoCurrency("CHW", 948, 2, ".", " ", null, false, "WIR Franc (complementary currency)", "Switzerland"),
            new IsoCurrency("CLF", 990, 4, ".", " ", " UF", false, "Unidad de Fomento (funds code)", "Chile"),
            new IsoCurrency("CLP", 152, 2, ".", " ", "$ ", false, "Chilean peso", "Chile", "CL"),
            new IsoCurrency("CNY", 156, 2, ".", " ", "\u00a5 ", false, "Chinese yuan", "China", "CN"),
            new IsoCurrency("COP", 170, 2, ".", " ", "$ ", false, "Colombian peso", "Colombia", "CO"),
            new IsoCurrency("COU", 970, 2, ".", " ", null, false, "Unidad de Valor Real (UVR) (funds code)[7]", "Colombia"),
            new IsoCurrency("CRC", 188, 2, ".", " ", "\u20a1 ", false, "Costa Rican colon", "Costa Rica", "CR"),
            new IsoCurrency("CUC", 931, 2, ".", " ", "$ ", false, "Cuban convertible peso", "Cuba"),
            new IsoCurrency("CUP", 192, 2, ".", " ", "\u20b1 ", false, "Cuban peso", "Cuba", "CU"),
            new IsoCurrency("CVE", 132, 2, ".", " ", "$ ", false, "Cape Verde escudo", "Cape Verde", "CV"),
            new IsoCurrency("CZK", 203, 2, ".", " ", " \u004b\u010d", false, "Czech koruna", "Czech Republic", "CZ"),
            new IsoCurrency("DEM", 999, 2, ".", " ", null, false, "DEMO, use for demo purposes", "DEMO"),
            new IsoCurrency("DJF", 262, 0, ".", " ", null, false, "Djiboutian franc", "Djibouti", "DJ"),
            new IsoCurrency("DKK", 208, 2, ".", " ", " kr", false, "Danish krone", "Denmark, Faroe Islands (FO), Greenland (GL)", "DK"),
            new IsoCurrency("DOP", 214, 2, ".", " ", "RD$ ", false, "Dominican peso", "Dominican Republic", "DO"),
            new IsoCurrency("DZD", 012, 2, ".", " ", " DA", false, "Algerian dinar", "Algeria", "DZ"),
            new IsoCurrency("EGP", 818, 2, ".", " ", "\u00a3 ", false, "Egyptian pound", "Egypt", "EG"),
            new IsoCurrency("ERN", 232, 0, ".", " ", " Nkf", false, "Eritrean nakfa", "Eritrea", "ER"),
            new IsoCurrency("ETB", 230, 2, ".", " ", " Br", false, "Ethiopian birr", "Ethiopia", "ET"),
            new IsoCurrency("EUR", 978, 2, ".", " ", "\u20ac ", false, "Euro", "Andorra (AD), Austria (AT), Belgium (BE), Cyprus (CY), Estonia (EE), Finland (FI), France (FR), Germany (DE), Greece (GR), Guadeloupe (GP), Ireland (IE), Italy (IT), Latvia (LV), Lithuania (LT), Luxembourg (LU), Malta (MT), Martinique (MQ), Mayotte (YT), Monaco (MC), Montenegro (ME), Netherlands (NL), Portugal (PT), Réunion (RE), Saint Barthélemy (BL), Saint Pierre and Miquelon (PM), San Marino (SM), Slovakia (SK), Slovenia (SI), Spain (ES)", "EU"),
            new IsoCurrency("FJD", 242, 2, ".", " ", "$ ", false, "Fiji dollar", "Fiji", "FJ"),
            new IsoCurrency("FKP", 238, 2, ".", " ", "\u00a3 ", false, "Falkland Islands pound", "Falkland Islands (pegged to GBP 1:1)", "FK"),
            new IsoCurrency("GBP", 826, 2, ".", " ", "\u00a3 ", false, "Pound sterling", "United Kingdom, the Isle of Man (IM, see Manx pound), Jersey (JE, see Jersey pound), and Guernsey (GG, see Guernsey pound)", "GB"),
            new IsoCurrency("GEL", 981, 2, ".", " ", null, false, "Georgian lari", "Georgia", "GE"),
            new IsoCurrency("GHS", 936, 2, ".", " ", "\u00a2 ", false, "Ghanaian cedi", "Ghana", "GH"),
            new IsoCurrency("GIP", 292, 2, ".", " ", "\u00a3 ", false, "Gibraltar pound", "Gibraltar (pegged to GBP 1:1)", "GI"),
            new IsoCurrency("GMD", 270, 2, ".", " ", null, false, "Gambian dalasi", "Gambia", "GM"),
            new IsoCurrency("GNF", 324, 2, ".", " ", " FG", false, "Guinean franc", "Guinea", "GN"),
            new IsoCurrency("GTQ", 320, 2, ".", " ", " Q", false, "Guatemalan quetzal", "Guatemala", "GT"),
            new IsoCurrency("GYD", 328, 2, ".", " ", "$ ", false, "Guyanese dollar", "Guyana", "GY"),
            new IsoCurrency("HKD", 344, 2, ".", " ", "$ ", false, "Hong Kong dollar", "Hong Kong", "HK"),
            new IsoCurrency("HNL", 340, 2, ".", " ", " L", false, "Honduran lempira", "Honduras", "HN"),
            new IsoCurrency("HRK", 191, 2, ".", " ", " kn", false, "Croatian kuna", "Croatia", "HR"),
            new IsoCurrency("HTG", 332, 2, ".", " ", " G", false, "Haitian gourde", "Haiti", "HT"),
            new IsoCurrency("HUF", 348, 2, ".", " ", " Ft", false, "Hungarian forint", "Hungary", "HU"),
            new IsoCurrency("IDR", 360, 2, ".", " ", " Rp", false, "Indonesian rupiah", "Indonesia", "ID"),
            new IsoCurrency("ILS", 376, 2, ".", " ", "\u20aa ", false, "Israeli new shekel", "Israel", "IL"),
            new IsoCurrency("INR", 356, 2, ".", " ", "\u20b9 ", false, "Indian rupee", "India, Bhutan", "IN"),
            new IsoCurrency("IQD", 368, 3, ".", " ", " د.ع", false, "Iraqi dinar", "Iraq", "IQ"),
            new IsoCurrency("IRR", 364, 2, ".", " ", " \ufdfc", false, "Iranian rial", "Iran", "IR"),
            new IsoCurrency("ISK", 352, 2, ".", " ", " kr", false, "Icelandic króna", "Iceland", "IS"),
            new IsoCurrency("JMD", 388, 2, ".", " ", "J$ ", false, "Jamaican dollar", "Jamaica", "JM"),
            new IsoCurrency("JOD", 400, 2, ".", " ", null, false, "Jordanian dinar", "Jordan", "JO"),
            new IsoCurrency("JPY", 392, 2, ".", " ", "\u00a5 ", false, "Japanese yen", "Japan", "JP"),
            new IsoCurrency("KES", 404, 2, ".", " ", " KSh", false, "Kenyan shilling", "Kenya", "KE"),
            new IsoCurrency("KGS", 417, 2, ".", " ", "⃀ ", false, "Kyrgyzstani som", "Kyrgyzstan", "KG"),
            new IsoCurrency("KHR", 116, 1, ".", " ", "\u17db ", false, "Cambodian riel", "Cambodia", "KH"),
            new IsoCurrency("KMF", 174, 2, ".", " ", null, false, "Comoro franc", "Comoros", "KM"),
            new IsoCurrency("KPW", 408, 2, ".", " ", "\u20a9 ", false, "North Korean won", "North Korea", "KP"),
            new IsoCurrency("KRW", 410, 2, ".", " ", "\u20a9 ", false, "South Korean won", "South Korea", "KR"),
            new IsoCurrency("KWD", 414, 3, ".", " ", " \u0643", false, "Kuwaiti dinar", "Kuwait", "KW"),
            new IsoCurrency("KYD", 136, 2, ".", " ", "$ ", false, "Cayman Islands dollar", "Cayman Islands", "KY"),
            new IsoCurrency("KZT", 398, 2, ".", " ", "₸ ", false, "Kazakhstani tenge", "Kazakhstan", "KZ"),
            new IsoCurrency("LAK", 418, 2, ".", " ", "\u20ad ", false, "Lao kip", "Laos", "LA"),
            new IsoCurrency("LBP", 422, 2, ".", " ", "\u00a3 ", false, "Lebanese pound", "Lebanon", "LB"),
            new IsoCurrency("LKR", 144, 2, ".", " ", "\u20a8 ", false, "Sri Lankan rupee", "Sri Lanka", "LK"),
            new IsoCurrency("LRD", 430, 2, ".", " ", "$ ", false, "Liberian dollar", "Liberia", "LR"),
            new IsoCurrency("LSL", 426, 0, ".", " ", null, false, "Lesotho loti", "Lesotho", "LS"),
            new IsoCurrency("LYD", 434, 3, ".", " ", " LD", false, "Libyan dinar", "Libya", "LY"),
            new IsoCurrency("MAD", 504, 2, ".", " ", null, false, "Moroccan dirham", "Morocco", "MA"),
            new IsoCurrency("MDL", 498, 2, ".", " ", null, false, "Moldovan leu", "Moldova", "MD"),
            new IsoCurrency("MGA", 969, 0.2, ".", " ", " Ar", false, "Malagasy ariary", "Madagascar", "MG"),
            new IsoCurrency("MKD", 807, 2, ".", " ", " \u0434\u0435\u043d", false, "Macedonian denar", "Macedonia", "MK"),
            new IsoCurrency("MMK", 104, 2, ".", " ", " K", false, "Myanmar kyat", "Myanmar", "MM"),
            new IsoCurrency("MNT", 496, 2, ".", " ", "\u20ae ", false, "Mongolian tögrög", "Mongolia", "MN"),
            new IsoCurrency("MOP", 446, 2, ".", " ", "$ ", false, "Macanese pataca", "Macao", "MO"),
            new IsoCurrency("MRO", 478, 0.2, ".", " ", null, false, "Mauritanian ouguiya", "Mauritania", "MR"),
            new IsoCurrency("MUR", 480, 2, ".", " ", "\u20a8 ", false, "Mauritian rupee", "Mauritius", "MU"),
            new IsoCurrency("MVR", 462, 2, ".", " ", null, false, "Maldivian rufiyaa", "Maldives", "MV"),
            new IsoCurrency("MWK", 454, 2, ".", " ", " K", false, "Malawian kwacha", "Malawi", "MW"),
            new IsoCurrency("MXN", 484, 2, ".", " ", "$ ", false, "Mexican peso", "Mexico", "MX"),
            new IsoCurrency("MXV", 979, 2, ".", " ", null, false, "Mexican Unidad de Inversion (UDI) (funds code)", "Mexico"),
            new IsoCurrency("MYR", 458, 2, ".", " ", " RM", false, "Malaysian ringgit", "Malaysia", "MY"),
            new IsoCurrency("MZN", 943, 2, ".", " ", " MT", false, "Mozambican metical", "Mozambique", "MZ"),
            new IsoCurrency("NAD", 516, 2, ".", " ", "$ ", false, "Namibian dollar", "Namibia", "NA"),
            new IsoCurrency("NGN", 566, 2, ".", " ", "\u20a6 ", false, "Nigerian naira", "Nigeria", "NG"),
            new IsoCurrency("NIO", 558, 2, ".", " ", "C$ ", false, "Nicaraguan córdoba", "Nicaragua", "NI"),
            new IsoCurrency("NOK", 578, 2, ".", " ", " kr", false, "Norwegian krone", "Norway, Svalbard and Jan Mayen (SJ), Bouvet Island (BV)", "NO"),
            new IsoCurrency("NPR", 524, 2, ".", " ", "\u20a8 ", false, "Nepalese rupee", "Nepal", "NP"),
            new IsoCurrency("NZD", 554, 2, ".", " ", "$ ", false, "New Zealand dollar", "New Zealand, Cook Islands (CK), Niue (NU), Pitcairn Islands (PN; see also Pitcairn Islands dollar), Tokelau (TK)", "NZ"),
            new IsoCurrency("OMR", 512, 3, ".", " ", " \ufdfc", false, "Omani rial", "Oman", "OM"),
            new IsoCurrency("PAB", 590, 2, ".", " ", "B/. ", false, "Panamanian balboa", "Panama", "PA"),
            new IsoCurrency("PEN", 604, 2, ".", " ", "S/. ", false, "Peruvian Sol", "Peru", "PE"),
            new IsoCurrency("PGK", 598, 2, ".", " ", " K", false, "Papua New Guinean kina", "Papua New Guinea", "PG"),
            new IsoCurrency("PHP", 608, 2, ".", " ", "\u20b1 ", false, "Philippine peso", "Philippines", "PH"),
            new IsoCurrency("PKR", 586, 2, ".", " ", "\u20a8 ", false, "Pakistani rupee", "Pakistan", "PK"),
            new IsoCurrency("PLN", 985, 2, ".", " ", "\u007a\u0142 ", false, "Polish złoty", "Poland", "PL"),
            new IsoCurrency("PYG", 600, 2, ".", " ", " Gs", false, "Paraguayan guaraní", "Paraguay", "PY"),
            new IsoCurrency("QAR", 634, 2, ".", " ", " \ufdfc", false, "Qatari riyal", "Qatar", "QA"),
            new IsoCurrency("RON", 946, 2, ".", " ", " lei", false, "Romanian leu", "Romania", "RO"),
            new IsoCurrency("RSD", 941, 2, ".", " ", "\u0414\u0438\u043d\u002e ", false, "Serbian dinar", "Serbia", "RS"),
            new IsoCurrency("RUB", 643, 2, ".", " ", "\u20bd ", false, "Russian ruble", "Russia", "RU"),
            new IsoCurrency("RWF", 646, 2, ".", " ", " FRw", false, "Rwandan franc", "Rwanda", "RW"),
            new IsoCurrency("SAR", 682, 2, ".", " ", " \ufdfc", false, "Saudi riyal", "Saudi Arabia", "SA"),
            new IsoCurrency("SBD", 090, 2, ".", " ", "$ ", false, "Solomon Islands dollar", "Solomon Islands", "SB"),
            new IsoCurrency("SCR", 690, 2, ".", " ", "\u20a8 ", false, "Seychelles rupee", "Seychelles", "SC"),
            new IsoCurrency("SDG", 938, 2, ".", " ", null, false, "Sudanese pound", "Sudan", "SD"),
            new IsoCurrency("SEK", 752, 2, ",", " ", " kr", false, "Swedish krona/kronor", "Sweden", "SE"),
            new IsoCurrency("SGD", 702, 2, ".", " ", "$ ", false, "Singapore dollar", "Singapore", "SG"),
            new IsoCurrency("SHP", 654, 2, ".", " ", "\u00a3 ", false, "Saint Helena pound", "Saint Helena (SH-SH), Ascension Island (SH-AC), Tristan da Cunha", "SH"),
            new IsoCurrency("SLL", 694, 2, ".", " ", " Le", false, "Sierra Leonean leone", "Sierra Leone", "SL"),
            new IsoCurrency("SOS", 706, 2, ".", " ", " S", false, "Somali shilling", "Somalia", "SO"),
            new IsoCurrency("SRD", 968, 2, ".", " ", "$ ", false, "Surinamese dollar", "Suriname", "SR"),
            new IsoCurrency("SSP", 728, 2, ".", " ", null, false, "South Sudanese pound", "South Sudan"),
            new IsoCurrency("STD", 678, 0, ".", " ", null, false, "São Tomé and Príncipe dobra", "São Tomé and Príncipe"),
            new IsoCurrency("SVC", 222, 2, ".", " ", "$ ", false, "Salvadoran colón", "El Salvador", "SV"),
            new IsoCurrency("SYP", 760, 2, ".", " ", "\u00a3 ", false, "Syrian pound", "Syria", "SY"),
            new IsoCurrency("SZL", 748, 2, ".", " ", null, false, "Swazi lilangeni", "Swaziland", "SZ"),
            new IsoCurrency("THB", 764, 2, ".", " ", "\u0e3f ", false, "Thai baht", "Thailand", "TH"),
            new IsoCurrency("TJS", 972, 2, ".", " ", null, false, "Tajikistani somoni", "Tajikistan", "TJ"),
            new IsoCurrency("TMT", 934, 0, ".", " ", null, false, "Turkmenistani manat", "Turkmenistan", "TM"),
            new IsoCurrency("TND", 788, 0, ".", " ", null, false, "Tunisian dinar", "Tunisia", "TN"),
            new IsoCurrency("TOP", 776, 2, ".", " ", "$ ", false, "Tongan paʻanga", "Tonga", "TO"),
            new IsoCurrency("TRY", 949, 2, ".", " ", "\u20ba ", false, "Turkish lira", "Turkey", "TR"),
            new IsoCurrency("TTD", 780, 2, ".", " ", "TT$ ", false, "Trinidad and Tobago dollar", "Trinidad and Tobago", "TT"),
            new IsoCurrency("TWD", 901, 2, ".", " ", "NT$ ", false, "New Taiwan dollar", "Taiwan", "TW"),
            new IsoCurrency("TZS", 834, 2, "/", " ", " TZs", false, "Tanzanian shilling", "Tanzania", "TZ"),
            new IsoCurrency("UAH", 980, 2, ".", " ", "\u20b4 ", false, "Ukrainian hryvnia", "Ukraine", "UA"),
            new IsoCurrency("UGX", 800, 0, ".", " ", " USh", false, "Ugandan shilling", "Uganda", "UG"),
            new IsoCurrency("USD", 840, 2, ".", " ", "$ ", false, "United States dollar", "United States, American Samoa (AS), Barbados (BB) (as well as Barbados Dollar), Bermuda (BM) (as well as Bermudian Dollar), British Indian Ocean Territory (IO) (also uses GBP), British Virgin Islands (VG), Caribbean Netherlands (BQ - Bonaire, Sint Eustatius and Saba), Ecuador (EC), El Salvador (SV), Guam (GU), Haiti (HT), Marshall Islands (MH), Federated States of Micronesia (FM), Northern Mariana Islands (MP), Palau (PW), Panama (PA), Puerto Rico (PR), Timor-Leste (TL), Turks and Caicos Islands (TC), U.S. Virgin Islands (VI), United States Minor Outlying Islands", "US"),
            new IsoCurrency("USN", 997, 2, ".", " ", null, false, "United States dollar (next day) (funds code)", "United States"),
            new IsoCurrency("UYI", 940, 0, ".", " ", null, false, "Uruguay Peso en Unidades Indexadas (URUIURUI) (funds code)", "Uruguay"),
            new IsoCurrency("UYU", 858, 2, ".", " ", "$U ", false, "Uruguayan peso", "Uruguay", "UY"),
            new IsoCurrency("UZS", 860, 2, ".", " ", " сўм", false, "Uzbekistan som", "Uzbekistan", "UZ"),
            new IsoCurrency("VEF", 937, 2, ".", " ", " Bs", false, "Venezuelan bolívar", "Venezuela", "VE"),
            new IsoCurrency("VND", 704, 1, ".", " ", "\u20ab ", false, "Vietnamese đồng", "Vietnam", "VN"),
            new IsoCurrency("VUV", 548, 0, ".", " ", " VT", false, "Vanuatu vatu", "Vanuatu", "VU"),
            new IsoCurrency("WST", 882, 2, ".", " ", "$ ", false, "Samoan tala", "Samoa", "WS"),
            new IsoCurrency("XAF", 950, 2, ".", " ", null, false, "CFA franc BEAC", "Cameroon (CM), Central African Republic (CF), Republic of the Congo (CG), Chad (TD), Equatorial Guinea (GQ), Gabon (GA)", "CM"),
            new IsoCurrency("XCD", 951, 2, ".", " ", "$ ", false, "East Caribbean dollar", "Anguilla (AI), Antigua and Barbuda (AG), Dominica (DM), Grenada (GD), Montserrat (MS), Saint Kitts and Nevis (KN), Saint Lucia (LC), Saint Vincent and the Grenadines (VC)", "LC"),
            new IsoCurrency("XOF", 952, 2, ".", " ", " CFA", false, "CFA franc BCEAO", "Benin (BJ), Burkina Faso (BF), Côte d'Ivoire (CI), Guinea-Bissau (GW), Mali (ML), Niger (NE), Senegal (SN), Togo (TG)", "CI"),
            new IsoCurrency("XPF", 953, 2, ".", " ", null, false, "CFP franc (franc Pacifique)", "French territories of the Pacific Ocean: French Polynesia (PF), New Caledonia (NC), Wallis and Futuna (WF)", "NC"),
            new IsoCurrency("YER", 886, 2, ".", " ", " \ufdfc", false, "Yemeni rial", "Yemen", "YE"),
            new IsoCurrency("ZAR", 710, 2, ".", " ", " R", false, "South African rand", "South Africa", "ZA"),
            new IsoCurrency("ZMW", 967, 2, ".", " ", " K", false, "Zambian kwacha", "Zambia", "ZM"),
            //new IsoCurrency("ZWD", 932, 2, ".", " ", "Z$ ", false, "Zimbabwean dollar A/10 (obsolete)", "Zimbabwe", "ZW"),
            //new IsoCurrency("ZWG", 932, 2, ".", " ", "Z$ ", false, "Zimbabwean dollar A/10", "Zimbabwe", "ZW"),
            new IsoCurrency("ZWL", 932, 2, ".", " ", "Z$ ", false, "Zimbabwean dollar A/10", "Zimbabwe", "ZW"),
            new IsoCurrency("STN", 930, 2, ".", " ", " Db", false, "Dobra", "São Tomé and Príncipe", "ST"),
            new IsoCurrency("SLE", 925, 2, ".", " ", " Le", false, "New Sierra Leonean leone", "Sierra Leone", "SL"),
        };

        public override string ToString()
        {
            return String.Concat(Iso4217, ' ', Name, " [" + Symbol + "]");
        }

        IsoCurrency(String i, int n, double d, String decimalSeparator, String thousandsSeparator, String symbol, bool isSuffix, String name, String commonUse, String majorUserCountryCode = null)
        {   
            Iso4217 = i;
            Country = majorUserCountryCode;
            Number = n;
            Name = name;
            DecimalDigits = d;
            CommonlyUsed = commonUse;

            if (String.IsNullOrEmpty(symbol))
            {
                Symbol = null;
                SymbolPrefix = i + " ";
                SymbolSuffix = String.Empty;
            }
            else
            {
                Symbol = symbol.Trim();
                isSuffix |= (symbol[0] == 32);
                SymbolPrefix = isSuffix ? String.Empty : symbol;
                SymbolSuffix = isSuffix ? symbol : String.Empty;
            }
            UnderUnits = d > 0 ? (d >= 1 ? (int)Math.Round(Math.Pow(10.0, d)) : (int)Math.Round(1.0 / d)) : 1;
            DecimalNumbers = (int)Math.Ceiling(d);
            ThousandsSeparator = thousandsSeparator;
            ThousandsSeparatorFormat = String.Join(thousandsSeparator, '#', String.Join(thousandsSeparator, Enumerable.Range(0, 6).Select(x => "###")));
            ThousandsSeparatorTrim = thousandsSeparator.ToCharArray();
            DecimalSeparator = decimalSeparator;
            Example = ToString(98765.4321M, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.AutomaticRounding | CurrencyFormatOptions.Symbol);
        }


        static void Add(HashSet<Char> h, String v)
        {
            if (String.IsNullOrEmpty(v))
                return;
            foreach (var x in v)
                h.Add(x);
        }

        static IsoCurrency()
        {
            var t = new Dictionary<string, IsoCurrency>(StringComparer.Ordinal);
            var t2 = new Dictionary<int, IsoCurrency>();
            var hf = new HashSet<Char>();
            Add(hf, "-0123456789");
            foreach (var c in Currencies)
            {
                t.Add(c.Iso4217.FastToLower(), c);
                t2.Add(c.Number, c);
                Add(hf, c.Iso4217);
                Add(hf, c.Symbol);
                Add(hf, c.SymbolPrefix);
                Add(hf, c.SymbolSuffix);
                Add(hf, c.DecimalSeparator);
                Add(hf, c.ThousandsSeparator);
            }
            IsoToInfo = t.Freeze();
            NumberToInfo = t2.Freeze();
            CurrencyGlyphs = hf.OrderBy(a => a).ToList();
            IsCurrencyGlyph = hf.Freeze();
        }

        static readonly IReadOnlyDictionary<String, IsoCurrency> IsoToInfo;
        static readonly IReadOnlyDictionary<int, IsoCurrency> NumberToInfo;
        

        /// <summary>
        /// All chars needed to display currency strings
        /// </summary>
        public static readonly IReadOnlyList<Char> CurrencyGlyphs;

        /// <summary>
        /// Test if char is required to display currency string
        /// </summary>
        public static readonly IReadOnlySet<Char> IsCurrencyGlyph;

    }




}
