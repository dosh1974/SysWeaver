


class MediaPlayerParamsImage {
    Crop = null;
    Duration = 10;
    Effect = null;
    EffectParams = null;
    EffectSizeFromImage = true;
    Transparent = false;
    AdaptiveSize = false;
}

class MediaPlayerImage {




    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsImage();
        const isSvg = url.endsWith(".svg");
        cp.AdaptiveSize = isSvg;
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("img");
        if (params.AdaptiveSize)
            e.style.objectFit = "contain";

        t.HaveVisual = true;
        t.CanPlay = true;
        MediaPlayerTools.InitBase(t, e, params);
        t.Url = url;
        t.Source = url;
        t.CurrentPosition = 0;
        t.Duration = params.Duration;
    }

    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        const c = t.Url;
        const res = await waitEvent2(e, "load", "error", () => {
            e.src = c;
        });
        if (res.type == "error")
            return false;
        t.Width = e.naturalWidth;
        t.Height = e.naturalHeight;
        if (t.Params.AdaptiveSize) {
            if ((t.Width > 0) && (t.Height > 0)) {
                t.AspectRatio = t.Width / t.Height;
            }
        }
        t.ApplyClip();
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        const setTimer = async () => {
            const dur = t.Params.Duration;
            if (dur <= 0) {
                t.CurrentPosition = 0;
                if (!t.Looping) {
                    t.Paused = true;
                    await t.OnEnded();
                }
                return;
            }
            let timeLeft = (dur - t.CurrentPosition) * 1000;
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            if (timeLeft <= 0) {
                t.CurrentPosition = 0;
                timeLeft = dur * 1000;
            }
            t.StartedAt = performance.now() - t.CurrentPosition * 1000;
            t.TimeFn = setTimeout(async () => {
                t.TimeFn = null;
                if (t.Looping) {
                    t.CurrentPosition = 0;
                    await setTimer();
                    await t.OnLoop();
                } else {
                    t.Paused = true;
                    t.CurrentPosition = dur;
                    await t.OnEnded();
                }
            }, timeLeft);
        };
        await setTimer();
    }

    Pause() {
        const t = this;
        if (t.Paused)
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.CurrentPosition = t.GetPos();
        t.TimeFn = null;
        t.Paused = true;
    }

    Stop() {
        const t = this;
        if (t.Paused && (t.CurrentPosition <= 0))
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.TimeFn = null;
        t.Paused = true;
        t.CurrentPosition = 0;
    }

    GetPos() {
        const t = this;
        if (t.Paused)
            return t.CurrentPosition;
        const dur = t.Params.Duration;
        if (dur <= 0)
            return 0;
        return Math.min((performance.now() - t.StartedAt) / 1000, dur);
    }

    async Once() {
        const t = this;
        t.Looping = false;
        if (t.Paused)
            return;
        if (t.Params.Duration > 0)
            return;
        t.Paused = true;
        await t.OnEnded();
    }

    async Seek(time) {
        if (time < 0)
            return null;
        const t = this;
        const dur = t.Params.Duration;
        if (time >= dur)
            time = dur;
        const p = t.Paused;
        if (!p) {
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            t.Paused = true;
        }
        t.CurrentPosition = time;
        if (!p)
            await t.Play();
    }


}




