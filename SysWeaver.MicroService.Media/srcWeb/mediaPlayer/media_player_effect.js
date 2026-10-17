/**
 * @file media_player_effect.js
 * WebGL GLSL effect player (MediaPlayerEffect) and the image texture provider (MediaPlayerImageTexture).
 *
 * Loading: included by MediaPlayer.Init (only when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (together with media_effect_program.js) through common.js includeJs; also loaded with static <script defer>
 * tags by the mediaView/backgroundEffects|imageEffects/index.html and *EffectsLib.html pages, which construct
 * MediaPlayerEffect directly.
 *
 * Globals defined: MediaPlayerParamsEffect, MediaPlayerEffect, MediaPlayerImageTexture.
 * At class definition time MediaPlayerEffect.DpiScale is read from localStorage["SysWeaver.Media.EffectDpiScale"].
 *
 * Dependencies: EffectProgramData / EffectProgram (media_effect_program.js); MediaPlayerTools
 * (media_player_tools.js: InitBase, IsDarkMode, Add/RemoveDarkModeChangeEvent, IsAttached, delay, waitEvent2,
 * OnCacheComplete); common.js hashString and bufferToBase64 (GetImage only); optional common/anim.js
 * (SmoothBool, SmoothValue, SpringAnimation, BounceAnimation, Smoothness) for dark-mode smoothing and the
 * CustomX/Y/Z animators.
 *
 * Conventions:
 * - The effect renders a fullscreen triangle; the fragment shader receives the uniforms time/iTime, iTimeDelta,
 *   resolution/iResolution, iFrame, iDate, mouse/iMouse, scroll, customDark (xyz = CustomX/Y/Z, w = dark mode
 *   0..1) and, with a texture provider, tex/texUv/texSize.
 * - Texture providers (MediaPlayerImageTexture, MediaPlayerTextTexture) are duck-typed:
 *   Cache(haveLod), Apply(uvLoc, sizeLoc), ApplyClip(p), Dispose(), Width/Height.
 * - Remote control: MediaPlayerTools.SetupMediaMessages maps MediaCustomX/Y/Z and MediaDebugLoseContext messages
 *   to SetCustomX/Y/Z and DebugLoseContext.
 */

/**
 * Parameters for an effect player; mirrors the C# type SysWeaver.MicroService.Media.MediaEffect
 * (field names match; values arrive as JSON). Instances are created by the MediaPlayerEffect constructor and
 * the supplied params are Object.assign'ed over these defaults.
 */
class MediaPlayerParamsEffect {
    /** @type {number} CSS width in px of the canvas (1280 if falsy); also the max extent for LimitSize and the backing-store width when not AdaptiveSize. Default 1920. */
    Width = 1920;
    /** @type {number} CSS height in px (Width * 9/16 if falsy). Default 1080. */
    Height = 1080;
    /** @type {number} Time multiplier for the time/iTime uniform; a value <= 0 also freezes iDate at MediaPlayerEffect.ZeroDate. Default 1. */
    Speed = 1;
    /** @type {boolean} Clamp the backing-store size to the Width/Height extent (long side <= max, short side <= min). Default false. */
    LimitSize = false;
    /** @type {boolean} Automatically adjust the global MediaPlayerEffect.DpiScale from measured draw time (ignored for Static). Default false (C# default is true). */
    DpiAdjust = false;
    /** @type {number} Fixed backing-store scale factor applied on top of the device pixel ratio. Default 1. */
    DpiScale = 1;
    /** @type {boolean} Render a single frame (re-rendered on resize / dark-mode change) instead of animating. Default false. */
    Static = false;
    /** @type {boolean} Create the context with alpha and clear to transparent instead of opaque green (0,0.5,0,1). Default false. */
    Transparent = false;
    /** @type {boolean} Backing store follows the element's client size x devicePixelRatio; set to false by Create when the texture dictates the size. Default true. */
    AdaptiveSize = true;
    /** @type {boolean} Use REPEAT wrapping on the S (U) axis of the texture (applied in render when ApplyTextue). Default false. */
    WrapU = false;
    /** @type {boolean} Use REPEAT wrapping on the T (V) axis of the texture (applied in render when ApplyTextue); render honors both WrapY and the C# name WrapV. Default false. */
    WrapY = false;
    /** @type {string|null} Declared (texture URL in C#); not read in this folder. Default null. */
    Texture = null;
    /** @type {boolean} Request a "webgl2" context instead of "webgl". Default false. */
    WebGL2 = false;
    /** @type {boolean} Generate mipmaps for the texture (read by the texture providers via EffectParams.MipMap). Default false. */
    MipMap = false;
    /** @type {boolean} Context antialias flag (retried without antialias when context creation fails). Default false. */
    AntiAlias = false;
    /** @type {string|null} Element id whose scroll position feeds the scroll uniform (resolved by GetElementFromId; null = top document body). Default null. */
    ScrollId = null;
    /** @type {string|null} Element id whose mousemove events feed the mouse/iMouse uniforms. Default null. */
    MouseId = null;
    /** @type {boolean} Enable mouse tracking on the top document body when MouseId is not given. Default false. */
    MouseEnable = false;
    /** @type {boolean} Declared; not read in this folder. Default false. */
    ImageSize = false;
    /** @type {boolean} Log the effect's default parameter values (pretty JSON) on Create. Default false. */
    ShowVars = false;
    /** @type {object|string|null} Effect parameter values (object or JSON string) validated into EffectProps by Create. Default null (= effect defaults). */
    FxProps = null;
    /** @type {string|null} "dark" or "light" to fix dark mode instead of tracking the top page's theme. Default null. */
	ForceMode = null;
    /** @type {number} Dark-mode transition time in ms (SmoothBool from anim.js); 0 disables smoothing. Default 200. */
    DarkModeSwitch = 200;
    /** @type {number} Initial value of the customDark.x uniform channel. Default 0. */
    CustomX = 0;
    /** @type {number} Initial value of the customDark.y uniform channel. Default 0. */
    CustomY = 0;
    /** @type {number} Initial value of the customDark.z uniform channel. Default 0. */
    CustomZ = 0;
    /** @type {string|function|null} Animator spec for CustomX (see MediaPlayerEffect.GetAnimFn); null = plain value. Default null. */
    CustomAnimX = null;
    /** @type {string|function|null} Animator spec for CustomY (see MediaPlayerEffect.GetAnimFn). Default null. */
    CustomAnimY = null;
    /** @type {string|function|null} Animator spec for CustomZ (see MediaPlayerEffect.GetAnimFn). Default null. */
    CustomAnimZ = null;
    /** @type {number} Seconds added to the time uniform (after Speed scaling). Default 0. */
    Offset = 0;
}

/**
 * Plays a GLSL effect in a <canvas> using WebGL, optionally sampling a texture produced by a texture provider
 * (image or text). Created by MediaPlayer.Create (Effect, or Image/Text with Effect), by GetImage, or directly by
 * the effect pages. Follows the common player interface set up by MediaPlayerTools.InitBase.
 * Lifecycle: constructor -> Cache (fetch .glsl + Create) -> Play (requestAnimationFrame render loop) /
 * Pause / Stop / Seek -> OnDispose. Lost contexts are recovered by Validate.
 * The canvas carries the expando property canvas.This = player.
 */
