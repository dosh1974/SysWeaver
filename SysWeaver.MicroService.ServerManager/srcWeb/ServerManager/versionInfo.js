/**
 * @file Script of the ServerManager "version info" page (versionInfo.html): details of one uploaded version
 * of a managed service, opened from the versions list of serviceInfo.html (`versionInfo.html?p=<svc>,<Uploaded>,<Name>`).
 * Loaded with a static `<script defer>` tag (after common.js and app/application.js) and started from
 * `<body onload="versionInfoMain()">`. Uses the static placeholders `<si-name>`, two `<si-values>` (version values
 * and action output) and `<si-actions>` of versionInfo.html.
 *
 * Globals defined: `BeginElementChildUpdate` (identical copy of the one in serviceInfo.js) and `versionInfoMain`.
 * No immediately executed code; `versionInfoMain` ends in an endless polling loop (every 5 s).
 *
 * Dependencies (globals from other files):
 * - common/common.js: `getUrlParams`, `sendRequest`, `Button`, `ValueFormat` (`getTimeStampTitle`, `copyOnClick`,
 *   `toString`, `formatByteSize`), `AbortHandler`, `delayWithAbort`, `Fail`, `PageLoaded`, `window.IsTop`.
 * - app/application.js: `Confirm`.
 *
 * Server API (page-relative, i.e. `ServerManager/<name>`): `GetVersion`, `VersionActivate`, `VersionTouch`,
 * `VersionVerify`, `VersionExpand`, `VersionCompress`, `VersionDelete`; all take the version key string
 * `"<service>,<uploadedIso>,<folderName>"`.
 */

/**
 * Starts an in-place reconciliation of the children of `targetElement`, so that polled lists only touch nodes
 * that actually changed (no flicker, focus is kept).
 * Usage: call `Add(e)` for every wanted child in order (use the returned node to attach handlers/expandos),
 * then `Complete()` to remove leftover children.
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
     * @returns {HTMLElement} The node now in place (the existing child or `e`); set handlers/expandos on this.
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
 * Entry point of versionInfo.html.
 * Reads the URL param `p` = version key `"<service>,<uploaded>,<folderName>"` (shows `Fail` and returns if missing)
 * and splits it into `versionParts`; adds `body.TopWindow` when `window.IsTop`. Sets the title, then polls forever:
 * `data = await sendRequest("GetVersion", version)`, `update(first)` (first = first successful fetch),
 * `await delayWithAbort(5000, abortWait)`; errors are shown with `Fail(e)`.
 * Note: nothing calls `abortWait.raise()` in this file, so the delay always runs the full 5 s.
 * @returns {Promise<void>} Never resolves (unless `p` is missing).
 */
