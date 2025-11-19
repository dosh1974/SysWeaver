


async function serviceInfoMain() {
    const p = getUrlParams();
    const service = p.get("p");
    if (!service) {
        Fail("No serivce paramater supplied!");
        return;
    }
    const viewOnly = !!p.get("v");
    if (viewOnly)
        document.body.classList.add("ViewOnly");
    if (window.IsTop)
        document.body.classList.add("TopWindow");

    document.title = "Service - " + service;
    document.body.getElementsByTagName("si-name")[0].innerText = service;
    const states = document.body.getElementsByTagName("si-state");

    const graphs = document.body.getElementsByTagName("si-graph");
    let ifr = graphs[0].getElementsByTagName("iframe")[0];
    ifr.src = "../chart/chart.html?transparent=true&m=false&aspect=false&q=../ServerManager/GetMem?\"" + service + "\"";
    ifr.tabIndex = "-1";
    ifr = graphs[1].getElementsByTagName("iframe")[0];
    ifr.tabIndex = "-1";
    ifr.src = "../chart/chart.html?transparent=true&m=false&aspect=false&q=../ServerManager/GetCpu?\"" + service + "\"";
    if (!viewOnly) {
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
        keyboardClick(graphs[0]);
        keyboardClick(graphs[1]);
    }
    const abortWait = new AbortHandler();
    let updateButtons = () => { };

    if (!viewOnly) {

        const serviceButtons = document.body.getElementsByTagName("si-servicebuttons")[0];

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

        updateButtons = status => {
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

        };
    }

    const stateMap = new Map();
    stateMap.set("Unknown", "Error");
    stateMap.set("NotInstalled", "Error");
    stateMap.set("Stopped", "Error");
    stateMap.set("StartPending", "Warning");
    stateMap.set("StopPending", "Error");
    stateMap.set("Running", "Ok");
    stateMap.set("ContinuePending", "Warning");
    stateMap.set("Paused", "Error");
    stateMap.set("PausePending", "Error");

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




    const configs = document.body.getElementsByTagName("si-configs")[0];
    const masterConfigs = document.body.getElementsByTagName("si-configs")[1];
    function updateFileList(el, files, headerText, headerTitle, folderSuffix) {
        let ccl = el.children?.length ?? 0;
        let cindex = 0;
        function Mod(e) {
            if (cindex >= ccl) {
                ++cindex;
                el.appendChild(e);
                return;
            }
            const cc = el.children[cindex];
            ++cindex;
            if (cc.outerHTML === e.outerHTML)
                return;
            el.replaceChild(e, cc);
        }
        if (files) {
            const cl = files.length;
            if (cl > 0) {
                if (!folderSuffix)
                    folderSuffix = "../FolderSync/Folders/";
                const header = document.createElement("si-file-header");
                header.innerText = headerText;
                if (headerTitle)
                    header.title = headerTitle;
                Mod(header);
                for (let i = 0; i < cl; ++i) {
                    const f = files[i];
                    const fn = f.Name;
                    const icon = document.createElement("si-file-icon");
                    const ext = fn.substring(fn.lastIndexOf('.') + 1);

                    icon.style.backgroundImage = "url('../icons/ext/" + ext + ".svg')";
                    Mod(icon);
                    const name = document.createElement("si-file-name");
                    name.innerText = fn;
                    name.onclick = ev => {
                        if (badClick(ev))
                            return;
                        Open("../logFile/logfile.html?api=" + folderSuffix + service + "/" + fn, "_self");
                    }; 
                    keyboardClick(name);
                    Mod(name);

                    const size = document.createElement("si-file-size");
                    size.innerText = ValueFormat.formatByteSize(f.Size);
                    ValueFormat.copyOnClick(size, f.Size, false, true);
                    Mod(size);

                    const time = document.createElement("si-file-time");
                    const dd = new Date(f.LastModified);
                    const v = ValueFormat.getTimeStampTitle(dd);
                    time.title = "Last modified\n" + v[0];
                    ValueFormat.copyOnClick(time, f.LastModified, false, true);
                    time.innerText = v[1];
                    Mod(time);

                }
            }
        }
        while (ccl > cindex) {
            --ccl;
            el.children[ccl].remove();
        }

    }



    let data = null;
    function update() {
        const status = data.Status;
        const state = states[0];
        state.innerText = status;
        state.className = "";
        state.classList.add(stateMap.get(status));

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
        states[4].innerText = "Tot: " + ValueFormat.formatTimeSpan(data.TotalProcessorTime);



        updateFileList(configs, data.Configs, "Active Config Files", "These are the files that are currently active");
        updateFileList(masterConfigs, data.MasterConfigs, "Master Config Files", "These are the master config files, that get copied when a new version is uploaded", "../ServerManager/Data/");

        updateButtons(status);
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