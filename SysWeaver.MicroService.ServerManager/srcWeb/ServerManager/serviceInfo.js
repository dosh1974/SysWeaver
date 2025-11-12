


async function serviceInfoMain() {
    const p = getUrlParams();
    const service = p.get("p");
    if (!service) {
        Fail("No serivce paramater supplied!");
        return;
    }
    document.title = "Service - " + service;
    document.body.getElementsByTagName("si-name")[0].innerText = service;
    const state = document.body.getElementsByTagName("si-state")[0]
    const graphs = document.body.getElementsByTagName("si-graph");
    graphs[0].getElementsByTagName("iframe")[0].src = "../chart/chart.html?transparent=true&m=false&aspect=false&q=../ServerManager/GetMem?\"" + service + "\"";
    graphs[1].getElementsByTagName("iframe")[0].src = "../chart/chart.html?transparent=true&m=false&aspect=false&q=../ServerManager/GetCpu?\"" + service + "\"";

    graphs[0].onclick = ev => {
        if (badClick(ev))
            return;
        Open("../chart/chart.html?q=../ServerManager/GetMem?\"" + service + "\"", "_self");
    };
    graphs[1].onclick = ev => {
        if (badClick(ev))
            return;
        Open("../chart/chart.html?q=../ServerManager/GetCpu?\"" + service + "\"", "_self");
    };

    const serviceButtons = document.body.getElementsByTagName("si-servicebuttons")[0];
    const restartButton = new Button(null, "Restart", "Click to restart the service", null, true, async () => {
    });

    const pauseButton = new Button(null, "Pause", "Click to pause the service", null, true, async () => {
    });

    const resumeButton = new Button(null, "Resume", "Click to resume the service", null, true, async () => {
    });

    const stopButton = new Button(null, "Stop", "Click to stop the service", null, true, async () => {
    });

    const startButton = new Button(null, "Stop", "Click to start the service", null, true, async () => {
    });

    const disableButton = new Button(null, "Disable", "Click to disable the service", null, true, async () => {
    });
    serviceButtons.appendChild(restartButton.Element);
    serviceButtons.appendChild(pauseButton.Element);
    serviceButtons.appendChild(resumeButton.Element);
    serviceButtons.appendChild(stopButton.Element);
    serviceButtons.appendChild(startButton.Element);
    serviceButtons.appendChild(disableButton.Element);


    const debugButtons = document.body.getElementsByTagName("si-debugbuttons")[0];
    const exploreButton = new Button(null, "Explore", "Explore the files", null, true, async () => {
        Open("../FolderSync/Folders/" + service + "/explore", "_self");
    });

    const logButton = new Button(null, "View log", "View the log", null, true, async () => {
    });

    debugButtons.appendChild(exploreButton.Element);
    debugButtons.appendChild(logButton.Element);



    let data = null;
    for (; ;) {
        try {
            data = await sendRequest("GetDetail", service);
            state.innerText = data.Status;

        }
        catch (e)
        {
            Fail(e);
        }
        await delay(5000);
    }


}