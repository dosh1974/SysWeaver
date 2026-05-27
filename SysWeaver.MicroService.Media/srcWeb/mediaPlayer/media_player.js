
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
     * @returns {string} The C# type name
     */
    static GetTypeName(mediaType) {
        return "SysWeaver.MicroService.Media.Media" + Names[mediaType];
    }


}

class MediaPlayer {

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

    static CurrentUrl;

    /**
     * Create a player for some media
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {object} A media player object.
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
     * @returns {object} A media player object.
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

MediaPlayer.Init();
