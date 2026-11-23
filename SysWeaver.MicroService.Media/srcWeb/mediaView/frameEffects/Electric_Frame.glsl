//  media: image none
// =============================================================================
//  Electric Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Bolt animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.10, 0.12, 0.20); // var: { "type": "col", "name": "Inner Color", "desc": "Base tint of the storm clouds inside the frame" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the storm clouds inside the frame", "scale": 100, "unit": "%" }
const float CloudSpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Cloud Speed", "desc": "Drift speed of the clouds", "unit": "x" }
const float CloudScale = 1.0; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Cloud Scale", "desc": "Size of the cloud features (higher = smaller)", "unit": "x" }
const float ImageLighting = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Image Lighting", "desc": "How strongly the frame's shadow, bolt glow and flashes light the picture", "unit": "x" }
const float FlashAmount = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Cloud Flashes", "desc": "Brightness of the lightning flashes inside the clouds", "unit": "x" }
const vec3 BoltColor = vec3(0.35, 0.6, 1.0); // var: { "type": "col", "name": "Bolt Color", "desc": "Glow color of the bolts" }
const float Intensity = 1.25; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Intensity", "desc": "Brightness of the bolts", "unit": "x" }
const float Punch = 1.5; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Punch", "desc": "Extra brightness and color saturation" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const int Bolts = 3; // var: { "min": 1, "max": 5, "step": 1, "name": "Bolts", "desc": "Number of bolt strands" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)

#define ZERO min(iFrame, 0)

varying vec2 vP;
varying vec4 vCloudQ;     // xy: cloud q, zw: cell noise pos
varying vec2 vFlashDiff;  // (uv0 - fp) * 2.2
varying vec2 vImgUv;      // p / (2*hs) + 0.5

flat varying vec4 vFrame;       // xy: hs, z: inset, w: rad
flat varying vec4 vBoltParams;  // x: reach, y: amp, z: invBoltDecay, w: invFlareDecay
flat varying vec4 vDecayParams; // x: invHazeDecay, y: invShadowDecay, z: cmaskLo, w: cmaskHi
flat varying vec4 vCloudOff;    // x: ct*0.04, y: 5.2-ct*0.03, z: 1.3-ct*0.03, w: flashActive
flat varying vec3 vFlashCol;    // flash color * amount * flick (0 if inactive)
flat varying vec4 vBoltTime;    // x: t*1.3, y: -t*0.9, z: -t*5.0, w: t*2.2
flat varying float vBoltT12;    // t * 12.0

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    vP = p;

    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {
        vec2 ts = max(iChannelResolution[0].xy, vec2(1.0));
        float A = ts.x / ts.y;
        float hx = min(hs.x, hs.y * A);
        hs = vec2(hx, hx / A);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float reach = inset * 2.8 + 16.0;
    vFrame = vec4(hs, inset, rad);
    vBoltParams = vec4(reach, inset * 0.8, 1.0 / (inset * 0.35 + 2.0), 1.0 / (inset * 0.5 + 4.0));
    vDecayParams = vec4(1.0 / (inset * 0.6 + 2.0), 1.0 / (inset * 0.7 + 4.0), rad + 0.6 * reach, rad + 1.4 * reach);
    vImgUv = p / (2.0 * hs) + 0.5;

    vec2 uv0 = p / res.y;
    vec2 uv = uv0 * CloudScale;
    float ct = mod(iTime * CloudSpeed, 1000.0);
    vec2 drift = vec2(ct * 0.06, ct * 0.015);
    vCloudQ = vec4(uv * 3.2 + drift, uv * 0.8 - drift * 0.3 + 9.1);

    float ft = ct * 0.8;
    float slot = floor(ft), ph = ft - slot;
    vec3 h = EwHash1To3(slot);
    float flashOn = (h.x < 0.6 && FlashAmount > 0.0) ? 1.0 : 0.0;
    vCloudOff = vec4(ct * 0.04, 5.2 - ct * 0.03, 1.3 - ct * 0.03, flashOn);
    if (flashOn > 0.0) {
        vec2 fp = (h.yz - 0.5) * vec2(res.x / res.y, 1.0) * 0.8;
        float flick = exp(-ph * 5.0) * (0.55 + 0.45 * step(0.4, fract(ph * 17.0 + h.y)));
        vFlashCol = (BoltColor + 0.25) * FlashAmount * flick;
        vFlashDiff = (uv0 - fp) * 2.2;
    } else {
        vFlashCol = vec3(0.0);
        vFlashDiff = vec2(0.0);
    }

    float t = mod(iTime * Speed, 500.0);
    vBoltTime = vec4(t * 1.3, -t * 0.9, -t * 5.0, t * 2.2);
    vBoltT12 = t * 12.0;
}

