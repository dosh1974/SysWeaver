/**
 * @file logfile.js - the "Debug / Log file" page (logFile/logfile.html, entry `logFileMain`; menu item registered by
 * FileLogService, auth debug,ops,admin). Also opened by ServerManager serviceInfo.html ("View" of a config file) with
 * `?api=<text url>`.
 * Classic script loaded with `<script defer>` after ../sw/core.js and ../sw/ui.js; started from `<body onload>`.
 *
 * Behavior:
 * - When the Edit project's TypeService is hosted (`Api/HaveService "SysWeaver.MicroService.TypeService"`), the page
 *   is a redirect (Sw.Page.Replace: no history entry, so Back skips it, and the app shell keeps the menu url) to the
 *   generic text viewer `../edit/text.html?r=<api>&scrollToEnd=true`, or for the log file
 *   `../edit/text.html?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true` (the original url; the viewer
 *   recognizes the log by its lines and shows it in its log mode with the level colors; no request before the redirect).
 * - Otherwise a minimal viewer in the Design v2 anatomy (a fill page: compact page header with the actions Download, Reload and
 *   Delete (only for the log file and users who may call DeleteLogFile: Sw.Access.CanCall, the server enforces the role), the text in a card that
 *   takes the rest of the height, a monospace block scrolled to the end).
 *
 * Url parameter: `api` = optional alternative text url (relative to logFile/) shown instead of the log file.
 * Globals defined: `logFileMain`.
 */

/** @type {string} Url of the log file text. */
const LogFileUrl = "../Api/logFile/LogFile.txt";

/** @type {string} Api deleting the log file. */
const LogFileDeleteUrl = "../Api/logFile/DeleteLogFile";
/** The one time delete token of the log file with the facts for the confirmation (the server asks for it; older servers: none). */
const LogFileTokenUrl = "../Api/logFile/GetDeleteLogFileToken";

/**
 * Entry point of logfile.html.
 * @returns {Promise<void>}
 */
function logFileMain() {
    return Sw.Page.Main(async () => {
        //  Loading phase handled here: a redirect must not signal "loaded" (the app shell would reveal this page)
        const remove = Sw.Page.Loading();
        const api = Sw.Url.Get("api");
        //  Security: the text url must be on this site (a crafted link must not show another site's text as ours)
        if (api && !Sw.Url.SafeSameOrigin(api)) {
            const msg = _TF("This log cannot be shown: its address (the api parameter) must be on this site.", "Error message on the log file page when the api url parameter points to another web site (not allowed)");
            Sw.PageLayout({ Type: "read", Title: _TF("Log file", "Title of the log file page") }).Add(Sw.StateView({ Kind: "error", Title: _TF("This text cannot be shown", "Title of the error on the log file page when the api url parameter points to another web site"), Text: msg }));
            remove();
            return;
        }
        let have = false;
        try {
            have = await Sw.Net.Send("../Api/HaveService", "SysWeaver.MicroService.TypeService");
        }
        catch (e) {
            console.warn("logfile: HaveService failed: " + e.message);
        }
        if (have) {
            //  The same viewer url as the original page (no other request first; the text viewer names the page after the file)
            Sw.Page.Replace(api
                ? "../edit/text.html?r=" + encodeURIComponent(api) + "&scrollToEnd=true"
                : "../edit/text.html?r=" + LogFileUrl + "&d=" + LogFileDeleteUrl + "&scrollToEnd=true");
            return;
        }
        try {
            await logFileViewer(api);
        }
        finally {
            remove();
        }
    });
}

/**
 * Build the fallback viewer.
 * @param {?string} api - Alternative text url, null for the log file.
 * @returns {Promise<void>}
 */
