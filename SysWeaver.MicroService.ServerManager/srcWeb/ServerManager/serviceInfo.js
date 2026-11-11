/**
 * @file serviceInfo.js - the ServerManager "service info" page (serviceInfo.html, entry `serviceInfoMain`): details of
 * one managed service: state, cpu / memory use, control buttons, files, the active and master config files (with upload
 * targets) and the uploaded versions. Opened from the services table (`serviceInfo.html?p=<svc>`).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js, common/md5.js, common/UZIP.js,
 * common/fileUploader.js and smShared.js; started from `<body onload>`.
 *
 * Design v2 page anatomy (Design v2, PORTING.md B11), built from the shared ui components (ui/page.js):
 *  - Sw.PageLayout: page header (the service name, its status badge, the seven control verbs as the page actions: always shown, the ones
 *    that don't apply to the current state disabled with the reason, as on the original page; Kill as a danger outline with a confirmation),
 *  - Sw.Facts: the facts strip (Process ID, Executable, Total CPU time) instead of the old state pills; the executable opens a menu
 *    to select it (ExeMenu: "Automatic" or one of the executables found in the active folder),
 *  - Sw.Kpi: CPU and memory tiles with meters (a click opens the metric page),
 *  - Sw.Card: the CPU / memory charts (SmShared.ChartTile of GetServiceCpuChart / GetServiceMemChart, as the original page),
 *  - Sw.Card: "Files" (folders, log; Explore / View log / Deleted files), "Active configuration" and "Master configuration" (row lists
 *    with an upload button in the card head, a row menu) and "Versions" (row list; a row opens versionInfo.html).
 *
 * Url parameters: `p` = service name (required), `v` = view only (any non empty value: no controls, menus, uploads
 * or links; class ViewOnly on body).
 *
 * Server API (relative to ServerManager/): GetDetail(name) -> SmServiceDetail (polled every 5 s, a request cache of
 * 4 s on the server), the verbs Restart, Pause, Continue, Stop, Start, Uninstall, Kill (name -> SmServiceDetail),
 * SelectExe ({ServiceName, ExeName}, an empty name = automatic -> SmServiceDetail),
 * ActivateConfig, UseMasterConfig, DeleteConfig ({ServiceName, Config, IsMaster} -> bool); upload repos
 * `Current_<svc>` and `Master_<svc>`; the chart tiles GetServiceCpuChart / GetServiceMemChart (?"<svc>"). The same requests as the original page.
 *
 * Globals defined: `serviceInfoMain`, `SmServiceInfo` (the page class), `smIsConfigBackup`.
 */

/**
 * Service status -> state kind (the badge kind of the status: ok / warning / error).
 * @type {Object<string, string>}
 */
const SmStateKinds = Object.freeze({
    Unknown: "error",
    NotInstalled: "error",
    Stopped: "error",
    StartPending: "warning",
    StopPending: "error",
    Running: "ok",
    ContinuePending: "warning",
    Paused: "error",
    PausePending: "error",
});

/**
 * Test if a config file name is a backup. Must match C# `ServiceHost.NewIsConfigBackupName` /
 * `IsConfigBackupName`:
 * - new format: `Bak_yyyy-MM-dd_HH_mm_ss_yyyy-MM-dd_HH_mm_ss[_<digits>].<rest>`,
 * - old format: `name.yyyy-MM-dd_HH_mm_ss[_<digits>].ext` or `name.LastGood.ext`.
 * @param {string} fn - File name.
 * @returns {boolean} True for a backup.
 */
function smIsConfigBackup(fn) {
    const ts = "\\d{4}-\\d{2}-\\d{2}_\\d{2}_\\d{2}_\\d{2}";
    if (new RegExp("^Bak_" + ts + "_" + ts + "(_\\d*)?(\\.|$)").test(fn))
        return true;
    const parts = fn.split(".");
    if (parts.length < 3)
        return false;
    const seg = parts[parts.length - 2];
    return seg === "LastGood" || new RegExp("^" + ts + "(_\\d*)?$").test(seg);
}

/**
 * The service info page.
 */
class SmServiceInfo {
    /**
     * @param {string} service - The service name.
     * @param {boolean} viewOnly - View only mode.
     */
    constructor(service, viewOnly) {
        /** @type {string} */
        this.Service = service;
        /** @type {boolean} */
        this.ViewOnly = viewOnly;
        /** @type {string} Api argument suffix of the per service charts: ?"<svc>" (a json string). */
        this.Arg = "?" + JSON.stringify(service);
        /** @type {?object} The last SmServiceDetail. */
        this.Data = null;
        /** @type {Sw.Util.AbortHandler} Ends the poll delay (poll now). */
        this.Wake = new Sw.Util.AbortHandler();
        /** @type {boolean} A poll was requested while no delay was running. */
        this.WakePending = false;
        /** @type {boolean} True while a verb runs (the control buttons stay disabled). */
        this.Busy = false;
        /** @type {Object<string, Sw.Button>} Control buttons by verb. */
        this.Verbs = {};
        /** @type {Object<string, Sw.Button>} File buttons (Explore, Log, Deleted). */
        this.Debug = {};
        /** Page parts (Build). */
        this.El = {};
    }

