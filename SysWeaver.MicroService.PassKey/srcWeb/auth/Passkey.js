

//https://github.com/passwordless-lib/fido2-net-lib?tab=readme-ov-file#examples
//https://developer.mozilla.org/en-US/docs/Web/API/PublicKeyCredentialCreationOptions#pubkeycredparams

/**
 * A server challenge that is fetched before the user starts the ceremony, so that navigator.credentials.get / create is called directly from the user gesture (required by Safari).
 */
class PassKeyChallenge {

    constructor(challengeName, arg, submitName, isCreate) {
        this.ChallengeName = challengeName;
        this.Arg = arg;
        this.SubmitName = submitName;
        this.IsCreate = isCreate;
        this.Options = null;
        this.Fetched = 0;
    }

    /**
     * Fetch a challenge (if there isn't a fresh one).
     * @returns {object} The options from the server (check the error member)
     */
    async Fetch() {
        const o = this.Options;
        if (o && (!o.error)) {
            const t = o.publicKey.timeout;
            const maxAge = (t && (t > 20000)) ? (t - 15000) : 60000;
            if ((Date.now() - this.Fetched) < maxAge)
                return o;
        }
        this.Options = null;
        const c = await PassKey.Call(this.ChallengeName, this.Arg);
        if (!c)
            throw new Error(_TF("Failed to get a challenge from the server", "Error message shown when a passkey challenge couldn't be fetched from the server"));
        RemoveNulls(c);
        this.Options = c;
        this.Fetched = Date.now();
        return c;
    }

    /**
     * Run the ceremony (fetches a challenge if needed).
     * @returns {object} The result, with an Error member (0 = success, see PassKey.Errors) and a User member if a user was signed in
     */
    async Run() {
        if (!PassKey.IsSupported())
            return { Error: PassKey.Errors.NotSupported };
        const c = await this.Fetch();
        this.Options = null;
        if (c.error)
            return { Error: c.error };
        return this.IsCreate ? await PassKey.Create(c, this.SubmitName) : await PassKey.Get(c, this.SubmitName);
    }

    /**
     * The user name (when creating a passkey), or null
     */
    get UserName() {
        const o = this.Options;
        return o?.publicKey?.user?.name ?? null;
    }
}


class PassKey {

    static ApiPrefix = "../Api/auth/passkey/";

    /**
     * The error codes (same as PassKeyErrors on the server), Cancelled and AlreadyOnDevice are client side only
     */
    static Errors = {
        None: 0,
        NotSupported: 1,
        AlreadySignedIn: 2,
        NotSignedIn: 3,
        ChallengeExpired: 4,
        UnknownCredential: 5,
        NoPassKeys: 6,
        VerificationFailed: 7,
        AlreadyRegistered: 8,
        TokenExpired: 9,
        AccountExists: 10,
        LastSignInMethod: 11,
        Cancelled: -1,
        AlreadyOnDevice: -2,
    };

    /**
     * Check if the browser supports passkeys
     * @returns {boolean} True if passkeys can be used
     */
    static IsSupported() {
        return (!!window.PublicKeyCredential) && (!!navigator.credentials) && window.isSecureContext;
    }

    /**
     * Get a user friendly text for a result
     * @param {object} result The result of a passkey operation
     * @returns {string} The text
     */
    static ErrorText(result) {
        const e = PassKey.Errors;
        switch (result?.Error) {
            case e.None:
                return "";
            case e.NotSupported:
                return _TF("Passkeys can't be used here, a secure connection (https) and a browser that supports passkeys is required.", "Error message shown when passkeys can't be used on the current site or browser");
            case e.AlreadySignedIn:
                return _TF("A user is already signed in, please sign out first.", "Error message shown when a passkey operation requires that no user is signed in");
            case e.NotSignedIn:
                return _TF("No user is signed in, please sign in first.", "Error message shown when a passkey operation requires a signed in user");
            case e.ChallengeExpired:
                return _TF("The request took too long, please try again.", "Error message shown when a passkey challenge has expired");
            case e.UnknownCredential:
                return _TF("This passkey isn't valid for this site any more, please select another one or sign in with your password.", "Error message shown when a passkey that the server doesn't know was used");
            case e.NoPassKeys:
                return _TF("No passkey was found for this user.", "Error message shown when a user that have no passkeys tried to sign in using a passkey");
            case e.VerificationFailed:
                return _TF("The passkey couldn't be verified.", "Error message shown when a passkey failed verification on the server");
            case e.AlreadyRegistered:
            case e.AlreadyOnDevice:
                return _TF("A passkey for this account already exists on this device.", "Error message shown when trying to add a passkey to a device that already have one for the account");
            case e.TokenExpired:
                return _TF("The link or QR code has expired or was already used.", "Error message shown when a passkey link or QR code is no longer valid");
            case e.AccountExists:
                return _TF("The account already exists.", "Error message shown when trying to create an account that already exists");
            case e.LastSignInMethod:
                return _TF("The last way to sign in can't be removed.", "Error message shown when trying to remove the last passkey of a user that have no password");
            case e.Cancelled:
                return _TF("Cancelled.", "Message shown when the user cancelled a passkey operation");
        }
        return _TF("The passkey operation failed.", "Generic error message for a failed passkey operation");
    }

