using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver.IsoData
{
    public class IsoLanguage
    {
        public override string ToString() => Comment == null ? String.Concat(Iso639_1, ' ', Name, " [", Iso639_2, ']') : String.Concat(Iso639_1, ' ', Name, " [", Iso639_2, "] - ", Comment);


        /// <summary>
        /// Flag
        /// </summary>
        [TableDataIsoLanguageImage]
        [TableDataOrder(-1)]
        public String Flag => Iso639_1;

        /// <summary>
        /// The two letter ISO 639-1 language code of this language
        /// </summary>
        public readonly String Iso639_1;

        /// <summary>
        /// The three letter ISO 639-2 language code of this language
        /// </summary>
        public readonly String Iso639_2;

        /// <summary>
        /// The official name of this language
        /// </summary>
        [TableDataWikipedia(null, "{0} language")]
        public readonly String Name;

        /// <summary>
        /// Optional comments
        /// </summary>
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the comments section for the language named \"{0}\"", nameof(Name))]
        [AutoTranslateContext("The two letter ISO 639-1 language code of this language is \"{0}\"", nameof(Iso639_1))]
        public readonly String Comment;

        protected IsoLanguage(string name, string iso639_1, string iso639_2, String comment)
        {
            Name = name;
            Iso639_1 = iso639_1;
            Iso639_2 = iso639_2;
            Comment = comment;
        }

        /// <summary>
        /// Get information about a language from a two letter ISO 639-1 language code or a three letter ISO 639-2 language code.
        /// Ex: 
        /// "en" => "English"
        /// "afr" => "Afrikaans"
        /// </summary>
        /// <param name="iso639">A two letter ISO 639-1 language code or a three letter ISO 639-2 language code</param>
        /// <returns>Information about the language if it's known, or null if it's unknown</returns>
        public static IsoLanguage TryGet(String iso639) => IsoToInfo.TryGetValue(iso639?.FastToLower() ?? "", out var i) ? i : null;

        /// <summary>
        /// Get information about a language from a two letter ISO 639-1 language code or a three letter ISO 639-2 language code.
        /// The code can optionally be combined with a two letter ISO 3166-A2 country code using a hyphen.
        /// Ex: 
        /// "en" => "English", null
        /// "en-GB" => "English", "UNITED KINGDOM"
        /// "es-MX" => "Spanish, Castilian", "MEXICO"
        /// </summary>
        /// <param name="country">Information about the country if specified and known, else null</param>
        /// <param name="regionalCode">A two letter ISO 639-1 language code or a three letter ISO 639-2 language code, optionally combined with a two letter ISO 3166-A2 country code using a hyphen.</param>
        /// <returns>Information about the language if it's known, or null if it's unknown</returns>
        public static IsoLanguage TryGet(out IsoCountry country, String regionalCode)
        {
            country = null;
            if (regionalCode == null)
                return null;
            var t = regionalCode.IndexOf('-');
            if (t > 0)
            {
                country = IsoCountry.TryGet(regionalCode.Substring(t + 1));
                if (country == null)
                    return null;
                regionalCode = regionalCode.Substring(0, t);
            }
            return IsoToInfo.TryGetValue(regionalCode, out var i) ? i : null;
        }

        /// <summary>
        /// Get information about a language from a language name, iso code and so on
        /// Ex:
        ///   "Swedish" => "sv"
        ///   "Maldivian" => "dv"
        /// </summary>
        /// <param name="name">The name of the language</param>
        /// <returns>Information about the language if it's known, or null if it's unknown</returns>
        public static IsoLanguage TryGetName(String name) => NameToInfo.TryGetValue((name ?? "").Split('-')[0].Trim().FastToLower(), out var i) ? i : null;


        /// <summary>
        /// Enumerates all aliases for a language
        /// </summary>
        public static IEnumerable<KeyValuePair<String, IsoLanguage>> Aliases => NameToInfo;

        /// <summary>
        /// All known languages
        /// </summary>
        public static readonly IReadOnlyList<IsoLanguage> Languages = new IsoLanguage[]
        {
            new IsoLanguage("Abkhazian", "ab", "abk", "Also known as Abkhaz"),
            new IsoLanguage("Afar", "aa", "aar", null),
            new IsoLanguage("Afrikaans", "af", "afr", null),
            new IsoLanguage("Akan", "ak", "aka", "Macrolanguage, Twi is tw/twi, Fanti is fat"),
            new IsoLanguage("Albanian", "sq", "sqi", "Macrolanguage, called Albanian Phylozone in 639-6"),
            new IsoLanguage("Amharic", "am", "amh", null),
            new IsoLanguage("Arabic", "ar", "ara", "Macrolanguage, Standard Arabic is arb"),
            new IsoLanguage("Aragonese", "an", "arg", null),
            new IsoLanguage("Armenian", "hy", "hye", "ISO 639-3 code hye is for Eastern Armenian, hyw is for Western Armenian, and xcl is for Classical Armenian"),
            new IsoLanguage("Assamese", "as", "asm", null),
            new IsoLanguage("Avaric", "av", "ava", "Also known as Avar"),
            new IsoLanguage("Avestan", "ae", "ave", "Ancient"),
            new IsoLanguage("Aymara", "ay", "aym", "Macrolanguage"),
            new IsoLanguage("Azerbaijani", "az", "aze", "Macrolanguage, also known as Azeri"),
            new IsoLanguage("Bambara", "bm", "bam", null),
            new IsoLanguage("Bashkir", "ba", "bak", null),
            new IsoLanguage("Basque", "eu", "eus", null),
            new IsoLanguage("Belarusian", "be", "bel", null),
            new IsoLanguage("Bengali", "bn", "ben", "Also known as Bangla"),
            new IsoLanguage("Bislama", "bi", "bis", "Language formed from English and Vanuatuan languages, with some French influence."),
            new IsoLanguage("Bosnian", "bs", "bos", null),
            new IsoLanguage("Breton", "br", "bre", null),
            new IsoLanguage("Bulgarian", "bg", "bul", null),
            new IsoLanguage("Burmese", "my", "mya", "Also known as Myanmar"),
            new IsoLanguage("Catalan, Valencian", "ca", "cat", null),
            new IsoLanguage("Chamorro", "ch", "cha", null),
            new IsoLanguage("Chechen", "ce", "che", null),
            new IsoLanguage("Chichewa, Chewa, Nyanja", "ny", "nya", null),
            new IsoLanguage("Chinese", "zh", "zho", "Macrolanguage"),
            new IsoLanguage("Church Slavonic, Old Slavonic, Old Church Slavonic", "cu", "chu", "ancient, in use by the Eastern Orthodox Church"),
            new IsoLanguage("Chuvash", "cv", "chv", null),
            new IsoLanguage("Cornish", "kw", "cor", null),
            new IsoLanguage("Corsican", "co", "cos", null),
            new IsoLanguage("Cree", "cr", "cre", "Macrolanguage"),
            new IsoLanguage("Croatian", "hr", "hrv", null),
            new IsoLanguage("Czech", "cs", "ces", null),
            new IsoLanguage("Danish", "da", "dan", null),
            new IsoLanguage("Divehi, Dhivehi, Maldivian", "dv", "div", null),
            new IsoLanguage("Dutch, Flemish", "nl", "nld", "Flemish is not to be confused with the closely related West Flemish which is referred to as Vlaams (Dutch for Flemish) in ISO 639-3 and has the ISO 639-3 code vls"),
            new IsoLanguage("Dzongkha", "dz", "dzo", null),
            new IsoLanguage("English", "en", "eng", null),
            new IsoLanguage("Esperanto", "eo", "epo", "Constructed, initially by L.L. Zamenhof in 1887"),
            new IsoLanguage("Estonian", "et", "est", "Macrolanguage"),
            new IsoLanguage("Ewe", "ee", "ewe", null),
            new IsoLanguage("Faroese", "fo", "fao", null),
            new IsoLanguage("Fijian", "fj", "fij", null),
            new IsoLanguage("Finnish", "fi", "fin", null),
            new IsoLanguage("French", "fr", "fra", null),
            new IsoLanguage("Western Frisian", "fy", "fry", "Also known as Frisian"),
            new IsoLanguage("Fulah", "ff", "ful", "Macrolanguage, also known as Fula"),
            new IsoLanguage("Gaelic, Scottish Gaelic", "gd", "gla", null),
            new IsoLanguage("Galician", "gl", "glg", null),
            new IsoLanguage("Ganda", "lg", "lug", null),
            new IsoLanguage("Georgian", "ka", "kat", null),
            new IsoLanguage("German", "de", "deu", null),
            new IsoLanguage("Greek, Modern (1453–)", "el", "ell", "For Ancient Greek, use the ISO 639-3 code grc"),
            new IsoLanguage("Kalaallisut, Greenlandic", "kl", "kal", null),
            new IsoLanguage("Guarani", "gn", "grn", "Macrolanguage"),
            new IsoLanguage("Gujarati", "gu", "guj", null),
            new IsoLanguage("Haitian, Haitian Creole", "ht", "hat", null),
            new IsoLanguage("Hausa", "ha", "hau", null),
            new IsoLanguage("Hebrew", "he", "heb", "Modern Hebrew. Code changed in 1989 from original ISO 639:1988, iw.[1]"),
            new IsoLanguage("Herero", "hz", "her", null),
            new IsoLanguage("Hindi", "hi", "hin", null),
            new IsoLanguage("Hiri Motu", "ho", "hmo", null),
            new IsoLanguage("Hungarian", "hu", "hun", null),
            new IsoLanguage("Icelandic", "is", "isl", null),
            new IsoLanguage("Ido", "io", "ido", "Constructed by De Beaufront, 1907, as variation of Esperanto"),
            new IsoLanguage("Igbo", "ig", "ibo", null),
            new IsoLanguage("Indonesian", "id", "ind", "Covered by Macrolanguage ms/msa. Changed in 1989 from original ISO 639:1988, in.[1]"),
            new IsoLanguage("Interlingua (International Auxiliary Language Association)", "ia", "ina", "Constructed by the International Auxiliary Language Association"),
            new IsoLanguage("Interlingue, Occidental", "ie", "ile", "Constructed by Edgar de Wahl, first published in 1922"),
            new IsoLanguage("Inuktitut", "iu", "iku", "Macrolanguage"),
            new IsoLanguage("Inupiaq", "ik", "ipk", "Macrolanguage"),
            new IsoLanguage("Irish", "ga", "gle", null),
            new IsoLanguage("Italian", "it", "ita", null),
            new IsoLanguage("Japanese", "ja", "jpn", null),
            new IsoLanguage("Javanese", "jv", "jav", null),
            new IsoLanguage("Kannada", "kn", "kan", null),
            new IsoLanguage("Kanuri", "kr", "kau", "Macrolanguage"),
            new IsoLanguage("Kashmiri", "ks", "kas", null),
            new IsoLanguage("Kazakh", "kk", "kaz", null),
            new IsoLanguage("Central Khmer", "km", "khm", "Also known as Khmer or Cambodian"),
            new IsoLanguage("Kikuyu, Gikuyu", "ki", "kik", null),
            new IsoLanguage("Kinyarwanda", "rw", "kin", null),
            new IsoLanguage("Kirghiz, Kyrgyz", "ky", "kir", null),
            new IsoLanguage("Komi", "kv", "kom", "Macrolanguage"),
            new IsoLanguage("Kongo", "kg", "kon", "Macrolanguage"),
            new IsoLanguage("Korean", "ko", "kor", null),
            new IsoLanguage("Kuanyama, Kwanyama", "kj", "kua", null),
            new IsoLanguage("Kurdish", "ku", "kur", "Macrolanguage"),
            new IsoLanguage("Lao", "lo", "lao", null),
            new IsoLanguage("Latin", "la", "lat", "ancient"),
            new IsoLanguage("Latvian", "lv", "lav", "Macrolanguage"),
            new IsoLanguage("Limburgan, Limburger, Limburgish", "li", "lim", null),
            new IsoLanguage("Lingala", "ln", "lin", null),
            new IsoLanguage("Lithuanian", "lt", "lit", null),
            new IsoLanguage("Luba-Katanga", "lu", "lub", "Also known as Luba-Shaba"),
            new IsoLanguage("Luxembourgish, Letzeburgesch", "lb", "ltz", null),
            new IsoLanguage("Macedonian", "mk", "mkd", null),
            new IsoLanguage("Malagasy", "mg", "mlg", "Macrolanguage"),
            new IsoLanguage("Malay", "ms", "msa", "Macrolanguage, Standard Malay is zsm, Indonesian is id/ind"),
            new IsoLanguage("Malayalam", "ml", "mal", null),
            new IsoLanguage("Maltese", "mt", "mlt", null),
            new IsoLanguage("Manx", "gv", "glv", null),
            new IsoLanguage("Maori", "mi", "mri", "Also known as Māori"),
            new IsoLanguage("Marathi", "mr", "mar", "Also known as Marāṭhī"),
            new IsoLanguage("Marshallese", "mh", "mah", null),
            new IsoLanguage("Mongolian", "mn", "mon", "Macrolanguage"),
            new IsoLanguage("Nauru", "na", "nau", "Also known as Nauruan"),
            new IsoLanguage("Navajo, Navaho", "nv", "nav", null),
            new IsoLanguage("North Ndebele", "nd", "nde", "Also known as Northern Ndebele"),
            new IsoLanguage("South Ndebele", "nr", "nbl", "Also known as Southern Ndebele"),
            new IsoLanguage("Ndonga", "ng", "ndo", null),
            new IsoLanguage("Nepali", "ne", "nep", "Macrolanguage"),
            new IsoLanguage("Norwegian", "no", "nor", "Macrolanguage, Bokmål is nb/nob, Nynorsk is nn/nno"),
            new IsoLanguage("Norwegian Bokmål", "nb", "nob", "covered by Macrolanguage no/nor"),
            new IsoLanguage("Norwegian Nynorsk", "nn", "nno", "covered by Macrolanguage no/nor"),
            new IsoLanguage("Sichuan Yi, Nuosu", "ii", "iii", "standard form of the Yi languages"),
            new IsoLanguage("Occitan", "oc", "oci", null),
            new IsoLanguage("Ojibwa", "oj", "oji", "Macrolanguage, also known as Ojibwe"),
            new IsoLanguage("Oriya", "or", "ori", "Macrolanguage, also known as Odia"),
            new IsoLanguage("Oromo", "om", "orm", "Macrolanguage"),
            new IsoLanguage("Ossetian, Ossetic", "os", "oss", null),
            new IsoLanguage("Pali", "pi", "pli", "ancient, also known as Pāli"),
            new IsoLanguage("Pashto, Pushto", "ps", "pus", "Macrolanguage"),
            new IsoLanguage("Persian", "fa", "fas", "Macrolanguage, also known as Farsi"),
            new IsoLanguage("Polish", "pl", "pol", null),
            new IsoLanguage("Portuguese", "pt", "por", null),
            new IsoLanguage("Punjabi, Panjabi", "pa", "pan", null),
            new IsoLanguage("Quechua", "qu", "que", "Macrolanguage"),
            new IsoLanguage("Romanian, Moldavian, Moldovan", "ro", "ron", "The identifiers mo and mol for Moldavian are deprecated. They will not be assigned to different items, and recordings using these identifiers will not be invalid."),
            new IsoLanguage("Romansh", "rm", "roh", null),
            new IsoLanguage("Rundi", "rn", "run", "Also known as Kirundi"),
            new IsoLanguage("Russian", "ru", "rus", null),
            new IsoLanguage("Northern Sami", "se", "sme", null),
            new IsoLanguage("Samoan", "sm", "smo", null),
            new IsoLanguage("Sango", "sg", "sag", null),
            new IsoLanguage("Sanskrit", "sa", "san", "Ancient"),
            new IsoLanguage("Sardinian", "sc", "srd", "Macrolanguage"),
            new IsoLanguage("Serbian", "sr", "srp", "The ISO 639-2/T code srp deprecated the ISO 639-2/B code scc[2]"),
            new IsoLanguage("Shona", "sn", "sna", null),
            new IsoLanguage("Sindhi", "sd", "snd", null),
            new IsoLanguage("Sinhala, Sinhalese", "si", "sin", null),
            new IsoLanguage("Slovak", "sk", "slk", null),
            new IsoLanguage("Slovenian", "sl", "slv", "Also known as Slovene"),
            new IsoLanguage("Somali", "so", "som", null),
            new IsoLanguage("Southern Sotho", "st", "sot", null),
            new IsoLanguage("Spanish, Castilian", "es", "spa", null),
            new IsoLanguage("Sundanese", "su", "sun", null),
            new IsoLanguage("Swahili", "sw", "swa", "Macrolanguage"),
            new IsoLanguage("Swati", "ss", "ssw", "Also known as Swazi"),
            new IsoLanguage("Swedish", "sv", "swe", null),
            new IsoLanguage("Tagalog", "tl", "tgl", "Filipino (Pilipino) has the code fil"),
            new IsoLanguage("Tahitian", "ty", "tah", "One of the Reo Mā`ohi (languages of French Polynesia)"),
            new IsoLanguage("Tajik", "tg", "tgk", null),
            new IsoLanguage("Tamil", "ta", "tam", null),
            new IsoLanguage("Tatar", "tt", "tat", null),
            new IsoLanguage("Telugu", "te", "tel", null),
            new IsoLanguage("Thai", "th", "tha", null),
            new IsoLanguage("Tibetan", "bo", "bod", "Also known as Standard Tibetan"),
            new IsoLanguage("Tigrinya", "ti", "tir", null),
            new IsoLanguage("Tonga (Tonga Islands)", "to", "ton", "Also known as Tongan"),
            new IsoLanguage("Tsonga", "ts", "tso", null),
            new IsoLanguage("Tswana", "tn", "tsn", null),
            new IsoLanguage("Turkish", "tr", "tur", null),
            new IsoLanguage("Turkmen", "tk", "tuk", null),
            new IsoLanguage("Twi", "tw", "twi", "Covered by Macrolanguage ak/aka"),
            new IsoLanguage("Uighur, Uyghur", "ug", "uig", null),
            new IsoLanguage("Ukrainian", "uk", "ukr", null),
            new IsoLanguage("Urdu", "ur", "urd", null),
            new IsoLanguage("Uzbek", "uz", "uzb", "Macrolanguage"),
            new IsoLanguage("Venda", "ve", "ven", null),
            new IsoLanguage("Vietnamese", "vi", "vie", null),
            new IsoLanguage("Volapük", "vo", "vol", "Constructed"),
            new IsoLanguage("Walloon", "wa", "wln", null),
            new IsoLanguage("Welsh", "cy", "cym", null),
            new IsoLanguage("Wolof", "wo", "wol", null),
            new IsoLanguage("Xhosa", "xh", "xho", null),
            new IsoLanguage("Yiddish", "yi", "yid", "Macrolanguage. Changed in 1989 from original ISO 639:1988, ji.[1]"),
            new IsoLanguage("Yoruba", "yo", "yor", null),
            new IsoLanguage("Zhuang, Chuang", "za", "zha", "Macrolanguage"),
            new IsoLanguage("Zulu", "zu", "zul", null),
        };



        public static String[] Common;


        static IsoLanguage()
        {
            var languages = Languages;
            var t = new Dictionary<string, IsoLanguage>(StringComparer.Ordinal);
            foreach (var c in languages)
            {
                t.Add(c.Iso639_1.FastToLower(), c);
                t.Add(c.Iso639_2.FastToLower(), c);
            }
            IsoToInfo = t.Freeze();

            t = new Dictionary<string, IsoLanguage>(StringComparer.Ordinal);


            void AddOne(String s, IsoLanguage c)
            {
                s = s.Trim();
                var cc = s[0];
                if (!Char.IsLetter(cc))
                    return;
                if (!Char.IsUpper(cc))
                    return;
                if (s.Length < 4)
                    return;
                s = s.FastToLower();
                t[s] = c;
            }

            void AddSplit(String s, IsoLanguage c, String split)
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
            
            foreach (var c in languages)
            {
                AddOne(c.Name, c);
                AddSplit(c.Name, c, ",");
            }

            foreach (var c in languages)
            {
                t[c.Iso639_1.FastToLower()] = c;
                t[c.Iso639_2.FastToLower()] = c;
            }
            NameToInfo = t.Freeze();

            var speakers = new Dictionary<String, long>();
            var cmap = new Dictionary<String, List<Tuple<IsoCountry, Decimal>>>(StringComparer.Ordinal);
            foreach (var c in IsoCountry.Countries)
            {
                Decimal score = c.Population;
                bool first = true;
                foreach (var x in c.Languages.Split(','))
                {
                    var key = x.Trim().FastToLower();
                    if (!cmap.TryGetValue(key, out var l))
                    {
                        l = new List<Tuple<IsoCountry, decimal>>();
                        cmap[key] = l;
                    }
                    l.Add(Tuple.Create(c, score));
                    score *= (first ? 0.03M : 0.5M);
                    first = false;
                    speakers.TryGetValue(key, out var count);
                    count += c.Population;
                    speakers[key] = count;
                }
            }
            var tmap = new Dictionary<String, String[]>(StringComparer.Ordinal);
            foreach (var x in cmap)
            {
                var d = x.Value;
                if (d.Count > 1)
                    d.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                tmap.Add(x.Key, d.Select(x => x.Item1.Iso3166a2).ToArray());
            }
            CountryMap = tmap.Freeze();

            List<String> common = new List<string>(languages.Count);
            foreach (var c in languages)
            {
                if (!speakers.ContainsKey(c.Iso639_1))
                    if (!speakers.ContainsKey(c.Iso639_2))
                        continue;
                var cc = c.Comment?.FastToLower();
                if (cc != null)
                {
                    if (cc.IndexOf("ancient") == 0)
                        continue;
                }
                common.Add(c.Iso639_1);
            }
            Common = common.ToArray();
        }

        /// <summary>
        /// Get countries where a language is used.
        /// </summary>
        /// <param name="language">The ISO 639-1 language code of the language</param>
        /// <returns>An array of ISO 3166 Alpha 2 country codes where the language is used, in order of population</returns>
        public static String[] GetCountries(String language) => CountryMap.TryGetValue((language ?? "").FastToLower(), out var countries) ? countries : [];


        static readonly IReadOnlyDictionary<String, String[]> CountryMap;

        static readonly IReadOnlyDictionary<String, IsoLanguage> IsoToInfo;
        static readonly IReadOnlyDictionary<String, IsoLanguage> NameToInfo;

        /// <summary>
        /// Validate that the input is a valid language code
        /// </summary>
        /// <param name="language">The ISO 639-1 language code of the language with an optional two letter ISO 3166-A2 country code separated by a hyphen ('-')</param>
        /// <param name="allowNames">If true a known language name is accepted</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static String Validate(string language, bool allowNames  = false)
        {
            language = language?.Trim();
            if (String.IsNullOrEmpty(language))
                return null;
            var cc = language.Split('-');
            IsoLanguage t;
            switch (cc.Length)
            {
                case 1:
                    t = TryGet(language);
                    if (t == null)
                    {
                        if (allowNames)
                            t = TryGetName(language);
                        if (t == null)
                            throw new Exception("Unknown language code");
                    }
                    return t.Iso639_1;
                case 2:
                    t = TryGet(cc[0]);
                    if (t == null)
                        throw new Exception("Unknown language code");
                    var t2 = IsoCountry.TryGet(cc[1]);
                    if (t2 == null)
                        throw new Exception("The country part of the language part is unknown");

                    return String.Join('-', t.Iso639_1, t2.Iso3166a2);
                default:
                    throw new Exception("Invalid language code, mat not contain more than one '-'");
            }
        }


    }


    public static class IsoLanguageExt
    {
        /// <summary>
        /// Get countries where a language is used.
        /// </summary>
        /// <param name="language">The ISO 639-1 language code of the language</param>
        /// <returns>An array of ISO 3166 Alpha 2 country codes where the language is used, in order of population</returns>
        public static String[] GetCountries(this IsoLanguage language) => IsoLanguage.GetCountries(language?.Iso639_1);

    }
}