    /**
     * The metric page url of the cpu ("Cpu") or memory ("Mem") use.
     * @param {string} kind - "Cpu" or "Mem".
     * @returns {string} The url.
     */
    MetricsUrl(kind) {
        const t = kind === "Cpu"
            ? _T("{0} cpu use", this.Service, "Title of a page showing the cpu use of a service.{0} is replaced with the service name")
            : _T("{0} memory use", this.Service, "Title of a page showing the memory use of a service.{0} is replaced with the service name");
        return SmShared.MetricsUrl("GetService" + kind + "Chart" + this.Arg, t);
    }

    /**
     * Open the metric detail page of the cpu ("Cpu") or memory ("Mem") use.
     * @param {string} kind - "Cpu" or "Mem".
     */
    OpenMetrics(kind) {
        Sw.Page.Navigate(this.MetricsUrl(kind), "_self");
    }

    /**
     * @returns {string} The page title ("Service - <name>").
     */
    Title() {
        return _T("Service - {0}", this.Service, "Title of the service details page.{0} is replaced with the service name");
    }

    /**
     * Build the page (header, facts, KPI tiles, charts, file and version cards).
     */
    Build() {
        const t = this;
        const el = t.El;
        //  The service name is url text until the server confirmed it (Poll): plain, limited text (Sw.Page.SetLinkTitle)
        Sw.Page.SetLinkTitle(t.Title());
        const showDetails = _TF("Show details.", "Tool tip line of a clickable statistic");
        t.Page = Sw.PageLayout({
            Main: ".si-page",
            Type: "dash",
            Title: Sw.Page.LinkText(t.Service, 80),
            MenuTitle: false,
            Description: _TF("A managed service: its state, resource use, files, configuration and versions.", "Description of the service details page"),
        });
        if (!t.ViewOnly)
            t.BuildServiceButtons();

        el.Facts = Sw.Facts({ Label: _TF("Service", "Accessible name of the service facts strip") });
        el.Pid = el.Facts.Add({ Label: _TF("Process ID", "Label of the service process id in the facts strip"), Value: "", Title: _TF("The operating system id of the running service process", "Tool tip of the service process id in the facts strip of the service info page") });
        el.Exe = el.Facts.Add({
            Label: _TF("Executable", "Label of the service executable in the facts strip"), Value: "",
            OnClick: t.ViewOnly ? undefined : () => t.ExeMenu(el.Exe.ValueE),
        });
        el.Tot = el.Facts.Add({ Label: _TF("Total CPU time", "Label of the service total cpu time in the facts strip"), Value: "", Title: _TF("The processor time the service process has used since it started (all cores added)", "Tool tip of the service total cpu time in the facts strip of the service info page") });

        //  The memory tile has no meter: the original page shows the memory use alone (no request for the server memory size)
        const kpi = (kind, label, icon, title) => Sw.Kpi({
            Label: label, Icon: icon, Unit: kind === "Cpu" ? "%" : "", Meter: kind === "Cpu" ? { Max: 100, Warn: 0.8, Danger: 0.92 } : undefined, Span: 6,
            Href: t.ViewOnly ? null : t.MetricsUrl(kind), Title: t.ViewOnly ? title : SmShared.Lines(title, "", showDetails),
        });
        el.Cpu = kpi("Cpu", _TF("CPU", "Label of the cpu use tile of the service page"), "cpu", _TF("Current cpu usage.", "Tool tip of the service cpu usage"));
        el.Mem = kpi("Mem", _TF("Memory", "Label of the memory use tile of the service page"), "memory", _TF("Current memory usage.", "Tool tip of the service memory usage"));

        //  The cpu and memory charts of the original page (GetServiceCpuChart / GetServiceMemChart in chart tiles, the same requests); a click opens
        //  the metric page (not in view only mode)
        const chart = (kind, title) => Sw.Card({
            Title: title, Span: 6, Flush: true, Class: "sm-linecard si-chartcard",
            Body: SmShared.ChartTile({
                Api: "GetService" + kind + "Chart" + t.Arg, Extra: "&aspect=false",
                Title: kind === "Cpu" ? _TF("Show cpu usage details", "Tool tip of the cpu chart of the service page") : _TF("Show memory usage details", "Tool tip of the memory chart of the service page"),
                OnClick: t.ViewOnly ? null : () => t.OpenMetrics(kind),
            }),
        });

        //  Files: folders and the log (Explore / View log / Deleted files)
        el.FileFacts = Sw.Facts({ Card: false, Label: _TF("Files", "Accessible name of the file facts of the service page") });
        el.CurFolder = el.FileFacts.Add({ Label: _TF("Active folder", "Label of the folder of the active service files"), Value: "", Mono: true });
        el.MasterFolder = el.FileFacts.Add({ Label: _TF("Master folder", "Label of the folder of the master config files"), Value: "", Mono: true });
        el.LogFile = el.FileFacts.Add({ Label: _TF("Log file", "Label of the current log file of the service"), Value: "", Mono: true });
        el.Files = Sw.Card({ Title: _TF("Files", "Title of the files card of the service page"), Span: 12, Flush: true, Body: el.FileFacts, Tools: t.ViewOnly ? null : t.BuildDebugButtons() });

        const located = _TF("Located in this folder:", "Tool tip line before a folder path");
        const configCard = (title, repo, isMaster) => {
            const list = Sw.el("ul.sw-rows.si-files");
            const c = Sw.Card({ Title: title, Span: 6, Flush: true, Body: list, Class: "si-configcard" });
            c.ListE = list;
            c.Repo = repo;
            c.IsMaster = isMaster;
            c.E.hidden = true;
            return c;
        };
        el.Configs = configCard(_TF("Active configuration", "Header of the list of active config files"), "Current_" + t.Service, false);
        el.Master = configCard(_TF("Master configuration", "Header of the list of master config files"), "Master_" + t.Service, true);
        el.VersionList = Sw.el("ul.sw-rows.si-versions");
        el.Versions = Sw.Card({ Title: _TF("Versions", "Header of the list of service versions"), Sub: _TF("All available versions of this service", "Tool tip of the list of service versions"), Span: 12, Flush: true, Body: el.VersionList });
        el.Versions.E.hidden = true;

        t.Page.Add(el.Facts,
            Sw.Grid({ Label: _TF("Resource use", "Accessible name of the KPI tiles of the service page"), Content: [el.Cpu, el.Mem] }),
            Sw.Grid({ Label: _TF("Charts", "Accessible name of the service chart area"), Content: [
                chart("Cpu", _TF("CPU use", "Title of the cpu chart of the service page")),
                chart("Mem", _TF("Memory use", "Title of the memory chart of the service page"))] }),
            Sw.Grid({ Content: [el.Files, el.Configs, el.Master, el.Versions] }));
        t.Located = located;
    }

