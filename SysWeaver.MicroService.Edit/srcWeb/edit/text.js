

async function textMain() {
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
        const scrollToEnd = (p.get("scrolltoend") === "true");

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
                // Should be synched with the TextExtensions in TypeService.cs
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
                m.set("log", "plain_text");
                m.set("cfg", "plain_text");
                m.set("bat", "batchfile");
                m.set("cmd", "batchfile");
                m.set("sql", "mysql");
                m.set("svg", "svg");
                m.set("xml", "xml");
                m.set("config", "xml");
                m.set("csproj", "xml");
                const ext = name.substring(extp + 1).toLowerCase();
                type = m.get(ext) ?? type;
            }
        }
        console.log("Type: " + type);

        const bmap = new Map();
        /*
        bmap.set("json", x => vkbeautify.json(x, "\t"));
        bmap.set("json5", x => vkbeautify.json(x, "\t"));
        bmap.set("css", x => vkbeautify.css(x, "\t"));
        bmap.set("mysql", x => vkbeautify.sql(x, "\t"));
        bmap.set("xml", x => vkbeautify.xml(x, "\t"));
        bmap.set("html", x => vkbeautify.xml(x, "\t"));
        */


        bmap.set("json", x => prettier.format(x, { parser: "json5", plugins: prettierPlugins, useTabs: true }));
        bmap.set("json5", x => prettier.format(x, { parser: "json5", plugins: prettierPlugins, useTabs: true }));
        bmap.set("css", x => prettier.format(x, { parser: "css", plugins: prettierPlugins, useTabs: true }));
        bmap.set("html", x => prettier.format(x, { parser: "html", plugins: prettierPlugins, useTabs: true }));
        bmap.set("markdown", x => prettier.format(x, { parser: "markdown", plugins: prettierPlugins, useTabs: true }));
        bmap.set("javascript", x => prettier.format(x, { parser: "typescript", plugins: prettierPlugins, useTabs: true }));
        bmap.set("xml", x => prettier.format(x, { parser: "xml", plugins: prettierPlugins, useTabs: true }));

        const beautify = bmap.get(type);



        const br = Button.CreateRow();
        target.appendChild(br);
        const edit = document.createElement("SysWeaver-AceEditor");
        target.appendChild(edit);
        let downloadButton = null;
        let saveButton = null;
        let deleteButton = null;


        if (beautify) {
            beautifyButton = new Button("", _TF("Beautify", "Text on a button that when clicked will beatify some source code text"),
                _TF("Click to beautify the text", "Tool tip description on a button that when clicked will beatify some source code text"), "../icons/disc.svg", true, async () => {
                    beautifyButton.StartWorking();
                    try {
                        text = await beautify(editor.session.getValue());
                        editor.session.setValue(text);
                    }
                    catch (e) {
                        Fail(e.message);
                    }
                    beautifyButton.StopWorking();
            });
            br.appendChild(beautifyButton.Element);
        }

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
                    const m = {
                        Url: url,
                        Name: name,
                        Content: editor.session.getValue(),
                    };
                    await sendRequest(update, m);
                    InterOp.Post("FileSaved", m);
                    Info(_T("File \"{0}\" saved!", name, "An information message that is displayed whan a file was saved. {0} is replaced by the name of the file that was saved"));
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
                if (await Confirm(
                    _TF("Delete file", "Title of a pop-up dialog that confirms that the user wants to delete a file"),
                    _T("The file \"{0}\" will be deleted!", name, "Text of of a pop-up dialog that confirms that the user wants to delete a file. {0} is replaced by the name of the file") + "\n\n" +
                    _TF("Are you sure that you want to delete the file?", "Text of of a pop-up dialog that confirms that the user wants to delete a file"),
                    _TF("Yes, delete", "Text of a button that when clicked will delete a file"),
                    _TF("No, keep it", "Text of a button that when clicked will leave a file as is, as opposed to deleting it"),
                    "../icons/close.svg",
                    "../icons/fav_on.svg",
                    _TF("Click to delete the file permamently", "Tool tip description of a button that when clicked will delete a file"),
                    _TF("Click to keep the file as is", "Tool tip description of a button that when clicked will leave a file as is, as opposed to deleting it")
                )) {
                    if (saveButton)
                        saveButton.Disable();
                    try {
                        const m = {
                            Url: url,
                            Name: name,
                        };
                        await sendRequest(del, m);
                        InterOp.Post("FileDeleted", m);
                        if (downloadButton)
                            downloadButton.Disable();
                        deleteButton.StopWorking();
                        deleteButton.Disable();
                        edit.remove();
                        Info(_T("File \"{0}\" deleted!", name, "An information message that is displayed whan a file was deleted. {0} is replaced by the name of the file that was deleted"));
                        return;
                    }
                    catch (e) {
                        Fail(_TF("Failed to delete the file", "Error message displayed when the file failed to be deleted") + ":\n" + e);
                    }
                    if (saveButton)
                        saveButton.Enable();
                }
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
        editor.renderer.setScrollMargin(2, 2);
        if (scrollToEnd) {
            const lastLine = 10000000;
            editor.resize(true);
            editor.scrollToLine(lastLine, false, false);
            editor.gotoLine(lastLine, 0, false);
        }

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
        document.body.innerText = "";
        Fail(e.message, 30000, true);
    }
    finally {
        PageLoaded();
    }

}