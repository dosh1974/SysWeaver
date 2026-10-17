/**
 * @file Shared script of the ServerManager "Server" page (server.html, entry `serverMain`) and of the
 * metric detail page (server_metrics.html, entry `serverMetricsMain`).
 * Loaded with a static `<script defer>` tag (server.html: after common.js and app/application.js;
 * server_metrics.html: after common.js only) and started from `<body onload="...">`.
 * Charts are embedded as iframes of `../chart/chart.html` (ChartJs service) that fetch chart JSON from
 * `../ServerManager/<api>`; a transparent overlay element on top of each iframe takes the clicks.
 *
 * Globals defined: `addChart`, `serverMain`, `serverMetricsMain` (no immediately executed code).
 * `addChart` is unrelated to `CanvasChart.addChart`; here it means "iframe a chart page".
 *
 * Dependencies (globals from other files):
 * - common/common.js: `createIFrame`, `badClick`, `keyboardClick`, `Open`, `ColorIcon`, `getRequest`,
 *   `getUrlParams`, `ValueFormat` (`formatByteSize`, `convertUTCDateToLocalDate`, `copyToClipboardInfo`),
 *   `Fail`, `Info`, `delay`, `AddLoading`, `PageLoaded`.
 * - app/application.js: `Confirm` (server.html only).
 *
 * Conventions: the metrics page URL is derived from the C# chart API names, which must follow the pattern
 * `GetXxxChart` / `GetXxxHistoryChart` / `GetXxxHistoryShortChart`.
 */

/**
 * Appends a chart tile to `to`: a `<server-item>` containing an iframe (from `createIFrame()`, `tabIndex -1`)
 * with `src = "../chart/chart.html?q=../ServerManager/" + fetchUrl + "&transparent=true&m=false&noLabels=true&center=true" + extra`,
 * plus a transparent `<server-itemclick>` overlay (made keyboard activatable with `keyboardClick`).
 * Clicking the overlay (ignoring `badClick`) opens `openOnClick` in place (`Open(url, "_self")`) if given;
 * otherwise it opens `server_metrics.html?q1=<fetchUrl>&q2=<short history>&q3=<history>&title=<title>`, where
 * the history name is `fetchUrl` with the first "Chart" replaced by "HistoryChart", and the short history name
 * is that string with its first "Chart" replaced by "ShortChart" (giving "HistoryShortChart").
 * @param {HTMLElement} to - Container to append the tile to (a `<server-charts>`).
 * @param {string} fetchUrl - Chart API name relative to `../ServerManager/`, optionally with a `?<arg>` suffix (e.g. "GetDriveChart?0").
 * @param {?string} openOnClick - URL to open on click; falsy to open the derived metrics page.
 * @param {?string} [title] - Title passed to the metrics page (only used when `openOnClick` is falsy).
 * @param {string} [extra] - Extra query string appended to the iframe URL; ignored (treated as "") unless it is a string.
 * @returns {void}
 */
function addChart(to, fetchUrl, openOnClick, title, extra) {
    const item = document.createElement("server-item");
    const iframe = createIFrame();
    if (typeof extra !== "string")
        extra = "";
    iframe.tabIndex = -1;
    iframe.src = "../chart/chart.html?q=../ServerManager/" + fetchUrl + "&transparent=true&m=false&noLabels=true&center=true" + extra;
    item.appendChild(iframe);
    const click = document.createElement("server-itemclick");
    item.appendChild(click);
    click.onclick = ev => {
        if (badClick(ev))
            return;
        if (openOnClick) {
            Open(openOnClick, "_self");
        } else {
            const h = fetchUrl.replace("Chart", "HistoryChart");
            Open("server_metrics.html?q1=" + fetchUrl + "&q2=" + h.replace("Chart", "ShortChart") + "&q3=" + h + "&title=" + title, "_self");
        }
    };
    keyboardClick(click);
    to.appendChild(item);
}

