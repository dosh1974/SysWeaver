

function addChart(fetchUrl, openOnClick, title) {
    const item = document.createElement("server-item");
    const iframe = document.createElement("iframe");
    iframe.src = "../chart/chart.html?q=../ServerManager/" + fetchUrl + "&transparent=true&m=false&noLabels=true&center=true";
    item.appendChild(iframe);
    const click = document.createElement("server-itemclick");
    item.appendChild(click);
    click.onclick = ev => {
        if (badClick(ev))
            return;
        if (openOnClick) {
            Open(openOnClick, "_self");
        } else {
            Open("server_metrics.html?q1=" + fetchUrl + "&q2=" + fetchUrl.replace("Chart", "HistoryChart") + "&title=" + title, "_self");
        }
    };
    keyboardClick(click);
    document.body.appendChild(item);
}

async function serverMain() {






    addChart("GetServicesChart", "services.html");
    addChart("GetCpuUsageChart", null, "Cpu Usage");
    addChart("GetMemoryChart", null, "Memory usage");


    const count = await getRequest("../ServerManager/GetDriveCount");
    for (let i = 0; i < count; ++ i)
        addChart("GetDriveChart?" + i, null, "Disc usage");
   


    PageLoaded();
}


async function serverMetricsMain()
{
    const p = getUrlParams();
    const q1 = p.get("q1");
    const q2 = p.get("q2");
    const title = p.get("title");
    if (title)
        document.title = title;
    document.body.classList.add("Dual");

    addChart(q1, "../chart/chart.html?q=../ServerManager/" + q1);
    addChart(q2, "../chart/chart.html?q=../ServerManager/" + q2);

}
