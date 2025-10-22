
includeJs(document.currentScript.src, "../mediaPlayer/media_player.js");

async function mediaCreatePlayer(target, type, data, props, noControls, before, noStats, fit, aspectX, aspectY, alignX, alignY) {
    const p = document.createElement("Media-Preview");
    if (before) {
        p.classList.add("Replacing");
        target.insertBefore(p, before);
    }
    else
        target.appendChild(p);

    const clip = document.createElement("Media-AspectContainer");
    p.appendChild(clip);
    const cache = document.createElement("Media-Cache");
    p.appendChild(cache);

    
    const player = MediaPlayer.Create(type, data, props);
    if (!player) {
        Fail("No supported player found for type " + type, 100000);
        p.remove();
        return;
    }
    if (!props)
        props = player.Params;
    if (!player.HaveVisual)
        p.classList.add("NoVisual");


    clip.appendChild(player.Element);
    if (!(await player.Cache())) {
        cache.remove();
        Fail("Failed to cache the media", 100000);
        return;
    }

    const mediaOverlay = document.createElement("Media-Overlay");
    clip.appendChild(mediaOverlay);

    if (typeof fit === "undefined")
        fit = true;
    if (typeof aspectX === "undefined")
        aspectX = 16;
    if (typeof aspectY === "undefined")
        aspectY = 9;
    if (typeof alignX === "undefined")
        alignX = 0.5;
    if (typeof alignY === "undefined")
        alignY = 0;
    const autoAspect = (aspectX <= 0) || (aspectY <= 0);


    clip.classList.add(fit ? "MediaPreviewFit" : "MediaPreviewFill");

    let mediaControls = null;
    let mediaStats = null;


    function UpdateSize(ev) {
        if (ev) {
            if (!isInDocument(player.Element)) {
                window.removeEventListener("resize", UpdateSize);
                return;
            }
        }
        props = player.Params;
        if (autoAspect) {
            if (props.AdaptiveSize) {
                const pr = p.getBoundingClientRect();
                aspectX = pr.width;
                aspectY = pr.height;
            } else {
                const useClip = player.UseClip;
                const ow = player.Width;
                const oh = player.Height;
                const crw = (useClip ? player.ClipWidth : ow) ?? ow;
                const crh = (useClip ? player.ClipHeight : oh) ?? oh;
                aspectX = crw;
                aspectY = crh;
            }
        }

        const useClip = player.UseClip;
        const ow = player.Width;
        const oh = player.Height;
        const crw = (useClip ? player.ClipWidth : ow) ?? ow;
        const crh = (useClip ? player.ClipHeight : oh) ?? oh;
        if ((crw <= 0) || (crh <= 0)) {
            clip.style.width = "0";
            clip.style.height = "0";
            return;
        }
        const crx = (useClip ? player.ClipX : 0) ?? 0;
        const cry = (useClip ? player.ClipY : 0) ?? 0;

        const rect = p.getBoundingClientRect();
        let cw = rect.width;
        let ch = cw * aspectY / aspectX;
        if (ch > rect.height) {
            ch = rect.height;
            cw = ch * aspectX / aspectY;
        }

        const e = player.Element;
        const playerE = e.style;
        const overlayS = mediaOverlay.style;
        if (props.AdaptiveSize) {
            playerE.transform = null;
            playerE.width = Math.round(rect.width) + "px";
            playerE.height = Math.round(rect.height) + "px";


            clip.style.left = null;
            clip.style.top = null;
            clip.style.width = null;
            clip.style.height = null;

            if (fit) {
                const b = window.innerHeight;
                const controlBottom = rect.bottom + (b * 0.02);
                if (mediaStats)
                    mediaStats.style.top = (controlBottom | 0) + "px";
                else
                    if (mediaControls)
                        mediaControls.style.top = (controlBottom | 0) + "px";
            }

        } else {
            playerE.width = props.Width + "px";
            playerE.height = props.Height + "px";

            const ox = (rect.width - cw) * alignX;
            const oy = (rect.height - ch) * alignY;
            clip.style.left = ox + "px";
            clip.style.top = oy + "px";
            clip.style.width = cw + "px";
            clip.style.height = ch + "px";
            const scaleX = cw / crw;
            const scaleY = ch / crh;
            const scale = fit ? (scaleX < scaleY ? scaleX : scaleY) : (scaleX > scaleY ? scaleX : scaleY);

            const dx = (cw - (scale * (crw + crx * 2))) * 0.5;
            const dy = (ch - (scale * (crh + cry * 2))) * 0.5;
            const tr = "scale(" + (scale * 100) + "%) translate(" + (dx / scale) + "px, " + (dy / scale) + "px)";
            playerE.transform = tr;
            if (fit) {
                const b = window.innerHeight;
                const controlBottom = oy + ch + rect.y + (b * 0.02);
                if (mediaStats)
                    mediaStats.style.top = (controlBottom | 0) + "px";
                else
                    if (mediaControls)
                        mediaControls.style.top = (controlBottom | 0) + "px";
            }
            if (player.UseClip) {
                mediaOverlay.classList.remove("Show");
            } else {
                mediaOverlay.classList.add("Show");
                const col = "px solid rgba(255, 0, 0, 0.25)";
                const cl = player.ClipX;
                const ct = player.ClipY;
                const cr = ow - cl - player.ClipWidth;
                const cb = oh - ct - player.ClipHeight;
                const ms = mediaOverlay.style;
                if (cl > 0)
                    ms.borderLeft = cl + col;
                else
                    ms.borderLeft = null;
                if (ct > 0)
                    ms.borderTop = ct + col;
                else
                    ms.borderTop = null;

                if (cr > 0)
                    ms.borderRight = cr + col;
                else
                    ms.borderRight = null;
                if (cb > 0)
                    ms.borderBottom = cb + col;
                else
                    ms.borderBottom = null;
                player.Element.style.clipPath = null;
            }
        }
        overlayS.transform = playerE.transform;
        overlayS.width = e.clientWidth + "px";
        overlayS.height = e.clientHeight + "px";
    }
    UpdateSize();
    player.OnClipChanged = () => {

        UpdateSize();
    }

    new ResizeObserver(() => UpdateSize()).observe(p);
//    window.addEventListener("resize", UpdateSize);
    cache.remove();

    let setButtonState = () => { };

    if (!noStats) {

        const p = document.createElement("Media-Stats");
        mediaStats = p;
        target.appendChild(p);

        function createStats(name, title, className) {
            const statE = document.createElement("Media-Stat");
            if (title)
                statE.title = title + "\n\nClick to copy value to the clipboard.";
            else
                statE.title = "Click to copy value to the clipboard.";
            if (className)
                statE.classList.add(className);
            p.appendChild(statE);
            const nameE = document.createElement("Media-StatName");
            nameE.innerText = name + ":";
            statE.appendChild(nameE);

            const val = document.createElement("Media-StatValue");
            val.innerText = "-";
            val.Value = 0;
            statE.appendChild(val);
            let currentText = "-";
            const statStyle = statE.classList;
            function setText(newText, newValue) {
                if (!newText) {
                    statStyle.add("Hide");
                    return;
                }
                statStyle.remove("Hide");
                if (newText === currentText)
                    return;
                currentText = newText;
                val.innerText = newText;
                val.Value = typeof newValue === "undefined" ? newText : newValue;
            }
            statE.onclick = ev => {
                if (badClick(ev))
                    return;
                ValueFormat.copyToClipboard(name + ": " + val.innerText + " (" + val.Value + ")");
            };
            keyboardClick(statE);
            return setText;
        }

        let width, height, gpuTime, compileTime, sVolume;
        const time = createStats("Time", "The current position in seconds");
        const duration = createStats("Duration", "The duration of the media in seconds, less than zero means unknown or infinity");
        if ((typeof player.AvgDrawTimeMs !== "undefined") && (typeof player.Query !== "undefined"))
            gpuTime = createStats("GPU time", "The GPU time spent rendering the effect in ms");
        if (typeof player.CompilationTime !== "undefined")
            compileTime = createStats("Build", "The duration of the compilation step of this effect effect in ms");
        if (player.HaveAudio)
            sVolume = createStats("Volume", "The current audio volume of the media as a percentage");
        if (player.HaveVisual) {
            width = createStats("Width", "The width in pixels of the media");
            height = createStats("Height", "The height in pixels of the media");
        }
        let adjustTime, adjustDuration;
        if (typeof props.StartAt !== "undefined") {
            adjustTime = createStats("Trimmed time", "The current position in the trimmed period in seconds", "Adjust");
            adjustDuration = createStats("Trimmed duration", "The duration of the trimmed period in seconds", "Adjust");
        }
        let clipWidth, clipHeight, clipX, clipY;
        if (player.HaveVisual && (typeof props.Crop !== "undefined")) {
            clipWidth = createStats("Cropped width", "The cropped width in pixels", "Clip");
            clipHeight = createStats("Cropped height", "The cropped height in pixels", "Clip");
            clipX = createStats("Cropped left", "The number of cropped pixels on the left", "Clip");
            clipY = createStats("Cropped top", "The number of cropped pixels on the top", "Clip");
        }
        let renderWidth, renderHeight;
        if (player.HaveVisual && (typeof props.AdaptiveSize !== "undefined")) {
            renderWidth = createStats("Render width", "The actual rendered width in pixels", "Actual");
            renderHeight = createStats("Render height", "The actual rendered height in pixels", "Actual");
        }


        function updateStats(atime) {

            if (typeof atime !== "undefiend")
                if (!isInDocument(p))
                    return;
            const par = player.Params;
            if (width) {
                try {
                    const sv = player.Width;
                    width(sv + " px", sv);
                }
                catch
                {
                }
                try {
                    const sv = player.Height;
                    height(sv + " px", sv);
                }
                catch
                {
                }
            }
            try {
                const sv = player.GetPos();
                time(sv.toFixed(2) + " s",  sv);
            }
            catch
            {
            }
            try {
                const sv = player.Duration;
                duration(sv < 0 ? "?" : (sv.toFixed(2) + " s"), sv);
            }
            catch
            {
            }
            if (sVolume) {
                try {
                    const sv = player.GetTrueVolume();
                    sVolume((sv * 100).toFixed(1) + " %", sv);
                }
                catch
                {
                }
            }
            if (gpuTime) {
                try {
                    let sv = player.AvgDrawTimeMs;
                    if (sv <= 0)
                        sv = player.TotalTime / Math.max(1, player.MeasureCount);
                    gpuTime(sv.toFixed(3) + " ms", sv);
                }
                catch
                {
                }
            }
            if (compileTime) {
                try {
                    const sv = player.CompilationTime;
                    if (sv < 0) {
                        switch (sv) {
                            case -3:
                                compileTime("error");
                                break;
                            default:
                                compileTime("compiling");
                                break;
                        }
                    } else {
                        compileTime(sv.toFixed(1) + " ms", sv);
                    }
                }
                catch
                {
                }
            }

            
            if (adjustTime) {
                try {
                    const b = par.StartAt;
                    if (b > 0) {
                        const sv = player.GetPos() - b;
                        adjustTime(sv.toFixed(2) + " s", sv);
                    } else {
                        adjustTime();
                    }
                }
                catch
                {
                }
            }
            if (adjustDuration) {
                try {
                    const b = par.StartAt;
                    const e = par.EndAt;
                    if ((b > 0) || (e > b)) {
                        let ee = e > b ? e : player.Duration;
                        ee -= b;
                        adjustDuration(ee.toFixed(2) + " s", ee);
                    } else {
                        adjustDuration();
                    }
                }
                catch
                {
                }
            }
            if (clipWidth) {
                try {
                    const cw = player.ClipWidth;
                    const ch = player.ClipHeight;
                    const cx = player.ClipX;
                    const cy = player.ClipY;
                    if ((cw !== player.Width) || (ch !== player.Height) || (cx !== 0) || (cy !== 0)) {
                        clipWidth(cw + " px", cw);
                        clipHeight(ch + " px", ch);
                        clipX(cx + " px", cx);
                        clipY(cy + " px", cy);
                    } else {
                        clipWidth();
                        clipHeight();
                        clipX();
                        clipY();
                    }
                }
                catch
                {
                }
            }
            if (renderWidth) {
                try {
                    if (props.AdaptiveSize) {
                        const res = player.Element;
                        const rsw = res.width;
                        const rsh = res.height;
                        renderWidth(rsw + "px", rsw);
                        renderHeight(rsh + "px", rsh);
                    } else {
                        renderWidth();
                        renderHeight();
                    }
                }
                catch
                {

                }
            }
            requestAnimationFrame(updateStats);
        }
        updateStats();
    }
    if (!noControls) {

        const p = document.createElement("Media-Controls");
        mediaControls = p;
        target.appendChild(p);


        
        UpdateSize();


        const bs = new ButtonStyle();
        bs.ButtonElement = "Media-Button";


        const canPlay = player.CanPlay;
        if (canPlay) {

            const buttonMute = new Button(bs, "Mute", "Click to mute media", "MediaIconMute", !player.Muted, async button => {
                localStorage.setItem("QuizzWeaver.MediaMute", "true");
                await player.Mute();
                return setButtonState;
            });

            const buttonUnMute = new Button(bs, "Un mute", "Click to un-mute media", "MediaIconUnMute", player.Muted, async button => {
                localStorage.setItem("QuizzWeaver.MediaMute", "false");
                await player.UnMute();
                return setButtonState;
            });

            const buttonPause = new Button(bs, "Pause", "Click to pause media", "MediaIconPause", !player.Paused, async button => {
                await player.Pause();
                return setButtonState;
            });

            const buttonPlay = new Button(bs, "Play", "Click to play media", "MediaIconPlay", player.Paused, async button => {
                await player.Play();
                return setButtonState;
            });

            const buttonStop = new Button(bs, "Stop", "Click to stop playing media", "MediaIconStop", true, async button => {
                await player.Stop();
                return setButtonState;
            });


            const buttonLoop = new Button(bs, "Loop", "Click to loop media", "MediaIconLoop", !player.Looping, async button => {
                localStorage.setItem("QuizzWeaver.MediaLoop", "true");
                await player.Loop();
                return setButtonState;
            });

            const buttonOnce = new Button(bs, "Once", "Click to play media once then stop", "MediaIconOnce", player.Looping, async button => {
                localStorage.setItem("QuizzWeaver.MediaLoop", "false");
                await player.Once();
                return setButtonState;
            });

            const buttonVol100 = new Button(bs, "VOL: 100%", "Click to set the volume to 100%", "MediaIconVolLoud", player.Volume != 1, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "1");
                await player.SetVolume(1);
                return setButtonState;
            });

            const buttonVol75 = new Button(bs, "VOL: 75%", "Click to set the volume to 75%", "MediaIconVolLoud", player.Volume != 0.75, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.75");
                await player.SetVolume(0.75);
                return setButtonState;
            });

            const buttonVol50 = new Button(bs, "VOL: 50%", "Click to set the volume to 50%", "MediaIconVolLow", player.Volume != 0.5, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.5");
                await player.SetVolume(0.5);
                return setButtonState;
            });

            const buttonVol25 = new Button(bs, "VOL: 25%", "Click to set the volume to 25%", "MediaIconVolLow", player.Volume != 0.25, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.25");
                await player.SetVolume(0.25);
                return setButtonState;
            });

            setButtonState = () => {
                buttonMute.SetEnabled(!player.Muted);
                buttonUnMute.SetEnabled(player.Muted);
                buttonPause.SetEnabled(!player.Paused);
                buttonPlay.SetEnabled(player.Paused);
                //buttonStop.SetEnabled(!player.Paused);

                buttonLoop.SetEnabled(!player.Looping);
                buttonOnce.SetEnabled(player.Looping);


                buttonVol100.SetEnabled(player.Volume != 1);
                buttonVol75.SetEnabled(player.Volume != 0.75);
                buttonVol50.SetEnabled(player.Volume != 0.5);
                buttonVol25.SetEnabled(player.Volume != 0.25);
            };

            const haveAudio = player.HaveAudio;
            p.appendChild(buttonPause.Element);
            p.appendChild(buttonPlay.Element);
            p.appendChild(buttonStop.Element);

            p.appendChild(buttonLoop.Element);
            p.appendChild(buttonOnce.Element);

            if (haveAudio) {
                p.appendChild(buttonMute.Element);
                p.appendChild(buttonUnMute.Element);
                p.appendChild(buttonVol100.Element);
                p.appendChild(buttonVol75.Element);
                p.appendChild(buttonVol50.Element);
                p.appendChild(buttonVol25.Element);
            }
        }
        const buttonShow = new Button(bs, "Source", "Click to open the source in a new tab", "MediaIconExpand", true, async button => {
            Open(player.Source);
        });
        p.appendChild(buttonShow.Element);

        player.MessageElement = p;
        p.addEventListener("mediaEnded", ev => setButtonState());
    }
    player.SetButtonState = setButtonState;
    player.UpdateSize = UpdateSize;
    player.TopElement = p;

    return [player, mediaControls, mediaStats];
}
async function mediaPlayWithPopup(player) {
    if (localStorage.getItem("QuizzWeaver.MediaLoop") !== "true")
        await player.Once();
    else
        await player.Loop();
    player.SetVolume(parseFloat(localStorage.getItem("QuizzWeaver.MediaVolume") ?? "0.25"));
    if (!player.HaveAudio) {
        if (player.CanPlay)
            await player.Play();
        player.SetButtonState();
        return;
    }
    if (await MediaPlayerTools.CanUnMute()) {
        if (localStorage.getItem("QuizzWeaver.MediaMute") !== "true")
            await player.UnMute();
        await player.Play();
        player.SetButtonState();
        return;
    }
    if (localStorage.getItem("QuizzWeaver.MediaMute") === "true") {
        await player.Play();
        player.SetButtonState();
        return;
    }
    const e = document.createElement("Media-NoAudio");
    e.innerText = "Must click once to get audio!";
    e.title = "Click anywhere!";
    e.onclick = async ev => {
        if (badClick(ev))
            return;
        await player.UnMute();
        await player.Play();
        player.SetButtonState();
        e.remove();
    }
    document.body.appendChild(e);
}
async function mediaEditProps(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix) {
    let player = playerRes[0];
    const editE = document.createElement("Media-Edit");
    target.appendChild(editE);
    let mediaStats = playerRes[2];
    let mediaControls = playerRes[1];
/*    if (mediaStats)
        editE.appendChild(mediaStats);
    if (mediaControls)
        editE.appendChild(mediaControls);
*/

    if (props) {
        if (Object.keys(props).length > 1) {
            if (props.Crop)
                props.Crop.VisualizeMargins = false;

            function GetApplyState() {

                const oldVol = props.Volume;
                delete props.Volume;
                const ret = JSON.stringify(props);
                if (typeof oldVol !== "undefined")
                    props.Volume = oldVol;
                return ret;
            }


//            editE.appendChild(document.createElement("br"));
//            editE.appendChild(document.createElement("br"));
            const typeInfo = await sendRequest('../edit/GetTypeInfo', props["$type"], false);
            if (player.ScriptTypeInfo) {
                Edit.AddType(player.ScriptTypeInfo);
                props.EffectProps = player.Params.EffectProps;
                typeInfo.Members.push(player.ScriptMember);
            }

            //const canEdit = (info.Edit & QuizzRights.UpdatePrivateData) !== 0;
            const options = new EditOptions();
            options.ReadOnly = !canEdit;
            options.Title = false;
            options.Dev = canEdit & options.Dev;
            options.UndoStack = new UndoStack();

            const input = new Edit(typeInfo, null, options);
            await input.SetObject(props);
            editE.appendChild(input.Element);
            if (canEdit) {
                const commands = document.createElement("SysWeaver-CenterBlock");
                input.Element.appendChild(commands);


                let applyButton = null;

                async function reload() {

                    applyButton.Disable();
                    const oldP = player;
                    const pVolume = player.Volume;
                    const pMuted = player.Muted;
                    const pPaused = player.Paused;
                    const pLooping = player.Looping;
                    const pPos = player.GetPos();
                    const orgE = player.OnEnded;
                    const prevE = player.TopElement;
                    const pd = await mediaCreatePlayer(target, type, data, props, !mediaControls, prevE, !mediaStats);
                    player = pd[0];
                    if (pd[1])
                        mediaControls.parentElement.replaceChild(pd[1], mediaControls);
                    if (pd[2])
                        mediaStats.parentElement.replaceChild(pd[2], mediaStats);
                    mediaControls = pd[1];
                    mediaStats = pd[2];
                    player.OnEnded = orgE;

                    await player.SetVolume(pVolume);
                    if (!pMuted)
                        await player.UnMute();
                    if (!pLooping)
                        await player.Once();
                    if (!pPaused)
                        await player.Play();
                    if (pPos > 0)
                        await player.Seek(pPos);

                    oldP.TopElement.remove();

                    player.TopElement.classList.remove("Replacing");
                    await oldP.OnDispose();


                    player.SetButtonState();
                    applyState = GetApplyState();
                };



                let objState = JSON.stringify(input.GetObject());
                let saveButton = null;
                if (saveFn) {
                    saveButton = new Button(null, "Save changes", "Click to save changes", "IconSave", false, async () => {
                        saveButton.Disable();
                        saveButton.StartWorking("IconWorking");
                        try {
                            if (await saveFn(reload)) {
                                if (messagePrefix)
                                    InterOp.Post(messagePrefix + "Reload", props);
                                saveButton.StopWorking();
                                Info("Saved updates!");
                                return;
                            }
                            Fail("Failed to update!");
                        }
                        catch (e) {
                            Fail("Failed to update!\n" + e);
                        }
                        saveButton.StopWorking();
                        saveButton.Enable();
                    });
                    commands.appendChild(saveButton.Element);
                }

                let applyState = GetApplyState();
                applyButton = new Button(null, "Apply changes", "Apply the changes that can't be applied on the fly.\nWithout saving.", "IconApply", false, async () => {
                    applyButton.StartWorking("IconWorking");
                    await reload();
                    if (messagePrefix)
                        InterOp.Post(messagePrefix + "Reload", props);
                    applyButton.StopWorking();
                });
                commands.appendChild(applyButton.Element);



                let clip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                input.Element.addEventListener("EditChange", async ev => {
                    let needApply = true;
                    const mn = ev.detail.Member.Name;
                    if (mn === "Volume") {
                        player.Params.Volume = props.Volume;
                        await player.ResetVolume();
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "SetVolume", { Volume: props.Volume });
                        needApply = false;
                    }
                    if (mn === "Speed") {
                        player.Params.Speed = props.Speed;
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "SetProps", { Speed: props.Speed });
                        needApply = false;
                    }
                    if (mn === "AdaptiveSize") {
                        player.UpdateSize();
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "UpdateSize", { AdaptiveSize: props.AdaptiveSize });
                        needApply = false;
                    }
                    if (mn === "Crop") {
                        const newClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                        if (newClip !== clip) {
                            clip = newClip;
                            player.UseClip = clip;
                            player.ApplyClip(props);
                            player.UpdateSize();
                        } else {
                            player.ApplyClip(props);
                        }
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "Crop", { Crop: props.Crop });
                        needApply = false;
                    }
                    if (mn === "EffectProps"){
                        try {
                            player.Params.EffectProps = props.EffectProps;
                            player.LazyCompile();
                            needApply = false;
                            props.FxProps = JSON.stringify(props.EffectProps);
                            if (messagePrefix)
                                InterOp.Post(messagePrefix + "Compile", { EffectProps: props.EffectProps });
                        }
                        catch (e)
                        {
                            console.warn("Failed to compile, error: \n" + e);
                        }
                    }
                    if (saveButton)
                        saveButton.SetEnabled(JSON.stringify(props) !== objState);
                    const newApplyState = GetApplyState();
                    if (needApply)
                        applyButton.SetEnabled(newApplyState !== applyState);
                    else
                        applyState = newApplyState;
                });


            }

        }
    }

}
async function mediaEditMain() {
    let target = document.body;

    const pageBack = document.createElement("SysWeaver-Page");
    pageBack.classList.add("Media");
    target.appendChild(pageBack);
    const page = document.createElement("Media-Full");
    target.appendChild(page);
    target = page;
    try {
        const ps = getUrlParams();
        const getApi = ps.get('get');
        const aidT = ps.get('aid');
        if (!aidT) {
            Fail("No assignment id parameter 'aid' supplied!", 400);
            return;
        }
        const aid = JSON.parse(aidT);
        let updateApi = ps.get('update');
        if (!updateApi) {
            updateApi = getApi.replace("MediaGetAssignedInfo", "MediaSet");
            if (updateApi != getApi) {
                updateApi += "Data";
            } else {
                updateApi = updateApi.replace("Get", "Update");
            }
        }


        const info = await sendRequest(getApi, aid);
        props = info.Data;
        const type = info.Type;
        const data = info.Link;



        const pd = await mediaCreatePlayer(target, type, data, props);
        let player = pd[0];
        if (!props)
            props = JSON.parse(JSON.stringify(player.Params));
        player.OnLoop = () => console.log("Loop");
        const orgE = player.OnEnded;
        player.OnEnded = () => {
            console.log("End");
            if (orgE)
                return orgE();
        };
        //  Collapse expand
        const exp = document.createElement("SysWeaver-CenterBlock");
        target.appendChild(exp);
        let collapseButton;
        const pcl = page.classList;
        let didPlay;
        function toggleCollape() {
            if (pcl.contains("Hide")) {
                pcl.remove("Hide");
                collapseButton.ChangeText("Collapse");
                collapseButton.ChangeTitle("Click to hide the media player");
                collapseButton.ChangeImage("IconMediaCollapse");
                if (didPlay)
                    player.Play();
            } else {
                didPlay = !player.Paused;
                player.Pause();
                pcl.add("Hide");
                collapseButton.ChangeText("Expand");
                collapseButton.ChangeTitle("Click to show the media player");
                collapseButton.ChangeImage("IconMediaExpand");
            }
        };
        collapseButton = new Button(null, "Collapse", "Click to hide the media player", "IconMediaCollapse", true, toggleCollape);
        exp.appendChild(collapseButton.Element);

        const messagePrefix = YaMD5.hashAsciiStr("" + getApi + "_" + aid + "_" + type + "_" + data);
        const newWindowButton = new Button(null, "New window", "Click to open the media player in a new window", "IconMediaNewWindow", true, () => {
            if (!pcl.contains("Hide"))
                toggleCollape();
            Open("MediaView.html?type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + JSON.stringify(props) + "&ctrl=" + messagePrefix, "_blank");

        });
        exp.appendChild(newWindowButton.Element);

        //  Props
        const canEdit = (info.Edit & QuizzRights.UpdatePrivateData) !== 0;
        await mediaEditProps(target, pd, type, data, props, canEdit, async reload =>
        {
            const r = {};
            if (typeof (aid) === "number")
                r.AssignmentId = aid;
            else
                Object.assign(r, aid);
            r.Data = props;
            if (await sendRequest('../quizz/' + updateApi, r, false)) {
                objState = JSON.stringify(props);
                await reload();
                return true;
            }
            return false;
        }, messagePrefix);
    //  Play
        await mediaPlayWithPopup(player);

        if (messagePrefix) {
            InterOp.AddListener(ev => {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const name = type.substring(messagePrefix.length);
                if (name === "GetProps") {
                    const dm = {};
                    Object.assign(dm, props);
                    InterOp.Post(messagePrefix + "SetProps", dm);
                }
            });
            {
                const dm = {};
                Object.assign(dm, props);
                InterOp.Post(messagePrefix + "Reload", dm);
            }   

        }

    }
    catch (e) {
        target.innerText = "";
        Fail("Generic failure.\n" + e);
        return;
    }
    PageLoaded();
}
async function mediaViewMain() {
    const target = document.body;
    try {
        const ps = getUrlParams();
        const stats = !ps.has('no_stats');
        const controls = !ps.has('no_controls');
        let type = ps.get('type') ?? ps.get('t');
        if (!type) {
            Fail("No type parameter 'type' supplied!");
            return;
        }
        type = parseInt(type);

        const data = ps.get('link') ?? ps.get('d');
        if (!data) {
            Fail("No link parameter 'link' supplied!");
            return;
        }
        let props = ps.get('props');
        if (props) {
            props = JSON.parse(props);
        } else {
            props = null;
        }

        const messagePrefix = ps.get('ctrl');

        if (messagePrefix) {
            let newProps = null;
            function getPropsFn(ev) {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const name = type.substring(messagePrefix.length);
                if (name !== "SetProps")
                    return;
                newProps = {};
                Object.assign(newProps, m);
                delete newProps.Type;
                delete newProps.From;
                delete newProps.MagicXyz;
                target.dispatchEvent(new Event("MediaGotProps"));
            }
            InterOp.AddListener(getPropsFn);
            await waitEvent(target, "MediaGotProps", () => InterOp.Post(messagePrefix + "GetProps"), 100);
            InterOp.RemoveListener(getPropsFn);
            if (newProps)
                props = newProps;
        }

        const mview = document.createElement("Media-View");
        target.appendChild(mview);
/*
        target.onclick = ev => {
            const tw = target.clientWidth;
            const th = target.clientHeight;

            let size = Math.min(tw, th) * 0.1;
            if (size < 16)
                size = 16;
            if (size > 96)
                size = 96;
            if (ev.clientY >= size)
                return;
            if (ev.clientX < (tw - size))
                return;
            Open("MediaPreview.html?type=" + type + "&link=" + data + "&props=" + JSON.stringify(player.Params), "_self");
        };
*/
        const pd = await mediaCreatePlayer(mview, type, data, props, !controls, null, !stats, false, 0, 0, 0.5, 0.5);
        let player = pd[0];

        const showFull = () => Open("MediaPreview.html?full&back&pos=" + player.GetPos() + "&type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + JSON.stringify(player.Params), "_self");

        player.Element.parentElement.onclick = showFull;

        let mediaControls = null;
        if (pd[1])
            mediaControls = pd[1];
        let mediaStats = null;
        if (pd[2])
            mediaStats = pd[2];
        props = player.Params;

        player.OnLoop = () => console.log("Loop");
        const orgE = player.OnEnded;
        player.OnEnded = () => {
            console.log("End");
            if (orgE)
                return orgE();
        };
        async function reload() {

            const oldP = player;
            const pVolume = player.Volume;
            const pMuted = player.Muted;
            const pPaused = player.Paused;
            const pLooping = player.Looping;
            const pPos = player.GetPos();
            const orgE = player.OnEnded;
            const prevE = player.TopElement;
            const pd = await mediaCreatePlayer(mview, type, data, props, !controls, prevE, !stats, false, 0, 0, 0.5, 0.5);
            player = pd[0];
            player.Element.parentElement.onclick = showFull;
            if (pd[1])
                mediaControls.parentElement.replaceChild(pd[1], mediaControls);
            if (pd[2])
                mediaStats.parentElement.replaceChild(pd[2], mediaStats);
            mediaControls = pd[1];
            mediaStats = pd[2];
            player.OnEnded = orgE;
            props = player.Params;

            await player.SetVolume(pVolume);
            if (!pMuted)
                await player.UnMute();
            if (!pLooping)
                await player.Once();
            if (!pPaused)
                await player.Play();
            if (pPos > 0)
                await player.Seek(pPos);

            oldP.TopElement.remove();

            player.TopElement.classList.remove("Replacing");
            await oldP.OnDispose();


            player.SetButtonState();
        };



        if (messagePrefix) {
            InterOp.AddListener(ev => {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const clip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                Object.getOwnPropertyNames(m).forEach(pn => {
                    if (typeof props[pn] === "undefined")
                        return;
                    props[pn] = m[pn];
                });
                const name = type.substring(messagePrefix.length);
                if (name === "Reload")
                    reload();
                if (name === "SetVolume")
                    player.ResetVolume();
                if (name === "UpdateSize")
                    player.UpdateSize();

                if (name === "Crop") {
                    player.UseClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                    player.ApplyClip(props);
                    player.UpdateSize();
                }
                if (name === "Compile")
                    player.LazyCompile();
            });
        }
        await mediaPlayWithPopup(player);


    }
    catch (e) {
        target.innerText = "";
        Fail("Generic failure.\n" + e);
        return;
    }
    PageLoaded();
}

class SsHask {
    isSupported() {
        console.log("isSupported()");
    }

    setSize(width, height) {
        console.log("setSize(" + width + ", " + height + ")");
    }

    doIt(duration, fps, error) {
        console.log("doIt(" + duration + ", " + fps + ", " + error + ")");

    }
}



async function mediaPreviewMain() {
    const target = document.body;
    try {
        const ps = getUrlParams();
        target.classList.add("MediaPreview");
        if (window["CefSharp"])
            await CefSharp.BindObjectAsync();
        function GetHo() {
            try {
                return chrome.webview.hostObjects.ScreenShotHost ?? null;
            }
            catch
            {
                return null;
            }
        }

        const shost = window["ScreenShotHost"] ?? GetHo() ?? (ps.has("hack") ? new SsHask() : null);
        if (shost) {
            await shost.isSupported();
            target.classList.add("NoFade");
        }
        const fill = ps.has('fill') || shost;
        const full = ps.has('full');
        const back = ps.has('back');
        let type = ps.get('type') ?? ps.get('t');
        if (!type) {
            Fail("No type parameter 'type' supplied!");
            return;
        }
        type = parseInt(type);
        const data = ps.get('link') ?? ps.get('d');
        if (!data) {
            Fail("No link parameter 'link' supplied!");
            return;
        }
        let pos = ps.get("pos")
        pos = pos ? parseFloat(pos) : 0;
        let props = ps.get('props');
        if (props) {
            props = JSON.parse(props);
        } else {
            props = null;
        }
        let enabledFull = true;
        if (full) {
            await FullScreen.Enter();
            const onInteraction = async () => {
                target.removeEventListener("keyup", onInteraction);
                target.removeEventListener("click", onInteraction);
                await FullScreen.Enter();
                setTimeout(() => enabledFull = true, 100);
            }
            enabledFull = FullScreen.IsFull();
            target.addEventListener("keyup", onInteraction);
            target.addEventListener("click", onInteraction);
        }
        let enabledAudio = true;
        if (back) {
            target.addEventListener("click", () => {
                if (!enabledAudio)
                    return;
                if (!enabledFull)
                    return;
                history.go(-1);
            });
        }

        const player = MediaPlayer.Create(type, data, props);
        const e = player.Element;
        e.draggable = false;
        const es = e.style;
        target.appendChild(e);
        try {
            await player.Cache(true);
            const pw = player.Width;
            const ph = player.Height;
            const adapt = !!player.Params.AdaptiveSize;
            function updateSize() {
                return MediaPlayer.UpdateSize(target, player, fill);
            }
            new ResizeObserver(updateSize).observe(target);
            if (pos > 0)
                await player.Seek(pos);
            if (player.HaveAudio) {
                if (await MediaPlayerTools.CanUnMute()) {
                    await player.UnMute();
                } else {
                    enabledAudio = false;
                    const onInteraction = () => {
                        target.removeEventListener("keyup", onInteraction);
                        target.removeEventListener("click", onInteraction);
                        player.UnMute();
                        setTimeout(() => enabledAudio = true, 100);
                    }
                    target.addEventListener("keyup", onInteraction);
                    target.addEventListener("click", onInteraction);
                }
            }
            await delay(50);
            const clippedSize = updateSize();
            if (shost) {
                if (adapt) {
                    const ar = player.AspectRatio;
                    if (ar) {
                        const aw = (window.innerHeight * ar) | 0;
                        if (window.innerWidth !== aw) {
                            await waitEvent(window, "resize", async () => {
                                await shost.setSize(aw, window.innerHeight);
                            }, 1000);
                            await delay(200);
                        }
                    }
                } else {
                    if ((window.innerWidth !== clippedSize[0]) || (window.innerHeight !== clippedSize[1])) {
                        await waitEvent(window, "resize", async () => {
                            await shost.setSize(clippedSize[0], clippedSize[1]);
                        }, 1000);
                        await delay(200);
                    }
                }
            }
            await player.Play();
            await player.Show();
        }
        catch (e) {
            if (shost)
                await shost.doIt(0, 0, "" + e);
            Fail(e);
        }
        if (shost) {
            await delay(200);
            if (typeof player.ResetCounters !== "undefined") {
                player.ResetCounters();
                await delay(600);
            }
            const tot = player.TotalTime ?? 0;
            const count = player.MeasureCount ?? 0;
            await player.Pause();
            await delay(200);
            await player.Seek(pos);
            await delay(200);
            let fps = tot > 0 ? (1000.0 * count / tot) : 0;
            await shost.doIt(player.Duration ?? 0, fps, null);
        }
    }
    catch (e) {
        target.innerText = "Generic failure.\n" + e;
        return;
    }
    PageLoaded();
}

