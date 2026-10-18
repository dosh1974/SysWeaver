/**
 * @file UserManager.js - API wrappers for the user manager service (SysWeaver.MicroService.UserManager, served as auth/).
 *
 * Loading: `<script defer>` in all 15 account pages of this folder (SignUp, InviteUser, ChoosePassword, SetPassword,
 * AddPassword, DeletePassword, ForgotPassword, ResetPassword, Add/Change/DeleteEmail, Add/Change/DeletePhone,
 * DeleteAccount), after ../common/common.js, Auth.js and AuthUi.js and before UserManagerUi.js.
 * (ChoosePassword.html / ResetPassword.html also load Passkey.js before Auth.js.)
 *
 * Globals defined: only global (async) function declarations, no variables, no code runs at load.
 * Every URL is the hard-coded prefix `../Api/auth/` (UserManagerService / SignUpService on the server;
 * GetUserSalt belongs to the LoginService of SysWeaver.MicroService.Auth).
 *
 * Dependencies:
 * - common.js (SysWeaver.HttpServer): sendRequest (POST JSON), getRequest (GET), hashString (SHA256 hex).
 * - Auth.js (SysWeaver.MicroService.Auth, also served as auth/): AuthTrim (input normalization).
 *
 * Conventions:
 * - "Proof hash": the password functions fetch `[salt, oneTimePad]` from GetUserSalt(user) and send
 *   `Hash = SHA256(SHA256(password|salt)|oneTimePad)` plus `OneTimePad`, so the plain password never leaves
 *   the browser. New passwords are sent as `NewHash = SHA256(newPassword|salt)`. This file does not handle the
 *   binary-salt ("|" prefixed) variant that loginRequest in Auth.js supports.
 * - Token arguments are either the long token from an e-mail / text message link or the short numeric code
 *   that the server stored in the current HTTP session (accepted only in that session).
 * - "Message senders" post the body `0` and return the array of targets (e-mails / phone numbers) a message
 *   was sent to, or null.
 *
 * Cross-folder consumers: validateShortCode and cleanUpShortCode are called by AuthPageValidation in AuthUi.js
 * (SysWeaver.MicroService.Auth), which implements the short-code step for every page of this folder that
 * sends a message. All other functions are used by UserManagerUi.js (or kept for application code).
 */

////// Set password ///////////////////////////////

/**
 * Set a new password for the signed-in user; requires the existing password.
 * Trims all inputs, fetches `[salt, oneTimePad]` from ../Api/auth/GetUserSalt and posts
 * `SetPassword {Hash: proof hash of password, OneTimePad, NewHash: SHA256(newPassword|salt)}`.
 * Used by setPasswordMain (UserManagerUi.js).
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPassword The new password (must meet the password policy)
 * @returns {Promise<boolean>} true if the password was changed successfully (server result)
 */
async function setPasswordRequest(user, password, newPassword) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    newPassword = AuthTrim(newPassword);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    var newStr = await hashString(newPassword + "|" + salt)
    const result = await sendRequest("../Api/auth/SetPassword", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
        NewHash: newStr,
    });
    return result;
}

/**
 * Get the password policy for creating a new password (GET ../Api/auth/GetCreatePasswordPolicy).
 * The policy object is passed to ValidatePassword / AuthPolicyText (Auth.js) and AuthNewPassword (AuthUi.js)
 * by every password form in UserManagerUi.js.
 * @returns {Promise<object>} The password policy
 */
async function getCreatePasswordPolicy() {

    const apiPrefix = "../Api/auth/";
    return await getRequest(apiPrefix + "GetCreatePasswordPolicy");
}

////// Choose password ///////////////////////////////