class MediaPlayerEffect {

    /**
     * Global adaptive DPI scale (0.125..1) shared by all effects, read from
     * localStorage["SysWeaver.Media.EffectDpiScale"] at load (default 1) and updated/persisted by AdjustDpi.
     * @type {number}
     */
    static DpiScale = parseFloat(localStorage.getItem("SysWeaver.Media.EffectDpiScale") ?? "1");


    /** Create a screenshot of an effect.
     * Cache the screen shot in local storage (recreates if etag changes)
     * Used by the *EffectsLib.html thumbnail galleries. Storage key:
     * "SysWeaver.EffectInfo." + cacheName + "." + hashString(url), value {R: result, T: etag}.
     * If cached, re-fetches url and returns the cached result when the etag is unchanged. Otherwise creates an
     * off-screen effect (position absolute, top -10000px, appended to document.body), caches it, seeks to 5 s,
     * plays until a GPU timer sample exists (when a timer query is available), stops, encodes the canvas as
     * JPEG, disposes the player and converts the blob to a data URI.
     * @param {string} cacheName - Cache namespace (e.g. gallery name).
     * @param {string} url - URL of the .glsl effect.
     * @param {MediaPlayerParamsEffect|object} mp - Effect params.
     * @param {function(WebGLRenderingContext):object} [ct] - Optional texture provider factory.
     * @returns {Promise<Array>} [dataUri, compilationTimeMs, avgDrawTimeMs], or [null, errorMessage] on failure.
     */
    static async GetImage(cacheName, url, mp, ct) {
        const key = "SysWeaver.EffectInfo." + cacheName + "." + await hashString(url);
        try {
            try {
                const cached = localStorage.getItem(key);
                if (cached) {
                    const data = JSON.parse(cached);
                    const r = new Request(url, {
                        method: "GET",
                        mode: "cors",
                        cache: "default",
                    });
                    const res = await fetch(r);
                    if (res.status == 200) {
                        const et = res.headers.get("etag");
                        if (data.T === et)
                            return data.R;
                    }
                }
            }
            catch (ce) {
                console.warn("Failed to load cached effect image: " + ce.message);
            }
            const effect = new MediaPlayerEffect(url, mp, ct);
            const ee = effect.Element;
            ee.style.position = "absolute";
            ee.style.top = "-10000px";
            document.body.appendChild(ee);
            await effect.Cache(true);
            const compilationTime = effect.Program.CompilationTime;
            const etag = effect.Etag;
            effect.Seek(5);
            await effect.Play();
            if (effect.Query) {
                while (effect.AvgDrawTimeMs <= 0)
                    await MediaPlayerTools.delay(1);
            }
            const avgDrawTimeMs = effect.AvgDrawTimeMs;
            await effect.Stop();
            const image = await new Promise((resolve, reject) => {
                effect.Element.toBlob(data => {
                    if (data) {
                        resolve(data);
                    } else
                        reject();
                }, "image/jpeg", 70);
            });
            await effect.OnDispose();
            url = await bufferToBase64(image, true);
            const ret = [url, compilationTime, avgDrawTimeMs];
			try
			{
				localStorage.setItem(key, JSON.stringify({
					R: ret,
					T: etag,
				}));
			}
			catch (ex)
			{
				console.warn(ex.message);
			}
            return ret;
        }
        catch (ex) {
            console.warn("Failed to read effect \"" + url + "\", " + ex.message);
            return [null, ex.message];
        }
    }


    /**
     * Finds an element by id in this window or up to 9 parent windows (stops at top).
     * @param {string} [id] - Element id.
     * @returns {HTMLElement} The element, or top.document.body when id is empty, not found or access throws (cross-origin).
     */
    static GetElementFromId(id) {
        const tw = top;
        if (!id)
            return tw.document.body;
        try {
            let win = window;
            for (let j = 0; j < 10; ++j) {
                const i = win.document.getElementById(id);
                if (i)
                    return i;
                if (win == tw)
                    break;
                const pw = win.parent;
                if (pw === win)
                    break;
                win = pw;
                if (!win)
                    break;
            }
        }
        catch {
        }
        return tw.document.body;
    }