    static async Call(name, arg) {
        return await sendRequest(PassKey.ApiPrefix + name, arg, true, null, false, null, true);
    }

    static async GetLoggedInUser() {
        const u = await getRequest(PassKey.ApiPrefix + "../GetUser", true);
        if (!u)
            return null;
        return u.Succeeded ? u : null;
    }

    static #SetUser(result) {
        if (result && (result.Error == 0) && result.User)
            sessionStorage.setItem("SysWeaver.User", JSON.stringify(result.User));
    }

    static #HintKey = "SysWeaver.PassKey.Hint";

    /**
     * True if a passkey was created or used on this device (browser) before.
     * This is only a hint, browsers doesn't tell if there are any passkeys (synced passkeys created on other devices aren't known).
     * @returns {boolean} True if a passkey is likely to be available on this device
     */
    static HaveHint() {
        try {
            return !!localStorage.getItem(PassKey.#HintKey);
        }
        catch {
            return false;
        }
    }

    /**
     * Remember that a passkey is available on this device (if the passkey was on this device and not on a phone or security key)
     * @param {string} authenticatorAttachment The authenticatorAttachment of the credential
     * @param {string} credentialId The credential id (base64url, as in PublicKeyCredential.id)
     */
    static #SetHint(authenticatorAttachment, credentialId) {
        if (authenticatorAttachment === "cross-platform")
            return;
        try {
            localStorage.setItem(PassKey.#HintKey, "1");
            if (credentialId) {
                const ids = PassKey.#GetList(PassKey.#LocalKey).filter(x => x !== credentialId);
                ids.push(credentialId);
                PassKey.#SetList(PassKey.#LocalKey, ids);
            }
        }
        catch {
        }
    }

    /**
     * Credential ids (base64url) that was created or used on this device
     */
    static #LocalKey = "SysWeaver.PassKey.Local";

    /**
     * User guids where the browser reported that a passkey for the user already exists on this device (but it's unknown which one)
     */
    static #UserOnDeviceKey = "SysWeaver.PassKey.UserOnDevice";

    static #GetList(key) {
        try {
            const v = JSON.parse(localStorage.getItem(key) ?? "[]");
            return Array.isArray(v) ? v : [];
        }
        catch {
            return [];
        }
    }

    static #SetList(key, list) {
        try {
            //  Keep it small
            while (list.length > 64)
                list.shift();
            localStorage.setItem(key, JSON.stringify(list));
        }
        catch {
        }
    }

    /**
     * Convert a standard base64 string (as used by the server) to base64url (as used by the browser)
     */
    static #ToBase64Url(s) {
        return (s ?? "").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    /**
     * Check if a passkey is known to be available on this device (it was created or used on this device)
     * @param {string} id The credential id (standard base64, as PassKeyInfo.Id from the server)
     * @returns {boolean} True if the passkey is known to be on this device
     */
    static IsOnDevice(id) {
        const u = PassKey.#ToBase64Url(id);
        return PassKey.#GetList(PassKey.#LocalKey).includes(u);
    }

    /**
     * Check if the browser reported that a passkey for a user is available on this device (when trying to add another one)
     * @param {string} userGuid The guid of the user
     * @returns {boolean} True if a passkey of the user is on this device
     */
    static IsUserOnDevice(userGuid) {
        return (!!userGuid) && PassKey.#GetList(PassKey.#UserOnDeviceKey).includes(userGuid);
    }

    /**
     * Remember that a passkey for a user is available on this device (the browser refused to add another one)
     * @param {string} userGuid The guid of the user
     */
    static SetUserOnDevice(userGuid) {
        if (!userGuid)
            return;
        const list = PassKey.#GetList(PassKey.#UserOnDeviceKey).filter(x => x !== userGuid);
        list.push(userGuid);
        PassKey.#SetList(PassKey.#UserOnDeviceKey, list);
    }

    /**
     * Forget what is known about passkeys on this device for a removed passkey
     * @param {string} id The credential id (standard base64, as PassKeyInfo.Id from the server)
     * @param {string} userGuid The guid of the user
     */
    static #ForgetOnDevice(id, userGuid) {
        const u = PassKey.#ToBase64Url(id);
        PassKey.#SetList(PassKey.#LocalKey, PassKey.#GetList(PassKey.#LocalKey).filter(x => x !== u));
        //  It's unknown which passkey the browser found, so it could have been the removed one
        if (userGuid)
            PassKey.#SetList(PassKey.#UserOnDeviceKey, PassKey.#GetList(PassKey.#UserOnDeviceKey).filter(x => x !== userGuid));
    }

    /**
     * Check if the browser supports passkey autofill (conditional mediation)
     * @returns {boolean} True if passkey autofill is supported
     */
    static async IsAutofillSupported() {
        try {
            return PassKey.IsSupported() && (!!PublicKeyCredential.isConditionalMediationAvailable) && (await PublicKeyCredential.isConditionalMediationAvailable());
        }
        catch {
            return false;
        }
    }

    /**
     * Start passkey autofill: passkeys of this site are listed in the autofill drop down of inputs with autocomplete="username webauthn".
     * The request is restarted with a new challenge before the challenge expires and after a failed sign in.
     * Only one WebAuthn request can be active, so call Stop() before starting another request, and Start() again when it's done.
     * @param {function(object)} onResult Called with the result when a user selected a passkey (the user is signed in if the Error member is 0)
     * @returns {object} An object with Start() and Stop() methods, the autofill is started
     */
    static Autofill(onResult) {
        const state = {
            Run: 0,
            Abort: null,
        };
        async function run(runId) {
            while (state.Run === runId) {
                let c;
                try {
                    c = await PassKey.Call("GetAuthChallenge", null);
                }
                catch {
                    return;
                }
                if ((!c) || c.error || (state.Run !== runId))
                    return;
                RemoveNulls(c);
                const ac = new AbortController();
                state.Abort = ac;
                //  Restart with a new challenge before the challenge expires
                const t = c.publicKey.timeout;
                const refresh = setTimeout(() => ac.abort(), (t && (t > 20000)) ? (t - 15000) : 60000);
                let r;
                try {
                    r = await PassKey.Get(c, "Auth", { mediation: "conditional", signal: ac.signal });
                }
                catch (e) {
                    r = { Error: PassKey.Errors.VerificationFailed, Message: "" + e };
                }
                finally {
                    clearTimeout(refresh);
                    if (state.Abort === ac)
                        state.Abort = null;
                }
                if (state.Run !== runId)
                    return;
                if (r.Error == PassKey.Errors.Cancelled)
                    continue;
                await onResult(r);
                if (r.Error == 0)
                    return;
                await delay(1000);
            }
        }
        const ctl = {
            Start() {
                const id = ++state.Run;
                run(id);
            },
            Stop() {
                ++state.Run;
                const a = state.Abort;
                state.Abort = null;
                if (a)
                    a.abort();
            },
        };
        ctl.Start();
        return ctl;
    }

    static #FromBrowserError(e) {
        const n = e?.name;
        if ((n === "NotAllowedError") || (n === "AbortError"))
            return { Error: PassKey.Errors.Cancelled };
        if (n === "InvalidStateError")
            return { Error: PassKey.Errors.AlreadyOnDevice };
        if (n === "SecurityError")
            return { Error: PassKey.Errors.NotSupported, Message: "" + e };
        throw e;
    }

    static #Ids(list) {
        if (list)
            list.forEach(x => x.id = base64ToArray(x.id));
    }

    static #Transports(res) {
        try {
            return res.getTransports ? res.getTransports() : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Run a navigator.credentials.get ceremony with options from the server and submit the response
     * @param {object} c The options from the server
     * @param {string} submitName The api to submit the response to
     * @param {object} extra Optional additional options to navigator.credentials.get, ex: { mediation: "conditional", signal: abortSignal }
     */
    static async Get(c, submitName, extra) {
        const pk = c.publicKey;
        pk.challenge = base64ToArray(pk.challenge);
        PassKey.#Ids(pk.allowCredentials);
        let cc;
        try {
            cc = await navigator.credentials.get(Object.assign({ publicKey: pk }, extra));
        }
        catch (e) {
            return PassKey.#FromBrowserError(e);
        }
        if (!cc)
            return { Error: PassKey.Errors.Cancelled };
        const res = cc.response;
        const r = await PassKey.Call(submitName, {
            challengeId: c.challengeId,
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    authenticatorData: await bufferToBase64(res.authenticatorData),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                    signature: await bufferToBase64(res.signature),
                    userHandle: await bufferToBase64(res.userHandle),
                },
            type: cc.type,
        });
        if (!r)
            throw new Error(_TF("No response from the server", "Error message shown when the server didn't respond to a passkey request"));
        if ((r.Error == PassKey.Errors.UnknownCredential) && PublicKeyCredential.signalUnknownCredential) {
            //  Let the passkey provider remove (or hide) the passkey
            try {
                await PublicKeyCredential.signalUnknownCredential({ rpId: pk.rpId, credentialId: cc.id });
            }
            catch {
            }
        }
        if (r.Error == 0)
            PassKey.#SetHint(cc.authenticatorAttachment, cc.id);
        PassKey.#SetUser(r);
        return r;
    }

    /**
     * Run a navigator.credentials.create ceremony with options from the server and submit the response
     */
    static async Create(c, submitName) {
        const pk = c.publicKey;
        pk.challenge = base64ToArray(pk.challenge);
        pk.user.id = base64ToArray(pk.user.id);
        PassKey.#Ids(pk.excludeCredentials);
        let cc;
        try {
            cc = await navigator.credentials.create({ publicKey: pk });
        }
        catch (e) {
            return PassKey.#FromBrowserError(e);
        }
        if (!cc)
            return { Error: PassKey.Errors.Cancelled };
        const res = cc.response;
        const r = await PassKey.Call(submitName, {
            challengeId: c.challengeId,
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    attestationObject: await bufferToBase64(res.attestationObject),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                    transports: PassKey.#Transports(res),
                },
            type: cc.type,
        });
        if (!r)
            throw new Error(_TF("No response from the server", "Error message shown when the server didn't respond to a passkey request"));
        if (r.Error == 0)
            PassKey.#SetHint(cc.authenticatorAttachment, cc.id);
        PassKey.#SetUser(r);
        return r;
    }

    /**
     * Sign in using a passkey
     * @param {string} userIdentifier Optional user id (user name, email or phone), if not supplied any passkey for this site can be used
     * @returns {PassKeyChallenge} Call Run() to sign in
     */
    static SignIn(userIdentifier) {
        return userIdentifier
            ? new PassKeyChallenge("GetUserAuthChallenge", userIdentifier, "Auth", false)
            : new PassKeyChallenge("GetAuthChallenge", null, "Auth", false);
    }

    /**
     * Add a passkey to the signed in user
     * @returns {PassKeyChallenge} Call Run() to add the passkey
     */
    static Attach() {
        return new PassKeyChallenge("GetCreateChallenge", null, "Create", true);
    }

    /**
     * Add a passkey to the user of a token (from a QR code, a reset password or an add password link) and sign in
     * @param {string} token The token
     * @returns {PassKeyChallenge} Call Run() to add the passkey
     */
    static AttachUsingToken(token) {
        return new PassKeyChallenge("GetResetChallenge", token, "New", true);
    }

    /**
     * Create a new account with a passkey using a sign up token and sign in
     * @param {string} token The sign up token
     * @returns {PassKeyChallenge} Call Run() to create the account
     */
    static NewAccount(token) {
        return new PassKeyChallenge("GetNewChallenge", token, "New", true);
    }

    /**
     * Get the passkeys of the signed in user
     * @returns {object[]} The passkeys (Id, Name, Created, LastUsed, Synced, ThisDevice)
     */
    static async GetPassKeys() {
        return await PassKey.Call("GetPassKeys", null);
    }

    /**
     * Rename a passkey of the signed in user
     */
    static async Rename(id, name) {
        return await PassKey.Call("RenamePassKey", { Id: id, Name: name });
    }

    /**
     * Remove a passkey of the signed in user
     * @param {string} id The credential id (PassKeyInfo.Id)
     * @param {string} userGuid Optional guid of the signed in user (to forget that a passkey of the user is on this device)
     * @returns {object} The result (Error = LastSignInMethod if it can't be removed)
     */
    static async Remove(id, userGuid) {
        const r = await PassKey.Call("DeletePassKey", id);
        if (r && (r.Error == 0))
            PassKey.#ForgetOnDevice(id, userGuid);
        return r;
    }

}