    /**
     * Create the service control buttons (Restart, Pause, Resume, Stop, Start, Disable, Kill) as the page header actions.
     */
    BuildServiceButtons() {
        const t = this;
        const add = (verb, text, title, icon, okText, confirm) => {
            const b = new Sw.Button({
                Text: text, Title: title, Icon: icon, Disabled: _TF("Wait until the service status is loaded", "Why a service control button (ex: Restart, Stop) is disabled before the status of the service is known"), Kind: verb === "Kill" ? "danger" : undefined,
                OnClick: async () => {
                    if (confirm && !await confirm())
                        return;
                    await t.DoVerb(verb, okText);
                },
            });
            t.Verbs[verb] = b;
            t.Page.Header.AddAction(b);
            return b;
        };
        add("Restart", _TF("Restart", "Text of a button that restarts a service"), _TF("Restart the service", "Tool tip of a button that restarts a service"), "refresh",
            _T("Restarted {0}", t.Service, "Message when a service was restarted.{0} is replaced with the service name"));
        add("Pause", _TF("Pause", "Text of a button that pauses a service"), _TF("Pause the service", "Tool tip of a button that pauses a service"), "pause",
            _T("Paused {0}", t.Service, "Message when a service was paused.{0} is replaced with the service name"));
        add("Continue", _TF("Resume", "Text of a button that resumes a paused service"), _TF("Resume the service", "Tool tip of a button that resumes a paused service"), "play",
            _T("Resumed {0}", t.Service, "Message when a service was resumed.{0} is replaced with the service name"));
        add("Stop", _TF("Stop", "Text of a button that stops a service"), _TF("Stop the service", "Tool tip of a button that stops a service"), "stop",
            _T("Stopped {0}", t.Service, "Message when a service was stopped.{0} is replaced with the service name"));
        add("Start", _TF("Start", "Text of a button that starts a service"), _TF("Start the service", "Tool tip of a button that starts a service"), "play",
            _T("Started {0}", t.Service, "Message when a service was started.{0} is replaced with the service name"));
        add("Uninstall", _TF("Disable", "Text of a button that disables (uninstalls) a service"), _TF("Disable the service", "Tool tip of a button that disables a service"), "close",
            _T("Disabled {0}", t.Service, "Message when a service was disabled.{0} is replaced with the service name"));
        add("Kill", _TF("Terminate...", "Text of a button that stops a service process at once (asks for a confirmation first)"),
            SmShared.Lines(
                _TF("Terminate the service process.", "Tool tip of a button that stops a service process at once"),
                _TF("WARNING! This can cause problems!", "Tool tip of a button that kills a service process"),
                _TF("Try to stop the service first.", "Tool tip of a button that kills a service process")),
            "kill",
            _T("Terminated {0}", t.Service, "Message when a service process was killed.{0} is replaced with the service name"),
            //  The shared danger confirmation (names the service; "Cancel" focused, the danger button names the action)
            () => Sw.Cells.ConfirmDanger({
                Title: SmShared.DangerTitle(_TF("Terminate", "Verb in the title of a dialog asking to stop a service process at once (Terminate <service>?)"), t.Service, () => _T("Kill the process of {0}?", t.Service, "Title of a dialog asking to kill a service process, {0} is the service name")),
                Item: t.Service,
                ItemLabel: _TF("Service:", "Label before the service name in a confirmation dialog"),
                Text: _TF("The process is ended at once, without shutting down: work in progress is lost and errors may follow. Try Stop first.", "Text of a dialog asking to kill a service process"),
                OkText: _T("Terminate {0}", t.Service, "Text of the danger button that confirms stopping a service process at once, {0} is the service name"),
                OkIcon: "kill",
                CancelTitle: _TF("Close this dialog, the service keeps running", "Tool tip of the Cancel button of the dialog asking to kill a service process"),
            }));
    }

