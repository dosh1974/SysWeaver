/**
 * @file Auth.js - request and validation helpers for authentication (SysWeaver.MicroService.Auth, served as auth/Auth.js).
 *
 * Purpose: hashed password login against LoginService (salt + one-time pad + double SHA-256), with a fallback to
 * HTTP Basic auth; logout; password policy text and validation; password leak check (pwnedpasswords k-anonymity);
 * e-mail and phone number validation through AuthManagerService.
 *
 * Loading: a `defer` <script> in auth/Login.html and auth/Logout.html (this folder), and in every page of the
 * other auth/ contributions: SysWeaver.MicroService.PassKey (AddPassKey.html, UsePassKey.html, MyPassKeys.html, ...),
 * SysWeaver.MicroService.UserManager (SignUp.html, AddEmail.html, ... via UserManagerUi.js) and SysWeaver.Auth.Core
 * (index.html, KeyMan.html via SimpleAuth.js). Always loaded before AuthUi.js.
 *
 * Globals defined (functions only, no top-level state, no immediately executed code):
 * logoutRequest, loginRequest, AuthTrim, AuthPolicyText, ValidatePassword, CheckPassword, AuthGetEmailError, AuthGetPhoneError.
 *
 * Dependencies (common.js): sendRequest, getRequest, hashString, hashData, hashStringSha1, bufferToBase64, bufferToHex,
 * base64ToArray, concatArrays, Base64EncodeString, waitEvent2, _T, _TF.
 *
 * Cross-folder: UserManager.js (SysWeaver.MicroService.UserManager) and SimpleAuth.js (SysWeaver.Auth.Core) re-implement
 * the login hashing instead of calling loginRequest; they do call AuthTrim / AuthPolicyText / ValidatePassword /
 * CheckPassword; UserManagerUi.js calls AuthGetEmailError / AuthGetPhoneError.
 *
 * Storage: sessionStorage["SysWeaver.User"] (JSON of the last AuthInfo login result; written here, by Passkey.js and
 * UserManager.js; no reader exists - informational write-only cache).
 */

////// Logout ///////////////////////////////

/**
 * Signs out the current session by requesting the server's forced logout endpoint.
 * Sends `GET <apiPrefix>logout` (with credentials) with the header `Authorization: Basic logout`, which overwrites any
 * Basic credentials the browser has cached. Called by logoutMain (AuthUi.js).
 * @param {string} [apiPrefix="../"] - URL prefix to the server root (falsy values use the default).
 * @returns {Promise<boolean>} The JSON result of the server (true on success).
 */
async function logoutRequest(apiPrefix) {
    if (!apiPrefix)
        apiPrefix = "../";
    return await getRequest(apiPrefix + "logout", true, false, req => req.headers["Authorization"] = "Basic logout");
}



////// Login ///////////////////////////////

/**
 * Signs in with user name and password using the LoginService hashed protocol, falling back to HTTP Basic auth.
 *
 * Normal path:
 * 1. POST `GetUserSalt(user)` returns `[salt, oneTimePad]`.
 * 2. If the salt starts with `|` the rest is base64 binary: hash = base64(SHA-256(saltBytes ++ utf8(password)));
 *    otherwise hash = hashString(password + "|" + salt) (SHA-256, base64).
 * 3. The hash is bound to the pad: hashString(hash + "|" + oneTimePad).
 * 4. POST `Login {OneTimePad, Hash}`; a truthy result is stored as JSON in sessionStorage["SysWeaver.User"],
 *    otherwise that key is removed.
 *
 * Fallback when GetUserSalt throws (no LoginService): `GET <apiPrefix>../../basic_auth` must be truthy (else throws
 * "Nor login or basic auth is enabled!"), then a raw XMLHttpRequest `GET <apiPrefix>../../login` is sent with
 * withCredentials and `Authorization: Basic base64(user:password)`. Completion is awaited with waitEvent2 on a detached
 * `SysWeaver-Coms` element that receives `LoginOk` / `LoginErr` custom events from the XHR onload / onerror handlers;
 * onreadystatechange records the response text when the status is 200. No sessionStorage write in this path.
 *
 * Called by loginMain (AuthUi.js).
 * @param {string} user - User name / id (trimmed with AuthTrim).
 * @param {string} password - Password (trimmed with AuthTrim).
 * @param {string} [apiPrefix="../Api/auth/"] - Prefix of the auth API (falsy values use the default).
 * @returns {Promise<object>} The server's AuthInfo ({Succeeded, Username, ...}) or a falsy value; in the Basic
 *   fallback `{Succeeded:true, Username:user}`.
 * @throws {Error} In the fallback path when Basic auth is disabled, the request fails or the status is not 200.
 */