async function addPassKeyMain()
{
    await AuthPage("IconAddPasskey", async (target, img) => {

        const ps = getUrlParams();
        const token = ps.get('token');

        const label = AuthLabel(target, "");
        const text = AuthText(target, "");
        const buttons = AuthButtonRow(target);
        let button = null;

        function done(message, icon) {
            if (icon)
                img.ChangeImage(icon);
            text.classList.remove("Fail");
            text.classList.add("Info");
            text.innerText = message;
            if (button)
                button.Element.classList.add("Hide");
        }

        if (!PassKey.IsSupported())
            return done(PassKey.ErrorText({ Error: PassKey.Errors.NotSupported }), "IconWarning");
        const user = await PassKey.GetLoggedInUser();
        if (token) {
            if (user)
                return done(_TF("A user is already signed in!\nPlease sign out before adding a passkey using a link or QR code.", "Message shown when a user is signed in while trying to add a passkey using a link or QR code"), "IconWarning");
        } else {
            if (!user)
                return done(_TF("No user is signed in!\nPlease sign in before adding a passkey.", "Message shown when no user is signed in while trying to add a passkey"), "IconWarning");
        }
        const ceremony = token ? PassKey.AttachUsingToken(token) : PassKey.Attach();
        //  Get the challenge before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
        const c = await ceremony.Fetch();
        if (c.error)
            return done(PassKey.ErrorText({ Error: c.error }), "IconWarning");
        const name = ceremony.UserName ?? user?.Username;
        if (name)
            label.innerText = _T("Add a passkey for {0}", name, "Label on the add passkey page, {0} is replaced with the user name of the account that the passkey is added to");
        text.classList.add("Info");
        text.innerText = _TF("Click the button to add a passkey on this device.", "Instruction shown on the add passkey page");

        button = AuthButton(buttons,
            _TF("Add passkey", "Text of a button that when pressed will add a passkey"),
            _TF("Click to add a passkey on this device", "Tool tip of a button that when pressed will add a passkey"),
            "IconAddPasskey", async b => {
                b.StartWorking();
                text.classList.remove("Fail");
                text.classList.add("Info");
                text.innerText = _TF("Follow the instructions of your browser or device..", "Message shown while a passkey is being added");
                try {
                    const r = await ceremony.Run();
                    if (r.Error == 0) {
                        done(_TF("Passkey added!", "Message shown when a passkey was added"), "IconAddPasskey");
                        if (token || r.User)
                            await AuthStartPage();
                        return;
                    }
                    text.innerText = PassKey.ErrorText(r);
                    if ((r.Error == PassKey.Errors.TokenExpired) || (r.Error == PassKey.Errors.AlreadyRegistered) || (r.Error == PassKey.Errors.AlreadyOnDevice)) {
                        done(PassKey.ErrorText(r), "IconWarning");
                        return;
                    }
                    if (r.Error != PassKey.Errors.Cancelled)
                        Fail(PassKey.ErrorText(r) + (r.Message ? "\n" + r.Message : ""));
                }
                catch (e) {
                    const m = _TF("Failed to add the passkey.", "Error message shown when a passkey couldn't be added") + "\n" + e;
                    text.innerText = m;
                    Fail(m);
                }
                finally {
                    b.StopWorking();
                }
                text.classList.remove("Info");
                text.classList.add("Fail");
            });
    });

}



