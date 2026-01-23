

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

async function serverMain() {


    const stats = document.createElement("server-stats");
    document.body.appendChild(stats);
    const charts = document.createElement("server-charts");
    document.body.appendChild(charts);
    const commands = document.createElement("server-commands");
    document.body.appendChild(commands);

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



    const startTime = new Date();
    const info = await getRequest("../ServerManager/GetServerInfo");
    let atTime = new Date();
    const halfPingMs = (atTime - startTime) * 0.5;

    addStats("computer", info.Machine, "Name of the server.");
    addStats("os_" + info.OsBase, info.Os, "Operative system.");
    addStats("cpu", info.ProcessorCount, "Number of logical CPU cores.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetCpuChart&q2=GetCpuHistoryShortChart&q3=GetCpuHistoryChart&title=Cpu use");
    addStats("memory", ValueFormat.formatByteSize(info.Memory), "Amount of physical RAM memory.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetMemChart&q2=GetMemHistoryShortChart&q3=GetMemHistoryChart&title=Memory use");

    let serverTimeStart = ValueFormat.convertUTCDateToLocalDate(new Date(new Date(info.Time).getTime() + halfPingMs)).getTime();
    const timeStats = addStats("clock", "", "", () => {
        const timeNow = new Date(serverTimeStart + (new Date() - atTime));
        const c = timeNow.toISOString().split('T');
        const t = c[0] + " " + c[1].split('.')[0];
        ValueFormat.copyToClipboardInfo(t);
    });
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

    const processStats = addStats("table_services", info.ProcessCount, "Number of processors that are running.\n\nClick to show details.", "../explore/table.html?q=../ServerManager/ProcessInfoTable");


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

    addChart(charts, "GetServicesChart", "services.html");
    addChart(charts, "GetCpuChart", null, "Cpu use");
    addChart(charts, "GetMemChart", null, "Memory use");
    const count = info.DriveCount;
    for (let i = 0; i < count; ++ i)
        addChart(charts, "GetDriveChart?" + i, null, "Disc use");

    PageLoaded();
    setTimeout(updateStats, 5000);
}


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
