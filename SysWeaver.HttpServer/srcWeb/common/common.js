
/**
 * Function called by the hacker theme (never call manually)
 * @param {number} time The current animation time for the theme
 */
function hackerThemeAnimate(time) {

    //const pos = -((time * 0.01) % 32);
    const pos = -Math.round((time * 0.01) % 16) * 4;
    //const pos = -Math.round(Math.random() * 16) * 5;
    document.documentElement.style.setProperty("--ThemePos", pos + "px");
}



/**
 * Use this function to select what text literals should be translated (automatically if enabled etc).
 * If the last argument is a string literal and it's not used in the format string, it is used as the translation context.
 * The text is translated on the server before serving the .js file (and the call may be removed if there are no parameters but the translation context parameter).
 * @param {String} format Must be a constant string (string literal). Can optionally use parameters such as: "Hello {0}! Haven't seen you since {1}." etc, that will be replcaed with the supplied paramaters.
 * If the last argument is a string literal and it's not used in the format string, it is used as the translation context.
 * @returns {String} The formatted (translated) string.
 */
function _T(format) {
    const args = Array.prototype.slice.call(arguments, 1);
    return ValueFormat.stringFormatArgs(format, args);
}

/**
 * Use this function to select what text literals should be translated (automatically if enabled etc).
 * If the last argument is a string literal and it's not used in the format string, it is used as the translation context.
 * The text is translated on the server before serving the .js file (and the call may be removed if there are no parameters but the translation context parameter).
 * The translated text will be made safe for use in HTML.
 * @param {String} format Must be a constant string (string literal). Can optionally use parameters such as: "Hello {0}! Haven't seen you since {1}." etc, that will be replcaed with the supplied paramaters.
 * If the last argument is a string literal and it's not used in the format string, it is used as the translation context.
 * @returns {String} The formatted (translated) string, made safe to use in html.
 */
function _TH(format) {
    const args = Array.prototype.slice.call(arguments, 1);
    return ValueFormat.stringFormatArgs(format, args);
}


/**
 * Use this function to select what text literals should be translated (automatically if enabled etc).
 * Use this if no parameters are required or if NO parameter replacements should be done.
 * The text is translated on the server before serving the .js file and the call will be removed.
 * @param {String} text Must be a constant string (string literal). Can optionally use parameters such as: "Hello {0}! Haven't seen you since {1}." etc - they will not be replaced.
 * @param {String} translationContext Optionally give some more context to the translation service, will be removed service side.
 * @returns {String} The (translated) string.
 */
function _TF(text, translationContext) {
    return text;
}


/**
 * Use this function to select what text literals should be translated (automatically if enabled etc).
 * Use this if no parameters are required or if NO parameter replacements should be done.
 * The text is translated on the server before serving the .js file and the call will be removed.
 * The translated text will be made safe for use in HTML.
 * @param {String} text Must be a constant string (string literal). Can optionally use parameters such as: "Hello {0}! Haven't seen you since {1}." etc - they will not be replaced.
 * @param {String} translationContext Optionally give some more context to the translation service, will be removed service side.
 * @returns {String} The (translated) string, made safe to use in html.
 */
function _TFH(text, translationContext) {
    return text;
}

/**
 * Read theme from local storage / OS
 * @returns The theme name
 */
function getTheme() {
    let theme = window.UseTheme;
    if (theme)
        return theme;
    theme = "dark";
    if (!!window.matchMedia) {
        if (window.matchMedia("(prefers-color-scheme: dark)").matches)
            theme = "dark";
        if (window.matchMedia("(prefers-color-scheme: light)").matches)
            theme = "light";
    }
    const ltheme = localStorage.getItem("SysWeaver.Theme");
    if (ltheme) {
        if (ltheme !== "")
            theme = ltheme;
    }
    return theme;
}

/**
 * Apply the current theme
 */
async function applyTheme() {
    let theme = getTheme();
    const de = document.documentElement;
    if (de["Theme"] === theme)
        return;
    const themeOrg = theme;
    const cssPos = theme.indexOf('@');
    if (cssPos > 0) {
        const css = theme.substring(cssPos + 1);
        theme = theme.substring(0, cssPos);
        await includeCss(null, css);
    }
    if (theme === "light")
        de.removeAttribute("data-theme");
    else
        de.setAttribute("data-theme", theme);

    console.log("Theme: Applying theme \"" + themeOrg + "\"");



    const ss = document.documentElement.style;
    const fix = function (name) {
        const hexVal = getComputedStyle(document.documentElement, null).getPropertyValue(name);
        if (!hexVal)
            return;
        let r = 0, g = 0, b = 0;
        if (hexVal.length == 4) {
            r = parseInt(hexVal.substring(1, 2), 16);
            g = parseInt(hexVal.substring(2, 3), 16);
            b = parseInt(hexVal.substring(3, 4), 16);
            r |= (r << 4);
            g |= (g << 4);
            b |= (b << 4);
        }
        if (hexVal.length == 7)
        {
            r = parseInt(hexVal.substring(1, 3), 16);
            g = parseInt(hexVal.substring(3, 5), 16);
            b = parseInt(hexVal.substring(5, 7), 16);
        }
        ss.setProperty(name + "R", r);
        ss.setProperty(name + "G", g);
        ss.setProperty(name + "B", b);
        ss.setProperty(name + "RGB", "" + r + "," + g + "," + b);
        const rgb = "rgba(" + r + "," + g + "," + b + ",";
        ss.setProperty(name + "25", rgb + "0.25)");
        ss.setProperty(name + "50", rgb + "0.50)");
        ss.setProperty(name + "75", rgb + "0.75)");
    }

    fix("--ThemeBackground");
    fix("--ThemeMain");
    fix("--ThemeAcc1");
    fix("--ThemeAcc2");

    de["Theme"] = themeOrg;
    Animator.Remove(de["ThemeAnim"]);
    const f = window[theme + "ThemeAnimate"];
    if (f) {
        de["ThemeAnim"] = f;
        Animator.Add(f);
    }
}


/**
 * Reset to the default theme.
 * @returns true if the theme changed
 */
async function resetTheme() {
    const theme = getTheme();
    localStorage.removeItem("SysWeaver.Theme");
    await applyTheme();
    //console.log("Theme: Posting change");
    InterOp.Post("Theme.Changed");
    return theme != getTheme();
}

/** 
 * Toggle theme between light and dark
 */
async function toggleTheme() {
    await setTheme(getTheme() === "light" ? "dark" : "light");
}

/**
 * Set the theme
 * @param {string} themeName The name of the theme to use
 * @returns return true if the theme changed
 */
async function setTheme(themeName) {

    const theme = getTheme();
    window.UseTheme = null;
    localStorage.setItem("SysWeaver.Theme", themeName);
    await applyTheme();
    //console.log("Theme: Posting change");
    InterOp.Post("Theme.Changed");
    return theme != getTheme();
}


function appendTheme(url) {
    const t = window.UseTheme;
    if (!t)
        return url;
    if (url.indexOf('?') < 0)
        return url + "?useTheme=" + t;
    return url + "&useTheme=" + t;
}

/**
 * Get the latency corrected time on the server 
 * @returns A Date object with the estimated server time
 */
function GetServerTime() {
    var now = new Date().getTime();
    const o = window.SysWeaverServerTimeOffset;
    if (typeof o === "number")
        now += o;
    return new Date(now);
}

/**
 * Get the latency corrected tick on the server (a number, representing the number of milliseconds since midnight January 1, 1970.)
 * @returns A number with the estimated server tick
 */
function GetServerTick() {
    var now = new Date().getTime();
    const o = window.SysWeaverServerTimeOffset;
    if (typeof o === "number")
        now += o;
    return now;
}


/** Static class that manages server session messages */
class SessionManager
{
    static Current = document.currentScript.src;

    /**
     * Start recieving a server event (may already recieve this if there are other listeners).
     * @param {string} name Start recieving server events with this name.
     * @returns true if this was the first listener to this event.
     */
    static AddServerEvent(name) {
        name = name.toLowerCase();
        if (SessionManager.AddRef(SessionManager.ServerEvents, name))
        {
            InterOp.Post("SessionManager.AddEvents", { Events: [name] });
            return true;
        }
        return false;
    }

    /**
     * Stop recieving a server event (may still recieve if there are other listeners).
     * @param {string} name Stop recieving the server event with this name.
     * @returns true if this was the last listener to this event.
     */
    static RemoveServerEvent(name)
    {
        name = name.toLowerCase();
        if (SessionManager.DecRef(SessionManager.ServerEvents, name)) {
            InterOp.Post("SessionManager.RemoveEvents", { Events: [name] });
            return true;
        }
        return false;
    }


    static AddRef(map, name) {
        let count = map.get(name);
        if (!count)
            count = 0;
        ++count;
        map.set(name, count);
        return count === 1;
    }

    static DecRef(map, name) {
        let count = map.get(name);
        if (!count) {
            map.delete(name);
            return false;
        }
        --count;
        if (count !== 0) {
            map.set(name, count);
            return false;
        }
        map.delete(name);
        return true;
    }

    static ServerEvents = new Map();

    static Url = new URL(SessionManager.Current + "/../../Api/application/GetMessages").href;

    static async Init() {
        if (window.HaveSessionManager)
            return;
        window.HaveSessionManager = true;

        const id = InterOp.Id;
        window.SysWeaverId = id;

        const prefix = "SessionManager";
        const logPrefix = prefix + ": ";
        const messagePrefix = prefix + ".";

        const ping = messagePrefix + "Ping";
        const pong = messagePrefix + "Pong";
        const masterClosed = messagePrefix + "MasterClosed";
        const masterChanged = messagePrefix + "MasterChanged";
        const addEvents = messagePrefix + "AddEvents";
        const removeEvents = messagePrefix + "RemoveEvents";
        const timeOffset = messagePrefix + "TimeOffset";
        const getTimeOffset = messagePrefix + "GetTimeOffset";

        const wtop = window.top;
        const wself = window.self;
        let isSameDomain = false;
        try {
            isSameDomain = wtop.location.origin === wself.location.origin;
        }
        catch
        {
        }
        const isTop = (wtop === wself) || (!isSameDomain);
        if (!isTop) {
            function WindowClose() {
                window.removeEventListener("beforeunload", WindowClose);
                window.removeEventListener("unload", WindowClose);
                const evs = Array.from(SessionManager.ServerEvents.keys());
                if (evs.length > 0)
                    InterOp.Post(removeEvents, { Events: evs });
            }
            window.addEventListener("beforeunload", WindowClose);
            window.addEventListener("unload", WindowClose);
            SessionManager.AddMessageHandler(timeOffset, async data => {
                if (data.From !== id)
                    window.SysWeaverServerTimeOffset = data.O;
            }, true);
            InterOp.Post(getTimeOffset);
            return;
        }
        //  Only top windows

        console.log(logPrefix + "Top window " + id + " \"" + window.location.href + "\" initialized.");
        //  Check if we're the first
        let isMaster = false;
        let gotMasterPong = false;

        function StartServerTimeSync() {
            let haveFirst = false;
            const rFirst = new Request(SessionManager.Current + "../../../serverTime?" + Intl.DateTimeFormat().resolvedOptions().timeZone + ";" + navigator.language, {
                method: "GET",
                mode: "cors",
                cache: "reload",
            });

            const r = new Request(SessionManager.Current + "../../../serverTime", {
                method: "GET",
                mode: "cors",
                cache: "reload",
            });
            let nextUpdate = 5000;
            let updateDelay = 2;
            const update = async () => {
                try {
                    if (!isMaster)
                        return;
                    //console.log("Getting new server time");
                    const start = Date.now();
                    const res = await fetch(haveFirst ? r : rFirst);
                    if (res.status == 200) {
                        const text = await res.text();
                        const browserTime = Date.now();
                        const val = parseInt(text);
                        const latency = (browserTime - start) * 0.5;
                        const serverTime = val + latency;
                        const offset = serverTime - browserTime;
                        if (!haveFirst) {
                            haveFirst = true;
                            window.SysWeaverServerTimeOffset = offset;
                            console.log("Initial server time adjustment: " + offset + " ms (latency: " + latency + " ms)");
                            InterOp.Post(timeOffset, { O: offset });
                        } else {
                            const dstart = window.SysWeaverServerTimeOffset;
                            if (offset !== dstart) {
                                const doffset = offset - dstart;
                                //console.log("Got new server time adjustment: " + offset + " ms" + ", adjusting " + doffset + " ms (over " + (updateDelay * 500) + " ms)");
                                let step = 0;
                                const updateTime = () => {
                                    if (!isMaster)
                                        return;
                                    ++step;
                                    if (step >= updateDelay) {
                                        window.SysWeaverServerTimeOffset = offset;
                                        InterOp.Post(timeOffset, { O: offset });
                                        console.log("Server time adjustment changed to: " + offset + " ms (latency: " + latency + " ms)");
                                        return;
                                    }
                                    const offs = (doffset * step) / updateDelay + dstart;
                                    window.SysWeaverServerTimeOffset = offs;
                                    InterOp.Post(timeOffset, { O: offs });
                                    setTimeout(updateTime, 500);
                                };
                                updateTime();
                            }
                        }
                        setTimeout(update, nextUpdate);
                        if (nextUpdate < 300000) {
                            nextUpdate *= 2;
                            if (updateDelay < 30)
                                updateDelay *= 2;
                        }
                        return;
                    }
                }
                catch
                {
                }
                setTimeout(update, haveFirst ? 15000 : 2000);
            };
            update();
        }



        let serverEvents = null;
        const eventElement = document.createElement("SysWeaver-EventHandler");


        let masterAbort = null;

        function serverEventsChanged() {
            const evs = Array.from(serverEvents.keys());
            //console.log(logPrefix + "Server events changed to: " + evs);
            const ma = masterAbort;
            if (ma) {
                try {
                    ma.abort();
                }
                catch
                {
                }
            }
        }





        // The master "thread""
        let cc = 0;
        async function RunMaster(startCc) {
            cc = startCc ? startCc : 0;
            let errCount = 0;
            const sendErrorOn = 3;
            function onError() {
                ++errCount;
                if (errCount === sendErrorOn) {
                    InterOp.Post("server.error");
                    cc = 0;
                }
            }
            StartServerTimeSync();
            console.log(logPrefix + "Master started from " + cc);
            while (isMaster) {

                const req =
                {
                    Cc: cc,
                    MessageTypes: Array.from(serverEvents.keys()),
                };
                const r = new Request(SessionManager.Url, {
                    method: "POST",
                    mode: "cors",
                    cache: "default",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: ToTypedJson(req),
                });
                masterAbort = new AbortController();
                try {
                    const res = await fetch(r, { signal: masterAbort.signal });
                    if (res.status === 200) {
                        const response = await res.json();
                        masterAbort = null;
                        if (response) {
                            cc = response.Cc;
                            const newEvents = response.Messages;
                            if (newEvents) {
                                const nel = newEvents.length;
                                for (let i = 0; i < nel; ++i) {
                                    const evData = newEvents[i];
                                    //console.log(logPrefix + "Master got event \"" + evData.Type + "\" from the server:\n" + JSON.stringify(evData, null, "\t"));
                                    InterOp.Post(evData.Type, evData, true);
                                }
                            }
                        }
                        if (errCount >= sendErrorOn)
                            InterOp.Post("server.restored");
                        errCount = 0;
                        continue;
                    } else {
                        onError();
                    }
                    masterAbort = null;
                }
                catch (e) {
                    masterAbort = null;
                    //  No delay if aborted
                    if (e instanceof DOMException) {
                        if (e.name === "AbortError")    
                            continue;
                    }
                    onError();
                }
                //  If we get some error, wait 5 seconds before retrying to avoid spamming
                await delay(5000);
            }
        }



        function sendEvents() {
            const evs = Array.from(SessionManager.ServerEvents.keys());
            if (evs.length > 0)
                InterOp.Post(addEvents, { Events: evs });
        }

        //  Master message responses
        const masterMap = new Map();
        let response = (name, fn) => masterMap.set(name, fn);
        response(addEvents, data => {
            const events = data.Events;
            let changed = false;
            events.forEach(e => {
                changed |= SessionManager.AddRef(serverEvents, e.toLowerCase());
            });
            if (changed)
                serverEventsChanged();
        });
        response(ping, data => {
            if (data.From !== id)
                InterOp.Post(pong, { To: data.From });
        });
        response(removeEvents, data => {
            const events = data.Events;
            let changed = false;
            events.forEach(e => {
                changed |= SessionManager.DecRef(serverEvents, e.toLowerCase());
            });
            if (changed)
                serverEventsChanged();
        });
        response(getTimeOffset, data => {
            InterOp.Post(timeOffset, { To: data.From, O: window.SysWeaverServerTimeOffset });
        });
        //  Child message responses
        const childMap = new Map();
        response = (name, fn) => childMap.set(name, fn);
        response(pong, async data => {
            if (data.To === id) {
                gotMasterPong = true;
                eventElement.dispatchEvent(new CustomEvent("GotPong"));
            }
        });
        response(masterClosed, async data => {
            if (data.From !== id)
                await StartMaster(1, data.Cc);
        });
        response(timeOffset, async data => {
            if (data.From !== id)
                window.SysWeaverServerTimeOffset = data.O;
        });
        //  All message repsonses
        response = (name, fn) => {
            masterMap.set(name, fn); childMap.set(name, fn);
        };
        response(masterChanged, async data => {
            sendEvents();
        });

        async function eventHandler(ev) {
            const data = InterOp.GetMessage(ev);
            const messageType = data.Type;
            //console.log("" + InterOp.Id + (isMaster ? " [master]" : "") + " got: " + messageType);
            if (!messageType)
                return;
            const fn = (isMaster ? masterMap : childMap).get(messageType);
            if (fn)
                await fn(data);
        }

        InterOp.AddListener(eventHandler);

        let checkMasterTimer = null;

        async function HaveMaster(first) {
            gotMasterPong = false;
            let time = null;
            await waitEvent2(eventElement, "GotPong", "GotTimeOut", () => {
                time = setTimeout(() => eventElement.dispatchEvent(new CustomEvent("GotTimeOut")), 100);
                gotMasterPong = false;
                InterOp.Post(ping);

            });
            clearTimeout(time);
            return gotMasterPong;
        }

        let isStarting = false;




        async function StartMaster(how, startCc) {
            //  Enter lock
            await navigator.locks.request("SysWeaver.SessionManager", async () => {
                if (isStarting)
                    return;
                isStarting = true;
                try {
                    //  Check if new master have been created
                    if (await HaveMaster())
                        return;
                    if (isMaster)
                        return;
                    //  
                    switch (how) {
                        case 0:
                            console.log(logPrefix + "Starting as master " + id);
                            break;
                        case 1:
                            console.log(logPrefix + "Switching to master " + id + " (from message)");
                            break;
                        case 2:
                            console.log(logPrefix + "Switching to master " + id + " (from polling)");
                            break;
                    }
                    if (checkMasterTimer)
                        clearInterval(checkMasterTimer);
                    isMaster = true;
                    //  Get server events
                    serverEvents = new Map();
                    InterOp.Post(masterChanged);
                    await delay(100); // Wait a bit so that we can collect events (that way we don't have to restart so often)
                    //
                    RunMaster(startCc);
                }
                finally {
                    isStarting = false;
                }
            });
        }

        async function MaybeStartMaster(first) {
            await StartMaster(first === true ? 0 : 2);
        }

        function StartChild() {
            console.log(logPrefix + "Starting as child " + id);
            InterOp.Post(getTimeOffset);
            checkMasterTimer = setInterval(MaybeStartMaster, 1000);
        }

        await StartMaster(0);
        if (!isMaster)
            StartChild();

        //  Send events incase some have been registered already
        //sendEvents();

        function WindowClose() {
            window.removeEventListener("beforeunload", WindowClose);
            window.removeEventListener("unload", WindowClose);
            InterOp.RemoveListener(eventHandler);
            if (!isMaster) {
                console.log(logPrefix + "Stopping child " + id);
                if (checkMasterTimer)
                    clearInterval(checkMasterTimer);
                const evs = Array.from(SessionManager.ServerEvents.keys());
                if (evs.length > 0)
                    InterOp.Post(removeEvents, { Events: evs });
            } else {
                console.log(logPrefix + "Stopping master " + id);
                isMaster = false;
                InterOp.Post(masterClosed, { Cc: cc });
            }
        }

        window.addEventListener("beforeunload", WindowClose);
        window.addEventListener("unload", WindowClose);
    }

    static GetMap() {
        let map = window["SessionManagerFns"];
        if (map)
            return map;
        map = new Map();
        window["SessionManagerFns"] = map;
        InterOp.AddListener(async ev => {
            const m = InterOp.GetMessage(ev);
            const t = m.Type;
            if (!t)
                return;
            const fns = map.get(t.toLowerCase());
            if (fns) {
                const fl = fns.length;
                for (let i = 0; i < fl; ++i)
                    await fns[i](m);
            }
        });
        return map;
    }

    /**
     * Start listening for an event, MUST remove the message handler or they will accumulate.
     * @param {string} messageType The type of the message to listen for.
     * @param {function(object)} fn An async function that is executed when the message arrives, the first argument is the message.
     * @param {boolean} ignoreServer If true, the event is NOT a server event, hence not listened for.
     */
    static AddMessageHandler(messageType, fn, ignoreServer) {

        const m = SessionManager.GetMap();
        messageType = messageType.toLowerCase();
        let list = m.get(messageType);
        if (!list) {
            list = [];
            m.set(messageType, list);
        }
        list.push(fn);
        if (!ignoreServer)
            SessionManager.AddServerEvent(messageType);
    }

    /**
     * Stop listening for an event.
     * @param {string} messageType The type of the message to listen for.
     * @param {function(object)} fn The async function that should be revmoved (same as used in AddMessageHandler).
     * @param {boolean} ignoreServer If true, the event is NOT a server event, hence not listened for (same as used in AddMessageHandler).
     */
    static RemoveMessageHandler(messageType, fn, ignoreServer) {
        const m = SessionManager.GetMap();
        messageType = messageType.toLowerCase();
        let list = m.get(messageType);
        if (!list)
            return;
        const i = list.indexOf(fn);
        if (i < 0)
            return;
        if (!ignoreServer)
            SessionManager.RemoveServerEvent(messageType);
        list.splice(i, 1);
        if (list.length > 0)
            return;
        m.delete(messageType);
    }

}




class InterOpBroadcast {
    constructor(name) {
        const s = new BroadcastChannel(name);
        const listeners = [];
        this.Post = m =>
        {
            s.postMessage(m);
            const e = {
                data: m,
            };
            const c = listeners.length;
            for (let i = 0; i < c; ++i) {
                try {
                    listeners[i](e);
                }
                catch (e)
                {
                    console.warn("EventListener failed: " + e.message);
                }
            }
        }
        this.AddListener = l => {

            listeners.push(l);
            s.addEventListener('message', l);
        };
        this.RemoveListener = l => {
            s.removeEventListener('message', l);
            const t = listeners.lastIndexOf(l);
            if (t >= 0)
                listeners.splice(t, 1);
        }
    }
}

