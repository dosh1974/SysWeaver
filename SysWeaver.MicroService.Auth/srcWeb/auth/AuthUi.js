/**
 * @file AuthUi.js - page entry points loginMain / logoutMain and the shared `Auth*` UI toolkit for all auth/ pages
 * (SysWeaver.MicroService.Auth, served as auth/AuthUi.js).
 *
 * Loading: a `defer` <script> after Auth.js (and after Passkey.js on Login.html). auth/Login.html calls loginMain()
 * and auth/Logout.html calls logoutMain() from <body onload>. Every page of the other auth/ contributions
 * (SysWeaver.MicroService.PassKey Passkey.js pages, SysWeaver.MicroService.UserManager UserManagerUi.js pages,
 * SysWeaver.Auth.Core SimpleAuth.js pages) also loads this file and builds its UI with AuthPage and the Auth* factories.
 *
 * Globals defined (functions only, no top-level state, no immediately executed code):
 * logoutMain, loginMain, GenPassword, CreateAuthWrapper, AuthPage, AuthPageValidation, AuthUserHtml, AuthText,
 * AuthLabel, AuthTab, AuthError, AuthNewPassword, AuthSetText, AuthSetError, AuthAddIcon, AuthPassword, AuthInput,
 * AuthInputPhone, AuthButtonRow, AuthButton, AuthHr, AuthBr, AuthStartPage, IsAutoFilled.
 *
 * Conventions: factories append the new element to `target` and return it; widgets keep their state and API in DOM
 * expandos (OnChange, Icon, Icon<n>, Selected, Add, SetIndex, OnLoaded, LastCheck) that consumers rely on.
 * Load order (defer) is the only contract; there is no module system or namespace.
 *
 * Dependencies:
 * - common.js: ColorIcon, Button, ValueFormat, Fail, SetTemporaryInnerText, tabToNextOnEnter, keyboardClick, badClick,
 *   createImg, Open, PageLoaded, SysWeaverIgnoreUserChanges, delay, CryptRandomInt32, makeHtmlSafe, sendRequest, _T, _TF.
 * - Auth.js (this folder): logoutRequest, loginRequest, AuthTrim, AuthPolicyText, ValidatePassword, CheckPassword.
 * - Soft (cross-folder): the `PassKey` class from Passkey.js (SysWeaver.MicroService.PassKey), guarded by
 *   `typeof PassKey !== "undefined"` in loginMain; its icon classes IconUsePasskey / IconPasskeyKey / PasskeyIconButton
 *   are styled by Passkey.css.
 * - Hidden, unguarded (cross-folder): cleanUpShortCode and validateShortCode from UserManager.js
 *   (SysWeaver.MicroService.UserManager), called by AuthPageValidation (only used by UserManagerUi.js pages).
 * - Auth.css (this folder) styles all SysWeaver-Auth* elements created here.
 *
 * Server APIs used directly: Api/auth/GetUser (logoutMain), Api/auth/GetPhonePrefixInfo (AuthInputPhone);
 * static ../iso_data/country/<ISO>.svg flags.
 */

////// Logout ///////////////////////////////

/**
 * Entry point of auth/Logout.html (called from <body onload>).
 * Builds an AuthPage with the IconAuthLogout icon, a status AuthText, a button row with a hidden *Retry* button
 * (IconRetry, runs tryOnce), calls target.OnLoaded() early and then awaits tryOnce() to sign out immediately.
 * @returns {Promise<void>}
 */
async function logoutMain() {
    await AuthPage("IconAuthLogout", async target => {
        /** Status text element (classes Info / Fail toggled). @type {HTMLElement} */
        const text = AuthText(target, "");
        /**
         * One sign-out attempt: shows "Signing out .." (class Info) and hides *Retry*; POSTs ../Api/auth/GetUser and,
         * when `Succeeded` is false, shows and Fail()s "No user is signed in!" (GetUser exceptions are ignored).
         * Otherwise awaits logoutRequest(): success shows "Signed out successfully!" and returns; failure / exception
         * Fail()s "Failed to sign out!". On any failure the text gets class Fail and *Retry* is shown again.
         * @returns {Promise<void>}
         */
        async function tryOnce() {
            text.classList.remove("Fail");
            text.classList.add("Info");
            AuthSetText(text, _TF("Signing out ..", "Text indicating that the user is being signed out"));
            button.Element.classList.add("Hide");
            try {
                let failed = false;
                try {
                    const r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded) {
                        const m = _TF("No user is signed in!", "Error text indicating that a sign out can't be performed since no user is signed in");
                        AuthSetText(text, m);
                        Fail(m);
                        failed = true;
                    }
                }
                catch
                {
                }
                if (!failed) {
                    if (!await logoutRequest()) {
                        const m = _TF("Failed to sign out!", "Error text indicating that the sign out request failed");
                        AuthSetText(text, m);
                        Fail(m);
                    } else {
                        AuthSetText(text, _TF("Signed out successfully!", "Text explaining that the user was signed out correctly"));
                        return;
                    }
                }
            }
            catch (e) {
                const m = _TF("Failed to sign out!", "Error text indicating that the sign out request failed") + "\n\n" + e;
                AuthSetText(text, text.innerText);
                Fail(m);
            }
            text.classList.remove("Info");
            text.classList.add("Fail");
            button.Element.classList.remove("Hide");
        }
        const buttons = AuthButtonRow(target);
        const button = AuthButton(buttons,
            _TF("Retry", "Text on a button that when pressed will retry an operation that failed"),
            _TF("Click to retry sign out", "Tool tip description on a button that when pressed will retry to sign out the logged in user"),
            "IconRetry", tryOnce);
        button.Element.classList.add("Hide");
        target.OnLoaded();
        await tryOnce();
    });
}

////// Login ///////////////////////////////

/**
 * Entry point of auth/Login.html (called from <body onload>).
 * Builds an AuthPage (icon IconAuthLogin) with: *User ID* label + AuthInput (autocomplete "username", or
 * "username webauthn" with passkeys), *Password* label + AuthPassword ("current-password", Enter clicks *Sign in* when
 * the input is valid), an info AuthError line and a button row with the *Sign in* AuthButton (IconAuthPassword,
 * initially disabled).
 *
 * Sign in: if a field is empty it re-validates (autofill may expose values only after the click) and focuses the
 * empty input; otherwise inputs become read-only, loginRequest (Auth.js) is awaited; on Succeeded shows
 * "User signed in." and awaits AuthStartPage(); on failure Fail()s, re-validates and shows a 5 s temporary
 * "Failed to login, try again!?".
 *
 * Passkeys (only if the PassKey class from Passkey.js, SysWeaver.MicroService.PassKey, is loaded and
 * PassKey.IsSupported()): starts PassKey.Autofill (conditional mediation) when PassKey.IsAutofillSupported() resolves
 * true, pre-fetches an any-passkey challenge (PassKey.SignIn().Fetch(); hides the passkey button on
 * PassKey.Errors.NotSupported), and adds either a *Use passkey* button (IconUsePasskey, when PassKey.HaveHint()) or
 * a small 32x32 ColorIcon IconPasskeyKey with class PasskeyIconButton.
 *
 * Finally validates once, focuses the user input and re-validates at 100..5000 ms when the autofill/length state
 * changed (late browser autofill without events).
 * @returns {Promise<void>}
 */
