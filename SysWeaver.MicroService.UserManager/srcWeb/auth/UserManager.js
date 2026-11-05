/**
 * @file UserManager.js - the UI-less api of the user manager service (SysWeaver.MicroService.UserManager, served as
 * auth/UserManager.js).
 *
 * Purpose: `Sw.UserManager`, one function per UserManagerService / SignUpService call (all under Api/auth/):
 *  - Password proof: sensitive calls prove the current password with Sw.Auth.Proof (GetUserSalt -> [salt, pad],
 *    Hash = b64(sha256(b64(sha256(pw|salt))|pad)), OneTimePad); the plain password never leaves the browser.
 *    New passwords are sent as NewHash = Sw.Auth.PasswordHash(newPassword, salt).
 *  - Token arguments: the long token of an e-mail / text message link or the short code stored in the HTTP session.
 *  - "Message senders" post 0 and return the targets (e-mails / phone numbers) a message was sent to, or null.
 *  - Successful sign ins (AddUser, ResetPassword) are remembered in sessionStorage["SysWeaver.User"].
 *
 * Loading: classic `defer` script after ../sw/core.js (or common/common.js) and Auth.js (MicroService.Auth).
 * Globals: `Sw.UserManager` and the old global function names (legacy aliases, kept for application code):
 * setPasswordRequest, getCreatePasswordPolicy, addUserRequest, sendDeleteUserRequest, deleteUserRequest, signUpRequest,
 * forgotPasswordRequest, resetPasswordRequest, sendAddPasswordRequest, sendDeletePasswordRequest, deletePassword,
 * inviteUserRequest, changeEmailRequest, changeEmail, deleteEmail, addEmailRequest, addEmail, changePhoneRequest,
 * changePhone, deletePhone, addPhoneRequest, addPhone, setNickName, setLanguage, isManagedUser, userManagerProps
 * (validateShortCode / cleanUpShortCode now live in Auth.js).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Auth)
        throw new Error("auth/UserManager.js requires sw/core.js and auth/Auth.js");
    const A = Sw.Auth;
    const T = A.Trim;

    /**
     * Prove the current password and call an api with the proof plus extra members.
     * @param {string} name Api name.
     * @param {string} user User id.
     * @param {string} password Current password.
     * @param {object} [extra] Extra members.
     * @returns {Promise<*>} The result.
     */
    async function withProof(name, user, password, extra) {
        const p = await A.Proof(user, password);
        return await A.Call(name, Object.assign({ Hash: p.Hash, OneTimePad: p.OneTimePad }, extra));
    }

    /**
     * Remember a sign in result.
     * @param {object} r The result.
     * @returns {object} r.
     */
    function remember(r) {
        A.SetUser(r && (r.Error === undefined || r.Error == 0) ? r : null);
        return r;
    }

    /**
     * Get the signed in user name (throws "No user signed in!").
     * @returns {Promise<string>} The user name.
     */
    async function userName() {
        const u = await A.GetUser();
        if (!u || !u.Username)
            throw new Error(_TF("No user signed in!", "Error message displayed when a user action is performed while no user is signed in"));
        return u.Username;
    }

    /** @namespace Sw.UserManager - the user manager api (see the file header). */
    const UM = Sw.UserManager = {
        UserName: userName,
        /**
         * Change the password of the signed in user (proof of the current one).
         * @param {string} user User id. @param {string} password Current password. @param {string} newPassword New password.
         * @returns {Promise<boolean>} True if changed.
         */
        async SetPassword(user, password, newPassword) {
            const p = await A.Proof(user, password);
            return await A.Call("SetPassword", { Hash: p.Hash, OneTimePad: p.OneTimePad, NewHash: await A.PasswordHash(T(newPassword), p.Salt) });
        },
        /** @returns {Promise<object>} The policy of new passwords (GetCreatePasswordPolicy). */
        CreatePolicy: () => A.CreatePolicy(),
        /**
         * Complete a sign up / invite with a password (signs in).
         * @param {string} token Token or short code. @param {string} salt Salt from NewUserData. @param {string} newPassword Password.
         * @returns {Promise<object>} AuthInfo or null.
         */
        async AddUser(token, salt, newPassword) {
            return remember(await A.Call("AddUser", { Token: token, NewHash: await A.PasswordHash(T(newPassword), salt) }));
        },
        /** @param {string} token Token. @returns {Promise<object>} {Salt, NickName, UserName} of a sign up / invite token, or null. */
        NewUserData: token => A.Call("GetNewUserData", token),
        /** @param {string} token Token. @returns {Promise<object>} {Salt, NickName, UserName, Forced, CurrentSalt} of a reset / add password token (a dead token throws: 410 with the reason). */
        NewPasswordData: token => A.Call("GetNewPasswordData", token),
        /** @returns {Promise<string[]>} Send delete account instructions; the targets or null. */
        SendDeleteUserRequest: () => A.Call("SendDeleteUserRequest", 0),
        /** @param {string} token Token. @returns {Promise<string>} Delete the account; the deleted user name or null. */
        DeleteUser: token => A.Call("DeleteUser", token),
        /** @param {string} token Token (the link in the changed e-mail notice). @returns {Promise<string>} Undo an e-mail address change (no sign in): the old address, where the reset link was sent; 410 for a dead link, 409 if another account now uses the old address. */
        UndoEmailChange: token => A.Call("UndoEmailChange", token),
        /** @returns {Promise<string[]>} The sign up methods (com names: "Email", "Sms", "WhatsApp"; older servers "Phone"). */
        SignUpMethods: () => A.Get("SignUpMethods"),
        /** @param {string} target E-mail / phone. @returns {Promise<boolean>} Send sign up instructions. */
        SignUp: target => A.Call("SignUp", T(target)),
        /** @param {string} userId User id. @returns {Promise<string[]>} Send reset instructions; always [userId trimmed] (the same for an unknown user id, never the account's addresses: word the result as "if an account uses ..."), 400 for an empty id. */
        ForgotPassword: userId => A.Call("ForgotPassword", T(userId)),
        /**
         * Check a password reset code in any browser (the code belongs to the account); a right code becomes this session's short code (then open ResetPassword.html?<code>).
         * @param {string} userId User id, null / empty for the one this session asked a reset for. @param {string} code The code.
         * @returns {Promise<number>} 1 right, 0 wrong or expired (also for an unknown user id), -2 too many wrong codes for the user id (wait); 400 for no user id.
         */
        ValidateResetCode: (userId, code) => A.Call("ValidateResetCode", { UserId: T(userId) || null, Code: code }),
        /**
         * Set a new password from a reset / add password token (signs in).
         * @param {string} token Token. @param {string} newPassword Password. @param {string} salt Salt from NewPasswordData.
         * @param {string} [currentSalt] NewPasswordData.CurrentSalt (set when an administrator required the new password): the hash of the new
         * password with it is sent too (CurrentHash), so the server can refuse the current password (Error 5, UM.Errors.SamePassword).
         * @returns {Promise<object>} {Error (0 = ok), ...} or null.
         */
        async ResetPassword(token, newPassword, salt, currentSalt) {
            const req = { Token: token, NewHash: await A.PasswordHash(T(newPassword), salt) };
            if (currentSalt)
                req.CurrentHash = await A.PasswordHash(T(newPassword), currentSalt);
            return remember(await A.Call("ResetPassword", req));
        },
        /** The UserErrors values of UserLoginResponse.Error that pages handle. */
        Errors: { None: 0, TokenExpired: 1, SamePassword: 5 },
        /** @returns {Promise<string[]>} Send add password instructions; the targets or null. */
        SendAddPasswordRequest: () => A.Call("SendAddPasswordRequest", 0),
        /** @returns {Promise<string[]>} Send delete password instructions; the targets or null. */
        SendDeletePasswordRequest: () => A.Call("SendDeletePasswordRequest", 0),
        /** @param {string} token Token. @returns {Promise<boolean>} Delete the password. */
        DeletePassword: token => A.Call("DeletePassword", token),
        /**
         * Invite a user (role InviteUser).
         * @param {string} target E-mail. @param {string} [name] User name. @param {string[]} [tokens] Security tokens. @param {string} [domain] Domain.
         * @returns {Promise<boolean>} True if sent.
         */
        Invite(target, name, tokens, domain) {
            const tk = (tokens || []).map(x => T(x)).filter(x => x);
            return A.Call("InviteUser", { Email: T(target), Name: T(name), Tokens: tk.length ? tk : null, Domain: domain });
        },
        /** @param {string} user User. @param {string} password Password. @param {string} email E-mail. @returns {Promise<string[]>} Targets. */
        SendAddEmailRequest: (user, password, email) => withProof("SendAddEmailRequest", user, password, { Email: T(email) }),
        /** @param {string} user User. @param {string} password Password. @param {string} email E-mail. @returns {Promise<string[]>} Targets. */
        SendChangeEmailRequest: (user, password, email) => withProof("SendChangeEmailRequest", user, password, { Email: T(email) }),
        /** @param {string} token Token. @returns {Promise<boolean>} Add / change the e-mail address (AddEmail). */
        AddEmail: token => A.Call("AddEmail", token),
        /** @param {string} user User. @param {string} password Password. @returns {Promise<string[]>} Restore instruction targets. */
        DeleteEmail: (user, password) => withProof("DeleteEmail", user, password),
        /** @param {string} user User. @param {string} password Password. @param {string} phone Phone. @returns {Promise<string[]>} Targets. */
        SendAddPhoneRequest: (user, password, phone) => withProof("SendAddPhoneRequest", user, password, { Phone: T(phone) }),
        /** @param {string} user User. @param {string} password Password. @param {string} phone Phone. @returns {Promise<string[]>} Targets. */
        SendChangePhoneRequest: (user, password, phone) => withProof("SendChangePhoneRequest", user, password, { Phone: T(phone) }),
        /** @param {string} token Token. @returns {Promise<boolean>} Add / change the phone number (AddPhone). */
        AddPhone: token => A.Call("AddPhone", token),
        /** @param {string} user User. @param {string} password Password. @returns {Promise<string[]>} Restore instruction targets. */
        DeletePhone: (user, password) => withProof("DeletePhone", user, password),
        /** @param {string} name Nick name. @returns {Promise<boolean>} True if changed (pages of the user may refresh). */
        SetNickName: name => A.Call("SetNickName", name),
        /** @param {string} lang ISO 639-1 [-ISO 3166] code. @returns {Promise<boolean>} True if changed. */
        SetLanguage: lang => A.Call("SetLanguage", lang),
        /** @returns {Promise<boolean>} True if the signed in user is managed by the user manager. */
        IsManagedUser: () => A.Get("IsManagedUser"),
        /** @returns {Promise<object>} Misc user manager properties. */
        Props: () => A.Get("UserManagerProps"),
    };

    // ------------------------------------------------------------------ legacy globals

    /**
     * Define a legacy global unless already defined.
     * @param {string} name Name.
     * @param {*} value Value.
     */
    function legacy(name, value) {
        if (!(name in window))
            window[name] = value;
    }

    legacy("setPasswordRequest", UM.SetPassword);
    legacy("getCreatePasswordPolicy", UM.CreatePolicy);
    legacy("addUserRequest", UM.AddUser);
    legacy("sendDeleteUserRequest", UM.SendDeleteUserRequest);
    legacy("deleteUserRequest", UM.DeleteUser);
    legacy("signUpRequest", UM.SignUp);
    legacy("forgotPasswordRequest", UM.ForgotPassword);
    legacy("resetPasswordRequest", UM.ResetPassword);
    legacy("sendAddPasswordRequest", UM.SendAddPasswordRequest);
    legacy("sendDeletePasswordRequest", UM.SendDeletePasswordRequest);
    legacy("deletePassword", UM.DeletePassword);
    legacy("inviteUserRequest", UM.Invite);
    legacy("changeEmailRequest", UM.SendChangeEmailRequest);
    legacy("changeEmail", UM.AddEmail);
    legacy("deleteEmail", UM.DeleteEmail);
    legacy("addEmailRequest", UM.SendAddEmailRequest);
    legacy("addEmail", UM.AddEmail);
    legacy("changePhoneRequest", UM.SendChangePhoneRequest);
    legacy("changePhone", UM.AddPhone);
    legacy("deletePhone", UM.DeletePhone);
    legacy("addPhoneRequest", UM.SendAddPhoneRequest);
    legacy("addPhone", UM.AddPhone);
    legacy("setNickName", UM.SetNickName);
    legacy("setLanguage", UM.SetLanguage);
    legacy("isManagedUser", UM.IsManagedUser);
    legacy("userManagerProps", UM.Props);
})();