class InterOpWindows {
    constructor() {
        const windows = [];
        let w = window.parent;
        while (w) {
            windows.push(w);
            w = w.parent;
        }
        window._open = window.open;
        window.open = (url, name, params) => {
            const win = window._open(url, name, params);
            if (!win)
                return win;
            if ((!IsAbsolutePath(url)) || IsSameOrigin(url))
                windows.push(win);
            return win;
        }

        this.Post = m => {
            const wl = windows.length;
            if (wl <= 0)
                return;
            const toRemove = [];
            for (let i = 0; i < wl; ++i) {
                const win = windows[i];
                try {
                    if (!win.closed) {
                        win.postMessage(m);
                        continue;
                    }
                }
                catch (e) {
                }
                toRemove.push(i);
            }
            let rl = toRemove.length;
            while (rl > 0) {
                --rl;
                windows.splice(toRemove[rl], 1);
            }
        };
        this.AddListener = l => window.addEventListener('message', l);
        this.RemoveListener = l => window.removeEventListener('message', l);

    }
}

class InterOp {

    static Id = function () {
        const id = new Date().valueOf() + Math.random();
        window.SysWeaverId = id;
        return id;
    }();

    static B = function () {
        try {
            return new InterOpBroadcast("SysWeaver");
        }
        catch
        {
        }
        console.warn("Using opened windows fallback");
        return new InterOpWindows();
    }();

    // type is the message type as a string (will be added into data.Type).
    // data is an optional object with data (must be an object, can't be string, number, etc).
    // Field names Type and To are reserved
    static Post(type, data, isFromServer) {
        if (typeof data === "object")
            data.Type = type;
        else
            data = { Type: type };
        data.From = isFromServer ? "Server" : InterOp.Id;
        data.MagicXyz = "SysWeaver";
        InterOp.B.Post(data);
    }


    // Use InterOp.GetMessage to check/get a message.
    static AddListener(fn) {
        InterOp.B.AddListener(fn);
        return fn;
    }

    static RemoveListener(fn) {
        InterOp.B.RemoveListener(fn);
        return fn;
    }

    // Always returns an object that will have the Type and From fields populated (no need to null check)
    static GetMessage(ev) {
        const data = ev.data;
        if (data)
            if (data.MagicXyz === "SysWeaver")
                if (typeof data.Type === "string") {
                    //delete data.MagicXyz;
                    return data;
                }
        return { Type: null, From: null };
    }

}

// Downloads some text (without going through the server)
function downloadText(filename, text) {
    const element = document.createElement('a');
    element.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(text));
    element.setAttribute('download', filename);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}

// Downloads some url
function downloadFile(filename, url) {
    if (!filename) {
        const l = url.lastIndexOf('/');
        filename = url.substring(l + 1);
    }
    const element = document.createElement('a');
    element.setAttribute('href', url);
    element.setAttribute('download', filename);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}

// Returns true if an element is attached to the dom
 function isInDocument(el) {
    const d = document.body;
    while (el) {
        const p = el.parentElement;
        if (p === d)
            return true;
        el = p;
    }
    return false;
}

/**
 * Get the url params of the current url, all keys are lower-cased.
 * @returns {URLSearchParams} Search parameters where all keys are lower-cased
 */
function getUrlParams()
{
    let p = window.sysweaverParams;
    if (p)
        return p;
    const o = new URLSearchParams(window.location.search);
    p = new URLSearchParams();
    for (const [k, v] of o) 
        p.append(k.toLowerCase(), v);
    window.sysweaverParams = p;
    return p;
}

/**
 * Get a (decimal) number from a paramater
 * @param {URLSearchParams} params The url search paramaters (use result of getUrlParams)
 * @param {string} name The name of the paramater (case sensitive, usewr lowercase if using getUrlParams)
 * @returns {number|null} The value or null if not found or invalid
 */
function getNumberParam(params, name) {
    const v = params.get(name);
    if (typeof v === "string") {
        try {
            return Number.parseFloat(v);
        }
        catch
        {
        }
    }
    return null;
}

/**
 * Get an integer number from a paramater
 * @param {URLSearchParams} params The url search paramaters (use result of getUrlParams)
 * @param {string} name The name of the paramater (case sensitive, usewr lowercase if using getUrlParams)
 * @returns {number|null} The value or null if not found or invalid
 */
function getIntParam(params, name) {
    const v = params.get(name);
    if (typeof v === "string") {
        try {
            return Number.parseInt(v);
        }
        catch
        {
        }
    }
    return null;
}

/**
 * Test if it's likely that this is running on a mobile device
 * @returns {boolean} True if it's likely that this code is running on a mobile device
 */
