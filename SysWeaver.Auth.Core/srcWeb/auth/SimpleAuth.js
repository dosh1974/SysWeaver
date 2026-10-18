
/**
 * @file SimpleAuth.js - page logic for the two web pages of SimpleAuthorizer (SysWeaver.Auth.Core),
 * served under the url folder auth/.
 *
 * Loading: a static <script defer> in index.html ("Generate password hash", body onload = pwdGenMain())
 * and KeyMan.html ("API-key management", body onload = keyManMain()). Script order:
 * ../common/common.js, (KeyMan.html only: ../app/application.js), Auth.js, AuthUi.js, SimpleAuth.js.
 *
 * Globals defined: only plain global functions (no classes, no top-level variables, no immediately
 * executed code): getUserSalt, getPasswordPolicy, pwdGenMain, isApiKeysSupported, getApiKeys,
 * removeApiKey, createApiKey, keyManMain. Nothing outside this folder calls them.
 *
 * Server APIs (SimpleAuthorizer.cs): "../Api/debug/simpleAuth/" (GetAuthSalt, GetPasswordPolicy) and
 * "../Api/simpleAuth/" (ApiKeysSupported, AppInfo, GetApiKeys, AddApiKey, RemoveApiKey). The prefixes are
 * repeated as local `apiPrefix` constants in each function.
 *
 * Dependencies:
 * - common.js: sendRequest, getRequest, hashString, _TF/_T, Fail, ValueFormat.copyToClipboardInfo,
 *   ColorIcon, Button, isLetter, downloadText, PageLoaded.
 * - app/application.js (KeyMan.html only): PopUp, Confirm.
 * - CROSS-FOLDER: Auth.js and AuthUi.js are NOT in this project; they belong to
 *   SysWeaver.MicroService.Auth (srcWeb/auth) and are merged into the same auth/ url folder at runtime.
 *   From Auth.js: AuthTrim, ValidatePassword, AuthPolicyText, CheckPassword (haveibeenpwned leak check).
 *   From AuthUi.js: AuthPage, AuthLabel, AuthInput, AuthNewPassword, AuthError, AuthText, AuthAddIcon,
 *   AuthButton, AuthButtonRow, AuthSetText, AuthSetError, CreateAuthWrapper.
 *   PassKey (Passkey.js, SysWeaver.MicroService.PassKey) is only probed with typeof, result unused.
 *
 * Conventions: user-visible strings are localized with _TF/_T; password hashes are computed as
 * hashString(password + "|" + salt), the same scheme as the non-binary branch of loginRequest in Auth.js.
 */

////// Salt ///////////////////////////////

/**
 * Fetches the per-user password salt from the server (SimpleAuthorizer.GetAuthSalt).
 * @param {string} user - The user name to get the salt for.
 * @returns {Promise<string>} The salt string; rejects if the request fails.
 */
async function getUserSalt(user) {

    const apiPrefix = "../Api/debug/simpleAuth/";
    return await sendRequest(apiPrefix + "GetAuthSalt", user);
}

/**
 * Fetches the password policy (SimpleAuthorizer.GetPasswordPolicy) used to validate new passwords.
 * The returned object is passed to ValidatePassword, AuthPolicyText and AuthNewPassword (MicroService.Auth).
 * @returns {Promise<object>} The password policy object.
 */
async function getPasswordPolicy() {

    const apiPrefix = "../Api/debug/simpleAuth/";
    return await getRequest(apiPrefix + "GetPasswordPolicy");
}



/**
 * Entry point (body onload) of index.html, the "Generate password hash" page.
 * Builds an auth box via AuthPage("IconLock", builder) (AuthUi.js, MicroService.Auth) containing:
 * a "Username" AuthInput (autocomplete "username"), a "Password" AuthNewPassword (autocomplete
 * "new-password", random-password icon, policy-aware), an AuthError info line and a read-only "Generated hash"
 * AuthText with an IconAuthCopy icon (24px) that copies the text unless it is "-".
 * The policy text (AuthPolicyText) is used as tool tip of the password label and the info line.
 * Every input change runs the inner setStatus; it also runs once initially, then the username input is focused.
 * Closure state: userHash / userHashName cache the salt of the last user name. `havePasskey` is computed but unused.
 * The output line "user : hash" is meant to be pasted into the SimpleAuthorizer user configuration.
 * @returns {Promise<void>}
 */