    /**
     * Create the file buttons (Explore, View log, Deleted files) for the head of the Files card.
     * @returns {Sw.Button[]} The buttons.
     */
    BuildDebugButtons() {
        const t = this;
        const svc = encodeURIComponent(t.Service);
        t.Debug.Explore = new Sw.Button({
            Text: _TF("Explore", "Text of a button that explores the files of a service"),
            Title: _TF("Explore the active files", "Tool tip of a button that explores the files of a service"),
            Icon: "folder",
            Small: true,
            OnClick: () => Sw.Page.Navigate("../FolderSync/ManagedFolders/" + svc + "/explore", "_self"),
        });
        t.Debug.Log = new Sw.Button({
            Text: _TF("View log", "Text of a button that shows the log file of a service"),
            Title: _TF("View the current log", "Tool tip of a button that shows the log file of a service"),
            Icon: "log",
            Small: true,
            Disabled: _TF("The service has no log file yet", "Why the view log button of a service is disabled"),
            OnClick: () => {
                const log = t.Data && t.Data.Log;
                if (log)
                    Sw.Page.Navigate("../edit/text.html?scrollToEnd=true&r=" + encodeURIComponent("../FolderSync/ManagedFolders/" + t.Service + "/" + log.Name) + "&d=../ServerManager/DeleteLogFile", "_self");
            },
        });
        t.Debug.Deleted = new Sw.Button({
            Text: _TF("Deleted files", "Text of a button that explores deleted files"),
            Title: _TF("Explore the backup of deleted / modified files", "Tool tip of a button that explores the deleted files of a service"),
            Icon: "trash",
            Small: true,
            OnClick: () => Sw.Page.Navigate("../ServerManager/ServiceBak/" + svc + "/explore", "_self"),
        });
        return [t.Debug.Explore, t.Debug.Log, t.Debug.Deleted];
    }

    /**
     * Run a service verb (all control buttons disabled meanwhile), then show the result and poll.
     * @param {string} verb - Api name: Restart, Pause, Continue, Stop, Start, Uninstall or Kill.
     * @param {string} okText - Success message.
     * @returns {Promise<void>}
     */
    async DoVerb(verb, okText) {
        const t = this;
        t.Busy = true;
        Object.values(t.Verbs).forEach(b => b.SetDisabled(true, _TF("Wait until the current service command is done", "Why the service control buttons (ex: Restart, Stop) are disabled while a command runs")));
        try {
            const d = await Sw.Net.Send(verb, t.Service);
            if (!d) {
                Sw.Fail(_T("Failed to {0}", verb.toLowerCase(), "Error message when a service command failed.{0} is replaced with the command"));
                return;
            }
            t.Data = d;
            Sw.Info(okText);
        }
        catch (e) {
            Sw.Fail(_T("Failed to {0}, error: {1}", verb.toLowerCase(), e.message, "Error message when a service command failed.{0} is replaced with the command, {1} with the error"));
        }
        finally {
            t.Busy = false;
            if (t.Data)
                t.UpdateButtons();
            t.WakeUp();
        }
    }

    /**
     * Enable the control buttons that apply to the current state (unless a verb runs). All seven are always shown, the others are
     * disabled with the reason, as on the original page.
     */
    UpdateButtons() {
        const t = this;
        const d = t.Data;
        if (t.ViewOnly || !d)
            return;
        if (!t.Busy) {
            const s = d.Status;
            const notSm = !d.IsSm;
            const running = s === "Running";
            const paused = s === "Paused";
            const stopped = s === "Stopped";
            const en = {
                Restart: running,
                Pause: notSm && running,
                Continue: notSm && paused,
                Stop: notSm && (running || paused),
                Start: notSm && (stopped || s === "NotInstalled" || s === "Unknown"),
                Uninstall: notSm && (stopped || running || paused),
                Kill: notSm && d.ProcId > 0,
            };
            const why = notSm
                ? _T("Not possible while the service is {0}", s, "Why a service control button (ex: Pause, Stop) is disabled.{0} is replaced with the service status, ex: Stopped")
                : _TF("The server manager can't do this to itself", "Why a service control button (ex: Pause, Stop) is disabled on the server manager service");
            for (const [k, v] of Object.entries(en))
                t.Verbs[k].SetDisabled(!v, why);
        }
        if (t.Debug.Log)
            t.Debug.Log.SetDisabled(!d.Log, _TF("The service has no log file yet", "Why the view log button of a service is disabled"));
    }

