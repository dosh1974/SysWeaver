/**
 * @file media_player_tools.js - Shared static helpers for all media players (MediaPlayerTools).
 *
 * Loading: included by MediaPlayer.Init() (media_player.js) via common's includeJs, and by direct
 * `<script defer>` tags on the effect pages (mediaView/backgroundEffects|imageEffects/index.html and the
 * *EffectsLib.html thumbnail pages) together with media_effect_program.js and media_player_effect.js.
 *
 * Globals defined: MediaPlayerTools (static-only class).
 *
 * Responsibilities: default player members (InitBase), dark mode detection and change events (observing
 * window.top), crop/clip computation (Crop margins -> CSS clip-path inset), sizing/alignment of a player
 * element inside a target (UpdateSize), blob caching of urls, autoplay-unmute detection (CanUnMute),
 * "PageLoaded" and "Media*" postMessage protocol with the parent/opener window, and error reporting to a
 * hosting CefSharp / WebView2 application (SetError).
 *
 * Dependencies: MediaPlayerAudio (media_player_audio.js, optional, used by CanUnMute); the "MediaHidden"
 * CSS class must be defined by the hosting page.
 *
 * Player conventions (duck typed, set up by InitBase): Element, Params, Width/Height, Cached, CanPlay,
 * Volume [0,1], Muted, Paused, Looping, HaveAudio, HaveVisual, Duration, UseClip, ClipX/ClipY/ClipWidth/ClipHeight,
 * MessageElement (receives "mediaLoop" / "mediaEnded" CustomEvents), async Play/Pause/Stop/Seek/...
 */

/**
 * Static-only helper class shared by all MediaPlayer* implementations. Never instantiated.
 */
