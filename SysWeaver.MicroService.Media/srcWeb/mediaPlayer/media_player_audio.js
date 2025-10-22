
class MediaPlayerParamsAudio {

    StartAt = 0;
    EndAt = 0;
    Volume = 100;
}

class MediaPlayerAudio {
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsAudio();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("audio");
        e.muted = true;
        t.HaveVisual = false;
        t.HaveAudio = true;
        MediaPlayerTools.InitBase(t, e, params);
        e.loop = this.Looping;
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.AtStart = true;
    }

    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        if (!await MediaPlayerAudio.LoadAudio(e, t.Url))
            return false;
        t.Width = 0;
        t.Height = 0;
        e.style.width = "0";
        e.style.height = "0";
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
            t.Duration = e.duration;
            const ct = e.currentTime;
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
                    t.LoopWatch = false;
                    await t.OnEnded();
                }
            }
        };
        e.onended = async ev => {
            if (!t.Paused) {
                t.Paused = true;
                t.AtEnd = true;
                e.pause();
                t.LoopWatch = false;
                await t.OnEnded();
            }
        };
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    static async LoadAudio(e, c) {
        const res = await waitEvent2(e, "loadeddata", "error", () => {
            const source = document.createElement('source');
            source.src = c;
            //source.type = "audio/webm";
            e.appendChild(source);
        });
        if (res.type == "error")
            return false;
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
        const n = document.createElement("audio");
        n.muted = e.muted;
        n.volume = e.volume;
        if (!await MediaPlayerAudio.LoadAudio(n, this.Url)) {
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
        t.Element = n;
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