/**
 * Add a new user (complete a sign-up / invite) by choosing a password.
 * Posts `AddUser {Token, NewHash: SHA256(newPassword|salt)}`; the server signs the session in.
 * Side effect: on a truthy result stores it as JSON in sessionStorage["SysWeaver.User"], otherwise removes that key.
 * Used by choosePasswordMain (UserManagerUi.js).
 * @param {string} token The token (or session short code) that was sent (using email, phone etc)
 * @param {string} salt The user salt, from GetNewUserData(token)
 * @param {string} newPassword The new password (must meet the password policy), trimmed with AuthTrim
 * @returns {Promise<{Error:number, Username:string, Tokens:Array<string>}>} A user login response or null if failed
 */
async function addUserRequest(token, salt, newPassword) {
    newPassword = AuthTrim(newPassword);
    var newStr = await hashString(newPassword + "|" + salt)
    const result = await sendRequest("../Api/auth/AddUser", {
        Token: token,
        NewHash: newStr,
    });
    if (result)
        sessionStorage.setItem("SysWeaver.User", JSON.stringify(result));
    else
        sessionStorage.removeItem("SysWeaver.User");
    return result;
}

////// Delete user account ///////////////////////////////

/**
 * Ask the server to send delete-account instructions (link + session short code) to the signed-in user
 * (using email, phone etc). Posts body `0` to SendDeleteUserRequest. Used by deleteAccountMain.
 * @returns {Promise<Array<string>>} An array with all targets that a message was sent to (emails, phone numbers), or null
 */
async function sendDeleteUserRequest() {

    return await sendRequest("../Api/auth/SendDeleteUserRequest", 0);
}

/**
 * Delete a user account (posts the token to DeleteUser). Used by deleteAccountMain in token mode.
 * @param {string} token The delete user token that was sent (using email, phone etc) or the session short code
 * @returns {Promise<string>} The user name of the user that was deleted or null if no user was deleted
 */
async function deleteUserRequest(token) {

    return await sendRequest("../Api/auth/DeleteUser", token);
}


////// Sign up ///////////////////////////////

/**
 * Sign up (joining) the service: trims the target and posts it to SignUp (SignUpService; refused when signed in).
 * The server sends a link + session short code to the target. Used by signUpMain.
 * @param {string} target Any of the server side supported sign up methods, typically email address or phone number
 * @returns {Promise<boolean>} true if a sign up request was sent to the target
 */
async function signUpRequest(target) {
    target = AuthTrim(target);
    return await sendRequest("../Api/auth/SignUp", target);
}


////// Forgot password ///////////////////////////////

/**
 * Send a reset password link (and session short code) to a user: trims userId and posts it to ForgotPassword.
 * Used by forgotPasswordMain.
 * @param {string} userId Any of the server side user identification methods, typically user name, email address or phone number
 * @returns {Promise<Array<string>>} An array with all targets that a message was sent to (emails, phone numbers), or null
 */
async function forgotPasswordRequest(userId) {

    userId = AuthTrim(userId);
    return await sendRequest("../Api/auth/ForgotPassword", userId);
}


////// Reset password ///////////////////////////////

/**
 * Set a new password (from data sent to user): posts `ResetPassword {Token, NewHash: SHA256(newPassword|newSalt)}`;
 * the server signs the session in. Side effect: on a truthy result stores it as JSON in
 * sessionStorage["SysWeaver.User"], otherwise removes that key.
 * Used by resetPasswordMain and addPasswordMain (callers require result.Error == 0).
 * @param {string} token The reset password token that was sent (using email, phone etc) or the session short code
 * @param {string} newPassword The new password (must meet the password policy), trimmed with AuthTrim
 * @param {string} newSalt The user salt, from GetNewPasswordData(token)
 * @returns {Promise<{Error:number, Username:string, Tokens:Array<string>}>} A user login response or null if failed
 */
async function resetPasswordRequest(token, newPassword, newSalt) {

    newPassword = AuthTrim(newPassword);
    const newStr = await hashString(newPassword + "|" + newSalt)
    const result = await sendRequest("../Api/auth/ResetPassword", {
        Token: token,
        NewHash: newStr,
    });
    if (result)
        sessionStorage.setItem("SysWeaver.User", JSON.stringify(result));
    else
        sessionStorage.removeItem("SysWeaver.User");
    return result;
}


