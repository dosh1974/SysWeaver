
class MediaPlayerParamsYouTube {

    Width = 1920;
    Height = 1920;
    StartAt = 0;
    EndAt = 0;
    Volume = 100;
    Stream = 0;
    Crop = {
        "MarginAsPercentage": true,
        "MarginTopP": 21.875,
        "MarginRightP": 0,
        "MarginBottomP": 21.875,
        "MarginLeftP": 0,
    }
}

class MediaPlayerYoutube {
    constructor(code, params) {
        const t = this;
        const cp = new MediaPlayerParamsYouTube();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("iframe");
        const audioOnly = params.Stream == 2;
        t.HaveVisual = !audioOnly;
        t.HaveAudio = params.Stream != 1;
        MediaPlayerTools.InitBase(t, e, params);
        t.Code = code;
        e.setAttribute("scrolling", "no");
        e.sandbox = "allow-same-origin allow-scripts allow-presentation";
        e.allow = "autoplay";
        const w = audioOnly ? 0 : (params.Width ? params.Width : 1280);
        const h = audioOnly ? 0 : (params.Height ? params.Height : (w * 9 / 16));
        e.style.width = w + "px";
        e.style.height = h + "px";
        if (audioOnly)
            e.style.opacity = "0";
        t.Width = w;
        t.Height = h;
        t.CanPlay = true;
        t.done = false;
        let instanceId = MediaPlayerYoutube.GlobalInstanceId;
        ++instanceId;
        MediaPlayerYoutube.GlobalInstanceId = instanceId;
        t.InstanceId = instanceId;
        t.Source = "https://www.youtube.com/watch?v=" + code;
        t.CurrentTime = params.StartAt;
        t.PauseGreaterThan = -1;
        t.Duration = 0;
        t.ApplyClip();
    }