function isMobile() {
    let p = navigator.userAgentData;
    if (typeof p !== 'undefined' && p != null) {
        const m = p.mobile;
        if (typeof m === "boolean")
            return m;
    }
    let check = false;
    (function (a) { if (/(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows ce|xda|xiino/i.test(a) || /1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-/i.test(a.substr(0, 4))) check = true; })(navigator.userAgent || navigator.vendor || window.opera);
    return check;
}

/**
 * Get a platform name.
 * Result depends on browser but generally checking if it starts with "win", "mac", "ios", "android", "linux" will do the trick.
 * @returns {string} Name of the platform in lowercase or 'unknown' 
 */
function getPlatform() {
    const macosPlatforms = /(macintosh|macintel|macppc|mac68k|macos)/i;
    const windowsPlatforms = /(win32|win64|windows|wince)/i;
    const iosPlatforms = /(iphone|ipad|ipod)/i;
    const androidPlatforms = /android/i;
    const linuxPlatforms = /linux/i;
    // 2022 way of detecting. Note : this userAgentData feature is available only in secure contexts (HTTPS)
    let p = navigator.userAgentData;
    if (typeof p !== 'undefined' && p != null)
        return p.platform.toLowerCase();
    // Deprecated but still works for most of the browser
    p = navigator.platform;
    const u = navigator.userAgent.toLowerCase();
    if (typeof p !== 'undefined') {
        if (typeof u !== 'undefined' && androidPlatforms.test(u))
            return 'android';
        return p.toLowerCase();;
    }
    //  Fallback to analyze user agent
    if (typeof u === 'undefined')
        return 'unknown';
    if (macosPlatforms.test(u))
        return "macos";
    if (iosPlatforms.test(u))
        return "ios";
    if (windowsPlatforms.test(u))
        return "windows";
    if (androidPlatforms.test(u))
        return "android";
    if (linuxPlatforms.test(u))
        return "linux";
    return 'unknown';
}


/**
 * Return mobile paramaters as an array:
 * [0] boolean : true if it's a mobile device.
 * [1] boolean : true if it's a narrow device and a single columns is prefered (typically mobile portrait).
 * [2] boolean : true if in portrait.
 * [3] number : Scale to use, 1.0.
 * @returns {array} Paramaters:
 * [0] boolean : true if it's a mobile device.
 * [1] boolean : true if it's a narrow device and a single columns is prefered (typically mobile portrait).
 * [2] boolean : true if in portrait.
 * [3] number : Scale to use, 1.0.
 */
function getMobileParams() {
    const isMob = isMobile();
    const width = window.innerWidth;
    const height = window.innerHeight;
    const isPortrait = width < height;
    const zoomIn = isMob && isPortrait;
    const minScale = zoomIn ? 200 : 400;
    const maxScale = zoomIn ? 400 : 800;

    let useSingleColumn = false;

    if (isPortrait)
        if ((width <= maxScale) || isMob)
            useSingleColumn = true;
    //  Scaling
    let scale = 1;
    if (isMob) {
        if (width > maxScale)
            scale = width / maxScale;
        else
            if (width < minScale)
                scale = width / minScale;
    }
    return [isMob, useSingleColumn, isPortrait, scale];
}


function CryptRandomInt32() {
    if (crypto) {
        try {
            const t = new Int32Array(1);
            crypto.getRandomValues(t);
            return t[0];
        }
        catch {
        }
    }
    return (Math.random() * 0xffffffff) | 0;
}

// Get a random GUID (uses crypto if available else a simple unsafe random is used)
function CryptRandomGuid() {
    if (crypto) {
        try {
            return crypto.randomUUID();
        }
        catch {
        }
    }
    let lut = CryptRandomGuid.lut;
    if (!lut) {
        lut = [];
        for (var i = 0; i < 256; i++)
            lut[i] = (i < 16 ? '0' : '') + (i).toString(16);
        CryptRandomGuid.lut = lut;
    }
    const d0 = (Math.random() * 0xffffffff) | 0;
    const d1 = (Math.random() * 0xffffffff) | 0;
    const d2 = (Math.random() * 0xffffffff) | 0;
    const d3 = (Math.random() * 0xffffffff) | 0;
    return lut[d0 & 0xff] + lut[d0 >> 8 & 0xff] + lut[d0 >> 16 & 0xff] + lut[d0 >> 24 & 0xff] + '-' +
        lut[d1 & 0xff] + lut[d1 >> 8 & 0xff] + '-' + lut[d1 >> 16 & 0x0f | 0x40] + lut[d1 >> 24 & 0xff] + '-' +
        lut[d2 & 0x3f | 0x80] + lut[d2 >> 8 & 0xff] + '-' + lut[d2 >> 16 & 0xff] + lut[d2 >> 24 & 0xff] +
        lut[d3 & 0xff] + lut[d3 >> 8 & 0xff] + lut[d3 >> 16 & 0xff] + lut[d3 >> 24 & 0xff];
}

function legacySha256(buffer)
{
    const func = legacySha256.data ?? (legacySha256.data = function () {

        const uint8Array = Uint8Array;
        const uint32Array = Uint32Array;

        // For cross-platform support we need to ensure that all 32-bit words are
        // in the same endianness. A UTF-8 TextEncoder will return BigEndian data,
        // so upon reading or writing to our ArrayBuffer we'll only swap the bytes
        // if our system is LittleEndian (which is about 99% of CPUs)
        const LittleEndian = !!new uint8Array(new uint32Array([1]).buffer)[0];

        function convertEndian(word) {
            if (LittleEndian) {
                return (
                    // byte 1 -> byte 4
                    (word >>> 24) |
                    // byte 2 -> byte 3
                    (((word >>> 16) & 0xff) << 8) |
                    // byte 3 -> byte 2
                    ((word & 0xff00) << 8) |
                    // byte 4 -> byte 1
                    (word << 24)
                );
            }
            else {
                return word;
            }
        }

        function rightRotate(word, bits) {
            return (word >>> bits) | (word << (32 - bits));
        }



        // To ensure cross-browser support even without a proper SubtleCrypto
        // impelmentation (or without access to the impelmentation, as is the case with
        // Chrome loaded over HTTP instead of HTTPS), this library can create SHA-256
        // HMAC signatures using nothing but raw JavaScript

        /* eslint-disable no-magic-numbers, id-length, no-param-reassign, new-cap */

        // By giving internal functions names that we can mangle, future calls to
        // them are reduced to a single byte (minor space savings in minified file)
        const pow = Math.pow;

        // Will be initialized below
        // Using a Uint32Array instead of a simple array makes the minified code
        // a bit bigger (we lose our `unshift()` hack), but comes with huge
        // performance gains
        const DEFAULT_STATE = new uint32Array(8);
        const ROUND_CONSTANTS = [];

        // Reusable object for expanded message
        // Using a Uint32Array instead of a simple array makes the minified code
        // 7 bytes larger, but comes with huge performance gains
        const M = new uint32Array(64);

        // After minification the code to compute the default state and round
        // constants is smaller than the output. More importantly, this serves as a
        // good educational aide for anyone wondering where the magic numbers come
        // from. No magic numbers FTW!
        function getFractionalBits(n) {
            return ((n - (n | 0)) * pow(2, 32)) | 0;
        }

        let n = 2, nPrime = 0;
        while (nPrime < 64) {
            // isPrime() was in-lined from its original function form to save
            // a few bytes
            let isPrime = true;
            // Math.sqrt() was replaced with pow(n, 1/2) to save a few bytes
            // let sqrtN = pow(n, 1 / 2);
            // So technically to determine if a number is prime you only need to
            // check numbers up to the square root. However this function only runs
            // once and we're only computing the first 64 primes (up to 311), so on
            // any modern CPU this whole function runs in a couple milliseconds.
            // By going to n / 2 instead of sqrt(n) we net 8 byte savings and no
            // scaling performance cost
            for (let factor = 2; factor <= n / 2; factor++) {
                if (n % factor === 0) {
                    isPrime = false;
                }
            }
            if (isPrime) {
                if (nPrime < 8) {
                    DEFAULT_STATE[nPrime] = getFractionalBits(pow(n, 1 / 2));
                }
                ROUND_CONSTANTS[nPrime] = getFractionalBits(pow(n, 1 / 3));

                nPrime++;
            }

            n++;
        }
        return function(data) {
            // Copy default state
            const STATE = DEFAULT_STATE.slice();

            // Caching this reduces occurrences of ".length" in minified JavaScript
            // 3 more byte savings! :D
            const legth = data.length;

            // Pad data
            const bitLength = legth * 8;
            const newBitLength = (512 - ((bitLength + 64) % 512) - 1) + bitLength + 65;

            // "bytes" and "words" are stored BigEndian
            const bytes = new uint8Array(newBitLength / 8);
            const words = new uint32Array(bytes.buffer);

            bytes.set(data, 0);
            // Append a 1
            bytes[legth] = 0b10000000;
            // Store length in BigEndian
            words[words.length - 1] = convertEndian(bitLength);

            // Loop iterator (avoid two instances of "let") -- saves 2 bytes
            let round;

            // Process blocks (512 bits / 64 bytes / 16 words at a time)
            for (let block = 0; block < newBitLength / 32; block += 16) {
                const workingState = STATE.slice();

                // Rounds
                for (round = 0; round < 64; round++) {
                    let MRound;
                    // Expand message
                    if (round < 16) {
                        // Convert to platform Endianness for later math
                        MRound = convertEndian(words[block + round]);
                    }
                    else {
                        let gamma0x = M[round - 15];
                        let gamma1x = M[round - 2];
                        MRound =
                            M[round - 7] + M[round - 16] + (
                                rightRotate(gamma0x, 7) ^
                                rightRotate(gamma0x, 18) ^
                                (gamma0x >>> 3)
                            ) + (
                                rightRotate(gamma1x, 17) ^
                                rightRotate(gamma1x, 19) ^
                                (gamma1x >>> 10)
                            )
                            ;
                    }

                    // M array matches platform endianness
                    M[round] = MRound |= 0;

                    // Computation
                    let t1 =
                        (
                            rightRotate(workingState[4], 6) ^
                            rightRotate(workingState[4], 11) ^
                            rightRotate(workingState[4], 25)
                        ) +
                        (
                            (workingState[4] & workingState[5]) ^
                            (~workingState[4] & workingState[6])
                        ) + workingState[7] + MRound + ROUND_CONSTANTS[round]
                        ;
                    let t2 =
                        (
                            rightRotate(workingState[0], 2) ^
                            rightRotate(workingState[0], 13) ^
                            rightRotate(workingState[0], 22)
                        ) +
                        (
                            (workingState[0] & workingState[1]) ^
                            (workingState[2] & (workingState[0] ^
                                workingState[1]))
                        )
                        ;

                    for (let i = 7; i > 0; i--) {
                        workingState[i] = workingState[i - 1];
                    }
                    workingState[0] = (t1 + t2) | 0;
                    workingState[4] = (workingState[4] + t1) | 0;
                }

                // Update state
                for (round = 0; round < 8; round++) {
                    STATE[round] = (STATE[round] + workingState[round]) | 0;
                }
            }

            // Finally the state needs to be converted to BigEndian for output
            // And we want to return a Uint8Array, not a Uint32Array
            return new uint8Array(new uint32Array(
                STATE.map(function (val) { return convertEndian(val); })
            ).buffer);
        }

    }
    ());


    return func(buffer);
}

function RemoveNulls(o) {
    const t = typeof o;
    if (t === "object") {
        if (o === null)
            return true;
        if (Array.isArray(o)) {
            const l = o.length;
            for (let i = 0; i < l; ++i)
                RemoveNulls(o[i]);
            return;
        }
        const toRemove = [];
        for (let propertyName in o) {
            if (RemoveNulls(o[propertyName]))
                toRemove.push(propertyName);
        }
        toRemove.forEach(x => delete o[x]);
    }
    return false;
}

/*
function MakeJsObject(o)
{
    const t = typeof o;
    if (t === "object") {
        if (o === null)
            return;
        if (Array.isArray(o)) {
            const l = o.length;
            for (let i = 0; i < l; ++i)
                MakeJsObject(o[i]);
            return;
        }
        const toAdd = [];
        const toRemove = [];
        for (let propertyName in o) {
            const fnew = propertyName.substring(0, 1).toLowerCase() + propertyName.substring(1);
            if (fnew === propertyName)
                continue;
            toAdd.push([fnew, o[propertyName]]);
            toRemove.push(propertyName);
        }
        toRemove.forEach(x => delete o[x]);
        toAdd.forEach(x => o[x[0]] = x[1]);
        for (let propertyName in o) {
            MakeJsObject(o[propertyName]);
        }
    }
}

function MakeCsObject(o) {
    const t = typeof o;
    if (t === "object") {
        if (o === null)
            return;
        if (Array.isArray(o)) {
            const l = o.length;
            for (let i = 0; i < l; ++i)
                MakeCsObject(o[i]);
            return;
        }
        const toAdd = [];
        const toRemove = [];
        for (let propertyName in o) {
            const fnew = propertyName.substring(0, 1).toUpperCase() + propertyName.substring(1);
            if (fnew === propertyName)
                continue;
            toAdd.push([fnew, o[propertyName]]);
            toRemove.push(propertyName);
        }
        toRemove.forEach(x => delete o[x]);
        toAdd.forEach(x => o[x[0]] = x[1]);
        for (let propertyName in o) {
            MakeCsObject(o[propertyName]);
        }
    }
}
*/



// Encode an arbitary string to a base64 encoded string
function Base64EncodeString(anyString) {
    const bytes = new TextEncoder().encode(anyString);
    const binString = Array.from(bytes, byte => String.fromCodePoint(byte)).join("");
    return btoa(binString);
}

// Decode a base64 encoded string
function Base64DecodeString(base64String) {
    const binString = atob(base64String);
    const bytes = Uint8Array.from(binString, m => m.codePointAt(0));
    return new TextDecoder().decode(bytes);
}


/**
 * Make a base64 encoded string uri safe, by using '-' instead of '+' and '_' instead of '/' also trim away the padding.
 * @param {string} str The uri encoded string.
 * @returns {string} The uri safe base64 encoded string without any padding.
 */
function MakeUriSafeBase64(str) {
    str = str.replaceAll('+', '-');
    str = str.replaceAll('/', '_');
    const l = str.indexOf('=');
    if (l >= 0)
        str = str.substring(0, l);
    return str;
}

/**
 * Convert a Uint8Array byte buffer to base 64
 * @param {Uint8Array} bytes The byte buffer to convert
 * @param {boolean} uriSafe If true, use the Uri safe alphabet '-' instead of '+' and '_' instead of '/' also trim's the padding.
 * @returns {string} The base64 encoded string.
 */
function Uint8ArrayToBase64(bytes, uriSafe) {

    if (bytes.toBase64) {
        try {
            if (uriSafe)
                return bytes.toBase64(
                    {
                        alphabet: "base64url",
                        omitPadding: true,
                    });
            return bytes.toBase64();
        }
        catch
        {
            if (uriSafe)
                return MakeUriSafeBase64(bytes.toBase64());
        }
    }
    const binString = Array.from(bytes, (byte) =>
        String.fromCodePoint(byte),
    ).join("");
    const str = btoa(binString);
    return uriSafe ? MakeUriSafeBase64(str) : str;
}


function bufferToHex(buffer) {
    if (!buffer)
        return null;
    buffer = new Uint8Array(buffer);
    const hex = "0123456789abcdef";
    const l = buffer.length;
    let r = "";
    for (let i = 0; i < l; ++i) {
        const c = buffer[i];
        r += hex.charAt(c >> 4);
        r += hex.charAt(c & 0xf);
    }
    return r;
}


/**
 * Convert data contained in the buffer to a base64 encoded string
 * @param {ArrayBuffer|Uint8Array|Blob} The data
 * @returns {string} The data contained in the buffer as a base64 encoded string
 */
async function bufferToBase64(buffer) {
    if (!buffer)
        return null;
    if (!(buffer instanceof Blob))
        buffer = new Blob([buffer]);

    // use a FileReader to generate a base64 data URI:
    const base64url = await new Promise(r => {
        const reader = new FileReader();
        reader.onload = () => r(reader.result);
        reader.readAsDataURL(buffer);
    });
    // remove the `data:...;base64,` part from the start
    return base64url.slice(base64url.indexOf(',') + 1);
}

function base64ToArray(base64String) {
    if (!base64String)
        return null;
    const binString = atob(base64String);
    const bytes = Uint8Array.from(binString, m => m.codePointAt(0));
    return bytes;
}


// Hash a UInt8Array data blob to SHA1 and return the hash as a UInt8Array
async function hashDataSha1(hashData) {
    return await crypto.subtle.digest("SHA-1", hashData);
}

// Create a base64 encoded SHA1 hash from a string
async function hashStringSha1(hashSrc) {
    const hashAsData = new TextEncoder().encode(hashSrc);
    const hashBuffer = await hashDataSha1(hashAsData);
    return await bufferToBase64(hashBuffer);
}





// Hash a UInt8Array data blob to SHA 256 and return the hash as a UInt8Array
async function hashData(hashData) {
    let hashBuffer;
    try {
        hashBuffer = await crypto.subtle.digest("SHA-256", hashData);
    }
    catch (e) {
        hashBuffer = legacySha256(hashData);
    }
    return hashBuffer;
}

// Create a base64 encoded hash from a string
async function hashString(hashSrc) {
    const hashAsData = new TextEncoder().encode(hashSrc);
    const hashBuffer = await hashData(hashAsData);
    return await bufferToBase64(hashBuffer);
}

function concatArrays(uint8arrays) {
    // Determine the length of the result.
    const totalLength = uint8arrays.reduce(
        (total, uint8array) => total + uint8array.byteLength,
        0
    );
    const result = new Uint8Array(totalLength);
    let offset = 0;
    uint8arrays.forEach((uint8array) => {
        result.set(uint8array, offset);
        offset += uint8array.byteLength;
    });
    return result;
}

// Test if an element is hidden, does NOT test parents!
// An element is hidden if it's computed style have "display: none".
function isHidden(element) {
    const cs = getComputedStyle(element);
    if (cs.display === "none")
        return true;
    if (element.tagName === "HEAD")
        return true;
    return false;
}

// Traverse the child elements (including children to children etc) on the supplied element in DOM order.
// okFn: takes the current element as argument, return anything "non-false" to stop traversal (and return the same value).
// traverseHidden: set to true to traverse hidden nodes (isHidden(element) returns true), default is to not traverse hiiden.
function traverseChildren(element, okFn, traverseHidden) {
    const stack = [];
    let e = element;
    let c = e.firstElementChild;
    for (; ;) {
        if (typeof c === "undefined") {
            const r = okFn(e);
            if (r)
                return r;
            c = e.firstElementChild;
        } else {
            if (c) {
                const n = c;
                c = c.nextElementSibling;
                if (traverseHidden || (!isHidden(n))) {
                    stack.push([e, c]);
                    e = n;
                    c = undefined;
                    continue;
                }
            } else {
                if (stack.length <= 0)
                    return null;
                const s = stack.pop();
                e = s[0];
                c = s[1];
            }
        }
    }
    return null;
}

// Traverse all DOM element (including children to children etc), starting from the supplied element in DOM order.
// If the okFn always returns a false statement, the function returns null.
// element: the element to start at, this is never supplied to the okFn.
// okFn: takes the current element as argument, return anything "non-false" to stop traversal (and return the same value).
// traverseHidden: set to true to traverse hidden nodes (isHidden(element) returns true), default is to not traverse hiiden.
function traverseForward(element, okFn, traverseHidden) {
    const start = element;
    const stack = [];
    let ip = element;
    for (; ;) {
        const ce = ip;
        ip = ip.parentElement;
        if (!ip)
            break;
        stack.push([ip, ce.nextElementSibling]);
    }
    stack.reverse();
    let e = element;
    let c = e.firstElementChild;
    for (; ;) {
        if (typeof c === "undefined") {
            const r = okFn(e);
            if (r)
                return r;
            c = e.firstElementChild;
        } else {
            if (c) {
                const n = c;
                if (n === start)
                    return null;
                c = c.nextElementSibling;
                if (traverseHidden || (!isHidden(n))) {
                    stack.push([e, c]);
                    e = n;
                    c = undefined;
                    continue;
                }
            } else {
                if (stack.length <= 0) {
                    e = document.documentElement;
                    c = undefined;
                    continue;
                }
                const s = stack.pop();
                e = s[0];
                c = s[1];
            }
        }
    }
    return null;
}

// Traverse all DOM elements (including children to children etc), starting from the supplied element in reverse DOM order.
// If the okFn always returns a false statement, the function returns null.
// element: the element to start at, this is never supplied to the okFn.
// okFn: takes the current element as argument, return anything "non-false" to stop traversal (and return the same value).
// traverseHidden: set to true to traverse hidden nodes (isHidden(element) returns true), default is to not traverse hiiden.
function traverseBackward(element, okFn, traverseHidden) {
    const start = element;
    const stack = [];
    let ip = element;
    for (; ;) {
        const ce = ip;
        ip = ip.parentElement;
        if (!ip)
            break;
        stack.push([ip, ce.previousElementSibling]);
    }
    stack.reverse();
    let e = element.previousElementSibling;
    let c = undefined;
    for (; ;) {
        if (c === null) {
            if (e === start)
                return null;
            const r = okFn(e);
            if (r)
                return r;
            if (stack.length <= 0) {
                e = document.documentElement;
                c = undefined;
                continue;
            }
            const s = stack.pop();
            e = s[0];
            c = s[1];
        } else {
            if (c)
                c = c.previousElementSibling;
            else
                c = e.lastElementChild;
            if (c) {
                stack.push([e, c]);
                e = c;
                c = undefined;
            }
        }
    }
    return null;
}


function isTabable(element) {
    if (element.tabIndex >= 0)
        return true;
    if (element.tagName === "INPUT") {
        if (!element.readOnly)
            return true;
    }
    return false;
}


// Set the focus to the next element after the supplied element that isTabable 
function focusNextTab(element) {
    /*
    traverseChildren(document.body, e => console.log(e), false);
    console.log("All done!");
    traverseForward(element, e => console.log(e), false);
    console.log("Forward done!");
    traverseBackward(element, e => console.log(e), false);
    console.log("Backward done!");
    */
    const f = traverseForward(element, e => isTabable(e) ? e : null);
    if (f)
        f.focus();
    return f;
}

// Set the focus to the previous element after the supplied element that isTabable 
function focusPrevTab(element) {
    const f = traverseBackward(element, e => isTabable(e) ? e : null);
    if (f)
        f.focus();
    return f;
}


function tabToNextOnEnter(inputElement, onNextItemFn) {
    inputElement.onkeyup = async ev => {
        const k = ev.key;
        if (k != "Enter")
            return;
        if (badClick(ev))
            return;
        const nf = focusNextTab(inputElement);
        if (onNextItemFn)
            await onNextItemFn(nf, inputElement);
    };
}

function keyboardClick(element) {
    const e = element;
    if ((!e.tabIndex) || (e.tabIndex < 0))
        e.tabIndex = "0";
    e.classList.add("KeyClick");
    e.onkeyup = ev => {
        const k = ev.key;
        if (k != " ")
            if (k != "Enter")
                return;
        if (badClick(ev))
            return;
        e.click();
    };
    return e;
}


function isPureClick(ev) {
    if (!ev)
        return true;
    if (ev.shiftKey)
        return false;
    if (ev.altKey)
        return false;
    if (ev.ctrlKey)
        return false;
    if (ev.metaKey)
        return false;
    return true;
}

function badClick(ev, keepProp) {
    if (!ev)
        return false;
    if (!keepProp) {
        ev.preventDefault();
        ev.stopPropagation();
    }
    if (ev.shiftKey)
        return true;
    if (ev.altKey)
        return true;
    if (ev.ctrlKey)
        return true;
    if (ev.metaKey)
        return true;
    return false;
}


function makeHtmlAttributeSafe(s, preserveCR) {
    preserveCR = preserveCR ? '&#13;' : '\n';
    return ('' + s) /* Forces the conversion to string. */
        .replace(/&/g, '&amp;') /* This MUST be the 1st replacement. */
        .replace(/'/g, '&apos;') /* The 4 other predefined entities, required. */
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        /*
        You may add other replacements here for HTML only 
        (but it's not necessary).
        Or for XML, only if the named entities are defined in its DTD.
        */
        .replace(/\r\n/g, preserveCR) /* Must be before the next replacement. */
        .replace(/[\r\n]/g, preserveCR);
    ;
}

function makeHtmlSafe(text) {
    const e = document.createElement("SysWeaver-Temp");
    e.innerText = text;
    return e.innerHTML;
}

function isValidEmail(s)
{
    if (s.trim() != s)
        return false;
    const i = s.indexOf('@');
    if (i < 1)
        return false;
    const pre = s.substr(0, i);
    if (pre.trim() != pre)
        return false;
    const post = s.substr(i + 1);
    if (post.trim() != post)
        return false;
    if (post.indexOf('@') >= 0)
        return false;
    const x = post.split('.');
    const xl = x.length;
    if (xl < 2)
        return false;
    if (xl > 4)
        return false;
    for (let c = 0; c < xl; ++c)
    {
        const v = x[c];
        if (v.trim() != v)
            return false;
        if (v.length <= 0)
            return false;
    }
    return true;
}

function isValidPhone(s) {
    if (s.trim() != s)
        return false;
    if (s.length < 3)
        return false;
    return true;
}

 /**
  * async include a js file (if it's not already included).
  * @param {string} current The js filename that the file to be included is realtive path, if null or undefined the location of common.js is used.
  * @param {string} file A relative path to the js file to load (relative to current) or an absolute path (starting with http:// or https://).
  * @param {boolean} noThrow If true, no exception is thrown.
 * @returns {Promise} A promise that resolves to an object with the members: "HTMLElement element", "string error", "bool loaded" and "object errorEv".
  */
function includeJs(current, file, noThrow)
{
    if (!current)
        current = ValueFormat.Current;
    if (!file.startsWith("http://"))
        if (!file.startsWith("https://"))
            file = new URL(current + "/../" + file).href;
    return new Promise((resolve, reject) => {
        try {
            let map = window["IncMap"];
            if (!map) {
                map = new Map();
                window["IncMap"] = map;
            }
            const e = map.get(file);
            if (e) {
                function ResCaches() {
                    if (e.Pending)
                    {
                        setTimeout(ResCaches, 10);
                        return;
                    }
                    if (e.error && (!noThrow))
                        reject(e);
                    else
                        resolve(e);
                }
                ResCaches();
                return;
            }
            const n = { element: null, error: null, elementEv: null, Pending: true, loaded: false };
            map.set(file, n);
            const s = document.createElement("script");
            s.type = "text/javascript";
            s.async = true;
            s.addEventListener("load", ev =>
            {
                n.element = s;
                delete n.Pending;
                const nn = Object.assign({}, n);
                nn.loaded = true;
                resolve(nn);
            });
            s.addEventListener("error", ev =>
            {
                s.remove();
                n.error = _T('Failed to load "{0}"', file, "Indicates a failure to dynamically load a js file.");
                n.errorEv = ev;
                delete n.Pending;
                if (noThrow)
                    resolve(e);
                else
                    reject(e);
            });
            s.src = file;
            document.head.appendChild(s);
        } catch (e) {
            reject({

                element: null,
                error: e.message,
                errorEv: e,
            });
        }
    });
}


/**
 * async include a css file (if it's not already included).
 * @param {string} current The js filename that the file to be included is realtive path, if null or undefined the location of common.js is used.
 * @param {string} file A relative path to the css file to load (relative to current) or an absolute path (starting with http:// or https://).
 * @param {boolean} noThrow If true, no exception is thrown.
 * @returns {Promise} A promise that resolves to an object with the members: "HTMLElement element", "string error", "bool loaded" and "object errorEv".
 */
function includeCss(current, file, noThrow) {
    if (!current)
        current = ValueFormat.Current;
    if (!file.startsWith("http://"))
        if (!file.startsWith("https://"))
            file = new URL(current + "/../" + file).href;
    return new Promise((resolve, reject) => {
        try {
            let map = window["IncMap"];
            if (!map) {
                map = new Map();
                window["IncMap"] = map;
            }
            const e = map.get(file);
            if (e) {
                function ResCaches() {
                    if (e.Pending) {
                        setTimeout(ResCaches, 10);
                        return;
                    }
                    if (e.error && (!noThrow))
                        reject(e);
                    else
                        resolve(e);
                }
                ResCaches();
                return;
            }
            const n = { element: null, error: null, elementEv: null, Pending: true, loaded: false };
            map.set(file, n);
            const s = document.createElement("link");
            s.rel = "stylesheet";
            s.async = true;
            s.addEventListener("load", ev => {
                n.element = s;
                delete n.Pending;
                const nn = Object.assign({}, n);
                nn.loaded = true;
                resolve(nn);
            });
            s.addEventListener("error", ev => {
                s.remove();
                n.error = _T('Failed to load "{0}"', file, "Indicates a failure to dynamically load a css file.");
                n.errorEv = ev;
                delete n.Pending;
                if (noThrow)
                    resolve(e);
                else
                    reject(e);
            });
            s.href = file;
            document.head.appendChild(s);
        } catch (e) {
            reject({

                element: null,
                error: e.message,
                errorEv: e,
            });
        }
    });
}

/**
 * Try to get the geo location position of a device
 * @param {boolean} lowPrecision Optional, if true, low precision is acceptable (usually faster)
 * @param {number} timeout Optional, a positive long value representing the maximum length of time (in milliseconds) the device is allowed to take in order to return a position. The default value is Infinity, meaning that getCurrentPosition() won't return until the position is available.
 * @param {number} maximumAge Optional, a positive long value indicating the maximum age in milliseconds of a possible cached position that is acceptable to return. If set to 0, it means that the device cannot use a cached position and must attempt to retrieve the real current position. If set to Infinity the device must return a cached position regardless of its age. Default: 0.
 * @returns {GeolocationPosition} The position if available
 */
async function getDevicePosition(lowPrecision, timeout, maximumAge) {

    if ((!timeout) || (timeout <= 0))
        timeout = Infinity;
    if ((!maximumAge) || (maximumAge <= 0))
        maximumAge = 0;

    let res = null;
    let err = null;
    await new Promise(resolve => {
        try {
            navigator.geolocation.getCurrentPosition(
                val => {
                    res = val;
                    resolve();
                },
                e => {
                    err = new Error(val.message);
                    resolve();
                },
                {
                    timeout: timeout,
                    maximumAge: maximumAge,
                    enableHighAccuracy: !lowPrecision,
                });
        }
        catch (e) {
            err = e;
            resolve();
        }
    });
    if (err)
        throw err;
    return res;


}




/**
 * Wait for an event in some async manner, returns the event.
 * @param {HTMLElement} element The element that will recieve the event to wait for.
 * @param {String} eventName Name of the event to wait for (uses addEventListener on that event).
 * @param {function(HTMLElement)} fn An optional function that is called after event hooking is done.
 * @param {number} timeout An optional time out in milliseconds (if the event haven't triggered before the timeout the function will return null).
 * @returns {Event} The event that was triggered or null if it timed out.
 */
async function waitEvent(element, eventName, fn, timeout) {
    return new Promise(resolve => {
        let timeoutT = null;
        const end = ev => {
            if (timeoutT)
                clearTimeout(timeoutT);
            timeoutT = null;
            element.removeEventListener(eventName, end);
            resolve(ev);
        };
        const timeoutFn = (timeout && (timeout > 0)) ? () => end(null) : null;
        element.addEventListener(eventName, end);
        if (fn)
            fn(element);
        if (timeoutFn)
            timeoutT = setTimeout(timeoutFn, timeout);
    });
}


/**
 * Wait for one of two event in some async manner, returns the event.
 * @param {HTMLElement} element The element that will recieve the event to wait for.
 * @param {String} eventName1 Name of one event to wait for (uses addEventListener on that event).
 * @param {String} eventName1 Name of another event to wait for (uses addEventListener on that event).
 * @param {function(HTMLElement)} fn An optional function that is called after event hooking is done.
 * @param {number} timeout An optional time out in milliseconds (if the event haven't triggered before the timeout the function will return null).
 * @returns {Event} The event that was triggered first (one of the two) or null if it timed out.
*/
async function waitEvent2(element, eventName1, eventName2, fn, timeout) {
    return new Promise(resolve => {
        let timeoutT = null;
        const end = ev => {
            if (timeoutT)
                clearTimeout(timeoutT);
            timeoutT = null;
            element.removeEventListener(eventName2, end);
            element.removeEventListener(eventName1, end);
            resolve(ev);
        };
        const timeoutFn = (timeout && (timeout > 0)) ? () => end(null) : null;
        element.addEventListener(eventName1, end);
        element.addEventListener(eventName2, end);
        if (fn)
            fn(element);
        if (timeoutFn)
            timeoutT = setTimeout(timeoutFn, timeout);
    });
}



// First argument to the async function is a function that should be called to end the wait
async function waitFor(fn) {
    const e = document.createElement("SysWeaver-Wait");
    const evName = "SysWeaverWaitCompleted";
    await waitEvent(e, evName, async () => {
        const end = () => e.dispatchEvent(new Event(evName));
        await fn(end);
    });
}

async function setMediaSrcAndWait(mediaElement, url, onResult) {
    const eo = "canplaythrough";
    const ee = "error";
    return new Promise(resolve => {
        let o, e;
        o = function () {
            mediaElement.removeEventListener(eo, o);
            mediaElement.removeEventListener(ee, e);
            if (onResult)
                onResult(true, mediaElement, url);
            resolve();
        };
        e = function () {
            mediaElement.removeEventListener(eo, o);
            mediaElement.removeEventListener(ee, e);
            if (onResult)
                onResult(false, mediaElement, url);
            resolve();
        };

        mediaElement.addEventListener(eo, o);
        mediaElement.addEventListener(ee, e);
        mediaElement.src = url;
    });
}

class AbortHandler {

    constructor() {
        this.Event = new Event("AbortHandlerEvent");
        this.Element = document.createElement("div");
    }

    // Raise the abort event
    raise() {
        this.Element.dispatchEvent(this.Event);
    }

    // Add a listener for abort events
    addListener(fn) {
        this.Element.addEventListener("AbortHandlerEvent", fn);
    }

    // Remove a listener from abort events
    removeListener(fn) {
        this.Element.removeEventListener("AbortHandlerEvent", fn);

    }
}

// Delay some time
async function delay(msToWait)
{
    return new Promise(resolve => setTimeout(resolve, msToWait));
}

// Delay some time, with custom abort
async function delayWithAbort(msToWait, abortHandler)
{
    return new Promise(resolve =>
    {
        let aborter;
        const th = setTimeout(ev => {
            if (aborter)
                abortHandler.removeListener(aborter);
            resolve(false);
        }, msToWait)
        aborter = function () {
            clearTimeout(th);
            abortHandler.removeListener(aborter);
            resolve(true);
        }
        abortHandler.addListener(aborter);

    });
}

function ReloadAll(flushCache, clearHash)
{
    if (clearHash)
        history.replaceState(null, null, ' ');
    if (flushCache)
        location.reload(true);
    else
        location.reload();
}

function PostAll(name, data) {
    if (!data) {
        data = {
            Type: name,
        };
    } else {
        data.Type = name;
    }
    const wt = window.opener ?? window.top;
    if (wt) {
        try {
            wt.postMessage(data);
        }
        catch
        {
        }
    }
}

function PostTop(name, data, targetOrigin) {
    if (!data) {
        data = {
            Type: name,
        };
    } else {
        data.Type = name;
    }
    const wt = window.top;
    if (wt) {
        try {
            wt.postMessage(data, targetOrigin);
        }
        catch
        {
        }
    }
}

function Open(l, target) {
    if (target === "_self") {
        if ((!IsAbsolutePath(l)) || IsSameOrigin(l)) {
            const a = document.createElement('a');
            a.href = l;
            const abs = a.href;
            console.log('"' + l + '" => "' + abs + '"');
            PostTop("IframeNavigating",
                {
                    Url: abs,
                }, "*");
            setTimeout(() => {
                window.open(appendTheme(l), "_self");
                console.warn("Parent didn't respond in time!");
            }, 100);
            return;
        }
    }
    window.open(appendTheme(l), target ? target : "_blank");
}


function IsSameOrigin(url) {
    const urlA = window.location;
    const urlB = new URL(url);
    return urlA.origin === urlB.origin;
}

function IsAbsolutePath(url) {
    if (url.startsWith("http://"))
        return true;
    if (url.startsWith("https://"))
        return true;
    return false;
}

function GetAbsolutePath(url, current) {
    if (IsAbsolutePath(url))
        return url;
    return new URL(url, current ? current : window.location.href).href;
}

async function FlushCache(url) {
    const x = new XMLHttpRequest();
    x.open("HEAD", url, true);
    x.setRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
    x.setRequestHeader("Pragma", "no-cache");
    x.setRequestHeader("Expires", "0");
    waitFor(endWait => {
        x.onreadystatechange = async () => {
            if (x.readyState == 4)
                endWait();
        };
        x.send();
    });
}

function FlushCacheSync(url, whenDone) {
    const x = new XMLHttpRequest();
    x.open("HEAD", url, true);
    x.setRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
    x.setRequestHeader("Pragma", "no-cache");
    x.setRequestHeader("Expires", "0");
    x.onreadystatechange = async () => {
        if (x.readyState == 4) {
            if (whenDone)
                await whenDone();
        }
    };
    x.send();
}


/**
 * Set / reload an img tag src, optionally flush caches etc
 * @param {HTMLImageElement} img The img element to load the image into.
 * @param {String} src An optional new url.
 * @param {String} fallback An optional url to use if the image load fails.
 * @param {Boolean} flushCache If is true, a clean copy will be downloaded.
 * @param {function(Boolean)} onLoaded An optional callback to call when done (or error), a boolean parameter is supplied with true if src is loaded, else false.
 * @param {String} reloadOnMessage An optional string with an event name, that will cause a reload.
 */
function SetImageSource(img, src, fallback, flushCache, onLoaded, reloadOnMessage) {
    const s = src ?? img.src;
    if (flushCache) {
        FlushCacheSync(s, () => SetImageSource(img, s, fallback, false, onLoaded, reloadOnMessage));
        return;
    }
    const f = fallback;
    let count = 0;
    const ims = img.style;
    const oldOp = ims.opacity;
    ims.opacity = 0;
    img.onerror = ev => {
        ev.preventDefault();
        ev.stopPropagation();
        ++count;
        if (count == 1) {
            if (f) {
                img.src = f;
                return;
            }
        }
        img.onerror = null;
        img.onload = null;
        ims.opacity = oldOp;
        if (onLoaded)
            onLoaded(false);
    };
    img.onload = ev => {
        ev.preventDefault();
        ev.stopPropagation();
        img.onerror = null;
        img.onload = null;
        ims.opacity = oldOp;
        if (onLoaded)
            onLoaded(count == 0);
    };
    img.src = s;
    if (img.complete && (img.naturalWidth > 0)) {
        img.onerror = null;
        img.onload = null;
        ims.opacity = oldOp;
        if (onLoaded)
            onLoaded(count == 0);
    }
    if (reloadOnMessage) {
        if (!img.MsgHandler) {
            async function handler(ev) {
                const msg = InterOp.GetMessage(ev);
                const type = msg.Type;
                if (type !== reloadOnMessage)
                    return;
                if (isInDocument(img)) {
                    msg.Waiter = msg.Waiter ?? FlushCache(src);
                    await msg.Waiter;
                    SetImageSource(img, src, fallback, false, onLoaded);
                }
                else {
                    InterOp.RemoveListener(handler);
                    SessionManager.RemoveServerEvent(reloadOnMessage);
                }
            }
            InterOp.AddListener(handler);
            SessionManager.AddServerEvent(reloadOnMessage);
            img.MsgHandler = handler;
        }
    }
}

/** Represents a (clickable) monochrome image */
class ColorIcon {

    static GenCss = new Map();
    /*
        CSS for an image class:
                .{ImageClass} {
                    background-image: url(....);
                }

        Following color classes are pre-defined:
            "IconColorThemeBackground"
            "IconColorThemeMain"
            "IconColorThemeAcc1"
            "IconColorThemeAcc2"

    */

    static GetStyleSheet() {
        //return [].slice.call(document.head.getElementsByTagName("link")).find(o => o.getAttribute("href").indexOf("theme.css") >= 0).sheet;
        let ss = [].slice.call(document.head.getElementsByTagName("style")).find(o => o.getAttribute("id") == "ColorIcon");
        if (!ss) {
            ss = document.createElement("style");
            ss.setAttribute("id", "ColorIcon");
            document.head.appendChild(ss);
        }
        return ss.sheet;
    }


    static SetImageClass(element, imageClass) {
        if (!imageClass) {
            element.style.maskImage = null;
            element.style.backgroundImage = null;
            element.style.backgroundColor = null;
            return;
        }

        const isLink = imageClass.includes('.');
        if (isLink)
        {
            const isColor = imageClass.charAt(imageClass.length - 1) === '*';
            if (isColor) {
                imageClass = imageClass.substring(0, imageClass.length - 1);
                element.style.maskImage = null;
                element.style.backgroundImage = "url('" + imageClass + "')";
                element.style.backgroundColor = "transparent";

            } else {
                element.style.maskImage = "url('" + imageClass + "')";
                element.style.backgroundImage = null;
                element.style.backgroundColor = null;
            }
        } else {
            element.classList.add(imageClass);
            element.style.maskImage = null;
            element.style.backgroundImage = null;
            element.style.backgroundColor = null;
        }
    }

    static RemoveImageClass(element, imageClass) {
        element.style.maskImage = null;
        element.style.backgroundImage = null;
        element.style.backgroundColor = null;
        if (!imageClass)
            return;
        if (isLetter(imageClass.charAt(0)))
            element.classList.remove(imageClass);
    }

    /**
     * Create a (clickable) icon of a specific color (can be multicoloured too with the right css)
     * @param {string} imageClass The css class to use for the icon image (should contain a single: mask-image: url("../icons/reload.svg"))
     * @param {string} colorClass Color of the icon, IconColorThemeBackground, IconColorThemeMain, IconColorThemeAcc1, IconColorThemeAcc2 are predefined.
     * @param {number} width Width in pixels of the icon
     * @param {number} height Height in pixels of the icon
     * @param {string} title Optional title (tool tip)
     * @param {function(event)} onClick Optional function to executo on click
     * @param {string} buttonClass Optional class to give the button (default is "IconButton")
     * @param {string} disabledClass Optional class to give the button when disabled (default is "IconDisabled")
     * @param {boolean} captureContextMenu If true, the context menu is captured.
     */
    constructor(imageClass, colorClass, width, height, title, onClick, buttonClass, disabledClass, captureContextMenu) {
        if ((!width) || (width <= 0))
            width = 16;
        if ((!height) || (height <= 0))
            height = 16;
        if (!onClick)
            onClick = null;
        if (!buttonClass)
            buttonClass = "IconButton";
        if (!disabledClass)
            disabledClass = "IconDisabled";
        const t = this;
        t.DisabledStyle = disabledClass;
        t.Width = width;
        t.Height = height;
        const key = width + "x" + height;
        if (!ColorIcon.GenCss.has(key)) {
            ColorIcon.GenCss.set(key, true);
            const ss = ColorIcon.GetStyleSheet();
            const min = width < height ? width : height;
            let dist = min * 0.03;
            if (dist < 0.2)
                dist = 0.2;
            if (dist > 8)
                dist = 8;
            const upDist = dist * 0.5;
//            ss.insertRule("icon-" + key + "{width:" + width + "px;height:" + height + "px;display:inline-block;overflow:hidden;}", ss.cssRules.length);
//            ss.insertRule("icon-" + key + ">icon-main{overflow:visible;background-position:-100% 0;background-repeat:no-repeat;background-size:" + width + "px " + height + "px;margin-left:-" + width + "px;display:block;width:" + (width + 0) + "px;height:" + height + "px;}", ss.cssRules.length);

            ss.insertRule("icon-" + key + "{width:calc(var(--ThemeIconSize)*" + width + "px);height:calc(var(--ThemeIconSize)*" + height + "px);display:inline-block;mask-size:calc(var(--ThemeIconSize)*" + width + "px) calc(var(--ThemeIconSize)*" + height + "px);mask-position:center;mask-repeat:no-repeat;background-position:center;background-repeat:no-repeat;}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconButton{cursor:pointer;opacity:0.8;transition:opacity 0.15s transform 0.15s;}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconButton:hover{opacity:1;transform:translateY(-" + upDist + "px);}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconButton:focus{outline:none;opacity:1;transform:translateY(-" + upDist + "px);}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconButton:active{transform:translateY(" + dist + "px);}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconDisabled{cursor:default;opacity:0.2;}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconDisabled:hover{opacity:0.2;transform: translateY(0);}", ss.cssRules.length);
            ss.insertRule("icon-" + key + ".IconDisabled:active{opacity:0.2;transform: translateY(0);}", ss.cssRules.length);
            t.AddColor("IconColorThemeBackground", "var(--ThemeBackground)");
            t.AddColor("IconColorThemeMain", "var(--ThemeMain)");
            t.AddColor("IconColorThemeAcc1", "var(--ThemeAcc1)");
            t.AddColor("IconColorThemeAcc2", "var(--ThemeAcc2)");
        }
        const e = document.createElement("icon-" + key);


        //const s = document.createElement("icon-main");
        //e.appendChild(s);
        const el = e.classList;
        //const sl = s.classList;
        if (onClick) {
            el.add(buttonClass);
            keyboardClick(e);
        }
        //if (imageClass)
            //sl.add(imageClass);
        if (imageClass)
            ColorIcon.SetImageClass(e, imageClass);
        if (colorClass)
            el.add(colorClass);
        t.Element = e;
        t.ImageElement = e;
        t.ImageClass = imageClass;
        t.ColorClass = colorClass;
        t.Title = title;
        let clickFn = null;
        if (onClick != null) {
            clickFn = async ev => {
                //  Isn't a pure click (shift + click for instance)
                if (badClick(ev))
                    return;
                //  Is disabled
                if (!t.IsEnabled)
                    return;
                //  Is working
                if (t.OldImage)
                    return;
                await onClick(ev);
            }
            e.onclick = clickFn;
            if (captureContextMenu) {
                e.oncontextmenu = ev => {
                    if (badClick(ev, true))
                        return true;
                    clickFn(ev);
                    return false;
                };
            }
        }
        this.setTitleAttr(title);
    }

    setTitleAttr(tit) {
        const e = this.Element;
        if (typeof tit === "undefined")
            e.removeAttribute("title");
        else
            e.title = tit;
    }

    /** The element of this icon */
    Element = null;


    TabStop = true;
    IsEnabled = true;
    Title = "";

    /**
     * Change the title of the icon
     * @param {string} title New title text
     * @returns {ColorIcon} Returns self so that chaining is possible
     */
    SetTitle(title) {
        if (this.Title === title)
            return this;
        this.Title = title;
        const e = this.Element;
        if (!e.classList.contains(this.DisabledStyle)) 
            this.setTitleAttr(title);
        return this;
    }

    /**
     * Make a button enabled or disabled
     * @param {boolean} enabled True to enable
     * @returns {ColorIcon} Returns self so that chaining is possible
     */
    SetEnabled(enabled)
    {
        if (this.IsEnabled === enabled)
            return this;
        this.IsEnabled = enabled;
        const e = this.Element;
        if (enabled) {
            e.classList.remove(this.DisabledStyle);
            this.setTitleAttr(this.Title);
            if (e.onclick && this.TabStop)
                e.setAttribute("tabindex", "0");
        }
        else {
            e.classList.add(this.DisabledStyle);
            this.setTitleAttr("");
            e.removeAttribute("tabindex");
        }
        return this;
    }

    static AddIconColor(name, width, height, col) {
        const key = width + "x" + height;
        const kn = key + "_" + name;
        if (ColorIcon.GenCss.has(kn))
            return;
        ColorIcon.GenCss.set(kn, true);
        const ss = ColorIcon.GetStyleSheet();
        const n = "icon-" + key + "." + name;
//        const colFilter = "drop-shadow(" + width + "px 0 0 " + col + ")";
//        ss.insertRule(n + ">icon-main{-webkit-filter:" + colFilter + ";filter:" + colFilter + ";}", ss.cssRules.length);
        ss.insertRule(n + "{background-color:" + col + ";}", ss.cssRules.length);
    }

    /**
     * Add a new color (css) for this button, this does not change the actual color, just creates the required css
     * @param {string} name The css class name
     * @param {string} col The css color
     */
    AddColor(name, col)
    {
        ColorIcon.AddIconColor(name, this.Width, this.Height, col);
    }

    /**
     * Change the icon image
     * @param {string} imageClass A new image class
     * @returns {ColorIcon} Returns self so that chaining is possible
     */
    ChangeImage(imageClass) {
        const c = this.ImageClass;
        if (c == imageClass)
            return this;
        const te = this.ImageElement;
        ColorIcon.RemoveImageClass(te, c);
        ColorIcon.SetImageClass(te, imageClass);
        this.ImageClass = imageClass;
        if (this.OldImage)
            this.OldImage = imageClass;
        return this;
    }

    /**
     * Change the icon color
     * @param {string} colorClass A new color class
     * @returns {ColorIcon} Returns self so that chaining is possible
     */
    ChangeColor(colorClass) {
        const c = this.ColorClass;
        if (c == colorClass)
            return this;
        const l = this.Element.classList;
        if (c)
            l.remove(c);
        if (colorClass)
            l.add(colorClass);
        this.ColorClass = colorClass;
        return this;
    }


    /**
     * Optionally call this when a button is clicked and some async function is called.
     * If the async function takes a long time, the button will show a "working" animation.
     * @param {string} iconName Optional class name of the "working" icon, default is IconWorking.
     * @param {number} changeAfterMs Number of ms before the "working" icon is shown, default is 250 ms.
     */
    StartWorking(iconName, changeAfterMs) {
        const t = this;
        if (t.OldImage)
            return;
        if (!iconName)
            iconName = "IconWorking";
        if (!changeAfterMs)
            changeAfterMs = 250;

        const c = t.ImageClass;
        t.OldImage = c;
        t.WorkTimer = setTimeout(() => {
            const te = t.ImageElement;
            ColorIcon.RemoveImageClass(te, c);
            ColorIcon.SetImageClass(te, iconName);
            t.WorkImage = iconName;
        }, changeAfterMs);
    }

    /**
     * If StartWorking was called, call this to remove the "Working" animation.
     */
    StopWorking() {
        const oi = this.OldImage;
        if (!oi)
            return;
        clearTimeout(this.WorkTimer);
        if (this.WorkImage)
            this.ImageElement.classList.remove(this.WorkImage);
        this.ImageClass = null;
        this.ChangeImage(oi);
        this.OldImage = null;
        this.WorkTimer = null;
    }
}

/**
 * Insert some text at an INPUT or TEXTAREA element at the current cursor position (replaces the current selection)
 * @param {HTMLElement} myField INPUT or TEXTAREA element.
 * @param {string} myValue The Text to insert.
 * @param {boolean} updateSelection Optionally set the newly inserted value as the new selection.
 */
function insertAtCursor(myField, myValue, updateSelection) {
    const v = myField.value;
    let s = myField.selectionStart;
    let e = myField.selectionEnd;
    let sl = myField.NextLast;
    if ((!s) && sl) {
        s = sl;
        e = sl;
    }
    myField.NextLast = null;
    s = s ? s : 0;
    myField.value =
        (s > 0 ? v.substring(0, s) : "")
        + myValue
        + v.substring(e);
    e = s + myValue.length;
    if (updateSelection) {
        myField.setSelectionRange(s, e);
    } else {
        myField.NextLast = e;
    }
    return e;
}

class ButtonStyle {

    ButtonElement = "Sysweaver-Button";
    ButtonStyle = null;
    MinPressed = 20;
    IconWidth = 24;
    IconHeight = 24;
    IconColor = "IconColorThemeBackground";
}

class Button {

    static CreateRow() {
        return document.createElement("SysWeaver-CenterBlock");
    }

    static DefaultStyle = new ButtonStyle();

    constructor(style, text, title, imageClass, enabled, onclick) {
        if (!style)
            style = Button.DefaultStyle;
        const pc = "Pressed";
        const e = document.createElement(style.ButtonElement);
        if (style.ButtonStyle)
            e.classList.add(style.ButtonStyle);
        const t = this;
        t.Element = e;
        t.Title = title;
        t.UserClick = onclick;
        const th = this;
        const d = style.MinPressed;

        let haveMouseDown = false;

        const clickFn = async ev => {
            if (badClick(ev))
                return;
            e.classList.add(pc);
            e.onclick = null;
            let exitFn = null;
            const uc = t.UserClick;
            if (uc) {
                try {
                    exitFn = await uc(th);
                }
                catch (e) {
                }
            }
            if (d > 0)
                await delay(d);
            if (th.IsEnabled)
                e.onclick = clickFn;
            e.classList.remove(pc);
            haveMouseDown = false;
            if (exitFn)
                exitFn();
        };

        e.onmousedown = ev => {
            if (ev.button != 0)
                return;
            haveMouseDown = true;
            if (th.IsEnabled)
                e.classList.add(pc);
        }

        e.onmouseout = ev => {
            e.classList.remove(pc);
        }

        e.onmouseover = ev => {
            if (!haveMouseDown)
                return;
            if ((ev.buttons & 1) === 0) {
                haveMouseDown = false;
                return;
            }
            if (th.IsEnabled)
                e.classList.add(pc);
        }


        t.OnClick = clickFn;


        const te = document.createElement("Sysweaver-ButtonText");
        t.Text = te;
        te.textContent = text;
        e.appendChild(te);

        if (imageClass && (imageClass.length > 0)) {
            const icon = new ColorIcon(imageClass, style.IconColor, style.IconWidth, style.IconHeight);
            icon.Element.title = title;
            e.appendChild(icon.Element);
            t.Icon = icon;
        }

        if (enabled)
            t.Enable();
        else
            e.classList.add("Disabled");

    }
    Element = null;
    Title = null;
    IsEnabled = false;
    OnClick = null;

    ChangeImage(imageClass) {
        const i = this.Icon;
        if (i)
            i.ChangeImage(imageClass);
    }

    ChangeImageColor(imageColorClass) {
        const i = this.Icon;
        if (i)
            i.ChangeColor(imageColorClass);
    }

    ChangeText(newText) {
        this.Text.textContent = newText;
    }

    ChangeTitle(newTitle) {
        if (this.Title === newTitle)
            return;
        this.Title = newTitle;
        this.Element.title = newTitle;
        const i = this.Icon;
        if (i)
            i.SetTitle(newTitle);
    }

    SetEnabled(enable) {
        if (enable)
            this.Enable();
        else
            this.Disable();
    }

    Enable() {
        if (this.IsEnabled)
            return;
        this.IsEnabled = true;
        const e = this.Element;
        e.tabIndex = "0";
        e.title = this.Title;
        const fn = this.OnClick;
        e.onclick = fn;
        e.onkeyup = async ev => {
            if (ev.key === "Enter")
                await fn(ev);
        };
        e.classList.remove("Disabled");
    }

    Disable() {
        if (!this.IsEnabled)
            return;
        this.IsEnabled = false;
        const e = this.Element;
        e.removeAttribute("tabindex");
        e.title = "";
        e.onclick = null;
        e.onkeyup = null;
        e.classList.add("Disabled");
    }

    // Will change the icon after some time (unless StopWorking is called before that)
    StartWorking(iconName, changeAfterMs) {
        const i = this.Icon;
        if (i)
            i.StartWorking(iconName, changeAfterMs);
    }

    StopWorking() {
        const i = this.Icon;
        if (i)
            i.StopWorking();
    }


}


/*
    Get a function by name.
    context will default to window.
*/
function GetFunction(functionName, context)
{
    if (!context)
        context = window;
    var namespaces = functionName.split(".");
    var func = namespaces.pop();
    for (var i = 0; i < namespaces.length; i++)
        context = context[namespaces[i]];
    return context[func];
}

/**
 * Show a "Fail" message
 * @param {string} text The text to display
 * @param {number} duration An optional duration in milliseconds to show the message (default to 5 seconds).
 * @param {boolean} noCopyOnClick By default the text message is copied to the clipboard on click (and then removed), set to true to disable the copying.
 */
function Fail(text, duration, noCopyOnClick) {
    Message(text, duration, "MsgError", noCopyOnClick);
}

/**
 * Show an "Information" message
 * @param {string} text The text to display
 * @param {number} duration An optional duration in milliseconds to show the message (default to 5 seconds).
 * @param {boolean} noCopyOnClick By default the text message is copied to the clipboard on click (and then removed), set to true to disable the copying.
 */
function Info(text, duration, noCopyOnClick) {
    Message(text, duration, "MsgInfo", noCopyOnClick);
}

/**
 * Show a message using some style
 * @param {string} text The text to display
 * @param {number} duration An optional duration in milliseconds to show the message (default to 5 seconds).
 * @param {string} className The class to aaply to the message (for styling), currently MsgInfo and MsgError is used (use the Fail or Info function instead).
 * @param {boolean} noCopyOnClick By default the text message is copied to the clipboard on click (and then removed), set to true to disable the copying.
 */
function Message(text, duration, className, noCopyOnClick) {
    console.log(text);
    if ((typeof duration === "boolean") && duration)
        duration = 1000 * 60 * 60 * 24;
    if (!duration)
        duration = 5000;

    const ke = "FailElement";
    const kt = "FailTimer";

    const el = document.body;
    let fe = el[ke];

    function Del() {
        fe.remove();
        el[ke] = null;
        el[kt] = null;
    }

    if (!fe) {
        fe = document.createElement("SysWeaver-Fail");
        fe.title = noCopyOnClick ? _TF("Click to remove", "A tool tip for a message shown to the user") : _TF("Click to copy to clipboard and remove!", "A tool tip for a message shown to the user");
        el.appendChild(fe);
        el[ke] = fe;
        fe.onclick = async ev => {
            if (badClick(ev))
                return;
            const text = fe.textContent;
            clearTimeout(el[kt]);
            Del();
            if (!noCopyOnClick)
                await ValueFormat.copyToClipboard(text);
        }
    } else {
        clearTimeout(el[kt]);
    }
    fe.className = className;
    fe.textContent = text;
    el[kt] = setTimeout(Del, duration);
}


/**
 * Compute a rank as to how well as set of string matches a search string, all string supplied must have the same casing.
 * @param {string} searchFor The string to search for, may not be null or empty.
 * @param {...string} inTexts Any number of strings to match against, the first string gets slighly better score and so on.
 * @returns A number with a score, zero means that no matching is found what so over
 */
function RankScoreMatch(searchFor, ...inTexts) {
    let rank = 0;
    const l = inTexts.length;
    const sl = searchFor.length;
    for (let i = 0; i < l; ++i) {
        const text = inTexts[i];
        if (!text)
            continue;
        const c = 1.0 / (1 + i * 0.5);
        for (let j = 0; ;) {
            const p = text.indexOf(searchFor, j);
            if (p < 0)
                break;
            j = p + sl;
            const pos = 1.0 / (1 + p * 0.5);
            rank += (c * pos);
        }
    }
    return rank;
}


function onLink(ev, url, target) {
    if (badClick(ev))
        return;
    Open(url, target);
}



function fixLinks() {
    const els = Array.prototype.slice.call(document.getElementsByTagName("a"));
    const el = els.length;
    for (let i = 0; i < el; ++i)
    {
        const a = els[i];
        let target = a.target;
        if ((!target) || (target.length <= 0))
            target = "_self";
        const c = a.firstElementChild;
        if (c) {
            if (c.tagName === "IMG") {
                const n = ValueFormat.createLink(a.href, c.src, target, a.title ?? c.title, true);
                a.replaceWith(n);
            } else {
                console.warn("Can't fix a tag!");
            }
        } else {
            const n = ValueFormat.createLink(a.href, a.textContent, target, a.title);
            a.replaceWith(n);
        }
    }
}


class ValueFormat {


    static Current = document.currentScript?.src ?? ".";

    // Set the element text (textContent) but respecting leading and trailing spaces
    static setElementText(element, text) {
        text = "" + text;
        const tl = text.length;
        let s;
        for (s = 0; s < tl; ++s) {
            if (text.charAt(s) !== ' ')
                break;
        }
        let e;
        for (e = 0; e < tl; ++e) {
            if (text.charAt(tl - 1 - e) !== ' ')
                break;
        }
        if ((s === 0) && (e === 0)) {
            element.textContent = text;
            return;
        }
        const nb = "&nbsp;";
        const sl = tl - (s + e);
        if (sl <= 0) {
            element.innerHTML = nb.repeat(tl);
            return;
        }
        element.textContent = text.substr(s, sl);
        if (s > 0) {
            if (e > 0)
                element.innerHTML = nb.repeat(s) + element.innerHTML + nb.repeat(e);
            else
                element.innerHTML = nb.repeat(s) + element.innerHTML;
        } else {
            element.innerHTML = element.innerHTML + nb.repeat(e);
        }
    }


    static AddNonNullLine(to, header, value) {
        if (value == null)
            return to;
        const t = typeof value;
        if (t === "undefined")
            return to;
        if ((t === "string") && (value.length <= 0))
            return to;
        if (!to)
            to = "";
        if (to.length > 0)
            to += "\n";
        value = ("" + value).trim();
        const isMultiLine = (value.indexOf('\n') >= 0) || (value.indexOf('\r') >= 0);
        if (header)
            to += (header + (isMultiLine ? ":\n" : ": "));
        to += value;
        return to;
    }

    static removeCamelCase(str, space) {
        if (!space)
            space = ' ';
        let sb = "";
        let prevIsUpper = true;
        const sl = str.length;
        for (let i = 0; i < sl; ++ i)
        {
            const c = str.charAt(i);
            const isUpper = (c === c.toUpperCase());
            if (isUpper && (!prevIsUpper)) {
                sb += space;
                sb += c.toLowerCase();
                prevIsUpper = true;
                continue;
            }
            sb += c;
            prevIsUpper = isUpper;
        }
        return sb;
    }

    static toString = function (value, decimals) {
        if (decimals < 0) {
            if (Math.floor(value) === value)
                decimals = 0;
            else
                decimals = -decimals;
        }
        const haveDecimals = (!!decimals) && (decimals > 0);
        if (haveDecimals)
        {
            const scale = Math.pow(10, decimals);
            value = Math.round(value * scale) / scale;
        }

        const v = '' + value;
        let r = v;
        let f = '';
        const p = v.indexOf('.');
        if (p >= 0) {
            r = v.slice(0, p);
            f = v.slice(p + 1);
        }
        let rl = r.length;
        for (; ;) {
            rl -= 3;
            if (rl <= 0)
                break;
            r = r.slice(0, rl) + ' ' + r.slice(rl);
        }
        if (!haveDecimals)
            return r;
        const fl = f.length;
        if (fl < decimals)
            f = f.padEnd(decimals, '0');
        else if (fl > decimals)
            f = f.slice(0, decimals);
        return r + '.' + f;
    }

    static keyValueSplit = function (text, split) {
        const s = text.indexOf(split);
        if (s < 0)
            return [text, ""];
        const v = text.substring(s + split.length).trim();
        return [text.substring(0, s).trim(), v];
    }

    static countSuffix(count, singularSuffix, pluralSuffix) {
        switch (count) {
            case 0:
                return _TF("zero", 'The prefix part of some text, such as "zero bananas"') + pluralSuffix;
            case 1:
                return _TF("one", 'The prefix part of some text, such as "one banana"') + singularSuffix;
            case 2:
                return _TF("two", 'The prefix part of some text, such as "two bananas"') + pluralSuffix;
            case 3:
                return _TF("three", 'The prefix part of some text, such as "three bananas"') + pluralSuffix;
            case 4:
                return _TF("four", 'The prefix part of some text, such as "four bananas"') + pluralSuffix;
            case 5:
                return _TF("five", 'The prefix part of some text, such as "five bananas"') + pluralSuffix;
            case 6:
                return _TF("six", 'The prefix part of some text, such as "six bananas"') + pluralSuffix;
            case 7:
                return _TF("seven", 'The prefix part of some text, such as "seven bananas"') + pluralSuffix;
            case 8:
                return _TF("eight", 'The prefix part of some text, such as "eight bananas"') + pluralSuffix;
            case 9:
                return _TF("nine", 'The prefix part of some text, such as "nine bananas"') + pluralSuffix;
        }
        return "" + count + pluralSuffix;
    }

    /*
    static countPrefix(singularPrefix, pluralPrefix, count) {
        switch (count) {
            case 0:
                return pluralPrefix + "no";
            case 1:
                return singularPrefix + "one";
            case 2:
                return pluralPrefix + "two";
            case 3:
                return pluralPrefix + "three";
            case 4:
                return pluralPrefix + "four";
            case 5:
                return pluralPrefix + "five";
            case 6:
                return pluralPrefix + "six";
            case 7:
                return pluralPrefix + "seven";
            case 8:
                return pluralPrefix + "eight";
            case 9:
                return pluralPrefix + "nine";
        }
        return pluralSuffix + count;
    }
    */
    static stringFormatArgs = function (format, args) {
        if (!format)
            return undefined;
        const upperArgs = [];
        const lowerArgs = [];
        return format.replace(/{[_^]?(\d+)}/g, function (match, number) {
            if (match[1] == '_') {
                const val = args[number];
                if (typeof val === 'undefined')
                    return match;
                let lv = lowerArgs[number];
                if (!lv) {
                    lv = ("" + val).toLowerCase();
                    lowerArgs[number] = lv;
                }
                return lv;
            }
            if (match[1] == '^') {
                const val = args[number];
                if (typeof val === 'undefined')
                    return match;
                let lv = upperArgs[number];
                if (!lv) {
                    lv = ("" + val).toUpperCase();
                    upperArgs[number] = lv;
                }
                return lv;
            }
            const val = args[number];
            if (typeof val === 'undefined')
                return match;
            return val;
        });
    };
    static stringFormat = function (format) {
        const args = Array.prototype.slice.call(arguments, 1);
        return ValueFormat.stringFormatArgs(format, args);
    };

    static formatSuffix(value, suffix, emptySuffix, k)
    {
        const isNeg = value < 0;
        const prefix = isNeg ? "- " : "";
        if (isNeg)
            value = -value;
        if (!k)
            k = 1024;
        const cutOff = k * 5;
        if (value < cutOff)
            return prefix + ValueFormat.toString(value, 0) + ' ' + emptySuffix;
        value /= k;
        if (value < cutOff)
            return prefix + ValueFormat.toString(value, 1) + ' k' + suffix;
        value /= k;
        if (value < cutOff)
            return prefix + ValueFormat.toString(value, 1) + ' M' + suffix;
        value /= k;
        if (value < cutOff)
            return prefix + ValueFormat.toString(value, 1) + ' G' + suffix;
        value /= k;
        if (value < cutOff)
            return prefix + ValueFormat.toString(value, 1) + ' T' + suffix;
        value /= k;
        return prefix + ValueFormat.toString(value, 1) + ' E' + suffix;
    }


    static formatTimeSpanMs(value, blankIfNeg) {
        const isNeg = value < 0;
        if (isNeg) {
            if (blankIfNeg)
                return typeof blankIfNeg === "string" ? blankIfNeg : "";
            value = -value;
        }
        const prefix = isNeg ? "-" : "";
        if (value < 100)
            return prefix + _T("{0} ms", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of milli seconds, example: "13 ms"');
        value /= 1000;
        if (value < 10)
            return prefix + _T("{0} seconds", ValueFormat.toString(value, 1), 'Text formatting for elapsed number of seconds, example: "48 seconds"');
        if (value < 600)
            return prefix + _T("{0} seconds", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of seconds, example: "48 seconds"');
        value /= 60;
        if (value < 600)
            return prefix + _T("{0} minutes", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of minutes, example: "5 minutes"');
        value /= 60;
        if (value < (3 * 24))
            return prefix + _T("{0} hours", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of hours, example: "9 hours"');
        value /= 24;
        if (value < (3 * 365))
            return prefix + _T("{0} days", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of days, example: "5 days"');
        value /= 365.242374;
        return prefix + _T("{0} years", ValueFormat.toString(value, 0), 'Text formatting for elapsed number of years, example: "7 years"');
    }

    static formatTimeSpan(value, zeroAsThis) {
        value = "" + value;
        if (value.length == 0)
            return "-";
        const isNeg = value[0] == '-';
        const prefix = isNeg ? "- " : "";
        if (isNeg)
            value = value.substring(1);
        const parts = value.split(':');
        if (parts.length === 1) {
            const vp = parseFloat(value);
            parts = [];
            parts.push("" + ((vp / 3600) | 0));
            parts.push("" + (((vp / 60) | 0) % 60));
            parts.push("" + (vp % 60));
        }
        const dh = parts[0].split('.');
        const dhl = dh.length;
        const days = dhl > 1 ? parseInt(dh[0]) : 0;
        if (days >= (3 * 365))
            return prefix + _T("{0} years", ValueFormat.toString(days / 365.242374, 0), 'Text formatting for elapsed number of years, example: "7 years"');
        if (days >= 3)
            return prefix + _T("{0} days", ValueFormat.toString(days, 0), 'Text formatting for elapsed number of days, example: "5 days"');
        const hours = parseInt(dh[dhl - 1]) + (days * 24);
        if (hours >= 10)
            return prefix + _T("{0} hours", ValueFormat.toString(hours, 0), 'Text formatting for elapsed number of hours, example: "9 hours"');
        const minutes = parseInt(parts[1]) + (hours * 60);
        if (minutes >= 10)
            return prefix + _T("{0} minutes", ValueFormat.toString(minutes, 0), 'Text formatting for elapsed number of minutes, example: "5 minutes"');
        const seconds = parseFloat(parts[2]) + (minutes * 60);
        if (seconds == 0)
            return zeroAsThis ? zeroAsThis : "0";
        if (seconds >= 10)
            return prefix + _T("{0} seconds", ValueFormat.toString(seconds, 1), 'Text formatting for elapsed number of seconds, example: "48 seconds"');
        const ms = seconds * 1000;
        if (ms >= 10)
            return prefix + _T("{0} ms", ValueFormat.toString(ms, 1), 'Text formatting for elapsed number of milli seconds, example: "13 ms"');
        const us = ms * 1000;
//        if (us >= 10)
        return prefix + _T("{0} µs", ValueFormat.toString(us, 1), 'Text formatting for elapsed number of micro seconds, example: "27 µs"');
//        const ns = us * 1000;
//        return prefix + ValueFormat.toString(ns, ns >= 10 ? 1 : 3) + " ns";
    }

    static TextNode = document.createElement("td");

    static updateText = function (el, text, title, flash) {
        text = (typeof text === 'undefined') || (text === null) ? '' : ('' + text);
        text = text.trim();
        if (title) {
            title = '' + title;
            if (el.title !== title)
                el.title = title;
        }
        if (text === el.textContent)
            if (!el.firstElementChild)
                return false;
        el.textContent = text;
        if (flash) {
            el.classList.remove(flash);
            const o = setTimeout(x => el.classList.remove(flash), 1050);
            setTimeout(x => {
                clearTimeout(o);
                el.classList.add(flash);
            }, 0);
        }
        return true;
    }

    static updateHtml = function (el, html, title, flash) {
        html = (typeof html === 'undefined') || (html === null) ? '' : ('' + html);
        if (title) {
            title = '' + title;
            if (el.title !== title)
                el.title = title;
        }
        if (html === el.innerHTML)
            return false;
        el.innerHTML = html;
        if (flash) {
            el.classList.remove(flash);
            const o = setTimeout(x => el.classList.remove(flash), 1050);
            setTimeout(x => {
                clearTimeout(o);
                el.classList.add(flash);
            }, 0);
        }
        return true;
    }

    static updateFormat = function (el, formatName) {
        const c = el["Format"];
        if (!c) {
            el["Format"] = formatName;
            return true;
        }
        if (c == formatName)
            return false;
        el["Format"] = formatName;
        const u = el.Updater;
        if (u) {
            clearInterval(u);
            el.Updater = null;
        }
        el.textContent = "error";
        const cl = el.classList;
        cl.remove("Right");
        cl.remove("Center");
        cl.remove("Null");
        cl.remove("False");
        cl.remove("Monospaced");
        cl.remove("Text");
        cl.remove("Toggle");
        el.onclick = null;
        return true;
    }

    // Returns null if there is an error, else an array where [0] = true if the data is json, else it's text, [1] = is the text.
    static async readFromClipboard() {
        const c = navigator.clipboard;
        if (!c)
            return null;
        try {
            return await c.readText();
        }
        catch (e)
        {
            console.warn('Failed to read text from clipboard, error: ' + e.message);
            return null;
        }
    }

    static async copyToClipboard(v) {
        try {
            await navigator.clipboard.writeText(v);
            console.log('Copied "' + v + '" to the clipboard');
            return true;
        }
        catch (e) {
            console.warn('Failed to copy "' + v + '" to the clipboard, error: ' + e.message);
            return false;
        }
    }

    static async copyToClipboardInfo(v, genericText) {
        try {
            await navigator.clipboard.writeText(v);
            const m = genericText
                ?
                _TF("Copied text to the clipboard", 'Message displayed when some text is copied to the clipboard.')
                :
                _T('Copied "{0}" to the clipboard', v, 'Message displayed when some text is copied to the clipboard.')
                ;
            console.log(m);
            Info(m, null, true);
            return true;
        }
        catch (e) {
            const m = genericText
                ?
                _T("Failed to copy the text to the clipboard.\{0}", e.message, 'Message displayed when the web page failed to copy some text to the clipboard.{0} is replaced with the java script exception text')
                :
                _T('Failed to copy "{0}" to the clipboard.\n{1}', v, e.message, 'Message displayed when the web page failed to copy some text to the clipboard.{0} is replaced with the text.{1} is replaced with the java script exception text')
                ;
            console.log(m);
            Fail(m, null, true);
            return false;
        }
    }

    static copyOnClick(el, value, addKeyboard, addTitleToElement) {
        const v = (value === null || (typeof(value) === "undefined")) ? null : ('' + value);
        const e = el;
        e.classList.add("MouseClick");
        async function Copy() {
            const val = v === null ? e.textContent : v;
            await ValueFormat.copyToClipboardInfo(val);
        }
        e.onclick = async ev => {
            if (badClick(ev))
                return;
            await Copy();
        };
        if (addKeyboard) {
            keyboardClick(e);
            e.oncopy = Copy;
        }
        const title = v !== null ? _T('Click to copy "{0}" to the clipboard', v, 'Tool tip to indicate that some known text can be copied to the clipboard by clicking on an element') : _T('Click to copy text to the clipboard', 'Tool tip to indicate that some text can be copied to the clipboard by clicking on an element');
        if (addTitleToElement)
            el.title = el.title ? (el.title + "\n\n" + title) : title;
        return title;
    }

    static joinNonEmpty(delim, ...args) {
        return args.filter(Boolean).join(delim ?? '\n');
    }

    static updateDefault(el, value, formats, nextValue, flash, type, onRefresh) {

        if (ValueFormat.updateFormat(el, "Default"))
            el.classList.add("Text");
        const text = value === null ? "" : ValueFormat.stringFormat(formats[1] ?? "{0}", value, nextValue);
        //  Get title (and copy func)
        let title = ValueFormat.stringFormat(formats[2] ?? (text != value ? "Raw: {2}" : ""), text, nextValue, value);
        let copy = formats[3];
        if (copy || typeof copy === "undefined") {
            let copyVal = value;
            if (typeof copy === "string")
                copyVal = ValueFormat.stringFormat(copy, text, nextValue, value);
            title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(el, copyVal));
        }
        ValueFormat.updateText(el, text, title, flash);
    }

    static updateType(el, value, formats, nextValue, flash, type, onRefresh) {

        if (formats.length <= 0)
            formats.splice(0, 0, 2, "{0}", "*../edit/type.html?q={2}", _TF('Click to shown information about the type "{0}".', "Tool tip to indicate that extra information about a C# type can be viewed by clicking on the element"));
        ValueFormat.updateUrl(el, value, formats, nextValue, flash, type, onRefresh);
    }
    

    static updateNumber(el, value, formats, nextValue, flash, type, onRefresh) {
        if (ValueFormat.updateFormat(el, "Number")) {
            el.classList.add("Right");
            el.classList.add("Monospaced");
        }
        //  Get number of decimals
        let decimals = formats[1];
        if (!decimals)
            decimals = -2;
        if (decimals < 0) {
            if (ValueFormat.isDecimal.get(type))
                decimals = -decimals;
            else
                decimals = 0;
        }
        //  Format value
        const valueStr = ValueFormat.toString(value, decimals);
        const text = ValueFormat.stringFormat(formats[2] ?? "{0}", valueStr, nextValue, value);
        //  Get title (and copy func)
        let title = ValueFormat.stringFormat(formats[3] ?? "Raw: {2}", valueStr, nextValue, value, text);
        let copy = formats[4];
        if (copy || typeof copy === "undefined")
            title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(el, value));
        //  Update
        if (ValueFormat.updateText(el, text, title, flash)) {
            if (value < 0) {
                el.classList.remove("Null");
                el.classList.add("False");
            } else {
                if (value > 0) {
                    el.classList.remove("Null");
                    el.classList.remove("False");
                } else {
                    el.classList.add("Null");
                    el.classList.remove("False");
                }
            }
        }
    }


    static LinkNode = document.createElement("SysWeaver-Link");
    static ImgNode = document.createElement("img");

    // Returns [url, target]
    static getUrlTarget(url) {
        if (!url)
            return ["", ""];
        const l = url.length;
        if (l <= 0)
            return ["", ""];
        const p = url.charAt(0);
        if (p == '+')
            return [url.substring(1), "_blank"];
        if (p == '*')
            return [url.substring(1), "_self"];
        if (p == '^')
            return [url.substring(1), "_top"];
        if (p == '-')
            return [url.substring(1), "_parent"];
        return [url, "_blank"];
    }


    static setOnClickAttr(element, url, target) {
        element.setAttribute("onclick", "onLink(event,'" + url + "','" + target + "')");
    }



    static createLink(url, text, target, title, isImage, useAttr) {
        const a = document.createElement("SysWeaver-Link");
        a.tabIndex = "0";
        const u = url;
        const t = target ?? "_self";
        if (useAttr)
            ValueFormat.setOnClickAttr(a, u, t);
        else {
            a.onclick = ev => onLink(ev, u, t);
            keyboardClick(a);
        }
        if (text) {
            if (isImage) {
                a.classList.add("Image");
                const i = document.createElement("img");
                i.src = text;
                a.appendChild(i);
            } else {
                a.textContent = text;
            }
        }
        if (title)
            a.title = title;
        return a;
    }

    static updateUrl(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Url");
        if (!value) {
            ValueFormat.updateText(el, "", "", flash);
            return;
        }
        const text = ValueFormat.stringFormat(formats[1] ?? "{0}", value, nextValue);
        const urlTarget = ValueFormat.getUrlTarget(ValueFormat.stringFormat(formats[2] ?? "{2}", value, nextValue, text));
        const url = GetAbsolutePath(urlTarget[0]);
        const target = urlTarget[1];
        const title = ValueFormat.stringFormat(formats[3] ?? _TF('Click to open "{3}".', "Tool tip that indicates that an url can be viewed by clicking on an element"), value, nextValue, text, url);
        if (url.length >= 0)
        {
            const a = ValueFormat.LinkNode;
            ValueFormat.setOnClickAttr(a, url, target);
            //a.target = target;
            //a.href = url;
            a.textContent = text;
            ValueFormat.updateHtml(el, a.outerHTML, title, flash);
            return;
        }
        ValueFormat.updateText(el, text, title, flash);
    }

    static updateImg(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Img");
        if (!value) {
            ValueFormat.updateText(el, "", "", flash);
            return;
        }
        const relPrefix = formats[6];
        if (relPrefix && (!IsAbsolutePath(value)))
            value = relPrefix + value;
        let srcLink = ValueFormat.stringFormat(formats[1] ?? "{0}", value, nextValue);
        const urlTarget = ValueFormat.getUrlTarget(formats[2] ? ValueFormat.stringFormat(formats[2], value, nextValue, srcLink) : null);
        const url = GetAbsolutePath(urlTarget[0]);
        const target = urlTarget[1];
        const title = ValueFormat.stringFormat(formats[3] ?? (url ? _TF('Click to open "{3}".', "Tool tip that indicates that an image can be viewed by clicking on an element") : ""), value, nextValue, srcLink, url);
        const a = ValueFormat.ImgNode;
        const f = srcLink.charAt(0);
        let align = null;
        if (f === '-') {
            align = "Left";
            srcLink = srcLink.substring(1);
        }
        if (f === '*')
            srcLink = srcLink.substring(1);
        if (f === '+') {
            align = "Right";
            srcLink = srcLink.substring(1);
        }
        a.src = srcLink;
        if (url.length > 0) {
            ValueFormat.setOnClickAttr(a, url, target);
            a.classList.add("ImgLink");
        } else {
            a.removeAttribute("onclick");
            a.classList.remove("ImgLink");
        }
        const maxW = parseInt(formats[4]);
        const maxH = parseInt(formats[5]);
        let style = "";
        if (maxW > 0)
            style += ("max-width:calc(var(--ThemeIconSize)*" + maxW + "px);");
        if (maxH > 0)
            style += ("max-height:calc(var(--ThemeIconSize)*" + maxH + "px);");
        if (style.length > 0)
            a.setAttribute("style", style);
        else
            a.removeAttribute("style");

        if (align)
            el.classList.add(align);
        ValueFormat.updateHtml(el, a.outerHTML, title, flash);
    }


    static getTimeStampTitle(dd, onlyDate, prefix, tab) {
        if (typeof dd === "string")
            dd = new Date(dd);
        if (typeof dd === "number")
            dd = new Date(dd);
        if (!tab)
            tab = "";
        if (!prefix)
            prefix = _TF("At", 'Tool tip row prefix to some time stamp, ex: "At: 1997-04-12 15:32:30"') + ": ";
        tab = "\n" + tab;
        let c = dd.toISOString().split('T');
        c = c[0] + " " + c[1].split('.')[0];
        if (onlyDate)
            c = c.substring(0, c.length - 9);
        const title = prefix + c + tab +
            _TF("Local", "Tool tip row prefix to some time stamp written using js Date.toLocaleString() syntax") + ": " + dd.toLocaleString() + tab + 
            _TF("UTC", "Tool tip row prefix to some time stamp written using js Date.toUTCString() syntax") + ": " + dd.toUTCString() + tab +
            _TF("Full", "Tool tip row prefix to some time stamp written using js Date.toString() syntax") + ": " + dd.toString() + tab +
            _TF("ISO", "Tool tip row prefix to some time stamp written using js Date.toISOString() syntax") + ": " + dd.toISOString();
        return [title, c];
    }


    static updateDateTimeLive(el, value, formats, nextValue, flash, type, onRefresh, noCopy, blankIfNeg, invert) {
        if (ValueFormat.updateFormat(el, "DateTime")) {
            el.classList.add("Monospaced");
            el.classList.add("Right");
        }
        if (value === "0001-01-01T00:00:00"){
            ValueFormat.updateText(el, "-", _TF("Not set", "Tool tip indicating that the value is not set"), flash);
            return el;
        }
        if (!value) {
            ValueFormat.updateText(el, _TF("Now", "Text that indicate that an event happened right now"), _TF("Right now", "Tool tip indicating that that an event happened right now"), flash);
            return el;
        }

        const dd = value instanceof Date ? value : new Date(value);
        let title = ValueFormat.getTimeStampTitle(dd)[0];
        if (!noCopy)
            title += "\n\n" + ValueFormat.copyOnClick(el, value);

        const scale = invert ? -1 : 1;
        const minute = 60 * 1000;
        let elapsed = (new Date() - dd) * scale;
        const fmt = (formats ? formats[2] : null) ?? "{0}";

        const tt = ValueFormat.stringFormat(fmt, ValueFormat.formatTimeSpanMs(elapsed, blankIfNeg));
        ValueFormat.updateText(el, tt, title, flash);
        let haveBeenAttached = isInDocument(el);
        const update = () => {
            const isAttached = isInDocument(el);
            if (haveBeenAttached) {
                if (!isAttached)
                    return;
            }
            haveBeenAttached |= isAttached;
            elapsed = (new Date() - dd) * scale;
            let next = 100;
            if (elapsed > (10 * 60 * 1000))
                next = Math.ceil(elapsed / minute) * minute - elapsed + 1000;
            const t = ValueFormat.stringFormat(fmt, ValueFormat.formatTimeSpanMs(elapsed, blankIfNeg));
            if (el.textContent != t)
                el.textContent = t;
            el.Updater = setTimeout(update, next);
        }
        update();
        return el;
    }

    static updateDateTime(el, value, formats, nextValue, flash, type, onRefresh, onlyDate, noCopy) {
        if (ValueFormat.updateFormat(el, "DateTime")) {
            el.classList.add("Monospaced");
            el.classList.add("Right");
        }
        if (value === "0001-01-01T00:00:00") {
            ValueFormat.updateText(el, "-", _TF("Not set", "Tool tip indicating that the value is not set (is undefined or null)"), flash);
            return el;
        }
        if (!value) {
            let haveBeenAttached = isInDocument(el);
            const update = () => {
                const isAttached = isInDocument(el);
                if (haveBeenAttached) {
                    if (!isAttached)
                        return;
                }
                haveBeenAttached |= isAttached;
                const dd = new Date();
                const v = ValueFormat.getTimeStampTitle(dd);
                let title = v[0];
                if (!noCopy)
                    title += "\n\n" + ValueFormat.copyOnClick(el, value);
                ValueFormat.updateText(el, v[1], title, flash);
                el.Updater = setTimeout(update, 1000);
            }
            update();
            return el;
        }
        const dd = new Date(value);
        const v = ValueFormat.getTimeStampTitle(dd);
        let title = v[0];
        if (!noCopy)
            title += "\n\n" + ValueFormat.copyOnClick(el, value);
        ValueFormat.updateText(el, v[1], title, flash);
        return el;
    }

    static updateTimeSpan(el, value, formats, nextValue, flash, type, onRefresh) {

        if (ValueFormat.updateFormat(el, "TimeSpan")) {
            el.classList.add("Monospaced");
            el.classList.add("Right");
        }
        const c = ValueFormat.formatTimeSpan(value, formats && formats[4]);
        const text = ValueFormat.stringFormat((formats ? formats[1] : null) ?? "{0}", c, nextValue, value);
        let title = ValueFormat.stringFormat((formats ? formats[2] : null) ?? "Raw: {2}", c, nextValue, value, text);
        title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(el, value));
        ValueFormat.updateText(el, text, title, flash);
        return el;
    }

    static jsonToHtml(jsonText) {
        jsonText = jsonText.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        return jsonText.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
            var cls = 'number';
            if (/^"/.test(match)) {
                if (/:$/.test(match)) {
                    cls = 'key';
                } else {
                    cls = 'string';
                }
            } else if (/true|false/.test(match)) {
                cls = 'boolean';
            } else if (/null/.test(match)) {
                cls = 'null';
            }
            return '<span class="' + cls + '">' + match + '</span>';
        });
    }

    static updateJson(el, value, formats, nextValue, flash, type, onRefresh) {

        if (ValueFormat.updateFormat(el, "Json"))
            el.classList.add("Monospaced");
        if (!value) {
            if (el.innerText !== "") {
                el.title = "";
                el.classList.remove("Capped");
                el.innerText = "";
                el.onclick = null;
            }
            return;
        }
        value = "" + value;
        let capLen = parseInt(formats[1]);
        if (capLen < 4)
            capLen = 4;
        let cap = capLen;
        const capN = value.indexOf('\n');
        const capR = value.indexOf('\r');
        if (capN >= 0)
            if (capN < cap)
                cap = capN;
        if (capR >= 0)
            if (capR < cap)
                cap = capR;
        const isCapped = cap < value.length;
        const capText = isCapped ? value.substring(0, cap) : value;
        if (isCapped)
            el.classList.add("Capped");
        else
            el.classList.remove("Capped");
        let fmtText = value;
        try {
            fmtText = JSON.stringify(JSON.parse(value), null, 4);
        }
        catch
        {
        }
        let title = ValueFormat.stringFormat(formats[2] ?? "{1}", value, fmtText, capText);
        let copy = formats[3];
        if (copy)
            copy = ValueFormat.stringFormat(copy, value, fmtText, capText);
        el.onclick = async ev => {
            if (ValueFormat.haveElementSelection(el))
                return;
            if (badClick(ev))
                return;
            await PopUp((el, closeFn, buttons) => {
                const e = document.createElement("SysWeaver-Code");
                e.classList.add("SysWeaverCode");
                e.innerHTML = ValueFormat.jsonToHtml(fmtText);
                el.classList.add("Resize");
                el.appendChild(e);
                if (buttons && copy)
                    buttons.appendChild(new ColorIcon(
                        "IconCopy", "IconColorThemeAcc1", 32, 32,
                        _TF("Click to copy the original json text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked"),
                        () => {
                            if (ValueFormat.copyToClipboard(copy))
                                Info(_TF("Copied text to the clipboard", "A pop-up message telling the user that some text was copied to the clipboard"));
                        }).Element);
            }, false, false, null, true);
        };
        ValueFormat.updateText(el, capText, title, flash);
        return el;
    }

    static async SetMarkDown(el, text) {
        el.innerHTML = await ValueFormat.MarkDownToHTML(text);
    }

    static async MarkDownToHTML(text) {
        const res = await includeJs(null, "marked.js");
        if (res.loaded) {
            const okMap = new Map();
            okMap.set("<hr>", 1);
            okMap.set("</hr>", 1);
            okMap.set("<br>", 1);
            okMap.set("</br>", 1);
            okMap.set("<img>", 1);
            okMap.set("</img>", 1);

            okMap.set("<figure>", 1);
            okMap.set("</figure>", 1);
            okMap.set("<figcaption>", 1);
            okMap.set("</figcaption>", 1);

            okMap.set("<br>", 1);
            okMap.set("<br>", 1);
            okMap.set("<b>", 1);
            okMap.set("<strong>", 1);
            okMap.set("<i>", 1);
            okMap.set("<em>", 1);
            okMap.set("<mark>", 1);
            okMap.set("<small>", 1);
            okMap.set("<del>", 1);
            okMap.set("<ins>", 1);
            okMap.set("<sub>", 1);
            okMap.set("<sup>", 1);
            okMap.set("</b>", 1);
            okMap.set("</strong>", 1);
            okMap.set("</i>", 1);
            okMap.set("</em>", 1);
            okMap.set("</mark>", 1);
            okMap.set("</small>", 1);
            okMap.set("</del>", 1);
            okMap.set("</ins>", 1);
            okMap.set("</sub>", 1);
            okMap.set("</sup>", 1);

            okMap.set("<blockquote>", 1);
            okMap.set("<q>", 1);
            okMap.set("<abbr>", 1);
            okMap.set("<address>", 1);
            okMap.set("<cite>", 1);
            okMap.set("<bdo>", 1);

            okMap.set("/<blockquote>", 1);
            okMap.set("/<q>", 1);
            okMap.set("/<abbr>", 1);
            okMap.set("/<address>", 1);
            okMap.set("/<cite>", 1);
            okMap.set("/<bdo>", 1);

            let inLink = false;
            const renderer = {
                link(l) {
                    let text = null;
                    inLink = true;
                    try {
                        text = this.parser.parseInline(l.tokens);
                    }
                    finally {
                        inLink = false;
                    }
                    let href = l.href;
                    if (!IsAbsolutePath(href))
                        href = appendTheme(href);
                    let escapedText = makeHtmlAttributeSafe(href);
                    let title = l.title;
                    if (!title)
                        title = _T('Click to open "{0}" in a new tab', href, "Tool tip that informs the user that an URL can be opened by clicking on the element");
                    if (text.startsWith("<img"))
                        escapedText += '" class="Image"';
                    return `<a target="_blank" title="${makeHtmlAttributeSafe(title)}" href="${escapedText}">${text}</a>`;
                },
                image(l) {
                    if (inLink)
                        return false;
                    const href = l.href;
                    const escapedText = makeHtmlAttributeSafe(href);
                    let title = l.title;
                    if (!title)
                        title = _T('Click to open "{0}" in a new tab', href, "Tool tip that informs the user that an image can be opened by clicking on the element");
                    if (l.text)
                        title = l.text + "\n\n" + title;
                    return `<a class="Image" target="_blank" title="${makeHtmlAttributeSafe(title)}" href="${escapedText}"><img src="${escapedText}" /></a>`;
                },
                html(l) {
                    const e = l.raw.indexOf('>');
                    if (e > 0) {
                        if (okMap.get(l.raw.substring(0, e + 1)))
                            return false;
                        if (l.raw.startsWith("<img "))
                            return false;
                    }
                    return "";
                }

            };
            marked.use({ renderer });
        }
        return marked.parse(text);
    }

    static isAnythingSelected(selection) {
        selection = selection ?? window.getSelection();
        const selCount = selection.rangeCount;
        if (selCount <= 0)
            return false;
        for (let i = 0; i < selCount; ++i) {
            const s = selection.getRangeAt(i);
            if (s.startOffset !== s.endOffset)
                return true;
        }
        return false;
    }


    static haveElementSelection(el) {
        try {
            const sel = window.getSelection();
            if (!sel.anchorNode)
                return false;
            if (sel.anchorNode.parentNode === el)
                return ValueFormat.isAnythingSelected(sel);
        }
        catch
        {
        }
        return false;
    }

    static updateMarkDown(el, value, formats, nextValue, flash, type, onRefresh) {

        ValueFormat.updateFormat(el, "MarkDown");
        if (!value) {
            if (el.innerText !== "") {
                el.title = "";
                el.classList.remove("Capped");
                el.innerText = "";
                el.onclick = null;
            }
            return;
        }
        value = "" + value;
        let capLen = parseInt(formats[1]);
        if (capLen < 4)
            capLen = 4;
        let cap = capLen;
        const capN = value.indexOf('\n');
        const capR = value.indexOf('\r');
        if (capN >= 0)
            if (capN < cap)
                cap = capN;
        if (capR >= 0)
            if (capR < cap)
                cap = capR;
        const isCapped = cap < value.length;
        const capText = isCapped ? value.substring(0, cap) : value;
        if (isCapped)
            el.classList.add("Capped");
        else
            el.classList.remove("Capped");
        let title = ValueFormat.stringFormat(formats[2] ?? "{0}", value, capText);
        let copy = formats[3];
        if (copy)
            copy = ValueFormat.stringFormat(copy, value, capText);


        el.onclick = async ev => {
            if (ValueFormat.haveElementSelection(el))
                return;
            if (badClick(ev))
                return;
            await PopUp(async (el, closeFn, buttons) => {
                const e = document.createElement("SysWeaver-MdText");
                el.classList.add("Resize");
                el.appendChild(e);
                e.innerHTML = await ValueFormat.MarkDownToHTML(value);
                if (buttons && copy)
                    buttons.appendChild(new ColorIcon(
                        "IconCopy", "IconColorThemeAcc1", 32, 32,
                        _TF("Click to copy the original MD text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked"),
                        () => {
                            if (ValueFormat.copyToClipboard(copy))
                                Info(_TF("Copied text to the clipboard", "A pop-up message telling the user that some text was copied to the clipboard"));
                        }).Element);
            }, false, false, null, true);
        };
        ValueFormat.updateText(el, capText, title, flash);
        return el;
    }


    static updateLongText(el, value, formats, nextValue, flash, type, onRefresh) {

        ValueFormat.updateFormat(el, "LongText");
        if (!value) {
            if (el.innerText !== "") {
                el.title = "";
                el.classList.remove("Capped");
                el.innerText = "";
                el.onclick = null;
            }
            return;
        }
        value = "" + value;
        let capLen = parseInt(formats[1]);
        if (capLen < 4)
            capLen = 4;
        let cap = capLen;
        const capN = value.indexOf('\n');
        const capR = value.indexOf('\r');
        if (capN >= 0)
            if (capN < cap)
                cap = capN;
        if (capR >= 0)
            if (capR < cap)
                cap = capR;
        const isCapped = cap < value.length;
        const capText = isCapped ? value.substring(0, cap) : value;
        if (isCapped)
            el.classList.add("Capped");
        else
            el.classList.remove("Capped");
        let title = ValueFormat.stringFormat(formats[2] ?? "{0}", value, capText);
        let copy = formats[3];
        if (copy)
            copy = ValueFormat.stringFormat(copy, value, capText);
        el.onclick = async ev => {
            if (ValueFormat.haveElementSelection(el))
                return;
            if (badClick(ev))
                return;
            await PopUp((el, closeFn, buttons) => {
                const e = document.createElement("SysWeaver-LongText");
                el.classList.add("Resize");
                el.appendChild(e);
                e.innerText = value;
                if (buttons && copy)
                    buttons.appendChild(new ColorIcon(
                        "IconCopy", "IconColorThemeAcc1", 32, 32,
                        _TF("Click to copy the full text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked"),
                        () => {
                            if (ValueFormat.copyToClipboard(copy))
                                Info(_TF("Copied text to the clipboard", "A pop-up message telling the user that some text was copied to the clipboard"));
                        }).Element);
            }, false, false, null, true);
        };
        ValueFormat.updateText(el, capText, title, flash);
        return el;
    }
    

    static updateBoolean(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Boolean");
        const c = value ? _TF('☑ True', 'A boolean true indicator, displayed when something is true') : _TF('☐ False', 'A boolean false indicator, displayed when something is false');
        const title = ValueFormat.joinNonEmpty("\n\n", value, ValueFormat.copyOnClick(el, value));
        if (ValueFormat.updateText(el, c, title, flash)) {
            if (!value) {
                el.classList.remove("Text");
                el.classList.add("False");
            } else {
                el.classList.add("Text");
                el.classList.remove("False");
            }
        }
        return el;
    }

    static formatByteSize(value) {
        return ValueFormat.formatSuffix(value, "b", "bytes", 1024)
    }

    static updateByteSize(el, value, formats, nextValue, flash, type, onRefresh) {
        if (ValueFormat.updateFormat(el, "ByteSize")) {
            el.classList.add("Right");
            el.classList.add("Monospaced");
        }
        //  Format value
        const valueStr = ValueFormat.formatByteSize(value);
        const text = ValueFormat.stringFormat((formats ? formats[2] : null) ?? "{0}", valueStr, nextValue, value);
        //  Get title (and copy copy func)
        let title = ValueFormat.stringFormat((formats ? formats[3] : null) ?? "Raw: {2}", valueStr, nextValue, value, text);
        //if (formats[4])
        title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(el, value));
        //  Update
        ValueFormat.updateText(el, text, title, flash);
        return el;
    }

    static updateByteSpeed(el, value, formats, nextValue, flash, type, onRefresh) {
        if (ValueFormat.updateFormat(el, "ByteSize")) {
            el.classList.add("Right");
            el.classList.add("Monospaced");
        }
        //  Format value
        const valueStr = ValueFormat.formatSuffix(value, "b/s", "bytes/s", 1024);
        const text = ValueFormat.stringFormat(formats[2] ?? "{0}", valueStr, nextValue, value);
        //  Get title (and copy func)
        let title = ValueFormat.stringFormat(formats[3] ?? "Raw: {2}", valueStr, nextValue, value, text);
        //if (formats[4])
        title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(el, value));
        //  Update
        ValueFormat.updateText(el, text, title, flash);
        return el;
    }


    static TagSpanNode = document.createElement("span");

    static updateTags(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Tags");
        if (value == null) {
            el.innerHTML = "";
            return;
        }
        const tags = value.length == 0 ? ["🔑"] : value.split(',');
        const tl = tags.length;
        let h = "";
        const fmtText = formats[1] ?? "{1}";
        const fmtTitle = formats[2] ?? "{2}";
        const fmtCopy = formats[3];
        const copy = [];
        for (let i = 0; i < tl; ++i) {
            const tag = tags[i].trim().replace('¤', ',');
            const [c, d] = ValueFormat.keyValueSplit(tag, ':', true);
            const text = ValueFormat.stringFormat(fmtText, tag, c, d);
            let title = ValueFormat.stringFormat(fmtTitle, tag, c, d, text);
            const a = ValueFormat.TagSpanNode;
            if (fmtCopy) {
                const cp = ValueFormat.stringFormat(fmtCopy, tag, c, d, text, title);
                copy[i] = cp;
                title = ValueFormat.joinNonEmpty("\n\n", title, ValueFormat.copyOnClick(a, cp));
            }
            a.textContent = text;
            a.title = title;
            h += a.outerHTML;
        }
        if (ValueFormat.updateHtml(el, h, null, flash)) {
            for (let i = 0; i < tl; ++i) {
                const t = copy[i];
                if (t)
                    ValueFormat.copyOnClick(el.children[i], t);
            }
            if (formats[4])
                el.title = ValueFormat.copyOnClick(el, value);
        }
        return el;
    }

    static updatePerRowFormat(el, value, formats, nextValue, flash, type, onRefresh) {
        if (!nextValue) {
            ValueFormat.updateDefault(el, value, formats, nextValue, flash, "System.String");
            return;
        }
        const s = nextValue.split('|');
        ValueFormat.update(el, s[0], value, flash, s[1], s[2], onRefresh);
        return el;
    }


    static TempToggle = document.createElement("SysWeaver-toggle");

    static updateToggle(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Toggle");
        const args = ("" + value).split(',');
        value = args[0].toLowerCase();
        value = (value == "true") || (value == "1");
        args[0] = value;
        args.splice(1, 0, nextValue);
        const text = value ? ("☑ " + (ValueFormat.stringFormatArgs(formats[1] ?? "True", args))) : ("☐ " + (ValueFormat.stringFormatArgs(formats[2] ?? "False", args)));
        const title = value ?
            (ValueFormat.stringFormatArgs(formats[3] ??
                _TF("Click to uncheck", "Tool tip to indicate that something can be unchecked (unselected) by clicking on the element)"), args))
            :
            (ValueFormat.stringFormatArgs(formats[4] ??
                _TF("Click to check", "Tool tip to indicate that something can be checked (selected) by clicking on the element)"), args));

        const e = ValueFormat.TempToggle;
        e.tabIndex = "0";
        e.textContent = text;
        e.title = title;
        if (ValueFormat.updateHtml(el, e.outerHTML, "", flash)) {
            el.classList.add("Toggle");
            const te = el.firstElementChild;
            let isProcessing = false;
            keyboardClick(te);
            te.onclick = async ev => {
                if (badClick(ev))
                    return;
                if (isProcessing)
                    return;
                isProcessing = true;
                te.classList.add("Null");
                const api = ValueFormat.stringFormatArgs(formats[5] ?? "", args);
                try {
                    await getRequest(api, true);
                }
                catch (ex) {
                    console.log("Request \"" + api + "\" failed!, exception: " + ex)
                }
                te.classList.remove("Null");
                isProcessing = false;
                onRefresh();
            };
        }
        return el;
    }


    static TempAction = document.createElement("SysWeaver-action");
    static TempSpan = document.createElement("SysWeaver-actionText");
    static TempImage = document.createElement("img");

    static updateActions(el, value, formats, nextValue, flash, type, onRefresh) {
        ValueFormat.updateFormat(el, "Actions");
        if (value === null) {
            if (el.innerHTML !== "")
                el.innerHTML = "";
            return;
        }
        const args = ("" + value).split(',');
        args.splice(1, 0, nextValue);
        const count = formats.length - 1;
        let html = "";
        const eaction = ValueFormat.TempAction;
        const espan = ValueFormat.TempSpan;
        const iimg = ValueFormat.TempImage;
        const links = [];
        for (let i = 0; i < count; ++i) {
            const fmts = formats[i + 1].split('|');
            eaction.textContent = "";
            const text = ValueFormat.stringFormatArgs(fmts[0] ?? "", args);
            eaction.title = ValueFormat.stringFormatArgs(fmts[1] ?? "", args);
            eaction.tabIndex = "0";
            links[i] = ValueFormat.stringFormatArgs(fmts[2] ?? "", args);
            const img = ValueFormat.stringFormatArgs(fmts[3] ?? "", args);
            if (img && (img.length > 0))
            {
                const ie = new ColorIcon(img, "IconColorThemeBackground", 20, 20);
                eaction.appendChild(ie.Element);
            }
            espan.textContent = text;
            eaction.appendChild(espan);
            html += eaction.outerHTML;
        }
        if (ValueFormat.updateHtml(el, html, "", flash)) {
            el.classList.add("Toggle");
            for (let i = 0; i < count; ++i) {
                const te = el.children[i];
                const url = links[i];
                if (url && (url != "")) {
                    te.classList.remove("Disabled");
                    let isProcessing = false;
                    keyboardClick(te);
                    te.onclick = async ev => {
                        if (badClick(ev))
                            return;
                        if (isProcessing)
                            return;
                        isProcessing = true;
                        te.classList.add("Null");
                        const t = url.charAt(0);
                        switch (t) {
                            case '&':
                                Open(url.substring(1), "_self");
                                break;
                            case '@':
                                Open(url.substring(1), "_blank");
                                break;
                            default:
                                try {
                                    await getRequest(url, true);
                                }
                                catch (ex) {
                                    console.log("Request \"" + url + "\" failed!, exception: " + ex)
                                }
                                break;
                        }





                        te.classList.remove("Null");
                        isProcessing = false;
                        onRefresh();
                    };
                } else {
                    te.classList.add("Disabled");
                    te.onclick = null;
                }
            }
        }
        return el;
    }

    static isDecimal = new Map([
        ["System.Single", true],
        ["System.Double", true],
        ["System.Decimal", true],
    ]);

    static typeFormatter = new Map([
        ["System.SByte", ValueFormat.updateNumber],
        ["System.Int16", ValueFormat.updateNumber],
        ["System.Int32", ValueFormat.updateNumber],
        ["System.Int64", ValueFormat.updateNumber],
        ["System.Byte", ValueFormat.updateNumber],
        ["System.UInt16", ValueFormat.updateNumber],
        ["System.UInt32", ValueFormat.updateNumber],
        ["System.UInt64", ValueFormat.updateNumber],
        ["System.Single", ValueFormat.updateNumber],
        ["System.Double", ValueFormat.updateNumber],
        ["System.Decimal", ValueFormat.updateNumber],
        ["System.DateTime", ValueFormat.updateDateTime],
        ["System.TimeSpan", ValueFormat.updateTimeSpan],
        ["System.Boolean", ValueFormat.updateBoolean],
        ["System.Type", ValueFormat.updateType],
    ]);

    static formatFormatter = new Map([
        ["Number", ValueFormat.updateNumber],
        ["Url", ValueFormat.updateUrl],
        ["Img", ValueFormat.updateImg],
        ["Tags", ValueFormat.updateTags],
        ["PerRowFormat", ValueFormat.updatePerRowFormat],
        ["ByteSize", ValueFormat.updateByteSize],
        ["ByteSpeed", ValueFormat.updateByteSpeed],
        ["Toggle", ValueFormat.updateToggle],
        ["Actions", ValueFormat.updateActions],
        ["Duration", ValueFormat.updateTimeSpan],
        ["Json", ValueFormat.updateJson],
        ["Text", ValueFormat.updateLongText],
        ["MD", ValueFormat.updateMarkDown],
    ]);

    static update = function (el, type, value, flash, format, nextValue, onRefresh) {
        const formats = format ? format.split(';') : [];
        const ts = ValueFormat.typeFormatter.get(type);
        let fmt = ts ?? ValueFormat.updateDefault;
        const cl = el.classList;
        const remove = new Map();
        let add = null;
        cl.forEach(val => {
            if (val.startsWith("ValueFormat"))
                remove.set(val, 1);
        });
        if (formats.length > 0) {
            add = "ValueFormat" + formats[0];
            const fs = ValueFormat.formatFormatter.get(formats[0]);
            if (fs)
                fmt = fs;
        }
        if (add) {
            if (remove.has(add)) {
                remove.delete(add);
                add = null;
            }
        }
        remove.forEach((value, key) => cl.remove(key));
        if (add)
            cl.add(add);
        fmt(el, value, formats, nextValue, flash, type, onRefresh);
        return el;
    }

}

