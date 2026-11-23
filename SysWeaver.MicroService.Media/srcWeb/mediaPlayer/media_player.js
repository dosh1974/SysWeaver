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
     * Key = MediaTypes id; "<id>Fx" = an image / a video / a sound / a text with an effect. Text also needs canvasTools.js (loaded by Init).
     * @type {Object<string, {Files: string[], Classes: string[]}>}
     */
    static #Needs = {
        0: { Files: ["media_player_image.js"], Classes: ["MediaPlayerImage"] },
        1: { Files: ["media_player_video.js"], Classes: ["MediaPlayerVideo"] },
        2: { Files: ["media_audio2.js", "media_player_audio.js"], Classes: ["MediaPlayerAudio"] },
        3: { Files: ["media_player_youtube.js"], Classes: ["MediaPlayerYoutube"] },
        4: { Files: ["media_player_effect.js", "media_effect_program.js"], Classes: ["MediaPlayerEffect", "EffectProgramData"] },
        5: { Files: ["media_player_text.js"], Classes: ["MediaPlayerText"] },
        //  (the image texture of the effect takes the defaults of MediaPlayerParamsImage, media_player_image.js)
        "0Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_image.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerImage"] },
        "5Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_text.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerText"] },
        //  A sound with an effect, or an effect with a sound as its texture (MediaPlayerAudioTexture)
        "2Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_audio2.js", "media_player_audio.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerAudio"] },
        //  A video with an effect (the video is the texture: MediaPlayerVideoTexture, the effect follows its time line)
        "1Fx": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_video.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerVideo", "MediaPlayerVideoTexture"] },
        //  An effect with a video as its texture (its media line "video", or a Texture setting that is a video file)
        "4Vid": { Files: ["media_player_effect.js", "media_effect_program.js", "media_player_video.js"], Classes: ["MediaPlayerEffect", "EffectProgramData", "MediaPlayerVideo", "MediaPlayerVideoTexture"] },
    };

    /**
     * Video file extensions (an effect's Texture with one of them is a video).
     * @type {RegExp}
     */
    static VideoExtRx = /\.(mp4|m4v|webm|mov|ogv)$/i;

    /**
     * Check if a url is a video by its file extension (the query and fragment are ignored).
     * @param {string} url The url.
     * @returns {boolean} True for a video file.
     */
    static IsVideoUrl(url) {
        return (typeof url === "string") && MediaPlayer.VideoExtRx.test(url.split(/[?#]/)[0]);
    }

    /**
     * Audio file extensions (an effect's Texture with one of them is a sound); keep in sync with the Audio extensions of the core
     * (Sw.Format, format.js), this library runs without it.
     * @type {RegExp}
     */
    static AudioExtRx = /\.(mp3|wav|aac|flac|m4a|m4b|opus|oga|ogg|weba)$/i;

    /**
     * Check if a url is a sound by its file extension (the query and fragment are ignored).
     * @param {string} url The url.
     * @returns {boolean} True for an audio file.
     */
    static IsAudioUrl(url) {
        return (typeof url === "string") && MediaPlayer.AudioExtRx.test(url.split(/[?#]/)[0]);
    }

    /** @type {string} The url of this script (the media line's server api is next to it: ../mediaView/Api/). */
    static #Script = document.currentScript ? document.currentScript.src : document.baseURI;

    /**
     * The media line of an effect (Effect Weaver's media effects, a comment line in the shader): "//  media: image stock:a.png",
     * "//  media: audio https://example.com/song.mp3", "//  media: collage stock:a.png my:b.jpg"; a line without references uses the
     * defaults of its kind (the server's). "audio": a sound with the 512 x 64 audio texture (MediaAudio2Engine, media_audio2.js; was
     * "audio2", still read); "legacyaudio": a sound with ShaderToy's 512 x 2 texture; "video": a video whose frames are the texture
     * (MediaPlayerVideoTexture, the effect follows its time line).
     * @type {RegExp}
     */
    static MediaLineRx = /^[ \t]*\/\/[ \t]*media[ \t]*:[ \t]*(image|video|legacyaudio|audio2|audio|collage)\b([^\r\n]*)$/im;

    /**
     * The media line of an effect source. The kind as Effect Weaver reads it (EffectMedia.KindOfLine): "audio2" → "audio"; an "audio" line of
     * a shader that reads ShaderToy's rows (iChannel0 at a constant v) and none of the EwAudio names → "legacyaudio" (effects made before the
     * rename, when "audio" was ShaderToy's texture).
     * @param {string} src The source (the .glsl file).
     * @returns {{Kind:string, Refs:string[], None:boolean, Transparent:boolean}|null} Its kind and references, null without a line. None: the reference "none"
     * (Effect Weaver's "//  media: image none"): the effect is used without media (no texture, SwHaveImage false). Transparent: the word
     * "transparent" (Effect Weaver's "//  media: image transparent stock:x.png"): the user marked the picture as transparent (SwTransparentImage).
     */
    static MediaLine(src) {
        const m = typeof src === "string" ? MediaPlayer.MediaLineRx.exec(src) : null;
        if (!m)
            return null;
        const words = m[2].split(/\s+/);
        let kind = m[1].toLowerCase();
        if (kind === "audio2")
            kind = "audio";
        else if (kind === "audio") {
            const code = src.replace(/\/\*[\s\S]*?\*\/|\/\/[^\n]*/g, " ");
            if (!/\bEwAudio(?:2|[A-Z]\w*)\b/.test(code) && /\b(?:texture|texture2D|texelFetch)\s*\(\s*iChannel0\s*,\s*i?vec2\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*,\s*[0-9.]+\s*\)/.test(code))
                kind = "legacyaudio";
        }
        return { Kind: kind, Refs: words.filter(x => /^(https?:\/\/\S+|[a-z][a-z0-9]*:[^\s/\\:]+)$/i.test(x)), None: words.some(x => x.toLowerCase() === "none"), Transparent: words.some(x => x.toLowerCase() === "transparent") };
    }

    /** @param {string} kind A media line's kind. @returns {boolean} It is a sound (the audio texture or ShaderToy's). */
    static IsAudioKind(kind) {
        return kind === "audio" || kind === "legacyaudio";
    }

    /**
     * The texture an effect's media line names, for an effect whose settings name no Texture (the settings win): urls as they are, other
     * references (Effect Weaver's "stock:file", "my:file") and a line without references by the server (MediaService.ResolveEffectMedia, a
     * host app that knows them). An image or a sound (one, a collage's first picture: the player has no atlas of several pictures).
     * Kept as hidden (non enumerable) params, LineTexture / LineTextureAudio, so the settings (the editor, a shared link) stay as they are.
     * @param {string} url The url of the effect.
     * @param {object} mediaParams The effect params (null: new ones are made).
     * @returns {Promise<object|null>} The params (the same object), with the texture of the line when it has one.
     */
    static async #LineMedia(url, mediaParams) {
        const line = await MediaPlayer.#ReadLine(url);
        if (!line)
            return mediaParams;
        //  The line marks the picture as transparent (the word "transparent"): SwTransparentImage (with the settings' TransparentImage, see #Construct)
        if (line.Transparent) {
            mediaParams = mediaParams || {};
            Object.defineProperty(mediaParams, "LineTransparentImage", { value: true, enumerable: false, configurable: true, writable: true });
        }
        //  A Texture setting wins, but the line still says which texture a sound gets (audio: the 512 x 64 one, legacyaudio: ShaderToy's)
        if (mediaParams && mediaParams.Texture) {
            Object.defineProperty(mediaParams, "LineTextureAudio2", { value: line.Kind === "audio", enumerable: false, configurable: true, writable: true });
            return mediaParams;
        }
        //  No media ("none"): no texture (not the defaults)
        if (line.None)
            return mediaParams;
        let urls = line.Refs.length && line.Refs.every(x => /^https?:/i.test(x)) ? line.Refs : null;
        if (!urls) {
            try {
                const res = await fetch(new URL("../mediaView/Api/ResolveEffectMedia", MediaPlayer.#Script).href, {
                    method: "POST", headers: { "Content-Type": "application/json" }, credentials: "same-origin",
                    body: JSON.stringify({ Kind: line.Kind, Refs: line.Refs }),
                });
                const r = res.ok ? await res.json() : null;
                const base = new URL("../mediaView/", MediaPlayer.#Script);
                urls = (r && r.Urls ? r.Urls : []).map(x => new URL(x, base).href);
            }
            catch (e) {
                console.warn("Media: the media of the effect " + url + " can't be resolved: " + e.message);
                urls = [];
            }
        }
        if (!urls.length)
            return mediaParams;
        const p = mediaParams || {};
        Object.defineProperty(p, "LineTexture", { value: urls[0], enumerable: false, configurable: true, writable: true });
        Object.defineProperty(p, "LineTextureAudio", { value: MediaPlayer.IsAudioKind(line.Kind), enumerable: false, configurable: true, writable: true });
        //  (audio2: the rich 512 x 64 audio texture, MediaAudio2Engine)
        Object.defineProperty(p, "LineTextureAudio2", { value: line.Kind === "audio", enumerable: false, configurable: true, writable: true });
        //  (video: its frames are the texture, MediaPlayerVideoTexture)
        Object.defineProperty(p, "LineTextureVideo", { value: line.Kind === "video", enumerable: false, configurable: true, writable: true });
        return p;
    }

    /**
     * The media line of an effect file (fetched).
     * @param {string} url The url of the effect.
     * @returns {Promise<{Kind:string, Refs:string[]}|null>} The line, null without one (or when the file can't be read).
     */
    static async #ReadLine(url) {
        try {
            const res = await fetch(url, { credentials: "same-origin" });
            return res.ok ? MediaPlayer.MediaLine(await res.text()) : null;
        }
        catch {
            return null;
        }
    }

    /**
     * The texture of an effect: its Texture setting, else the texture of its media line (#LineMedia).
     * @param {object} mediaParams The effect params.
     * @returns {{Url:string, Audio:boolean, Audio2:boolean, Video:boolean}|null} The url, whether it is a sound, whether the sound gets the
     *   audio texture (512 x 64: the media line's kind "audio") and whether it is a video (by its extension, or the line's kind), null for none.
     */
    static #TextureOf(mediaParams) {
        if (!mediaParams)
            return null;
        if (mediaParams.Texture)
            return { Url: mediaParams.Texture, Audio: MediaPlayer.IsAudioUrl(mediaParams.Texture), Audio2: !!mediaParams.LineTextureAudio2, Video: MediaPlayer.IsVideoUrl(mediaParams.Texture) || !!mediaParams.LineTextureVideo };
        return mediaParams.LineTexture ? {
            Url: mediaParams.LineTexture, Audio: !!mediaParams.LineTextureAudio || MediaPlayer.IsAudioUrl(mediaParams.LineTexture), Audio2: !!mediaParams.LineTextureAudio2,
            Video: !!mediaParams.LineTextureVideo || MediaPlayer.IsVideoUrl(mediaParams.LineTexture),
        } : null;
    }

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
                "media_audio2.js",
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
        if ((type === MediaTypes.Effect) && MediaPlayer.#TextureOf(mediaParams)?.Audio)
            return "2Fx";
        if ((type === MediaTypes.Effect) && MediaPlayer.#TextureOf(mediaParams)?.Video)
            return "4Vid";
        const fx = (type === MediaTypes.Image || type === MediaTypes.Text || type === MediaTypes.Audio || type === MediaTypes.Video) && mediaParams && mediaParams.Effect;
        return fx ? (type + "Fx") : ("" + type);
    }

    /**
     * Construct the player for a type (the classes must be loaded).
     * Image -> MediaPlayerImage (with an Effect: a MediaPlayerEffect for mediaParams.Effect / mediaParams.EffectParams
     * with a MediaPlayerImageTexture as texture provider); Video -> MediaPlayerVideo; Audio -> MediaPlayerAudio (with an Effect: a
     * MediaPlayerEffect that plays the sound, with a MediaPlayerAudioTexture); YouTube -> MediaPlayerYoutube (data = video id);
     * Effect -> MediaPlayerEffect (data = .glsl url; mediaParams.Texture: a sound (IsAudioUrl) that plays with it as an audio texture,
     * else an image texture);
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
        //  A picture (an image, a video, a text) marked Transparent with an effect: the effect's SwTransparentImage is true (its TransparentImage)
        if (fx && mediaParams.Transparent && (type === MediaTypes.Image || type === MediaTypes.Video || type === MediaTypes.Text) && !(mediaParams.EffectParams && mediaParams.EffectParams.TransparentImage))
            mediaParams.EffectParams = Object.assign({}, mediaParams.EffectParams || {}, { TransparentImage: true });
        //  An effect whose media line marks its picture as transparent ("//  media: image transparent ..."): SwTransparentImage
        if (type === MediaTypes.Effect && mediaParams && mediaParams.LineTransparentImage && !mediaParams.TransparentImage) {
            const lp = mediaParams;
            mediaParams = Object.assign({}, lp, { TransparentImage: true });
            for (const k of ["LineTexture", "LineTextureAudio", "LineTextureAudio2", "LineTextureVideo"])
                if (k in lp)
                    Object.defineProperty(mediaParams, k, { value: lp[k], enumerable: false, configurable: true, writable: true });
        }
        switch (type) {
            case MediaTypes.Image:
                if (fx)
                    return new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayerImageTexture(gl, mediaParams, data));
                return new MediaPlayerImage(data, mediaParams);
            case MediaTypes.Video:
                //  (with an effect: the effect draws the video, it plays on the video's time line, see #WithVideo)
                if (fx)
                    return MediaPlayer.#WithVideo(mediaParams.Effect, mediaParams.EffectParams, data, mediaParams);
                return new MediaPlayerVideo(data, mediaParams);
            case MediaTypes.Audio:
                //  (the effect's media line "audio": the 512 x 64 texture, see Create)
                //  (made by CreateSync, the kind is not known yet: null, read from the effect's source when its texture is made)
                if (fx)
                    return MediaPlayer.#WithSound(mediaParams.Effect, mediaParams.EffectParams, data, mediaParams, ("EffectAudio2" in mediaParams) ? !!mediaParams.EffectAudio2 : null);
                return new MediaPlayerAudio(data, mediaParams);
            case MediaTypes.YouTube:
                return new MediaPlayerYoutube(data, mediaParams);
            case MediaTypes.Effect: {
                //  The Texture of an effect (else the one of its media line): a sound (by its extension, or the line's kind) plays with it,
                //  else it is an image
                const tex = MediaPlayer.#TextureOf(mediaParams);
                const tx = tex ? tex.Url : null;
                if (tex && tex.Audio)
                    return MediaPlayer.#WithSound(data, mediaParams, tx, null, !!tex.Audio2);
                //  A video: its frames, on its time line (the effect keeps its own size: it isn't a video with an effect)
                if (tex && tex.Video)
                    return MediaPlayer.#WithVideo(data, mediaParams, tx, { EffectSizeFromImage: false });
                if (tx)
                    return new MediaPlayerEffect(data, mediaParams, gl => new MediaPlayerImageTexture(gl, { EffectSizeFromImage: false, EffectParams: mediaParams }, tx));
                return new MediaPlayerEffect(data, mediaParams);
            }
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
     * An effect that plays a sound on its time line and gets it as its texture (MediaPlayerAudioTexture, see MediaPlayerEffect.FollowMedia).
     * @param {string} effect Url of the .glsl effect.
     * @param {object} effectParams The effect params.
     * @param {string} sound Url of the sound.
     * @param {object|null} soundParams The audio params (MediaPlayerParamsAudio: Volume, StartAt, EndAt), null = defaults.
     * @param {boolean|null} [audio2] The 512 x 64 audio texture (the effect's media line "audio"), else ShaderToy's 512 x 2; null = not known
     *   yet (CreateSync: the media line of the effect's source decides when the texture is made, the source is loaded by then).
     * @returns {MediaPlayerEffect} The player.
     */
    static #WithSound(effect, effectParams, sound, soundParams, audio2) {
        const media = new MediaPlayerAudio(sound, soundParams);
        let p = null;
        const rich = () => {
            if ((audio2 !== null) && (audio2 !== undefined))
                return !!audio2;
            const line = p && (typeof p.Src === "string") ? MediaPlayer.MediaLine(p.Src) : null;
            return !!line && line.Kind === "audio";
        };
        p = new MediaPlayerEffect(effect, effectParams, gl => new MediaPlayerAudioTexture(gl, media, rich()), media);
        return p;
    }

    /**
     * An effect that draws a video: the video is its texture (MediaPlayerVideoTexture: the current frame), and it plays on the video's time
     * line with the video's controls and sound (MediaPlayerEffect.FollowMedia, as an effect with a sound). The video params (play window,
     * volume, Stream, Crop of the texture, EffectSizeFromImage) are the video's, EffectParams the effect's.
     * @param {string} effect Url of the .glsl effect.
     * @param {object} effectParams The effect params.
     * @param {string} video Url of the video.
     * @param {object} videoParams The video params (MediaPlayerParamsVideo).
     * @returns {MediaPlayerEffect} The player.
     */
    static #WithVideo(effect, effectParams, video, videoParams) {
        const media = new MediaPlayerVideo(video, videoParams);
        return new MediaPlayerEffect(effect, effectParams, gl => new MediaPlayerVideoTexture(gl, videoParams, media), media);
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
        //  An effect without a Texture setting: the media its shader names (its media line)
        if (type === MediaTypes.Effect && typeof data === "string")
            mediaParams = await MediaPlayer.#LineMedia(data, mediaParams);
        //  A sound with an effect: the effect's media line says which audio texture it reads ("audio": the 512 x 64 one, else
        //  ShaderToy's 512 x 2); a hidden param, so the settings stay as they are
        if (type === MediaTypes.Audio && mediaParams && typeof mediaParams.Effect === "string" && mediaParams.Effect) {
            const line = await MediaPlayer.#ReadLine(mediaParams.Effect);
            Object.defineProperty(mediaParams, "EffectAudio2", { value: !!line && line.Kind === "audio", enumerable: false, configurable: true, writable: true });
        }
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
