/**
 * @file UserManagerUi.js - page logic of the self-service account pages of SysWeaver.MicroService.UserManager
 * (served as auth/...).
 *
 * Loading: `<script defer>` as the LAST script of all 15 HTML shells of this folder, after ../common/common.js,
 * Auth.js, AuthUi.js (SysWeaver.MicroService.Auth, same auth/ folder at runtime) and UserManager.js.
 * ChoosePassword.html and ResetPassword.html also load Passkey.js (SysWeaver.MicroService.PassKey) before Auth.js.
 * Each page calls exactly one entry point from `<body onload="...">`:
 * SetPassword -> setPasswordMain, ChoosePassword -> choosePasswordMain, DeleteAccount -> deleteAccountMain,
 * SignUp -> signUpMain, ForgotPassword -> forgotPasswordMain, ResetPassword -> resetPasswordMain,
 * AddPassword -> addPasswordMain, DeletePassword -> deletePasswordMain, InviteUser -> inviteUserMain,
 * ChangeEmail / AddEmail / DeleteEmail -> changeEmailMain / addEmailMain / deleteEmailMain,
 * ChangePhone / AddPhone / DeletePhone -> changePhoneMain / addPhoneMain / deletePhoneMain.
 *
 * Globals defined: only the global async *Main() functions above; no variables, nothing runs at load.
 * None of them is called from another folder (the server links to the HTML pages from the User menu and
 * from e-mail / text message links).
 *
 * Dependencies:
 * - AuthUi.js (SysWeaver.MicroService.Auth): AuthPage, AuthLabel, AuthInput, AuthInputPhone, AuthPassword,
 *   AuthNewPassword, AuthError, AuthText, AuthTab, AuthButton, AuthButtonRow, AuthSetText, AuthSetError,
 *   AuthUserHtml, AuthPageValidation (the shared short-code step, which in turn calls validateShortCode /
 *   cleanUpShortCode from UserManager.js), AuthStartPage.
 * - Auth.js (SysWeaver.MicroService.Auth): AuthTrim, AuthPolicyText, ValidatePassword, CheckPassword,
 *   AuthGetEmailError, AuthGetPhoneError.
 * - UserManager.js (this folder): all request wrappers (setPasswordRequest, addUserRequest, ...).
 * - Passkey.js (SysWeaver.MicroService.PassKey, optional, only checked with `typeof PassKey`): PassKey.IsSupported,
 *   PassKey.NewAccount, PassKey.AttachUsingToken, PassKey.Errors, PassKey.ErrorText.
 * - common.js: sendRequest, getRequest, getUrlParams, isValidEmail, _T / _TF / _TH (localization), Fail,
 *   SetTemporaryInnerText.
 * - Icon classes: UserManager.css (page icons), Auth.css (IconAuthPassword, IconAuthPost, IconAuthEmail,
 *   IconAuthPhone), Passkey.css (IconAddPasskey).
 *
 * Conventions:
 * - Every entry point wraps its work in AuthPage(icon, build): builds the floating auth box, shows the icon,
 *   calls build(box, img), calls PageLoaded once and passes exceptions thrown by build to Fail.
 * - Token pages read the token as `decodeURIComponent(window.location.search)` without the leading "?"
 *   (the whole query string is the long token or the session short code; no named parameters).
 * - "Standard form": AuthLabel + inputs, an info line `AuthError(target, "", policyText)`, an AuthButtonRow with
 *   a main AuthButton created disabled; an inner setStatus validates on every input (Enter clicks the button
 *   when setStatus returns true). Click: StartWorking, inputs readOnly, request; success -> message,
 *   AuthStartPage() or AuthPageValidation(..., "<Page>.html?"); failure -> Fail(...), StopWorking, setStatus,
 *   5 s SetTemporaryInnerText retry text, inputs editable again.
 * - "Token mode / request mode" pages: with a query string a single confirm button calls the token API; without
 *   one the page sends the request message and hands over to AuthPageValidation.
 * - Naming carried over from the login form: the current-password input is called `uname` and the second
 *   input `pwd`, even when it holds an e-mail or phone number. The current password is validated against the
 *   CREATE password policy.
 */

////// Set password ///////////////////////////////



/**
 * Entry point of SetPassword.html (change the password of the signed-in user), page icon IconAuthSetPassword.
 * Standard form: current password (AuthPassword, `uname`) and new password (AuthNewPassword with the create
 * policy from getCreatePasswordPolicy, `pwd`). Button "Set password" (IconAuthPassword): GET ../Api/auth/GetUser
 * for the user name (throws "No user signed in!" unless Succeeded and Username), then
 * setPasswordRequest(username, current, new). On success shows "Password set." and disables the button;
 * the user stays on the page.
 * @returns {Promise<void>}
 */
async function setPasswordMain() {

    await AuthPage("IconAuthSetPassword", async target => {
        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);
        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;

        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));

        AuthLabel(target, _TF("New password", "Label text for an input box where the user should enter a new password")).title = policyText;
        /**
         * Validates both inputs and updates the error marks, the info line and the "Set password" button.
         * The current password is checked with ValidatePassword against the create policy; if invalid, `uname`
         * is marked, `pwd` unmarked, the button disabled and false returned. Otherwise the new password is
         * validated (ValidatePassword with the current password as "user" argument); when valid a leak check
         * CheckPassword(info, pwText) is started without awaiting.
         * @param {string} uText Current password text (trimmed with AuthTrim)
         * @param {string} pwText New password text (trimmed with AuthTrim)
         * @returns {Promise<boolean>} false if the current password is invalid; otherwise true, even when the
         * new password is invalid (the button state reflects that case)
         */
        async function setStatus(uText, pwText) {
            uText = AuthTrim(uText);
            pwText = AuthTrim(pwText);
            const e = await ValidatePassword(null, uText, null, null, null, policy);
            AuthSetError(uname, e);
            if (e) {
                AuthSetError(pwd, false);
                AuthSetText(info, e);
                setButton.SetEnabled(false);
                return false;
            }
            const p = await ValidatePassword(uText, pwText, null, null, null, policy);
            AuthSetError(pwd, p);
            if (p)
                AuthSetText(info, p);
            else
                CheckPassword(info, pwText); // Don't await, run async
            setButton.SetEnabled(!p);
            return true;
        }
        const pwd = AuthNewPassword(target,
            _TF("Enter a new password", "Placeholder text for an input box where the user should enter a new password"),
            null, "new-password", async input => await setStatus(uname.value, pwd.value),
            async el => {
                if (await setStatus(uname.value, pwd.value))
                    el.click();
            }, policy);
        const info = AuthError(target, "", policyText);
        const br = AuthButtonRow(target);

        const setButton = AuthButton(br,
            _TF("Set password", "Text of a button that when clicked will set a new password"),
            _TF("Click to change the password", "Tool tip description on a button that when clicked will set a new password"),
            "IconAuthPassword", async button => {
            button.StartWorking();
            pwd.readOnly = true;
            uname.readOnly = true;
            try {
                const usr = uname.value;
                AuthSetText(info, _TF('Changing password ..', "Message displayed when the users password is being changed"));
                const pw = pwd.value;
                var r = await sendRequest("../Api/auth/GetUser");
                if (!r.Succeeded)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const username = r.Username;
                if (!username)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const res = await setPasswordRequest(username, usr, pw);
                if (res) {
                    AuthSetText(info, _TF("Password set.", "Message displayed when a password have been set succesfully"));
                    setButton.SetEnabled(false);
                    return;
                }
                Fail(_TF("Failed to change password.", "Error message displayed when a user tried to change their password but didn't succeed"));
            }
            catch (e) {
                Fail(_TF("Failed to change password.", "Error message displayed when a user tried to change their password but didn't succeed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            await setStatus(uname.value, pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to change password, try again!?", "Message displayed when a user tried to change their password but didn't succeed"), 5000);
            uname.readOnly = false;
            pwd.readOnly = false;
            pwd.focus();
        }, true);
        await setStatus("", "");
        uname.focus();
    });

}


////// Choose password ///////////////////////////////

