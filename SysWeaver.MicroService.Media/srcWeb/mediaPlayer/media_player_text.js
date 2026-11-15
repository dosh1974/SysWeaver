/**
 * @file media_player_text.js - stylised text player (single text or a collage grid) and its WebGL texture provider.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Text). Must be loaded after canvasTools.js because MediaPlayerParamsText extends TextStyle
 * (MediaPlayer.Init loads canvasTools.js eagerly for this reason).
 * Globals defined: MediaPlayerParamsText, MediaPlayerText, MediaPlayerTextTexture.
 * Dependencies: TextStyle and CanvasTools (canvasTools.js), MediaPlayerTools (media_player_tools.js), the
 * MediaPlayerTools.waitEvent2 and LoadFont (the font url is parsed and escaped, never pasted into CSS), and the browser FontFace / document.fonts API for web fonts.
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
 * CanvasTools and shows it in an <img>, with the timer-based virtual timeline of Params.Duration seconds of
 * MediaPlayerPicture (the same as MediaPlayerImage). Created by MediaPlayer.Create(MediaTypes.Text, text, params) when no
 * Effect is set. Lifecycle: constructor -> Cache -> Play/Pause/Seek/... -> OnDispose (InitBase default, calls Stop).
 * Url/Source is the text.
 */
class MediaPlayerText extends MediaPlayerPicture {

    /**
     * Creates the player and its <img> element (nothing rendered until Cache; see MediaPlayerPicture).
     * @param {string} url - The text to render (named url for symmetry with the other players).
     * @param {MediaPlayerParamsText|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const cp = new MediaPlayerParamsText();
        if (params)
            Object.assign(cp, params);
        super(url, cp);
    }

    /**
     * Renders the text with the style of the player: loads the web font (LoadFontOrFallback) and draws a collage grid or a
     * single text. Shared by MediaPlayerText (a blob URL) and MediaPlayerTextTexture (a canvas).
     * When FontUrl is set the font is loaded with MediaPlayerTools.LoadFontOrFallback (parsed, http(s) only, escaped; on a
     * failure a default font is used and the error is kept in t.FontError).
     * With CollageSeparator the text is split and rendered as a grid (CanvasTools.CreateTextGrid*; the grid stats are written to
     * EffectParams.FxProps, both created when missing), otherwise as one text (CanvasTools.CreateTextImage*).
     * @param {MediaPlayerText|MediaPlayerTextTexture} t - The player or texture provider (Params, Url; FontError is set).
     * @param {boolean} asUrl - True: a WebP blob URL (CreateText*Url), false: a canvas (CreateText*).
     * @returns {Promise<string|HTMLCanvasElement>} The blob URL or the canvas.
     */
    static async Render(t, asUrl) {
        const p = t.Params;
        /** {Error|null} The web font error of the last Cache (a default font was used), null when none. */
        t.FontError = await MediaPlayerTools.LoadFontOrFallback(p);
        if (!p.CollageSeparator)
            return asUrl ? CanvasTools.CreateTextImageUrl(t.Url, p) : CanvasTools.CreateTextImage(t.Url, p);
        const texts = t.Url.split(p.CollageSeparator);
        const ep = p.EffectParams || (p.EffectParams = {});
        const fx = ep.FxProps || (ep.FxProps = {});
        return asUrl ? CanvasTools.CreateTextGridUrl(texts, p, p.CollageColumns, fx) : CanvasTools.CreateTextGrid(texts, p, p.CollageColumns, fx);
    }

    /**
     * Renders the text (Render) and loads it into the <img>: sets src and waits for "load"/"error", logs the URL when
     * DebugImage, then the rest as MediaPlayerPicture.ShowImage (Width/Height, AspectRatio, ApplyClip, OnCacheComplete).
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false on an image load error.
     */
    async Cache(keepHidden) {
        const t = this;
        const url = await MediaPlayerText.Render(t, true);
        return t.ShowImage(url, keepHidden, e => {
            if (t.Params.DebugImage)
                console.log("Text image: " + e.src);
        });
    }
}


/**
 * Texture provider that renders text to a canvas and uploads it as a WebGL texture for MediaPlayerEffect
 * (used when a Text media has an Effect: MediaPlayer.Create passes `gl => new MediaPlayerTextTexture(gl, params, text)`
 * as createTextureFn). The provider interface and the upload are those of MediaPlayerTexture (media_player_tools.js); only the
 * way the image is produced differs from MediaPlayerImageTexture. Extra instance fields: FontError, Image (debug <img>, only
 * with DebugImage).
 */
class MediaPlayerTextTexture extends MediaPlayerTexture {

    /**
     * Creates the provider and an (empty) GL texture.
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - The effect's GL context.
     * @param {MediaPlayerParamsText|object} params - The text params (raw object as passed to MediaPlayer.Create; merged over the text defaults, as MediaPlayerText does: a link with only some style members still draws; EffectParams stays the same object).
     * @param {string} url - The text to render.
     */
    constructor(gl, params, url) {
        super(gl, Object.assign(new MediaPlayerParamsText(), params), url);
    }

    /**
     * Renders the text (MediaPlayerText.Render: the font, a collage grid or a single text, to a canvas) and uploads it to the
     * texture (MediaPlayerTexture.Upload). With DebugImage it also creates a blob URL <img> (stored in Image) and logs it.
     * @param {boolean} haveLod - True when the shader uses texture2DLod (mipmaps required).
     * @returns {Promise<number[]|null>} [width, height] of the image when EffectSizeFromImage, else null.
     */
    async Cache(haveLod) {
        const t = this;
        const image = await MediaPlayerText.Render(t, false);
        if (t.Params.DebugImage) {
            const c = await CanvasTools.CreateCanvasImageUrl(image);
            const e = new Image();
            const res = await MediaPlayerTools.waitEvent2(e, "load", "error", () => {
                e.src = c[0];
            });
            if (res.type !== "error") {
                t.Image = e;
                console.log("Text image: " + e.src);
            }
        }
        return t.Upload(image, haveLod);
    }
}

/** Publish the classes of this file under the new namespace Sw.Media (the global class names stay the public legacy API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaPlayerParamsText = MediaPlayerParamsText;
    m.MediaPlayerText = MediaPlayerText;
    m.MediaPlayerTextTexture = MediaPlayerTextTexture;
})();
