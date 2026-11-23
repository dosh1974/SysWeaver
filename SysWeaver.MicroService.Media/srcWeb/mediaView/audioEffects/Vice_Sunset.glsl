//  media: audio stock:YuE2_00051.flac
// Vice Sunset: an 80's Miami neon sunset over the ocean, driven by the music.
//  iMouse.x = Reactivity | How strongly the scene reacts to the music | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Hue Shift | Rotates the whole neon palette | 0.5 | -90 .. 90 | °
//  iMouse.z = Neon Glow | Brightness of the neon outlines, laser, grid and sun halo | 0.5 | 0.0 .. 2.0 | x
//  iMouse.w = Skyline | Height of the music driven skyline | 0.5 | 0.0 .. 2.0 | x
#define Reactivity mix(0.0, 2.0, iMouse.x)
#define HueShift mix(-90.0, 90.0, iMouse.y)
#define NeonGlow mix(0.0, 2.0, iMouse.z)
#define SkylineAmt mix(0.0, 2.0, iMouse.w)

// --- Colors ---
const vec3 SkyTop = vec3(0.06, 0.02, 0.2); // var: { "type": "col", "name": "Sky Top", "desc": "Night color at the top of the sky" }
const vec3 SkyMid = vec3(0.55, 0.1, 0.55); // var: { "type": "col", "name": "Sky Middle", "desc": "Magenta band of the sky" }
const vec3 SkyHorizon = vec3(1.0, 0.45, 0.4); // var: { "type": "col", "name": "Sky Horizon", "desc": "Sunset glow at the horizon" }
const vec3 SunTop = vec3(1.0, 0.9, 0.35); // var: { "type": "col", "name": "Sun Top", "desc": "Color of the upper sun" }
const vec3 SunBottom = vec3(1.0, 0.2, 0.6); // var: { "type": "col", "name": "Sun Bottom", "desc": "Color of the lower, striped sun" }
const vec3 NeonPink = vec3(1.0, 0.15, 0.65); // var: { "type": "col", "name": "Neon Pink", "desc": "First neon color (grid, outlines, palm rims)" }
const vec3 NeonCyan = vec3(0.15, 0.9, 1.0); // var: { "type": "col", "name": "Neon Cyan", "desc": "Second neon color (laser, outlines)" }
const vec3 WaterTint = vec3(0.8, 0.7, 1.0); // var: { "type": "col", "name": "Water Tint", "desc": "Tint of the ocean reflection" }
const vec3 WaterDeep = vec3(0.02, 0.03, 0.09); // var: { "type": "col", "name": "Deep Water", "desc": "Base color of the deep ocean" }
const vec3 PalmColor = vec3(0.04, 0.01, 0.07); // var: { "type": "col", "name": "Palm Color", "desc": "Silhouette color of the palm trees" }

// --- Sun & Sky ---
const float SunSize = 0.26; // var: { "min": 0.1, "max": 0.4, "step": 0.01, "name": "Sun Size", "desc": "Radius of the sun (screen heights)" }
const float HorizonHeight = -0.08; // var: { "min": -0.3, "max": 0.15, "step": 0.01, "name": "Horizon", "desc": "Height of the horizon line" }
const float StripeSpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Stripe Speed", "desc": "Speed of the sun stripes in silence", "unit": "x" }
const float LaserHeight = 0.43; // var: { "min": 0.1, "max": 0.7, "step": 0.01, "name": "Laser Height", "desc": "Height of the waveform laser" }
const bool ShowLaser = true; // var: { "name": "Laser", "desc": "Show the waveform laser in the sky" }

// --- Ocean & Waves ---
const float WaveSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Wave Speed", "desc": "Speed of the ocean waves and grid", "unit": "x" }
const bool ShowGrid = true; // var: { "name": "Neon Grid", "desc": "Show the neon grid on the ocean" }
const bool ShowStreaks = true; // var: { "name": "Sun Streaks", "desc": "Show golden sun streaks on the water" }

