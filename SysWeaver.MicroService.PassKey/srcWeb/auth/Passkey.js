

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
     */
    static async Get(c, submitName) {
        const pk = c.publicKey;
        pk.challenge = base64ToArray(pk.challenge);
        PassKey.#Ids(pk.allowCredentials);
        let cc;
        try {
            cc = await navigator.credentials.get({ publicKey: pk });
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
     * @returns {object} The result (Error = LastSignInMethod if it can't be removed)
     */
    static async Remove(id) {
        return await PassKey.Call("DeletePassKey", id);
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
