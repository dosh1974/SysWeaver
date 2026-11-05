/**
 * @file PasskeyUi.js - the passkey pages of SysWeaver.MicroService.PassKey (served as auth/PasskeyUi.js).
 *
 * Entry points (body onload of the pages, globals): usePassKeyMain (UsePassKey.html), addPassKeyMain (AddPassKey.html,
 * optional ?token=<t> from a QR code / link), addPassKeyOtherMain (AddPassKeyOnOtherDevice.html: a single use QR code),
 * myPassKeysMain (MyPassKeys.html: list, rename, remove, add). Also on `Sw.PasskeyUi`.
 *
 * Loading: classic `defer` scripts in this order: ../sw/core.js, ../sw/ui.js, Auth.js, AuthUi.js (MicroService.Auth,
 * same auth/ folder), Passkey.js (the UI-less PassKey class), PasskeyUi.js. Styles: Auth.css, Passkey.css.
 * Server: through PassKey (Api/auth/passkey/*), the QR image Api/auth/passkey/GetQR.svg.
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const U = Sw.AuthUi;
    const P = Sw.PassKey;
    const el = Sw.el;

    /**
     * Show the result of a ceremony error: the status text, and a fail toast unless cancelled.
     * @param {object} r The result.
     * @param {object} status The Sw.AuthUi.Status.
     */
    function showError(r, status) {
        status.Set(P.ErrorText(r), r.Error == P.Errors.Cancelled ? "warning" : "error");
        if (r.Error != P.Errors.Cancelled)
            Sw.Fail(P.ErrorText(r) + (r.Message ? "\n" + r.Message : ""));
    }

    /**
     * End a page with a final message and a warning icon.
     * @param {object} page The AuthPage.
     * @param {object} status The status line.
     * @param {string} text The message.
     * @param {Sw.Button} [button] A button to hide.
     */
    function stop(page, status, text, button) {
        page.SetIcon("IconWarning");
        status.Set(text, "warning");
        U.Hide(status, button);
    }

    /**
     * UsePassKey.html: sign in with any passkey of the site (not when unsupported or already signed in).
     * @returns {Promise<void>}
     */
    async function usePassKeyMain() {
        await U.Page("IconUsePasskey", async page => {
            const status = new U.Status(page);
            if (!P.IsSupported())
                return stop(page, status, P.ErrorText({ Error: P.Errors.NotSupported }));
            const user = await P.GetLoggedInUser();
            if (user)
                return stop(page, status, _T("The user {0} is already signed in!", user.Username, "Message shown when trying to sign in with a passkey while a user is signed in, {0} is replaced with the user name") + "\n" + _TF("Please sign out before signing in with another user.", "Message shown when trying to sign in with a passkey while a user is signed in"));
            const ceremony = P.SignIn();
            //  Pre-fetch so the browser prompt opens directly from the click (Safari)
            let c;
            try {
                c = await ceremony.Fetch();
            }
            catch (e) {
                return stop(page, status, P.ErrorText({ Error: P.Errors.NotSupported }) + "\n" + (e.message || e));
            }
            if (c.error)
                return stop(page, status, P.ErrorText({ Error: c.error }));
            status.Set(_TF("Use the button below to sign in with a passkey. A passkey on your phone or a security key can also be used.", "Instruction shown on the sign in with passkey page"));
            const button = U.Button(U.Buttons(page), {
                Text: _TF("Sign in with passkey", "Text of a button that when pressed will sign in using a passkey"),
                Title: _TF("Sign in using a passkey", "Tool tip of a button that when pressed will sign in using a passkey"),
                Icon: "IconUsePasskey", Submit: true,
            }, page);
            page.OnSubmit = async () => {
                status.Set(_TF("Select your passkey..", "Message shown while the user should select a passkey"), "info");
                try {
                    const r = await ceremony.Run();
                    if (r.Error == 0) {
                        status.Set(_TF("Signed in!", "Message shown when a user signed in successfully"), "ok");
                        U.Hide(status, button);
                        await Sw.Auth.StartPage();
                        return;
                    }
                    showError(r, status);
                }
                catch (e) {
                    const m = _TF("Failed to sign in.", "Error message shown when a passkey sign in failed") + "\n" + (e.message || e);
                    status.Set(m, "error");
                    Sw.Fail(m);
                }
                ceremony.Fetch().catch(() => { });
            };
            page.Focus(button.E);
        });
    }

    /**
     * AddPassKey.html[?token=<t>]: add a passkey on this device; with a token no user may be signed in (the token
     * ceremony signs in), without one a signed in user is required.
     * @returns {Promise<void>}
     */
    async function addPassKeyMain() {
        //  Signed in (no token): an account page (inside the app shell a Design v2 form page with a page header, AuthUi Account)
        const accountOpts = Sw.Url.Get("token") ? {} : { Account: true, Description: _TF("Add a passkey on this device, so you can sign in without a password.", "One line description of the add passkey page") };
        await U.Page("IconAddPasskey", async page => {
            const token = Sw.Url.Get("token");
            //  A link / QR code that no longer works is said on load, with the real cause (used, a newer one, the account changed ...)
            if (token && U.CheckLinkToken && !(await U.CheckLinkToken("AddPassKey", { Page: page, Token: token })).Live)
                return;
            const heading = U.Heading(page, "");
            const status = new U.Status(page);
            if (!P.IsSupported())
                return stop(page, status, P.ErrorText({ Error: P.Errors.NotSupported }));
            const user = await P.GetLoggedInUser();
            if (token && user)
                return stop(page, status, _TF("A user is already signed in!", "Message shown when a user is signed in while trying to add a passkey using a link or QR code") + "\n" + _TF("Please sign out before adding a passkey using a link or QR code.", "Message shown when a user is signed in while trying to add a passkey using a link or QR code"));
            if (!token && !user)
                return stop(page, status, _TF("No user is signed in!", "Message shown when no user is signed in while trying to add a passkey") + "\n" + _TF("Please sign in before adding a passkey.", "Message shown when no user is signed in while trying to add a passkey"));
            const ceremony = token ? P.AttachUsingToken(token) : P.Attach();
            let c;
            try {
                c = await ceremony.Fetch();
            }
            catch (e) {
                return stop(page, status, P.ErrorText({ Error: P.Errors.NotSupported }) + "\n" + (e.message || e));
            }
            if (c.error)
                return stop(page, status, P.ErrorText({ Error: c.error }));
            const name = ceremony.UserName ?? user?.Username;
            if (name)
                heading.textContent = _T("Add a passkey for {0}", name, "Label on the add passkey page, {0} is replaced with the user name of the account that the passkey is added to");
            status.Set(_TF("Use the button below to add a passkey on this device.", "Instruction shown on the add passkey page"));
            const button = U.Button(U.Buttons(page), {
                Text: _TF("Add passkey", "Text of a button that when pressed will add a passkey"),
                Title: _TF("Add a passkey on this device", "Tool tip of a button that when pressed will add a passkey"),
                Icon: "IconAddPasskey", Submit: true,
            }, page);
            page.OnSubmit = async () => {
                status.Set(_TF("Follow the instructions of your browser or device..", "Message shown while a passkey is being added"), "info");
                try {
                    const r = await ceremony.Run();
                    if (r.Error == 0) {
                        status.Set(_TF("Passkey added!", "Message shown when a passkey was added"), "ok");
                        U.Hide(status, button);
                        if (token || r.User)
                            await Sw.Auth.StartPage();
                        return;
                    }
                    if (r.Error == P.Errors.TokenExpired || r.Error == P.Errors.AlreadyRegistered || r.Error == P.Errors.AlreadyOnDevice)
                        return stop(page, status, P.ErrorText(r), button);
                    showError(r, status);
                }
                catch (e) {
                    const m = _TF("Failed to add the passkey.", "Error message shown when a passkey couldn't be added") + "\n" + (e.message || e);
                    status.Set(m, "error");
                    Sw.Fail(m);
                }
                ceremony.Fetch().catch(() => { });
            };
            page.Focus(button.E);
        }, accountOpts);
    }

    /**
     * AddPassKeyOnOtherDevice.html: a single use QR code (the page image, Api/auth/passkey/GetQR.svg) that opens
     * AddPassKey.html?token=.. on another device. Requires a signed in user.
     * @returns {Promise<void>}
     */
    async function addPassKeyOtherMain() {
        let user = null;
        try {
            user = await P.GetLoggedInUser();
        }
        catch {
        }
        if (!user) {
            await U.Page("IconWarning", async page => {
                new U.Status(page).Set(_TF("No user is signed in!", "Message shown when no user is signed in while trying to add a passkey") + "\n" + _TF("Please sign in before adding a passkey.", "Message shown when no user is signed in while trying to add a passkey"), "warning");
            });
            return;
        }
        let minutes = 15;
        try {
            minutes = (await P.Call("GetQRLifeTime", null)) ?? minutes;
        }
        catch {
        }
        await U.Page(P.ApiPrefix + "GetQR.svg", async page => {
            page.IconE.alt = _TF("QR code", "Alternative text of a QR code image");
            //  The server refuses the QR code (ex: 401 for an account the passkey service does not manage): say so instead of a broken image
            const noQr = () => {
                page.IconWrap.hidden = true;
                new U.Status(page).Set(_TF("The QR code could not be created for this account.", "Error shown on the add passkey on another device page when the server did not return the QR code"), "error");
            };
            if (page.IconE.complete && !page.IconE.naturalWidth)
                setTimeout(noQr, 0);
            else
                page.IconE.addEventListener("error", noQr, { once: true });
            U.Heading(page, _T("Allow {0} to sign in on another device using a passkey, by scanning the QR code and following the link.", "<em>" + Sw.Format.HtmlSafe(user.Username) + "</em>", "Instruction shown next to a QR code used to add a passkey on another device, {0} is replaced with the user name"), null, true);
            U.Text(page, _T("The QR code may only be used once and expires in {0} minutes.", minutes, "Information about a QR code used to add a passkey on another device, {0} is replaced with the number of minutes"), { Class: "auth-muted" });
        }, { IconIsFile: true, Account: true, Description: _TF("Add a passkey on another device, by scanning a QR code with it.", "One line description of the add passkey on another device page") });
    }

    /**
     * MyPassKeys.html: the signed in user's passkeys (name, "(this device)", created / last used live times, type) with
     * Rename / Remove, and Add passkey (disabled when a passkey of the user is known to be on this device).
     * @returns {Promise<void>}
     */
    async function myPassKeysMain() {
        await U.Page("IconUsePasskey", async page => {
            const user = await P.GetLoggedInUser();
            if (!user) {
                page.SetIcon("IconWarning");
                new U.Status(page).Set(_TF("No user is signed in!", "Message shown when no user is signed in on the my passkeys page") + "\n" + _TF("Please sign in to see your passkeys.", "Message shown when no user is signed in on the my passkeys page"), "warning");
                return;
            }
            U.Heading(page, _T("Passkeys of {0}", user.Username, "Title of the my passkeys page, {0} is replaced with the user name"));
            const list = el("ul.auth-list");
            page.Box.append(list);
            const buttons = P.IsSupported() ? U.Buttons(page) : null;
            const ago = [null, null, _TF("{0} ago", "Text describing how long ago something happened, {0} is replaced with a time span, ex: 5 minutes")];
            const noName = () => _TF("Passkey", "Default name of a passkey without a name");

            /**
             * A "label value" line.
             * @param {HTMLElement} parent The item.
             * @param {string} label Label.
             * @param {*} value Value (dates are shown live as "{0} ago").
             * @param {boolean} isDate Value is a date.
             * @param {string} [title] Tooltip.
             */
            function line(parent, label, value, isDate, title) {
                const v = el("span.auth-item-value");
                if (isDate)
                    Sw.Cells.DateTimeLive(v, value, ago, null, false, null, null, true);
                else
                    v.textContent = value;
                parent.append(el("div.auth-item-line", { title: title || null }, label + " ", v));
            }

            /**
             * Confirm and remove a passkey.
             * @param {object} key PassKeyInfo.
             * @returns {Promise<void>}
             */
            async function remove(key) {
                if (!await Sw.Dialog.Confirm(
                    _TF("Remove the passkey?", "Title of a confirmation dialog shown when a user wants to remove a passkey"),
                    _T("The passkey {0} will be removed from your account, it can't be used to sign in any more.", key.Name || noName(), "Text of a confirmation dialog shown when a user wants to remove a passkey, {0} is replaced with the name of the passkey") + "\n" + _TF("The passkey isn't deleted from the device or passkey provider, you can remove it there.", "Text of a confirmation dialog shown when a user wants to remove a passkey"),
                    _TF("Remove passkey", "Text of a button on a confirmation dialog that when pressed will remove a passkey"),
                    {
                        Danger: true, OkIcon: "IconDelete",
                        OkTitle: _TF("Remove the passkey from your account", "Tool tip of a button on a confirmation dialog that when pressed will remove a passkey"),
                        CancelTitle: _TF("Keep the passkey", "Tool tip of a button on a confirmation dialog that when pressed will close the dialog without removing the passkey"),
                    }))
                    return;
                try {
                    const r = await P.Remove(key.Id, user.Guid);
                    if (r && r.Error == 0) {
                        await rebuild();
                        return;
                    }
                    Sw.Fail(r?.Error == P.Errors.LastSignInMethod
                        ? _TF("This is the last way you can sign in, add a password or another passkey before removing it.", "Error message shown when trying to remove the last passkey of a user that have no password")
                        : P.ErrorText(r));
                }
                catch (e) {
                    Sw.Fail(_TF("Failed to remove the passkey.", "Error message shown when a passkey couldn't be removed") + "\n" + (e.message || e));
                }
            }

            /**
             * Rename a passkey in a dialog (name max 64 chars; Rename only when non-empty and changed).
             * @param {object} key PassKeyInfo.
             * @returns {Promise<void>}
             */
            async function rename(key) {
                const old = key.Name ?? "";
                const tb = new Sw.TextBox({
                    Value: old, MaxLength: 64, AutoComplete: "off",
                    Label: _TF("Name", "Label of an input field where the user enters a new name of a passkey"),
                    Placeholder: _TF("Passkey name", "Place holder of an input field where the user enters a new name of a passkey"),
                    Title: _TF("Enter a name that helps you identify the passkey, ex: the device or passkey provider", "Tool tip of an input field where the user enters a new name of a passkey"),
                    OnInput: () => ok.SetDisabled(!canSave(), whyNot()),
                });
                tb.E.classList.add("auth-field");
                /**
                 * True if the trimmed name is non-empty and changed.
                 * @returns {boolean}
                 */
                const canSave = () => {
                    const n = tb.Get().trim();
                    return n.length > 0 && n !== old;
                };
                /**
                 * Why Rename is disabled (empty or unchanged name).
                 * @returns {string}
                 */
                const whyNot = () => tb.Get().trim().length > 0
                    ? _TF("Change the name first", "Why the rename button of a passkey is disabled: the entered name is the current name")
                    : _TF("Enter a name first", "Why the rename button of a passkey is disabled: the name is empty");
                const ok = new Sw.Button({
                    Text: _TF("Rename", "Text of a button that when pressed will save the new name of a passkey"),
                    Title: _TF("Save the new name of the passkey", "Tool tip of a button that when pressed will save the new name of a passkey"),
                    Icon: "IconPasskeyRename", Kind: "primary", Disabled: whyNot(),
                    OnClick: () => save(),
                });
                const d = Sw.Dialog.Open({
                    Title: _TF("Rename passkey", "Title of the dialog where a passkey is renamed"),
                    Content: el("div.auth-dialog", null, tb.E, Sw.ButtonRow(ok)),
                    Focus: tb.Input,
                    OnShow: () => tb.Input.select(),
                });
                tb.Input.addEventListener("keydown", e => {
                    if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                        e.preventDefault();
                        ok.Click();
                    }
                });
                /**
                 * Save the name.
                 * @returns {Promise<void>}
                 */
                async function save() {
                    if (!canSave())
                        return;
                    tb.Input.readOnly = true;
                    try {
                        if (await P.Rename(key.Id, tb.Get().trim())) {
                            d.Close(true);
                            await rebuild();
                            return;
                        }
                        Sw.Fail(_TF("The passkey doesn't exist any more.", "Error message shown when trying to rename a passkey that was removed"));
                    }
                    catch (e) {
                        Sw.Fail(_TF("Failed to rename the passkey.", "Error message shown when a passkey couldn't be renamed") + "\n" + (e.message || e));
                    }
                    tb.Input.readOnly = false;
                }
                await d.Result;
            }

            /**
             * (Re)build the list and the add button.
             * @returns {Promise<void>}
             */
            async function rebuild() {
                const keys = (await P.GetPassKeys()) ?? [];
                list.replaceChildren();
                if (keys.length <= 0)
                    list.append(el("li.auth-text", { text: _TF("You don't have any passkeys.", "Message shown on the my passkeys page when the user don't have any passkeys") }));
                //  Browsers don't tell which passkeys they hold: known when created / used here, or the browser refused to add another
                const onDevice = key => key.ThisDevice || P.IsOnDevice(key.Id);
                const anyOnDevice = keys.some(onDevice) || P.IsUserOnDevice(user.Guid);
                for (const key of keys) {
                    const name = key.Name || noName();
                    const item = el("li.auth-item", null, el("div.auth-item-name", {
                        text: onDevice(key) ? _T("{0} (this device)", name, "Name of a passkey that is available on the current device, {0} is replaced with the name of the passkey") : name,
                    }));
                    line(item, _TF("Created:", "Label of the time when a passkey was created"), key.Created, true);
                    line(item, _TF("Last used:", "Label of the time when a passkey was last used to sign in"), key.LastUsed, true);
                    line(item, _TF("Type:", "Label of the type of a passkey (synced or device bound)"),
                        key.Synced
                            ? _TF("Synced", "Type of a passkey that is synced between the users devices by the passkey provider")
                            : _TF("Device bound", "Type of a passkey that only exists on one device or security key"),
                        false,
                        key.Synced
                            ? _TF("The passkey is synced by your passkey provider and can be used on your other devices", "Tool tip of a synced passkey")
                            : _TF("The passkey only exists on one device or security key", "Tool tip of a device bound passkey"));
                    const row = U.Buttons(item);
                    U.Button(row, {
                        Text: _TF("Rename", "Text of a button that when pressed will rename a passkey"),
                        Title: _TF("Change the name of this passkey", "Tool tip of a button that when pressed will rename a passkey"),
                        Icon: "IconPasskeyRename", Small: true, OnClick: () => rename(key),
                    });
                    U.Button(row, {
                        Text: _TF("Remove", "Text of a button that when pressed will remove a passkey"),
                        Title: _TF("Remove this passkey from your account", "Tool tip of a button that when pressed will remove a passkey"),
                        Icon: "IconDelete", Small: true, Kind: "danger", OnClick: () => remove(key),
                    });
                    list.append(item);
                }
                if (!buttons)
                    return;
                buttons.replaceChildren();
                const add = anyOnDevice ? null : P.Attach();
                //  Pre-fetch so the browser prompt opens directly from the click (Safari)
                /* SwWeb:allow ux-fbk-5 (a best effort pre-fetch: the click fetches again and shows its failure) */
                add?.Fetch().catch(() => { });
                const onDeviceWhy = _TF("A passkey for your account is already available on this device", "Tool tip of a disabled button that would add a passkey, when there already is a passkey on the device");
                const addButton = U.Button(buttons, {
                    Text: _TF("Add passkey", "Text of a button that when pressed will add a passkey"),
                    Title: anyOnDevice ? onDeviceWhy : _TF("Add a passkey on this device", "Tool tip of a button that when pressed will add a passkey"),
                    //  (disabled with its reason: the tool tip and an activation say why, INP-12)
                    Icon: "IconAddPasskey", Kind: "primary", Disabled: anyOnDevice ? onDeviceWhy : false,
                    OnClick: async () => {
                        try {
                            const r = await add.Run();
                            if (r.Error == 0)
                                return await rebuild();
                            if (r.Error == P.Errors.AlreadyOnDevice || r.Error == P.Errors.AlreadyRegistered) {
                                P.SetUserOnDevice(user.Guid);
                                Sw.Fail(P.ErrorText(r));
                                return await rebuild();
                            }
                            if (r.Error != P.Errors.Cancelled)
                                Sw.Fail(P.ErrorText(r) + (r.Message ? "\n" + r.Message : ""));
                            add.Fetch().catch(() => { });
                        }
                        catch (e) {
                            Sw.Fail(_TF("Failed to add the passkey.", "Error message shown when a passkey couldn't be added") + "\n" + (e.message || e));
                        }
                    },
                });
                if (anyOnDevice) {
                    //  A disabled button shows no tooltip: explain it below
                    buttons.append(el("p.auth-text.auth-muted", { text: addButton.Title }));
                }
            }
            await rebuild();
        }, { Class: "auth-wide", Account: true, Description: _TF("The passkeys that sign in to your account, on this and other devices.", "One line description of the my passkeys page") });
    }

    /** @namespace Sw.PasskeyUi - the passkey page entry points. */
    Sw.PasskeyUi = { usePassKeyMain, addPassKeyMain, addPassKeyOtherMain, myPassKeysMain };
    window.usePassKeyMain = usePassKeyMain;
    window.addPassKeyMain = addPassKeyMain;
    window.addPassKeyOtherMain = addPassKeyOtherMain;
    window.myPassKeysMain = myPassKeysMain;
})();