    static GlobalInstanceId = 0;


    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        const restore = MediaPlayerTools.MustBeAttached(e);
        try {
            const code = t.Code;
            const p = t.Params;
            let haveConnection = false;
            const instanceId = t.InstanceId;
            const url = "https://www.youtube-nocookie.com/embed/" + code + "?&autoplay=0&mute=1&rel=0&start=0&autohide=1&showinfo=0&controls=0&showsearch=0&fs=0&enablejsapi=1&iv_load_policy=3&cc_load_policy=3&disablekb=1&playsinline=1&loop=1&cc_lang_pref=en&playlist=" + code;
            let isLooping = false;
            let gotError = false;
            let shouldPauseOnce = t.Paused;
            let startAt = p.StartAt;
            if (!startAt)
                startAt = 0;

            t.WaitCommand = async (cmd, args) => {
                const ev = await waitEvent2(e, "YTinfoDelivery", "YTonError", () => t.SendCommand(cmd, args), 500);
                const res = ev && (ev.type === "YTinfoDelivery");
                if (!res)
                    console.warn("YT command \"" + cmd + "\", with args: \"" + args + "\" failed!");
                return res;
            }
            let sendPauseEvent = false;

            async function messageHandler(ev) {
                let d;
                try {
                    d = JSON.parse(ev.data);
                    if (d.id != instanceId)
                        return;
                }
                catch (e) {
                    return;
                }
                try {
                    if (!haveConnection) {
                        haveConnection = true;
                        console.log("Connected with iframe " + instanceId);
                    }
                    if (d.event === "onError") {
                        gotError = true;
                    }
                    if (d.event === "infoDelivery") {

                        const now = d.info.currentTime;
                        if (typeof now === "undefined")
                            return;
                        const newD = d.info.progressState.duration;
                        if (newD > t.Duration)
                            t.Duration = newD;

                        const duration = t.Duration;
                        t.CurrentTime = now;
                        const shouldLoop = t.Looping;
                        let endAt = p.EndAt;
                        if (endAt > duration)
                            endAt = 0;
                        let endTime = endAt > startAt ? endAt : duration;
                        const loopWatch = (endTime + startAt) * 0.5;
                        if (shouldLoop)
                            endTime = endAt > startAt ? endAt : loopWatch;
                        else
                            endTime -= (endAt > startAt ? 0 : 0.5);

                        const pgt = t.PauseGreaterThan;
                        if ((pgt >= 0) && (now > pgt)) {
                            t.PauseGreaterThan = -1;
                            await t.WaitCommand("pauseVideo");
                            await t.WaitCommand("seekTo", "[" + pgt + ",true]");
                            if (!t.Muted)
                                t.SendCommand("unMute");
                            t.CurrentTime = pgt;
                            if (sendPauseEvent)
                                e.dispatchEvent(new Event("YTInitCompleted"));
                            sendPauseEvent = false;
                        }
                        if (isLooping) {
                            if (now < loopWatch) {
                                if (startAt > 0)
                                    if (now <= 0)
                                        return;
                                t.SendCommand("seekTo", "[" + startAt + ",true]");
                                isLooping = false;
                                if (t.OnLoop)
                                    await t.OnLoop();
                            }
                            return;
                        }
                        if (now < endTime)
                            return;
                        if (!shouldLoop) {
                            if (!t.Paused) {
                                t.SendCommand("pauseVideo");
                                t.SendCommand("seekTo", "[" + endTime + ",true]");
                                t.CurrentTime = endTime;
                                t.Paused = true;
                                t.AtStart = false;
                                t.AtEnd = true;
                                await t.OnEnded();
                            }
                            return;
                        }
                        if (endAt > startAt) {
                            t.SendCommand("seekTo", "[" + startAt + ",true]");
                            if (t.OnLoop)
                                await t.OnLoop();
                        } else {
                            isLooping = true;
                        }

                    }

                }
                catch (e) {
                }
                finally {
                    e.dispatchEvent(new CustomEvent("YT" + d.event, {
                        detail: d,
                    }));
                    //console.log("YT" + d.event + ": " + ev.data);
                }
            }

            async function connect() {
                t.SendCommand("mute");
                haveConnection = false;
                isLooping = false;
                gotError = false;
                const res = await waitEvent2(e, "load", "error", () => e.src = url);
                if (res.type === "error")
                    return false;
                const conFn = () => {
                    if (haveConnection)
                        return;
                    e.contentWindow.postMessage('{"event":"listening","id":' + instanceId + ',"channel":"widget"}', '*');
                    setTimeout(conFn, 100);
                };
                const res2 = await waitEvent2(e, "YTonReady", "YTonError", conFn);
                if (res2.type === "YTonError")
                    return false;
                if (gotError)
                    return false;
                return true;
            }

            window.addEventListener("message", messageHandler);
            const onD = this.OnDispose;
            this.OnDispose = () => {
                window.removeEventListener("message", messageHandler);
                onD();
            };
            let timeOut = 50;
            for (let retry = 0; ; ++retry) {
                if (await connect()) {
                    t.SendCommand("mute");
                    const res2 = await waitEvent2(e, "YTInitCompleted", "YTonError", () => {
                        let time = startAt - 0.001;
                        if (time < 0)
                            time = 0;
                        t.PauseGreaterThan = startAt;
                        sendPauseEvent = true;
                        t.SendCommand("seekTo", "[" + time + ",true]");
                    });
                    if (res2.type === "YTonError") {
                        console.error("Youtube video playback failed for " + code);
                        return false;
                    }
                    await delay(50);
                    break;
                }
                if (retry >= 5) {
                    console.error("Youtube video playback failed for " + code);
                    return false;
                }
                console.warn("Youtube video playback failed for " + code + ", retrying in " + timeOut + " ms");
                await delay(timeOut);
                timeOut *= 2;
                if (timeOut > 5000)
                    timeOut = 5000;
            }
            console.log("Cached youtube video " + code);
            return MediaPlayerTools.OnCacheComplete(t, keepHidden);
        }
        finally {
            restore();
        }
    }

    //https://memo.ag2works.tokyo/post-1283/
    SendCommand(command, args) {

        const cmd = typeof args === "undefined" ? '{"event":"command", "func":"' + command + '"}' : '{"event":"command", "func":"' + command + '", "args":' + args + '}';
        const ce = this.Element.contentWindow;
        if (!ce)
            return;
        //console.log("YT sending command:\n" + cmd);
        ce.postMessage(cmd, '*');
    }


    async Mute() {
        if (this.Muted)
            return;
        this.Muted = true;
        this.SendCommand("mute");
    }

    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        if (!this.HaveAudio)
            return;
        const vol = this.Volume * this.Params.Volume;
        if (vol > 0) {
            this.SendCommand("setVolume", "[" + vol + "]");
            this.SendCommand("unMute");
        }
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
        vol *= this.Params.Volume;
        if (this.PlayerVolume !== vol) {
            this.PlayerVolume = vol;
            if (vol <= 0)
                this.SendCommand("mute");
            else {
                this.SendCommand("setVolume", "[" + vol + "]");
                this.SendCommand("unMute");
            }
        }
    }


    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        if (t.AtEnd) {
            t.AtEnd = false;
            await t.WaitCommand("seekTo", "[" + t.Params.StartAt + ",true]");
        }
        await t.WaitCommand("playVideo");
    }

    async Stop() {
        const t = this;
        const startTime = t.Params.StartAt;
        if ((t.Paused) && (t.CurrentTime <= startTime))
            return;
        t.Paused = true;
        t.CurrentTime = startTime;
        let time = startTime;
        t.PauseGreaterThan = time;
        time -= 0.001;
        if (time < 0)
            time = 0;
        await t.WaitCommand("seekTo", "[" + time + ",true]");
        if (t.Paused) {
            if (!t.Muted)
                t.SendCommand("mute");
            await t.WaitCommand("playVideo");
        }
    }

    async Pause() {
        const t = this;
        if (t.Paused)
            return;
        t.Paused = true;
        await t.WaitCommand("pauseVideo");
    }

    async Seek(time) {
        if (time < 0)
            return;
        const t = this;
        if (time == t.CurrentTime)
            return;
        t.CurrentTime = time;
        if (t.Paused) {
            t.PauseGreaterThan = time;
            time -= 0.001;
            if (time < 0)
                time = 0;
            await t.WaitCommand("seekTo", "[" + time + ",true]");
            await t.WaitCommand("playVideo");
        } else {
            await t.WaitCommand("seekTo", "[" + time + ",true]");
        }
    }

    GetPos() {
        return this.CurrentTime;
    }

}