    /**
     * Creates the WebGL context on the canvas ("webgl" or "webgl2" per Params.WebGL2; antialias = AntiAlias,
     * alpha = Transparent, no depth/stencil, no preserveDrawingBuffer; retries without antialias), enables
     * OES_standard_derivatives and EXT_shader_texture_lod, creates the fullscreen-triangle vertex buffer Pos
     * ([-1,1, 3,1, -1,-3]), creates Texture via CreateTextureFn(gl) and a timer Query
     * ({E: ext, Q: query, IsQ: false}) when EXT_disjoint_timer_query exists.
     * @throws {Error} "Can't initiate WebGL!" when no context can be created.
     * @returns {void}
     */
    InitGL() {
        const t = this;
        const params = t.Params;
        const e = t.Element;
        const glp = {
            antialias: params.AntiAlias,
            depth: false,
            stencil: false,
            alpha: params.Transparent,
            preserveDrawingBuffer: false,
        };
        let glver = params.WebGL2 ? "webgl2" : "webgl";
        let gl = e.getContext(glver, glp);
        if (!gl) {
            if (glp.antialias) {
                glp.antialias = false;
                gl = e.getContext(glver, glp);
            }
        }
        if (!gl)
            throw new Error("Can't initiate WebGL!");
        t.GL = gl;
        gl.getExtension("OES_standard_derivatives");
        gl.getExtension("EXT_shader_texture_lod");
        const pos = gl.createBuffer();
        t.Pos = pos;
        gl.bindBuffer(gl.ARRAY_BUFFER, pos);
        const positions = [
            -1, 1,
            3, 1,
            -1, -3,
        ];
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);
        const createTextureFn = t.CreateTextureFn;
        if (createTextureFn)
            t.Texture = createTextureFn(gl);
        const ext = gl.getExtension("EXT_disjoint_timer_query");
        if (ext) {
            t.Query =
            {
                E: ext,
                Q: ext.createQueryEXT(),
                IsQ: false,
            };
        }
    }

    /**
     * Creates the player and its <canvas> (not yet loaded; call Cache).
     * Merges params over MediaPlayerParamsEffect defaults, sets the canvas CSS size to Width x Height, calls
     * MediaPlayerTools.InitBase, sets CompilationTime = -1, resets counters and applies the clip.
     * Dark mode: fixed by params.ForceMode, otherwise read from the top page and tracked through
     * MediaPlayerTools.AddDarkModeChangeEvent (optionally smoothed with SmoothBool when anim.js is loaded,
     * not Static and DarkModeSwitch > 0). Creates the CustomX/Y/Z animators with GetAnimFn.
     * @param {string} url - URL of the .glsl effect source.
     * @param {MediaPlayerParamsEffect|object} [params] - Effect params.
     * @param {function(WebGLRenderingContext):object} [createTextureFn] - Factory for a texture provider
     * (MediaPlayerImageTexture / MediaPlayerTextTexture), called from InitGL.
     */
    constructor(url, params, createTextureFn) {
        const t = this;
        const cp = new MediaPlayerParamsEffect();
        if (params)
            Object.assign(cp, params);
        params = cp;
        const e = document.createElement("canvas");
        e.This = t;
        t.Params = params;
        t.CreateTextureFn = createTextureFn;
        t.ResumeTime = 0;
        t.CurrentTime = 0;
        t.Rendered = false;
        t.Program = null;
        const w = params.Width ? params.Width : 1280;
        const h = params.Height ? params.Height : (w * 9 / 16);
        e.style.width = w + "px";
        e.style.height = h + "px";
        t.HaveVisual = true;
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.CurrentPosition = 0;
        t.FrameIndex = 0;
        MediaPlayerTools.InitBase(t, e, params);
        t.Width = w;
        t.Height = h;
        t.CompilationTime = -1;
        t.ResetCounters();
        t.ApplyClip();
		if (params.ForceMode)
		{
			t.IsDarkMode = params.ForceMode === "dark";
		}else {
			t.IsDarkMode = MediaPlayerTools.IsDarkMode();
			const smooth = ((!params.Static) && (typeof Smoothness === "object") && (params.DarkModeSwitch > 0)) ? new SmoothBool(null, t.IsDarkMode, params.DarkModeSwitch, Smoothness.Smooth) : null;
			t.SmoothDark = smooth;
			/**
			 * Dark-mode change callback (registered with MediaPlayerTools.AddDarkModeChangeEvent): updates IsDarkMode,
			 * re-renders immediately when Static, and feeds the SmoothDark smoother.
			 * @param {boolean} isDark - New dark-mode state.
			 * @returns {void}
			 */
			t.DarkModeChanged = isDark =>
			{
				t.IsDarkMode = isDark;
				if (params.Static)
				{
					t.Rendered = false;
					MediaPlayerEffect.render(t);
				}
				if (smooth)
					smooth.Update(null, isDark);
			};
			MediaPlayerTools.AddDarkModeChangeEvent(t.DarkModeChanged);
		}
		t.CustomX = MediaPlayerEffect.GetAnimFn(params.CustomAnimX, params.CustomX);
		t.CustomY = MediaPlayerEffect.GetAnimFn(params.CustomAnimY, params.CustomY);
		t.CustomZ = MediaPlayerEffect.GetAnimFn(params.CustomAnimZ, params.CustomZ);
    }

	/**
	 * Sets the customDark.x channel (target of the MediaCustomX message). If m.Anim is given the channel
	 * animator is replaced by GetAnimFn(m.Anim, m.Start ?? m.Value) (Start only when numeric) before Set(m.Value).
	 * @param {{Value:number, Anim?:string, Start?:number}} m - Message payload.
	 * @returns {void}
	 */
	SetCustomX(m)
	{
		const v = m.Value;
		const a = m.Anim;
		let n = this.CustomX;
		if (a)
		{
			const sv = m.Start;
			n = MediaPlayerEffect.GetAnimFn(a, typeof sv === "number" ? sv : v);
			this.CustomX = n;
		}
		n.Set(v);
	}

	/**
	 * Sets the customDark.y channel (target of the MediaCustomY message); see SetCustomX.
	 * @param {{Value:number, Anim?:string, Start?:number}} m - Message payload.
	 * @returns {void}
	 */
	SetCustomY(m)
	{
		const v = m.Value;
		const a = m.Anim;
		let n = this.CustomY;
		if (a)
		{
			const sv = m.Start;
			n = MediaPlayerEffect.GetAnimFn(a, typeof sv === "number" ? sv : v);
			this.CustomY = n;
		}
		n.Set(v);
	}

	/**
	 * Sets the customDark.z channel (target of the MediaCustomZ message); see SetCustomX.
	 * @param {{Value:number, Anim?:string, Start?:number}} m - Message payload.
	 * @returns {void}
	 */
	SetCustomZ(m)
	{
		const v = m.Value;
		const a = m.Anim;
		let n = this.CustomZ;
		if (a)
		{
			const sv = m.Start;
			n = MediaPlayerEffect.GetAnimFn(a, typeof sv === "number" ? sv : v);
			this.CustomZ = n;
		}
		n.Set(v);
	}


	/**
	 * Builds a channel animator for a customDark component.
	 * Spec forms (CSV, classes from common/anim.js):
	 * - null/empty: plain value.
	 * - function: Get = f, Set is a no-op.
	 * - "SmoothBool[,delayMs=200[,smoothness=Smoothness.Normal]]": value != 0 as a smoothed boolean.
	 * - "SmoothValue[,delayMs=200]": smoothed value.
	 * - "Spring[,frequency=7,initialLength=100,decay=90,snap=0.01]" and
	 *   "Bounce[,initialVelocity=5,gravity=10,restitution=0.8,restVelocity=0.1,normalize=true,startAtPeak=false]":
	 *   Get = curve value at the time since the last Set x magnitude; Set = new magnitude and restart.
	 * - Unknown names: plain value.
	 * @param {string|function|null} f - Animator spec.
	 * @param {number} [v=0] - Initial value (magnitude for Spring/Bounce).
	 * @returns {{Get:function():number, Set:function(number):void}} The animator.
	 */
	static GetAnimFn(f, v)
	{
		if (!v)
			v = 0;
		if (!f)
		{
			let val = v;
			/** @returns {number} The current plain value. */
			const Get = () => val;
			/** @param {number} nv - New value. */
			const Set = nv => val = nv;
			return { Get, Set };
        }
        if (typeof f === "function") {

            /** @returns {number} The value computed by the supplied function. */
            const Get = f;
            /** No-op: a function animator can't be set. @param {number} nv - Ignored. */
            const Set = nv => { };
            return { Get, Set };
        }
		const c = f.split(',');
		const cl = c.length;
		if (c[0] === "SmoothBool")
		{
			const delay = cl > 1 ? parseFloat(c[1]) : 200;
			const sm = cl > 2 ? parseInt(c[2]) : Smoothness.Normal;
			const anim = new SmoothBool(null, v !== 0, delay, sm);
			/** @returns {number} The smoothed 0..1 value now. */
			const Get = () => anim.ValueAt();
			/** @param {number} nv - New target (non-zero = true). */
			const Set = nv => anim.Update(null, nv !== 0);
			return { Get, Set };
		}
		if (c[0] === "SmoothValue")
		{
			const delay = cl > 1 ? parseFloat(c[1]) : 200;
			const anim = new SmoothValue(null, v, delay);
			/** @returns {number} The smoothed value now. */
			const Get = () => anim.ValueAt();
			/** @param {number} nv - New target value. */
			const Set = nv => anim.Update(null, nv);
			return { Get, Set };
		}
        if (c[0] === "Spring") {
            const frequency = cl > 1 ? parseFloat(c[1]) : 7;
            const initialLength = cl > 2 ? parseFloat(c[2]) : 100;
            const decay = cl > 3 ? parseFloat(c[3]) : 90;
            const snap = cl > 4 ? parseFloat(c[4]) : 0.01;
            let mag = v;
            let startTime = performance.now();
            const anim = new SpringAnimation(frequency, initialLength, decay, snap);
            /** @returns {number} Spring curve at the time since the last Set, times the magnitude. */
            const Get = () => anim.ValueAt(performance.now() - startTime) * mag;
            /** @param {number} nv - New magnitude; restarts the spring. */
            const Set = nv => {
                mag = nv;
                startTime = performance.now();
            };
            return { Get, Set };
        }
        if (c[0] === "Bounce") {
            const initialVelocity = cl > 1 ? parseFloat(c[1]) : 5;
            const gravity = cl > 2 ? parseFloat(c[2]) : 10;
            const restitution = cl > 3 ? parseFloat(c[3]) : 0.8;
            const restVelocity = cl > 4 ? parseFloat(c[4]) : 0.1;
            const normalize = cl > 5 ? (c[5] === "true") : true;
            const startAtPeak = cl > 6 ? (c[6] === "true") : false;
            let mag = v;
            let startTime = performance.now();
            const anim = new BounceAnimation(initialVelocity, gravity, restitution, restVelocity, normalize, startAtPeak);
            /** @returns {number} Bounce curve at the time since the last Set, times the magnitude. */
            const Get = () => anim.ValueAt(performance.now() - startTime) * mag;
            /** @param {number} nv - New magnitude; restarts the bounce. */
            const Set = nv => {
                mag = nv;
                startTime = performance.now();
            };
            return { Get, Set };
        }
		{
			let val = v;
			/** @returns {number} The current plain value (unknown spec name). */
			const Get = () => val;
			/** @param {number} nv - New value. */
			const Set = nv => val = nv;
			return { Get, Set };
		}
	}


    /**
     * Applies a crop to the texture provider (overrides the InitBase default; effects use no CSS clip-path).
     * Without a texture nothing happens. Otherwise forwards to Texture.ApplyClip(p), sets ApplyTextue so the
     * next render re-binds the texture uniforms, and renders a frame if not animating (GetRender()()).
     * @param {object} [p] - Crop (MediaCrop-like) or undefined to use the provider's params.
     * @returns {void}
     */
    ApplyClip(p) {
        const t = this;
        if (t.Texture) {
            t.Texture.ApplyClip(p);
            t.ApplyTextue = true;
            t.GetRender()();
        }
    }

	/** @type {{Get:function():number, Set:function(number):void}} customDark.x animator (initializer 0 is replaced in the constructor). */
	CustomX = 0;
	/** @type {{Get:function():number, Set:function(number):void}} customDark.y animator. */
	CustomY = 0;
	/** @type {{Get:function():number, Set:function(number):void}} customDark.z animator. */
	CustomZ = 0;

    /**
     * Releases all GL resources: unobserves the ResizeObserver, disposes the texture provider, the program and
     * the position buffer and clears GL (no-op for GL objects when there is no context).
     * @returns {void}
     */
    ReleaseGL() {
        const t = this;
        if (t.ResizeObserver)
            t.ResizeObserver.unobserve(t.Element);
        t.ResizeObserver = null;
        const gl = t.GL;
        if (!gl)
            return;
        t.GL = null;
        if (t.Texture)
            t.Texture.Dispose();
        t.Texture = null;
        if (t.Program)
            t.Program.Dispose();
        t.Program = null;
        if (t.Pos)
            gl.deleteBuffer(t.Pos);
        t.Pos = null;
    }

    /**
     * Disposes the player: pauses, waits until the frame loop has stopped, unregisters DarkModeChanged,
     * removes the canvas from the DOM, releases GL and removes the mouse listener.
     * @returns {Promise<void>}
     */
    async OnDispose() {
        const t = this;
        await t.Pause();
        while (t.IsAnimating)
            await MediaPlayerTools.delay(1);
		MediaPlayerTools.RemoveDarkModeChangeEvent(t.DarkModeChanged);
        if (t.Element.parentNode)
            t.Element.remove();
        t.ReleaseGL();
        const rml = t.RemoveMouseListener;
        if (rml)
            rml();
        t.RemoveMouseListener = null;
    }

    /**
     * Resets the performance counters: TotalTime = 10000000 (min-tracking start), MeasureCount = 0,
     * AvgDrawTimeMs = 0, PrintCounter = 0.
     * @returns {void}
     */
    ResetCounters() {
        const t = this;
        t.TotalTime = 10000000;
        t.MeasureCount = 0;
        t.AvgDrawTimeMs = 0;
        t.PrintCounter = 0;
    }


    /**
     * Debounced recompile for live parameter edits (after Params.EffectProps changed). If a compile is running
     * only CompileAgain is flagged; otherwise after 50 ms runs Compile(() => CompileAgain) in a loop until no new
     * request arrived during the compile (the running compile is aborted when a new request arrives).
     * @returns {void}
     */
    LazyCompile() {
        const t = this;
        if (t.Compiling) {
            t.CompileAgain = true;
            return;
        }
        t.Compiling = true;
        setTimeout(async () => {
            for (; ;) {
                t.CompileAgain = false;
                await t.Compile(() => t.CompileAgain);
                if (!t.CompileAgain)
                    break;
            }
            t.Compiling = false;
        }, 50);
    }

    /**
     * Returns a function that renders a single frame when needed.
     * @returns {function():void} When not animating or Static: clears Rendered and renders one frame if the canvas
     * is attached; otherwise a no-op (the animation loop will render anyway).
     */
    GetRender() {
        const t = this;
        const params = t.Params;
        if ((!t.IsAnimating) || params.Static)
            return () => {
                t.Rendered = false;
                if (MediaPlayerTools.IsAttached(t.Element))
                    MediaPlayerEffect.render(t);
            }
        return () => { };
    }

    /**
     * Testing aid (MediaDebugLoseContext message): forces a WebGL context loss via WEBGL_lose_context and logs a warning.
     * @returns {void}
     */
    DebugLoseContext() {
        const gl = this.GL;
        if (!gl)
            return;
        gl.getExtension("WEBGL_lose_context").loseContext();
        console.warn("Forcefully lost webgl context!");
    }

    /**
     * Compiles ProgramData with Params.EffectProps and the default vertex shader
     * (attribute vec2 position; gl_Position = vec4(position,0,1)). On success: uses the program, resolves the
     * position attribute, builds the Uniform* setters (no-ops for absent uniforms), binds the texture provider
     * (tex on unit 0, texUv, texSize), sets up the vertex attribute, disposes the previous program, stores Program
     * and CompilationTime, and re-renders when called with an abort function (live edit).
     * @param {function():boolean} [abortCheckFn] - Abort check passed to EffectProgramData.Compile.
     * @returns {Promise<boolean|string>} true on success; false on failure (CompilationTime = -3);
     * "Aborted" when aborted (CompilationTime = -2).
     */
    async Compile(abortCheckFn) {
        const t = this;
        t.CompilationTime = -1;
        const program = await t.ProgramData.Compile(t.Params.EffectProps, "attribute vec2 position;void main(){gl_Position=vec4(position, 0, 1);}", abortCheckFn);
        if (!program) {
            t.CompilationTime = -3;
            return false;
        }
        if (program === "Aborted") {
            t.CompilationTime = -2;
            return program;
        }
        const renderUpdate = abortCheckFn ? t.GetRender() : () => { };
        const p = program.Program;
        const gl = t.GL;
        gl.useProgram(p);
        t.AttrPos = gl.getAttribLocation(p, "position");
        //  Time
        const pt = gl.getUniformLocation(p, "time") ?? gl.getUniformLocation(p, "iTime");
        /** Sets time (or iTime) in seconds; no-op when absent. @type {function(number):void} */
        t.UniformTime = pt ? time => gl.uniform1f(pt, time) : time => { };
        //  Time delta
        const ptd = gl.getUniformLocation(p, "iTimeDelta");
        /** Sets iTimeDelta (seconds since the previous frame); no-op when absent. @type {function(number):void} */
        t.UniformTimeDelta = ptd ? timeDelta => gl.uniform1f(ptd, timeDelta) : timeDelta => { };
        //  Resolution
        /**
         * Sets the resolution uniform (no-op when absent).
         * @param {number} w - Backing-store width in pixels.
         * @param {number} h - Backing-store height in pixels.
         * @returns {void}
         */
        t.UniformResolution = (w, h) => { };
        const pr = gl.getUniformLocation(p, "resolution");
        if (pr)
            /** Sets resolution (vec2). @param {number} w - Width. @param {number} h - Height. */
            t.UniformResolution = (w, h) => gl.uniform2f(pr, w, h);
        const pr3 = gl.getUniformLocation(p, "iResolution");
        if (pr3)
            /** Sets iResolution (vec3, z = 0); takes precedence over resolution. @param {number} w - Width. @param {number} h - Height. */
            t.UniformResolution = (w, h) => gl.uniform3f(pr3, w, h, 0);
        //  Frame
        const pf = gl.getUniformLocation(p, "iFrame");
        /** Sets iFrame (frame index); no-op when absent. @type {function(number):void} */
        t.UniformFrameIndex = pf ? frameIndex => gl.uniform1i(pf, frameIndex) : frameIndex => { };
        //  Date
        const pd = gl.getUniformLocation(p, "iDate");
        /** Sets iDate = (year, month 1-12, day, seconds of day); no-op when absent. @type {function(Date):void} */
        t.UniformDate = pd ? date => gl.uniform4f(pd, date.getFullYear(), date.getMonth() + 1, date.getDate(), date.getSeconds() + date.getMinutes() * 60 + date.getHours() * 3600) : date => { };
        //  Mouse
        const pm = gl.getUniformLocation(p, "mouse");
        const pm2 = gl.getUniformLocation(p, "iMouse");
        /**
         * Sets mouse (vec2) and/or iMouse (vec4, zw = 0) when present.
         * @param {number} x - Mouse x.
         * @param {number} y - Mouse y.
         * @returns {void}
         */
        t.UniformMouse = (x, y) => {
            if (pm)
                gl.uniform2f(pm, x, y);
            if (pm2)
                gl.uniform4f(pm2, x, y, 0, 0);
        };
        //  Scroll
        const sc = gl.getUniformLocation(p, "scroll");
        /**
         * Sets the scroll uniform (no-op when absent).
         * @param {number} x - scrollLeft in px.
         * @param {number} y - scrollTop in px.
         * @param {number} rx - Horizontal scroll ratio.
         * @param {number} ry - Vertical scroll ratio.
         * @returns {void}
         */
        t.UniformScroll = (x, y, rx, ry) => { };
        if (sc)
            /** Sets scroll (vec4 = x, y, rx, ry). @param {number} x - X. @param {number} y - Y. @param {number} rx - X ratio. @param {number} ry - Y ratio. */
            t.UniformScroll = (x, y, rx, ry) => gl.uniform4f(sc, x, y, rx, ry);

        const cd = gl.getUniformLocation(p, "customDark");
        /**
         * Sets the customDark uniform (no-op when absent).
         * @param {number} x - CustomX channel.
         * @param {number} y - CustomY channel.
         * @param {number} z - CustomZ channel.
         * @param {number} isDarkMode - Dark mode 0..1 (smoothed).
         * @returns {void}
         */
        t.UniformCustomDark = (x, y, z, isDarkMode) => { };
        if (cd)
            /** Sets customDark (vec4). @param {number} x - X. @param {number} y - Y. @param {number} z - Z. @param {number} isDarkMode - Dark 0..1. */
            t.UniformCustomDark = (x, y, z, isDarkMode) => gl.uniform4f(cd, x, y, z, isDarkMode);

        const tex = t.Texture;
        if (tex) {
            const tu = gl.getUniformLocation(p, "tex");
            const tuv = gl.getUniformLocation(p, "texUv");
            const ts = gl.getUniformLocation(p, "texSize");
            if (tu) {
                gl.activeTexture(gl.TEXTURE0);
                tex.Apply(tuv, ts);
                gl.uniform1i(tu, 0);
            }
        }
        gl.enableVertexAttribArray(t.AttrPos);
        gl.bindBuffer(gl.ARRAY_BUFFER, t.Pos);
        gl.vertexAttribPointer(t.AttrPos, 2, gl.FLOAT, false, 0, 0);
        if (t.Program)
            t.Program.Dispose();
        t.Program = program;
        t.CompilationTime = program.CompilationTime;
        if (abortCheckFn)
            t.GetRender()();
        return true;
    }


    /**
     * Initializes GL and compiles the effect from Src (called by Cache and again by Validate after context loss).
     * Steps: InitGL; parse Src into ProgramData (EffectProgramData); log defaults if ShowVars; cache the texture
     * provider (when it returns a size: resize the canvas CSS size, Width/Height and Params.Width/Height, and set
     * Params.AdaptiveSize = false); validate FxProps into Params.EffectProps (deleted if the effect has no
     * parameters) and expose ScriptMember/ScriptTypeInfo; Compile; disable depth/blend; set the clear color
     * (transparent or opaque green); Cached = true; render once if attached; install a ResizeObserver that stores
     * ClientWidth/ClientHeight and re-renders; resolve ScrollElement; with MouseId/MouseEnable install a
     * mousemove listener and RemoveMouseListener.
     * @returns {Promise<false|undefined>} false when compilation failed, otherwise undefined.
     */
    async Create() {

        const t = this;
        const e = t.Element;
        const props = t.Params;
        const c = t.Url;

        t.InitGL();

        const gl = t.GL;

        const programData = new EffectProgramData(gl, t.Src, c);
        const p = t.Params;
        t.ProgramData = programData;

        if (p.ShowVars) {
            const pp = programData.ScriptTypeInfo;
            if (pp) {
                console.log("Default effect parameters:");
                try {
                    console.log(JSON.stringify(JSON.parse(pp.Default), null, "    "));
                }
                catch {
                    console.log(pp.Default);
                }
            }
        }

        //  Load / create texture, may change FxProps

        const tex = t.Texture;
        if (tex) {
            const newSize = await tex.Cache(programData.HaveLod);
            if (newSize) {
                const w = newSize[0];
                const h = newSize[1];
                if ((t.Width != w) || (t.Height != h)) {
                    e.style.width = w + "px";
                    e.style.height = h + "px";
                    t.Width = w;
                    t.Height = h;
                    p.Width = w;
                    p.Height = h;
                    p.AdaptiveSize = false;
                }
            }
        }


        let fxProps = props.FxProps;
        if (typeof fxProps === "object")
            fxProps = JSON.stringify(fxProps);
        fxProps = programData.ValidateVars(fxProps);
        if (fxProps) {
            props.EffectProps = JSON.parse(fxProps);
            t.ScriptMember = programData.ScriptMember;
            t.ScriptTypeInfo = programData.ScriptTypeInfo;
        }
        else {
            delete props.EffectProps;
            t.ScriptMember = null;
            t.ScriptTypeInfo = null;
        }



        if (!await t.Compile())
            return false;

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.BLEND);
        if (props.Transparent)
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
        else
            gl.clearColor(0.0, 0.5, 0.0, 1.0);
        gl.clearDepth(1.0);

        t.Cached = true;

        if (MediaPlayerTools.IsAttached(t.Element))
            MediaPlayerEffect.render(t);

        t.ResizeObserver = new ResizeObserver(() => {
            t.ClientWidth = e.clientWidth;
            t.ClientHeight = e.clientHeight;
            t.GetRender()();
        }); 
        t.ResizeObserver.observe(e);
        t.ClientWidth = e.clientWidth;
        t.ClientHeight = e.clientHeight;

        t.ScrollElement = MediaPlayerEffect.GetElementFromId(props.ScrollId);

        if (props.MouseId || props.MouseEnable) {

            const mouseElement = MediaPlayerEffect.GetElementFromId(props.MouseId);
            /**
             * mousemove handler: stores the client coordinates in MouseX/MouseY (fed to the mouse uniforms by render).
             * @param {MouseEvent} ev - Mouse event.
             * @returns {void}
             */
            function OnMouseMove(ev) {
                t.MouseX = ev.clientX;
                t.MouseY = ev.clientY;
            }
            mouseElement.addEventListener("mousemove", OnMouseMove);
            /**
             * Removes the mousemove listener installed by Create (called by OnDispose).
             * @returns {void}
             */
            t.RemoveMouseListener = () => mouseElement.removeEventListener("mousemove", OnMouseMove);
        }

    }



    /**
     * Loads the effect: fetches the .glsl source (mode "cors"), stores Etag and Src, runs Create and completes
     * through MediaPlayerTools.OnCacheComplete.
     * @param {boolean} [keepHidden] - Passed to OnCacheComplete (keep the element hidden after loading).
     * @returns {Promise<boolean>} false when the fetch status is not 200 (a warning is logged), else the
     * OnCacheComplete result.
     */
    async Cache(keepHidden) {
        const t = this;
        const c = t.Url;
        let src;
        const r = new Request(c, {
            method: "GET",
            mode: "cors",
            cache: "default",
        });
        const res = await fetch(r);
        //const contentType = res.headers.get('Content-Type');
        if (res.status != 200) {
            console.warn("Failed to load effect \"" + c + "\"");
            return false;
        }
        t.Etag = res.headers.get("etag");
        //const contentType = res.headers.get('Content-Type');


        t.Src = await res.text();


        await t.Create();

        return MediaPlayerTools.OnCacheComplete(this, keepHidden);
    }

    /**
     * Adapts the global DpiScale to a measured draw time: halves it (min 0.125) when timeElapsed > max, doubles
     * it (max 1) when timeElapsed < min. On change logs it and persists it to
     * localStorage["SysWeaver.Media.EffectDpiScale"].
     * @param {number} timeElapsed - Measured draw time in ms.
     * @param {number} max - Upper threshold in ms.
     * @param {number} min - Lower threshold in ms.
     * @returns {void}
     */
    static AdjustDpi(timeElapsed, max, min) {
        const oldDpi = MediaPlayerEffect.DpiScale;
        let newDpi = oldDpi;
        if (timeElapsed > max) {
            newDpi *= 0.5;
            if (newDpi < 0.125)
                newDpi = 0.125;
        }
        if (timeElapsed < min) {
            newDpi *= 2;
            if (newDpi > 1)
                newDpi = 1;
        }
        if (newDpi === oldDpi)
            return;
        console.log("Dpi Scale changed to: " + newDpi);
        MediaPlayerEffect.DpiScale = newDpi;
        localStorage.setItem("SysWeaver.Media.EffectDpiScale", "" + newDpi);
    }

    /**
     * Accumulates a frame-time measurement: keeps the minimum in TotalTime; every 32 measurements (every call
     * when Static) publishes it in AvgDrawTimeMs, calls AdjustDpi when Params.DpiAdjust (from the 2nd
     * publication on), logs every 8th publication when window.ShaderDebug, and resets the window.
     * @param {number} timeElapsed - Draw time in ms (ignored when <= 0).
     * @param {number} max - Upper threshold for AdjustDpi.
     * @param {number} min - Lower threshold for AdjustDpi.
     * @returns {void}
     */
    AccTime(timeElapsed, max, min) {
        if (timeElapsed <= 0)
            return;
        const t = this;
        const p = t.Params;
        let tot = t.TotalTime;
        if (timeElapsed < tot)
            tot = timeElapsed;
        const c = t.MeasureCount + 1;
        t.TotalTime = tot;
        t.MeasureCount = c;
        if ((c & 31) !== 0)
            if (!p.Static)
                return;
        t.AvgDrawTimeMs = tot;
        const pc = t.PrintCounter + 1;
        if (p.DpiAdjust)
            if (pc > 1)
                MediaPlayerEffect.AdjustDpi(t.AvgDrawTimeMs, max, min);
        t.PrintCounter = pc;
        if (window.ShaderDebug && ((pc & 7) === 0))
            console.log(t.Url + " - Average draw time: " + t.AvgDrawTimeMs + " ms [" + c + " measurements");
        t.MeasureCount = 0;
        t.TotalTime = 100000000000.0;
    }

    /** @type {HTMLElement|null} Element whose scroll position feeds the scroll uniform (set by Create). */
    ScrollElement = null;
    /** @type {number} Last mouse client x (set by OnMouseMove). */
    MouseX = 0.5;
    /** @type {number} Last mouse client y (set by OnMouseMove). */
    MouseY = 0.5;

    /**
     * Optional hook called before each draw, e.g. by mediaView/textEffect.js to upload extra uniforms.
     * @type {function(WebGLRenderingContext, MediaPlayerEffect):void|null}
     */
    OnRender = null;

    /** @type {Date} Fixed iDate value used when Params.Speed <= 0. */
    static ZeroDate = new Date("2001-01-01T11:37:42Z");

    /**
     * Draws one frame of player t (no-op without a Program).
     * With animTime (requestAnimationFrame timestamp) the time is computed from RenderTime/ResumeTime and the GPU
     * timer query is used. Computes the backing-store size (adaptive: client size x devicePixelRatio (min 0.25)
     * x Params.DpiScale x global DpiScale (when DpiAdjust and not Static); else Width/Height x DpiScale; LimitSize
     * clamps), resizes the canvas, skips repeated Static frames and invisible canvases (checkVisibility), sets
     * the viewport and all uniforms, re-applies the texture when ApplyTextue (plus WrapU / WrapV-or-WrapY repeat), calls
     * OnRender, draws and feeds AccTime (GPU query thresholds 12/2 ms, else CPU frame delta 35/17 ms).
     * The scroll ratios are computed as scrollLeft / Math.max(1.0, scrollWidth) (and likewise for Y), i.e. the
     * scroll position relative to the scrollable content size, guarded against division by zero.
     * @param {MediaPlayerEffect} t - The player.
     * @param {number} [animTime] - Animation timestamp in ms; undefined for a single (paused) render.
     * @returns {void}
     */
    static render(t, animTime) {
        if (!t.Program)
            return;
        let time = t.CurrentPosition;
        const isRunning = typeof animTime !== "undefined";
        const pq = isRunning ? t.Query : null;
        if (isRunning) {
            if (typeof t.RenderTime === "undefined")
                t.RenderTime = animTime;
            time = (animTime - t.RenderTime) * 0.001 + t.ResumeTime;
        }
        t.CurrentPosition = time;

        const e = t.Element;
        const p = t.Params;
        let dpi = window.devicePixelRatio;
        if (dpi <= 0.25)
            dpi = 0.25;
        let dpiS = p.DpiScale;
        if (p.DpiAdjust && (!p.Static))
            dpiS *= MediaPlayerEffect.DpiScale;
        dpi *= dpiS;
        const adaptive = p.AdaptiveSize;
        let w = Math.round(adaptive ? (t.ClientWidth * dpi) : (p.Width * dpiS)) | 0;
        let h = Math.round(adaptive ? (t.ClientHeight * dpi) : (p.Height * dpiS)) | 0;
        if (p.LimitSize) {
            const max = Math.max(p.Width, p.Height);
            const min = Math.min(p.Width, p.Height);
            const ow = w;
            const oh = h;
            if (w > h) {
                if (w > max) {
                    h = Math.round(max * oh / ow) | 0;
                    w = max;
                }
                if (h > min) {
                    w = Math.round(min * ow / oh) | 0;
                    h = min;
                }
            } else {
                if (w > min) {
                    h = Math.round(min * oh / ow) | 0;
                    w = min;
                }
                if (h > max) {
                    w = Math.round(max * ow / oh) | 0;
                    h = max;
                }
            }
        }
        if ((e.width !== w) || (e.height !== h)) {
            e.width = w;
            e.height = h;
            t.Rendered = false;
        }
        if (p.Static) {
            if (t.Rendered)
                if ((!t.Query) || (t.MeasureCount > 1))
                    return;
            //t.CurrentPosition = 0;
            //time = 0;
        }
        t.Rendered = true;
        if (!t.Element.checkVisibility(
            {
                opacityProperty: true,
                visibilityProperty: true,
            })) {
            return;
        }
        //console.log("Render: " + t.Url + " @ " + time);

        const gl = t.GL;
        gl.viewport(0, 0, w, h);
        t.UniformTime(time * p.Speed + p.Offset);
        t.UniformResolution(w, h);


        let scrollX = 0.0;
        let scrollY = 0.0;
        let scrollRX = 0.0;
        let scrollRY = 0.0;
        const scrollElement = t.ScrollElement;
        if (scrollElement) {
            const x = scrollElement.scrollLeft;
            const y = scrollElement.scrollTop;
            scrollX = x * dpi;
            scrollY = y * dpi;
            scrollRX = x / Math.max(1.0, scrollElement.scrollWidth);
            scrollRY = y / Math.max(1.0, scrollElement.scrollHeight);
        }
        t.UniformScroll(scrollX, scrollY, scrollRX, scrollRY);
        t.UniformMouse(t.MouseX * dpi, t.MouseY * dpi);
        t.UniformFrameIndex(t.FrameIndex);
		const darkMode = t.SmoothDark ? t.SmoothDark.ValueAt() : (t.IsDarkMode ? 1.0 : 0.0);
		const customX = t.CustomX.Get();
		const customY = t.CustomY.Get();
		const customZ = t.CustomZ.Get();
        t.UniformCustomDark(customX, customY, customZ, darkMode);
        ++t.FrameIndex;
		
        if (t.ApplyTextue) {
            t.ApplyTextue = false;
            const tex = t.Texture;
            if (tex) {
                const pr = t.Program.Program;
                const tu = gl.getUniformLocation(pr, "tex");
                const tuv = gl.getUniformLocation(pr, "texUv");
                const ts = gl.getUniformLocation(pr, "texSize");
                if (tu) {
                    gl.activeTexture(gl.TEXTURE0);
                    tex.Apply(tuv, ts);
                    gl.uniform1i(tu, 0);
                    if (p.WrapU)
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
                    if (p.WrapV || p.WrapY)
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
                }
            }
        }
        const date = p.Speed > 0 ? new Date() : MediaPlayerEffect.ZeroDate;
        t.UniformDate(date);
        /**
         * Draws the fullscreen triangle (3 vertices).
         * @returns {void}
         */
        const draw = () => {
            gl.drawArrays(gl.TRIANGLES, 0, 3);

        };

        const onr = t.OnRender;
        if (onr)
            onr(gl, t);
        if (pq) {
            const ext = pq.E;
            const query = pq.Q;
            if (!pq.IsQ) {
            //  New perf query 
                ext.beginQueryEXT(ext.TIME_ELAPSED_EXT, query);
                draw();
                ext.endQueryEXT(ext.TIME_ELAPSED_EXT);
                pq.IsQ = true;
            } else {
                //  Waiting for previous result to be available
                draw();
                const available = ext.getQueryObjectEXT(query, ext.QUERY_RESULT_AVAILABLE_EXT);
                const disjoint = gl.getParameter(ext.GPU_DISJOINT_EXT);
                if (available && !disjoint) {
                    const timeElapsed = ext.getQueryObjectEXT(query, ext.QUERY_RESULT_EXT) / 1000000;
                    t.AccTime(timeElapsed, 12, 2);
                    pq.IsQ = false;
                }
            }
        } else {
            //  No perf query
            const ts = performance.now();
            let pt = t.PrevTime;
            if (pt) {
                const timeElapsed = ts - pt;
                t.AccTime(timeElapsed, 35, 17);
            }
            t.PrevTime = ts;
            draw();
        }
    }

    /**
     * Recovers from a missing or lost WebGL context: ReleaseGL, replace the canvas in the DOM by a cloneNode()
     * copy (a canvas can't get a new context after loss) and re-run Create. No-op when the context is fine.
     * @returns {Promise<void>}
     */
    async Validate() {
        const t = this;
        const gl = t.GL;
        if (gl && (!gl.isContextLost()))
            return;
        const e = t.Element;
        t.ReleaseGL();
        const ne = e.cloneNode();
        if (e.parentNode)
            e.parentNode.replaceChild(ne, e);
        t.Element = ne;
        await t.Create();
    }


    /**
     * Starts/resumes the render loop (no-op when not paused): Validate, unpause, ResumeTime = CurrentPosition,
     * reset RenderTime and counters; unless an already rendered Static effect, sets IsAnimating and starts the
     * requestAnimationFrame loop.
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        await t.Validate();
        t.Paused = false;
        t.ResumeTime = t.CurrentPosition;
        delete t.RenderTime;
        t.ResetCounters();
        if (t.Params.Static && t.Rendered)
            return;
        t.IsAnimating = true;
        /**
         * One animation step: renders and re-schedules itself with requestAnimationFrame until paused (or the
         * Static frame is done), then clears IsAnimating.
         * @param {number} [animTime] - rAF timestamp (undefined on the first, direct call).
         * @returns {void}
         */
        function renderFrame(animTime) {
            MediaPlayerEffect.render(t, animTime);
            if (t.Paused || (t.Params.Static && t.Rendered)) {
                t.IsAnimating = false;
            } else {
                window.requestAnimationFrame(renderFrame);
            }
        }
        renderFrame();
    }

    /**
     * Pauses: sets Paused; the render loop stops on its next frame.
     * @returns {Promise<void>}
     */
    async Pause() {
        const t = this;
        if (t.Paused)
            return;
        t.Paused = true;
    }

    /**
     * Stops (no-op when already paused at position 0): pauses, waits for the loop to end, CurrentPosition = 0,
     * Validate and renders one frame.
     * @returns {Promise<void>}
     */
    async Stop() {
        const t = this;
        if (t.Paused && (t.CurrentPosition <= 0))
            return;
        t.Paused = true;
        while (t.IsAnimating)
            await MediaPlayerTools.delay(1);
        t.CurrentPosition = 0;
        await t.Validate();
        MediaPlayerEffect.render(t);
    }

    /**
     * @returns {number} Current effect time in seconds (before Speed/Offset are applied).
     */
    GetPos() {
        return this.CurrentPosition;
    }

    /**
     * Seeks to a time. Paused: sets the position, Validate and renders one frame. Playing: sets
     * ResumeTime = time - CurrentPosition and CurrentPosition = time (the next frame recomputes the time).
     * @param {number} time - Target time in seconds.
     * @returns {Promise<null|undefined>} null for a negative time, otherwise undefined.
     */
    async Seek(time) {
        if (time < 0)
            return null;
        const t = this;
        if (t.Paused) {
            t.CurrentPosition = time;
            t.Rendered = false;
            await t.Validate();
            MediaPlayerEffect.render(t);
        } else {
            t.ResumeTime = (time - t.CurrentPosition);
            t.CurrentPosition = time;
            t.Rendered = false;
        }

    }


}



