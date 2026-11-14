/**
 * @file core/region.js - detect the locale of the viewer's region for dates and times (Sw.FormatRegion.Detect), loaded on demand.
 *
 * Purpose: a web page can't read the date and time formats of the operating system (browsers don't expose them), but it knows the time
 * zone (Intl) and the browser's languages. The time zone gives the country ("Europe/Stockholm" => SE); the locale is then a browser
 * language of that country ("sv-SE" in navigator.languages), else the main language of the country with it ("sv-SE"): that's the
 * region format an operating system uses by default, ex: Swedish dates and times "2018-01-30 07:30:40". A zone that isn't known gives
 * the first browser language.
 * Used by core/format.js (Sw.Format.Locale): loaded once per browser (and after a time zone change), the result is stored in
 * localStorage "SysWeaver.FormatLocaleAuto"; the user can choose another locale (Sw.Format.SetLocale, the app's settings menu).
 *
 * Load order: any (Sw.Net.IncludeJs). Dependencies: none.
 * Globals defined: none (Sw.FormatRegion).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Time zone => country (ISO 3166-1 alpha-2), the zones of the IANA database that people live in (aliases included).
     * @type {Object<string, string>}
     */
    const Zones = {
        //  Europe
        "Europe/Stockholm": "SE", "Europe/Oslo": "NO", "Arctic/Longyearbyen": "NO", "Europe/Copenhagen": "DK", "Atlantic/Faroe": "FO", "America/Nuuk": "GL",
        "America/Godthab": "GL", "Europe/Helsinki": "FI", "Europe/Mariehamn": "AX", "Atlantic/Reykjavik": "IS", "Europe/London": "GB", "Europe/Belfast": "GB",
        "Europe/Guernsey": "GG", "Europe/Jersey": "JE", "Europe/Isle_of_Man": "IM", "Europe/Dublin": "IE", "Europe/Lisbon": "PT", "Atlantic/Madeira": "PT",
        "Atlantic/Azores": "PT", "Europe/Madrid": "ES", "Africa/Ceuta": "ES", "Atlantic/Canary": "ES", "Europe/Andorra": "AD", "Europe/Gibraltar": "GI",
        "Europe/Paris": "FR", "Europe/Monaco": "MC", "Europe/Brussels": "BE", "Europe/Amsterdam": "NL", "Europe/Luxembourg": "LU", "Europe/Berlin": "DE",
        "Europe/Busingen": "DE", "Europe/Zurich": "CH", "Europe/Vaduz": "LI", "Europe/Vienna": "AT", "Europe/Rome": "IT", "Europe/San_Marino": "SM",
        "Europe/Vatican": "VA", "Europe/Malta": "MT", "Europe/Prague": "CZ", "Europe/Bratislava": "SK", "Europe/Warsaw": "PL", "Europe/Budapest": "HU",
        "Europe/Ljubljana": "SI", "Europe/Zagreb": "HR", "Europe/Sarajevo": "BA", "Europe/Belgrade": "RS", "Europe/Podgorica": "ME", "Europe/Skopje": "MK",
        "Europe/Tirane": "AL", "Europe/Athens": "GR", "Europe/Sofia": "BG", "Europe/Bucharest": "RO", "Europe/Chisinau": "MD", "Europe/Kiev": "UA",
        "Europe/Kyiv": "UA", "Europe/Uzhgorod": "UA", "Europe/Zaporozhye": "UA", "Europe/Simferopol": "UA", "Europe/Minsk": "BY", "Europe/Vilnius": "LT",
        "Europe/Riga": "LV", "Europe/Tallinn": "EE", "Europe/Moscow": "RU", "Europe/Kaliningrad": "RU", "Europe/Samara": "RU", "Europe/Volgograd": "RU",
        "Europe/Saratov": "RU", "Europe/Ulyanovsk": "RU", "Europe/Astrakhan": "RU", "Europe/Kirov": "RU", "Europe/Istanbul": "TR", "Asia/Istanbul": "TR",
        "Asia/Nicosia": "CY", "Europe/Nicosia": "CY", "Asia/Famagusta": "CY",
        //  Russia (Asia)
        "Asia/Yekaterinburg": "RU", "Asia/Omsk": "RU", "Asia/Novosibirsk": "RU", "Asia/Barnaul": "RU", "Asia/Tomsk": "RU", "Asia/Novokuznetsk": "RU",
        "Asia/Krasnoyarsk": "RU", "Asia/Irkutsk": "RU", "Asia/Chita": "RU", "Asia/Yakutsk": "RU", "Asia/Khandyga": "RU", "Asia/Vladivostok": "RU",
        "Asia/Ust-Nera": "RU", "Asia/Magadan": "RU", "Asia/Sakhalin": "RU", "Asia/Srednekolymsk": "RU", "Asia/Kamchatka": "RU", "Asia/Anadyr": "RU",
        //  North America
        "America/New_York": "US", "America/Detroit": "US", "America/Kentucky/Louisville": "US", "America/Kentucky/Monticello": "US",
        "America/Indiana/Indianapolis": "US", "America/Indianapolis": "US", "America/Indiana/Vincennes": "US", "America/Indiana/Winamac": "US",
        "America/Indiana/Marengo": "US", "America/Indiana/Petersburg": "US", "America/Indiana/Vevay": "US", "America/Chicago": "US",
        "America/Indiana/Tell_City": "US", "America/Indiana/Knox": "US", "America/Menominee": "US", "America/North_Dakota/Center": "US",
        "America/North_Dakota/New_Salem": "US", "America/North_Dakota/Beulah": "US", "America/Denver": "US", "America/Boise": "US", "America/Phoenix": "US",
        "America/Los_Angeles": "US", "America/Anchorage": "US", "America/Juneau": "US", "America/Sitka": "US", "America/Metlakatla": "US",
        "America/Yakutat": "US", "America/Nome": "US", "America/Adak": "US", "Pacific/Honolulu": "US", "US/Eastern": "US", "US/Central": "US",
        "US/Mountain": "US", "US/Pacific": "US", "US/Alaska": "US", "US/Hawaii": "US", "America/Puerto_Rico": "PR",
        "America/Toronto": "CA", "America/Montreal": "CA", "America/Nipigon": "CA", "America/Thunder_Bay": "CA", "America/Iqaluit": "CA",
        "America/Winnipeg": "CA", "America/Rainy_River": "CA", "America/Rankin_Inlet": "CA", "America/Resolute": "CA", "America/Regina": "CA",
        "America/Swift_Current": "CA", "America/Edmonton": "CA", "America/Cambridge_Bay": "CA", "America/Inuvik": "CA", "America/Yellowknife": "CA",
        "America/Vancouver": "CA", "America/Whitehorse": "CA", "America/Dawson": "CA", "America/Dawson_Creek": "CA", "America/Fort_Nelson": "CA",
        "America/Creston": "CA", "America/Halifax": "CA", "America/Glace_Bay": "CA", "America/Moncton": "CA", "America/Goose_Bay": "CA",
        "America/St_Johns": "CA", "America/Atikokan": "CA", "America/Blanc-Sablon": "CA",
        "America/Mexico_City": "MX", "America/Cancun": "MX", "America/Merida": "MX", "America/Monterrey": "MX", "America/Matamoros": "MX",
        "America/Chihuahua": "MX", "America/Ciudad_Juarez": "MX", "America/Ojinaga": "MX", "America/Mazatlan": "MX", "America/Bahia_Banderas": "MX",
        "America/Hermosillo": "MX", "America/Tijuana": "MX",
        //  Central America and the Caribbean
        "America/Guatemala": "GT", "America/Belize": "BZ", "America/El_Salvador": "SV", "America/Tegucigalpa": "HN", "America/Managua": "NI",
        "America/Costa_Rica": "CR", "America/Panama": "PA", "America/Havana": "CU", "America/Jamaica": "JM", "America/Port-au-Prince": "HT",
        "America/Santo_Domingo": "DO", "America/Nassau": "BS", "America/Barbados": "BB", "America/Port_of_Spain": "TT", "America/Martinique": "MQ",
        "America/Guadeloupe": "GP", "America/Curacao": "CW", "America/Aruba": "AW",
        //  South America
        "America/Sao_Paulo": "BR", "America/Rio_Branco": "BR", "America/Manaus": "BR", "America/Belem": "BR", "America/Fortaleza": "BR",
        "America/Recife": "BR", "America/Bahia": "BR", "America/Maceio": "BR", "America/Araguaina": "BR", "America/Cuiaba": "BR",
        "America/Campo_Grande": "BR", "America/Porto_Velho": "BR", "America/Boa_Vista": "BR", "America/Santarem": "BR", "America/Eirunepe": "BR",
        "America/Noronha": "BR", "America/Argentina/Buenos_Aires": "AR", "America/Buenos_Aires": "AR", "America/Argentina/Cordoba": "AR",
        "America/Argentina/Salta": "AR", "America/Argentina/Jujuy": "AR", "America/Argentina/Tucuman": "AR", "America/Argentina/Catamarca": "AR",
        "America/Argentina/La_Rioja": "AR", "America/Argentina/San_Juan": "AR", "America/Argentina/Mendoza": "AR", "America/Argentina/San_Luis": "AR",
        "America/Argentina/Rio_Gallegos": "AR", "America/Argentina/Ushuaia": "AR", "America/Santiago": "CL", "America/Punta_Arenas": "CL",
        "Pacific/Easter": "CL", "America/Bogota": "CO", "America/Lima": "PE", "America/Caracas": "VE", "America/Guayaquil": "EC",
        "Pacific/Galapagos": "EC", "America/La_Paz": "BO", "America/Asuncion": "PY", "America/Montevideo": "UY", "America/Guyana": "GY",
        "America/Paramaribo": "SR", "America/Cayenne": "GF", "Atlantic/Stanley": "FK",
        //  Asia
        "Asia/Tokyo": "JP", "Asia/Seoul": "KR", "Asia/Pyongyang": "KP", "Asia/Shanghai": "CN", "Asia/Urumqi": "CN", "Asia/Chongqing": "CN",
        "Asia/Harbin": "CN", "Asia/Hong_Kong": "HK", "Asia/Macau": "MO", "Asia/Taipei": "TW", "Asia/Ulaanbaatar": "MN", "Asia/Hovd": "MN",
        "Asia/Choibalsan": "MN", "Asia/Manila": "PH", "Asia/Ho_Chi_Minh": "VN", "Asia/Saigon": "VN", "Asia/Bangkok": "TH", "Asia/Vientiane": "LA",
        "Asia/Phnom_Penh": "KH", "Asia/Yangon": "MM", "Asia/Rangoon": "MM", "Asia/Kuala_Lumpur": "MY", "Asia/Kuching": "MY", "Asia/Singapore": "SG",
        "Asia/Brunei": "BN", "Asia/Jakarta": "ID", "Asia/Pontianak": "ID", "Asia/Makassar": "ID", "Asia/Jayapura": "ID", "Asia/Dili": "TL",
        "Asia/Kolkata": "IN", "Asia/Calcutta": "IN", "Asia/Colombo": "LK", "Asia/Kathmandu": "NP", "Asia/Katmandu": "NP", "Asia/Thimphu": "BT",
        "Asia/Dhaka": "BD", "Asia/Karachi": "PK", "Asia/Kabul": "AF", "Indian/Maldives": "MV", "Asia/Tashkent": "UZ", "Asia/Samarkand": "UZ",
        "Asia/Almaty": "KZ", "Asia/Qostanay": "KZ", "Asia/Qyzylorda": "KZ", "Asia/Aqtobe": "KZ", "Asia/Aqtau": "KZ", "Asia/Atyrau": "KZ",
        "Asia/Oral": "KZ", "Asia/Bishkek": "KG", "Asia/Dushanbe": "TJ", "Asia/Ashgabat": "TM", "Asia/Tehran": "IR", "Asia/Baghdad": "IQ",
        "Asia/Kuwait": "KW", "Asia/Riyadh": "SA", "Asia/Bahrain": "BH", "Asia/Qatar": "QA", "Asia/Dubai": "AE", "Asia/Muscat": "OM", "Asia/Aden": "YE",
        "Asia/Amman": "JO", "Asia/Damascus": "SY", "Asia/Beirut": "LB", "Asia/Jerusalem": "IL", "Asia/Tel_Aviv": "IL", "Asia/Gaza": "PS",
        "Asia/Hebron": "PS", "Asia/Tbilisi": "GE", "Asia/Yerevan": "AM", "Asia/Baku": "AZ",
        //  Oceania
        "Australia/Sydney": "AU", "Australia/Melbourne": "AU", "Australia/Brisbane": "AU", "Australia/Lindeman": "AU", "Australia/Adelaide": "AU",
        "Australia/Broken_Hill": "AU", "Australia/Darwin": "AU", "Australia/Perth": "AU", "Australia/Eucla": "AU", "Australia/Hobart": "AU",
        "Australia/Currie": "AU", "Australia/Canberra": "AU", "Australia/Lord_Howe": "AU", "Antarctica/Macquarie": "AU", "Pacific/Auckland": "NZ",
        "Pacific/Chatham": "NZ", "Pacific/Fiji": "FJ", "Pacific/Port_Moresby": "PG", "Pacific/Bougainville": "PG", "Pacific/Guadalcanal": "SB",
        "Pacific/Noumea": "NC", "Pacific/Tahiti": "PF", "Pacific/Apia": "WS", "Pacific/Tongatapu": "TO", "Pacific/Guam": "GU", "Pacific/Efate": "VU",
        //  Africa
        "Africa/Cairo": "EG", "Africa/Tripoli": "LY", "Africa/Tunis": "TN", "Africa/Algiers": "DZ", "Africa/Casablanca": "MA", "Africa/El_Aaiun": "EH",
        "Africa/Khartoum": "SD", "Africa/Juba": "SS", "Africa/Addis_Ababa": "ET", "Africa/Asmara": "ER", "Africa/Djibouti": "DJ",
        "Africa/Mogadishu": "SO", "Africa/Nairobi": "KE", "Africa/Kampala": "UG", "Africa/Kigali": "RW", "Africa/Bujumbura": "BI",
        "Africa/Dar_es_Salaam": "TZ", "Africa/Lagos": "NG", "Africa/Accra": "GH", "Africa/Abidjan": "CI", "Africa/Dakar": "SN", "Africa/Bamako": "ML",
        "Africa/Ouagadougou": "BF", "Africa/Niamey": "NE", "Africa/Ndjamena": "TD", "Africa/Douala": "CM", "Africa/Kinshasa": "CD",
        "Africa/Lubumbashi": "CD", "Africa/Brazzaville": "CG", "Africa/Libreville": "GA", "Africa/Luanda": "AO", "Africa/Lusaka": "ZM",
        "Africa/Harare": "ZW", "Africa/Maputo": "MZ", "Africa/Blantyre": "MW", "Africa/Johannesburg": "ZA", "Africa/Windhoek": "NA",
        "Africa/Gaborone": "BW", "Africa/Maseru": "LS", "Africa/Mbabane": "SZ", "Indian/Antananarivo": "MG", "Indian/Mauritius": "MU",
        "Indian/Reunion": "RE", "Africa/Conakry": "GN", "Africa/Freetown": "SL", "Africa/Monrovia": "LR", "Africa/Banjul": "GM",
        "Africa/Bissau": "GW", "Africa/Nouakchott": "MR", "Africa/Lome": "TG", "Africa/Porto-Novo": "BJ", "Atlantic/Cape_Verde": "CV",
    };

    /**
     * Country => its main language (ISO 639-1) for the region format; a country that isn't listed uses the browser's first language.
     * @type {Object<string, string>}
     */
    const Languages = {
        SE: "sv", AX: "sv", NO: "nb", DK: "da", FO: "fo", GL: "kl", FI: "fi", IS: "is", GB: "en", GG: "en", JE: "en", IM: "en", IE: "en", GI: "en",
        PT: "pt", ES: "es", AD: "ca", FR: "fr", MC: "fr", BE: "nl", NL: "nl", LU: "fr", DE: "de", CH: "de", LI: "de", AT: "de", IT: "it", SM: "it",
        VA: "it", MT: "mt", CZ: "cs", SK: "sk", PL: "pl", HU: "hu", SI: "sl", HR: "hr", BA: "bs", RS: "sr", ME: "sr", MK: "mk", AL: "sq", GR: "el",
        BG: "bg", RO: "ro", MD: "ro", UA: "uk", BY: "be", LT: "lt", LV: "lv", EE: "et", RU: "ru", TR: "tr", CY: "el",
        US: "en", PR: "es", CA: "en", MX: "es", GT: "es", BZ: "en", SV: "es", HN: "es", NI: "es", CR: "es", PA: "es", CU: "es", JM: "en", HT: "fr",
        DO: "es", BS: "en", BB: "en", TT: "en", MQ: "fr", GP: "fr", CW: "nl", AW: "nl", BR: "pt", AR: "es", CL: "es", CO: "es", PE: "es", VE: "es",
        EC: "es", BO: "es", PY: "es", UY: "es", GY: "en", SR: "nl", GF: "fr", FK: "en",
        JP: "ja", KR: "ko", KP: "ko", CN: "zh", HK: "zh", MO: "zh", TW: "zh", MN: "mn", PH: "en", VN: "vi", TH: "th", LA: "lo", KH: "km", MM: "my",
        MY: "ms", SG: "en", BN: "ms", ID: "id", TL: "pt", IN: "en", LK: "si", NP: "ne", BT: "dz", BD: "bn", PK: "ur", AF: "fa", MV: "dv", UZ: "uz",
        KZ: "kk", KG: "ky", TJ: "tg", TM: "tk", IR: "fa", IQ: "ar", KW: "ar", SA: "ar", BH: "ar", QA: "ar", AE: "ar", OM: "ar", YE: "ar", JO: "ar",
        SY: "ar", LB: "ar", IL: "he", PS: "ar", GE: "ka", AM: "hy", AZ: "az",
        AU: "en", NZ: "en", FJ: "en", PG: "en", SB: "en", NC: "fr", PF: "fr", WS: "en", TO: "en", GU: "en", VU: "en",
        EG: "ar", LY: "ar", TN: "ar", DZ: "ar", MA: "ar", EH: "ar", SD: "ar", SS: "en", ET: "am", ER: "ti", DJ: "fr", SO: "so", KE: "en", UG: "en",
        RW: "rw", BI: "fr", TZ: "sw", NG: "en", GH: "en", CI: "fr", SN: "fr", ML: "fr", BF: "fr", NE: "fr", TD: "fr", CM: "fr", CD: "fr", CG: "fr",
        GA: "fr", AO: "pt", ZM: "en", ZW: "en", MZ: "pt", MW: "en", ZA: "en", NA: "en", BW: "en", LS: "en", SZ: "en", MG: "mg", MU: "en", RE: "fr",
        GN: "fr", SL: "en", LR: "en", GM: "en", GW: "pt", MR: "ar", TG: "fr", BJ: "fr", CV: "pt",
    };

    /**
     * Test if the browser has the formats of a locale itself (not only a fallback to its language).
     * @param {string} l The locale, ex: "sv-SE".
     * @returns {boolean} True when supported.
     */
    function has(l) {
        try {
            return new Intl.DateTimeFormat(l).resolvedOptions().locale.toLowerCase() === l.toLowerCase();
        }
        catch {
            return false;
        }
    }

    Sw.FormatRegion = {
        /** @type {Object<string, string>} Time zone => country. */
        Zones: Zones,
        /** @type {Object<string, string>} Country => main language. */
        Languages: Languages,

        /**
         * The country of a time zone.
         * @param {string} [tz] The IANA time zone (default the viewer's).
         * @returns {?string} The country (ISO 3166-1 alpha-2), null when not known.
         */
        Country(tz) {
            try {
                tz = tz || Intl.DateTimeFormat().resolvedOptions().timeZone;
            }
            catch {
                return null;
            }
            return Zones[tz] || null;
        },

        /**
         * Detect the locale of the viewer's region (see the file description).
         * @param {string} [tz] The IANA time zone (default the viewer's).
         * @param {string[]} [languages] The browser languages (default navigator.languages).
         * @returns {?string} The locale, ex: "sv-SE"; null when nothing is known (the browser's default is used).
         */
        Detect(tz, languages) {
            const langs = (languages || (navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language])).filter(x => x);
            const cc = Sw.FormatRegion.Country(tz);
            if (cc) {
                //  1. A browser language of the region ("sv-SE", "en-SE")
                const own = langs.find(l => l.toUpperCase().endsWith("-" + cc) && has(l));
                if (own)
                    return own;
                //  2. The main language of the region (the operating system's region format), ex: "sv-SE"
                const main = Languages[cc];
                if (main && has(main + "-" + cc))
                    return main + "-" + cc;
                //  3. The browser's language in the region, ex: "en-SE"
                const base = (langs[0] || "en").split("-")[0];
                if (has(base + "-" + cc))
                    return base + "-" + cc;
            }
            return langs[0] || null;
        },
    };
})();
