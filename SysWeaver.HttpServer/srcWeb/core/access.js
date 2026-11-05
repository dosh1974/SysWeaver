/**
 * @file core/access.js - what the signed in user may do, and the shared "not allowed" state.
 *
 * Purpose: `Sw.Access`
 *  - User(reload) / Tokens(): the signed in user (`Api/auth/GetUser`, null when not signed in or without an auth service) and
 *    its lower cased tokens, cached for 30 s.
 *  - Check(auth, tokens): the server's rule for an auth text (ApiInfo.Auth, WebApiAuth): null / undefined = open, "" = any
 *    signed in user, "-" = no one, else comma separated tokens of which the user needs at least one (case insensitive).
 *  - Can(what): resolves true / false for an ApiInfo ({Auth}), an auth text or a list of required tokens, for the signed in user.
 *  - ApiInfo(url) / CanCall(url): the auth of a server method ({Uri, Auth, Allowed}; null when the server can't tell) and
 *    whether the user may call it (true / false, null = unknown). Asked with `Api/auth/CanCall` (the urls asked in
 *    the same task go in one request, answers cached 60 s; Allowed is the server's answer for the session).
 *    Older servers without CanCall: `Api/debug/GetApiInfo` (LegacyApiInfo; restricted to Dev and Debug on newer servers): when
 *    the server refuses it (403, a sign in redirect, 401, 404) Sw.Access asks once per page (concurrent callers wait for
 *    the first answer), remembers the refusal for the user in the browser session (sessionStorage
 *    "SysWeaver.Access.NoApiInfo") and degrades: CanCall is null, Guard disables after the first refused request, the texts
 *    name "your role". HasApiInfo() tells which mode is used.
 *  - RoleText(auth) ("Admin or Ops"; Debug is left out unless it is the only token) and Requires(auth) ("Requires Admin").
 *  - IsDenied(error): a refused request (a Sw.Net.Error with Status 403, or Denied set: a sign in redirect while signed in).
 *  - Denied(info): emits Sw.Events "access.denied" {Url, Error} (Guard disables its targets on it).
 *  - Guard(target, opts): disable (or hide) a button / element the user may not use, with the tool tip "Requires Admin".
 *    The required tokens come from opts.Auth, or from the api info of opts.Url; when they are unknown the target is
 *    disabled after the first refused request ("access.denied") of that url (or of any url without opts.Url).
 *  - Disable(target, text, hide): disable (or hide) a target now with a reason as tool tip (class sw-denied-action).
 *  - ActionDenied(error, target): a refused action (ex: a button click). A ROLE refusal (IsRoleDenied: the server's
 *    "X-Sw-Denied: role" signal, e.RoleDenied, or a 403 without a reason): a toast with the server's sentence (else the needed
 *    role) and the target disabled with it as the reason; a refusal of this request with a reason and no role signal: the reason,
 *    the target stays usable.
 *  - NotAllowed(opts) -> element: the shared "not allowed" state: a lock icon, "You don't have access to this page" (a part
 *    of a page: "... to this part of the page"; with opts.Name: "You don't have access to Users"), the signed in user and the
 *    role needed (or "your role" when unknown), a link Home (page scope), and no "Try again" (a refusal is not transient).
 *    The details are filled in asynchronously, only from what is already known (no GetUser, no CanCall: the original sent
 *    nothing more on a refusal): an unknown user leaves its line out.
 *  - NotFound(opts) -> element: the shared "not found" state: a search icon, "Not found" / "Services was not found", the
 *    missing url, an optional hint and links (default Home), no "Try again" (a missing page or table is not transient).
 * `Sw.Page.ShowNotAllowed(opts)` / `Sw.Page.ShowNotFound(opts)` show these states in the page or in opts.Element (used
 * automatically by Sw.Page.Main for a refused request, and by the table, chart, api and text pages); with a Name a page
 * scope state also names the page (Sw.Page.SetName: title and app shell title, not the generic "Table"). Styles:
 * ui/components.css (`.sw-denied*`); without the ui css the states are plain, readable html.
 *
 * Load order: after core/page.js. Dependencies: Sw.Net, Sw.Url, Sw.Events, Sw.Page; Sw.Icon, Sw.Fail (ui, optional, at call time).
 * Globals defined: none (Sw.Access, Sw.Page.ShowNotAllowed).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** @type {number} Milliseconds the signed in user is re-used. */
    const userLifeMs = 30000;

    /** @type {Promise<object|null>|null} The pending / cached user request. */
    let userP = null;

    /** @type {number} Time (Date.now) the cached user expires. */
    let userExp = 0;

    /** @type {?Promise<?object>} The user the app shell got at its load (SeedUser; frames use it, CachedUser). */
    let seedP = null;

    /** @type {string} sessionStorage key: {root, user} of a site whose GetApiInfo refused that user (not asked again). */
    const noInfoKey = "SysWeaver.Access.NoApiInfo";

    /** @type {boolean} GetApiInfo was refused for the signed in user (Sw.Access.ApiInfo returns null without asking). */
    let infoBlocked = false;

    /** @type {boolean} GetApiInfo answered once (it can be used). */
    let infoKnown = false;

    /** @type {Promise<boolean>|null} Pending / done check of the session storage flag (noInfoKey). */
    let infoStoredP = null;

    /** @type {Promise<void>|null} The first pending GetApiInfo request (concurrent callers wait for it). */
    let infoProbe = null;

    /** @type {Set<string>} Paths (apiPath, "" for an unknown url) refused on this page ("access.denied"); late Guards use it. */
    const deniedPaths = new Set();

    /** @type {boolean|null} The server has `Api/auth/CanCall` (true), hasn't (false: an older server, GetApiInfo is used), or not known yet (null). */
    let canCallApi = null;

    /** @type {number} Milliseconds an answer of Api/auth/CanCall is re-used. */
    const permLifeMs = 60000;

    /** @type {number} Max urls asked in one Api/auth/CanCall request (the server allows 128). */
    const maxBatch = 100;

    /** @type {Map<string, {Exp:number, Value:Promise<object|null>}>} apiPath -> pending / cached CanCall answer ({Url, Found, Allowed, Auth}). */
    const perms = new Map();

    /** @type {{Paths:string[], Done:Promise<Array<object>>}|null} The batch being collected (sent after the current task). */
    let batch = null;

    /**
     * Ask the server if the signed in user may call a method (`Api/auth/CanCall`). The urls asked in the same task are sent in
     * one request; the answers are cached 60 s. Rejects when the request fails (a 404 marks the api as missing: older server).
     * @param {string} p - The method path (apiPath).
     * @returns {Promise<{Url:string, Found:boolean, Allowed:boolean, Auth:string|null}|null>}
     */
    function askPermission(p) {
        const now = Date.now();
        const c = perms.get(p);
        if (c && now < c.Exp)
            return c.Value;
        if (!batch || batch.Paths.length >= maxBatch) {
            const b = batch = { Paths: [], Done: null };
            b.Done = new Promise(r => setTimeout(r, 0)).then(() => {
                if (batch === b)
                    batch = null;
                return Sw.Net.Send(Sw.Url.Root + "Api/auth/CanCall", b.Paths, { NoNavigate: true });
            });
        }
        const b = batch;
        const i = b.Paths.push(p) - 1;
        const v = b.Done.then(list => {
            canCallApi = true;
            return (Array.isArray(list) && list[i]) || null;
        }, e => {
            if (e && e.Status === 404)
                canCallApi = false;
            perms.delete(p);
            throw e;
        });
        perms.set(p, { Exp: now + permLifeMs, Value: v });
        //  Not unhandled when no one waits (the caller gets the same promise)
        v.catch(() => { });
        return v;
    }

    /**
     * The signed in user if this page or the app shell above it already asked (no request), else null.
     * @returns {?Promise<?object>} The user promise, or null when not known.
     */
    function knownUser() {
        try {
            const own = Sw.Access.CachedUser();
            if (own)
                return own;
            const ta = window.top !== window ? window.top.Sw && window.top.Sw.Access : null;
            return ta && ta !== Sw.Access && typeof ta.CachedUser === "function" ? ta.CachedUser() : null;
        }
        catch {
            //  Another origin above
            return null;
        }
    }

    /**
     * The name of a user for the session flag.
     * @param {object|null} u - The user.
     * @returns {string}
     */
    function userKey(u) {
        return u ? String(u.Username || u.NickName || "?") : "";
    }

    /**
     * Was GetApiInfo refused for the signed in user (now, or earlier in this browser session on this site)?
     * @returns {Promise<boolean>}
     */
    async function infoOff() {
        if (infoBlocked)
            return true;
        if (!infoStoredP) {
            infoStoredP = (async () => {
                const st = Sw.Storage && Sw.Storage.Session;
                const v = st ? st.GetJson(noInfoKey) : null;
                if (!v || v.root !== Sw.Url.Root)
                    return false;
                return v.user === userKey(await Sw.Access.User());
            })().catch(() => false);
        }
        if (await infoStoredP)
            infoBlocked = true;
        return infoBlocked;
    }

    /**
     * Is a GetApiInfo failure a refusal (the server doesn't let this user use it, or has no type service), not a transient error?
     * @param {*} e - The error.
     * @returns {boolean}
     */
    function infoRefused(e) {
        if (!e || typeof e !== "object")
            return false;
        return e.Denied === true || e.Redirect === true || e.Status === 401 || e.Status === 403 || e.Status === 404 || e.Status === 302;
    }

    /**
     * Remember that GetApiInfo is refused for the signed in user (this page at once, the browser session for the site).
     */
    function setInfoOff() {
        infoBlocked = true;
        Sw.Access.User().then(u => {
            const st = Sw.Storage && Sw.Storage.Session;
            if (st)
                st.SetJson(noInfoKey, { root: Sw.Url.Root, user: userKey(u) });
        }).catch(() => { });
    }

    /**
     * Split an auth text (or a token list) into lower cased tokens.
     * @param {string|string[]} auth - Comma separated tokens or a list.
     * @returns {string[]} Tokens (empty for "" = any signed in user).
     */
    function tokensOf(auth) {
        const list = Array.isArray(auth) ? auth : String(auth).split(",");
        return list.map(x => String(x).trim().toLowerCase()).filter(x => x.length > 0);
    }

    /**
     * The auth text of what Can / Guard got: an ApiInfo ({Auth}), an auth text or a token list.
     * @param {*} what - ApiInfo, auth text, token list, null (open).
     * @returns {string|string[]|null} The auth (null = open).
     */
    function authOf(what) {
        if (what === null || typeof what === "undefined")
            return null;
        if (typeof what === "object" && !Array.isArray(what))
            return ("Auth" in what) ? what.Auth : null;
        return what;
    }

    /**
     * The path of a server method relative to the site root, as GetApiInfo expects it ("ServerManager/GetServerInfo",
     * "Api/logFile/DeleteLogFile"); the query is dropped.
     * @param {string} url - Absolute or relative (to this page) url.
     * @returns {string|null} The path, null for an url of another site.
     */
    function apiPath(url) {
        try {
            const u = new URL(String(url), window.location.href);
            const r = new URL(Sw.Url.Root, window.location.href);
            if (u.origin !== r.origin || !u.pathname.startsWith(r.pathname))
                return null;
            return decodeURIComponent(u.pathname.substring(r.pathname.length));
        }
        catch {
            return null;
        }
    }

    /**
     * Capitalize a token for display ("admin" -> "Admin").
     * @param {string} t - Token.
     * @returns {string}
     */
    function tokenName(t) {
        return t.charAt(0).toUpperCase() + t.substring(1);
    }

    /**
     * Disable (or hide) a target with a reason (tool tip and accessible description).
     * @param {*} target - A Sw component ({E, SetDisabled, SetTitle}) or an element.
     * @param {string} text - The reason ("Requires Admin").
     * @param {boolean} hide - Hide instead of disable.
     */
    function disableTarget(target, text, hide) {
        if (!target)
            return;
        const e = target.E || target;
        if (hide) {
            e.style.setProperty("display", "none", "important");
            return;
        }
        //  The reason ("Requires Admin") is the disabled reason too: shown and announced on an attempt (UX guide INP-12)
        if (typeof target.SetDisabled === "function")
            target.SetDisabled(true, text);
        else if (Sw.Disabled && typeof Sw.Disabled.Set === "function")
            Sw.Disabled.Set(e, true, text);
        else if ("disabled" in e)
            /* SwWeb:allow ux-inp-12 (fallback without the ui library: no Sw.Disabled to keep it focusable) */
            e.disabled = true;
        else
            e.setAttribute("aria-disabled", "true");
        if (typeof target.SetTitle === "function")
            target.SetTitle(text);
        else
            e.title = text;
        e.classList.add("sw-denied-action");
        e.dataset.swDenied = text;
    }

    /**
     * @namespace Sw.Access - what the signed in user may do, and the shared "not allowed" state (see the file header).
     */
    Sw.Access = {

        /**
         * The signed in user (Api/auth/GetUser, cached 30 s). Never throws.
         * @param {boolean} [reload] - Ask the server again (bypasses the caches).
         * @returns {Promise<object|null>} The user ({Username, NickName, Tokens, ...}), null when not signed in (or no auth service).
         */
        User(reload) {
            const now = Date.now();
            //  A page in the app shell (same origin) uses the user the shell just asked for (SeedUser), so a framed page sends no GetUser
            //  of its own (the original pages made none)
            if (!reload && (!userP || now >= userExp)) {
                try {
                    const ta = window.top !== window ? window.top.Sw && window.top.Sw.Access : null;
                    const p = ta && ta !== Sw.Access && typeof ta.CachedUser === "function" ? ta.CachedUser() : null;
                    if (p) {
                        userP = p;
                        userExp = now + userLifeMs;
                        return userP;
                    }
                }
                catch {
                    //  (another origin above: ask the server)
                }
            }
            if (reload || !userP || now >= userExp) {
                userExp = now + userLifeMs;
                userP = Sw.Net.Get(Sw.Url.Root + "Api/auth/GetUser", { NoNavigate: true, Reload: !!reload })
                    .then(u => (u && u.Succeeded) ? u : null)
                    .catch(() => null);
            }
            return userP;
        },

        /**
         * Remember a GetUser reply the page already has (the app shell's own request at its load), so Sw.Access.User in the shell's same
         * origin frames (CachedUser) needs no request: the shell reloads on every sign in / sign out, so its user stays right.
         * @param {?object|Promise<?object>} u The GetUser reply or its request (null: nobody signed in; a failed request: null).
         */
        SeedUser(u) {
            seedP = Promise.resolve(u).then(x => (x && x.Succeeded) ? x : null, () => null);
            userP = seedP;
            userExp = Date.now() + userLifeMs;
        },

        /**
         * The cached signed in user (User) while it is fresh, else null (no request).
         * @returns {?Promise<?object>} The user promise or null.
         */
        CachedUser() {
            return seedP || (userP && Date.now() < userExp ? userP : null);
        },

        /**
         * The tokens of the signed in user (lower case).
         * @returns {Promise<string[]|null>} Tokens, null when not signed in.
         */
        async Tokens() {
            const u = await Sw.Access.User();
            return u ? tokensOf(u.Tokens || []) : null;
        },

        /**
         * Test an auth requirement against tokens (the server's rule, see the file header).
         * @param {string|string[]|null} auth - Required: null = open, "" = any signed in user, "-" = no one, else any of the tokens.
         * @param {string[]|null} tokens - The user's tokens, null = not signed in.
         * @returns {boolean}
         */
        Check(auth, tokens) {
            if (auth === null || typeof auth === "undefined")
                return true;
            if (!Array.isArray(auth) && String(auth).trim() === "-")
                return false;
            if (!tokens)
                return false;
            const need = tokensOf(auth);
            if (need.length <= 0)
                return true;
            const have = tokensOf(tokens);
            return need.some(x => have.includes(x));
        },

        /**
         * May the signed in user use something?
         * @param {object|string|string[]|null} what - An ApiInfo ({Auth}), an auth text ("admin, ops") or a list of required tokens.
         * @returns {Promise<boolean>}
         */
        async Can(what) {
            const auth = authOf(what);
            if (auth === null || typeof auth === "undefined")
                return true;
            return Sw.Access.Check(auth, await Sw.Access.Tokens());
        },

        /**
         * The auth of a server method for the signed in user: `Api/auth/CanCall` (asked in batches, cached 60 s), on
         * older servers without it the api info of `Api/debug/GetApiInfo` (cached 60 s; refusals are remembered, see the file
         * header). Never throws. For the full api info (arguments, types) call Api/debug/GetApiInfo (Dev and Debug only).
         * @param {string} url - The method url (relative to this page or absolute; the query is ignored).
         * @returns {Promise<object|null>} {Uri, Auth, Allowed} (CanCall: Allowed is the server's answer for this user) or the
         * ApiInfo ({Uri, Auth, Desc, ...}: older servers), null when unknown (not an api, or the server can't tell).
         */
        async ApiInfo(url) {
            const p = apiPath(url);
            if (!p)
                return null;
            if (canCallApi !== false) {
                try {
                    const r = await askPermission(p);
                    return (r && r.Found) ? { Uri: p, Auth: r.Auth, Allowed: !!r.Allowed } : null;
                }
                catch {
                    //  A transient failure: unknown (asked again next time); a missing api (older server): GetApiInfo below
                    if (canCallApi !== false)
                        return null;
                }
            }
            return Sw.Access.LegacyApiInfo(url);
        },

        /**
         * The api info of a server method from `Api/debug/GetApiInfo` (the type service, cached 60 s; used by ApiInfo on servers
         * without Api/auth/CanCall). Never throws. GetApiInfo is restricted to Dev and Debug on newer servers: a refusal is asked
         * once per page and remembered for the user in the browser session (see the file header).
         * @param {string} url - The method url (relative to this page or absolute; the query is ignored).
         * @returns {Promise<object|null>} ApiInfo ({Uri, Auth, Desc, ...}), null when unknown.
         */
        async LegacyApiInfo(url) {
            const p = apiPath(url);
            if (!p)
                return null;
            //  GetApiInfo refused for this user (ex: locked down to Debug): don't ask again (no repeated 403s)
            if (await infoOff())
                return null;
            //  Until the first answer is in, concurrent callers wait for it (one refusal per page, not one per guard)
            while (!infoKnown && infoProbe) {
                await infoProbe;
                if (infoBlocked)
                    return null;
            }
            const req = Sw.Net.Cached("Sw.Access.ApiInfo", p, 60000,
                () => Sw.Net.Send(Sw.Url.Root + "Api/debug/GetApiInfo", p, { NoNavigate: true }));
            let probe = null;
            if (!infoKnown) {
                probe = infoProbe = req.then(() => {
                    infoKnown = true;
                }, e => {
                    if (infoRefused(e))
                        setInfoOff();
                }).finally(() => {
                    if (infoProbe === probe)
                        infoProbe = null;
                });
            }
            try {
                return (await req) || null;
            }
            catch {
                return null;
            }
        },

        /**
         * Can the requirements of server methods be known for the signed in user? True when the server has Api/auth/CanCall
         * (asked once). Older servers: can the api info (Api/debug/GetApiInfo) be used? When nothing asked yet, it asks
         * once (one GetApiInfo request, shared with concurrent callers). False once the server refused it (403, a sign in
         * redirect, 401, or 404 = no type service) for this user in this browser session (the page doesn't ask again); Guard,
         * NotAllowed and ActionDenied then use the generic texts ("your role") and Guard disables its target after the first
         * refused request of its url. Null when it can't tell (the request failed for another reason, ex: no connection; asked
         * again next time).
         * @returns {Promise<boolean|null>}
         */
        async HasApiInfo() {
            if (canCallApi === null)
                await Sw.Access.ApiInfo(Sw.Url.Root + "Api/auth/CanCall");
            if (canCallApi === true)
                return true;
            if (canCallApi === null)
                return null;
            if (await infoOff())
                return false;
            if (infoKnown)
                return true;
            //  Nothing asked yet (or still asking): ask once (the api info of GetApiInfo itself), so the answer is never a guess
            await Sw.Access.LegacyApiInfo(Sw.Url.Root + "Api/debug/GetApiInfo");
            if (infoBlocked)
                return false;
            return infoKnown ? true : null;
        },

        /**
         * May the signed in user call a server method?
         * @param {string} url - The method url.
         * @returns {Promise<boolean|null>} true / false, null when the requirement is unknown.
         */
        async CanCall(url) {
            const info = await Sw.Access.ApiInfo(url);
            if (!info)
                return null;
            //  The server's answer for this user (Api/auth/CanCall)
            if (typeof info.Allowed === "boolean")
                return info.Allowed;
            return Sw.Access.Can(info);
        },

        /**
         * The roles of an auth text for people: "Admin", "Admin or Ops" (Debug, the developer super role, is left out unless it is
         * the only token).
         * @param {string|string[]|null} auth - Auth text or tokens.
         * @returns {string} The role text, "" for none (open or any signed in user).
         */
        RoleText(auth) {
            if (auth === null || typeof auth === "undefined")
                return "";
            let list = tokensOf(auth);
            if (list.length > 1)
                list = list.filter(x => x !== "debug");
            list = [...new Set(list)].map(tokenName);
            if (list.length <= 0)
                return "";
            if (list.length === 1)
                return list[0];
            return _T("{0} or {1}", list.slice(0, -1).join(", "), list[list.length - 1], "A list of user roles where any one is enough, ex: 'Admin or Ops' or 'Admin, Ops or Dev'. {0} is replaced by the first role names (comma separated), {1} by the last role name");
        },

        /**
         * The reason text of a disabled action: "Requires Admin" (or "Requires a role you don't have" when unknown).
         * @param {string|string[]|null} [auth] - Auth text or tokens (unknown when missing).
         * @returns {string}
         */
        Requires(auth) {
            const r = Sw.Access.RoleText(auth);
            return r
                ? _T("Requires {0}", r, "Tool tip of a disabled button or action that the signed in user is not allowed to use. {0} is replaced by the role(s) needed, ex: Admin or Ops")
                : _TF("Your role doesn't allow this", "Tool tip of a disabled button or action that the signed in user is not allowed to use (the role needed is unknown)");
        },

        /**
         * Is an error a refused request (not allowed for this user)? A Sw.Net.Error with Status 403, or one marked Denied
         * (Sw.Net: the server answered with the sign in page although the user is signed in).
         * @param {*} e - The error.
         * @returns {boolean}
         */
        IsDenied(e) {
            return !!e && typeof e === "object" && (e.Denied === true || e.Status === 403);
        },

        /**
         * Report a refused request: Sw.Events "access.denied" {Url, Error} (Guards waiting for it disable their targets).
         * @param {{Url:string, Error:*}|Error} info - The info, or the error (its Url is used).
         */
        Denied(info) {
            const d = info instanceof Error ? { Url: info.Url || "", Error: info } : (info || {});
            try {
                Sw.Events.Emit("access.denied", { Url: d.Url || "", Error: d.Error || null });
            }
            catch {
            }
        },

        /**
         * Disable (or hide) a button / element the signed in user may not use, with the reason as tool tip ("Requires Admin").
         * The requirement comes from opts.Auth (an auth text, a token list or an ApiInfo) or from the api info of opts.Url. When it
         * is unknown (no type service) the target stays usable until the first refused request ("access.denied") of opts.Url
         * (any url when no Url is given).
         * @param {*} target - A Sw component (Sw.Button: {E, SetDisabled, SetTitle}) or an element.
         * @param {{Url:string, Auth:*, Hide:boolean}} opts - Url: the server method the target calls; Auth: the requirement
         * (wins over Url); Hide: hide instead of disable.
         * @returns {Promise<boolean|null>} true allowed, false refused (target disabled / hidden), null unknown (waiting for a refusal).
         */
        async Guard(target, opts) {
            opts = opts || {};
            Sw.DevOptions && Sw.DevOptions("Sw.Access.Guard", opts);
            const hasAuth = ("Auth" in opts) && typeof opts.Auth !== "undefined";
            const info = hasAuth ? null : (opts.Url ? await Sw.Access.ApiInfo(opts.Url) : null);
            const auth = hasAuth ? authOf(opts.Auth) : (info ? info.Auth : undefined);
            //  The server's answer for this user (Api/auth/CanCall)
            if (info && typeof info.Allowed === "boolean") {
                if (info.Allowed)
                    return true;
                disableTarget(target, Sw.Access.Requires(auth), !!opts.Hide);
                return false;
            }
            if (typeof auth !== "undefined") {
                if (await Sw.Access.Can(auth))
                    return true;
                disableTarget(target, Sw.Access.Requires(auth), !!opts.Hide);
                return false;
            }
            const want = opts.Url ? apiPath(opts.Url) : null;
            //  Refused already (ex: the page's table got its 403 while the api info was asked for): disable now
            if (want ? deniedPaths.has(want) : deniedPaths.size > 0) {
                disableTarget(target, Sw.Access.Requires(null), !!opts.Hide);
                return false;
            }
            const off = Sw.Events.On("access.denied", d => {
                if (want && apiPath(d && d.Url || "") !== want)
                    return;
                off();
                disableTarget(target, Sw.Access.Requires(null), !!opts.Hide);
            });
            return null;
        },

        /**
         * Disable (or hide) a button / element now with a reason as tool tip ("Requires Admin"); class sw-denied-action.
         * @param {*} target - A Sw component (Sw.Button) or an element (disabled, else aria-disabled).
         * @param {string} [text] - The reason (default Requires(null): "Your role doesn't allow this").
         * @param {boolean} [hide] - Hide instead of disable.
         */
        Disable(target, text, hide) {
            disableTarget(target, text || Sw.Access.Requires(null), !!hide);
        },

        /**
         * A refused action (ex: a button click got 403): a toast naming the needed role (when known) and the target disabled.
         * @param {*} e - The error (its Url gives the requirement).
         * @param {*} [target] - The Sw.Button / element that started it (disabled with the reason as tool tip).
         * @returns {Promise<void>}
         */
        async ActionDenied(e, target) {
            const reason = e && typeof e.Reason === "string" ? e.Reason : "";
            //  Two kinds of 403, two signals (never guessed from the text): RoleDenied (the server's "X-Sw-Denied: role", or a 403 without
            //  a reason) = the user's role doesn't allow it: the target is disabled with the reason (INP-12), whatever reason text the
            //  server sent; else a refusal of THIS request with its own reason (ex: "You can only invite users to your own domain …"): the
            //  reason is shown as it is, and the button stays usable (another input may be allowed)
            if (!Sw.Access.IsRoleDenied(e)) {
                const m = Sw.Net && Sw.Net.Message ? String(Sw.Net.Message("", e)) : reason;
                if (typeof Sw.Fail === "function")
                    Sw.Fail(m);
                else
                    console.warn(m);
                return;
            }
            const info = e && e.Url ? await Sw.Access.ApiInfo(e.Url) : null;
            const needs = Sw.Access.Requires(info ? info.Auth : null);
            //  The server's sentence is the reason when it gave one (translated, it may name the role), else "Requires Admin"
            const why = reason || needs;
            const text = reason || (_TF("You don't have permission to do this.", "Message shown when the signed in user tried an action that the server refused because the user's role doesn't allow it") + "\n" + needs);
            if (typeof Sw.Fail === "function")
                Sw.Fail(text);
            else
                console.warn(text);
            Sw.Page.Announce(text);
            if (target)
                disableTarget(target, why, false);
            Sw.Access.Denied({ Url: e && e.Url || "", Error: e });
        },

        /**
         * Is an error a refusal because of the user's ROLE (the server's "X-Sw-Denied: role" signal, e.RoleDenied; a 403 without a reason,
         * or a sign in redirect while signed in, of an older server)? Then the action is disabled (INP-12); a 403 with a reason and without
         * the role signal is about the request (the button stays usable).
         * @param {*} e The error.
         * @returns {boolean} True for a role refusal.
         */
        IsRoleDenied(e) {
            if (!Sw.Access.IsDenied(e))
                return false;
            if (typeof e.RoleDenied === "boolean")
                return e.RoleDenied;
            return !(typeof e.Reason === "string" && e.Reason);
        },

        /**
         * Create the shared "not allowed" state (see the file header). The user and role lines are filled in asynchronously.
         * The wording follows the scope: a whole page ("You don't have access to this page", the default without opts.Element)
         * or a part of a working page ("You don't have access to this part of the page", the default with opts.Element);
         * with a Name: "You don't have access to Users".
         * @param {{Url:string, Auth:*, Reason:string, Error:*, Name:string, Scope:string, Element:HTMLElement}} [opts] - Url: the
         * refused server method (its api info names the role), Auth: the requirement (wins over Url), Reason: an extra line, Error:
         * the refused request (its Url is used), Name: what is refused, for people ("Users", "the API details"), Scope: "page" or
         * "section" (default: "section" when an Element is given, else "page").
         * @returns {HTMLElement} The element (class sw-denied, role alert).
         */
        NotAllowed(opts) {
            opts = typeof opts === "string" ? { Reason: opts } : (opts || {});
            Sw.DevOptions && Sw.DevOptions("Sw.Access.NotAllowed", opts);
            const section = isSection(opts);
            const box = stateBox("lock", deniedTitle(opts), "alert");
            const who = mk("p", "sw-denied-text");
            const need = mk("p", "sw-denied-text");
            box.append(who, need);
            if (opts.Reason)
                box.append(mk("p", "sw-denied-reason", String(opts.Reason)));
            box.append(mk("p", "sw-denied-text", _TF("Ask an administrator if you need access.", "Hint on the message shown instead of a page that the signed in user is not allowed to see")));
            if (!section)
                box.append(homeLink());
            const url = opts.Url || (opts.Error && opts.Error.Url) || "";
            (async () => {
                const hasAuth = ("Auth" in opts) && typeof opts.Auth !== "undefined";
                //  Only what the page (or the app shell) already knows: the original refused page sent nothing more (P3-A-5), so no
                //  GetUser and no CanCall just to explain the refusal; an unknown user leaves its line out, an unknown role says so
                const known = knownUser();
                const kp = (!hasAuth && url) ? perms.get(apiPath(url) || "") : null;
                const [user, info] = await Promise.all([known, kp && Date.now() < kp.Exp ? kp.Value.catch(() => null) : null]);
                if (!known)
                    who.hidden = true;
                const auth = hasAuth ? authOf(opts.Auth) : (info && info.Found ? info.Auth : null);
                const name = user ? (user.NickName || user.Username || "") : "";
                who.textContent = user
                    ? _T("You are signed in as {0}.", name, "Line of the message shown instead of a page that the signed in user is not allowed to see. {0} is replaced by the user name")
                    : _TF("You are not signed in.", "Line of the message shown instead of a page when the user is not signed in");
                const roles = Sw.Access.RoleText(auth);
                need.textContent = roles
                    ? (section
                        ? _T("It requires the role {0}, which your account doesn't have.", roles, "Line of the message shown instead of a part of a page (ex: a table) that the signed in user is not allowed to see. {0} is replaced by the role(s) needed, ex: Admin or Ops")
                        : _T("This page requires the role {0}, which your account doesn't have.", roles, "Line of the message shown instead of a page that the signed in user is not allowed to see. {0} is replaced by the role(s) needed, ex: Admin or Ops"))
                    : _TF("Your role doesn't give access to it.", "Line of the message shown instead of a page that the signed in user is not allowed to see (the role needed is unknown)");
            })();
            return box;
        },

        /**
         * Create the shared "not found" state: a search icon, a heading ("Not found", "Services was not found"), what is missing
         * (the url), an optional hint (ex: the url rule of the page), and links (default: Go to Home for a page); no "Try again"
         * (a missing page or table is not transient).
         * @param {{Url:string, Name:string, Title:string, Text:string, Hint:string, Links:Array<{Text:string, Url:string, Icon:string}>, Scope:string, Element:HTMLElement}} [opts]
         * - Url: the missing address (named in the text), Name: what is missing, for people, Title / Text: replace the heading /
         * the first line, Hint: an extra line, Links: buttons (relative urls resolve against this page; in the app shell they open
         * in the content frame), Scope: "page" / "section" (as NotAllowed).
         * @returns {HTMLElement} The element (classes sw-denied sw-notfound, role status).
         */
        NotFound(opts) {
            opts = typeof opts === "string" ? { Text: opts } : (opts || {});
            Sw.DevOptions && Sw.DevOptions("Sw.Access.NotFound", opts);
            const name = opts.Name ? Sw.Page.LinkText(opts.Name, 120) : "";
            const title = opts.Title || (name
                ? _T("{0} was not found", name, "Heading of the message shown instead of a page or table that does not exist on the server. {0} is replaced by its name, ex: Services")
                : _TF("Not found", "Heading of the message shown instead of a page or table that does not exist on the server"));
            const box = stateBox("search", title, "status");
            box.classList.add("sw-notfound");
            let url = opts.Url ? String(opts.Url) : "";
            if (url) {
                //  Name it below the site root (as the server knows it), so a wrong url is obvious
                const abs = (Sw.Url.Absolute && Sw.Url.Absolute(url)) || url;
                url = abs.startsWith(Sw.Url.Root) ? abs.substring(Sw.Url.Root.length) : url;
            }
            const text = opts.Text || (url
                ? _T('The server has nothing at the address "{0}". Check the link, or open the page from the menu.', Sw.Page.LinkText(url, 200), "Line of the message shown instead of a page or table that does not exist on the server. {0} is replaced by the address (url) that was asked for")
                : _TF("The service doesn't have what this link points to. Check the link, or open the page from the menu.", "Line of the message shown instead of a page or table that does not exist on the service (no address known)"));
            box.append(mk("p", "sw-denied-text", text));
            if (opts.Hint)
                box.append(mk("p", "sw-denied-reason", String(opts.Hint)));
            const links = Array.isArray(opts.Links) ? opts.Links : (isSection(opts) ? [] : null);
            if (!links)
                box.append(homeLink());
            else
                for (const l of links) {
                    if (!l || !l.Url)
                        continue;
                    const a = mk("a", "sw-denied-home", "");
                    a.href = String(l.Url);
                    if (typeof Sw.Icon === "function" && l.Icon) {
                        try {
                            a.append(Sw.Icon(l.Icon, "sw-denied-home-icon"));
                        }
                        catch {
                        }
                    }
                    a.append(document.createTextNode(String(l.Text || l.Url)));
                    box.append(a);
                }
            return box;
        },
    };

    /**
     * Create an element with a class and text.
     * @param {string} tag - Tag name.
     * @param {string} cls - Class name.
     * @param {string} [text] - Text.
     * @returns {HTMLElement}
     */
    function mk(tag, cls, text) {
        const x = document.createElement(tag);
        x.className = cls;
        if (text)
            x.textContent = text;
        return x;
    }

    /**
     * The box of a shared state (not allowed, not found): an icon and a heading.
     * @param {string} icon - Sw.Icon name.
     * @param {string} title - The heading.
     * @param {string} role - The aria role ("alert", "status"): announced once when shown.
     * @returns {HTMLElement}
     */
    function stateBox(icon, title, role) {
        const box = mk("div", "sw-denied");
        box.setAttribute("role", role);
        if (typeof Sw.Icon === "function") {
            try {
                box.append(Sw.Icon(icon, "sw-denied-icon"));
            }
            catch {
            }
        }
        box.append(mk("h2", "sw-denied-title", title));
        return box;
    }

    /**
     * The "Go to Home" link (inside the app shell it replaces the shell, not the content frame; stand-alone this page).
     * @returns {HTMLElement}
     */
    function homeLink() {
        const home = mk("a", "sw-denied-home", "");
        home.href = Sw.Url.Root;
        let inShell = false;
        try {
            inShell = window.top !== window && !!window.top.SysWeaverApp;
        }
        catch {
        }
        home.target = inShell ? "_top" : "_self";
        if (typeof Sw.Icon === "function") {
            try {
                home.append(Sw.Icon("home", "sw-denied-home-icon"));
            }
            catch {
            }
        }
        home.append(document.createTextNode(_TF("Go to Home", "Link on the message shown instead of a page that the signed in user is not allowed to see; opens the home page")));
        return home;
    }

    /**
     * Is a state for a part of a page (opts.Scope "section", or an Element without a Scope)?
     * @param {{Scope:string, Element:HTMLElement}} opts - Options.
     * @returns {boolean}
     */
    function isSection(opts) {
        return opts.Scope ? opts.Scope === "section" : !!opts.Element;
    }

    /**
     * The heading of the not allowed state.
     * @param {{Name:string, Scope:string, Element:HTMLElement}} opts - Options.
     * @returns {string}
     */
    function deniedTitle(opts) {
        const name = opts.Name ? Sw.Page.LinkText(opts.Name, 120) : "";
        if (name)
            return _T("You don't have access to {0}", name, "Heading of the message shown instead of a page or a part of a page that the signed in user is not allowed to see. {0} is replaced by its name, ex: Users");
        return isSection(opts)
            ? _TF("You don't have access to this part of the page", "Heading of the message shown instead of a part of a page (ex: a table) that the signed in user is not allowed to see, while the rest of the page works")
            : _TF("You don't have access to this page", "Heading of the message shown instead of a page (or its data) that the signed in user is not allowed to see");
    }

    /**
     * Show a state element in the page: in opts.Element (replacing its content), else instead of the page content (the children
     * of the body are hidden, so no action of the page can be used). A page scope state with a Name names the page
     * (Sw.Page.SetName). Signals "loaded" when the page didn't yet (the app shell reveals the frame).
     * @param {HTMLElement} box - The state.
     * @param {{Element:HTMLElement, Name:string, Scope:string}} opts - Options.
     */
    function showState(box, opts) {
        const host = opts.Element;
        if (host) {
            host.replaceChildren(box);
        }
        else {
            const b = document.body;
            for (const c of Array.from(b.children)) {
                if (c.classList.contains("sw-page-live") || c.classList.contains("sw-page-heading") || c.classList.contains("sw-denied"))
                    continue;
                if (c.tagName === "SCRIPT" || c.tagName === "STYLE" || c.tagName === "LINK")
                    continue;
                //  Inline: an author display rule (ex: display:flex) would win over the hidden attribute
                c.style.setProperty("display", "none", "important");
            }
            for (const x of b.querySelectorAll(":scope > .sw-denied"))
                x.remove();
            box.classList.add("sw-denied-page");
            b.append(box);
            b.classList.add("sw-denied-body");
        }
        if (opts.Name && !isSection(opts))
            Sw.Page.SetName(opts.Name);
        //  After the current task: Sw.Page.Main's loading remover signals loaded itself
        setTimeout(() => {
            if (!Sw.Page.HasLoaded)
                Sw.Page.Loaded();
        }, 0);
    }

    //  Remember the refused urls of this page (a Guard created after the refusal disables its target at once)
    try {
        Sw.Events.On("access.denied", d => deniedPaths.add(apiPath(d && d.Url || "") || ""));
    }
    catch {
    }

    /**
     * Show the shared "not allowed" state (Sw.Access.NotAllowed) in the page: by default it replaces the page content (the
     * children of the body are hidden, so no action of the page can be used), or it is put into opts.Element (a part of the
     * page: "You don't have access to this part of the page", unless opts.Scope is "page"). A page scope state with opts.Name
     * also names the page (document title and the app shell title, Sw.Page.SetName; ex: "Users" instead of "Table"). Emits
     * "access.denied" and signals "loaded" when the page didn't yet (the app shell reveals the frame); the state is announced
     * once to screen readers (role alert).
     * @param {string|{Url:string, Auth:*, Reason:string, Error:*, Name:string, Scope:string, Element:HTMLElement}} [opts] - A reason
     * text, or options (see Sw.Access.NotAllowed; Element: show it there, replacing its content, instead of in the page).
     * @returns {HTMLElement} The state element.
     */
    Sw.Page.ShowNotAllowed = function (opts) {
        opts = typeof opts === "string" ? { Reason: opts } : (opts || {});
        const box = Sw.Access.NotAllowed(opts);
        showState(box, opts);
        Sw.Access.Denied({ Url: opts.Url || (opts.Error && opts.Error.Url) || "", Error: opts.Error || null });
        return box;
    };

    /**
     * Show the shared "not found" state (Sw.Access.NotFound: names the missing url, no "Try again") in the page, replacing the
     * page content, or in opts.Element. A page scope state with opts.Name names the page (heading and title
     * "Services was not found"). Signals "loaded" when the page didn't yet. Pages use it for a 404 of their data (ex: an unknown table url).
     * @param {string|{Url:string, Name:string, Title:string, Text:string, Hint:string, Links:Array<{Text:string, Url:string, Icon:string}>, Scope:string, Element:HTMLElement}} [opts]
     * - A text, or options (see Sw.Access.NotFound).
     * @returns {HTMLElement} The state element.
     */
    Sw.Page.ShowNotFound = function (opts) {
        opts = typeof opts === "string" ? { Text: opts } : (opts || {});
        const box = Sw.Access.NotFound(opts);
        //  The page title is the heading ("Services was not found"), not just the name
        const head = box.querySelector(".sw-denied-title");
        showState(box, Object.assign({}, opts, { Name: (opts.Name && head) ? head.textContent : "" }));
        return box;
    };

    //  The option schemas (Sw.DevOptions registry: checked in developer mode, listed by the walker of core/contracttest.html)
    if (Sw.DevOptions && Sw.DevOptions.Register) {
        const StateOptions = { Url: "string", Auth: "any", Reason: "string", Error: "any", Name: "string", Scope: { Type: "string", Values: ["page", "section"] }, Element: "element" };
        const NotFoundOptions = { Url: "string", Name: "string", Title: "string", Text: "string", Hint: "string", Links: "array", Scope: { Type: "string", Values: ["page", "section"] }, Element: "element" };
        Sw.DevOptions.Register("Sw.Access.Guard", { Url: "string", Auth: "any", Hide: "boolean" });
        Sw.DevOptions.Register("Sw.Access.NotAllowed", StateOptions);
        Sw.DevOptions.Register("Sw.Page.ShowNotAllowed", StateOptions);
        Sw.DevOptions.Register("Sw.Access.NotFound", NotFoundOptions);
        Sw.DevOptions.Register("Sw.Page.ShowNotFound", NotFoundOptions);
    }
})();