/**
 * Entry point of server.html.
 * Appends `<server-stats>`, `<server-charts>` and `<server-commands>` to the body. Fetches
 * `GET ../ServerManager/GetServerInfo` (`SmServerInfo`) and measures the round trip; half of it is added to
 * the server time to estimate the current server clock. Adds stats: machine name, OS (icon `os_<OsBase>`),
 * CPU count (links to the CPU metrics page), memory (links to the memory metrics page), a live server clock
 * (click copies "yyyy-MM-dd HH:mm:ss") and the process count (links to the ProcessInfoTable explore page).
 * Adds the commands "Reboot" (Confirm, then `GET ../ServerManager/RebootComputer`) and "Deleted" (opens
 * `../ServerManager/Bak/explore`). Adds charts `GetServicesChart` (click opens services.html), `GetCpuChart`,
 * `GetMemChart` and `GetDriveChart?<i>` for each of `info.DriveCount` drives.
 * Calls `PageLoaded()` and starts polling `updateStats` every 5 s; the clock is redrawn every 100 ms.
 * @returns {Promise<void>} Resolves once the page has been built (the timers keep running).
 */
async function serverMain() {


    /** Container for the stat pills. @type {HTMLElement} */
    const stats = document.createElement("server-stats");
    document.body.appendChild(stats);
    /** Container for the chart tiles. @type {HTMLElement} */
    const charts = document.createElement("server-charts");
    document.body.appendChild(charts);
    /** Container for the command pills. @type {HTMLElement} */
    const commands = document.createElement("server-commands");
    document.body.appendChild(commands);

    /**
     * Creates a stat pill (`<server-stat>`) or a command pill (`<server-command>`) with a `ColorIcon`
     * (`../icons/<icon>.svg`; 32px `IconColorThemeAcc2` for commands, 24px `IconColorThemeMain` for stats)
     * and a `<server-text>` label, and appends it to `<server-commands>` or `<server-stats>`.
     * If `link` is set the element gets class `Click`, `keyboardClick` and an async click handler: a string
     * link is opened in place with `Open(link, "_self")`; a function link is awaited, one run at a time
     * (re-entry guarded by `isProcessing`), with class `Click` swapped for `Disabled` while it runs; errors
     * are shown with `Fail(e.message)`.
     * @param {string} icon - Icon name (file name without ".svg" in ../icons/).
     * @param {string|number} text - Label text.
     * @param {string} title - Tooltip of the element.
     * @param {string|function():(void|Promise<void>)} [link] - URL to open, or an (async) action to run on click.
     * @param {boolean} [isCommand] - True to create a command pill, false/omitted for a stat pill.
     * @returns {Array<HTMLElement>} `[element, textElement]`.
     */
    function addStats(icon, text, title, link, isCommand) {
        const e = document.createElement(isCommand ? "server-command" : "server-stat");
        const size = isCommand ? 32 : 24;
        e.title = title;
        e.appendChild(new ColorIcon("../icons/" + icon + ".svg", isCommand ? "IconColorThemeAcc2" : "IconColorThemeMain", size, size).Element);
        const t = document.createElement("server-text");
        t.innerText = text;
        e.appendChild(t);
        if (link) {
            e.classList.add("Click");
            let isProcessing = false;
            
            e.onclick = async ev => {
                if (badClick(ev))
                    return;
                if (typeof link === "string") {
                    Open(link, "_self");
                    return;
                }
                if (isProcessing)
                    return;
                isProcessing = true;
                e.classList.remove("Click");
                e.classList.add("Disabled");
                try {
                    await link();
                }
                catch (e) {
                    Fail(e.message);
                }
                isProcessing = false;
                e.classList.remove("Disabled");
                e.classList.add("Click");
            };
            keyboardClick(e);
        }
        if (isCommand)
            commands.appendChild(e);
        else
            stats.appendChild(e);
        return [e, t];
    }



    /** Local time when the GetServerInfo request was sent. @type {Date} */
    const startTime = new Date();
    /**
     * Server information.
     * @type {{Machine:string, Os:string, OsBase:string, ProcessorCount:number, Memory:number, Time:string, TzName:string, TzDayName:string, ProcessCount:number, DriveCount:number}}
     */
    const info = await getRequest("../ServerManager/GetServerInfo");
    if (!info) {
        //  No data: the request was redirected (ex: the session expired, getRequest is already navigating to the
        //  login page) or the server returned nothing, so there is nothing to show.
        PageLoaded();
        return;
    }
    /** Local time when the last server time was received (updated by `updateStats`). @type {Date} */
    let atTime = new Date();
    /** Half the measured round trip in ms, added to the reported server time. @type {number} */
    const halfPingMs = (atTime - startTime) * 0.5;

    addStats("computer", info.Machine, "Name of the server.");
    addStats("os_" + info.OsBase, info.Os, "Operative system.");
    addStats("cpu", info.ProcessorCount, "Number of logical CPU cores.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetCpuChart&q2=GetCpuHistoryShortChart&q3=GetCpuHistoryChart&title=Cpu use");
    addStats("memory", ValueFormat.formatByteSize(info.Memory), "Amount of physical RAM memory.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetMemChart&q2=GetMemHistoryShortChart&q3=GetMemHistoryChart&title=Memory use");

    /**
     * Estimated server time (ms, converted with `ValueFormat.convertUTCDateToLocalDate`) at local time `atTime`.
     * The current server time is `serverTimeStart + (now - atTime)`. Re-synced by `updateStats`.
     * @type {number}
     */
    let serverTimeStart = ValueFormat.convertUTCDateToLocalDate(new Date(new Date(info.Time).getTime() + halfPingMs)).getTime();
    /**
     * The clock stat pill `[element, textElement]`; clicking it copies the current server date and time
     * ("yyyy-MM-dd HH:mm:ss") to the clipboard.
     * @type {Array<HTMLElement>}
     */
    const timeStats = addStats("clock", "", "", () => {
        const timeNow = new Date(serverTimeStart + (new Date() - atTime));
        const c = timeNow.toISOString().split('T');
        const t = c[0] + " " + c[1].split('.')[0];
        ValueFormat.copyToClipboardInfo(t);
    });
    /**
     * Redraws the server clock: computes `serverTimeStart + (now - atTime)`, writes "HH:mm:ss" into the clock's
     * `<server-text>` and a tooltip with the date, the time zone (`info.TzDayName`, plus `info.TzName` in
     * parentheses if different) and a copy hint; the DOM is only touched when the text changed.
     * Exceptions are swallowed. Reschedules itself every 100 ms with `setTimeout` (runs forever).
     * @returns {void}
     */
    function updateServerTime() {
        try {
            const timeNow = new Date(serverTimeStart + (new Date() - atTime));
            const c = timeNow.toISOString().split('T');
            let t = c[1].split('.')[0];
            let e = timeStats[1];
            if (e.textContent !== t)
                e.textContent = t;
            t = "Date: " + c[0] + "\nTime zone: " + info.TzDayName;
            if (info.TzDayName !== info.TzName)
                t += " (" + info.TzName + ")";
            t += "\nThis is the local server time and date.\n\nClick to copy this to the clipboard";
            e = timeStats[0];
            if (e.title !== t)
                e.title = t;
        }
        catch (e) {
        }
        setTimeout(updateServerTime, 100);
    }
    updateServerTime();

    /**
     * The process count stat pill `[element, textElement]` (links to the ProcessInfoTable explore page;
     * note the tooltip text says "processors" although it is the number of processes).
     * @type {Array<HTMLElement>}
     */
    const processStats = addStats("table_services", info.ProcessCount, "Number of processors that are running.\n\nClick to show details.", "../explore/table.html?q=../ServerManager/ProcessInfoTable");


    /**
     * Polls `GET ../ServerManager/GetServerStats` (`{Time, ProcessCount}`), re-syncs `atTime` and
     * `serverTimeStart` with a half-ping correction, and updates the process count text if it changed.
     * Exceptions are swallowed. Reschedules itself every 5000 ms with `setTimeout` (runs forever).
     * @returns {Promise<void>}
     */
    async function updateStats() {

        try {
            const startTime = new Date();
            const s = await getRequest("../ServerManager/GetServerStats");
            atTime = new Date();
            const halfPingMs = (atTime - startTime) * 0.5;
            serverTimeStart = ValueFormat.convertUTCDateToLocalDate(new Date(new Date(s.Time).getTime() + halfPingMs)).getTime();
            const e = processStats[1];
            const t = "" + s.ProcessCount;
            if (e.textContent !== t)
                e.textContent = t;
        }
        catch (e) {
        }
        setTimeout(updateStats, 5000);
    }

    // Reboot command: asks for confirmation, then calls RebootComputer and waits 10 s (the Info text says 5 seconds).
    addStats("power", "Reboot", "Click to reboot the server", async () => {
        if (await Confirm("Reboot server",
            "WARNING!\nAny unsaved data on the server will be lost!\n\nAre you sure you wan't to continue?",
            "Yes, reboot",
            "No, keep going",
            "../icons/power.svg",
            "../icons/fav_on.svg",
            "Click to reboot the server.\n\nWARNING!\nAny unsaved data on the server will be lost!",
            "Click to keep the server running, do nothing.")) {
            Info("Rebooting in 5 second");
            await getRequest("../ServerManager/RebootComputer");
            await delay(10000);
            }
    }, true);

    addStats("trash", "Deleted", "Click to explore deleted / changed files", () => Open("../ServerManager/Bak/explore", "_self"), true);

    // Chart tiles: services (click opens services.html), CPU, memory and one per drive.
    addChart(charts, "GetServicesChart", "services.html");
    addChart(charts, "GetCpuChart", null, "Cpu use");
    addChart(charts, "GetMemChart", null, "Memory use");
    /** Number of drives reported by the server. @type {number} */
    const count = info.DriveCount;
    for (let i = 0; i < count; ++ i)
        addChart(charts, "GetDriveChart?" + i, null, "Disc use");

    PageLoaded();
    setTimeout(updateStats, 5000);
}