    /**
     * Show the executable selection menu: "Automatic" (the best guess, replaced if it doesn't respond as a service) and every executable
     * found in the active folder (newest first within each kind); the one in use is checked.
     * @param {Element} anchor - Anchor.
     */
    ExeMenu(anchor) {
        const t = this;
        const d = t.Data;
        if (!d)
            return;
        const select = async (exeName, okText) => {
            try {
                const r = await Sw.Net.Send("SelectExe", { ServiceName: t.Service, ExeName: exeName });
                if (r) {
                    t.Data = r;
                    t.Update(false);
                }
                Sw.Info(okText);
            }
            catch (e) {
                Sw.Fail(_T("Failed to select the executable, error: {0}", e.message, "Error message when selecting the executable of a service failed.{0} is replaced with the error"));
            }
            t.WakeUp();
        };
        const exes = d.Executables || [];
        const best = exes.find(x => x.IsBestGuess);
        const items = [
            {
                Text: best
                    ? _T("Automatic ({0})", best.Name, "Menu option that selects the service executable automatically.{0} is replaced with the best guess")
                    : _TF("Automatic", "Menu option that selects the service executable automatically"),
                Title: SmShared.Lines(
                    _TF("Use the best guess: console executables first, known bundled tools last, then the newest.", "Tool tip of the menu option that selects the service executable automatically"),
                    _TF("If it doesn't respond as a service, the next best executable that does is used.", "Tool tip of the menu option that selects the service executable automatically")),
                Radio: true, Checked: !d.ExeManual,
                OnClick: () => select("", _TF("The executable is selected automatically", "Message when the executable of a service is set to be selected automatically")),
            },
            "-",
        ];
        for (const x of exes) {
            const facts = [x.Format];
            if (x.IsDotNetHost)
                facts.push(_TF(".NET app host", "Fact about a service executable candidate"));
            if (x.IsKnownTool)
                facts.push(_TF("known bundled tool", "Fact about a service executable candidate"));
            if (!x.CanExecute)
                facts.push(_TF("not executable", "Fact about a service executable candidate (it lacks the execute permission)"));
            const ts = Sw.Format.TimeStamp(new Date(x.LastModified));
            items.push({
                Text: x.Name,
                Title: SmShared.Lines(
                    facts.join(", "),
                    _TF("Last modified:", "Tool tip line before a time") + " " + ts[0],
                    _TF("Size:", "Tool tip line before a file size") + " " + Sw.Format.Bytes(x.Size)),
                Radio: true, Checked: d.ExeManual && x.IsCurrent,
                OnClick: () => select(x.Name, _T("Using {0} as the executable", x.Name, "Message when the executable of a service was selected.{0} is replaced with the file name")),
            });
        }
        if (!exes.length)
            items.push({ Text: _TF("No executables found", "Menu text when there are no executables in the folder of a service"), Disabled: true });
        Sw.Menu.Show(anchor, items, { Label: _TF("Executable", "Accessible name of the executable selection menu") });
    }

    /**
     * Show a config file menu.
     * @param {Element|{X:number, Y:number}} anchor - Anchor.
     * @param {{Name: string}} f - The file.
     * @param {boolean} isMaster - Master config.
     */
    ConfigMenu(anchor, f, isMaster) {
        const t = this;
        const fn = f.Name;
        const base = (isMaster ? "../ServerManager/Data/" : "../FolderSync/ManagedFolders/") + t.Service + "/" + fn;
        const folder = isMaster ? t.Data.Folder : t.Data.CurrentFolder;
        const path = folder + "\\" + fn;
        const req = { ServiceName: t.Service, Config: fn, IsMaster: isMaster };
        /** Run a config request; poll on success. */
        const call = async (api, failText) => {
            try {
                if (await Sw.Net.Send(api, req)) {
                    t.WakeUp();
                    return;
                }
                Sw.Fail(failText);
            }
            catch (e) {
                Sw.Fail(failText + " " + e.message);
            }
        };
        const copyTitle = v => _T("Copy \"{0}\" to the clipboard", v, "Tool tip of a menu option that copies a text to the clipboard.{0} is replaced with the text");
        const items = [
            {
                Text: _TF("View", "Menu option that shows the content of a config file"), Icon: "../icons/notes.svg",
                Title: _TF("View the file content", "Tool tip of a menu option that shows the content of a config file"),
                OnClick: () => Sw.Page.Navigate("../logFile/logfile.html?api=" + encodeURIComponent(base), "_self"),
            },
        ];
        if (smIsConfigBackup(fn))
            items.push({
                Text: _TF("Activate", "Menu option that activates a backup config file"), Icon: "../icons/fav_on.svg",
                Title: SmShared.Lines(
                    _TF("Use this as the active config:", "Tool tip of a menu option that activates a backup config file"),
                    _TF("1. Rename any original (backup)", "Tool tip of a menu option that activates a backup config file"),
                    _TF("2. Rename this to original name", "Tool tip of a menu option that activates a backup config file")),
                OnClick: () => call("ActivateConfig", _TF("Failed to activate the config!", "Error message when activating a config file failed")),
            });
        if (isMaster)
            items.push({
                Text: _TF("Use as current", "Menu option that copies a master config file to the current version"), Icon: "../icons/preview_play.svg",
                Title: _TF("Copy this configuration to the current version", "Tool tip of a menu option that copies a master config file to the current version"),
                OnClick: () => call("UseMasterConfig", _TF("Failed to copy the configuration to the current version!", "Error message when copying a master config file failed")),
            });
        items.push(
            "-",
            { Text: _TF("Copy file name", "Menu option that copies a file name"), Icon: "../icons/copy.svg", Title: copyTitle(fn), OnClick: () => Sw.Clipboard.Copy(fn) },
            { Text: _TF("Copy local path", "Menu option that copies a local file path"), Icon: "../icons/copy.svg", Title: copyTitle(path), OnClick: () => Sw.Clipboard.Copy(path) },
            { Text: _TF("Copy local folder", "Menu option that copies a local folder path"), Icon: "../icons/copy.svg", Title: copyTitle(folder), OnClick: () => Sw.Clipboard.Copy(folder) },
            {
                Text: _TF("Download", "Menu option that downloads a file"), Icon: "../icons/disc.svg",
                Title: _T("Download the \"{0}\" config", fn, "Tool tip of a menu option that downloads a config file.{0} is replaced with the file name"),
                OnClick: () => Sw.Net.Download(base, fn),
            },
            "-",
            {
                Text: _TF("Delete", "Menu option that deletes a file"), Icon: "../icons/close.svg", Danger: true,
                Title: _TF("Delete the file", "Tool tip of a menu option that deletes a file"),
                OnClick: async () => {
                    //  The shared danger confirmation ("Cancel" focused, the danger button names the action)
                    const ok = await Sw.Cells.ConfirmDanger({
                        Title: SmShared.DangerTitle(_TF("Delete", "Verb in the title of a dialog asking to delete a config file (Delete <file>?)"), SmShared.BaseName(path), () => _TF("Delete the configuration file?", "Title of a dialog asking to delete a config file")),
                        Item: path,
                        ItemLabel: _TF("File:", "Label before the file name in a confirmation dialog"),
                        Warning: _TF("The file will be permanently removed!", "Warning of a dialog asking to delete a file"),
                        OkText: _TF("Delete", "Text of the danger button that confirms deleting a file"),
                        OkIcon: "../icons/close.svg",
                        OkTitle: _TF("Permanently delete the file", "Tool tip of a button that confirms deleting a file"),
                        CancelTitle: _TF("Keep the file", "Tool tip of a button that cancels deleting a file"),
                    });
                    if (ok)
                        await call("DeleteConfig", _TF("Failed to delete configuration file!", "Error message when deleting a config file failed"));
                },
            });
        Sw.Menu.Show(anchor, items, { Label: fn });
    }