class MediaPlayerTools {
    /**
     * Initialize the common state and default members of a player instance. Called from each player
     * constructor before (or after) it defines its own methods; existing methods on `i` are kept, missing
     * ones get trivial defaults that only update the state flags. Adds the "MediaHidden" class to the element.
     * Initial state: Cached=false, Width=Height=0, Volume=0.25, Muted=true, Paused=true, Looping=true,
     * CanPlay/HaveAudio/HaveVisual=false and Duration=0 unless already set, UseClip=true unless defined.
     * Note: OnEnded is always overwritten (not only when missing).
     * @param {object} i The player instance (MediaPlayerImage, MediaPlayerVideo, ...)
     * @param {HTMLElement} e The element that displays the media (stored as i.Element)
     * @param {object} [params] The MediaPlayerParams* object; if it has a Volume (percent) it scales GetTrueVolume
     * @returns {void}
     */
    static InitBase(i, e, params) {
        i.Cached = false;
        i.Element = e;
        i.Width = 0;
        i.Height = 0;
        if (!i.CanPlay)
            i.CanPlay = false;
        e.classList.add("MediaHidden");
        i.Volume = 0.25;
        i.Muted = true;
        i.Paused = true;
        i.Looping = true;

        if (!i.OnDispose)
            /** Default dispose: awaits i.Stop(). @returns {Promise<void>} */
            i.OnDispose = async () => await i.Stop();
        if (!i.SetVolume)
            /** Default volume setter: stores v ([0, 1]) in i.Volume. @param {number} v @returns {Promise<number>} */
            i.SetVolume = async v => i.Volume = v;
        if (!i.UnMute)
            /** Default unmute: sets i.Muted = false. @returns {Promise<boolean>} */
            i.UnMute = async () => i.Muted = false;
        if (!i.Mute)
            /** Default mute: sets i.Muted = true. @returns {Promise<boolean>} */
            i.Mute = async () => i.Muted = true;
        if (!i.Play)
            /** Default play: sets i.Paused = false. @returns {Promise<boolean>} */
            i.Play = async () => i.Paused = false;
        if (!i.Pause)
            /** Default pause: sets i.Paused = true. @returns {Promise<boolean>} */
            i.Pause = async () => i.Paused = true;
        if (!i.Stop)
            /** Default stop: sets i.Paused = true. @returns {Promise<boolean>} */
            i.Stop = async () => i.Paused = true;
        if (!i.Seek)
            /** Default seek: no-op. @param {number} time Position in seconds @returns {Promise<void>} */
            i.Seek = async time => { }
        if (!i.GetPos)
            /** Default position getter. @returns {number} Always 0 */
            i.GetPos = () => 0;
        if (!i.Loop)
            /** Default loop: sets i.Looping = true. @returns {Promise<boolean>} */
            i.Loop = async () => i.Looping = true;
        if (!i.Once)
            /** Default play-once: sets i.Looping = false. @returns {Promise<boolean>} */
            i.Once = async () => i.Looping = false;
        if (!i.Show)
            /**
             * Default show: restores the inline style saved by Hide (i.OldStyle) and removes "MediaHidden".
             * @returns {void}
             */
            i.Show = () => {
                const oldStyle = i.OldStyle;
                if (oldStyle)
                    i.Element.setAttribute("style", oldStyle);
                i.Element.classList.remove("MediaHidden");
            }
        if (!i.Hide)
            /**
             * Default hide: adds "MediaHidden", saves the element's inline style attribute in i.OldStyle and removes it.
             * @returns {void}
             */
            i.Hide = () => {
                i.Element.classList.add("MediaHidden");
                i.OldStyle = i.Element.getAttribute("style");
                if (i.OldStyle)
                    i.Element.removeAttribute("style");
            }
        if (!i.ResetVolume)
            /**
             * Default volume re-apply: sets i.Volume = -1 then calls i.SetVolume(oldVolume), forcing players
             * that skip unchanged volumes to push the volume again.
             * @returns {Promise<void>}
             */
            i.ResetVolume = async () => {
                const vol = i.Volume;
                i.Volume = -1;
                await i.SetVolume(vol);
            };
        if (!i.HaveAudio)
            i.HaveAudio = false;
        if (!i.HaveVisual)
            i.HaveVisual = false;
        if (!i.Duration)
            i.Duration = 0;
        if (!i.GetTrueVolume) {
            if (params && (typeof params.Volume !== "undefined"))
                /** Effective volume: 0 when muted, else i.Volume * params.Volume (percent) / 100. @returns {number} */
                i.GetTrueVolume = () => i.Muted ? 0 : (i.Volume * params.Volume * 0.01);
            else
                /** Effective volume: 0 when muted, else i.Volume. @returns {number} */
                i.GetTrueVolume = () => i.Muted ? 0 : i.Volume;
        }
        if (typeof i.UseClip === "undefined")
            i.UseClip = true;
        else
            i.UseClip = !!i.UseClip;


        if (!i.ApplyClip)
            /**
             * Default clip: MediaPlayerTools.ApplyClip(i, ap).
             * @param {object} [ap] Params to use instead of i.Params
             * @returns {void}
             */
            i.ApplyClip = ap => MediaPlayerTools.ApplyClip(i, ap);

        if (!i.OnLoop)
            /**
             * Default loop notification: dispatches a "mediaLoop" CustomEvent (detail = player) on i.MessageElement, if set.
             * @returns {void}
             */
            i.OnLoop = () => {
                const me = i.MessageElement;
                if (me)
                    me.dispatchEvent(new CustomEvent("mediaLoop", {
                        detail: i,
                    }));
                //console.warn("Ended!");
            };
        /**
         * End notification (always assigned): dispatches a "mediaEnded" CustomEvent (detail = player) on
         * i.MessageElement, if set.
         * @returns {void}
         */
        i.OnEnded = () => {
            const me = i.MessageElement;
            if (me)
                me.dispatchEvent(new CustomEvent("mediaEnded", {
                    detail: i,
                }));
            //console.warn("Ended!");
        };
        //i.ApplyClip();

    }
	
	
	/**
	 * Check if the top window currently uses a dark color scheme (computed "color-scheme" of
	 * window.top's body equals "dark").
	 * @returns {boolean} True if dark mode is active
	 */
	static IsDarkMode()
	{
		const w = window.top;
		const c = w.getComputedStyle(w.document.body);
		return c.getPropertyValue("color-scheme") === "dark";
	}