////// Add password ///////////////////////////////

/**
 * Send an add password link (and session short code) to the signed-in user (e.g. a passkey-only account).
 * Posts body `0` to SendAddPasswordRequest. Used by addPasswordMain in request mode.
 * @returns {Promise<Array<string>>} An array with all targets that a message was sent to (emails, phone numbers), or null
 */
async function sendAddPasswordRequest() {

    return await sendRequest("../Api/auth/SendAddPasswordRequest", 0);
}


////// Delete password ///////////////////////////////

/**
 * Send a delete password link (and session short code) to the signed-in user.
 * Posts body `0` to SendDeletePasswordRequest. Used by deletePasswordMain in request mode.
 * @returns {Promise<Array<string>>} An array with all targets that a message was sent to (emails, phone numbers), or null
 */
async function sendDeletePasswordRequest() {

    return await sendRequest("../Api/auth/SendDeletePasswordRequest", 0);
}

/**
 * Deletes the user's password (posts the token to DeletePassword). Used by deletePasswordMain in token mode.
 * @param {string} token The delete password token that was sent (using email, phone etc) or the session short code
 * @returns {Promise<boolean>} True if the password was deleted
 */
async function deletePassword(token) {

    return await sendRequest("../Api/auth/DeletePassword", token);
}


////// Invite user ///////////////////////////////

/**
 * Invite a user to join (privileged, requires the InviteUser role on the server).
 * Trims target and userName; tokens are trimmed one by one, empty ones dropped and an empty list becomes null.
 * Posts `InviteUser {Email: target, Name: userName, Tokens, Domain: domain}`; the invitee gets a
 * ChoosePassword.html link. Used by inviteUserMain (which never passes domain).
 * @param {string} target Any of the server side supported sign up methods, typically email address or phone number
 * @param {string} userName An optional user name to give this user
 * @param {Array<string>} [tokens] An optional set of auth (security) tokens to give
 * @param {string} [domain] An optional domain (application specific) to assign the user to
 * @returns {Promise<boolean>} True if an invite was sent
 */
async function inviteUserRequest(target, userName, tokens, domain) {

    target = AuthTrim(target);
    userName = AuthTrim(userName);
    if (tokens) {
        const ok = [];
        const l = tokens.length;
        for (let i = 0; i < l; ++i) {
            const q = AuthTrim(tokens[i]);
            if (q.length > 0)
                ok.push(q);
        }
        tokens = ok.length > 0 ? ok : null;
    }
    return await sendRequest("../Api/auth/InviteUser", {
        Email: target,
        Name: userName,
        Tokens: tokens,
        Domain: domain
    });
}

////// Change email ///////////////////////////////

/**
 * Request a change of the email address associated with a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `SendChangeEmailRequest {Hash, OneTimePad, Email: newEmail}`.
 * The server sends a link + session short code to the NEW address. Used by changeEmailMain in request mode.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newEmail The new email
 * @returns {Promise<Array<string>>} An array with all emails that a message was sent to (always just one if successful), or null
 */
async function changeEmailRequest(user, password, newEmail) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    newEmail = AuthTrim(newEmail);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/SendChangeEmailRequest", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
        Email: newEmail,
    });
    return result;
}

/**
 * Change (or set) a user's email address. Posts the token to AddEmail (the same API as addEmail; the pair
 * differs only by name). Used by changeEmailMain in token mode.
 * @param {string} token The change email address token that was sent (using email) or the session short code
 * @returns {Promise<boolean>} True if the email address was changed
 */
async function changeEmail(token) {

    return await sendRequest("../Api/auth/AddEmail", token);
}

////// Delete email ///////////////////////////////

/**
 * Delete the email address associated with a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `DeleteEmail {Hash, OneTimePad}`. Used by deleteEmailMain.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @returns {Promise<Array<string>>} An array with all targets that restore instructions were sent to, or null
 */
