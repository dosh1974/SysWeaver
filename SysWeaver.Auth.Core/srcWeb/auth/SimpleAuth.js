/**
 * @file SimpleAuth.js - the SimpleAuthorizer pages of SysWeaver.Auth.Core (served as auth/SimpleAuth.js).
 *
 * Entry points (globals, body onload; also on `Sw.SimpleAuth`):
 *  - pwdGenMain (auth/index.html, menu Debug/SimpleAuth/GenPwd; a Sw.PageLayout form page with Sw.Card + Sw.TextBox + Sw.Form):
 *    enter a user name and a password (generator, the
 *    server's fixed policy, leak check) and get the configuration line "user : b64(sha256(password|salt))" with the
 *    per user salt of Api/debug/simpleAuth/GetAuthSalt; copy button.
 *  - keyManMain (auth/KeyMan.html, menu Debug/SimpleAuth/KeyMan): the API keys ("name:key"), masked by default with a
 *    per key "Show the key" toggle: copy, download a credentials file "<name>_<AppName>.txt", remove (confirmed), create
 *    (dialog: name of letters, digits, '_', '-'; the new key is copied to the clipboard). Copy toasts and tool tips never
 *    show the secret ('Key "name" copied to the clipboard.'), and a key's copy toast is closed when the key is removed.
 *    The list refreshes every 30 s (unchanged items are kept). This page
 *    reloads on user changes (it doesn't call IgnoreUserChanges).
 *
 * Loading: classic `defer` scripts ../sw/core.js, ../sw/ui.js, Auth.js, AuthUi.js (MicroService.Auth, same auth/
 * folder at runtime), SimpleAuth.js. Styles: Auth.css, SimpleAuth.css.
 * Server: Api/debug/simpleAuth/{GetAuthSalt, GetPasswordPolicy}, Api/simpleAuth/{ApiKeysSupported, AppInfo, GetApiKeys,
 * AddApiKey, RemoveApiKey}.
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const A = Sw.Auth;
    const U = Sw.AuthUi;
    const el = Sw.el;

    /**
     * Api urls.
     * @param {string} name Method.
     * @returns {string} Url under Api/simpleAuth/.
     */
    const keyApi = name => Sw.Url.Root + "Api/simpleAuth/" + name;
    /**
     * Debug api urls.
     * @param {string} name Method.
     * @returns {string} Url under Api/debug/simpleAuth/.
     */
    const dbgApi = name => Sw.Url.Root + "Api/debug/simpleAuth/" + name;

    /**
     * Turn an auth card page into a Design v2 tool page (Design v2, PORTING.md B10): the page column (main.auth-page) becomes a form page
     * (.sw-page-form, class auth-tool) with a left aligned page header (Sw.PageHeader: title, one line description, actions) above
     * the card; the big centered page icon is hidden (the header names the page).
     * @param {object} page The AuthUi page.
     * @param {string} title The page title (h1).
     * @param {string} desc The one line description.
     * @param {Array<HTMLElement>} [actions] Header actions (buttons, right aligned; at most one primary).
     * @returns {object} The header (Sw.PageHeader).
     */
    function toolPage(page, title, desc, actions) {
        //  The shared form page layout of AuthUi (MicroService.Auth, AuthPage.SetHeader); the local version below for an older AuthUi.js
        if (typeof page.SetHeader === "function")
            return page.SetHeader({ Title: title, Description: desc, Actions: actions || [] });
        page.E.classList.add("sw-page-form", "auth-tool");
        page.IconWrap.hidden = true;
        const h = Sw.PageHeader({ Title: title, Description: desc, Actions: actions || [] });
        page.E.prepend(h.E);
        return h;
    }
    const post = { Post: true, Reload: true };

    // ------------------------------------------------------------------ password hash generator

    /**
     * auth/index.html: generate a SimpleAuthorizer user line. A Design v2 form page (Sw.PageLayout "form"): a "User" card (Sw.TextBox
     * user name and password with show / hide, the policy rules listed under the password, a "Generate password" button, the leak
     * check as a badge) checked by Sw.Form (messages under the fields, no toast), and a "Configuration line" card with the result in
     * code type and a Copy action in the card head (an "empty" token until the input is valid).
     * @returns {Promise<void>}
     */
    async function pwdGenMain() {
        Sw.Session.IgnoreUserChanges();
        document.body.classList.add("sw-root");
        const remover = Sw.Page.Loading();
        try {
            const page = Sw.PageLayout({
                Type: "form",
                Title: _TF("Generate password hash", "Title of the page that computes the configuration line (user name and password hash) of a SimpleAuthorizer user"),
                Description: _TF("Compute the line of a user for the SimpleAuthorizer configuration. The password itself is not stored.", "One line description below the title of the password hash generator page"),
                Class: "auth-hashgen",
            });
            const policy = await Sw.Net.Get(dbgApi("GetPasswordPolicy"));
            const touched = { user: false, pwd: false };
            const user = new Sw.TextBox({
                Label: _TF("Username", "The label of a user input field where the user should enter their user name"),
                Placeholder: _TF("User name, ex: anna", "The place holder text of the user name field of the password hash generator (an example, not an error)"),
                AutoComplete: "off", SpellCheck: false,
                OnInput: () => { touched.user = true; update(); },
            });
            const pwd = new Sw.TextBox({
                Label: _TF("Password", "The label of a user input field where the user should enter a new password"),
                Placeholder: _TF("Enter a password", "The tool tip and placeholder description of a user text input box where the user should eneter a new password"),
                Type: "password", Reveal: true, AutoComplete: "new-password",
                OnInput: () => { touched.pwd = true; update(); },
            });
            user.Input.name = "hash-user";
            pwd.Input.name = "hash-password";
            //  The policy rules stay visible under the password (a reminder, not hidden in a tool tip)
            const rules = String(A.PolicyText(policy) || "").split("\n- ").slice(1);
            const rulesE = el("ul.auth-hashgen-rules", { attrs: { "aria-label": _TF("Password policy", "The title of a section that describes requirements for a user defined password") } }, ...rules.map(r => el("li", { text: r })));
            const leakE = el("div.auth-hashgen-leak", { attrs: { role: "status", "aria-live": "polite" } });
            const gen = new Sw.Button({
                Text: _TF("Generate password", "Button of the password hash generator that fills in a new random password (and copies it to the clipboard)"),
                Icon: "wand", Kind: "secondary",
                Title: _TF("Generate a new random password and copy it to the clipboard", "Tool tip description of a button that when pressed will generated a random safe password in the input field and also copy it to the clipboard"),
                OnClick: async () => {
                    const v = A.GeneratePassword(policy || undefined);
                    pwd.Set(v);
                    touched.pwd = true;
                    update();
                    await Sw.Clipboard.Copy(v, { Generic: true });
                },
            });
            const form = Sw.Form({
                Fields: [
                    { Name: "user", Control: user, Required: _TF("Enter a user name.", "Message under the user name field of the password hash generator when it is empty") },
                    { Name: "pwd", Control: pwd, Required: _TF("Enter a password.", "Message under the password field of the password hash generator when it is empty"), Validate: v => A.CheckPolicy(A.Trim(v), policy) || null },
                ],
            });
            page.Add(Sw.Card({
                Title: _TF("User", "Title of the card with the user name and password inputs of the password hash generator"),
                Body: el("div.auth-hashgen-form", null, user.E, el("div.auth-hashgen-pwd", null, pwd.E, rulesE), el("div.auth-hashgen-tools", null, gen.E, leakE)),
            }));
            const lineE = el("code.auth-hashgen-line", { attrs: { "data-empty": _TF("empty", "Token shown instead of the configuration line of the password hash generator while the input is not valid") } });
            const copy = new Sw.Button({
                Text: _TF("Copy", "Button in the head of the configuration line card of the password hash generator that copies the line"),
                Icon: "copy", Kind: "secondary",
                Title: _TF("Copy the password hash to the clipboard", "Tool tip description of a button placed inside a text input box that when clicked will copy the text to the clipboard"),
                OnClick: () => Sw.Clipboard.Copy(lineE.textContent),
            });
            page.Add(Sw.Card({
                Title: _TF("Configuration line", "Title of the card with the result of the password hash generator: the line for the SimpleAuthorizer configuration"),
                Sub: _TF("user : hash", "Sub title of the result card of the password hash generator: the format of the line"),
                Actions: [copy],
                Body: [lineE, el("p.auth-hashgen-note", { text: _TF("Add this line to the Users of the SimpleAuthorizer configuration.", "Note under the result of the password hash generator") })],
            }));
            /**
             * Show the result line (empty: the "empty" token, the copy button disabled).
             * @param {string} v The line.
             * @param {boolean} [busy] Still computing.
             */
            const show = (v, busy) => {
                lineE.textContent = v || "";
                lineE.classList.toggle("auth-hashgen-empty", !v);
                lineE.classList.toggle("auth-hashgen-busy", !!busy);
                copy.SetDisabled(!v || !!busy, busy
                    ? _TF("The hash is still being computed", "Reason of the disabled Copy button of the password hash generator while the hash is computed")
                    : _TF("Enter a user name and a valid password first", "Reason of the disabled Copy button of the password hash generator while there is no result"));
            };
            let salt = null;
            let saltUser = null;
            let seq = 0;
            let leakSeq = 0;
            /** @type {string|null} The password whose leak check is running or shown (the old CheckPassword memo: no new request for it). */
            let leakLast = null;
            /**
             * The leak check of a valid password as a badge next to the generate button, at once on every change of the password (the
             * request of the old CheckPassword; the same password as the shown or running check makes no new request; newest wins).
             * @param {string} p The password.
             * @returns {Promise<void>}
             */
            const leak = async p => {
                if (p === leakLast)
                    return;
                leakLast = p;
                const my = ++leakSeq;
                const wait = A.LeakText(null);
                leakE.replaceChildren(Sw.Badge(wait.Text, "info", wait.Title));
                const r = await A.CheckLeak(p);
                if (my !== leakSeq)
                    return;
                const lt = A.LeakText(r);
                leakE.replaceChildren(...(lt.Text ? [Sw.Badge(lt.Text, lt.Kind || null, lt.Title)] : []));
            };
            /**
             * Validate (messages only under the fields the user touched) and compute the hash line (newest call wins).
             * @returns {Promise<void>}
             */
            async function update() {
                const my = ++seq;
                const u = A.Trim(user.Get());
                const p = A.Trim(pwd.Get());
                const uErr = u ? null : _TF("Enter a user name.", "Message under the user name field of the password hash generator when it is empty");
                const pErr = p ? A.CheckPolicy(p, policy) : _TF("Enter a password.", "Message under the password field of the password hash generator when it is empty");
                form.SetError("user", touched.user ? uErr : null);
                form.SetError("pwd", touched.pwd && p ? pErr : null);
                //  As the old page: the leak check runs for a valid password once a user name is entered
                if (u && p && !pErr && !(A.LeakCheckIsOff && A.LeakCheckIsOff()))
                    leak(p);
                else {
                    ++leakSeq;
                    leakLast = null;
                    leakE.replaceChildren();
                }
                if (uErr || pErr) {
                    show("");
                    return;
                }
                show(_TF("Computing..", "A message shown to the user while a new password hash is computed"), true);
                if (u !== saltUser) {
                    try {
                        salt = await Sw.Net.Send(dbgApi("GetAuthSalt"), u, post);
                        saltUser = u;
                    }
                    catch (e) {
                        if (my === seq) {
                            show("");
                            Sw.Fail(_TF("Failed to get user salt.", "An error message that is displayed when the backend failed to provide a salt") + "\n" + (e.message || e));
                        }
                        return;
                    }
                }
                const hash = await A.PasswordHash(p, salt);
                if (my === seq)
                    show(u + " : " + hash);
            }
            await update();
            user.Focus();
        }
        catch (e) {
            console.error(e);
            Sw.Fail(e && e.message ? e.message : String(e));
        }
        finally {
            remover();
        }
    }

    // ------------------------------------------------------------------ api keys

    /**
     * True if API keys can be managed here (else a fail toast: not supported / not authorized).
     * @param {function(*):void} [onDenied] Called instead of the fail toast when the user's role doesn't allow it (a refused
     * request, Sw.Access.IsDenied), ex: to show the shared "not allowed" state.
     * @returns {Promise<boolean>}
     */
    async function isApiKeysSupported(onDenied) {
        try {
            if (await Sw.Net.Get(keyApi("ApiKeysSupported")))
                return true;
            Sw.Fail(_TF("Can't manage API keys for this service!", "An error message that is displayed if the back end service doesn't allow API key management"), 20000);
        }
        catch (e) {
            if ((typeof onDenied === "function") && Sw.Access && Sw.Access.IsDenied(e)) {
                onDenied(e);
                return false;
            }
            Sw.Fail(_TF("Not authorized to manage API keys!", "An error message that is displayed if the current session isn't authorized to manage API keys") + "\n" + (e.message || e), 20000);
        }
        return false;
    }

    /**
     * Ask and remove an API key.
     * @param {string} name Key name.
     * @returns {Promise<boolean>} True if removed.
     */
    async function removeApiKey(name) {
        //  The texts, icons and tool tips of the old confirmation (owner: no gap to the old behaviour). Kept from the rewrite (a
        //  protection, listed for the owner): Cancel ("No, keep it!") has the focus, so Enter or Escape never removes the key.
        //  The old text is three translatable sentences joined by line breaks (one literal with both quote kinds and \n is not
        //  translated after minification)
        //  SwWeb:allow-design (the old wording of this confirmation, owner rule: no gap to the old behaviour)
        if (!await Sw.Dialog.Confirm(
            _TF("Remove key", "The title of a confirmation dialog that is shown when a user wants to remove an API key"),
            _T("The key \"{0}\" will be removed!", name, "First line of the text of a confirmation dialog that is shown when a user wants to remove an API key. {0} is replaced with the name (identifier) of the API key") + "\n" +
            _TF("You won't be able to restore it with the same password, ever!", "Second line of the text of a confirmation dialog that is shown when a user wants to remove an API key") + "\n\n" +
            _TF("Are you sure?", "Last line of the text of a confirmation dialog that is shown when a user wants to remove an API key"),
            _TF("Yes, remove it!", "The text of a button on a confirmation dialog that when pressed will remove an API key"),
            {
                CancelText: _TF("No, keep it!", "The text of a button on a confirmation dialog that when pressed will close the dialog without any action"),
                Danger: true, OkIcon: "IconCancel", CancelIcon: "IconOk",
                OkTitle: _TF("Remove the API key forever!", "The tool tip description of a button on a confirmation dialog that when pressed will remove an API key"),
                CancelTitle: _TF("I'm not ready to take the blame for this", "The tool tip description of a button on a confirmation dialog that when pressed will close the dialog without any action"),
            }))
            return false;
        try {
            return !!(await Sw.Net.Send(keyApi("RemoveApiKey"), name, post));
        }
        catch (e) {
            Sw.Fail(e.message || String(e));
            return false;
        }
    }

    /**
     * Keep only letters, digits, '_' and '-'.
     * @param {string} v Typed text.
     * @returns {string} The valid key name.
     */
    function cleanKeyName(v) {
        let o = "";
        for (const c of v)
            if (Sw.Format.IsLetter(c) || (c >= "0" && c <= "9") || c === "_" || c === "-")
                o += c;
        return o;
    }

    /** @type {Map<string, {Close: function():void}>} Key name -> the visible "Key copied" toast (closed when the key is removed). */
    const copyToasts = new Map();

    /**
     * Copy a "name:key" to the clipboard. The announcement names the key, never the secret; a toast (msg) only after a create, closed when the key is removed.
     * @param {string} kv "name:key".
     * @param {string} [msg] A toast text (none by default: the copy button confirms in place).
     * @returns {Promise<boolean>} True when copied.
     */
    async function copyKey(kv, msg) {
        const name = kv.substring(0, kv.indexOf(":"));
        //  What: the announcement names the key, never its value (UX guide SEC-1); the copy button confirms in place (INP-31), so a
        //  row copy shows no toast; only the create dialog (closed by then) says it in a toast
        const ok = await Sw.Clipboard.Copy(kv, { Toast: false, What: _T('The API key "{0}"', name, "What was copied, in the announcement after an API key was copied (the key itself is never read out). {0} is the name of the key") });
        if (!ok) {
            Sw.Fail(_TF("Failed to copy the key to the clipboard.", "Error message when an API key could not be copied to the clipboard"), null, true);
            return false;
        }
        copyToasts.get(name)?.Close();
        if (msg)
            copyToasts.set(name, Sw.Info(msg, null, true));
        return true;
    }

    /**
     * The reason of the disabled Create key button.
     * @returns {string} The text.
     */
    const noKeyName = () => _TF("Enter the name of the service first", "Reason of the disabled Create key button of the create API key dialog while the name is empty");

    /**
     * Close the "Key copied" toast of a (removed) key.
     * @param {string} name Key name.
     */
    function closeCopyToast(name) {
        copyToasts.get(name)?.Close();
        copyToasts.delete(name);
    }

    /**
     * The "create API key" dialog: on success the new "name:key" is copied to the clipboard.
     * @returns {Promise<boolean>} True if a key was created.
     */
    async function createApiKey() {
        let created = false;
        /** @type {Sw.Button|null} The dialog's Create button (set when the dialog is open). */
        let ok = null;
        /** @type {Set<string>} The characters removed from the typed name (shown at the field, never removed silently). */
        const removed = new Set();
        const name = new U.Field(null, {
            Label: _TF("Name", "The label of a user text input field where the user should enter the name (identifier) of a new API key"),
            Placeholder: _TF("Service name", "The place holder text of a user text input field where the user should enter the name (identifier) of a new API key"),
            Hint: _TF("Enter the name of the service that will use this API key.", "The tool tip description of a user text input field where the user should enter the name (identifier) of a new API key") + "\n" + _TF("Only alpha numericals, '_' and '-' is allowed.", "Part of the tool tip description of a user text input field where the user should enter the name (identifier) of a new API key"),
            AutoComplete: "off", MaxLength: 64,
            OnInput: f => {
                const v = f.Get();
                const c = cleanKeyName(v);
                //  the message is about the last input only, it goes away with the next input that leaves nothing out
                //  (a valid name typed, the field emptied), before it stayed for the life of the dialog
                removed.clear();
                if (c !== v) {
                    for (const ch of v)
                        if (cleanKeyName(ch) !== ch)
                            removed.add(ch);
                    f.Set(c, true);
                }
                //  Say which characters were left out and which are allowed (the name that is created is the one in the field)
                if (!removed.size)
                    f.Box.SetError(null);
                else
                    f.Box.SetError(_T("Left out {0}: a name may only have letters, digits, \"_\" and \"-\".",
                        [...removed].map(ch => ch === " " ? _TF("spaces", "Name of the space character in the message about characters left out of an API key name") : "\"" + ch + "\"").join(", "),
                        "Message at the name field of the create API key dialog when typed characters were left out, {0} is the list of left out characters"));
                ok && ok.SetDisabled(!c, noKeyName());
            },
        });
        /** Create the key (the dialog's primary button and Enter in the field). @returns {Promise<false|undefined>} false keeps the dialog open. */
        const create = async () => {
            if (!cleanKeyName(name.Get()))
                return false;
            name.SetReadOnly(true);
            try {
                const res = await Sw.Net.Send(keyApi("AddApiKey"), name.Get(), post);
                if (res != null) {
                    created = true;
                    d.Close(true);
                    copyKey(String(res), _T('Key "{0}" created and copied to the clipboard.', name.Get(), "Message displayed when a new API key was created and copied to the clipboard (the key itself is never shown). {0} is the name of the key"));
                    return;
                }
                Sw.Fail(_TF("Could not create the API key.", "An error message that is displayed when the back end service failed to create a new API key"));
            }
            catch (e) {
                Sw.Fail(e.message || String(e));
            }
            name.SetReadOnly(false);
            return false;
        };
        name.Input.addEventListener("keydown", e => {
            if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                e.preventDefault();
                ok.Click();
            }
        });
        //  the one dialog anatomy (title, left aligned text, the field, a footer with Cancel and the verb on the right)
        const buttons = [
            { Text: _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: false },
            {
                Text: _TF("Create key", "The text of a button that when pressed will create a new API key"),
                Title: _TF("Create the new API key for the given service", "The tool tip description of a button that when pressed will create a new API key"),
                Icon: "IconAdd", Kind: "primary", Disabled: noKeyName(), Close: false,
                OnClick: () => create(),
            },
        ];
        const d = Sw.Dialog.Open({
            Title: _TF("Create API key", "Title of the dialog where the user enters the name of a new API key"),
            Content: el("div.auth-dialog", null,
                el("p.sw-dialog-text", { text: _TF("Enter the name of the service that will use the key. Letters, digits, \"_\" and \"-\" only.", "Text of the create API key dialog, above the name field") }),
                name.E),
            Buttons: buttons,
            Focus: name.Input,
        });
        ok = buttons[1].Button;
        await d.Result;
        return created;
    }

    /**
     * auth/KeyMan.html: manage the API keys.
     * @returns {Promise<void>}
     */
    async function keyManMain() {
        /** The title of KeyMan.html (translated by the server). */
        const docTitle = document.title;
        await U.Page("IconKey", async page => {
            //  Not allowed for this role: the shared "not allowed" state (names the user and the role), not a toast
            let denied = false;
            const notAllowed = e => {
                denied = true;
                Sw.Page.ShowNotAllowed({ Error: e });
            };
            if (!await isApiKeysSupported(notAllowed)) {
                if (!denied)
                    page.SetIcon("IconWarning");
                return;
            }
            const appName = (await Sw.Net.Get(keyApi("AppInfo"))) || ["", ""];
            const href = window.location.href;
            const baseUrl = href.substring(0, href.indexOf("/auth/"));
            const list = el("ul.auth-list.auth-keys", { attrs: { "aria-live": "polite" } });
            page.Box.append(list);
            const create = U.Button(el("div"), {
                Text: _TF("Create new key", "The text of a button that when pressed will show a dialog where the user can enter information about a new API key"),
                Title: _TF("Enter a service name and create a new API key", "The tool tip description of a button that when pressed will show a dialog where the user can enter information about a new API key"),
                Icon: "IconAdd", Kind: "primary",
                OnClick: async () => {
                    if (await createApiKey())
                        await rebuild();
                },
            });
            toolPage(page, _TF("API keys", "The title of a table containing all API keys available on a server"),
                _TF("Keys that let services and scripts call this service. A key is only shown when you ask for it.", "One line description below the title of the API key management page"), [create.E]);
            //  Opened alone, the tab keeps the page's own title ("API key management", as before; the heading says "API keys" as the
            //  old table header did). In the app shell the heading and the title are the menu item's name.
            if (!Sw.Page.IsEmbedded && docTitle)
                document.title = docTitle;
            /** @type {Map<string, HTMLElement>} "name:key" -> list item. */
            const items = new Map();
            let timer = 0;

            /**
             * A list item of a key.
             * @param {string} kv "name:key".
             * @returns {HTMLLIElement}
             */
            function item(kv) {
                const i = kv.indexOf(":");
                const name = kv.substring(0, i);
                const row = U.Buttons(null);
                const secret = kv.substring(i + 1);
                //  The key is masked until revealed (people looking at the screen, screen sharing)
                const keyLine = el("div.auth-item-line.sw-mono.auth-key.auth-key-masked", { text: "•".repeat(Math.min(24, Math.max(8, secret.length))) });
                const li = el("li.auth-item", null,
                    el("div.auth-item-name", { text: name, title: _TF("This is typically the name of the service that will use this API key", "The tool tip description of a column that contains the names (identifiers) of API keys available on a server") }),
                    keyLine,
                    row);
                const reveal = U.Button(row, {
                    Icon: "eye", Small: true, Pressable: true, Pressed: false,
                    Title: _T("Show the key of \"{0}\"", name, "The tool tip description of a toggle button that shows (or hides again) an API key that is masked by default. {0} is replaced with the name of the API-key"),
                    OnClick: b => {
                        const show = b.Pressed;
                        keyLine.textContent = show ? secret : "•".repeat(Math.min(24, Math.max(8, secret.length)));
                        keyLine.classList.toggle("auth-key-masked", !show);
                        b.SetIcon(show ? "eyeOff" : "eye");
                    },
                });
                reveal.E.setAttribute("aria-label", _T("Show the key of {0}", name, "Accessible name of a toggle button that shows an API key that is masked by default, {0} is the name of the key"));
                U.Button(row, {
                    Icon: "IconCopy", Small: true,
                    Title: _T("Copy the key of \"{0}\" to the clipboard", name, "The tool tip description of a button that when pressed will copy an API-key to the clipboard. {0} is replaced with the name of the API-key (the key itself is never shown in the tool tip)"),
                    OnClick: () => copyKey(kv),
                });
                U.Button(row, {
                    Icon: "IconDownload", Small: true,
                    Title: _TF("Download a credentials file, ready to use", "The tool tip description of a button that when pressed will download an API-key in a text file"),
                    OnClick: () => Sw.Net.DownloadText(name + "_" + appName[0] + ".txt", "# API credentials for accessing the " + appName[1] + " service at " + baseUrl + "\n\n" + kv + "\n"),
                });
                U.Button(row, {
                    Icon: "IconRemove", Small: true, Kind: "danger", Class: "auth-remove-key",
                    Title: _T("Remove the key for \"{0}\"", name, "The tool tip description of a button that when pressed will remove an API-key from a service. {0} is replaced with the name (identifier) of the API-key that will be removed"),
                    OnClick: async () => {
                        if (await removeApiKey(name)) {
                            closeCopyToast(name);
                            //  The focus goes to the same button of the next key, else the previous one, else Create new key (FOC-1)
                            const next = li.nextElementSibling || li.previousElementSibling;
                            const nextKey = next ? [...items].find(([, x]) => x === next)?.[0] : null;
                            await rebuild();
                            const target = (nextKey && items.get(nextKey)?.querySelector(".auth-remove-key")) || create.E;
                            target.focus();
                        }
                    },
                });
                return li;
            }

            /**
             * Refresh the list (unchanged items are kept), then again in 30 s.
             * @returns {Promise<void>}
             */
            async function rebuild() {
                clearTimeout(timer);
                try {
                    const keys = (await Sw.Net.Get(keyApi("GetApiKeys"))) ?? [];
                    //  Allowed again after a refusal (the role came back): the page starts over (the not allowed state goes)
                    if (denied) {
                        window.location.reload();
                        return;
                    }
                    const want = new Set(keys);
                    for (const [k, li] of items)
                        if (!want.has(k)) {
                            li.remove();
                            items.delete(k);
                        }
                    let prev = null;
                    for (const k of keys) {
                        let li = items.get(k);
                        if (!li) {
                            li = item(k);
                            items.set(k, li);
                        }
                        if (prev ? prev.nextSibling !== li : list.firstChild !== li)
                            prev ? prev.after(li) : list.prepend(li);
                        prev = li;
                    }
                    list.querySelector(".auth-empty")?.remove();
                    if (keys.length === 0)
                        list.append(el("li.auth-empty", null, Sw.StateView({
                            Icon: "key",
                            Title: _TF("No API keys", "Text shown when there are no API keys"),
                            Text: _TF("Create a key for each service or script that calls this service.", "Text below \"No API keys\" on the API key management page"),
                        }).E));
                }
                catch (e) {
                    //  The role was taken away meanwhile (or only listing is refused): the shared state (once, not a toast every 30 s);
                    //  the refresh goes on every 30 s as before (the old page kept polling)
                    if (Sw.Access && Sw.Access.IsDenied(e)) {
                        if (!denied)
                            notAllowed(e);
                    }
                    else
                        Sw.Fail(e.message || String(e));
                }
                timer = setTimeout(rebuild, 30000);
            }
            await rebuild();
        }, { KeepUserChanges: true, Class: "auth-wide" });
    }

    /** @namespace Sw.SimpleAuth - the SimpleAuthorizer pages. */
    Sw.SimpleAuth = { pwdGenMain, keyManMain, isApiKeysSupported, removeApiKey, createApiKey };
    window.pwdGenMain = pwdGenMain;
    window.keyManMain = keyManMain;
})();