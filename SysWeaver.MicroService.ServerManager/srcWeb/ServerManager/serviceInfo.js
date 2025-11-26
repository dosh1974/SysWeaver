
function BeginElementChildUpdate(targetElement) {
    const el = targetElement;
    let ccl = el.children?.length ?? 0;
    let cindex = 0;
    function add(e) {
        if (cindex >= ccl) {
            ++cindex;
            el.appendChild(e);
            return e;
        }
        const cc = el.children[cindex];
        ++cindex;
        if (cc.outerHTML === e.outerHTML)
            return cc;
        el.replaceChild(e, cc);
        return e;
    }
    function complete() {
        while (ccl > cindex) {
            --ccl;
            el.children[ccl].remove();
        }
    }
    return {
        Target: el,
        Add: add,
        Complete: complete,
    };
}

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

    function updateNow() {
        abortWait.raise();
    }

    let exploreButton = null;
    let logButton = null;
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
                updateNow();
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
        exploreButton = new Button(null, "Explore", "Explore the active files", "si-icon-explore", true, async () => {
            Open("../FolderSync/Folders/" + service + "/explore", "_self");
        });

        logButton = new Button(null, "View log", "View the current log", "si-icon-log", true, async () => {
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
    const uploads = document.body.getElementsByTagName("si-uploads")[0];
    const masterConfigs = document.body.getElementsByTagName("si-configs")[1];
    const masterUploads = document.body.getElementsByTagName("si-uploads")[1];
    const versions = document.body.getElementsByTagName("si-versions")[0];

    const bakMap = new Map();
    bakMap.set(4, '-');
    bakMap.set(7, '-');
    bakMap.set(10, '_');
    bakMap.set(13, '_');
    bakMap.set(16, '_');

    const digitMap = new Map();
    digitMap.set('0', true);

    function isBackup(fn) {
        let e = fn.lastIndexOf('.');
        if (e < 0)
            return false;
        fn = fn.substring(0, e);
        e = fn.lastIndexOf('.');
        if (e < 0)
            return false;
        fn = fn.substring(e + 1);
        if (fn === "LastGood")
            return true;
        if (fn.length != 19)
            return false;
        for (let i = 0; i < 19; ++i) {

            const c = fn.charAt(i);
            const m = bakMap.get(i);
            if (m) {
                if (c === m)
                    continue;
            }
            if (c < '0')
                return false;
            if (c > '9')
                return false;
        }
        return true;
    }

    function updateFileList(el, files, headerText, headerTitle, folderSuffix, isMaster) {

        isMaster = !!isMaster;
        const updater = BeginElementChildUpdate(el)
        if (!folderSuffix)
            folderSuffix = "../FolderSync/Folders/";
        const header = document.createElement("si-file-header");
        header.innerText = headerText;
        if (headerTitle)
            header.title = headerTitle;
        updater.Add(header);
        if (files) {
            const cl = files.length;
            if (cl > 0) {
                for (let i = 0; i < cl; ++i) {
                    const f = files[i];
                    const fn = f.Name;
                    const icon = document.createElement("si-file-icon");
                    const ext = fn.substring(fn.lastIndexOf('.') + 1);
                    icon.style.backgroundImage = "url('../icons/ext/" + ext + ".svg')";
                    if (!viewOnly) {
                        icon.title = "Click to show options";
                        icon.onclick = async ev => {
                            if (badClick(ev))
                                return;
                            const f = ev.target.Config;
                            const fn = f.Name;
                            const folder = isMaster ? data.Folder : data.CurrentFolder;
                            const path = folder + "\\" + fn;
                            await PopUpMenu(icon, (close, el) => {
                                el.classList.add("Rel");
                                const menu = new WebMenu();
                                menu.Name = "SmConfigFiles";
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("View", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/notes.svg",
                                    Title: _TF("View the file content", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        Open("../logFile/logfile.html?api=" + folderSuffix + service + "/" + fn, "_self");
                                        close();
                                    },
                                }));
                                if (isBackup(fn)) {
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Activate", "The text of a menu option that when clicked will exit from full screen mode"),
                                        Flags: 0,
                                        IconClass: "../icons/fav_on.svg",
                                        Title: _TF("Use this as the active config:\n1. Rename any original (backup)\n2. Rename this to original name", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                        Data: async () => {
                                            try {
                                                if (!await sendRequest("ActivateConfig", {
                                                    ServiceName: service,
                                                    Config: fn,
                                                    IsMaster: isMaster,
                                                })) {
                                                    Fail("Failed to activate the config!");
                                                    return;
                                                }
                                                updateNow();
                                            }
                                            catch (e) {
                                                Fail("Failed to activate the config! " + e);
                                            }
                                            close();
                                        },
                                    }));
                                }

                                if (isMaster) {
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Use as current", "The text of a menu option that when clicked will exit from full screen mode"),
                                        Flags: 0,
                                        IconClass: "../icons/preview_play.svg",
                                        Title: _TF("Copy this configuration to the current version", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                        Data: async () => {
                                            try {
                                                if (!await sendRequest("UseMasterConfig", {
                                                    ServiceName: service,
                                                    Config: fn,
                                                    IsMaster: isMaster,
                                                })) {
                                                    Fail("Failed to copy the configuration to the current version!");
                                                    return;
                                                }
                                                updateNow();
                                            }
                                            catch (e) {
                                                Fail("Failed to copy the configuration to the current version! " + e);
                                            }
                                            close();
                                        },
                                    }));
                                }

                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy file name", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/copy.svg",
                                    Title: _T("Click to copy \"{0}\" to the clipboard", fn, "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {
                                        ValueFormat.copyToClipboardInfo(fn);
                                        close();
                                    },
                                }));
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy local path", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/copy.svg",
                                    Title: _T("Click to copy \"{0}\" to the clipboard", path, "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        ValueFormat.copyToClipboardInfo(path);
                                        close();
                                    },
                                }));
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy local folder", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/copy.svg",
                                    Title: _T("Click to copy \"{0}\" to the clipboard", folder, "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        ValueFormat.copyToClipboardInfo(folder);
                                        close();
                                    },
                                }));

                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Download", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/disc.svg",
                                    Title: _T("Click to download the \"{0}\" config", folder, "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        downloadFile(fn, folderSuffix + service + "/" + fn);
                                        close();
                                    },
                                }));


                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Delete", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/close.svg",
                                    Title: _TF("Delete the file", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        if (await Confirm("Delete",
                                            "Delete the configuration file:\n\n" +
                                            '"' + path + '"\n\n' +
                                            "The file will be permamently removed!\n" +
                                            "Are you sure ?",
                                            "Yes, Delete!",
                                            "No, Keep it!",
                                            "../icons/close.svg",
                                            "../icons/fav_on.svg",
                                            "Click to pemamently delete the file",
                                            "Click to keep the file")) {
                                            downloadFile(fn, folderSuffix + service + "/" + fn);
                                            await delay(500);
                                            try {
                                                if (!await sendRequest("DeleteConfig", {
                                                    ServiceName: service,
                                                    Config: fn,
                                                    IsMaster: isMaster,
                                                })) {
                                                    Fail("Failed to delete configuration file!");
                                                    return;
                                                }
                                                updateNow();
                                            }
                                            catch (e) {
                                                Fail("Failed to delete configuration file! " + e);
                                            }
                                        }
                                        close();
                                    },
                                }));
                                return menu;
                            }, true);
                        };
                        keyboardClick(icon);
                        rightClick(icon);
                    }

                    const state = document.createElement("si-file-state");
                    state.title = _TF("This file is not a backup");
                    if (isBackup(fn))
                        state.classList.remove("Show");
                    else
                        state.classList.add("Show");


                    const name = document.createElement("si-file-name");
                    name.innerText = fn;
                    name.onclick = ev => {
                        if (badClick(ev))
                            return;
                        Open("../logFile/logfile.html?api=" + folderSuffix + service + "/" + fn, "_self");
                    }; 
                    keyboardClick(name);

                    const size = document.createElement("si-file-size");
                    size.innerText = ValueFormat.formatByteSize(f.Size);
                    ValueFormat.copyOnClick(size, f.Size, false, true);

                    const time = document.createElement("si-file-time");
                    const dd = new Date(f.LastModified);
                    const v = ValueFormat.getTimeStampTitle(dd);
                    time.title = "Last modified\n" + v[0];
                    ValueFormat.copyOnClick(time, f.LastModified, false, true);
                    time.innerText = v[1];


                    updater.Add(state);
                    updater.Add(icon).Config = f;
                    updater.Add(name);
                    updater.Add(size);
                    updater.Add(time);

                }
            }
        }
        updater.Complete();
    }



    let data = null;
    function update(first) {

        if (first)
            PageLoaded();
        const status = data.Status;
        const state = states[0];
        state.innerText = status;
        state.className = "";
        state.classList.add(stateMap.get(status));
        const masterFolder = data.Folder;
        const currentFolder = data.CurrentFolder;
        if (first) {
            if (exploreButton)
                exploreButton.ChangeTitle("Explore the active files.\n\nLocated in this folder:\n" + currentFolder);
            if (logButton)
                logButton.ChangeTitle("View the current log file.\n\nLocated here:\n" + currentFolder + "\\" + data.Log?.Name);
            uploads.title = "Click to upload config files into the current version.\n\nLocated in this folder:\n" + currentFolder;
            masterUploads.title = "Click to upload config files into the master configs.\n\nLocated in this folder:\n" + masterFolder;
        }

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

        function UploadCompleted(e, res, files) {
            const err = res.Error;
            if (err) {
                Fail(err);
                return;
            }
            const ss = res.Status;
            const sl = ss.length;
            for (let i = 0; i < sl; ++i) {
                const res0 = ss[i];
                switch (res0) {
                    case UploadStatus.AlreadyUploaded:
                    case UploadStatus.None:
                        break;
                    default:
                        Fail(_T("{0}, when uploading \"{1}\"", fileUploaderStatusText(res0), files[i].name, "Text displayed when uploading of a file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the file"));
                        return;
                }
            }
            for (let i = 0; i < sl; ++i) {
                const res0 = ss[i];
                if (res0 === UploadStatus.AlreadyUploaded) {
                    Info(_T("File \"{0}\" was already uploaded", files[i].name, "Text displayed when a file have already been uploaded to a server.{0} is replaced with the name of the file"));
                    return;
                }
            }
            for (let i = 0; i < sl; ++i) {
                const res0 = ss[i];
                if (res0 === UploadStatus.None) {
                    Info(_T("Uploaded \"{0}\"", files[i].name, "Text displayed when a file was succesfully uploaded to a server.{0} is replaced with the name of the file"));
                    updateNow();
                    return;
                }
            }
        }


        if (data.Configs) {
            updateFileList(configs, data.Configs, "Active Config Files", "These are the files that are currently active.\n\nLocated in this folder:\n" + currentFolder);
            configs.classList.add("Show");
            if ((!viewOnly) && first) {
                fileUploaderSetup(uploads, "Current_" + service, null, UploadCompleted, null, true);
                keyboardClick(uploads);
                uploads.classList.add("Show");
            }
        } else {
            configs.classList.remove("Show");

        }


        if (data.MasterConfigs) {
            updateFileList(masterConfigs, data.MasterConfigs, "Master Config Files", "These are the master config files, that get copied when a new version is uploaded..\n\nLocated in this folder:\n" + masterFolder, "../ServerManager/Data/", true);
            masterConfigs.classList.add("Show");
            if ((!viewOnly) && first) {
                fileUploaderSetup(masterUploads, "Master_" + service, null, UploadCompleted, null, true);
                keyboardClick(masterUploads);
                masterUploads.classList.add("Show");
            }

        } else {
            masterUploads.classList.remove("Show");
            masterConfigs.classList.remove("Show");
        }

        const updater = BeginElementChildUpdate(versions);
        const vs = data.Versions;
        if (vs) {
            const vl = vs.length
            if (vl > 0) {
                let e = document.createElement("si-version-header");
                e.innerText = "Versions";
                e.title = "All available versions of this service";
                updater.Add(e);
                for (let i = 0; i < vl; ++i) {
                    const v = vs[i];
                //  Active
                    e = document.createElement("si-version-active");
                    if (v.IsActive)
                        e.classList.add("Active");
                    updater.Add(e);
                //  Uploaded
                    e = document.createElement("si-version-uploaded");
                    let dv = ValueFormat.getTimeStampTitle(new Date(v.Uploaded));
                    e.title = "Click to show details.\n\nUploaded:\n" + dv[0];
                    e.innerText = dv[1];

                    e.onclick = ev => {
                        if (badClick(ev))
                            return;
                        Open("versionInfo.html?p=" + service + "," + v.Uploaded + "," + v.Name, "_self");
                    };
                    keyboardClick(e);
                    updater.Add(e);
                //  User
                    e = document.createElement("si-version-user");
                    e.title = "The name of the user that was logged on to the machine that this version was uploaded from at the upload start time";
                    e.innerText = v.User;
                    ValueFormat.copyOnClick(e, v.User, false, true);
                    updater.Add(e);
                //  Machine
                    e = document.createElement("si-version-machine");
                    e.title = "The name of the machine that this version was uploaded from";
                    e.innerText = v.Machine;
                    ValueFormat.copyOnClick(e, v.Machine, false, true);
                    updater.Add(e);
                //  Comment
                    e = document.createElement("si-version-comment");
                    e.title = "The comment supplied when uploading this version";
                    e.innerText = v.Comment;
                    ValueFormat.copyOnClick(e, v.Comment, false, true);
                    updater.Add(e);
                //  Last used
                    e = document.createElement("si-version-last");
                    dv = ValueFormat.getTimeStampTitle(new Date(v.LastUsed));
                    e.title = "Last activated\n" + dv[0];
                    ValueFormat.copyOnClick(e, v.LastUsed, false, true);
                    e.innerText = dv[1];
                    updater.Add(e);


                }
            }
        }
        updater.Complete();



        updateButtons(status);
    }
    for (; ;) {
        try {
            const noOld = !data;
            data = await sendRequest("GetDetail", service);
            update(noOld && data);
        }
        catch (e)
        {
            Fail(e);
        }
        await delayWithAbort(5000, abortWait);
    }


}