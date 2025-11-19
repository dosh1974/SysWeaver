





async function logFileMain() {

    try {
        const ps = getUrlParams();
        const url = ps.get("api");
        const target = document.body;
        const p = document.createElement("pre");
        target.appendChild(p);
        async function updateText() {
            p.innerText = await getRequest(url ?? "../Api/logFile/LogFile.txt", false, false, null, async e => e.text());
            window.scrollTo(0, target.scrollHeight);
        }
        const textUpdate = updateText();
        const user = await getRequest("../Api/auth/GetUser");
        const haveAdmin = (!url) && user && user.Succeeded && user.Tokens && ((user.Tokens.indexOf("admin") >= 0) || (user.Tokens.indexOf("debug") >= 0));
        const br = Button.CreateRow();
        target.appendChild(br);
        const d = new Button("", "Download", "Click to download the file", "IconDownload", true, async () => {
            d.StartWorking();
            try {
                const name = url ? url.substring(url.lastIndexOf('/') + 1) : await getRequest("../Api/logFile/DownloadName");
                downloadFile(name, url ?? "../Api/logFile/LogFile.txt");
            }
            catch (e) {
                Fail("Failed to download log file:\n" + e);
            }
            d.StopWorking();
        });
        br.appendChild(d.Element);
        if (haveAdmin) {
            const del = new Button("", "Delete", "Click to delete the log file from the disc on the server", "IconDelete", true, async () => {
                del.StartWorking();
                if (await Confirm("Delete log",
                    "The file will be deleted from the disc on the server.\n\nAre you sure you want to delete it?",
                    "Yes, delete!", "No, keep it!", "IconDelete")) {
                    try {
                        if (await getRequest("../Api/logFile/DeleteLogFile")) {
                            Info("Log file deleted from disc!");
                            await updateText();

                        } else {
                            Fail("Failed to delete log file!");
                        }
                    }
                    catch (e) {
                        Fail("Failed to delete log file:\n" + e);
                    }
                };
                del.StopWorking();
            });
            br.appendChild(del.Element);

        }
        await textUpdate;
        PageLoaded();
    }
    catch (e) {
        PageLoaded();
        Fail(e);
    }
}