async function versionInfoMain() {
    /** URL parameters. @type {URLSearchParams} */
    const p = getUrlParams();
    /** Current version key string sent to the server; updated when the folder name changes. @type {string} */
    let version = p.get("p");
    if (!version) {
        Fail("No version paramater supplied!");
        return;
    }
    /** Comma-split parts of `version`: [service, uploaded timestamp, folder name]. @type {string[]} */
    const versionParts = version.split(',');
    if (window.IsTop)
        document.body.classList.add("TopWindow");


    /**
     * Joins `versionParts` with " - ", with the uploaded timestamp replaced by its short form
     * (`ValueFormat.getTimeStampTitle(...)[1]`), and writes it to `document.title` and `<si-name>`.
     * @returns {void}
     */
    function setTitle() {
        const o = versionParts[1];
        versionParts[1] = ValueFormat.getTimeStampTitle(new Date(o))[1];
        const t = versionParts.join(" - ");
        versionParts[1] = o;
        document.title = t;
        name.innerText = t;
    }


    /** The `<si-name>` title bar. @type {HTMLElement} */
    const name = document.body.getElementsByTagName("si-name")[0];
    /** First `<si-values>` grid: the version's key/value pairs. @type {HTMLElement} */
    const values = document.body.getElementsByTagName("si-values")[0];
    /** The `<si-actions>` row holding the action buttons. @type {HTMLElement} */
    const actions = document.body.getElementsByTagName("si-actions")[0];
    /** Second `<si-values>` grid: output (chunk statistics) of the last action. @type {HTMLElement} */
    const output = document.body.getElementsByTagName("si-values")[1];

    /** False while `doWork` runs an action (all buttons disabled). @type {boolean} */
    let buttonsEnabled = true;

    /** Action buttons (common.js `Button` instances), created on the first `update`. @type {?Button} */
    let activateButton = null;
    /** @type {?Button} */
    let touchButton = null;

    /** "Verify" button. @type {?Button} */
    let validateButton = null;
    /** @type {?Button} */
    let expandButton = null;

    /** @type {?Button} */
    let compressButton = null;
    /** @type {?Button} */
    let deleteButton = null;


    /**
     * Enables the action buttons from the current `data`, only when `buttonsEnabled` and the version is not
     * active: Activate, Touch and Delete always; Verify and Expand if `data.Comp`; Compress if not `Comp` and
     * `data.IsCompressedService`.
     * @returns {void}
     */
    function updateButtonState() {
        const isActive = data.IsActive;
        const nonActive = !isActive;
        const isComp = data.Comp;
        activateButton.SetEnabled(buttonsEnabled && nonActive);
        touchButton.SetEnabled(buttonsEnabled && nonActive);
        validateButton.SetEnabled(buttonsEnabled && nonActive && isComp);
        expandButton.SetEnabled(buttonsEnabled && nonActive && isComp);
        compressButton.SetEnabled(buttonsEnabled && nonActive && (!isComp) && (data.IsCompressedService));
        deleteButton.SetEnabled(buttonsEnabled && nonActive);
    }

    /**
     * Runs one action at a time: returns immediately if `buttonsEnabled` is false; clears the output grid,
     * disables all buttons, shows `button.StartWorking()` and awaits `action()` (an exception shows
     * `Fail(e.message)`). In `finally` it
     * re-fetches `sendRequest("GetVersion", version)`, updates `versionParts[2]` / `version` from `data.Name`
     * (the folder name changes on activation), calls `setTitle()` and `update()`, re-enables the buttons and
     * calls `button.StopWorking()`.
     * @param {Button} button - The button that started the action.
     * @param {function():Promise<void>} action - The work to run.
     * @returns {Promise<void>}
     */
    async function doWork(button, action) {
        if (!buttonsEnabled)
            return;
        output.innerText = "";
        buttonsEnabled = false;
        updateButtonState();
        button.StartWorking();
        try {
            await action();
            //await delay(2000);
        }
        catch (e) {
            Fail(e.message);
        }
        finally {
            data = await sendRequest("GetVersion", version);
            versionParts[2] = data.Name;
            version = versionParts.join(',');
            setTitle();
            update();
            buttonsEnabled = true;
            button.StopWorking();
            updateButtonState();
        }
    }


    setTitle();
    /** Abort handle of the polling delay. @type {AbortHandler} */
    const abortWait = new AbortHandler();

    /**
     * Last version DTO from `GetVersion` (fields used: ServiceName, Uploaded, Name, IsActive, Comp, IsRunning,
     * IsCompressedService, User, Machine, Comment, LastUsed, Count, Size, FullPath).
     * @type {?object}
     */
    let data = null;



    /**
     * Renders the latest `data`.
     * On `first`: refreshes `versionParts` / `version` from `data.ServiceName`, `Uploaded` and `Name`, calls
     * `setTitle()`, creates the action `Button`s in `<si-actions>` (Activate -> `VersionActivate`, with a tooltip
     * listing the steps depending on `Comp` / `IsRunning` / `IsCompressedService`; Touch -> `VersionTouch` +
     * `showStats`; Verify -> `VersionVerify` + `showStats`; Expand -> `VersionExpand`; Compress -> `VersionCompress`;
     * Delete -> `Confirm`, `VersionDelete`, then `History.back()`), each wrapped in `doWork`, and calls `PageLoaded()`.
     * Always: `updateButtonState()` and reconciles `si-values[0]` with Uploaded, Active, Folder, User, Machine,
     * Comment, Last used, Compressed, Files, Size and Path.
     * @param {boolean} [first] - True on the first successful poll.
     * @returns {void}
     */
    function update(first) {

        if (first) {
            versionParts[0] = data.ServiceName;
            versionParts[1] = data.Uploaded;
            versionParts[2] = data.Name;
            version = versionParts.join(',');
            setTitle();

            /** Tooltip text of the button being created. @type {string} */
            let tt;
            tt = "Click to activate this version.\nThis will: \n";
            if (data.Comp)
                tt = tt + "- Expand this version files\n";
            if (data.IsRunning)
                tt = tt + "- Uninstall the current service\n";
            tt = tt + "- Swap folders (current and this)\n";
            tt = tt + "- Start this version of the service\n";
            if (data.IsCompressedService)
                tt = tt + "- Compress the current version files\n";
            activateButton = new Button(null, "Activate", tt, "../icons/fav_on.svg", false, () => doWork(activateButton, async () => {
                await sendRequest("VersionActivate", version);
            }));
            actions.appendChild(activateButton.Element);

            tt = "Set the last used timestamp to now.\nThis will prevent this version from being expired and removed for a bit longer.";
            touchButton = new Button(null, "Touch", tt, "../icons/rating_on.svg", false, () => doWork(touchButton, async () => {
                const r = await sendRequest("VersionTouch", version);
                if (r)
                    showStats(r);
            }));
            actions.appendChild(touchButton.Element);

            tt = "Validate the integrity of the compressed version.";
            validateButton = new Button(null, "Verify", tt, "../icons/keep.svg", false, () => doWork(validateButton, async () => {
                const r = await sendRequest("VersionVerify", version);
                if (r)
                    showStats(r);
            }));
            actions.appendChild(validateButton.Element);

            tt = "Expand the files in this compressed version.";
            expandButton = new Button(null, "Expand", tt, "../icons/table_log.svg", false, () => doWork(expandButton, async () => {
                await sendRequest("VersionExpand", version);
            }));
            actions.appendChild(expandButton.Element);

            tt = "Compress the files.";
            compressButton = new Button(null, "Compress", tt, "../icons/table_compression.svg", false, () => doWork(compressButton, async () => {
                await sendRequest("VersionCompress", version);
            }));
            actions.appendChild(compressButton.Element);


            tt = "Delete this version";
            deleteButton = new Button(null, "Delete", tt, "../icons/close.svg", false, () => doWork(deleteButton, async () => {
                if (await Confirm("Delete",
                    "Delete this version:\n\n" +
                    '"' + data.FullPath + '"\n\n' +
                    "The folder will be permamently removed!\n" +
                    "Are you sure ?",
                    "Yes, Delete!",
                    "No, Keep it!",
                    "../icons/close.svg",
                    "../icons/fav_on.svg",
                    "Click to pemamently delete the folder containing this version",
                    "Click to keep this version")) {
                        if (await sendRequest("VersionDelete", version))
                            History.back();
                }
            }));
            actions.appendChild(deleteButton.Element);


            PageLoaded();
        }

        updateButtonState();

        /**
         * The current child updater the Add* helpers write to (values grid by default; reassigned by `showStats`).
         * @type {{Target:HTMLElement, Add:function(HTMLElement):HTMLElement, Complete:function():void}}
         */
        let updater = BeginElementChildUpdate(values);

        /**
         * Renders chunk statistics `r` (`SmCompFileStats`) into the output grid `si-values[1]`: Files, Chunks,
         * Unique chunks, Chunk reuse % (if `ChunkCount > 0`), Compressed size (`ChunkCompSize + FileSize`),
         * Expanded size and Compression ratio (if `ChunkExpSize > 0`), Broken files and Missing chunks
         * (if `TotalMissing > 0`). Reassigns `updater` of the enclosing `update` call (the one that created the
         * buttons, i.e. the first one).
         * @param {{FileCount:number, ChunkCount:number, UniqueChunks:number, ChunkCompSize:number, FileSize:number, ChunkExpSize:number, TotalMissing:number, BrokenFiles:number}} r - The statistics.
         * @returns {void}
         */
        function showStats(r) {
            updater = BeginElementChildUpdate(output);
            AddCount("Files", r.FileCount, null, "Total number of files found in the compressed version");
            AddCount("Chunks", r.ChunkCount, null, "Total number chunks used the compressed version");
            AddCount("Unique chunks", r.UniqueChunks, null, "Total number unique chunks used the compressed version");
            if (r.ChunkCount > 0)
                AddPercentage("Chunk reuse", 100 - 100.0 * r.UniqueChunks / r.ChunkCount, null, "How many chunks that is reused as a percentage");
            const compSize = r.ChunkCompSize + r.FileSize;
            AddByteSize("Compressed size", compSize, null, "The size of all compressed chunks + the size of the archive file");
            if (r.ChunkExpSize > 0) {
                AddByteSize("Expanded size", r.ChunkExpSize, null, "The size of all files after expansion");
                AddPercentage("Compression ratio", 100.0 * compSize / r.ChunkExpSize, null, "The effective compression ratio");
            }
            if (r.TotalMissing > 0) {
                AddCount("Broken files", r.BrokenFiles, null, "Total number of files that have missing chunks");
                AddCount("Missing chunks", r.TotalMissing, null, "Total number of missing chunks");
            }
            updater.Complete();
        }

        /**
         * Adds a `<si-value-name>` (text `key`, title `keyTitle`) and a `<si-value-value>` showing the short
         * timestamp of `value`; its title is `valueTitle` plus the full timestamp; clicking copies the raw value.
         * @param {string} key - Name cell text.
         * @param {string} value - ISO timestamp.
         * @param {?string} [valueTitle] - Extra tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @returns {void}
         */
        function AddTime(key, value, valueTitle, keyTitle) {
            let v = document.createElement("si-value-name");
            v.innerText = key;
            if (keyTitle)
                v.title = keyTitle;
            updater.Add(v);

            v = document.createElement("si-value-value");
            let dv = ValueFormat.getTimeStampTitle(new Date(value));
            v.innerText = dv[1];
            v.title = valueTitle ? (valueTitle + "\n\n" + dv[0]) : dv[0];
            ValueFormat.copyOnClick(v, value, false, true);
            updater.Add(v);
        }

        /**
         * Adds a `<si-value-name>` / `<si-value-value>` pair through `updater`; clicking the value copies
         * `copyValue` (or `value` when `copyValue` is falsy).
         * @param {string} key - Name cell text.
         * @param {*} value - Value cell text.
         * @param {?string} [valueTitle] - Tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @param {*} [copyValue] - Value copied on click instead of `value`.
         * @returns {void}
         */
        function AddString(key, value, valueTitle, keyTitle, copyValue) {
            let v = document.createElement("si-value-name");
            v.innerText = key;
            if (keyTitle)
                v.title = keyTitle;
            updater.Add(v);

            v = document.createElement("si-value-value");
            v.innerText = value;
            if (valueTitle)
                v.title = valueTitle;
            ValueFormat.copyOnClick(v, copyValue || value, false, true);
            updater.Add(v);
        }

        /**
         * `AddString` with "Yes" / "No" for a truthy / falsy `value`.
         * @param {string} key - Name cell text.
         * @param {*} value - Flag.
         * @param {?string} [valueTitle] - Tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @returns {void}
         */
        function AddYesNo(key, value, valueTitle, keyTitle) {
            AddString(key, value ? "Yes" : "No", valueTitle, keyTitle);
        }

        /**
         * `AddString` with `ValueFormat.toString(value, 0)`; clicking copies the raw number.
         * @param {string} key - Name cell text.
         * @param {number} value - Count.
         * @param {?string} [valueTitle] - Tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @returns {void}
         */
        function AddCount(key, value, valueTitle, keyTitle) {
            AddString(key, ValueFormat.toString(value, 0) , valueTitle, keyTitle, value);
        }

        /**
         * `AddString` with `ValueFormat.toString(value, 2) + " %"`; clicking copies the raw number.
         * @param {string} key - Name cell text.
         * @param {number} value - Percentage (0-100).
         * @param {?string} [valueTitle] - Tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @returns {void}
         */
        function AddPercentage(key, value, valueTitle, keyTitle) {
            AddString(key, ValueFormat.toString(value, 2) + " %", valueTitle, keyTitle, value);
        }
        /**
         * `AddString` with `ValueFormat.formatByteSize(value)`; clicking copies the raw byte count.
         * @param {string} key - Name cell text.
         * @param {number} value - Size in bytes.
         * @param {?string} [valueTitle] - Tooltip of the value cell.
         * @param {?string} [keyTitle] - Tooltip of the name cell.
         * @returns {void}
         */
        function AddByteSize(key, value, valueTitle, keyTitle) {
            AddString(key, ValueFormat.formatByteSize(value), valueTitle, keyTitle, value);
        }


        AddTime("Uploaded", data.Uploaded, "The time when this version was uploaded.");
        AddYesNo("Active", data.IsActive);
        AddString("Folder", data.Name, "Folder names change when activated / deactivated.", "Folder names change when activated / deactivated.");
        AddString("User", data.User);
        AddString("Machine", data.Machine);
        AddString("Comment", data.Comment);
        AddTime("Last used", data.LastUsed, "The time when this version was used last.");
        AddYesNo("Compressed", data.Comp);
        AddCount("Files", data.Count);
        AddByteSize("Size", data.Size);
        AddString("Path", data.FullPath);

        updater.Complete();

    }
    // Endless polling loop: fetch the version, render it, wait 5 s.
    for (; ;) {
        try {
            const noOld = !data;
            data = await sendRequest("GetVersion", version);
            update(noOld && data);
        }
        catch (e)
        {
            Fail(e);
        }
        await delayWithAbort(5000, abortWait);
    }


}