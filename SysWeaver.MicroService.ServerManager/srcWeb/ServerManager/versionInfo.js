function BeginElementChildUpdate(targetElement) {
    const el = targetElement;
    let ccl = el.children?.length ?? 0;
    let cindex = 0;
    function add(e) {
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
    const version = p.get("p");
    if (!version) {
        Fail("No version paramater supplied!");
        return;
    }
    if (window.IsTop)
        document.body.classList.add("TopWindow");

    const name = document.body.getElementsByTagName("si-name")[0];
    const values = document.body.getElementsByTagName("si-values")[0];
    let title = version.replace(',', " - ");
    document.title = title;
    name.innerText = title;
    const abortWait = new AbortHandler();

    let data = null;
    function update(first) {

        if (first) {
            PageLoaded();
            title = data.ServiceName + " - " + ValueFormat.getTimeStampTitle(new Date(data.Uploaded))[1] + " - " + data.Name;
            document.title = title;
            name.innerText = title;
        }
        const updater = BeginElementChildUpdate(values);

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
        AddYesNo("Compressed", data.Compressed);
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