	/**
	 * Register a callback for dark mode changes. The first registration creates two MutationObservers
	 * on window.top's documentElement and body (style/class attributes) that re-evaluate IsDarkMode()
	 * and call every registered callback when the value changes (exceptions are swallowed); it also
	 * defines MediaPlayerTools.DarkModeClear to disconnect them.
	 * @param {function(boolean):void} fn Callback receiving the new dark mode state
	 * @returns {boolean} False if fn was already registered, else true
	 */
	static AddDarkModeChangeEvent(fn)
	{
		const m = MediaPlayerTools.DarkModeEventsMap;
		if (m.size > 0)
		{
			if (m.get(fn))
				return false;
			m.set(fn, true);
			return true;
		}
        m.set(fn, true);
        let current = MediaPlayerTools.IsDarkMode();

        /**
         * Mutation callback: recomputes the dark mode state and, if it changed, calls all registered callbacks.
         * @returns {void}
         */
        function onChange() {
            const n = MediaPlayerTools.IsDarkMode();
            if (n === current)
                return;
            current = n;
            m.forEach((v, k) => {
                try {
                    k(n);
                }
                catch {
                }
            });
        }

        const op = {
            attributes: true,
            attributeFilter: ['style', 'class'],
        };


        const w = window.top;

        const od = new MutationObserver(onChange);
        const ob = new MutationObserver(onChange);
        od.observe(w.document.documentElement, op);
        ob.observe(w.document.body, op);

        /**
         * Disconnects both dark mode observers and deletes itself (static, only defined while observing).
         * @returns {void}
         */
        MediaPlayerTools.DarkModeClear = () => {
            ob.disconnect();
            od.disconnect();
            delete MediaPlayerTools.DarkModeClear;
        };

		return true;
	}
	
	/**
	 * Unregister a dark mode callback; when the last one is removed the observers are disconnected
	 * (DarkModeClear).
	 * @param {function(boolean):void} fn The callback passed to AddDarkModeChangeEvent
	 * @returns {boolean} False if fn was not registered, else true
	 */
	static RemoveDarkModeChangeEvent(fn)
	{
		const m = MediaPlayerTools.DarkModeEventsMap;
		if (!m.get(fn))
			return false;
        m.delete(fn);
        if (m.size > 0)
            return true;
        const c = MediaPlayerTools.DarkModeClear;
        if (c)
            c();
		return true;
	}
	
	
	/** @type {Map<function(boolean):void, boolean>} Registered dark mode change callbacks (key = callback). */
	static DarkModeEventsMap = new Map();

    /**
     * Compute the crop rectangle of a player from params.Crop (MediaCrop: MarginTop/Right/Bottom/Left in
     * pixels, or MarginTopP/... in percent when MarginAsPercentage). Always resets i.ClipX/ClipY to 0 and
     * i.ClipWidth/ClipHeight to i.Width/i.Height first; on success sets ClipX, ClipY, ClipWidth, ClipHeight
     * (clamped >= 0), ClipR and ClipB.
     * @param {object} i The player instance (uses Width, Height, Params)
     * @param {object} [params=i.Params] Params holding Crop
     * @returns {boolean} True if a non-empty crop applies, false if no params/crop or all margins <= 0
     */
    static ComputeClip(i, params) {
        const ow = i.Width;
        const oh = i.Height;
        i.ClipX = 0;
        i.ClipY = 0;
        i.ClipWidth = ow;
        i.ClipHeight = oh;
        params = params ?? i.Params;
        if (!params)
            return false;
        const crop = params.Crop;
        if (!crop)
            return false;
        let t = crop.MarginTop;
        let r = crop.MarginRight;
        let b = crop.MarginBottom;
        let l = crop.MarginLeft;
        if (crop.MarginAsPercentage) {
            t = crop.MarginTopP;
            r = crop.MarginRightP;
            b = crop.MarginBottomP;
            l = crop.MarginLeftP;
            t = Math.round(t * oh / 100);
            r = Math.round(r * ow / 100);
            b = Math.round(b * oh / 100);
            l = Math.round(l * ow / 100);
        }
        if ((t <= 0) && (r <= 0) && (b <= 0) && (l <= 0))
            return false;
        let w = ow - l - r;
        if (w < 0)
            w = 0;
        let h = oh - t - b;
        if (h < 0)
            h = 0;
        i.ClipX = l;
        i.ClipY = t;
        i.ClipWidth = w;
        i.ClipHeight = h;
        i.ClipR = r;
        i.ClipB = b;
        return true;



    }

