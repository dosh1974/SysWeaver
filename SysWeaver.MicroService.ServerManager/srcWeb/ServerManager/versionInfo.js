/**
 * @file versionInfo.js - the ServerManager "version info" page (versionInfo.html, entry `versionInfoMain`): details of
 * one uploaded version of a managed service, opened from the versions grid of serviceInfo.html
 * (`versionInfo.html?p=<svc>,<Uploaded>,<Name>`).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js and smShared.js; started from
 * `<body onload>`.
 *
 * Design v2 page anatomy, built from the shared ui components (ui/page.js): Sw.PageLayout (page header: the version (service, upload
 * time, folder), an "Active" status badge, the actions Activate, Touch, Verify, Expand, Compress and Delete (danger outline, confirmed)),
 * Sw.Facts (the version values; a value copies on click) and a "Result" card (Sw.Card + Sw.Facts) with the statistics of Touch / Verify.
 *
 * Url parameter: `p` = the version key "<service>,<uploadedIso>,<folderName>" (sent to the server as is; the folder
 * name changes when a version is activated, so the key is refreshed from the data).
 *
 * Server API (relative to ServerManager/, all take the version key): GetVersion (polled every 5 s), VersionActivate,
 * VersionTouch / VersionVerify (-> SmCompFileStats), VersionExpand, VersionCompress, VersionDelete.
 *
 * Globals defined: `versionInfoMain`, `SmVersionInfo` (the page class).
 */

/**
 * The version info page.
 */
class SmVersionInfo {
    /**
     * @param {string} key - The version key "<service>,<uploaded>,<folder>".
     */
    constructor(key) {
        /** @type {string[]} The key parts [service, uploaded, folder]. */
        this.Parts = key.split(",");
        /** @type {?object} The last SmVersionDetail. */
        this.Data = null;
        /** @type {boolean} True while an action runs. */
        this.Busy = false;
        /** @type {Object<string, Sw.Button>} The action buttons. */
        this.Buttons = null;
        /** Page parts (Build). */
        this.El = {};
    }

    /**
     * Build the page: the header (title, status, actions added by BuildActions), the version facts and the hidden "Result" card.
     */
    Build() {
        const el = this.El;
        this.Page = Sw.PageLayout({
            Main: ".vi-page",
            Type: "dash",
            Title: "",
            MenuTitle: false,
            Description: _TF("An uploaded version of a managed service. Select a value to copy it.", "Description of the version details page"),
        });
        el.Values = Sw.Facts({ Label: _TF("Version", "Accessible name of the facts of the version details page") });
        el.Output = Sw.Facts({ Card: false, Label: _TF("Result", "Accessible name of the statistics of the version details page") });
        el.Result = Sw.Card({ Title: _TF("Result", "Title of the card with the statistics of Touch / Verify on the version details page"), Span: 12, Flush: true, Body: el.Output });
        el.Result.E.hidden = true;
        this.Page.Add(el.Values, Sw.Grid({ Content: [el.Result] }));
    }

    /**
     * Empty and hide the "Result" card.
     */
    ClearOutput() {
        const el = this.El;
        const o = Sw.Facts({ Card: false, Label: _TF("Result", "Accessible name of the statistics of the version details page") });
        el.Output.E.replaceWith(o.E);
        el.Output = o;
        el.Result.E.hidden = true;
    }

    /** @returns {string} The current version key. */
    get Key() {
        return this.Parts.join(",");
    }

    /**
     * Show the title: the key parts joined with " - " (the time stamp short formatted). Before the server confirmed the version
     * (fromLink: the parts are the url's `p` key) the title is untrusted url text: Sw.Page.SetLinkTitle (plain, at most 80
     * characters, no caption in the shell); once confirmed it is the server's title.
     * @param {boolean} [fromLink] The parts come from the url.
     */
    SetTitle(fromLink) {
        const p = this.Parts.slice();
        if (p[1])
            p[1] = Sw.Format.TimeStamp(new Date(p[1]))[1];
        const t = p.join(" - ");
        if (fromLink) {
            this.Page.Header.SetTitle(Sw.Page.SetLinkTitle(t));
            return;
        }
        Sw.Page.TitleFromLink = null;
        document.title = t;
        this.Page.Header.SetTitle(t);
    }

