using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver
{
    public sealed class ManagedMessages
    {
        /// <summary>
        /// Holds a bunch of messages, localized, text and mail templates
        /// </summary>
        /// <param name="sourceFolder">The folder where the source files are, should have a subfolder for each language, folder name should match the language iso, ex: "en-US", "en-GB", "es-MX", "es-ES", "de", "sv".
        /// Can use a "*" to define a subfolder pattern, ex: "C:\locale\*\UserManager".</param>
        /// <param name="defaultLang">The default language if the supplied language isn't found</param>
        /// <param name="htmlMail">True if the folder contain html mail templates.
        /// If true files are named: "[Name]Mail.html" (body) and "[Name]Mail.txt" (subject)".
        /// If false files are named: "[Name]MailBody.txt" (body) and "[Name]Mail.txt" (subject)".
        /// Where [Name] is any of the supplies names.
        /// </param>
        /// <param name="names">The names/keys of messages.
        /// Text (SMS/message) files are names as "[Name]Text.txt", mail fails are named as specified in the htmlMail parameter.
        /// If the name starts with a '*', the data is optional (will return empty strings if not found)</param>
        /// <exception cref="Exception"></exception>
        public ManagedMessages(String sourceFolder, String defaultLang, bool htmlMail, params String[] names)
        {
            var languages = new ConcurrentDictionary<String, ManagedLanguageMessages>(StringComparer.Ordinal);
            sourceFolder = sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar);
            var i = sourceFolder.IndexOf('*');
            String searchFolder = sourceFolder;
            if (i < 0)
            {
                sourceFolder = String.Join(Path.DirectorySeparatorChar, sourceFolder, '*');
            }else
            {
                searchFolder = sourceFolder.Substring(0, i).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar);
            }
            foreach (var ff in Directory.GetDirectories(searchFolder))
            {
                var lang = Path.GetFileName(ff);
                var f = PathExt.GetFullDirectoryName(sourceFolder.Replace("*", lang));
                var text = new Dictionary<String, ManagedTextMessage>(StringComparer.Ordinal);
                var mail = new Dictionary<String, ManagedMailMessage>(StringComparer.Ordinal);
                foreach (var name in names)
                {
                    bool isOptional = name.StartsWith("*");
                    var n = isOptional ? name.Substring(1) : name;

                    var mm = new ManagedMailMessage
                    {
                        Body = Path.Combine(f, n + (htmlMail ? "Mail.html" : "MailBody.txt")),
                        Subject = Path.Combine(f, n + "Mail.txt"),
                        IsHtml = htmlMail,
                    };
                    var tm = new ManagedTextMessage
                    {
                        Body = Path.Combine(f, n + "Text.txt"),
                    };
                    if (!File.Exists(mm.Body))
                        if (isOptional)
                            mm.Body = "";
                        else
                            throw new Exception("Localized message file " + mm.Body.ToFilename() + " does NOT exist!");
                    if (!File.Exists(mm.Subject))
                        if (isOptional)
                            mm.Subject = "";
                        else
                            throw new Exception("Localized message file " + mm.Subject.ToFilename() + " does NOT exist!");
                    if (!File.Exists(tm.Body))
                        if (isOptional)
                            tm.Body = "";
                        else
                            throw new Exception("Localized message file " + tm.Body.ToFilename() + " does NOT exist!");
                    var key = name.FastToLower();
                    text.Add(key, tm);
                    mail.Add(key, mm);
                }
                languages.TryAdd(lang.FastToLower(), new ManagedLanguageMessages(lang, text.Freeze(), mail.Freeze()));
            }
            ManagedLanguageMessages def = null;
            if (languages.Count > 0)
            {
                var langs = languages.OrderBy(x => x.Key).ToList();
                foreach (var lang in langs)
                {
                    var x = lang.Key.Split('-');
                    if (x.Length <= 1)
                        continue;
                    var key = x[0];
                    if (languages.ContainsKey(key))
                        continue;
                    languages.TryAdd(key, lang.Value);
                }
                var dl = (defaultLang ?? "en").FastToLower();
                foreach (var x in languages)
                {
                    if (x.Key.FastEquals(dl))
                    {
                        def = x.Value;
                        break;
                    }
                }
                if (def == null)
                    languages.TryGetValue("en", out def);
                if (def == null)
                    def = langs.First().Value;
            }
            Default = def == null ? null : new ManagedLanguageMessages(def);
            Languages = languages;
        }

        public override string ToString() => String.Concat("Default: ", Default, ", languages: ", Languages.Count);

        readonly ConcurrentDictionary<String, ManagedLanguageMessages> Languages = new ConcurrentDictionary<String, ManagedLanguageMessages>(StringComparer.Ordinal);


        public bool TryAddTranslatedLanguage(String language, ManagedLanguageMessages data)
            => Languages.TryAdd(language, data);

        /// <summary>
        /// The language used if the supplied language isn't found
        /// </summary>
        public readonly ManagedLanguageMessages Default;

 
        /// <summary>
        /// Get 
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public ManagedLanguageMessages GetLang(String language)
        {
            if (language == null)
                return Default;
            var l = Languages;
            language = language.FastToLower();
            if (l.TryGetValue(language, out var x))
                return x;
            var s = language.Split('-');
            if (s.Length > 1)
                if (l.TryGetValue(s[0], out x))
                    return x;
            return Default;
        }

    }


}
