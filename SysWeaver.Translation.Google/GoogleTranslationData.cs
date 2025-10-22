using System;

namespace SysWeaver.Translation
{
    sealed class GoogleTranslationData
    {
        public GoogleTranslationData(Tuple<String, DateTime, Object> d)
        {
            var url = d.Item1;
            var i1 = url.IndexOf("?tl=");
            i1 += 4;
            var i2 = url.IndexOf("&sl=", i1);
            To = url.Substring(i1, i2 - i1);
            i2 += 4;
            var i3 = url.IndexOf("&q=", i2);
            From = url.Substring(i2, i3 - i2);
            i3 += 3;
            Text = Uri.UnescapeDataString(url.Substring(i3));
            Time = d.Item2;
            Result = d.Item3 as String;
        }

        /// <summary>
        /// The time when this data was stored in the cache (fetched)
        /// </summary>
        public DateTime Time { get; set; }
        /// <summary>
        /// The two letter ISO 639-1 language code of the source with an optional two letter ISO 3166-A2 country code separated using a hyphen.
        /// </summary>
        public String From { get; set; }
        /// <summary>
        /// The original text, assumed to be in the source (From) language.
        /// </summary>
        public String Text { get; set; }
        /// <summary>
        /// The two letter ISO 639-1 language code of the target with an optional two letter ISO 3166-A2 country code separated using a hyphen.
        /// </summary>
        public String To { get; set; }
        /// <summary>
        /// The resulting text, assumed to be in the target (To) language.
        /// </summary>
        public String Result { get; set; }
    }

}
