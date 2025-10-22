
////// Set password ///////////////////////////////

/**
 * Set a new password, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPassword The new password (must meet the password policy)
 * @returns {boolean} true if the password was changed successfully
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
 * Get the password policy for creating a new passowrd
 * @returns {object} The password policy
 */
async function getCreatePasswordPolicy() {

    const apiPrefix = "../Api/auth/";
    return await getRequest(apiPrefix + "GetCreatePasswordPolicy");
}

////// Choose password ///////////////////////////////

/**
 * Add a new user
 * @param {string} token The token that was sent (using email, phone etc)
 * @param {string} salt The user salt that was sent (using email, phone etc)
 * @param {string} newPassword The new password (must meet the password policy)
 * @returns {{Error:number, Username:string, Tokens:array.<string>}} A user login response or null if failed
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
 * Send a message to the user (using email, phone etc)
 * @returns {array.<string>} An array with all targets that a message was sent to (emails, phone numbers)
 */
async function sendDeleteUserRequest() {

    return await sendRequest("../Api/auth/SendDeleteUserRequest", 0);
}

/**
 * Delete an user account
 * @param {string} token The delete user token that was sent (using email, phone etc)
 * @returns {string} The user name of the user that was deleted or null if no user was deleted
 */
async function deleteUserRequest(token) {

    return await sendRequest("../Api/auth/DeleteUser", token);
}


////// Sign up ///////////////////////////////

/**
 * Sign up (joining) the service
 * @param {string} target Any of the server side supported sign up methods, typically email address or phone number
 * @returns {boolean} true if a sign up request was sent to the target
 */
async function signUpRequest(target) {
    target = AuthTrim(target);
    return await sendRequest("../Api/auth/SignUp", target);
}


////// Forgot password ///////////////////////////////

/**
 * Send a reset password link to a user
 * @param {string} userId Any of the server side user identification methods, typically user name, email address or phone number
 * @returns {array.<string>} An array with all targets that a message was sent to (emails, phone numbers)
 */
async function forgotPasswordRequest(userId) {

    userId = AuthTrim(userId);
    return await sendRequest("../Api/auth/ForgotPassword", userId);
}


////// Reset password ///////////////////////////////

/**
 * Set a new password (from data sent to user)
 * @param {string} token The reset password token that was sent (using email, phone etc)
 * @param {string} newPassword The new password (must meet the password policy)
 * @param {string} newSalt The new user salt that was sent (using email, phone etc)
 * @returns {{Error:number, Username:string, Tokens:array.<string>}} A user login response or null if failed
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
 * Send an add password link to a user
 * @returns {array.<string>} An array with all targets that a message was sent to (emails, phone numbers)
 */
async function sendAddPasswordRequest() {

    return await sendRequest("../Api/auth/SendAddPasswordRequest", 0);
}


////// Delete password ///////////////////////////////

/**
 * Send a delete password link to a user
 * @returns {array.<string>} An array with all targets that a message was sent to (emails, phone numbers)
 */
async function sendDeletePasswordRequest() {

    return await sendRequest("../Api/auth/SendDeletePasswordRequest", 0);
}

/**
 * Deletes the users password
 * @param {string} token The delete password token that was sent (using email, phone etc)
 * @returns {boolean} True if the password was deleted
 */
async function deletePassword(token) {

    return await sendRequest("../Api/auth/DeletePassword", token);
}


////// Invite user ///////////////////////////////

/**
 * Invite a user to join
 * @param {string} target Any of the server side supported sign up methods, typically email address or phone number
 * @param {string} userName An optional user name to give this user
 * @param {array.<string>} tokens An optional set of auth tokens to give 
 * @param {string} domain An optional domain (application specific) to assign the user to
 * @returns {boolean} True if an invite was sent
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
 * Change the email address associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newEmail The new email 
* @returns {array.<string>} An array with all emails that a message was sent to (always just one if successfull)
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
 * Change (or set) a users email address 
 * @param {string} token The change email address token that was sent (using email)
 * @returns {boolean} True if the email address was changed
 */