class FullScreen {

    /**
     * Returns true if the page is in fullscreen mode
     * @returns {boolean} True if the application is in full screen
     */
    static IsFull() {
        if (document.fullscreenElement)
            return true;
        if (document.webkitFullscreenElement)
            return true;
        let doHack = false;
        try {

            if (window.matchMedia('(display-mode: standalone)').matches)
                return true;
        }
        catch (e) {
            doHack = true;
        }
        try {

            if (window.matchMedia('(display-mode: fullscreen)').matches)
                return true;
        }
        catch (e) {
            doHack = true;
        }
        if (!doHack)
            return false;

        const sw = screen.width;
        const sh = screen.height;

        const ww = window.outerWidth;
        const wh = window.outerHeight;

        const dw = sw - ww;
        const dh = sh - wh;

        const margin = 16;
        return (dw <= margin) && (dh <= margin);
    }

    static async Enter() {

        const e = document.documentElement;
        var requestMethod = e.requestFullScreen ||
            e.webkitRequestFullscreen ||
            e.mozRequestFullScreen ||
            e.msRequestFullscreen;
        if (requestMethod) {
            try {
                await requestMethod.call(e);
                return true;
            }
            catch (e) {
                return false;
            }
        }
        return false;
    }

    static async Exit() {
        try {
            await document.exitFullscreen();
        }
        catch (e) {
            try {
                documnet.webkitCancelFullScreen();
            }
            catch (e) {
            }
        }
    }