async function loginMain() {
    await AuthPage("IconAuthLogin", async target => {

        const havePasskey = (typeof PassKey !== "undefined") && PassKey.IsSupported();

        AuthLabel(target, _TF("User ID", "Label for an input box where a user should enter their user id (user name, email or phone number)"));
        const uname = AuthInput(target, _TF("Enter your user id", "Placeholder description on an input box where the user should enter their user id"), null, "username", async input => await setStatus(input.value, pwd.value));

        const pwdLabel = AuthLabel(target, _TF("Password", "Label for an input box where a user should enter their password"));
        const pwd = AuthPassword(target, _TF("Enter your password", "Placeholder description on an input box where the user should eneter their password"), null, "current-password", async input => await setStatus(uname.value, pwd.value),
            async el => {
                if (await setStatus(uname.value, pwd.value))
                    el.click();
            }
        );
        const info = AuthError(target, "", "error");
        const br = AuthButtonRow(target);

        //  setStatus is async (the password policy may be fetched) and is called by several events (input, change, autofill), only the latest call may update the status
        /** Sequence number of the latest setStatus call. @type {number} */
        let statusSeq = 0;
        /** Promise of the latest updateStatus call. @type {Promise<boolean>} */
        let statusPromise = null;
        /**
         * Starts a (sequenced) validation of the inputs; only the newest call updates the UI.
         * @param {string} uText - Current user id text.
         * @param {string} pwText - Current password text.
         * @returns {Promise<boolean>} True when sign in is possible (resolves to the newest validation's result).
         */
        function setStatus(uText, pwText) {
            const seq = ++statusSeq;
            const p = updateStatus(uText, pwText, seq);
            statusPromise = p;
            return p;
        }

        /**
         * Validates the inputs and updates the UI. Awaits ValidatePassword with the form element as policy cache;
         * on a new policy the AuthPolicyText becomes the tooltip of the password label and info line.
         * If superseded (seq !== statusSeq) returns the newest result instead. An empty but autofilled password
         * (IsAutoFilled) counts as valid. An empty user id marks the user input Error and disables *Sign in*.
         * Otherwise marks the password input by the result, shows the error or starts CheckPassword (not awaited)
         * and enables *Sign in* when valid.
         * @param {string} uText - User id text (trimmed here).
         * @param {string} pwText - Password text (trimmed here).
         * @param {number} seq - Sequence number assigned by setStatus.
         * @returns {Promise<boolean>} True when sign in is possible.
         */
        async function updateStatus(uText, pwText, seq) {
            uText = AuthTrim(uText);
            pwText = AuthTrim(pwText);
            const p = await ValidatePassword(uText, pwText, target, newPolicy => {
                const policyText = AuthPolicyText(newPolicy);
                pwdLabel.title = policyText;
                info.title = policyText;
            });
            if (seq !== statusSeq)
                return await statusPromise;
            //  Some browsers (Chrome) doesn't expose autofilled values until the user interacts with the page (the values are available when the sign in button is clicked)
            if ((!pwText) && IsAutoFilled(pwd) && (uText || IsAutoFilled(uname))) {
                AuthSetError(uname, false);
                AuthSetError(pwd, false);
                AuthSetText(info, "");
                setButton.SetEnabled(true);
                return true;
            }
            if ((!uText) || (uText.length < 1)) {
                AuthSetError(uname, true);
                AuthSetError(pwd, false);
                AuthSetText(info, _TF("Enter your user id", "Information text explaining that the user should enter their user id (user name, email or phone number)"));
                setButton.SetEnabled(false);
                return false;
            }
            AuthSetError(uname, false);
            AuthSetError(pwd, p);
            if (p)
                AuthSetText(info, p);
            else
                CheckPassword(info, pwText); // Don't await, run async
            setButton.SetEnabled(!p);
            return !p;
        }


        /** The passkey Button or ColorIcon (null without passkey support). @type {Button|ColorIcon} */
        let passKeyButton = null;
        /** The *Sign in* button (common.js Button). @type {Button} */
        const setButton = AuthButton(br,
            _TF("Sign in", "Text of a button that when pressed will attempt to sign in a user using their entered credentials"),
            _TF("Click to sign in using the entered credentials", "Tool tip description on a button that when pressed will attempt to sign in a user using their entered credentials"),
            "IconAuthPassword", async button => {
            if ((!AuthTrim(uname.value)) || (!AuthTrim(pwd.value))) {
                //  An autofilled value that the browser still doesn't expose (should be exposed after the click)
                await setStatus(uname.value, pwd.value);
                if ((!AuthTrim(uname.value)) || (!AuthTrim(pwd.value))) {
                    (AuthTrim(uname.value) ? pwd : uname).focus();
                    return;
                }
            }
            button.StartWorking();
            pwd.readOnly = true;
            uname.readOnly = true;
            if (passKeyButton)
                passKeyButton.SetEnabled(false);
            try {
                const usr = uname.value;
                AuthSetText(info, _T('Signing in "{0}" ..', usr, "Text displayed when a user is trying to sign in.{0} is replaced with the user id (user name, email or phone number)"));
                const pw = pwd.value;
                const res = await loginRequest(usr, pw);
                if (res && res.Succeeded) {
                    AuthSetText(info, _TF("User signed in.", "Text displayed when a user have successfully signed in"));
                    setButton.SetEnabled(false);
                    await AuthStartPage();
                    return;
                }
                Fail(_TF("Failed to login.", "Error message displayed when a sign in failed"));
            }
            catch (e) {
                Fail(_TF("Failed to login.", "Error message displayed when a sign in failed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            if (passKeyButton)
                passKeyButton.SetEnabled(true);
            await setStatus(uname.value, pwd.value);
            SetTemporaryInnerText(info, _TF("Failed to login, try again!?", "Message displayed when a sign in failed"), 5000);
            uname.readOnly = false;
            pwd.readOnly = false;
            pwd.focus();
        }, true);
        if (havePasskey) {
            //  Passkey autofill: passkeys of this site are listed in the user id autofill drop down (if there are any)
            /** Running PassKey.Autofill request (has Start / Stop), null until supported. @type {object} */
            let autofill = null;
            uname.autocomplete = "username webauthn";
            PassKey.IsAutofillSupported().then(ok => {
                if (!ok)
                    return;
                autofill = PassKey.Autofill(async res => {
                    if (res.Error == 0) {
                        AuthSetText(info, _TF("User signed in.", "Text displayed when a user have successfully signed in"));
                        setButton.SetEnabled(false);
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    Fail(PassKey.ErrorText(res) + (res.Message ? "\n\n" + res.Message : ""));
                });
            });

            //  Get a challenge for any passkey before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
            /** Sign-in request for any passkey (no user), challenge pre-fetched. @type {object} */
            const anyPassKey = PassKey.SignIn();
            anyPassKey.Fetch().then(c => {
                if (c.error == PassKey.Errors.NotSupported)
                    passKeyButton.Element.classList.add("Hide");
            }).catch(() => { });

            /**
             * Passkey sign-in click handler (button or icon). Stops the autofill (only one WebAuthn request may be
             * active), disables the inputs and *Sign in*, shows "Select your passkey ..", then runs
             * PassKey.SignIn(user).Run() when a user id is entered, else the pre-fetched anyPassKey.Run().
             * res.Error == 0: "User signed in." and AuthStartPage(). Other errors except PassKey.Errors.Cancelled
             * are Fail()ed. Afterwards re-fetches the any-passkey challenge, restarts the autofill, re-validates and
             * (unless cancelled) shows a 5 s retry text.
             * @param {Button|ColorIcon} button - The clicked control (StartWorking / StopWorking are called on it).
             * @returns {Promise<void>}
             */
            async function usePassKey(button) {
                //  Only one passkey request can be active
                autofill?.Stop();
                button.StartWorking();
                uname.readOnly = true;
                pwd.readOnly = true;
                setButton.SetEnabled(false);
                let res = null;
                try {
                    AuthSetText(info, _TF("Select your passkey ..", "Text explaining to the user that he should select their passkey to continue sign in"));
                    const usr = AuthTrim(uname.value);
                    res = await (usr ? PassKey.SignIn(usr) : anyPassKey).Run();
                    if (res.Error == 0) {
                        AuthSetText(info, _TF("User signed in.", "Text displayed when a user have successfully signed in"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    if (res.Error != PassKey.Errors.Cancelled)
                        Fail(PassKey.ErrorText(res) + (res.Message ? "\n\n" + res.Message : ""));
                }
                catch (e) {
                    Fail(_TF("Failed to login.", "Error message displayed when a sign in failed") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                anyPassKey.Fetch().catch(() => { });
                autofill?.Start();
                await setStatus(uname.value, pwd.value);
                if (res?.Error != PassKey.Errors.Cancelled)
                    SetTemporaryInnerText(info, _TF("Failed to login, try again!?", "Message displayed when a sign in failed"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
            }

            if (PassKey.HaveHint()) {
                //  A passkey was used or created on this device before, show the passkey button
                passKeyButton = AuthButton(br,
                    _TF("Use passkey", "Text on a button that when pressed will attempt to sign in using a passkey"),
                    _TF("Click to sign in using a passkey (if a user id is entered, only passkeys of that user are shown)", "Tool tip description of a button that when pressed will attempt to sign in using a passkey"),
                    "IconUsePasskey", usePassKey, false);
            } else {
                //  No passkey is known to be on this device, show a small icon (passkeys on a phone or a security key can still be used, passkeys on this device are shown by the autofill)
                passKeyButton = new ColorIcon("IconPasskeyKey", "IconColorThemeMain", 32, 32,
                    _TF("Sign in with a passkey, ex: on a phone or a security key (if a user id is entered, only passkeys of that user can be used)", "Tool tip description of a small icon button that when pressed will attempt to sign in using a passkey"),
                    async () => await usePassKey(passKeyButton));
                passKeyButton.Element.classList.add("PasskeyIconButton");
                br.appendChild(passKeyButton.Element);
            }
        }
        setStatus(uname.value, pwd.value);
        uname.focus();
        //  Browsers may autofill the inputs after the page is loaded without any event (or without exposing the values), check for a while
        let lastAutoFill = "";
        for (const ms of [100, 250, 500, 1000, 2000, 3500, 5000]) {
            setTimeout(() => {
                const state = String(IsAutoFilled(uname)) + IsAutoFilled(pwd) + uname.value.length + "," + pwd.value.length;
                if (state === lastAutoFill)
                    return;
                lastAutoFill = state;
                setStatus(uname.value, pwd.value);
            }, ms);
        }

    });

}

// Common UI

/**
 * Generates a random password from look-alike-free alphabets.
 * Alphabets: lower "abcdefghjkmnpqrstuvwxyz", upper "ACDEFGHJKLMNOPQRSTUVWXY", digits "2345679", specials "!.?_"
 * (specials join the "any" pool only when minSpecials > 0). Draws the minimum counts per class, fills the rest from
 * the pool, then does 4 * length random swaps. Randomness from common.js CryptRandomInt32 via power-of-two masks.
 * Note: minSpecials is not part of the minimum-total length adjustment. Called by AuthNewPassword.
 * @param {number} [length=16] - Password length (falsy or < 8 gives 16; max 256; at least minUpper+minLower+minDigits).
 * @param {number} [minUpper=4] - Minimum upper-case letters (falsy/negative gives 4).
 * @param {number} [minLower=4] - Minimum lower-case letters (falsy/negative gives 4).
 * @param {number} [minDigits=4] - Minimum digits (falsy/negative gives 4).
 * @param {number} [minSpecials=0] - Minimum special characters.
 * @returns {string} The generated password.
 */
function GenPassword(length, minUpper, minLower, minDigits, minSpecials) {
    if ((!length) || (length < 8))
        length = 16;
    if ((!minUpper) || (minUpper < 0))
        minUpper = 4;
    if ((!minLower) || (minLower < 0))
        minLower = 4;
    if ((!minDigits) || (minDigits < 0))
        minDigits = 4;
    if ((!minSpecials) || (minSpecials < 0))
        minSpecials = 0;
    if (length > 256)
        length = 256;
    const minTotal = minUpper + minLower + minDigits;
    if (length < minTotal)
        length = minTotal;
    /** Alphabets (look-alike characters removed). Index order in lengths/masks: lower, upper, digits, specials, all, length. */
    const lower = "abcdefghjkmnpqrstuvwxyz";
    const upper = "ACDEFGHJKLMNOPQRSTUVWXY";
    const digits = "2345679";
    const specials = "!.?_";
    const all = lower + upper + digits + (minSpecials > 0 ? specials : "");
    const lengths = [lower.length, upper.length, digits.length, specials.length, all.length, length];
    const masks = [];
    for (let i = 0; i < 6; ++i) {
        const l = lengths[i];
        let m = 1;
        while (m < l)
            m += m;
        masks[i] = m - 1;
    }
    /**
     * Uniform-ish random integer in [0, cl): takes one CryptRandomInt32 value and tries `ri & mask`, then the bits
     * shifted by 8 and 16, accepting the first value < cl; falls back to (ri & 0xffffff) % cl.
     * @param {number} cl - Exclusive upper bound.
     * @param {number} mask - Smallest (power of two - 1) >= cl - 1.
     * @returns {number}
     */
    function getRandom(cl, mask) {
        const ri = CryptRandomInt32();
        let t = ri & mask;
        if (t < cl)
            return t;
        t = (ri >> 8) & mask;
        if (t < cl)
            return t;
        t = (ri >> 16) & mask;
        if (t < cl)
            return t;
        t = (ri & 0xffffff) % cl;
        return t;
    }

    /**
     * Random character from an alphabet.
     * @param {string} chrs - Alphabet.
     * @param {number} cl - Alphabet length.
     * @param {number} mask - Bit mask for cl (see getRandom).
     * @returns {string} One character.
     */
    function getRandomChar(chrs, cl, mask) {
        return chrs.charAt(getRandom(cl, mask));
    }

    /** Generated characters (shuffled at the end). @type {string[]} */
    const pwd = [];
    /**
     * Pushes `count` random characters from `chrs` onto pwd.
     * @param {string} chrs - Alphabet.
     * @param {number} count - Number of characters.
     * @param {number} index - Slot in the precomputed lengths / masks tables for this alphabet.
     * @returns {void}
     */
    function Add(chrs, count, index) {
        const mask = masks[index];
        const cl = lengths[index];
        for (let i = 0; i < count; ++i)
            pwd.push(getRandomChar(chrs, cl, mask));
    }
    Add(lower, minLower, 0);
    Add(upper, minUpper, 1);
    Add(digits, minDigits, 2);
    Add(specials, minSpecials, 3);
    Add(all, length - minTotal, 4);
    const sc = length * 4;
    const mask = masks[5];
    for (let i = 0; i < sc; ++i) {
        const a = getRandom(length, mask);
        const b = getRandom(length, mask);
        const t = pwd[a];
        pwd[a] = pwd[b];
        pwd[b] = t;
    }
    return pwd.join("");
}

/**
 * Creates (does not attach) the auth box: a <form class="SysWeaver-AuthBox"> whose onsubmit returns false.
 * Used by AuthPage; also by SimpleAuth.js (Auth.Core) and Passkey.js (PassKey).
 * @returns {HTMLFormElement} The new form.
 */
function CreateAuthWrapper() {
    const box = document.createElement("form");
    box.onsubmit = () => false;
    box.classList.add("SysWeaver-AuthBox");
    return box;

}


/**
 * Common page bootstrap used by every page of all four auth/ contributions.
 * 1. SysWeaverIgnoreUserChanges() (window.IgnoreUserChanged, so common.js does not reload this window on
 *    user.login / user.logout / reload server events).
 * 2. Creates the box (CreateAuthWrapper), defines box.OnLoaded and appends the box to document.body.
 * 3. Adds the icon: a 128x128 ColorIcon (IconColorThemeMain) for an icon class, or a non-draggable <img>
 *    (common.js createImg) when the string contains '.' or '/' after position 0 or iconIsFile is truthy.
 * 4. Awaits build(box, img), then calls box.OnLoaded(); on exception calls box.OnLoaded() and Fail(e).
 * Used by loginMain / logoutMain, SimpleAuth.js (Auth.Core), Passkey.js (PassKey), UserManagerUi.js (UserManager).
 * @param {string} icon - Icon class name (e.g. "IconAuthLogin") or image URL; falsy for no icon.
 * @param {function(HTMLFormElement, (ColorIcon|HTMLImageElement)):Promise<void>} build - Builds the page content.
 * @param {boolean} [iconIsFile] - Force treating `icon` as an image URL.
 * @returns {Promise<void>}
 */
async function AuthPage(icon, build, iconIsFile) {
    SysWeaverIgnoreUserChanges();
    const box = CreateAuthWrapper();
    try {
        const target = document.body;
        /**
         * Expando on the box: calls PageLoaded() once, then replaces itself with a no-op.
         * Build callbacks may call it early (e.g. logoutMain).
         * @returns {void}
         */
        box.OnLoaded = () => {
            PageLoaded();
            /** Replaced by a no-op after the first call. */
            box.OnLoaded = () => { };
        };

        target.appendChild(box);
        let img = null;
        if (icon) {
            if ((icon.indexOf('.') > 0) || (icon.indexOf('/') > 0) || iconIsFile) {
                img = createImg("img");
                img.draggable = false;
                img.src = icon;
                box.appendChild(img);
            } else {
                img = new ColorIcon(icon, "IconColorThemeMain", 128, 128);
                box.appendChild(img.Element);
            }
        }
        if (build)
            await build(box, img);
        box.OnLoaded();
    }
    catch (e) {
        box.OnLoaded();
        Fail(e);
    }
}


/**
 * Short-code entry step shared by the UserManager pages (only called by UserManagerUi.js,
 * SysWeaver.MicroService.UserManager). Replaces all children of an AuthPage box except the first (the icon); the
 * first non-"IconColor*" class of the icon element becomes the button icon. Adds the HTML message (AuthText + AuthBr),
 * a label, a code AuthInput (min 3 / max 16 chars) whose change handler normalizes the value with
 * cleanUpShortCode(value, true) and enables the button when cleanUpShortCode(value) is non-null, a *Continue* button
 * and an error line (class Error).
 * The button calls validateShortCode(code): < 0 disables the input ("Code expired or too many tries"), 0 shows
 * "Invalid code, try again", otherwise navigates with common.js Open(linkOnSuccess + code, "_self").
 * Cross-folder dependency (unguarded): cleanUpShortCode / validateShortCode are defined in UserManager.js
 * (SysWeaver.MicroService.UserManager), not in this project.
 * @param {HTMLFormElement} authPageElement - The box created by AuthPage.
 * @param {string} messageHtml - HTML message shown above the input (optional).
 * @param {string} linkOnSuccess - URL prefix; the validated code is appended and opened in the same window.
 * @param {number} [timeout=900] - Seconds; defaulted but not used.
 * @param {string} [instructions="Code"] - Label text.
 * @param {string} [placeHolder="Enter the sent code"] - Input placeholder / tooltip.
 * @param {string} [buttonText="Continue"] - Button text / tooltip.
 * @returns {Promise<void>}
 */
async function AuthPageValidation(authPageElement, messageHtml, linkOnSuccess, timeout, instructions, placeHolder, buttonText) {
    if ((!timeout) || (timeout <= 0))
        timeout = 15 * 60;
    if (!instructions)
        instructions = _TF("Code", "Label text for an input box where the user should enter a short code sent through mail or sms");
    if (!placeHolder)
        placeHolder = _TF("Enter the sent code", "Placeholder text for an input box where the user should enter a short code sent through mail or sms");
    if (!buttonText)
        buttonText = _TF("Continue", "Text of a button that when clicked will apply a short code that was sent through mail or sms");
    while (authPageElement.childElementCount > 1)
        authPageElement.lastElementChild.remove();
    let icon = null;
    const cl = authPageElement.firstElementChild.classList;
    const cll = cl.length;
    for (let i = 0; i < cll; ++i) {
        const cin = cl[i];
        if (cin.startsWith("IconColor"))
            continue;
        icon = cin;
        break;
    }

    if (messageHtml) {
        AuthText(authPageElement, messageHtml, null, true);
        AuthBr(authPageElement);
    }
    if (instructions)
        AuthLabel(authPageElement, instructions, instructions, false);
    let prevCode = null;


    const code = AuthInput(authPageElement, placeHolder, placeHolder, null, ev => {
        const shortCode = cleanUpShortCode(code.value, true);
        if (code.value !== shortCode)
            code.value = shortCode;
        if (prevCode === shortCode)
            return;
        prevCode = shortCode;
        AuthSetText(err, "");
        conButton.SetEnabled(cleanUpShortCode(shortCode) !== null);
    });
    code.minLength = 3;
    code.maxLength = 16;
    const buttons = AuthButtonRow(authPageElement);
    const conButton = AuthButton(buttons, buttonText, buttonText, icon, async button => {
        button.StartWorking();
        button.SetEnabled(false);
        try {
            const shortCode = cleanUpShortCode(code.value);
            if (!shortCode) {
                code.focus();
                throw new Error(_TF("Invalid code syntax", "Error message displayed when an entered short code has an invalid syntax"));
            }
            const valid = await validateShortCode(shortCode);
            if (valid < 0) {
                code.disabled = true;
                throw new Error(_TF("Code expired or too many tries", "Error message displayed when an entered short code have expired or if it's being used multiple times"));
            }
            if (valid <= 0) {
                code.focus();
                throw new Error(_TF("Invalid code, try again", "Error message displayed when an entered short code is invalid"));
            }
            const url = linkOnSuccess + shortCode;
            Open(url, "_self");
        }
        catch (e) {
            AuthSetText(err, e.message);
            button.StopWorking();
        }
    }, true);
    const err = AuthError(authPageElement, "", "");
    AuthSetError(err, true);
    code.focus();
}

/**
 * Formats user names / e-mail addresses as emphasized, HTML-escaped text. Used by UserManagerUi.js (UserManager).
 * @param {string|string[]} emailOrUsernameArray - One value (trimmed, wrapped in <em>) or an array (each wrapped in
 *   <em>, joined by ", ").
 * @returns {string} HTML string.
 */
function AuthUserHtml(emailOrUsernameArray) {
    if (typeof emailOrUsernameArray === "string")
        return "<em>" + makeHtmlSafe(emailOrUsernameArray.trim()) + "</em>";
    const t = [];
    emailOrUsernameArray.forEach(v => t.push(makeHtmlSafe(v.trim())));
    return "<em>" + t.join("</em>, <em>") + "</em>";
}

/**
 * Appends a SysWeaver-AuthText element (centered block text), copy-on-click (ValueFormat.copyOnClick).
 * Used by Auth.Core, PassKey and UserManager pages.
 * @param {HTMLElement} target - Parent element.
 * @param {string} text - Text (or HTML when isHtml); falsy leaves it empty.
 * @param {string} [title] - Tooltip.
 * @param {boolean} [isHtml] - Set innerHTML instead of innerText.
 * @returns {HTMLElement} The created element.
 */
function AuthText(target, text, title, isHtml) {
    const e = document.createElement("SysWeaver-AuthText");
    target.appendChild(e);
    if (text)
        if (isHtml)
            e.innerHTML = text;
        else
            e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

/**
 * Appends a SysWeaver-AuthLabel element (bold accent label above inputs), copy-on-click.
 * Used by Auth.Core, PassKey and UserManager pages.
 * @param {HTMLElement} target - Parent element.
 * @param {string} text - Text (or HTML when isHtml).
 * @param {string} [title] - Tooltip.
 * @param {boolean} [isHtml] - Set innerHTML instead of innerText.
 * @returns {HTMLElement} The created element.
 */
function AuthLabel(target, text, title, isHtml) {
    const e = document.createElement("SysWeaver-AuthLabel");
    target.appendChild(e);
    if (text)
        if (isHtml)
            e.innerHTML = text;
        else
            e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

/**
 * Create a "tab" bar
 * @param {HtmlElement} target where to add the tab bar
 * @param {string} title the tool tip to show on hover (if any)
 * @param {function(number)} onChangeFn the function that gets called whenever a tab is changed
 * @returns {HtmlElement} the created element
 *
 * Expandos on the returned SysWeaver-AuthTab element: Selected (index, -1 until the first header is added),
 * Add(text, title, isHtml, icon) and SetIndex(i, noFn) (= inner activateIndex). Used by UserManagerUi.js (UserManager).
 */
function AuthTab(target, title, onChangeFn) {
    const e = document.createElement("SysWeaver-AuthTab");
    target.appendChild(e);
    if (title)
        e.title = title;
    e.Selected = -1;
    /** Header elements in index order. @type {HTMLElement[]} */
    const headers = [];
    /**
     * Selects a tab: sets e.Selected, marks the chosen header Selected (icon color IconColorThemeBackground,
     * tabIndex -1) and the others unselected (IconColorThemeAcc2, tabIndex 0); calls onChangeFn(newIndex) unless noFn.
     * @param {number} newIndex - Index of the header to select.
     * @param {boolean} [noFn] - Do not call onChangeFn.
     * @returns {void}
     */
    function activateIndex(newIndex, noFn) {
        e.Selected = newIndex;
        const hl = headers.length;
        for (let i = 0; i < hl; ++i) {
            const h = headers[i];
            if (i === newIndex) {
                if (h.Icon)
                    h.Icon.ChangeColor("IconColorThemeBackground");
                h.classList.add("Selected");
                h.tabIndex = -1;
            } else {
                if (h.Icon)
                    h.Icon.ChangeColor("IconColorThemeAcc2");
                h.classList.remove("Selected");
                h.tabIndex = 0;
            }
        }
        if (!noFn)
            onChangeFn(newIndex);
    }


    /**
     * Adds a keyboard-clickable SysWeaver-AuthTabHeader. The first header added becomes selected (class Selected,
     * tabIndex -1). An optional 24x24 ColorIcon is appended and stored as header expando `Icon`.
     * Header clicks (ignoring badClick) call activateIndex(index).
     * @param {string} text - Header text (or HTML when isHtml).
     * @param {string} [title] - Tooltip.
     * @param {boolean} [isHtml] - Set innerHTML instead of innerText.
     * @param {string} [icon] - Icon class name.
     * @returns {void}
     */
    e.Add = (text, title, isHtml, icon) => {
        const t = document.createElement("SysWeaver-AuthTabHeader");
        keyboardClick(t);
        if (isHtml)
            t.innerHTML = text;
        else
            t.innerText = text;
        if (title)
            t.title = title;
        e.appendChild(t);
        const index = headers.length;
        const isSelected = e.Selected < 0;
        if (isSelected) {
            e.Selected = index;
            t.tabIndex = -1;
            t.classList.add("Selected");
        }
        if (icon) {
            icon = new ColorIcon(icon, isSelected ? "IconColorThemeBackground" : "IconColorThemeAcc2", 24, 24);
            t.appendChild(icon.Element);
            t.Icon = icon;
        }
        headers.push(t);
        t.onclick = ev => {
            if (badClick(ev))
                return;
            activateIndex(index);
        };
    };
    e.SetIndex = activateIndex;



    return e;
}

/**
 * Appends a SysWeaver-AuthError status / error line (plain text, copy-on-click). Classes Error / Warning / Good are
 * set later by CheckPassword / AuthSetError. Used by loginMain, Auth.Core and UserManager pages.
 * @param {HTMLElement} target - Parent element.
 * @param {string} text - Initial text.
 * @param {string} [title] - Tooltip.
 * @returns {HTMLElement} The created element.
 */
function AuthError(target, text, title) {
    const e = document.createElement("SysWeaver-AuthError");
    target.appendChild(e);
    if (text)
        e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

/**
 * New-password input: an AuthPassword (with show/hide icon) plus a second inside icon (IconAuthRandom, slot 1) that
 * generates GenPassword(len, 1, 1, 1, minSpecials), writes it to the input, copies it to the clipboard
 * (ValueFormat.copyToClipboardInfo) and awaits input.OnChange(). `len` is 16 clamped into the policy's
 * MinLength..MaxLength (max 128); minSpecials is 1 when the policy has MixedSpecial.
 * Used by SimpleAuth.js (Auth.Core) and UserManagerUi.js (UserManager).
 * @param {HTMLElement} target - Parent element.
 * @param {string} placeholder - Input placeholder.
 * @param {string} [title] - Tooltip.
 * @param {string} autocomplete - autocomplete attribute (e.g. "new-password").
 * @param {function(HTMLInputElement):Promise<void>} [onChange] - Change callback.
 * @param {function} [onNextItemFn] - Passed to tabToNextOnEnter.
 * @param {object} [passwordPolicy] - Password policy ({MinLength, MaxLength, MixedSpecial, ...}).
 * @returns {HTMLInputElement} The input.
 */
function AuthNewPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, passwordPolicy) {

    const e = AuthPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn);
    let len = 16;
    let minUpper = 1;
    let minLower = 1;
    let minDigits = 1;
    let minSpecials = 0;
    if (passwordPolicy) {
        let minLen = passwordPolicy.MinLength;
        if (minLen < 1)
            minLen = 1;
        let maxLen = passwordPolicy.MaxLength;
        if (maxLen < minLen)
            maxLen = minLen;
        if (maxLen > 128)
            maxLen = 128;
        if (len < minLen)
            len = minLen;
        if (len > maxLen)
            len = maxLen;
        if (passwordPolicy.MixedSpecial)
            minSpecials = 1;
    }
    AuthAddIcon(e, "IconAuthRandom", async inp => {
        const v = GenPassword(len, minUpper, minLower, minDigits, minSpecials);
        inp.value = v;
        await ValueFormat.copyToClipboardInfo(v);
        await inp.OnChange();
    }, null, _TF("Click to generate a new random password and copy it to the clipboard", "Tool tip description of a button that when pressed will generated a random safe password in the input field and also copy it to the clipboard"), null, 1);

    return e;
}


/**
 * Sets the text of a status element and resets its state: clears className and LastCheck (the CheckPassword memo).
 * Empty text becomes "&nbsp;" (keeps the line height). Used by this file, Auth.Core and UserManager.
 * @param {HTMLElement} target - Element to update.
 * @param {string} text - Text (or HTML when isHtml).
 * @param {boolean} [isHtml] - Set innerHTML instead of innerText.
 * @returns {void}
 */
function AuthSetText(target, text, isHtml) {
    target.className = "";
    target.LastCheck = null;
    if (!text) {
        target.innerHTML = "&nbsp;";
        return;
    }
    if (isHtml) {
        target.innerHTML = text;
        return;
    }
    target.innerText = text;
}

/**
 * Adds or removes the class Error (red glow on inputs / red text on SysWeaver-AuthError, see Auth.css).
 * @param {HTMLElement} target - Element to update.
 * @param {*} haveError - Truthy adds the class.
 * @returns {void}
 */
function AuthSetError(target, haveError) {
    if (haveError)
        target.classList.add("Error");
    else
        target.classList.remove("Error");
}

/**
 * Adds a clickable ColorIcon (IconColorThemeMain, not a tab stop, class InsideInput) inside the right edge of an input.
 * Position: `top` is computed from the input's computed height and bottom margin (the icon is appended to the input's
 * parent, after the input), `left` from the slot index; the input's paddingRight is widened to make room.
 * Wraps the existing onchange / oninput so each change re-evaluates iconEnabledFn(e) (SetEnabled) before calling the
 * original handler. Expandos on the input: OnChange (the wrapping onChange), "Icon" + index and, for index 0, Icon.
 * Used by AuthPassword, AuthInput, AuthInputPhone, AuthNewPassword and SimpleAuth.js (Auth.Core).
 * @param {HTMLInputElement} e - The input (must already be attached).
 * @param {string} iconClass - Icon class name.
 * @param {function(HTMLInputElement):(void|Promise<void>)} iconFn - Click handler (awaited).
 * @param {function(HTMLInputElement):boolean} [iconEnabledFn] - Enabled state (default always true).
 * @param {string} [iconTitle] - Tooltip of the icon.
 * @param {number} [size=32] - Icon size in px.
 * @param {number} [index=0] - Icon slot from the right (0 = right-most).
 * @returns {void}
 */
function AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle, size, index) {

    if ((!size) || (size <= 0))
        size = 32;
    if ((!index) || (index < 0))
        index = 0;
    const iconSpacing = 4;
    const index0 = index;
    ++index;
    if (!iconEnabledFn)
        iconEnabledFn = e => true;
    /** The previous onchange handler (wrapped). */
    const oc = e.onchange;
    /**
     * Change handler: updates the icon's enabled state and awaits the previous onchange handler.
     * @param {Event} [ev] - The change event (undefined when called as input.OnChange()).
     * @returns {Promise<void>}
     */
    async function onChange(ev) {
        b.SetEnabled(iconEnabledFn(e));
        if (oc)
            await oc(ev);
    }
    e.OnChange = onChange;
    let b = null;
    e.onchange = onChange;
    /** The previous oninput handler (wrapped). */
    const oi = e.oninput;
    //  Input handler: re-evaluates the icon state, keeps the icon out of the tab order and calls the previous handler
    e.oninput = async ev => {
        b.SetEnabled(iconEnabledFn(e));
        b.Element.tabIndex = "-1";
        if (oi)
            await oi(ev);
    };
    e.style.paddingRight = (size * index + (iconSpacing * index0) + 16) + "px";
    b = new ColorIcon(iconClass, "IconColorThemeMain", size, size, iconTitle ?? null, async () => {
        await iconFn(e);
    });
    e["Icon" + index0] = b;
    if (index0 <= 0)
        e.Icon = b;
    b.TabStop = false;
    const be = b.Element;
    const cs = getComputedStyle(e);
    const height = parseFloat(cs.height);
    const mb = parseFloat(cs.marginBottom);
    const p = -0.5 * (height + mb + size);
    be.style.top = p + "px";
    be.style.left = "calc(100% - " + (size * index + index0 * (size + iconSpacing) + 8) + "px)";
    be.classList.add("InsideInput");
    b.SetEnabled(iconEnabledFn(e));
    e.parentElement.appendChild(be);
    //e.appendChild(be);
}

/**
 * Appends a required <input type="password"> (length 8..128). onChange(input) is wired to onchange, oninput and the
 * expando OnChange (local wrapper oc). Enter moves on via tabToNextOnEnter(e, onNextItemFn).
 * Without iconClass a show/hide toggle icon is added (IconAuthView <-> IconAuthViewOff, switching type between
 * "password" and "text" and updating the tooltip); iconClass "-" means no icon.
 * Used by loginMain, AuthNewPassword and UserManagerUi.js (UserManager).
 * @param {HTMLElement} target - Parent element.
 * @param {string} placeholder - Placeholder text.
 * @param {string} [title] - Tooltip.
 * @param {string} autocomplete - autocomplete attribute ("current-password" / "new-password").
 * @param {function(HTMLInputElement):Promise<void>} [onChange] - Change callback.
 * @param {function} [onNextItemFn] - Passed to tabToNextOnEnter (called on Enter instead of focusing the next item).
 * @param {string} [iconClass] - Inside icon class; falsy = show/hide toggle, "-" = none.
 * @param {function(HTMLInputElement):void} [iconFn] - Icon click handler.
 * @param {function(HTMLInputElement):boolean} [iconEnabledFn] - Icon enabled state.
 * @param {string} [iconTitle] - Icon tooltip.
 * @returns {HTMLInputElement} The input.
 */
function AuthPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    e.type = "password";
    e.placeholder = placeholder;
    e.autocomplete = autocomplete;
    e.required = true;
    e.minLength = 8;
    e.maxLength = 128;
    target.appendChild(e);
    if (title)
        e.title = title;
    if (onChange) {
        /**
         * Change wrapper wired to onchange / oninput / OnChange; awaits onChange(input).
         * @returns {Promise<void>}
         */
        const oc = async () => {
            await onChange(e);
        };
        e.onchange = oc;
        e.oninput = oc;
        e.OnChange = oc;
    }
    tabToNextOnEnter(e, onNextItemFn);
    if (!iconClass) {
        iconClass = "IconAuthView";
        const shw = _TF("Click to show password", "Tool tip description of a button that when clicked will show the password entered in an input box");
        iconFn = inp => {
            const icon = inp.Icon;
            if (inp.type === "password") {
                inp.type = "text";
                icon.ChangeImage("IconAuthViewOff");
                icon.SetTitle(_TF("Click to hide password", "Tool tip description of a button that when clicked will hide the password entered in an input box"));
            }
            else {
                inp.type = "password";
                icon.ChangeImage("IconAuthView");
                icon.SetTitle(shw);
            }
        };
        iconTitle = shw;
    }
    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}

/**
 * Appends a required <input type="text"> (length 1..128), wiring onChange like AuthPassword (local oc, expando OnChange).
 * Without iconFn the inside icon (default IconAuthCopy) copies the value to the clipboard and is enabled only when the
 * input has a value; iconClass "-" means no icon. Used by loginMain, AuthPageValidation, Auth.Core, PassKey, UserManager.
 * @param {HTMLElement} target - Parent element.
 * @param {string} placeholder - Placeholder text.
 * @param {string} [title] - Tooltip.
 * @param {string} [autocomplete] - autocomplete attribute.
 * @param {function(HTMLInputElement):Promise<void>} [onChange] - Change callback.
 * @param {function} [onNextItemFn] - Passed to tabToNextOnEnter.
 * @param {string} [iconClass="IconAuthCopy"] - Inside icon class; "-" = none.
 * @param {function(HTMLInputElement):void} [iconFn] - Icon click handler (default: copy to clipboard).
 * @param {function(HTMLInputElement):boolean} [iconEnabledFn] - Icon enabled state (overridden when iconFn is falsy).
 * @param {string} [iconTitle] - Icon tooltip (overridden when iconFn is falsy).
 * @returns {HTMLInputElement} The input.
 */
function AuthInput(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    e.type = "text";
    e.placeholder = placeholder;
    if (autocomplete)
        e.autocomplete = autocomplete;
    e.required = true;
    e.minLength = 1;
    e.maxLength = 128;
    target.appendChild(e);
    if (title)
        e.title = title;
    if (onChange) {
        /**
         * Change wrapper wired to onchange / oninput / OnChange; awaits onChange(input).
         * @returns {Promise<void>}
         */
        const oc = async () => {
            await onChange(e);
        };
        e.onchange = oc;
        e.oninput = oc;
        e.OnChange = oc;
    }
    tabToNextOnEnter(e, onNextItemFn);

    if (!iconClass)
        iconClass = "IconAuthCopy";
    if (!iconFn) {
        iconFn = async inp => {
            const v = inp.value;
            if (inp)
                await ValueFormat.copyToClipboardInfo(v);
        };
        iconEnabledFn = e => !!e.value;
        iconTitle = _TF("Clip to copy text", "Tool tip description on a button that when clicked will copy the content of an input box to the clipboard");
    }
    if (!iconEnabledFn)
        iconEnabledFn = e => true;

    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}

/**
 * Appends a SysWeaver-AuthPhone group holding a required <input type="tel" class="Phone"> (length 1..128) and a
 * keyboard-clickable SysWeaver-AuthPhoneCc flag button. The change handler (local oc, also onchange / oninput /
 * OnChange) ignores unchanged values, POSTs ../Api/auth/GetPhonePrefixInfo(value), builds a tooltip of the matching
 * "+CountryCode RegionPrefixes Name" lines, calls setCountry with the first IsoCountry (or resets it), then awaits
 * onChange(input). onkeydown allows only digits, '.', space, named/control keys, Ctrl combinations and a single '+'.
 * The flag onclick only filters badClick (empty stub). Copy icon defaults as in AuthInput.
 * Used by UserManagerUi.js (UserManager).
 * @param {HTMLElement} target - Parent element.
 * @param {string} placeholder - Placeholder text.
 * @param {string} [title] - Tooltip.
 * @param {string} autocomplete - autocomplete attribute (e.g. "tel").
 * @param {function(HTMLInputElement):Promise<void>} [onChange] - Change callback.
 * @param {function} [onNextItemFn] - Passed to tabToNextOnEnter.
 * @param {string} [iconClass="IconAuthCopy"] - Inside icon class; "-" = none.
 * @param {function(HTMLInputElement):void} [iconFn] - Icon click handler (default: copy to clipboard).
 * @param {function(HTMLInputElement):boolean} [iconEnabledFn] - Icon enabled state.
 * @param {string} [iconTitle] - Icon tooltip.
 * @returns {HTMLInputElement} The tel input (not the group).
 */
function AuthInputPhone(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    const group = document.createElement("SysWeaver-AuthPhone");
    group.appendChild(e);
    /** The flag button. @type {HTMLElement} */
    const cc = document.createElement("SysWeaver-AuthPhoneCc");

    /**
     * Updates the flag: background image ../iso_data/country/<ISO>.svg, tooltip = title + "Click to select country
     * code prefix", toggles class AllSame (no CSS rule uses it).
     * @param {string} [iso="_"] - ISO country code ("_" = unknown flag).
     * @param {string} [title] - Matching prefixes text.
     * @param {boolean} [allSame] - True when all matching prefixes share one country calling code.
     * @returns {void}
     */
    function setCountry(iso, title, allSame) {
        if (!iso)
            iso = "_";
        if (!title)
            title = _TF("Click to select country code prefix", "Tool tip description on a button that when pressed will let the user select a country code calling prefix");
        else {
            title = title + "\n\n" + _TF("Click to select country code prefix", "Tool tip description on a button that when pressed will let the user select a country code calling prefix");
        }
        if (allSame)
            cc.classList.add("AllSame");
        else
            cc.classList.remove("AllSame");
            

        cc.style.backgroundImage = "url('../iso_data/country/" + iso.toUpperCase() + ".svg')";
        cc.title = title;
    }

    e.type = "tel";
    e.placeholder = placeholder;
    e.autocomplete = autocomplete;;
    e.required = true;
    e.minLength = 1;
    e.maxLength = 128;
    e.classList.add("Phone");
    target.appendChild(group);
    if (title)
        e.title = title;
    let isInternal = false;
    let old = null;
    let prefixes = null;
    /**
     * Change wrapper (onchange / oninput / OnChange): looks up the phone prefix and updates the flag, then awaits
     * onChange(input). Skips when the trimmed value is unchanged (isInternal is never set to true).
     * @returns {Promise<void>}
     */
    const oc = async () => {
        if (isInternal)
            return;
        const val = e.value.trim();
        if (val === old)
            return;
        old = val;
        let found = false;
        if (val.length > 0) {
            try {
                prefixes = await sendRequest("../Api/auth/GetPhonePrefixInfo", val);
                if (prefixes) {
                    const pl = prefixes.length;
                    let allSame = true;
                    for (let i = 1; i < pl; ++i) {
                        allSame = prefixes[0].CountryCode === prefixes[i].CountryCode;
                        if (!allSame)
                            break;
                    }
                    let name = "";
                    let fiso = null;
                    for (let i = 0; i < pl; ++i) {
                        const pp = prefixes[i];
                        const iso = pp.IsoCountry;
                        if (iso) {
                            if (!fiso)
                                fiso = iso;
                            if (name.length > 0)
                                name += "\n";
                            name += ("+" + pp.CountryCode + " ");
                            const rp = pp.RegionPrefixes;
                            if (rp) 
                                name += (rp + " ");
                            name += pp.Name;
                        }
                    }
                    if (fiso) {
                        setCountry(fiso, name, allSame);
                        found = true;
                    }
                }
            }
            catch
            {
            }
        }
        if (!found)
            setCountry();
        if (onChange)
            await onChange(e);
    };
    e.onkeydown = evt => {
        if (evt.ctrlKey)
            return;
        if (evt.key.length > 1)
            return;
        if (evt.key === ' ')
            return;
        if (evt.key === '+') {
            if (e.value.indexOf('+') < 0)
                return;
        }
        if (/[0-9.]/.test(evt.key))
            return;
        evt.preventDefault();
    };
    e.onchange = oc;
    e.oninput = oc;
    e.OnChange = oc;
    tabToNextOnEnter(e, onNextItemFn);

    if (!iconClass)
        iconClass = "IconAuthCopy";
    if (!iconFn) {
        iconFn = async inp => {
            const v = inp.value;
            if (inp)
                await ValueFormat.copyToClipboardInfo(v);
        };
        iconEnabledFn = e => !!e.value;
        iconTitle = _TF("Clip to copy text", "Tool tip description on a button that when clicked will copy the content of an input box to the clipboard");
    }
    if (!iconEnabledFn)
        iconEnabledFn = e => true;

        
    group.appendChild(cc);
    setCountry();
    cc.onclick = async (ev) => {
        if (badClick(ev))
            return;

    };
    keyboardClick(cc);
    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}



/**
 * Appends a SysWeaver-CenterBlock container for buttons. Used by this file, Auth.Core, PassKey and UserManager.
 * @param {HTMLElement} target - Parent element.
 * @returns {HTMLElement} The created element.
 */
function AuthButtonRow(target) {
    const e = document.createElement("SysWeaver-CenterBlock");
    target.appendChild(e);
    return e;
}

/**
 * Creates a common.js Button (enabled unless `disabled`) whose click awaits onClick(button) and appends its Element.
 * Members used by callers: SetEnabled, StartWorking, StopWorking, Element. Used by this file, Auth.Core, PassKey, UserManager.
 * @param {HTMLElement} target - Parent element.
 * @param {string} name - Button text.
 * @param {string} title - Tooltip.
 * @param {string} icon - Icon class name.
 * @param {function(Button):Promise<void>} onClick - Click handler, receives the Button.
 * @param {boolean} [disabled] - Create disabled.
 * @returns {Button} The Button instance.
 */
function AuthButton(target, name, title, icon, onClick, disabled) {
    const button = new Button(null, name, title, icon, !disabled, async fn => {
        await onClick(button);
    });
    target.appendChild(button.Element);
    return button;
}

/**
 * Appends a horizontal rule (<hr>). Used by Passkey.js (PassKey).
 * @param {HTMLElement} target - Parent element.
 * @returns {HTMLHRElement} The created element.
 */
function AuthHr(target) {
    const e = document.createElement("hr");
    target.appendChild(e);
    return e;
}

/**
 * Appends a line break (<br>). Used by AuthPageValidation.
 * @param {HTMLElement} target - Parent element.
 * @returns {HTMLBRElement} The created element.
 */
function AuthBr(target) {
    const e = document.createElement("br");
    target.appendChild(e);
    return e;
}

/**
 * Post-login navigation used by all flows that end signed in (loginMain, Passkey.js, UserManagerUi.js).
 * Removes the query and the last two path segments (assumes the URL is exactly <root>/auth/<page>.html), ensures a
 * trailing '/', waits 3 s (delay(3000)), then history.replaceState to that root URL and location.reload(true).
 * When the page is embedded in a same-origin window (ex: the app shell's content iframe), the top window is
 * reloaded instead (its hash cleared), so the shell never ends up nested inside its own iframe; this does not
 * depend on the "user.login" server message reaching the top window in time.
 * @returns {Promise<void>}
 */
async function AuthStartPage() {
    if (window.top !== window.self) {
        let top = null;
        try {
            if (typeof window.top.ReloadAll === "function")
                top = window.top;
        }
        catch
        {
            //  Cross-origin parent: navigate this window as usual
        }
        if (top) {
            await delay(3000);
            top.ReloadAll(true, true);
            return;
        }
    }
    const loc = window.location;
    const url = new URL(loc.href);
    url.search = "";
    const t = url.pathname.split('/');
    t.splice(t.length - 2, 2);
    url.pathname = t.join('/');
    let startUrl = url.href;
    if (!startUrl.endsWith("/"))
        startUrl += "/";
    await delay(3000);
    history.replaceState({}, "", startUrl);
    location.reload(true);
}

/**
 * Check if an input element was filled by the browser (some browsers doesn't expose the value until the user interacts with the page)
 * @param {HTMLInputElement} inputElement The input element
 * @returns {boolean} True if the browser autofilled the input
 */
function IsAutoFilled(inputElement) {
    if (!inputElement)
        return false;
    try {
        if (inputElement.matches(':autofill'))
            return true;
    } catch (err) {
    }
    try {
        return inputElement.matches(':-webkit-autofill');
    } catch (er) {
    }
    return false;
}