/**
 * Entry point of ChoosePassword.html (complete a new account from a sign-up / invite link or short code),
 * page icon IconChoosePassword.
 * Requires a token (query string longer than 3 chars incl. "?"), else Fail("Token not found or invalid").
 * POST ../Api/auth/GetNewUserData(token) -> {Salt, NickName, UserName} (null -> Fail); shows "Welcome {user}".
 * If PassKey (Passkey.js) is defined and supported, creates PassKey.NewAccount(token) and awaits its Fetch()
 * before rendering, so the later click opens the browser prompt directly; passkey support is dropped if that
 * throws or returns `error`. The label text depends on passkey availability.
 * "Set password" (IconAuthPassword) calls addUserRequest(token, salt, pw) then AuthStartPage().
 * Optional "Create a passkey" (IconAddPasskey) runs newPassKey.Run(); on Error == 0 calls AuthStartPage(),
 * otherwise Fail(PassKey.ErrorText(res)) unless PassKey.Errors.Cancelled, then pre-fetches a fresh challenge
 * (Fetch(), errors ignored).
 * Note: invite links use `ChoosePassword.html?user=..&salt=..&token=..`; the whole string is passed as token.
 * @returns {Promise<void>}
 */
async function choosePasswordMain() {

    await AuthPage("IconChoosePassword", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if ((!token) || (token.length <= 3))
            return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
        token = token.substring(1);
        const udata = await sendRequest("../Api/auth/GetNewUserData", token);
        if (udata == null)
            return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
        const salt = udata.Salt;
        const user = udata.NickName ?? udata.UserName;
        let havePasskey = (typeof PassKey !== "undefined") && PassKey.IsSupported();
        //  Get the challenge before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
        const newPassKey = havePasskey ? PassKey.NewAccount(token) : null;
        if (havePasskey) {
            try {
                havePasskey = !(await newPassKey.Fetch()).error;
            }
            catch {
                havePasskey = false;
            }
        }
        if (user)
            AuthText(target, _T('Welcome {0}.', user, "A message displayed to greet a user.{0} is replaced with the user id (user name, email or phone number)"));

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);
        AuthLabel(target,
            havePasskey
                ?
                _TF("Choose a password or create a passkey", "A message letting a user know that they should provide a new password or assign a new passkey")
                :
                _TF("Choose password", "A message letting a user know that they should provide a new password")
        ).title = policyText;

        /**
         * Validates the new password with ValidatePassword (no user name) against the create policy, shows the
         * error or starts the CheckPassword(info, text) leak check (not awaited), enables "Set password" when
         * valid and marks `pwd`.
         * @param {string} text New password text (trimmed with AuthTrim)
         * @returns {Promise<boolean>} true if the password is valid
         */
        async function setStatus(text) {
            text = AuthTrim(text);
            const p = await ValidatePassword(null, text, null, null, null, policy);
            if (p)
                AuthSetText(info, p);
            else
                CheckPassword(info, text); // Don't await, run async
            setButton.SetEnabled(!p);
            AuthSetError(pwd, p);
            return !p;
        }


        const pwd = AuthNewPassword(target,
            _TF("Enter a new password", "Placeholder text for an input box where the user should enter a new password"),
            null, "new-password", async input => await setStatus(pwd.value),
            async el => {
                if (await setStatus(pwd.value))
                    el.click();
            }, policy);

        const info = AuthError(target, "", policyText);
        const br = AuthButtonRow(target);

        let passKeyButton = null;
        const setButton = AuthButton(br,
            _TF("Set password", "Text of a button that when clicked will set a new password"),
            _TF("Click to set the password and login", "Tool tip description on a button that when clicked will set a new password and perform a login"),
            "IconAuthPassword", async button => {
            button.StartWorking();
            pwd.readOnly = true;
            if (passKeyButton)
                passKeyButton.SetEnabled(false);
            try {
                AuthSetText(info, _TF("Setting password..", "Message displayed when a users password is being set"));
                const pw = pwd.value;
                const res = await addUserRequest(token, salt, pw);
                if (res) {
                    AuthSetText(info, _TF("Password set", "Message displayed when a users password was set successfully"));
                    setButton.SetEnabled(false);
                    await AuthStartPage();
                    return;
                }
                Fail(_TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed"));
            }
            catch (e) {
                Fail(_TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            if (passKeyButton)
                passKeyButton.SetEnabled(true);
            await setStatus(pwd.value);
            SetTemporaryInnerText(info, _TF("Failed to set password, try again!?", "Message displayed when a user tried to set a password but didn't succeed"), 5000);
            pwd.readOnly = false;
            pwd.focus();
        }, true);
        if (havePasskey) {
            passKeyButton = AuthButton(br,
                _TF("Create a passkey", "Text of a button that when pressed will instruct the user to add a passkey"),
                _TF("Click to create and allow sign in using a passkey", "Tool tip description on a button that when pressed will instruct the user to add a passkey"),
                "IconAddPasskey", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                setButton.SetEnabled(false);
                let res = null;
                try {
                    AuthSetText(info, _TF("Create passkey..", "Message shown when a passkey is being created"));
                    res = await newPassKey.Run();
                    if (res.Error == 0) {
                        AuthSetText(info, _TF("Passkey created", "Message shown when a new passkey was created successfully"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    if (res.Error != PassKey.Errors.Cancelled)
                        Fail(PassKey.ErrorText(res) + (res.Message ? "\n\n" + res.Message : ""));
                }
                catch (e) {
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                newPassKey.Fetch().catch(() => { });
                await setStatus(pwd.value);
                if (res?.Error != PassKey.Errors.Cancelled)
                    SetTemporaryInnerText(info, _TF("Failed to create passkey, try again!?", "Message shown when a new passkey couldn't be created"), 5000);
                pwd.readOnly = false;
            }, false);
        }
        await setStatus("");
        pwd.focus();


    });

}


////// Delete user account ///////////////////////////////


/**
 * Entry point of DeleteAccount.html.
 * Token mode (query string present, page icon IconAuthDeleteAccount): a confirm button "Click to delete account"
 * (IconAuthDeleteAccount, warning tooltip) hides the button row, calls deleteUserRequest(token) and shows
 * "Account deleted" (no navigation); on failure Fail(...) and the row is shown again.
 * Request mode (no query string, page icon IconAuthDeleteMail): calls target.OnLoaded(), sendDeleteUserRequest(),
 * then AuthPageValidation(target, "Code sent to {targets}", "DeleteAccount.html?"); on failure shows and Fails.
 * Note: the server's SendDeleteUserRequest link uses `DeleteAccount.html?token=<t>`, so "token=..." is passed as
 * token. The computed `ps` (getUrlParams()) is unused.
 * @returns {Promise<void>}
 */
async function deleteAccountMain() {
    const ps = getUrlParams();
    let token = decodeURIComponent(window.location.search ?? "");
    await AuthPage(token ? "IconAuthDeleteAccount" : "IconAuthDeleteMail", async (target, img) => {
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Click to delete account", "Text on a button that when pressed will delete the a user's account"),
                _TF("Clicking this button will delete your account and all associated data.", "Part of a tool tip for a button that when pressed will delete the a user account") + "\n" +
                _TF("WARNING! All data will be removed!.", "Part of a tool tip for a button that when pressed will delete the a user account") + "\n" +
                _TF("Just close this page to keep your account.", "Part of a tool tip for a button that when pressed will delete the a user account"),
                "IconAuthDeleteAccount", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Deleting account ..", "Text displayed while a user account is being deleted"));
                try {
                    const res = await deleteUserRequest(token);
                    if (res) {
                        AuthSetText(info, _TF("Account deleted", "Text displayed when a user account was successfully deleted"));
                        return;
                    }
                    Fail(_TF("Failed to delete account.", "Error message displayed when the deletion of a user account failed"));
                }
                catch (e) {
                    Fail(_TF("Failed to delete account.", "Error message displayed when the deletion of a user account failed")  + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const text = AuthError(target, _TF("Sending remove account instructions ..", "Message displayed when sending instructions on how to delete a user account"));
            target.OnLoaded();
            const emails = await sendDeleteUserRequest();
            if (!emails) {
                const m = _TF("Failed to send remove account instructions!", "Error message displayed when failing to send instructions on how to delete a user account");
                AuthSetText(text, m);
                Fail(m);
            } else {
                AuthPageValidation(target,
                    _TH("Code sent to {0}.", AuthUserHtml(emails), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                    "DeleteAccount.html?");
                return;
            }
        }
    });
}


////// Sign up ///////////////////////////////

/**
 * Entry point of SignUp.html, page icon IconAuthSignUp.
 * GET ../Api/auth/SignUpMethods (array of "Email" / "Phone"; empty -> Fail "No sign up methods found!").
 * Creates an AuthTab and, per supported method, a SysWeaver-AuthTabContent element `g` appended to the box, with a
 * tab header (tab.Add, icons IconAuthEmail / IconAuthPhone) and an AuthInput (type "email") or AuthInputPhone.
 * Each content element gets the expandos g.SetStatus / g.GetValue. The tab change callback shows only the
 * selected content, re-validates it and focuses its input. No content created -> Fail "No supported sign up
 * methods found!".
 * "Sign up" (IconAuthPost) adds class Disabled to the tab and all contents, calls signUpRequest(value) and on
 * success AuthPageValidation(..., "ChoosePassword.html?"); on failure removes Disabled again.
 * Ends with the hard-coded tab.SetIndex(1), then validates and focuses elements[tab.Selected].
 * @returns {Promise<void>}
 */
async function signUpMain() {
    await AuthPage("IconAuthSignUp", async target => {

        const methods = await getRequest("../Api/auth/SignUpMethods");
        const methodLen = methods.length;
        if (methodLen === 0) {
            Fail(_TF("No sign up methods found!", "Error message displayed when there are no ways to sign up"));
            return;
        }

        const elements = [];
        const tab = AuthTab(target,
            _TF("The methods that can be used to sign up", "The description of a header about what sign up methods can be used"),
            newIndex => {
            const elementLength = elements.length;
            for (let i = 0; i < elementLength; ++i) {
                const el = elements[i];
                if (i === newIndex) {
                    el.style.display = null;
                    el.SetStatus(el.value);
                    el.getElementsByTagName("input")[0].focus();
                } else {
                    el.style.display = "none";
                }
            }
        });


        /**
         * Validates the e-mail input with AuthGetEmailError ("Enter a valid email address" when empty),
         * marks the input, updates `info` and enables / disables the "Sign up" button.
         * @param {HTMLInputElement} el The e-mail input element
         * @returns {Promise<boolean>} true if the e-mail address is valid
         */
        async function setEmailStatus(el) {
            let uText = el.value;
            uText = AuthTrim(uText);
            const p = uText.length > 0 ? await AuthGetEmailError(uText) : _TF("Enter a valid email address", "Text displayed to let users know that they should enter a valid email address");
            AuthSetError(el, p);
            if (p)
                AuthSetText(info, p);
            else
                AuthSetText(info);
            setButton.SetEnabled(!p);
            return !p;
        }

        /**
         * Validates the phone input with AuthGetPhoneError ("Enter a valid phone number" when empty),
         * marks the input, updates `info` and enables / disables the "Sign up" button.
         * @param {HTMLInputElement} el The phone input element (from AuthInputPhone)
         * @returns {Promise<boolean>} true if the phone number is valid
         */
        async function setPhoneStatus(el) {
            let uText = el.value;
            uText = AuthTrim(uText);
            const p = uText.length > 0 ? await AuthGetPhoneError(uText) : _TF("Enter a valid phone number", "Text displayed to let users know that they should enter a valid phone number");
            AuthSetError(el, p);
            if (p)
                AuthSetText(info, p);
            else
                AuthSetText(info);
            setButton.SetEnabled(!p);
            return !p;
        }

        for (let i = 0; i < methodLen; ++i) {
            const g = document.createElement("SysWeaver-AuthTabContent");
            if (methods[i] === "Email") {
                const header = tab.Add(
                    _TF("Email", "Text of a tab header for a tab where the user should enter a valid email address in order to sign up"),
                    _TF("Click to sign up using your email address", "Tool tip description on a tab header for a tab where the user should enter a valid email address in order to sign up"),
                    null, "IconAuthEmail");
                const uname = AuthInput(g,
                    _TF("Enter your email address", "Placeholder text on an input box where users are expected to enter their email address"),
                    null, "email", async input => await setEmailStatus(uname), async el => {
                    if (await setEmailStatus(uname))
                        el.click();
                });
                uname.type = "email";
                /**
                 * Expando on the tab content element: re-validates its e-mail input (setEmailStatus).
                 * Called by the tab change callback, after a failed sign-up and at start-up.
                 * @returns {Promise<boolean>} true if valid
                 */
                g.SetStatus = () => setEmailStatus(uname);
                /**
                 * Expando on the tab content element: returns the raw value of its e-mail input (not read by the page code).
                 * @returns {string} The input value
                 */
                g.GetValue = () => uname.value;
            }
            if (methods[i] === "Phone") {
                const header = tab.Add(
                    _TF("Phone", "Text of a tab header for a tab where the user should enter a valid phone number in order to sign up"),
                    _TF("Click to sign up using your phone number", "Tool tip description on a tab header for a tab where the user should enter a valid phone number in order to sign up"),
                    null, "IconAuthPhone");
                const uname = AuthInputPhone(g,
                    _TF("Enter your phone number", "Placeholder text on an input box where users are expected to enter their phone number"),
                    null, "tel", async input => await setPhoneStatus(uname), async el => {
                    if (await setPhoneStatus(uname))
                        el.click();
                });
                /**
                 * Expando on the tab content element: re-validates its phone input (setPhoneStatus).
                 * @returns {Promise<boolean>} true if valid
                 */
                g.SetStatus = () => setPhoneStatus(uname);
                /**
                 * Expando on the tab content element: returns the raw value of its phone input (not read by the page code).
                 * @returns {string} The input value
                 */
                g.GetValue = () => uname.value;
            }
            if (!g.SetStatus)
                continue;
            target.appendChild(g);
            if (elements.length > 0)
                g.style.display = "none";
            elements.push(g);
        }

        if (elements.length <= 0) {
            Fail(_TF("No supported sign up methods found!", "Error message displayed when there are no supported sign up methods"));
            return;

        }

        const info = AuthError(target, "", "error");
        const br = AuthButtonRow(target);

        let passKeyButton = null;
        const setButton = AuthButton(br,
            _TF("Sign up", "Text on a button that when pressed will sign up to a service using the supplied user id (phone number or email address)"),
            _TF("Click to send an message with further instructions", "Tool tip on a button that when pressed will sign up to a service using the supplied user id (phone number or email address)"),
            "IconAuthPost", async button => {
            button.StartWorking();
            const tabC = elements[tab.Selected];
            const uname = tabC.getElementsByTagName("INPUT")[0];
            tab.classList.add("Disabled");
            for (let i = 0; i < elements.length; ++i)
                elements[i].classList.add("Disabled");
            try {
                const usr = uname.value;
                AuthSetText(info, _TF("Sending instructions to {0} ..", AuthUserHtml(usr), "Message displayed when sending sign up instructions.Text must be safe to use in HTML.{0} is replaced with the user id (phone number or email address)"), true);
                const res = await signUpRequest(usr);
                if (res) {

                    AuthPageValidation(target,
                        _TH("Code sent to {0}.", AuthUserHtml(usr), "Message displayed when sign up instructions have been sent.Text must be safe to use in HTML.{0} is replaced with the user id (phone number or email address)"),
                        "ChoosePassword.html?");
                    return;
                }
                Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"));
            }
            catch (e) {
                Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            tabC.SetStatus();
                SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
            for (let i = 0; i < elements.length; ++i)
                elements[i].classList.remove("Disabled");
            tab.classList.remove("Disabled");
            uname.focus();
        }, true);

        tab.SetIndex(1);

        const sel = elements[tab.Selected];
        sel.SetStatus();
        sel.firstElementChild.focus();


    });
}


////// Forgot password ///////////////////////////////

/**
 * Entry point of ForgotPassword.html, page icon IconAuthForgotPassword.
 * Standard form with one "User ID" AuthInput (autocomplete "userid"). Button (IconAuthPost) calls
 * forgotPasswordRequest(user); on success AuthPageValidation(..., "ResetPassword.html?").
 * @returns {Promise<void>}
 */
async function forgotPasswordMain() {
    await AuthPage("IconAuthForgotPassword", async target => {

        AuthLabel(target, _TF("User ID", "Label text for an input field where the user should enter their user id (username, email, phone)"));
        const uname = AuthInput(target,
            _TF("Enter your user id", "Placeholder text for an input field where the user should enter their user id (username, email, phone)"),
            _TF("User id's are typically your email address, phone number or username", "Tool tip description for an input field where the user should enter their user id (username, email, phone)"),
            "userid", input => setStatus(uname.value),
            el => {
                if (setStatus(uname.value))
                    el.click();
            });
        /**
         * Synchronous validation: the user id only has to be non-empty after AuthTrim. Updates the error mark,
         * the info line and the button state.
         * @param {string} uText The user id text
         * @returns {boolean} true if non-empty
         */
        function setStatus(uText) {
            uText = AuthTrim(uText);
            const p = uText.length > 0;
            AuthSetError(uname, !p);
            if (!p)
                AuthSetText(info, _TF("Enter a valid user id", "Message letting users know that they are expected to enter their user id in an input field"));
            else
                AuthSetText(info);
            setButton.SetEnabled(p);
            return p;
        }
        const info = AuthError(target, "", "");
        const br = AuthButtonRow(target);

        let passKeyButton = null;
        const setButton = AuthButton(br,
            _TF("Request password request", "Text of a button that when pressed will request a password reset"),
            _TF("Click to send instructions on how to reset your password", "Tool tip description of a button that when pressed will request a password reset"),
            "IconAuthPost", async button => {
            button.StartWorking();
            uname.readOnly = true;
            try {
                const usr = uname.value;
                AuthSetText(info, _TH("Sending password reset instructions for {0}", AuthUserHtml(usr), "Text displayed when a password reset email or sms message is being sent.{0} is replaced with the user name specified"), true);
                const targets = await forgotPasswordRequest(usr);
                if (targets) {
                    AuthPageValidation(target,
                        _TH("Code sent to {0}.", AuthUserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                        "ResetPassword.html?");
                    return;
                }
                Fail(_TF("Failed to send password reset instructions.", "Error message displayed when the server failed to sent password reset instructions"));
            }
            catch (e) {
                Fail(_TF("Failed to send password reset instructions.", "Error message displayed when the server failed to sent password reset instructions") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            setStatus(uname.value);
                SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
            uname.readOnly = false;
            uname.focus();
        }, true);
        setStatus("");
        uname.focus();
    });

}


////// Reset password ///////////////////////////////

/**
 * Entry point of ResetPassword.html (reset the password from a link or short code), page icon IconAuthResetPassword.
 * Same structure as choosePasswordMain, but uses POST ../Api/auth/GetNewPasswordData(token) -> {Salt, NickName,
 * UserName}, PassKey.AttachUsingToken(token) for the optional "Create a passkey" button, and
 * resetPasswordRequest(token, pw, salt), which must return Error == 0 before AuthStartPage(). No welcome text.
 * @returns {Promise<void>}
 */
async function resetPasswordMain() {
    await AuthPage("IconAuthResetPassword", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if ((!token) || (token.length <= 3))
            return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
        token = token.substring(1);
        const udata = await sendRequest("../Api/auth/GetNewPasswordData", token);
        if (udata == null)
            return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
        const salt = udata.Salt;
        const user = udata.NickName ?? udata.UserName;

        let havePasskey = (typeof PassKey !== "undefined") && PassKey.IsSupported();
        //  Get the challenge before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
        const newPassKey = havePasskey ? PassKey.AttachUsingToken(token) : null;
        if (havePasskey) {
            try {
                havePasskey = !(await newPassKey.Fetch()).error;
            }
            catch {
                havePasskey = false;
            }
        }

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target,
            havePasskey
                ?
                _TF("Choose a new password or create a passkey", "A message letting a user know that they should provide a new password or assign a new passkey")
                :
                _TF("Choose a new password", "A message letting a user know that they should provide a new password")
        ).title = policyText;

        /**
         * Validates the new password with ValidatePassword (no user name) against the create policy, shows the
         * error or starts the CheckPassword(info, text) leak check (not awaited), enables "Set password" when
         * valid and marks `pwd`.
         * @param {string} text New password text (trimmed with AuthTrim)
         * @returns {Promise<boolean>} true if the password is valid
         */
        async function setStatus(text) {
            text = AuthTrim(text);
            const p = await ValidatePassword(null, text, null, null, null, policy);
            if (p)
                AuthSetText(info, p);
            else
                CheckPassword(info, text); // Don't await, run async
            setButton.SetEnabled(!p);
            AuthSetError(pwd, p);
            return !p;
        }


        const pwd = AuthNewPassword(target,
            _TF("Enter a new password", "Placeholder text of an input field where users should enter a new secure password"),
            null, "new-password", async input => await setStatus(pwd.value),
            async el => {
                if (await setStatus(pwd.value))
                    el.click();
            }, policy);

        const info = AuthError(target, "", policyText);
        const br = AuthButtonRow(target);

        let passKeyButton = null;
        const setButton = AuthButton(br,
            _TF("Set password", "Text of a button that when clicked will set a new password"),
            _TF("Click to set the password and login", "Tool tip description on a button that when clicked will set a new password and perform a login"),
            "IconAuthPassword", async button => {
            button.StartWorking();
            pwd.readOnly = true;
            if (passKeyButton)
                passKeyButton.SetEnabled(false);
            try {
                AuthSetText(info, _TF("Setting password..", "Message displayed when a users password is being set"));
                const pw = pwd.value;
                const res = await resetPasswordRequest(token, pw, salt);
                if (res) {
                    const err = res.Error;
                    if (err == 0) {
                        AuthSetText(info, _TF("Password set", "Message displayed when a users password was set successfully"));
                        setButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                }
                Fail(_TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed"));
            }
            catch (e) {
                Fail(_TF("Failed to set password.", "Error message displayed when a user tried to set a password but didn't succeed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            if (passKeyButton)
                passKeyButton.SetEnabled(true);
            await setStatus(pwd.value);
            SetTemporaryInnerText(info, _TF("Failed to set password, try again!?", "Message displayed when a user tried to set a password but didn't succeed"), 5000);
            pwd.readOnly = false;
            pwd.focus();
        }, true);
        if (havePasskey) {

                passKeyButton = AuthButton(br,
                    _TF("Create a passkey", "Text of a button that when pressed will instruct the user to add a passkey"),
                    _TF("Click to create and allow sign in using a passkey", "Tool tip description on a button that when pressed will instruct the user to add a passkey"),
                    "IconAddPasskey", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                setButton.SetEnabled(false);
                let res = null;
                try {
                    AuthSetText(info, _TF("Create passkey..", "Message shown when a passkey is being created"));
                    res = await newPassKey.Run();
                    if (res.Error == 0) {
                        AuthSetText(info, _TF("Passkey created", "Message shown when a new passkey was created successfully"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    if (res.Error != PassKey.Errors.Cancelled)
                        Fail(PassKey.ErrorText(res) + (res.Message ? "\n\n" + res.Message : ""));
                }
                catch (e) {
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                newPassKey.Fetch().catch(() => { });
                await setStatus(pwd.value);
                if (res?.Error != PassKey.Errors.Cancelled)
                    SetTemporaryInnerText(info, _TF("Failed to create passkey, try again!?", "Message shown when a new passkey couldn't be created"), 5000);
                pwd.readOnly = false;
            }, false);
        }
        await setStatus("");
        pwd.focus();


    });
}


////// Add password ///////////////////////////////
/**
 * Entry point of AddPassword.html (add a password to an account without one, e.g. passkey-only),
 * page icon IconAuthAddPassword.
 * Token mode: POST ../Api/auth/GetNewPasswordData(token) -> {Salt, NickName, UserName}, then a new-password
 * standard form; "Add password" (IconAuthPassword) calls resetPasswordRequest(token, pw, salt) and on
 * Error == 0 AuthStartPage().
 * Request mode: target.OnLoaded(), sendAddPasswordRequest(), then AuthPageValidation(..., "AddPassword.html?").
 * The computed `ps` (getUrlParams()) is unused.
 * @returns {Promise<void>}
 */
async function addPasswordMain() {
    await AuthPage("IconAuthAddPassword", async target => {

        const ps = getUrlParams();
        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if ((!token) || (token.length < 4))
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const udata = await sendRequest("../Api/auth/GetNewPasswordData", token);
            if (udata == null)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            const salt = udata.Salt;
            const user = udata.NickName ?? udata.UserName;

            const policy = await getCreatePasswordPolicy();
            const policyText = AuthPolicyText(policy);
            AuthLabel(target, _TF("Choose password", "A message letting a user know that they should provide a new password")).title = policyText;
            /**
             * Validates the new password with ValidatePassword(user, ...) (the nick / user name from the token
             * data is passed, unlike in choose / reset password), shows the error or starts the CheckPassword
             * leak check (not awaited), enables "Add password" when valid and marks `pwd`.
             * @param {string} text New password text (trimmed with AuthTrim)
             * @returns {Promise<boolean>} true if the password is valid
             */
            async function setStatus(text) {
                text = AuthTrim(text);
                const p = await ValidatePassword(user, text, null, null, null, policy);
                if (p)
                    AuthSetText(info, p);
                else
                    CheckPassword(info, text); // Don't await, run async
                setButton.SetEnabled(!p);
                AuthSetError(pwd, p);
                return !p;
            }

            const pwd = AuthNewPassword(target,
                _TF("Enter a new password", "Placeholder text of an input field where users should enter a new secure password"),
                null, "new-password", async input => await setStatus(pwd.value),
                async el => {
                    if (await setStatus(pwd.value))
                        el.click();
                }, policy);

            const info = AuthError(target, "", policyText);
            const br = AuthButtonRow(target);

            const setButton = AuthButton(br,
                _TF("Add password", "Text of a button that when clicked will add a new password"),
                _TF("Click to add the password and login", "Tool tip description on a button that when clicked will add a new password and perform a login"),
                "IconAuthPassword", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                try {
                    AuthSetText(info, _TF("Adding password..", "Message displayed when a user is adding a password"));
                    const pw = pwd.value;
                    const res = await resetPasswordRequest(token, pw, salt);
                    if (res) {
                        const err = res.Error;
                        if (err == 0) {
                            AuthSetText(info, _TF("Password added", "Message displayed when a user added a password successfully"));
                            setButton.SetEnabled(false);
                            await AuthStartPage();
                            return;
                        }
                    }
                    Fail(_TF("Failed to add password.", "Error message displayed when adding a user password failed"));
                }
                catch (e) {
                    Fail(_TF("Failed to add password.", "Error message displayed when adding a user password failed") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(pwd.value);
                    SetTemporaryInnerText(info, _TF("Failed to add password, try again!?", "Message displayed when adding a user password failed"), 5000);
                pwd.readOnly = false;
                pwd.focus();
            }, true);
            await setStatus("");
            pwd.focus();
        } else {
            const text = AuthError(target, _TF("Sending instructions ..", "Message displayed when sending instructions using email and/or sms"));
            target.OnLoaded();
            const emails = await sendAddPasswordRequest();
            if (!emails) {
                const m = _TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms");
                AuthSetText(text, m);
                Fail(m);
            } else {
                AuthPageValidation(target,
                    _TH("Code sent to {0}.", AuthUserHtml(emails), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                    "AddPassword.html?");
                return;
            }
        }
    });
}


////// Delete password ///////////////////////////////

/**
 * Entry point of DeletePassword.html, page icon IconAuthDeletePassword.
 * Token mode: confirm button "Delete password" (IconAuthDeletePassword) hides the row, calls deletePassword(token)
 * and shows "Password deleted" (row shown again on failure).
 * Request mode: target.OnLoaded(), sendDeletePasswordRequest(), then AuthPageValidation(..., "DeletePassword.html?").
 * @returns {Promise<void>}
 */
async function deletePasswordMain() {
    await AuthPage("IconAuthDeletePassword", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Delete password", "Text on a button that when clicked will delete the users password"),
                _TF("Clicking this button will delete the current password.", "Tool tip description on a button that when clicked will delete the users password") + "\n" +
                _TF("A new password will have to be added in order to log in using a password.", "Tool tip description on a button that when clicked will delete the users password") + "\n" +
                _TF("Just close this page to keep yor existing password.", "Tool tip description on a button that when clicked will delete the users password"),
                "IconAuthDeletePassword", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Deleting password ..", "Message displayed when a users password is being deleted"));
                try {
                    const res = await deletePassword(token);
                    if (res) {
                        AuthSetText(info, _TF("Password deleted", "Message displayed when a users password was deleted successfully"));
                        return;
                    }
                    Fail(_TF("Failed to delete password.", "Error message displayed when a users password couldn't be deleted"));
                }
                catch (e) {
                    Fail(_TF("Failed to delete password.", "Error message displayed when a users password couldn't be deleted") + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const text = AuthError(target, _TF("Sending instructions on how to delete your password..", "Message displayed when the server is sedning instructions on how to delete a password"));
            target.OnLoaded();
            const emails = await sendDeletePasswordRequest();
            if (!emails) {
                const m = _TF("Failed to send instuctions on how to delete your password");
                AuthSetText(text, m);
                Fail(m);
            } else {
                AuthPageValidation(target,
                    _TH("Code sent to {0}.", AuthUserHtml(emails), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                    "DeletePassword.html?");
                //AuthSetText(text, "Sent delete password instructions to " + AuthUserHtml(emails) + ".", true);
                return;
            }
        }
    });
}

////// Invite user ///////////////////////////////

/**
 * Entry point of InviteUser.html (privileged), page icon IconAuthInviteUser.
 * Requires POST ../Api/auth/GetUser -> Succeeded (throws "No user signed in!" otherwise, caught by AuthPage).
 * Inputs: e-mail (validated client-side with isValidEmail from common.js), an optional user name, and only when
 * the inviter has Tokens a comma-separated "Security tokens" input whose tooltip lists the inviter's tokens.
 * "Send invitation" (IconAuthPost) calls inviteUserRequest(email, name, tokens.split(',')) (no domain) and shows
 * "Invitation sent to {email}." with the button disabled.
 * @returns {Promise<void>}
 */
async function inviteUserMain() {
    await AuthPage("IconAuthInviteUser", async target => {

        const currentUser = await sendRequest("../Api/auth/GetUser");
        if (!(currentUser && currentUser.Succeeded))
            throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));


        AuthLabel(target, _TF("Email", "Label text for an input field where the user should supply an email address"));
        const email = AuthInput(target,
            _TF("Enter the email to the person you like to invite", "Placeholder text for an input field where the user should supply an email address"),
            null, "email", input => setStatus(email.value));


        let title = _TF("Username (can optionally) be used when logging in, instead of email", "Tool tip description on an input field where the user can enter a username");
        AuthLabel(target, _TF("Username", "Label text for an input field where the user can enter an optional username"), title);
        const username = AuthInput(target, _TF("An optional username", "Placeholder text for an input field where the user can enter an optional username"), title, "username");

        let tokens = null;
        if (currentUser.Tokens && (currentUser.Tokens.length > 0)) {
            let title =
                _TF("You can only give security tokens that you have.", "Tool tip description of an input field where the user can enter auth tokens to give to a new user") + "\n" +
                _TF("Multiple tokens can be specificed separated by a comma.", "Tool tip description of an input field where the user can enter auth tokens to give to a new user") + "\n" +
                _TF("The available tokens are:", "Tool tip description of an input field where the user can enter auth tokens to give to a new user.The following line(s) will contain the token names.") +
                "\n - " + currentUser.Tokens.join("\n - ");
            AuthLabel(target, _TF("Security tokens", "Label text for an input field where the user can enter auth tokens to give to a new user"), title);
            tokens = AuthInput(target,
                _TF("An optional list of security tokens to give to the new user", "Placeholder text for an input field where the user can enter auth tokens to give to a new user"),
                title, "securitytokens");
        }

        /**
         * Synchronous validation of the e-mail input with isValidEmail (common.js); updates the error mark,
         * the info line and the "Send invitation" button.
         * @param {string} uText The e-mail text (trimmed with AuthTrim)
         * @returns {boolean} true if the e-mail address is valid
         */
        function setStatus(uText) {
            uText = AuthTrim(uText);
            const p = isValidEmail(uText);
            AuthSetError(email, !p);
            if (!p)
                AuthSetText(info, _TF("Enter a valid email", "Message to let users know that they should enter a valid email address in the input field"));
            else
                AuthSetText(info);
            setButton.SetEnabled(p);
            return p;
        }


        const info = AuthError(target, "", "");
        const br = AuthButtonRow(target);

        const setButton = AuthButton(br,
            _TF("Send invitation", "Text on a button that if pressed will send an invitation to join the site to a given email address"),
            _TF("Send an invitation to the supplied email", "Tool tip description for a button that if pressed will send an invitation to join the site to a given email address"),
            "IconAuthPost", async button => {
            button.StartWorking();
            email.readOnly = true;
            username.readOnly = true;
            if (tokens)
                tokens.readOnly = true;
            try {
                const em = email.value;
                const emh = AuthUserHtml(em);
                const uname = username.value;
                const tk = tokens ? tokens.value.split(',') : null;
                AuthSetText(info, _TH("Sending invitation to {0} ..", emh, "Message disaplyed when an invitation to join the site is being sent.{0} is replaced with the email address"), true);
                const res = await inviteUserRequest(em, uname, tk);
                if (res) {
                    AuthSetText(info, _TH("Invitation sent to {0}.", emh, "Message displayed when an invitation to join the site have been sent.{0} is replaced with the email address"), true);
                    setButton.SetEnabled(false);
                    return;
                }
                Fail(_TF("Failed to send invitation.", "Error message displayed when an invitation to join the site failed to be sent"));
            }
            catch (e) {
                Fail(_TF("Failed to send invitation.", "Error message displayed when an invitation to join the site failed to be sent") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            setStatus(email.value);
                SetTemporaryInnerText(info, _TF("Failed to send invitation, try again!?", "Message displayed when an invitation to join the site failed to be sent"), 5000);
            if (tokens)
                tokens.readOnly = false;
            username.readOnly = false;
            email.readOnly = false;
            email.focus();
        }, true);

        setStatus("");
        email.focus();


    });
}

////// Change email ///////////////////////////////

/**
 * Entry point of ChangeEmail.html, page icon IconAuthChangeEmail.
 * Token mode: confirm button "Change email address" (IconAuthChangeEmail) calls changeEmail(token) (AddEmail API)
 * and shows "Email address changed".
 * Request mode: standard form with current password (`uname`) and new e-mail (AuthInput, `pwd`, validated with
 * AuthGetEmailError); "Verify email address" (IconAuthEmail): GET ../Api/auth/GetUser, then
 * changeEmailRequest(username, password, email), then AuthPageValidation(..., "ChangeEmail.html?").
 * @returns {Promise<void>}
 */
async function changeEmailMain() {

    await AuthPage("IconAuthChangeEmail", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Change email address", "Text on a button that if clicked will change the email address associated with an account"),
                _TF("Click to change the email address associated with your account.", "Tool tip description for a button that if clicked will change the email address associated with an account"),
                "IconAuthChangeEmail", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Changing email address ..", "Message displayed when the server changes a users email address"));
                try {
                    const res = await changeEmail(token);
                    if (res) {
                        AuthSetText(info, _TF("Email address changed", "Message displayed when an email address of an account have been successfully changed"));
                        return;
                    }
                    Fail(_TF("Failed to change email address.", "Error message displayed when changing an email address on an account fail"));
                }
                catch (e) {
                    Fail(_TF("Failed to change email address.", "Error message displayed when changing an email address on an account fail") + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const policy = await getCreatePasswordPolicy();
            const policyText = AuthPolicyText(policy);
            AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
            const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));

            AuthLabel(target, _TF("New email address", "Label for an input field where the user should enter a new email address")).title = policyText;
            /**
             * Validates the current password (ValidatePassword, create policy) and the new e-mail address
             * (AuthGetEmailError, or "Enter your new email address" when empty); updates marks, info line and button.
             * @param {string} uText Current password text (trimmed with AuthTrim)
             * @param {string} pwText New e-mail text (trimmed with AuthTrim)
             * @returns {Promise<boolean>} false if the current password is invalid; otherwise true, even when the
             * e-mail is invalid (the button state reflects that case)
             */
            async function setStatus(uText, pwText) {
                uText = AuthTrim(uText);
                pwText = AuthTrim(pwText);
                const e = await ValidatePassword(null, uText, null, null, null, policy);
                AuthSetError(uname, e);
                if (e) {
                    AuthSetError(pwd, false);
                    AuthSetText(info, e);
                    setButton.SetEnabled(false);
                    return false;
                }
                const p = pwText.length > 0
                    ?
                    await AuthGetEmailError(pwText)
                    :
                    _TF("Enter your new email address", "Placeholder text for an input field where the user should enter a new email address")
                    ;
                AuthSetError(pwd, p);
                if (p)
                    AuthSetText(info, p);
                else
                    AuthSetText(info, "");
                setButton.SetEnabled(!p);
                return true;
            }
            const pwd = AuthInput(target,
                _TF("Enter your new email address", "Placeholder text for an input field where the user should enter a new email address"),
                null, "email", async input => await setStatus(uname.value, pwd.value),
                async el => {
                    if (await setStatus(uname.value, pwd.value))
                        el.click();
                });
            const info = AuthError(target, "", policyText);
            const br = AuthButtonRow(target);

            const setButton = AuthButton(br,
                _TF("Verify email address", "Text on a button that when clicked will send a verfication message to an email address"),
                _TF("Click to send verification instructions to the new email address", "Tool tip description for a button that when clicked will send a verfication message to an email address"),
                "IconAuthEmail", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                uname.readOnly = true;
                try {
                    const usr = uname.value;
                    AuthSetText(info, _TH("Sending instructions to {0}..", AuthUserHtml(usr), "Message displayed when sending instructions using email.{0} is replaced with the email address"), true);
                    const pw = pwd.value;
                    var r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const username = r.Username;
                    if (!username)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const targets = await changeEmailRequest(username, usr, pw);
                    if (targets) {
                        AuthPageValidation(target,
                            _TH("Code sent to {0}.", AuthUserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                            "ChangeEmail.html?");
                        //AuthSetText(info, "Sent verification instructions to " + AuthUserHtml(targets) + ".", true);
                        //setButton.SetEnabled(false);
                        return;
                    }
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"));
                }
                catch (e) {
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(uname.value, pwd.value);
                    SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
                pwd.focus();
            }, true);
            await setStatus("", "");
            uname.focus();
        }
    });

}

////// Delete email ///////////////////////////////

/**
 * Entry point of DeleteEmail.html, page icon IconAuthDeleteEmail. Single mode (no token).
 * Standard form with only the current password; "Delete email address" (IconAuthDeleteEmail) calls
 * GET ../Api/auth/GetUser, then deleteEmail(username, password) and shows "Sent restore instructions to {targets}."
 * @returns {Promise<void>}
 */
async function deleteEmailMain() {

    await AuthPage("IconAuthDeleteEmail", async target => {

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value));

        /**
         * Validates the current password only (ValidatePassword, create policy); enables the button when valid.
         * @param {string} uText Current password text (trimmed with AuthTrim)
         * @returns {Promise<boolean>} true if valid
         */
        async function setStatus(uText) {
            uText = AuthTrim(uText);
            const e = await ValidatePassword(null, uText, null, null, null, policy);
            AuthSetError(uname, e);
            if (e) {
                AuthSetText(info, e);
                setButton.SetEnabled(false);
                return false;
            }
            AuthSetText(info, "");
            setButton.SetEnabled(true);
            return true;
        }
        const info = AuthError(target, "", policyText);
        const br = AuthButtonRow(target);

        const setButton = AuthButton(br,
            _TF("Delete email address", "Text on a button that when pressed will delete the email address associated with a user"),
            _TF("Click to remove the email address associated with this account", "Tool tip description for a button that when pressed will delete the email address associated with a user"),
            "IconAuthDeleteEmail", async button => {
            button.StartWorking();
            uname.readOnly = true;
            try {
                const usr = uname.value;
                AuthSetText(info, _TF('Deleting email address..', "Message displayed when an email address is being deleted"));
                var r = await sendRequest("../Api/auth/GetUser");
                if (!r.Succeeded)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const username = r.Username;
                if (!username)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const targets = await deleteEmail(username, usr);
                if (targets) {
                    AuthSetText(info, _TH("Sent restore instructions to {0}.", AuthUserHtml(targets), "Message displayed when an email address is deleted from an account.{0} is the email address that was deleted"), true);
                    setButton.SetEnabled(false);
                    return;
                }
                Fail(_TF("Failed to delete email address.", "Error message displayed when the deletion of an email address failed"));
            }
            catch (e) {
                Fail(_TF("Failed to delete email address.", "Error message displayed when the deletion of an email address failed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            await setStatus(uname.value);
                SetTemporaryInnerText(info, _TF("Failed to delete email address, try again!?", "Message displayed when the deletion of an email address failed"), 5000);
            uname.readOnly = false;
        }, true);
        await setStatus(uname.value);
        uname.focus();
    }
    );

}


////// Add email ///////////////////////////////

/**
 * Entry point of AddEmail.html, page icon IconAuthAddEmail. Same as changeEmailMain but with addEmail(token)
 * (confirm button "Add email address", IconAuthAddEmail) / addEmailRequest and the link "AddEmail.html?".
 * @returns {Promise<void>}
 */
async function addEmailMain() {

    await AuthPage("IconAuthAddEmail", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Add email address", "Text on a button that when clicked will add (associate) the email address to the account"),
                _TF("Click to associate the email address with your account.", "Tool tip description for a button that when clicked will add (associate) the email address to the account"),
                "IconAuthAddEmail", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Adding email address ..", "Message displayed when an email address is being associated with an account"));
                try {
                    const res = await addEmail(token);
                    if (res) {
                        AuthSetText(info, _TF("Email address added", "Message displayed when an email address was successfully associated with an account"));
                        return;
                    }
                    Fail(_TF("Failed to add email address.", "Error message when the server failed to associate an email address with an account"));
                }
                catch (e) {
                    Fail(_TF("Failed to add email address.", "Error message when the server failed to associate an email address with an account") + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const policy = await getCreatePasswordPolicy();
            const policyText = AuthPolicyText(policy);

            AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
            const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));
            
            AuthLabel(target, _TF("Email address", "Label text for an input box where the user should input a valid email address")).title = policyText;
            /**
             * Validates the current password (ValidatePassword, create policy) and the e-mail address to add
             * (AuthGetEmailError, or "Enter your email address" when empty); updates marks, info line and button.
             * @param {string} uText Current password text (trimmed with AuthTrim)
             * @param {string} pwText E-mail text (trimmed with AuthTrim)
             * @returns {Promise<boolean>} false if the current password is invalid; otherwise true, even when the
             * e-mail is invalid (the button state reflects that case)
             */
            async function setStatus(uText, pwText) {
                uText = AuthTrim(uText);
                pwText = AuthTrim(pwText);
                const e = await ValidatePassword(null, uText, null, null, null, policy);
                AuthSetError(uname, e);
                if (e) {
                    AuthSetError(pwd, false);
                    AuthSetText(info, e);
                    setButton.SetEnabled(false);
                    return false;
                }
                const p = pwText.length > 0 ? await AuthGetEmailError(pwText) : _TF("Enter your email address", "Place holder text for an input field where the user should enter n email address");
                AuthSetError(pwd, p);
                if (p)
                    AuthSetText(info, p);
                else
                    AuthSetText(info, "");
                setButton.SetEnabled(!p);
                return true;
            }
            const pwd = AuthInput(target, _TF("Enter your email address", "Place holder text for an input field where the user should enter n email address"), null, "email", async input => await setStatus(uname.value, pwd.value),

                async el => {
                    if (await setStatus(uname.value, pwd.value))
                        el.click();
                });
            const info = AuthError(target, "", policyText);
            const br = AuthButtonRow(target);

            const setButton = AuthButton(br,
                _TF("Verify email address", "Text on a button that when clicked will send a verfication message to an email address"),
                _TF("Click to send verification instructions to the new email address", "Tool tip description for a button that when clicked will send a verfication message to an email address"),
                "IconAuthEmail", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                uname.readOnly = true;
                try {
                    const usr = uname.value;
                    AuthSetText(info, _TH("Sending instructions to {0}..", AuthUserHtml(usr), "Message displayed when sending instructions using email.{0} is replaced with the email address"), true);
                    const pw = pwd.value;
                    var r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const username = r.Username;
                    if (!username)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const targets = await addEmailRequest(username, usr, pw);
                    if (targets) {
                        AuthPageValidation(target,
                            _TH("Code sent to {0}.", AuthUserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                            "AddEmail.html?");
                        return;
                    }
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"));
                }
                catch (e) {
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(uname.value, pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
                pwd.focus();
            }, true);
            await setStatus("", "");
            uname.focus();
        }
    });

}









////// Change phone ///////////////////////////////

/**
 * Entry point of ChangePhone.html, page icon IconAuthChangePhone. Same as changeEmailMain, but the new value uses
 * AuthInputPhone (with a "+country code" tooltip) validated by AuthGetPhoneError; button "Verify phone number"
 * (IconAuthPhone); APIs changePhone(token) (AddPhone API) / changePhoneRequest; link "ChangePhone.html?".
 * @returns {Promise<void>}
 */
async function changePhoneMain() {

    await AuthPage("IconAuthChangePhone", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Change phone number", "Text on a button that when clicked will change the phone number assoicated with a user"),
                _TF("Clicking this button will change the phone number.", "Tool til description for a button that when clicked will change the phone number assoicated with a user"),
                "IconAuthChangePhone", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Changing phone number ..", "Message displayed while the server is changing the phone number associated with an account"));
                try {
                    const res = await changePhone(token);
                    if (res) {
                        AuthSetText(info, _TF("Phone number changed", "Message displayed when the phone number of an account have successfully been changed"));
                        return;
                    }
                    Fail(_TF("Failed to change phone number.", "Error message displayed when a request for changing the phone number associated with an account failed"));
                }
                catch (e) {
                    Fail(_TF("Failed to change phone number.", "Error message displayed when a request for changing the phone number associated with an account failed") + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const policy = await getCreatePasswordPolicy();
            const policyText = AuthPolicyText(policy);

            AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
            const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));


            const tt = _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number");
            AuthLabel(target, _TF("Phone number", "Label text for an input field where the user should enter their phone number")).title = tt;
            const epf = _TF("Enter your phone number", "Placeholder text for an input field where the user should enter their phone number");
            /**
             * Validates the current password (ValidatePassword, create policy) and the new phone number
             * (AuthGetPhoneError, or the placeholder text `epf` when empty); updates marks, info line and button.
             * @param {string} uText Current password text (trimmed with AuthTrim)
             * @param {string} pwText New phone number text (trimmed with AuthTrim)
             * @returns {Promise<boolean>} false if the current password is invalid; otherwise true, even when the
             * phone number is invalid (the button state reflects that case)
             */
            async function setStatus(uText, pwText) {
                uText = AuthTrim(uText);
                pwText = AuthTrim(pwText);
                const e = await ValidatePassword(null, uText, null, null, null, policy);
                AuthSetError(uname, e);
                if (e) {
                    AuthSetError(pwd, false);
                    AuthSetText(info, e);
                    setButton.SetEnabled(false);
                    return false;
                }
                const p = pwText.length > 0 ? await AuthGetPhoneError(pwText) : epf;
                AuthSetError(pwd, p);
                if (p)
                    AuthSetText(info, p);
                else
                    AuthSetText(info, "");
                setButton.SetEnabled(!p);
                return true;
            }
            const pwd = AuthInputPhone(target, epf, tt, "phone", async input => await setStatus(uname.value, pwd.value),

                async el => {
                    if (await setStatus(uname.value, pwd.value))
                        el.click();
                });
            const info = AuthError(target, "", policyText);
            const br = AuthButtonRow(target);

            const setButton = AuthButton(br,
                _TF("Verify phone number", "Text on a button then when pressed will send a verification text message to the supplied phone number"),
                _TF("Click to send a verification instructions to the new phone number", "Tool tip description for a button then when pressed will send a verification text message to the supplied phone number"),
                "IconAuthPhone", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                uname.readOnly = true;
                try {
                    const usr = uname.value;
                    AuthSetText(info, _TH("Sending instructions to {0}..", AuthUserHtml(usr), "Message displayed when sending instructions using text message to a phone.{0} is replaced with the phone number"), true);
                    const pw = pwd.value;
                    var r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const username = r.Username;
                    if (!username)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const targets = await changePhoneRequest(username, usr, pw);
                    if (targets) {
                        AuthPageValidation(target,
                            _TH("Code sent to {0}.", AuthUserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                            "ChangePhone.html?");
                        //AuthSetText(info, "Sent verification instructions to " + AuthUserHtml(targets) + ".", true);
                        //setButton.SetEnabled(false);
                        return;
                    }
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"));
                }
                catch (e) {
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(uname.value, pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
                pwd.focus();
            }, true);
            await setStatus("", "");
            uname.focus();
        }
    });

}

////// Delete phone ///////////////////////////////

/**
 * Entry point of DeletePhone.html, page icon IconAuthDeletePhone. Same as deleteEmailMain with
 * "Delete phone number" (IconAuthDeletePhone) and deletePhone(username, password).
 * @returns {Promise<void>}
 */
async function deletePhoneMain() {

    await AuthPage("IconAuthDeletePhone", async target => {

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value));

        /**
         * Validates the current password only (ValidatePassword, create policy); enables the button when valid.
         * @param {string} uText Current password text (trimmed with AuthTrim)
         * @returns {Promise<boolean>} true if valid
         */
        async function setStatus(uText) {
            uText = AuthTrim(uText);
            const e = await ValidatePassword(null, uText, null, null, null, policy);
            AuthSetError(uname, e);
            if (e) {
                AuthSetText(info, e);
                setButton.SetEnabled(false);
                return false;
            }
            AuthSetText(info, "");
            setButton.SetEnabled(true);
            return true;
        }
        const info = AuthError(target, "", policyText);
        const br = AuthButtonRow(target);

        const setButton = AuthButton(br,
            _TF("Delete phone number", "Text on a button that when pressed will remove the phone number associated with an account"),
            _TF("Click to remove the phone number associated with this account", "Tool tip description for a button that when pressed will remove the phone number associated with an account"),
            "IconAuthDeletePhone", async button => {
            button.StartWorking();
            uname.readOnly = true;
            try {
                const usr = uname.value;
                AuthSetText(info, _TF('Deleting phone number ..', "Text displayed when the server is deleting a phone number from an account"));
                var r = await sendRequest("../Api/auth/GetUser");
                if (!r.Succeeded)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const username = r.Username;
                if (!username)
                    throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                const targets = await deletePhone(username, usr);
                if (targets) {
                    AuthSetText(info, _TH("Sent restore instructions to {0}.", AuthUserHtml(targets), "Text displayed when a phone number is deleted from an account.{0} is replaced with the phone number"), true);
                    setButton.SetEnabled(false);
                    return;
                }
                Fail(_TF("Failed to delete phone number.", "Error message displayed when the server failed to delete a phone number from an account"));
            }
            catch (e) {
                Fail(_TF("Failed to delete phone number.", "Error message displayed when the server failed to delete a phone number from an account") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            await setStatus(uname.value);
            SetTemporaryInnerText(info, _TF("Failed to delete phone number, try again!?", "Message displayed when the server failed to delete a phone number from an account"), 5000);
            uname.readOnly = false;
        }, true);
        await setStatus(uname.value);
        uname.focus();
    }
    );

}


////// Add phone ///////////////////////////////

/**
 * Entry point of AddPhone.html, page icon IconAuthAddPhone. Same as changePhoneMain with addPhone(token)
 * (confirm button "Add phone number", IconAuthAddPhone) / addPhoneRequest and the link "AddPhone.html?".
 * @returns {Promise<void>}
 */
async function addPhoneMain() {

    await AuthPage("IconAuthAddPhone", async target => {

        let token = decodeURIComponent(window.location.search ?? "");
        if (token) {
            if (token.length <= 3)
                return Fail(_TF("Token not found or invalid", "An error message shown when a required security token wasn't supplied or is invalid"));
            token = token.substring(1);
            const br = AuthButtonRow(target);
            const button = AuthButton(br,
                _TF("Add phone number", "Text on a button that when clicked will associate a phone number with an account"),
                _TF("Clicking this button will associate the phone number with your account.", "Tool tip description for a button that when clicked will associate a phone number with an account"),
                "IconAuthAddPhone", async () => {
                br.style.display = "none";
                const info = AuthText(target, _TF("Adding phone number ..", "Message displayed when the server is associating a phone number with an account"));
                try {
                    const res = await addPhone(token);
                    if (res) {
                        AuthSetText(info, _TF("Phone number added", "Message displayed when a phone number was successfully associated with an acount"));
                        return;
                    }
                    Fail(_TF("Failed to add phone number.", "Error message displayed when the server failed to associate a phone number with an account"));
                }
                catch (e) {
                    Fail(_TF("Failed to add phone number.", "Error message displayed when the server failed to associate a phone number with an account") + "\n\n" + e);
                }
                br.style.display = null;
            });
        } else {
            const policy = await getCreatePasswordPolicy();
            const policyText = AuthPolicyText(policy);

            AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
            const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));

            const tt = _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number");
            AuthLabel(target, _TF("Phone number", "Label text for an input field where the user should enter their phone number")).title = tt;
            const epf = _TF("Enter your phone number", "Placeholder text for an input field where the user should enter their phone number");
            /**
             * Validates the current password (ValidatePassword, create policy) and the phone number to add
             * (AuthGetPhoneError, or the placeholder text `epf` when empty); updates marks, info line and button.
             * @param {string} uText Current password text (trimmed with AuthTrim)
             * @param {string} pwText Phone number text (trimmed with AuthTrim)
             * @returns {Promise<boolean>} false if the current password is invalid; otherwise true, even when the
             * phone number is invalid (the button state reflects that case)
             */
            async function setStatus(uText, pwText) {
                uText = AuthTrim(uText);
                pwText = AuthTrim(pwText);
                const e = await ValidatePassword(null, uText, null, null, null, policy);
                AuthSetError(uname, e);
                if (e) {
                    AuthSetError(pwd, false);
                    AuthSetText(info, e);
                    setButton.SetEnabled(false);
                    return false;
                }
                const p = pwText.length > 0 ? await AuthGetPhoneError(pwText) : epf;
                AuthSetError(pwd, p);
                if (p)
                    AuthSetText(info, p);
                else
                    AuthSetText(info, "");
                setButton.SetEnabled(!p);
                return true;
            }
            const pwd = AuthInputPhone(target, epf, tt, "phone", async input => await setStatus(uname.value, pwd.value),

                async el => {
                    if (await setStatus(uname.value, pwd.value))
                        el.click();
                });
            const info = AuthError(target, "", policyText);
            const br = AuthButtonRow(target);

            const setButton = AuthButton(br,
                _TF("Verify phone number", "Text on a button then when pressed will send a verification text message to the supplied phone number"),
                _TF("Click to send a verification instructions to the new phone number", "Tool tip description for a button then when pressed will send a verification text message to the supplied phone number"),
                "IconAuthPhone", async button => {
                button.StartWorking();
                pwd.readOnly = true;
                uname.readOnly = true;
                try {
                    const usr = uname.value;
                    AuthSetText(info, _TH("Sending instructions to {0}..", AuthUserHtml(usr), "Message displayed when sending instructions using text message to a phone.{0} is replaced with the phone number"), true);
                    const pw = pwd.value;
                    var r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const username = r.Username;
                    if (!username)
                        throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
                    const targets = await addPhoneRequest(username, usr, pw);
                    if (targets) {
                        AuthPageValidation(target,
                            _TH("Code sent to {0}.", AuthUserHtml(targets), "Message displayed when a code was sent to one or more email addresses and/or phone numbers.{0} is replaced with a list of email addresses and/or phone numbers"),
                            "AddPhone.html?");
                        //AuthSetText(info, "Sent verification instructions to " + AuthUserHtml(targets) + ".", true);
                        //setButton.SetEnabled(false);
                        return;
                    }
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms"));
                }
                catch (e) {
                    Fail(_TF("Failed to send instructions.", "Error message displayed when the server failed to send instructions using email and/or sms") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(uname.value, pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to send instructions, try again!?", "Message displayed when the server failed to send instructions using email and/or sms"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
                pwd.focus();
            }, true);
            await setStatus("", "");
            uname.focus();
        }
    });

}

