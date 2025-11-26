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

async function versionInfoMain() {
    const p = getUrlParams();
    let version = p.get("p");
    if (!version) {
        Fail("No version paramater supplied!");
        return;
    }
    const versionParts = version.split(',');
    if (window.IsTop)
        document.body.classList.add("TopWindow");


    function setTitle() {
        const o = versionParts[1];
        versionParts[1] = ValueFormat.getTimeStampTitle(new Date(o))[1];
        const t = versionParts.join(" - ");
        versionParts[1] = o;
        document.title = t;
        name.innerText = t;
    }


    const name = document.body.getElementsByTagName("si-name")[0];
    const values = document.body.getElementsByTagName("si-values")[0];
    const actions = document.body.getElementsByTagName("si-actions")[0];
    const output = document.body.getElementsByTagName("si-values")[1];

    let buttonsEnabled = true;

    let activateButton = null;
    let touchButton = null;

    let validateButton = null;
    let expandButton = null;

    let compressButton = null;
    let deleteButton = null;


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
            Fail(e.Message);
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
    const abortWait = new AbortHandler();

    let data = null;



    function update(first) {

        if (first) {
            versionParts[0] = data.ServiceName;
            versionParts[1] = data.Uploaded;
            versionParts[2] = data.Name;
            version = versionParts.join(',');
            setTitle();

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

        let updater = BeginElementChildUpdate(values);

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

        function AddYesNo(key, value, valueTitle, keyTitle) {
            AddString(key, value ? "Yes" : "No", valueTitle, keyTitle);
        }

        function AddCount(key, value, valueTitle, keyTitle) {
            AddString(key, ValueFormat.toString(value, 0) , valueTitle, keyTitle, value);
        }

        function AddPercentage(key, value, valueTitle, keyTitle) {
            AddString(key, ValueFormat.toString(value, 2) + " %", valueTitle, keyTitle, value);
        }
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