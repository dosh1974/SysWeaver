using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver
{

    [Flags]
    public enum ManagedMessageTypes
    {
        /// <summary>
        /// Plain text message intended for SMS 
        /// </summary>
        Text = 1,

        /// <summary>
        /// Email message subject (plain text)
        /// </summary>
        EmailSubject = 2,

        /// <summary>
        /// Email message body (html or plain text)
        /// </summary>
        EmailBody = 4,

        /// <summary>
        /// Markdown message headline, intended for web display (plain text).
        /// </summary>
        NotificationHeadline = 8,

        /// <summary>
        /// Markdown message body, intended for web display (md converted to html).
        /// </summary>
        NotificationBody = 16,

        /// <summary>
        /// Email message, subject as plain text and body as (html or plain text.
        /// </summary>
        Email = EmailSubject | EmailBody,


        /// <summary>
        /// Markdown message headline (plain text) and body (using markdown), intended for web display 
        /// </summary>
        Notification = NotificationHeadline | NotificationBody,

        /// <summary>
        /// All message types
        /// </summary>
        All = Text | Email | Notification


    }


    /// <summary>
    /// Holds a set of localized text and mail message templates, loaded from a folder with one sub folder per language.
    /// Used for account related messages (ex: by the user manager).
    /// </summary>
    /// <remarks>
    /// For language codes with a region (ex: "en-GB"), the neutral language ("en") is also mapped to the first (alphabetically) regional variant if no neutral folder exists.
    /// </remarks>
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
        /// <param name="required">The requred message types</param>
        /// <param name="optional">The optional message types, data is empty string if they don't exist</param>
        /// <param name="names">The names/keys of messages.
        /// Text (SMS/message) files are names as "[Name]Text.txt", mail fails are named as specified in the htmlMail parameter.
        /// If the name starts with a '*', the data is optional (will return empty strings if not found), note that the '*' is part of the lookup key.</param>
        /// <exception cref="FileNotFoundException">A required message file doesn't exist (checked in the order: mail body, mail subject, text).</exception>
        /// <exception cref="DirectoryNotFoundException">The source folder (the part before any '*') doesn't exist.</exception>
        public ManagedMessages(String sourceFolder, String defaultLang, bool htmlMail, ManagedMessageTypes required, ManagedMessageTypes optional, params String[] names)
            : this(sourceFolder, defaultLang, htmlMail, required, optional, null, names)
        {
        }
        /// <summary>
        /// Holds a bunch of messages, localized, text and mail templates
        /// </summary>
        /// <param name="sourceFolder">The folder where the source files are, should have a subfolder for each language, folder name should match the language iso, ex: "en-US", "en-GB", "es-MX", "es-ES", "de", "sv".
        /// Can use a "*" to define a subfolder pattern, ex: "C:\locale\*\UserManager".</param>
        /// <param name="defaultLang">The default language if the supplied language isn't found</param>
        /// <param name="vars">Set of optional variables, example: "[Amount]"</param>
        /// <param name="htmlMail">True if the folder contain html mail templates.
        /// If true files are named: "[Name]Mail.html" (body) and "[Name]Mail.txt" (subject)".
        /// If false files are named: "[Name]MailBody.txt" (body) and "[Name]Mail.txt" (subject)".
        /// Where [Name] is any of the supplies names.
        /// </param>
        /// <param name="required">The requred message types</param>
        /// <param name="optional">The optional message types, data is empty string if they don't exist</param>
        /// <param name="names">The names/keys of messages.
        /// Text (SMS/message) files are names as "[Name]Text.txt", mail fails are named as specified in the htmlMail parameter.
        /// If the name starts with a '*', the data is optional (will return empty strings if not found), note that the '*' is part of the lookup key.</param>
        /// <exception cref="FileNotFoundException">A required message file doesn't exist (checked in the order: mail body, mail subject, text).</exception>
        /// <exception cref="DirectoryNotFoundException">The source folder (the part before any '*') doesn't exist.</exception>
        public ManagedMessages(String sourceFolder, String defaultLang, bool htmlMail, ManagedMessageTypes required, ManagedMessageTypes optional, IReadOnlySet<String> vars, params String[] names)
        {
            vars = vars.Freeze();
            Vars = vars;
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
                var notification = new Dictionary<String, ManagedMarkDownMessage>(StringComparer.Ordinal);
                var comb = optional | required;
                foreach (var name in names)
                {
                    var isOptional = name.FastStartsWith("*");
                    var n = isOptional ? name.Substring(1) : name;
                    var key = name.FastToLower();
                    if ((comb & ManagedMessageTypes.Email) != 0)
                    {
                        var mm = new ManagedMailMessage(vars)
                        {
                            Body = Path.Combine(f, (string)(n + (htmlMail ? "Mail.html" : "MailBody.txt"))),
                            Subject = Path.Combine(f, (string)(n + "Mail.txt")),
                            IsHtml = htmlMail,
                        };
                        if (!File.Exists(mm.Body))
                            if (((required & ManagedMessageTypes.EmailBody) != 0) && (!isOptional))
                                throw new FileNotFoundException("Localized email body message file " + mm.Body.ToFilename() + " does NOT exist!", mm.Body);
                            else
                                mm.Body = "";
                        if (!File.Exists(mm.Subject))
                            if (((required & ManagedMessageTypes.EmailSubject) != 0)&& (!isOptional))
                                throw new FileNotFoundException("Localized email subject message file " + mm.Subject.ToFilename() + " does NOT exist!", mm.Subject);
                            else
                                mm.Subject = "";
                        mail.Add(key, mm);
                    }
                    if ((comb & ManagedMessageTypes.Text) != 0)
                    {
                        var tm = new ManagedTextMessage(vars)
                        {
                            Body = Path.Combine(f, (string)(n + "Text.txt")),
                        };
                        if (!File.Exists(tm.Body))
                            if (((required & ManagedMessageTypes.Text) != 0)&& (!isOptional))
                                throw new FileNotFoundException("Localized text message file " + tm.Body.ToFilename() + " does NOT exist!", tm.Body);
                            else
                                tm.Body = "";
                        text.Add(key, tm);
                    }
                    if ((comb & ManagedMessageTypes.Notification) != 0)
                    {
                        var nm = new ManagedMarkDownMessage(vars)
                        {
                            Body = Path.Combine(f, (string)(n + "Notification.md")),
                            Headline = Path.Combine(f, (string)(n + "Notification.txt")),
                        };
                        if (!File.Exists(nm.Body))
                            if (((required & ManagedMessageTypes.NotificationBody) != 0)&& (!isOptional))
                                throw new FileNotFoundException("Localized notification body message file " + nm.Body.ToFilename() + " does NOT exist!", nm.Body);
                            else
                                nm.Body = "";
                        if (!File.Exists(nm.Headline))
                            if (((required & ManagedMessageTypes.NotificationHeadline) != 0)&& (!isOptional))
                                throw new FileNotFoundException("Localized notification headline message file " + nm.Headline.ToFilename() + " does NOT exist!", nm.Headline);
                            else
                                nm.Headline = "";
                        notification.Add(key, nm);
                    }
                }
                languages.TryAdd(lang.FastToLower(), new ManagedLanguageMessages(lang, text.Freeze(), mail.Freeze(), notification.Freeze()));
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

        /// <summary>
        /// The additional variables that the templates may use (frozen), may be null.
        /// </summary>
        public readonly IReadOnlySet<String> Vars;

        /// <inheritdoc/>
        public override string ToString() => String.Concat("Default: ", Default, ", languages: ", Languages.Count);

        readonly ConcurrentDictionary<String, ManagedLanguageMessages> Languages = new ConcurrentDictionary<String, ManagedLanguageMessages>(StringComparer.Ordinal);


        /// <summary>
        /// Add a language (typically machine translated at runtime) if it doesn't exist.
        /// </summary>
        /// <param name="language">The language code, should be lower case to be found by <see cref="GetLang(string)"/></param>
        /// <param name="data">The messages</param>
        /// <returns>True if added, false if the language already exists</returns>
        public bool TryAddTranslatedLanguage(String language, ManagedLanguageMessages data)
            => Languages.TryAdd(language, data);

        /// <summary>
        /// The language used if the supplied language isn't found (null if there are no languages)
        /// </summary>
        public readonly ManagedLanguageMessages Default;

 
        /// <summary>
        /// Get the messages for a language, falling back to the neutral language (ex: "en" for "en-US") and then to <see cref="Default"/>.
        /// </summary>
        /// <param name="language">The language code (case insensitive), null returns <see cref="Default"/></param>
        /// <returns>The messages, null only if no languages exist</returns>
        public ManagedLanguageMessages GetLang(String language)
        {
            if (language == null)
                return Default;
            var l = Languages;
            language = language.FastToLower();
            if (l.TryGetValue(language, out var x))
                return x;
            //  The neutral language (the text before the first '-'), looked up without allocating it (the languages use an ordinal comparer)
            var i = language.IndexOf('-');
            if (i >= 0)
                if (l.GetAlternateLookup<ReadOnlySpan<Char>>().TryGetValue(language.AsSpan(0, i), out x))
                    return x;
            return Default;
        }

    }


}