    static async Toggle() {
        if (FullScreen.IsFull())
            await FullScreen.Exit();
        else
            await FullScreen.Enter();
    }

}



function ToTypedJson(data, optionalType) {
    if (data == null)
        return "null";
    const ht = data["$type"];
    if (!optionalType)
        optionalType = ht;
    if (!optionalType)
        return JSON.stringify(data);
    if (ht) {
        if (Object.keys(data)[0] === "$type")
            return JSON.stringify(data);
        data = Object.assign({}, data);
        delete data["$type"];
    }
    data = Object.assign({
        $type: optionalType
    }, data);
    return JSON.stringify(data);
}

async function EnterNoSleep() {
    let state = document["NoSleep"];
    if (!state) {
        const e = document.createElement("video");
        e.setAttribute("muted", "");
        e.setAttribute("title", "No Sleep");
        e.setAttribute("playsinline", "");
        e.setAttribute("loop", "");
        function Add(type, dataURI) {
            const source = document.createElement("source");
            source.src = dataURI;
            source.type = "video/" + type;
            e.appendChild(source);
        }
        Add("webm", "data:Video/webm;base64,GkXfo59ChoEBQveBAULygQRC84EIQoKEd2VibUKHgQJChYECGFOAZwEAAAAAAAJQEU2bdLpNu4tTq4QVSalmU6yBoU27i1OrhBZUrmtTrIHYTbuMU6uEElTDZ1OsggEpTbuMU6uEHFO7a1OsggI67AEAAAAAAABZAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVSalmsirXsYMPQkBNgI1MYXZmNTguNjQuMTAwV0GNTGF2ZjU4LjY0LjEwMESJiEB/QAAAAAAAFlSua8yuAQAAAAAAAEPXgQFzxYg/RBAYXrWgH5yBACK1nIN1bmSGhVZfVlA4g4EBI+ODhA7msoDgAQAAAAAAABCwgRC6gRCagQJVsIRVuYEBElTDZ0Cac3MBAAAAAAAAJ2PAgGfIAQAAAAAAABpFo4dFTkNPREVSRIeNTGF2ZjU4LjY0LjEwMHNzAQAAAAAAAF9jwItjxYg/RBAYXrWgH2fIAQAAAAAAACJFo4dFTkNPREVSRIeVTGF2YzU4LjExMi4xMDEgbGlidnB4Z8iiRaOIRFVSQVRJT05Eh5QwMDowMDowMC41MDAwMDAwMDAAAB9DtnXs54EAo72BAACA8AIAnQEqEAAQAABHCIWFiIWEiAICAnWqA/gD+gINTRgA/v1u8//jmTcwxP+Obf/xYTwOKMj/8VEAo6iBAPoAsQEABhDMABgAMCgv9AAgAP7ujn+u8bZhtu2//hx9RhQf+F/AHFO7a5G7j7OBALeK94EB8YIByfCBAw==");
        Add("mp4", "data:Video/mp4;base64,AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAAIZnJlZQAAAuVtZGF0AAACoQYF//+d3EXpvebZSLeWLNgg2SPu73gyNjQgLSBjb3JlIDE2MSAtIEguMjY0L01QRUctNCBBVkMgY29kZWMgLSBDb3B5bGVmdCAyMDAzLTIwMjAgLSBodHRwOi8vd3d3LnZpZGVvbGFuLm9yZy94MjY0Lmh0bWwgLSBvcHRpb25zOiBjYWJhYz0xIHJlZj0xNiBkZWJsb2NrPTE6MDowIGFuYWx5c2U9MHgzOjB4MTMzIG1lPXVtaCBzdWJtZT0xMCBwc3k9MSBwc3lfcmQ9MS4wMDowLjAwIG1peGVkX3JlZj0xIG1lX3JhbmdlPTI0IGNocm9tYV9tZT0xIHRyZWxsaXM9MiA4eDhkY3Q9MSBjcW09MCBkZWFkem9uZT0yMSwxMSBmYXN0X3Bza2lwPTEgY2hyb21hX3FwX29mZnNldD0tMiB0aHJlYWRzPTEgbG9va2FoZWFkX3RocmVhZHM9MSBzbGljZWRfdGhyZWFkcz0wIG5yPTAgZGVjaW1hdGU9MSBpbnRlcmxhY2VkPTAgYmx1cmF5X2NvbXBhdD0wIGNvbnN0cmFpbmVkX2ludHJhPTAgYmZyYW1lcz04IGJfcHlyYW1pZD0yIGJfYWRhcHQ9MiBiX2JpYXM9MCBkaXJlY3Q9MyB3ZWlnaHRiPTEgb3Blbl9nb3A9MCB3ZWlnaHRwPTIga2V5aW50PTI1MCBrZXlpbnRfbWluPTQgc2NlbmVjdXQ9NDAgaW50cmFfcmVmcmVzaD0wIHJjX2xvb2thaGVhZD02MCByYz1jcmYgbWJ0cmVlPTEgY3JmPTIwLjAgcWNvbXA9MC42MCBxcG1pbj0wIHFwbWF4PTY5IHFwc3RlcD00IGlwX3JhdGlvPTEuNDAgYXE9MToxLjAwAIAAAAAVZYiBAAIf/urj/MsrpVzX+SWZakk1AAAAG0GaCC2Ib//UcD3gUv2YrYFNTFfYNHPNfm5UhgAAAzRtb292AAAAbG12aGQAAAAAAAAAAAAAAAAAAAPoAAAB9AABAAABAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAACXnRyYWsAAABcdGtoZAAAAAMAAAAAAAAAAAAAAAEAAAAAAAAB9AAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAEAAAABAAAAAAACRlZHRzAAAAHGVsc3QAAAAAAAAAAQAAAfQAAAAAAAEAAAAAAdZtZGlhAAAAIG1kaGQAAAAAAAAAAAAAAAAAAEAAAAAgAFXEAAAAAAAtaGRscgAAAAAAAAAAdmlkZQAAAAAAAAAAAAAAAFZpZGVvSGFuZGxlcgAAAAGBbWluZgAAABR2bWhkAAAAAQAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAAAAABAAABQXN0YmwAAADBc3RzZAAAAAAAAAABAAAAsWF2YzEAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAEAAQAEgAAABIAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY//8AAAA3YXZjQwFkAAr/4QAZZ2QACqxyBF7ARAAAAwAEAAADACA8SJYRgAEAB2joQ4bLIsD9+PgAAAAAEHBhc3AAAAABAAAAAQAAABRidHJ0AAAAAAAALdAAAC3QAAAAGHN0dHMAAAAAAAAAAQAAAAIAABAAAAAAFHN0c3MAAAAAAAAAAQAAAAEAAAAcc3RzYwAAAAAAAAABAAAAAQAAAAIAAAABAAAAHHN0c3oAAAAAAAAAAAAAAAIAAAK+AAAAHwAAABRzdGNvAAAAAAAAAAEAAAAwAAAAYnVkdGEAAABabWV0YQAAAAAAAAAhaGRscgAAAAAAAAAAbWRpcmFwcGwAAAAAAAAAAAAAAAAtaWxzdAAAACWpdG9vAAAAHWRhdGEAAAABAAAAAExhdmY1OC42NC4xMDA=");
        e.addEventListener("timeupdate", () => {
            if (e.currentTime > 0.25)
                e.currentTime = Math.random() * 0.02;
        });
        state = {
            Video: e,
            Count: 0,
        };
        document["NoSleep"] = state;
    }
    const c = state.Count;
    state.Count = c + 1;
    if (c == 0) {
        console.log("Sleep prevented");
        await state.Video.play();
    }
}
function IsSleepPrevented() {
    const state = document["NoSleep"];
    if (!state)
        return false;
    return state.Count > 0;
}



