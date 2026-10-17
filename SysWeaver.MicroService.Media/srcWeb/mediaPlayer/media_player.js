
/**
 * @file media_player.js - entry point of the SysWeaver media player.
 *
 * Purpose: defines the media type ids (MediaTypes) and the player factory/loader (MediaPlayer).
 * Loading: classic (non-module) script, included by mediaView/media.js and mediaView/textEffect.js via
 * common.js `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")`.
 * At load it runs `MediaPlayer.Init()` (not awaited, last line of the file), which includes
 * media_player_tools.js and canvasTools.js relative to this script's URL (canvasTools.js must be loaded
 * before media_player_text.js because MediaPlayerParamsText extends TextStyle).
 * Globals defined: MediaTypes, MediaPlayer.
 * Globals used: includeJs (common/common.js), window.MediaLoadAll (optional input flag set by the host page:
 * when truthy all player scripts are loaded eagerly in Init and lazy loading is skipped), and the player
 * classes from the media_player_*.js files (MediaPlayerImage, MediaPlayerVideo, MediaPlayerAudio,
 * MediaPlayerYoutube, MediaPlayerEffect, MediaPlayerText, MediaPlayerImageTexture, MediaPlayerTextTexture,
 * EffectProgramData, MediaPlayerTools).
 * Conventions: every player is a standalone class whose params class (MediaPlayerParams*) mirrors a C# type
 * in SysWeaver.MicroService.Media/Media; a player is only usable after `await player.Cache(keepHidden)`.
 */

/**
 * Static holder of the media type ids, mirroring the C# MediaTypes enum (JS adds Text = 5; the C# MediaVimeo
 * has no JS player). Never instantiated.
 */
class MediaTypes {

    /** Media is an image file */
    static Image = 0;
    /** Media is a video file */
    static Video = 1;
    /** Media is an audio file */
    static Audio = 2;
    /** Media is a youtube clip */
    static YouTube = 3;
    /** Media is a pixel shader effect, from GlslSandbox or ShaderToy (must be a pure shader with no inputs or multiple satges) */
    static Effect = 4;
    /** Media is some stylized text, optionally with an effect */
    static Text = 5;


    /** Array with names */
    static Names = Object.freeze(
        [
            "Image",
            "Video",
            "Audio",
            "YouTube",
            "Effect",
            "Text",
        ]);

    /**
     * Get the C# type that matches the data (parameters) expected by a media type
     * @param {number} mediaType One of the MediaTypes types
     * @returns {string} The C# type name ("SysWeaver.MicroService.Media.Media" + MediaTypes.Names[mediaType]).
     */
    static GetTypeName(mediaType) {
        return "SysWeaver.MicroService.Media.Media" + MediaTypes.Names[mediaType];
    }


}

/**
 * Static script loader and media player factory. Never instantiated.
 * Usage: `const p = await MediaPlayer.Create(type, data, params); host.appendChild(p.Element); await p.Cache();`
 * then `MediaPlayerTools.UpdateSize(...)`, `Play()`, ..., `OnDispose()`.
 */
class MediaPlayer {

    /**
     * Initializes the media player scripts; called once at the end of this file (not awaited).
     * Stores document.currentScript.src in MediaPlayer.CurrentUrl and includes media_player_tools.js and
     * canvasTools.js (in parallel) relative to it. When window.MediaLoadAll is truthy it also includes
     * media_effect_program.js and every media_player_*.js file, then awaits MediaPlayerTools.CanUnMute()
     * (the autoplay-with-sound probe).
     * @returns {Promise<void>} Resolves when the scripts are loaded.
     */
    static async Init() {
        const current = document.currentScript.src;
        MediaPlayer.CurrentUrl = current;
        await Promise.all([
            includeJs(current, "media_player_tools.js"),
            includeJs(current, "canvasTools.js"),
        ]);
        if (window.MediaLoadAll) {
            await Promise.all([
                includeJs(current, "media_effect_program.js"),
                includeJs(current, "media_player_image.js"),
                includeJs(current, "media_player_audio.js"),
                includeJs(current, "media_player_video.js"),
                includeJs(current, "media_player_youtube.js"),
                includeJs(current, "media_player_effect.js"),
                includeJs(current, "media_player_text.js"),
            ]
            );
            await MediaPlayerTools.CanUnMute();
        }
    }

    /**
     * URL of this script (media_player.js), set by Init; base URL for all relative includeJs calls.
     * @type {string}
     */
    static CurrentUrl;

