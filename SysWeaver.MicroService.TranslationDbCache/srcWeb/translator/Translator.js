

/**
 * @file Page script for translator/Edit.html, the editor for one cached translation of the Translations DB.
 * Edit.html is opened from the "Edit" row action of the Translations DB explorer table (defined in DbInput.cs as
 * "&../translator/Edit.html?id={0}", {0} = row key). It is a standalone page without the app shell.
 * Loaded with a static `<script defer>` after ../common/common.js; the page calls translatorEditMain() from
 * `<body onload>`.
 * Defines one global: translatorEditMain. Nothing else runs at load time.
 * Dependencies (common.js): AddLoading, getUrlParams, sendRequest, Button, Info, Fail, _T / _TF (every user-visible
 * string goes through these with a translator context string, so the page itself is translated).
 * Server API (both Roles.Debug, under Api/translator/): GetTranslation, SetTranslation.
 * Conventions: the DOM is built from custom SysWeaver-* elements (SysWeaver-Page, SysWeaver-Section, SysWeaver-Text,
 * SysWeaver-CenterBlock) styled by Translator.css / theme.css. PageLoaded() is not called.
 */

/**
 * Page entry point (called by `<body onload="translatorEditMain()">`).
 * Shows a loader (AddLoading), reads the URL param `id` (throws "No id supplied!" if missing) and appends a
 * `<SysWeaver-Page class="Wide">` to document.body. Adds a "Translation Key" section with the id, then calls
 * `sendRequest("../Api/translator/GetTranslation", id)` (response: {ServiceName, UserName, IsManual, From, FromName,
 * To, ToName, Text, Context, Translated}; a falsy result throws "No data for the supplied id").
 * Renders, in order: Application (ServiceName, if set); Translator (UserName, when IsManual) or Auth (UserName,
 * when automatic) if UserName is set; Source language (flag ../iso_data/language/<From>, FromName ?? From);
 * Text (bold); Context (if set); Target language (flag for To, ToName ?? To); Translation as an editable
 * `<textarea class="Translation">` holding data.Translated.
 * Below that a `SysWeaver-CenterBlock.Space` holds the "Save translation" Button (icon ../icons/disc.svg, initially
 * disabled). Its click handler makes the textarea read-only, calls StartWorking and sends
 * `sendRequest("../Api/translator/SetTranslation", {Key, OriginalTranslation, NewTranslation})` (OriginalTranslation
 * lets the server detect concurrent changes). A falsy result throws "Couldn't set translation (key no longer exist?)";
 * on success Info("Translation saved!") and data.Translated becomes the new baseline. Finally it re-validates, makes
 * the textarea editable and calls StopWorking. Errors thrown by the click handler are not caught there.
 * ta.oninput is set to the inner Validate. Errors in the main body are shown with Fail(e); the loader is always removed.
 * @returns {Promise<void>} Resolves when the page is built (or an error was shown).
 */
