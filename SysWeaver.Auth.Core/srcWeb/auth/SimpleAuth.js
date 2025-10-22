
////// Salt ///////////////////////////////

async function getUserSalt(user) {

    const apiPrefix = "../Api/debug/simpleAuth/";
    return await sendRequest(apiPrefix + "GetAuthSalt", user);
}
async function getPasswordPolicy() {

    const apiPrefix = "../Api/debug/simpleAuth/";
    return await getRequest(apiPrefix + "GetPasswordPolicy");
}



async function pwdGenMain() {
    await AuthPage("IconLock", async target => {

        const policy = await getPasswordPolicy();
        const policyText = AuthPolicyText(policy);
        const havePasskey = typeof PassKey !== "undefined";
        AuthLabel(target, _TF("Username", "The label of a user input field where the user should enter their user name"));
        const uname = AuthInput(target, _TF("Enter a valid username", "The tool tip and place holder text for a user input field where the user should enter their user name"), null, "username", async input => await setStatus(uname.value, pwd.value));
        AuthLabel(target, _TF("Password", "The label of a user input field where the user should enter a new password")).title = policyText;


        let userHash = null;
        let userHashName = null;

        async function setStatus(uText, pwText) {
            uText = AuthTrim(uText);
            pwText = AuthTrim(pwText);
            if ((!uText) || (uText.length < 1)) {
                AuthSetError(uname, true);
                AuthSetError(pwd, false);
                AuthSetText(info, _TF("Enter a valid username", "Text that is displayed to the user when no valid user name is entered"));
                res.innerHTML = "-";
                return false;
            }
            const p = await ValidatePassword(uText, pwText, null, null, null, policy);
            AuthSetError(uname, false);
            AuthSetError(pwd, p);
            if (p) {
                AuthSetText(info, p);
                res.innerHTML = "-";
            } else {
                CheckPassword(info, pwText); // Don't await, run async
                res.innerText = _TF("Computing..", "A message shown to the user while a new password hash is computed");
                if (uText !== userHashName) {
                    try {
                        userHash = await getUserSalt(uText);
                    }
                    catch (e) {
                        AuthSetText(info);
                        Fail(_TF("Failed to get user salt.", "An error message that is displayed when the backend failed to provide a salt") + "\n" + e);
                        return;
                    }
                    userHashName = uText;
                }
                const str = pwText + "|" + userHash;
                const hash = await hashString(str);
                res.innerText = uText + " : " + hash;

            }
            return !p;
        }

        const pwd = AuthNewPassword(target, _TF("Enter a password", "The tool tip and placeholder description of a user text input box where the user should eneter a new password"), null, "new-password", async input => await setStatus(uname.value, pwd.value), null, policy);

        const info = AuthError(target, "", _TF("Click to copy text to the clipboard", "Tool tip description of a button placed inside a text input box that when clicked will copy the inputted text to the clipboard"));
        info.title = policyText;

        AuthLabel(target, _TF("Generated hash", "The label of readonly text input box that will be populated with a password hash"));
        const res = AuthText(target, "", _TF("Click to copy text to the clipboard", "Tool tip description of a button placed inside a text input box that when clicked will copy the inputted text to the clipboard"));

        AuthAddIcon(res, "IconAuthCopy", async e => {
            const v = res.textContent;
            if (v && (v !== "-")) {
                await ValueFormat.copyToClipboardInfo(v);
            }
        }, null, _TF("Click to copy the password hash to the clipboard", "Tool tip description of a button placed inside a text input box that when clicked will copy the text to the clipboard"), 24);

        await setStatus(uname.value, pwd.value);
        uname.focus();
    });

}

async function isApiKeysSupported() {

    const apiPrefix = "../Api/simpleAuth/";
    try {
        if (await getRequest(apiPrefix + "ApiKeysSupported"))
            return true;
        Fail(_TF("Can't manage API keys for this service!", "An error message that is displayed if the back end service doesn't allow API key management"), 20000);
    }
    catch (e)
    {
        Fail(_TF("Not authorized to manage API keys!", "An error message that is displayed if the current session isn't authorized to manage API keys") + "\n" + e, 20000);
    }
    return false;
}


async function getApiKeys() {

    const apiPrefix = "../Api/simpleAuth/";
    return await getRequest(apiPrefix + "GetApiKeys");
}

async function removeApiKey(keyName) {

    if (!await Confirm(
        _TF("Remove key", "The title of a confirmation dialog that is shown when a user wants to remove an API key"),
        _T("The key \"{0}\" will be removed!\nYou won't be able to restore it with the same password, ever!\n\nAre you sure?", keyName, "The text of a confirmation dialog that is shown when a user wants to remove an API key. {0} is replaced with the name (identifier) of the API key"),
        _TF("Yes, remove it!", "The text of a button on a confirmation dialog that when pressed will remove an API key"),
        _TF("No, keep it!", "The text of a button on a confirmation dialog that when pressed will close the dialog without any action"),
        "IconCancel", "IconOk",
        _TF("Remove the API key forever!", "The tool tip description of a button on a confirmation dialog that when pressed will remove an API key"),
        _TF("I'm not ready to take the blame for this", "The tool tip description of a button on a confirmation dialog that when pressed will close the dialog without any action"))
    )
        return false;
    try {
        const apiPrefix = "../Api/simpleAuth/";
        return await sendRequest(apiPrefix + "RemoveApiKey", keyName);
    }
    catch (e) {
        Fail(e);
        return false;
    }
}


