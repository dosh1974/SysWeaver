

function addChart(fetchUrl, openOnClick, title, extra) {
    const item = document.createElement("server-item");
    const iframe = document.createElement("iframe");
    if (typeof extra !== "string")
        extra = "";
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
    document.body.appendChild(item);
}

async function serverMain() {






    addChart("GetServicesChart", "services.html");
    addChart("GetCpuUsageChart", null, "Cpu use");
    addChart("GetMemoryChart", null, "Memory use");


    const count = await getRequest("../ServerManager/GetDriveCount");
    for (let i = 0; i < count; ++ i)
        addChart("GetDriveChart?" + i, null, "Disc use");
   


    PageLoaded();
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

    addChart(q1, "../chart/chart.html?q=../ServerManager/" + q1 + "&center=true");
    addChart(q2, "../chart/chart.html?q=../ServerManager/" + q2 + "&aspect=false&noLabels=true", null, "&aspect=false");
    addChart(q3, "../chart/chart.html?q=../ServerManager/" + q3 + "&aspect=false&noLabels=true", null, "&aspect=false");

}
