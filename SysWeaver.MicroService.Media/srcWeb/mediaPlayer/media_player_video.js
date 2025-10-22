
class MediaPlayerParamsVideo {

    StartAt = 0;
    EndAt = 0;
    Volume = 100;
    Stream = 0;
    Transparent = false;
}


class MediaPlayerVideo {
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsVideo();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("video");
        e.muted = true;
        const audioOnly = params.Stream == 2;
        t.HaveVisual = !audioOnly;
        t.HaveAudio = params.Stream != 1;
        MediaPlayerTools.InitBase(t, e, params);
        e.loop = t.Looping;
        if (audioOnly) {
            e.style.width = "0";
            e.style.height = "0";
            e.style.opacity = "0";
        }
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.AtStart = true;
    }

    async Cache(keepHidden) {
        const t = this;
        const e = t.Element;
        if (!await MediaPlayerVideo.LoadVideo(e, t.Url))
            return false;
        if (t.HaveVisual) {
            t.Width = e.videoWidth;
            t.Height = e.videoHeight;
        } else {
            t.Width = 0;
            t.Height = 0;
            e.style.width = "0";
            e.style.height = "0";
            e.style.opacity = "0";
        }
        t.ApplyClip();
        const duration = e.duration;
        t.Duration = duration;
        const p = t.Params;
        const startAt = p.StartAt;
        if (startAt > 0) {
            e.currentTime = startAt;
            t.LoopWatch = false;
        }
        let endAt = p.EndAt;
        if (endAt > duration)
            endAt = 0;
        const endTime = endAt > startAt ? endAt : duration;
        const loopWatch = (endTime + startAt) * 0.5;
        e.ontimeupdate = async () => {
            const ct = e.currentTime;
            t.Duration = e.duration;
            if (ct < endTime) {
                if (t.LoopWatch) {
                    if (ct < loopWatch) {
                        e.currentTime = startAt;
                        t.LoopWatch = false;
                        if (t.OnLoop)
                            await t.OnLoop();
                    }
                } else {
                    t.LoopWatch = (startAt > 0) && t.Looping && (!t.Paused) && (ct > loopWatch);
                }
                return;
            }
            if (t.Looping) {
                e.currentTime = startAt;
                t.LoopWatch = false;
            } else {
                if (!t.Paused) {
                    t.Paused = true;
                    t.AtEnd = true;
                    e.pause();
                    e.currentTime = endTime;
                    await t.OnEnded();
                }
            }
        };
        e.onended = async ev => {
            if (!t.Paused) {
                t.Paused = true;
                e.pause();
                t.AtEnd = true;
                await t.OnEnded();
            }
        };
        return MediaPlayerTools.OnCacheComplete(this, keepHidden);
    }


    //static VideoCache = new Map();

    static async LoadVideo(e, c) {
        /*
        const cache = MediaPlayerVideo.VideoCache;
        let blobUrl = cache.get(c);
        if (!blobUrl) {
            if (blobUrl !== "failed") {

                const r = new Request(c, {
                    method: "GET",
                    mode: "cors",
                    cache: "default",
                });
                const res = await fetch(r);
                if (res.status != 200) {
                    setTimeout(() => LoadVideos(e, c), 10000);
                    return;
                }
                const blob = await res.blob();
                if (blob.type !== "") {
                    const newC = URL.createObjectURL(blob);
                    cache.set(c, newC);
                    c = newC;
                } else {
                    log.warn("Can't use video blob cache for \"" + c + "\"");
                    cache.set(c, "failed");
                }
            }
        }
        */

        const res = await waitEvent2(e, "loadeddata", "error", () => {
            const source = document.createElement('source');
            source.src = c;
            //source.type = "video/webm";
            e.appendChild(source);
        });
        if (res.type == "error") {
            return false;
        }
        while (e.readyState < 3)
            await delay(100);
        return true;
    }

    async Mute() {
        if (this.Muted)
            return;
        this.Muted = true;
        this.Element.muted = true;
    }

    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        if (!this.HaveAudio)
            return;
        this.Element.volume = this.Volume * this.Params.Volume * 0.01;
        this.Element.muted = false;
    }

    async SetVolume(vol) {
        if (vol < 0)
            vol = 0;
        if (vol > 1)
            vol = 1;
        if (vol == this.Volume)
            return;
        this.Volume = vol;
        if (!this.HaveAudio)
            return;
        if (this.Muted)
            return;
        this.Element.volume = vol * this.Params.Volume * 0.01;
    }


    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        const e = t.Element;
        const p = t.Params;
        const startAt = p.StartAt;
        if (t.AtEnd) {
            t.AtEnd = false;
            e.currentTime = startAt;
            t.LoopWatch = false;
        }
        try {
            e.loop = t.Looping;
            await e.play();
            t.Paused = false;
            t.AtStart = false;
            t.Duration = e.duration;
        }
        catch
        {
        }
    }


    async Stop() {
        const t = this;
        const e = t.Element;
        if (t.Paused) {
            if (t.AtStart)
                return;
        }
/*
        const n = document.createElement("video");
        n.muted = e.muted;
        n.volume = e.volume;
        if (!await MediaPlayerVideo.LoadVideo(n, t.Url)) {
            t.Paused = true;
            const res = await waitEvent2(e, "loadeddata", "error", () => e.load());
            if (res.type == "error")
                return false;
            t.Paused = true;
            t.AtStart = true;
            return;
        }
        const startAt = t.Params.StartAt;
        if (startAt > 0)
        {
            n.currentTime = startAt;
            t.LoopWatch = false;
        }
        n.className = e.className;
        n.setAttribute("style", e.getAttribute("style"));
        e.replaceWith(n);
        this.Element = n;
*/
        e.pause();
        e.currentTime = t.Params.StartAt;
        t.LoopWatch = false;
        t.Paused = true;
        t.AtStart = true;
        return true;
    }

    async Pause() {
        if (this.Paused)
            return;
        this.Paused = true;
        this.Element.pause();
    }

    async Seek(time) {
        const t = this;
        const p = t.Params;
        const e = t.Element;
        const startAt = this.Params.StartAt;
        const endAt = p.EndAt;
        const endTime = endAt > startAt ? endAt : e.duration;
        if (time < startAt)
            time = startAt;
        if (time > endTime)
            time = endTime;
        t.AtEnd = time >= endTime;
       t.AtStart = time <= startAt;
        e.currentTime = time;
        t.LoopWatch = false;
    }

    GetPos() {
        return this.Element.currentTime;
    }

    async Loop() {
        const t = this;
        if (t.Looping)
            return;
        const e = t.Element;
        t.Looping = true;
        e.loop = true;
    }

    async Once() {
        const t = this;
        if (!t.Looping)
            return;
        t.Looping = false;
        t.Element.loop = false;
    }

}
