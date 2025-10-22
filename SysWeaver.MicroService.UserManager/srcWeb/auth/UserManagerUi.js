
////// Set password ///////////////////////////////



async function setPasswordMain() {

    await AuthPage("IconAuthSetPassword", async target => {
        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);
        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;

        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value, pwd.value));

        AuthLabel(target, _TF("New password", "Label text for an input box where the user should enter a new password")).title = policyText;
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
        const havePasskey = typeof PassKey !== "undefined";
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
                try {
                    AuthSetText(info, _TF("Create passkey..", "Message shown when a passkey is being created"));
                    const res = await PassKey.NewAccount(token);
                    if (res) {
                        AuthSetText(info, _TF("Passkey created", "Message shown when a new passkey was created successfully"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created"));
                }
                catch (e) {
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to create passkey, try again!?", "Message shown when a new passkey couldn't be created"), 5000);
                pwd.readOnly = false;
            }, false);
        }
        await setStatus("");
        pwd.focus();


    });

}


////// Delete user account ///////////////////////////////


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
                g.SetStatus = () => setEmailStatus(uname);
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
                g.SetStatus = () => setPhoneStatus(uname);
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

        const havePasskey = typeof PassKey !== "undefined";

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target,
            havePasskey
                ?
                _TF("Choose a new password or create a passkey", "A message letting a user know that they should provide a new password or assign a new passkey")
                :
                _TF("Choose a new password", "A message letting a user know that they should provide a new password")
        ).title = policyText;

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
                try {
                    AuthSetText(info, _TF("Create passkey..", "Message shown when a passkey is being created"));
                    const res = await PassKey.AttachNewToAccountFromToken(token);
                    if (res) {
                        AuthSetText(info, _TF("Passkey created", "Message shown when a new passkey was created successfully"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created"));
                }
                catch (e) {
                    Fail(_TF("Failed to create passkey.", "Error message shown when a new passkey couldn't be created") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to create passkey, try again!?", "Message shown when a new passkey couldn't be created"), 5000);
                pwd.readOnly = false;
            }, false);
        }
        await setStatus("");
        pwd.focus();


    });
}


////// Add password ///////////////////////////////
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

async function deleteEmailMain() {

    await AuthPage("IconAuthDeleteEmail", async target => {

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value));

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

async function deletePhoneMain() {

    await AuthPage("IconAuthDeletePhone", async target => {

        const policy = await getCreatePasswordPolicy();
        const policyText = AuthPolicyText(policy);

        AuthLabel(target, _TF("Current password", "Label text for an input box where the user should enter their current password")).title = policyText;
        const uname = AuthPassword(target, _TF("Enter the current password", "Placeholder text for an input box where the user should enter their current password"), null, "", async input => await setStatus(input.value));

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

