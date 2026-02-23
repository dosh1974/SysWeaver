class MediaPlayerParamsEffect {
    Width = 1920;
    Height = 1080;
    Speed = 1;
    LimitSize = false;
    DpiAdjust = true;
    DpiScale = 1;
    Static = false;
    Transparent = false;
    AdaptiveSize = true;
    WrapU = false;
    WrapY = false;
    Texture = null;
    WebGL2 = false;
    MipMap = false;
    AntiAlias = false;
    ScrollId = null;
    MouseId = null;
}

class MediaPlayerEffect {

    static DpiScale = parseFloat(localStorage.getItem("SysWeaver.Media.EffectDpiScale") ?? "1");


    static GetElementFromId(id) {
        const tw = top;
        if (!id)
            return tw.document.body;
        try {
            let win = window;
            for (let j = 0; j < 10; ++j) {
                const i = win.document.getElementById(id);
                if (i)
                    return i;
                if (win == tw)
                    break;
                const pw = win.parent;
                if (pw === win)
                    break;
                win = pw;
                if (!win)
                    break;
            }
        }
        catch {
        }
        return tw.document.body;
    }

    constructor(url, params, createTextureFn) {
        const t = this;
        const cp = new MediaPlayerParamsEffect();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("canvas");
        const glp = {
            antialias: params.AntiAlias,
            depth: false,
            stencil: false,
            alpha: params.Transparent,
            preserveDrawingBuffer: false,
        };
        let glver = params.WebGL2 ? "webgl2" : "webgl";
        let gl = e.getContext(glver, glp);
        if (!gl) {
            if (glp.antialias) {
                glp.antialias = false;
                gl = e.getContext(glver, glp);
            }
        }
        if (!gl)
            throw new Error("Can't initiate WebGL!");
        t.GL = gl;

        gl.getExtension("OES_standard_derivatives");

        const pos = gl.createBuffer(); 
        t.Pos = pos;
        t.ResumeTime = 0;
        t.CurrentTime = 0;
        t.Rendered = false;
        t.Program = null;
        gl.bindBuffer(gl.ARRAY_BUFFER, pos);
        const positions = [
            -1, 1,
            3, 1,
            -1, -3,
        ];
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);
        if (createTextureFn)
            t.Texture = createTextureFn(gl);
        const w = params.Width ? params.Width : 1280;
        const h = params.Height ? params.Height : (w * 9 / 16);
        e.style.width = w + "px";
        e.style.height = h + "px";
        t.HaveVisual = true;
        this.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.CurrentPosition = 0;
        MediaPlayerTools.InitBase(this, e, params);
        t.Width = w;
        t.Height = h;
        t.CompilationTime = -1;
        t.ResetCounters();
        const ext = gl.getExtension("EXT_disjoint_timer_query");
        if (ext) {
            t.Query =
            {
                E: ext,
                Q: ext.createQueryEXT(),
                IsQ: false,
            };
        }
        t.ApplyClip();
    }

    ApplyClip(p) {
        const t = this;
        if (t.Texture) {
            t.Texture.ApplyClip(p);
            t.ApplyTextue = true;
            t.GetRender()();
        }
    }

    async OnDispose() {
        const t = this;
        await t.Pause();
        while (t.IsAnimating)
            await delay(1);
        const gl = t.GL;
        if (t.Program)
            t.Program.Dispose();
        gl.deleteBuffer(t.Pos);
    }

    ResetCounters() {
        const t = this;
        t.TotalTime = 0;
        t.MeasureCount = 0;
        t.AvgDrawTimeMs = 0;
        t.PrintCounter = 0;
    }


    LazyCompile() {
        const t = this;
        if (t.Compiling) {
            t.CompileAgain = true;
            return;
        }
        t.Compiling = true;
        setTimeout(async () => {
            for (; ;) {
                t.CompileAgain = false;
                await t.Compile(() => t.CompileAgain);
                if (!t.CompileAgain)
                    break;
            }
            t.Compiling = false;
        }, 50);
    }

    GetRender() {
        const t = this;
        const params = t.Params;
        if ((!t.IsAnimating) || params.Static)
            return () => {
                t.Rendered = false;
                if (IsAttached(t.Element))
                    MediaPlayerEffect.render(t);
            }
        return () => { };
    }


    async Compile(abortCheckFn) {
        const t = this;
        t.CompilationTime = -1;
        const program = await t.ProgramData.Compile(t.Params.EffectProps, "attribute vec2 position;void main(){gl_Position=vec4(position, 0, 1);}", abortCheckFn);
        if (!program) {
            t.CompilationTime = -3;
            return false;
        }
        if (program === "Aborted") {
            t.CompilationTime = -2;
            return program;
        }
        const renderUpdate = abortCheckFn ? t.GetRender() : () => { };
        const p = program.Program;
        const gl = t.GL;
        gl.useProgram(p);
        t.AttrPos = gl.getAttribLocation(p, "position");
        //  Time
        const pt = gl.getUniformLocation(p, "time") ?? gl.getUniformLocation(p, "iTime");
        t.UniformTime = pt ? time => gl.uniform1f(pt, time) : time => { };
        //  Time delta
        const ptd = gl.getUniformLocation(p, "iTimeDelta");
        t.UniformTimeDelta = ptd ? timeDelta => gl.uniform1f(ptd, timeDelta) : timeDelta => { };
        //  Resolution
        t.UniformResolution = (w, h) => { };
        const pr = gl.getUniformLocation(p, "resolution");
        if (pr)
            t.UniformResolution = (w, h) => gl.uniform2f(pr, w, h);
        const pr3 = gl.getUniformLocation(p, "iResolution");
        if (pr3)
            t.UniformResolution = (w, h) => gl.uniform3f(pr3, w, h, 0);
        //  Frame
        const pf = gl.getUniformLocation(p, "iFrame");
        t.UniformFrameIndex = pf ? frameIndex => gl.uniform1i(pf, frameIndex) : frameIndex => { };
        //  Date
        const pd = gl.getUniformLocation(p, "iDate");
        t.UniformDate = pd ? date => gl.uniform4f(pd, date.getFullYear(), date.getMonth() + 1, date.getDate(), date.getSeconds() + date.getMinutes() * 60 + date.getHours() * 3600) : date => { };
        //  Mouse
        const pm = gl.getUniformLocation(p, "mouse");
        const pm2 = gl.getUniformLocation(p, "iMouse");
        t.UniformMouse = (x, y) => {
            if (pm)
                gl.uniform2f(pm, x, y);
            if (pm2)
                gl.uniform4f(pm2, x, y, 0, 0);
        };
        //  Scroll
        const sc = gl.getUniformLocation(p, "scroll");
        t.UniformScroll = (x, y, rx, ry) => { };
        if (sc)
            t.UniformScroll = (x, y, rx, ry) => gl.uniform4f(sc, x, y, rx, ry);


        const tex = t.Texture;
        if (tex) {
            const tu = gl.getUniformLocation(p, "tex");
            const tuv = gl.getUniformLocation(p, "texUv");
            const ts = gl.getUniformLocation(p, "texSize");
            if (tu) {
                gl.activeTexture(gl.TEXTURE0);
                tex.Apply(tuv, ts);
                gl.uniform1i(tu, 0);
            }
        }
        gl.enableVertexAttribArray(t.AttrPos);
        gl.bindBuffer(gl.ARRAY_BUFFER, t.Pos);
        gl.vertexAttribPointer(t.AttrPos, 2, gl.FLOAT, false, 0, 0);
        if (t.Program)
            t.Program.Dispose();
        t.Program = program;
        t.CompilationTime = program.CompilationTime;
        if (abortCheckFn)
            t.GetRender()();
        return true;
    }

    async Cache(keepHidden) {
        const t = this;
        const e = t.Element;
        const c = t.Url;
        const gl = t.GL;
        const props = t.Params;
        let src;
        const r = new Request(c, {
            method: "GET",
            mode: "cors",
            cache: "default",
        });
        const res = await fetch(r);
        //const contentType = res.headers.get('Content-Type');
        if (res.status != 200) {
            console.warn("Failed to load effect \"" + c + "\"");
            return false;
        }
        src = await res.text();

        const programData = new EffectProgramData(gl, src, c);
        t.ProgramData = programData;
        props.FxProps = programData.ValidateVars(props.FxProps);
        if (props.FxProps) {
            props.EffectProps = JSON.parse(props.FxProps);
            t.ScriptMember = programData.ScriptMember;
            t.ScriptTypeInfo = programData.ScriptTypeInfo;
        }
        else {
            delete props.EffectProps;
            t.ScriptMember = null;
            t.ScriptTypeInfo = null;
        }

        const tex = t.Texture;
        if (tex)
            await tex.Cache();

        if (!await t.Compile())
            return false;

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.BLEND);
        if (props.Transparent)
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
        else
            gl.clearColor(0.0, 0.5, 0.0, 1.0);
        gl.clearDepth(1.0);

        t.Cached = true;

        if (IsAttached(t.Element))
            MediaPlayerEffect.render(t);

        new ResizeObserver(() =>
        {
            t.GetRender()();
        }).observe(e);

        t.ScrollElement = MediaPlayerEffect.GetElementFromId(props.ScrollId);
        const mouseElement = MediaPlayerEffect.GetElementFromId(props.MouseId);
        mouseElement.addEventListener("mousemove", ev => {
            t.MouseX = ev.offsetX;
            t.MouseY = ev.offsetY;
        });
        return MediaPlayerTools.OnCacheComplete(this, keepHidden);
    }

    static AdjustDpi(timeElapsed, max, min) {
        const oldDpi = MediaPlayerEffect.DpiScale;
        let newDpi = oldDpi;
        if (timeElapsed > max) {
            newDpi *= 0.5;
            if (newDpi < 0.125)
                newDpi = 0.125;
        }
        if (timeElapsed < min) {
            newDpi *= 2;
            if (newDpi > 1)
                newDpi = 1;
        }
        if (newDpi === oldDpi)
            return;
        console.log("Dpi Scale changed to: " + newDpi);
        MediaPlayerEffect.DpiScale = newDpi;
        localStorage.setItem("SysWeaver.Media.EffectDpiScale", "" + newDpi);
    }

    AccTime(timeElapsed, max, min) {
        if (timeElapsed <= 0)
            return;
        const t = this;
        const tot = t.TotalTime + timeElapsed;
        const c = t.MeasureCount + 1;
        t.TotalTime = tot;
        t.MeasureCount = c;
        if ((c & 31) !== 0)
            return;
        t.AvgDrawTimeMs = tot / c;
        const pc = t.PrintCounter + 1;
        if (t.Params.DpiAdjust)
            if (pc > 1)
                MediaPlayerEffect.AdjustDpi(t.AvgDrawTimeMs, max, min);
        t.PrintCounter = pc;
//        if ((pc & 7) === 0)
//            console.log(t.Url + " - Average draw time: " + t.AvgDrawTimeMs + " ms [" + c + " measurements");
        t.MeasureCount = 0;
        t.TotalTime = 0;
    }

    ScrollElement = null;
    MouseX = 0.5;
    MouseY = 0.5;

    static render(t, animTime) {
        if (!t.Program)
            return;
        let time = t.CurrentPosition;
        const isRunning = typeof animTime !== "undefined";
        const pq = isRunning ? t.Query : null;
        if (isRunning) {
            if (typeof t.RenderTime === "undefined")
                t.RenderTime = animTime;
            time = (animTime - t.RenderTime) * 0.001 + t.ResumeTime;
        }
        t.CurrentPosition = time;

        const e = t.Element;
        const p = t.Params;
        let dpi = window.devicePixelRatio;
        if (dpi <= 0.25)
            dpi = 0.25;
        dpi *= p.DpiScale;
        if (p.DpiAdjust && (!p.Static))
            dpi *= MediaPlayerEffect.DpiScale;
        let w = Math.round(e.clientWidth * dpi) | 0;
        let h = Math.round(e.clientHeight * dpi) | 0;
        if (p.LimitSize) {
            const max = Math.max(p.Width, p.Height);
            const min = Math.min(p.Width, p.Height);
            const ow = w;
            const oh = h;
            if (w > h) {
                if (w > max) {
                    h = Math.round(max * oh / ow) | 0;
                    w = max;
                }
                if (h > min) {
                    w = Math.round(min * ow / oh) | 0;
                    h = min;
                }
            } else {
                if (w > min) {
                    h = Math.round(min * oh / ow) | 0;
                    w = min;
                }
                if (h > max) {
                    w = Math.round(max * ow / oh) | 0;
                    h = max;
                }
            }
        }
        if ((e.width !== w) || (e.height !== h)) {
            e.width = w;
            e.height = h;
            t.Rendered = false;
        }
        if (p.Static) {
            if (t.Rendered)
                return;
            t.CurrentPosition = 0;
            time = 0;
        }
        t.Rendered = true;
        const gl = t.GL;
        gl.viewport(0, 0, w, h);
        t.UniformTime(time * p.Speed);
        t.UniformResolution(w, h);


        let scrollX = 0.0;
        let scrollY = 0.0;
        let scrollRX = 0.0;
        let scrollRY = 0.0;
        const scrollElement = t.ScrollElement;
        if (scrollElement) {
            const x = scrollElement.scrollLeft;
            const y = scrollElement.scrollTop;
            scrollX = x;
            scrollY = y;
            scrollRX = x / Math.abs(1.0, scrollElement.scrollWidth);
            scrollRY = y / Math.abs(1.0, scrollElement.scrollHeight);
        }
        t.UniformScroll(scrollX, scrollY, scrollRX, scrollRY);
        t.UniformMouse(t.MouseX, t.MouseY);
        //t.UniformTimeDelta
        t.UniformFrameIndex(t.FrameIndex);
        ++t.FrameIndex;
        t.UniformDate(new Date());

        if (t.ApplyTextue) {
            t.ApplyTextue = false;
            const tex = t.Texture;
            if (tex) {
                const pr = t.Program.Program;
                const tu = gl.getUniformLocation(pr, "tex");
                const tuv = gl.getUniformLocation(pr, "texUv");
                const ts = gl.getUniformLocation(pr, "texSize");
                if (tu) {
                    gl.activeTexture(gl.TEXTURE0);
                    tex.Apply(tuv, ts);
                    gl.uniform1i(tu, 0);
                    if (p.WrapU)
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
                    if (p.WrapV)
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
                }
            }
        }

        const draw = () => {
            gl.drawArrays(gl.TRIANGLES, 0, 3);

        };


        if (pq) {
            const ext = pq.E;
            const query = pq.Q;
            if (!pq.IsQ) {
            //  New perf query 
                ext.beginQueryEXT(ext.TIME_ELAPSED_EXT, query);
                draw();
                ext.endQueryEXT(ext.TIME_ELAPSED_EXT);
                pq.IsQ = true;
            } else {
                //  Waiting for previous result to be available
                draw();
                const available = ext.getQueryObjectEXT(query, ext.QUERY_RESULT_AVAILABLE_EXT);
                const disjoint = gl.getParameter(ext.GPU_DISJOINT_EXT);
                if (available && !disjoint) {
                    const timeElapsed = ext.getQueryObjectEXT(query, ext.QUERY_RESULT_EXT) / 1000000;
                    t.AccTime(timeElapsed, 12, 2);
                    pq.IsQ = false;
                }
            }
        } else {
            //  No perf query
            const ts = performance.now();
            let pt = t.PrevTime;
            if (pt) {
                const timeElapsed = ts - pt;
                t.AccTime(timeElapsed, 35, 17);
            }
            t.PrevTime = ts;
            draw();
        }
    }


    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        t.ResumeTime = t.CurrentPosition;
        delete t.RenderTime;
        t.ResetCounters();
        if (t.Params.Static && t.Rendered)
            return;
        t.IsAnimating = true;
        function renderFrame(animTime) {
            MediaPlayerEffect.render(t, animTime);
            if (t.Paused || (t.Params.Static && t.Rendered)) {
                t.IsAnimating = false;
            } else {
                window.requestAnimationFrame(renderFrame);
            }
        }
        renderFrame();
    }

    async Pause() {
        const t = this;
        if (t.Paused)
            return;
        t.Paused = true;
    }

    async Stop() {
        const t = this;
        if (t.Paused && (t.CurrentPosition <= 0))
            return;
        t.Paused = true;
        while (t.IsAnimating)
            await delay(1);
        t.CurrentPosition = 0;
        MediaPlayerEffect.render(t);
    }

    GetPos() {
        return this.CurrentPosition;
    }

    async Seek(time) {
        if (time < 0)
            return null;
        const t = this;
        if (t.Paused) {
            t.CurrentPosition = time;
            MediaPlayerEffect.render(t);
        } else {
            t.ResumeTime = (time - t.CurrentPosition);
            t.CurrentPosition = time;
        }

    }


}
