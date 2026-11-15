


/**
 * @file media_player_image.js - static image player with a timer-based virtual timeline.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Image without an Effect; with an Effect, MediaPlayerEffect + MediaPlayerImageTexture are used instead).
 * Globals defined: MediaPlayerParamsImage, MediaPlayerImage.
 * Dependencies: MediaPlayerPicture (media_player_tools.js: the <img> element, the shared Cache tail and the timer based
 * timeline, also the base of MediaPlayerText) and MediaPlayerTools (InitBase, ApplyClip default, OnCacheComplete).
 */

/**
 * Parameters of an image player; mirrors the C# type MediaImage (SysWeaver.MicroService.Media.MediaImage).
 * The field initializers are the defaults; MediaPlayerImage merges the supplied params over a new instance.
 * Also used as the params of MediaPlayerImageTexture when an Effect is set.
 */
class MediaPlayerParamsImage {
    /**
     * Optional crop (C# MediaCrop): {MarginTop, MarginRight, MarginBottom, MarginLeft} in pixels, or, when
     * MarginAsPercentage is true, {MarginTopP, MarginRightP, MarginBottomP, MarginLeftP} in percent.
     * null = no crop. See MediaPlayerTools.ComputeClip.
     * @type {object|null}
     */
    Crop = null;
    /**
     * Display duration in seconds (length of the virtual timeline); <= 0 means infinite / still image.
     * @type {number}
     */
    Duration = 10;
    /**
     * URL of a .glsl effect; when set, MediaPlayer.Create builds a MediaPlayerEffect that uses this image as
     * texture (MediaPlayerImageTexture) instead of a MediaPlayerImage. null = plain image.
     * @type {string|null}
     */
    Effect = null;
    /**
     * Parameters for the Effect (MediaPlayerParamsEffect values); becomes the effect player's Params. null = defaults.
     * @type {object|null}
     */
    EffectParams = null;
    /**
     * When an Effect is used: true makes the effect canvas take the (cropped) image size
     * (MediaPlayerImageTexture.Cache returns [w,h]); false keeps the effect's own size.
     * @type {boolean}
     */
    EffectSizeFromImage = true;
    /**
     * Whether the image has transparency (mirrors C#); not read by MediaPlayerImage itself.
     * @type {boolean}
     */
    Transparent = false;
    /**
     * When true the <img> uses object-fit: contain, AspectRatio is set and MediaPlayerTools.UpdateSize sizes the
     * element to its container (no transform). The constructor pre-sets it to true for ".svg" URLs before
     * applying the supplied params, so the class default false only applies to non-SVG images.
     * @type {boolean}
     */
    AdaptiveSize = false;
}

/**
 * Image player: shows an <img> and simulates a timeline of Params.Duration seconds with setTimeout.
 * Created by MediaPlayer.Create(MediaTypes.Image, url, params) when no Effect is given.
 * Lifecycle: constructor -> Cache -> Play/Pause/Seek/... (the timeline of MediaPlayerPicture) -> OnDispose (InitBase
 * default, calls Stop).
 */
class MediaPlayerImage extends MediaPlayerPicture {

    /**
     * Creates the player and its <img> element (not loaded until Cache).
     * AdaptiveSize defaults to url.endsWith(".svg") before params are merged; when adaptive the element gets
     * object-fit: contain. Sets IsStill (MediaPlayerPicture does the rest).
     * @param {string} url - URL of the image.
     * @param {MediaPlayerParamsImage|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const cp = new MediaPlayerParamsImage();
        cp.AdaptiveSize = url.endsWith(".svg");
        if (params)
            Object.assign(cp, params);
        super(url, cp);
        /**
         * @type {boolean} A still picture: the timeline (Duration) only times slideshows / playlists, so viewers (mediaView
         * media.js) show no play position, duration or transport buttons for it.
         */
        this.IsStill = true;
    }

    /**
     * Loads the image (sets src, waits for "load"/"error", reads Width/Height, AspectRatio when AdaptiveSize, ApplyClip,
     * OnCacheComplete: see MediaPlayerPicture.ShowImage).
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false on a load error.
     */
    async Cache(keepHidden) {
        return this.ShowImage(this.Url, keepHidden);
    }
}





/** Publish the classes of this file under the new namespace Sw.Media (the global class names stay the public legacy API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaPlayerParamsImage = MediaPlayerParamsImage;
    m.MediaPlayerImage = MediaPlayerImage;
})();