    /**
     * Reconcile a config file list (ul.sw-rows: file type icon, name (opens the text editor), a "Backup" badge, size and time, a
     * menu button; right click / long press opens the menu too).
     * @param {object} card - The config card (ListE, IsMaster).
     * @param {Array<{Name: string, Size: number, LastModified: string}>} files - The files.
     */
    UpdateFiles(card, files) {
        const t = this;
        const isMaster = card.IsMaster;
        const u = SmShared.ChildUpdater(card.ListE);
        const editBase = isMaster ? "../ServerManager/Data/" : "../FolderSync/ManagedFolders/";
        for (const f of files) {
            const fn = f.Name;
            const ext = fn.substring(fn.lastIndexOf(".") + 1).toLowerCase();
            const icon = Sw.el("span.si-file-icon", { attrs: { "aria-hidden": "true" }, style: { backgroundImage: "url('../icons/ext/" + ext + ".svg')" } });
            const name = t.ViewOnly
                ? Sw.el("span.sw-rows-text.si-file-name", { text: fn })
                : Sw.el("button.sw-rows-text.si-file-name.si-link", { type: "button", text: fn, title: _TF("Edit the file", "Tool tip of a config file name") });
            const bak = smIsConfigBackup(fn) ? Sw.Badge(_TF("Backup", "Badge of a config file that is a backup"), null, _TF("This file is a backup (not the original name)", "Tool tip of the backup badge of a config file")) : null;
            const size = Sw.el("span.si-file-size", { text: Sw.Format.Bytes(f.Size) });
            Sw.Clipboard.CopyOnClick(size, f.Size, { AddTitle: true });
            const time = SmShared.TimeCell(Sw.el("span.si-file-time"), f.LastModified, _TF("Last modified", "Tool tip line of a file time"));
            const menu = t.ViewOnly ? null : Sw.el("button.si-file-menu", { type: "button", title: _TF("Show options", "Tool tip of a button that opens the file options menu"), attrs: { "aria-label": _T("Options of {0}", fn, "Accessible name of the menu button of a config file, {0} is the file name"), "aria-haspopup": "menu" } }, Sw.Icon("kebab"));
            const row = Sw.el("li.si-row", null, icon, name, bak, Sw.el("span.sw-rows-meta", null, size, " · ", time), menu);
            const node = u.Add(row, r => {
                if (t.ViewOnly)
                    return;
                const nameEl = r.querySelector(".si-file-name");
                nameEl.addEventListener("click", ev => {
                    if (Sw.BadClick(ev))
                        return;
                    Sw.Page.Navigate("../edit/text.html?r=" + encodeURIComponent(editBase + t.Service + "/" + r.SmData.Name) + "&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile", "_self");
                });
                const menuEl = r.querySelector(".si-file-menu");
                menuEl.addEventListener("click", ev => {
                    if (!Sw.BadClick(ev))
                        t.ConfigMenu(menuEl, r.SmData, isMaster);
                });
                Sw.Menu.Context(r, ev => {
                    t.ConfigMenu(ev.clientX || ev.clientY ? { X: ev.clientX, Y: ev.clientY } : menuEl, r.SmData, isMaster);
                    return null;
                });
            });
            node.SmData = f;
        }
        u.Complete();
        if (!files.length)
            card.ListE.replaceChildren(Sw.el("li", null, Sw.StateView({ Small: true, Icon: "file", Title: _TF("No config files", "Empty list of config files on the service page") }).E));
    }