/**
 * Texture provider for an image, created by the createTextureFn passed from MediaPlayer.Create
 * (Image + Effect) to MediaPlayerEffect, which calls it from InitGL. Duck-typed interface shared with
 * MediaPlayerTextTexture: Cache(haveLod), Apply(uv, size), ApplyClip(p), Dispose(), Width/Height.
 */
class MediaPlayerImageTexture {

    /**
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - Effect context.
     * @param {MediaPlayerParamsImage|object} params - The outer image params (Crop, EffectSizeFromImage, EffectParams...).
     * @param {string} url - Image URL.
     */
    constructor(gl, params, url) {
        const t = this;
        const texture = gl.createTexture();
        t.Params = params;
        t.Url = url;
        t.GL = gl;
        t.Texture = texture;
    }

    /**
     * Loads the image (MediaPlayerTools.waitEvent2), stores Image/Width/Height and uploads it flipped-Y with
     * clamp-to-edge wrapping and linear filtering. Generates mipmaps (LINEAR_MIPMAP_LINEAR) when haveLod or
     * Params.EffectParams.MipMap; WebGL1 requires power-of-two sizes, otherwise a warning is logged and mipmaps
     * are skipped.
     * @param {boolean} haveLod - The effect uses texture2DLod.
     * @returns {Promise<Array<number>|null|false>} [w,h] when Params.EffectSizeFromImage, null otherwise,
     * false if the image failed to load.
     */
    async Cache(haveLod) {
        const t = this;
        const gl = t.GL;
        const texture = t.Texture;
        const image = new Image();
        const res = await MediaPlayerTools.waitEvent2(image, "load", "error", () => image.src = t.Url);
        if (res.type == "error")
            return false;
        t.Image = image;
        t.Width = image.width;
        t.Height = image.height;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        const tp = t.Params;
        const ep = tp.EffectParams;
        while (haveLod || (ep && ep.MipMap)) {
            if (!(gl instanceof WebGL2RenderingContext)) {
                /**
                 * Power-of-two test.
                 * @param {number} v - Value.
                 * @returns {boolean} True if v is a power of two.
                 */
                function powerOf2(v) {
                    return v && !(v & (v - 1));
                }
                if (!powerOf2(image.width)) {
                    console.warn("Need to enabled WebGL2 to have mipmaps of images then has a non power of two dimension, width = " + image.width);
                    break;
                }
                if (!powerOf2(image.height)) {
                    console.warn("Need to enabled WebGL2 to have mipmaps of images then has a non power of two dimension, height = " + image.height);
                    break;
                }
            }
            gl.generateMipmap(gl.TEXTURE_2D);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
            break;
        }
        if (!tp.EffectSizeFromImage)
            return null;
        return [image.width, image.height];
    }

    /**
     * Binds the texture and sets the uv transform and size uniforms: with a clip, texUv = (UvA, UvB, UvC, UvD)
     * and texSize = clip size; otherwise texUv = (1, -1, 0, 1) and texSize = image size.
     * @param {WebGLUniformLocation|null} uvScaleAndOffset - texUv uniform (vec4 scale.xy, offset.xy).
     * @param {WebGLUniformLocation|null} texSize - texSize uniform (vec2).
     * @returns {void}
     */
    Apply(uvScaleAndOffset, texSize) {

        const t = this;
        const gl = t.GL;
        const texture = t.Texture;
        gl.bindTexture(gl.TEXTURE_2D, texture);
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
     * Converts a crop into a UV scale/offset (UvA..UvD) using MediaPlayerTools.ComputeClip (which sets
     * ClipX/ClipB/ClipWidth/ClipHeight); HaveClip tells Apply whether to use it.
     * @param {object} [p] - Params with a Crop (defaults to this provider's Params).
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
