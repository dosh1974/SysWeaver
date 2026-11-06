/**
 * @file media_player.js - entry point of the SysWeaver media player library.
 *
 * Purpose: defines the media type ids (MediaTypes) and the player factory / script loader (MediaPlayer), and
 * publishes the library under the new namespace `Sw.Media` (Types, Player, Tools and the player classes as they load).
 *
 * Loading: a classic (non-module) script. Hosts include it with a static `<script defer src=".../mediaPlayer/media_player.js">`
 * or dynamically (`Sw.Net.IncludeJs`, the legacy `includeJs`, or `MediaPlayer.Include` from another library file).
 * At load it runs `MediaPlayer.Init()` (not awaited, stored in MediaPlayer.Ready), which loads media_player_tools.js and
 * canvasTools.js relative to this script (canvasTools.js must be loaded before media_player_text.js because
 * MediaPlayerParamsText extends TextStyle).
 * The library does NOT need the SysWeaver core: it works on lean pages (effect backgrounds, screenshot hosts) as well as
 * on new-format pages (sw/core.js) and old-format pages (common/common.js). When the core is present its de-duplicating
 * loader `Sw.Net.IncludeJs` is used, otherwise an equivalent built in loader.
 *
 * Globals defined (public API, kept for host pages): MediaTypes, MediaPlayer; new names
 * `Sw.Media.Types`, `Sw.Media.Player`. Input flag: window.MediaLoadAll (truthy = load every player script up front in Init).
 * Conventions: every player is a standalone class whose params class (MediaPlayerParams*) mirrors a C# type in
 * SysWeaver.MicroService.Media/Media; a player is only usable after `await player.Cache(keepHidden)`.
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
    /** Media is a pixel shader effect, from GlslSandbox or ShaderToy (must be a pure shader with no inputs or multiple stages) */
    static Effect = 4;
    /** Media is some stylized text, optionally with an effect */
    static Text = 5;

    /** Array with names (index = type id) */
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
     * URL of this script (media_player.js), set at load; base URL of all relative includes.
     * @type {string}
     */
    static CurrentUrl = document.currentScript ? document.currentScript.src : "";

    /**
     * Resolves when Init is done (media_player_tools.js and canvasTools.js are loaded). Create awaits it.
     * @type {Promise<void>|null}
     */
    static Ready = null;

    /** @type {Map<string, Promise<boolean>>} Built in loader: absolute script url -> load promise. */
    static #Loads = new Map();

    /**
     * The scripts each media type needs, and the global classes that must exist after loading them (checked by CreateSync).
     * Key = MediaTypes id; "<id>Fx" = an image / text with an effect. Text also needs canvasTools.js (loaded by Init).
     * @type {Object<string, {Files: string[], Classes: string[]}>}
     */
    static #Needs = {
        0: { Files: ["media_player_image.js"], Classes: ["MediaPlayerImage"] },
        1: { Files: ["media_player_video.js"], Classes: ["MediaPlayerVideo"] },
        2: { Files: ["media_player_audio.js"], Classes: ["MediaPlayerAudio"] },
        3: { Files: ["media_player_youtube.js"], Classes: ["MediaPlayerYoutube"] },
        4: { Files: ["media_player_effect.js", "media_effect_program.js"], Classes: ["MediaPlayerEffect", "EffectProgramData"] },
        5: { Files: ["media_player_text.js"], Classes: ["MediaPlayerText"] },
        //  (the image texture of the effect takes the defaults of MediaPlayerParamsImage, media_player_image.js)
        "0Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_image.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerImage"] },
        "5Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_text.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerText"] },
    };

    /**
     * Load a script once (relative to this folder; absolute urls are used as is).
     * Uses the core loader `Sw.Net.IncludeJs` when available (it also knows the scripts of static script tags),
     * otherwise a built in loader that de-duplicates by absolute url and waits for a matching static script tag
     * instead of loading the file twice.
     * @param {string} file File name (ex: "media_player_image.js") or url.
     * @returns {Promise<boolean>} True if the script loaded (or was already loaded), false on error.
     */
    static async Include(file) {
        const base = MediaPlayer.CurrentUrl || document.baseURI;
        const sw = window.Sw;
        if (sw && sw.Net && sw.Net.IncludeJs) {
            const r = await sw.Net.IncludeJs(base, file, true);
            return !(r && r.error);
        }
        const url = new URL(file, base).href;
        const m = MediaPlayer.#Loads;
        let p = m.get(url);
        if (p)
            return p;
        p = new Promise(resolve => {
            const existing = Array.from(document.scripts).find(s => s.src === url);
            if (existing) {
                //  A static (deferred) tag: it has run once the document is complete
                if (document.readyState === "complete")
                    resolve(true);
                else
                    window.addEventListener("load", () => resolve(true), { once: true });
                return;
            }
            const s = document.createElement("script");
            s.onload = () => resolve(true);
            s.onerror = () => {
                m.delete(url);
                console.warn("Failed to load script \"" + url + "\"");
                resolve(false);
            };
            s.src = url;
            document.head.appendChild(s);
        });
        m.set(url, p);
        return p;
    }

    /**
     * Load several scripts in parallel (see Include).
     * @param {string[]} files File names.
     * @returns {Promise<boolean>} True if all loaded.
     */
    static async IncludeAll(files) {
        const r = await Promise.all(files.map(f => MediaPlayer.Include(f)));
        return r.every(x => x);
    }

    /**
     * Initializes the media player scripts; called once at the end of this file (not awaited, see Ready).
     * Loads media_player_tools.js and canvasTools.js. When window.MediaLoadAll is truthy it also loads
     * media_effect_program.js and every media_player_*.js file, then awaits MediaPlayerTools.CanUnMute()
     * (the autoplay-with-sound probe).
     * @returns {Promise<void>} Resolves when the scripts are loaded.
     */
    static async Init() {
        await MediaPlayer.IncludeAll(["media_player_tools.js", "canvasTools.js"]);
        if (window.MediaLoadAll) {
            await MediaPlayer.IncludeAll([
                "media_effect_program.js",
                "media_player_image.js",
                "media_player_audio.js",
                "media_player_video.js",
                "media_player_youtube.js",
                "media_player_effect.js",
                "media_player_text.js",
            ]);
            await MediaPlayerTools.CanUnMute();
        }
    }

    /**
     * The key of #Needs for a type and its params.
     * @param {number} type Media type.
     * @param {object} [mediaParams] Params (an Image / Text with an Effect uses the effect player).
     * @returns {string} The key.
     */
    static #NeedKey(type, mediaParams) {
        const fx = (type === MediaTypes.Image || type === MediaTypes.Text) && mediaParams && mediaParams.Effect;
        return fx ? (type + "Fx") : ("" + type);
    }

    /**
     * Construct the player for a type (the classes must be loaded).
     * Image -> MediaPlayerImage (with an Effect: a MediaPlayerEffect for mediaParams.Effect / mediaParams.EffectParams
     * with a MediaPlayerImageTexture as texture provider); Video -> MediaPlayerVideo; Audio -> MediaPlayerAudio;
     * YouTube -> MediaPlayerYoutube (data = video id); Effect -> MediaPlayerEffect (data = .glsl url);
     * Text -> MediaPlayerText (with an Effect: MediaPlayerEffect + MediaPlayerTextTexture).
     * In the "with effect" variants the returned player's Params are the effect params, not mediaParams.
     * @param {number} type Media type.
     * @param {string} data Url or data.
     * @param {object} mediaParams Params.
     * @returns {object|null} The player, null for an unknown type.
     */
    static #Construct(type, data, mediaParams) {
        const fx = mediaParams && mediaParams.Effect;
        //  A transparent image / text with an effect: the effect draws see-through too (its canvas gets an alpha channel), unless its
        //  own params say otherwise
        if (fx && mediaParams.Transparent && (type === MediaTypes.Image || type === MediaTypes.Text) && !(mediaParams.EffectParams && mediaParams.EffectParams.Transparent === false))
            mediaParams.EffectParams = Object.assign({}, mediaParams.EffectParams || {}, { Transparent: true });
        switch (type) {
            case MediaTypes.Image:
                if (fx)
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerImageTexture(gl, mediaParams, data));
                return new MediaPlayerImage(data, mediaParams);
            case MediaTypes.Video:
                return new MediaPlayerVideo(data, mediaParams);
            case MediaTypes.Audio:
                return new MediaPlayerAudio(data, mediaParams);
            case MediaTypes.YouTube:
                return new MediaPlayerYoutube(data, mediaParams);
            case MediaTypes.Effect:
                return new MediaPlayerEffect(data, mediaParams);
            case MediaTypes.Text:
                //  A collage writes its grid (tile counts, sizes) into EffectParams.FxProps while the texture is made: that object must be
                //  the one the effect player reads (its params are a shallow copy), also when the props had no EffectParams / FxProps
                if (fx && mediaParams.CollageSeparator) {
                    const ep = mediaParams.EffectParams = Object.assign({}, mediaParams.EffectParams || {});
                    let fp = ep.FxProps;
                    if (typeof fp === "string")
                        try {
                            fp = JSON.parse(fp);
                        }
                        catch {
                            fp = null;
                        }
                    ep.FxProps = fp && typeof fp === "object" ? fp : {};
                }
                if (fx)
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerTextTexture(gl, mediaParams, data));
                return new MediaPlayerText(data, mediaParams);
        }
        return null;
    }

    /**
     * Create a player for some media.
     * Unless window.MediaLoadAll is set, the scripts needed for the type are loaded lazily first.
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id, for text the text itself etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {Promise<object|null>} A media player object, null for an unknown type.
     */
    static async Create(type, data, mediaParams) {
        const need = MediaPlayer.#Needs[MediaPlayer.#NeedKey(type, mediaParams)];
        if (!need)
            return null;
        await MediaPlayer.Ready;
        if (!window.MediaLoadAll)
            await MediaPlayer.IncludeAll(need.Files);
        return MediaPlayer.#Construct(type, data, mediaParams);
    }

    /**
     * Create a player for some media without loading anything (the scripts must already be loaded).
     * When window.MediaLoadAll is not set it checks that the needed classes exist and throws
     * Error("Must use CreateAsync when lazy loading (or perform manual init)!") otherwise.
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {object|null} A media player object, null for an unknown type.
     */
    static CreateSync(type, data, mediaParams) {
        const need = MediaPlayer.#Needs[MediaPlayer.#NeedKey(type, mediaParams)];
        if (!need)
            return null;
        if (!window.MediaLoadAll) {
            //  Top level classes are global lexical bindings (not window properties): test them with typeof
            for (const c of need.Classes)
                if (Function("return typeof " + c)() !== "function")
                    throw new Error("Must use CreateAsync when lazy loading (or perform manual init)!");
        }
        return MediaPlayer.#Construct(type, data, mediaParams);
    }
}

// Load the shared helper scripts immediately (not awaited here; Create waits for it).
MediaPlayer.Ready = MediaPlayer.Init();

/** Publish the new API names (the classes above stay the public legacy globals). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.Types = MediaTypes;
    m.Player = MediaPlayer;
})();