async function usePassKeyMain() {
    await AuthPage("IconUsePasskey", async (target, img) => {

        const text = AuthText(target, "");
        const buttons = AuthButtonRow(target);

        function done(message, icon) {
            if (icon)
                img.ChangeImage(icon);
            text.classList.add("Info");
            text.innerText = message;
        }

        if (!PassKey.IsSupported())
            return done(PassKey.ErrorText({ Error: PassKey.Errors.NotSupported }), "IconWarning");
        const user = await PassKey.GetLoggedInUser();
        if (user)
            return done(_T("The user {0} is already signed in!\nPlease sign out before signing in with another user.", user.Username, "Message shown when trying to sign in with a passkey while a user is signed in, {0} is replaced with the user name"), "IconWarning");

        const ceremony = PassKey.SignIn();
        //  Get the challenge before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
        const c = await ceremony.Fetch();
        if (c.error)
            return done(PassKey.ErrorText({ Error: c.error }), "IconWarning");
        done(_TF("Click the button to sign in with a passkey, a passkey on your phone or a security key can also be used.", "Instruction shown on the sign in with passkey page"));

        const button = AuthButton(buttons,
            _TF("Sign in with passkey", "Text of a button that when pressed will sign in using a passkey"),
            _TF("Click to sign in using a passkey", "Tool tip of a button that when pressed will sign in using a passkey"),
            "IconUsePasskey", async b => {
                b.StartWorking();
                text.classList.remove("Fail");
                text.classList.add("Info");
                text.innerText = _TF("Select your passkey..", "Message shown while the user should select a passkey");
                try {
                    const r = await ceremony.Run();
                    if (r.Error == 0) {
                        button.Element.classList.add("Hide");
                        text.innerText = _TF("Signed in!", "Message shown when a user signed in successfully");
                        await AuthStartPage();
                        return;
                    }
                    text.innerText = PassKey.ErrorText(r);
                    if (r.Error != PassKey.Errors.Cancelled)
                        Fail(PassKey.ErrorText(r) + (r.Message ? "\n" + r.Message : ""));
                }
                catch (e) {
                    const m = _TF("Failed to sign in.", "Error message shown when a passkey sign in failed") + "\n" + e;
                    text.innerText = m;
                    Fail(m);
                }
                finally {
                    b.StopWorking();
                }
                text.classList.remove("Info");
                text.classList.add("Fail");
            });
    });

}


