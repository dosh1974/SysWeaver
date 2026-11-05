/**
 * @file Translator.js - the editor of one cached translation of the Translations DB (translator/Edit.html, entry
 * `translatorEditMain`). Opened from the "Edit" row action of the Translations DB table (DbInput.cs:
 * `&../translator/Edit.html?id={0}`, same tab).
 * Classic script loaded with `<script defer>` after ../sw/core.js and ../sw/ui.js; started from `<body onload>`.
 *
 * Design v2 form page (Design v2, PORTING.md B10 / PORTING.md B14 / PORTING.md B11) built from the shared ui components: a page header (Sw.PageHeader), a facts
 * strip with the read only facts (Sw.Facts: key, application, translator / auth, source and target language with flags), a card
 * with the source text and its context, and a card with the editable translation and its form footer (Save, the primary action,
 * right aligned). Errors are a state view (Sw.StateView) in the page plus a toast.
 *
 * Url parameter: `id` = the translation key (required).
 * Server API (Roles.Debug): `Api/translator/GetTranslation` (id -> {ServiceName, UserName, IsManual, From, FromName,
 * To, ToName, Text, Context, Translated}), `Api/translator/SetTranslation` ({Key, OriginalTranslation,
 * NewTranslation} -> bool; the original text gives optimistic concurrency).
 * All labels are translated (with context strings), so the page itself can be translated.
 *
 * Globals defined: `translatorEditMain`.
 */

/**
 * A language value for the facts strip: the flag and the name.
 * @param {string} iso - The ISO code (the flag file name).
 * @param {string} name - The language name.
 * @returns {HTMLElement} The value.
 */
function trLanguage(iso, name) {
    return Sw.el("span.tr-lang", null,
        iso ? Sw.el("img.tr-flag", { src: "../iso_data/language/" + iso, alt: "", title: _T('Language with the ISO code "{0}"', iso, "Tool tip description of a language name.{0} is replaced with the ISO code of the language") }) : null,
        Sw.el("span", { text: name ?? iso ?? "" }));
}

/**
 * A titled read only text block inside a card (the source text, the context).
 * @param {string} title - Label.
 * @param {string} tip - Label tool tip.
 * @param {string} text - The text.
 * @param {boolean} [bold] - Bold text (the text to translate).
 * @returns {HTMLElement} The block.
 */
function trBlock(title, tip, text, bold) {
    return Sw.el("div.tr-block", null,
        Sw.el("h3.tr-label", { text: title, title: tip }),
        Sw.el("div.tr-text.sw-selectable" + (bold ? ".tr-bold" : ""), { text: text ?? "" }));
}

/**
 * Entry point of Edit.html.
 * @returns {Promise<void>}
 */