    /**
     * Set up the upload button in the head of a config card (first data only).
     * @param {object} card - The config card (Repo).
     * @param {string} title - Tool tip.
     */
    AddUpload(card, title) {
        card.SetTools(SmShared.UploadButton({
            Text: _TF("Upload", "Text of the upload button of a config file list (files can also be dropped on it)"),
            Title: title, Repo: card.Repo, Small: true, OnUploaded: () => this.WakeUp(),
        }));
    }

    /**
     * Render the current data.
     * @param {boolean} first - True for the first data.
     */
    Update(first) {
        const t = this;
        const d = t.Data;
        const el = t.El;
        const status = d.Status;
        const kind = SmStateKinds[status] || "error";
        t.Page.Header.SetStatus({ Text: status, Kind: kind, Dot: true });
        const noProc = d.ProcId === 0;
        el.Pid.Set(noProc ? "-" : String(d.ProcId));
        el.Exe.Set(d.ExeName ? (d.ExeManual ? d.ExeName : _T("{0} (auto)", d.ExeName, "Value of the executable fact when the executable was detected automatically.{0} is replaced with the file name")) : "-");
        const exeLines = [
            d.ExeManual
                ? _TF("The executable was selected by a user.", "Tool tip line of the executable fact of a service")
                : _TF("The executable was detected automatically (the newest console executable).", "Tool tip line of the executable fact of a service"),
        ];
        if (!t.ViewOnly)
            exeLines.push("", _TF("Click to select the executable used for the service.", "Tool tip line of the executable fact of a service"));
        el.Exe.SetTitle(SmShared.Lines(...exeLines));
        el.Tot.Set(noProc ? "-" : Sw.Format.TimeSpan(d.TotalProcessorTime));
        const cpu = +d.CpuUsage || 0;
        el.Cpu.Set({ Value: noProc ? "-" : cpu.toFixed(1), MeterValue: noProc ? 0 : cpu });
        SmShared.KpiSub(el.Cpu, noProc ? _TF("Not running", "Context line of the cpu / memory tile of a service without a process") : _TF("Of all processors", "Context line of the cpu tile of the service page"));
        const mem = +d.MemUsage || 0;
        el.Mem.Set({ Value: noProc ? "-" : Sw.Format.Bytes(mem, 1), Sub: noProc ? _TF("Not running", "Context line of the cpu / memory tile of a service without a process") : _TF("Working set", "Context line of the memory tile of the service page") });

        const located = t.Located;
        el.CurFolder.Set(d.CurrentFolder || "-");
        el.MasterFolder.Set(d.Folder || "-");
        el.LogFile.Set(d.Log ? d.Log.Name : _TF("None", "Value of the log file fact when the service has no log file"));
        if (first && !t.ViewOnly) {
            t.Debug.Explore.SetTitle(SmShared.Lines(_TF("Explore the active files.", "Tool tip of a button that explores the files of a service"), "", located, d.CurrentFolder));
            if (d.Log)
                t.Debug.Log.SetTitle(SmShared.Lines(_TF("View the current log file.", "Tool tip of a button that shows the log file of a service"), "", _TF("Located here:", "Tool tip line before a file path"), d.CurrentFolder + "\\" + d.Log.Name));
        }

        const showUploads = first && !t.ViewOnly;
        el.Configs.E.hidden = !d.Configs;
        if (d.Configs) {
            el.Configs.SetSub(d.CurrentFolder || "");
            el.Configs.E.title = SmShared.Lines(_TF("These are the files that are currently active.", "Tool tip of the list of active config files"), "", located, d.CurrentFolder);
            t.UpdateFiles(el.Configs, d.Configs);
            if (showUploads)
                t.AddUpload(el.Configs, SmShared.Lines(_TF("Upload config files into the current version.", "Tool tip of an upload button"), "", located, d.CurrentFolder));
        }
        el.Master.E.hidden = !d.MasterConfigs;
        if (d.MasterConfigs) {
            el.Master.SetSub(d.Folder || "");
            el.Master.E.title = SmShared.Lines(_TF("These are the master config files, that get copied when a new version is uploaded.", "Tool tip of the list of master config files"), "", located, d.Folder);
            t.UpdateFiles(el.Master, d.MasterConfigs);
            if (showUploads)
                t.AddUpload(el.Master, SmShared.Lines(_TF("Upload config files into the master configs.", "Tool tip of an upload button"), "", located, d.Folder));
        }
        //  One config list alone takes the full width
        const both = !!d.Configs && !!d.MasterConfigs;
        for (const c of [el.Configs, el.Master]) {
            c.E.classList.toggle("sw-span-6", both);
            c.E.classList.toggle("sw-span-12", !both);
        }
        t.UpdateVersions(d.Versions || []);
        t.UpdateButtons();
    }