async function ExitNoSleep() {
    const state = document["NoSleep"];
    if (!state)
        return;
    const c = state.Count;
    state.Count = c - 1;
    if (c == 1) {
        console.log("Sleep resumed");
        await state.Video.pause();
    }
}



/**
 * Merge an array of Uint8Array's
 * @param {Uint8Array[]} arrayOfArrays The arrays to merge
 * @returns {Uint8Array} The merged arrays
 */
function ConcatUint8Arrays(arrayOfArrays) {
    const l = arrayOfArrays.length;
    if (l <= 0)
        return new Uint8Array(0);
    if (l === 1)
        return arrayOfArrays[0];
    let size = 0;
    for (let i = 0; i < l; ++i)
        size += arrayOfArrays[i].length;
    const m = new Uint8Array(size);
    let d = 0;
    for (let i = 0; i < l; ++i) {
        const a = arrayOfArrays[i];
        m.set(a, d);
        d += a.lenght;
    }
    return m;
}

/**
 * Read all data from a stream into a buffer.
 * @param {Stream} stream The source stream to read
 * @returns {Uint8Array} The data read from the stream
 */
async function ReadAllData(stream) {
    const b = [];
    for await (const chunk of stream) 
        b.push(chunk)
    return ConcatUint8Arrays(b);
} 