async function logFileViewer(api) {
    const url = api || LogFileUrl;
    const text = Sw.el("pre.lf-text.sw-selectable", { attrs: { tabindex: "0", "aria-label": _TF("Log text", "Accessible name of the log text") } });
    //  Design v2 fill page: a compact page header (the menu name; the actions on the right) and the text in a card that takes the height
    const page = Sw.PageLayout({
        Type: "fill",
        Title: api ? _TF("Text file", "Title of the log viewer page when it shows another text file (the api url parameter)") : _TF("Log file", "Title of the log file page"),
        Description: api ? "" : _TF("The log of this service, newest lines at the end.", "Description of the log file page"),
    });
    page.Add(Sw.el("div.sw-card.sw-fill.lf-card", null, text));

    /** Load the text and scroll to its end. */
    const load = async () => {
        text.textContent = await Sw.Net.Get(url, { Text: true });
        text.scrollTop = text.scrollHeight;
    };
    const loading = load();

    //  every user who may call DeleteLogFile gets the button (no token names in pages; the server enforces the role)
    const canDelete = !api && await Sw.Access.CanCall(Sw.Url.Root + "Api/logFile/DeleteLogFile") !== false;

    const buttons = [
        new Sw.Button({
            Text: _TF("Download", "Text of a button that downloads a file"),
            Title: _TF("Download the file", "Tool tip of a button that downloads a file"),
            Icon: "download",
            OnClick: async () => {
                try {
                    const name = api ? Sw.Url.FileName(api) : await Sw.Net.Get("../Api/logFile/DownloadName");
                    Sw.Net.Download(url, name);
                }
                catch (e) {
                    Sw.Fail(_TF("Failed to download log file:", "Error message when downloading a log file failed") + "\n" + e.message);
                }
            },
        }),
        new Sw.Button({
            Text: _TF("Reload", "Text of a button that reloads a text"),
            Title: _TF("Load the latest text", "Tool tip of a button that reloads a text"),
            Icon: "refresh",
            OnClick: load,
        }),
    ];
    if (canDelete) {
        //  The log is the server's history: the service hands out a one time delete token with the facts and what is lost; the
        //  confirmation shows them (a plain confirmation, like the original page). Sw.Danger.Run does it the one safe way: the token first
        //  (Prepare: the button busy at once; a failure stops with the reason, never a weaker question), then the delete with the
        //  busy state and one result. The click itself is no work (it only asks): no promise returned.
        let delBtn = null;
        const deleteLog = () => Sw.Danger.Run({
            Action: _TF("Delete", "Button text that confirms deleting the log file"),
            Title: _TF("Delete the log file?", "Title of the dialog that confirms deleting the server's log file"),
            ItemLabel: _TF("File:", "Label before the file name in a confirmation dialog of the text editor (ex: File: LogFile.txt)"),
            Text: _TF("The log is the history of what happened on this server (sign ins, user changes, errors); it can't be restored.", "Text of the dialog that confirms deleting the server's log file"),
            //  (a plain confirmation, like the original page: no word to type)
            OkIcon: "trash",
            Button: delBtn,
            Prepare: async signal => {
                const info = await Sw.Net.Send(LogFileTokenUrl, null, { Post: true, Signal: signal });
                if (!info || !info.Token)
                    throw new Error(_TF("the service gave no delete token", "Reason when the log file delete could not be prepared: the service answered without a one time delete token"));
                const facts = [];
                if (info.Size >= 0 && Sw.Format.Bytes)
                    facts.push({ Label: _TF("Size", "Label of the file size in the confirmation that deletes the log file"), Value: Sw.Format.Bytes(info.Size) });
                if (info.LastWrite)
                    facts.push({ Label: _TF("Last written", "Label of the time of the last write in the confirmation that deletes the log file"), Value: new Date(info.LastWrite).toLocaleString() });
                return { Token: info.Token, Item: info.Name || "", Facts: facts, Text: info.Warning || undefined };
            },
            Run: async (signal, prep) => {
                if (!(await Sw.Net.Send(LogFileDeleteUrl, { Token: prep.Token }, { Post: true })))
                    throw new Error(_TF("the service did not delete the file", "Reason when deleting the log file failed without an error from the service"));
                await load();
            },
            Done: _TF("Log file deleted from disc!", "Message when the log file was deleted"),
        });
        delBtn = new Sw.Button({
            Text: _TF("Delete", "Text of a button that deletes the log file"),
            Title: _TF("Delete the log file from the disc on the server", "Tool tip of a button that deletes the log file"),
            Icon: "trash",
            Kind: "danger",
            OnClick: () => {
                deleteLog();
            },
        });
        buttons.push(delBtn);
    }
    page.Header.SetActions(buttons);
    try {
        await loading;
    }
    catch (e) {
        Sw.Fail(e.message || String(e));
    }
}