async function loginRequest(user, password, apiPrefix) {

    if (!apiPrefix)
        apiPrefix = "../Api/auth/";
    user = AuthTrim(user);
    password = AuthTrim(password);
    let saltPad;
    try {
        saltPad = await sendRequest(apiPrefix + "GetUserSalt", user);
    }
    catch (e) {
        if (!await getRequest(apiPrefix + "../../basic_auth", true))
            throw Error(_TF("Nor login or basic auth is enabled!", "An error message explaining that there is no way to log in to the service"));

        
        const com = document.createElement("SysWeaver-Coms");
        const r = new XMLHttpRequest();
        r.open("GET", apiPrefix + "../../login", true);
        r.withCredentials = true;
        r.setRequestHeader("Authorization", "Basic " + Base64EncodeString(user + ":" + password));
        r.onload = e => {
            com.dispatchEvent(new CustomEvent("LoginOk", { detail: e }));
        };
        r.onerror = () => {
            com.dispatchEvent(new Event("LoginErr"));
        };
        let ok = false;
        r.onreadystatechange = () => {
            if ((r.readyState === 4) && (r.status === 200))
                ok = r.responseText;
        };
        const er = await waitEvent2(com, "LoginOk", "LoginErr", () => r.send());
        if (er.type !== "LoginOk")
            throw new Error(_TF("Failed to send login request!", "An error message explaining that the log in request failed"));
        if (!ok)
            throw new Error(_TF("Failed to login!", "An error message explaining that the log in failed (wrong password or user)"));
        return { Succeeded: true, Username: user };
    }
    let salt = saltPad[0];
    const oneTimePad = saltPad[1];
    const useBinary = salt.charAt(0) === '|';
    if (useBinary)
        salt = salt.substring(1);
    let hashStr;
    if (useBinary) {
        const binSalt = base64ToArray(salt);
        const binPw = new TextEncoder().encode(password);
        const binBlob = concatArrays([binSalt, binPw]);
        const hashBuffer = await hashData(binBlob);
        hashStr = await bufferToBase64(hashBuffer);
    } else {
        hashStr = await hashString(password + "|" + salt)
    }
    hashStr = await hashString(hashStr + "|" + oneTimePad)
    const result = await sendRequest(apiPrefix + "Login", {
        OneTimePad: oneTimePad,
        Hash: hashStr,
    });
    if (result)
        sessionStorage.setItem("SysWeaver.User", JSON.stringify(result));
    else
        sessionStorage.removeItem("SysWeaver.User");
    return result; 
}



/// Tools

/**
 * Given any value, return null if it's null, else the trimmed string version of it
 * @param {any} value Any object, typically a string
 * @returns {string} null or the trimmed string version of the input
 */
function AuthTrim(value) {
    if (value === null)
        return value;
    return ("" + value).trim();
}

/**
 * Given a password policy, generate text string
 * @param {object} policy A password policy (as returned from some API)
 * @returns {string} A multiline string with the policy in text form.
 */
function AuthPolicyText(policy) {
    let t = _TF("Password policy", "The title of a section that describes requirements for a user defined password") + ":\n- " +
        _TF("Trailing or leading white-spaces are removed.", "Text explaining that any password will be trimmed before use");
    let minLen = 8;
    let maxLen = 64;
    if (policy) {
        minLen = policy.MinLength;
        if (minLen < 1)
            minLen = 1;
        maxLen = policy.MaxLength;
        if (maxLen < minLen)
            maxLen = minLen;
        if (maxLen > 128)
            maxLen = 128;
    }
    t += ("\n- " + _T("Must be at least {0} characters long.", minLen, "Text explaining what the minimum allowed password length is.{0} is replaced with the minimun number of chars required"));
    t += ("\n- " + _T("Must be at most {0} characters long.", maxLen, "Text explaining what the maximum allowed password length is.{0} is replaced with the maximun number of chars allowed"));
    if (!policy)
        return t;
    const needLetter = policy.MixedSpecial | policy.MixedNumerical;
    if (policy.MixedCase) {
        t += ("\n- " + _TF("Must have at least one lowercase letter.", "Text explaining that a password must contain at least one lowercase letter"));
        t += ("\n- " + _TF("Must have at least one uppercase letter.", "Text explaining that a password must contain at least one uppercase letter"));
    } else {
        if (needLetter)
            t += ("\n- " + _TF("Must have at least one letter.", "Text explaining that a password must contain at least one letter"));
    }
    if (policy.MixedNumerical)
        t += ("\n- " + _TF("Must have at least one digit.", "Text explaining that a password must contain at least one digit"));
    if (policy.MixedSpecial)
        t += ("\n- " + _TF("Must have at least one special character (non-letter and non-numeric).", "Text explaining that a password must contain at least one special character"));
    return t;
}