    /**
     * Reconcile the versions list (ul.sw-rows: an "Active" badge, the upload time (opens versionInfo.html), user, machine, comment,
     * last used; values copy on click).
     * @param {Array<object>} vs - The versions ({IsActive, Uploaded, Name, User, Machine, Comment, LastUsed}).
     */
    UpdateVersions(vs) {
        const t = this;
        const card = t.El.Versions;
        card.E.hidden = vs.length === 0;
        card.SetCount(vs.length || null);
        const u = SmShared.ChildUpdater(t.El.VersionList);
        for (const v of vs) {
            const active = v.IsActive ? Sw.Badge(_TF("Active", "Badge of the active version of a service"), "ok", _TF("The active version", "Tool tip of a star marking the active version")) : null;
            const up = t.ViewOnly ? Sw.el("span.si-version-uploaded") : Sw.el("button.si-version-uploaded.si-link", { type: "button" });
            const ts = Sw.Format.TimeStamp(new Date(v.Uploaded));
            up.textContent = ts[1];
            up.title = SmShared.Lines(_TF("Show details.", "Tool tip line of a clickable statistic"), "", _TF("Uploaded:", "Tool tip line before a time"), ts[0]);
            const cell = (cls, text, title) => {
                const e = Sw.el("span." + cls, { text: text || "", title });
                Sw.Clipboard.CopyOnClick(e, text || "", { AddTitle: true });
                return e;
            };
            const row = Sw.el("li.si-row", null, up, active,
                Sw.el("span.sw-rows-text.si-version-text", null,
                    cell("si-version-user", v.User, _TF("The name of the user that was logged on to the machine that this version was uploaded from at the upload start time", "Tool tip of a version user name")),
                    cell("si-version-machine", v.Machine, _TF("The name of the machine that this version was uploaded from", "Tool tip of a version machine name")),
                    cell("si-version-comment", v.Comment, _TF("The comment supplied when uploading this version", "Tool tip of a version comment"))),
                Sw.el("span.sw-rows-meta", null, SmShared.TimeCell(Sw.el("span.si-version-last"), v.LastUsed, _TF("Last activated", "Tool tip line of a version last used time"))));
            const node = u.Add(row, r => {
                if (t.ViewOnly)
                    return;
                r.querySelector(".si-version-uploaded").addEventListener("click", ev => {
                    if (Sw.BadClick(ev))
                        return;
                    const x = r.SmData;
                    Sw.Page.Navigate("versionInfo.html?p=" + encodeURIComponent(t.Service + "," + x.Uploaded + "," + x.Name), "_self");
                });
            });
            node.SmData = v;
        }
        u.Complete();
    }

    /**
     * Fetch the details once and render them.
     * @returns {Promise<void>}
     */
    async Poll() {
        const t = this;
        try {
            const first = !t.Data;
            const d = await Sw.Net.Send("GetDetail", t.Service);
            if (!d)
                return;
            t.Data = d;
            //  The server knows the service: its title is no longer only text from the link
            if (first) {
                Sw.Page.TitleFromLink = null;
                t.Page.Header.SetTitle(t.Service);
                //  The document title of the original page: "Service - <name>" (after the header title, which sets it to the name)
                document.title = t.Title();
            }
            t.Update(first);
        }
        catch (e) {
            Sw.Fail(e.message || String(e));
        }
    }

    /**
     * Poll every 5 s (or right after an action) as long as the page lives.
     * @returns {Promise<void>}
     */
    async Loop() {
        for (; ;) {
            if (!this.WakePending)
                await Sw.Util.DelayAbort(5000, this.Wake);
            this.WakePending = false;
            await this.Poll();
        }
    }

    /**
     * Poll as soon as possible (ends the current delay, or skips the next one when a poll is running).
     */
    WakeUp() {
        this.WakePending = true;
        this.Wake.Raise();
    }
}

/**
 * Entry point of serviceInfo.html.
 * @returns {Promise<void>}
 */
function serviceInfoMain() {
    let page = null;
    return Sw.Page.Main(async () => {
        const service = Sw.Url.Get("p");
        if (!service) {
            Sw.PageLayout({
                Main: ".si-page", Type: "dash", MenuTitle: false,
                Title: _TF("Service", "Title of the service details page without a service in the link"),
            }).Add(Sw.StateView({
                Kind: "error",
                Title: _TF("No service parameter supplied!", "Error message when the service name is missing in the url"),
                Text: _TF("Open a service from the Managed services page.", "Text of the error on the service page when the url names no service"),
                Actions: [new Sw.Button({ Text: _TF("Managed services", "Button that opens the managed services page"), OnClick: () => Sw.Page.Navigate("services.html", "_self") })],
            }));
            return;
        }
        const viewOnly = !!Sw.Url.Get("v");
        document.body.classList.toggle("ViewOnly", viewOnly);
        page = new SmServiceInfo(service, viewOnly);
        page.Build();
        await page.Poll();
    }, { Loading: true }).then(() => {
        if (page)
            page.Loop();
    });
}
