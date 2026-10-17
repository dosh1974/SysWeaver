/**
 * @file media_player_text.js - stylised text player (single text or a collage grid) and its WebGL texture provider.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Text). Must be loaded after canvasTools.js because MediaPlayerParamsText extends TextStyle
 * (MediaPlayer.Init loads canvasTools.js eagerly for this reason).
 * Globals defined: MediaPlayerParamsText, MediaPlayerText, MediaPlayerTextTexture.
 * Dependencies: TextStyle and CanvasTools (canvasTools.js), MediaPlayerTools (media_player_tools.js), the
 * common.js global waitEvent2, and the browser FontFace / document.fonts API for web fonts.
 * Note: the timeline methods of MediaPlayerText are copies of MediaPlayerImage's, and MediaPlayerTextTexture
 * shares its upload/mipmap/clip/apply code with MediaPlayerImageTexture (media_player_effect.js).
 * Blob URLs created for the text images are never revoked.
 */

/**
 * Parameters of a text player: all TextStyle fields (font, letter spacing, fill/stroke colors or gradients,
 * shadows, margins, ...; see canvasTools.js, including FontUrl / FontUrlName used by Cache) plus the fields below.
 * The field initializers are the defaults; MediaPlayerText merges the supplied params over a new instance.
 * When Effect is set, MediaPlayer.Create passes the raw params object to MediaPlayerTextTexture instead.
 * @extends TextStyle
 */
class MediaPlayerParamsText extends TextStyle {
    /**
     * When set (non-empty), the text is split on this separator and the parts are rendered as a grid
     * (CanvasTools.CreateTextGrid*), with grid stats written into EffectParams.FxProps. null = single text.
     * @type {string|null}
     */
    CollageSeparator = null;
    /**
     * Number of grid columns for a collage (only used when CollageSeparator is set).
     * @type {number}
     */
    CollageColumns = 2;
    /**
     * Optional crop (C# MediaCrop): pixel margins MarginTop/Right/Bottom/Left, or percent margins Margin*P when
     * MarginAsPercentage is true. null = no crop. See MediaPlayerTools.ComputeClip.
     * @type {object|null}
     */
    Crop = null;
    /**
     * Display duration in seconds (length of the virtual timeline); <= 0 means infinite / still.
     * @type {number}
     */
    Duration = 10;
    /**
     * URL of a .glsl effect; when set, MediaPlayer.Create builds a MediaPlayerEffect with a MediaPlayerTextTexture
     * as texture provider instead of a MediaPlayerText. null = plain text image.
     * @type {string|null}
     */
    Effect = null;
    /**
     * Parameters for the Effect (MediaPlayerParamsEffect values); becomes the effect player's Params. For a collage
     * it is created when missing and receives FxProps {TileCountX/Y, TileWidth/Height, PageWidth/Height,
     * ImageWidth/Height, Border, UniqueCount} so the shader gets the grid layout as //var: constants.
     * @type {object|null}
     */
    EffectParams = null;
    /**
     * When an Effect is used: true makes the effect canvas take the text image size (MediaPlayerTextTexture.Cache
     * returns [w,h]); false keeps the effect's own size.
     * @type {boolean}
     */
    EffectSizeFromImage = true;
    /**
     * Whether the output has transparency (mirrors C#); not read by MediaPlayerText itself.
     * @type {boolean}
     */
    Transparent = false;
    /**
     * When true the <img> uses object-fit: contain, AspectRatio is set and MediaPlayerTools.UpdateSize sizes the
     * element to its container.
     * @type {boolean}
     */
    AdaptiveSize = false;
    /**
     * Debug aid: logs the generated text image URL to the console (the texture provider also keeps an <img> of it
     * in its Image field).
     * @type {boolean}
     */
    DebugImage = false;
}

/**
 * Text player: renders the text (data is the text itself, kept in Url/Source) into an image blob URL with
 * CanvasTools and shows it in an <img>, with a timer-based virtual timeline of Params.Duration seconds
 * (identical to MediaPlayerImage). Created by MediaPlayer.Create(MediaTypes.Text, text, params) when no Effect
 * is set. Lifecycle: constructor -> Cache -> Play/Pause/Seek/... -> OnDispose (InitBase default, calls Stop).
 * Instance fields: Params, Url/Source (the text), CurrentPosition, StartedAt, TimeFn, AspectRatio (as in
 * MediaPlayerImage). Other state comes from MediaPlayerTools.InitBase.
 */
class MediaPlayerText {