async function addPassKeyOtherMain() {
    const user = await PassKey.GetLoggedInUser();
    if (!user) {
        await AuthPage("IconWarning", async target => {
            AuthText(target, _TF("No user is signed in!\nPlease sign in before adding a passkey.", "Message shown when no user is signed in while trying to add a passkey"));
        });
        return;
    }
    let minutes = 15;
    try {
        minutes = await PassKey.Call("GetQRLifeTime", null) ?? minutes;
    }
    catch {
    }
    await AuthPage(PassKey.ApiPrefix + "GetQR.svg", async target => {
        AuthLabel(target, _T('Allow {0} to sign in on another device using a passkey, by scanning the QR code and following the link.', '<em>' + makeHtmlSafe(user.Username) + '</em>', "Instruction shown next to a QR code used to add a passkey on another device, {0} is replaced with the user name"), null, true);
        AuthText(target, _T("The QR code may only be used once and expires in {0} minutes.", minutes, "Information about a QR code used to add a passkey on another device, {0} is replaced with the number of minutes"));
    }, true);
}


async function myPassKeysMain() {
    await AuthPage("IconUsePasskey", async (target, img) => {

        const user = await PassKey.GetLoggedInUser();
        if (!user) {
            img.ChangeImage("IconWarning");
            AuthText(target, _TF("No user is signed in!\nPlease sign in to see your passkeys.", "Message shown when no user is signed in on the my passkeys page"));
            return;
        }

        AuthLabel(target, _T("Passkeys of {0}", user.Username, "Title of the my passkeys page, {0} is replaced with the user name"));
        const list = document.createElement("SysWeaver-AuthList");
        target.appendChild(list);
        const agoFormat = [null, null, _TF("{0} ago", "Text describing how long ago something happened, {0} is replaced with a time span, ex: 5 minutes")];

        const buttons = PassKey.IsSupported() ? AuthButtonRow(target) : null;
        if (buttons)
            buttons.classList.add("NoMargin");

        function stat(parent, label, value, isDate, title) {
            const e = AuthText(parent, "");
            e.classList.add("NoMargin");
            e.appendChild(document.createTextNode(label + " "));
            const s = document.createElement("span");
            e.appendChild(s);
            if (isDate)
                ValueFormat.updateDateTimeLive(s, value, agoFormat);
            else
                s.innerText = value;
            if (title)
                e.title = title;
            return e;
        }

        async function remove(key, button) {
            const name = key.Name || _TF("Passkey", "Default name of a passkey without a name");
            if (!await Confirm(
                _TF("Remove passkey", "Title of a confirmation dialog shown when a user wants to remove a passkey"),
                _T("The passkey \"{0}\" will be removed from your account, it can't be used to sign in any more.\nThe passkey isn't deleted from the device or passkey provider, you can remove it there.\n\nAre you sure?", name, "Text of a confirmation dialog shown when a user wants to remove a passkey, {0} is replaced with the name of the passkey"),
                _TF("Yes, remove it!", "Text of a button on a confirmation dialog that when pressed will remove a passkey"),
                _TF("No, keep it!", "Text of a button on a confirmation dialog that when pressed will close the dialog without removing the passkey"),
                "IconDelete", "IconOk",
                _TF("Remove the passkey from your account", "Tool tip of a button on a confirmation dialog that when pressed will remove a passkey"),
                _TF("Keep the passkey", "Tool tip of a button on a confirmation dialog that when pressed will close the dialog without removing the passkey")))
                return;
            button.StartWorking();
            try {
                const r = await PassKey.Remove(key.Id, user.Guid);
                if (r && (r.Error == 0)) {
                    await rebuild();
                    return;
                }
                Fail(r?.Error == PassKey.Errors.LastSignInMethod
                    ? _TF("This is the last way you can sign in, add a password or another passkey before removing it.", "Error message shown when trying to remove the last passkey of a user that have no password")
                    : PassKey.ErrorText(r));
            }
            catch (e) {
                Fail(_TF("Failed to remove the passkey.", "Error message shown when a passkey couldn't be removed") + "\n" + e);
            }
            finally {
                button.StopWorking();
            }
        }

        async function rename(key) {
            const oldName = key.Name ?? "";
            await PopUp(async (el, closeFn) => {
                const box = CreateAuthWrapper();
                box.classList.add("SysWeaver-PassKeyPopUp");
                el.appendChild(box);
                AuthLabel(box, _TF("Name", "Label of an input field where the user enters a new name of a passkey"));

                function newName() {
                    return AuthTrim(inp.value);
                }

                function canSave() {
                    const n = newName();
                    return (n.length > 0) && (n !== oldName);
                }

                async function save() {
                    if (!canSave())
                        return;
                    button.StartWorking();
                    inp.readOnly = true;
                    try {
                        if (await PassKey.Rename(key.Id, newName())) {
                            await closeFn();
                            await rebuild();
                            return;
                        }
                        Fail(_TF("The passkey doesn't exist any more.", "Error message shown when trying to rename a passkey that was removed"));
                    }
                    catch (e) {
                        Fail(_TF("Failed to rename the passkey.", "Error message shown when a passkey couldn't be renamed") + "\n" + e);
                    }
                    finally {
                        button.StopWorking();
                    }
                    inp.readOnly = false;
                }

                const inp = AuthInput(box,
                    _TF("Passkey name", "Place holder of an input field where the user enters a new name of a passkey"),
                    _TF("Enter a name that helps you identify the passkey, ex: the device or passkey provider", "Tool tip of an input field where the user enters a new name of a passkey"),
                    "off", () => button.SetEnabled(canSave()), save);
                inp.maxLength = 64;
                inp.value = oldName;
                const button = AuthButton(AuthButtonRow(box),
                    _TF("Rename", "Text of a button that when pressed will save the new name of a passkey"),
                    _TF("Click to save the new name of the passkey", "Tool tip of a button that when pressed will save the new name of a passkey"),
                    "IconPasskeyRename", save, true);
                inp.focus();
                inp.select();
            }, true);
        }

        async function rebuild() {
            const keys = (await PassKey.GetPassKeys()) ?? [];
            list.innerHTML = "";
            if (keys.length <= 0) {
                AuthText(list, _TF("You don't have any passkeys.", "Message shown on the my passkeys page when the user don't have any passkeys"));
            }
            //  Browsers doesn't tell what passkeys are on the device, a passkey is known to be on this device if it was created here, created or used in this browser or if the browser refused to add another one
            const onDevice = key => key.ThisDevice || PassKey.IsOnDevice(key.Id);
            const anyOnDevice = keys.some(onDevice) || PassKey.IsUserOnDevice(user.Guid);

            let first = true;

            //for (let i = 0; i < 5; ++ i)
            for (const key of keys) {
                if (!first)
                    AuthHr(list).classList.add("NoMargin");
                first = false;
                const name = key.Name || _TF("Passkey", "Default name of a passkey without a name");
                AuthLabel(list, onDevice(key)
                    ? _T("{0} (this device)", name, "Name of a passkey that is available on the current device, {0} is replaced with the name of the passkey")
                    : name).classList.add("Highlight");
                stat(list, _TF("Created:", "Label of the time when a passkey was created"), key.Created, true);
                stat(list, _TF("Last used:", "Label of the time when a passkey was last used to sign in"), key.LastUsed, true);
                stat(list,
                    _TF("Type:", "Label of the type of a passkey (synced or device bound)"),
                    key.Synced
                        ? _TF("Synced", "Type of a passkey that is synced between the users devices by the passkey provider")
                        : _TF("Device bound", "Type of a passkey that only exists on one device or security key"),
                    false,
                    key.Synced
                        ? _TF("The passkey is synced by your passkey provider and can be used on your other devices", "Tool tip of a synced passkey")
                        : _TF("The passkey only exists on one device or security key", "Tool tip of a device bound passkey"));
                const row = AuthButtonRow(list);
                AuthButton(row,
                    _TF("Rename", "Text of a button that when pressed will rename a passkey"),
                    _TF("Click to change the name of this passkey", "Tool tip of a button that when pressed will rename a passkey"),
                    "IconPasskeyRename", async () => await rename(key));
                AuthButton(row,
                    _TF("Remove", "Text of a button that when pressed will remove a passkey"),
                    _TF("Click to remove this passkey from your account", "Tool tip of a button that when pressed will remove a passkey"),
                    "IconDelete", async button => await remove(key, button));
            }

            if (buttons) {
                buttons.innerText = "";
                const row = buttons;
                //  The add button is placed on the same row as the buttons of the last passkey
                const add = anyOnDevice ? null : PassKey.Attach();
                //  Get the challenge before the user clicks, so that the browser prompt is opened directly by the click (required by some browsers)
                add?.Fetch().catch(() => { });
                const addButton = AuthButton(row,
                    _TF("Add passkey", "Text of a button that when pressed will add a passkey"),
                    anyOnDevice
                        ? _TF("A passkey for your account is already available on this device", "Tool tip of a disabled button that would add a passkey, when there already is a passkey on the device")
                        : _TF("Click to add a passkey on this device", "Tool tip of a button that when pressed will add a passkey"),
                    "IconAddPasskey", async button => {
                        button.StartWorking();
                        try {
                            const r = await add.Run();
                            if (r.Error == 0) {
                                await rebuild();
                                return;
                            }
                            if ((r.Error == PassKey.Errors.AlreadyOnDevice) || (r.Error == PassKey.Errors.AlreadyRegistered)) {
                                //  Remember it, so that the button is disabled the next time
                                PassKey.SetUserOnDevice(user.Guid);
                                Fail(PassKey.ErrorText(r));
                                await rebuild();
                                return;
                            }
                            if (r.Error != PassKey.Errors.Cancelled)
                                Fail(PassKey.ErrorText(r) + (r.Message ? "\n" + r.Message : ""));
                            add.Fetch().catch(() => { });
                        }
                        catch (e) {
                            Fail(_TF("Failed to add the passkey.", "Error message shown when a passkey couldn't be added") + "\n" + e);
                        }
                        finally {
                            button.StopWorking();
                        }
                    }, anyOnDevice);
                //  Disabled buttons have no tool tip, explain why it's disabled
                if (anyOnDevice)
                    addButton.Element.title = _TF("A passkey for your account is already available on this device", "Tool tip of a disabled button that would add a passkey, when there already is a passkey on the device");
            }





        }

        await rebuild();
    });
}