    /**
     * Create the action buttons (first data).
     */
    BuildActions() {
        const t = this;
        const d = t.Data;
        const steps = [_TF("Activate this version.", "Tool tip of a button that activates a service version"), _TF("This will:", "Tool tip line before a list of steps")];
        if (d.Comp)
            steps.push("- " + _TF("Expand this version files", "Step of the activation of a service version"));
        if (d.IsRunning)
            steps.push("- " + _TF("Uninstall the current service", "Step of the activation of a service version"));
        steps.push("- " + _TF("Swap folders (current and this)", "Step of the activation of a service version"));
        steps.push("- " + _TF("Start this version of the service", "Step of the activation of a service version"));
        if (d.IsCompressedService)
            steps.push("- " + _TF("Compress the current version files", "Step of the activation of a service version"));
        const b = (text, title, icon, action, kind) => new Sw.Button({ Text: text, Title: title, Icon: icon, Kind: kind, Disabled: _TF("Wait until the version is loaded", "Why a service version button (ex: Activate, Delete) is disabled before the version details are known"), OnClick: () => t.DoWork(action) });
        t.Buttons = {
            Activate: b(_TF("Activate", "Text of a button that activates a service version"), steps.join("\n"), "../icons/fav_on.svg",
                () => Sw.Net.Send("VersionActivate", t.Key)),
            Touch: b(_TF("Touch", "Text of a button that updates the last used time of a service version"),
                SmShared.Lines(_TF("Set the last used timestamp to now.", "Tool tip of a button that updates the last used time of a service version"),
                    _TF("This will prevent this version from being expired and removed for a bit longer.", "Tool tip of a button that updates the last used time of a service version")),
                "../icons/rating_on.svg", () => t.Stats("VersionTouch")),
            Verify: b(_TF("Verify", "Text of a button that verifies a compressed service version"),
                _TF("Validate the integrity of the compressed version.", "Tool tip of a button that verifies a compressed service version"),
                "../icons/keep.svg", () => t.Stats("VersionVerify")),
            Expand: b(_TF("Expand", "Text of a button that expands a compressed service version"),
                _TF("Expand the files in this compressed version.", "Tool tip of a button that expands a compressed service version"),
                "../icons/table_log.svg", () => Sw.Net.Send("VersionExpand", t.Key)),
            Compress: b(_TF("Compress", "Text of a button that compresses a service version"),
                _TF("Compress the files.", "Tool tip of a button that compresses a service version"),
                "../icons/table_compression.svg", () => Sw.Net.Send("VersionCompress", t.Key)),
            Delete: b(_TF("Delete", "Text of a button that deletes a service version"),
                _TF("Delete this version", "Tool tip of a button that deletes a service version"),
                "../icons/close.svg", () => t.Delete(), "danger"),
        };
        for (const x of Object.values(t.Buttons))
            t.Page.Header.AddAction(x);
    }

    /**
     * Enable the buttons: only for a version that is not active and while no action runs.
     */
    UpdateButtons() {
        const t = this;
        const d = t.Data;
        if (!t.Buttons || !d)
            return;
        const ok = !t.Busy && !d.IsActive;
        const comp = !!d.Comp;
        const why = t.Busy
            ? _TF("Wait until the current action is done", "Why the service version buttons (ex: Activate, Delete) are disabled while an action runs")
            : _TF("This is the active version of the service", "Why a service version button (ex: Activate, Delete) is disabled for the version that is in use");
        const whyComp = ok ? _TF("This version is not compressed", "Why the verify or expand button of a service version is disabled") : why;
        const whyNoComp = !ok ? why : comp
            ? _TF("This version is already compressed", "Why the compress button of a service version is disabled")
            : _TF("This service doesn't use compressed versions", "Why the compress button of a service version is disabled");
        t.Buttons.Activate.SetDisabled(!ok, why);
        t.Buttons.Touch.SetDisabled(!ok, why);
        t.Buttons.Verify.SetDisabled(!(ok && comp), whyComp);
        t.Buttons.Expand.SetDisabled(!(ok && comp), whyComp);
        t.Buttons.Compress.SetDisabled(!(ok && !comp && d.IsCompressedService), whyNoComp);
        t.Buttons.Delete.SetDisabled(!ok, why);
    }

    /**
     * Run one action at a time (all buttons disabled), then refetch the version (its folder name may have changed).
     * @param {function():Promise<*>} action - The action.
     * @returns {Promise<void>}
     */
    async DoWork(action) {
        const t = this;
        if (t.Busy)
            return;
        t.Busy = true;
        t.ClearOutput();
        t.UpdateButtons();
        try {
            await action();
        }
        catch (e) {
            Sw.Fail(e.message || String(e));
        }
        finally {
            try {
                const d = await Sw.Net.Send("GetVersion", t.Key);
                if (d) {
                    t.Data = d;
                    t.Parts[2] = d.Name;
                    t.SetTitle();
                    t.Render();
                }
            }
            catch (e) {
                Sw.Fail(e.message || String(e));
            }
            t.Busy = false;
            t.UpdateButtons();
        }
    }