async function translatorEditMain() {
    const removeLoader = AddLoading();
    try {
        const ps = getUrlParams();
        const id = ps.get('id');
        if (!id)
            throw new Error(_TF("No id supplied!", "Error message displayed when there is a missing query parameter"));
        const page = document.createElement("SysWeaver-Page");
        page.classList.add("Wide");
        document.body.appendChild(page);

        /**
         * Inner helper: appends a section heading (`<SysWeaver-Section>`) to the page.
         * @param {string} name - Heading text (set as innerText).
         * @param {string} [title] - Optional tooltip (element title).
         * @returns {HTMLElement} The created SysWeaver-Section element.
         */
        function AddSection(name, title) {
            const s = document.createElement("SysWeaver-Section");
            s.innerText = name;
            if (title)
                s.title = title;
            page.appendChild(s);
            return s;
        }

        /**
         * Inner helper: appends a selectable value block (`<SysWeaver-Text>`) to the page.
         * @param {string} name - Text content (set as innerText).
         * @param {string} [title] - Optional tooltip (element title).
         * @returns {HTMLElement} The created SysWeaver-Text element (the caller adds class "Bold" to the source text).
         */
        function AddText(name, title) {
            const s = document.createElement("SysWeaver-Text");
            s.innerText = name;
            if (title)
                s.title = title;
            page.appendChild(s);
            return s;
        }

        /**
         * Inner helper: appends a `<SysWeaver-Text class="Icon">` block with an image (a language flag) as inline
         * background-image (positioned by the SysWeaver-Text.Icon rule in Translator.css).
         * @param {string} icon - Image URL (e.g. ../iso_data/language/<iso>, without .svg extension).
         * @param {string} name - Text content (set as innerText).
         * @param {string} [title] - Optional tooltip (element title).
         * @returns {void} Nothing (unlike AddSection/AddText).
         */
        function AddImgText(icon, name, title) {
            const s = document.createElement("SysWeaver-Text");
            s.style.backgroundImage = "url('" + icon + "')";
            s.classList.add("Icon");
            s.innerText = name;
            if (title)
                s.title = title;
            page.appendChild(s);
        }

        AddSection(
            _TF("Translation Key", "Title text for a section of text that contains a key (guid) for a translation"),
            _TF("This is the translation key (guid) for this translation", "Tool tip description for a section of text that contains a key (guid) for a translation")
        );
        AddText(id);

        const data = await sendRequest("../Api/translator/GetTranslation", id);
        if (!data)
            throw new Error(_TF("No data for the supplied id", "Error message displayed when a supplied id is unknown to the server"));

        const sn = data.ServiceName;
        if (sn) {
            AddSection(
                _TF("Application", "Title text for a section that displays the application that performed some action"),
                _TF("The name of the application that requested this translation (first)", "Tool tip description for a section that displays the application that performed some action")
            );
            AddText(sn);
        }

        const un = data.UserName;
        if (un) {
            if (data.IsManual) {
                AddSection(
                    _TF("Translator", "Title text for a section that displays who translated some text"),
                    _TF("The name of the user that manually translated this text", "Tool tip description for a section that displays who translated some text")
                );
                AddText(un);
            } else {
                AddSection(
                    _TF("Auth", "Title text for a section that displays the name of the authentication used"),
                    _TF("The name of the authentication user / API-key used for the automatic translation", "Tool tip description for a section that displays the name of the authentication used")
                );
                AddText(un);
            }
        }

        AddSection(
            _TF("Source language", "Title text for a section that displays the language used of a piece of text that will be translated to some other language"),
            _TF("The language that the original text (and prompt) was written in, typically 'en', 'en-US' or 'en-GB'", "Tool tip description for a section that displays the language used of a piece of text that will be translated to some other language")
        );
        AddImgText("../iso_data/language/" + data.From, data.FromName ?? data.From,
            _T('Language with the ISO code "{0}"', data.From, "Tool tip description of a language name.{0} is replaced with the ISO code of the language")
        );

        AddSection(
            _TF("Text", "Title text for a section that displays a piece of text, that will be translated to some other language"),
            _TF("The text to translate", "Tool tip description for a section that displays a piece of text, that will be translated to some other language")
        );
        AddText(data.Text).classList.add("Bold");

        if (data.Context) {
            AddSection(
                _TF("Context", "Title text for a section that displays the context that will be used when translating some text"),
                _TF("An optional context that is used when translating", "Tool tip description for a section that displays the context that will be used when translating some text")
                );
            AddText(data.Context);
        }

        AddSection(
            _TF("Target language", "Title text for a section that displays the language to translate some piece of text into"),
            _TF("The language that the translation should have", "Tool tip description for a section that displays the language to translate some piece of text into")
        );
        AddImgText("../iso_data/language/" + data.To, data.ToName ?? data.To,
            _T('Language with the ISO code "{0}"', data.To, "Tool tip description of a language name.{0} is replaced with the ISO code of the language")
        );

        AddSection(
            _TF("Translation", "Title text for a section that displays a translated text"),
            _TF("The translated text", "Tool tip description for a section that displays a translated text")
        );

        const ta = document.createElement("textarea");
        ta.classList.add("Translation");
        ta.value = data.Translated;
        page.appendChild(ta);

        const br = document.createElement("SysWeaver-CenterBlock");
        page.appendChild(br);
        br.classList.add("Space");

        const changeButton = new Button(null,
            _TF("Save translation", "Text on a button that when pressed will save an updated version of a text translation"),
            _TF("Click to save the new translation", "Tool tip description for a button that when pressed will save an updated version of a text translation"),
            "../icons/disc.svg",
            false,
            async () => {
                ta.readOnly = true;
                changeButton.StartWorking();
                try {
                    const newData = ta.value;
                    const res = await sendRequest("../Api/translator/SetTranslation",
                        {
                            Key: id,
                            OriginalTranslation: data.Translated,
                            NewTranslation: newData,
                                
                        });
                    if (!res)
                        throw Error(_TF("Couldn't set translation (key no longer exist?)", "Error message displayed when an API request failed"));
                    Info(_TF("Translation saved!", "Message displayed when a new translation was successfully saved"));
                    data.Translated = newData;
                }
                finally {
                    Validate();
                    ta.readOnly = false;
                    changeButton.StopWorking();
                }
            });
        br.appendChild(changeButton.Element);

        /**
         * Inner helper: enables the save button (changeButton.SetEnabled) only when the textarea is non-empty and
         * differs from the last saved value (data.Translated). Used as ta.oninput and after each save attempt.
         * @returns {void}
         */
        function Validate() {
            const v = ta.value;
            const ok = (v.length > 0) && (v !== data.Translated);
            changeButton.SetEnabled(ok);
        }

        ta.oninput = Validate;
    }
    catch (e) {
        Fail(e);
    }
    finally {
        removeLoader();
    }

}
