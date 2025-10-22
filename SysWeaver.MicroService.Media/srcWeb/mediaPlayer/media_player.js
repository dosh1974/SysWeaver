
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

    /** Array with names */
    static Names = Object.freeze(
        [
            "Image",
            "Video",
            "Audio",
            "YouTube",
            "Effect",
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
        await Promise.all([
            includeJs(current, "media_effect_program.js"),
            includeJs(current, "media_player_tools.js"),

            includeJs(current, "media_player_image.js"),
            includeJs(current, "media_player_audio.js"),
            includeJs(current, "media_player_video.js"),
            includeJs(current, "media_player_youtube.js"),
            includeJs(current, "media_player_effect.js"),
        ]
        );
        await MediaPlayerTools.CanUnMute();
    }

    /**
     * Create a player for some media
     * @param {number} type One of the predefined media type integers defined in MediaTypes
     * @param {string} data The url or other data (for youtube is the video id etc)
     * @param {object} mediaParams The type specific parameters, mirroring one the Media* C# types.
     * @returns {object} A media player object
     */
    static Create(type, data, mediaParams) {
        switch (type) {
            case MediaTypes.Image:
                if (mediaParams && mediaParams.Effect)
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
        }
        return null;
    }



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


}

MediaPlayer.Init();