// Vectorized 2D value noise (exact match to EwValueNoise2D, all 4 corner hashes in vec4)
float vnoise2D(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 c0 = cell * 0.1031;
    vec4 c = fract(vec4(c0, c0 + 0.1031));
    vec2 x = c.xz, y = c.yw;
    vec2 a = x * (x + 66.66);
    vec2 b = x * 2.0 + 33.33;
    vec4 d = a.xyxy + b.xyxy * y.xxyy;
    vec4 xd = x.xyxy + d;
    vec4 h = fract((xd + y.xxyy + d) * xd);
    vec2 r = mix(h.xz, h.yw, u.x);
    return mix(r.x, r.y, u.y);
}

// Dual 3-octave FBM in one loop (for domain warp w.xy)
vec2 fbm2D3Pair(vec2 p0, vec2 p1)
{
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    vec2 sum = vec2(0.0);
    float amp = 0.5;
    for (int i = ZERO; i < 3; i++) {
        sum += amp * vec2(vnoise2D(p0), vnoise2D(p1));
        p0 = m * p0;
        p1 = m * p1;
        amp *= 0.5;
    }
    return sum;
}

// FBM with exact interval early-out: stops once result is guaranteed <= lo or >= hi
float fbm2DBounded(vec2 p, int octaves, float rem, float lo, float hi)
{
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * vnoise2D(p);
        rem -= amp;
        if (sum >= hi || sum + rem <= lo) break;
        p = m * p;
        amp *= 0.5;
    }
    return sum;
}

// 4 octave signed noise, roughly [-1, 1]
float boltNoise(vec2 p)
{
    float n = 0.0, a = 1.1;
    for (int i = ZERO; i < 4; i++) {
        n += a * vnoise2D(p);
        p = p * 2.03 + vec2(13.7, 7.1);
        a *= 0.5;
    }
    return n - 1.03125;
}

// Lightning flash light (precomputed per frame in vertexMain)
vec3 flashLight()
{
    return vCloudOff.w > 0.0 ? vFlashCol * exp(-length(vFlashDiff)) : vec3(0.0);
}

// Storm clouds: domain warped fbm, dark undersides, lit tops, in-cloud lightning flashes
vec3 stormClouds()
{
    vec2 q = vCloudQ.xy;
    vec2 q04 = q * 0.4;
    // gentle domain warp: slow churning, keeps the billows round
    vec2 w = fbm2D3Pair(q04 + vec2(0.0, vCloudOff.x), q04 + vCloudOff.yz) - 0.44;
    vec2 cp = q + 1.5 * w;
    float f = fbm2DBounded(cp, 6, 0.984375, 0.3740625, 2.0);
    float dens = smoothstep(0.3740625, 0.6496875, f);
    // shading: billows lit from above (skip when dens == 0; bounded early-out)
    float lit = 0.0;
    if (dens > 0.0) {
        float f2 = fbm2DBounded(cp + vec2(0.04, 0.12), 6, 0.984375, f - 0.06890625, f + 0.04921875);
        lit = smoothstep(-0.06890625, 0.04921875, f2 - f);
    }
    // large scale darkness: heavy storm cells vs. thinner gaps
    float cell = smoothstep(0.21875, 0.6125, fbm2DBounded(vCloudQ.zw, 3, 0.875, 0.21875, 0.6125));
    vec3 dark = InnerColor * 0.3;
    vec3 light = mix(InnerColor, vec3(0.62, 0.66, 0.76), 0.7);
    vec3 col = mix(dark, light, dens * (0.2 + 0.8 * lit));
    col *= 0.45 + 0.8 * cell;
    // lightning flashes lighting the clouds from inside
    if (vCloudOff.w > 0.0) {
        col += vFlashCol * exp(-length(vFlashDiff)) * (0.15 + 1.3 * dens * (0.4 + 0.6 * lit));
    }
    // hue preserving limit (flashes never shift toward cyan / white by channel clipping)
    return col / max(1.0, max(col.r, max(col.g, col.b)));
}

