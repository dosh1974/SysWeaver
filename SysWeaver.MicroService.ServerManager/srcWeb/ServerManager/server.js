

function addChart(to, fetchUrl, openOnClick, title, extra) {
    const item = document.createElement("server-item");
    const iframe = document.createElement("iframe");
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

    function addStats(icon, text, title, link) {
        const e = document.createElement("server-stat");
        e.title = title;
        e.appendChild(new ColorIcon("../icons/" + icon + ".svg", "IconColorThemeMain", 24, 24).Element);
        const t = document.createElement("server-text");
        t.innerText = text;
        e.appendChild(t);
        if (link) {
            e.classList.add("Click");
            e.onclick = ev => {
                if (badClick(ev))
                    return;
                Open(link, "_self");
            };
            keyboardClick(e);
        }
        stats.appendChild(e);
        return [e, t];
    }

    const info = await getRequest("../ServerManager/GetServerInfo");

    addStats("computer", info.Machine, "Name of the server.");
    addStats("os_" + info.OsBase, info.Os, "Operative system.");
    addStats("cpu", info.ProcessorCount, "Number of logical CPU cores.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetCpuChart&q2=GetCpuHistoryShortChart&q3=GetCpuHistoryChart&title=Cpu use");
    addStats("memory", ValueFormat.formatByteSize(info.Memory), "Amount of physical RAM memory.\n\nClick to show details.", "../ServerManager/server_metrics.html?q1=GetMemChart&q2=GetMemHistoryShortChart&q3=GetMemHistoryChart&title=Memory use");
    addStats("ssd", info.DriveCount, "Number of installed drives.\n\nClick to show details.", "../explore/table.html?q=../ServerManager/DriveInfoTable");
    const processStats = addStats("table_services", info.ProcessCount, "Number of processors that are running.\n\nClick to show details.", "../explore/table.html?q=../ServerManager/ProcessInfoTable");

    async function updateStats() {

        try {
            const s = await getRequest("../ServerManager/GetServerStats");
            processStats[1].innerText = s.ProcessCount;
        }
        catch (e) {
        }
        setTimeout(updateStats, 5000);
    }


    const charts = document.createElement("server-charts");
    document.body.appendChild(charts);

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

    PageLoaded();

}
