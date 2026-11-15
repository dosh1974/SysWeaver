/**
 * @file media_player_tools.js - Shared static helpers for all media players (MediaPlayerTools).
 *
 * Loading: included by MediaPlayer.Init() (media_player.js), and by direct `<script defer>` tags on the effect
 * pages (hosted by mediaView/effectPage.js) and the *EffectsLib.html galleries together with media_effect_program.js
 * and media_player_effect.js. Needs no SysWeaver core (uses it when present: Sw.Page.SignalLoaded, Sw.Util.HashString).
 *
 * Globals defined: MediaPlayerTools (static-only class), also published as `Sw.Media.Tools`; and the two base classes the
 * players derive from: MediaPlayerPicture (the <img> player with the timer based timeline: MediaPlayerImage,
 * MediaPlayerText) and MediaPlayerTexture (the GL texture provider of an effect: MediaPlayerImageTexture,
 * MediaPlayerTextTexture).
 *
 * Responsibilities: default player members (InitBase), dark mode detection and change events (observing
 * window.top), crop/clip computation (Crop margins -> CSS clip-path inset), sizing/alignment of a player
 * element inside a target (UpdateSize), blob caching of urls, safe web font loading (LoadFont), autoplay-unmute detection (CanUnMute),
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
		const w = MediaPlayerTools.ThemeWindow();
		const c = w.getComputedStyle(w.document.body ?? w.document.documentElement);
		return c.getPropertyValue("color-scheme").trim() === "dark";
	}

	/**
	 * The window whose theme the players follow: window.top when it is readable (same origin), else this window
	 * (a page embedded by another origin used to throw a SecurityError in the effect constructor).
	 * @returns {Window} The window.
	 */
	static ThemeWindow()
	{
		try
		{
			const w = window.top;
			if (w && w.document && w.document.documentElement)
				return w;
		}
		catch
		{
		}
		return window;
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


        const w = MediaPlayerTools.ThemeWindow();

        const od = new MutationObserver(onChange);
        const ob = new MutationObserver(onChange);
        od.observe(w.document.documentElement, op);
        if (w.document.body)
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
     * the clip-path actually changed.
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
     * Load a web font and add it to document.fonts (the FontUrl / FontUrlName of a text player or a text effect).
     * Security: the url often comes from a page url, so it is never pasted into CSS as is (a quote in it would close the
     * CSS string and add a second `url()` of any site). It is parsed relative to the document, must be a http(s) url (or
     * a blob: url of this origin), and only its normalized href is used, as an escaped CSS string (JSON.stringify).
     * Same-site rules are up to the page (mediaView: MediaLinkProblem / ParseTools.UrlProblem).
     * @param {string} name The font family name (FontUrlName).
     * @param {string} url The font url (FontUrl).
     * @returns {Promise<FontFace>} The loaded font face.
     * @throws {Error} When the url is not a web address or the font can't be loaded; the error has `FontLoad = true` (a
     * page can show "The font could not be loaded" for it).
     */
    static async LoadFont(name, url) {
        let u = null;
        try {
            u = new URL(String(url ?? ""), document.baseURI);
        }
        catch {
            u = null;
        }
        let ok = !!u && (u.protocol === "http:" || u.protocol === "https:");
        if (!ok && u && u.protocol === "blob:") {
            try {
                ok = new URL(u.pathname).origin === window.location.origin;
            }
            catch {
                ok = false;
            }
        }
        /**
         * @param {string} why The reason (logged, not shown).
         * @returns {Error} The font error.
         */
        const fail = why => {
            const err = new Error("The font could not be loaded: " + why);
            err.FontLoad = true;
            return err;
        };
        if (!ok)
            throw fail("the font address is not a web address");
        const ff = new FontFace(String(name ?? ""), "url(" + JSON.stringify(u.href) + ")");
        try {
            await ff.load();
        }
        catch (e) {
            throw fail(u.href + " (" + (e && e.message ? e.message : e) + ")");
        }
        document.fonts.add(ff);
        return ff;
    }

    /**
     * Load the web font of a text style (FontUrl / FontUrlName, with LoadFont) and fall back to a default font when it
     * fails: the failure is logged, the css font of the style gets the families "Verdana, sans-serif" appended (else the
     * canvas would use the browser's default serif font for the missing family) and rendering goes on. The text players
     * keep the returned error (`FontError`), and the window gets the event "MediaFontFallback" (detail: the error; a text
     * texture inside an effect player is not reachable from the page), so a page can show a notice (media.js FontNotice).
     * @param {object} style The text style (TextStyle / MediaPlayerParamsText): FontUrl, FontUrlName and Font are used;
     * Font is changed on a failure.
     * @returns {Promise<Error|null>} null when the font loaded (or the style has no FontUrl), else the error (FontLoad = true).
     */
    static async LoadFontOrFallback(style) {
        if (!style || !style.FontUrl)
            return null;
        try {
            await MediaPlayerTools.LoadFont(style.FontUrlName, style.FontUrl);
            return null;
        }
        catch (e) {
            console.warn("Media: " + (e && e.message ? e.message : e) + ", using a default font");
            const f = String(style.Font ?? "").trim();
            if (f && !/(^|[\s,])(sans-serif|serif|monospace)\s*$/i.test(f))
                style.Font = f + ", Verdana, sans-serif";
            const err = e instanceof Error ? e : new Error("" + e);
            err.FontLoad = true;
            try {
                window.dispatchEvent(new CustomEvent("MediaFontFallback", { detail: err }));
            }
            catch {
            }
            return err;
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
     * MediaPlayerElement.Load, then tries to play it unmuted at volume 0.01. On success dispatches an
     * "AudioEnabled" CustomEvent on document.body. On failure installs one-shot keydown/mousedown/click
     * listeners on document.body that set Result = true and dispatch "AudioEnabled" on the first user gesture.
     * The probe gets at most 3 s to load and 3 s to start (no answer in time counts as blocked).
     * @returns {Promise<boolean>} True if unmuted playback works now; false if blocked, the audio failed to
     * load or the browser gave no answer in time
     */
    static async CanUnMute() {
        if (MediaPlayerTools.HaveTested)
            return MediaPlayerTools.Result;
        const e = document.createElement("audio");
        let res = false;
        //  The probe gets at most 3 s to load and 3 s to start: a browser can keep it waiting (a background tab, a frame where it
        //  defers media), and everything that waits for the players (MediaPlayer.Ready) waited with it, ex: a slide show stayed
        //  empty until the page was loaded again. No answer in time = the sound needs a click, as when the browser refuses.
        const timeUp = () => MediaPlayerTools.delay(3000).then(() => "timeout");
        const loaded = await Promise.race([MediaPlayerElement.Load(e, "data:audio/mpeg;base64,/+MYxAAAAANIAUAAAASEEB/jwOFM/0MM/90b/+RhST//w4NFwOjf///PZu////9lns5GFDv//l9GlUIEEIAAAgIg8Ir/JGq3/+MYxDsLIj5QMYcoAP0dv9HIjUcH//yYSg+CIbkGP//8w0bLVjUP///3Z0x5QCAv/yLjwtGKTEFNRTMuOTeqqqqqqqqqqqqq/+MYxEkNmdJkUYc4AKqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"), timeUp()]);
        if (loaded === "timeout")
            e.SwCancelLoad = true;
        //  (no answer: the click listeners below, like a refused play)
        if (loaded) {
            try {
                if (loaded === "timeout")
                    throw new Error("The sound probe did not load in time");
                e.muted = false;
                e.volume = 0.01;
                const played = e.play();
                if (await Promise.race([played.then(() => "played"), timeUp()]) !== "played") {
                    //  (it may still start later: then it stops at once)
                    played.then(() => e.pause(), () => { });
                    throw new Error("The sound probe did not start in time");
                }
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
     * Notify the hosting windows that the page is loaded. With the SysWeaver core present this is
     * `Sw.Page.SignalLoaded()`; otherwise the same protocol by hand: `{ Type: "PageLoaded" }` to window.parent, or to
     * window.opener when not framed, plus `{ Type: "LoaderRemoved" }` to window.top when framed (the app shell reveals its
     * content frame on it instead of waiting for its 1 s fallback). Security: posted to this page's origin only ("/"), a
     * foreign window framing the page receives nothing. Errors are ignored.
     * @returns {void}
     */
    static PostPageLoaded() {
        const sw = window.Sw;
        if (sw && sw.Page && sw.Page.SignalLoaded) {
            sw.Page.SignalLoaded();
            return;
        }
        const p = window.parent;
        const framed = p !== window;
        const wt = framed ? p : window.opener;
        try {
            if (wt)
                wt.postMessage({ Type: "PageLoaded" }, "/");
            if (framed)
                window.top.postMessage({ Type: "LoaderRemoved" }, "/");
        }
        catch {
        }
    }

    /**
     * Hash a string like the core (`Sw.Util.HashString`: base64 of the SHA-256 of the UTF-8 bytes), so cache keys stay
     * the same as with the old common.js hash helper. Without the core: crypto.subtle, or (insecure context, no
     * crypto.subtle) a 53 bit text hash.
     * @param {string} s The text.
     * @returns {Promise<string>} The hash.
     */
    static async HashString(s) {
        const sw = window.Sw;
        if (sw && sw.Util && sw.Util.HashString)
            return await sw.Util.HashString(s);
        try {
            const d = new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)));
            let b = "";
            for (const x of d)
                b += String.fromCharCode(x);
            return btoa(b);
        }
        catch {
            let h1 = 0xdeadbeef, h2 = 0x41c6ce57;
            for (let i = 0; i < s.length; ++i) {
                const c = s.charCodeAt(i);
                h1 = Math.imul(h1 ^ c, 2654435761);
                h2 = Math.imul(h2 ^ c, 1597334677);
            }
            return "" + ((h2 >>> 0) * 4294967296 + (h1 >>> 0));
        }
    }

    /**
     * Convert a Blob to a data: url.
     * @param {Blob} blob The data.
     * @returns {Promise<string>} The data url.
     */
    static BlobToDataUrl(blob) {
        return new Promise((resolve, reject) => {
            const r = new FileReader();
            r.onload = () => resolve(r.result);
            r.onerror = () => reject(r.error);
            r.readAsDataURL(blob);
        });
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
     * (the whole data object is passed to SetCustomX/Y/Z). Security: only messages from this origin, sent by the parent,
     * the opener, the top window or this window, are accepted (a foreign page framing or opening the player can't control it).
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
            if (ev.origin !== window.location.origin)
                return;
            const s = ev.source;
            if (!s || (s !== window.parent && s !== window.opener && s !== window.top && s !== window))
                return;
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

/**
 * Base class of the players that show a picture in an <img> and time it with a virtual timeline (MediaPlayerImage,
 * MediaPlayerText): the element, the shared Cache tail (ShowImage) and the timer based Play / Pause / Stop / GetPos /
 * Once / Seek. Instance fields: Params, Url / Source, CurrentPosition (paused / last position in seconds), StartedAt
 * (performance.now() based start of the current run), TimeFn (active timeout id or null), AspectRatio (width / height,
 * adaptive only). Other state comes from MediaPlayerTools.InitBase. A derived class implements Cache.
 */
class MediaPlayerPicture {

    /**
     * Creates the player and its <img> element (not loaded until Cache). Sets HaveVisual, CanPlay, calls
     * MediaPlayerTools.InitBase, Url = Source = url, CurrentPosition = 0, Duration = Params.Duration.
     * @param {string} url - What is shown (the image url, or the text of a text player).
     * @param {object} params - The params, already merged over the defaults of the derived class (AdaptiveSize: the
     * element gets object-fit: contain; Duration).
     */
    constructor(url, params) {
        const t = this;
        t.Params = params;
        const e = document.createElement("img");
        if (params.AdaptiveSize)
            e.style.objectFit = "contain";
        t.HaveVisual = true;
        t.CanPlay = true;
        MediaPlayerTools.InitBase(t, e, params);
        t.Url = url;
        t.Source = url;
        t.CurrentPosition = 0;
        t.Duration = params.Duration;
    }

    /**
     * The end of Cache: sets src and waits for "load" / "error" (MediaPlayerTools.waitEvent2), reads naturalWidth / Height
     * into Width / Height (they keep their previous values until the image has loaded), sets AspectRatio when AdaptiveSize,
     * calls ApplyClip() and MediaPlayerTools.OnCacheComplete.
     * @param {string} src - The image url (a blob url for a text).
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @param {function(HTMLImageElement):void} [onLoad] - Called when the image has loaded, before the size is read.
     * @returns {Promise<boolean>} True when loaded, false on a load error.
     */
    async ShowImage(src, keepHidden, onLoad) {
        const t = this;
        const e = t.Element;
        const res = await MediaPlayerTools.waitEvent2(e, "load", "error", () => {
            e.src = src;
        });
        if (res.type == "error")
            return false;
        if (onLoad)
            onLoad(e);
        t.Width = e.naturalWidth;
        t.Height = e.naturalHeight;
        if (t.Params.AdaptiveSize) {
            if ((t.Width > 0) && (t.Height > 0)) {
                t.AspectRatio = t.Width / t.Height;
            }
        }
        t.ApplyClip();
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    /**
     * Starts the virtual timeline from CurrentPosition. No-op when already playing.
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        /**
         * Arms (or re-arms) the end-of-duration timer. Duration <= 0: position 0 and, when not looping, pauses
         * immediately and calls OnEnded. Otherwise restarts from 0 if the remaining time is <= 0, sets StartedAt
         * and a setTimeout (TimeFn) for the remaining time; on expiry it either loops (position 0, re-arm, OnLoop)
         * or pauses with CurrentPosition = Duration and calls OnEnded.
         * @returns {Promise<void>}
         */
        const setTimer = async () => {
            const dur = t.Params.Duration;
            if (dur <= 0) {
                t.CurrentPosition = 0;
                if (!t.Looping) {
                    t.Paused = true;
                    await t.OnEnded();
                }
                return;
            }
            let timeLeft = (dur - t.CurrentPosition) * 1000;
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            if (timeLeft <= 0) {
                t.CurrentPosition = 0;
                timeLeft = dur * 1000;
            }
            t.StartedAt = performance.now() - t.CurrentPosition * 1000;
            t.TimeFn = setTimeout(async () => {
                t.TimeFn = null;
                if (t.Looping) {
                    t.CurrentPosition = 0;
                    await setTimer();
                    await t.OnLoop();
                } else {
                    t.Paused = true;
                    t.CurrentPosition = dur;
                    await t.OnEnded();
                }
            }, timeLeft);
        };
        await setTimer();
    }

    /**
     * Pauses the timeline (synchronous, unlike the InitBase default): clears the timer, stores GetPos() in
     * CurrentPosition and sets Paused. No-op when already paused.
     * @returns {void}
     */
    Pause() {
        const t = this;
        if (t.Paused)
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.CurrentPosition = t.GetPos();
        t.TimeFn = null;
        t.Paused = true;
    }

    /**
     * Stops the timeline (synchronous): clears the timer, pauses and resets CurrentPosition to 0.
     * No-op when already paused at position 0.
     * @returns {void}
     */
    Stop() {
        const t = this;
        if (t.Paused && (t.CurrentPosition <= 0))
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.TimeFn = null;
        t.Paused = true;
        t.CurrentPosition = 0;
    }

    /**
     * Gets the timeline position.
     * @returns {number} CurrentPosition when paused; 0 when Duration <= 0; otherwise the seconds since StartedAt,
     * capped at Duration.
     */
    GetPos() {
        const t = this;
        if (t.Paused)
            return t.CurrentPosition;
        const dur = t.Params.Duration;
        if (dur <= 0)
            return 0;
        return Math.min((performance.now() - t.StartedAt) / 1000, dur);
    }

    /**
     * Disables looping. When playing with an infinite Duration (<= 0) it pauses immediately and calls OnEnded;
     * with a finite Duration the running timer ends playback at Duration.
     * @returns {Promise<void>}
     */
    async Once() {
        const t = this;
        t.Looping = false;
        if (t.Paused)
            return;
        if (t.Params.Duration > 0)
            return;
        t.Paused = true;
        await t.OnEnded();
    }

    /**
     * Seeks the timeline. Negative times are ignored; times >= Duration are clamped to Duration. When playing,
     * the timer is stopped, CurrentPosition set and Play() called again; when paused only CurrentPosition changes.
     * @param {number} time - Position in seconds.
     * @returns {Promise<null|undefined>} null when the time was negative (ignored), else undefined.
     */
    async Seek(time) {
        if (time < 0)
            return null;
        const t = this;
        const dur = t.Params.Duration;
        if (time >= dur)
            time = dur;
        const p = t.Paused;
        if (!p) {
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            t.Paused = true;
        }
        t.CurrentPosition = time;
        if (!p)
            await t.Play();
    }
}

/**
 * Base class of the players that play a media file with an HTML5 media element (MediaPlayerVideo, MediaPlayerAudio): the
 * element, a StartAt / EndAt play window, loop handling, volume, rate and the transport (Play, Pause, Stop, Seek ...).
 * Instance fields: Params (StartAt, EndAt, Volume), Url / Source, AtStart (positioned at the window start), AtEnd (paused at
 * the window end), LoopWatch (native-loop wrap detector, see Cache), Rate. A derived class sets what differs: the element, the
 * size (Measure) and what happens before playing / after unmuting.
 */
class MediaPlayerElement {

    /**
     * Creates the player and its muted element (not loaded until Cache). Sets HaveVisual / HaveAudio, calls
     * MediaPlayerTools.InitBase, element.loop = Looping, CanPlay = true, Url = Source = url, AtStart = true.
     * @param {string} url - URL of the media file.
     * @param {object} params - The params, already merged over the defaults of the derived class (StartAt, EndAt, Volume).
     * @param {string} tag - The element to make: "video" or "audio".
     * @param {boolean} haveVisual - The media shows a picture.
     * @param {boolean} haveAudio - The media has a sound.
     */
    constructor(url, params, tag, haveVisual, haveAudio) {
        const t = this;
        t.Params = params;
        const e = document.createElement(tag);
        e.muted = true;
        t.HaveVisual = haveVisual;
        t.HaveAudio = haveAudio;
        MediaPlayerTools.InitBase(t, e, params);
        e.loop = t.Looping;
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.AtStart = true;
    }

    /**
     * Loads a media URL into a media element: appends a <source src=c> child, waits for "loadeddata" or "error"
     * (MediaPlayerTools.waitEvent2), then polls every 100 ms until readyState >= 3 (HAVE_FUTURE_DATA). A caller that gave up
     * waiting (ex: the sound probe of MediaPlayerTools.CanUnMute) sets e.SwCancelLoad to end the polling.
     * @param {HTMLMediaElement} e - The media element to load into.
     * @param {string} c - The source URL.
     * @returns {Promise<boolean>} False when the element fired "error" (or the load was cancelled), else true.
     */
    static async Load(e, c) {
        const res = await MediaPlayerTools.waitEvent2(e, "loadeddata", "error", () => {
            const source = document.createElement('source');
            source.src = c;
            //  A failed <source> fires "error" on the source only (it does not bubble): without this a broken url waited forever
            source.addEventListener("error", () => e.dispatchEvent(new Event("error")), { once: true });
            e.appendChild(source);
        });
        if (res.type == "error")
            return false;
        while (e.readyState < 3) {
            if (e.SwCancelLoad)
                return false;
            await MediaPlayerTools.delay(100);
        }
        return true;
    }

    /**
     * Called by Cache when the media has loaded: sets Width / Height (0 when there is no picture) and the clip. The default:
     * no picture (0 x 0, a 0 sized element, no clip).
     * @returns {void}
     */
    Measure() {
        const t = this;
        t.Width = 0;
        t.Height = 0;
        t.Element.style.width = "0";
        t.Element.style.height = "0";
    }

    /**
     * Called by Play before the element plays (after AtEnd was handled). The default: nothing.
     * @returns {void}
     */
    BeforePlay() {
    }

    /**
     * Loads the media and installs the play-window logic. Must be awaited before using the player.
     * Calls Measure (the size), reads Duration, seeks to StartAt (when > 0), computes the effective endTime (EndAt, or the
     * duration when EndAt <= StartAt; EndAt > duration counts as 0) and the loop-watch midpoint (endTime + StartAt) / 2, then
     * installs:
     * - ontimeupdate: updates Duration; before endTime it detects a native `loop` wrap back below the midpoint
     *   (when LoopWatch is armed) and seeks back to StartAt + OnLoop, otherwise arms LoopWatch once past the
     *   midpoint (only when StartAt > 0, looping and playing); at/after endTime it seeks to StartAt when looping,
     *   else pauses at endTime, sets AtEnd and calls OnEnded.
     * - onended: natural end while playing -> pause, AtEnd = true, OnEnded.
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false when loading failed.
     */
    async Cache(keepHidden) {
        const t = this;
        const e = t.Element;
        if (!await MediaPlayerElement.Load(e, t.Url))
            return false;
        t.Measure();
        const duration = e.duration;
        t.Duration = duration;
        const p = t.Params;
        const startAt = p.StartAt;
        if (startAt > 0) {
            e.currentTime = startAt;
            t.LoopWatch = false;
        }
        let endAt = p.EndAt;
        if (endAt > duration)
            endAt = 0;
        const endTime = endAt > startAt ? endAt : duration;
        const loopWatch = (endTime + startAt) * 0.5;
        e.ontimeupdate = async () => {
            const ct = e.currentTime;
            t.Duration = e.duration;
            if (ct < endTime) {
                if (t.LoopWatch) {
                    if (ct < loopWatch) {
                        e.currentTime = startAt;
                        t.LoopWatch = false;
                        if (t.OnLoop)
                            await t.OnLoop();
                    }
                } else {
                    t.LoopWatch = (startAt > 0) && t.Looping && (!t.Paused) && (ct > loopWatch);
                }
                return;
            }
            if (t.Looping) {
                e.currentTime = startAt;
                t.LoopWatch = false;
            } else {
                if (!t.Paused) {
                    t.Paused = true;
                    t.AtEnd = true;
                    e.pause();
                    e.currentTime = endTime;
                    t.LoopWatch = false;
                    await t.OnEnded();
                }
            }
        };
        e.onended = async ev => {
            if (!t.Paused) {
                t.Paused = true;
                t.AtEnd = true;
                e.pause();
                t.LoopWatch = false;
                await t.OnEnded();
            }
        };
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    /**
     * Applies Muted and Volume * Params.Volume * 0.01 to the element (nothing when the media has no sound). A derived class may
     * route the sound elsewhere (MediaPlayerAudio: the analyser's gain).
     * @returns {void}
     */
    ApplyVolume() {
        const t = this;
        if (!t.HaveAudio)
            return;
        const e = t.Element;
        e.volume = t.Volume * t.Params.Volume * 0.01;
        e.muted = t.Muted;
    }

    /**
     * Mutes the player (sets Muted, ApplyVolume mutes the element). No-op when already muted.
     * @returns {Promise<void>}
     */
    async Mute() {
        if (this.Muted)
            return;
        this.Muted = true;
        this.ApplyVolume();
    }

    /**
     * Unmutes the player (ApplyVolume: when the media has a sound, element.volume = Volume * Params.Volume * 0.01 and
     * element.muted = false). No-op when not muted. May be blocked by the browser autoplay policy (see MediaPlayerTools.CanUnMute).
     * @returns {Promise<void>}
     */
    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        this.ApplyVolume();
    }

    /**
     * Sets the player volume. Clamps to 0..1, stores it in Volume (no-op when unchanged) and applies it (ApplyVolume).
     * @param {number} vol - Volume 0..1.
     * @returns {Promise<void>}
     */
    async SetVolume(vol) {
        if (vol < 0)
            vol = 0;
        if (vol > 1)
            vol = 1;
        if (vol == this.Volume)
            return;
        this.Volume = vol;
        this.ApplyVolume();
    }

    /**
     * Starts playback. No-op when already playing. When AtEnd, seeks to StartAt first. Sets element.loop = Looping
     * and awaits element.play(); on success Paused=false, AtStart=false and Duration is refreshed. A rejected
     * play() (e.g. autoplay policy) is swallowed and the player stays paused.
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        const e = t.Element;
        if (t.AtEnd) {
            t.AtEnd = false;
            e.currentTime = t.Params.StartAt;
            t.LoopWatch = false;
        }
        t.BeforePlay();
        try {
            e.loop = t.Looping;
            await e.play();
            t.Paused = false;
            t.AtStart = false;
            t.Duration = e.duration;
        }
        catch
        {
        }
    }

    /**
     * Stops playback: pauses, seeks to StartAt, clears LoopWatch and sets Paused = AtStart = true.
     * No-op when already paused at the start.
     * @returns {Promise<true|undefined>} True when stopped, undefined when it was already stopped.
     */
    async Stop() {
        const t = this;
        const e = t.Element;
        if (t.Paused && t.AtStart)
            return;
        e.pause();
        e.currentTime = t.Params.StartAt;
        t.LoopWatch = false;
        t.Paused = true;
        t.AtStart = true;
        return true;
    }

    /**
     * Pauses playback (keeps the position). No-op when already paused.
     * @returns {Promise<void>}
     */
    async Pause() {
        if (this.Paused)
            return;
        this.Paused = true;
        this.Element.pause();
    }

    /**
     * Seeks to a time: clamps it to [StartAt, endTime] (endTime = EndAt when > StartAt, else the duration),
     * updates AtEnd/AtStart, sets element.currentTime and clears LoopWatch. Does not change the play state.
     * @param {number} time - Position in seconds (absolute media time, not relative to StartAt).
     * @returns {Promise<void>}
     */
    async Seek(time) {
        const t = this;
        const e = t.Element;
        const startAt = t.Params.StartAt;
        const endAt = t.Params.EndAt;
        const endTime = endAt > startAt ? endAt : e.duration;
        if (time < startAt)
            time = startAt;
        if (time > endTime)
            time = endTime;
        t.AtEnd = time >= endTime;
        t.AtStart = time <= startAt;
        e.currentTime = time;
        t.LoopWatch = false;
    }

    /**
     * Gets the current playback position.
     * @returns {number} element.currentTime in seconds (absolute media time).
     */
    GetPos() {
        return this.Element.currentTime;
    }

    /**
     * The playback speed (1 = normal).
     * @type {number}
     */
    Rate = 1;

    /**
     * The fastest speed SetRate takes (the browsers play media up to 16 times faster).
     * @type {number}
     */
    MaxRate = 16;

    /**
     * Set the playback speed (element.playbackRate; the pitch of the sound is kept).
     * @param {number} rate The speed, 1 = normal (clamped to 0.0625..MaxRate).
     * @returns {Promise<void>}
     */
    async SetRate(rate) {
        const r = Math.max(0.0625, Math.min(this.MaxRate, rate || 1));
        this.Rate = r;
        this.Element.playbackRate = r;
    }

    /**
     * Enables looping (Looping = element.loop = true). No-op when already looping.
     * @returns {Promise<void>}
     */
    async Loop() {
        const t = this;
        if (t.Looping)
            return;
        t.Looping = true;
        t.Element.loop = true;
    }

    /**
     * Disables looping (Looping = element.loop = false), so playback ends at the window end. No-op when not looping.
     * @returns {Promise<void>}
     */
    async Once() {
        const t = this;
        if (!t.Looping)
            return;
        t.Looping = false;
        t.Element.loop = false;
    }
}

/**
 * Base class of the texture providers of MediaPlayerEffect (MediaPlayerImageTexture, MediaPlayerTextTexture): the GL
 * texture, the upload with mipmaps, the crop as uv transform and the uniforms. Duck-typed provider interface used by the
 * effect: Cache(haveLod), Apply(uv, size), ApplyClip(p), Dispose(), Width / Height. A derived class implements Cache, which
 * produces the picture and calls Upload. Instance fields: Params, Url, GL, Texture (WebGLTexture), Width / Height (picture
 * size), HaveClip, Clip* (from ComputeClip), UvA..UvD (uv scale x/y and offset x/y of the crop).
 */
class MediaPlayerTexture {

    /**
     * Creates the provider and an (empty) GL texture.
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - The effect's GL context.
     * @param {object} params - The params, already merged over the defaults of the derived class (Crop, EffectSizeFromImage,
     * EffectParams ...).
     * @param {string} url - The image url, or the text to render.
     */
    constructor(gl, params, url) {
        const t = this;
        t.Params = params;
        t.Url = url;
        t.GL = gl;
        t.Texture = gl.createTexture();
    }

    /**
     * Uploads a picture to the texture, flipped-Y with clamp-to-edge wrapping and linear filtering, and stores its size in
     * Width / Height. When haveLod or Params.EffectParams.MipMap, generates mipmaps (LINEAR_MIPMAP_LINEAR); on WebGL1 only for
     * power-of-two sizes (otherwise a warning is logged and mipmaps are skipped).
     * @param {HTMLImageElement|HTMLCanvasElement|ImageBitmap} image - The picture.
     * @param {boolean} haveLod - True when the shader uses texture2DLod (mipmaps required).
     * @returns {number[]|null} [width, height] of the picture when Params.EffectSizeFromImage, else null.
     */
    Upload(image, haveLod) {
        const t = this;
        const gl = t.GL;
        t.Width = image.width;
        t.Height = image.height;
        gl.bindTexture(gl.TEXTURE_2D, t.Texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        const tp = t.Params;
        const ep = tp.EffectParams;
        if (haveLod || (ep && ep.MipMap)) {
            //  (WebGL1 has mipmaps for power-of-two sizes only; a non zero v with no other bit set)
            const powerOf2 = v => v && !(v & (v - 1));
            let ok = true;
            if (!(gl instanceof WebGL2RenderingContext)) {
                if (!powerOf2(image.width)) {
                    console.warn("Need to enabled WebGL2 to have mipmaps of images then has a non power of two dimension, width = " + image.width);
                    ok = false;
                }
                else if (!powerOf2(image.height)) {
                    console.warn("Need to enabled WebGL2 to have mipmaps of images then has a non power of two dimension, height = " + image.height);
                    ok = false;
                }
            }
            if (ok) {
                gl.generateMipmap(gl.TEXTURE_2D);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
            }
        }
        if (!tp.EffectSizeFromImage)
            return null;
        return [image.width, image.height];
    }

    /**
     * Binds the texture and sets the uv transform and size uniforms (called by MediaPlayerEffect each frame): with a clip,
     * texUv = (UvA, UvB, UvC, UvD) and texSize = clip size; otherwise texUv = (1, -1, 0, 1) (flip only) and texSize = picture size.
     * @param {WebGLUniformLocation|null} uvScaleAndOffset - Location of the vec4 "texUv" uniform (scale.xy, offset.xy), may be null.
     * @param {WebGLUniformLocation|null} texSize - Location of the vec2 "texSize" uniform, may be null.
     * @returns {void}
     */
    Apply(uvScaleAndOffset, texSize) {
        const t = this;
        const gl = t.GL;
        gl.bindTexture(gl.TEXTURE_2D, t.Texture);
        if (t.HaveClip) {
            if (uvScaleAndOffset)
                gl.uniform4f(uvScaleAndOffset, t.UvA, t.UvB, t.UvC, t.UvD);
            if (texSize)
                gl.uniform2f(texSize, t.ClipWidth, t.ClipHeight);
            return;
        }
        if (uvScaleAndOffset)
            gl.uniform4f(uvScaleAndOffset, 1.0, -1.0, 0.0, 1.0);
        if (texSize)
            gl.uniform2f(texSize, t.Width, t.Height);
    }

    /**
     * Computes the crop as UV scale / offset: runs MediaPlayerTools.ComputeClip (which sets ClipX / ClipB / ClipWidth /
     * ClipHeight) and, when clipped, sets HaveClip and UvA = ClipWidth/Width, UvB = -ClipHeight/Height, UvC = ClipX/Width,
     * UvD = (Height - ClipB)/Height. HaveClip tells Apply whether to use it.
     * @param {object} [p] - Params holding a Crop; defaults to this.Params.
     * @returns {void}
     */
    ApplyClip(p) {
        const t = this;
        t.HaveClip = false;
        p = p ?? t.Params;
        if (!MediaPlayerTools.ComputeClip(t, p))
            return;
        t.HaveClip = true;
        const ow = t.Width;
        const oh = t.Height;
        const cw = t.ClipWidth;
        const ch = t.ClipHeight;
        t.UvA = cw / ow;
        t.UvB = -ch / oh;
        t.UvC = t.ClipX / ow;
        t.UvD = (oh - t.ClipB) / oh;
    }

    /**
     * Deletes the GL texture.
     * @returns {void}
     */
    Dispose() {
        const t = this;
        t.GL.deleteTexture(t.Texture);
    }
}


/** Publish the new API name (MediaPlayerTools stays the public legacy global). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    (Sw.Media = Sw.Media || {}).Tools = MediaPlayerTools;
})();
