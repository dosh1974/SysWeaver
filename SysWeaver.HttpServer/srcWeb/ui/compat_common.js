/**
 * @file ui/compat_common.js - the legacy compatibility layer of the old common/common.js (last part of the GENERATED common/common.js
 * bundle = sw/core.js + sw/ui.js + this file; also part of the GENERATED sw/compat.js bundle that migrated new format pages load after
 * sw/core.js + sw/ui.js while they still use legacy names).
 *
 * Defines every global of the old common.js (functions, classes, constants and window state variables) with the same names, signatures,
 * return values and member names, implemented on top of the Sw core (core/*.js) and the Sw ui library (ui/*.js), so that old format pages,
 * other projects and the srcData templates keep working unchanged:
 *  - non UI helpers map 1:1 onto Sw.Util / Sw.Url / Sw.Net / Sw.Page / Sw.Format / Sw.History / Sw.Session / Sw.InterOp (see
 *    core/README.md);
 *  - toasts (Fail / Info / Message), the loading overlay (AddLoading), the server block overlay, clipboard, smooth image loading,
 *    full screen and the ValueFormat cell renderers map onto Sw components (ui/README.md);
 *  - elements that old page style sheets target by tag name keep being produced as before: ColorIcon (icon-WxH), Button
 *    (Sysweaver-Button), Button.CreateRow (SysWeaver-CenterBlock), ProgressBar (SysWeaver-ProgressBar*), ValueFormat.createLink
 *    (SysWeaver-Link); their styles stay in common/theme.css;
 *  - the old cell classes (Right, Monospaced, Null, False, Text, Toggle, Capped, ValueFormat<Kind>) are assigned to Sw.Cells.Classes, and the
 *    copy on click / keyboard click classes MouseClick / KeyClick are added next to the Sw ones, so old table style sheets keep matching.
 * Old pages don't link sw/ui.css: this file adds it (unless the page already links it) and ui/compat_common.css (the old look of the Sw cell
 * elements), both in front of the page's own style sheets, so page rules still win.
 * The permanent globals (translation markers, getTheme, applyTheme, setTheme, resetTheme, toggleTheme, appendTheme) are defined by the
 * core and not redefined here. The legacy messaging names (SessionManager, InterOp, SysWeaverInit, GetServerTime, PostTop, ReloadAll,
 * Mobile, Portrait, UseTheme ...) are defined by core/legacy_session.js, which the bundles load before this file (it is also part of the
 * small messaging bundle sw/session.js). New format pages must not use these legacy names (SwWeb check --strict).
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Util || !Sw.Cells || !Sw.Legacy) {
        console.error("compat_common.js: sw/core.js, sw/ui.js and core/legacy_session.js must be loaded first");
        return;
    }
    const U = Sw.Util;
    const F = Sw.Format;
    const N = Sw.Net;
    const P = Sw.Page;
    const C = Sw.Cells;

    //  The helpers that define globals / live globals (core/legacy_session.js, which defines the session and InterOp names)
    const def = Sw.Legacy.Def;
    const live = Sw.Legacy.Live;

    // ------------------------------------------------------------------ styles (old pages don't link sw/ui.css)

    /**
     * Link sw/ui.css (the Sw component styles used by toasts, loaders, dialogs, cells and the icon classes) when the page doesn't already
     * link it, and ui/compat_common.css (old look of the Sw cell elements on old pages). They are inserted in front of the first style sheet
     * of the page, so the page's own rules (and common/theme.css) keep their priority. Style sheets inserted before the window load event
     * delay that event, so onload page mains find them loaded.
     * New format pages that load this file through sw/compat.js (after sw/core.js + sw/ui.js) link sw/ui.css themselves and should link
     * sw/compat.css (ui/compat_common.css + shell/compat_application.css) after it; when they don't, sw/compat.css is inserted right after
     * their last sw/*.css link.
     */
    function linkUiCss() {
        const head = document.head;
        if (!head)
            return;
        const links = () => Array.from(head.querySelectorAll('link[rel="stylesheet"]'));
        const has = re => links().some(l => re.test(l.getAttribute("href") || ""));
        const first = head.querySelector('link[rel="stylesheet"], style');
        const add = (file, before) => {
            const l = document.createElement("link");
            l.rel = "stylesheet";
            l.href = Sw.Url.Root + file;
            l.setAttribute("data-sw-compat", "");
            head.insertBefore(l, before === undefined ? first : before);
        };
        if (has(/(^|\/)sw\/compat\.css(\?|$)/))
            return;
        const newFormat = Array.from(document.scripts).some(s => /(^|\/)sw\/core\.js(\?|$)/.test(s.getAttribute("src") || ""));
        if (newFormat) {
            const sw = links().filter(l => /(^|\/)sw\/(ui|shell)\.css(\?|$)/.test(l.getAttribute("href") || ""));
            if (!sw.length)
                add("sw/ui.css");
            const last = sw.length ? sw[sw.length - 1] : null;
            add("sw/compat.css", last ? last.nextSibling : first);
            return;
        }
        if (!has(/(^|\/)sw\/ui\.css(\?|$)/))
            add("sw/ui.css");
        if (!has(/(^|\/)ui\/compat_common\.css(\?|$)/))
            add("ui/compat_common.css");
    }
    linkUiCss();

    // ------------------------------------------------------------------ window state variables (live; the layout, session and InterOp ones: core/legacy_session.js)

    live("DidInteract", () => P.DidInteract);
    live("IsDarkMode", () => Sw.Theme.IsDark);
    live("IsDarkMode", () => Sw.Theme.IsDark, null, document.documentElement);
    live("Theme", () => Sw.Theme.Current, null, document.documentElement);

    //  window.OnInteraction(ev): an optional page hook called on the first user interaction
    Sw.Events.On("page.interacted", ev => {
        const cb = window.OnInteraction;
        if (typeof cb === "function")
            cb(ev);
    });

    // ------------------------------------------------------------------ theme (the session and InterOp names: core/legacy_session.js)

    def("hackerThemeAnimate", Sw.Theme.Animations.hacker);

    // ------------------------------------------------------------------ downloads, url parameters, platform

    /**
     * Download a text (without the server).
     * @param {string} filename - Suggested file name.
     * @param {string} text - The text.
     */
    def("downloadText", (filename, text) => N.DownloadText(filename, text));

    /**
     * Download an url.
     * @param {string} [filename] - Suggested file name (default the last path segment).
     * @param {string} url - The url.
     */
    def("downloadFile", (filename, url) => N.Download(url, filename));

    /**
     * True if an element is below document.body (old semantics: body itself and head elements are false).
     * @param {Element} el - The element.
     * @returns {boolean}
     */
    def("isInDocument", el => {
        const d = document.body;
        while (el) {
            const p = el.parentElement;
            if (p === d)
                return true;
            el = p;
        }
        return false;
    });

    /**
     * Get a number parameter (parseFloat; NaN if not a number).
     * @param {URLSearchParams} params - Parameters (ex: getUrlParams()).
     * @param {string} name - The name (lower case for getUrlParams).
     * @returns {number|null} The value, null if missing.
     */
    def("getNumberParam", (params, name) => {
        const v = params.get(name);
        return typeof v === "string" ? Number.parseFloat(v) : null;
    });

    /**
     * Get an integer parameter (parseInt; NaN if not a number).
     * @param {URLSearchParams} params - Parameters (ex: getUrlParams()).
     * @param {string} name - The name (lower case for getUrlParams).
     * @returns {number|null} The value, null if missing.
     */
    def("getIntParam", (params, name) => {
        const v = params.get(name);
        return typeof v === "string" ? Number.parseInt(v) : null;
    });

    /**
     * Get the device position.
     * @param {boolean} [lowPrecision] - Low precision is acceptable.
     * @param {number} [timeout] - Max time in ms.
     * @param {number} [maximumAge] - Max age of a cached position in ms.
     * @returns {Promise<GeolocationPosition>}
     */
    def("getDevicePosition", (lowPrecision, timeout, maximumAge) => Sw.Platform.GetPosition(lowPrecision, timeout, maximumAge));

    // ------------------------------------------------------------------ random, bytes, hashing

    def("CryptRandomInt32", () => U.RandomInt32());
    def("CryptRandomGuid", () => U.RandomGuid());
    /**
     * Synchronous pure JS SHA-256.
     * @param {ArrayBuffer|Uint8Array} buffer - Data.
     * @returns {Uint8Array} The digest.
     */
    def("legacySha256", buffer => U.Sha256Sync(buffer));
    def("RemoveNulls", o => U.RemoveNulls(o));
    def("Base64EncodeString", s => U.Base64EncodeString(s));
    def("Base64DecodeString", s => U.Base64DecodeString(s));
    def("MakeUriSafeBase64", s => U.Base64UrlSafe(s));
    def("Uint8ArrayToBase64", (bytes, uriSafe) => U.Base64Encode(bytes, uriSafe));
    def("bufferToHex", buffer => buffer ? U.ToHex(buffer) : null);
    def("bufferToBase64", (buffer, keepUri) => U.ToBase64Async(buffer, keepUri));
    def("base64ToArray", s => U.Base64Decode(s));
    def("hashDataSha1", data => U.Sha1(data));
    def("hashStringSha1", s => U.Sha1B64(s));
    def("hashData", data => U.Sha256(data));
    def("hashString", s => U.HashString(s));
    def("concatArrays", arrays => U.ConcatBytes(arrays));
    def("ConcatUint8Arrays", arrays => U.ConcatBytes(arrays));
    def("ReadAllData", stream => U.ReadAll(stream));
    def("DeflateCompress", buffer => U.Deflate(buffer));

    // ------------------------------------------------------------------ DOM traversal and focus (old implementations, pure DOM)

    /**
     * True if the element is display:none (or the head element).
     * @param {Element} element - The element.
     * @returns {boolean}
     */
    function isHidden(element) {
        if (getComputedStyle(element).display === "none")
            return true;
        return element.tagName === "HEAD";
    }
    def("isHidden", isHidden);

    /**
     * Visit the descendants of an element in DOM order (the element itself is not visited).
     * @param {Element} element - Root.
     * @param {function(Element):*} okFn - Visitor; a truthy result stops and is returned.
     * @param {boolean} [traverseHidden] - Also visit hidden elements.
     * @returns {*} The first truthy okFn result, or null.
     */
    function traverseChildren(element, okFn, traverseHidden) {
        const stack = [];
        let e = element;
        let c = e.firstElementChild;
        for (; ;) {
            if (c === undefined) {
                const r = okFn(e);
                if (r)
                    return r;
                c = e.firstElementChild;
            }
            else if (c) {
                const n = c;
                c = c.nextElementSibling;
                if (traverseHidden || !isHidden(n)) {
                    stack.push([e, c]);
                    e = n;
                    c = undefined;
                }
            }
            else {
                if (stack.length <= 0)
                    return null;
                [e, c] = stack.pop();
            }
        }
    }
    def("traverseChildren", traverseChildren);

    /**
     * Visit all elements after an element in DOM order (wrapping around the document, stops at the start element).
     * @param {Element} element - Start element (not visited).
     * @param {function(Element):*} okFn - Visitor; a truthy result stops and is returned.
     * @param {boolean} [traverseHidden] - Also visit hidden elements.
     * @returns {*} The first truthy okFn result, or null.
     */
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
            if (c === undefined) {
                const r = okFn(e);
                if (r)
                    return r;
                c = e.firstElementChild;
            }
            else if (c) {
                const n = c;
                if (n === start)
                    return null;
                c = c.nextElementSibling;
                if (traverseHidden || !isHidden(n)) {
                    stack.push([e, c]);
                    e = n;
                    c = undefined;
                }
            }
            else {
                if (stack.length <= 0) {
                    e = document.documentElement;
                    c = undefined;
                    continue;
                }
                [e, c] = stack.pop();
            }
        }
    }
    def("traverseForward", traverseForward);

    /**
     * Visit all elements before an element in reverse DOM order (wrapping around the document, stops at the start element).
     * @param {Element} element - Start element (not visited).
     * @param {function(Element):*} okFn - Visitor; a truthy result stops and is returned.
     * @param {boolean} [traverseHidden] - Ignored (as in the old implementation).
     * @returns {*} The first truthy okFn result, or null.
     */
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
                [e, c] = stack.pop();
            }
            else {
                c = c ? c.previousElementSibling : e.lastElementChild;
                if (c) {
                    stack.push([e, c]);
                    e = c;
                    c = undefined;
                }
            }
        }
    }
    def("traverseBackward", traverseBackward);

    /**
     * True if the element takes the focus with Tab (tabIndex >= 0 or a writable input).
     * @param {HTMLElement} element - The element.
     * @returns {boolean}
     */
    function isTabable(element) {
        if (element.tabIndex >= 0)
            return true;
        return element.tagName === "INPUT" && !element.readOnly;
    }
    def("isTabable", isTabable);

    /**
     * Focus the next tabbable element after an element.
     * @param {Element} element - Start.
     * @returns {HTMLElement|null} The focused element.
     */
    function focusNextTab(element) {
        const f = traverseForward(element, e => isTabable(e) ? e : null);
        if (f)
            f.focus();
        return f;
    }
    def("focusNextTab", focusNextTab);

    /**
     * Focus the previous tabbable element before an element.
     * @param {Element} element - Start.
     * @returns {HTMLElement|null} The focused element.
     */
    def("focusPrevTab", element => {
        const f = traverseBackward(element, e => isTabable(e) ? e : null);
        if (f)
            f.focus();
        return f;
    });

    /**
     * Make Enter in an input move the focus to the next tabbable element (replaces inputElement.onkeyup).
     * @param {HTMLElement} inputElement - The input.
     * @param {function(HTMLElement|null, HTMLElement):Promise<void>} [onNextItemFn] - Called with the newly focused element.
     */
    def("tabToNextOnEnter", (inputElement, onNextItemFn) => {
        inputElement.onkeyup = async ev => {
            if (ev.key !== "Enter")
                return;
            if (badClick(ev))
                return;
            const nf = focusNextTab(inputElement);
            if (onNextItemFn)
                await onNextItemFn(nf, inputElement);
        };
    });

    /**
     * True if the event has no modifier keys.
     * @param {Event} [ev] - The event.
     * @returns {boolean}
     */
    def("isPureClick", ev => Sw.IsPureEvent(ev));

    /**
     * The old click guard: prevents the default and stops the propagation (unless keepProp), true if a modifier key is held.
     * @param {Event} [ev] - The event.
     * @param {boolean} [keepProp] - Don't prevent / stop.
     * @returns {boolean} True if the click should be ignored.
     */
    function badClick(ev, keepProp) {
        return Sw.BadClick(ev, keepProp);
    }
    def("badClick", badClick);

    /**
     * Make an element keyboard clickable (Space / Enter click it). Sets tabIndex 0, adds the classes KeyClick (old style sheets) and
     * sw-keyclick, replaces element.onkeyup (as before).
     * @param {HTMLElement} element - The element.
     * @returns {HTMLElement} The element.
     */
    function keyboardClick(element) {
        const e = element;
        if (!e.tabIndex || e.tabIndex < 0)
            e.tabIndex = 0;
        e.classList.add("KeyClick", "sw-keyclick");
        e.onkeyup = ev => {
            const k = ev.key;
            if (k !== " " && k !== "Enter")
                return;
            if (badClick(ev))
                return;
            e.click();
        };
        return e;
    }
    def("keyboardClick", keyboardClick);

    /**
     * Undo keyboardClick.
     * @param {HTMLElement} element - The element.
     * @returns {HTMLElement} The element.
     */
    function removeKeyboardClick(element) {
        const e = element;
        e.removeAttribute("tabindex");
        e.classList.remove("KeyClick", "sw-keyclick");
        e.onkeyup = null;
        return e;
    }
    def("removeKeyboardClick", removeKeyboardClick);

    /**
     * Make a right click perform a click (replaces element.oncontextmenu; with a modifier key the browser menu shows).
     * @param {HTMLElement} element - The element.
     * @returns {HTMLElement} The element.
     */
    def("rightClick", element => {
        element.oncontextmenu = ev => {
            if (badClick(ev, true))
                return true;
            element.click();
            return false;
        };
        return element;
    });

    /**
     * Undo rightClick.
     * @param {HTMLElement} element - The element.
     * @returns {HTMLElement} The element.
     */
    def("removeRightClick", element => {
        element.oncontextmenu = null;
        return element;
    });

    def("makeHtmlAttributeSafe", (s, preserveCR) => F.HtmlAttributeSafe(s, preserveCR));
    def("makeHtmlSafe", text => F.HtmlSafe(text));
    def("isValidEmail", s => F.IsValidEmail(s));
    def("isValidPhone", s => F.IsValidPhone(s));

    /**
     * Read an attribute as a number (default if missing, malformed or negative).
     * @param {HTMLElement} element - The element.
     * @param {string} attribute - The attribute name.
     * @param {number} [defValue=0] - Default.
     * @returns {number}
     */
    def("getNumberAttribute", (element, attribute, defValue) => {
        const v = parseFloat(element.getAttribute(attribute));
        return v >= 0 ? v : (defValue ?? 0);
    });

    /**
     * Read an attribute as an integer (default if missing, malformed or negative).
     * @param {HTMLElement} element - The element.
     * @param {string} attribute - The attribute name.
     * @param {number} [defValue=0] - Default.
     * @returns {number}
     */
    def("getIntegerAttribute", (element, attribute, defValue) => {
        const v = parseInt(element.getAttribute(attribute));
        return v >= 0 ? v : (defValue ?? 0);
    });

    /**
     * Insert text at the cursor of an input / textarea (replaces the selection).
     * @param {HTMLInputElement|HTMLTextAreaElement} myField - The field.
     * @param {string} myValue - The text.
     * @param {boolean} [updateSelection] - Select the inserted text (else the end is remembered for the next insert).
     * @returns {number} The position after the text.
     */
    def("insertAtCursor", (myField, myValue, updateSelection) => {
        const v = myField.value;
        let s = myField.selectionStart;
        let e = myField.selectionEnd;
        const sl = myField.NextLast;
        if (!s && sl) {
            s = sl;
            e = sl;
        }
        myField.NextLast = null;
        s = s || 0;
        myField.value = (s > 0 ? v.substring(0, s) : "") + myValue + v.substring(e);
        e = s + myValue.length;
        if (updateSelection)
            myField.setSelectionRange(s, e);
        else
            myField.NextLast = e;
        return e;
    });

    // ------------------------------------------------------------------ smooth img / iframe loading

    /**
     * True if a node opts in to smooth src loading (data-sw-fade, data-sw-fadein or data-sw-useevent).
     * @param {Node} e - The node.
     * @returns {boolean}
     */
    def("doHandleSrc", e => !!(e && typeof e.hasAttribute === "function" && (e.hasAttribute("data-sw-fade") || e.hasAttribute("data-sw-fadein") || e.hasAttribute("data-sw-useevent"))));

    /**
     * Make an img / iframe hidden while loading and fade it in.
     * @param {HTMLElement} element - The element.
     * @param {boolean} [showError] - Show the broken image on errors.
     * @param {number} [fadeIn] - Fade in duration (ms).
     * @param {number} [fadeDelay] - Delay before fading in (ms).
     * @param {string} [fadeChannel] - "name,ms" channel (staggered fade ins).
     * @param {number} [fadeParent] - Fade the n:th parent instead.
     * @returns {HTMLElement} The element.
     */
    function fixElementWithSrc(element, showError, fadeIn, fadeDelay, fadeChannel, fadeParent) {
        return Sw.FadeSrc(element, { ShowError: showError, FadeIn: fadeIn, FadeDelay: fadeDelay, Channel: fadeChannel, Parent: fadeParent });
    }
    def("fixElementWithSrc", fixElementWithSrc);

    /**
     * Undo fixElementWithSrc.
     * @param {HTMLElement} e - The element.
     */
    def("cleanElementWithSrc", e => Sw.UnfadeSrc(e));

    /** Old internal of the smooth loading (fade channels are handled by Sw.FadeSrc): no-op. */
    def("removeFadeChannel", () => {
    });
    /** Old internal event handler of the smooth loading (handled by Sw.FadeSrc): no-op. */
    def("InternalSrcElementOnEvent", () => {
    });
    /** Old internal event handler of the smooth loading (handled by Sw.FadeSrc): no-op. */
    def("InternalSrcElementOnLoad", () => {
    });
    /** Old internal event handler of the smooth loading (handled by Sw.FadeSrc): no-op. */
    def("InternalSrcElementOnError", () => {
    });

    /**
     * Create an element that is hidden while loading its src.
     * @param {string} tagName - "img" or "iframe".
     * @param {boolean} [showError] - Show the broken image on errors.
     * @param {number} [fadeIn] - Fade in duration (ms).
     * @returns {HTMLElement} The element.
     */
    def("createElementWithSrc", (tagName, showError, fadeIn) => fixElementWithSrc(document.createElement(tagName), showError, fadeIn));

    /**
     * Create an iframe that is hidden while loading.
     * @param {number} [fadeIn] - Fade in duration (ms).
     * @returns {HTMLIFrameElement}
     */
    def("createIFrame", fadeIn => Sw.CreateIFrame(fadeIn));

    /**
     * Create an img that is hidden while loading.
     * @param {boolean} [showError] - Show the broken image on errors.
     * @param {number} [fadeIn] - Fade in duration (ms).
     * @returns {HTMLImageElement}
     */
    def("createImg", (showError, fadeIn) => Sw.CreateImg(!!showError, fadeIn));

    /**
     * Set / reload an img src.
     * @param {HTMLImageElement} img - The image.
     * @param {string} [src] - New url (default the current).
     * @param {string} [fallback] - Url used when loading fails.
     * @param {boolean} [flushCache] - Re-validate the cached copy first.
     * @param {function(boolean):void} [onLoaded] - Called with true if loaded.
     * @param {string} [reloadOnMessage] - A server message type that reloads the image.
     * @returns {Promise<boolean>} True if loaded.
     */
    def("SetImageSource", (img, src, fallback, flushCache, onLoaded, reloadOnMessage) =>
        Sw.SetImageSource(img, src, { Fallback: fallback, FlushCache: flushCache, OnLoaded: onLoaded, ReloadOn: reloadOnMessage }));

    def("PendingImages", Sw.PendingImages);

    // ------------------------------------------------------------------ includes, async helpers

    /**
     * Load a script once.
     * @param {string} [current] - A script url the file is relative to (null = the common/ folder).
     * @param {string} file - Relative or absolute url.
     * @param {boolean} [noThrow] - Resolve instead of rejecting on errors.
     * @returns {Promise<{element: HTMLElement, error: string, errorEv: Event, loaded: boolean}>}
     */
    def("includeJs", (current, file, noThrow) => N.IncludeJs(current, file, noThrow));

    /**
     * Load a style sheet once.
     * @param {string} [current] - A script url the file is relative to (null = the common/ folder).
     * @param {string} file - Relative or absolute url.
     * @param {boolean} [noThrow] - Resolve instead of rejecting on errors.
     * @returns {Promise<{element: HTMLElement, error: string, errorEv: Event, loaded: boolean}>}
     */
    def("includeCss", (current, file, noThrow) => N.IncludeCss(current, file, noThrow));

    /**
     * Wait for an event.
     * @param {EventTarget} element - The target.
     * @param {string} eventName - The event name.
     * @param {function(EventTarget):void} [fn] - Called after the listener is added.
     * @param {number} [timeout] - Time out in ms (resolves null).
     * @returns {Promise<Event|null>}
     */
    def("waitEvent", (element, eventName, fn, timeout) => U.WaitEvent(element, eventName, timeout, fn));

    /**
     * Wait for the first of two events.
     * @param {EventTarget} element - The target.
     * @param {string} eventName1 - An event name.
     * @param {string} eventName2 - Another event name.
     * @param {function(EventTarget):void} [fn] - Called after the listeners are added.
     * @param {number} [timeout] - Time out in ms (resolves null).
     * @returns {Promise<Event|null>}
     */
    def("waitEvent2", (element, eventName1, eventName2, fn, timeout) => U.WaitEvent(element, [eventName1, eventName2], timeout, fn));

    /**
     * Wait until fn calls the end function it gets.
     * @param {function(function():void):*} fn - Gets the end function.
     * @returns {Promise<void>}
     */
    def("waitFor", fn => new Promise(resolve => {
        const r = fn(() => resolve());
        if (r && typeof r.catch === "function")
            r.catch(e => console.error(e));
    }));

    /**
     * Set the src of a media element and wait until it can play through (or fails).
     * @param {HTMLMediaElement} mediaElement - The element.
     * @param {string} url - The url.
     * @param {function(boolean, HTMLMediaElement, string):void} [onResult] - Called with (success, element, url).
     * @returns {Promise<void>}
     */
    def("setMediaSrcAndWait", (mediaElement, url, onResult) => new Promise(resolve => {
        const done = ok => {
            mediaElement.removeEventListener("canplaythrough", o);
            mediaElement.removeEventListener("error", e);
            if (onResult)
                onResult(ok, mediaElement, url);
            resolve();
        };
        const o = () => done(true);
        const e = () => done(false);
        mediaElement.addEventListener("canplaythrough", o);
        mediaElement.addEventListener("error", e);
        mediaElement.src = url;
    }));

    def("AbortHandler", U.AbortHandler);
    def("delayOneFrame", () => U.DelayFrame());
    def("delay", ms => U.Delay(ms));
    def("delayWithAbort", (ms, abortHandler) => U.DelayAbort(ms, abortHandler));

    /**
     * The old shared frame loop (Sw.Util.Animator).
     */
    class Animator {
        /**
         * Call a function every frame with the elapsed ms; it returns true to stop.
         * @param {function(number):boolean} func - The function.
         */
        static Add(func) {
            U.Animator.Add(func);
        }

        /**
         * Stop calling a function.
         * @param {function(number):boolean} func - The function.
         */
        static Remove(func) {
            U.Animator.Remove(func);
        }

        /** @type {Map<function, number>} The registered functions. */
        static get Functions() {
            return U.Animator.Functions;
        }

        /** @type {boolean} True while the frame loop runs. */
        static get HaveAnimation() {
            return U.Animator.Running;
        }
    }
    def("Animator", Animator);

    // ------------------------------------------------------------------ navigation, frames

    def("PageLoaded", () => P.SignalLoaded());

    /**
     * Open an url (default target "_blank"; "_self" uses the IframeNavigating protocol).
     * @param {string} l - The url.
     * @param {string} [target] - The window target.
     */
    function Open(l, target) {
        P.Navigate(l, target || "_blank");
    }
    def("Open", Open);

    def("EditOrOpenUrl", (read, target, save, remove) => P.EditOrOpen(read, target, save, remove));
    def("EditUrl", (read, target, save, remove) => P.Edit(read, target, save, remove));
    def("ViewUrl", (read, target) => P.View(read, target));
    def("IsSameOrigin", url => Sw.Url.IsSameOrigin(url));
    def("IsAbsolutePath", url => Sw.Url.IsAbsolute(url));
    def("GetAbsolutePath", (url, current) => Sw.Url.Absolute(url, current));
    def("FlushCache", url => N.FlushCache(url));

    /**
     * Flush an url from the cache, then call whenDone.
     * @param {string} url - The url.
     * @param {function():*} [whenDone] - Called when done.
     */
    def("FlushCacheSync", (url, whenDone) => {
        N.FlushCache(url).catch(() => {
        }).then(() => {
            if (whenDone)
                whenDone();
        });
    });

    // ------------------------------------------------------------------ requests

    def("ToTypedJson", (data, optionalType) => N.ToTypedJson(data, optionalType));
    def("postData", (url, data) => N.Beacon(url, data));

    /**
     * Send an api request with one json argument (GET or POST, compressed when worth it).
     * @param {string} url - The api url.
     * @param {*} data - The argument.
     * @param {boolean} [reload] - Bypass the cache.
     * @param {function(number):Promise<boolean>} [errHandler] - Error handler, true = retry.
     * @param {boolean} [returnBlob] - Return the Blob.
     * @param {function(Request):void} [setupRequestFn] - Setup of the Request.
     * @param {boolean} [usePost] - Force POST (default in debug builds).
     * @returns {Promise<*>} The decoded json (Blob, null after a redirect, undefined if not decodable).
     */
    def("sendRequest", (url, data, reload, errHandler, returnBlob, setupRequestFn, usePost) => {
        //  (the usual positional nulls are "left out", as the old function treated them: no option warnings)
        const o = { Reload: reload ?? undefined, ErrHandler: errHandler ?? undefined, Blob: returnBlob ?? undefined, Setup: setupRequestFn ?? undefined };
        if (usePost !== undefined && usePost !== null)
            o.Post = usePost;
        return N.Send(url, data, o);
    });

    /**
     * Send a GET request.
     * @param {string|Request} url - The url.
     * @param {boolean} [reload] - Bypass the cache.
     * @param {boolean} [returnBlob] - Return the Blob.
     * @param {function(Request):void} [setupRequestFn] - Setup of the Request.
     * @param {function(Response):Promise<*>} [returnFn] - Custom response reader.
     * @param {number} [maxRetryCount] - The maximum number of attempts (mapped to the Sw.Net option Retry = attempts - 1,
     * at most 10 retries; retried are network errors and 502 / 503 / 504). Missing or below 2: no retry (the old function
     * documented a default of 10000 but never retried).
     * @returns {Promise<*>} The decoded json (or the returnFn / Blob result).
     */
    def("getRequest", (url, reload, returnBlob, setupRequestFn, returnFn, maxRetryCount) => {
        const n = Math.floor(+maxRetryCount || 0);
        return N.Get(url, { Reload: reload ?? undefined, Blob: returnBlob ?? undefined, Setup: setupRequestFn ?? undefined, Read: returnFn ?? undefined, Retry: n > 1 ? Math.min(10, n - 1) : 0 });
    });

    /**
     * The old in-memory cache (Sw.Net.Cached).
     */
    class Cache {
        /** @type {Map} Old internal storage (unused). */
        static InternalCache = new Map();

        /**
         * Get data from the cache or fetchFn.
         * @param {string} cacheName - Cache id.
         * @param {*} cacheKey - Key object.
         * @param {number} lifeTime - Life time in ms.
         * @param {function(*, string):Promise<*>} fetchFn - Fetches the data (cacheKey, cacheName).
         * @returns {Promise<*>}
         */
        static Get(cacheName, cacheKey, lifeTime, fetchFn) {
            return N.Cached(cacheName, cacheKey, lifeTime, fetchFn);
        }

        /**
         * A cached GET request.
         * @param {string} url - The url.
         * @param {number} lifeTime - Life time in ms.
         * @returns {Promise<*>}
         */
        static GetRequest(url, lifeTime) {
            return N.CachedGet(url, lifeTime);
        }

        /**
         * A cached api request.
         * @param {string} url - The api url.
         * @param {*} data - The argument.
         * @param {number} lifeTime - Life time in ms.
         * @returns {Promise<*>}
         */
        static SendRequest(url, data, lifeTime) {
            return N.CachedSend(url, data, lifeTime);
        }
    }
    def("Cache", Cache);

    // ------------------------------------------------------------------ formatting helpers, extensions

    def("RankScoreMatch", (searchFor, ...inTexts) => F.RankScoreMatch(searchFor, ...inTexts));
    def("CurrencyFormatOptions", F.CurrencyOptions);
    def("ArgAsHtml", (argValue, elementClass, elementType) => Sw.I18n.ArgAsHtml(argValue, elementClass, elementType));
    def("getFileExtension", path => F.FileExtension(path));
    def("imageExtensions", F.Ext.Image);
    def("videoExtensions", F.Ext.Video);
    def("audioExtensions", F.Ext.Audio);
    def("programmingExtensions", F.Ext.Programming);
    def("compressExtensions", F.Ext.Compress);
    def("isImageExt", ext => F.IsImageExt(ext));
    def("isAudioExt", ext => F.IsAudioExt(ext));
    def("isProgrammingExt", ext => F.IsProgrammingExt(ext));
    def("isVideoExt", ext => F.IsVideoExt(ext));
    def("isCompressibleExt", ext => F.IsCompressibleExt(ext));
    def("isImageFile", path => F.IsImageFile(path));
    def("isVideoFile", path => F.IsVideoFile(path));
    def("isLetter", c => F.IsLetter(c));

    /**
     * Resolve a dotted name ("A.B.fn") from a context (throws if an intermediate is missing, as before).
     * @param {string} functionName - The path.
     * @param {object} [context=window] - The root.
     * @returns {*} The value.
     */
    def("GetFunction", (functionName, context) => {
        let c = context || window;
        const parts = functionName.split(".");
        const last = parts.pop();
        for (const p of parts)
            c = c[p];
        return c[last];
    });

    /**
     * Resolve a css color to its computed "rgb(...)" / "rgba(...)" text.
     * @param {string} colorKeyword - Any css color.
     * @returns {string}
     */
    def("GetRgbColor", colorKeyword => {
        let e = window.DivGetRgbColor;
        if (!e) {
            e = document.createElement("div");
            e.style.display = "none";
            window.DivGetRgbColor = e;
        }
        e.style.color = colorKeyword;
        document.body.appendChild(e);
        const v = getComputedStyle(e).color;
        e.remove();
        return v;
    });

    // ------------------------------------------------------------------ toasts, loading, focus blocking, full screen

    /**
     * The old Fail / Info / Message kept the shown message in document.body.FailElement (code reused it to restyle or remove the
     * message; S5-A-16): the toast element of the last one, null when it is gone.
     * @param {*} t The toast (Sw.Fail / Info / Message).
     */
    function keepFailElement(t) {
        const e = t && t.E instanceof Element ? t.E : (t instanceof Element ? t : null);
        if (!e || !e.isConnected || !document.body)
            return;
        document.body.FailElement = e;
    }

    /**
     * Show an error toast.
     * @param {string} text - The text.
     * @param {number|boolean} [duration] - Ms (default 5000, true = 24 h).
     * @param {boolean} [noCopyOnClick] - A click only removes it.
     * @returns {undefined} Nothing, as the original (the toast element is document.body.FailElement, as the original's).
     */
    function Fail(text, duration, noCopyOnClick) {
        keepFailElement(Sw.Fail(text, duration, noCopyOnClick));
    }
    def("Fail", Fail);

    /**
     * Show an information toast.
     * @param {string} text - The text.
     * @param {number|boolean} [duration] - Ms (default 5000, true = 24 h).
     * @param {boolean} [noCopyOnClick] - A click only removes it.
     * @returns {undefined} Nothing, as the original (the toast element is document.body.FailElement, as the original's).
     */
    function Info(text, duration, noCopyOnClick) {
        keepFailElement(Sw.Info(text, duration, noCopyOnClick));
    }
    def("Info", Info);

    /**
     * Show a toast with an old message class ("MsgError", "MsgInfo").
     * @param {string} text - The text.
     * @param {number|boolean} [duration] - Ms (default 5000, true = 24 h).
     * @param {string} [className] - "MsgError" | "MsgInfo".
     * @param {boolean} [noCopyOnClick] - A click only removes it.
     * @returns {undefined} Nothing, as the original (the toast element is document.body.FailElement, as the original's).
     */
    def("Message", (text, duration, className, noCopyOnClick) => {
        keepFailElement(Sw.Message(text, duration, className, noCopyOnClick));
    });

    /**
     * Show the loading overlay (Sw.Loader through Sw.Page.Loading; none with the url flag noloader).
     * @param {HTMLElement} [page] - The element to cover (default the page).
     * @param {string} [text] - A text.
     * @param {boolean} [opaque] - Hide the content completely.
     * @param {string} [useIcon] - An icon class instead of the spinner ("" = none).
     * @returns {function(boolean):void} Removes the overlay; unless called with true it signals PageLoaded / WindowLoaded and restores the
     * scroll position.
     */
    def("AddLoading", (page, text, opaque, useIcon) => P.Loading({ Element: page || null, Text: text, Opaque: !!opaque, Icon: useIcon }));

    /**
     * Keep the keyboard focus inside an element (stacked; call UnblockTab when done).
     * @param {HTMLElement} element - The element.
     */
    def("BlockTab", element => {
        Sw.Focus.Trap(element);
    });

    /**
     * Release a BlockTab (restores the previous focus when it was the active block).
     * @param {HTMLElement} element - The element.
     */
    def("UnblockTab", element => {
        Sw.Focus.Release(element);
    });

    /**
     * Browser full screen helpers (Sw.FullScreen).
     */
    class FullScreen {
        /**
         * True in full screen (also an installed app running standalone).
         * @returns {boolean}
         */
        static IsFull() {
            return Sw.FullScreen.IsFull();
        }

        /**
         * Enter full screen (the whole page).
         * @returns {Promise<boolean>} True on success.
         */
        static Enter() {
            return Sw.FullScreen.Enter();
        }

        /**
         * Exit full screen.
         * @returns {Promise<void>}
         */
        static async Exit() {
            await Sw.FullScreen.Exit();
        }

        /**
         * Toggle full screen.
         * @returns {Promise<void>}
         */
        static async Toggle() {
            await Sw.FullScreen.Toggle();
        }
    }
    def("FullScreen", FullScreen);

    /**
     * Prevent the device from sleeping (ref counted; plays a tiny muted looping video).
     * @returns {Promise<void>}
     */
    def("EnterNoSleep", async () => {
        let state = document.NoSleep;
        if (!state) {
            const e = document.createElement("video");
            e.setAttribute("muted", "");
            e.muted = true;
            e.setAttribute("title", _TF("No Sleep", "Title of an invisible looping video that keeps the screen awake: it may show in the media controls of the system"));
            e.setAttribute("playsinline", "");
            e.setAttribute("loop", "");
            const add = (type, uri) => {
                const s = document.createElement("source");
                s.src = uri;
                s.type = "video/" + type;
                e.appendChild(s);
            };
            add("webm", "data:Video/webm;base64,GkXfo59ChoEBQveBAULygQRC84EIQoKEd2VibUKHgQJChYECGFOAZwEAAAAAAAJQEU2bdLpNu4tTq4QVSalmU6yBoU27i1OrhBZUrmtTrIHYTbuMU6uEElTDZ1OsggEpTbuMU6uEHFO7a1OsggI67AEAAAAAAABZAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVSalmsirXsYMPQkBNgI1MYXZmNTguNjQuMTAwV0GNTGF2ZjU4LjY0LjEwMESJiEB/QAAAAAAAFlSua8yuAQAAAAAAAEPXgQFzxYg/RBAYXrWgH5yBACK1nIN1bmSGhVZfVlA4g4EBI+ODhA7msoDgAQAAAAAAABCwgRC6gRCagQJVsIRVuYEBElTDZ0Cac3MBAAAAAAAAJ2PAgGfIAQAAAAAAABpFo4dFTkNPREVSRIeNTGF2ZjU4LjY0LjEwMHNzAQAAAAAAAF9jwItjxYg/RBAYXrWgH2fIAQAAAAAAACJFo4dFTkNPREVSRIeVTGF2YzU4LjExMi4xMDEgbGlidnB4Z8iiRaOIRFVSQVRJT05Eh5QwMDowMDowMC41MDAwMDAwMDAAAB9DtnXs54EAo72BAACA8AIAnQEqEAAQAABHCIWFiIWEiAICAnWqA/gD+gINTRgA/v1u8//jmTcwxP+Obf/xYTwOKMj/8VEAo6iBAPoAsQEABhDMABgAMCgv9AAgAP7ujn+u8bZhtu2//hx9RhQf+F/AHFO7a5G7j7OBALeK94EB8YIByfCBAw==");
            add("mp4", "data:Video/mp4;base64,AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAAIZnJlZQAAAuVtZGF0AAACoQYF//+d3EXpvebZSLeWLNgg2SPu73gyNjQgLSBjb3JlIDE2MSAtIEguMjY0L01QRUctNCBBVkMgY29kZWMgLSBDb3B5bGVmdCAyMDAzLTIwMjAgLSBodHRwOi8vd3d3LnZpZGVvbGFuLm9yZy94MjY0Lmh0bWwgLSBvcHRpb25zOiBjYWJhYz0xIHJlZj0xNiBkZWJsb2NrPTE6MDowIGFuYWx5c2U9MHgzOjB4MTMzIG1lPXVtaCBzdWJtZT0xMCBwc3k9MSBwc3lfcmQ9MS4wMDowLjAwIG1peGVkX3JlZj0xIG1lX3JhbmdlPTI0IGNocm9tYV9tZT0xIHRyZWxsaXM9MiA4eDhkY3Q9MSBjcW09MCBkZWFkem9uZT0yMSwxMSBmYXN0X3Bza2lwPTEgY2hyb21hX3FwX29mZnNldD0tMiB0aHJlYWRzPTEgbG9va2FoZWFkX3RocmVhZHM9MSBzbGljZWRfdGhyZWFkcz0wIG5yPTAgZGVjaW1hdGU9MSBpbnRlcmxhY2VkPTAgYmx1cmF5X2NvbXBhdD0wIGNvbnN0cmFpbmVkX2ludHJhPTAgYmZyYW1lcz04IGJfcHlyYW1pZD0yIGJfYWRhcHQ9MiBiX2JpYXM9MCBkaXJlY3Q9MyB3ZWlnaHRiPTEgb3Blbl9nb3A9MCB3ZWlnaHRwPTIga2V5aW50PTI1MCBrZXlpbnRfbWluPTQgc2NlbmVjdXQ9NDAgaW50cmFfcmVmcmVzaD0wIHJjX2xvb2thaGVhZD02MCByYz1jcmYgbWJ0cmVlPTEgY3JmPTIwLjAgcWNvbXA9MC42MCBxcG1pbj0wIHFwbWF4PTY5IHFwc3RlcD00IGlwX3JhdGlvPTEuNDAgYXE9MToxLjAwAIAAAAAVZYiBAAIf/urj/MsrpVzX+SWZakk1AAAAG0GaCC2Ib//UcD3gUv2YrYFNTFfYNHPNfm5UhgAAAzRtb292AAAAbG12aGQAAAAAAAAAAAAAAAAAAAPoAAAB9AABAAABAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAACXnRyYWsAAABcdGtoZAAAAAMAAAAAAAAAAAAAAAEAAAAAAAAB9AAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAEAAAABAAAAAAACRlZHRzAAAAHGVsc3QAAAAAAAAAAQAAAfQAAAAAAAEAAAAAAdZtZGlhAAAAIG1kaGQAAAAAAAAAAAAAAAAAAEAAAAAgAFXEAAAAAAAtaGRscgAAAAAAAAAAdmlkZQAAAAAAAAAAAAAAAFZpZGVvSGFuZGxlcgAAAAGBbWluZgAAABR2bWhkAAAAAQAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAAAAABAAABQXN0YmwAAADBc3RzZAAAAAAAAAABAAAAsWF2YzEAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAEAAQAEgAAABIAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY//8AAAA3YXZjQwFkAAr/4QAZZ2QACqxyBF7ARAAAAwAEAAADACA8SJYRgAEAB2joQ4bLIsD9+PgAAAAAEHBhc3AAAAABAAAAAQAAABRidHJ0AAAAAAAALdAAAC3QAAAAGHN0dHMAAAAAAAAAAQAAAAIAABAAAAAAFHN0c3MAAAAAAAAAAQAAAAEAAAAcc3RzYwAAAAAAAAABAAAAAQAAAAIAAAABAAAAHHN0c3oAAAAAAAAAAAAAAAIAAAK+AAAAHwAAABRzdGNvAAAAAAAAAAEAAAAwAAAAYnVkdGEAAABabWV0YQAAAAAAAAAhaGRscgAAAAAAAAAAbWRpcmFwcGwAAAAAAAAAAAAAAAAtaWxzdAAAACWpdG9vAAAAHWRhdGEAAAABAAAAAExhdmY1OC42NC4xMDA=");
            e.addEventListener("timeupdate", () => {
                if (e.currentTime > 0.25)
                    e.currentTime = Math.random() * 0.02;
            });
            state = { Video: e, Count: 0 };
            document.NoSleep = state;
        }
        const c = state.Count;
        state.Count = c + 1;
        if (c === 0)
            await state.Video.play();
    });

    /**
     * True if EnterNoSleep is active.
     * @returns {boolean}
     */
    def("IsSleepPrevented", () => !!document.NoSleep && document.NoSleep.Count > 0);

    /**
     * Release one EnterNoSleep.
     * @returns {Promise<void>}
     */
    def("ExitNoSleep", async () => {
        const state = document.NoSleep;
        if (!state)
            return;
        const c = state.Count;
        state.Count = c - 1;
        if (c === 1)
            await state.Video.pause();
    });

    // ------------------------------------------------------------------ scrolling, tracking (old implementations, pure DOM)

    /**
     * True if an element is scrolled to the bottom (1 px tolerance).
     * @param {HTMLElement} element - The element.
     * @returns {boolean}
     */
    def("IsAtMaxScrollV", element => element.scrollTop >= element.scrollHeight - element.clientHeight - 1);

    /**
     * Keep an element scrolled to the bottom while new content arrives (freed by scrolling up, re-attached by scrolling to the end).
     * Replaces element.onscroll, toggles class "Stick".
     * @param {HTMLElement} element - The scrolling element.
     * @param {boolean} [stickByDefault=true] - Start stuck.
     * @param {function(HTMLElement)} [onBottom] - Called when it sticks.
     * @param {function(HTMLElement)} [onFree] - Called when it's freed.
     * @param {function(HTMLElement)} [onTop] - Called when scrolled to the top.
     * @param {boolean} [instant] - No smooth scrolling.
     */
    def("StickToBottom", (element, stickByDefault, onBottom, onFree, onTop, instant) => {
        let s = typeof stickByDefault === "boolean" ? stickByDefault : true;
        let isInternal = false;
        const stickIt = () => {
            isInternal = true;
            try {
                element.scrollTo({ left: element.scrollLeft, top: element.scrollHeight, behavior: instant ? "instant" : "smooth" });
                requestAnimationFrame(() => isInternal = false);
            }
            catch {
                isInternal = false;
            }
        };
        element.onscroll = () => {
            if (isInternal)
                return;
            const t = element.scrollTop;
            if (onTop && t <= 1)
                onTop(element);
            const ss = t >= element.scrollHeight - element.clientHeight - 4;
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
        };
        new ResizeObserver(() => {
            if (s)
                stickIt();
        }).observe(element);
        new MutationObserver(() => {
            if (s)
                stickIt();
        }).observe(element, { childList: true, subtree: true });
        if (s)
            requestAnimationFrame(stickIt);
    });

    /**
     * True if an element is document.body or below it.
     * @param {HTMLElement} el - The element.
     * @returns {boolean}
     */
    function IsAttached(el) {
        while (el) {
            if (el === document.body)
                return true;
            el = el.parentElement;
        }
        return false;
    }
    def("IsAttached", IsAttached);

    /**
     * Get the document relative rectangle of an element.
     * @param {Element} el - The element.
     * @returns {{found: boolean|undefined, left: number, top: number, width: number, height: number, right: number, bottom: number}}
     */
    function GetAbsoluteRect(el) {
        let found, left = 0, top = 0, width = 0, height = 0;
        if (el && el.ownerDocument === document && el.getBoundingClientRect) {
            const r = el.getBoundingClientRect();
            found = true;
            left = r.left + window.scrollX;
            top = r.top + window.scrollY;
            width = r.width;
            height = r.height;
        }
        return { found, left, top, width, height, right: left + width, bottom: top + height };
    }
    def("GetAbsoluteRect", GetAbsoluteRect);

    /**
     * Keep a fixed position element next to an element (or the point of a mouse event on it) while scrolling / resizing; stops when the
     * target element is removed.
     * @param {MouseEvent|HTMLElement} mouseEventOrElement - The anchor (an event: its target, at the clicked point).
     * @param {HTMLElement} targetElement - The fixed element to move.
     * @param {number} [margin] - Keep it on screen with this margin.
     */
    def("TrackElement", (mouseEventOrElement, targetElement, margin) => {
        const b = document.body;
        let relX = 0.5, relY = 0.5, x, y;
        let track = mouseEventOrElement;
        if (mouseEventOrElement instanceof MouseEvent) {
            track = mouseEventOrElement.target;
            const st = GetAbsoluteRect(track);
            if (!mouseEventOrElement.isTrusted) {
                x = (st.left + st.right) * 0.5 - window.scrollX;
                y = (st.top + st.bottom) * 0.5 - window.scrollY;
            }
            else {
                x = mouseEventOrElement.clientX;
                y = mouseEventOrElement.clientY;
                relX = st.width > 0 ? ((x + window.scrollX - st.left) / st.width) : 0.5;
                relY = st.height > 0 ? ((y + window.scrollY - st.top) / st.height) : 0.5;
            }
        }
        else {
            const st = GetAbsoluteRect(track);
            x = (st.left + st.right) * 0.5 - window.scrollX;
            y = (st.top + st.bottom) * 0.5 - window.scrollY;
        }
        const ax = x >= b.clientWidth * 0.5 ? 1 : 0;
        const ay = y >= b.clientHeight * 0.5 ? 1 : 0;
        const css = targetElement.style;
        let obs = null;
        let frame = 0;
        const onScroll = () => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(update);
        };
        function update(ev) {
            if (ev && obs && !IsAttached(targetElement)) {
                document.removeEventListener("scroll", onScroll);
                obs.disconnect();
                return;
            }
            const cr = GetAbsoluteRect(track);
            const mr = GetAbsoluteRect(targetElement);
            let px = relX * cr.width + cr.left - window.scrollX - ax * mr.width;
            let py = relY * cr.height + cr.top - window.scrollY - ay * mr.height;
            if (margin && margin >= 0) {
                px = Math.max(px, margin);
                py = Math.max(py, margin);
                const vw = b.clientWidth - margin;
                const vh = b.clientHeight - margin;
                if (px + mr.width > vw)
                    px = vw - mr.width;
                if (py + mr.height > vh)
                    py = vh - mr.height;
            }
            css.top = Math.round(py) + "px";
            css.left = Math.round(px) + "px";
        }
        obs = new ResizeObserver(update);
        obs.observe(track);
        obs.observe(b);
        obs.observe(targetElement);
        document.addEventListener("scroll", onScroll);
        update();
    });

    /**
     * Show a text for a while, then restore the previous text (or optionalNewText) unless it was changed meanwhile.
     * @param {HTMLElement} element - The element.
     * @param {string} text - The temporary text.
     * @param {number} duration - Ms.
     * @param {string} [optionalNewText] - The text afterwards.
     */
    def("SetTemporaryInnerText", (element, text, duration, optionalNewText) => {
        const nt = optionalNewText ?? element.textContent;
        if (element.TempText)
            clearTimeout(element.TempText);
        const id = performance.now() + "_" + Math.random();
        element.textContent = text;
        element.TempId = id;
        element.TempText = setTimeout(() => {
            if (element.TempId !== id || element.textContent !== text)
                return;
            element.textContent = nt;
            element.TempText = null;
        }, duration);
    });

    // ------------------------------------------------------------------ page state (history)

    /**
     * Get the page state object (old internal).
     * @returns {object}
     */
    def("InternalGetHistoryState", () => {
        let s = {};
        Sw.History.Update(state => {
            s = state;
            return false;
        });
        return s;
    });
    def("ClearHistoryState", () => Sw.History.Clear());
    def("ClearOtherHistoryState", url => Sw.History.ClearOther(url));
    def("SetHistoryState", (key, value) => Sw.History.Set(key, value));
    def("UpdateHistoryState", fn => Sw.History.Update(fn));
    def("GetHistoryState", (key, ifNotFoundValue) => Sw.History.Get(key, ifNotFoundValue));

    // ------------------------------------------------------------------ media

    /**
     * Capture a frame of a video (its srcObject must be a MediaStream) as base64 encoded image bytes.
     * @param {HTMLVideoElement} video - The video.
     * @param {string} [mime="image/jpeg"] - The image type.
     * @param {number} [quality=80] - Encoding quality 0..100.
     * @returns {Promise<string>} Base64 text.
     */
    def("CaptureVideoFrame", async (video, mime, quality) => {
        mime = mime || "image/jpeg";
        if (!quality || quality <= 0)
            quality = 80;
        const s = video.srcObject.getVideoTracks()[0].getSettings();
        const c = document.createElement("canvas");
        c.width = s.width;
        c.height = s.height;
        c.getContext("2d").drawImage(video, 0, 0, s.width, s.height);
        const blob = await new Promise((resolve, reject) => {
            try {
                c.toBlob(resolve, mime, quality * 0.01);
            }
            catch (e) {
                reject(e);
            }
        });
        if (!blob)
            throw new Error(_TF("Failed to capture an image!", "An exception text when a video frame capture failed"));
        return await U.ToBase64Async(await blob.arrayBuffer());
    });

    // ------------------------------------------------------------------ ColorIcon (icon-WxH elements, styled by old page css)

    /**
     * A (clickable) single color icon: a custom element "icon-WxH" whose image is a css mask (an icon class or an url) filled with the
     * background color of a color class (IconColorThemeBackground / Main / Acc1 / Acc2 are predefined). Size dependent rules are generated
     * once per size into a style element with id "ColorIcon" (scaled by --ThemeIconSize). Kept as before because old style sheets target
     * the icon-WxH element names.
     */
    class ColorIcon {
        /** @type {Map<string, boolean>} Sizes and size + color keys whose css rules were generated. */
        static GenCss = new Map();

        /**
         * Get (create) the style sheet for the generated icon rules.
         * @returns {CSSStyleSheet}
         */
        static GetStyleSheet() {
            let ss = document.getElementById("ColorIcon");
            if (!ss || ss.tagName !== "STYLE") {
                ss = document.createElement("style");
                ss.id = "ColorIcon";
                document.head.appendChild(ss);
            }
            return ss.sheet;
        }

        /**
         * Set the image of an icon element: falsy hides it, an url (contains a ".") is used as the mask (a trailing "*" = a full color
         * background image), else it's added as a class.
         * @param {HTMLElement} element - The icon element.
         * @param {string} imageClass - Icon class or url.
         */
        static SetImageClass(element, imageClass) {
            const st = element.style;
            if (!imageClass) {
                st.maskImage = "";
                st.backgroundImage = "";
                st.backgroundColor = "";
                st.visibility = "hidden";
                return;
            }
            st.visibility = "";
            if (imageClass.includes(".")) {
                if (imageClass.charAt(imageClass.length - 1) === "*") {
                    st.maskImage = "";
                    st.backgroundImage = "url('" + imageClass.substring(0, imageClass.length - 1) + "')";
                    st.backgroundColor = "transparent";
                }
                else {
                    st.maskImage = "url('" + imageClass + "')";
                    st.backgroundImage = "";
                    st.backgroundColor = "";
                }
            }
            else {
                element.classList.add(imageClass);
                st.maskImage = "";
                st.backgroundImage = "";
                st.backgroundColor = "";
            }
        }

        /**
         * Undo SetImageClass.
         * @param {HTMLElement} element - The icon element.
         * @param {string} imageClass - The image class or url that was set.
         */
        static RemoveImageClass(element, imageClass) {
            const st = element.style;
            st.maskImage = "";
            st.backgroundImage = "";
            st.backgroundColor = "";
            if (imageClass && F.IsLetter(imageClass.charAt(0)))
                element.classList.remove(imageClass);
        }

        /**
         * Add a color class for icons of a size (once): "icon-WxH.name {background-color: col}".
         * @param {string} name - The class name.
         * @param {number} width - Width.
         * @param {number} height - Height.
         * @param {string} col - Css color.
         */
        static AddIconColor(name, width, height, col) {
            const kn = width + "x" + height + "_" + name;
            if (ColorIcon.GenCss.has(kn))
                return;
            ColorIcon.GenCss.set(kn, true);
            const ss = ColorIcon.GetStyleSheet();
            ss.insertRule("icon-" + width + "x" + height + "." + name + "{background-color:" + col + ";}", ss.cssRules.length);
        }

        /**
         * Create an icon.
         * @param {string} imageClass - Icon class (a mask-image rule) or url.
         * @param {string} colorClass - Color class (IconColorThemeBackground, IconColorThemeMain, IconColorThemeAcc1, IconColorThemeAcc2 ...).
         * @param {number} [width=16] - Width (px, scaled by --ThemeIconSize).
         * @param {number} [height=16] - Height.
         * @param {string} [title] - Tooltip.
         * @param {function(Event):*} [onClick] - Click handler (makes it a keyboard clickable button).
         * @param {string} [buttonClass="IconButton"] - Class of clickable icons.
         * @param {string} [disabledClass="IconDisabled"] - Class while disabled.
         * @param {boolean} [captureContextMenu] - A right click clicks too.
         */
        constructor(imageClass, colorClass, width, height, title, onClick, buttonClass, disabledClass, captureContextMenu) {
            if (!width || width <= 0)
                width = 16;
            if (!height || height <= 0)
                height = 16;
            buttonClass = buttonClass || "IconButton";
            disabledClass = disabledClass || "IconDisabled";
            const t = this;
            t.DisabledStyle = disabledClass;
            t.Width = width;
            t.Height = height;
            const key = width + "x" + height;
            if (!ColorIcon.GenCss.has(key)) {
                ColorIcon.GenCss.set(key, true);
                const ss = ColorIcon.GetStyleSheet();
                const n = "icon-" + key;
                // Design v2 icon buttons: no lift / press movement; idle 80 % opacity, full on hover / focus, 65 % while pressed; the
                // blue focus ring (--sw-focus, never the accent) with the small radius; disabled 35 %.
                [
                    n + "{--w:calc(var(--ThemeIconSize)*" + width + "px);--h:calc(var(--ThemeIconSize)*" + height + "px);width:var(--w);height:var(--h);display:inline-block;mask-size:var(--w) var(--h);mask-position:center;mask-repeat:no-repeat;background-size:contain;background-position:center;background-repeat:no-repeat;}",
                    n + ".IconButton{cursor:pointer;opacity:0.8;border-radius:var(--sw-radius-sm,4px);transition:opacity var(--sw-dur-1,90ms);}",
                    n + ".IconButton:hover{opacity:1;}",
                    n + ".IconButton:focus{outline:none;opacity:1;}",
                    n + ".IconButton:focus-visible{outline:2px solid var(--sw-focus,#1a66d6);outline-offset:2px;}",
                    n + ".IconButton:active{opacity:0.65;}",
                    n + ".IconDisabled{cursor:default;opacity:0.35;}",
                    n + ".IconDisabled:hover{opacity:0.35;}",
                    n + ".IconDisabled:active{opacity:0.35;}",
                ].forEach(r => ss.insertRule(r, ss.cssRules.length));
                t.AddColor("IconColorThemeBackground", "var(--ThemeBackground)");
                t.AddColor("IconColorThemeMain", "var(--ThemeMain)");
                t.AddColor("IconColorThemeAcc1", "var(--ThemeAcc1)");
                t.AddColor("IconColorThemeAcc2", "var(--ThemeAcc2)");
            }
            const e = document.createElement("icon-" + key);
            if (onClick) {
                e.classList.add(buttonClass);
                keyboardClick(e);
                e.setAttribute("role", "button");
            }
            if (imageClass)
                ColorIcon.SetImageClass(e, imageClass);
            if (colorClass)
                e.classList.add(colorClass);
            t.Element = e;
            t.ImageElement = e;
            t.ImageClass = imageClass;
            t.ColorClass = colorClass;
            t.Title = title;
            if (onClick) {
                const clickFn = async ev => {
                    if (badClick(ev))
                        return;
                    if (!t.IsEnabled || t.OldImage)
                        return;
                    await onClick(ev);
                };
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
            t.setTitleAttr(title);
        }

        /** @type {HTMLElement} The icon element. */
        Element = null;
        /** @type {boolean} An enabled clickable icon gets tabindex 0 when re-enabled. */
        TabStop = true;
        /** @type {boolean} Enabled state. */
        IsEnabled = true;
        /** @type {string} The tooltip (not shown while disabled). */
        Title = "";

        /**
         * Set (or remove when falsy) the title attribute (also the accessible name of clickable icons).
         * @param {string} tit - The text.
         */
        setTitleAttr(tit) {
            const e = this.Element;
            if (tit) {
                e.title = tit;
                if (e.getAttribute("role") === "button")
                    e.setAttribute("aria-label", tit);
            }
            else
                e.removeAttribute("title");
        }

        /**
         * Change the tooltip.
         * @param {string} title - The text.
         * @returns {ColorIcon} this.
         */
        SetTitle(title) {
            if (this.Title === title)
                return this;
            this.Title = title;
            if (!this.Element.classList.contains(this.DisabledStyle))
                this.setTitleAttr(title);
            return this;
        }

        /**
         * Enable / disable.
         * @param {boolean} enabled - True to enable.
         * @returns {ColorIcon} this.
         */
        SetEnabled(enabled) {
            if (this.IsEnabled === enabled)
                return this;
            this.IsEnabled = enabled;
            const e = this.Element;
            if (enabled) {
                e.classList.remove(this.DisabledStyle);
                this.setTitleAttr(this.Title);
                if (e.onclick && this.TabStop)
                    e.setAttribute("tabindex", "0");
                e.removeAttribute("aria-disabled");
            }
            else {
                e.classList.add(this.DisabledStyle);
                this.setTitleAttr("");
                e.removeAttribute("tabindex");
                if (e.onclick)
                    e.setAttribute("aria-disabled", "true");
            }
            return this;
        }

        /**
         * Generate the css of a color class for this icon size (doesn't change the color).
         * @param {string} name - The class name.
         * @param {string} col - Css color.
         */
        AddColor(name, col) {
            ColorIcon.AddIconColor(name, this.Width, this.Height, col);
        }

        /**
         * Change the image.
         * @param {string} imageClass - Icon class or url.
         * @returns {ColorIcon} this.
         */
        ChangeImage(imageClass) {
            const c = this.ImageClass;
            if (c === imageClass)
                return this;
            ColorIcon.RemoveImageClass(this.ImageElement, c);
            ColorIcon.SetImageClass(this.ImageElement, imageClass);
            this.ImageClass = imageClass;
            if (this.OldImage)
                this.OldImage = imageClass;
            return this;
        }

        /**
         * Change the color class.
         * @param {string} colorClass - The class.
         * @returns {ColorIcon} this.
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
         * Show a working animation after a while (clicks are ignored until StopWorking).
         * @param {string} [iconName="IconWorking"] - The working icon.
         * @param {number} [changeAfterMs=250] - Delay before it shows.
         */
        StartWorking(iconName, changeAfterMs) {
            const t = this;
            if (t.OldImage)
                return;
            iconName = iconName || "IconWorking";
            const c = t.ImageClass;
            t.OldImage = c;
            t.WorkTimer = setTimeout(() => {
                ColorIcon.RemoveImageClass(t.ImageElement, c);
                ColorIcon.SetImageClass(t.ImageElement, iconName);
                t.WorkImage = iconName;
            }, changeAfterMs || 250);
        }

        /** End StartWorking (restores the image). */
        StopWorking() {
            const oi = this.OldImage;
            if (!oi)
                return;
            clearTimeout(this.WorkTimer);
            if (this.WorkImage)
                this.ImageElement.classList.remove(this.WorkImage);
            this.WorkImage = null;
            this.ImageClass = null;
            this.ChangeImage(oi);
            this.OldImage = null;
            this.WorkTimer = null;
        }
    }
    def("ColorIcon", ColorIcon);

    // ------------------------------------------------------------------ Button (Sysweaver-Button elements, styled by common/theme.css)

    /** Style options of Button (create one and change fields; Button.DefaultStyle is shared). */
    class ButtonStyle {
        /** @type {string} Tag name of the button element. */
        ButtonElement = "Sysweaver-Button";
        /** @type {string|null} Extra class of the button element. */
        ButtonStyle = null;
        /** @type {number} Minimum time (ms) the Pressed class stays after a click. */
        MinPressed = 20;
        /** @type {number} Icon width. */
        IconWidth = 24;
        /** @type {number} Icon height. */
        IconHeight = 24;
        /** @type {string} Icon color class. */
        IconColor = "IconColorThemeBackground";
    }
    def("ButtonStyle", ButtonStyle);

    /**
     * A text button with an optional ColorIcon: element Sysweaver-Button (style.ButtonElement) with a Sysweaver-ButtonText child; classes
     * Pressed and Disabled; the element has an expando Button pointing to the instance.
     */
    class Button {
        /**
         * Create a row container for buttons.
         * @returns {HTMLElement} A SysWeaver-CenterBlock element.
         */
        static CreateRow() {
            return document.createElement("SysWeaver-CenterBlock");
        }

        /** @type {ButtonStyle} The default style. */
        static DefaultStyle = new ButtonStyle();

        /**
         * @param {ButtonStyle} [style] - Style (default Button.DefaultStyle).
         * @param {string} text - The text.
         * @param {string} title - The tooltip (only while enabled).
         * @param {string} [imageClass] - Icon class / url (none if empty).
         * @param {boolean} enabled - Initially enabled.
         * @param {function(Button):Promise<(function():void)|void>} [onclick] - Click handler (UserClick); a returned function is called
         * after the press ends; exceptions are logged.
         */
        constructor(style, text, title, imageClass, enabled, onclick) {
            style = style || Button.DefaultStyle;
            const pc = "Pressed";
            const e = document.createElement(style.ButtonElement);
            if (style.ButtonStyle)
                e.classList.add(style.ButtonStyle);
            e.setAttribute("role", "button");
            const t = this;
            t.Element = e;
            e.Button = t;
            t.Title = title;
            t.UserClick = onclick;
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
                        exitFn = await uc(t);
                    }
                    catch (err) {
                        console.warn("Button click failed: " + (err && err.message ? err.message : err));
                    }
                }
                if (d > 0)
                    await U.Delay(d);
                if (t.IsEnabled)
                    e.onclick = clickFn;
                e.classList.remove(pc);
                haveMouseDown = false;
                if (typeof exitFn === "function")
                    exitFn();
            };
            e.onmousedown = ev => {
                if (ev.button !== 0)
                    return;
                haveMouseDown = true;
                if (t.IsEnabled)
                    e.classList.add(pc);
            };
            e.onmouseout = () => e.classList.remove(pc);
            e.onmouseover = ev => {
                if (!haveMouseDown)
                    return;
                if ((ev.buttons & 1) === 0) {
                    haveMouseDown = false;
                    return;
                }
                if (t.IsEnabled)
                    e.classList.add(pc);
            };
            t.OnClick = clickFn;
            const te = document.createElement("Sysweaver-ButtonText");
            t.Text = te;
            te.textContent = text;
            e.appendChild(te);
            if (imageClass && imageClass.length > 0) {
                const icon = new ColorIcon(imageClass, style.IconColor, style.IconWidth, style.IconHeight);
                icon.Element.title = title || "";
                e.appendChild(icon.Element);
                t.Icon = icon;
            }
            if (enabled)
                t.Enable();
            else {
                e.classList.add("Disabled");
                e.setAttribute("aria-disabled", "true");
            }
        }

        /** @type {HTMLElement} The button element. */
        Element = null;
        /** @type {string} The tooltip. */
        Title = null;
        /** @type {boolean} Enabled state. */
        IsEnabled = false;
        /** @type {function(Event):Promise<void>} The internal click handler. */
        OnClick = null;

        /**
         * Change the icon image (no-op without an icon).
         * @param {string} imageClass - Icon class / url.
         */
        ChangeImage(imageClass) {
            if (this.Icon)
                this.Icon.ChangeImage(imageClass);
        }

        /**
         * Change the icon color class (no-op without an icon).
         * @param {string} imageColorClass - The class.
         */
        ChangeImageColor(imageColorClass) {
            if (this.Icon)
                this.Icon.ChangeColor(imageColorClass);
        }

        /**
         * Change the text.
         * @param {string} newText - The text.
         */
        ChangeText(newText) {
            this.Text.textContent = newText;
        }

        /**
         * Change the tooltip.
         * @param {string} newTitle - The text.
         */
        ChangeTitle(newTitle) {
            if (this.Title === newTitle)
                return;
            this.Title = newTitle;
            this.Element.title = newTitle;
            if (this.Icon)
                this.Icon.SetTitle(newTitle);
        }

        /**
         * Enable or disable.
         * @param {boolean} enable - True to enable.
         */
        SetEnabled(enable) {
            if (enable)
                this.Enable();
            else
                this.Disable();
        }

        /** Enable (tab stop, title, click and Enter / Space handlers). */
        Enable() {
            if (this.IsEnabled)
                return;
            this.IsEnabled = true;
            const e = this.Element;
            e.tabIndex = 0;
            e.title = this.Title || "";
            const fn = this.OnClick;
            e.onclick = fn;
            e.onkeyup = async ev => {
                if (ev.key === "Enter" || ev.key === " ")
                    await fn(ev);
            };
            e.classList.remove("Disabled");
            e.removeAttribute("aria-disabled");
        }

        /** Disable. */
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
            e.setAttribute("aria-disabled", "true");
        }

        /**
         * Show the icon's working animation after a while.
         * @param {string} [iconName] - Working icon (default IconWorking).
         * @param {number} [changeAfterMs] - Delay (default 250).
         */
        StartWorking(iconName, changeAfterMs) {
            if (this.Icon)
                this.Icon.StartWorking(iconName, changeAfterMs);
        }

        /** End StartWorking. */
        StopWorking() {
            if (this.Icon)
                this.Icon.StopWorking();
        }
    }
    def("Button", Button);

    // ------------------------------------------------------------------ ProgressBar (SysWeaver-ProgressBar elements, styled by common/theme.css)

    /**
     * A progress bar: SysWeaver-ProgressBar with the text layers TI (inside the fill) / TO (outside) and the fill Prog.
     */
    class ProgressBar {
        /** Create the elements (Element, Prog, TI, TO). */
        constructor() {
            const e = document.createElement("SysWeaver-ProgressBar");
            const f = document.createElement("SysWeaver-ProgressBarFill");
            const ti = document.createElement("SysWeaver-ProgressBarTextInside");
            const to = document.createElement("SysWeaver-ProgressBarTextOutside");
            e.setAttribute("role", "progressbar");
            e.setAttribute("aria-valuemin", "0");
            e.setAttribute("aria-valuemax", "100");
            e.append(ti, to, f);
            this.Element = e;
            this.Prog = f;
            this.TI = ti;
            this.TO = to;
        }

        /**
         * Set the progress as current / max.
         * @param {number} current - Current value.
         * @param {number} max - Max value.
         * @param {string} [text] - Template ({0} percent, {1} current, {2} max).
         * @param {string} [title] - Tooltip template.
         */
        SetValue(current, max, text, title) {
            const val = max <= 0 ? 0 : (current * 100 / max);
            const pc = F.Number(current, -2);
            const pm = F.Number(max, -2);
            const pval = pc + " / " + pm;
            text = text ? F.StringFormat(text, "{0}", pc, pm) : pval;
            title = title ? F.StringFormat(title, "{0}", pc, pm) : pval + " - {0}";
            this.SetPercentage(val, text, title);
        }

        /**
         * Set the progress in percent (0..100).
         * @param {number} value - Percentage.
         * @param {string} [text] - Template ({0} = "NN.NN%").
         * @param {string} [title] - Tooltip template.
         */
        SetPercentage(value, text, title) {
            value = Math.max(0, Math.min(100, value));
            const pf = F.Number(value, 2) + "%";
            text = text ? F.StringFormat(text, pf) : pf;
            title = title ? F.StringFormat(title, pf) : pf;
            const cp = value + "%";
            const icp = (100 - value) + "%";
            this.Prog.style.left = "-" + icp;
            this.Element.title = title;
            this.Element.setAttribute("aria-valuenow", "" + value);
            const ti = this.TI, to = this.TO;
            ti.style.width = cp;
            to.style.left = cp;
            to.style.width = icp;
            ti.textContent = text;
            to.textContent = text;
            ti.classList.toggle("Active", value >= 50);
            to.classList.toggle("Active", value < 50);
        }
    }
    def("ProgressBar", ProgressBar);

    // ------------------------------------------------------------------ links

    /**
     * Click handler of links: a click with a modifier key copies the url, else the viewer / editor / the url is opened.
     * @param {Event} ev - The click.
     * @param {string} url - The url.
     * @param {string} [target] - The window target.
     * @param {boolean} [useEdit] - Open the registered editor.
     * @param {boolean} [useView] - Open the registered viewer (takes precedence).
     */
    function onLink(ev, url, target, useEdit, useView) {
        if (badClick(ev)) {
            Sw.Clipboard.Copy(url);
            return;
        }
        if (useView)
            P.View(url, target);
        else if (useEdit)
            P.Edit(url, target);
        else
            P.Navigate(url, target);
    }
    def("onLink", onLink);

    /**
     * Set an inline onclick attribute calling onLink (survives html serialization).
     * @param {HTMLElement} element - The element.
     * @param {string} url - The url.
     * @param {string} target - The window target.
     */
    function setOnClickAttr(element, url, target) {
        const q = s => "'" + String(s).replace(/\\/g, "\\\\").replace(/'/g, "\\'") + "'";
        element.setAttribute("onclick", "onLink(event," + q(url) + "," + q(target) + ")");
    }

    /**
     * Create a clickable SysWeaver-Link element (keyboard clickable) that opens an url through onLink.
     * @param {string} url - The url.
     * @param {string} [text] - The text, or the image src when isImage.
     * @param {string} [target="_self"] - The window target.
     * @param {string} [title] - The tooltip.
     * @param {boolean} [isImage] - Show an image (class Image).
     * @param {boolean} [useAttr] - Use an inline onclick attribute (for serialized html).
     * @param {boolean} [useEdit] - Open the registered editor.
     * @param {boolean} [useView] - Open the registered viewer.
     * @returns {HTMLElement} The link.
     */
    function createLink(url, text, target, title, isImage, useAttr, useEdit, useView) {
        const a = document.createElement("SysWeaver-Link");
        a.tabIndex = 0;
        a.setAttribute("role", "link");
        const t = target ?? "_self";
        if (useAttr)
            setOnClickAttr(a, url, t);
        else {
            a.onclick = ev => onLink(ev, url, t, useEdit, useView);
            keyboardClick(a);
        }
        if (text) {
            if (isImage) {
                a.classList.add("Image");
                const i = Sw.CreateImg(true);
                i.src = text;
                if (title)
                    i.alt = title;
                a.appendChild(i);
            }
            else
                a.textContent = text;
        }
        if (title)
            a.title = title;
        return a;
    }

    /**
     * Replace every a element of the document by a createLink link (target default "_self").
     */
    def("fixLinks", () => {
        for (const a of Array.from(document.getElementsByTagName("a"))) {
            const target = a.target || "_self";
            const c = a.firstElementChild;
            if (c) {
                if (c.tagName === "IMG")
                    a.replaceWith(createLink(a.href, c.src, target, a.title || c.title, true));
                else
                    console.warn("Can't fix a tag!");
            }
            else
                a.replaceWith(createLink(a.href, a.textContent, target, a.title));
        }
    });

    // ------------------------------------------------------------------ ValueFormat (Sw.Format + Sw.Cells)

    //  The old cell classes, so old table style sheets (explore/table.css ...) keep styling the cells
    Object.assign(C.Classes, {
        Right: "Right",
        Left: "Left",
        Center: "Center",
        Mono: "Monospaced",
        Null: "Null",
        False: "False",
        Text: "Text",
        Actions: "Toggle",
        Capped: "Capped",
        Kind: "ValueFormat",
    });

    //  The old interaction classes next to the Sw ones (old style sheets: "td.KeyClick:focus", ".ValueFormatDefault.MouseClick:hover" ...)
    const swKeyClick = Sw.KeyClick, swRemoveKeyClick = Sw.RemoveKeyClick, swCopyOnClick = Sw.Clipboard.CopyOnClick;
    /**
     * Sw.KeyClick that also adds the old class KeyClick.
     * @param {HTMLElement} e - The element.
     * @param {function(Event):void} [onClick] - Optional click action.
     * @returns {*} The Sw.KeyClick result.
     */
    Sw.KeyClick = function (e, onClick) {
        e.classList.add("KeyClick");
        return swKeyClick.call(this, e, onClick);
    };
    /**
     * Sw.RemoveKeyClick that also removes the old class KeyClick.
     * @param {HTMLElement} e - The element.
     * @returns {*} The Sw.RemoveKeyClick result.
     */
    Sw.RemoveKeyClick = function (e) {
        e.classList.remove("KeyClick");
        return swRemoveKeyClick.call(this, e);
    };
    /**
     * Sw.Clipboard.CopyOnClick that also adds the old class MouseClick.
     * @param {HTMLElement} e - The element.
     * @param {*} [value] - The value.
     * @param {object} [o] - Options.
     * @returns {string} The hint text.
     */
    Sw.Clipboard.CopyOnClick = function (e, value, o) {
        e.classList.add("MouseClick");
        return swCopyOnClick.call(this, e, value, o);
    };

    /**
     * Restart a flash class on an element (removed after 1050 ms).
     * @param {HTMLElement} el - The element.
     * @param {string} flash - The class.
     */
    function flashIt(el, flash) {
        el.classList.remove(flash);
        void el.offsetWidth;
        el.classList.add(flash);
        clearTimeout(el.SwFlash);
        el.SwFlash = setTimeout(() => el.classList.remove(flash), 1050);
    }

    /**
     * The old value formatting class: formatting helpers (Sw.Format), clipboard (Sw.Clipboard), markdown (Sw.Markdown) and the cell
     * renderers update* (Sw.Cells, signature (el, value, formats, nextValue, flash, type, onRefresh)).
     */
    class ValueFormat {
        /** @type {string} The url of the script (old: of common.js), base of includes. */
        static get Current() {
            return Sw.Url.Script;
        }

        /**
         * Set an element's text keeping leading / trailing spaces (as &nbsp;).
         * @param {HTMLElement} element - The element.
         * @param {*} text - The text.
         */
        static setElementText(element, text) {
            text = "" + text;
            const tl = text.length;
            let s = 0, e = 0;
            while (s < tl && text.charAt(s) === " ")
                ++s;
            while (e < tl && text.charAt(tl - 1 - e) === " ")
                ++e;
            if (s === 0 && e === 0) {
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
            element.innerHTML = nb.repeat(s) + element.innerHTML + nb.repeat(e);
        }

        static AddNonNullLine = F.AddNonNullLine;
        static removeCamelCase = F.RemoveCamelCase;
        static toString = F.Number;
        static toAmount = F.Amount;
        static keyValueSplit = F.KeyValueSplit;
        static countSuffix = F.CountSuffix;
        static FormatMaps = Sw.I18n.FormatMaps;
        static stringFormatArgs = F.StringFormatArgs;
        static stringFormat = F.StringFormat;
        static formatSuffix = F.Suffix;
        static formatTimeSpanMs = F.DurationMs;
        static formatTimeSpan = F.TimeSpan;
        static formatByteSize = F.Bytes;
        static getTimeStampTitle = F.TimeStamp;
        static convertLocalDateToUTCDate = F.LocalToUtc;
        static convertUTCDateToLocalDate = F.UtcToLocal;
        static jsonToHtml = F.JsonToHtml;
        static joinNonEmpty = F.JoinNonEmpty;
        static isDecimal = new Map(Object.entries(F.DecimalTypes));

        /** @type {HTMLTableCellElement} Detached template node. */
        static TextNode = document.createElement("td");
        /** @type {HTMLElement} Detached template node. */
        static LinkNode = document.createElement("SysWeaver-Link");
        /** @type {HTMLImageElement} Detached template node. */
        static ImgNode = document.createElement("img");
        /** @type {HTMLSpanElement} Detached template node. */
        static TagSpanNode = document.createElement("span");
        /** @type {HTMLElement} Detached template node. */
        static TempToggle = document.createElement("SysWeaver-toggle");
        /** @type {HTMLElement} Detached template node. */
        static TempAction = document.createElement("SysWeaver-action");
        /** @type {HTMLElement} Detached template node. */
        static TempSpan = document.createElement("SysWeaver-actionText");
        /** @type {HTMLImageElement} Detached template node. */
        static TempImage = document.createElement("img");

        /**
         * Set the (trimmed) text and the title of an element when changed.
         * @param {HTMLElement} el - The element.
         * @param {*} text - The text.
         * @param {string} [title] - The tooltip (only set when truthy).
         * @param {string} [flash] - A class flashed when changed.
         * @returns {boolean} True if changed.
         */
        static updateText(el, text, title, flash) {
            return C.SetText(el, text, title, flash);
        }

        /**
         * Set the html and the title of an element when changed.
         * @param {HTMLElement} el - The element.
         * @param {*} html - The html.
         * @param {string} [title] - The tooltip (only set when truthy).
         * @param {string} [flash] - A class flashed when changed.
         * @returns {boolean} True if changed.
         */
        static updateHtml(el, html, title, flash) {
            html = html === undefined || html === null ? "" : "" + html;
            if (title && el.title !== "" + title)
                el.title = "" + title;
            if (html === el.innerHTML)
                return false;
            el.innerHTML = html;
            if (flash)
                flashIt(el, flash);
            return true;
        }

        /**
         * Track the renderer of a cell; a changed renderer resets the cell.
         * @param {HTMLElement} el - The cell.
         * @param {string} formatName - The renderer name.
         * @returns {boolean} True if new (the renderer must set up the cell).
         */
        static updateFormat(el, formatName) {
            return C.Reset(el, formatName);
        }

        /**
         * Read text from the clipboard.
         * @returns {Promise<string|null>}
         */
        static readFromClipboard() {
            return Sw.Clipboard.Read();
        }

        /**
         * Copy text to the clipboard (no toast).
         * @param {string} v - The text.
         * @returns {Promise<boolean>} True on success.
         */
        static copyToClipboard(v) {
            return Sw.Clipboard.Copy(v, { Toast: false });
        }

        /**
         * Copy text to the clipboard with an info (or fail) toast.
         * @param {string} v - The text.
         * @param {boolean} [genericText] - Don't quote the text in the toast.
         * @returns {Promise<boolean>} True on success.
         */
        static copyToClipboardInfo(v, genericText) {
            return Sw.Clipboard.Copy(v, { Generic: !!genericText });
        }

        /**
         * Make a click copy a value (or the element's text) with a toast; adds the classes MouseClick (old style sheets) and sw-copyable.
         * The original contract, exactly (S5-A-1): the copy is installed as el.onclick (so assigning el.onclick replaces or removes
         * it); the old click guard runs first (it cancels the default and stops the event, so a click on an inner link copies and
         * doesn't navigate; a click with a modifier copies nothing); a right click shows the browser's menu. addKeyboard makes the
         * element keyboard clickable (keyboardClick: Enter / Space click it, so they copy) and sets el.oncopy to the copy.
         * New code uses Sw.Clipboard.CopyOnClick (inner links keep working, a value menu, Ctrl+C).
         * @param {HTMLElement} el - The element.
         * @param {*} [value] - The value (null = the current text).
         * @param {boolean} [addKeyboard] - Also Enter / Space and the copy event.
         * @param {boolean} [addTitleToElement] - Append the hint to the element's title.
         * @returns {string} The "Click to copy" hint.
         */
        static copyOnClick(el, value, addKeyboard, addTitleToElement) {
            const v = value === null || value === undefined ? null : ("" + value);
            const e = el;
            if (e.SwCopy)
                Sw.Clipboard.RemoveCopy(e);
            e.classList.add("MouseClick", "sw-copyable");
            /** Copy the value (or the current text) with an info toast. @returns {Promise<boolean>} */
            const copy = () => ValueFormat.copyToClipboardInfo(v === null ? e.textContent : v);
            e.onclick = async ev => {
                if (badClick(ev))
                    return;
                await copy();
            };
            if (addKeyboard) {
                keyboardClick(e);
                e.oncopy = copy;
            }
            const title = v !== null
                ? _T('Click to copy "{0}" to the clipboard', v, 'Tool tip to indicate that some known text can be copied to the clipboard by clicking on an element. {0} is the text to copy, ex: 192.168.1.10')
                : _T('Click to copy text to the clipboard', 'Tool tip to indicate that some text can be copied to the clipboard by clicking on an element');
            if (addTitleToElement)
                e.title = e.title ? (e.title + "\n\n" + title) : title;
            return title;
        }

        static updateWebColor = C.WebColor;
        static updateDefault = C.Default;
        static updateType = C.Type;
        static updateNumber = C.Number;
        static getUrlTarget = C.UrlTarget;
        static setOnClickAttr = setOnClickAttr;
        static createLink = createLink;
        static updateUrl = C.Url;
        static updateImg = C.Img;
        static updateDateTimeLive = C.DateTimeLive;
        static updateDateTime = C.DateTime;
        static updateTimeSpan = C.TimeSpan;
        static updateJson = C.Json;
        static updateMarkDown = C.MarkDown;
        static updateLongText = C.LongText;
        static updateBoolean = C.Boolean;
        static updateByteSize = C.ByteSize;
        static updateByteSpeed = C.ByteSpeed;
        static updateTags = C.Tags;
        static updatePerRowFormat = C.PerRowFormat;
        static updateToggle = C.Toggle;
        static updateActions = C.Actions;
        static typeFormatter = C.TypeFormatters;
        static formatFormatter = C.FormatFormatters;
        static update = C.Update;

        /**
         * Render markdown into an element.
         * @param {HTMLElement} el - The element.
         * @param {string} text - Markdown.
         * @returns {Promise<void>}
         */
        static SetMarkDown(el, text) {
            return Sw.Markdown.Set(el, text);
        }

        /**
         * Convert markdown to html.
         * @param {string} text - Markdown.
         * @returns {Promise<string>} The html.
         */
        static MarkDownToHTML(text) {
            return Sw.Markdown.ToHtml(text);
        }

        /**
         * True if a selection has a non collapsed range.
         * @param {Selection} [selection] - Default the window selection.
         * @returns {boolean}
         */
        static isAnythingSelected(selection) {
            const sel = selection ?? window.getSelection();
            for (let i = 0; i < sel.rangeCount; ++i) {
                const r = sel.getRangeAt(i);
                if (r.startOffset !== r.endOffset)
                    return true;
            }
            return false;
        }

        /**
         * True if the user selected text inside an element.
         * @param {HTMLElement} el - The element.
         * @returns {boolean}
         */
        static haveElementSelection(el) {
            return Sw.HasSelectionIn(el);
        }
    }
    def("ValueFormat", ValueFormat);
})();