    /**
     * Creates the player and its <img> element (nothing rendered until Cache).
     * Sets object-fit: contain when AdaptiveSize, HaveVisual, CanPlay, calls MediaPlayerTools.InitBase,
     * Url = Source = text, CurrentPosition = 0, Duration = Params.Duration.
     * @param {string} url - The text to render (named url for symmetry with the other players).
     * @param {MediaPlayerParamsText|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsText();
        if (params)
            Object.assign(cp, params);
        params = cp;
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
     * Renders the text and loads it into the <img>.
     * When FontUrl is set, loads the font as FontFace(FontUrlName, url(FontUrl)) and adds it to document.fonts.
     * With CollageSeparator the text is split and rendered by CanvasTools.CreateTextGridUrl (grid stats written to
     * EffectParams.FxProps, both created when missing), otherwise by CanvasTools.CreateTextImageUrl (WebP blob URL).
     * Then sets src and waits for "load"/"error", logs the URL when DebugImage, reads naturalWidth/Height into
     * Width/Height (they keep their previous values until the image has loaded), sets AspectRatio
     * when AdaptiveSize, calls ApplyClip() and MediaPlayerTools.OnCacheComplete.
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false on an image load error.
     */
    async Cache(keepHidden) {

        const t = this;
        const p = t.Params;
        if (p.FontUrl) {
            const ff = new FontFace(p.FontUrlName, "url('" + p.FontUrl + "')");
            await ff.load();
            document.fonts.add(ff);
        }
        let c;
        if (p.CollageSeparator) {
            const texts = t.Url.split(p.CollageSeparator);
            let ep = p.EffectParams;
            if (!ep) {
                ep = {};
                p.EffectParams = ep;
            }
            let fx = ep.FxProps;
            if (!fx) {
                fx = {};
                ep.FxProps = fx;
            }
            c = await CanvasTools.CreateTextGridUrl(texts, p, p.CollageColumns, fx);
        } else {
            c = await CanvasTools.CreateTextImageUrl(t.Url, p);
        }
        const e = t.Element;
        const res = await waitEvent2(e, "load", "error", () => {
            e.src = c;
        });
        if (res.type == "error")
            return false;
        if (p.DebugImage)
            console.log("Text image: " + e.src);
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
     * Starts the virtual timeline from CurrentPosition. No-op when already playing. Same as MediaPlayerImage.Play.
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
 * Texture provider that renders text to a canvas and uploads it as a WebGL texture for MediaPlayerEffect
 * (used when a Text media has an Effect: MediaPlayer.Create passes `gl => new MediaPlayerTextTexture(gl, params, text)`
 * as createTextureFn). Duck-typed provider interface: Cache(haveLod), Apply(uv, size), ApplyClip(p), Dispose(),
 * Width/Height. Identical to MediaPlayerImageTexture except for how the image is produced.
 * Instance fields: Params (the text params, MediaPlayerParamsText shape), Url (the text), GL, Texture (WebGLTexture),
 * Width/Height (image size), Image (debug <img>, only with DebugImage), HaveClip, Clip* (from ComputeClip),
 * UvA..UvD (uv scale x/y and offset x/y for the crop).
 */
class MediaPlayerTextTexture {

    /**
     * Creates the provider and an (empty) GL texture.
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - The effect's GL context.
     * @param {MediaPlayerParamsText|object} params - The text params (raw object as passed to MediaPlayer.Create; not merged with defaults).
     * @param {string} url - The text to render.
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
     * Renders the text and uploads it to the texture.
     * Loads FontUrl as a FontFace when set; renders a collage grid (CanvasTools.CreateTextGrid, stats into
     * EffectParams.FxProps, created when missing) or a single text (CanvasTools.CreateTextImage) to a canvas;
     * with DebugImage also creates a blob URL <img> (stored in Image) and logs it. Uploads the canvas flipped-Y with
     * clamp-to-edge wrapping and linear filtering. When haveLod or EffectParams.MipMap, generates mipmaps
     * (LINEAR_MIPMAP_LINEAR); on WebGL1 only for power-of-two sizes (otherwise warns and skips).
     * @param {boolean} haveLod - True when the shader uses texture2DLod (mipmaps required).
     * @returns {Promise<number[]|null>} [width, height] of the image when EffectSizeFromImage, else null.
     */
    async Cache(haveLod) {
        const t = this;
        const gl = t.GL;
        const texture = t.Texture;
        const p = t.Params;
        if (p.FontUrl) {
            const ff = new FontFace(p.FontUrlName, "url('" + p.FontUrl + "')");
            await ff.load();
            document.fonts.add(ff);
        }
        let image;
        if (p.CollageSeparator) {
            const texts = t.Url.split(p.CollageSeparator);
            let ep = p.EffectParams;
            if (!ep) {
                ep = {};
                p.EffectParams = ep;
            }
            let fx = ep.FxProps;
            if (!fx) {
                fx = {};
                ep.FxProps = fx;
            }
            image = await CanvasTools.CreateTextGrid(texts, p, p.CollageColumns, fx);
        } else {
            image = await CanvasTools.CreateTextImage(t.Url, p);
        }
        if (p.DebugImage) {
            const c = await CanvasTools.CreateCanvasImageUrl(image);
            const e = new Image();
            const res = await waitEvent2(e, "load", "error", () => {
                e.src = c[0];
            });
            if (res.type !== "error") {
                t.Image = e;
                console.log("Text image: " + e.src);
            }
        }
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
                 * Tests whether a number is a power of two.
                 * @param {number} v - The value (a texture dimension).
                 * @returns {boolean|number} Truthy when v is a non-zero power of two (returns v itself when v is 0).
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
     * Binds the texture and sets the crop uniforms (called by MediaPlayerEffect each frame).
     * With a clip: texUv = (UvA, UvB, UvC, UvD) and texSize = clip size; without: texUv = (1, -1, 0, 1) (flip only)
     * and texSize = full image size.
     * @param {WebGLUniformLocation|null} uvScaleAndOffset - Location of the vec4 "texUv" uniform (scale.xy, offset.xy), may be null.
     * @param {WebGLUniformLocation|null} texSize - Location of the vec2 "texSize" uniform, may be null.
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
     * Computes the crop as UV scale/offset: runs MediaPlayerTools.ComputeClip and, when clipped, sets HaveClip and
     * UvA = ClipWidth/Width, UvB = -ClipHeight/Height, UvC = ClipX/Width, UvD = (Height - ClipB)/Height.
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
