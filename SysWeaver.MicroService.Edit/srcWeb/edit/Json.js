

async function jsonMain() {
    try {
        const p = getUrlParams();
        const url = p.get("u");
        if (!url) {
            Fail("No url supplied!");
            return;
        }

        const target = document.body;
        const edit = document.createElement("div");
        edit.id = "editor";
        const br = Button.CreateRow();
        //target.appendChild(br);
        target.appendChild(edit);


        const d = new Button("", "Download", "Click to download the file", "../icons/disc.svg", true, async () => {
            d.StartWorking();
            try {
                const name = url.substring(url.lastIndexOf('/') + 1);
                downloadText(name, text);
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
            
        const editor = ace.edit("editor");
        edit.classList.add("ace-sysweaver");
        editor.session.setMode("ace/mode/json");
        editor.setShowPrintMargin(false);



    }
    catch (e) {
        Fail(e.message);
    }
    finally {
        PageLoaded();
    }

}