    /**
     * Run an action returning chunk statistics and show them.
     * @param {string} api - VersionTouch or VersionVerify.
     * @returns {Promise<void>}
     */
    async Stats(api) {
        const r = await Sw.Net.Send(api, this.Key);
        if (r)
            this.ShowStats(r);
    }

    /**
     * Confirm and delete this version, then go back.
     * @returns {Promise<void>}
     */
    async Delete() {
        //  The shared danger confirmation ("Cancel" focused, the danger button names the action)
        const ok = await Sw.Cells.ConfirmDanger({
            Title: SmShared.DangerTitle(_TF("Delete", "Verb in the title of a dialog asking to delete a service version (Delete <version folder>?)"), SmShared.BaseName(this.Data.FullPath), () => _TF("Delete this version?", "Title of a dialog asking to delete a service version")),
            Item: this.Data.FullPath,
            ItemLabel: _TF("Folder:", "Label before the folder name in a confirmation dialog"),
            Warning: _TF("The folder will be permanently removed!", "Warning of a dialog asking to delete a service version"),
            OkText: _TF("Delete version", "Text of the danger button that confirms deleting a service version"),
            OkIcon: "../icons/close.svg",
            OkTitle: _TF("Permanently delete the folder containing this version", "Tool tip of a button that confirms deleting a service version"),
            CancelTitle: _TF("Keep this version", "Tool tip of a button that cancels deleting a service version"),
        });
        if (ok && await Sw.Net.Send("VersionDelete", this.Key))
            history.back();
    }

    /**
     * Fill a facts strip (Sw.Facts): a fact per row (created once, then only its value changes); a value copies on click.
     * @param {object} target - The facts (Sw.Facts).
     * @param {Array<{Name: string, Value: string, Title: (string|undefined), NameTitle: (string|undefined), Copy: *}>} rows - The rows.
     */
    static FillValues(target, rows) {
        for (const r of rows) {
            let f = target.Get(r.Name);
            if (!f) {
                const key = r.Name;
                f = target.Add({ Key: key, Label: r.Name, Value: "", OnClick: () => Sw.Clipboard.Copy(String(target.Get(key).Copy ?? "")) });
            }
            f.Copy = r.Copy ?? r.Value;
            f.Set(r.Value);
            f.SetTitle(SmShared.Lines(...[r.NameTitle, r.Title].filter(x => x), _TF("Select to copy the value", "Tool tip line of a value on the version details page")));
        }
    }

    /**
     * Show chunk statistics (SmCompFileStats) in the output list.
     * @param {object} r - The statistics.
     */
    ShowStats(r) {
        const rows = [];
        const count = (n, v, tip) => rows.push({ Name: n, Value: Sw.Format.Number(v, 0), NameTitle: tip, Copy: v });
        const pct = (n, v, tip) => rows.push({ Name: n, Value: Sw.Format.Number(v, 2) + " %", NameTitle: tip, Copy: v });
        const bytes = (n, v, tip) => rows.push({ Name: n, Value: Sw.Format.Bytes(v), NameTitle: tip, Copy: v });
        count(_TF("Files", "Name of a statistic"), r.FileCount, _TF("Total number of files found in the compressed version", "Description of a statistic"));
        count(_TF("Chunks", "Name of a statistic"), r.ChunkCount, _TF("Total number chunks used the compressed version", "Description of a statistic"));
        count(_TF("Unique chunks", "Name of a statistic"), r.UniqueChunks, _TF("Total number unique chunks used the compressed version", "Description of a statistic"));
        if (r.ChunkCount > 0)
            pct(_TF("Chunk reuse", "Name of a statistic"), 100 - 100.0 * r.UniqueChunks / r.ChunkCount, _TF("How many chunks that is reused as a percentage", "Description of a statistic"));
        const compSize = r.ChunkCompSize + r.FileSize;
        bytes(_TF("Compressed size", "Name of a statistic"), compSize, _TF("The size of all compressed chunks + the size of the archive file", "Description of a statistic"));
        if (r.ChunkExpSize > 0) {
            bytes(_TF("Expanded size", "Name of a statistic"), r.ChunkExpSize, _TF("The size of all files after expansion", "Description of a statistic"));
            pct(_TF("Compression ratio", "Name of a statistic"), 100.0 * compSize / r.ChunkExpSize, _TF("The effective compression ratio", "Description of a statistic"));
        }
        if (r.TotalMissing > 0) {
            count(_TF("Broken files", "Name of a statistic"), r.BrokenFiles, _TF("Total number of files that have missing chunks", "Description of a statistic"));
            count(_TF("Missing chunks", "Name of a statistic"), r.TotalMissing, _TF("Total number of missing chunks", "Description of a statistic"));
        }
        SmVersionInfo.FillValues(this.El.Output, rows);
        this.El.Result.E.hidden = false;
    }