async function createApiKey(rebuildFn) {

    const validMap = new Map();
    validMap.set('0', true);
    validMap.set('1', true);
    validMap.set('2', true);
    validMap.set('3', true);
    validMap.set('4', true);
    validMap.set('5', true);
    validMap.set('6', true);
    validMap.set('7', true);
    validMap.set('8', true);
    validMap.set('9', true);
    validMap.set('_', true);
    validMap.set('-', true);
    let didCreate = false;
    await PopUp(async (el, closeFn) => {
        const element = CreateAuthWrapper();
        element.classList.add("SysWeaver-SimpleAuth");
        el.appendChild(element);
        AuthLabel(element,
            _TF("Name", "The label of a user text input field where the user should enter the name (identifier) of a new API key"),
            _TF("Please enter the name of the service that will use this API-key below", "The tool tip description of a label to user text input field where the user should enter the name (identifier) of a new API key"));
        let isInternal = false;

        async function CreateKey() {
            button.StartWorking();
            inp.readOnly = true;
            try {
                const apiPrefix = "../Api/simpleAuth/";
                const res = await sendRequest(apiPrefix + "AddApiKey", inp.value);
                if (res != null) {
                    didCreate = true;
                    await closeFn();
                    ValueFormat.copyToClipboardInfo(res);
                    rebuildFn();
                } else {
                    Fail(_TF("Failed to create API-key!", "A error message that is displayed when the back end service failed to create a new API key"));
                }
            }
            catch (e) {
                Fail(e);
            }
            inp.readOnly = false;
            button.StopWorking();
        }


        const inp = AuthInput(element,
            _TF("Service name", "The place holder text of a user text input field where the user should enter the name (identifier) of a new API key"),
            _TF("Enter the name of the service that will use this API-key.\nOnly alpha numericals, '_' and '-' is allowed.", "The tool tip description of a user text input field where the user should enter the name (identifier) of a new API key"),
            null, () => {
            if (isInternal)
                return;
            isInternal = true;
            const val = inp.value;
            const vl = val.length;
            let valid = "";
            for (let i = 0; i < vl; ++i) {
                const c = val.charAt(i);
                if (!validMap.get(c)) {
                    if (!isLetter(c))
                        continue;
                }
                valid += c;
            }
            if (valid != val) {
                inp.value = valid;
                inp.OnChange();
            }
            button.SetEnabled(valid.length > 0);
            isInternal = false;
        }, CreateKey);
        inp.maxLength = 64;
        const button = AuthButton(AuthButtonRow(element),
            _TF("Create new API-key", "The text of a button that when pressed will create a new API key"),
            _TF("Click to create the new API-key for the given service", "The tool tip description of a button that when pressed will create a new API key"),
            "IconAdd", CreateKey, true);
        inp.focus();
    }, true);
    return didCreate;
}

