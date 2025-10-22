class MediaPlayerTools {
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
            i.OnDispose = async () => await i.Stop();
        if (!i.SetVolume)
            i.SetVolume = async v => i.Volume = v;
        if (!i.UnMute)
            i.UnMute = async () => i.Muted = false;
        if (!i.Mute)
            i.Mute = async () => i.Muted = true;
        if (!i.Play)
            i.Play = async () => i.Paused = false;
        if (!i.Pause)
            i.Pause = async () => i.Paused = true;
        if (!i.Stop)
            i.Stop = async () => i.Paused = true;
        if (!i.Seek)
            i.Seek = async time => { }
        if (!i.GetPos)
            i.GetPos = () => 0;
        if (!i.Loop)
            i.Loop = async () => i.Looping = true;
        if (!i.Once)
            i.Once = async () => i.Looping = false;
        if (!i.Show)
            i.Show = () => {
                const oldStyle = i.OldStyle;
                if (oldStyle)
                    i.Element.setAttribute("style", oldStyle);
                i.Element.classList.remove("MediaHidden");
            }
        if (!i.Hide)
            i.Hide = () => {
                i.Element.classList.add("MediaHidden");
                i.OldStyle = i.Element.getAttribute("style");
                if (i.OldStyle)
                    i.Element.removeAttribute("style");
            }
        if (!i.ResetVolume)
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
                i.GetTrueVolume = () => i.Muted ? 0 : (i.Volume * params.Volume * 0.01);
            else
                i.GetTrueVolume = () => i.Muted ? 0 : i.Volume;
        }
        if (typeof i.UseClip === "undefined")
            i.UseClip = true;
        else
            i.UseClip = !!i.UseClip;


        if (!i.ApplyClip)
            i.ApplyClip = ap => MediaPlayerTools.ApplyClip(i, ap);

        if (!i.OnLoop)
            i.OnLoop = () => {
                const me = i.MessageElement;
                if (me)
                    me.dispatchEvent(new CustomEvent("mediaLoop", {
                        detail: i,
                    }));
                //console.warn("Ended!");
            };
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
/*
        function NoClip() {
            e.style.clipPath = null;
            if (i.OnClipChanged)
                i.OnClipChanged();
        }
        const ow = i.Width;
        const oh = i.Height;
        i.ClipX = 0;
        i.ClipY = 0;
        i.ClipWidth = ow;
        i.ClipHeight = oh;
        params = params ?? i.Params;
        if (!params)
            return;
        const crop = params.Crop;
        if (typeof crop === "undefined")
            return;
        const e = i.Element;
        function NoClip() {
            e.style.clipPath = null;
            if (i.OnClipChanged)
                i.OnClipChanged();
        }
        if (!crop) {
            NoClip();
            return;
        }
        if (typeof params.Stream !== "undefined")
            if (params.Stream === 2) {
                NoClip();
                return;
            }

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
        if ((t <= 0) && (r <= 0) && (b <= 0) && (l <= 0)) {
            NoClip();
            return;
        }
        let w = ow - l - r;
        if (w < 0)
            w = 0;
        let h = oh - t - b;
        if (h < 0)
            h = 0;
        const newP = "inset(" + t + "px " + r + "px " + b + "px " + l + "px)";
        if (newP === e.style.clipPath)
            return;
        e.style.clipPath = newP;
        i.ClipX = l;
        i.ClipY = t;
        i.ClipWidth = w;
        i.ClipHeight = h;
        if (i.OnClipChanged)
            i.OnClipChanged();
*/
    }

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

    static async OnCacheComplete(i, keepHidden) {
        if (!keepHidden)
            i.Element.classList.remove("MediaHidden");
        i.Cached = true;
        return true;
    }

    static async CanUnMute() {
        if (MediaPlayerTools.HaveTested)
            return MediaPlayerTools.Result;

        const e = document.createElement("audio");
        let res = false;
        if (await MediaPlayerAudio.LoadAudio(e, "data:audio/mpeg;base64,/+MYxAAAAANIAUAAAASEEB/jwOFM/0MM/90b/+RhST//w4NFwOjf///PZu////9lns5GFDv//l9GlUIEEIAAAgIg8Ir/JGq3/+MYxDsLIj5QMYcoAP0dv9HIjUcH//yYSg+CIbkGP//8w0bLVjUP///3Z0x5QCAv/yLjwtGKTEFNRTMuOTeqqqqqqqqqqqqq/+MYxEkNmdJkUYc4AKqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq")) {
            try {
                e.muted = false;
                e.volume = 0.01;
                await e.play();
                res = true;
                await e.pause();
                document.body.dispatchEvent(new CustomEvent("AudioEnabled"));
            }
            catch
            {
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



    static MustBeAttached(e) {
        if (IsAttached(e))
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


}