/**
 * A function that compresses a buffer using the deflate-raw algorithm.
 * This function uses the browsers built in functionality.
 * @param {Buffer} buffer Any buffer
 * @returns {Uint8Array} A buffer with the compressed bytes.
 */
async function DeflateCompress(buffer) {
    const inp = new ReadableStream({
        start(controller) {
            controller.enqueue(buffer);
            controller.close();
        },
    });
    const compStream = inp.pipeThrough(new CompressionStream("deflate-raw"));
    return await ReadAllData(compStream);


}

/**
 * Post some data, not waiting for completion nor response (good for logging, metrics etc).
 * @param {string} url Server target (url).
 * @param {any} data Post payload (serialized to json).
 * @returns {boolean} True is successful (does not mean that the server have processed it, just that it's queued).
 */
function postData(url, data) {
    if (data) {
        const blob = new Blob([JSON.stringify(obj, data)], {
            type: "application/json",
        });
        return navigator.sendBeacon(url, blob);
    }
    return navigator.sendBeacon(url);
}


/**
 * Send a json request using GET or POST (compressed if possible), using some heurstics to choose.
 * @param {string} url Server target (url).
 * @param {any} data Post payload (serialized to json)
 * @param {boolean} reload If true, try to force a reload (bypassing the cache).
 * @param {function(number)} errHandler An optional async function that takes the error code as input, if it returns true, the request will be retried, else it will fail.
 * @param {boolean} returnBlob If true, return the raw blob instead of the json decoded data.
 * @param {function(Request)} setupRequestFn Optional function to setup the Request object.
 * @param {boolean} usePost Always use POST with uncompressed data, if false data may be sent using GET and it may be compressed using deflate-raw.
 * @returns {object|Blob} The repsonse object (decoded JSON) or a blob if returnBlob is true.
 */
async function sendRequest(url, data, reload, errHandler, returnBlob, setupRequestFn, usePost) {
//DEBUG_BEGIN
    if (typeof (usePost) === 'undefined')
        usePost = true;
//DEBUG_END
    const json = ToTypedJson(data);
    let r = null;
    let compData = null;
    //  If it's "small enough" to be compressed
    if ((!usePost) && (json.length < 16384)) {
        let uri = encodeURIComponent(json);
        //  If the uri is so short that it should fit into a MTU, just use it without even trying to compress it
        if (uri.length < 128) {
            r = new Request(url + "?" + uri, {
                method: "GET",
                mode: "cors",
                cache: reload ? "reload" : "default",
            });
        } else {
            //  Get binary data
            let data = new TextEncoder("utf-8").encode(json);
            let testData = null;
            //  Try to compress it 
            try {
                testData = await DeflateCompress(data);
            }
            catch (e) {
                //console.warn(e);
            }
            //  If we have compressed data that is significantly smaller than the uncompressed data, use the compressed data
            if (testData && (testData.length < ((data.length * 7) >> 3)))
            {
                compData = testData;
                data = testData;
            }
            //  If the compressed uri length is less than the text base length, use the compressed
            const compDataStr = (compData ? "_d" : "_u") + Uint8ArrayToBase64(data, true);
            if (compDataStr.length < uri.length)
                uri = compDataStr;
            //  If the uri length is "small enough", use a GET request with it.
            if (compDataStr.length < 4096) {
                r = new Request(url + "?" + uri, {
                    method: "GET",
                    mode: "cors",
                    cache: reload ? "reload" : "default",
                });
            }
        }
    }
    if (!r) {
        if (compData) {
            r = new Request(url, {
                method: "POST",
                mode: "cors",
                cache: reload ? "reload" : "default",
                headers: {
                    "Content-Type": "application/json",
                    "Content-Encoding": "deflate"
                },
                body: compData,
            });

        } else {
            r = new Request(url, {
                method: "POST",
                mode: "cors",
                cache: reload ? "reload" : "default",
                headers: {
                    "Content-Type": "application/json",
                },
                body: json,
            });
        }

    }
    if (setupRequestFn)
        setupRequestFn(r);
    for (let count = 0; ;++count){

        const res = await fetch(r);
        //const contentType = res.headers.get('Content-Type');
        if ((res.status != 200) || (res.redirected && errHandler)) {
            if (errHandler) {
                if (await errHandler(res.status))
                {
                    --count;
                    continue;
                }
            }
            let text = "";
            if (res.status == 500)
                text = await res.text();
            if (text.length > 0)
                text = "\n" + text;
            const s = res.statusText;
            if ((!s) || (s.length <= 0))
                throw new Error('' + res.status + text);
            throw new Error(res.status + " - " + s + text);
        }
        if (res.redirected)
        {
            if (count > 0)
                throw new Error(_TF("Request was redirected!", "An exception text when a request was redirected multiple times"));
            Open(res.url, "_self");
            return null;
//            window.location = res.url;
//            await waitEvent(window, "popstate");
//            continue;
        }
        try {
            if (returnBlob) {
                const bl = await res.blob();
                return bl;
            }
            const json = await res.json();
            return json;
        }
        catch
        {
            return undefined;
        }
    }
}



/**
 * Send a json request using GET:
 * @param {string} url Server target (url).
 * @param {boolean} reload If true, try to force a reload (bypassing the cache).
 * @param {boolean} returnBlob If true, return the raw blob instead of the json decoded data.
 * @param {function(Request)} setupRequestFn Optional function to setup the Request object.
 * @param {function(Request)} returnFn Optional method used to read the response, the first argument is the request object, return whatever.
 * @param {number} maxRetryCount The maximum number to retry the request.
 * @returns {object|Blob} The repsonse object (decoded JSON) or a blob if returnBlob is true.
 */
async function getRequest(url, reload, returnBlob, setupRequestFn, returnFn, maxRetryCount) {
    let r = url;
    if (typeof (r) === "string")
    {
        r = new Request(url, {
            method: "GET",
            mode: "cors",
            cache: reload ? "reload" : "default",
        });
        if (setupRequestFn)
            setupRequestFn(r);
    }
    if ((!maxRetryCount) || (maxRetryCount < 1))
        maxRetryCount = 10000;
    for (let count = 0; count < maxRetryCount; ++count) {

        const res = await fetch(r);
        //const contentType = res.headers.get('Content-Type');
        if (res.status != 200) {
            let text = "";
            if (res.status == 500)
                text = await res.text();
            if (text.length > 0)
                text = "\n" + text;
            const s = res.statusText;
            if ((!s) || (s.length <= 0))
                throw new Error('' + res.status + text);
            throw new Error(res.status + " - " + s + text);
        }
        if (res.redirected) {
            if (count > 0)
                throw new Error(_TF("Request was redirected!", "An exception text when a request was redirected multiple times"));
            Open(res.url, "_self");
            return null;
            //window.location = res.url;
            //await waitEvent(window, "popstate");
            //continue;
        }
        try {
            if (returnFn)
                return await returnFn(res);
            if (returnBlob) {
                const bl = await res.blob();
                return bl;
            }
            const json = await res.json();
            return json;
        }
        catch
        {
            return undefined;
        }
    }

}


function getFileExtension(path)
{
    const i = path.lastIndexOf('.');
    if (i < 0)
        return "";
    return path.substring(i + 1);
}


const imageExtensions = Object.freeze({
    png: 1,
    gif: 1,
    jpg: 1,
    jpeg: 1,
    avif: 1,
    webp: 1,
    tiff: 1,
    tif: 1,
    svg: 1,
    jfif: 1,
});

const videoExtensions = Object.freeze({
    webm: 1,
    mp4: 1,
    ogg: 1,
    mov: 1,
});

const programmingExtensions = Object.freeze({
    js: 1,
    json: 1,
    css: 1,
    html: 1,
    xml: 1,
    cs: 1,
    cpp: 1,
    c: 1,
    h: 1,
    vbs: 1,
    bat: 1,
    vb: 1,
});

const compressExtensions = Object.freeze({
    txt: 1,
    log: 1,
    cfg: 1,
    svg: 1,
});

function isImageExt(ext) {
    return imageExtensions[ext] == 1;
}

function isProgrammingExt(ext) {
    return programmingExtensions[ext] == 1;
}

function isVideoExt(ext) {
    return videoExtensions[ext] == 1;
}

function isCompressibleExt(ext) {
    return (programmingExtensions[ext] == 1) || (compressExtensions[ext] == 1);
}

function isImageFile(path) {
    const ext = getFileExtension(path).toLowerCase();
    return imageExtensions[ext] == 1;
}

function isVideoFile(path) {
    const ext = getFileExtension(path).toLowerCase();
    return videoExtensions[ext] == 1;
}


function isLetter(c) {
    if (c.toUpperCase() != c.toLowerCase())
        return true;
    if (c.charCodeAt(0) < 128)
        return false;
    try {
        eval("function " + c + "(){}");
        return true;
    } catch {
        return false;
    }
}

function IsAtMaxScrollV(element) {
    const maxh = element.scrollHeight - element.clientHeight - 1;
    return element.scrollTop >= maxh;
}

/**
 * Make an element stick to the bottom scroll (can be "free" by scrolling and re-attached by scrolling to the end).
 * @param {HTMLElement} element The element to control.
 * @param {boolean} stickByDefault If true, stick the element to the bottom right away.
 * @param {function(HTMLElement)} onBottom An optional function that is called when the scroll sticks.
 * @param {function(HTMLElement)} onFree An optional function that is called when the scroll is "freed".
 * @param {function(HTMLElement)} onTop An optional function that is called when the scroll is scrolled top the top.
 * @param {boolean} instant If true, do not smooth scroll
 */
function StickToBottom(element, stickByDefault, onBottom, onFree, onTop, instant) {
    if (typeof stickByDefault !== "boolean")
        stickByDefault = true;
    let s = !!stickByDefault;
    let isInternal = false;


    function stickIt(isMutating) {
        isInternal = true;
        try {
            element.scrollTo({
                left: element.scrollLeft,
                top: element.scrollHeight,
                behavior: instant ? "instant" : "smooth",
            });
            requestAnimationFrame(() => isInternal = false);
        }
        catch
        {
            isInternal = false;
        }
    }

    element.onscroll = () => {
        if (isInternal)
            return;
        const t = element.scrollTop;
        if ((onTop) && (t <= 1))
            onTop(element);
        const maxh = element.scrollHeight - element.clientHeight - 4;
        const ss = t >= maxh;
        if (ss === s)
            return;
        s = ss;
        if (s) {
            element.classList.add("Stick");
            if (onBottom)
                onBottom(element);
        }
        else {
            element.classList.remove("Stick");
            if (onFree)
                onFree(element);
        }
        //console.log(ss ? "Sticking" : "Free!");
    };
    //  Handle element resize
    new ResizeObserver(() => {
        if (s)
            stickIt();
    }).observe(element);
    //  Handle sub element modifications (add/remove)
    new MutationObserver(
        () => {
            if (s)
                stickIt(true);
        }).observe(element,
            {
                childList: true,
                subtree: true,
            });
    //  Handle initial
    if (s)
        requestAnimationFrame(() => stickIt());
}

function PageLoaded() {
    PostTop("LoaderRemoved", null, "*");
}

/**
 * Add a loading overlay
 * @param {HTMLElement} page The element to add the loading page to, use null or undefined to use the default (document.body).
 * @param {string} text An optional text to display while loading.
 * @param {boolean} opaque If true, make the loader background opaque
 * @returns {function(boolean):void} A function that when executed removes the loading overlay, if the argument is true, no events ('LoaderRemoved
 */
function AddLoading(page, text, opaque) {

    if (!page)
        page = document.body;
    const scroll = {
        left: GetHistoryState("ScrollX", -1),
        top: GetHistoryState("ScrollY", -1),
        behavior: "instant"
    };
    let le = document.createElement("SysWeaver-Loading");
    if (opaque)
        le.classList.add("Opaque");
    if (text) {
        const te = document.createElement("SysWeaver-LoadingText");
        te.innerText = text;
        le.appendChild(te);
    }
    const loading = new ColorIcon("IconWorking", "IconColorThemeMain", 64, 64);
    le.appendChild(loading.Element);
    le.onclick = ev => {
        ev.preventDefault();
        ev.stopPropagation();
    };
    page.appendChild(le);
//    const op = page.style.pointerEvents;
    //    page.style.pointerEvents = "none";

    return noEvents => {
        if (le) {
            le.remove();
            if (!noEvents) {
                PostTop("LoaderRemoved", null, "*");
                InterOp.Post("WindowLoaded");
                if ((scroll.left > 0) || (scroll.top > 0)) {
                    //console.log("Scroll target: " + scroll.left + ", " + scroll.top);
                    let lastChanged = performance.now();
                    let lastHeight = document.scrollHeight;
                    function tryScroll() {
                        if (window.DidInteract)
                            return;
                        let s = window.ScrollCounter;
                        if (!s)
                            s = 0;
                        window.ScrollCounter = s + 1;
                        window.scrollTo(scroll);
                        requestAnimationFrame(() => {
                            if (window.DidInteract)
                                return;
                            const nh = document.scrollHeight;
                            const pn = performance.now();
                            if (nh !== lastHeight) {
                                lastChanged = pn;
                                lastHeight = document.scrollHeight;
                            }
                            if ((pn - lastChanged) > 1500)
                                return;
                            requestAnimationFrame(tryScroll);
                        });
                    }
                    tryScroll();
                }
            }
            le = null;
        }
    };
}

class ProgressBar {
    constructor() {
        const e = document.createElement("SysWeaver-ProgressBar");
        const f = document.createElement("SysWeaver-ProgressBarFill");
        const ti = document.createElement("SysWeaver-ProgressBarTextInside");
        const to = document.createElement("SysWeaver-ProgressBarTextOutside");
        e.appendChild(ti);
        e.appendChild(to);
        e.appendChild(f);
        this.Element = e;
        this.Prog = f;
        this.TI = ti;
        this.TO = to;
    }

    SetValue(current, max, text, title) {
        const val = max <= 0 ? 0 : (current * 100 / max);
        const pc = ValueFormat.toString(current, -2);
        const pm = ValueFormat.toString(max, -2);
        const pval = pc + " / " + pm;
        if (!text)
            text = pval;
        else
            text = ValueFormat.stringFormat(text, "{0}", pc, pm);

        if (!title)
            title = pval + " - {0}";
        else
            title = ValueFormat.stringFormat(title, "{0}", pc, pm);

        this.SetPercentage(val, text, title);
    }