function translatorEditMain() {
    return Sw.Page.Main(async () => {
        const page = Sw.PageLayout({
            Main: ".tr-page",
            Type: "form",
            //  "Edit", as the original page (its document title; the shell top bar shows it)
            Title: _TF("Edit", "Title of the page that shows a cached translation and lets a translator correct it"),
            Description: _TF("Check the source text and correct the translation. Ctrl+Enter saves.", "One line description below the title of the translation editor page"),
            //  A page about one record: keep its own title even when a menu item opens this page
            MenuTitle: false,
        });
        //  The top bar shows the document title: the page title ("Edit" as Edit.html, translated)
        document.title = page.Header.TitleE.textContent;
        /**
         * Show an error state in the page (the toast is shown too).
         * @param {string} title What failed.
         * @param {string} [details] Technical text.
         */
        const failed = (title, details) => {
            Sw.Fail(details ? title + "\n" + details : title);
            page.Add(Sw.Card({ Body: Sw.StateView({ Kind: "error", Title: title, Details: details || null }) }));
        };
        const id = Sw.Url.Get("id");
        if (!id) {
            failed(_TF("No id supplied!", "Error message displayed when there is a missing query parameter"));
            return;
        }
        const facts = Sw.Facts({ Label: _TF("Translation facts", "Accessible name of the facts strip of the translation editor") });
        facts.Add({
            Label: _TF("Translation Key", "Title text for a section of text that contains a key (guid) for a translation"),
            Title: _TF("This is the translation key (guid) for this translation", "Tool tip description for a section of text that contains a key (guid) for a translation"),
            Value: id, Mono: true,
        });
        page.Add(facts);

        let data = null;
        try {
            data = await Sw.Net.Send("../Api/translator/GetTranslation", id);
        }
        catch (e) {
            failed(_TF("The translation could not be loaded", "Title of the error state of the translation editor when the translation failed to load"), e.message || String(e));
            return;
        }
        if (!data) {
            failed(_TF("No data for the supplied id", "Error message displayed when a supplied id is unknown to the server"));
            return;
        }
        if (data.ServiceName)
            facts.Add({
                Label: _TF("Application", "Title text for a section that displays the application that performed some action"),
                Title: _TF("The name of the application that requested this translation (first)", "Tool tip description for a section that displays the application that performed some action"),
                Value: data.ServiceName,
            });
        if (data.UserName) {
            if (data.IsManual)
                facts.Add({
                    Label: _TF("Translator", "Title text for a section that displays who translated some text"),
                    Title: _TF("The name of the user that manually translated this text", "Tool tip description for a section that displays who translated some text"),
                    Value: data.UserName,
                });
            else
                facts.Add({
                    Label: _TF("Auth", "Title text for a section that displays the name of the authentication used"),
                    Title: _TF("The name of the authentication user / API-key used for the automatic translation", "Tool tip description for a section that displays the name of the authentication used"),
                    Value: data.UserName,
                });
        }
        facts.Add({
            Label: _TF("Source language", "Title text for a section that displays the language used of a piece of text that will be translated to some other language"),
            Title: _TF("The language that the original text (and prompt) was written in, typically 'en', 'en-US' or 'en-GB'", "Tool tip description for a section that displays the language used of a piece of text that will be translated to some other language"),
            Value: trLanguage(data.From, data.FromName ?? data.From),
        });
        facts.Add({
            Label: _TF("Target language", "Title text for a section that displays the language to translate some piece of text into"),
            Title: _TF("The language that the translation should have", "Tool tip description for a section that displays the language to translate some piece of text into"),
            Value: trLanguage(data.To, data.ToName ?? data.To),
        });

        //  The source: the text and its optional context
        const source = Sw.Card({ Span: 6, Title: _TF("Source", "Title of the card with the text to translate (and its context) in the translation editor") });
        source.SetBody([
            trBlock(_TF("Text", "Title text for a section that displays a piece of text, that will be translated to some other language"),
                _TF("The text to translate", "Tool tip description for a section that displays a piece of text, that will be translated to some other language"),
                data.Text, true),
            data.Context ? trBlock(_TF("Context", "Title text for a section that displays the context that will be used when translating some text"),
                _TF("An optional context that is used when translating", "Tool tip description for a section that displays the context that will be used when translating some text"),
                data.Context) : null,
        ]);

        //  The translation: the editable text and the form footer (Save, primary, right aligned)
        const label = _TF("Translation", "Title text for a section that displays a translated text");
        const ta = Sw.el("textarea.tr-translation", { value: data.Translated ?? "", attrs: { "aria-label": label, rows: "4" } });
        const save = new Sw.Button({
            Text: _TF("Save translation", "Text on a button that when pressed will save an updated version of a text translation"),
            Title: _TF("Save the new translation", "Tool tip description for a button that when pressed will save an updated version of a text translation"),
            Icon: "save",
            Kind: "primary",
            Disabled: _TF("Change the translation first", "Why the save button of a translation is disabled: the text is the saved one"),
            OnClick: async () => {
                ta.readOnly = true;
                try {
                    const newText = ta.value;
                    const res = await Sw.Net.Send("../Api/translator/SetTranslation", {
                        Key: id,
                        OriginalTranslation: data.Translated,
                        NewTranslation: newText,
                    });
                    if (!res) {
                        Sw.Fail(_TF("Couldn't set translation (key no longer exist?)", "Error message displayed when an API request failed"));
                        return;
                    }
                    Sw.Info(_TF("Translation saved!", "Message displayed when a new translation was successfully saved"));
                    data.Translated = newText;
                }
                finally {
                    ta.readOnly = false;
                    validate();
                }
            },
        });
        const card = Sw.Card({ Title: label, Class: "tr-edit", Span: 6 });
        card.SetBody([ta, Sw.el("div.tr-buttons", null, save.E)]);
        page.Add(Sw.Grid({ Content: [source, card] }));

        /** Enable Save only for a non empty text that differs from the last saved one. */
        const validate = () => save.SetDisabled(!(ta.value.length > 0 && ta.value !== data.Translated), ta.value.length > 0
            ? _TF("Change the translation first", "Why the save button of a translation is disabled: the text is the saved one")
            : _TF("Enter a translation first", "Why the save button of a translation is disabled: the text is empty"));
        ta.addEventListener("input", validate);
        //  Ctrl+Enter (Cmd+Enter) saves
        ta.addEventListener("keydown", ev => {
            if (ev.key === "Enter" && (ev.ctrlKey || ev.metaKey) && !save.E.disabled) {
                ev.preventDefault();
                save.Click();
            }
        });
    }, { Loading: true });
}