async function keyManMain() {
    if (!await isApiKeysSupported())
        return;

    let autoUpdate = null;
    let table = null;

    const appName = await getRequest("../Api/simpleAuth/AppInfo");
    const fullUrl = window.location.href;
    const fi = fullUrl.indexOf("/auth/");
    const baseUrl = fullUrl.substring(0, fi);
   

    async function rebuild() {

        if (autoUpdate)
            clearTimeout(autoUpdate);
        autoUpdate = null;
        try {
            const keys = (await getApiKeys()) ?? [];
            if (!table) {
                table = document.createElement("table");
                const headerRow = table.insertRow();
                let th = document.createElement("th");
                th.colSpan = 4;
                th.classList.add("header");
                th.innerText = _TF("API keys", "The title of a table containing all API keys available on a server");
                headerRow.appendChild(th);

                const titelRow = table.insertRow();
                th = document.createElement("th");
                th.classList.add("title");
                th.innerText = _TF("Name", "The header text of a column that contains the names (identifiers) of API keys available on a server");
                th.title = _TF("This is typically the name of the service that will use this API-key", "The tool tip description of a column that contains the names (identifiers) of API keys available on a server");
                titelRow.appendChild(th);

                th = document.createElement("th");
                th.classList.add("title");
                th.colSpan = 3;
                th.innerText = _TF("Key", "The header text of a column that contains actions that can be performed on API keys (per row) available on a server");
                th.title = _TF("Operations that can be made on the key", "The tool tip description of a column that contains actions that can be performed on API keys (per row) available on a server");
                titelRow.appendChild(th);

                document.body.appendChild(table);
            }
            const kl = keys.length;
            const rows = table.rows;
            let rowCount = rows.length;
            for (let i = 0; i < kl; ++i) {
                const kv = keys[i];
                const ni = kv.indexOf(':');
                const name = kv.substring(0, ni);
                const pwd = kv.substring(ni + 1);
                const ri = i + 2;
                if (ri < rowCount) {
                    const row = rows[ri];
                    if (row.classList.contains("action")) {
                        while (rowCount > ri) {
                            --rowCount;
                            rows[rowCount].remove();
                        }
                    }
                }
                if (ri >= rowCount) {
                    const row = table.insertRow();
                    let c = row.insertCell();
                    c.innerText = name;
                    c = row.insertCell();
                    c.Pwd = pwd;
                    const copy = new ColorIcon("IconCopy", "IconColorThemeMain", 32, 32,
                        _T("Click to copy \"{0}\" to the clipboard", kv, "The tool tip description of a button that when pressed will copy an API-key to the clipboard. {0} is replaced with the API-key"),
                        async () => {
                            copy.StartWorking();
                            await ValueFormat.copyToClipboardInfo(kv);
                            copy.StopWorking();
                        });
                    c.appendChild(copy.Element);

                    c = row.insertCell();
                    const download = new ColorIcon("IconDownload", "IconColorThemeAcc1", 32, 32,
                        _TF("Click to download a credentials file, ready to use", "The tool tip description of a button that when pressed will download an API-key in a text file"),
                        () => {
                            downloadText(name + "_" + appName[0] + ".txt", "# API credentials for accessing the " + appName[1] + " service at " + baseUrl + "\n\n" + kv + "\n");
                        });
                    c.appendChild(download.Element);

                    c = row.insertCell();
                    const remove = new ColorIcon("IconRemove", "IconColorThemeMain", 32, 32,
                        _T("Click to remove the key for \"{0}\"", name, "The tool tip description of a button that when pressed will remove an API-key from a service. {0} is replaced with the name (identifier) of the API-key that will be removed"),
                        async () => {
                            remove.StartWorking();
                            if (await removeApiKey(name))
                                await rebuild();
                            remove.StopWorking();
                        });
                    c.appendChild(remove.Element);

                } else {
                    const row = rows[ri];
                    let c = row.firstElementChild;
                    const newName = c.textContent !== name;
                    if (newName)
                        c.textContent = name;
                    c = row.children[1];
                    if (newName || (c.Pwd !== pwd)) {
                        c.Pwd = pwd;
                        c.firstElementChild.remove();
                        const copy = new ColorIcon("IconCopy", "IconColorThemeMain", 32, 32,
                            _T("Click to copy \"{0}\" to the clipboard", kv, "The tool tip description of a button that when pressed will copy an API-key to the clipboard. {0} is replaced with the API-key"),
                            async () => {
                                copy.StartWorking();
                                await ValueFormat.copyToClipboardInfo(kv);
                                copy.StopWorking();
                            });
                        c.appendChild(copy.Element);

                        c = row.children[2];
                        c.firstElementChild.remove();
                        const download = new ColorIcon("IconDownload", "IconColorThemeAcc1", 32, 32,
                            _TF("Click to download a credentials file, ready to use", "The tool tip description of a button that when pressed will download an API-key in a text file"),
                            () => {
                                downloadText(name + "_" + appName[0] + ".txt", "# API credentials for accessing the " + appName[1] + " service at " + baseUrl + "\n\n" + kv + "\n");
                            });
                        c.appendChild(download.Element);
                    }
                    if (newName) {
                        c = row.children[3];
                        c.firstElementChild.remove();
                        const remove = new ColorIcon("IconRemove", "IconColorThemeMain", 32, 32,
                            _T("Click to remove the key for \"{0}\"", name, "The tool tip description of a button that when pressed will remove an API-key from a service. {0} is replaced with the name (identifier) of the API-key that will be removed"),
                            async () => {
                                remove.StartWorking();
                                if (await removeApiKey(name))
                                    await rebuild();
                                remove.StopWorking();
                            });
                        c.appendChild(remove.Element);
                    }
                }

            }
            const ai = kl + 2;
            if (ai < rowCount) {
                const row = rows[ai];
                if (!row.classList.contains("action")) {
                    while (rowCount > ai) {
                        --rowCount;
                        rows[rowCount].remove();
                    }
                }
            }
            if (ai >= rowCount) {
                const row = table.insertRow();
                row.classList.add("action");
                let c = row.insertCell();
                c.colSpan = 4;
                const add = new Button(null,
                    _TF("Create new key", "The text of a button that when pressed will show a dialog where the user can enter information about a new API key"),
                    _TF("Click to enter a service name and create a new API-key", "The tool tip description of a button that when pressed will show a dialog where the user can enter information about a new API key"),
                    "IconAdd", true, async () => {
                        add.StartWorking();
                        await createApiKey(rebuild);
                        add.StopWorking();
                    });
                const br = document.createElement("SysWeaver-CenterBlock");
                c.appendChild(br);
                br.appendChild(add.Element);
            }
        }
        catch (e) {
            Fail(e);
        }
        autoUpdate = setTimeout(rebuild, 30000);
    }

    rebuild();


}