/**
 * @file core/draft.js - drafts of text that was typed but not sent or saved (UX guide SAF-11).
 *
 * Purpose: `Sw.Draft`
 *  - Attach(field, opts) -> handle: keep the value of a text field (textarea, text input or a Sw.TextBox; anything else, ex: a
 *    contenteditable editor, gets a handle that does nothing and a developer warning) as a draft in local storage while typing
 *    (debounced 500 ms), restore it on the next load when the field is empty, and say so: an inline line "Draft restored (saved 10:42)"
 *    (with the date when it is not today) and a "Discard draft" button (drawn when the ui library is loaded; one announcement for all
 *    lines of a field, the lines themselves are groups, not live regions, so nothing is read twice). Discarding offers Undo (a toast
 *    with the ui library). handle.Clear() after the text of THIS field was sent or saved: removes only what this field wrote (a newer
 *    text another tab typed into the same draft is kept and offered, never erased); the earlier drafts kept for it stay offered unless
 *    Clear({ All: true }) (explicit; Sw.Draft.Sent(form) does it for every drafted field of a sent form); handle.Dispose() stops it.
 *    A Clear while a save of the field's form runs (Sw.Work.Current) removes the draft only when that save succeeded.
 *    opts: Context (required: what the text is for, ex: "chat." + chatId, "comment." + rowId), Notice (the element the notice line is
 *    inserted after; default the field row, after its hint), Dirty (false (default, as the original: P3-A-4): never; "restored": a restored draft marks the page
 *    dirty (Sw.Page.SetDirty) until it is sent, discarded or edited away; true: any unsent text), OnRestore(text) (called after
 *    the restore, ex: to update a preview).
 *  - Locked fields: a disabled / read only / inert field (ex: a sent, locked form) gets no restore and no Restore offer (a restore
 *    would change a locked value); the offer comes back when the field is unlocked.
 *  - Never loses text: a stored draft that this field did not restore or write itself (typed first on a slow network, another tab, a
 *    second typed-first in a row) is never overwritten: it is KEPT (a list per context, newest first, 7 days, capped by SIZE:
 *    MaxKeptBytes for all kept texts of a field; only when that is full the oldest go, and the line says so) and offered under the
 *    field: one: "You have an earlier draft (saved 10:42): "the start of it…"", several: a picker of every kept draft (Sw.Select: time +
 *    start of the text; it fits the field's width); "Restore draft" (a swap: the text in the field is kept in its place, every saved time stays) and "Discard earlier
 *    draft" (Undo). Only the user's Restore / Discard, a Clear / Sent (the form was sent) or a RemoveFor removes a kept one.
 *  - Never drafts secrets (language neutral first): type password / hidden / file, autocomplete one-time-code / current-password /
 *    new-password / cc-*, a masked text (-webkit-text-security), a short numeric code field (inputmode numeric, maxlength 4-8),
 *    data-sw-secret, fields inside or marked with data-sw-nodraft; names / ids / labels / placeholders / Context that say key, token,
 *    secret, password, PIN, passphrase, credentials, IBAN, card number ... in many languages (normalized: "Lösenord", "Contraseña",
 *    "Zugangsschlüssel", "пароль", "密码"); and values that look like a secret (a PEM block, an api token, a JWT, a connection string or
 *    url with a password, "<secret word>: value", a card number (Luhn), an IBAN (mod 97), a seed phrase, a long random string): Attach
 *    gives a handle that does nothing (developer warning), Set refuses them (developer warning).
 *  - Subject deleted: RemoveFor(contextPrefix) removes every draft (all users of this site, kept ones too) whose context starts with
 *    the prefix, ex: Sw.Draft.RemoveFor("fakeEmail.reply." + id + ".") after the mail was deleted. List(prefix) lists the drafts.
 *  - Low level (async, the user is looked up once): Get(context) -> {Text, Saved: Date} | null, Set(context, text) (empty removes),
 *    Remove(context, { All }) (All: the kept earlier drafts too), ClearAll() (all drafts of the signed in user on this site: called by an
 *    explicit sign out; a time-out or a forced sign out keeps them), Key(context) -> the storage key, IsSecret(field), LooksSecret(text).
 *  - Storage: localStorage "SysWeaver.Draft.<site>.<user>.<context>" = json {T: text, S: saved ms}, kept drafts under the same key +
 *    "~kept" = {S: newest ms, L: [{T, S}, ...], D: removed for lack of room}, site = the site root path (apps behind one reverse proxy
 *    origin stay apart), user = the signed in user's id (GetUser Guid, "anon" for nobody); at most 64 KB (UTF-8) per draft (a longer
 *    text is not stored, the previous draft stays), expired after 7 days (pruned on load).
 *
 * Load order: after core/page.js. Dependencies: Sw.Storage, Sw.Net, Sw.Url, Sw.Page; Sw.el / Sw.Button / Sw.Toast when loaded.
 * Globals defined: none (Sw.Draft).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    if (Sw.Draft)
        return;

    /** Key prefix of every draft. */
    const Prefix = "SysWeaver.Draft.";

    /** Maximum UTF-8 size of one draft (bytes). */
    const MaxBytes = 65536;

    /** Drafts expire after this long (ms): 7 days. */
    const MaxAgeMs = 7 * 24 * 3600 * 1000;

    /** Debounce of the save while typing (ms). */
    const DebounceMs = 500;

    /** Key suffix of the earlier drafts kept per context (never overwritten by new typing; offered by "Restore draft"). */
    const KeptSuffix = "~kept";

    /** Key suffix of the single earlier draft of older versions (read and merged into the kept list). */
    const PrevSuffix = "~prev";

    /**
     * The space of the earlier drafts kept per context (UTF-8 bytes of all their texts): drafts are capped by SIZE, not by count, so a
     * long text is never dropped because many short ones came after it. Only when the space is full the oldest go, and the notice says
     * so ("1 older draft was removed: there was no room").
     */
    const MaxKeptBytes = 4 * 65536;

    /** A safety limit of the number of kept drafts per context (far above any real use; the space limit decides first). */
    const MaxKept = 100;

    /** @type {Promise<string>|null} The user part of the keys (memoized). */
    let userP = null;

    /** @type {boolean} The expired drafts were pruned in this page. */
    let pruned = false;

    /** Words of a field's name / id / label / Context that say it holds a secret (SEC-1), matched on whole normalized words. */
    const SecretWord = /^(?:(?:api|access|private|secret|ssh|license|licence|signing|auth|session|client|master|encryption|product|activation|recovery|public)?keys?|[a-z]*tokens?|[a-z]*secrets?|[a-z]*passwords?|passwd|pwd|pw|passphrase|passcode|credentials?|creds?|private|otp|totp|hotp|mfa|2fa|pin|pins|cvc|cvv|cvv2|csc|apikey|iban|bic|swift|seed|mnemonic|wif|xprv|connectionstring|connstr|dsn|cardnumber|ccnumber|ssn)$/;

    /**
     * Secret words of other languages and terms of several words (normalized: lower case, without diacritics), found ANYWHERE in a
     * normalized name / label (so "Zugangsschlüssel", "Benutzerkennwort", "mot de passe", "Lösenord" are found as parts of words).
     * Words that are parts of ordinary words in some language ("pin", "key") are only matched as whole words (SecretWord).
     */
    const SecretParts = [
        //  password / pass phrase
        "password", "passwort", "kennwort", "passord", "losenord", "salasana", "wachtwoord", "adgangskode", "adgangsord", "lykilord",
        "motdepasse", "mot de passe", "contrasena", "contrasenya", "senha", "palavrapasse", "parola d'ordine", "paroladordine", "haslo",
        "heslo", "jelszo", "geslo", "lozinka", "slaptazodis", "sifre", "sifra", "matkhau", "mat khau",
        "kata sandi", "katasandi", "katalaluan", "passphrase", "passcode", "passwd",
        "пароль", "парола", "лозинка", "κωδικος", "סיסמה", "سر", "كلمة المرور", "كلمه المرور", "رمز عبور", "गुप्त", "पासवर्ड", "รหัสผ่าน",
        "密码", "密碼", "口令", "パスワード", "暗証", "비밀번호", "암호",
        //  secret / key / token / credential
        "geheim", "segreto", "secreto", "segredo", "tajny", "salainen", "hemlig", "hemmelig", "秘密", "秘钥",
        "密钥", "シークレット", "비밀", "credential", "zugangsdaten", "anmeldedaten", "identifiants", "credenciales", "credenziali",
        "apikey", "api key", "api-key", "access key", "accesskey", "zugangsschlussel", "schlussel", "cle d'api", "cle secrete", "clave de api",
        "clave secreta", "chiave api", "chiave segreta", "chave de api", "токен", "ключ", "トークン", "토큰", "令牌",
        "private key", "privatekey", "privater schlussel", "cle privee", "clave privada", "chiave privata", "chave privada", "закрытый ключ",
        "recovery phrase", "seed phrase", "recovery code", "backup code", "wiederherstellungs", "phrase de recuperation", "frase de recuperacion",
        "connection string", "connectionstring", "verbindungszeichenfolge", "chaine de connexion", "cadena de conexion",
        //  payment / identity
        "card number", "cardnumber", "credit card", "creditcard", "kartennummer", "numero de carte", "numero de tarjeta", "numero di carta",
        "kortnummer", "cvv", "cvc", "iban", "sozialversicherungsnummer", "social security",
        //  one-time codes
        "one-time", "one time code", "einmalcode", "einmalpasswort", "code de verification", "codigo de verificacion", "bestatigungscode",
        "verification code", "otp", "tan",
    ];

    /** The short entries of SecretParts ("tan", "otp", "cvv", "سر" ...) match whole words only (they are parts of ordinary words). */
    const ShortPart = 4;

    /** Texts that are secrets wherever they appear: PEM blocks, well known api token formats, JWTs. */
    const SecretText = [
        /-----BEGIN [A-Z0-9 ]{3,}-----/,
        /\b[rsp]k_(?:live|test)_[A-Za-z0-9]{8,}/,
        /\bgh[pousr]_[A-Za-z0-9]{20,}/,
        /\bgithub_pat_\w{20,}/,
        /\bxox[abprs]-[\w-]{10,}/,
        /\bAKIA[0-9A-Z]{16}\b/,
        /\beyJ[\w-]{8,}\.[\w-]{8,}\.[\w-]{4,}/,
    ];

    /**
     * Warn the page author (Sw.DevWarn: only in developer mode, once per key).
     * @param {string} msg The message.
     * @param {string} key The de-duplication key.
     */
    function devWarn(msg, key) {
        if (Sw.DevWarn)
            Sw.DevWarn(msg, key);
    }

    /**
     * The site part of the keys: the path of the site root (several apps behind one origin stay apart).
     * @returns {string} The site part (no dots).
     */
    function site() {
        let p = "/";
        try {
            p = new URL(Sw.Url.Root).pathname || "/";
        }
        catch {
        }
        return encodeURIComponent(p).replace(/\./g, "%2E");
    }

    /**
     * The user part of a GetUser reply.
     * @param {object|null} u The reply.
     * @returns {string} The user part ("anon" for nobody).
     */
    function userPart(u) {
        return (u && u.Succeeded && u.Guid && u.Guid !== "none") ? String(u.Guid).replace(/[^\w-]/g, "") : "anon";
    }

    /**
     * The sessionStorage key of the user part known in this tab (written by the app shell from its GetUser reply, and after every
     * lookup), so a draft can be restored synchronously, before any input is possible.
     * @returns {string} The key.
     */
    function userCacheKey() {
        return "SysWeaver.DraftUser." + site();
    }

    /**
     * The user part known synchronously in this tab (see userCacheKey), or null.
     * @returns {string|null} The user part.
     */
    function knownUser() {
        try {
            const v = window.sessionStorage.getItem(userCacheKey());
            return v && /^[\w-]+$/.test(v) ? v : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Remember the user part for this tab (see userCacheKey).
     * @param {string|null} v The user part, null to forget it.
     */
    function rememberUser(v) {
        try {
            if (v)
                window.sessionStorage.setItem(userCacheKey(), v);
            else
                window.sessionStorage.removeItem(userCacheKey());
        }
        catch {
        }
    }

    /**
     * The user part of the keys: the signed in user's id (Api/auth/GetUser Guid), "anon" for nobody or without the auth service.
     * @returns {Promise<string>} The user part.
     */
    function user() {
        if (!userP) {
            //  (inside the app shell the shell's user is reused: Sw.Access.CachedUser of the top window, no request of its own)
            let shared = null;
            try {
                const ta = window.top !== window ? window.top.Sw && window.top.Sw.Access : null;
                shared = ta && typeof ta.CachedUser === "function" ? ta.CachedUser() : null;
            }
            catch {
            }
            //  (else the user this tab already knows, written by the app shell and forgotten on every sign in / out push: no request
            //  either; the original pages asked nothing)
            const known = shared ? null : knownUser();
            userP = known ? Promise.resolve(known) : (shared || Sw.Net.Get(Sw.Url.Root + "Api/auth/GetUser", { NoNavigate: true }))
                .then(u => {
                    const p = userPart(u);
                    rememberUser(p);
                    return p;
                })
                .catch(() => knownUser() || "anon");
        }
        return userP;
    }

    //  A sign in or sign out (the server's user.login / user.logout push, the shell's own sign out): the remembered user is stale
    if (Sw.Events && Sw.Events.On) {
        const forget = () => {
            rememberUser(null);
            userP = null;
        };
        Sw.Events.On("user.login", forget);
        Sw.Events.On("user.logout", forget);
    }

    /**
     * Can the user be known without a request (the app shell's user, or the user this tab remembered)?
     * @returns {boolean} True when user() sends no request.
     */
    function userFree() {
        if (userP || knownUser())
            return true;
        try {
            const ta = window.top !== window ? window.top.Sw && window.top.Sw.Access : null;
            return !!(ta && typeof ta.CachedUser === "function" && ta.CachedUser());
        }
        catch {
            return false;
        }
    }

    /**
     * Is a draft of a context stored for any user of this site (so a restore needs the user)?
     * @param {string} context The context.
     * @param {boolean} [prefix] The context is only the start of the contexts (Sw.Draft.List); the earlier drafts (~prev) count too.
     * @returns {boolean} True if one is stored.
     */
    function anyStored(context, prefix) {
        const pre = Prefix + site() + ".";
        const end = "." + String(context);
        try {
            for (let i = 0; i < localStorage.length; ++i) {
                const k = localStorage.key(i);
                if (!k || !k.startsWith(pre))
                    continue;
                if (prefix) {
                    //  (<site>.<user>.<context>[~kept|~prev]: the user part has no ".")
                    const rest = k.substring(pre.length);
                    const dot = rest.indexOf(".");
                    if (dot > 0 && rest.substring(dot + 1).startsWith(String(context)))
                        return true;
                }
                else if (k.endsWith(end) || k.endsWith(end + KeptSuffix) || k.endsWith(end + PrevSuffix))
                    return true;
            }
        }
        catch {
        }
        return false;
    }

    /** @type {Set<object>} The attached drafts of this page (flushed on pagehide, disposed when their field leaves the document). */
    const attached = new Set();

    /** @type {number} The timer that disposes drafts whose field was removed (a closed dialog). */
    let sweepTimer = 0;

    /** Dispose the drafts whose field is no longer in the document (the stored draft stays); runs while any is attached. */
    function sweep() {
        sweepTimer = 0;
        for (const h of Array.from(attached))
            if (!h.Field.isConnected)
                h.Dispose();
        if (attached.size)
            sweepTimer = setTimeout(sweep, 2000);
    }

    //  One pagehide handler for every draft of the page (a handler per draft kept closed dialogs alive)
    try {
        window.addEventListener("pagehide", () => {
            for (const h of Array.from(attached))
                h.Flush();
        });
        //  A dialog's "Discard" (Sw.Dialog: the event "sw-dialog-discard" on its box) discards the drafts inside it
        document.addEventListener("sw-dialog-discard", ev => {
            if (ev.target && ev.target.nodeType === 1)
                Sw.Draft.DiscardIn(ev.target);
        }, true);
    }
    catch {
    }

    /**
     * A handle that does nothing (a secret field, no context): the same shape as an attached draft, so callers never need a null check.
     * @param {Element|null} field The field.
     * @returns {object} The handle.
     */
    function noDraft(field) {
        return { Field: field || null, Context: "", Active: false, Ready: Promise.resolve(false), Clear: async () => { }, Dispose: () => { }, Flush: () => { }, Forget: () => { }, Lock: () => { } };
    }

    /**
     * The storage key of a context for a user.
     * @param {string} context The context.
     * @param {string} u The user part.
     * @returns {string} The key.
     */
    function keyOf(context, u) {
        return Prefix + site() + "." + u + "." + String(context);
    }

    /**
     * Read a stored draft entry.
     * @param {string} key The storage key.
     * @returns {{T: string, S: number}|null} The entry, null when there is none (or it expired).
     */
    function readEntry(key) {
        const v = Sw.Storage.Local.GetJson(key, null);
        return (v && typeof v.T === "string" && v.T && typeof v.S === "number" && Date.now() - v.S <= MaxAgeMs) ? { T: v.T, S: v.S } : null;
    }

    /**
     * The kept earlier drafts of a key, newest first (the single "~prev" of older versions is merged in).
     * @param {string} key The storage key of the context.
     * @returns {Array<{T: string, S: number}>} The kept drafts.
     */
    function readKept(key) {
        const v = Sw.Storage.Local.GetJson(key + KeptSuffix, null);
        let list = v && Array.isArray(v.L) ? v.L.filter(x => x && typeof x.T === "string" && x.T && typeof x.S === "number" && Date.now() - x.S <= MaxAgeMs) : [];
        const old = readEntry(key + PrevSuffix);
        if (old) {
            if (!list.some(x => x.T === old.T))
                list.push(old);
            Sw.Storage.Local.Remove(key + PrevSuffix);
            writeKept(key, list);
        }
        list.sort((a, b) => b.S - a.S);
        return list;
    }

    /**
     * Store the kept earlier drafts of a key (an empty list removes them).
     * @param {string} key The storage key of the context.
     * @param {Array<{T: string, S: number}>} list The kept drafts.
     */
    function writeKept(key, list) {
        list = list.slice().sort((a, b) => b.S - a.S);
        //  Capped by size (MaxKeptBytes), newest first; what does not fit is counted (D) so the notice can say it, never a silent drop
        const prev = Sw.Storage.Local.GetJson(key + KeptSuffix, null);
        let dropped = prev && typeof prev.D === "number" ? prev.D : 0;
        let total = 0;
        const fit = [];
        for (const x of list) {
            total += bytes(x.T);
            if (fit.length >= MaxKept || (fit.length && total > MaxKeptBytes))
                ++dropped;
            else
                fit.push(x);
        }
        if (fit.length < list.length)
            devWarn("Sw.Draft: " + (list.length - fit.length) + " older draft(s) of a field were removed: the space for earlier drafts (" + MaxKeptBytes + " bytes) is full; the notice tells the user", "draft.keptfull." + key);
        if (!fit.length) {
            Sw.Storage.Local.Remove(key + KeptSuffix);
            return;
        }
        //  The storage is full: the oldest go until it fits (counted, said in the notice)
        while (fit.length && !Sw.Storage.Local.SetJson(key + KeptSuffix, { S: fit[0].S, L: fit, D: dropped || undefined })) {
            if (fit.length === 1)
                break;
            fit.pop();
            ++dropped;
        }
    }

    /**
     * The number of kept drafts that were removed for lack of room (not told to the user yet), and reset it.
     * @param {string} key The storage key of the context.
     * @param {boolean} [reset] Reset the count (the user was told).
     * @returns {number} The count.
     */
    function keptDropped(key, reset) {
        const v = Sw.Storage.Local.GetJson(key + KeptSuffix, null);
        const n = v && typeof v.D === "number" ? v.D : 0;
        if (n && reset) {
            delete v.D;
            Sw.Storage.Local.SetJson(key + KeptSuffix, v);
        }
        return n;
    }

    /**
     * Keep a stored draft aside (it is never overwritten): added to the kept list unless the same text is kept already.
     * @param {string} key The storage key of the context.
     * @param {{T: string, S: number}} entry The draft.
     * @returns {boolean} True when it was added.
     */
    function keep(key, entry) {
        if (!entry || !entry.T || !entry.T.trim())
            return false;
        const list = readKept(key);
        if (list.some(x => x.T === entry.T))
            return false;
        list.push({ T: entry.T, S: entry.S || Date.now() });
        writeKept(key, list);
        return true;
    }

    /** Remove the expired drafts (once per page). */
    function prune() {
        if (pruned)
            return;
        pruned = true;
        try {
            const ls = window.localStorage;
            const now = Date.now();
            const drop = [];
            const rewrite = [];
            for (let i = 0; i < ls.length; ++i) {
                const k = ls.key(i);
                if (!k || !k.startsWith(Prefix))
                    continue;
                const v = Sw.Storage.Local.GetJson(k, null);
                if (v && Array.isArray(v.L)) {
                    const L = v.L.filter(x => x && typeof x.S === "number" && now - x.S <= MaxAgeMs);
                    if (!L.length)
                        drop.push(k);
                    else if (L.length !== v.L.length)
                        rewrite.push([k, L]);
                    continue;
                }
                if (!v || typeof v.S !== "number" || now - v.S > MaxAgeMs)
                    drop.push(k);
            }
            drop.forEach(k => Sw.Storage.Local.Remove(k));
            rewrite.forEach(([k, L]) => Sw.Storage.Local.SetJson(k, { S: L[0].S, L }));
        }
        catch {
            //  No storage
        }
    }

    /**
     * The UTF-8 size of a text.
     * @param {string} s The text.
     * @returns {number} Bytes.
     */
    function bytes(s) {
        try {
            return new TextEncoder().encode(s).length;
        }
        catch {
            return s.length * 3;
        }
    }

    /**
     * The words of a name, id or label ("apiKey" -> "api", "key"; "API_KEY" -> "api", "key"; "APIKey" -> "apikey").
     * @param {string} s The text.
     * @returns {string[]} Lower case words.
     */
    function words(s) {
        return normalize(String(s || "").replace(/(\p{Ll}|\p{N})(\p{Lu})/gu, "$1 $2")).split(/[^\p{L}\p{N}]+/u).filter(Boolean);
    }

    /**
     * Normalize a text for the secret word test: lower case, without diacritics ("Lösenord" -> "losenord", "Contraseña" ->
     * "contrasena"), any language.
     * @param {string} s The text.
     * @returns {string} The normalized text.
     */
    function normalize(s) {
        let t = String(s || "");
        try {
            t = t.normalize("NFD").replace(/\p{M}+/gu, "");
        }
        catch {
        }
        return t.replace(/ß/g, "ss").replace(/[øØ]/g, "o").replace(/[łŁ]/g, "l").replace(/[đĐ]/g, "d").replace(/æ/gi, "ae").toLowerCase();
    }

    /**
     * Test if a name, id, label or context says it is a secret: a whole word (key, token, secret, password, pin, iban ...) in any
     * spelling ("apiKey", "API_KEY", "api-key"), or a secret term of any of the listed languages anywhere in it (SecretParts).
     * @param {string} s The text.
     * @returns {boolean} True when it says secret.
     */
    function namesSecret(s) {
        const ws = words(s);
        if (ws.some(w => SecretWord.test(w)))
            return true;
        const n = normalize(s);
        const joined = " " + ws.join(" ") + " ";
        return SecretParts.some(p => {
            const q = normalize(p);
            return q.length <= ShortPart ? joined.includes(" " + q + " ") : (n.includes(q) || joined.includes(q));
        });
    }

    /**
     * Shannon entropy of a text (bits per character).
     * @param {string} s The text.
     * @returns {number} The entropy.
     */
    function entropy(s) {
        const n = new Map();
        for (const c of s)
            n.set(c, (n.get(c) || 0) + 1);
        let e = 0;
        for (const k of n.values()) {
            const p = k / s.length;
            e -= p * Math.log2(p);
        }
        return e;
    }

    /**
     * Luhn check of a card number.
     * @param {string} d The digits.
     * @returns {boolean} True when valid.
     */
    function luhn(d) {
        let sum = 0;
        for (let i = 0; i < d.length; ++i) {
            let x = d.charCodeAt(d.length - 1 - i) - 48;
            if (i % 2) {
                x *= 2;
                if (x > 9)
                    x -= 9;
            }
            sum += x;
        }
        return sum % 10 === 0;
    }

    /**
     * Mod-97 check of an IBAN.
     * @param {string} iban The IBAN without spaces, upper case.
     * @returns {boolean} True when valid.
     */
    function ibanOk(iban) {
        const r = iban.substring(4) + iban.substring(0, 4);
        let m = 0;
        for (const c of r) {
            const v = c >= "A" && c <= "Z" ? String(c.charCodeAt(0) - 55) : c;
            for (const ch of v)
                m = (m * 10 + (ch.charCodeAt(0) - 48)) % 97;
        }
        return m === 1;
    }

    /**
     * Language-neutral value patterns of secrets anywhere in a text (SEC-1): a connection string or url with a password, "<secret
     * word in any listed language>: value", a card number (Luhn), an IBAN (mod 97), a recovery / seed phrase (12-24 plain words), a
     * long high-entropy string inside the text (not a GUID).
     * @param {string} s The text.
     * @returns {boolean} True when it holds a secret.
     */
    function secretValue(s) {
        //  Connection strings, urls with a user and password
        if (/(?:^|[;\s,{"'])(?:password|pwd|passwd|accountkey|sharedaccesskey|sharedaccesssignature|clientsecret|client_secret|apikey|api_key|access_token|refresh_token|secret)\s*[=:]\s*[^;\s"']{3,}/i.test(s))
            return true;
        if (/\b[a-z][a-z0-9+.-]*:\/\/[^\s/:@]+:[^\s/@]{3,}@/i.test(s))
            return true;
        //  "Passwort: Hunter2", "Lösenord = x" ...: a secret word (any listed language) followed by a value
        const kv = /([\p{L}][\p{L}\p{N} _'.-]{0,40}?)\s*[:=]\s*(\S{4,})/gu;
        for (let m; (m = kv.exec(s));)
            if (namesSecret(m[1]))
                return true;
        //  Card numbers: 13 to 19 digits (spaces / dashes between groups), Luhn valid
        for (const m of s.matchAll(/(?:\d[ -]?){12,18}\d/g)) {
            const d = m[0].replace(/\D/g, "");
            if (d.length >= 13 && d.length <= 19 && luhn(d) && !/^(\d)\1+$/.test(d))
                return true;
        }
        //  IBAN
        for (const m of s.toUpperCase().matchAll(/\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,4})?\b/g)) {
            const v = m[0].replace(/ /g, "");
            if (v.length >= 15 && v.length <= 34 && ibanOk(v))
                return true;
        }
        //  A recovery (seed) phrase: the whole text is 12, 15, 18, 21 or 24 plain lower case words
        const ws = s.trim().split(/\s+/);
        if ([12, 15, 18, 21, 24].includes(ws.length) && ws.every(w => /^[a-z]{3,8}$/.test(w)))
            return true;
        //  A long random string inside a text (a pasted key): mixed case and digits, high entropy, not a GUID
        for (const m of s.matchAll(/[A-Za-z0-9+/=_-]{32,}/g)) {
            const v = m[0];
            if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v))
                continue;
            if (/\d/.test(v) && /[a-z]/.test(v) && /[A-Z]/.test(v) && entropy(v) >= 4)
                return true;
        }
        return false;
    }

    /**
     * Test if a text looks like a secret: a PEM block, an api token of a well known format, a JWT anywhere in it, a connection string,
     * a card number, an IBAN, a seed phrase, "<secret word>: value" in any listed language (secretValue), or a value that is one long
     * random string (Sw.Clipboard.LooksSecret when the ui library is loaded).
     * @param {string} text The text.
     * @returns {boolean} True when it must not be stored.
     */
    function looksSecret(text) {
        const s = String(text || "");
        if (!s.trim())
            return false;
        if (SecretText.some(r => r.test(s)))
            return true;
        if (secretValue(s))
            return true;
        const t = s.trim();
        if (/\s/.test(t))
            return false;
        if (Sw.Clipboard && typeof Sw.Clipboard.LooksSecret === "function") {
            try {
                return !!Sw.Clipboard.LooksSecret(t);
            }
            catch {
            }
        }
        return t.length >= 24 && /^[A-Za-z0-9+/=_-]+$/.test(t) && /\d/.test(t) && /[a-z]/.test(t) && /[A-Z]/.test(t);
    }

    /**
     * Why a field is never drafted (a secret), or "" when it may be.
     * @param {Element} field The field.
     * @param {string} [context] The draft context (its words count too).
     * @returns {string} The reason (for the developer warning), "" when not secret.
     */
    function secretReason(field, context) {
        if (!field || !field.getAttribute)
            return "no field";
        if (field.closest && field.closest("[data-sw-nodraft]"))
            return "data-sw-nodraft";
        const type = String(field.getAttribute("type") || "").toLowerCase();
        if (type === "password" || type === "hidden" || type === "file")
            return "type=\"" + type + "\"";
        if (field.hasAttribute("data-sw-secret"))
            return "data-sw-secret";
        //  Language-neutral signals first: what the field IS (autocomplete tokens of the HTML standard, a masked text)
        const ac = String(field.getAttribute("autocomplete") || "").toLowerCase().split(/\s+/);
        const acHit = ac.find(a => a === "one-time-code" || a === "current-password" || a === "new-password" || a.startsWith("cc-") || a === "webauthn");
        if (acHit)
            return "autocomplete=\"" + acHit + "\"";
        try {
            const cs = field.ownerDocument.defaultView.getComputedStyle(field);
            const sec = cs.webkitTextSecurity || cs.getPropertyValue("-webkit-text-security");
            if (sec && sec !== "none")
                return "-webkit-text-security: " + sec;
        }
        catch {
        }
        //  A short numeric code field (inputmode numeric + a max length of 4 to 8, ex: a PIN or a one-time code)
        const im = String(field.getAttribute("inputmode") || "").toLowerCase();
        const ml = +field.getAttribute("maxlength") || 0;
        if (im === "numeric" && ml >= 4 && ml <= 8 && field.tagName !== "TEXTAREA")
            return "inputmode=\"numeric\" maxlength=\"" + ml + "\" (a code)";
        const names = [["name", field.getAttribute("name")], ["id", field.id], ["aria-label", field.getAttribute("aria-label")], ["placeholder", field.getAttribute("placeholder")], ["title", field.getAttribute("title")]];
        try {
            for (const l of Array.from(field.labels || []))
                names.push(["label", l.textContent]);
            const lb = field.getAttribute("aria-labelledby");
            if (lb)
                for (const id of lb.split(/\s+/)) {
                    const e = id && field.ownerDocument.getElementById(id);
                    if (e)
                        names.push(["label", e.textContent]);
                }
        }
        catch {
        }
        if (context)
            names.push(["Context", context]);
        const hit = names.find(([, v]) => v && namesSecret(v));
        return hit ? hit[0] + " \"" + String(hit[1]).trim().substring(0, 60) + "\"" : "";
    }

    /**
     * Test if a field holds a secret that is never drafted.
     * @param {Element} field The field.
     * @param {string} [context] The draft context.
     * @returns {boolean} True for passwords, one-time codes, fields named / labelled as a key, token, secret, password and fields
     * marked (or inside) data-sw-nodraft.
     */
    function isSecret(field, context) {
        return !!secretReason(field, context);
    }

    /**
     * A short "saved at" text: the time, with the date when it is not today.
     * @param {Date} d The time.
     * @returns {string} The time text.
     */
    function whenText(d) {
        const now = new Date();
        const time = d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        if (d.toDateString() === now.toDateString())
            return time;
        return d.toLocaleDateString([], { day: "numeric", month: "short" }) + ", " + time;
    }

    /**
     * The "Draft restored (saved ...)" text.
     * @param {Date} d The time.
     * @returns {string} The text.
     */
    function savedText(d) {
        const now = new Date();
        const time = d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        if (d.toDateString() === now.toDateString())
            return _T("Draft restored (saved {0})", time, "Line under a text field after the page restored text that was typed before a reload but not sent.{0} is replaced with the time it was saved, ex: 10:42");
        const date = d.toLocaleDateString([], { day: "numeric", month: "short" });
        return _T("Draft restored (saved {0}, {1})", date, time, "Line under a text field after the page restored text that was typed before a reload but not sent.{0} is replaced with the date, {1} with the time it was saved");
    }

    /**
     * Remove stored drafts by a test on their context (every user of this site).
     * @param {function(string):boolean} test Gets the context (without the kept suffix).
     * @returns {number} The number of removed keys.
     */
    function removeWhere(test) {
        const base = Prefix + site() + ".";
        const drop = [];
        try {
            const ls = window.localStorage;
            for (let i = 0; i < ls.length; ++i) {
                const k = ls.key(i);
                if (!k || !k.startsWith(base))
                    continue;
                const rest = k.substring(base.length);
                const dot = rest.indexOf(".");
                if (dot < 0)
                    continue;
                const ctx = rest.substring(dot + 1).replace(/~(?:kept|prev)$/, "");
                if (test(ctx))
                    drop.push(k);
            }
        }
        catch {
        }
        drop.forEach(k => Sw.Storage.Local.Remove(k));
        return drop.length;
    }

    /**
     * @namespace Sw.Draft - drafts of uncommitted text (see the file header).
     */
    Sw.Draft = {
        /** @type {string} The key prefix of every draft. */
        Prefix,

        /** @type {number} The maximum UTF-8 size of one draft (bytes). */
        MaxBytes,

        /** @type {number} Drafts expire after this long (ms). */
        MaxAgeMs,

        /** @type {number} The space (UTF-8 bytes) of all earlier drafts kept per context (the cap: by size, not by count). */
        MaxKeptBytes,

        /** @type {number} A safety limit of the number of earlier drafts kept per context (the space decides first). */
        MaxKept,

        /** Test if a field is a secret that is never drafted (see isSecret): IsSecret(field, context). */
        IsSecret: isSecret,

        /** Test if a text looks like a secret that is never stored (see looksSecret). */
        LooksSecret: looksSecret,

        /**
         * The storage key of a context (for the signed in user).
         * @param {string} context The context.
         * @returns {Promise<string>} The key.
         */
        async Key(context) {
            return keyOf(context, await user());
        },

        /**
         * Read a draft.
         * @param {string} context The context.
         * @returns {Promise<{Text: string, Saved: Date}|null>} The draft, null when there is none (or it expired).
         */
        async Get(context) {
            prune();
            //  (nothing stored for this context by anyone and the user not known yet: no user lookup; the original pages sent nothing
            //  when a form opened, ex: the composer of the stand-alone FakeEmail inbox)
            if (!userFree() && !anyStored(context))
                return null;
            const k = keyOf(context, await user());
            const v = readEntry(k);
            if (!v) {
                if (Sw.Storage.Local.GetJson(k, null))
                    Sw.Storage.Local.Remove(k);
                return null;
            }
            return { Text: v.T, Saved: new Date(v.S) };
        },

        /**
         * Store a draft (an empty text removes it; a text over MaxBytes is not stored and the previous draft stays). A context or a
         * text that looks like a secret is refused (SEC-1; developer warning).
         * @param {string} context The context.
         * @param {string} text The text.
         * @returns {Promise<boolean>} True when stored (or removed).
         */
        async Set(context, text) {
            const k = keyOf(context, await user());
            text = text === null || text === undefined ? "" : String(text);
            if (!text.trim()) {
                Sw.Storage.Local.Remove(k);
                return true;
            }
            if (namesSecret(context) || looksSecret(text)) {
                devWarn("Sw.Draft.Set(\"" + context + "\"): the context or the text looks like a secret (key, token, password, PEM block); secrets are never stored as drafts (SEC-1)", "draft.secret." + context);
                return false;
            }
            if (bytes(text) > MaxBytes) {
                devWarn("Sw.Draft: the draft of " + context + " is larger than " + MaxBytes + " bytes and is not stored", "draft.size." + context);
                return false;
            }
            return Sw.Storage.Local.SetJson(k, { T: text, S: Date.now() });
        },

        /**
         * Remove a draft (the text was sent or saved). The earlier drafts kept for the context stay (they were not looked at) unless
         * opts.All.
         * @param {string} context The context.
         * @param {{All: boolean}} [opts] All: the kept earlier drafts too.
         * @returns {Promise<void>}
         */
        async Remove(context, opts) {
            if (opts && Sw.DevOptions)
                Sw.DevOptions("Sw.Draft.Remove", opts);
            //  (nothing stored for this context: nothing to remove, no user lookup)
            if (!userFree() && !anyStored(context))
                return;
            const k = keyOf(context, await user());
            Sw.Storage.Local.Remove(k);
            if (opts && opts.All) {
                Sw.Storage.Local.Remove(k + KeptSuffix);
                Sw.Storage.Local.Remove(k + PrevSuffix);
            }
        },

        /**
         * Remove every draft of a subject that was deleted (a mail, a row, a chat): all contexts that start with the prefix, of every
         * user on this site, kept earlier drafts included; attached fields of the page drop their notices. Synchronous.
         * Ex: after the mail was deleted, Sw.Draft.RemoveFor("fakeEmail.reply." + id + ".").
         * @param {string} contextPrefix The start of the contexts (end it with "." to not match "mail.12" for "mail.1").
         * @returns {number} The number of removed stored drafts.
         */
        RemoveFor(contextPrefix) {
            const p = String(contextPrefix || "");
            if (!p) {
                devWarn("Sw.Draft.RemoveFor(): pass the context prefix of the deleted subject (ex: \"comment.\" + id + \".\"); nothing was removed", "draft.removefor.empty");
                return 0;
            }
            for (const h of Array.from(attached))
                if (h.Context.startsWith(p))
                    h.Forget();
            return removeWhere(ctx => ctx.startsWith(p));
        },

        /**
         * List the drafts of the signed in user whose context starts with a prefix (ex: to tag the mails that have a reply draft).
         * @param {string} [contextPrefix] The start of the contexts ("" for all).
         * @returns {Promise<Array<{Context: string, Saved: Date, Kept: number}>>} The drafts (Kept: the number of earlier drafts).
         */
        async List(contextPrefix) {
            prune();
            const p = String(contextPrefix || "");
            //  (no draft with this prefix stored by anyone and the user not known yet: none, no user lookup)
            if (!userFree() && !anyStored(p, true))
                return [];
            const base = Prefix + site() + "." + (await user()) + ".";
            const found = new Map();
            try {
                const ls = window.localStorage;
                for (let i = 0; i < ls.length; ++i) {
                    const k = ls.key(i);
                    if (!k || !k.startsWith(base))
                        continue;
                    const ctx = k.substring(base.length).replace(/~(?:kept|prev)$/, "");
                    if (ctx.startsWith(p) && !found.has(ctx))
                        found.set(ctx, null);
                }
            }
            catch {
            }
            const res = [];
            for (const ctx of found.keys()) {
                const k = base + ctx;
                const d = readEntry(k);
                const kept = readKept(k);
                if (!d && !kept.length)
                    continue;
                res.push({ Context: ctx, Saved: new Date(d ? d.S : kept[0].S), Kept: kept.length });
            }
            return res;
        },

        /**
         * The app shell tells the drafts who is signed in (its GetUser reply), so a framed page restores its drafts synchronously.
         * @param {object|null} u The GetUser reply (null: nobody / forget).
         */
        SetUser(u) {
            rememberUser(u ? userPart(u) : null);
        },

        /**
         * Discard the drafts attached to fields inside a container (a dialog's "Discard changes": the text is thrown away on purpose,
         * the earlier drafts kept for those fields too). Sw.Dialog calls it (or dispatches "sw-dialog-discard" on its box).
         * @param {Element} container The container.
         * @returns {number} The number of drafts discarded.
         */
        DiscardIn(container) {
            let n = 0;
            for (const h of Array.from(attached))
                if (container && container.contains(h.Field)) {
                    h.Clear({ All: true });
                    h.Dispose();
                    ++n;
                }
            return n;
        },

        /**
         * A form was sent or saved successfully: clear EVERY draft of it (each attached field's draft and the earlier drafts kept for
         * it: they are versions of what was sent), so nothing is offered again on the sent form, on "Send another" or on the next visit
         * (SAF-11 "cleared when sent"). Pass the form, its dialog / card, or any element inside it (its form, dialog, [data-sw-form] or
         * card is used). Sw.Form calls it on success; a hand-made form calls it after its send succeeded.
         * @param {Element} el The form (or an element inside it).
         * @returns {Promise<number>} The number of drafts cleared.
         */
        async Sent(el) {
            if (!el || el.nodeType !== 1) {
                devWarn("Sw.Draft.Sent(el): pass the form (or an element inside it) that was sent; nothing was cleared", "draft.sent.none");
                return 0;
            }
            const form = (el.matches && el.matches("form, [data-sw-form], .sw-dialog, [role='dialog'], .sw-card, .sw-form")) ? el
                : (el.closest && el.closest("form, [data-sw-form], .sw-form, .sw-dialog, [role='dialog'], .sw-card")) || el;
            const hs = Array.from(attached).filter(h => h.Field === el || form.contains(h.Field));
            await Promise.all(hs.map(h => h.Clear({ All: true }).catch(() => null)));
            return hs.length;
        },

        /**
         * Stop the drafts attached to fields inside a container (a dialog closed: the stored drafts stay for the next time).
         * @param {Element} container The container.
         * @returns {number} The number of drafts stopped.
         */
        DisposeIn(container) {
            let n = 0;
            for (const h of Array.from(attached))
                if (container && container.contains(h.Field)) {
                    h.Dispose();
                    ++n;
                }
            return n;
        },

        /**
         * Remove all drafts of the signed in user on this site (an explicit sign out on a shared computer). Call it before the sign
         * out request. Never sends a request (a sign out sends the original's requests only, S6 F9): the user is the caller's GetUser
         * reply, else the one this tab or the app shell already knows; when nobody knows it, the drafts of every user of this site go
         * (safe on a shared computer).
         * @param {object} [who] The signed in user (a GetUser reply), when the caller knows it.
         * @returns {Promise<number>} The number of removed drafts.
         */
        async ClearAll(who) {
            let part = who && typeof who === "object" ? userPart(who) : null;
            if (!part) {
                try {
                    if (userP)
                        part = await userP;
                    else {
                        const ta = window.top !== window ? window.top.Sw && window.top.Sw.Access : null;
                        const shared = ta && typeof ta.CachedUser === "function" ? ta.CachedUser() : null;
                        part = shared ? userPart(await shared) : knownUser();
                    }
                }
                catch {
                    part = knownUser();
                }
            }
            const p = Prefix + site() + "." + (part ? part + "." : "");
            const drop = [];
            try {
                const ls = window.localStorage;
                for (let i = 0; i < ls.length; ++i) {
                    const k = ls.key(i);
                    if (k && k.startsWith(p))
                        drop.push(k);
                }
            }
            catch {
            }
            drop.forEach(k => Sw.Storage.Local.Remove(k));
            return drop.length;
        },

        /**
         * Keep the text of a field as a draft (see the file header).
         * @param {HTMLTextAreaElement|HTMLInputElement|{Input: Element, E: Element}} field The field, or a Sw component with an Input (a
         * Sw.TextBox: the notice goes under its field row).
         * @param {{Context: string, Notice: Element, Dirty: (boolean|string), OnRestore: function(string):void}} o Options.
         * @returns {{Clear: function(object=):Promise<void>, Dispose: function():void, Forget: function():void, Ready: Promise<boolean>,
         * Field: Element, Context: string, Active: boolean}} The handle (Ready resolves true when a draft was restored). A secret field
         * or a missing context gets a handle that does nothing (Active false), so callers never need a null check. A field that is
         * attached already gets its existing handle.
         */
        Attach(field, o) {
            o = o || {};
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Draft.Attach", o, { Context: ["string", "number"], Notice: "element", Dirty: "boolean|string", OnRestore: "function", Active: "boolean" });
            //  A component (Sw.TextBox): its input is the field, the notice goes after its field row
            let comp = null;
            if (field && !field.nodeType && field.Input && field.Input.nodeType === 1) {
                comp = field;
                field = field.Input;
            }
            if (!field)
                return noDraft(null);
            //  Only a text field holds a value to draft: a contenteditable editor (or any other element) would be "Active" and store nothing
            if (!(field.tagName === "TEXTAREA" || (field.tagName === "INPUT" && /^(?:|text|search|email|url|tel|number)$/i.test(field.getAttribute("type") || "")))) {
                devWarn("Sw.Draft.Attach(\"" + (o.Context || "") + "\"): only a textarea, a text input or a Sw.TextBox can be drafted (got " + String(field.tagName || "?").toLowerCase()
                    + (field.isContentEditable ? " contenteditable" : "") + "); nothing is drafted (Active false). For a rich text editor keep its text with Sw.Draft.Get / Set.", "draft.kind." + (o.Context || ""));
                return noDraft(field);
            }
            if (!o.Context) {
                devWarn("Sw.Draft.Attach: a Context is required (what the text is for, ex: \"comment.\" + rowId); nothing is drafted", "draft.context");
                return noDraft(field);
            }
            const ctx = String(o.Context);
            const why = secretReason(field, ctx);
            if (why) {
                devWarn("Sw.Draft.Attach(\"" + ctx + "\"): never drafted, the field holds a secret (" + why + "); secrets are never stored on the device (SEC-1)", "draft.secretfield." + ctx);
                return noDraft(field);
            }
            //  One draft per field, one field per context: a second Attach would show no notice / lose one field's text on reload
            for (const x of attached) {
                if (x.Field === field) {
                    devWarn("Sw.Draft.Attach(\"" + ctx + "\"): this field has a draft already (\"" + x.Context + "\"; ex: Sw.TextBox({ Draft }) plus Sw.Draft.Attach(tb.Input)); the existing one is used", "draft.twice." + ctx);
                    return x;
                }
                if (x.Context === ctx)
                    devWarn("Sw.Draft.Attach(\"" + ctx + "\"): another field on this page uses the same Context; the last one typed in wins on the next load and the other text is lost: give every field its own Context", "draft.shared." + ctx);
            }
            //  Inside a dialog the dialog is the form: its own dirty tracking (POP-6) asks, the page is not marked dirty by the draft
            const dialog = field.closest ? field.closest(".sw-dialog, [role='dialog'], [role='alertdialog']") : null;
            //  Opt in (Dirty: true / "restored"): the original pages never asked "Leave site?" (P3-A-4)
            const dirtyMode = dialog ? false : (o.Dirty === undefined ? false : o.Dirty);
            const dirtyKey = "draft:" + ctx;
            let timer = 0;
            /** @type {HTMLElement|null} The "Draft restored" line. */
            let notice = null;
            /** @type {HTMLElement|null} The "You have an earlier draft" line. */
            let keptNotice = null;
            let restoredText = null;
            let disposed = false;
            /** @type {boolean} The stored draft was not looked at yet: nothing is saved over it until then. */
            let restoring = true;
            /** @type {boolean} The user typed while restoring (saved once the restore decided). */
            let typedEarly = false;
            /** @type {boolean} The value is being set by the restore (not a user edit). */
            let applying = false;
            /** @type {string|null} The user part the draft was read with. */
            let readWith = null;
            /** @type {string|null} The text of the stored draft this field restored or wrote last: the only one it may overwrite. */
            let own = null;
            const dirtyText = _TF("Your unsent text is kept as a draft on this device.", "Text of the question 'Leave without saving?' when a field holds text that was not sent yet (it stays saved as a draft)");
            /** The storage key with the user part the draft was read with. */
            const key = () => keyOf(ctx, readWith || knownUser() || "anon");
            /** Update the dirty mark of the page. */
            const markDirty = () => {
                if (!Sw.Page || !Sw.Page.SetDirty || disposed)
                    return;
                const v = field.value || "";
                const on = dirtyMode === true ? !!v.trim() : (dirtyMode === "restored" ? (restoredText !== null && !!v.trim()) : false);
                Sw.Page.SetDirty(dirtyKey, on ? dirtyText : null, { Draft: true });
            };
            /** Remove the "Draft restored" line. */
            const dropNotice = () => {
                if (notice) {
                    notice.remove();
                    notice = null;
                }
            };
            /** Remove the "earlier draft" line. */
            const dropKept = () => {
                if (keptNotice) {
                    keptNotice.remove();
                    keptNotice = null;
                }
            };
            /**
             * Write the field's value as the stored draft: a stored draft this field did not restore or write (another tab, an earlier
             * page that typed first) is kept aside first and offered, so no text is ever overwritten.
             */
            const write = at => {
                if (disposed || !readWith)
                    return;
                const k = key();
                const v = field.value || "";
                const cur = readEntry(k);
                if (cur && cur.T !== own && cur.T !== v && keep(k, cur))
                    offerKept();
                if (v.trim() && looksSecret(v))
                    return;
                own = v.trim() ? v : null;
                //  A restored earlier draft keeps its saved time (a restore is not a new save)
                if (at && v.trim() && bytes(v) <= MaxBytes)
                    Sw.Storage.Local.SetJson(k, { T: v, S: at });
                else
                    Sw.Draft.Set(ctx, v);
            };
            /** @type {boolean|null} Lock(bool) of the caller (a control that knows it is locked), null = read the field. */
            let forcedLock = null;
            /**
             * The field can't be changed by the user (disabled, read only, inert, aria-disabled / aria-readonly: ex: a sent, locked
             * form): no draft is restored into it and no Restore is offered (a restore would change a locked value).
             * @returns {boolean} True when locked.
             */
            const locked = () => {
                if (forcedLock !== null)
                    return forcedLock;
                try {
                    return !!(field.disabled || field.readOnly || field.getAttribute("aria-disabled") === "true" || field.getAttribute("aria-readonly") === "true"
                        || (field.closest && field.closest("[inert], fieldset[disabled], [aria-disabled='true']")));
                }
                catch {
                    return false;
                }
            };
            /** Save the current value (debounced by the input handler); never over a draft that was not restored yet. */
            const save = () => {
                timer = 0;
                if (disposed)
                    return;
                if (restoring) {
                    typedEarly = true;
                    return;
                }
                write();
            };
            const onInput = () => {
                if (applying)
                    return;
                clearTimeout(timer);
                timer = setTimeout(save, DebounceMs);
                if (restoring)
                    typedEarly = true;
                //  An edit ends the "restored" state of the notice (the text is still drafted)
                if (notice && restoredText !== null && field.value !== restoredText)
                    dropNotice();
                markDirty();
            };
            field.addEventListener("input", onInput);
            /** Set the field's value as a user edit would (components listen to input); not saved again (the time stays). */
            const setValue = v => {
                applying = true;
                try {
                    field.value = v;
                    field.dispatchEvent(new Event("input", { bubbles: true }));
                }
                finally {
                    applying = false;
                }
            };
            /** The element the notices go after: the option, else the component's field row (after its hint), else the field. */
            const anchor = () => {
                if (o.Notice)
                    return o.Notice;
                let a = (comp && (comp.Field || comp.E)) || (field.closest && field.closest(".sw-text-field")) || field;
                //  The hint belongs to its field: the notice goes after it (field, hint, notice)
                while (a.nextElementSibling && a.nextElementSibling.matches(".sw-field-hint"))
                    a = a.nextElementSibling;
                return a;
            };
            /**
             * Build a notice line (role="status") with its buttons (and other controls), announced unless quiet.
             * @param {string} msg The text.
             * @param {Array<object|HTMLElement>} parts The Sw.Buttons (or elements).
             * @param {boolean} [quiet] Not announced (the caller announces one combined sentence).
             * @returns {HTMLElement|null} The line (null without the ui library).
             */
            const makeNotice = (msg, parts, quiet) => {
                if (!quiet && Sw.Page && Sw.Page.Announce)
                    Sw.Page.Announce(msg);
                if (!Sw.el || !parts.length)
                    return null;
                //  A group, not a live region: the one announcement above says it (a role="status" line was read a second time)
                return Sw.el("div.sw-draft-notice", { attrs: { role: "group", "aria-label": msg } },
                    Sw.el("span.sw-draft-notice-text.sw-selectable", { text: msg, attrs: { "aria-hidden": "true" } }), ...parts.map(b => b && b.nodeType ? b : b.E));
            };
            /**
             * Put a notice line in place: the "Draft restored" line first, the "earlier draft" line after it.
             * @param {HTMLElement} line The line.
             * @param {boolean} isKept It is the "earlier draft" line.
             */
            const place = (line, isKept) => {
                const after = isKept && notice && notice.isConnected ? notice : anchor();
                if (after.parentNode)
                    after.parentNode.insertBefore(line, after.nextSibling);
            };
            /**
             * The start of a draft's text for a notice or the picker (one line, at most 60 characters).
             * @param {string} t The text.
             * @returns {string} The preview.
             */
            const preview = t => {
                const s = String(t || "").replace(/\s+/g, " ").trim();
                return s.length > 60 ? s.substring(0, 59).trimEnd() + "…" : s;
            };
            /**
             * Show "Draft restored (saved 10:42)" with "Discard draft".
             * @param {Date} saved When it was saved.
             * @param {boolean} [quiet] Not announced (restoreWith announces once).
             * @returns {string} The text of the line.
             */
            const showRestored = (saved, quiet) => {
                dropNotice();
                const discard = Sw.Button ? new Sw.Button({
                    Text: _TF("Discard draft", "Button next to the 'Draft restored' line under a text field: removes the restored text"),
                    Title: _TF("Remove the restored text from the field (Undo is offered)", "Tool tip of the 'Discard draft' button"),
                    Kind: "ghost", Small: true, Icon: "trash",
                    OnClick: () => {
                        if (locked())
                            return;
                        const keepText = field.value;
                        dropNotice();
                        restoredText = null;
                        setValue("");
                        own = null;
                        Sw.Draft.Remove(ctx);
                        markDirty();
                        field.focus({ preventScroll: true });
                        const undo = () => {
                            setValue(keepText);
                            restoredText = keepText;
                            write();
                            markDirty();
                            field.focus({ preventScroll: true });
                        };
                        const done = _TF("Draft discarded", "Toast after the user discarded a restored draft (Undo puts the text back)");
                        if (Sw.Toast)
                            Sw.Toast(done, { Action: { Text: _TF("Undo", "Button of the toast 'Draft discarded': puts the text back"), OnClick: undo } });
                        else if (Sw.Page && Sw.Page.Announce)
                            Sw.Page.Announce(done);
                    },
                }) : null;
                const msg = savedText(saved);
                notice = makeNotice(msg, discard ? [discard] : [], quiet);
                if (notice)
                    place(notice, false);
                return msg;
            };
            /**
             * Offer the kept earlier drafts: one: "You have an earlier draft (saved 10:42): "the text…"" with "Restore draft" (a swap:
             * the text in the field is kept in its place, its saved time too) and "Discard earlier draft" (Undo); several: "You have 5
             * earlier drafts" with a picker of every kept draft (time + the start of its text) and the same two buttons for the chosen
             * one. Drafts removed for lack of room are said once. Nothing when none is kept or the field is locked (disabled / read only:
             * a restore would change a locked value).
             * @param {boolean} [quiet] Not announced (restoreWith announces once).
             * @returns {string} The text of the line ("" when none).
             */
            function offerKept(quiet) {
                dropKept();
                if (disposed || !readWith || locked())
                    return "";
                const k = key();
                const list = readKept(k).filter(x => x.T !== (field.value || ""));
                if (!list.length)
                    return "";
                const dropped = keptDropped(k, true);
                const when = x => whenText(new Date(x.S));
                let msg = list.length === 1
                    ? _T("You have an earlier draft (saved {0}): \"{1}\"", when(list[0]), preview(list[0].T), "Line under a text field when an earlier draft of the field is kept (the user typed before it could be restored, or in another tab).{0} is replaced with the time it was saved, ex: 10:42, {1} with the start of its text")
                    : _T("You have {0} earlier drafts (the newest saved {1}). Choose one to restore or discard.", list.length, when(list[0]), "Line under a text field when several earlier drafts of the field are kept (a list to choose from follows).{0} is replaced with the number, {1} with the time the newest was saved, ex: 10:42");
                if (dropped)
                    msg += " " + (dropped === 1
                        ? _TF("1 older draft was removed: there was no more room for drafts.", "Added to the earlier drafts line when the oldest kept draft had to be removed because the space for drafts was full")
                        : _T("{0} older drafts were removed: there was no more room for drafts.", dropped, "Added to the earlier drafts line when the oldest kept drafts had to be removed because the space for drafts was full.{0} is replaced with the number"));
                if (!Sw.Button) {
                    if (!quiet && Sw.Page && Sw.Page.Announce)
                        Sw.Page.Announce(msg);
                    return msg;
                }
                /**
                 * The picker (several kept drafts): the framework's Sw.Select (its list fits a phone, 44 px rows on touch, the same look
                 * as every select; the button never grows wider than the field), else a styled native select (no ui library).
                 * @type {HTMLElement|null}
                 */
                let pick = null;
                let picked = 0;
                const pickName = _TF("Earlier draft", "Accessible name of the list of earlier drafts under a text field (choose the one to restore or discard)");
                if (list.length > 1 && Sw.Select) {
                    const sel = new Sw.Select({
                        Options: list.map((x, i) => ({ Value: i, Label: when(x) + " · " + preview(x.T) })), Value: 0,
                        AriaLabel: pickName, Title: pickName, Dirty: false,
                        OnChange: v => {
                            picked = +v || 0;
                        },
                    });
                    pick = sel.E;
                    pick.classList.add("sw-draft-pick");
                }
                else if (list.length > 1 && Sw.el) {
                    pick = Sw.el("select.sw-draft-pick.sw-draft-pick-native", { attrs: { "aria-label": pickName } },
                        ...list.map((x, i) => Sw.el("option", { attrs: { value: String(i) }, text: when(x) + " · " + preview(x.T) })));
                    pick.addEventListener("change", () => {
                        picked = +pick.value || 0;
                    });
                }
                /** @returns {{T: string, S: number}} The chosen kept draft. */
                const chosen = () => list[picked] || list[0];
                const restore = new Sw.Button({
                    Text: _TF("Restore draft", "Button next to the 'You have an earlier draft' line: puts the earlier draft in the field (the text typed now is kept as an earlier draft)"),
                    Title: _TF("Put the earlier draft in the field; the text in the field now is kept as an earlier draft", "Tool tip of the 'Restore draft' button"),
                    Kind: "ghost", Small: true, Icon: "history",
                    OnClick: () => {
                        //  A locked field (a sent form) is never changed by a restore
                        if (locked()) {
                            dropKept();
                            return;
                        }
                        const d = chosen();
                        const now = field.value || "";
                        //  A swap: the earlier draft goes into the field, the text typed now is kept with its saved time (nothing is lost)
                        const rest = readKept(k).filter(x => x.T !== d.T);
                        if (now.trim() && !rest.some(x => x.T === now)) {
                            const cur = readEntry(k);
                            rest.push({ T: now, S: cur && cur.T === now ? cur.S : Date.now() });
                        }
                        writeKept(k, rest);
                        //  The "Draft restored" line spoke of the text that is now kept aside
                        dropNotice();
                        restoredText = d.T;
                        setValue(d.T);
                        write(d.S);
                        markDirty();
                        offerKept(true);
                        field.focus({ preventScroll: true });
                        const text = now.trim()
                            ? _TF("Earlier draft restored. The text you had typed is kept as an earlier draft.", "Message after the user restored an earlier draft into a field that held other text (that text is kept)")
                            : _TF("Earlier draft restored", "Message after the user restored an earlier draft into an empty field");
                        if (Sw.Page && Sw.Page.Announce)
                            Sw.Page.Announce(text);
                    },
                });
                const discard = new Sw.Button({
                    Text: _TF("Discard earlier draft", "Button next to the 'You have an earlier draft' line: removes that earlier draft, not the text in the field (Undo is offered)"),
                    Title: _TF("Remove the earlier draft; the text in the field stays (Undo is offered)", "Tool tip of the 'Discard earlier draft' button next to 'You have an earlier draft'"),
                    Kind: "ghost", Small: true, Icon: "trash",
                    OnClick: () => {
                        const d = chosen();
                        writeKept(k, readKept(k).filter(x => x.T !== d.T));
                        offerKept(true);
                        field.focus({ preventScroll: true });
                        const done = _TF("Earlier draft discarded", "Toast after the user discarded an earlier draft (Undo keeps it again)");
                        const undo = () => {
                            keep(k, d);
                            offerKept();
                        };
                        if (Sw.Toast)
                            Sw.Toast(done, { Action: { Text: _TF("Undo", "Button of the toast 'Earlier draft discarded': keeps the draft again"), OnClick: undo } });
                        else if (Sw.Page && Sw.Page.Announce)
                            Sw.Page.Announce(done);
                    },
                });
                keptNotice = makeNotice(msg, pick ? [pick, restore, discard] : [restore, discard], quiet);
                if (keptNotice)
                    place(keptNotice, true);
                return msg;
            }
            /**
             * Read the stored draft with a user part and restore it (or keep it aside when the user typed first). A locked field
             * (disabled / read only) gets nothing: the stored draft stays for when it can be edited. One announcement for both lines.
             * @param {string} u The user part.
             * @returns {boolean} True when it was restored into the field.
             */
            const restoreWith = u => {
                readWith = u;
                prune();
                const k = keyOf(ctx, u);
                const d = readEntry(k);
                const typed = !!(field.value && field.value.trim());
                const say = (...t) => {
                    const s = t.filter(Boolean).join(" ");
                    if (s && Sw.Page && Sw.Page.Announce)
                        Sw.Page.Announce(s);
                };
                if (locked()) {
                    own = d ? d.T : null;
                    return false;
                }
                if (d && typed && d.T !== field.value) {
                    //  Typed first: the stored draft is kept (never overwritten) and offered
                    keep(k, d);
                    own = d.T;
                    offerKept();
                    return false;
                }
                if (!d || typed) {
                    own = d ? d.T : null;
                    offerKept();
                    return false;
                }
                own = d.T;
                restoredText = d.T;
                setValue(d.T);
                say(showRestored(new Date(d.S), true), offerKept(true));
                markDirty();
                if (o.OnRestore)
                    o.OnRestore(d.T);
                return true;
            };
            /** The restore decided: what was typed meanwhile is saved now (the stored draft was kept aside first). */
            const decided = () => {
                restoring = false;
                if (typedEarly && !disposed) {
                    typedEarly = false;
                    clearTimeout(timer);
                    timer = 0;
                    write();
                }
            };
            let result = false;
            //  Synchronous restore when the user is known in this tab (the app shell tells it): the text is back before any input is
            //  possible, so typing on a slow network can't race the restore
            const known = knownUser();
            try {
                if (known)
                    result = restoreWith(known);
            }
            catch (e) {
                devWarn("Sw.Draft: restore failed: " + (e && e.message), "draft.restore." + ctx);
            }
            const ready = (async () => {
                try {
                    //  (a page opened on its own with nothing stored for this field: no user lookup now, the first save looks it up;
                    //  attaching a field sends no request, as the original pages)
                    if (!userFree() && !anyStored(ctx))
                        return false;
                    const u = await user();
                    if (disposed)
                        return false;
                    if (u !== readWith) {
                        //  Another user than the tab remembered (a sign in in between): take back what was restored for the old one
                        if (readWith !== null && restoredText !== null && field.value === restoredText) {
                            restoredText = null;
                            setValue("");
                            dropNotice();
                            markDirty();
                        }
                        dropKept();
                        own = null;
                        result = restoreWith(u);
                    }
                    return result;
                }
                catch (e) {
                    devWarn("Sw.Draft: restore failed: " + (e && e.message), "draft.restore." + ctx);
                    return false;
                }
                finally {
                    decided();
                }
            })();
            const h = {
                Field: field,
                Context: ctx,
                Active: true,
                Ready: ready,
                /** Save now when typing is pending (the page is left). */
                Flush() {
                    if (timer) {
                        clearTimeout(timer);
                        save();
                    }
                },
                /**
                 * The text of THIS field was sent or saved (or discarded on purpose): remove its draft and the line; the page is no
                 * longer dirty from it. Only what this field wrote is removed: a newer text that another tab typed into the same draft
                 * after it (the store holds another text than this field's) is kept as an earlier draft and offered, never erased
                 * (SAF-11 "drafts never lose text"). The earlier drafts kept for the field stay offered unless opts.All (a form sent as a
                 * whole: Sw.Draft.Sent, a dialog's Discard): then they go too (another tab's newer text is still kept).
                 * Cleared only after the save SUCCEEDED: while a save of the field's form / card / dialog runs (Sw.Work.Current of the ui
                 * library: Clear called before or after the request in the click's work), the removal waits for its outcome and happens
                 * only on success; a failed or stopped save keeps the draft (and the page's leave question). The returned promise does
                 * not wait for that outcome (the work itself may await Clear).
                 * @param {{All: boolean}} [opts] All (default false): the kept earlier drafts too.
                 */
                async Clear(opts) {
                    if (Sw.DevOptions)
                        Sw.DevOptions("Sw.Draft handle.Clear", opts, { All: "boolean" });
                    //  What this field sent: its value now (a page clears the field after the send) or what it stored last
                    const sent = field.value || "";
                    const mine = own;
                    //  (looked up before any await: the synchronous part of a click's work is its scope)
                    let pending = null;
                    try {
                        pending = Sw.Work && typeof Sw.Work.Current === "function" ? Sw.Work.Current(field, { Then: true }) : null;
                    }
                    catch {
                        pending = null;
                    }
                    if (pending && typeof pending.then === "function") {
                        Promise.resolve(pending).then(ok => {
                            if (ok === true && !disposed)
                                h.ClearNow(opts, sent, mine);
                        }, () => null);
                        return;
                    }
                    await h.ClearNow(opts, sent, mine);
                },
                /**
                 * Remove the draft now (internal: Clear, after the save succeeded).
                 * @param {{All: boolean}} [opts] All: the kept earlier drafts too.
                 * @param {string} sent The text that was sent.
                 * @param {string|null} mine The stored text this field wrote last when Clear was called.
                 * @returns {Promise<void>}
                 */
                async ClearNow(opts, sent, mine) {
                    const all = !!(opts && opts.All === true);
                    clearTimeout(timer);
                    timer = 0;
                    typedEarly = false;
                    dropNotice();
                    if (all)
                        dropKept();
                    restoredText = null;
                    if (Sw.Page && Sw.Page.ClearDirty)
                        Sw.Page.ClearDirty(dirtyKey);
                    await ready.catch(() => false);
                    const k = key();
                    //  Another tab's text in the store (neither what this field wrote nor what it sent) is kept, not removed with the sent one
                    const cur = readEntry(k);
                    const other = cur && cur.T !== mine && cur.T !== sent ? cur : null;
                    own = null;
                    await Sw.Draft.Remove(ctx, { All: all });
                    if (other)
                        keep(k, other);
                    if (!disposed && (!all || other))
                        offerKept();
                },
                /**
                 * The field is locked (true: disabled / read only, ex: a sent form) or editable again (false); null = read the field's
                 * attributes again. A locked field shows no draft lines and offers no Restore (a restore would change a locked value).
                 * Sw.TextBox calls it when it becomes disabled / read only (the field's own attributes are watched too).
                 * @param {boolean|null} on Locked.
                 */
                Lock(on) {
                    forcedLock = on === null || on === undefined ? null : !!on;
                    if (disposed)
                        return;
                    if (locked()) {
                        dropNotice();
                        dropKept();
                    }
                    else if (!keptNotice)
                        offerKept(true);
                },
                /** The subject was deleted (RemoveFor): drop the notices and the restored state (the stored drafts are removed by the caller). */
                Forget() {
                    clearTimeout(timer);
                    timer = 0;
                    typedEarly = false;
                    dropNotice();
                    dropKept();
                    restoredText = null;
                    own = null;
                    if (Sw.Page && Sw.Page.ClearDirty)
                        Sw.Page.ClearDirty(dirtyKey);
                },
                /** Stop drafting (the stored draft stays); also done by itself when the field leaves the document (a closed dialog). */
                Dispose() {
                    if (disposed)
                        return;
                    h.Flush();
                    disposed = true;
                    clearTimeout(timer);
                    attached.delete(h);
                    field.removeEventListener("input", onInput);
                    if (lockWatch)
                        lockWatch.disconnect();
                    if (field.SwDraft === h)
                        field.SwDraft = null;
                    dropNotice();
                    dropKept();
                    if (Sw.Page && Sw.Page.ClearDirty)
                        Sw.Page.ClearDirty(dirtyKey);
                },
            };
            attached.add(h);
            //  A field that becomes locked (a sent form: disabled / read only) offers no Restore; unlocked again: the offer comes back
            const lockWatch = typeof MutationObserver === "function" ? new MutationObserver(() => {
                if (disposed)
                    return;
                if (locked()) {
                    dropNotice();
                    dropKept();
                }
                else if (!keptNotice)
                    offerKept(true);
            }) : null;
            if (lockWatch)
                lockWatch.observe(field, { attributes: true, attributeFilter: ["disabled", "readonly", "aria-disabled", "aria-readonly"] });
            //  The field knows its draft (a Sw control then doesn't count the restore as an edit of its own: one leave sentence)
            field.SwDraft = h;
            if (comp && !comp.DraftH)
                comp.DraftH = h;
            //  In a Sw.Dialog: Discard throws the draft away, any close stops drafting (the field's listeners go with the dialog)
            try {
                const dlg = Sw.Dialog && typeof Sw.Dialog.Of === "function" ? Sw.Dialog.Of(field) : null;
                if (dlg && typeof dlg.OnClose === "function")
                    dlg.OnClose(r => {
                        if (r && r.Discarded)
                            h.Clear({ All: true });
                        h.Dispose();
                    });
                if (dlg && result && typeof dlg.Recheck === "function")
                    dlg.Recheck();
            }
            catch {
            }
            if (!sweepTimer)
                sweepTimer = setTimeout(sweep, 2000);
            return h;
        },
    };

    //  The option schemas (Sw.DevOptions registry: checked in developer mode, listed by the walker of core/contracttest.html)
    if (Sw.DevOptions && Sw.DevOptions.Register) {
        Sw.DevOptions.Register("Sw.Draft.Attach", { Context: ["string", "number"], Notice: "element", Dirty: "boolean|string", OnRestore: "function", Active: "boolean" });
        Sw.DevOptions.Register("Sw.Draft.Remove", { All: "boolean" });
    }
})();