async function deleteEmail(user, password) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/DeleteEmail", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
    });
    return result;
}


////// Add email ///////////////////////////////

/**
 * Request adding an email address to a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `SendAddEmailRequest {Hash, OneTimePad, Email: newEmail}`.
 * The server sends a link + session short code to the new address. Used by addEmailMain in request mode.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newEmail The new email
 * @returns {Promise<Array<string>>} An array with all emails that a message was sent to (always just one if successful), or null
 */
async function addEmailRequest(user, password, newEmail) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    newEmail = AuthTrim(newEmail);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/SendAddEmailRequest", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
        Email: newEmail,
    });
    return result;
}

/**
 * Add an email address: posts the token to AddEmail (same API as changeEmail). Used by addEmailMain in token mode.
 * @param {string} token The add email address token that was sent (using email) or the session short code
 * @returns {Promise<boolean>} True if the email address was added
 */
async function addEmail(token) {

    return await sendRequest("../Api/auth/AddEmail", token);
}




////// Change phone ///////////////////////////////

/**
 * Request a change of the phone number associated with a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `SendChangePhoneRequest {Hash, OneTimePad, Phone: newPhone}`.
 * The server sends a link + session short code to the NEW number. Used by changePhoneMain in request mode.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPhone The new phone number
 * @returns {Promise<Array<string>>} An array with all phones that a message was sent to (always just one if successful), or null
 */
async function changePhoneRequest(user, password, newPhone) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    newPhone = AuthTrim(newPhone);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/SendChangePhoneRequest", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
        Phone: newPhone,
    });
    return result;
}

/**
 * Change (or set) a user's phone number. Posts the token to AddPhone (the same API as addPhone).
 * Used by changePhoneMain in token mode.
 * @param {string} token The change phone number token that was sent (using phone) or the session short code
 * @returns {Promise<boolean>} True if the phone number was changed
 */
async function changePhone(token) {

    return await sendRequest("../Api/auth/AddPhone", token);
}

////// Delete phone ///////////////////////////////

/**
 * Delete the phone number associated with a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `DeletePhone {Hash, OneTimePad}`. Used by deletePhoneMain.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @returns {Promise<Array<string>>} An array with all targets that restore instructions were sent to, or null
 */
async function deletePhone(user, password) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/DeletePhone", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
    });
    return result;
}


////// Add phone ///////////////////////////////

/**
 * Request adding a phone number to a user; requires the existing password.
 * Proof hash via GetUserSalt, then posts `SendAddPhoneRequest {Hash, OneTimePad, Phone: newPhone}`.
 * The server sends a link + session short code to the new number. Used by addPhoneMain in request mode.
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPhone The new phone number
 * @returns {Promise<Array<string>>} An array with all phones that a message was sent to (always just one if successful), or null
 */
async function addPhoneRequest(user, password, newPhone) {
    user = AuthTrim(user);
    password = AuthTrim(password);
    newPhone = AuthTrim(newPhone);
    const saltPad = await sendRequest("../Api/auth/GetUserSalt", user);
    const salt = saltPad[0];
    const oneTimePad = saltPad[1];
    var hashStr = await hashString(password + "|" + salt)
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest("../Api/auth/SendAddPhoneRequest", {
        Hash: hashStr,
        OneTimePad: oneTimePad,
        Phone: newPhone,
    });
    return result;
}

/**
 * Add a phone number: posts the token to AddPhone (same API as changePhone). Used by addPhoneMain in token mode.
 * @param {string} token The add phone number token that was sent (using phone) or the session short code
 * @returns {Promise<boolean>} True if the phone number was added
 */
async function addPhone(token) {

    return await sendRequest("../Api/auth/AddPhone", token);
}