    /**
     * Apply the crop of a player to its element as a CSS clip-path "inset(t r b l)" (via ComputeClip).
     * Removes an existing clip-path when no crop applies. Calls i.OnClipChanged() (if defined) only when
     * the clip-path actually changed. The block comment below is the older implementation, kept as is.
     * @param {object} i The player instance
     * @param {object} [params=i.Params] Params holding Crop
     * @returns {void}
     */
    static ApplyClip(i, params) {

        const e = i.Element;
        if (!MediaPlayerTools.ComputeClip(i, params)) {
            if (e.style.clipPath) {
                e.style.clipPath = null;
                if (i.OnClipChanged)
                    i.OnClipChanged();
            }
            return;
        }
        const newP = "inset(" + i.ClipY + "px " + i.ClipR + "px " + i.ClipB + "px " + i.ClipX + "px)";
        if (newP === e.style.clipPath)
            return;
        e.style.clipPath = newP;
        if (i.OnClipChanged)
            i.OnClipChanged();
/*
        function NoClip() {
            e.style.clipPath = null;
            if (i.OnClipChanged)
                i.OnClipChanged();
        }
        const ow = i.Width;
        const oh = i.Height;
        i.ClipX = 0;
        i.ClipY = 0;
        i.ClipWidth = ow;
        i.ClipHeight = oh;
        params = params ?? i.Params;
        if (!params)
            return;
        const crop = params.Crop;
        if (typeof crop === "undefined")
            return;
        const e = i.Element;
        function NoClip() {
            e.style.clipPath = null;
            if (i.OnClipChanged)
                i.OnClipChanged();
        }
        if (!crop) {
            NoClip();
            return;
        }
        if (typeof params.Stream !== "undefined")
            if (params.Stream === 2) {
                NoClip();
                return;
            }

        let t = crop.MarginTop;
        let r = crop.MarginRight;
        let b = crop.MarginBottom;
        let l = crop.MarginLeft;
        if (crop.MarginAsPercentage) {
            t = crop.MarginTopP;
            r = crop.MarginRightP;
            b = crop.MarginBottomP;
            l = crop.MarginLeftP;
            t = Math.round(t * oh / 100);
            r = Math.round(r * ow / 100);
            b = Math.round(b * oh / 100);
            l = Math.round(l * ow / 100);
        }
        if ((t <= 0) && (r <= 0) && (b <= 0) && (l <= 0)) {
            NoClip();
            return;
        }
        let w = ow - l - r;
        if (w < 0)
            w = 0;
        let h = oh - t - b;
        if (h < 0)
            h = 0;
        const newP = "inset(" + t + "px " + r + "px " + b + "px " + l + "px)";
        if (newP === e.style.clipPath)
            return;
        e.style.clipPath = newP;
        i.ClipX = l;
        i.ClipY = t;
        i.ClipWidth = w;
        i.ClipHeight = h;
        if (i.OnClipChanged)
            i.OnClipChanged();
*/
    }