// --- Palms ---
const bool ShowPalms = true; // var: { "name": "Palms", "desc": "Show the palm tree silhouettes" }
const float PalmHatGlow = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Palm Hi-Hat Glow", "desc": "How much the hi-hats drive the neon glow of the palms", "unit": "x" }
const float PalmGlowReach = 0.3; // var: { "min": 0.1, "max": 0.6, "step": 0.01, "name": "Palm Glow Reach", "desc": "How far the neon halo around the palms reaches (screen heights)" }

// --- CRT & Post ---
const float Exposure = 1.1; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float Scanlines = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Scanlines", "desc": "Strength of the CRT scanlines", "scale": 100, "unit": "%" }
const float Grain = 0.06; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Grain", "desc": "VHS style film grain", "scale": 100, "unit": "%" }

// audio values, read once per pixel
float gBass = 0.0, gKick = 0.0, gSnare = 0.0, gHat = 0.0, gHatE = 0.0, gBeat = 0.0, gPhase = 0.0, gTempoFade = 0.0;
float gPx = 0.001;
float gAspect = 1.0;

vec3 toLin(vec3 c) { return pow(max(c, 0.0), vec3(2.2)); }

float specAt(float u) {
    float s = texture(iChannel0, vec2(EwAudioGet64u(u), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
    return smoothstep(0.3, 0.95, s);
}

float waveAt(float x) {
    float f = clamp(x, 0.0, 1.0) * 64.0 - 0.5;
    float i0 = floor(f);
    float w0 = EwAudioGetWave(iChannel0, clamp((i0 + 0.5) / 64.0, 0.0, 1.0), 3);
    float w1 = EwAudioGetWave(iChannel0, clamp((i0 + 1.5) / 64.0, 0.0, 1.0), 3);
    return mix(w0, w1, smoothstep(0.0, 1.0, f - i0));
}

// everything above the horizon (also used for the ocean reflection)
vec3 skyScene(vec2 p, bool refl) {
    float h = max(p.y - HorizonHeight, 0.0);
    float t = clamp(h / 0.65, 0.0, 1.0);
    vec3 col = mix(toLin(SkyHorizon), toLin(SkyMid), smoothstep(0.0, 0.4, t));
    col = mix(col, toLin(SkyTop), smoothstep(0.3, 1.0, t));

    // stars
    if (!refl) {
        vec2 g = p * 70.0;
        vec2 id = floor(g);
        vec2 rnd = EwHash2To2(id);
        float d = length(g - id - 0.2 - 0.6 * rnd);
        float on = step(0.8, EwHash2To1(id + 17.3));
        float tw = 0.55 + 0.45 * sin(iTime * (1.5 + rnd.x * 3.0) + rnd.y * 40.0);
        col += vec3(0.9, 0.85, 1.0) * on * exp(-d * d * 140.0) * (tw + gHat * Reactivity * 2.0) * smoothstep(0.25, 0.7, t) * 0.8;
    }

    // striped retro sun
    vec2 sc = vec2(0.0, HorizonHeight + SunSize * 0.85);
    vec2 q = (p - sc) / SunSize;
    float r = length(q);
    float aa = gPx / SunSize * 1.5;
    vec3 sunCol = mix(toLin(SunBottom), toLin(SunTop), smoothstep(-0.3, 0.9, q.y));
    float sy = q.y * 5.0 + mod(iTime * 0.12 * StripeSpeed, 1.0) + gTempoFade * (1.0 - pow(1.0 - gPhase, 3.0));
    float gap = smoothstep(0.35, -0.9, q.y) * 0.75;
    float sw = aa * 5.0;
    float stripes = smoothstep(gap * 0.5 - sw, gap * 0.5 + sw, abs(fract(sy) - 0.5));
    stripes = max(stripes, smoothstep(0.3, 0.35, q.y));
    float inside = EwFillAA(r - 1.0, aa);
    float disc = inside * stripes;
    float pulse = gKick * Reactivity * 0.6 + gBeat * 0.3;
    float halo = exp(-max(r - 1.0, 0.0) * 3.0) * 0.35 + exp(-max(r - 1.0, 0.0) * 12.0) * 0.4;
    halo *= 1.0 - 0.75 * inside;
    col += sunCol * halo * (0.4 + 0.5 * NeonGlow) * (1.0 + pulse);
    col = mix(col, sunCol * (1.15 + pulse * 0.6), disc);

    // waveform laser
    if (ShowLaser) {
        float x = p.x / gAspect + 0.5;
        float amp = 0.04 * Reactivity;
        float ly = LaserHeight + waveAt(x) * amp;
        float slope = (waveAt(x + 0.004) - waveAt(x - 0.004)) / (0.008 * gAspect) * amp;
        float ld = abs(p.y - ly) / sqrt(1.0 + slope * slope);
        float fade = smoothstep(0.0, 0.15, x) * smoothstep(1.0, 0.85, x);
        col += toLin(NeonCyan) * (exp(-ld / (gPx * 1.2)) * 1.5 + exp(-ld * 35.0) * 0.12) * NeonGlow * fade;
    }

    // music driven skyline
    float cw = 0.032;
    float cell = floor(p.x / cw);
    vec3 glow = vec3(0.0);
    for (int k = -1; k <= 1; k++) {
        float c = cell + float(k);
        vec2 hh = EwHash1To2(c * 1.37 + 11.0);
        float cx = (c + 0.5) * cw;
        float u = clamp(abs(cx) / (0.5 * gAspect), 0.0, 1.0);
        float spec = specAt(0.04 + u * 0.8);
        float env = mix(0.45, 1.0, smoothstep(0.05, 0.5, abs(cx)));
        float H = (0.012 + 0.07 * hh.x * hh.x + spec * 0.17 * Reactivity) * SkylineAmt * env;
        float W = cw * (0.3 + 0.18 * hh.y);
        vec2 bp = p - vec2(cx, HorizonHeight + H * 0.5);
        float d = EwSdBox2D(bp, vec2(W, H * 0.5));
        vec3 neon = fract(hh.x * 7.31) > 0.5 ? toLin(NeonPink) : toLin(NeonCyan);
        float ni = NeonGlow * (0.5 + spec * 0.8 + gSnare * Reactivity * 1.2) * step(0.002, H);
        glow += neon * ni * (exp(-abs(d) / (gPx * 1.3)) * 0.9 + exp(-max(d, 0.0) * 90.0) * 0.08);
        if (k == 0 && d < gPx) {
            vec3 b = toLin(vec3(0.07, 0.02, 0.12));
            // windows
            vec2 wg = vec2(bp.x / 0.0065, (p.y - HorizonHeight) / 0.0095);
            vec2 wid = floor(wg);
            vec2 wf = fract(wg);
            float lit = step(0.62, EwHash2To1(wid + c * 3.1));
            float wm = step(0.25, wf.x) * step(wf.x, 0.75) * step(0.3, wf.y) * step(wf.y, 0.7);
            wm *= step(abs(bp.x), W - 0.003) * step(p.y, HorizonHeight + H - 0.006);
            vec3 wc = EwHash2To1(wid + 5.0) > 0.7 ? toLin(NeonCyan) : vec3(1.0, 0.6, 0.3);
            float flick = 0.5 + 0.5 * sin(iTime * 2.0 + EwHash2To1(wid) * 30.0);
            b += wc * wm * lit * (0.25 + 0.2 * flick + gHat * Reactivity * 0.6);
            col = mix(col, b, EwFillAA(d, gPx));
        }
    }
    col += glow;
    return col;
}

float palmDist(vec2 p, vec2 base, vec2 top, float s, float sway) {
    top.x += sway;
    float d = 1e5;
    vec2 ctrl = vec2(base.x + (top.x - base.x) * 0.1, mix(base.y, top.y, 0.65)) + 1e-4;
    // bounds = shape extent + the glow reach, so the glow has faded to 0 before a bound
    float tx0 = min(min(base.x, top.x), ctrl.x) - 0.025 * s - PalmGlowReach;
    float tx1 = max(max(base.x, top.x), ctrl.x) + 0.025 * s + PalmGlowReach;
    if (p.y < top.y + PalmGlowReach && p.x > tx0 && p.x < tx1) {
        float tt = clamp((p.y - base.y) / (top.y - base.y), 0.0, 1.0);
        float w = mix(0.022, 0.01, tt) * s * (0.88 + 0.12 * abs(sin(tt * 90.0)));
        d = EwSdQuadraticBezier(p, base, ctrl, top) - w;
    }
    vec2 rel = p - top;
    float cr = 0.38 * s + PalmGlowReach;
    if (dot(rel, rel) < cr * cr) {
        d = min(d, length(rel + vec2(0.008 * s, 0.014 * s)) - 0.011 * s);
        d = min(d, length(rel + vec2(-0.009 * s, 0.016 * s)) - 0.01 * s);
        for (int i = 0; i < 9; i++) {
            float fi = float(i);
            float a = (i < 5 ? mix(-0.4, 1.2, fi / 4.0) : mix(1.95, 3.55, (fi - 5.0) / 3.0)) + 0.1 * sin(fi * 4.1) + sway * 3.0;
            vec2 dir = vec2(cos(a), sin(a));
            float len = (0.22 + 0.05 * sin(fi * 2.7)) * s;
            vec2 e = top + dir * len * vec2(1.0, 0.3) - vec2(0.0, (0.11 - 0.03 * dir.y) * s);
            vec2 cp = top + dir * len * vec2(0.6, 0.7) + vec2(0.0, 0.05 * s);
            float rr = clamp(length(rel) / len, 0.0, 1.0);
            float w = (0.021 * pow(1.0 - rr, 0.8) * smoothstep(0.0, 0.15, rr) + 0.001) * s;
            w *= mix(0.35, 1.0, fract(rr * 20.0 + fi * 0.37));
            d = min(d, EwSdQuadraticBezier(p, top, cp, e) - w);
        }
    }
    return d;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 p = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    gPx = 1.0 / iResolution.y;
    gAspect = iResolution.x / iResolution.y;

    // audio
    gBass = EwAudioGetBassEnergy(iChannel0, true);
    gKick = EwAudioGetKickPulse(iChannel0);
    gSnare = EwAudioGetSnarePulse(iChannel0);
    gHat = EwAudioGetHiHatPulse(iChannel0);
    gPhase = EwAudioGetBeatPhase(iChannel0);
    gTempoFade = smoothstep(0.1, 0.3, EwAudioGetTempoConfidence(iChannel0));
    gBeat = EwAudioGetBeatPulse(iChannel0) * gTempoFade;
    gHatE = EwAudioGetHiHatEnergy(iChannel0, true);

    float t = mod(iTime, 1000.0) * WaveSpeed;

    // ocean coordinates (computed everywhere for derivatives)
    float dh = max(HorizonHeight - p.y, 0.002);
    float z = 0.25 / dh;
    vec2 wp = vec2(p.x * z, z);
    float fwv = max(fwidth(wp.y * 3.0), 1e-4); // streak AA (uniform control flow)

    // neon grid on the water
    float grid = 0.0;
    if (ShowGrid) {
        vec2 gv = vec2(wp.x * 1.5, wp.y * 1.0 - mod(t * 0.6, 1.0));
        vec2 fw = max(fwidth(gv), vec2(1e-4));
        vec2 gl = abs(fract(gv - 0.5) - 0.5) / fw;
        float line = 1.0 - min(min(gl.x, gl.y), 1.0);
        float fade = 1.0 - smoothstep(0.15, 0.6, max(fw.x, fw.y));
        grid = line * fade;
    }

    vec3 col;
    if (p.y >= HorizonHeight) {
        col = skyScene(p, false);
    } else {
        float n = EwValueNoise2D(vec2(wp.x * 3.0, wp.y * 7.0 - t * 1.2)) + 0.5 * EwValueNoise2D(vec2(wp.x * 8.0 + 3.0, wp.y * 18.0 - t * 2.1));
        n = n / 1.5 - 0.5;
        float amp = 0.6 + gBass * Reactivity * 0.8;
        vec2 rp = vec2(p.x + n * 0.12 * dh * amp, HorizonHeight + dh * 1.05 + n * 0.25 * dh * amp);
        vec3 refl = skyScene(rp, true);
        float fres = mix(0.35, 0.8, exp(-dh * 6.0));
        col = toLin(WaterDeep) + refl * toLin(WaterTint) * fres;
        // sun streaks: crisp horizontal glints below the sun
        if (ShowStreaks) {
            float sv = wp.y * 3.0 - t * 0.5;
            float row = floor(sv);
            vec2 rh = EwHash1To2(row);
            float ln = abs(fract(sv) - 0.5);
            float lw = 0.04 + 0.07 * rh.x;
            float streak = 1.0 - smoothstep(lw - fwv, lw + fwv, ln);
            streak *= 1.0 - smoothstep(0.2, 0.6, fwv);
            float ext = SunSize * (0.3 + 0.7 * rh.y) * (0.6 + 0.4 * smoothstep(0.0, 0.4, dh));
            float xo = abs(p.x - (rh.x - 0.5) * SunSize * 0.3);
            streak *= 1.0 - smoothstep(ext * 0.15, ext, xo);
            col += toLin(SunTop) * streak * (0.45 + gBass * Reactivity * 0.8) * exp(-dh * 2.0);
        }
        col += toLin(NeonPink) * grid * NeonGlow * (0.35 + gKick * Reactivity * 0.9 + gBeat * 0.4) * exp(-z * 0.08);
        // horizon haze
        col += toLin(SkyHorizon) * exp(-dh * 40.0) * 0.25;
    }

    // palm silhouettes
    if (ShowPalms) {
        float xl = -0.5 * gAspect, xr = 0.5 * gAspect;
        float sw = sin(mod(iTime, 100.0) * 0.6) * 0.012 + gBass * Reactivity * 0.012 * sin(mod(iTime, 100.0) * 1.7);
        float d = palmDist(p, vec2(xl + 0.08, -0.62), vec2(xl + 0.3, 0.3), 1.0, sw);
        d = min(d, palmDist(p, vec2(xl + 0.32, -0.62), vec2(xl + 0.4, 0.02), 0.7, sw * 0.8));
        d = min(d, palmDist(p, vec2(xr - 0.06, -0.62), vec2(xr - 0.27, 0.22), 0.9, -sw));
        float fill = EwFillAA(d, gPx);
        // hi-hat drive: the neon rim brightens, widens and gets a soft outer halo
        float hd = (smoothstep(0.25, 0.75, gHatE) * 0.8 + gHat * 0.9) * Reactivity * PalmHatGlow;
        float od = max(d, 0.0);
        float reach = 1.0 - smoothstep(PalmGlowReach * 0.35, PalmGlowReach, od); // fades to 0 inside the palm bounds
        float rim = exp(-od / (gPx * (2.5 + 5.0 * hd))) * (0.35 + 1.3 * hd) * reach;
        float halo = exp(-od * 12.0) * 0.3 * hd * reach;
        col += toLin(NeonPink) * (rim + halo) * NeonGlow * (1.0 - fill);
        col += toLin(NeonPink) * (rim + halo) * NeonGlow * (1.0 - fill);
        col = mix(col, toLin(PalmColor), fill);
    }

    // grade
    col = max(EwHueRotate(col, radians(HueShift)), 0.0);
    col = EwTonemapAces(col * Exposure);
    col = EwLinearToSrgbFast(col);

    vec2 uv = fragCoord / iResolution.xy;
    float sl = 0.5 + 0.5 * sin(uv.y * 270.0 * 6.2831853);
    col *= 1.0 - Scanlines * 0.35 * sl * smoothstep(400.0, 800.0, iResolution.y);
    col *= EwVignette(uv, 16.0, 0.2, 0.6);
    col *= EwFilmGrain(fragCoord, mod(iTime, 100.0), 1.0, Grain);
    col += EwDither(fragCoord, iTime, 1.0);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
