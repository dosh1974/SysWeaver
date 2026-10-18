/**
 * @file Script of the ServerManager "service info" page (serviceInfo.html): details of one managed service
 * (state, CPU/memory graphs, control buttons, debug buttons, active and master config files with uploads, and the
 * list of uploaded versions). Opened from the services table (`SmServiceBrief` link `serviceInfo.html?p=<svc>`).
 * Loaded with a static `<script defer>` tag (after common.js, md5.js, UZIP.js, fileUploader.js and
 * app/application.js) and started from `<body onload="serviceInfoMain()">`.
 * Relies on the static placeholders of serviceInfo.html, found by tag name and index: `<si-name>`, five
 * `<si-state>`, `<si-servicebuttons>`, two `<si-graph>` (each with an iframe), `<si-debugbuttons>`, two
 * `<si-configs>`, two `<si-uploads>` and `<si-versions>`.
 *
 * URL params: `p` = service name (required), `v` = view-only flag (any non-empty value; adds `body.ViewOnly`).
 *
 * Globals defined: `BeginElementChildUpdate` (identical copy of the one in versionInfo.js) and `serviceInfoMain`.
 * No immediately executed code; `serviceInfoMain` ends in an endless polling loop (`GetDetail` every 5 s).
 *
 * Dependencies (globals from other files):
 * - common/common.js: `getUrlParams`, `sendRequest`, `Open`, `badClick`, `keyboardClick`, `rightClick`, `Button`,
 *   `ValueFormat`, `AbortHandler`, `delayWithAbort`, `downloadFile`, `Fail`, `Info`, `_T`, `_TF`, `PageLoaded`,
 *   `window.IsTop`.
 * - app/application.js: `Confirm`, `PopUpMenu`, `WebMenu`, `WebMenuItem`.
 * - common/fileUploader.js: `fileUploaderSetup`, `UploadStatus`, `fileUploaderStatusText`
 *   (repos `Current_<svc>` and `Master_<svc>`).
 *
 * Server API (page-relative, i.e. `ServerManager/<name>`): `GetDetail` and the verbs `Restart`, `Pause`,
 * `Continue`, `Stop`, `Start`, `Uninstall`, `Kill` (take the service name, return `SmServiceDetail`);
 * `ActivateConfig`, `UseMasterConfig`, `DeleteConfig` (take `SmConfigRequest` `{ServiceName, Config, IsMaster}`).
 */

/**
 * Starts an in-place reconciliation of the children of `targetElement`, so that polled lists only touch nodes
 * that actually changed (no flicker, focus is kept).
 * Usage: call `Add(e)` for every wanted child in order (use the returned node to attach expandos), then
 * `Complete()` to remove leftover children. Note: when an existing equal child is kept, the handlers of the
 * new element `e` are discarded and the old node keeps its old handlers.
 * @param {HTMLElement} targetElement - The element whose children are reconciled.
 * @returns {{Target:HTMLElement, Add:function(HTMLElement):HTMLElement, Complete:function():void}} The updater.
 */
