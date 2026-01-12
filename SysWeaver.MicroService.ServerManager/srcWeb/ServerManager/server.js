



async function serverMain() {



    function addChart(Url) {
        const item = document.createElement("server-item");
        const iframe = document.createElement("iframe");
        iframe.src = "../chart/chart.html?q=../ServerManager/" + Url + "&transparent=true&m=false&noLabels=true";
        item.appendChild(iframe);
        const click = document.createElement("server-itemclick");
        item.appendChild(click);
        click.onclick = ev => {
            if (badClick(ev))
                return;
            Open("../chart/chart.html?q=../ServerManager/" + Url, "_self");
        };
        keyboardClick(click);
        document.body.appendChild(item);
    }


    addChart("GetCpuUsageChart");
    addChart("GetMemoryChart");


    const count = await getRequest("../ServerManager/GetDriveCount");
    for (let i = 0; i < count; ++ i)
        addChart("GetDriveChart?" + i);
   


    PageLoaded();
}