/**
 * Validate a password against the current policy for a given user
 * @param {string} u The user name (policy depends on user name since different user may come from different authorizers with different policies)
 * @param {string} p The password to validate
 * @param {object} cache Any object, that is used to store a cached version of the policy (instead of requesting it on every validation)
 * @param {function(object)} onNewPolicy optional callback that is executed every time a new policy is loaded from the server
 * @param {string} apiPrefix Optional api prefix, default is "../Api/auth/"
 * @param {object} policy An optional policy, if this is set there will be no request for per user policy (and no need for a cache)
 * @returns {Promise<string>} null if the password is ok, else a string with some text explaining why it fails.
 *
 * Details: without `policy`, when `cache.PasswordPolicyUser !== u` it POSTs `GetPasswordPolicy(user)` (errors give a
 * null policy) and stores it in the cache expandos PasswordPolicy, PasswordPolicyUser, PasswordPolicyStr (JSON).
 * onNewPolicy is awaited when the fetched policy's JSON differs from PasswordPolicyStr, or when the fetch yields null
 * and the cached policy was also null. Note: the user comparison uses the untrimmed `u` but stores the trimmed user.
 * Checks in order: blank, length (default 8..64, clamped min >= 1, max >= min, max <= 128), letter needed (when
 * MixedSpecial or MixedNumerical), MixedCase, MixedNumerical (digit via parseInt), MixedSpecial; first failure wins.
 * Without a policy (null) only the blank and default length checks apply; the flag checks are skipped.
 * Used by loginMain, SimpleAuth.js (Auth.Core) and UserManagerUi.js (UserManager).
 */
async function ValidatePassword(u, p, cache, onNewPolicy, apiPrefix, policy) {
    const user = AuthTrim(u);
    if (!policy) {
        policy = cache.PasswordPolicy;
        if (cache.PasswordPolicyUser !== u) {
            if (!apiPrefix)
                apiPrefix = "../Api/auth/";
            try {
                policy = await sendRequest(apiPrefix + "GetPasswordPolicy", user);
            }
            catch
            {
                policy = null;
            }
            if (policy) {
                const policyStr = JSON.stringify(policy);
                if (cache.PasswordPolicyStr !== policyStr) {
                    await onNewPolicy(policy);
                    cache.PasswordPolicyStr = policyStr;
                }
            } else {
                if (cache.PasswordPolicy === policy)
                    await onNewPolicy(policy);
            }
            cache.PasswordPolicy = policy;
            cache.PasswordPolicyUser = user;
        }
    }
    if (!p)
        return _TF("Password may not be blank!", "Text explaining that a password may now be null, empty or blank");
    let minLen = 8;
    let maxLen = 64;
    if (policy) {
        minLen = policy.MinLength;
        if (minLen < 1)
            minLen = 1;
        maxLen = policy.MaxLength;
        if (maxLen < minLen)
            maxLen = minLen;
        if (maxLen > 128)
            maxLen = 128;
    }
    const pl = p.length;
    if (pl < minLen)
        return _T("Password must be at least {0} characters!", minLen, "Text explaining that a password must contain at least this many characters.{0} is replaced by the minimum number of required characters");
    if (pl > maxLen)
        return _T("Password may not be longer than {0} characters!", maxLen, "Text explaining that a password may not contain more than this many characters.{0} is replaced by the maximum number of allowed characters");
    let letter = false;
    let lowerCase = false;
    let upperCase = false;
    let numeric = false;
    let special = false;
    for (let i = 0; i < pl; ++i) {
        const c = p.charAt(i);
        const l = c.toLowerCase() === c;
        const u = c.toUpperCase() === c;
        if (l !== u) {
            letter = true;
            lowerCase |= l;
            upperCase |= u;
            continue;
        }
        if (!isNaN(parseInt(c, 10))) {
            numeric = true;
            continue;
        }
        special = true;
    }
    if (!policy)
        return null;
    const needLetter = policy.MixedSpecial | policy.MixedNumerical;
    if (needLetter && (!letter))
        return _TF("Password need to have at least one letter!", "Text explaining that a valid password need to have at least one letter");
    if (policy.MixedCase)
        if (!(lowerCase & upperCase))
            return lowerCase
                ?
                _TF("Password need to have at least one uppercase letter!", "Text explaining that a valid password need to have at least one uppercase letter")
                :
                _TF("Password need to have at least one lowercase letter!", "Text explaining that a valid password need to have at least one lowercase letter")
                ;
    if (policy.MixedNumerical)
        if (!numeric)
            return _TF("Password need to have at least one digit!", "Text explaining that a valid password need to have at least one digit")
    if (policy.MixedSpecial)
        if (!special)
            return _TF("Password need to have at least one special character (non-letter and non-number)!", "Text explaining that a valid password need to have at least one special character")
    return null;
}


