

async function jsonMain() {
    try {
        const p = getUrlParams();
        const url = p.get("r");
        if (!url) {
            Fail(TF("No read url supplied!", "Error message displayed when a required url parameter wasn't supplied") + " (r=..)");
            return;
        }
        const del = p.get("d");
        const update = p.get("u");
        const download = (p.get("download") !== "false");
        let type = p.get("t");

        const readOnly = !update;
        const name = url.substring(url.lastIndexOf('/') + 1);
        document.title = name;
        const target = document.body;

        if (!type) {
            type = "plain_text";
            const extp = name.lastIndexOf('.');
            if (extp > 0) {
                const m = new Map();
                m.set("c", "c_cpp");
                m.set("c++", "c_cpp");
                m.set("cpp", "c_cpp");
                m.set("h", "c_cpp");
                m.set("hpp", "c_cpp");
                m.set("cs", "csharp");
                m.set("css", "css");
                m.set("csv", "csv");
                m.set("glsl", "glsl");
                m.set("htm", "html");
                m.set("html", "html");
                m.set("js", "javascript");
                m.set("json", "json");
                m.set("md", "markdown");
                m.set("txt", "plain_text");
                m.set("sql", "mysql");
                m.set("svg", "svg");
                m.set("xml", "xml");
                m.set("config", "xml");
                const ext = name.substring(extp + 1).toLowerCase();
                type = m.get(ext) ?? type;
            }
        }
        console.log("Type: " + type);
        const br = Button.CreateRow();
        target.appendChild(br);
        const edit = document.createElement("SysWeaver-AceEditor");
        target.appendChild(edit);
        let downloadButton = null;
        let saveButton = null;
        let deleteButton = null;
        if (download){
            downloadButton = new Button("", _TF("Download", "Text on a button that when clicked will download the file"), _TF("Click to download the file", "Tool tip description on a button that when clicked will download the file"), "../icons/disc.svg", true, async () => {
                downloadButton.StartWorking();
                try {
                    downloadText(name, editor.session.getValue());
                }
                catch (e) {
                    Fail(_TF("Failed to download the file", "Error message displayed when a file download failed") + ":\n" + e);
                }
                downloadButton.StopWorking();
            });
            br.appendChild(downloadButton.Element);
        }
        if (update) {
            saveButton = new Button("", _TF("Save", "Text on a button that when clicked will save the data"), _TF("Click to save your changes", "Tool tip description on a button that when clicked will save the data"), "../icons/disc.svg", true, async () => {
                saveButton.StartWorking();
                if (deleteButton)
                    deleteButton.Disable();
                try {
                    await sendRequest(update, {
                        Url: url,
                        Name: name,
                        Content: editor.session.getValue(),
                    });
                }
                catch (e) {
                    Fail(_TF("Failed to save the file", "Error message displayed when the file failed to be saved") + ":\n" + e);
                }
                if (deleteButton)
                    deleteButton.Enable();
                saveButton.StopWorking();
            });
            br.appendChild(saveButton.Element);
        }
        if (del) {
            deleteButton = new Button("", _TF("Delete", "Text on a button that when clicked will delete the file from the server"), _TF("Click to delete the file", "Tool tip description on a button that when clicked will delete the file from the server"), "../icons/close.svg", true, async () => {
                deleteButton.StartWorking();
                if (saveButton)
                    saveButton.Disable();
                try {
                    const m = {
                        Url: url,
                        Name: name,
                    };
                    await sendRequest(del, m);
                    InterOp.Post("FileClose", m);
                    if (downloadButton)
                        downloadButton.Disable();
                    deleteButton.StopWorking();
                    deleteButton.Disable();
                    return;
                }
                catch (e) {
                    Fail(_TF("Failed to delete the file", "Error message displayed when the file failed to be deleted") + ":\n" + e);
                }
                if (saveButton)
                    saveButton.Enable();
                deleteButton.StopWorking();
            });
            br.appendChild(deleteButton.Element);
        }




        let text = await getRequest(url, false, false, null, r => r.text());
        try {
            //text = JSON.stringify(JSON.parse(text), null, "\t");
        }
        catch
        {
        }
        edit.textContent = text;

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
        editor.session.setMode("ace/mode/" + type);

        //var beautify = ace.require("ace/ext/beautify"); // get reference to extension
        //beautify.beautify(editor.session);

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