/**
 * Entry point of server_metrics.html: detail view of one metric (drive, CPU or memory of the server, a
 * process or a managed service).
 * Shows a loader (`AddLoading()`), reads the URL params `q1` (current value chart), `q2` (short history),
 * `q3` (long history) and `title` (sets `document.title` if present), adds class `Dual` to the body and
 * appends a `<server-charts>` with three `addChart` tiles. `q2` / `q3` get `extra = "&aspect=false"`.
 * Clicking a tile opens that chart full size in `../chart/chart.html` (with its menu; `q1` with
 * `&center=true`, `q2` / `q3` with `&aspect=false&noLabels=true`). Closes the loader.
 * Note: does not call `PageLoaded()`.
 * @returns {Promise<void>}
 */
async function serverMetricsMain()
{
    const closeLoader = AddLoading();
    const p = getUrlParams();
    const q1 = p.get("q1");
    const q2 = p.get("q2");
    const q3 = p.get("q3");
    const title = p.get("title");
    if (title)
        document.title = title;
    document.body.classList.add("Dual");

    const charts = document.createElement("server-charts");
    document.body.appendChild(charts);

    addChart(charts, q1, "../chart/chart.html?q=../ServerManager/" + q1 + "&center=true");
    addChart(charts, q2, "../chart/chart.html?q=../ServerManager/" + q2 + "&aspect=false&noLabels=true", null, "&aspect=false");
    addChart(charts, q3, "../chart/chart.html?q=../ServerManager/" + q3 + "&aspect=false&noLabels=true", null, "&aspect=false");

    closeLoader();

}
