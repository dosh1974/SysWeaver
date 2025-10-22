
////// Logout ///////////////////////////////

async function logoutRequest(apiPrefix) {
    if (!apiPrefix)
        apiPrefix = "../";
    return await getRequest(apiPrefix + "logout", true, false, req => req.headers["Authorization"] = "Basic logout");
}



////// Login ///////////////////////////////

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
 * @returns {string} null if the password is ok, else a string with some text explaining why it fails.
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
 * Check a password against haveibeenpwned.com
 * @param {HTMLElement} el An element where the result will be shown, innerText, title and classes will be changed
 * @param {string} p The password to check
 */
async function CheckPassword(el, p) {
    const db = "https://haveibeenpwned.com";
    const cl = el.classList;
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