    /**
     * Create a player for some media
     * Unless window.MediaLoadAll is set, the scripts needed for the type are loaded lazily with includeJs first.
     * Mapping: Image -> MediaPlayerImage (or, when mediaParams.Effect is set, a MediaPlayerEffect for
     * mediaParams.Effect / mediaParams.EffectParams with a MediaPlayerImageTexture as texture provider);
     * Video -> MediaPlayerVideo; Audio -> MediaPlayerAudio; YouTube -> MediaPlayerYoutube; Effect -> MediaPlayerEffect;
     * Text -> MediaPlayerText (or MediaPlayerEffect + MediaPlayerTextTexture when mediaParams.Effect is set).
     * In the "with effect" variants the returned player's Params are the effect params, not mediaParams.
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id, for text the text itself etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {Promise<object|null>} A media player object, null for an unknown type.
     */
    static async Create(type, data, mediaParams) {
        const current = MediaPlayer.CurrentUrl;
        const inc = !window.MediaLoadAll;
        switch (type) {
            case MediaTypes.Image:
                if (mediaParams && mediaParams.Effect) {
                    if (inc)
                        await Promise.all([includeJs(current, "media_player_effect.js"), includeJs(current, "media_effect_program.js")]);
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerImageTexture(gl, mediaParams, data));
                }
                if (inc)
                    await includeJs(current, "media_player_image.js");
                return new MediaPlayerImage(data, mediaParams);
            case MediaTypes.Video:
                {
                    if (inc)
                        await includeJs(current, "media_player_video.js");
                    return new MediaPlayerVideo(data, mediaParams);
                }
            case MediaTypes.Audio:
                {
                    if (inc)
                        await includeJs(current, "media_player_audio.js");
                    return new MediaPlayerAudio(data, mediaParams);
                }
            case MediaTypes.YouTube:
                {
                    if (inc)
                        await includeJs(current, "media_player_youtube.js");
                    return new MediaPlayerYoutube(data, mediaParams);
                }
            case MediaTypes.Effect:
                {
                    if (inc)
                        await Promise.all([includeJs(current, "media_player_effect.js"), includeJs(current, "media_effect_program.js")]);
                    return new MediaPlayerEffect(data, mediaParams);
                }
            case MediaTypes.Text:
                if (mediaParams && mediaParams.Effect) {
                    if (inc)
                        await Promise.all([includeJs(current, "media_player_effect.js"), includeJs(current, "media_effect_program.js"), includeJs(current, "media_player_text.js")]);
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerTextTexture(gl, mediaParams, data));
                }
                if (inc)
                    await includeJs(current, "media_player_text.js");
                return new MediaPlayerText(data, mediaParams);
        }
        return null;
    }

    /**
     * Create a player for some media
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {object|null} A media player object, null for an unknown type.
     * Same dispatch as Create but loads nothing. When window.MediaLoadAll is not set it checks with
     * `typeof X === "function"` that the needed classes exist and throws
     * Error("Must use CreateAsync when lazy loading (or perform manual init)!") otherwise.
     */
    static CreateSync(type, data, mediaParams) {
        const valid = !window.MediaLoadAll;
        switch (type) {
            case MediaTypes.Image:
                if (mediaParams && mediaParams.Effect) {
                    if (valid)
                        if ((typeof MediaPlayerEffect !== "function") || (typeof EffectProgramData !== "function"))
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerImageTexture(gl, mediaParams, data));
                }
                if (valid)
                    if (typeof MediaPlayerImage !== "function")
                        throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                return new MediaPlayerImage(data, mediaParams);
            case MediaTypes.Video:
                {
                    if (valid)
                        if (typeof MediaPlayerVideo !== "function")
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerVideo(data, mediaParams);
                }
            case MediaTypes.Audio:
                {
                    if (valid)
                        if (typeof MediaPlayerAudio !== "function")
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerAudio(data, mediaParams);
                }
            case MediaTypes.YouTube:
                {
                    if (valid)
                        if (typeof MediaPlayerYoutube !== "function")
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerYoutube(data, mediaParams);
                }
            case MediaTypes.Effect:
                {
                    if (valid)
                        if ((typeof MediaPlayerEffect !== "function") || (typeof EffectProgramData !== "function"))
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerEffect(data, mediaParams);
                }
            case MediaTypes.Text:
                if (mediaParams && mediaParams.Effect) {
                    if (valid)
                        if ((typeof MediaPlayerEffect !== "function") || (typeof EffectProgramData !== "function") || (typeof MediaPlayerText !== "function"))
                            throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerTextTexture(gl, mediaParams, data));
                }
                if (valid)
                    if (typeof MediaPlayerText !== "function")
                        throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
                return new MediaPlayerText(data, mediaParams);
        }
        return null;
    }





}

// Load the shared helper scripts immediately (fire and forget).
MediaPlayer.Init();