function BeginElementChildUpdate(targetElement) {
    /** The target element. @type {HTMLElement} */
    const el = targetElement;
    /** Number of children the target had when the update began (decremented by `complete`). @type {number} */
    let ccl = el.children?.length ?? 0;
    /** Index of the next child position to fill. @type {number} */
    let cindex = 0;
    /**
     * Places `e` at the next child position. Past the original children it appends `e`. Otherwise it compares
     * the existing child at that index with `e` by `outerHTML`: if equal the existing child is kept and returned,
     * else it is replaced by `e`. Advances the index.
     * @param {HTMLElement} e - The wanted child element.
     * @returns {HTMLElement} The node now in place (the existing child or `e`); set expandos on this.
     */
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
    /**
     * Removes all original children at or after the current index (from the end backwards).
     * @returns {void}
     */
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

/**
 * Entry point of serviceInfo.html.
 * Reads URL params `p` (service name; shows `Fail` and returns if missing) and `v` (view-only, adds
 * `body.ViewOnly`); adds `body.TopWindow` when `window.IsTop`. Sets `document.title = "Service - <svc>"` and the
 * `<si-name>` text. Unless view-only, makes `si-state[2]` / `si-state[3]` (CPU / memory) and both `si-graph`
 * tiles clickable (`keyboardClick`) to open `server_metrics.html` with the `GetServiceCpu*` / `GetServiceMem*`
 * charts. Points the two graph iframes (`tabIndex -1`) at `../chart/chart.html?...&q=../ServerManager/GetServiceCpuChart?"<svc>"`
 * and `...GetServiceMemChart?"<svc>"` (the API argument is a JSON string literal in the query).
 * Unless view-only, builds the service control buttons (Restart, Pause, Resume -> `Continue`, Stop, Start,
 * Disable -> `Uninstall`, Kill after `Confirm`; each calls `DoVerb`) and the debug buttons (Explore, View log,
 * Deleted), and installs the real `updateButtons`. Then polls forever: `data = await sendRequest("GetDetail", svc)`,
 * `update(first)`, `await delayWithAbort(5000, abortWait)`; errors are shown with `Fail(e)`.
 * @returns {Promise<void>} Never resolves (unless `p` is missing).
 */
async function serviceInfoMain() {
    /** URL parameters. @type {URLSearchParams} */
    const p = getUrlParams();
    /** Name of the managed service (URL param `p`). @type {string} */
    const service = p.get("p");
    if (!service) {
        Fail("No serivce paramater supplied!");
        return;
    }
    /** True when URL param `v` is set: no controls, no click handlers, `body.ViewOnly`. @type {boolean} */
    const viewOnly = !!p.get("v");
    if (viewOnly)
        document.body.classList.add("ViewOnly");
    if (window.IsTop)
        document.body.classList.add("TopWindow");

    document.title = "Service - " + service;
    document.body.getElementsByTagName("si-name")[0].innerText = service;
    /**
     * The five `<si-state>` pills: [0] status, [1] process id, [2] CPU, [3] memory, [4] total CPU time.
     * @type {HTMLCollectionOf<HTMLElement>}
     */
    const states = document.body.getElementsByTagName("si-state");

    if (!viewOnly) {
        // CPU / memory pills open the service CPU / memory metrics page (serviceArg is declared below; only read on click).
        states[2].onclick = ev => {
            if (badClick(ev))
                return;
            Open("server_metrics.html?q1=GetServiceCpuChart" + serviceArg + "&q2=GetServiceCpuHistoryShortChart" + serviceArg + "&q3=GetServiceCpuHistoryChart" + serviceArg + "&title=" + service + " cpu use", "_self");
        };
        keyboardClick(states[2]);
        states[3].onclick = ev => {
            if (badClick(ev))
                return;
            Open("server_metrics.html?q1=GetServiceMemChart" + serviceArg + "&q2=GetServiceMemHistoryShortChart" + serviceArg + "&q3=GetServiceMemHistoryChart" + serviceArg + "&title=" + service + " memory use", "_self");
        };
        keyboardClick(states[3]);
    }

    /** The two `<si-graph>` tiles: [0] CPU chart, [1] memory chart. @type {HTMLCollectionOf<HTMLElement>} */
    const graphs = document.body.getElementsByTagName("si-graph");
    /** The iframe currently being configured. @type {HTMLIFrameElement} */
    let ifr = graphs[0].getElementsByTagName("iframe")[0];
    /** Query suffix for the per-service chart APIs: `?"<svc>"` (JSON string argument). @type {string} */
    const serviceArg = "?\"" + service + "\"";
    ifr.src = "../chart/chart.html?transparent=true&m=false&aspect=false&noLabels=true&center=true&q=../ServerManager/GetServiceCpuChart" + serviceArg;
    ifr.tabIndex = "-1";
    ifr = graphs[1].getElementsByTagName("iframe")[0];
    ifr.tabIndex = "-1";
    ifr.src = "../chart/chart.html?transparent=true&m=false&aspect=false&noLabels=true&center=true&q=../ServerManager/GetServiceMemChart" + serviceArg;
    if (!viewOnly) {
        // Graph tiles open the same metrics pages as the CPU / memory pills.
        graphs[0].onclick = ev => {
            if (badClick(ev))
                return;
            Open("server_metrics.html?q1=GetServiceCpuChart" + serviceArg + "&q2=GetServiceCpuHistoryShortChart" + serviceArg + "&q3=GetServiceCpuHistoryChart" + serviceArg + "&title=" + service + " cpu use", "_self");
        };
        graphs[1].onclick = ev => {
            if (badClick(ev))
                return;
            Open("server_metrics.html?q1=GetServiceMemChart" + serviceArg + "&q2=GetServiceMemHistoryShortChart" + serviceArg + "&q3=GetServiceMemHistoryChart" + serviceArg + "&title=" + service + " memory use", "_self");
        };
        keyboardClick(graphs[0]);
        keyboardClick(graphs[1]);
    }
    /** Aborts the 5 s polling delay so the next `GetDetail` runs immediately (see `updateNow`). @type {AbortHandler} */
    const abortWait = new AbortHandler();
    /**
     * Enables / disables the control buttons for a service status. A no-op in view-only mode; otherwise replaced
     * below by a closure (see the comment there).
     * @type {function(string):void}
     */
    let updateButtons = () => { };

    /**
     * Ends the current polling delay (`abortWait.raise()`) so the next `GetDetail` poll runs immediately.
     * @returns {void}
     */
    function updateNow() {
        abortWait.raise();
    }

    /** "Explore" debug button (null in view-only mode). @type {?Button} */
    let exploreButton = null;
    /** "View log" debug button (null in view-only mode). @type {?Button} */
    let logButton = null;
    /** "Deleted" debug button (null in view-only mode). @type {?Button} */
    let deletedButton = null;
    if (!viewOnly) {

        /** The `<si-servicebuttons>` row holding the control buttons. @type {HTMLElement} */
        const serviceButtons = document.body.getElementsByTagName("si-servicebuttons")[0];

        /** True while a verb runs; suppresses the control-button part of `updateButtons`. @type {boolean} */
        let blockButtonStates = false;
        /**
         * Runs a service verb: sets `blockButtonStates`, calls `button.StartWorking()`, disables every control
         * button (through the `.Button` expando of the children of `<si-servicebuttons>`) and awaits
         * `sendRequest(v, service)`. On a falsy result shows `Fail("Failed to <v>")`, clears the block and raises
         * `abortWait`. On success stores the returned `SmServiceDetail` in `data`, clears the block, calls `updateNow()`
         * and shows `Info(ValueFormat.stringFormat(okText, service))`. On exception shows `Fail`, clears the block
         * and raises `abortWait`. Always calls `button.StopWorking()`.
         * @param {Button} button - The button that was clicked.
         * @param {string} v - Verb / API name: "Restart", "Pause", "Continue", "Stop", "Start", "Uninstall" or "Kill".
         * @param {string} okText - Success message; `{0}` is replaced with the service name.
         * @returns {Promise<void>}
         */
        async function DoVerb(button, v, okText) {
            try {
                blockButtonStates = true;
                button.StartWorking();
                [].forEach.call(serviceButtons.children, e => e.Button.SetEnabled(false));
                const newData = await sendRequest(v, service);
                if (!newData) {
                    Fail("Failed to " + v.toLowerCase());
                    blockButtonStates = false;
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
        /** Control buttons (common.js `Button`; icon class `si-icon-*` from serviceInfo.css). @type {Button} */
        const restartButton = new Button(null, "Restart", "Click to restart the service", "si-icon-restart", false, async () => {
            await DoVerb(restartButton, "Restart", "Restarted {0}");
        });

        /** @type {Button} */
        const pauseButton = new Button(null, "Pause", "Click to pause the service", "si-icon-pause", false, async () => {
            await DoVerb(pauseButton, "Pause", "Paused {0}");
        });

        /** "Resume" sends the verb `Continue`. @type {Button} */
        const resumeButton = new Button(null, "Resume", "Click to resume the service", "si-icon-resume", false, async () => {
            await DoVerb(resumeButton, "Continue", "Resumed {0}");
        });

        /** @type {Button} */
        const stopButton = new Button(null, "Stop", "Click to stop the service", "si-icon-stop", false, async () => {
            await DoVerb(stopButton, "Stop", "Stopped {0}");
        });

        /** @type {Button} */
        const startButton = new Button(null, "Start", "Click to start the service", "si-icon-start", false, async () => {
            await DoVerb(startButton, "Start", "Started {0}");
        });

        /** "Disable" sends the verb `Uninstall`. @type {Button} */
        const disableButton = new Button(null, "Disable", "Click to disable the service", "si-icon-disable", false, async () => {
            await DoVerb(disableButton, "Uninstall", "Disabled {0}");
        });

        /** "Kill" asks for confirmation before sending the verb `Kill`. @type {Button} */
        const killButton = new Button(null, "Kill", "Click to kill the service process.\nWARNING! This can cause problems!\nTry to stop the service first.", "si-icon-kill", false, async () => {
            if (await Confirm("Kill",
                "Forcefully kill the service process?\n\n" +
                "WARNING! This can cause errors!\n\n" +
                "Are you sure?",
                "Yes, Kill!",
                "No, Leave it!",
                "../icons/skull.svg",
                "../icons/preview_play.svg",
                "Click to terminate the service process",
                "Click to leave it running")) {
                await DoVerb(killButton, "Kill", "Terminated {0}");
            }
        });

        serviceButtons.appendChild(restartButton.Element);
        serviceButtons.appendChild(pauseButton.Element);
        serviceButtons.appendChild(resumeButton.Element);
        serviceButtons.appendChild(stopButton.Element);
        serviceButtons.appendChild(startButton.Element);
        serviceButtons.appendChild(disableButton.Element);
        serviceButtons.appendChild(killButton.Element);


        /**
         * The `<si-debugbuttons>` row: Explore (`../FolderSync/ManagedFolders/<svc>/explore`), View log
         * (`../edit/text.html?scrollToEnd=true&r=../FolderSync/ManagedFolders/<svc>/<data.Log.Name>&d=../ServerManager/DeleteLogFile`)
         * and Deleted (`../ServerManager/ServiceBak/<svc>/explore`); all open in place.
         * @type {HTMLElement}
         */
        const debugButtons = document.body.getElementsByTagName("si-debugbuttons")[0];
        exploreButton = new Button(null, "Explore", "Explore the active files", "si-icon-explore", true, async () => {
            Open("../FolderSync/ManagedFolders/" + service + "/explore", "_self");
        });

        logButton = new Button(null, "View log", "View the current log", "si-icon-log", false, async () => {
            Open("../edit/text.html?scrollToEnd=true&r=../FolderSync/ManagedFolders/" + service + "/" + data.Log.Name
                + "&d=../ServerManager/DeleteLogFile", "_self");

        });

        deletedButton = new Button(null, "Deleted", "Explore the backup of deleted / modified files", "../icons/trash.svg", true, async () => {
            Open("../ServerManager/ServiceBak/" + service + "/explore", "_self");
        });



        debugButtons.appendChild(exploreButton.Element);
        debugButtons.appendChild(logButton.Element);
        debugButtons.appendChild(deletedButton.Element);

        /**
         * Real `updateButtons(status)`: unless `blockButtonStates` is set, enables Restart if "Running"; and, only
         * when the service is not the server manager itself (`!data.IsSm`): Pause if Running; Resume if "Paused";
         * Stop if Running/Paused; Start if "Stopped"/"NotInstalled"/"Unknown"; Disable if Stopped/Running/Paused;
         * Kill if `data.ProcId > 0` (combined with bitwise `&`, so `SetEnabled` receives 0/1). Always enables
         * View log iff `data.Log` is set.
         * @param {string} status - Service status string from `data.Status`.
         * @returns {void}
         */
        updateButtons = status => {
            if (!blockButtonStates) {
                const isNotSm = !data.IsSm;
                const isRunning = status === "Running";
                const isPaused = status === "Paused";
                const isStopped = status === "Stopped";
                restartButton.SetEnabled(isRunning);
                pauseButton.SetEnabled(isNotSm & isRunning);
                resumeButton.SetEnabled(isNotSm & isPaused);
                stopButton.SetEnabled(isNotSm & (isRunning || isPaused));
                startButton.SetEnabled(isNotSm & (isStopped || status === "NotInstalled" || status === "Unknown"));
                disableButton.SetEnabled(isNotSm & (isStopped || isRunning || isPaused));
                killButton.SetEnabled(isNotSm & (data.ProcId > 0));
            }
            logButton.SetEnabled(!!data.Log);

        };
    }

    /**
     * Service status -> CSS class of `si-state[0]`: Running -> Ok; StartPending, ContinuePending -> Warning;
     * Unknown, NotInstalled, Stopped, StopPending, Paused, PausePending -> Error.
     * @type {Map<string,string>}
     */
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





    /** Grid of the active (current version) config files. @type {HTMLElement} */
    const configs = document.body.getElementsByTagName("si-configs")[0];
    /** Upload target for the active config files (repo `Current_<svc>`). @type {HTMLElement} */
    const uploads = document.body.getElementsByTagName("si-uploads")[0];
    /** Grid of the master config files. @type {HTMLElement} */
    const masterConfigs = document.body.getElementsByTagName("si-configs")[1];
    /** Upload target for the master config files (repo `Master_<svc>`). @type {HTMLElement} */
    const masterUploads = document.body.getElementsByTagName("si-uploads")[1];
    /** Grid of the uploaded versions. @type {HTMLElement} */
    const versions = document.body.getElementsByTagName("si-versions")[0];

    /**
     * Character index -> expected separator for the old backup timestamp `yyyy-MM-dd_HH_mm_ss` (used by `isBackup`).
     * @type {Map<number,string>}
     */
    const bakMap = new Map();
    bakMap.set(4, '-');
    bakMap.set(7, '-');
    bakMap.set(10, '_');
    bakMap.set(13, '_');
    bakMap.set(16, '_');


    /**
     * Character index -> expected separator for the new backup prefix
     * `Bak_yyyy-MM-dd_HH_mm_ss_yyyy-MM-dd_HH_mm_ss` (used by `isBackupNew`).
     * @type {Map<number,string>}
     */
    const bakMapNew = new Map();
    bakMapNew.set(8, '-');
    bakMapNew.set(11, '-');
    bakMapNew.set(14, '_');
    bakMapNew.set(17, '_');
    bakMapNew.set(20, '_');
    bakMapNew.set(23, '_');
    bakMapNew.set(28, '-');
    bakMapNew.set(31, '-');
    bakMapNew.set(34, '_');
    bakMapNew.set(37, '_');
    bakMapNew.set(40, '_');


    // "Bak_2012-12-01_09_40_20_2012-12-01_09_40_20.File.json" or "Bak_2012-12-01_09_40_20_2012-12-01_09_40_20_12.File.json" (same as ServiceHost.NewIsConfigBackupName)
    /**
     * Tests for the new backup file name format `Bak_yyyy-MM-dd_HH_mm_ss_yyyy-MM-dd_HH_mm_ss[_<digits>].<rest>`:
     * must start with "Bak_", the part before the first "." must be at least 43 chars, positions 4-42 must be
     * digits except the separators in `bakMapNew`, and anything after position 43 must be "_" followed by digits.
     * Must stay in sync with C# `ServiceHost.NewIsConfigBackupName`.
     * @param {string} fn - File name.
     * @returns {boolean} True if `fn` is a backup in the new format.
     */
    function isBackupNew(fn) {
        if (fn.substring(0, 4) !== "Bak_")
            return false;
        let e = fn.indexOf('.');
        if (e < 0)
            e = fn.length;
        if (e < 43)
            return false;
        fn = fn.substring(0, e);
        for (let i = 4; i < 43; ++i)
        {
            const c = fn[i];
            const m = bakMapNew.get(i);
            if (m) {
                if (c === m)
                    continue;
            }
            if (c < '0')
                return false;
            if (c > '9')
                return false;
        }
        if (e === 43)
            return true;
        if (fn[43] != '_')
            return false;
        for (let i = 44; i < e; ++i)
        {
            const c = fn[i];
            if (c < '0')
                return false;
            if (c > '9')
                return false;
        }
        return true;
    }

    /**
     * Tests whether a config file name is a backup: true if `isBackupNew(fn)`, or if the second-to-last dot
     * segment (`name.<seg>.ext`) is "LastGood" or matches `yyyy-MM-dd_HH_mm_ss[_<digits>]` (first 19 chars checked
     * against `bakMap`, the rest must be "_" plus digits).
     * @param {string} fn - File name.
     * @returns {boolean} True if `fn` is a backup (old or new format).
     */
    function isBackup(fn) {
        if (isBackupNew(fn))
            return true;
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
        const len = fn.length;
        if (len < 19)
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
        if (len === 19)
            return true;
        if (fn.charAt(19) !== '_')
            return false;
        for (let i = 20; i < len; ++i)
        {
            const c = fn.charAt(i);
            if (c < '0')
                return false;
            if (c > '9')
                return false;
        }
        return true;
    }

    /**
     * Reconciles a config file grid (`<si-configs>`) via `BeginElementChildUpdate`: a `<si-file-header>`, then five
     * cells per file: `<si-file-state>` (class `Show` = not a backup per `isBackup`, shown as a star),
     * `<si-file-icon>` (background `../icons/ext/<ext>.svg`; expando `.Config` = the file record, set on the node
     * returned by `Add`), `<si-file-name>` (click opens `../edit/text.html?r=<folderSuffix><svc>/<name>&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile`),
     * `<si-file-size>` (byte size, copy-on-click) and `<si-file-time>` (last modified, copy-on-click).
     * Unless view-only, the icon gets click / keyboard / right-click handlers opening a `PopUpMenu` (popup element
     * gets class `Rel`) with a `WebMenu` named "SmConfigFiles": View (`../logFile/logfile.html?api=...`), Activate
     * (backups only; `ActivateConfig`), Use as current (master only; `UseMasterConfig`), Copy file name, Copy local
     * path (`<data.Folder or data.CurrentFolder>\<name>`), Copy local folder, Download (`downloadFile`) and Delete
     * (`Confirm`, then `DeleteConfig`). Successful server actions call `updateNow()`.
     * @param {HTMLElement} el - The `<si-configs>` grid.
     * @param {?Array<{Name:string, Size:number, LastModified:string}>} files - The config files.
     * @param {string} headerText - Header text.
     * @param {?string} headerTitle - Header tooltip.
     * @param {string} [folderSuffix="../FolderSync/ManagedFolders/"] - URL prefix of the folder that serves the files (`<prefix><svc>/<file>`).
     * @param {boolean} [isMaster=false] - True for the master config list (uses `data.Folder`, adds "Use as current").
     * @returns {void}
     */
    function updateFileList(el, files, headerText, headerTitle, folderSuffix, isMaster) {

        isMaster = !!isMaster;
        const updater = BeginElementChildUpdate(el)
        if (!folderSuffix)
            folderSuffix = "../FolderSync/ManagedFolders/";
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
                        // Icon click: pop up the file options menu for the file in the `.Config` expando of the clicked node.
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

                                // The Download tooltip is formatted with the file name.
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Download", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "../icons/disc.svg",
                                    Title: _T("Click to download the \"{0}\" config", fn, "The tool tip description of a menu option that when clicked will exit from full screen mode"),
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
                                            //downloadFile(fn, folderSuffix + service + "/" + fn);
                                            //await delay(500);
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
                        Open("../edit/text.html?r=" + folderSuffix + service + "/" + fn + 
                            "&u=../ServerManager/UpdateConfig" +
                            "&d=../ServerManager/DeleteConfigFile"
                        , "_self");
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



    /**
     * Last `SmServiceDetail` from `GetDetail` or a verb (Status, ProcId, CpuUsage, MemUsage, TotalProcessorTime,
     * IsSm, Log, Folder, CurrentFolder, Configs, MasterConfigs, Versions).
     * @type {?object}
     */
    let data = null;
    /**
     * Renders the latest `data`.
     * On `first`: calls `PageLoaded()` and sets the tooltips of Explore / View log and both upload targets with
     * the folder paths. Writes `data.Status` into `si-state[0]` and replaces its classes with `stateMap.get(status)`.
     * Adds / removes `Hide` on `si-state[1..4]` when `ProcId === 0`, and writes "Id:", "Cpu: <n> %", "Mem: <bytes>",
     * "Tot: <timespan>". If `data.Configs` exists: `updateFileList(configs, ...)`, `configs.Show`, and on first
     * (not view-only) `fileUploaderSetup(uploads, "Current_<svc>", null, UploadCompleted, null, true)` +
     * `keyboardClick` + `Show`; else removes `Show` from `configs` only. Same for `data.MasterConfigs` (folder
     * `../ServerManager/Data/`, `isMaster = true`, repo `Master_<svc>`; else removes `Show` from both master
     * elements). Reconciles `<si-versions>` (header plus six cells per version) and finally calls `updateButtons(status)`.
     * @param {boolean} [first] - True on the first successful poll.
     * @returns {void}
     */
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
        states[2].innerText = "Cpu: " + ValueFormat.toString(data.CpuUsage, 2) + " %";
        states[3].innerText = "Mem: " + ValueFormat.formatByteSize(data.MemUsage);
        states[4].innerText = "Tot: " + ValueFormat.formatTimeSpan(data.TotalProcessorTime);

        /**
         * Upload-finished callback passed to both `fileUploaderSetup` calls (copy of the one in keys.js).
         * If `res.Error` is set shows `Fail(err)`. Otherwise reports only one message: the first status that is
         * neither `UploadStatus.AlreadyUploaded` nor `UploadStatus.None` -> `Fail`; else the first
         * `AlreadyUploaded` -> `Info`; else the first `None` (success) -> `Info` and `updateNow()`.
         * @param {HTMLElement} e - The upload element (`<si-uploads>`).
         * @param {{Error:?string, Status:Array<number>}} res - Upload result; `Status` holds one `UploadStatus` value per file.
         * @param {File[]|FileList} files - The uploaded files (same order as `res.Status`).
         * @returns {void}
         */
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

        // Versions grid: <si-version-header>, then per version active star, uploaded time (click opens
        // versionInfo.html?p=<svc>,<Uploaded>,<Name>), user, machine, comment (copy-on-click) and last used time.
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
    // Endless polling loop: fetch the service detail, render it, wait 5 s (or until updateNow()).
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