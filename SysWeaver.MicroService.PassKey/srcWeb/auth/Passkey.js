/**
 * @file Passkey.js - the UI-less WebAuthn (passkey) client of SysWeaver.MicroService.PassKey (served as auth/Passkey.js).
 *
 * Purpose: ceremonies against PassKeyService (Api/auth/passkey/*) with navigator.credentials.get / create:
 *  - class PassKeyChallenge: one pre-fetchable ceremony (challenge api + submit api). Fetch() early (the challenge is
 *    cached for publicKey.timeout - 15 s, else 60 s), then Run() from a click so the browser prompt opens directly in
 *    the user gesture (Safari). A challenge is single use; Fetch() again for a retry.
 *  - class PassKey (static): support detection (IsSupported, IsAvailable: the service runs), server error codes 0..11 (+ client side Cancelled -1, AlreadyOnDevice -2)
 *    and their translated texts, Get / Create wrappers (base64 <-> ArrayBuffer, browser error mapping,
 *    signalUnknownCredential), conditional mediation autofill (Autofill), device hints in localStorage, factories
 *    SignIn / Attach / AttachUsingToken / NewAccount, GetPassKeys / Rename / Remove, GetLoggedInUser.
 *  - Sw.PassKey = PassKey, Sw.PassKeyChallenge = PassKeyChallenge.
 *
 * Loading: classic `defer` script after ../sw/core.js (or common/common.js) and Auth.js. An optional plug-in of the
 * Login (MicroService.Auth), ChoosePassword / ResetPassword (MicroService.UserManager) pages: they test
 * `typeof PassKey`. The passkey pages load PasskeyUi.js after it.
 *
 * Globals: PassKey, PassKeyChallenge (classes, also on window and Sw).
 * Server: POST Api/auth/passkey/{GetAuthChallenge, GetUserAuthChallenge, GetCreateChallenge, GetResetChallenge,
 * GetNewChallenge, Auth, Create, New, GetPassKeys, RenamePassKey, DeletePassKey, GetQRLifeTime}, Api/auth/GetUser.
 * DTOs use lowercase WebAuthn names, byte[] as standard base64.
 * Storage: sessionStorage "SysWeaver.User" (AuthInfo after a ceremony that signed in), localStorage
 * "SysWeaver.PassKey.Hint" ("1" once a platform passkey was created / used here), "SysWeaver.PassKey.Local" (JSON array,
 * max 64, base64url credential ids created / used here), "SysWeaver.PassKey.UserOnDevice" (JSON array of user guids the
 * browser already holds a passkey for).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Net)
        throw new Error("auth/Passkey.js requires sw/core.js (or common/common.js)");

    /** localStorage keys. */
    const HintKey = "SysWeaver.PassKey.Hint";
    const LocalKey = "SysWeaver.PassKey.Local";
    const UserOnDeviceKey = "SysWeaver.PassKey.UserOnDevice";

    /**
     * Read a JSON array from localStorage.
     * @param {string} key The key.
     * @returns {string[]} The list ([] when missing or invalid).
     */
    function getList(key) {
        const v = Sw.Storage.Local.GetJson(key, []);
        return Array.isArray(v) ? v : [];
    }

    /**
     * Write a list (at most the 64 newest entries) as JSON to localStorage.
     * @param {string} key The key.
     * @param {string[]} list Entries, oldest first.
     */
    function setList(key, list) {
        Sw.Storage.Local.SetJson(key, list.slice(-64));
    }

    /**
     * Standard base64 (server) to base64url without padding (browser credential ids).
     * @param {string} s Base64.
     * @returns {string} Base64url.
     */
    function toBase64Url(s) {
        return (s ?? "").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    /**
     * Bytes (ArrayBuffer / view) to standard base64, null stays null.
     * @param {ArrayBuffer|ArrayBufferView|null} b The bytes.
     * @returns {string|null}
     */
    function b64(b) {
        return b ? Sw.Util.Base64Encode(b instanceof ArrayBuffer ? new Uint8Array(b) : new Uint8Array(b.buffer, b.byteOffset, b.byteLength)) : null;
    }

    /**
     * A server challenge fetched before the user starts the ceremony (navigator.credentials is then called directly from
     * the user gesture, as Safari requires).
     */
    class PassKeyChallenge {
        /**
         * @param {string} challengeName Api returning the WebAuthn options (ex: "GetAuthChallenge").
         * @param {*} arg Argument of the challenge api (user id, token or null).
         * @param {string} submitName Api the browser response is posted to ("Auth", "Create", "New").
         * @param {boolean} isCreate True for a registration (create), false for an assertion (get).
         */
        constructor(challengeName, arg, submitName, isCreate) {
            this.ChallengeName = challengeName;
            this.Arg = arg;
            this.SubmitName = submitName;
            this.IsCreate = isCreate;
            /** @type {object|null} Cached options ({challengeId, publicKey, error?}). */
            this.Options = null;
            /** @type {number} When Options were fetched (Date.now()). */
            this.Fetched = 0;
        }

        /**
         * Fetch a challenge unless a fresh one is cached.
         * @returns {Promise<object>} The options (check the error member).
         */
        async Fetch() {
            const o = this.Options;
            if (o && !o.error && (Date.now() - this.Fetched) < PassKey.MaxAge(o))
                return o;
            this.Options = null;
            const c = await PassKey.Call(this.ChallengeName, this.Arg);
            if (!c)
                throw new Error(_TF("Failed to get a challenge from the service", "Error message shown when a passkey challenge couldn't be fetched from the service"));
            Sw.Util.RemoveNulls(c);
            this.Options = c;
            this.Fetched = Date.now();
            return c;
        }

        /**
         * Run the ceremony (fetches a challenge if needed; consumes the cached one).
         * @returns {Promise<object>} {Error (0 = success, see PassKey.Errors), User?, Message?}.
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
         * The user name of a registration (after Fetch, before Run), or null.
         * @returns {string|null}
         */
        get UserName() {
            return this.Options?.publicKey?.user?.name ?? null;
        }
    }

    /**
     * Static passkey helpers (see the file header).
     */
    class PassKey {
        /**
         * The api prefix.
         * @returns {string} "<root>Api/auth/passkey/".
         */
        static get ApiPrefix() {
            return Sw.Url.Root + "Api/auth/passkey/";
        }

        /**
         * Usable age of a challenge.
         * @param {object} c The options.
         * @returns {number} Milliseconds (publicKey.timeout - 15 s, or 60 s when missing / <= 20 s).
         */
        static MaxAge(c) {
            const t = c?.publicKey?.timeout;
            return (t && t > 20000) ? (t - 15000) : 60000;
        }

        /**
         * True if passkeys can be used (PublicKeyCredential, navigator.credentials and a secure context).
         * @returns {boolean}
         */
        static IsSupported() {
            return (!!window.PublicKeyCredential) && (!!navigator.credentials) && window.isSecureContext;
        }

        /**
         * True if passkeys can be used here: supported by the browser (IsSupported). This script is only loaded where the
         * PassKeyService runs (Sw.AuthUi.LoadPassKeys asks Api/HaveService first, and the passkey pages belong to the service), so
         * no other request is made: as before the pages fetch their challenge directly, and a failing challenge hides the passkey
         * button.
         * @param {boolean} [refresh] Ignored (kept for the signature).
         * @returns {Promise<boolean>}
         */
        static IsAvailable(refresh) {
            void refresh;
            return Promise.resolve(PassKey.IsSupported());
        }

        /**
         * A user friendly text for a result.
         * @param {object} result The result ({Error}).
         * @returns {string} The translated text ("" for None).
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

        /**
         * Call a PassKeyService api (POST json, no cache, a redirect throws).
         * @param {string} name Method name, ex: "GetAuthChallenge".
         * @param {*} arg Argument (may be null).
         * @returns {Promise<*>} The response.
         */
        static async Call(name, arg) {
            return await Sw.Net.Send(PassKey.ApiPrefix + name, arg ?? null, { Post: true, Reload: true, NoNavigate: true });
        }

        /**
         * The signed in user (GET Api/auth/GetUser, as the old Passkey.js getRequest).
         * @returns {Promise<object|null>} AuthInfo when signed in, else null.
         */
        static async GetLoggedInUser() {
            const u = await Sw.Net.Get(Sw.Url.Root + "Api/auth/GetUser", { Reload: true, NoNavigate: true });
            return u && u.Succeeded ? u : null;
        }

        /**
         * Store the user of a successful result in sessionStorage "SysWeaver.User" and note the sign in for the sign in page
         * (Sw.Auth.NoteSignedIn, when auth/Auth.js is loaded).
         * @param {object} r The result.
         */
        static SetUser(r) {
            if (r && r.Error == 0 && r.User) {
                Sw.Storage.Session.SetJson("SysWeaver.User", r.User);
                Sw.Auth?.NoteSignedIn?.(true);
            }
        }

        /**
         * True if a passkey was created or used in this browser before (only a hint).
         * @returns {boolean}
         */
        static HaveHint() {
            return !!Sw.Storage.Local.Get(HintKey);
        }

        /**
         * Remember a platform passkey on this device (nothing for cross-platform authenticators).
         * @param {string} attachment The authenticatorAttachment.
         * @param {string} credentialId The credential id (base64url).
         */
        static SetHint(attachment, credentialId) {
            if (attachment === "cross-platform")
                return;
            Sw.Storage.Local.Set(HintKey, "1");
            if (credentialId) {
                const ids = getList(LocalKey).filter(x => x !== credentialId);
                ids.push(credentialId);
                setList(LocalKey, ids);
            }
        }

        /**
         * True if the passkey was created or used on this device.
         * @param {string} id Credential id (standard base64, PassKeyInfo.Id).
         * @returns {boolean}
         */
        static IsOnDevice(id) {
            return getList(LocalKey).includes(toBase64Url(id));
        }

        /**
         * True if the browser reported a passkey of the user on this device.
         * @param {string} userGuid User guid.
         * @returns {boolean}
         */
        static IsUserOnDevice(userGuid) {
            return !!userGuid && getList(UserOnDeviceKey).includes(userGuid);
        }

        /**
         * Remember that the browser holds a passkey of the user (it refused to add another one).
         * @param {string} userGuid User guid.
         */
        static SetUserOnDevice(userGuid) {
            if (!userGuid)
                return;
            const list = getList(UserOnDeviceKey).filter(x => x !== userGuid);
            list.push(userGuid);
            setList(UserOnDeviceKey, list);
        }

        /**
         * Forget the device hints of a removed passkey (and the user's "on device" flag).
         * @param {string} id Credential id (standard base64).
         * @param {string} [userGuid] User guid.
         */
        static ForgetOnDevice(id, userGuid) {
            const u = toBase64Url(id);
            setList(LocalKey, getList(LocalKey).filter(x => x !== u));
            if (userGuid)
                setList(UserOnDeviceKey, getList(UserOnDeviceKey).filter(x => x !== userGuid));
        }

        /**
         * True if passkey autofill (conditional mediation) is available.
         * @returns {Promise<boolean>}
         */
        static async IsAutofillSupported() {
            try {
                return PassKey.IsSupported() && !!PublicKeyCredential.isConditionalMediationAvailable && await PublicKeyCredential.isConditionalMediationAvailable();
            }
            catch {
                return false;
            }
        }

        /**
         * Start passkey autofill: the site's passkeys are offered in the autofill list of inputs with
         * autocomplete="username webauthn". The request is restarted before the challenge expires and after failures
         * (1 s pause); a cancelled request restarts at once. Only one WebAuthn request may run: Stop() before another
         * request, Start() afterwards.
         * @param {function(object):*} onResult Called with the result when a passkey was picked (Error 0 = signed in).
         * @returns {{Start:function():void, Stop:function():void}} The controller (already started).
         */
        static Autofill(onResult) {
            const state = { Run: 0, Abort: null };
            /**
             * One loop instance.
             * @param {number} runId Id of this loop (superseded when state.Run changes).
             * @returns {Promise<void>}
             */
            async function run(runId) {
                while (state.Run === runId) {
                    let c;
                    try {
                        c = await PassKey.Call("GetAuthChallenge", null);
                    }
                    catch {
                        return;
                    }
                    if (!c || c.error || state.Run !== runId)
                        return;
                    Sw.Util.RemoveNulls(c);
                    const ac = new AbortController();
                    state.Abort = ac;
                    const refresh = setTimeout(() => ac.abort(), PassKey.MaxAge(c));
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
                    await Sw.Util.Delay(1000);
                }
            }
            const ctl = {
                /** (Re)start the loop. */
                Start() {
                    run(++state.Run);
                },
                /** Stop the loop and abort the pending request. */
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

        /**
         * Map a navigator.credentials exception to a result (others are re-thrown).
         * @param {Error} e The exception.
         * @returns {{Error:number, Message?:string}}
         */
        static FromBrowserError(e) {
            const n = e?.name;
            if (n === "NotAllowedError" || n === "AbortError")
                return { Error: PassKey.Errors.Cancelled };
            if (n === "InvalidStateError")
                return { Error: PassKey.Errors.AlreadyOnDevice };
            if (n === "SecurityError")
                return { Error: PassKey.Errors.NotSupported, Message: "" + e };
            throw e;
        }

        /**
         * Decode the base64 ids of credential descriptors in place.
         * @param {Array<{id:string}>} [list] The descriptors.
         */
        static DecodeIds(list) {
            if (list)
                list.forEach(x => x.id = Sw.Util.Base64Decode(x.id));
        }

        /**
         * Run navigator.credentials.get with server options and submit the assertion.
         * @param {object} c The options ({challengeId, publicKey}); decoded in place.
         * @param {string} submitName Submit api.
         * @param {object} [extra] More get() options, ex: {mediation: "conditional", signal}.
         * @returns {Promise<object>} The result.
         */
        static async Get(c, submitName, extra) {
            const pk = c.publicKey;
            pk.challenge = Sw.Util.Base64Decode(pk.challenge);
            PassKey.DecodeIds(pk.allowCredentials);
            let cc;
            try {
                cc = await navigator.credentials.get(Object.assign({ publicKey: pk }, extra));
            }
            catch (e) {
                return PassKey.FromBrowserError(e);
            }
            if (!cc)
                return { Error: PassKey.Errors.Cancelled };
            const res = cc.response;
            const r = await PassKey.Call(submitName, {
                challengeId: c.challengeId,
                authenticatorAttachment: cc.authenticatorAttachment,
                id: cc.id,
                rawId: b64(cc.rawId),
                response: res ? {
                    authenticatorData: b64(res.authenticatorData),
                    clientDataJSON: b64(res.clientDataJSON),
                    signature: b64(res.signature),
                    userHandle: b64(res.userHandle),
                } : null,
                type: cc.type,
            });
            if (!r)
                throw new Error(_TF("No response from the service", "Error message shown when the service didn't respond to a passkey request"));
            if (r.Error == PassKey.Errors.UnknownCredential && PublicKeyCredential.signalUnknownCredential) {
                //  Let the passkey provider remove (or hide) the passkey
                try {
                    await PublicKeyCredential.signalUnknownCredential({ rpId: pk.rpId, credentialId: cc.id });
                }
                catch {
                }
            }
            if (r.Error == 0)
                PassKey.SetHint(cc.authenticatorAttachment, cc.id);
            PassKey.SetUser(r);
            return r;
        }

        /**
         * Run navigator.credentials.create with server options and submit the attestation.
         * @param {object} c The options ({challengeId, publicKey}); decoded in place.
         * @param {string} submitName Submit api ("Create" or "New").
         * @returns {Promise<object>} The result.
         */
        static async Create(c, submitName) {
            const pk = c.publicKey;
            pk.challenge = Sw.Util.Base64Decode(pk.challenge);
            pk.user.id = Sw.Util.Base64Decode(pk.user.id);
            PassKey.DecodeIds(pk.excludeCredentials);
            let cc;
            try {
                cc = await navigator.credentials.create({ publicKey: pk });
            }
            catch (e) {
                return PassKey.FromBrowserError(e);
            }
            if (!cc)
                return { Error: PassKey.Errors.Cancelled };
            const res = cc.response;
            let transports = null;
            try {
                transports = res && res.getTransports ? res.getTransports() : null;
            }
            catch {
            }
            const r = await PassKey.Call(submitName, {
                challengeId: c.challengeId,
                authenticatorAttachment: cc.authenticatorAttachment,
                id: cc.id,
                rawId: b64(cc.rawId),
                response: res ? {
                    attestationObject: b64(res.attestationObject),
                    clientDataJSON: b64(res.clientDataJSON),
                    transports,
                } : null,
                type: cc.type,
            });
            if (!r)
                throw new Error(_TF("No response from the service", "Error message shown when the service didn't respond to a passkey request"));
            if (r.Error == 0)
                PassKey.SetHint(cc.authenticatorAttachment, cc.id);
            PassKey.SetUser(r);
            return r;
        }

        /**
         * Sign in with a passkey.
         * @param {string} [userIdentifier] Only passkeys of this user (else any passkey of the site).
         * @returns {PassKeyChallenge}
         */
        static SignIn(userIdentifier) {
            return userIdentifier
                ? new PassKeyChallenge("GetUserAuthChallenge", userIdentifier, "Auth", false)
                : new PassKeyChallenge("GetAuthChallenge", null, "Auth", false);
        }

        /**
         * Add a passkey to the signed in user.
         * @returns {PassKeyChallenge}
         */
        static Attach() {
            return new PassKeyChallenge("GetCreateChallenge", null, "Create", true);
        }

        /**
         * Add a passkey to the user of a token (QR code, reset / add password link) and sign in.
         * @param {string} token The token.
         * @returns {PassKeyChallenge}
         */
        static AttachUsingToken(token) {
            return new PassKeyChallenge("GetResetChallenge", token, "New", true);
        }

        /**
         * Create a new account with a passkey from a sign up token and sign in.
         * @param {string} token The token.
         * @returns {PassKeyChallenge}
         */
        static NewAccount(token) {
            return new PassKeyChallenge("GetNewChallenge", token, "New", true);
        }

        /**
         * The passkeys of the signed in user.
         * @returns {Promise<object[]>} PassKeyInfo: Id, Name, Created, LastUsed, Synced, ThisDevice.
         */
        static async GetPassKeys() {
            return await PassKey.Call("GetPassKeys", null);
        }

        /**
         * Rename a passkey.
         * @param {string} id Credential id.
         * @param {string} name New name.
         * @returns {Promise<boolean>} Falsy if the passkey doesn't exist (any more).
         */
        static async Rename(id, name) {
            return await PassKey.Call("RenamePassKey", { Id: id, Name: name });
        }

        /**
         * Remove a passkey (forgets its device hints on success).
         * @param {string} id Credential id.
         * @param {string} [userGuid] Guid of the signed in user.
         * @returns {Promise<object>} The result (Error LastSignInMethod when it can't be removed).
         */
        static async Remove(id, userGuid) {
            const r = await PassKey.Call("DeletePassKey", id);
            if (r && r.Error == 0)
                PassKey.ForgetOnDevice(id, userGuid);
            return r;
        }
    }

    /** The error codes (server PassKeyErrors; Cancelled and AlreadyOnDevice are client side). */
    PassKey.Errors = Object.freeze({
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
    });

    window.PassKey = PassKey;
    window.PassKeyChallenge = PassKeyChallenge;
    Sw.PassKey = PassKey;
    Sw.PassKeyChallenge = PassKeyChallenge;
})();
