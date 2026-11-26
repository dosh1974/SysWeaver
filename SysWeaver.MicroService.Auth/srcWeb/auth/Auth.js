/**
 * @file Auth.js - the UI-less authentication library (SysWeaver.MicroService.Auth, served as auth/Auth.js).
 *
 * Purpose: `Sw.Auth`, everything the auth pages (and any other page or project) need without a user interface:
 *  - Login with the LoginService hashed protocol (GetUserSalt -> [salt, oneTimePad], hash = b64(sha256(pw|salt)) or,
 *    for a "|" prefixed binary salt, b64(sha256(saltBytes ++ utf8(pw))), proof = b64(sha256(hash|pad)),
 *    Login {OneTimePad, Hash}). No automatic Basic fallback; BasicLogin (explicit only: basic_auth must answer truthy, then GET login with
 *    Authorization: Basic).
 *    When GetUserSalt answers [salt, pad, "s1", scramSalt, iterations, binding] the SCRAM login is used instead:
 *    PBKDF2-SHA256(utf8(hash|binding), scramSalt, iterations) -> ClientKey -> StoredKey, Login {OneTimePad, ClientNonce, ClientProof}
 *    (see ScramInfo in SysWeaver.Auth.Core/Scram.cs). PBKDF2 uses crypto.subtle, with a pure JS fallback on plain HTTP.
 *  - Logout (GET logout with "Authorization: Basic logout", overwriting cached Basic credentials).
 *  - Password proof objects for sensitive UserManager calls ({Hash, OneTimePad}), new password hashes.
 *  - Password policy text, validation and a crypto random password generator (look-alike free alphabets).
 *  - Password leak check (haveibeenpwned k-anonymity range api, only 5 hex chars of the SHA-1 leave the browser); a
 *    deployment turns it off with `:root{--sw-password-leak-check:off}` in app/app.css (see LeakCheckEnabled).
 *  - E-mail / phone validation and phone prefix lookups (AuthManagerService).
 *  - What a user id can be on this site (UserIdKinds from Api/auth/UserManagerProps): the tool tip (UserIdHint) and the label
 *    (UserIdLabel, ex: "Email or user name") of a user id box.
 *  - Short codes (the digits sent by e-mail / text message, validated against the HTTP session).
 *  - Token urls: the keyless "Page.html?<token>" form (the whole query is the token) and the keyed forms, passed whole.
 *  - The post sign in navigation (StartPage): reloads the top window when embedded in the app shell (never nests the
 *    shell in its own iframe), else navigates to the site root.
 *
 * Loading: a classic `defer` script after ../sw/core.js (or the legacy ../common/common.js, which contains the core; or, for a page that
 * renders its own UI, after the lean pair ../sw/session.js + ../sw/net.js: messaging and requests without theme or UI),
 * before AuthUi.js, UserManager.js, Passkey.js, SimpleAuth.js.
 *
 * Globals: `Sw.Auth`.
 *
 * Server: Api/auth/GetUserSalt, Login, GetUser, GetPasswordPolicy, ValidateEmailAddress, GetPhoneNumberInfo,
 * GetPhonePrefixInfo, ValidateShortCode; logout, login, basic_auth (site root); https://api.pwnedpasswords.com.
 * Storage: sessionStorage["SysWeaver.User"] (JSON of the last AuthInfo of a successful sign in; write only), localStorage
 * "SysWeaver.SignedInHint" (json {siteRoot: 1} while a user may be signed in on that site: NoteSignedIn / MaybeSignedIn).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Net || !Sw.Net.Send)
        throw new Error("auth/Auth.js requires sw/core.js (or common/common.js, or sw/session.js + sw/net.js) to be loaded first");

    /**
     * The auth api prefix.
     * @returns {string} "<root>Api/auth/".
     */
    const api = () => Sw.Url.Root + "Api/auth/";

    /** Request options of every auth call: always POST (no secrets in urls / logs), no cache, never navigate on a redirect. */
    const post = { Post: true, Reload: true, NoNavigate: true };

    /** Site name of the leak check (shown in texts). */
    const LeakDb = "https://haveibeenpwned.com";

    /**
     * Trim a value (null / undefined give null, anything else a trimmed string).
     * @param {*} value The value.
     * @returns {string|null} The trimmed text.
     */
    function Trim(value) {
        if (value === null || value === undefined)
            return null;
        return ("" + value).trim();
    }

    /**
     * localStorage key of the "a user may be signed in in this browser" hint (see MaybeSignedIn): a json map {siteRoot: 1}, so
     * the sites behind one origin (a reverse proxy) each have their own.
     */
    const SignedInHintKey = "SysWeaver.SignedInHint";

    /**
     * Note whether a user is signed in on this site in this browser (localStorage "SysWeaver.SignedInHint", shared by the tabs).
     * Set by every sign in (SetUser with a user: password, user manager, passkey) and by a GetUser that found a user; removed by
     * a sign out and by a GetUser that found nobody. A failed sign in leaves it as it is.
     * @param {boolean} on A user is signed in.
     */
    function NoteSignedIn(on) {
        const m = Sw.Storage.Local.GetJson(SignedInHintKey, null);
        const map = (m && typeof m === "object") ? m : {};
        if (!!map[Sw.Url.Root] === !!on)
            return;
        if (on)
            map[Sw.Url.Root] = 1;
        else
            delete map[Sw.Url.Root];
        if (Object.keys(map).length)
            Sw.Storage.Local.SetJson(SignedInHintKey, map);
        else
            Sw.Storage.Local.Remove(SignedInHintKey);
    }

    /**
     * True when a user may be signed in on this site in this browser (the hint of NoteSignedIn). The session cookie is HttpOnly,
     * so this is how the sign in page knows whether asking the server (POST Api/auth/GetUser, for "You are already signed in") is
     * worth a request: an anonymous visitor sends only the requests of the original page. A stale hint (a session that ended)
     * costs one GetUser, which then removes it; a browser without storage never asks (the sign in form shows, as the original).
     * @returns {boolean} A user may be signed in.
     */
    function MaybeSignedIn() {
        const m = Sw.Storage.Local.GetJson(SignedInHintKey, null);
        return !!(m && typeof m === "object" && m[Sw.Url.Root]);
    }

    /**
     * Remember (or forget) the signed in user in sessionStorage["SysWeaver.User"]. A user also sets the signed in hint (NoteSignedIn).
     * @param {object|null} info The AuthInfo (falsy removes it).
     */
    function SetUser(info) {
        Sw.Storage.Session.Set("SysWeaver.User", info ? JSON.stringify(info) : null);
        if (info)
            NoteSignedIn(true);
    }

    /**
     * Hash a password with a salt: b64(sha256(utf8(password + "|" + salt))), or for a binary salt ("|" + base64)
     * b64(sha256(saltBytes ++ utf8(password))).
     * @param {string} password The (trimmed) password.
     * @param {string} salt The salt.
     * @returns {Promise<string>} Base64 hash.
     */
    async function PasswordHash(password, salt) {
        salt = salt ?? "";
        password = password ?? "";
        if (salt.charAt(0) === "|") {
            const bin = Sw.Util.ConcatBytes([Sw.Util.Base64Decode(salt.substring(1)), Sw.Util.Utf8Encode(password)]);
            return await Sw.Util.Sha256B64(bin);
        }
        return await Sw.Util.Sha256B64(password + "|" + salt);
    }

    /**
     * Get the salt and a one time pad (valid 10 s, single use) for a user.
     * @param {string} user User id (name, e-mail, phone).
     * @returns {Promise<string[]>} [salt, oneTimePad].
     */
    async function SaltPad(user) {
        return await Sw.Net.Send(api() + "GetUserSalt", Trim(user), post);
    }

    // ---- SCRAM (RFC 5802 / 7677 style), see ScramInfo in SysWeaver.Auth.Core/Scram.cs ----
    //  SaltedPassword = PBKDF2-SHA256(utf8(H + "|" + binding), salt, iterations), ClientKey = HMAC(SaltedPassword, "Client Key"),
    //  StoredKey = SHA256(ClientKey), ClientProof = ClientKey XOR HMAC(StoredKey, utf8(user + "," + pad + "," + clientNonce)).
    //  PBKDF2 uses crypto.subtle when available, else the pure JS implementation below (plain HTTP on a LAN).

    const K256 = Int32Array.of(
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2);
    const IV256 = Int32Array.of(0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19);

    /** One SHA-256 block: out = compress(h, w[0..15]) (w is used as the 64 word schedule). */
    function compress256(h, w, out) {
        for (let i = 16; i < 64; ++i) {
            const x = w[i - 15], y = w[i - 2];
            const s0 = ((x >>> 7) | (x << 25)) ^ ((x >>> 18) | (x << 14)) ^ (x >>> 3);
            const s1 = ((y >>> 17) | (y << 15)) ^ ((y >>> 19) | (y << 13)) ^ (y >>> 10);
            w[i] = (w[i - 16] + s0 + w[i - 7] + s1) | 0;
        }
        let a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], k = h[7];
        for (let i = 0; i < 64; ++i) {
            const S1 = ((e >>> 6) | (e << 26)) ^ ((e >>> 11) | (e << 21)) ^ ((e >>> 25) | (e << 7));
            const t1 = (k + S1 + ((e & f) ^ (~e & g)) + K256[i] + w[i]) | 0;
            const S0 = ((a >>> 2) | (a << 30)) ^ ((a >>> 13) | (a << 19)) ^ ((a >>> 22) | (a << 10));
            const t2 = (S0 + ((a & b) ^ (a & c) ^ (b & c))) | 0;
            k = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
        }
        out[0] = (h[0] + a) | 0; out[1] = (h[1] + b) | 0; out[2] = (h[2] + c) | 0; out[3] = (h[3] + d) | 0;
        out[4] = (h[4] + e) | 0; out[5] = (h[5] + f) | 0; out[6] = (h[6] + g) | 0; out[7] = (h[7] + k) | 0;
    }

    /** Big endian bytes to words (count words). */
    function toWords(bytes, w, count) {
        for (let i = 0; i < count; ++i)
            w[i] = (bytes[i * 4] << 24) | (bytes[i * 4 + 1] << 16) | (bytes[i * 4 + 2] << 8) | bytes[i * 4 + 3];
    }

    /** Words to big endian bytes. */
    function toBytes(w) {
        const r = new Uint8Array(w.length * 4);
        for (let i = 0; i < w.length; ++i) {
            const v = w[i];
            r[i * 4] = v >>> 24; r[i * 4 + 1] = (v >>> 16) & 255; r[i * 4 + 2] = (v >>> 8) & 255; r[i * 4 + 3] = v & 255;
        }
        return r;
    }

    /** SHA-256 of bytes (synchronous). */
    const sha256 = data => Sw.Util.Sha256Sync(data);

    /** HMAC-SHA256 (synchronous). */
    function hmac256(key, msg) {
        if (key.length > 64)
            key = sha256(key);
        const i = new Uint8Array(64 + msg.length);
        const o = new Uint8Array(64 + 32);
        for (let n = 0; n < 64; ++n) {
            const k = n < key.length ? key[n] : 0;
            i[n] = k ^ 0x36;
            o[n] = k ^ 0x5c;
        }
        i.set(msg, 64);
        o.set(sha256(i), 64);
        return sha256(o);
    }

    /** PBKDF2-HMAC-SHA256 with a 32 byte output (pure JS, the iteration loop uses precomputed HMAC states, 2 blocks per iteration). */
    function pbkdf2Js(pwd, salt, iterations) {
        const key = pwd.length > 64 ? sha256(pwd) : pwd;
        const w = new Int32Array(64);
        const istate = new Int32Array(8), ostate = new Int32Array(8);
        const pad = new Uint8Array(64);
        for (let n = 0; n < 64; ++n)
            pad[n] = (n < key.length ? key[n] : 0) ^ 0x36;
        toWords(pad, w, 16);
        compress256(IV256, w, istate);
        for (let n = 0; n < 64; ++n)
            pad[n] ^= 0x36 ^ 0x5c;
        toWords(pad, w, 16);
        compress256(IV256, w, ostate);
        const first = new Uint8Array(salt.length + 4);
        first.set(salt);
        first[salt.length + 3] = 1;
        const u = new Int32Array(8), tmp = new Int32Array(8), t = new Int32Array(8);
        toWords(hmac256(key, first), u, 8);
        t.set(u);
        //  The 32 byte message after the 64 byte key block: 0x80 padding and a bit length of 768
        w.fill(0, 8, 16);
        w[8] = 0x80000000 | 0;
        w[15] = 768;
        for (let i = 1; i < iterations; ++i) {
            w.set(u, 0);
            compress256(istate, w, tmp);
            w.set(tmp, 0);
            compress256(ostate, w, u);
            for (let j = 0; j < 8; ++j)
                t[j] ^= u[j];
        }
        return toBytes(t);
    }

    /** PBKDF2-HMAC-SHA256 (32 bytes), crypto.subtle when available. */
    async function pbkdf2(pwd, salt, iterations) {
        try {
            const s = crypto.subtle;
            const k = await s.importKey("raw", pwd, "PBKDF2", false, ["deriveBits"]);
            return new Uint8Array(await s.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: salt, iterations: iterations }, k, 256));
        }
        catch {
            return pbkdf2Js(pwd, salt, iterations);
        }
    }

    /** A random base64 nonce (16 bytes). */
    function Nonce() {
        const b = new Uint8Array(16);
        crypto.getRandomValues(b);
        return Sw.Util.Base64Encode(b);
    }

    /**
     * True if a GetUserSalt answer asks for the SCRAM login: [salt, pad, "s1", scramSalt, iterations, binding].
     * @param {string[]} sp The answer.
     * @returns {boolean} SCRAM is used.
     */
    const IsScram = sp => !!sp && sp.length >= 6 && sp[2] === "s1";

    /**
     * Build the SCRAM proof.
     * @param {string} user The user id (exactly as sent to GetUserSalt).
     * @param {string} hash The legacy password hash (PasswordHash(password, sp[0])).
     * @param {string[]} sp The GetUserSalt answer (SCRAM shaped, see IsScram).
     * @returns {Promise<{ClientProof:string, ClientNonce:string, OneTimePad:string}>} The proof.
     */
    async function ScramProof(user, hash, sp) {
        const u = Sw.Util.Utf8Encode;
        const iterations = parseInt(sp[4], 10) || 0;
        //  Never fewer iterations than the server minimum (a fake server could ask for a cheap proof, to brute force it offline)
        if (iterations < 4096)
            throw new Error("Invalid sign in parameters");
        const salted = await pbkdf2(u(hash + "|" + (sp[5] || "")), Sw.Util.Base64Decode(sp[3]), iterations);
        const clientKey = hmac256(salted, u("Client Key"));
        const storedKey = sha256(clientKey);
        const nonce = Nonce();
        const sig = hmac256(storedKey, u(Trim(user) + "," + sp[1] + "," + nonce));
        for (let i = 0; i < 32; ++i)
            sig[i] ^= clientKey[i];
        return { ClientProof: Sw.Util.Base64Encode(sig), ClientNonce: nonce, OneTimePad: sp[1] };
    }

    /**
     * Build the password proof of Login and of the sensitive UserManager calls (SCRAM when the server asks for it, else the one time hash of authorizers without SCRAM, ex: SolidAdmin).
     * @param {string} user User id.
     * @param {string} password The current password.
     * @returns {Promise<{Hash:string, OneTimePad:string, Salt:string, ClientProof:string, ClientNonce:string}>} The proof (Salt for new password hashes).
     */
    async function Proof(user, password) {
        const sp = await SaltPad(user);
        if (!sp)
            throw new Error(_TF("Failed to get the user salt", "An error message when the server didn't return a password salt"));
        const hash = await PasswordHash(Trim(password), sp[0]);
        if (IsScram(sp)) {
            const p = await ScramProof(user, hash, sp);
            p.Salt = sp[0];
            return p;
        }
        return { Hash: await Sw.Util.Sha256B64(hash + "|" + sp[1]), OneTimePad: sp[1], Salt: sp[0] };
    }

    /**
     * Sign in with HTTP Basic (explicit use only, Login never falls back to it): GET basic_auth must be truthy, then GET login with Authorization: Basic.
     * @param {string} user User id.
     * @param {string} password Password.
     * @returns {Promise<{Succeeded:boolean, Username:string}>} The result.
     */
    async function BasicLogin(user, password) {
        let allowed = false;
        try {
            allowed = !!(await Sw.Net.Get(Sw.Url.Root + "basic_auth", { Reload: true, NoNavigate: true }));
        }
        catch {
        }
        if (!allowed)
            throw new Error(_TF("Neither the password sign in nor basic auth is enabled!", "An error message explaining that there is no way to sign in to the service"));
        let res;
        try {
            res = await fetch(Sw.Url.Root + "login", {
                method: "GET", credentials: "include", cache: "reload",
                headers: { "Authorization": "Basic " + Sw.Util.Base64EncodeString(user + ":" + password) },
            });
        }
        catch {
            throw new Error(_TF("Failed to send the sign in request!", "An error message explaining that the sign in request failed"));
        }
        if (res.status !== 200)
            throw new Error(_TF("Failed to sign in!", "An error message explaining that the sign in failed (wrong password or user)"));
        return { Succeeded: true, Username: user };
    }

    /**
     * Sign in with a user id and password (hashed protocol). There is no automatic Basic fallback: the password is never sent in plain
     * text by this function (BasicLogin can still be called explicitly, it needs basic auth to be enabled for the user).
     * @param {string} user User id (name, e-mail, phone).
     * @param {string} password Password.
     * @returns {Promise<object>} The AuthInfo ({Succeeded, Username, Guid, Tokens ...}) or a falsy value.
     */
    async function Login(user, password) {
        user = Trim(user);
        password = Trim(password);
        let sp;
        try {
            sp = await SaltPad(user);
        }
        catch {
            sp = null;
        }
        if (!sp)
            throw new Error(_TF("The sign in service isn't available, try again later.", "An error message explaining that the password sign in can't be used right now (the server didn't answer the sign in request)"));
        const hash = await PasswordHash(password, sp[0]);
        const result = await Sw.Net.Send(api() + "Login", IsScram(sp) ? await ScramProof(user, hash, sp) : {
            OneTimePad: sp[1],
            Hash: await Sw.Util.Sha256B64(hash + "|" + sp[1]),
        }, post);
        SetUser(result && result.Succeeded ? result : null);
        return result;
    }

    /**
     * Sign out the session (GET logout with "Authorization: Basic logout" to overwrite cached Basic credentials). On success every
     * window of the site hears it at once (Sw.Session.SignedOut): the app shell (this tab and the others) reloads the whole page, as
     * the server's "user.logout" push makes it do too, and the reloaded shell of this tab says "You are signed out" once.
     * @param {object} [who] The signed in user (GetUser reply) when the caller has it: the drafts are removed without asking again.
     * @returns {Promise<boolean>} The server result (true on success).
     */
    async function Logout(who) {
        SetUser(null);
        NoteSignedIn(false);
        //  An explicit sign out removes the user's drafts of unsent text (shared computers, UX guide SAF-11); a time-out keeps them
        if (Sw.Draft && typeof Sw.Draft.ClearAll === "function") {
            try {
                await Sw.Draft.ClearAll(who);
            }
            catch {
            }
        }
        const ok = await Sw.Net.Get(Sw.Url.Root + "logout", {
            Reload: true,
            NoNavigate: true,
            Setup: r => r.headers.set("Authorization", "Basic logout"),
        });
        if (ok && Sw.Session && typeof Sw.Session.SignedOut === "function")
            Sw.Session.SignedOut();
        return ok;
    }

    /**
     * Get the signed in user.
     * @returns {Promise<object|null>} The AuthInfo when a user is signed in, else null.
     */
    async function GetUser() {
        const u = await Sw.Net.Send(api() + "GetUser", null, post);
        const who = u && u.Succeeded ? u : null;
        NoteSignedIn(!!who);
        return who;
    }

    /**
     * Clamp the length limits of a policy (defaults 8..64, min >= 1, max >= min, max <= 128).
     * @param {object} [policy] The password policy.
     * @returns {{Min:number, Max:number}} The limits.
     */
    function Limits(policy) {
        let min = 8;
        let max = 64;
        if (policy) {
            min = Math.max(1, policy.MinLength | 0);
            max = Math.min(128, Math.max(min, policy.MaxLength | 0));
        }
        return { Min: min, Max: max };
    }

    /**
     * A translated multi line description of a password policy.
     * @param {object} [policy] The policy ({MinLength, MaxLength, MixedCase, MixedNumerical, MixedSpecial}).
     * @returns {string} The text.
     */
    function PolicyText(policy) {
        const l = Limits(policy);
        let t = _TF("Password policy", "The title of a section that describes requirements for a user defined password") + ":\n- " +
            _TF("Trailing or leading white-spaces are removed.", "Text explaining that any password will be trimmed before use");
        t += "\n- " + _T("Must be at least {0} characters long.", l.Min, "Text explaining what the minimum allowed password length is.{0} is replaced with the minimun number of chars required");
        t += "\n- " + _T("Must be at most {0} characters long.", l.Max, "Text explaining what the maximum allowed password length is.{0} is replaced with the maximun number of chars allowed");
        if (!policy)
            return t;
        if (policy.MixedCase) {
            t += "\n- " + _TF("Must have at least one lowercase letter.", "Text explaining that a password must contain at least one lowercase letter");
            t += "\n- " + _TF("Must have at least one uppercase letter.", "Text explaining that a password must contain at least one uppercase letter");
        }
        else if (policy.MixedSpecial || policy.MixedNumerical)
            t += "\n- " + _TF("Must have at least one letter.", "Text explaining that a password must contain at least one letter");
        if (policy.MixedNumerical)
            t += "\n- " + _TF("Must have at least one digit.", "Text explaining that a password must contain at least one digit");
        if (policy.MixedSpecial)
            t += "\n- " + _TF("Must have at least one special character (non-letter and non-numeric).", "Text explaining that a password must contain at least one special character");
        return t;
    }

    /**
     * Check a password against a policy (no request).
     * @param {string} p The (trimmed) password.
     * @param {object} [policy] The policy (null: only the blank and default length checks).
     * @returns {string|null} The first failing rule as a translated text, or null if valid.
     */
    function CheckPolicy(p, policy) {
        if (!p)
            return _TF("Password may not be blank!", "Text explaining that a password may now be null, empty or blank");
        const l = Limits(policy);
        if (p.length < l.Min)
            return _T("Password must be at least {0} characters!", l.Min, "Text explaining that a password must contain at least this many characters.{0} is replaced by the minimum number of required characters");
        if (p.length > l.Max)
            return _T("Password may not be longer than {0} characters!", l.Max, "Text explaining that a password may not contain more than this many characters.{0} is replaced by the maximum number of allowed characters");
        if (!policy)
            return null;
        let letter = false, lower = false, upper = false, digit = false, special = false;
        for (const c of p) {
            const lc = c.toLowerCase() === c;
            const uc = c.toUpperCase() === c;
            if (lc !== uc) {
                letter = true;
                lower = lower || lc;
                upper = upper || uc;
            }
            else if (c >= "0" && c <= "9")
                digit = true;
            else
                special = true;
        }
        if ((policy.MixedSpecial || policy.MixedNumerical) && !letter)
            return _TF("Password need to have at least one letter!", "Text explaining that a valid password need to have at least one letter");
        if (policy.MixedCase && !(lower && upper))
            return lower
                ? _TF("Password need to have at least one uppercase letter!", "Text explaining that a valid password need to have at least one uppercase letter")
                : _TF("Password need to have at least one lowercase letter!", "Text explaining that a valid password need to have at least one lowercase letter");
        if (policy.MixedNumerical && !digit)
            return _TF("Password need to have at least one digit!", "Text explaining that a valid password need to have at least one digit");
        if (policy.MixedSpecial && !special)
            return _TF("Password need to have at least one special character (non-letter and non-number)!", "Text explaining that a valid password need to have at least one special character");
        return null;
    }

    /**
     * The rules of a policy as a checklist (the sign in reminder): each rule with a short translated text and whether
     * the password meets it. The maximum length is only listed when it is exceeded.
     * @param {string} p The (trimmed) password.
     * @param {object} [policy] The policy (null: only the default length rules).
     * @returns {Array<{Text:string, Met:boolean}>} The rules, in the order CheckPolicy checks them.
     */
    function PolicyRules(p, policy) {
        p = p || "";
        const l = Limits(policy);
        let letter = false, lower = false, upper = false, digit = false, special = false;
        for (const c of p) {
            const lc = c.toLowerCase() === c;
            const uc = c.toUpperCase() === c;
            if (lc !== uc) {
                letter = true;
                lower = lower || lc;
                upper = upper || uc;
            }
            else if (c >= "0" && c <= "9")
                digit = true;
            else
                special = true;
        }
        const r = [{ Text: _T("At least {0} characters", l.Min, "A password rule in a checklist: the password has at least this many characters.{0} is replaced by the minimum number of characters"), Met: p.length >= l.Min }];
        if (p.length > l.Max)
            r.push({ Text: _T("At most {0} characters", l.Max, "A password rule in a checklist: the password has at most this many characters.{0} is replaced by the maximum number of characters"), Met: false });
        if (!policy)
            return r;
        if (!policy.MixedCase && (policy.MixedSpecial || policy.MixedNumerical))
            r.push({ Text: _TF("A letter", "A password rule in a checklist: the password has at least one letter"), Met: letter });
        if (policy.MixedCase) {
            r.push({ Text: _TF("A lowercase letter", "A password rule in a checklist: the password has at least one lowercase letter"), Met: lower });
            r.push({ Text: _TF("An uppercase letter", "A password rule in a checklist: the password has at least one uppercase letter"), Met: upper });
        }
        if (policy.MixedNumerical)
            r.push({ Text: _TF("A digit", "A password rule in a checklist: the password has at least one digit (0-9)"), Met: digit });
        if (policy.MixedSpecial)
            r.push({ Text: _TF("A special character (not a letter or digit)", "A password rule in a checklist: the password has at least one special character"), Met: special });
        return r;
    }

    /**
     * Get the password policy of a user (POST GetPasswordPolicy(user)): the policy of the authorizer that knows the user, else the
     * common policy of all authorizers. One request per call, no memo and no time limit: the
     * policy depends on the user id, so every new user id asks the server again (the server may add or remove authorizers at any
     * time and doesn't tell the client how many there are, so the client never skips the request).
     * Failures give null, or throw with o.Throw (to tell a failure from a server without a policy).
     * @param {string} user User id.
     * @param {{Throw?: boolean}} [o] Options.
     * @returns {Promise<object|null>} The policy.
     */
    async function UserPolicy(user, o) {
        user = Trim(user) ?? "";
        try {
            return (await Sw.Net.Send(api() + "GetPasswordPolicy", user, Object.assign({ ReadOnly: true, Detached: true }, post))) ?? null;
        }
        catch (e) {
            if (o && o.Throw)
                throw e;
            return null;
        }
    }

    /**
     * Get the policy for new passwords of the UserManager (GetCreatePasswordPolicy), memoized for 60 s.
     * @returns {Promise<object|null>} The policy.
     */
    async function CreatePolicy() {
        return (await Sw.Net.Cached("Sw.Auth.CreatePolicy", null, 60000, () => Sw.Net.Get(api() + "GetCreatePasswordPolicy", { Reload: true, NoNavigate: true }))) ?? null;
    }

    /**
     * Validate a password with a given policy, or with
     * the per user policy cached on `cache`. Without `policy` a request (POST GetPasswordPolicy(user)) is made whenever the user id
     * differs from the user of the cached policy (`cache.PasswordPolicyUser`, set when the answer arrives, so every typed user id is
     * one request, at once: no delay, no memo of earlier users). A check of the same user id while its request is on its way shares
     * that request (`cache.PasswordPolicyPending`), so a repeated check never sends a duplicate. onNewPolicy is awaited when the fetched
     * policy differs from the last one (JSON), or when the fetch gave no policy and none was known before. A failed request gives a null
     * policy (only the blank and the default length rules apply).
     * @param {string} u User id (for the per user policy).
     * @param {string} p Password.
     * @param {object} [cache] Object caching the policy (expandos PasswordPolicy, PasswordPolicyUser, PasswordPolicyStr, PasswordPolicyPending).
     * @param {function(object):*} [onNewPolicy] Called when the fetched policy changed.
     * @param {object} [policy] A fixed policy (no request).
     * @returns {Promise<string|null>} Error text or null.
     */
    async function ValidatePassword(u, p, cache, onNewPolicy, policy) {
        if (!policy && cache) {
            const user = Trim(u);
            policy = cache.PasswordPolicy;
            if (cache.PasswordPolicyUser !== u) {
                //  The request for this user id is already on its way (a second check of the same inputs before the answer came,
                //  ex: the sign in page's autofill re-check while typing): share it, so one change of the user id is one request
                const pending = cache.PasswordPolicyPending;
                if (pending && pending.User === user)
                    policy = await pending.Promise;
                else {
                    const job = { User: user, Promise: null };
                    job.Promise = (async () => {
                        const fetched = await UserPolicy(user);
                        if (fetched) {
                            const s = JSON.stringify(fetched);
                            if (cache.PasswordPolicyStr !== s) {
                                if (onNewPolicy)
                                    await onNewPolicy(fetched);
                                cache.PasswordPolicyStr = s;
                            }
                        }
                        else if (cache.PasswordPolicy === fetched && onNewPolicy)
                            await onNewPolicy(fetched);
                        cache.PasswordPolicy = fetched;
                        cache.PasswordPolicyUser = user;
                        return fetched;
                    })();
                    cache.PasswordPolicyPending = job;
                    try {
                        policy = await job.Promise;
                    }
                    finally {
                        if (cache.PasswordPolicyPending === job)
                            cache.PasswordPolicyPending = null;
                    }
                }
            }
        }
        return CheckPolicy(Trim(p), policy);
    }

    /**
     * Is a switch value "off"?
     * @param {*} v The value (text, boolean).
     * @returns {boolean}
     */
    function isOff(v) {
        if (v === false)
            return true;
        const s = String(v ?? "").trim().replace(/^["']|["']$/g, "").trim().toLowerCase();
        return s === "off" || s === "none" || s === "false" || s === "0" || s === "no";
    }

    /**
     * Is the online password leak check turned off for this deployment? (Read when a check starts, no waiting.) Default on.
     * It is off when any of these says "off" (also none / false / 0 / no):
     *  - the css custom property `--sw-password-leak-check` on :root, ex: `:root{--sw-password-leak-check:off}` in the app's
     *    `app/app.css` (reaches every page, also the stand-alone sign in page);
     *  - `<meta name="sw-password-leak-check" content="off">` in the page;
     *  - `window.SysWeaverPasswordLeakCheck = false` (this window, or the app shell's, ex: set in app/app.js).
     * @returns {boolean} True when the switch says off.
     */
    function leakSwitchOff() {
        try {
            if (window.SysWeaverPasswordLeakCheck === false)
                return true;
            try {
                if (window.top !== window && window.top.SysWeaverPasswordLeakCheck === false)
                    return true;
            }
            catch {
            }
            const meta = document.querySelector('meta[name="sw-password-leak-check"]');
            if (meta && isOff(meta.getAttribute("content")))
                return true;
            return isOff(getComputedStyle(document.documentElement).getPropertyValue("--sw-password-leak-check"));
        }
        catch {
            return false;
        }
    }

    /**
     * The deployment level switch of the online password leak check (see leakSwitchOff). When off no request is made and no result
     * is shown (CheckLeak answers State 0).
     * @returns {Promise<boolean>} True when the leak check may run.
     */
    function LeakCheckEnabled() {
        return Promise.resolve(!leakSwitchOff());
    }

    /**
     * Check if a password is part of known data leaks: GET https://api.pwnedpasswords.com/range/<the first 5 hex chars of the
     * upper case SHA-1> (cors; k-anonymity, the password never leaves the browser), then the rest of the hash is looked up in the
     * answer. One request per call, at once, no time limit, no retry pause and no memo
     * (the caller remembers the last checked password, see Sw.AuthUi.Status.Leak). Turned off for the deployment
     * (LeakCheckEnabled): no request, State 0.
     * @param {string} p The password.
     * @returns {Promise<{State:number, Count:number, Error:*}>} State 0 = turned off (no check, nothing to show), 1 = safe,
     * 2 = leaked Count times, 3 = an answer other than 200, 4 = the request failed (Error, ex: no internet).
     */
    async function CheckLeak(p) {
        //  Turned off for this deployment: never a request
        if (leakSwitchOff())
            return { State: 0, Count: 0, Error: null };
        try {
            const sha1 = Sw.Util.ToHex(await Sw.Util.Sha1(p)).toUpperCase();
            //  SwWeb:allow-online (an optional online feature: a deployment switch turns it off, offline it shows "Password leak check failed!")
            const res = await fetch("https://api.pwnedpasswords.com/range/" + sha1.substring(0, 5), { method: "GET", mode: "cors" });
            if (res.status !== 200)
                return { State: 3, Count: 0, Error: null };
            const text = await res.text();
            const rest = sha1.substring(5);
            for (const line of text.split("\n")) {
                if (line.startsWith(rest)) {
                    const n = parseInt(line.substring(rest.length + 1), 10);
                    return { State: 2, Count: isNaN(n) ? 1 : n, Error: null };
                }
            }
            return { State: 1, Count: 0, Error: null };
        }
        catch (e) {
            return { State: 4, Count: 0, Error: e };
        }
    }

    /**
     * Translated texts of a leak check result (texts, tool tips and kinds).
     * @param {{State:number, Count:number, Error:*}|null} r The result (null = "checking").
     * @returns {{Text:string, Title:string, Kind:string}} Kind: "ok", "error", "warning" or
     * "info" (checking); "" and an empty text when the check is turned off (State 0).
     */
    function LeakText(r) {
        if (r && r.State === 0)
            return { Text: "", Title: "", Kind: "" };
        if (!r)
            return {
                Text: _TF("Checking password ..", "Text displayed when checking if a password entered by a user have been leaked"),
                Title: _T("Checking passwords against {0} for leaks.", LeakDb, "Tool tip description explaining that a password entered by a user is checked if it's part of any data leaks.{0} is replaced by the url to the site used for password checking"),
                Kind: "info",
            };
        if (r.State === 1)
            return {
                Text: _TF("Not found in known data leaks", "Text indicating that an entered password was not found in the known password leaks (a check can not prove that a password is safe)"),
                Title: _T("Password was checked against {0} and wasn't found in any data leaks.", LeakDb, "Tool tip description explaining that the entered password is not found in any data leaks.{0} is replaced by the url to the site used for password checking") + "\n\n" +
                    _TF("It was not found in known data leaks (checked anonymously with the Have I Been Pwned service: only the first 5 characters of a hash of the password are sent). This check does not show how strong the password is.", "Tool tip explaining that the entered password was not found in known password leaks (it says nothing about its strength)"),
                Kind: "ok",
            };
        if (r.State === 2)
            return r.Count === 1
                ? {
                    Text: _TF("This password is part of a data leak", "Text explaining that the entered password is part of one data leak"),
                    Title: _T("Password was checked against {0} that found the password in a data leak.", LeakDb, "Tool tip description explaining that the entered password is part of one data leak.{0} is replaced by the url to the site used for password checking"),
                    Kind: "error",
                }
                : {
                    Text: _T("This password is part of {0} data leaks", r.Count, "Text explaining that the entered password is part of multiple data leaks.{0} is replaced by the number of leaks"),
                    Title: _T("Password was checked against {0} that found the password in {1} data leaks.", LeakDb, r.Count, "Tool tip description explaining that the entered password is part of multiple data leaks.{0} is replaced by the url to the site used for password checking.{1} is replaced by the number of leaks"),
                    Kind: "error",
                };
        //  The check failed (no internet, the site answered with an error): the old warning
        let title =
            _T("We tried to check the password against {0}, but failed.", LeakDb, "Tool tip description explaining that the entered password couldn't be checked for any leaks.{0} is replaced by the url to the site used for password checking") + "\n" +
            _TF("We couldn't assert that the password isn't in any data leak.", "Tool tip description explaining that the entered password couldn't be checked for any leaks.") + "\n\n" +
            _TF("This means that attackers may have this password in their dictionaries.", "Tool tip description explaining that the entered password could be known to attackers.");
        if (r.State === 4 && r.Error)
            title += "\n\n" + _TF("Error:", "Tool tip description header, the next line will show the java script exception message") + "\n" + r.Error;
        return { Text: _TF("Password leak check failed!", "Text explaining that the entered password couldn't be checked for any leaks"), Title: title, Kind: "warning" };
    }

    /**
     * An error text from a validation api (null when valid; the server answers 500 with a text/plain message).
     * @param {string} name Api name under Api/auth/.
     * @param {string} value The value.
     * @returns {Promise<string|null>} The error text or null.
     */
    async function ApiError(name, value) {
        try {
            await Sw.Net.Send(api() + name, value, post);
            return null;
        }
        catch (e) {
            let m = (e && (e.Text || e.message)) || String(e);
            if (m.indexOf("500\n") === 0)
                m = m.substring(4);
            //  The server appends the status (" [500]")
            return m.replace(/\s*\[\d{3}\]\s*$/, "").trim();
        }
    }

    /**
     * Validate an e-mail address on the server (ValidateEmailAddress).
     * @param {string} email The address.
     * @returns {Promise<string|null>} Error text or null.
     */
    function EmailError(email) {
        return ApiError("ValidateEmailAddress", Trim(email));
    }

    /**
     * Validate a phone number (including the country calling code) on the server (GetPhoneNumberInfo).
     * @param {string} phone The number.
     * @returns {Promise<string|null>} Error text or null.
     */
    function PhoneError(phone) {
        return ApiError("GetPhoneNumberInfo", Trim(phone));
    }

    /**
     * Look up the country of a (partial) phone number (GetPhonePrefixInfo): one request per call for any non empty text (also a
     * lone "+"), no memo, exactly as the old AuthInputPhone did on every change of the number.
     * @param {string} phone The number.
     * @returns {Promise<{Iso:string|null, Text:string}>} The ISO country code of the first match and the matches as
     * text lines ("+CountryCode RegionPrefixes Name").
     */
    async function PhoneCountry(phone) {
        phone = Trim(phone);
        if (!phone)
            return { Iso: null, Text: "" };
        let list = null;
        try {
            list = await Sw.Net.Send(api() + "GetPhonePrefixInfo", phone, post);
        }
        catch {
        }
        let iso = null;
        const lines = [];
        for (const pp of list || []) {
            if (!pp.IsoCountry)
                continue;
            iso = iso || pp.IsoCountry;
            lines.push("+" + pp.CountryCode + " " + (pp.RegionPrefixes ? pp.RegionPrefixes + " " : "") + pp.Name);
        }
        return { Iso: iso, Text: lines.join("\n") };
    }

    /**
     * Keep only phone number characters: digits, '.', ' ' and one leading '+' (works for typing, paste and soft keyboards).
     * @param {string} v The typed text.
     * @returns {string} The cleaned text.
     */
    function CleanPhone(v) {
        const s = String(v ?? "").replace(/[^0-9. +]/g, "");
        const lead = s.trimStart().startsWith("+");
        const body = s.replace(/\+/g, "");
        return lead ? "+" + body.trimStart() : body;
    }

    /**
     * Random integer in [0, n) without modulo bias (crypto random, rejection sampling).
     * @param {number} n Exclusive upper bound (1..65536).
     * @returns {number} The value.
     */
    function RandomBelow(n) {
        const lim = 65536 - (65536 % n);
        const b = new Uint16Array(1);
        for (; ;) {
            crypto.getRandomValues(b);
            if (b[0] < lim)
                return b[0] % n;
        }
    }

    /**
     * Generate a random password from look-alike free alphabets (no 0/O/1/l/I/8/B), with per class minimums, shuffled
     * (Fisher-Yates, crypto random).
     * @param {number} [length=16] Length (< 8 gives 16, max 256, at least the sum of the minimums).
     * @param {number} [minUpper=4] Minimum upper case letters.
     * @param {number} [minLower=4] Minimum lower case letters.
     * @param {number} [minDigits=4] Minimum digits.
     * @param {number} [minSpecials=0] Minimum specials (specials only join the general pool when > 0).
     * @returns {string} The password.
     */
    function GenPassword(length, minUpper, minLower, minDigits, minSpecials) {
        /**
         * Positive value or a default.
         * @param {number} v Value.
         * @param {number} d Default.
         * @returns {number}
         */
        const pos = (v, d) => (!v || v < 0) ? d : v;
        length = (!length || length < 8) ? 16 : Math.min(256, length);
        minUpper = pos(minUpper, 4);
        minLower = pos(minLower, 4);
        minDigits = pos(minDigits, 4);
        minSpecials = (!minSpecials || minSpecials < 0) ? 0 : minSpecials;
        const minTotal = minUpper + minLower + minDigits + minSpecials;
        if (length < minTotal)
            length = minTotal;
        const lower = "abcdefghjkmnpqrstuvwxyz";
        const upper = "ACDEFGHJKLMNOPQRSTUVWXY";
        const digits = "2345679";
        const specials = "!.?_";
        const all = lower + upper + digits + (minSpecials > 0 ? specials : "");
        const out = [];
        /**
         * Push random characters.
         * @param {string} chars Alphabet.
         * @param {number} count Count.
         */
        const add = (chars, count) => {
            for (let i = 0; i < count; ++i)
                out.push(chars.charAt(RandomBelow(chars.length)));
        };
        add(lower, minLower);
        add(upper, minUpper);
        add(digits, minDigits);
        add(specials, minSpecials);
        add(all, length - minTotal);
        for (let i = out.length - 1; i > 0; --i) {
            const j = RandomBelow(i + 1);
            const t = out[i];
            out[i] = out[j];
            out[j] = t;
        }
        return out.join("");
    }

    /**
     * Generate a password that satisfies a policy (16 chars clamped to the policy, >= 1 upper, lower, digit, a special if required).
     * @param {object} [policy] The policy.
     * @returns {string} The password.
     */
    function GeneratePassword(policy) {
        const l = Limits(policy);
        return GenPassword(Math.min(l.Max, Math.max(l.Min, 16)), 1, 1, 1, policy && policy.MixedSpecial ? 1 : 0);
    }

    /**
     * Keep only the digits of a short code.
     * @param {string} code User input.
     * @param {boolean} [noLengthCheck] Don't require at least 3 digits.
     * @returns {string|null} The digits; null for empty input or (unless noLengthCheck) fewer than 3 digits.
     */
    function CleanShortCode(code, noLengthCheck) {
        if (!code)
            return null;
        const o = String(code).replace(/[^0-9]/g, "");
        if (noLengthCheck)
            return o;
        return o.length < 3 ? null : o;
    }

    /**
     * Validate a short code against the code stored in the HTTP session (ValidateShortCode).
     * @param {string} code The cleaned up code.
     * @returns {Promise<number>} 1 valid, 0 invalid, -1 expired / no code / too many tries.
     */
    async function ValidateShortCode(code) {
        return await Sw.Net.Send(api() + "ValidateShortCode", code, post);
    }

    /**
     * The token of a token link (url decoded once): the keyless form "Page.html?<token>" (or a short code) is the whole
     * query string after "?"; a keyed link ("DeleteAccount.html?token=..", the invitation "ChoosePassword.html?user=..&salt=..&token=..")
     * gives the value of its `token` parameter (the token apis only know the bare token; the whole query was refused as
     * "Token not found or invalid", also before the rewrite). Theme flags appended by the client or the app shell ("usetheme=..",
     * "settheme=.."; also as the only parameter: the shell opens a user menu page as "AddEmail.html?useTheme=dark", which was taken
     * as a token and showed the token confirm step instead of the form) are removed wherever they are.
     * @param {string} [search] The query (default location.search).
     * @returns {string|null} The token (more than 2 chars) or null.
     */
    function QueryToken(search) {
        let q = search ?? window.location.search ?? "";
        if (q.charAt(0) === "?")
            q = q.substring(1);
        //  (the page options of the core url flags are no part of a token: theme, ?transparent, ?fillscreen, ?css)
        q = q.split("&").filter(p => !/^(usetheme|settheme|transparent|fillscreen|css)(=|$)/i.test(p)).join("&");
        //  Keyed link: only the token parameter is the token (a bare base64 token never starts with "token=" or contains "&token=")
        const keyed = /(?:^|&)token=([^&]*)/.exec(q);
        if (keyed)
            q = keyed[1];
        try {
            q = decodeURIComponent(q);
        }
        catch {
        }
        return q.length > 2 ? q : null;
    }

    /**
     * Navigate after a successful sign in. Embedded in a same origin window with ReloadAll (the app shell): after the
     * delay the top window is reloaded (hash cleared), so the shell is never nested in its own iframe. Otherwise this
     * window navigates to the site root (the query and the last two path segments "auth/<page>.html" removed; location.replace, no history entry).
     * @param {number} [delayMs=3000] Delay before navigating (time to read the success message).
     * @returns {Promise<void>}
     */
    async function StartPage(delayMs) {
        const ms = delayMs ?? 3000;
        if (window.top !== window.self) {
            let top = null;
            try {
                if (typeof window.top.ReloadAll === "function")
                    top = window.top;
            }
            catch {
                //  Cross origin parent: navigate this window
            }
            if (top) {
                await Sw.Util.Delay(ms);
                top.ReloadAll(true, true);
                return;
            }
        }
        const url = new URL(window.location.href);
        url.search = "";
        url.hash = "";
        const t = url.pathname.split("/");
        t.splice(t.length - 2, 2);
        url.pathname = t.join("/");
        let start = url.href;
        if (!start.endsWith("/"))
            start += "/";
        await Sw.Util.Delay(ms);
        //  A real navigation (no history entry), not replaceState + reload: with replaceState the sign in page lives on for a moment
        //  under the site root's url, so its relative urls resolve against the wrong folder (behind a reverse proxy sub path
        //  "../icon.svg" became the proxy's own /ReverseProxyFiles/icon.svg: a 503)
        location.replace(start);
    }

    /** @type {Promise<{Email:boolean, Phone:boolean}>|null} The kinds of user ids the server can reach (UserIdKinds, asked once). */
    let idKinds = null;

    /**
     * The kinds of user ids besides the user name: e-mail when the user manager can send e-mails, phone numbers when it can send text
     * or WhatsApp messages (its coms, Api/auth/UserManagerProps). Without a user manager (built-in users only): neither.
     * @returns {Promise<{Email:boolean, Phone:boolean}>} The kinds (asked once per page).
     */
    function UserIdKinds() {
        if (!idKinds)
            idKinds = (async () => {
                try {
                    const p = await Sw.Net.Get(api() + "UserManagerProps", { NoNavigate: true });
                    const c = (p && Array.isArray(p.Coms)) ? p.Coms : [];
                    return { Email: c.includes("Email"), Phone: c.some(x => (x === "Sms") || (x === "WhatsApp") || (x === "Phone")) };
                }
                catch {
                    return { Email: false, Phone: false };
                }
            })();
        return idKinds;
    }

    /**
     * The hint of a user id field (sign in, forgot password): what a user id can be on this server (UserIdKinds).
     * @returns {Promise<string>} "Your user id is your email address, phone number or username" or a shorter one.
     */
    async function UserIdHint() {
        const k = await UserIdKinds();
        if (k.Email && k.Phone)
            return _TF("Your user id is your email address, phone number or username", "Tool tip description for an input field where the user should enter their user id (username, email, phone)");
        if (k.Email)
            return _TF("Your user id is your email address or username", "Tool tip description for an input field where the user should enter their user id, on a site where it can be a username or an email address (no phone numbers)");
        if (k.Phone)
            return _TF("Your user id is your phone number or username", "Tool tip description for an input field where the user should enter their user id, on a site where it can be a username or a phone number (no email addresses)");
        return _TF("Your user id is your username", "Tool tip description for an input field where the user should enter their user id, on a site where only usernames can be used");
    }

    /**
     * The label of a user id box: what a user id can be on this site (UserIdKinds), ex: "Email or user name" ("User ID" alone told
     * nobody what to type).
     * @returns {Promise<string>} The label.
     */
    async function UserIdLabel() {
        const k = await UserIdKinds();
        if (k.Email && k.Phone)
            return _TF("Email, phone or user name", "Label of an input box where the user enters their user id, on a site where it can be an email address, a phone number or a user name");
        if (k.Email)
            return _TF("Email or user name", "Label of an input box where the user enters their user id, on a site where it can be an email address or a user name");
        if (k.Phone)
            return _TF("Phone or user name", "Label of an input box where the user enters their user id, on a site where it can be a phone number or a user name");
        return _TF("User name", "Label of an input box where the user enters their user id, on a site where only user names can be used");
    }

    /**
     * @namespace Sw.Auth - UI-less authentication helpers (see the file header).
     */
    Sw.Auth = {
        UserIdKinds,
        UserIdHint,
        UserIdLabel,
        Trim,
        SetUser,
        NoteSignedIn,
        MaybeSignedIn,
        PasswordHash,
        SaltPad,
        Proof,
        IsScram,
        ScramProof,
        /** PBKDF2-HMAC-SHA256, 32 bytes (crypto.subtle, pure JS fallback): (pwdBytes, saltBytes, iterations) => Promise<Uint8Array>. */
        Pbkdf2: pbkdf2,
        /** HMAC-SHA256 (synchronous): (keyBytes, msgBytes) => Uint8Array. */
        Hmac256: hmac256,
        Login,
        BasicLogin,
        Logout,
        GetUser,
        Limits,
        PolicyText,
        CheckPolicy,
        PolicyRules,
        UserPolicy,
        ValidatePassword,
        CreatePolicy,
        CheckLeak,
        LeakText,
        LeakCheckEnabled,
        /** @returns {boolean} True when the deployment switch says "off" (LeakCheckEnabled, read now). */
        LeakCheckIsOff: () => leakSwitchOff(),
        EmailError,
        PhoneError,
        PhoneCountry,
        CleanPhone,
        GenPassword,
        GeneratePassword,
        CleanShortCode,
        ValidateShortCode,
        QueryToken,
        StartPage,
        /**
         * Call an Api/auth/ method (POST, no cache, throws on a redirect).
         * @param {string} name Method name.
         * @param {*} [arg] Argument.
         * @returns {Promise<*>} The result.
         */
        Call: (name, arg) => Sw.Net.Send(api() + name, arg ?? null, post),
        /**
         * GET an Api/auth/ method without argument (no cache, throws on a redirect).
         * @param {string} name Method name.
         * @returns {Promise<*>} The result.
         */
        Get: name => Sw.Net.Get(api() + name, { Reload: true, NoNavigate: true }),
    };
})();