// Inner area (premultiplied, over the Background): the picture if there is one,
// else (or under a transparent picture) the storm clouds. light = bolt light (tone mapped)
vec4 innerLayer(float d, vec3 light)
{
    vec4 bg = vec4(Background * Opacity, Opacity);
    float im = InnerOpacity * clamp(0.5 - d, 0.0, 1.0);   // AA edge on the frame center line
    if (im <= 0.0) return bg;
    vec4 inner;
    if (SwHaveImage) {
        vec4 img = texture(iChannel0, vImgUv);
        // shadow: the picture darkens toward the frame edge; lights: bolt glow + flashes
        float shadow = 0.55 * exp(min(d, 0.0) * vDecayParams.y);
        vec3 L = light * 1.6 + flashLight() * 0.5;
        vec3 c = img.rgb * (1.0 - shadow * ImageLighting) + (img.rgb * L + L * 0.08) * ImageLighting;
        c /= max(1.0, max(c.r, max(c.g, c.b)));
        inner = vec4(c * img.a, img.a);
        if (SwTransparentImage && img.a < 1.0)
            inner += vec4(stormClouds(), 1.0) * (1.0 - img.a);
    } else {
        inner = vec4(stormClouds(), 1.0);
    }
    inner *= im;
    return inner + bg * (1.0 - inner.a);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 p = vP;
    vec2 hs = vFrame.xy;
    float inset = vFrame.z;
    float rad = vFrame.w;
    float d = EwSdRoundedBox2D(p, hs, rad); // px, 0 on the frame center line

    float reach = vBoltParams.x;
    vec3 col = vec3(0.0);
    if (d >= -reach) {
        float amp = vBoltParams.y;
        float whiteSum = 0.0;
        float colorSum = 0.0;

        // bolt glow is negligible beyond amp + 75 px from the center line
        if (abs(d) < amp + 75.0) {
            float invBoltDecay = vBoltParams.z;
            float boltSkip = 0.26 * amp + 6.5 * (inset * 0.35 + 2.0);
            vec2 qBase = p * (1.0 / 90.0);
            for (int i = ZERO; i < Bolts; i++) {
                float fi = float(i);
                float tf = floor(vBoltT12 + fi * 0.37);
                vec2 q = qBase + vec2(fi * 17.3, fi * 5.1);
                float n = boltNoise(q + vBoltTime.xy + 3.1 * tf * 0.05);
                if (abs(d - clamp(n, -1.0, 1.0) * amp) > boltSkip) continue;
                n += 0.25 * boltNoise(q * 4.0 + vec2(vBoltTime.z, tf * 0.7));
                float off = clamp(n, -1.0, 1.0) * amp;
                float dd = abs(d - off);
                float flick = 0.55 + 0.45 * EwHash2To1(vec2(tf, fi));
                float core = smoothstep(1.6, 0.0, dd);
                float glow = 2.5 / (dd + 2.5) * exp(-dd * invBoltDecay);
                whiteSum += flick * core * 1.5;
                colorSum += flick * glow * 0.9;
            }
        }
        // random flares: short jagged branch bolts across the frame
        {
            vec2 ap = abs(p) - hs;
            float along = -min(ap.x, ap.y);
            if (along > vDecayParams.z) {
                bool side = ap.x > ap.y;                       // nearer a vertical edge
                float s = side ? p.y + sign(p.x) * 7919.0 : p.x + sign(p.y) * 3571.0;
                float cmask = smoothstep(vDecayParams.z, vDecayParams.w, along);
                float sNorm = s * (1.0 / 170.0);
                float cell = floor(sNorm);
                float fc = sNorm - cell;                       // fade at cell borders (no seams)
                float cellEnv = cmask * smoothstep(0.0, 0.18, fc) * smoothstep(1.0, 0.82, fc);
                float tapOut = smoothstep(inset * 0.8, inset * 0.3, d);
                float invFlareDecay = vBoltParams.w;
                for (int k = ZERO; k < 2; k++) {
                    float fk = float(k);
                    float ftime = vBoltTime.w + fk * 0.5 + EwHash2To1(vec2(cell, fk * 7.0));
                    float slot = floor(ftime);
                    vec3 h = EwHash3To3(vec3(cell, slot, fk));
                    if (h.x > 0.45) continue;                  // only some cells fire
                    float ph = ftime - slot;
                    float env = exp(-ph * 7.0) * cellEnv;
                    float u = s - (cell + 0.35 + 0.3 * h.y) * 170.0;
                    float L = inset * (1.2 + 1.2 * h.z) + 10.0; // inward length
                    float et = env * smoothstep(-L, -L * 0.4, d) * tapOut;
                    if (et > 0.0 && abs(u) < 95.0) {           // bolt glow is ~0 beyond (|jag| <= 22)
                        float jag = boltNoise(vec2(d * (1.0 / 22.0), h.y * 40.0 + floor(ph * 20.0) * 0.3)) * 22.0;
                        float bd = abs(u - jag);
                        float bcore = smoothstep(1.4, 0.0, bd);
                        float bglow = 3.0 / (bd + 3.0) * exp(-bd * 0.1);
                        whiteSum += et * bcore * 1.3;
                        colorSum += et * bglow * 0.8;
                    }
                    colorSum += env * 1.2 * exp(-length(vec2(u, d)) * invFlareDecay);
                }
            }
        }
        // ambient haze along the frame
        colorSum += 0.12 * exp(-abs(d) * vDecayParams.x);
        col = vec3(whiteSum) + BoltColor * colorSum;
        // soft fades: to the screen edges and into the empty middle
        vec2 eb2 = min(fragCoord, iResolution.xy - fragCoord);
        float eb = min(eb2.x, eb2.y);
        float scale = (Intensity * (1.0 + Punch)) * smoothstep(0.0, inset * 0.9, eb) * smoothstep(-reach, -reach * 0.45, d);
        col *= scale;                          // exposure boost + fades
        col = 1.0 - exp(-col);                 // tone map to [0,1]
        // saturation / contrast punch (keeps white-hot cores, deepens the colored glow)
        float lum = dot(col, vec3(0.2126, 0.7152, 0.0722));
        col = clamp(mix(vec3(lum), col, 1.0 + 0.6 * Punch), 0.0, 1.0);
    }
    vec4 under = innerLayer(d, col);
    if (d < -reach) { fragColor = under; return; }
    // dither against banding (gamma-space 8 bit step), alpha dithered alike
    float dz = EwDither(fragCoord, iTime, 2.0);
    float a = max(col.r, max(col.g, col.b));
    a = clamp(a + dz, 0.0, 1.0);
    col = clamp(col + dz, 0.0, a);
    a *= 1.0 - Additive;
    fragColor = vec4(col + under.rgb * (1.0 - a), a + under.a * (1.0 - a));
}