/**
 * Check a password against haveibeenpwned.com (k-anonymity range API).
 * If `p === el.LastCheck` the memoized state (el.PwdState / el.PwdError) is re-applied without a request.
 * Otherwise clears el.className, shows "Checking password ..", computes the upper-case hex SHA-1 (hashStringSha1 ->
 * base64ToArray -> bufferToHex), fetches `https://api.pwnedpasswords.com/range/<first 5 hex>` (CORS) and searches the
 * response for the remaining 35 hex chars, parsing the digit run after the ':' as the leak count.
 * Side effects on el: innerText, title, class Good / Error / Warning, expandos LastCheck, PwdState, PwdError.
 * Called (not awaited) by loginMain; also used by SimpleAuth.js (Auth.Core) and UserManagerUi.js (UserManager).
 * @param {HTMLElement} el An element where the result will be shown, innerText, title and classes will be changed
 * @param {string} p The password to check
 * @returns {Promise<void>}
 */
async function CheckPassword(el, p) {
    /** Site name shown in texts/tooltips. @type {string} */
    const db = "https://haveibeenpwned.com";
    /** Class list of the result element. @type {DOMTokenList} */
    const cl = el.classList;
    /**
     * Applies a check result to `el` and memoizes it in el.PwdState / el.PwdError.
     * @param {number} state - 1 = safe (class Good), 2 = leaked `e` times (class Error), 3 = non-200 response
     *   (class Warning), 4 = exception `e` (class Warning, error appended to the tooltip); 0/other = no text change.
     * @param {number|Error} [e] - Leak count (state 2) or the caught exception (state 4).
     * @returns {void}
     */
    function setState(state, e) {
        switch (state) {
            case 1:
                el.innerText = _TF("Password is safe (not leaked)!", "Text indicating that an entered password is considered good");
                el.title =
                    _T("Password was checked against {0} and wasn't found in any data leaks.", db, "Tool tip description explaining that the entered password is not found in any data leaks.{0} is replaced by the url to the site used for password checking") + "\n\n" +
                    _TF("This means that the password is probably safe to use for now.", "Tool tip description explaining that the enetered password is safe for use")
                    ;
                cl.add("Good");
                break;
            case 2:
                el.innerText =
                    e === 1
                        ?
                        _TF("This password is part of a data leak", "Text explaining that the entered password is part of one data leak")
                        :
                        _T("This password is part of {0} data leaks", e, "Text explaining that the entered password is part of multiple data leaks.{0} is replaced by the number of leaks")
                    ;

                el.title =
                    e === 1
                        ?
                        _T("Password was checked against {0} that found the password in a data leak.", db, "Tool tip description explaining that the entered password is part of one data leak.{0} is replaced by the url to the site used for password checking")
                        :
                    _T("Password was checked against {0} that found the password in {1} data leaks.", db, e, "Tool tip description explaining that the entered password is part of multiple data leaks.{0} is replaced by the url to the site used for password checking.{1} is replaced by the number of leaks")
                    ; 
                cl.add("Error");
                break;
            case 3:
                el.innerText = _TF("Password leak check failed!", "Text explaining that the entered password couldn't be checked for any leaks");
                el.title =
                    _T("We tried to check the password against {0}, but failed.", db, "Tool tip description explaining that the entered password couldn't be checked for any leaks.{0} is replaced by the url to the site used for password checking") + "\n" +
                    _TF("We couldn't assert that the password isn't in any data leak.", "Tool tip description explaining that the entered password couldn't be checked for any leaks.") + "\n\n" +
                    _TF("This means that attackers may have this password in their dictionaries.", "Tool tip description explaining that the entered password could be known to attackers.");
                cl.add("Warning");
                break;
            case 4:
                el.innerText = _TF("Password leak check failed!", "Text explaining that the entered password couldn't be checked for any leaks");
                el.title =
                    _T("We tried to check the password against {0}, but failed.", db, "Tool tip description explaining that the entered password couldn't be checked for any leaks.{0} is replaced by the url to the site used for password checking") + "\n" +
                    _TF("We couldn't assert that the password isn't in any data leak.", "Tool tip description explaining that the entered password couldn't be checked for any leaks.") + "\n\n" +
                    _TF("This means that attackers may have this password in their dictionaries.", "Tool tip description explaining that the entered password could be known to attackers.") + "\n\n" +
                    _TF("Error:", "Tool tip description header, the next line will show the java script exception message") + "\n" + e;
                cl.add("Warning");
                break;
        }
        el.PwdState = state;
        el.PwdError = e;
    }
    if (p === el.LastCheck) {
        setState(el.PwdState, el.PwdError);
        return;
    }
    el.className = "";
    el.PwdState = 0;
    el.PwdError = null;
    el.LastCheck = p;
    el.innerText = _TF("Checking password ..", "Text displayed when checking if a password entered by a user have been leaked");
    el.title = _T("Checking passwords against {0} for leaks.", db, "Tool tip description explaining that a password entered by a user is checked if it's part of any data leaks.{0} is replaced by the url to the site used for password checking");
    try {
        const sha1 = bufferToHex(base64ToArray(await hashStringSha1(p))).toUpperCase();
        const ff = sha1.substring(0, 5);
        const url = "https://api.pwnedpasswords.com/range/" + ff;
        const r = new Request(url, {
            method: "GET",
            mode: "cors",
        });
        const res = await fetch(r);
        if (res.status == 200) {
            const text = await res.text();
            const shaEnd = sha1.substring(5);
            let found = text.indexOf(shaEnd);
            if (found >= 0) {
                let count = 1;
                try {
                    found += sha1.length - 4;
                    const tlen = text.length;
                    const start = found;
                    for (; found < tlen; ++found) {
                        const c = text.charAt(found);
                        if (c < '0')
                            break;
                        if (c > '9')
                            break;
                    }
                    const ct = text.substring(start, found);
                    count = parseInt(ct);
                }
                catch
                {
                }
                setState(2, count);
                return;
            }
            setState(1);
            return;
        }
        setState(3);
        return;
    }
    catch (e) {
        setState(4, e);
    }
}

/**
 * Check if a string is a valid email address
 * @param {string} emailAddress The text to check if it's a valid email address (server decides if ip's and/or NetBIOS names can be used etc).
 * @returns {string} null if the text is a valid email address, else an error text (describing what's wrong with it).
 */
async function AuthGetEmailError(emailAddress) {
    try {
        await sendRequest("../Api/auth/ValidateEmailAddress", emailAddress);
        return null;
    }
    catch (e) {
        const m = e.message;
        if (m.indexOf("500\n") === 0)
            return m.substring(4);
        return m;
    }
}

/**
 * Check if a string is a valid phone number
 * @param {string} phoneNumber The text to check if it's a valid phone number (including country dialing code prefix).
 * @returns {string} null if the text is a valid phone number, else an error text (describing what's wrong with it).
 */
async function AuthGetPhoneError(phoneNumber) {
    try {
        await sendRequest("../Api/auth/GetPhoneNumberInfo", phoneNumber);
        return null;
    }
    catch (e) {
        const m = e.message;
        if (m.indexOf("500\n") === 0)
            return m.substring(4);
        return m;
    }
}