async function changeEmail(token) {

    return await sendRequest("../Api/auth/AddEmail", token);
}

////// Delete email ///////////////////////////////

/**
 * Delete the email address associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
* @returns {array.<string>} An array with all emails that a message was sent to (always just one if successfull)
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
 * Add the email address associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newEmail The new email 
* @returns {array.<string>} An array with all emails that a message was sent to (always just one if successfull)
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
 * Add an email address 
 * @param {string} token The add email address token that was sent (using email)
 * @returns {boolean} True if the email address was added
 */
async function addEmail(token) {

    return await sendRequest("../Api/auth/AddEmail", token);
}




////// Change phone ///////////////////////////////

/**
 * Change the phone number associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPhone The new phone numbers
* @returns {array.<string>} An array with all phones that a message was sent to (always just one if successfull)
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
 * Change (or set) a users phone number 
 * @param {string} token The change phone number token that was sent (using phone)
 * @returns {boolean} True if the phone number was changed
 */
async function changePhone(token) {

    return await sendRequest("../Api/auth/AddPhone", token);
}

////// Delete phone ///////////////////////////////

/**
 * Delete the phone number associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
* @returns {array.<string>} An array with all phones that a message was sent to (always just one if successfull)
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
 * Add the phone number associated with a user, requires the existing password
 * @param {string} user The user identifier (name/email/phone etc)
 * @param {string} password The existing password
 * @param {string} newPhone The new phone 
* @returns {array.<string>} An array with all phones that a message was sent to (always just one if successfull)
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
 * Add an phone number 
 * @param {string} token The add phone number token that was sent (using phone)
 * @returns {boolean} True if the phone number was added
 */
async function addPhone(token) {

    return await sendRequest("../Api/auth/AddPhone", token);
}


/**
 * Set the nick name of the currently logged in user.
 * If changed, a refresh of all pages that the user is logged into will be requested.
 * @param {string} newNickName The new nick name to use for the current user.
 * @returns {boolean} True if the nick name was changed (refresh of page may occur before any value is returned and/or processed).
 */
async function setNickName(newNickName) {

    return await sendRequest("../Api/auth/SetNickName", newNickName);
}


/**
 * Set the preferred language of the currently logged in user.
 * If changed, a refresh of all pages that the user is logged into will be requested.
 * @param {string} newLanguage The new language to use for the current user. A two letter letter ISO 639-1 code with an optional hypen followed by an ISO 3166 Alpha 2 country code. Examples: "fr", "en-US", "en-GB", "es-ES", "es-MX"
 * @returns {boolean} True if the nick name was changed (refresh of page may occur before any value is returned and/or processed).
 */
async function setLanguage(newLanguage) {

    return await sendRequest("../Api/auth/SetLanguage", newLanguage);
}


/**
 * Check if the currently logged in user is managed by the user manager.
 * @returns {boolean} True if the currently logged in user is managed by the user manager.
 */
async function isManagedUser() {

    return await getRequest("../Api/auth/IsManagedUser");
}


/**
 * Check if the currently logged in user is managed by the user manager.
 * @returns {boolean} True if the currently logged in user is managed by the user manager.
 */
async function isManagedUser() {

    return await getRequest("../Api/auth/IsManagedUser");
}


/**
 * Get misc properties about the user manager configuration
 * @returns {object} Misc properties about the user manager configuration.
 */
async function userManagerProps() {

    return await getRequest("../Api/auth/UserManagerProps");
}


////// Short  code /////////

/**
 * Test if a short code is valid
 * @param {any} shortCode
 * @returns {number} 1 if code is valid, 0 if code is invalid, -1 if no code exist.
 */
async function validateShortCode(shortCode) {

    return await sendRequest("../Api/auth/ValidateShortCode", shortCode);
}

/**
 * Clean up some short code input
 * @param {string} shortCode User input of some short code
 * @param {boolean} noLengthCheck Optionally skip the length check
 * @returns {string} Cleaned up short code
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