/**
 * @file SimpleAuth.js - the SimpleAuthorizer pages of SysWeaver.Auth.Core (served as auth/SimpleAuth.js).
 *
 * Entry points (globals, body onload; also on `Sw.SimpleAuth`):
 *  - pwdGenMain (auth/index.html, menu Admin/GenPwd; a Sw.PageLayout form page with Sw.Card + Sw.TextBox + Sw.Form):
 *    enter a user name and a password (generator, the
 *    server's fixed policy, leak check) and get the configuration line "user : $s1$iterations$salt$storedKey" (a SCRAM verifier of
 *    b64(sha256(password|salt)), see ScramVerifier in Scram.cs, PBKDF2 parameters from Api/debug/simpleAuth/GetVerifierParams) with the
 *    per user salt of Api/debug/simpleAuth/GetAuthSalt; copy button.
 *  - keyManMain (auth/KeyMan.html, menu Admin/KeyMan): the API keys ("name:key") with their tokens and allowed addresses, masked by
 *    default with a per key "Show the key" toggle: copy, download a credentials file "<name>_<AppName>.txt", edit the tokens
 *    (SetApiKeyTokens: at least one, each "service" or "service_<scope>"), edit the allowed client addresses (SetApiKeyAddresses,
 *    empty = any address), remove (confirmed), create (dialog: name of letters, digits, '_', '-' and the tokens, prefilled with the
 *    default of ApiKeysSupported; the new key is copied to the clipboard). The copy announcement and tool tips never show the
 *    secret ('The API key "name"'; only a create shows a toast), and that toast is closed when the key is removed.
 *    The list refreshes every 30 s (unchanged items are kept). This page reloads on user changes (it doesn't call IgnoreUserChanges).
 *  - userManMain (auth/UserMan.html, menu Admin/UserMan): the stored users (GetStoredUsers: name and tokens), add (dialog: the user
 *    line "username : $s1$..." of the Generate password hash page and the tokens), edit the tokens (SetStoredUserTokens) and remove
 *    (confirmed). A row the caller may not change (CanChange false: the user has tokens the caller doesn't have) has its buttons disabled with the
 *    reason. The list refreshes every 30 s (unchanged items are kept).
 *
 * Loading: classic `defer` scripts ../sw/core.js, ../sw/ui.js, Auth.js, AuthUi.js (MicroService.Auth, same auth/
 * folder at runtime), SimpleAuth.js. Styles: Auth.css, SimpleAuth.css.
 * Server: Api/debug/simpleAuth/{GetAuthSalt, GetVerifierParams, GetPasswordPolicy}, Api/simpleAuth/{ApiKeysSupported, AppInfo, GetApiKeys,
 * AddApiKey, RemoveApiKey, SetApiKeyAddresses, SetApiKeyTokens, StoredUsersSupported, GetStoredUsers, AddStoredUser, RemoveStoredUser,
 * SetStoredUserTokens}.
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
        //  The shared form page layout of AuthUi (MicroService.Auth, AuthPage.SetHeader); the local version below when AuthUi.js has no SetHeader
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
            //  GetVerifierParams: {Iterations, Binding}
            let vparams = null;
            let seq = 0;
            let leakSeq = 0;
            /** @type {string|null} The password whose leak check is running or shown (no new request for it). */
            let leakLast = null;
            /**
             * The leak check of a valid password as a badge next to the generate button, at once on every change of the password (the
             * the same password as the shown or running check makes no new request; newest wins).
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
                //  The leak check runs for a valid password once a user name is entered
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
                            Sw.Fail(_TF("Failed to get user salt.", "An error message that is displayed when the backend failed to provide a salt") + "\n" + Sw.ErrorMessage(e));
                        }
                        return;
                    }
                }
                if (!vparams) {
                    try {
                        vparams = await Sw.Net.Send(dbgApi("GetVerifierParams"), null, post);
                    }
                    catch (e) {
                        if (my === seq) {
                            show("");
                            Sw.Fail(_TF("Failed to get the password hashing parameters.", "An error message that is displayed when the backend failed to provide the password hashing parameters") + "\n" + Sw.ErrorMessage(e));
                        }
                        return;
                    }
                }
                const hash = await A.PasswordHash(p, salt);
                const line = await verifier(hash, vparams);
                if (my === seq)
                    show(u + " : " + line);
            }

            /**
             * The SCRAM verifier of a legacy hash: "$s1$" + iterations + "$" + b64(salt) + "$" + b64(SHA256(HMAC(PBKDF2(hash|binding), "Client Key"))),
             * with a random 16 byte salt (see ScramVerifier in Scram.cs). The verifier can't be used to sign in.
             * @param {string} hash The legacy hash (b64(sha256(password|salt))).
             * @param {{Iterations:number, Binding:string}} vp The parameters of GetVerifierParams.
             * @returns {Promise<string>} The verifier.
             */
            async function verifier(hash, vp) {
                const s = new Uint8Array(16);
                crypto.getRandomValues(s);
                const it = vp.Iterations | 0;
                const salted = await A.Pbkdf2(Sw.Util.Utf8Encode(hash + "|" + (vp.Binding || "")), s, it);
                const storedKey = Sw.Util.Sha256Sync(A.Hmac256(salted, Sw.Util.Utf8Encode("Client Key")));
                return "$s1$" + it + "$" + Sw.Util.Base64Encode(s) + "$" + Sw.Util.Base64Encode(storedKey);
            }
            await update();
            user.Focus();
        }
        catch (e) {
            console.error(e);
            Sw.Fail(Sw.ErrorMessage(e));
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
     * @returns {Promise<string|false>} The tokens of a new key when none are given (ex: "service", truthy), false if API keys can't be managed.
     */
    async function isApiKeysSupported(onDenied) {
        try {
            const def = await Sw.Net.Get(keyApi("ApiKeysSupported"));
            if (def)
                return String(def);
            Sw.Fail(_TF("Can't manage API keys for this service!", "An error message that is displayed if the back end service doesn't allow API key management"), 20000);
        }
        catch (e) {
            if ((typeof onDenied === "function") && Sw.Access && Sw.Access.IsDenied(e)) {
                onDenied(e);
                return false;
            }
            Sw.Fail(_TF("Not authorized to manage API keys!", "An error message that is displayed if the current session isn't authorized to manage API keys") + "\n" + Sw.ErrorMessage(e), 20000);
        }
        return false;
    }

    /**
     * Ask and remove an API key.
     * @param {string} name Key name.
     * @returns {Promise<boolean>} True if removed.
     */
    async function removeApiKey(name) {
        //  Cancel ("No, keep it!") has the focus, so Enter or Escape never removes the key.
        //  The text is three translatable sentences joined by line breaks (one literal with both quote kinds and \n is not
        //  translated after minification)
        //  SwWeb:allow-design (the wording of this confirmation is the owner's choice)
        if (!await Sw.Dialog.Confirm(
            _TF("Remove key", "The title of a confirmation dialog that is shown when a user wants to remove an API key"),
            _T("The key \"{0}\" will be removed!", name, "First line of the text of a confirmation dialog that is shown when a user wants to remove an API key. {0} is replaced with the name (identifier) of the API key") + "\n" +
            _TF("Services and scripts that use it stop working at once, and a removed key can't be restored.", "Second line of the text of a confirmation dialog that is shown when a user wants to remove an API key: the consequence (a key is not a password)") + "\n\n" +
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

    /** The maximum length of a token (SimpleAuthorizer.MaxApiKeyTokenLength). */
    const maxTokenLength = 64;

    /**
     * Normalize a comma separated token list as the server does: trimmed, lower case, no empty entries or duplicates, sorted.
     * @param {string} v The typed list.
     * @returns {string[]} The tokens.
     */
    function normTokens(v) {
        const t = [...new Set(String(v || "").split(",").map(x => x.trim().toLowerCase()).filter(x => x))];
        t.sort();
        return t;
    }

    /**
     * A token list for display: "a, b", or "none" for an empty list.
     * @param {string} v The stored list ("a,b").
     * @returns {string} The text.
     */
    const tokensText = v => normTokens(v).join(", ") || _TF("none", "Shown instead of the tokens of a stored user that has no tokens (any signed in user pages only)");

    /**
     * The problem with the tokens of an API key (SimpleAuthorizer.NormalizeApiKeyTokens): at least one, each "service" or "service_<scope>"
     * (scope: letters, digits, '_', '-', '.'), at most 64 chars.
     * @param {string} v The typed list.
     * @returns {string|null} The reason, null if valid.
     */
    function apiKeyTokensError(v) {
        const t = normTokens(v);
        if (!t.length)
            return _TF("An API key needs at least one token: \"service\" or \"service_\" followed by a scope.", "Message at the tokens field of an API key when it is empty (service and service_ are literal token names, not translated)");
        for (const x of t)
            if ((x.length > maxTokenLength) || !/^service(_[a-z0-9_.-]+)?$/.test(x))
                return _T("\"{0}\" isn't allowed: a token is \"service\", or \"service_\" followed by letters, digits, \"_\", \"-\" or \".\".", x, "Message at the tokens field of an API key when a token is invalid, {0} is the invalid token (service and service_ are literal token names, not translated)");
        return null;
    }

    /**
     * The problem with the tokens of a stored user (SimpleAuthorizer.NormalizeUserTokens): no ':', '|' or spaces in a token, at most 64 chars.
     * @param {string} v The typed list.
     * @returns {string|null} The reason, null if valid.
     */
    function userTokensError(v) {
        for (const x of normTokens(v))
            if ((x.length > maxTokenLength) || /[:|\s]/.test(x))
                return _T("\"{0}\" isn't allowed: a token can't have spaces, \":\" or \"|\".", x, "Message at the tokens field of a stored user when a token is invalid, {0} is the invalid token");
        return null;
    }

    /**
     * A tokens field (comma separated) that shows the problem of the typed list at the field.
     * @param {object} o { Label, Placeholder, Hint, Value, Check (list => reason or null), OnState (reason or null) }.
     * @returns {object} The field (U.Field).
     */
    function tokensField(o) {
        const f = new U.Field(null, {
            Label: o.Label || _TF("Tokens", "The label of a text field with the comma separated tokens (roles) of an API key or a user"),
            Placeholder: o.Placeholder,
            Hint: o.Hint,
            AutoComplete: "off", MaxLength: 2000,
            OnInput: x => {
                const err = o.Check(x.Get());
                x.Box.SetError(err);
                o.OnState && o.OnState(err);
            },
        });
        f.Set(o.Value || "", true);
        return f;
    }

    /**
     * A dialog that edits a token list and saves it (the server's refusal is shown at the field).
     * @param {object} o { Title, Text, Label, Hint, Value (current "a,b"), Check (list => reason or null), Save (async tokens => void) }.
     * @returns {Promise<boolean>} True if saved.
     */
    async function editTokens(o) {
        let changed = false;
        /** @type {Sw.Button|null} */
        let ok = null;
        const field = tokensField({
            Label: o.Label, Hint: o.Hint, Value: normTokens(o.Value).join(", "), Check: o.Check,
            OnState: err => ok && ok.SetDisabled(!!err, err),
        });
        const save = async () => {
            if (o.Check(field.Get()))
                return false;
            field.SetReadOnly(true);
            try {
                await o.Save(normTokens(field.Get()).join(","));
                changed = true;
                d.Close(true);
                return;
            }
            catch (e) {
                field.Box.SetError(e.message || String(e));
            }
            field.SetReadOnly(false);
            return false;
        };
        field.Input.addEventListener("keydown", e => {
            if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                e.preventDefault();
                ok.Click();
            }
        });
        const buttons = [
            { Text: _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: false },
            {
                Text: _TF("Save", "The text of a button that saves the tokens of an API key or a user"),
                Icon: "IconOk", Kind: "primary", Close: false,
                OnClick: () => save(),
            },
        ];
        const d = Sw.Dialog.Open({
            Title: o.Title,
            Content: el("div.auth-dialog", null, el("p.sw-dialog-text", { text: o.Text }), field.E),
            Buttons: buttons,
            Focus: field.Input,
        });
        ok = buttons[1].Button;
        const err = o.Check(field.Get());
        if (err)
            ok.SetDisabled(true, err);
        await d.Result;
        return changed;
    }

    /**
     * The tokens dialog of an API key (SetApiKeyTokens).
     * @param {string} keyName The name of the key.
     * @param {string} current The current tokens ("service,service_x").
     * @returns {Promise<boolean>} True if changed.
     */
    const editApiKeyTokens = (keyName, current) => editTokens({
        Title: _T("Tokens of \"{0}\"", keyName, "Title of the dialog where the tokens of an API key are edited, {0} is the name of the key"),
        Text: _TF("The key only reaches what requires one of its tokens. \"service\" reaches the service APIs, \"service_<scope>\" only the APIs of that scope.", "Text of the tokens dialog of an API key (service and service_<scope> are literal token names, not translated)"),
        Hint: _TF("Comma separated, ex: service, service_translate", "The tool tip description of the tokens field of an API key (the example tokens are literal, not translated)"),
        Value: current,
        Check: apiKeyTokensError,
        Save: t => Sw.Net.Send(keyApi("SetApiKeyTokens"), { Name: keyName, Tokens: t }, post),
    });

    /**
     * The "allowed addresses" dialog of an API key (SetApiKeyAddresses): a comma separated list of addresses / networks, empty = any address.
     * @param {string} keyName The name of the key.
     * @param {string} current The current list ("" = any address).
     * @returns {Promise<boolean>} True if changed.
     */
    async function editApiKeyAddresses(keyName, current) {
        let changed = false;
        const addr = new U.Field(null, {
            Label: _TF("Allowed addresses", "The label of a text field with the client addresses (IP addresses or networks) that may use an API key"),
            Placeholder: _TF("Any address", "The place holder text of the allowed addresses field of an API key (empty means any address)"),
            Hint: _TF("Comma separated IP addresses or networks, ex: 10.0.0.5, 192.168.1.0/24. Empty allows any address.", "The tool tip description of the allowed addresses field of an API key"),
            AutoComplete: "off", MaxLength: 2000,
        });
        addr.Set(current || "", true);
        const save = async () => {
            addr.SetReadOnly(true);
            try {
                await Sw.Net.Send(keyApi("SetApiKeyAddresses"), { Name: keyName, Addresses: addr.Get() || "" }, post);
                changed = true;
                d.Close(true);
                return;
            }
            catch (e) {
                addr.Box.SetError(e.message || String(e));
            }
            addr.SetReadOnly(false);
            return false;
        };
        const buttons = [
            { Text: _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: false },
            {
                Text: _TF("Save", "The text of a button that saves the allowed addresses of an API key"),
                Icon: "IconOk", Kind: "primary", Close: false,
                OnClick: () => save(),
            },
        ];
        const d = Sw.Dialog.Open({
            Title: _T("Allowed addresses of \"{0}\"", keyName, "Title of the dialog where the allowed client addresses of an API key are edited, {0} is the name of the key"),
            Content: el("div.auth-dialog", null,
                el("p.sw-dialog-text", { text: _TF("The key only works from these addresses (also when the session it signed in is used).", "Text of the allowed addresses dialog of an API key") }),
                addr.E),
            Buttons: buttons,
            Focus: addr.Input,
        });
        await d.Result;
        return changed;
    }

    /**
     * The "create API key" dialog (a name and the tokens): on success the new "name:key" is copied to the clipboard.
     * @param {string} [defaultTokens] The tokens the field starts with (the server's default for a new key, from ApiKeysSupported).
     * @returns {Promise<boolean>} True if a key was created.
     */
    async function createApiKey(defaultTokens) {
        let created = false;
        /** @type {string|null} The problem with the typed tokens. */
        let tokensErr = null;
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
                updateOk();
            },
        });
        const tokens = tokensField({
            Value: normTokens(defaultTokens || "service").join(", "),
            Hint: _TF("Comma separated, ex: service, service_translate", "The tool tip description of the tokens field of an API key (the example tokens are literal, not translated)"),
            Check: apiKeyTokensError,
            OnState: err => {
                tokensErr = err;
                updateOk();
            },
        });
        /** Enable the Create button when the name and the tokens are valid (else disabled with the reason). */
        function updateOk() {
            const c = cleanKeyName(name.Get());
            ok && ok.SetDisabled(!c || !!tokensErr, !c ? noKeyName() : tokensErr);
        }
        /** Create the key (the dialog's primary button and Enter in the field). @returns {Promise<false|undefined>} false keeps the dialog open. */
        const create = async () => {
            if (!cleanKeyName(name.Get()) || apiKeyTokensError(tokens.Get()))
                return false;
            name.SetReadOnly(true);
            tokens.SetReadOnly(true);
            try {
                const res = await Sw.Net.Send(keyApi("AddApiKey"), { Name: name.Get(), Tokens: normTokens(tokens.Get()).join(",") }, post);
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
            tokens.SetReadOnly(false);
            return false;
        };
        for (const f of [name, tokens])
            f.Input.addEventListener("keydown", e => {
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
                name.E,
                el("p.sw-dialog-text", { text: _TF("The key only reaches what requires one of its tokens: \"service\" for the service APIs, \"service_<scope>\" only for the APIs of that scope.", "Text of the create API key dialog, above the tokens field (service and service_<scope> are literal token names, not translated)") }),
                tokens.E),
            Buttons: buttons,
            Focus: name.Input,
        });
        ok = buttons[1].Button;
        tokensErr = apiKeyTokensError(tokens.Get());
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
            const defaultTokens = await isApiKeysSupported(notAllowed);
            if (!defaultTokens) {
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
                    if (await createApiKey(defaultTokens))
                        await rebuild();
                },
            });
            toolPage(page, _TF("API keys", "The title of a table containing all API keys available on a server"),
                _TF("Keys that let services and scripts call this service. Anyone who has a key can call it, so keep keys as safe as passwords.", "One line description below the title of the API key management page"), [create.E]);
            //  Opened alone, the tab keeps the page's own title ("API key management"; the heading says "API keys"). In the app shell the heading and the title are the menu item's name.
            if (!Sw.Page.IsEmbedded && docTitle)
                document.title = docTitle;
            /** @type {Map<string, HTMLElement>} "name:key\ntokens\naddresses" -> list item. */
            const items = new Map();
            let timer = 0;

            /**
             * A list item of a key.
             * @param {{Name:string, Key:string, Tokens:string, Addresses:string}} k The key (GetApiKeys): Key is "name:key", Addresses "" = any address.
             * @returns {HTMLLIElement}
             */
            function item(k) {
                const kv = String(k.Key || "");
                const i = kv.indexOf(":");
                const name = kv.substring(0, i);
                const addresses = k.Addresses || "";
                const row = U.Buttons(null);
                const secret = kv.substring(i + 1);
                const tokenLine = el("div.auth-item-line.auth-key-tokens", {
                    text: _TF("Tokens", "Label before the tokens of an API key or a user in a list") + ": " + tokensText(k.Tokens),
                });
                const addrLine = el("div.auth-item-line.auth-key-addresses", {
                    text: _TF("Allowed from", "Label before the allowed client addresses of an API key") + ": " +
                        (addresses || _TF("any address", "Shown when an API key may be used from any client address")),
                });
                //  The key is masked until revealed (people looking at the screen, screen sharing)
                //  (the dots are hidden from screen readers, which hear "Key hidden" instead of two dozen bullets)
                const masked = () => [
                    el("span", { text: "•".repeat(Math.min(24, Math.max(8, secret.length))), attrs: { "aria-hidden": "true" } }),
                    el("span.sw-sr-only", { text: _TF("Key hidden", "Screen reader text of a masked API key (shown as dots until Show the key is pressed)") })];
                const keyLine = el("div.auth-item-line.sw-mono.auth-key.auth-key-masked", null, ...masked());
                const li = el("li.auth-item", null,
                    el("div.auth-item-name", { text: name, title: _TF("This is typically the name of the service that will use this API key", "The tool tip description of a column that contains the names (identifiers) of API keys available on a server") }),
                    keyLine,
                    tokenLine,
                    addrLine,
                    row);
                const reveal = U.Button(row, {
                    Icon: "eye", Small: true, Pressable: true, Pressed: false,
                    Title: _T("Show the key of \"{0}\"", name, "The tool tip description of a toggle button that shows (or hides again) an API key that is masked by default. {0} is replaced with the name of the API-key"),
                    OnClick: b => {
                        const show = b.Pressed;
                        keyLine.textContent = show ? secret : "•".repeat(8);
                        if (!show)
                            keyLine.replaceChildren(...masked());
                        keyLine.classList.toggle("auth-key-masked", !show);
                        b.SetIcon(show ? "eyeOff" : "eye");
                    },
                });
                reveal.E.setAttribute("aria-label", _T("Show the key of \"{0}\"", name, "Accessible name of a toggle button that shows an API key that is masked by default, {0} is the name of the key"));
                U.Button(row, {
                    Icon: "IconCopy", Small: true,
                    Title: _T("Copy the key of \"{0}\" to the clipboard", name, "The tool tip description of a button that when pressed will copy an API-key to the clipboard. {0} is replaced with the name of the API-key (the key itself is never shown in the tool tip)"),
                    OnClick: () => copyKey(kv),
                });
                const download = U.Button(row, {
                    Icon: "IconDownload", Small: true,
                    Title: _TF("Download a credentials file, ready to use (it holds the key as plain text: keep the file as safe as a password)", "The tool tip description of a button that when pressed will download an API-key in a text file"),
                    OnClick: () => Sw.Net.DownloadText(name + "_" + appName[0] + ".txt", "# API credentials for accessing the " + appName[1] + " service at " + baseUrl + "\n\n" + kv + "\n"),
                });
                //  (the name says which key, as Show / Copy / Remove; the plain text warning stays in the tool tip)
                download.E.setAttribute("aria-label", _T("Download the credentials file of \"{0}\"", name, "Accessible name of the button that downloads the credentials file of an API key. {0} is the name of the key"));
                U.Button(row, {
                    Icon: "IconLock", Small: true,
                    Title: _T("Change the tokens of \"{0}\"", name, "The tool tip description of a button that edits the tokens (what the key may reach) of an API key. {0} is the name of the key"),
                    OnClick: async () => {
                        if (await editApiKeyTokens(name, k.Tokens))
                            await rebuild();
                    },
                });
                U.Button(row, {
                    Icon: "IconEdit", Small: true,
                    Title: _T("Change the allowed addresses of \"{0}\"", name, "The tool tip description of a button that edits the client addresses an API key may be used from. {0} is the name of the key"),
                    OnClick: async () => {
                        if (await editApiKeyAddresses(name, addresses))
                            await rebuild();
                    },
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
                    /** @type {Array<{Name:string, Key:string, Tokens:string, Addresses:string}>} */
                    const keys = (await Sw.Net.Get(keyApi("GetApiKeys"))) ?? [];
                    //  Allowed again after a refusal (the role came back): the page starts over (the not allowed state goes)
                    if (denied) {
                        window.location.reload();
                        return;
                    }
                    //  An item is rebuilt when its key, its tokens or its addresses changed (the map key holds all of them)
                    const idOf = k => [k.Key, k.Tokens || "", k.Addresses || ""].join("\n");
                    const want = new Set(keys.map(idOf));
                    for (const [k, li] of items)
                        if (!want.has(k)) {
                            li.remove();
                            items.delete(k);
                        }
                    let prev = null;
                    for (const k of keys) {
                        const id = idOf(k);
                        let li = items.get(id);
                        if (!li) {
                            li = item(k);
                            items.set(id, li);
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
                    //  the refresh goes on every 30 s
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

    // ------------------------------------------------------------------ stored users

    /**
     * True if the stored users can be managed here (else a fail toast: not supported / not authorized).
     * @param {function(*):void} [onDenied] Called instead of the fail toast when the user's role doesn't allow it (a refused request, Sw.Access.IsDenied).
     * @returns {Promise<boolean>}
     */
    async function isStoredUsersSupported(onDenied) {
        try {
            if (await Sw.Net.Get(keyApi("StoredUsersSupported")))
                return true;
            Sw.Fail(_TF("Can't manage users for this service!", "An error message that is displayed if the back end service doesn't allow adding and removing users"), 20000);
        }
        catch (e) {
            if ((typeof onDenied === "function") && Sw.Access && Sw.Access.IsDenied(e)) {
                onDenied(e);
                return false;
            }
            Sw.Fail(_TF("Not authorized to manage users!", "An error message that is displayed if the current session isn't authorized to add and remove users") + "\n" + Sw.ErrorMessage(e), 20000);
        }
        return false;
    }

    /**
     * Ask and remove a stored user.
     * @param {string} name User name.
     * @returns {Promise<boolean>} True if removed.
     */
    async function removeStoredUser(name) {
        //  Cancel has the focus, so Enter or Escape never removes the user
        if (!await Sw.Dialog.Confirm(
            _TF("Remove user", "The title of a confirmation dialog that is shown when an administrator wants to remove a stored user"),
            _T("The user \"{0}\" will be removed!", name, "First line of the text of a confirmation dialog that is shown when an administrator wants to remove a stored user. {0} is the user name") + "\n" +
            _TF("The user is signed out at once, and a removed user can't be restored (add the user again with a new password line).", "Second line of the text of a confirmation dialog that is shown when an administrator wants to remove a stored user: the consequence") + "\n\n" +
            _TF("Are you sure?", "Last line of the text of a confirmation dialog that is shown when an administrator wants to remove a stored user"),
            _TF("Yes, remove the user", "The text of a button on a confirmation dialog that when pressed will remove a stored user"),
            {
                CancelText: _TF("No, keep the user", "The text of a button on a confirmation dialog that when pressed will close the dialog without removing the user"),
                Danger: true, OkIcon: "IconCancel", CancelIcon: "IconOk",
            }))
            return false;
        try {
            return !!(await Sw.Net.Send(keyApi("RemoveStoredUser"), name, post));
        }
        catch (e) {
            Sw.Fail(e.message || String(e));
            return false;
        }
    }

    /**
     * The problem with a user line ("username : $s1$iterations$salt$storedKey", from the Generate password hash page).
     * @param {string} v The typed line.
     * @returns {string|null} The reason, null if it looks valid (the server checks it).
     */
    function userLineError(v) {
        v = String(v || "").trim();
        const i = v.indexOf(":");
        const name = i > 0 ? v.substring(0, i).trim() : "";
        const verifier = i > 0 ? v.substring(i + 1).trim() : "";
        if (!name || name.includes("|") || name.startsWith("#") || name.startsWith("//") || !/^\$s1\$\d+\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+$/.test(verifier))
            return _TF("Paste the line of the Generate password hash page: \"username : $s1$...\".", "Message at the user line field of the add user dialog when the text isn't a user name followed by a password verifier ($s1$... is literal, not translated)");
        return null;
    }

    /**
     * The "add user" dialog: the user line of the Generate password hash page and the tokens (AddStoredUser).
     * @returns {Promise<boolean>} True if a user was added.
     */
    async function addStoredUser() {
        let added = false;
        /** @type {Sw.Button|null} */
        let ok = null;
        let tokensErr = null;
        const update = () => {
            const le = userLineError(line.Get());
            ok && ok.SetDisabled(!!le || !!tokensErr, le || tokensErr);
        };
        const line = new U.Field(null, {
            Label: _TF("User line", "The label of a text field where an administrator pastes the line \"username : password verifier\" of the Generate password hash page"),
            Placeholder: _TF("username : $s1$...", "The place holder text of the user line field of the add user dialog ($s1$ is literal, not translated)"),
            Hint: _TF("Make the line on the Generate password hash page: it holds the user name and a verifier of the password, never the password.", "The tool tip description of the user line field of the add user dialog"),
            AutoComplete: "off", MaxLength: 1000, Copy: false,
            OnInput: f => {
                const v = f.Get();
                f.Box.SetError(v ? userLineError(v) : null);
                update();
            },
        });
        const tokens = tokensField({
            Hint: _TF("Comma separated, ex: admin, ops. Empty: pages for any signed in user only.", "The tool tip description of the tokens field of a stored user (the example tokens are literal, not translated)"),
            Check: userTokensError,
            OnState: err => {
                tokensErr = err;
                update();
            },
        });
        const add = async () => {
            if (userLineError(line.Get()) || userTokensError(tokens.Get()))
                return false;
            line.SetReadOnly(true);
            tokens.SetReadOnly(true);
            try {
                await Sw.Net.Send(keyApi("AddStoredUser"), { Auth: String(line.Get()).trim(), Tokens: normTokens(tokens.Get()).join(",") }, post);
                added = true;
                d.Close(true);
                return;
            }
            catch (e) {
                Sw.Fail(e.message || String(e));
            }
            line.SetReadOnly(false);
            tokens.SetReadOnly(false);
            return false;
        };
        for (const f of [line, tokens])
            f.Input.addEventListener("keydown", e => {
                if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                    e.preventDefault();
                    ok.Click();
                }
            });
        const genLink = el("a.sw-link", { text: _TF("Generate password hash", "Link from the add user dialog to the page that makes the user line (the page title)"), attrs: { href: "index.html", target: "_blank", rel: "noopener" } });
        const buttons = [
            { Text: _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: false },
            {
                Text: _TF("Add user", "The text of a button that adds a stored user"),
                Icon: "IconAdd", Kind: "primary", Close: false,
                Disabled: userLineError(""),
                OnClick: () => add(),
            },
        ];
        const d = Sw.Dialog.Open({
            Title: _TF("Add user", "Title of the dialog where an administrator adds a stored user"),
            Content: el("div.auth-dialog", null,
                el("p.sw-dialog-text", null, _TF("Paste the line made on the page", "Start of the text of the add user dialog, followed by a link to the Generate password hash page") + " ", genLink,
                    ". " + _TF("The password itself is never sent here.", "End of the text of the add user dialog (after the link)")),
                line.E,
                el("p.sw-dialog-text", { text: _TF("The tokens decide what the user may do. You can only give tokens that you have yourself.", "Text of the add user dialog, above the tokens field") }),
                tokens.E),
            Buttons: buttons,
            Focus: line.Input,
        });
        ok = buttons[1].Button;
        await d.Result;
        return added;
    }

    /**
     * The tokens dialog of a stored user (SetStoredUserTokens).
     * @param {string} name The user name.
     * @param {string} current The current tokens.
     * @returns {Promise<boolean>} True if changed.
     */
    const editStoredUserTokens = (name, current) => editTokens({
        Title: _T("Tokens of \"{0}\"", name, "Title of the dialog where the tokens of a stored user are edited, {0} is the user name"),
        Text: _TF("The tokens decide what the user may do, the user is signed out when they change. You can only give tokens that you have yourself.", "Text of the tokens dialog of a stored user"),
        Hint: _TF("Comma separated, ex: admin, ops. Empty: pages for any signed in user only.", "The tool tip description of the tokens field of a stored user (the example tokens are literal, not translated)"),
        Value: current,
        Check: userTokensError,
        Save: t => Sw.Net.Send(keyApi("SetStoredUserTokens"), { Name: name, Tokens: t }, post),
    });

    /**
     * auth/UserMan.html: manage the stored users.
     * @returns {Promise<void>}
     */
    async function userManMain() {
        /** The title of UserMan.html (translated by the server). */
        const docTitle = document.title;
        await U.Page("IconUsers", async page => {
            let denied = false;
            const notAllowed = e => {
                denied = true;
                Sw.Page.ShowNotAllowed({ Error: e });
            };
            if (!await isStoredUsersSupported(notAllowed)) {
                if (!denied)
                    page.SetIcon("IconWarning");
                return;
            }
            const list = el("ul.auth-list.auth-users", { attrs: { "aria-live": "polite" } });
            page.Box.append(list);
            const add = U.Button(el("div"), {
                Text: _TF("Add user", "The text of a button that when pressed will show a dialog where an administrator adds a stored user"),
                Title: _TF("Add a user with a line from the Generate password hash page", "The tool tip description of the button that opens the add user dialog"),
                Icon: "IconAdd", Kind: "primary",
                OnClick: async () => {
                    if (await addStoredUser())
                        await rebuild();
                },
            });
            toolPage(page, _TF("Stored users", "The title of the page where administrators add and remove the stored users of a server"),
                _TF("Users added here. The users of the configuration and the user file are not listed and can't be changed here.", "One line description below the title of the stored users page"), [add.E]);
            if (!Sw.Page.IsEmbedded && docTitle)
                document.title = docTitle;
            /** @type {Map<string, HTMLElement>} "name\ntokens\ncanChange" -> list item. */
            const items = new Map();
            let timer = 0;
            const debugOnly = () => _TF("This user has tokens that you don't have, so you can't change or remove it", "Reason of the disabled edit and remove buttons of a stored user that has tokens the signed in user doesn't have");

            /**
             * A list item of a user.
             * @param {{Name:string, Tokens:string, CanChange:boolean}} u The user (GetStoredUsers).
             * @returns {HTMLLIElement}
             */
            function item(u) {
                const name = String(u.Name || "");
                const row = U.Buttons(null);
                const li = el("li.auth-item", null,
                    el("div.auth-item-name", { text: name }),
                    el("div.auth-item-line.auth-user-tokens", { text: _TF("Tokens", "Label before the tokens of an API key or a user in a list") + ": " + tokensText(u.Tokens) }),
                    row);
                const edit = U.Button(row, {
                    Icon: "IconLock", Small: true,
                    Title: _T("Change the tokens of \"{0}\"", name, "The tool tip description of a button that edits the tokens of a stored user. {0} is the user name"),
                    OnClick: async () => {
                        if (await editStoredUserTokens(name, u.Tokens))
                            await rebuild();
                    },
                });
                const remove = U.Button(row, {
                    Icon: "IconRemove", Small: true, Kind: "danger", Class: "auth-remove-user",
                    Title: _T("Remove the user \"{0}\"", name, "The tool tip description of a button that removes a stored user. {0} is the user name"),
                    OnClick: async () => {
                        if (await removeStoredUser(name)) {
                            //  The focus goes to the same button of the next user, else the previous one, else Add user
                            const next = li.nextElementSibling || li.previousElementSibling;
                            const nextId = next ? [...items].find(([, x]) => x === next)?.[0] : null;
                            await rebuild();
                            const target = (nextId && items.get(nextId)?.querySelector(".auth-remove-user")) || add.E;
                            target.focus();
                        }
                    },
                });
                if (!u.CanChange) {
                    edit.SetDisabled(true, debugOnly());
                    remove.SetDisabled(true, debugOnly());
                }
                return li;
            }

            /**
             * Refresh the list (unchanged items are kept), then again in 30 s.
             * @returns {Promise<void>}
             */
            async function rebuild() {
                clearTimeout(timer);
                try {
                    /** @type {Array<{Name:string, Tokens:string, CanChange:boolean}>} */
                    const users = (await Sw.Net.Get(keyApi("GetStoredUsers"))) ?? [];
                    if (denied) {
                        window.location.reload();
                        return;
                    }
                    const idOf = u => [u.Name, u.Tokens || "", u.CanChange ? "1" : "0"].join("\n");
                    const want = new Set(users.map(idOf));
                    for (const [k, li] of items)
                        if (!want.has(k)) {
                            li.remove();
                            items.delete(k);
                        }
                    let prev = null;
                    for (const u of users) {
                        const id = idOf(u);
                        let li = items.get(id);
                        if (!li) {
                            li = item(u);
                            items.set(id, li);
                        }
                        if (prev ? prev.nextSibling !== li : list.firstChild !== li)
                            prev ? prev.after(li) : list.prepend(li);
                        prev = li;
                    }
                    list.querySelector(".auth-empty")?.remove();
                    if (users.length === 0)
                        list.append(el("li.auth-empty", null, Sw.StateView({
                            Icon: "user",
                            Title: _TF("No stored users", "Text shown when there are no stored users"),
                            Text: _TF("Add a user with a line from the Generate password hash page.", "Text below \"No stored users\" on the stored users page"),
                        }).E));
                }
                catch (e) {
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
    Sw.SimpleAuth = { pwdGenMain, keyManMain, userManMain, isApiKeysSupported, isStoredUsersSupported, removeApiKey, createApiKey, removeStoredUser, addStoredUser };
    window.pwdGenMain = pwdGenMain;
    window.keyManMain = keyManMain;
    window.userManMain = userManMain;
})();