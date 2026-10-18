





/**
 * @file Page script for logFile/logfile.html (the "Debug / Log file" menu page registered by FileLogService).
 * Loaded with a static `<script defer>` after ../common/common.js and ../app/application.js; the page calls
 * logFileMain() from `<body onload>`.
 * Defines one global: logFileMain. Nothing else runs at load time.
 * Dependencies (common.js): getUrlParams, sendRequest, getRequest, Button, downloadFile, Confirm, Info, Fail, PageLoaded.
 * Conventions: the page is primarily a redirector to the Edit project's generic text viewer (../edit/text.html with
 * URL params r = read URL, d = delete URL, scrollToEnd=true). Only when that service is not registered does it render
 * its own minimal fallback viewer (a <pre> plus Download / Delete buttons).
 * Optional URL param `api`: a different text resource URL to show instead of ../Api/logFile/LogFile.txt
 * (deleting is then never offered).
 */

/**
 * Page entry point (called by `<body onload="logFileMain()">`).
 * 1. Reads the optional URL param `api`.
 * 2. If `sendRequest("../Api/HaveService", "SysWeaver.MicroService.TypeService")` is truthy, redirects with
 *    location.replace to ../edit/text.html?r=<api>&scrollToEnd=true, or (without `api`) to
 *    ../edit/text.html?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true, and returns.
 * 3. Otherwise appends a <pre> to document.body and starts loading the text into it (inner updateText), fetches
 *    ../Api/auth/GetUser meanwhile, and appends a Button.CreateRow() row (SysWeaver-CenterBlock, fixed at the top via
 *    logfile.css) containing:
 *    - Download (IconDownload): file name = last path segment of `api`, or ../Api/logFile/DownloadName;
 *      calls downloadFile(name, url). Errors are reported with Fail.
 *    - Delete (IconDelete), only when `api` is absent, user.Succeeded is set and user.Tokens contains "admin" or
 *      "debug" (client-side hiding only; the server enforces Roles.Admin): Confirm dialog, then
 *      ../Api/logFile/DeleteLogFile; on a truthy result Info(...) and a reload of the text, otherwise Fail(...).
 *    Both buttons use StartWorking/StopWorking while busy.
 * 4. Awaits the first text load and calls PageLoaded(). On any exception calls PageLoaded() and then Fail(e).
 * @returns {Promise<void>} Resolves when the page is loaded (or the redirect was issued).
 */
async function logFileMain() {

    const ps = getUrlParams();
    const url = ps.get("api");
    if (await sendRequest("../Api/HaveService", "SysWeaver.MicroService.TypeService"))
    {
        if (url)
            location.replace("../edit/text.html?r=" + url + "&scrollToEnd=true");
        else
            location.replace("../edit/text.html?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true");
        return;
    }
    try {
        const target = document.body;
        const p = document.createElement("pre");
        target.appendChild(p);
        /**
         * Inner function of logFileMain: (re)loads the text into the <pre>.
         * Sets p.innerText to the response text of `api` (or ../Api/logFile/LogFile.txt) fetched with
         * getRequest(..., async e => e.text()), then scrolls the window to the end (document.body.scrollHeight).
         * Called once at start and again after a successful delete.
         * @returns {Promise<void>} Resolves when the text is shown; rejects on request failure.
         */
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