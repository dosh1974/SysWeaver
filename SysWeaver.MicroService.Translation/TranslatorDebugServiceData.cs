using System;
using SysWeaver.Data;
using SysWeaver.IsoData;

namespace SysWeaver.MicroService
{


    sealed class TranslatorDebugServiceData : IsoLanguage
    {
        public TranslatorDebugServiceData(String locale, String countryCode, IsoLanguage l) :
            base(l?.Name ?? "-", l?.Iso639_1 ?? "-", l?.Iso639_2 ?? "-", l == null ? String.Concat('"', locale, "\" is an unknown language?") : l.Comment)
        {
            Locale = locale;
            CountryCode = countryCode;
            CountryFlag = countryCode;
        }

        /// <summary>
        /// The locale to use for this translation
        /// </summary>
        public readonly String Locale;

        /// <summary>
        /// The ISO 3166 Alpha 2 country code of the country (if present in the locale)
        /// </summary>
        [TableDataIsoCountry]
        public readonly String CountryCode;

        /// <summary>
        /// The flag of the country (if present in the locale)
        /// </summary>
        [TableDataIsoCountryImage]
        public readonly String CountryFlag;

        public static TranslatorDebugServiceData FromString(String s)
        {
            var l = IsoLanguage.TryGet(out var c, s);
            return new TranslatorDebugServiceData(s, c?.Iso3166a2, l);
        }
    }



}
