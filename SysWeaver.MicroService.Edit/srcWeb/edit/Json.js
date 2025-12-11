

async function jsonMain() {
    try {
        const p = getUrlParams();
        const url = p.get("u");
        if (!url) {
            Fail("No url supplied!");
            return;
        }

        const name = url.substring(url.lastIndexOf('/') + 1);
        document.title = name;
        const target = document.body;


        const br = Button.CreateRow();
        target.appendChild(br);
        const edit = document.createElement("SysWeaver-AceEditor");
        target.appendChild(edit);


        const d = new Button("", "Download", "Click to download the file", "../icons/disc.svg", true, async () => {
            d.StartWorking();
            try {
                downloadText(name, editor.session.getValue());
            }
            catch (e) {
                Fail("Failed to download the file:\n" + e);
            }
            d.StopWorking();
        });
        br.appendChild(d.Element);


        let text = await getRequest(url, false, false, null, r => r.text());
        try {
            text = JSON.stringify(JSON.parse(text), null, "\t");
        }
        catch
        {
        }
        edit.textContent = text;
        const readOnly = false;

        const options = {   
            readOnly: readOnly,
            animatedScroll: true,
            displayIndentGuides: true,
            enableAutoIndent: true,
            firstLineNumber: 1,
            highlightGutterLine: true,
            showFoldWidgets: true,
            showFoldedAnnotations: true,
            showLineNumbers: true,
            enableBasicAutocompletion: true,
            enableLiveAutocompletion: true,
            useSvgGutterIcons: true,
            useWorker: !readOnly,
        };

        const editor = ace.edit(edit, options);
        edit.classList.add("ace-sysweaver");
        editor.session.setMode("ace/mode/json");

        new MutationObserver(mut => {
            const c = target.children;
            const cl = c.length;
            for (let i = 0; i < cl; ++i) {
                const cc = c[i];
                if (cc.tagName !== "DIV")
                    continue;
                if (!cc.classList.contains("ace_editor"))
                    if (!cc.classList.contains("ace_tooltip"))
                        continue;
                cc.classList.add("ace-sysweaver");
            }
        }).observe(edit.parentElement, { childList: true})

        //editor.session.setMode("ace/mode/plain_text");
        editor.setShowPrintMargin(false);



    }
    catch (e) {
        Fail(e.message);
    }
    finally {
        PageLoaded();
    }

}