    SetPercentage(value, text, title) {
        if (value < 0)
            value = 0;
        if (value > 100)
            value = 100;
        const pformat = ValueFormat.toString(value, 2) + "%";
        if (!text)
            text = pformat;
        else
            text = ValueFormat.stringFormat(text, pformat);

        if (!title)
            title = pformat;
        else
            title = ValueFormat.stringFormat(title, pformat);

        const cp = value + "%";
        const icp = (100 - value) + "%";
        this.Prog.style.left = "-" + icp;
        this.Element.title = title;
        const ti = this.TI;
        const to = this.TO;
        ti.style.width = cp;
        to.style.left = cp;
        to.style.width = icp;
        ti.textContent = text;
        to.textContent = text;
        const a = "Active";
        if (value >= 50) {
            ti.classList.add(a);
            to.classList.remove(a);
        } else {
            to.classList.add(a);
            ti.classList.remove(a);
        }
    }




}


/** Static functions that performas animation every frame */
class Animator {


    /**
     * Add a function that should be executed every frame.
     * @param {function(value):boolean} func The function that should be executed every frame, the argument is a timer value in ms that starts at 0.
     * Return true to remove the function (stop the animation), you do not need to call Animator.Remove if you returned true.
     */
    static Add(func) {
        const fns = Animator.Functions;
        if (!Animator.HaveAnimation) {

            const fn = timeStamp => {

                let empty = true;
                for (let [key, value] of fns) {
                    empty = false;
                    if (value < 0) {
                        value = timeStamp;
                        fns.set(key, value);
                    }
                    try {
                        if (key(timeStamp - value)) {
                            fns.delete(key);
                        }
                    }
                    catch
                    {
                    }
                }
                if (empty) {

                    Animator.HaveAnimation = null;
                    return;
                }
                window.requestAnimationFrame(fn);
            };
            Animator.HaveAnimation = fn;
            window.requestAnimationFrame(fn);
        }
        fns.set(func, -1);
    }

    /**
     * Stop a function from being executed every frame (no need to call this if the function returned true)
     * @param {function(value):boolean} func A function that have previously been added.
     */
    static Remove(func) {
        if (func)
            Animator.Functions.delete(func);
    }

    static Functions = new Map();
    static HaveAnimation;

}

/** Keep tracks on pending image loads and some basic stats, can be used to wait for all images to load (or fail) */
class PendingImages {

    /** Number of images started */
    Count = 0;
    /** Number of images currently in progress */
    InProgress = 0;
    /** Number of fails */
    Fails = 0;

    /**
     * Call before setting an image src, or use this to set the source.
     * imgElement.onerror and imgElement.onload will be set (calling any previous handlers).
     * @param {HTMLImageElement} imgElement The image element that should be tracked.
     * @param {string} src An optional image src to set.
     */
    Start(imgElement, src) {

        const t = this;
        const ol = imgElement.onload;
        const oe = imgElement.onerror;
        imgElement.onload = ev => {
            --t.InProgress;
            if (ol)
                ol(ev);
        };
        imgElement.onerror = ev => {
            --t.InProgress;
            ++t.Fails;
            if (oe)
                oe(ev);
        };
        ++t.InProgress;
        ++t.Count;
        if (src)
            imgElement.src = src;
    }

    /**
     * Wait for all pending images (in progress) to complete
     * @returns {Promise} a Promise that is resolved when all pending images have been loaded.
     */
    async WaitAll() {
        const t = this;
        for (; ;) {
            if (t.InProgress <= 0)
                return;
            await delay(10);
        }
    }
}


/**
 * Test if an element is attached to the DOM.
 * @param {HTMLElement} el A html element
 * @returns {boolean} True if the supplied element is attached to the DOM.
 */
function IsAttached(el) {

    while (el) {
        if (el === document.body)
            return true;
        el = el.parentElement;
    }
    return false;
}


function GetAbsoluteRect(el) {
    let
        found,
        left = 0,
        top = 0,
        width = 0,
        height = 0,
        offsetBase = GetAbsoluteRect.offsetBase;
    if (!offsetBase && document.body) {
        offsetBase = GetAbsoluteRect.offsetBase = document.createElement('div');
        offsetBase.style.cssText = 'position:absolute;left:0;top:0';
        document.body.appendChild(offsetBase);
    }
    if (el && el.ownerDocument === document && 'getBoundingClientRect' in el && offsetBase) {
        const boundingRect = el.getBoundingClientRect();
        const baseRect = offsetBase.getBoundingClientRect();
        found = true;
        left = boundingRect.left - baseRect.left;
        top = boundingRect.top - baseRect.top;
        width = boundingRect.right - boundingRect.left;
        height = boundingRect.bottom - boundingRect.top;
    }
    return {
        found: found,
        left: left,
        top: top,
        width: width,
        height: height,
        right: left + width,
        bottom: top + height
    };
}


// Track an elements movement and move a fixed element along with it.
// If margin is 0 or greater the target element will be kept on screen (using that margin)
function TrackElement(mouseEventOrElement, targetElement, margin) {
    const b = document.body;
    let relX = 0.5;
    let relY = 0.5;
    let x, y;
    let elmentToTrack = mouseEventOrElement;
    if (mouseEventOrElement instanceof MouseEvent) {
        elmentToTrack = mouseEventOrElement.target;
        const start = GetAbsoluteRect(elmentToTrack);
        if (!mouseEventOrElement.isTrusted) {
            x = (start.left + start.right) * 0.5 - window.scrollX;
            y = (start.top + start.bottom) * 0.5 - window.scrollY;
        } else {
            x = mouseEventOrElement.clientX;
            y = mouseEventOrElement.clientY;
            const mouseX = x + window.scrollX;
            const mouseY = y + window.scrollY;
            relX = start.width > 0 ? ((mouseX - start.left) / start.width) : 0.5;
            relY = start.height > 0 ? ((mouseY - start.top) / start.height) : 0.5;
        }
    } else {
        const start = GetAbsoluteRect(elmentToTrack);
        x = (start.left + start.right) * 0.5 - window.scrollX;
        y = (start.top + start.bottom) * 0.5 - window.scrollY;
    }
    const ax = x >= (b.clientWidth * 0.5) ? 1 : 0;
    const ay = y >= (b.clientHeight * 0.5) ? 1 : 0;
    const css = targetElement.style;
    let obs = null;
    const updatePos = ev => {
        if (ev && obs) {
            if (!IsAttached(targetElement)) {
                document.removeEventListener("scroll", updatePos);
                obs.unobserve(targetElement);
                obs.unobserve(b);
                obs.unobserve(elmentToTrack);
                obs.disconnect();
                return;
            }
        }
        const cr = GetAbsoluteRect(elmentToTrack);
        const mr = GetAbsoluteRect(targetElement);
        const sx = window.scrollX;
        const sy = window.scrollY;
        let posX = relX * cr.width + cr.left - sx - ax * mr.width;
        let posY = relY * cr.height + cr.top - sy - ay * mr.height;
        if (margin && (margin >= 0)) {
            if (posX < margin)
                posX = margin;
            if (posY < margin)
                posY = margin;
            const vw = b.clientWidth - margin;
            const vh = b.clientHeight - margin;
            if ((posX + mr.width) > vw)
                posX = vw - mr.width;
            if ((posY + mr.height) > vh)
                posY = vh - mr.height;
        }
        css.top = Math.round(posY) + "px";
        css.left = Math.round(posX) + "px";

    };
    obs = new ResizeObserver(updatePos);
    obs.observe(elmentToTrack);
    obs.observe(b);
    obs.observe(targetElement);
    document.addEventListener("scroll", updatePos);
    updatePos();
}



function InternalGetHistoryState() {
    let state = window.HistoryState;
    if (state)
        return state;
    if (window.top === window.self) {
        state = history.state;
        if (!state)
            state = {};
    } else {
        state = sessionStorage.getItem(window.location.href);
        if (!state)
            state = {};
        else
            state = JSON.parse(state);
    }
    window.HistoryState = state;
    return state;
}

function ClearHistoryState() {
    window.HistoryState = null;
    if (window.top === window.self)
        history.replaceState(null, "");
    else
        sessionStorage.removeItem(window.location.href);
}

function ClearOtherHistoryState(url) {
    sessionStorage.removeItem(url);
}

function SetHistoryState(key, value) {
    const state = InternalGetHistoryState();
    if (window.top === window.self) {
        state[key] = value;
        history.replaceState(state, "");
    } else {
        state[key] = value;
        sessionStorage.setItem(window.location.href, JSON.stringify(state));
    }
}

function SetTemporaryInnerText(element, text, duration, optionalNewText) {
    const nt = optionalNewText ?? element.textContent;
    if (element.TempText)
        clearTimeout(element.TempText);
    const id = performance.now + "_" + Math.random();
    element.textContent = text;
    element.TempId = id;
    element.TempText = setTimeout(() => {
        if (element.TempId !== id)
            return;
        if (element.textContent !== text)
            return;
        element.textContent = nt;
        element.TempText = null;
    }, duration);
}

// bool onStateFn(stateObject), return true to update the data
function UpdateHistoryState(onStateFn) {
    const state = InternalGetHistoryState();
    if (onStateFn(state)) {
        if (window.top === window.self)
            history.replaceState(state, "");
        else
            sessionStorage.setItem(window.location.href, JSON.stringify(state));
    }
}


function GetHistoryState(key, ifNotFoundValue) {

    const state = InternalGetHistoryState();
    if (!state)
        return ifNotFoundValue;
    const val = state[key];
    if (typeof val === "undefined")
        return ifNotFoundValue;
    return val;        
}


/**
 * A simple in-memory cache.
 * Automatic pruning of old data.
 */
class Cache {

     /** Do not touch */
    static InternalCache = new Map();

    /**
     * Get some data from the cache or by using the fetchFn
     * @param {string} cacheName an unique id for the cache (typically an API end-point).
     * @param {any} cacheKey A key object (typically the parameters for a request).
     * @param {number} lifeTime The number of milliseconds to re-use cached data.
     * @param {function(any, string)} fetchFn An async function that is called when data need to be refreshed, called like: await fetchFn(cacheKey, cacheName).
     * @returns {any} the result of the fetchFn
     */
    static async Get(cacheName, cacheKey, lifeTime, fetchFn) {
        const cache = Cache.InternalCache;
        if (!Cache.InternalPrune) {
            Cache.InternalPrune = true;
            function Prune() {
                const toDel = [];
                const n = new Date();
                cache.forEach((val, key) => {
                    if (n >= val.Exp)
                        toDel.push(key);
                });
                const dc = toDel.length;
                for (let i = 0; i < dc; ++i)
                    cache.delete(toDel[i]);
                if (cache.size <= 0) {
                    Cache.InternalPrune = false;
                    return;
                }
                setTimeout(Prune, 5000);
            }
            setTimeout(Prune, 5000);
        }
        const now = new Date();
        const key = "" + cacheName + "|" + JSON.stringify(cacheKey);
        let ce = cache.get(key);
        if (ce) {
            if (now < ce.Exp)
                return ce.Value;
        }
        const val = await fetchFn(cacheKey, cacheName);
        ce =
        {
            Value: val,
            Exp: new Date(now.getTime() + lifeTime),
        };
        cache.set(key, ce);
        return val;
    }

    /**
     * Perform a get request and cache results.
     * @param {string} url The url to do the get request for.
     * @param {number} lifeTime The number of milliseconds to re-use cached data.
     * @returns {any} The response of the get request
     */
    static async GetRequest(url, lifeTime) {
        return Cache.Get("_Greq" + url, null, lifeTime, async () => await getRequest(url, false));
    }

    /**
     * Send a request and cache results.
     * @param {string} url The url to do the post request to.
     * @param {any} data The post data.
     * @param {number} lifeTime The number of milliseconds to re-use cached data.
     * @returns {any} The response of the post request
     */
    static async SendRequest(url, data, lifeTime) {
        return Cache.Get("_Sreq" + url, data, lifeTime, async () => await sendRequest(url, data, false));
    }


}

/**
 * Capture a frame from a video stream
 * @param {HtmlVideoElement} video The video element to get a frame of
 * @param {string} mime The mime type of the image format to use, default is "image/jpeg"
 * @param {number} quality [0, 100] encoding quality (depends on the mime type), default is 80.
 * @returns {Uint8Array} The image bytes
 */
async function CaptureVideoFrame(video, mime, quality) {
    if (!mime)
        mime = "image/jpeg";
    if ((!quality) || (quality <= 0))
        quality = 80;
    quality *= 0.01;
    const s = video.srcObject.getVideoTracks()[0].getSettings();
    const w = s.width;
    const h = s.height;
    const c = document.createElement("canvas");
    c.width = w;
    c.height = h;
    const cc = c.getContext("2d");
    cc.drawImage(video, 0, 0, w, h);

    let blob = null;
    let err = null;
    await new Promise(resolve => {
        try {

            c.toBlob(b => {
                blob = b;
                resolve();
            }, mime, quality);
        }
        catch (e) {
            err = e;
            resolve();
        }
    });
    if (err)
        throw err;
    if (!blob)
        throw new Error(_TF("Failed to capture an image!", "An exception text when a video frame capture failed"));
    return await bufferToBase64(await blob.arrayBuffer());
}


function SysWeaverIgnoreUserChanges() {
    window.IgnoreUserChanged = true;
}



async function SysWeaverInit() {
    if (window.HaveSysWeaverInit)
        return;
    window.HaveSysWeaverInit = true;

    const ps = getUrlParams();
    const css = ps.get('css');
    if (css)
        await includeCss(null, css);
    const setTheme = ps.get('settheme');
    if (setTheme)
        localStorage.setItem("SysWeaver.Theme", setTheme);
    const useTheme = ps.get('usetheme');
    if (useTheme)
        window.UseTheme = useTheme;
    window.addEventListener("load", () => {
        if (ps.get("transparent")) {
            const bod = b.body;
            if (bod)
                bod.classList.add("Embedded");
        }
    });



    await applyTheme();
   
    const didReload = (
        (window.performance.navigation && window.performance.navigation.type === 1) ||
        window.performance
            .getEntriesByType('navigation')
            .map((nav) => nav.type)
            .includes('reload')
    );
    if (didReload)
        ClearHistoryState();

    let isProcessingScroll = false;

    const b = document;


    function onInteraction(ev) {
        console.log("User interacted!");
        window.DidInteract = true;
        const cb = window.OnInteraction;
        if (cb)
            cb(ev);
        b.removeEventListener("mousedown", onInteraction);
        b.removeEventListener("keydown", onInteraction);
        b.removeEventListener("touchstart", onInteraction);
        const bb = b.body;
        if (bb)
            bb.dispatchEvent(new Event("UserInteracted"));
    }
    b.addEventListener("mousedown", onInteraction);
    b.addEventListener("keydown", onInteraction);
    b.addEventListener("touchstart", onInteraction);
    function saveScroll() {
        if (!isProcessingScroll) {
            isProcessingScroll = true;
            window.requestAnimationFrame(() => {
                UpdateHistoryState(state => {
                    state.ScrollX = window.scrollX;
                    state.ScrollY = window.scrollY;
                    return true;
                });
                isProcessingScroll = false;
            });
        }
    }


    b.addEventListener("scroll", () =>
    {
        let s = window.ScrollCounter;
        if (!s)
            s = 0;
        if (s > 0)
        {
            --s;
            window.ScrollCounter = s;
        } else {
            if (!window.DidInteract)
                onInteraction();
            saveScroll();
        }
    });
    //document.addEventListener("scrollend", saveScroll);
    b.addEventListener("beforeunload ", saveScroll);
    const id = InterOp.Id;
    const logPrefix = "SysWeaver: "
    const childLogPrefix = "SysWeaver [" + id + "]: ";
    const wtop = window.top;
    const wself = window.self;
    let isSameDomain = false;
    try {
        isSameDomain = wtop.location.origin === wself.location.origin;
    }
    catch
    {
    }
    const isTop = (wtop === wself) || (!isSameDomain);
    const sin = "SysWeaver.ServerInstance";
    let serverInstance = sessionStorage.getItem(sin) ?? null;
    if (serverInstance === "")
        serverInstance = null;
    function setServerInstance(newValue) {
        serverInstance = newValue;
        if (newValue)
            sessionStorage.setItem(sin, newValue);
        else
            sessionStorage.removeItem(sin);
    }

    function blockFn(ev) {
        ev.stopPropagation();
        ev.preventDefault();
        ev.stopImmediatePropagation();
        return false;
    }

    function setElementBlock(el, fn) {
        el.onkeydown = fn;
        el.onclick = fn;
        el.onmousedown = fn;
        el.onmouseover = fn;
        el.onmousemove = fn;
        el.onmouseenter = fn;
        el.onmouseleave = fn;
        el.onmouseout = fn;

        const m = fn ? el.addEventListener : el.removeEventListener;
        if (!fn)
            fn = blockFn;
        m("keydown", fn);
        m("click", fn);
        m("mousedown", fn);
        m("mouseover", fn);
        m("mousemove", fn);
        m("mouseenter", fn);
        m("mouseleave", fn);
        m("mouseout", fn);
    }




    //  Server blocker
    let isBlocking = null;
    function StartBlock(title, waiting, icon, replace) {
        console.warn(title);
        if (!isBlocking) {
            isBlocking = b.createElement("SysWeaver-ServerBlock");
            const i = b.createElement("SysWeaver-ServerImage");
            const ii = new ColorIcon(icon, "IconColorThemeMain", 128, 128);
            i.Icon = ii;
            i.appendChild(ii.Element);
            isBlocking.appendChild(i);
            isBlocking.appendChild(b.createElement("SysWeaver-ServerTitle"));
            isBlocking.appendChild(b.createElement("SysWeaver-ServerText"));
            const w = b.createElement("SysWeaver-ServerWaiting");
            w.appendChild(new ColorIcon("IconWorking", "IconColorThemeMain", 48, 48).Element);
            isBlocking.appendChild(w);
            b.body.appendChild(isBlocking);
            setElementBlock(isBlocking, blockFn);
            setElementBlock(b, blockFn);
        } else {
            if (!replace)
                return;
            isBlocking.children[4].remove();
        }
        isBlocking.appendChild(b.createElement("SysWeaver-ServerTime"));
        isBlocking.children[0].Icon.ChangeImage(icon);
        isBlocking.children[1].textContent = title;
        isBlocking.children[2].textContent = waiting;
        isBlocking.children[4].textContent = "";
        ValueFormat.updateDateTimeLive(isBlocking.children[4], new Date());
    }

    function RemoveBlock() {
        if (!isBlocking)
            return;
        isBlocking.remove();
        isBlocking = null;
        setElementBlock(b, null);
    }

    async function delayedReload(ev) {
        if (window.IgnoreUserChanged)
            return;
        //console.trace();
        //alert("Reloading due to " + ev.Type);
        await delay(100);
        ReloadAll(true, true);
    }

    async function delayedRefresh(ev) {
        if (window.IgnoreUserChanged)
            return;
        //console.trace();
        //alert("Refreshing due to " + ev.Type);
        await delay(100);
        location.reload(true);
    }
    async function delayedUserReload(ev) {
        if (window.IgnoreUserChanged)
            return;
        //console.trace();
        //alert("Reloading due to " + ev.Type);
        await delay(100);
        ReloadAll(true, true);
    }

//  Interop responses for any window
    const allMap = new Map();
    allMap.set("Theme.Changed", async msg => {
        if (msg.From !== id) {
            window.UseTheme = null;
            await applyTheme(true);
        }
    });

    const map = new Map();
    
    if (isTop) {
        //  Interop responses for top windows
        map.set("reload", delayedReload);
        map.set("refresh", delayedRefresh);
        map.set("user.logout", async () => {
            try {
                await navigator.credentials.preventSilentAccess();
            }
            catch
            {
            }
            await delayedUserReload();
        });
        map.set("user.login", ev => {
            setServerInstance(null);
            delayedUserReload(ev);
        });
        map.set("server.connect", async msg => {
            /*if (sessionStorage.getItem("SysWeaver.User")) {
                sessionStorage.removeItem("SysWeaver.User");
                InterOp.Post("user.logout");
                await delayedReload(msg);
                return;
            }*/
            const isNew = (serverInstance !== msg.Value) && (serverInstance != null);
            console.log("Current server instance: " + serverInstance);
            console.log("New server instance: " + msg.Value);
            console.log("Is new: " + isNew);
            setServerInstance(msg.Value);
            if (isNew) {
                await delayedReload(msg);
                return;
            }
            console.log(logPrefix + "Connected to server " + serverInstance);
            RemoveBlock();
        });
        map.set("server.reconnect", async msg => {
            const isNew = serverInstance !== msg.Value;
            console.log("Current server instance: " + serverInstance);
            console.log("New server instance: " + msg.Value);
            console.log("Is new: " + isNew);
            setServerInstance(msg.Value);
            if (isNew) {
                await delayedReload(msg);
                return;
            }
            console.log(logPrefix + "Re-connected to server " + serverInstance);
            RemoveBlock();
        });
        map.set("server.pause", () => StartBlock(
            _TF("Server is paused.", "Displayed as the title on a connected web page if the web service is paused"),
            _TF("Waiting for server to resume.", "Displayed as the details pon a connected web page if the web service is paused"),
            "IconPaused"));
        map.set("server.continue", () => RemoveBlock());
        map.set("server.restart", () => StartBlock(
            _TF("Server is restarting.", "Displayed as the title on a connected web page if the web service is restarting"),
            _TF("Waiting for new server instance to start.", "Displayed as the details pon a connected web page if the web service is restarting"),
            "IconReload"));
        map.set("server.shutdown", () => StartBlock(
            _TF("Server has been shut down.", "Displayed as the title on a connected web page if the web service has been shutdown"),
            _TF("Waiting for new server instance to start.", "Displayed as the details pon a connected web page if the web service has been shutdown"),
            "IconPower"));
        map.set("server.error", () => StartBlock(
            _TF("Server communication lost.", "Displayed as the title on a connected web page if the web service is not reachable anymore"),
            _TF("Waiting for network or server to become available.", "Displayed as the details pon a connected web page if the web service is not reachable anymore"),
            "IconWarning"));
        map.set("server.restore", () => RemoveBlock());
    } else {
        //  Interop responses for child windows (none top windows such as iframe's)
    }

    InterOp.AddListener(async ev => {
        const msg = InterOp.GetMessage(ev);
        const type = msg.Type;
        if (!type)
            return;
        const fn = allMap.get(type) ?? map.get(type);
        if (fn) {
            //console.log(childLogPrefix + "Got \"" + type + "\":\n" + JSON.stringify(msg, null, "\t"));
            await fn(msg);
        }
    });
    SessionManager.Init();

    /*
    if (isTop)
    document.addEventListener("readystatechange", () => {
        if (document.readyState === "complete") StartBlock("Server is paused.", "Waiting for server to resume.", "IconServerPaused");
    });
    */
}

SysWeaverInit();
