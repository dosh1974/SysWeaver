/**
 * @file UserManagerUi.js - the self-service account pages of SysWeaver.MicroService.UserManager (served as auth/).
 *
 * Entry points (globals, called from <body onload>; also on `Sw.UserManagerUi`):
 * SignUp -> signUpMain, ChoosePassword -> choosePasswordMain, SetPassword -> setPasswordMain,
 * ForgotPassword -> forgotPasswordMain, ResetPassword -> resetPasswordMain, AddPassword -> addPasswordMain,
 * DeletePassword -> deletePasswordMain, AddEmail / ChangeEmail / DeleteEmail -> addEmailMain / changeEmailMain /
 * deleteEmailMain, AddPhone / ChangePhone / DeletePhone -> addPhoneMain / changePhoneMain / deletePhoneMain,
 * DeleteAccount -> deleteAccountMain, UndoEmail -> undoEmailMain, InviteUser -> inviteUserMain.
 *
 * Page kinds:
 *  - Request mode (no query): the page sends a message (e-mail / text) and shows the short code step
 *    (Sw.AuthUi.ShortCode), which navigates to "<Page>.html?<code>".
 *  - Token mode ("<Page>.html?<token|code>", the whole query is the token; for keyed links like "DeleteAccount.html?token=.."
 *    and "ChoosePassword.html?user=..&salt=..&token=.." the token parameter, Sw.Auth.QueryToken): a confirm button or a new password form.
 *    The link is checked on load without using it (Sw.AuthUi.CheckLinkToken): a dead link shows "This link no longer works" with
 *    the real cause (Sw.AuthUi.LinkGone) before anything is offered; a live one shows its facts; a 410 of the action shows the same.
 *  - Proof forms: the current password proves the user (Sw.Auth.Proof), plus a new password / e-mail / phone.
 * Forms validate live (newest validation wins), the main button is enabled only when valid, Enter moves to the next
 * empty field or submits, autocomplete attributes (and a hidden user name) let password managers fill and save.
 * Pages that end signed in navigate with Sw.Auth.StartPage (reloads the shell when embedded).
 *
 * Loading: classic `defer` scripts: ../sw/core.js, ../sw/ui.js, Auth.js, AuthUi.js (MicroService.Auth), UserManager.js, this file.
 * The optional passkey plug-in (Passkey.js / .css of MicroService.PassKey: ChoosePassword / ResetPassword offer "Create a
 * passkey") is linked statically by those two pages, as the original pages (a 404 when the service does not run; LoadPassKeys then
 * sends no probe).
 * Styles: Auth.css. Icons: sw/ui.css (IconAuth*, IconChoosePassword, IconAddPasskey ...).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const A = Sw.Auth;
    const U = Sw.AuthUi;
    const UM = Sw.UserManager;
    const el = Sw.el;

    // ------------------------------------------------------------------ helpers

    /**
     * The optional passkey plug-in when loaded and supported.
     * @returns {*} PassKey or null.
     */
    function passKeys() {
        const P = (typeof PassKey !== "undefined") ? Sw.PassKey : window.PassKey;
        return (P && P.IsSupported && P.IsSupported()) ? P : null;
    }

    /**
     * Error text of an exception.
     * @param {*} e The exception.
     * @returns {string}
     */
    const msg = e => (e && e.message) ? e.message : String(e);

    /**
     * The reason of a button that is disabled while the form's request runs, or after it was done (the page moves on).
     * @returns {string} The text.
     */
    const waitReason = () => _TF("Wait until the current step is done", "Reason of a disabled button of an account or sign in page while its request runs");

    /**
     * Build an account page of the signed in user (U.Page with Account: inside the app shell a Design v2 form page with a page header
     * and this one line description; stand alone, ex: opened from an e-mail link, the centered card).
     * @param {string} icon The page icon class.
     * @param {string} desc The one line description of the page header.
     * @param {function(object):Promise<void>} build Builds the content.
     * @returns {Promise<object>} The page.
     */
    const accountPage = (icon, desc, build) => U.Page(icon, build, { Account: true, Description: desc });

    /**
     * Live validation of a form: runs check() on every input (newest wins) and applies the result to the fields, the
     * status line and the main button.
     * @param {object} status The Sw.AuthUi.Status.
     * @param {Sw.Button} button The main button.
     * @param {function():Promise<{Error:string|null, Field:object|null, Neutral:boolean, Leak:string|null}>} check
     * Returns the first problem (Field = the field to mark, Neutral = show the text without error color, ex: an empty
     * field), or Error null; Leak = a valid new password to leak check.
     * @param {object[]} fields All fields (unmarked first).
     * @returns {function(boolean=):Promise<boolean>} update(all): validates now, resolves true when valid; all = show the problems of
     * every field (a submit attempt).
     *
     * When problems show (UX guide FRM-3): a field's error appears once the user left the field (blur with a value) or tried to submit,
     * never while the first characters are typed; after that it follows every input (it goes as soon as the value is fixed). Neutral
     * texts (ex: "Enter the email address of the person to invite.") show at once. The error line is the field's description
     * (aria-describedby), and the disabled main button gives the problem as its reason (INP-12).
     */
    function liveValidation(status, button, check, fields) {
        let seq = 0;
        let all = false;
        /** @type {Set<object>} Fields the user has left with a value (their errors show). */
        const touched = new Set();
        if (status.E && !status.E.id)
            status.E.id = Sw.Uid ? Sw.Uid("um-status") : "um-status-" + Math.random().toString(36).slice(2);
        //  LAY-9: the line that follows every input has room for two lines from the start (a problem that comes, changes or goes
        //  while typing moves nothing below it; a longer one keeps its height once shown)
        if (status.E && Sw.Reserve)
            Sw.Reserve.Line(status.E, { Lines: 2 });
        const describe = (f, on) => {
            const i = f && f.Input;
            if (!i || !status.E)
                return;
            const ids = (i.getAttribute("aria-describedby") || "").split(/\s+/).filter(x => x && x !== status.E.id);
            if (on)
                ids.push(status.E.id);
            if (ids.length)
                i.setAttribute("aria-describedby", ids.join(" "));
            else
                i.removeAttribute("aria-describedby");
        };
        /** The values of the fields when a submit failed (update.Hold): the failure text stays while they are the same. */
        let held = null;
        const values = () => fields.map(f => (f && f.Input ? f.Input.value : "")).join("\n");
        const update = async show => {
            if (show)
                all = true;
            const my = ++seq;
            const r = await check();
            if (my !== seq)
                return false;
            //  A failed submit's text stays until a value changes (a blur, ex: the refocus after the failure, or a late leak check
            //  must not replace it; the old pages kept it too)
            if (held !== null) {
                if (held === values()) {
                    button.SetDisabled(!!r.Error, r.Error || undefined);
                    return !r.Error;
                }
                held = null;
            }
            fields.forEach(f => {
                f.SetInvalid(false);
                describe(f, false);
            });
            if (r.Error) {
                const visible = r.Neutral || all || !r.Field || touched.has(r.Field);
                if (visible) {
                    if (r.Field && !r.Neutral)
                        r.Field.SetInvalid(true);
                    status.Set(r.Error, r.Neutral ? null : "error");
                    describe(r.Field, true);
                }
                else
                    status.Clear();
            }
            else if (r.Leak)
                status.Leak(r.Leak);
            else
                status.Clear();
            button.SetDisabled(!!r.Error, r.Error || undefined);
            return !r.Error;
        };
        for (const f of fields)
            if (f && f.Input) {
                f.Input.addEventListener("blur", () => {
                    //  (not the blur of a field that a running submit locks or removes: no extra validation request)
                    if (f.Input.value && !touched.has(f) && f.Input.isConnected && !f.Input.disabled && !f.Input.readOnly) {
                        touched.add(f);
                        update();
                    }
                });
                //  Enter is a submit attempt: show every problem
                f.Input.addEventListener("keydown", ev => {
                    if (ev.key === "Enter" && !all)
                        update(true);
                });
            }
        //  So is a click on the (disabled) main button
        button.E?.addEventListener("click", () => {
            if (!all)
                update(true);
        }, true);
        /** Keep the status text (a failed submit) until a field's value changes. */
        update.Hold = () => {
            held = values();
        };
        return update;
    }

    /**
     * The problem of an empty new password: the reason of the disabled main button and, after a submit attempt, the error line
     * (not shown before: the rules checklist below the field already says what is needed).
     * @returns {string} The text.
     */
    const noPassword = () => _TF("Enter a new password that follows the rules below", "Reason of the disabled button of a new password form while no password is entered (the password rules are listed below the field)");

    /**
     * The reason of a main button that starts disabled (liveValidation replaces it with the first problem a moment later).
     * @returns {string} The text.
     */
    const fillFirst = () => _TF("Fill in the form first", "Reason of the disabled main button of an account form before it was checked");

    /**
     * A sequenced server validation (e-mail / phone): as the old pages (AuthGetEmailError / AuthGetPhoneError in setStatus), every
     * call asks the server at once (one request per change of the value, no delay and no memo); only the newest call's answer counts
     * (an older one that finishes later answers undefined).
     * @param {function(string):Promise<string|null>} fn The validation.
     * @returns {function(string):Promise<string|null|undefined>} The sequenced validation.
     */
    function sequenced(fn) {
        let n = 0;
        return async v => {
            const my = ++n;
            const r = await fn(v);
            return my === n ? r : undefined;
        };
    }

    /**
     * Add a hidden user name input (helps password managers to pair a new / current password with the account).
     * @param {object} page The AuthPage.
     * @param {string} name The user name.
     */
    function hiddenUser(page, name) {
        if (name)
            page.Box.prepend(el("input", { type: "text", name: "username", value: name, hidden: true, attrs: { autocomplete: "username", "aria-hidden": "true", tabindex: "-1" } }));
    }

    /**
     * A current password field (proof of the user).
     * @param {object} page The AuthPage.
     * @param {function():void} onInput Input callback.
     * @returns {object} The field.
     */
    function currentPassword(page, onInput) {
        return new U.Field(page, {
            Label: _TF("Current password", "Label text for an input box where the user should enter their current password"),
            Placeholder: _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"),
            Type: "password", AutoComplete: "current-password", Name: "password",
            OnInput: onInput,
        });
    }

    /**
     * The problem of a current password field, as the old account pages (ValidatePassword with the create policy): empty asks for
     * it (a neutral hint), a password that can't follow the policy is an error of the field (the main button stays disabled).
     * @param {object} cur The current password field.
     * @param {object} policy The create policy (null: the default length rules).
     * @returns {{Error:string, Field:object, Neutral:boolean}|null} The problem, or null.
     */
    function currentPasswordProblem(cur, policy) {
        const p = A.Trim(cur.Get());
        if (!p)
            return { Error: _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), Field: cur, Neutral: true };
        const e = A.CheckPolicy(p, policy);
        return e ? { Error: e, Field: cur, Neutral: false } : null;
    }

    /**
     * A new password field with generator and the policy "?" button.
     * @param {object} page The AuthPage.
     * @param {object} policy The create policy.
     * @param {string} label Label text.
     * @param {function():void} onInput Input callback.
     * @returns {object} The field.
     */
    function newPassword(page, policy, label, onInput) {
        return new U.Field(page, {
            Label: label,
            Placeholder: _TF("Enter a new password", "Placeholder text for an input box where the user should enter a new password"),
            Type: "password", AutoComplete: "new-password", Name: "new-password",
            Generate: policy || true,
            MinLength: A.Limits(policy).Min,
            LabelExtra: U.InfoButton(A.PolicyText(policy)).E,
            OnInput: onInput,
        });
    }

    /**
     * Run a submit: on success the handler returns true; otherwise the failure is shown once, in the page's status line (UX guide
     * FRM-3: one error surface, no toast besides it; TXT-30: what failed, why, what to do), the values stay (FRM-5), the form is
     * re-validated and the field refocused. The line stays until the user changes something (the re-validation sets it again).
     * @param {object} o Options: { Status, Update (re-validation), Focus (field), Failed (what failed, ex: "Failed to send
     * invitation."), Run (async, returns true on success; throws (with the reason) or returns false on failure), Gone(e)
     * (optional: a link page; a 410 = the link died since the page was opened, ex: U.LinkGone) }. (Retry, the old temporary "try
     * again" text, is no longer shown.) The text is composed by Sw.Net.Message.
     * @returns {Promise<void>}
     */
    async function submit(o) {
        const todo = _TF("Check the entries and try again in a moment.", "Last sentence of a failure message on an account page: what the user can do");
        let text = "";
        try {
            if (await o.Run())
                return;
        }
        catch (e) {
            //  The link died since the page was opened (410): the page says so, with the cause (trying again can't help)
            if (o.Gone && U.IsLinkGone && U.IsLinkGone(e))
                return o.Gone(e);
            //  The one message composer: what failed, the server's reason (a lost connection in the shared words), what to do (423: a
            //  disabled account, the reason says what to do: trying again can't help)
            text = e && e.Status === 423 ? Sw.Net.Message(o.Failed, e) : Sw.Net.Message(o.Failed, e, { Do: todo });
        }
        if (o.Update)
            await o.Update();
        o.Status.Set(text || (o.Failed + " " + _TF("The service did not accept it.", "Reason added to a failure message on an account page when the service refused without saying why") + " " + todo), "error");
        if (o.Update && o.Update.Hold)
            o.Update.Hold();
        if (o.Focus)
            setTimeout(() => o.Focus.Focus(true), 0);
    }

    /**
     * Request mode, as the old pages (AddPassword, DeletePassword, DeleteAccount): the message with the code is sent at once when the
     * page opens ("Sending instructions .."), then the short code step shows ("Code sent to ..."). Only when sending failed the reason
     * is shown with a "Send the code" button to try again.
     * @param {object} page The AuthPage.
     * @param {object} o Options: { Busy (text while sending), Failed (text), Send (async, returns the targets), Link }.
     * @returns {Promise<void>}
     */
    async function requestCode(page, o) {
        const status = new U.Status(page);
        let button = null;
        let busy = false;
        const send = async () => {
            if (busy)
                return;
            busy = true;
            button?.SetDisabled(true, waitReason());
            status.Set(o.Busy, "info");
            let targets = null;
            let error = null;
            try {
                targets = await o.Send();
            }
            catch (e) {
                error = e;
            }
            if (targets) {
                U.ShortCode(page, {
                    Html: _TH("Code sent to {0}.", U.UserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                    Link: o.Link,
                });
                return;
            }
            //  A failure is said once, in the status line with the reason (FRM-3: no toast besides it), with a button to try again
            status.Set(error
                ? Sw.Net.Message(o.Failed, error)
                : o.Failed + " " + _TF("The service did not accept it.", "Reason added to a failure message on an account page when the service refused without saying why"), "error");
            busy = false;
            if (!button)
                button = U.Button(U.Buttons(page), {
                    Text: _TF("Send the code", "Button of an account page that sends a message with a code (email or text message) when pressed"),
                    Title: _TF("Send a message with a code to your email address or phone", "Tool tip of the send the code button of an account page"),
                    Icon: "IconAuthPost", Submit: true,
                }, page);
            button.SetDisabled(false);
            page.Focus(button.E);
        };
        page.OnSubmit = send;
        page.Loaded();
        await send();
    }

    /**
     * The facts of a live e-mail link (the account and the address it adds), for the confirmation (SAF-2).
     * @param {object} link The Sw.AuthUi.CheckLinkToken result.
     * @returns {string|null} The sentence, or null when the server gave no facts.
     */
    const emailFacts = link => (link.Account && link.Target)
        ? _T("Account {0}, email address {1}.", link.Account, link.Target, "Facts on the page of a link that adds or changes an email address.{0} is replaced with the user name of the account, {1} with the email address that the link adds")
        : null;

    /**
     * The facts of a live phone link (the account and the number it adds), for the confirmation (SAF-2).
     * @param {object} link The Sw.AuthUi.CheckLinkToken result.
     * @returns {string|null} The sentence, or null when the server gave no facts.
     */
    const phoneFacts = link => (link.Account && link.Target)
        ? _T("Account {0}, phone number {1}.", link.Account, link.Target, "Facts on the page of a link that adds or changes a phone number.{0} is replaced with the user name of the account, {1} with the phone number that the link adds")
        : null;

    /**
     * The facts of a live account link (the account it acts on), for the confirmation (SAF-2).
     * @param {object} link The Sw.AuthUi.CheckLinkToken result.
     * @returns {string|null} The sentence, or null when the server gave no facts.
     */
    const accountFacts = link => link.Account
        ? _T("Account {0}.", link.Account, "Facts on the page of a link that deletes the password or the account.{0} is replaced with the user name of the account")
        : null;

    /**
     * Token mode confirm page: first the link is checked without using it (Sw.AuthUi.CheckLinkToken): a dead link shows "This link
     * no longer works" with the real cause before anything is offered; a live one shows the facts (o.Facts) and one button that calls
     * the token api and shows the result (no navigation). A 410 of the call (the link died meanwhile) shows the same page.
     * @param {object} page The AuthPage.
     * @param {string} token The token.
     * @param {object} o Options: { Kind (the link kind, see CheckLinkToken), Text, Title, Icon, Danger, Busy, Done, Failed, Call(token),
     * Facts(link) (optional: a sentence with the facts of a live link, ex: the new address; null for none), Confirm(link) (async, false
     * cancels: a confirmation dialog before Call, ex: Sw.Cells.ConfirmDanger; link = the CheckLinkToken result), After(page, link)
     * (optional: the end of the page after a success instead of the Done line, ex: Sw.AuthUi.Done with where to go next) }.
     * @returns {Promise<void>}
     */
    async function tokenConfirm(page, token, o) {
        const link = await U.CheckLinkToken(o.Kind, { Page: page, Token: token });
        if (!link.Live)
            return;
        const facts = o.Facts ? o.Facts(link) : null;
        if (facts)
            U.Text(page, facts, { Class: "um-link-facts", NoCopy: true });
        const status = new U.Status(page);
        const row = U.Buttons(page);
        U.Button(row, { Text: o.Text, Title: o.Title, Icon: o.Icon, Submit: true, Kind: o.Danger ? "danger" : "primary" }, page);
        const note = o.Title ? U.Text(page, o.Title, { Class: "auth-muted", NoCopy: true }) : null;
        //  (the page's initial focus: its one action, never the page body; Admin N20)
        page.Focus(page.MainButton ? page.MainButton.E : null);
        page.OnSubmit = async () => {
            if (o.Confirm && !await o.Confirm(link))
                return;
            status.Set(o.Busy, "info");
            try {
                if (await o.Call(token)) {
                    //  (the page ends with its result and where to go next, Sw.AuthUi.Done: never a dead end)
                    if (o.After && U.Done)
                        return o.After(page, link);
                    //  The explanation of the button ("This button will ...", "Just close this page to keep ...") is stale once done
                    status.Set(o.Done, "ok");
                    //  (the focus was on the used button: it goes to the result, not to the page body; FOC-1)
                    U.Hide(status, row, note);
                    return;
                }
                status.Set(o.Failed + " " + _TF("The service did not accept it.", "Reason added to a failure message on an account page when the service refused without saying why"), "error");
            }
            catch (e) {
                //  The link died since the page was opened: the page says so, with the cause (the button can't work any more)
                if (U.IsLinkGone(e))
                    return U.LinkGone(page, { Reason: e.Reason }, o.Kind);
                //  Said once, in the status line with the reason (FRM-3: no toast besides it)
                status.Set(Sw.Net.Message(o.Failed, e), "error");
            }
        };
    }

    /**
     * The page of a link (or code) that no longer works (the shared Sw.AuthUi.LinkGone: the cause, what to do, Request a new link /
     * Sign in), when the link was not checked on load (a short code, an older server) and its data could not be read.
     * @param {object} page The AuthPage.
     * @param {string} kind The link kind (see Sw.AuthUi.CheckLinkToken).
     * @param {object} [state] The CheckLinkToken result (its reason), if any.
     */
    function badToken(page, kind, state) {
        U.LinkGone(page, state && !state.Live ? state : null, kind);
    }

    // ------------------------------------------------------------------ new password pages (choose / reset / add)

    /**
     * A new password form (optionally with "Create a passkey"), ending signed in.
     * @param {object} page The AuthPage.
     * @param {object} o Options: { Token, Kind (the link kind: a 410 of Save shows Sw.AuthUi.LinkGone), Data ({Salt, NickName, UserName}), Welcome (show "Welcome {user}"),
     * Heading, HeadingPasskey (heading when a passkey can be created), PassKeyCeremony (function(P, token) -> PassKeyChallenge),
     * ButtonText, ButtonTitle, Busy, Done, Failed, Save(pw, salt) (async, true on success, false on failure, or a text: the password was
     * refused for that reason, ex: the current password after a forced reset; shown at the field, the same password is flagged again while typing) }.
     * @returns {Promise<void>}
     */
    async function newPasswordPage(page, o) {
        const user = o.Data.NickName ?? o.Data.UserName;
        hiddenUser(page, o.Data.UserName ?? user);
        if (o.PassKeyCeremony && U.LoadPassKeys)
            await U.LoadPassKeys();
        let P = o.PassKeyCeremony ? passKeys() : null;
        //  No challenge request (404) when the passkey service isn't running
        if (P && P.IsAvailable && !(await P.IsAvailable()))
            P = null;
        let ceremony = null;
        if (P) {
            //  Pre-fetch the challenge so the browser prompt opens directly from the click (Safari)
            try {
                ceremony = o.PassKeyCeremony(P, o.Token);
                if ((await ceremony.Fetch()).error)
                    ceremony = null;
            }
            catch {
                ceremony = null;
            }
        }
        if (o.Welcome && user)
            U.Heading(page, _T("Welcome {0}.", user, "A message displayed to greet a user.{0} is replaced with the user id (user name, email or phone number)"));
        const policy = await UM.CreatePolicy();
        U.Text(page, ceremony ? o.HeadingPasskey : o.Heading, { NoCopy: true });
        const pwd = newPassword(page, policy, _TF("New password", "Label text for an input box where the user should enter a new password"), () => update());
        const rules = U.Rules ? new U.Rules(page, policy, pwd) : null;
        const status = new U.Status(page);
        const row = U.Buttons(page);
        const setButton = U.Button(row, { Text: o.ButtonText, Title: o.ButtonTitle, Icon: "IconAuthPassword", Submit: true, Disabled: fillFirst() }, page);
        /** @type {Map<string, string>} Passwords the server refused (Save returned a text), with the reason. */
        const refused = new Map();
        const update = liveValidation(status, setButton, async () => {
            const p = A.Trim(pwd.Get());
            rules?.Update(p);
            if (!p)
                return { Error: noPassword(), Field: pwd };
            const e = refused.get(p) || A.CheckPolicy(p, policy);
            return { Error: e, Field: pwd, Leak: p };
        }, [pwd]);
        let keyButton = null;
        page.OnSubmit = () => submit({
            Status: status, Update: update, Focus: pwd, Failed: o.Failed,
            Gone: o.Kind ? e => U.LinkGone(page, { Reason: e.Reason }, o.Kind) : undefined,
            Run: async () => {
                keyButton?.SetDisabled(true, waitReason());
                status.Set(o.Busy, "info");
                const ok = await o.Save(pwd.Get(), o.Data.Salt);
                if (typeof ok === "string") {
                    //  Refused for a reason the user can fix (not a failure): say it at the field, keep the form
                    refused.set(A.Trim(pwd.Get()), ok);
                    keyButton?.SetDisabled(false);
                    await update();
                    pwd.Focus(true);
                    return true;
                }
                if (ok) {
                    status.Set(o.Done, "ok");
                    setButton.SetDisabled(true, waitReason());
                    page.SetBusy(true);
                    await A.StartPage();
                    return true;
                }
                keyButton?.SetDisabled(false);
                return false;
            },
        });
        if (ceremony) {
            keyButton = U.Button(row, {
                Text: _TF("Create a passkey", "Text of a button that when pressed will instruct the user to add a passkey"),
                Title: _TF("Create and allow sign in using a passkey", "Tool tip description on a button that when pressed will instruct the user to add a passkey"),
                Icon: "IconAddPasskey",
                OnClick: () => page.Run(async () => {
                    setButton.SetDisabled(true, waitReason());
                    status.Set(_TF("Create passkey..", "Message shown when a passkey is being created"), "info");
                    let res = null;
                    //  A failure is said once, in the status line (FRM-3: no toast besides it); a cancelled ceremony says nothing
                    let failed = null;
                    try {
                        res = await ceremony.Run();
                        if (res.Error == 0) {
                            status.Set(_TF("Passkey created", "Message shown when a new passkey was created successfully"), "ok");
                            keyButton.SetDisabled(true, waitReason());
                            page.SetBusy(true);
                            await A.StartPage();
                            return;
                        }
                        if (res.Error != P.Errors.Cancelled)
                            failed = P.ErrorText(res) + (res.Message ? " " + res.Message : "");
                    }
                    catch (e) {
                        failed = _TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created") + " " + msg(e);
                    }
                    ceremony.Fetch().catch(() => { });
                    await update();
                    if (failed)
                        status.Set(failed + " " + _TF("Try again in a moment.", "Last sentence of the message when a new passkey could not be created"), "error");
                }),
            });
        }
        await update();
        pwd.Focus();
    }

    /**
     * ChoosePassword.html?<token|code> (or ?user=..&salt=..&token=..): complete a new account with a password or a passkey.
     * @returns {Promise<void>}
     */
    async function choosePasswordMain() {
        await U.Page("IconChoosePassword", async page => {
            const token = A.QueryToken();
            //  A dead link is said on load, with the real cause (used, withdrawn, a newer invitation ...)
            const link = await U.CheckLinkToken("NewUser", { Page: page, Token: token });
            if (token && !link.Live)
                return;
            const data = token ? await UM.NewUserData(token).catch(() => null) : null;
            if (!data)
                return badToken(page, "NewUser", link);
            await newPasswordPage(page, {
                Token: token, Kind: "NewUser", Data: data, Welcome: true,
                Heading: _TF("Choose password", "A message letting a user know that they should provide a new password"),
                HeadingPasskey: _TF("Choose a password or create a passkey", "A message letting a user know that they should provide a new password or assign a new passkey"),
                PassKeyCeremony: (P, t) => P.NewAccount(t),
                ButtonText: _TF("Set password", "Text of a button that when clicked will set a new password"),
                ButtonTitle: _TF("Set the password and login", "Tool tip description on a button that when clicked will set a new password and perform a login"),
                Busy: _TF("Setting password..", "Message displayed when a users password is being set"),
                Done: _TF("Password set", "Message displayed when a users password was set successfully"),
                Failed: _TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed"),
                Save: async (pw, salt) => !!(await UM.AddUser(token, salt, pw)),
            });
        });
    }

    /**
     * ResetPassword.html?<token|code>: set a new password (or create a passkey) from a reset link / code; ends signed in.
     * @returns {Promise<void>}
     */
    async function resetPasswordMain() {
        await U.Page("IconAuthResetPassword", async page => {
            const token = A.QueryToken();
            //  A dead link is said on load, with the real cause (used, a newer link, the password changed ...)
            const link = await U.CheckLinkToken("ResetPassword", { Page: page, Token: token });
            if (token && !link.Live)
                return;
            const data = token ? await UM.NewPasswordData(token).catch(() => null) : null;
            if (!data)
                return badToken(page, "ResetPassword", link);
            //  An administrator required the new password: say so (the current password no longer signs in), and it must differ
            const forced = !!data.Forced;
            await newPasswordPage(page, {
                Token: token, Kind: "ResetPassword", Data: data,
                Heading: forced
                    ? _TF("An administrator asked you to choose a new password. Your current password no longer signs you in, and the new password must be different.", "Text on the reset password page when an administrator forced a password reset")
                    : _TF("Choose a new password", "A message letting a user know that they should provide a new password"),
                HeadingPasskey: forced
                    ? _TF("An administrator asked you to choose a new password (or create a passkey). Your current password no longer signs you in, and the new password must be different.", "Text on the reset password page when an administrator forced a password reset and a passkey can be created instead")
                    : _TF("Choose a new password or create a passkey", "A message letting a user know that they should provide a new password or assign a new passkey"),
                PassKeyCeremony: (P, t) => P.AttachUsingToken(t),
                ButtonText: _TF("Set password", "Text of a button that when clicked will set a new password"),
                ButtonTitle: _TF("Set the password and login", "Tool tip description on a button that when clicked will set a new password and perform a login"),
                Busy: _TF("Setting password..", "Message displayed when a users password is being set"),
                Done: _TF("Password set", "Message displayed when a users password was set successfully"),
                Failed: _TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed"),
                Save: async (pw, salt) => {
                    const r = await UM.ResetPassword(token, pw, salt, data.CurrentSalt);
                    if (r && (r.Error == UM.Errors.SamePassword || r.Error === "SamePassword"))
                        return _TF("This is your current password. Choose a different password.", "Error on the reset password page when an administrator forced a password reset and the user entered the current password again");
                    return r?.Error == 0;
                },
            });
        });
    }

    /**
     * AddPassword.html[?<token|code>]: request mode sends add password instructions; token mode sets the password.
     * @returns {Promise<void>}
     */
    async function addPasswordMain() {
        await accountPage("IconAuthAddPassword", _TF("Add a password, so you can also sign in with your user name and a password.", "One line description of the add password page"), async page => {
            const token = A.QueryToken();
            if (!token)
                return requestCode(page, {
                    Busy: _TF("Sending instructions ..", "Message displayed when sending instructions using email and/or sms"),
                    Failed: _TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"),
                    Send: () => UM.SendAddPasswordRequest(),
                    Link: "AddPassword.html?",
                });
            const link = await U.CheckLinkToken("AddPassword", { Page: page, Token: token });
            if (!link.Live)
                return;
            const data = await UM.NewPasswordData(token).catch(() => null);
            if (!data)
                return badToken(page, "AddPassword", link);
            await newPasswordPage(page, {
                Token: token, Kind: "AddPassword", Data: data,
                Heading: _TF("Choose password", "A message letting a user know that they should provide a new password"),
                ButtonText: _TF("Add password", "Text of a button that when clicked will add a new password"),
                ButtonTitle: _TF("Add the password and login", "Tool tip description on a button that when clicked will add a new password and perform a login"),
                Busy: _TF("Adding password..", "Message displayed when a user is adding a password"),
                Done: _TF("Password added", "Message displayed when a user added a password successfully"),
                Failed: _TF("Failed to add password.", "Error message displayed when adding a user password failed"),
                Save: async (pw, salt) => (await UM.ResetPassword(token, pw, salt))?.Error == 0,
            });
        });
    }

    // ------------------------------------------------------------------ set password

    /**
     * SetPassword.html: change the password of the signed in user (current + new); stays on the page.
     * @returns {Promise<void>}
     */
    async function setPasswordMain() {
        await accountPage("IconAuthSetPassword", _TF("Change the password of your account.", "One line description of the set (change) password page"), async page => {
            //  (the signed in user is asked when the form is sent, as the old page: the form loads with the policy only)
            const policy = await UM.CreatePolicy();
            const cur = currentPassword(page, () => update());
            const pwd = newPassword(page, policy, _TF("New password", "Label text for an input box where the user should enter a new password"), () => update());
            const rules = U.Rules ? new U.Rules(page, policy, pwd) : null;
            const status = new U.Status(page);
            const button = U.Button(U.Buttons(page), {
                Text: _TF("Set password", "Text of a button that when clicked will set a new password"),
                Title: _TF("Change the password", "Tool tip description on a button that when clicked will set a new password"),
                Icon: "IconAuthPassword", Submit: true, Disabled: fillFirst(),
            }, page);
            const update = liveValidation(status, button, async () => {
                if (!A.Trim(cur.Get()))
                    return { Error: _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), Field: cur, Neutral: true };
                const p = A.Trim(pwd.Get());
                rules?.Update(p);
                if (!p)
                    return { Error: noPassword(), Field: pwd };
                return { Error: A.CheckPolicy(p, policy), Field: pwd, Leak: p };
            }, [cur, pwd]);
            page.OnSubmit = () => submit({
                Status: status, Update: update, Focus: cur,
                Failed: _TF("Failed to change password.", "Error message displayed when a user tried to change their password but didn't succeed"),
                Run: async () => {
                    status.Set(_TF("Changing password ..", "Message displayed when the users password is being changed"), "info");
                    const username = await UM.UserName();
                    if (!await UM.SetPassword(username, cur.Get(), pwd.Get()))
                        return false;
                    status.Set(_TF("Password set.", "Message displayed when a password have been set successfully"), "ok");
                    button.SetDisabled(true, waitReason());
                    return true;
                },
            });
            await update();
            cur.Focus();
        });
    }

    // ------------------------------------------------------------------ e-mail / phone

    /**
     * Add / change an e-mail address or phone number: request mode = current password + the new value, then the short
     * code step; token mode = a confirm button.
     * @param {object} page The AuthPage.
     * @param {object} o Options: { IsPhone, Token, Link, Confirm (tokenConfirm options), Label, Placeholder, Hint,
     * ButtonText, ButtonTitle, ButtonIcon, Send(user, password, value) }.
     * @returns {Promise<void>}
     */
    async function contactPage(page, o) {
        if (o.Token)
            return tokenConfirm(page, o.Token, o.Confirm);
        //  The current password is checked against the policy of new passwords, as before (GET GetCreatePasswordPolicy on load); the
        //  signed in user is asked when the form is sent, as the old page (Admin N34: no "No user signed in!" page on load)
        const policy = await UM.CreatePolicy();
        const cur = currentPassword(page, () => update());
        const fo = {
            Label: o.Label, Placeholder: o.Placeholder, Hint: o.Hint, Name: o.IsPhone ? "tel" : "email",
            OnInput: () => update(),
        };
        const value = o.IsPhone
            ? new U.PhoneField(page, fo)
            : new U.Field(page, Object.assign(fo, { Type: "email", AutoComplete: "email", InputMode: "email" }));
        const status = new U.Status(page);
        const button = U.Button(U.Buttons(page), { Text: o.ButtonText, Title: o.ButtonTitle, Icon: o.ButtonIcon, Submit: true, Disabled: fillFirst() }, page);
        const check = sequenced(o.IsPhone ? A.PhoneError : A.EmailError);
        const update = liveValidation(status, button, async () => {
            const bad = currentPasswordProblem(cur, policy);
            if (bad)
                return bad;
            const v = A.Trim(value.Get());
            if (!v)
                return { Error: o.Placeholder, Field: value, Neutral: true };
            const e = await check(v);
            return { Error: e === undefined ? "" : e, Field: value };
        }, [cur, value]);
        page.OnSubmit = () => submit({
            Status: status, Update: update, Focus: value,
            Failed: _TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"),
            Run: async () => {
                status.Set(_TH("Sending instructions to {0}..", U.UserHtml(value.Get()), "Message displayed when sending instructions using email.{0} is replaced with the email address"), "info", { Html: true });
                const username = await UM.UserName();
                const targets = await o.Send(username, cur.Get(), value.Get());
                if (!targets)
                    return false;
                U.ShortCode(page, {
                    Html: _TH("Code sent to {0}.", U.UserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                    Link: o.Link,
                });
                return true;
            },
        });
        await update();
        cur.Focus();
    }

    /**
     * Delete the e-mail address / phone number: current password, then "Sent restore instructions to ...".
     * @param {object} page The AuthPage.
     * @param {object} o Options: { ButtonText, ButtonTitle, ButtonIcon, Busy, Failed, Done(targetsHtml), Delete(user, password) }.
     * @returns {Promise<void>}
     */
    async function deleteContactPage(page, o) {
        //  (the signed in user is asked when the form is sent, as the old page: the form loads with the policy only)
        const policy = await UM.CreatePolicy();
        const cur = currentPassword(page, () => update());
        const status = new U.Status(page);
        const button = U.Button(U.Buttons(page), { Text: o.ButtonText, Title: o.ButtonTitle, Icon: o.ButtonIcon, Submit: true, Disabled: fillFirst(), Kind: "danger" }, page);
        const update = liveValidation(status, button, async () => currentPasswordProblem(cur, policy) || { Error: null }, [cur]);
        page.OnSubmit = () => submit({
            Status: status, Update: update, Focus: cur, Failed: o.Failed,
            Run: async () => {
                status.Set(o.Busy, "info");
                const username = await UM.UserName();
                const targets = await o.Delete(username, cur.Get());
                if (!targets)
                    return false;
                status.Set(o.Done(U.UserHtml(targets)), "ok", { Html: true });
                button.SetDisabled(true, waitReason());
                return true;
            },
        });
        await update();
        cur.Focus();
    }

    /**
     * AddEmail.html[?<token|code>].
     * @returns {Promise<void>}
     */
    async function addEmailMain() {
        await accountPage("IconAuthAddEmail", _TF("Add an email address to your account. A verification code is sent to it.", "One line description of the add email page"), page => contactPage(page, {
            Token: A.QueryToken(), Link: "AddEmail.html?",
            Confirm: {
                Kind: "AddEmail", Facts: emailFacts,
                Text: _TF("Add email address", "Text on a button that when clicked will add (associate) the email address to the account"),
                Title: _TF("Associate the email address with your account.", "Tool tip description for a button that when clicked will add (associate) the email address to the account"),
                Icon: "IconAuthAddEmail",
                Busy: _TF("Adding email address ..", "Message displayed when an email address is being associated with an account"),
                Done: _TF("Email address added", "Message displayed when an email address was successfully associated with an account"),
                Failed: _TF("Failed to add email address.", "Error message when the server failed to associate an email address with an account"),
                Call: t => UM.AddEmail(t),
            },
            Label: _TF("Email address", "Label text for an input box where the user should input a valid email address"),
            Placeholder: _TF("Enter your email address", "Place holder text for an input field where the user should enter n email address"),
            ButtonText: _TF("Verify email address", "Text on a button that when clicked will send a verfication message to an email address"),
            ButtonTitle: _TF("Send verification instructions to the new email address", "Tool tip description for a button that when clicked will send a verfication message to an email address"),
            ButtonIcon: "IconAuthEmail",
            Send: (u, p, v) => UM.SendAddEmailRequest(u, p, v),
        }));
    }

    /**
     * ChangeEmail.html[?<token|code>].
     * @returns {Promise<void>}
     */
    async function changeEmailMain() {
        await accountPage("IconAuthChangeEmail", _TF("Change the email address of your account. A verification code is sent to the new address.", "One line description of the change email page"), page => contactPage(page, {
            Token: A.QueryToken(), Link: "ChangeEmail.html?",
            Confirm: {
                Kind: "AddEmail", Facts: emailFacts,
                Text: _TF("Change email address", "Text on a button that if clicked will change the email address associated with an account"),
                Title: _TF("Change the email address associated with your account.", "Tool tip description for a button that if clicked will change the email address associated with an account"),
                Icon: "IconAuthChangeEmail",
                Busy: _TF("Changing email address ..", "Message displayed when the server changes a users email address"),
                Done: _TF("Email address changed", "Message displayed when an email address of an account have been successfully changed"),
                Failed: _TF("Failed to change email address.", "Error message displayed when changing an email address on an account fail"),
                Call: t => UM.AddEmail(t),
            },
            Label: _TF("New email address", "Label for an input field where the user should enter a new email address"),
            Placeholder: _TF("Enter your new email address", "Placeholder text for an input field where the user should enter a new email address"),
            ButtonText: _TF("Verify email address", "Text on a button that when clicked will send a verfication message to an email address"),
            ButtonTitle: _TF("Send verification instructions to the new email address", "Tool tip description for a button that when clicked will send a verfication message to an email address"),
            ButtonIcon: "IconAuthEmail",
            Send: (u, p, v) => UM.SendChangeEmailRequest(u, p, v),
        }));
    }

    /**
     * DeleteEmail.html.
     * @returns {Promise<void>}
     */
    async function deleteEmailMain() {
        await accountPage("IconAuthDeleteEmail", _TF("Remove the email address from your account.", "One line description of the delete email page"), page => deleteContactPage(page, {
            ButtonText: _TF("Delete email address", "Text on a button that when pressed will delete the email address associated with a user"),
            ButtonTitle: _TF("Remove the email address associated with this account", "Tool tip description for a button that when pressed will delete the email address associated with a user"),
            ButtonIcon: "IconAuthDeleteEmail",
            Busy: _TF("Deleting email address..", "Message displayed when an email address is being deleted"),
            Failed: _TF("Failed to delete email address.", "Error message displayed when the deletion of an email address failed"),
            Done: h => _TH("Sent restore instructions to {0}.", h, "Message displayed when an email address is deleted from an account.{0} is the email address that was deleted"),
            Delete: (u, p) => UM.DeleteEmail(u, p),
        }));
    }

    /**
     * AddPhone.html[?<token|code>].
     * @returns {Promise<void>}
     */
    async function addPhoneMain() {
        await accountPage("IconAuthAddPhone", _TF("Add a phone number to your account. A verification code is sent to it.", "One line description of the add phone page"), page => contactPage(page, {
            IsPhone: true, Token: A.QueryToken(), Link: "AddPhone.html?",
            Confirm: {
                Kind: "AddPhone", Facts: phoneFacts,
                Text: _TF("Add phone number", "Text on a button that when clicked will associate a phone number with an account"),
                Title: _TF("This button will associate the phone number with your account.", "Tool tip description for a button that when clicked will associate a phone number with an account"),
                Icon: "IconAuthAddPhone",
                Busy: _TF("Adding phone number ..", "Message displayed when the server is associating a phone number with an account"),
                Done: _TF("Phone number added", "Message displayed when a phone number was successfully associated with an acount"),
                Failed: _TF("Failed to add phone number.", "Error message displayed when the server failed to associate a phone number with an account"),
                Call: t => UM.AddPhone(t),
            },
            Label: _TF("Phone number", "Label text for an input field where the user should enter their phone number"),
            Placeholder: _TF("Enter your phone number", "Placeholder text for an input field where the user should enter their phone number"),
            Hint: _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number"),
            ButtonText: _TF("Verify phone number", "Text on a button then when pressed will send a verification text message to the supplied phone number"),
            ButtonTitle: _TF("Send a verification instructions to the new phone number", "Tool tip description for a button then when pressed will send a verification text message to the supplied phone number"),
            ButtonIcon: "IconAuthPhone",
            Send: (u, p, v) => UM.SendAddPhoneRequest(u, p, v),
        }));
    }

    /**
     * ChangePhone.html[?<token|code>].
     * @returns {Promise<void>}
     */
    async function changePhoneMain() {
        await accountPage("IconAuthChangePhone", _TF("Change the phone number of your account. A verification code is sent to the new number.", "One line description of the change phone page"), page => contactPage(page, {
            IsPhone: true, Token: A.QueryToken(), Link: "ChangePhone.html?",
            Confirm: {
                Kind: "AddPhone", Facts: phoneFacts,
                Text: _TF("Change phone number", "Text on a button that when clicked will change the phone number assoicated with a user"),
                Title: _TF("This button will change the phone number.", "Tool til description for a button that when clicked will change the phone number assoicated with a user"),
                Icon: "IconAuthChangePhone",
                Busy: _TF("Changing phone number ..", "Message displayed while the server is changing the phone number associated with an account"),
                Done: _TF("Phone number changed", "Message displayed when the phone number of an account have successfully been changed"),
                Failed: _TF("Failed to change phone number.", "Error message displayed when a request for changing the phone number associated with an account failed"),
                Call: t => UM.AddPhone(t),
            },
            Label: _TF("Phone number", "Label text for an input field where the user should enter their phone number"),
            Placeholder: _TF("Enter your phone number", "Placeholder text for an input field where the user should enter their phone number"),
            Hint: _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number"),
            ButtonText: _TF("Verify phone number", "Text on a button then when pressed will send a verification text message to the supplied phone number"),
            ButtonTitle: _TF("Send a verification instructions to the new phone number", "Tool tip description for a button then when pressed will send a verification text message to the supplied phone number"),
            ButtonIcon: "IconAuthPhone",
            Send: (u, p, v) => UM.SendChangePhoneRequest(u, p, v),
        }));
    }

    /**
     * DeletePhone.html.
     * @returns {Promise<void>}
     */
    async function deletePhoneMain() {
        await accountPage("IconAuthDeletePhone", _TF("Remove the phone number from your account.", "One line description of the delete phone page"), page => deleteContactPage(page, {
            ButtonText: _TF("Delete phone number", "Text on a button that when pressed will remove the phone number associated with an account"),
            ButtonTitle: _TF("Remove the phone number associated with this account", "Tool tip description for a button that when pressed will remove the phone number associated with an account"),
            ButtonIcon: "IconAuthDeletePhone",
            Busy: _TF("Deleting phone number ..", "Text displayed when the server is deleting a phone number from an account"),
            Failed: _TF("Failed to delete phone number.", "Error message displayed when the server failed to delete a phone number from an account"),
            Done: h => _TH("Sent restore instructions to {0}.", h, "Text displayed when a phone number is deleted from an account.{0} is replaced with the phone number"),
            Delete: (u, p) => UM.DeletePhone(u, p),
        }));
    }

    // ------------------------------------------------------------------ delete password / account

    /**
     * DeletePassword.html[?<token|code>]: request mode sends instructions; token mode confirms the deletion.
     * @returns {Promise<void>}
     */
    async function deletePasswordMain() {
        await accountPage("IconAuthDeletePassword", _TF("Remove the password from your account (then you sign in another way, ex: with a passkey).", "One line description of the delete password page"), async page => {
            const token = A.QueryToken();
            if (!token)
                return requestCode(page, {
                    Busy: _TF("Sending instructions on how to delete your password..", "Message displayed when the server is sedning instructions on how to delete a password"),
                    Failed: _TF("Failed to send instuctions on how to delete your password", "Error message displayed when the server failed to send instructions on how to delete a password"),
                    Send: () => UM.SendDeletePasswordRequest(),
                    Link: "DeletePassword.html?",
                });
            return tokenConfirm(page, token, {
                Kind: "DeletePassword", Facts: accountFacts,
                Text: _TF("Delete password", "Text on a button that when clicked will delete the users password"),
                Title: _TF("This button will delete the current password.", "Tool tip description on a button that when clicked will delete the users password") + "\n" +
                    _TF("A new password will have to be added to sign in with a password.", "Tool tip description on a button that when clicked will delete the users password") + "\n" +
                    _TF("Just close this page to keep your existing password.", "Tool tip description on a button that when clicked will delete the users password"),
                Icon: "IconAuthDeletePassword", Danger: true,
                Busy: _TF("Deleting password ..", "Message displayed when a users password is being deleted"),
                Done: _TF("Password deleted", "Message displayed when a users password was deleted successfully"),
                Failed: _TF("Failed to delete password.", "Error message displayed when a users password couldn't be deleted"),
                Call: t => UM.DeletePassword(t),
            });
        });
    }

    /**
     * DeleteAccount.html[?<code>|?<token>|?token=<t> (older links)]: request mode sends instructions; token mode deletes the account.
     * @returns {Promise<void>}
     */
    async function deleteAccountMain() {
        const token = A.QueryToken();
        await accountPage(token ? "IconAuthDeleteAccount" : "IconAuthDeleteMail", _TF("Delete your account and all its data. You get instructions first.", "One line description of the delete account page"), async page => {
            //  (request mode: the code is sent when the page opens, as before; nothing is deleted before the code is confirmed)
            if (!token)
                return requestCode(page, {
                    Busy: _TF("Sending remove account instructions ..", "Message displayed when sending instructions on how to delete a user account"),
                    Failed: _TF("Failed to send remove account instructions!", "Error message displayed when failing to send instructions on how to delete a user account"),
                    Send: () => UM.SendDeleteUserRequest(),
                    Link: "DeleteAccount.html?",
                });
            return tokenConfirm(page, token, {
                Kind: "DeleteAccount", Facts: accountFacts,
                Text: _TF("Delete account", "Text on a button that when pressed will delete the a user's account"),
                Title: _TF("Deleting removes your account and all its data. You are asked to confirm first.", "Note under the Delete account button of the delete account page") + "\n" +
                    _TF("Just close this page to keep your account.", "Part of a tool tip for a button that when pressed will delete the a user account"),
                Icon: "IconAuthDeleteAccount", Danger: true,
                Busy: _TF("Deleting account ..", "Text displayed while a user account is being deleted"),
                Done: _TF("Account deleted", "Text displayed when a user account was successfully deleted"),
                Failed: _TF("Failed to delete account.", "Error message displayed when the deletion of a user account failed"),
                //  The end of the page: what happened and where to go next (Admin N23: "Account deleted" was a dead end); the session of
                //  the deleted account has ended, so the start page opens in the whole window (the shell reloads as a visitor)
                After: (pg, link) => U.Done(pg, {
                    Title: _TF("Your account is deleted", "Heading of the delete account page after the account was deleted"),
                    Text: link && link.Account
                        ? _T("{0} and all data stored for it were removed, and you are signed out. You can use this site as a visitor, or sign up again.", link.Account, "Text of the delete account page after the account was deleted.{0} is replaced with the user name of the account")
                        : _TF("Your account and all data stored for it were removed, and you are signed out. You can use this site as a visitor, or sign up again.", "Text of the delete account page after the account was deleted, when the account name is not known"),
                }),
                //  One click never deletes: a danger dialog that names the account (the link's account, from the check on load), and the
                //  account name (or "delete" when it is not known, ex: a typed code) must be typed. No request for the name (Admin N40:
                //  the original page sent nothing before DeleteUser)
                Confirm: async link => {
                    const name = link && link.Account ? link.Account : null;
                    return Sw.Cells.ConfirmDanger({
                        Title: _TF("Delete your account?", "Title of the confirmation dialog of the delete account page"),
                        ItemLabel: _TF("Account:", "Label before the account name in the confirmation dialog of the delete account page"),
                        Item: name || undefined,
                        Text: _TF("Your account, its sign in methods and all data stored for it are removed. You can't sign in with it again.", "Text of the confirmation dialog of the delete account page"),
                        Confirm: name || _TF("delete", "The word to type to confirm the deletion of an account when the account name is not known"),
                        OkText: _TF("Delete account", "Text on a button that when pressed will delete the a user's account"),
                        OkIcon: "IconAuthDeleteAccount",
                    });
                },
                Call: t => UM.DeleteUser(t),
            });
        });
    }

    /**
     * UndoEmail.html?<token>: the link of the notice to the old e-mail address after a change ("Not you? Undo this change"). No sign in (the
     * account may be taken): one button gives the account back to the old address, signs everyone out and sends a reset link there.
     * Opening the page changes nothing (mail scanners open links).
     * @returns {Promise<void>}
     */
    async function undoEmailMain() {
        const token = A.QueryToken();
        await U.Page("IconRecover", async page => {
            if (!token)
                return badToken(page, "UndoEmail");
            return tokenConfirm(page, token, {
                Kind: "UndoEmail",
                Facts: link => link.Target
                    ? (link.Account
                        ? _T("The account {0} gets the email address {1} back.", link.Account, link.Target, "Facts on the undo e-mail change page.{0} is replaced with the account name, {1} with the old e-mail address that the account gets back")
                        : _T("The account gets the email address {0} back.", link.Target, "Facts on the undo e-mail change page when the account name is not known.{0} is replaced with the old e-mail address that the account gets back"))
                    : null,
                Text: _TF("Undo this change", "Text of the button of the undo e-mail change page"),
                Title: _TF("Everyone is signed out, the current password stops working, and passkeys added in the day before the change or later are removed. Then we send a link to this email address to choose a new password.", "Note under the button of the undo e-mail change page"),
                Icon: "IconRecover",
                Busy: _TF("Giving the account back ..", "Text displayed while an e-mail address change is being undone"),
                Done: _TF("Done", "Text displayed when an e-mail address change was undone"),
                Failed: _TF("Failed to undo the change.", "Error message displayed when an e-mail address change could not be undone"),
                After: (pg, link) => U.Done(pg, {
                    Title: _TF("The change is undone", "Heading of the undo e-mail change page after the change was undone"),
                    Text: link && link.Target
                        ? _T("We sent a link and a code to {0}. Open it to choose a new password, then sign in. Check the phone number and the passkeys of the account.", link.Target, "Text of the undo e-mail change page after the change was undone.{0} is replaced with the e-mail address the reset link was sent to")
                        : _TF("We sent a link and a code to your email address. Open it to choose a new password, then sign in. Check the phone number and the passkeys of the account.", "Text of the undo e-mail change page after the change was undone, when the address is not known"),
                }),
                Call: t => UM.UndoEmailChange(t),
            });
        });
    }

    // ------------------------------------------------------------------ sign up / forgot password

    /**
     * SignUp.html: choose e-mail or phone (SignUpMethods), send the sign up message, then the short code step
     * leading to ChoosePassword.html?<code>.
     * @returns {Promise<void>}
     */
    async function signUpMain() {
        await U.Page("IconAuthSignUp", async page => {
            // The server names the phone coms "Sms" and "WhatsApp" (older servers: "Phone"); all of them sign up with a phone number: one tab.
            const methods = [...new Set(((await UM.SignUpMethods()) || []).map(m => (m === "Sms" || m === "WhatsApp") ? "Phone" : m))].filter(m => m === "Email" || m === "Phone");
            if (methods.length === 0) {
                const m = _TF("No supported sign up methods found!", "Error message displayed when there are no supported sign up methods");
                new U.Status(page).Set(m, "error");
                return;
            }
            const seg = methods.length > 1 ? new U.Segments(page, {
                Label: _TF("The methods that can be used to sign up", "The description of a header about what sign up methods can be used"),
                OnChange: i => show(i),
            }) : null;
            const fields = methods.map(m => {
                if (m === "Email") {
                    seg?.Add(_TF("Email", "Text of a tab header for a tab where the user should enter a valid email address in order to sign up"), "IconAuthEmail",
                        _TF("Sign up using your email address", "Tool tip description on a tab header for a tab where the user should enter a valid email address in order to sign up"));
                    const f = new U.Field(page, {
                        Label: _TF("Email", "Text of a tab header for a tab where the user should enter a valid email address in order to sign up"),
                        Placeholder: _TF("Enter your email address", "Placeholder text on an input box where users are expected to enter their email address"),
                        Type: "email", AutoComplete: "email", InputMode: "email", Name: "email", OnInput: () => update(),
                    });
                    f.Check = sequenced(A.EmailError);
                    f.Empty = _TF("Enter a valid email address", "Text displayed to let users know that they should enter a valid email address");
                    return f;
                }
                seg?.Add(_TF("Phone", "Text of a tab header for a tab where the user should enter a valid phone number in order to sign up"), "IconAuthPhone",
                    _TF("Sign up using your phone number", "Tool tip description on a tab header for a tab where the user should enter a valid phone number in order to sign up"));
                const f = new U.PhoneField(page, {
                    Label: _TF("Phone", "Text of a tab header for a tab where the user should enter a valid phone number in order to sign up"),
                    Placeholder: _TF("Enter your phone number", "Placeholder text on an input box where users are expected to enter their phone number"),
                    Hint: _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number"),
                    Name: "tel", OnInput: () => update(),
                });
                f.Check = sequenced(A.PhoneError);
                f.Empty = _TF("Enter a valid phone number", "Text displayed to let users know that they should enter a valid phone number");
                return f;
            });
            const status = new U.Status(page);
            const button = U.Button(U.Buttons(page), {
                Text: _TF("Sign up", "Text on a button that when pressed will sign up to a service using the supplied user id (phone number or email address)"),
                Title: _TF("Send an message with further instructions", "Tool tip on a button that when pressed will sign up to a service using the supplied user id (phone number or email address)"),
                Icon: "IconAuthPost", Submit: true, Disabled: fillFirst(),
            }, page);
            let current = 0;
            const update = liveValidation(status, button, async () => {
                const f = fields[current];
                const v = A.Trim(f.Get());
                if (!v)
                    return { Error: f.Empty, Field: f, Neutral: true };
                const e = await f.Check(v);
                return { Error: e === undefined ? "" : e, Field: f };
            }, fields);
            /**
             * Show the field of a method.
             * @param {number} i Index.
             */
            function show(i) {
                current = i;
                fields.forEach((f, j) => f.E.hidden = j !== i);
                update();
                fields[i].Focus();
            }
            page.OnSubmit = () => submit({
                Status: status, Update: update, Focus: fields[current],
                Failed: _TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"),
                Run: async () => {
                    seg?.SetDisabled(true, waitReason());
                    try {
                        const v = fields[current].Get();
                        status.Set(_TH("Sending instructions to {0} ..", U.UserHtml(v), "Message displayed when sending sign up instructions.Text must be safe to use in HTML.{0} is replaced with the user id (phone number or email address)"), "info", { Html: true });
                        if (!await UM.SignUp(v))
                            return false;
                        U.ShortCode(page, {
                            Html: _TH("Code sent to {0}.", U.UserHtml(v), "Message displayed when sign up instructions have been sent.Text must be safe to use in HTML.{0} is replaced with the user id (phone number or email address)"),
                            Link: "ChoosePassword.html?",
                        });
                        return true;
                    }
                    finally {
                        seg?.SetDisabled(false);
                    }
                },
            });
            //  The second method is the initial choice (as before), when there is one
            const first = fields.length > 1 ? 1 : 0;
            seg?.Select(first, true);
            show(first);
        });
    }

    /**
     * ForgotPassword.html: user id -> reset instructions -> short code step leading to ResetPassword.html?<code>.
     * @returns {Promise<void>}
     */
    async function forgotPasswordMain() {
        await U.Page("IconAuthForgotPassword", async page => {
            const user = new U.Field(page, {
                Label: _TF("User ID", "Label text for an input field where the user should enter their user id (username, email, phone)"),
                Placeholder: _TF("Enter your user id", "Placeholder text for an input field where the user should enter their user id (username, email, phone)"),
                Hint: _TF("Your user id is your email address, phone number or username","Tool tip description for an input field where the user should enter their user id (username, email, phone)"),
                AutoComplete: "username", Name: "username", OnInput: () => update(),
            });
            const status = new U.Status(page);
            const buttons = U.Buttons(page);
            const button = U.Button(buttons, {
                Text: _TF("Send reset instructions", "Text of a button that when pressed will request a password reset"),
                Title: _TF("Send instructions on how to reset your password", "Tool tip description of a button that when pressed will request a password reset"),
                Icon: "IconAuthPost", Submit: true, Disabled: fillFirst(),
            }, page);
            //  The code belongs to the account, not to the browser that asked: a code can be entered here in any browser (ex: asked on
            //  a computer, the text message read on the phone)
            const again = {
                Text: _TF("Ask for a new code", "Button of the password reset code step, shown when the code no longer works: opens the forgot password page again"),
                Title: _TF("Open the forgot password page again to ask for a new code", "Tool tip of the button of the password reset code step that opens the forgot password page again to ask for a new code"),
                Url: "ForgotPassword.html",
            };
            U.Button(buttons, {
                Text: _TF("I have a code", "Text of a button on the forgot password page that opens the step where a reset code that was already received is entered"),
                Title: _TF("Enter a password reset code that you already received, also one that was asked for in another browser", "Tool tip of the button on the forgot password page that opens the step where a reset code that was already received is entered"),
                Icon: "IconAuthResetPassword",
                OnClick: () => {
                    const u = A.Trim(user.Get());
                    if (!u) {
                        status.Set(_TF("Enter your user id first: the code belongs to that account", "Error message on the forgot password page when the user wants to enter a received code but has not entered their user id"), "error");
                        user.SetInvalid(true);
                        user.Focus();
                        return;
                    }
                    U.ShortCode(page, {
                        Html: _TH("Enter the password reset code that was sent for {0}. A code works for 15 minutes, in any browser.", U.UserHtml(u), "Message of the step where a password reset code that was already received is entered.{0} is replaced with the user id that was entered"),
                        Link: "ResetPassword.html?",
                        Validate: code => UM.ValidateResetCode(u, code),
                        Again: again,
                    });
                },
            }, page);
            const update = liveValidation(status, button, async () => A.Trim(user.Get())
                ? { Error: null }
                : { Error: _TF("Enter a valid user id", "Message letting users know that they are expected to enter their user id in an input field"), Field: user, Neutral: true }, [user]);
            page.OnSubmit = () => submit({
                Status: status, Update: update, Focus: user,
                Failed: _TF("Failed to send password reset instructions.", "Error message displayed when the server failed to sent password reset instructions"),
                Run: async () => {
                    const u = user.Get();
                    status.Set(_TH("Sending password reset instructions for {0}", U.UserHtml(u), "Text displayed when a password reset email or sms message is being sent.{0} is replaced with the user name specified"), "info", { Html: true });
                    const targets = await UM.ForgotPassword(u);
                    if (!targets)
                        return false;
                    //  The server answers the same whether the user id has an account or not (it would tell who has one)
                    U.ShortCode(page, {
                        Html: _TH("If an account uses {0}, we sent a code to the email address or phone number of that account. It can take a minute. Enter the code here, or open the link in the message. No message after a few minutes? Check the user id, or contact this site's support.", U.UserHtml(targets), "Message displayed after password reset instructions were requested (the server never tells whether the user id has an account, and asking again within minutes sends nothing new).{0} is replaced with the user id that was entered"),
                        Link: "ResetPassword.html?",
                        //  The code belongs to the account (any code sent in the last 15 minutes works); the server knows the user id of this session
                        Validate: code => UM.ValidateResetCode(null, code),
                        //  A way to ask for a new code (not a dead end)
                        Again: again,
                    });
                    return true;
                },
            });
            await update();
            user.Focus();
        });
    }

    // ------------------------------------------------------------------ invite

    /**
     * InviteUser.html (role InviteUser): e-mail, optional user name and (when the inviter has any) security tokens.
     * @returns {Promise<void>}
     */
    async function inviteUserMain() {
        await accountPage("IconAuthInviteUser", _TF("Invite a new user by email.", "One line description of the invite user page"), async page => {
            const me = await A.GetUser();
            if (!me) {
                page.SetIcon("IconWarning");
                new U.Status(page).Set(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"), "warning");
                return;
            }
            //  Enter walks the fields to Send invitation, as the old page: it never sends from a field (two Enters must not invite
            //  someone before the user name and tokens were looked at)
            page.EnterWalks = true;
            //  A restored draft (the Draft option) calls the input handlers while the fields are made: they act once the form is built
            let built = false;
            const email = new U.Field(page, {
                Label: _TF("Email", "Label text for an input field where the user should supply an email address"),
                Placeholder: _TF("Email address of the person to invite", "Placeholder text for an input field where the user should supply an email address"),
                Type: "email", AutoComplete: "off", InputMode: "email", Name: "invite-email", Draft: { Context: "invite.email", Dirty: false }, OnInput: () => {
                    if (built) {
                        update();
                        markDirty();
                    }
                },
            });
            const nameTitle = _TF("An optional username that can be used to sign in, instead of the email address", "Tool tip description on an input field where the user can enter a username");
            const name = new U.Field(page, {
                Label: _TF("Username", "Label text for an input field where the user can enter an optional username"),
                Placeholder: _TF("An optional username", "Placeholder text for an input field where the user can enter an optional username"),
                Hint: nameTitle, AutoComplete: "off", Name: "invite-name", Draft: { Context: "invite.name", Dirty: false }, OnInput: () => built && markDirty(),
            });
            name.Input.required = false;
            //  Unsent invitation (UX guide SAF-10 / SAF-11): leaving asks, the typed address and username are kept as drafts on this
            //  device (restored with "Draft restored" after a reload or a closed tab), cleared when the invitation is sent
            let sent = false;
            //  While the invitation is sent, the leave question says one thing: the send's own line ("The invitation to … may not be
            //  sent."), not also "is not sent" of the unsent form (Admin N5: two contradicting texts)
            let sending = false;
            const markDirty = () => {
                const to = A.Trim(email.Get());
                const any = !sent && !sending && (to || A.Trim(name.Get()) || (tokens && tokens.Get().length > 0));
                Sw.Page.SetDirty("invite", any
                    ? (to
                        ? _T("Your invitation to {0} is not sent.", to, "Text of the question 'Leave without saving?' on the invite user page.{0} is replaced with the typed email address")
                        : _TF("Your invitation is not sent.", "Text of the question 'Leave without saving?' on the invite user page when no email address is typed yet"))
                    : null);
            };
            //  security tokens as a picker (pressable chips), from the tokens the server lets this user give (InviteUser):
            //  the user's own tokens; Debug may give every standard role, Admin every standard role but Debug
            let tokens = null;
            const own = (me.Tokens || []).map(x => String(x).trim()).filter(x => x);
            const lower = own.map(x => x.toLowerCase());
            const names = new Map();
            //  The standard roles by their usual name ("Admin", not the lower cased token "admin")
            const known = ["Admin", "Ops", "Dev", "UserManagement", "Debug"];
            const add = t => {
                const k = t.toLowerCase();
                if (!names.has(k))
                    names.set(k, known.find(x => x.toLowerCase() === k) || t);
            };
            own.forEach(add);
            if (lower.includes("debug") || lower.includes("admin"))
                ["Admin", "Ops", "Dev", "UserManagement"].forEach(add);
            if (lower.includes("debug"))
                add("Debug");
            if (names.size > 0) {
                const hint = _TF("The new user gets the security tokens (roles) you select. You can only give tokens that you may give.", "Tool tip of the security token picker on the invite user page");
                const label = Sw.el("span.sw-text-label", { id: "invite-tokens-label", text: _TF("Security tokens", "Label text for an input field where the user can enter auth tokens to give to a new user") });
                const chips = [...names.values()].map(t => new Sw.Chip({ Text: t, Title: _T("Give the new user the {0} token", t, "Tool tip of a security token in the picker of the invite user page.{0} is replaced with the token name"), OnChange: () => markDirty() }));
                const list = Sw.el("div.um-token-list", { attrs: { role: "group", "aria-labelledby": "invite-tokens-label" } }, ...chips.map(c => c.E));
                const e = Sw.el("div.auth-field.um-tokens", { title: hint }, label, list,
                    Sw.el("div.um-token-hint", { text: _TF("Optional. Without a token the new user can only use the pages open to every signed in user.", "Help line below the security token picker on the invite user page") }));
                (page.Box || page.E || document.body).append(e);
                tokens = { Get: () => chips.filter(c => c.Get()).map(c => c.O.Text) };
                //  Enter walks as on the old page, where the tokens were a text field (Admin N39: e-mail, user name, tokens, Send = 4 Enters):
                //  Enter in the user name goes to the tokens, Enter on a token goes to Send invitation (Space selects a token)
                const isEnter = ev => ev.key === "Enter" && Sw.IsPureEvent(ev) && !ev.isComposing;
                name.Input.addEventListener("keydown", ev => {
                    if (!isEnter(ev) || !chips.length)
                        return;
                    ev.preventDefault();
                    ev.stopPropagation();
                    chips[0].E.focus();
                });
                list.addEventListener("keydown", ev => {
                    if (!isEnter(ev))
                        return;
                    ev.preventDefault();
                    ev.stopPropagation();
                    page.MainButton?.E.focus();
                }, true);
            }
            const status = new U.Status(page);
            const row = U.Buttons(page);
            const button = U.Button(row, {
                Text: _TF("Send invitation", "Text on a button that if pressed will send an invitation to join the site to a given email address"),
                Title: _TF("Send an invitation to the supplied email", "Tool tip description for a button that if pressed will send an invitation to join the site to a given email address"),
                Icon: "IconAuthPost", Submit: true, Disabled: fillFirst(),
            }, page);
            //  a way back to the Users page (Administration > Users, also the breadcrumb), and after sending: invite another
            //  person or see the pending invitations. No request for it (Admin N38: the original page sent GetUser only): who may
            //  invite (UserManagerRoles.InviteUser) may also see the users and the pending invitations
            const back = U.Button(row, {
                Text: _TF("Cancel", "Text of a button on the invite user page that goes back to the users page without sending"),
                Title: _TF("Back to the users page", "Tool tip of the cancel / back button on the invite user page"),
                Kind: "ghost",
                OnClick: () => Sw.Page.Navigate("Users.html", "_self"),
            });
            const again = U.Button(row, {
                Text: _TF("Invite another person", "Text of a button on the invite user page, after an invitation was sent, that clears the form"),
                Icon: "plus", Hidden: true,
                OnClick: () => Sw.Page.Navigate("InviteUser.html", "_self"),
            });
            const pending = U.Button(row, {
                Text: _TF("Pending invitations", "Text of a button on the invite user page, after an invitation was sent, that opens the pending invitations of the users page"),
                Kind: "ghost", Hidden: true,
                OnClick: () => Sw.Page.Navigate("Users.html?tab=invitations", "_self"),
            });
            const update = liveValidation(status, button, async () => {
                const v = A.Trim(email.Get());
                return Sw.Format.IsValidEmail(v)
                    ? { Error: null }
                    : v
                        ? { Error: _TF("Enter a valid email", "Message to let users know that they should enter a valid email address in the input field"), Field: email }
                        : { Error: _TF("Enter the email address of the person to invite.", "Neutral hint on the invite user page before an email address was typed"), Field: email, Neutral: true };
            }, [email]);
            page.OnSubmit = () => submit({
                Status: status, Update: update, Focus: email,
                Failed: _TF("Failed to send invitation.", "Error message displayed when an invitation to join the site failed to be sent"),
                Run: async () => {
                    const h = U.UserHtml(email.Get());
                    status.Set(_TH("Sending invitation to {0} ..", h, "Message disaplyed when an invitation to join the site is being sent.{0} is replaced with the email address"), "info", { Html: true });
                    //  Leaving while it is sent asks first (UX guide SAF-10: work that stops with the page)
                    sending = true;
                    markDirty();
                    Sw.Page.SetDirty("invite.send", _T("The invitation to {0} is being sent: if you leave now, it may not be sent.", A.Trim(email.Get()), "Text of the question 'Leave this page?' while an invitation is being sent.{0} is replaced with the email address"), { Work: true });
                    try {
                        if (!await UM.Invite(email.Get(), name.Get(), tokens ? tokens.Get() : null))
                            return false;
                    }
                    finally {
                        Sw.Page.ClearDirty("invite.send");
                        //  (not sent: the form is unsent again and asks before leaving; sent: `sent` clears it below)
                        sending = false;
                        markDirty();
                    }
                    sent = true;
                    for (const f of [email, name])
                        f.Box.ClearDraft?.();
                    markDirty();
                    status.Set(_TH("Invitation sent to {0}.", h, "Message displayed when an invitation to join the site have been sent.{0} is replaced with the email address"), "ok", { Html: true });
                    //  Done: the form is locked (the values say what was sent), the next steps replace Send / Cancel
                    const why = _TF("The invitation was sent: use Invite another person for a new one", "Reason shown on the locked fields of the invite user page after the invitation was sent");
                    button.SetDisabled(true, why);
                    again.E.hidden = false;
                    if (pending)
                        pending.E.hidden = false;
                    U.Hide(again, button, back);
                    email.SetDisabled?.(true, why);
                    name.SetDisabled?.(true, why);
                    page.Focus ? page.Focus(again.E) : again.E.focus();
                    return true;
                },
            });
            built = true;
            markDirty();
            await update();
            email.Focus();
        });
    }

    /** @namespace Sw.UserManagerUi - the account page entry points. */
    const pages = {
        signUpMain, choosePasswordMain, setPasswordMain, forgotPasswordMain, resetPasswordMain, addPasswordMain,
        deletePasswordMain, addEmailMain, changeEmailMain, deleteEmailMain, addPhoneMain, changePhoneMain, deletePhoneMain,
        deleteAccountMain, undoEmailMain, inviteUserMain,
    };
    Sw.UserManagerUi = pages;
    Object.keys(pages).forEach(k => window[k] = pages[k]);
})();