async function pwdGenMain() {
    await AuthPage("IconLock", async target => {

        /** @type {object} The server password policy. */
        const policy = await getPasswordPolicy();
        /** @type {string} Human readable policy description (AuthPolicyText, Auth.js), used as tool tip. */
        const policyText = AuthPolicyText(policy);
        /** @type {boolean} True if Passkey.js (MicroService.PassKey) is loaded; computed but never used. */
        const havePasskey = typeof PassKey !== "undefined";
        AuthLabel(target, _TF("Username", "The label of a user input field where the user should enter their user name"));
        /** @type {HTMLInputElement} User name input (AuthInput, AuthUi.js); runs setStatus on change. */
        const uname = AuthInput(target, _TF("Enter a valid username", "The tool tip and place holder text for a user input field where the user should enter their user name"), null, "username", async input => await setStatus(uname.value, pwd.value));
        AuthLabel(target, _TF("Password", "The label of a user input field where the user should enter a new password")).title = policyText;


        /** @type {string|null} Cached salt of the user name in userHashName (fetched with getUserSalt). */
        let userHash = null;
        /** @type {string|null} The user name the cached salt in userHash belongs to. */
        let userHashName = null;

        /**
         * Validates the inputs and recomputes the displayed hash (inner function of pwdGenMain).
         * Trims both values (AuthTrim). Empty user name: marks the username input as error, shows
         * "Enter a valid username", sets the result to "-" and returns false. Otherwise validates the password
         * with ValidatePassword(uText, pwText, null, null, null, policy); a policy error is shown in the info line
         * and the result is "-". When valid: starts CheckPassword(info, pwText) (haveibeenpwned leak check, not
         * awaited), shows "Computing..", fetches the salt with getUserSalt if the user name changed (on failure
         * clears the info line, shows Fail and returns undefined), then shows "user : hashString(pw|salt)".
         * @param {string} uText - The user name as typed.
         * @param {string} pwText - The password as typed.
         * @returns {Promise<boolean|undefined>} True if the password is valid, false if not, undefined if the salt request failed.
         */
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

        /** @type {HTMLInputElement} New password input with random-password icon (AuthNewPassword, AuthUi.js). */
        const pwd = AuthNewPassword(target, _TF("Enter a password", "The tool tip and placeholder description of a user text input box where the user should eneter a new password"), null, "new-password", async input => await setStatus(uname.value, pwd.value), null, policy);

        /** @type {HTMLElement} Info / error line showing policy errors and the leak check result. */
        const info = AuthError(target, "", _TF("Click to copy text to the clipboard", "Tool tip description of a button placed inside a text input box that when clicked will copy the inputted text to the clipboard"));
        info.title = policyText;

        AuthLabel(target, _TF("Generated hash", "The label of readonly text input box that will be populated with a password hash"));
        /** @type {HTMLElement} Read-only SysWeaver-AuthText showing "user : hash" (or "-"). */
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

/**
 * Checks whether API key management is available for the current session ("../Api/simpleAuth/ApiKeysSupported").
 * Shows a Fail toast (20 s) "Can't manage API keys for this service!" if the server returns false, or
 * "Not authorized to manage API keys!" + error if the request throws (e.g. session lacks ApiKeyAuth).
 * @returns {Promise<boolean>} True if supported and authorized, otherwise false.
 */
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


/**
 * Fetches all API keys ("../Api/simpleAuth/GetApiKeys").
 * @returns {Promise<string[]|null>} Array of "name:key" strings, or null.
 */
async function getApiKeys() {

    const apiPrefix = "../Api/simpleAuth/";
    return await getRequest(apiPrefix + "GetApiKeys");
}

/**
 * Asks the user for confirmation (Confirm, app/application.js) and then removes an API key
 * ("../Api/simpleAuth/RemoveApiKey", audited on the server).
 * @param {string} keyName - The name (identifier) of the key to remove.
 * @returns {Promise<boolean|*>} False if cancelled or on error (a Fail toast is shown), otherwise the server result.
 */
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


/**
 * Shows a closable PopUp (app/application.js) where the user enters a service name and creates a new API key.
 * The pop-up content is an auth box from CreateAuthWrapper() (AuthUi.js, MicroService.Auth) with the extra class
 * SysWeaver-SimpleAuth (SimpleAuth.css resets the floating-box styling), a "Name" AuthLabel, an AuthInput
 * (maxLength 64) and an initially disabled "Create new API-key" AuthButton (IconAdd) in an AuthButtonRow.
 * The input change handler filters the value to letters (isLetter), digits, '_' and '-' (validMap), writes the
 * filtered value back (calling inp.OnChange()), and enables the button only when non-empty; the isInternal flag
 * prevents re-entry. Enter in the input or the button runs the inner CreateKey.
 * @param {function():void} rebuildFn - Called after a key was created (refreshes the key table).
 * @returns {Promise<boolean>} True if a key was created before the pop-up closed.
 */
async function createApiKey(rebuildFn) {

    /** @type {Map<string, boolean>} Non-letter characters allowed in a key name (digits, '_' and '-'). */
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
        /** @type {boolean} Re-entrancy guard for the input change handler (set while it rewrites the value). */
        let isInternal = false;

        /**
         * Creates the key (inner function of createApiKey). Puts the button in working state and the input
         * read-only, then sends "../Api/simpleAuth/AddApiKey" with the input value. On a non-null result sets
         * didCreate, awaits closeFn() (closes the pop-up), copies the returned "name:key" to the clipboard and
         * calls rebuildFn(). A null result shows Fail("Failed to create API-key!"), exceptions show Fail(e).
         * Finally restores the input and stops the working state.
         * @returns {Promise<void>}
         */
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

/**
 * Entry point (body onload) of KeyMan.html, the "API-key management" page (menu entry only shown when
 * SimpleAuthorizer.CanManageKeys). Returns early if isApiKeysSupported() is false. Otherwise fetches
 * "../Api/simpleAuth/AppInfo", derives baseUrl from location.href, runs the inner rebuild() (not awaited)
 * and calls PageLoaded() itself (this page does not use AuthPage, so SysWeaverIgnoreUserChanges is not called).
 * @returns {Promise<void>}
 */
async function keyManMain() {
    if (!await isApiKeysSupported())
        return;

    /** @type {number|null} Handle of the pending 30 s auto refresh timer (setTimeout) of rebuild. */
    let autoUpdate = null;
    /** @type {HTMLTableElement|null} The key table, created on the first rebuild and appended to document.body. */
    let table = null;

    /** @type {string[]} Application info [AppName, AppDisplayName], used for the credentials file name and text. */
    const appName = await getRequest("../Api/simpleAuth/AppInfo");
    /** @type {string} The full url of this page. */
    const fullUrl = window.location.href;
    /** @type {number} Index of "/auth/" in fullUrl. */
    const fi = fullUrl.indexOf("/auth/");
    /** @type {string} The service base url (everything before "/auth/"), written into downloaded credentials files. */
    const baseUrl = fullUrl.substring(0, fi);


    /**
     * Renders or updates the key table in place (inner function of keyManMain).
     * Clears any pending autoUpdate timer and fetches keys (getApiKeys() ?? []). On the first call creates the
     * <table> with a header row (th.header, colspan 4, "API keys") and a title row (th.title "Name", th.title
     * colspan 3 "Key"). Each key "name:key" (split at the first ':') occupies row index i+2:
     * new rows get a name cell, a copy cell (ColorIcon IconCopy, copies the whole "name:key"; key stored in the
     * expando cell.Pwd), a download cell (ColorIcon IconDownload, downloadText("<name>_<AppName>.txt", credentials
     * text with AppDisplayName and baseUrl)) and a remove cell (ColorIcon IconRemove, removeApiKey(name) then rebuild()).
     * Existing rows are updated: the name is replaced if changed, copy/download icons are rebuilt if name or key
     * changed, the remove icon if the name changed. If an "action" row occupies a key row index (or a key row
     * occupies the action row index) all rows from that index are removed. The trailing row with class "action"
     * holds a SysWeaver-CenterBlock with a "Create new key" Button (IconAdd) that awaits createApiKey(rebuild).
     * Errors show Fail(e). Always reschedules itself with setTimeout(rebuild, 30000).
     * @returns {Promise<void>}
     */
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

    PageLoaded();

}