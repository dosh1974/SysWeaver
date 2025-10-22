

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

        function AddSection(name, title) {
            const s = document.createElement("SysWeaver-Section");
            s.innerText = name;
            if (title)
                s.title = title;
            page.appendChild(s);
            return s;
        }

        function AddText(name, title) {
            const s = document.createElement("SysWeaver-Text");
            s.innerText = name;
            if (title)
                s.title = title;
            page.appendChild(s);
            return s;
        }

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