/**
 * Set the nick name of the currently logged in user.
 * If changed, a refresh of all pages that the user is logged into will be requested.
 * Posts to ../Api/auth/SetNickName. Not called by any page in this repository (kept for application code).
 * @param {string} newNickName The new nick name to use for the current user.
 * @returns {Promise<boolean>} True if the nick name was changed (refresh of page may occur before any value is returned and/or processed).
 */
async function setNickName(newNickName) {

    return await sendRequest("../Api/auth/SetNickName", newNickName);
}


/**
 * Set the preferred language of the currently logged in user.
 * If changed, a refresh of all pages that the user is logged into will be requested.
 * @param {string} newLanguage The new language to use for the current user. A two letter letter ISO 639-1 code with an optional hypen followed by an ISO 3166 Alpha 2 country code. Examples: "fr", "en-US", "en-GB", "es-ES", "es-MX"
 * Posts to ../Api/auth/SetLanguage. Not called by any page in this repository (kept for application code).
 * @returns {Promise<boolean>} True if the language was changed (refresh of page may occur before any value is returned and/or processed).
 */
async function setLanguage(newLanguage) {

    return await sendRequest("../Api/auth/SetLanguage", newLanguage);
}


/**
 * Check if the currently logged in user is managed by the user manager (GET ../Api/auth/IsManagedUser).
 * NOTE: declared twice in this file with identical bodies; the second declaration below is the one in effect.
 * Not called by any page in this repository (kept for application code).
 * @returns {Promise<boolean>} True if the currently logged in user is managed by the user manager.
 */
async function isManagedUser() {

    return await getRequest("../Api/auth/IsManagedUser");
}


/**
 * Check if the currently logged in user is managed by the user manager (GET ../Api/auth/IsManagedUser).
 * Duplicate of the declaration above (identical body); as the later function declaration it wins.
 * @returns {Promise<boolean>} True if the currently logged in user is managed by the user manager.
 */
async function isManagedUser() {

    return await getRequest("../Api/auth/IsManagedUser");
}


/**
 * Get misc properties about the user manager configuration (GET ../Api/auth/UserManagerProps).
 * Not called by any page in this repository (kept for application code).
 * @returns {Promise<object>} Misc properties about the user manager configuration.
 */
async function userManagerProps() {

    return await getRequest("../Api/auth/UserManagerProps");
}


////// Short  code /////////

/**
 * Test if a short code is valid against the code the server stored in the current HTTP session
 * (posts to ../Api/auth/ValidateShortCode; one slot per session, limited tries, expiry).
 * Cross-folder: called by AuthPageValidation in AuthUi.js (SysWeaver.MicroService.Auth); on 1 that function
 * navigates to "<Page>.html?<code>".
 * @param {string} shortCode The (cleaned up, digits only) short code typed by the user
 * @returns {Promise<number>} 1 if code is valid, 0 if code is invalid, -1 if no code exist.
 */
async function validateShortCode(shortCode) {

    return await sendRequest("../Api/auth/ValidateShortCode", shortCode);
}

/**
 * Clean up some short code input (synchronous): keeps only the characters '0'-'9'.
 * Cross-folder: called by AuthPageValidation in AuthUi.js (SysWeaver.MicroService.Auth), with
 * noLengthCheck = true to normalize the input while typing, and without it to decide whether the code is
 * complete enough to enable the confirm button / submit.
 * @param {string} shortCode User input of some short code
 * @param {boolean} [noLengthCheck] Optionally skip the length check
 * @returns {string} Cleaned up short code; null for empty input, or (unless noLengthCheck) when fewer than 3 digits remain
 */
function cleanUpShortCode(shortCode, noLengthCheck)
{
    if (!shortCode)
        return null;
    const l = shortCode.length;
    let o = "";
    for (let i = 0; i < l; ++i) {
        const c = shortCode.charAt(i);
        if (c < '0')
            continue;
        if (c > '9')
            continue;
        o += c;
    }
    if (noLengthCheck)
        return o;
    return o.length < 3 ? null : o;
}