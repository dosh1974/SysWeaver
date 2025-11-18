


async function serviceInfoMain() {
    const p = getUrlParams();
    const service = p.get("p");
    if (!service) {
        Fail("No serivce paramater supplied!");
        return;
    }
    document.title = "Service - " + service;
    document.body.getElementsByTagName("si-name")[0].innerText = service;
    const states = document.body.getElementsByTagName("si-state");

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
    const abortWait = new AbortHandler();

    let blockButtonStates = false;
    async function DoVerb(button, v, okText) {
        try {
            blockButtonStates = true;
            button.StartWorking();
            [].forEach.call(serviceButtons.children, e => e.Button.SetEnabled(false));
            const newData = await sendRequest(v, service);
            if (!newData) {
                Fail("Failed to " + v.toLowerCase());
                abortWait.raise();
                return;
            }
            data = newData;
            blockButtonStates = false;
            update();
            Info(ValueFormat.stringFormat(okText, service));
        }
        catch (e) {
            Fail("Failed to " + v.toLowerCase() + ", error: " + e.message);
            blockButtonStates = false;
            abortWait.raise();
        }
        finally {
            button.StopWorking();
        }
    }

    const restartButton = new Button(null, "Restart", "Click to restart the service", "si-icon-restart", true, async () => {
        await DoVerb(restartButton, "Restart", "Restarted {0}");
    });

    const pauseButton = new Button(null, "Pause", "Click to pause the service", "si-icon-pause", true, async () => {
        await DoVerb(pauseButton, "Pause", "Paused {0}");
    });

    const resumeButton = new Button(null, "Resume", "Click to resume the service", "si-icon-resume", true, async () => {
        await DoVerb(resumeButton, "Continue", "Resumed {0}");
    });

    const stopButton = new Button(null, "Stop", "Click to stop the service", "si-icon-stop", true, async () => {
        await DoVerb(stopButton, "Stop", "Stopped {0}");
    });

    const startButton = new Button(null, "Start", "Click to start the service", "si-icon-start", true, async () => {
        await DoVerb(startButton, "Start", "Started {0}");
    });

    const disableButton = new Button(null, "Disable", "Click to disable the service", "si-icon-disable", true, async () => {
        await DoVerb(disableButton, "Uninstall", "Disabled {0}");
    });

    serviceButtons.appendChild(restartButton.Element);
    serviceButtons.appendChild(pauseButton.Element);
    serviceButtons.appendChild(resumeButton.Element);
    serviceButtons.appendChild(stopButton.Element);
    serviceButtons.appendChild(startButton.Element);
    serviceButtons.appendChild(disableButton.Element);


    const debugButtons = document.body.getElementsByTagName("si-debugbuttons")[0];
    const exploreButton = new Button(null, "Explore", "Explore the files", "si-icon-explore", true, async () => {
        Open("../FolderSync/Folders/" + service + "/explore", "_self");
    });

    const logButton = new Button(null, "View log", "View the log", "si-icon-log", true, async () => {
        Open("../logFile/logfile.html?api=../FolderSync/Folders/" + service + "/" + data.Log.Name, "_self");
    });

    debugButtons.appendChild(exploreButton.Element);
    debugButtons.appendChild(logButton.Element);

    /*
        Unknown,
        NotInstalled,
        Stopped,
        StartPending,
        StopPending,
        Running,
        ContinuePending,
        PausePending,
        Paused,
    */


    let data = null;
    function update() {
        const status = data.Status;
        states[0].innerText = status;
        const hide = data.ProcId === 0;
        if (hide) {
            for (let i = 1; i < 5; ++i)
                states[i].classList.add("Hide");
        } else {
            for (let i = 1; i < 5; ++i)
                states[i].classList.remove("Hide");
        }
        states[1].innerText = "Id: " + data.ProcId;
        states[2].innerText = "Mem: " + ValueFormat.formatByteSize(data.MemUsage);
        states[3].innerText = "Cpu: " + ValueFormat.toString(data.CpuUsage, 2) + " %";
        states[4].innerText = "Tot: " + data.TotalProcessorTime;
        if (!blockButtonStates) {
            const isRunning = status === "Running";
            const isPaused = status === "Paused";
            const isStopped = status === "Stopped";
            restartButton.SetEnabled(isRunning);
            pauseButton.SetEnabled(isRunning);
            resumeButton.SetEnabled(isPaused);
            stopButton.SetEnabled(isRunning || isPaused);
            startButton.SetEnabled(isStopped || status === "NotInstalled");
            disableButton.SetEnabled(isStopped || isRunning || isPaused);
        }
        logButton.SetEnabled(!!data.Log);

    }
    for (; ;) {
        try {
            data = await sendRequest("GetDetail", service);
            update();
        }
        catch (e)
        {
            Fail(e);
        }
        await delayWithAbort(5000, abortWait);
    }


}