    /**
     * Render the version values.
     */
    Render() {
        const d = this.Data;
        const yes = _TF("Yes", "Value of a yes / no field");
        const no = _TF("No", "Value of a yes / no field");
        const time = (n, v, tip) => {
            const ts = Sw.Format.TimeStamp(new Date(v));
            return { Name: n, Value: ts[1], Title: tip ? SmShared.Lines(tip, "", ts[0]) : ts[0], Copy: v };
        };
        const folderTip = _TF("Folder names change when activated / deactivated.", "Tool tip of a version folder name");
        SmVersionInfo.FillValues(this.El.Values, [
            time(_TF("Uploaded", "Name of a version field"), d.Uploaded, _TF("The time when this version was uploaded.", "Tool tip of a version field")),
            { Name: _TF("Active", "Name of a version field"), Value: d.IsActive ? yes : no },
            { Name: _TF("Folder", "Name of a version field"), Value: d.Name, Title: folderTip, NameTitle: folderTip },
            { Name: _TF("User", "Name of a version field"), Value: d.User || "" },
            { Name: _TF("Machine", "Name of a version field"), Value: d.Machine || "" },
            { Name: _TF("Comment", "Name of a version field"), Value: d.Comment || "" },
            time(_TF("Last used", "Name of a version field"), d.LastUsed, _TF("The time when this version was used last.", "Tool tip of a version field")),
            { Name: _TF("Compressed", "Name of a version field"), Value: d.Comp ? yes : no },
            { Name: _TF("Files", "Name of a version field"), Value: Sw.Format.Number(d.Count, 0), Copy: d.Count },
            { Name: _TF("Size", "Name of a version field"), Value: Sw.Format.Bytes(d.Size), Copy: d.Size },
            { Name: _TF("Path", "Name of a version field"), Value: d.FullPath || "" },
        ]);
        this.Page.Header.SetStatus(d.IsActive ? { Text: _TF("Active", "Status badge of the active version of a service"), Kind: "ok", Dot: true } : null);
        this.UpdateButtons();
    }

    /**
     * Fetch and render once.
     * @returns {Promise<void>}
     */
    async Poll() {
        const t = this;
        if (t.Busy)
            return;
        try {
            const d = await Sw.Net.Send("GetVersion", t.Key);
            if (!d || t.Busy)
                return;
            const first = !t.Data;
            t.Data = d;
            if (first) {
                t.Parts = [d.ServiceName, d.Uploaded, d.Name];
                t.SetTitle();
                t.BuildActions();
            }
            t.Render();
        }
        catch (e) {
            Sw.Fail(e.message || String(e));
        }
    }

    /**
     * Poll every 5 s as long as the page lives.
     * @returns {Promise<void>}
     */
    async Loop() {
        for (; ;) {
            await Sw.Util.Delay(5000);
            await this.Poll();
        }
    }
}

/**
 * Entry point of versionInfo.html.
 * @returns {Promise<void>}
 */
function versionInfoMain() {
    let page = null;
    return Sw.Page.Main(async () => {
        const key = Sw.Url.Get("p");
        if (!key) {
            Sw.PageLayout({ Main: ".vi-page", Type: "dash", MenuTitle: false, Title: _TF("Service", "Title of the version details page without a version in the link (the original page title: S5-B-1)") })
                .Add(Sw.StateView({
                    Kind: "error",
                    Title: _TF("No version parameter supplied!", "Error message when the version key is missing in the url"),
                    Text: _TF("Open a version from the Versions list of a service.", "Text of the error on the version page when the url names no version"),
                }));
            return;
        }
        page = new SmVersionInfo(key);
        page.Build();
        page.SetTitle(true);
        await page.Poll();
    }, { Loading: true }).then(() => {
        if (page)
            page.Loop();
    });
}