    /**
     * Download a url and wrap the data in an object URL (never revoked here).
     * @param {string} url The url to fetch
     * @returns {Promise<string|null>} The blob: object URL, or null on any error
     */
    static async CreateBlob(url) {
        try {
            const response = await fetch(url);
            const blob = await response.blob();
            return URL.createObjectURL(blob)
        }
        catch (e) {
            return null;
        }
    }

    /**
     * Mark a player as cached (i.Cached = true) and, unless keepHidden, remove "MediaHidden" from its element.
     * @param {object} i The player instance
     * @param {boolean} keepHidden If true the element stays hidden
     * @returns {Promise<boolean>} Always true
     */
    static async OnCacheComplete(i, keepHidden) {
        if (!keepHidden)
            i.Element.classList.remove("MediaHidden");
        i.Cached = true;
        return true;
    }

    /**
     * Detect if the browser allows unmuted audio playback (autoplay policy). Tests once (cached in the
     * static fields HaveTested / Result): loads a tiny embedded silent MP3 into a new `<audio>` with
     * MediaPlayerAudio.LoadAudio, then tries to play it unmuted at volume 0.01. On success dispatches an
     * "AudioEnabled" CustomEvent on document.body. On failure installs one-shot keydown/mousedown/click
     * listeners on document.body that set Result = true and dispatch "AudioEnabled" on the first user gesture.
     * @returns {Promise<boolean>} True if unmuted playback works now; false if blocked, the audio failed to
     * load, or MediaPlayerAudio is not loaded
     */
    static async CanUnMute() {
        if (MediaPlayerTools.HaveTested)
            return MediaPlayerTools.Result;
        if (typeof MediaPlayerAudio !== "function")
            return false;
        const e = document.createElement("audio");
        let res = false;
        if (await MediaPlayerAudio.LoadAudio(e, "data:audio/mpeg;base64,/+MYxAAAAANIAUAAAASEEB/jwOFM/0MM/90b/+RhST//w4NFwOjf///PZu////9lns5GFDv//l9GlUIEEIAAAgIg8Ir/JGq3/+MYxDsLIj5QMYcoAP0dv9HIjUcH//yYSg+CIbkGP//8w0bLVjUP///3Z0x5QCAv/yLjwtGKTEFNRTMuOTeqqqqqqqqqqqqq/+MYxEkNmdJkUYc4AKqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq")) {
            try {
                e.muted = false;
                e.volume = 0.01;
                await e.play();
                res = true;
                await e.pause();
                document.body.dispatchEvent(new CustomEvent("AudioEnabled"));
            }
            catch
            {
                /**
                 * First user gesture handler: removes the three listeners, sets Result = true and dispatches "AudioEnabled".
                 * @returns {Promise<void>}
                 */
                const l = async () => {
                    document.body.removeEventListener("keydown", l);
                    document.body.removeEventListener("mousedown", l);
                    document.body.removeEventListener("click", l);
                    MediaPlayerTools.Result = true;
                    document.body.dispatchEvent(new CustomEvent("AudioEnabled"));
                };
                document.body.addEventListener("keydown", l);
                document.body.addEventListener("mousedown", l);
                document.body.addEventListener("click", l);
            }
        }
        MediaPlayerTools.HaveTested = true;
        MediaPlayerTools.Result = res;
        return res;
    }

    /**
     * Check if an element is inside document.body (walks parentElement).
     * @param {HTMLElement} el The element
     * @returns {boolean} True if attached to the document body
     */
    static IsAttached(el) {

        while (el) {
            if (el === document.body)
                return true;
            el = el.parentElement;
        }
        return false;
    }

    /**
     * Notify the hosting window that the page is loaded: posts `{ Type: "PageLoaded" }` (target origin "*")
     * to window.parent, or to window.opener when not framed. Errors are ignored.
     * @returns {void}
     */
    static PostPageLoaded() {
        const p = window.parent;
        const wt = p === window ? window.opener : p;
        if (wt) {
            try {
                wt.postMessage({ Type: "PageLoaded" }, "*");
            }
            catch {
            }
        }
    }

    /**
     * Ensure an element is in the DOM (some media elements only load/play when attached). If it is not,
     * it is appended to document.body invisibly (position fixed, opacity 0, left -10000px).
     * @param {HTMLElement} e The element
     * @returns {function():void} Undo function: removes the element again and restores position/opacity/left
     * (a no-op if the element was already attached)
     */
    static MustBeAttached(e) {
        if (MediaPlayerTools.IsAttached(e))
            return () => { };
        const es = e.style;
        const op = es.position;
        const oo = es.opacity;
        const ol = es.left;
        es.position = "fixed";
        es.opacity = "0";
        es.left = "-10000px";
        document.body.appendChild(e);
        return () => {
            e.remove();
            es.left = ol;
            es.opacity = oo;
            es.position = op;
        };
    }

    /**
     * Wait a number of milliseconds.
     * @param {number} msToWait Milliseconds
     * @returns {Promise<void>} Resolves after the timeout
     */
    static delay(msToWait) {
        return new Promise(resolve => setTimeout(resolve, msToWait));
    }

    /**
     * Wait for the first of two events on an element (e.g. "canplaythrough" / "error"), optionally with a timeout.
     * Listeners are attached before fn is called, so fn can trigger the event synchronously.
     * @param {EventTarget} element The element to listen on
     * @param {string} eventName1 First event name
     * @param {string} eventName2 Second event name
     * @param {function(EventTarget):void} [fn] Called with the element after the listeners are attached (e.g. to set src)
     * @param {number} [timeout] Timeout in ms; when > 0 the promise resolves with null after it
     * @returns {Promise<Event|null>} The event that fired, or null on timeout
     */
    static waitEvent2(element, eventName1, eventName2, fn, timeout) {
        return new Promise(resolve => {
            let timeoutT = null;
            /**
             * Finish: clears the timer, removes both listeners and resolves with ev.
             * @param {Event|null} ev The event or null (timeout)
             * @returns {void}
             */
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


    /**
     * Let a parent/opener window control a player via window.postMessage. Listens for "message" events
     * whose data.Type is one of: MediaPlay, MediaPause, MediaStop, MediaOnce, MediaLoop, MediaMute,
     * MediaUnMute, MediaSetVolume (data.Volume ?? data.Value ?? 0), MediaSeek (data.Time ?? data.Position ??
     * data.Value ?? 0), and, when the player implements them, MediaDebugLoseContext, MediaCustomX/Y/Z
     * (the whole data object is passed to SetCustomX/Y/Z). The message origin is not checked.
     * @param {object} obj The player instance
     * @returns {function():void} Function that removes the message listener
     */
    static SetupMediaMessages(obj) {
        const m = new Map();
        m.set("MediaPlay", v => obj.Play());
        m.set("MediaPause", v => obj.Pause());
        m.set("MediaStop", v => obj.Stop());
        m.set("MediaOnce", v => obj.Once());
        m.set("MediaLoop", v => obj.Loop());
        m.set("MediaMute", v => obj.Mute());
        m.set("MediaUnMute", v => obj.UnMute());
        m.set("MediaSetVolume", v => obj.SetVolume(v.Volume ?? v.Value ?? 0.0));
        m.set("MediaSeek", v => obj.Seek(v.Time ?? v.Position ?? v.Value ?? 0.0));
		if (typeof(obj.DebugLoseContext) === "function")
			m.set("MediaDebugLoseContext", v => obj.DebugLoseContext());
        if (typeof obj.SetCustomX === "function")
            m.set("MediaCustomX", v => obj.SetCustomX(v));
        if (typeof obj.SetCustomY === "function")
            m.set("MediaCustomY", v => obj.SetCustomY(v));
        if (typeof obj.SetCustomZ === "function")
            m.set("MediaCustomZ", v => obj.SetCustomZ(v));


        /**
         * Window "message" handler: dispatches ev.data to the handler registered for ev.data.Type, if any.
         * @param {MessageEvent} ev The message event
         * @returns {void}
         */
        function h(ev) {
            const data = ev.data;
            if (!data)
                return;
            const t = data.Type;
            if (!t)
                return;
            const fn = m.get(t);
            if (!fn)
                return;
            //console.warn(t + " @ " + obj.Url);
            fn(data);
        }

        window.addEventListener("message", h);
        return () => window.removeEventListener("message", h);
    }

    /**
     * Size/position a player element inside a target element. With player.Params.AdaptiveSize the element
     * simply gets the target's pixel size (no transform). Otherwise the element keeps the media size
     * (Width x Height) and is scaled with a CSS transform (origin left top) so the (clipped, if UseClip)
     * area fits (contain) or fills (cover) the target, then translated by the free space times alignX/alignY.
     * Note: the translation uses (clipSize + clipOffset * 2), i.e. it assumes symmetric crop margins.
     * @param {HTMLElement} target The container whose bounding rect is used
     * @param {object} player The player instance (Element, Params, Width, Height, UseClip, Clip*)
     * @param {boolean} fill True to cover the target (crop overflow), false to fit inside it
     * @param {number} [alignX=0.5] Horizontal alignment of the free space [0 = left, 1 = right]
     * @param {number} [alignY=0.5] Vertical alignment of the free space [0 = top, 1 = bottom]
     * @returns {Array<number>|undefined} [visibleWidth, visibleHeight] of the media (clipped size), [0, 0] if
     * it has no size, undefined if no player
     */
    static UpdateSize(target, player, fill, alignX, alignY) {

        if (!player)
            return;
        if (typeof alignX === "undefined")
            alignX = 0.5;
        if (typeof alignY === "undefined")
            alignY = 0.5;
        const fit = !fill;

        const rect = target.getBoundingClientRect();
        const props = player.Params;
        const ow = player.Width;
        const oh = player.Height;
        const useClip = player.UseClip;
        const adapt = props.AdaptiveSize;

        const crw = (useClip ? player.ClipWidth : ow) ?? ow;
        const crh = (useClip ? player.ClipHeight : oh) ?? oh;
        if ((crw <= 0) || (crh <= 0))
            return [0, 0];

        const crx = (useClip ? player.ClipX : 0) ?? 0;
        const cry = (useClip ? player.ClipY : 0) ?? 0;

        const cw = rect.width;
        const ch = rect.height;

        const e = player.Element;
        const playerE = e.style;
        if (adapt) {
            playerE.transform = null;
            playerE.transformOrigin = null;
            playerE.width = cw + "px";
            playerE.height = ch + "px";
        } else {
            playerE.width = ow + "px";
            playerE.height = oh + "px";

            const scaleX = cw / crw;
            const scaleY = ch / crh;
            const scale = fit ? (scaleX < scaleY ? scaleX : scaleY) : (scaleX > scaleY ? scaleX : scaleY);

            const dx = (cw - (scale * (crw + crx * 2))) * alignX;
            const dy = (ch - (scale * (crh + cry * 2))) * alignY;
            const tr = "scale(" + scale + ") translate(" + (dx / scale) + "px, " + (dy / scale) + "px)";
            playerE.transformOrigin = "left top";
            playerE.transform = tr;
        }
        return [crw, crh];

    }


    /**
     * Report an error text to a hosting desktop application: binds CefSharp objects if window.CefSharp
     * exists, then calls SetText(text) on window.ErrorText (CefSharp) or chrome.webview.hostObjects.ErrorText
     * (WebView2). Errors are ignored; does nothing in a normal browser.
     * @param {string} text The error text
     * @returns {Promise<void>}
     */
    static async SetError(text) {
        try {
            if (window["CefSharp"])
                await CefSharp.BindObjectAsync();
            const cs = window["ErrorText"] ?? chrome?.webview?.hostObjects?.ErrorText;
            if (cs)
                await cs.SetText(text);
        }
        catch (e) {
        }
    }


}

