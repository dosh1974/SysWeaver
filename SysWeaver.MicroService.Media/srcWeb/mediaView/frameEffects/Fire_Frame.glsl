//  media: image none
// =============================================================================
//  Fire Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame line from the left, right and bottom edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Top Inset | Distance of the frame line from the top edge (room for the flames) | 0.5 | 8 .. 256 | {0} px

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Flame animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const float InnerOpacity = 0.9; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the volcanic ash landscape inside the frame", "scale": 100, "unit": "%" }
const vec3 FlameColor = vec3(1.0, 0.32, 0.05); // var: { "type": "col", "name": "Flame Color", "desc": "Base color of the flames" }
const float Intensity = 0.8; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Intensity", "desc": "Brightness of the flames", "unit": "x" }
const float EmberAmount = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Embers", "desc": "Brightness of sparks and falling embers", "unit": "x" }
const float EmberFall = 260.0; // var: { "min": 0.0, "max": 600.0, "step": 10.0, "name": "Ember Fall", "desc": "How far embers drop from the top before fading out", "unit": "px" }
const float Punch = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Punch", "desc": "Extra brightness and color saturation" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const float Wind = 0.6; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Wind", "desc": "Strength of the gusts bending the flames sideways", "unit": "x" }
const float MiddleBoost = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Middle Boost", "desc": "Taller, hotter flames toward the middle of each edge", "scale": 100, "unit": "%" }
const float GlowAdd = 0.35; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Glow Opacity", "desc": "Alpha of the thin glow: 0 = purely additive glow, 1 = blended", "scale": 100, "unit": "%" }
const float FlameHeight = 0.65; // var: { "min": 0.1, "max": 1.5, "step": 0.01, "name": "Flame Height", "desc": "Flame tongue height relative to the Top Inset", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define TopInset mix(8.0, 256.0, iMouse.z)

#define ZERO min(iFrame, 0)

// 4-corner SIMD vectorized EwValueNoise2D (bit-exact with EwHash2To1, 4x smaller HLSL IR)
float fastNoise2D(vec2 p)
{
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec4 xy = fract(vec4(c.x, c.x + 1.0, c.y, c.y + 1.0) * 0.1031);
    vec4 cx = xy.xyxy;
    vec4 cy = xy.zzww;
    vec4 d = cx * (cy + 33.33) + cy * (cx + 33.33) + cx * (cx + 33.33);
    vec4 px = cx + d;
    vec4 h = fract((px + (cy + d)) * px);
    vec2 m = mix(h.xz, h.yw, u.x);
    return mix(m.x, m.y, u.y);
}

// Non-unrolling 2D fbm matching EwFbm2D (avoids driver loop unrolling compile bloat)
float fbm2(vec2 p, int oct)
{
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0, amp = 0.5;
    for (int i = ZERO; i < oct; i++) {
        sum += amp * fastNoise2D(p);
        p = m * p;
        amp *= 0.5;
    }
    return sum;
}

// Domain-warped 4-oct fbm (x) + vertically offset 3-oct fbm (y) sharing one fbm2 site
vec2 warpedFbm(vec2 uv, float wScale, vec2 wOff, float vScale, vec2 wAmp, float dy, float minV)
{
    float w = 0.0, v = 0.0, v2 = 0.0;
    for (int s = ZERO; s < 3; s++) {
        vec2 sp = s == 0 ? uv * wScale + wOff
                : s == 1 ? uv * vScale + w * wAmp
                         : (uv + vec2(0.0, dy)) * vScale + w * wAmp;
        float r = fbm2(sp, s == 1 ? 4 : 3);
        if (s == 0) {
            w = r - 0.44;
        } else if (s == 1) {
            v = r;
            if (v <= minV) break;
        } else {
            v2 = r;
        }
    }
    return vec2(v, v2);
}

// 4 octave signed noise, roughly [-1, 1]
float boltNoise(vec2 p)
{
    float n = 0.0, a = 0.55;
    for (int i = ZERO; i < 4; i++) {
        n += a * fastNoise2D(p);
        p = p * 2.03 + vec2(13.7, 7.1);
        a *= 0.5;
    }
    return n * 2.0 - 1.03125;
}

// Particle sparks in screen columns: start at baseY, travel dir (+1 up, -1 down)
// over travel px. Each column checks its own 2 particles (stay inside the column).
float sparks(vec2 fc, float baseY, float dir, float travel, float rate, float t, float seed)
{
    const float colW = 28.0;
    float cx = floor(fc.x * (1.0 / colW));
    float s = 0.0;
    for (int k = ZERO; k < 2; k++) {
        float fk = float(k) + seed * 7.0;
        float off = EwHash2To1(vec2(cx, fk));
        float tt = t * rate * (0.7 + 0.6 * off) + off * 5.0;
        float cyc = floor(tt);
        float ph = fract(tt);
        vec3 h = EwHash3To3(vec3(cx, cyc, fk));
        if (h.x > 0.5) continue;                      // only some columns are active
        float y = baseY + dir * travel * (dir > 0.0 ? ph * (0.6 + 0.4 * h.z) : ph * ph * (0.5 + 0.5 * h.z));
        float dy = fc.y - y;
        if (abs(dy) > 75.0) continue;                 // far outside spark halo
        float x = (cx + 0.3 + 0.4 * h.y) * colW + sin(ph * 7.0 + h.z * 6.3) * (colW * 0.18);
        vec2 dv = vec2(fc.x - x, dy);
        float r = 1.5 + 1.6 * h.y;
        float life = smoothstep(0.0, 0.08, ph) * (1.0 - ph) * (1.0 - ph);
        float flick = 0.6 + 0.4 * sin(t * 25.0 + h.y * 40.0);
        float d2 = dot(dv, dv);
        s += life * flick * (exp(-d2 / (r * r)) * 1.6 + 0.25 * exp(-sqrt(d2) / (r * 3.0)));
    }
    return s;
}

// Fast 2-octave ridged ash hills + carved main lava crevices (5 noises total)
// Returns vec2(height, mainCreviceDist)
vec2 ashTerrain(vec2 p)
{
    vec2 q = p * 0.23;
    float w1 = fastNoise2D(q + vec2(1.7, 3.2)) * 2.0 - 1.0;
    float w2 = fastNoise2D(q + vec2(6.4, 8.9)) * 2.0 - 1.0;
    vec2 wp = p + vec2(w1, w2) * 1.45;
    // Main meandering chasm + primary organic fissure network
    float heroX = sin(p.y * 0.24 + 0.4) * 1.85 + cos(p.y * 0.11 - 0.7) * 0.95 + w1 * 1.35;
    float c0 = abs(p.x - heroX) * 0.28;
    float c1 = abs(fastNoise2D(wp * 0.23 + vec2(2.4, 7.1)) * 2.0 - 1.0);
    float cf = min(c0, c1);
    // Ridged rolling ash hills
    vec2 hp = p * 0.15 + vec2(4.2, 1.1);
    float n1 = 1.0 - abs(fastNoise2D(hp) * 2.0 - 1.0);
    float n2 = 1.0 - abs(fastNoise2D(mat2(1.6, 1.2, -1.2, 1.6) * hp + vec2(3.7, 1.9)) * 2.0 - 1.0);
    float hills = (0.65 * n1 + 0.35 * n2) * 0.80;
    float carve = smoothstep(0.03, 0.34, cf);
    float h = (0.10 + hills) * mix(0.20, 1.0, carve);
    return vec2(h, cf);
}

// Distant volcanic mountain silhouette profile
float mountainProfile(float x)
{
    float s = 0.0, a = 0.54;
    float px = x * 0.92 + 1.65;
    for (int i = ZERO; i < 4; i++) {
        float n = fastNoise2D(vec2(px, 2.3 + float(i) * 3.1)) * 2.0 - 1.0;
        s += a * (1.0 - abs(n));
        px = px * 2.15 + 2.7;
        a *= 0.48;
    }
    float env = 0.68 + 0.55 * sin(x * 1.45 + 0.7) * cos(x * 0.58 - 0.35);
    float dx = (x - 0.16) * 1.65;
    float dip = 1.0 - 0.34 * exp(-dx * dx);
    return (s * s) * 0.38 * env * dip;
}

// Max envelope of 3 rising volcanic smoke plumes (single smokeColumn body)
float smokeEnv3(vec2 uv, vec4 c0, vec4 c1, vec4 c2, float lean, float t)
{
    float best = 0.0;
    for (int i = ZERO; i < 3; i++) {
        vec4 c = i == 0 ? c0 : i == 1 ? c1 : c2;
        vec2 q = uv - c.xy;
        if (q.y < -0.10 || q.y > c.w + 0.25) continue;
        float hy = clamp(q.y / c.w, 0.0, 1.25);
        float xShift = lean * hy * hy + 0.05 * sin(hy * 3.8 - t * 0.35 + c.x * 5.0) * hy;
        float bw = c.z * (0.48 + 1.70 * pow(max(hy, 0.001), 0.78));
        float dx = (q.x - xShift) / max(bw, 0.02);
        float v = exp(-dx * dx * 1.25) * smoothstep(-0.08, 0.16, hy) * smoothstep(1.24, 0.52, hy);
        best = max(best, v);
    }
    return best;
}
vec3 volcanicScene(vec2 uv, float t, float scaleY, vec2 fc)
{
    float windDrift = t * (0.045 + 0.065 * Wind);
    float windLean = 0.18 + 0.38 * Wind;
    float px = 1.6 / scaleY;

    // Lava palette harmonized with FlameColor
    vec3 lavaBase = max(FlameColor, vec3(0.05));
    vec3 lavaDeep = lavaBase * 0.26 + vec3(0.16, 0.006, 0.0);
    vec3 lavaHot  = mix(lavaBase * 1.45, vec3(1.0, 0.76, 0.22), 0.52);
    vec3 lavaCore = vec3(1.0, 0.96, 0.78);

    // Camera perched above the ash wasteland looking across crevices toward volcanic peaks
    vec3 ro = vec3(0.0, 1.38, 0.0);
    vec3 rd = normalize(vec3(uv.x, uv.y - 0.11, 1.42));
    float horY = 0.11;

    float ventX1 = (uv.x - 0.16) * 1.45;
    float ventX2 = (uv.x + 0.88) * 1.55;
    float ventGlow = 0.48 + 0.95 * exp(-ventX1 * ventX1)
                          + 0.48 * exp(-ventX2 * ventX2);
    vec3 horizonHaze = vec3(0.036, 0.031, 0.036) + (lavaBase * 0.13 + vec3(0.04, 0.01, 0.004)) * ventGlow;

    // -------------------------------------------------------------------------
    // 1. Fast 3D raymarched ash landscape (evaluated first for front-to-back culling)
    // -------------------------------------------------------------------------
    bool hit = false;
    float dist = 0.85;
    const float maxD = 34.0;
    vec2 td = vec2(0.0);
    float hR = 0.0, hF = 0.0, eps = 0.0;
    vec3 pos = vec3(0.0);

    // ashTerrain height is strictly <= 0.90; skip rays that stay above 0.914 up to maxD
    if (rd.y < -0.0135) {
        float prevD = dist;
        float prevDiff = 1.0;
        float lastDiff = 0.0;
        for (int i = ZERO; i < 28; i++) {
            pos = ro + rd * dist;
            td = ashTerrain(pos.xz);
            float diff = pos.y - td.x;
            if (diff < 0.014) {
                lastDiff = diff;
                hit = true;
                break;
            }
            prevD = dist;
            prevDiff = diff;
            dist += max(0.048, diff * 0.42) + dist * 0.028;
            if (dist > maxD) break;
        }
        if (hit) {
            // Bisection (r=0,1), final sample (r=2), and forward-diff normal (r=3,4)
            // merged into one loop to keep ashTerrain inlined only twice in the shader.
            float lo = prevD;
            float hi = dist;
            float tMid = mix(lo, hi, clamp(prevDiff / max(prevDiff - lastDiff, 1e-4), 0.0, 1.0));
            for (int r = ZERO; r < 5; r++) {
                vec2 qxz = r < 3 ? ro.xz + rd.xz * tMid
                                 : pos.xz + (r == 3 ? vec2(eps, 0.0) : vec2(0.0, eps));
                vec2 rtd = ashTerrain(qxz);
                if (r < 2) {
                    if (ro.y + rd.y * tMid - rtd.x < 0.0) hi = tMid; else lo = tMid;
                    tMid = 0.5 * (lo + hi);
                } else if (r == 2) {
                    dist = tMid;
                    td = rtd;
                    pos = ro + rd * dist;
                    eps = 0.04 + 0.008 * dist;
                } else if (r == 3) {
                    hR = rtd.x;
                } else {
                    hF = rtd.x;
                }
            }
        }
    }

    float bgWeight = hit ? smoothstep(maxD - 4.0, maxD, dist) : 1.0;
    vec3 col = vec3(0.0);

    // -------------------------------------------------------------------------
    // 2. Sky, clouds, back smoke & mountain ridge (ONLY where visible behind terrain)
    // -------------------------------------------------------------------------
    if (bgWeight > 0.0) {
        float farY = horY + 0.01 + mountainProfile(uv.x);
        float farMask = smoothstep(farY + px, farY - px, uv.y);
        float horBand = exp(-max(uv.y - horY + 0.02, 0.0) * 3.8);

        if (farMask < 1.0) {
            float skyH = clamp((uv.y - (horY - 0.08)) / (1.08 - horY), 0.0, 1.0);
            vec3 skyTop = vec3(0.010, 0.011, 0.016);
            vec3 skyMid = vec3(0.046, 0.040, 0.046);
            col = mix(skyMid, skyTop, pow(skyH, 0.54));

            col += (lavaBase * 0.55 + vec3(0.18, 0.028, 0.004)) * horBand * ventGlow;
            col += (lavaBase * 0.10 + vec3(0.028, 0.012, 0.014)) * exp(-max(uv.y - horY, 0.0) * 1.65) * ventGlow;

            // Heavy brooding dark storm clouds across the sky
            float cy = pow(max(skyH + 0.04, 0.01), 0.58);
            vec2 cUv = vec2(uv.x * 1.5 - windDrift * 0.35, cy * 3.0 - t * 0.014);
            float minC1 = skyH > -0.02 ? 0.24 : 9.0;
            vec2 cRes = warpedFbm(cUv, 0.9, vec2(2.1, 4.7), 1.55, vec2(1.3, -0.85), -0.14, minC1);
            float cloudDens = smoothstep(0.24, 0.78, cRes.x) * smoothstep(-0.02, 0.20, skyH);
            if (cloudDens > 0.0) {
                float underLit = clamp((cRes.x - cRes.y) * 3.4 + 0.30, 0.0, 1.0) * exp(-skyH * 2.2) * ventGlow;
                vec3 cloudCol = mix(vec3(0.011, 0.011, 0.015), vec3(0.044, 0.041, 0.048), smoothstep(0.45, 0.85, cRes.x));
                cloudCol += (lavaBase * 0.36 + vec3(0.10, 0.018, 0.004)) * underLit * 0.72;
                col = mix(col, cloudCol, cloudDens * 0.86);
            }

            // Thick billowing dark smoke plumes rising behind the caldera peaks
            float bgEnv = smokeEnv3(uv,
                vec4( 0.14, horY - 0.02, 0.24, 1.18),
                vec4(-0.80, horY - 0.04, 0.28, 1.08),
                vec4( 1.02, horY - 0.04, 0.25, 1.04),
                windLean, t);
            if (bgEnv > 0.01) {
                vec2 sq = vec2(uv.x * 2.1 - windDrift * 0.58, uv.y * 1.9 - t * 0.16);
                vec2 sRes = warpedFbm(sq, 1.15, vec2(1.4, 5.2), 1.85, vec2(1.45, -0.85), 0.11, -1.0);
                float sMask = clamp(bgEnv * (0.42 + 1.05 * smoothstep(0.20, 0.76, sRes.x)), 0.0, 1.0);
                float fold = clamp((sRes.x - sRes.y) * 3.4 + 0.45, 0.0, 1.0);
                vec3 sCol = mix(vec3(0.009, 0.009, 0.012), vec3(0.056, 0.052, 0.058), fold * 0.65);
                float rimGlow = smoothstep(0.08, 0.45, sMask) * (1.0 - smoothstep(0.45, 0.90, sMask)) * horBand * ventGlow;
                sCol += (lavaBase * 0.36 + vec3(0.09, 0.016, 0.004)) * (rimGlow * 0.72 + fold * horBand * 0.18);
                col = mix(col, sCol, sMask * 0.93);
            }
        }

        if (farMask > 0.0) {
            float slope = 0.0;
            for (int k = ZERO; k < 2; k++) {
                float mp = mountainProfile(uv.x + (k == 0 ? 0.018 : -0.018));
                slope += k == 0 ? mp : -mp;
            }
            slope *= 18.0;
            float rTex = fbm2(vec2(uv.x * 7.5, uv.y * 12.0), 3);
            vec3 mtnCol = vec3(0.020, 0.019, 0.023) * (0.72 + 0.52 * rTex);
            mtnCol += vec3(0.040, 0.039, 0.044) * smoothstep(0.09, 0.0, farY - uv.y) * rTex;
            mtnCol += lavaBase * 0.09 * clamp(-slope * 0.4 + 0.25, 0.0, 1.0) * horBand * ventGlow;
            float mHaze = exp(-max(uv.y - (horY - 0.02), 0.0) * 9.5);
            mtnCol = mix(mtnCol, horizonHaze, mHaze * 0.85);
            col = farMask >= 1.0 ? mtnCol : mix(col, mtnCol, farMask);
        }

        if (!hit && uv.y < horY - 0.01) {
            float gapFill = smoothstep(horY - 0.01, horY - 0.06, uv.y);
            col = mix(col, horizonHaze, gapFill * 0.92);
        }
    }

    // -------------------------------------------------------------------------
    // 3. Terrain shading & molten lava crevices
    // -------------------------------------------------------------------------
    if (hit) {
        float cf = td.y;

        // Sharp rocky cinder/ash micro-detail normal (single fbm2 loop)
        vec2 gritBase = pos.xz * 3.1 + vec2(1.9, 4.3);
        float gritAmp = 0.30 * smoothstep(22.0, 2.2, dist);
        float gritC = 0.0, gritR = 0.0, gritF = 0.0;
        for (int g = ZERO; g < 3; g++) {
            vec2 gp = g == 0 ? gritBase
                    : g == 1 ? gritBase + vec2(eps * 3.1, 0.0)
                             : gritBase + vec2(0.0, eps * 3.1);
            float gv = fbm2(gp, g == 0 ? 4 : 3);
            if (g == 0) {
                gritC = gv;
                if (gritAmp <= 0.0) break;
            } else if (g == 1) {
                gritR = gv;
            } else {
                gritF = gv;
            }
        }
        float fineCinder = fastNoise2D(pos.xz * 14.0 + vec2(3.1, 7.4));
        vec3 nrm = normalize(vec3(
            (td.x - hR) + (gritC - gritR) * gritAmp,
            eps,
            (td.x - hF) + (gritC - gritF) * gritAmp
        ));

        float lavaZone = smoothstep(28.0, 4.5, dist);
        float c2 = 1.0;
        float subLine = 1.0;
        if (lavaZone > 0.0) {
            float subW = 0.0;
            bool needSub = dist < 13.0 && td.x < 0.68;
            for (int s = ZERO; s < 2; s++) {
                vec2 sp = s == 0 ? pos.xz * 0.55 + vec2(4.1, 7.8)
                                 : pos.xz * 1.25 + vec2(subW * 1.4, -subW * 0.9);
                float sv = fbm2(sp, 3);
                if (s == 0) {
                    subW = sv - 0.44;
                    if (!needSub) break;
                } else {
                    subLine = abs(sv - 0.475);
                }
            }
            c2 = abs(fastNoise2D((pos.xz + vec2(subW * 1.6, -subW * 1.1)) * 0.45 + vec2(8.3, 2.9)) * 2.0 - 1.0);
        }

        float jag = (gritC - 0.48) * 0.075;
        float distAA = 0.005 + 0.0024 * dist;

        float mainCrevice = (1.0 - smoothstep(0.032 + jag, 0.088 + jag + distAA, cf)) * lavaZone;
        float mainCore    = (1.0 - smoothstep(0.008 + jag * 0.5, 0.048 + jag + distAA, cf)) * lavaZone;
        float secCrevice  = (1.0 - smoothstep(0.018 + jag * 0.5, 0.056 + jag * 0.6 + distAA, c2))
                          * smoothstep(18.0, 3.0, dist) * smoothstep(0.75, 0.22, td.x);
        float subMask     = (1.0 - smoothstep(0.004, 0.022 + distAA * 0.5, subLine))
                          * smoothstep(13.0, 2.2, dist) * smoothstep(0.68, 0.16, td.x);
        float lavaMask = clamp(max(mainCrevice, max(secCrevice * 0.92, subMask * 0.86)), 0.0, 1.0);
        float minC = min(cf, c2 * 1.25);

        float pulse = 0.82;
        vec3 lavaCol = vec3(0.0);
        if (lavaMask > 0.0 || (lavaZone > 0.0 && minC < 0.75)) {
            float magmaFlow = fbm2(pos.xz * 2.1 + vec2(gritC * 0.85, -t * 0.15), 4);
            pulse = 0.82 + 0.18 * sin(t * 0.9 + magmaFlow * 6.5 + pos.z * 0.35);
            if (lavaMask > 0.0) {
                float slagOpen = smoothstep(0.32, 0.68, magmaFlow);
                float heatMain = mainCrevice * (0.22 + 0.84 * mainCore) * (0.24 + 0.96 * slagOpen) * pulse;
                float heatSec  = secCrevice * (0.30 + 0.75 * slagOpen) * 0.85 * pulse;
                float heatSub  = subMask * (0.35 + 0.65 * slagOpen) * 0.72 * pulse;
                float heat = clamp(max(heatMain, max(heatSec, heatSub)), 0.0, 1.35);

                lavaCol = mix(vec3(0.022, 0.012, 0.012), lavaDeep, smoothstep(0.0, 0.18, heat));
                lavaCol = mix(lavaCol, lavaBase * 1.35, smoothstep(0.16, 0.50, heat));
                lavaCol = mix(lavaCol, lavaHot, smoothstep(0.48, 0.88, heat));
                lavaCol = mix(lavaCol, lavaCore, smoothstep(0.88, 1.26, heat));
                lavaCol *= 0.85 + 1.35 * heat;
            }
        }

        // Gritty scorched basalt & dusty charcoal-grey volcanic ash
        float cinderMask = smoothstep(0.24, 0.78, gritC * 0.72 + fineCinder * 0.38);
        float ashDust = smoothstep(0.36, 0.88, nrm.y) * (0.25 + 0.75 * cinderMask);
        vec3 basaltCol = vec3(0.020, 0.020, 0.024);
        vec3 ashGrey   = vec3(0.168, 0.164, 0.172);
        vec3 rockCol = mix(basaltCol, ashGrey, ashDust) * (0.72 + 0.42 * fineCinder);

        float skyDiff = 0.24 + 0.76 * clamp(nrm.y, 0.0, 1.0);
        float backRim = clamp(nrm.z * 0.5 + 0.5, 0.0, 1.0) * smoothstep(0.25, 0.85, nrm.y);
        vec3 surfCol = rockCol * (0.42 + 0.88 * skyDiff);
        surfCol += (lavaBase * 0.07 + vec3(0.028, 0.020, 0.022)) * backRim * ventGlow * 0.55;

        float charLip = smoothstep(0.16, 0.065, minC) * lavaZone;
        surfCol *= 1.0 - 0.58 * charLip;

        float bankGlow = exp(-max(minC - 0.05, 0.0) * 9.5) * lavaZone * (0.65 + 0.35 * pulse);
        float steepFac = 0.22 + 0.78 * (1.0 - clamp(nrm.y, 0.0, 1.0));
        float subGlow  = (dist < 12.0 && td.x < 0.68)
                       ? exp(-subLine * 32.0) * 0.38 * smoothstep(12.0, 2.2, dist) * smoothstep(0.68, 0.16, td.x)
                       : 0.0;
        surfCol += (lavaBase * 0.92 + vec3(0.22, 0.032, 0.0))
                 * (bankGlow * steepFac * 1.15 + bankGlow * 0.22 + subGlow) * (0.45 + 0.55 * gritC);

        surfCol = mix(surfCol, lavaCol, lavaMask);
        surfCol += lavaBase * 0.16 * bankGlow * mainCrevice * (0.5 + 0.5 * pulse);

        float fog = 1.0 - exp(-max(dist - 2.8, 0.0) * 0.145);
        float edgeBlend = smoothstep(maxD - 11.0, maxD, dist);
        vec3 fogged = mix(surfCol, horizonHaze, clamp(max(fog, edgeBlend), 0.0, 1.0));
        col = bgWeight > 0.0 ? mix(fogged, col, bgWeight) : fogged;
    }

    // -------------------------------------------------------------------------
    // 4. Billowing foreground & midground dark smoke rising from the crevices
    // -------------------------------------------------------------------------
    float fgColEnv = smokeEnv3(uv,
        vec4(-0.30, -0.78, 0.30, 1.72),
        vec4( 0.62, -0.68, 0.32, 1.62),
        vec4(-1.05, -0.50, 0.34, 1.45),
        windLean, t);
    vec2 fUv = vec2(uv.x * 1.55 - windDrift * 0.78, uv.y * 1.75 - t * 0.12);
    float minS = fgColEnv > 0.001 ? -1.0 : 0.28;
    vec2 fRes = warpedFbm(fUv, 1.1, vec2(3.4, 9.2), 1.75, vec2(1.45, -0.95), 0.09, minS);
    float wisp = smoothstep(0.28, 0.80, fRes.x);
    float fgSmoke = clamp(fgColEnv * (0.32 + 0.88 * wisp) * 0.74 + wisp * 0.30 * smoothstep(-0.92, 0.42, uv.y), 0.0, 0.80);
    if (fgSmoke > 0.001) {
        float sFold = clamp((fRes.x - fRes.y) * 3.4 + 0.42, 0.0, 1.0);
        float lowGlow = exp(-abs(uv.y - horY + 0.08) * 3.0) * ventGlow + smoothstep(0.15, -0.85, uv.y) * 0.25;
        vec3 fgSmokeCol = mix(vec3(0.011, 0.010, 0.014), vec3(0.056, 0.053, 0.058), sFold * 0.58)
                        + (lavaBase * 0.16 + vec3(0.045, 0.009, 0.003)) * lowGlow * (0.16 + 0.45 * sFold);
        col = mix(col, fgSmokeCol, fgSmoke);
    }

    // Filmic tone map + dither against 8-bit banding in dark skies & smoke
    col = 1.0 - exp(-max(col, 0.0) * 1.42);
    col = clamp(col + EwDither(fc, iTime, 1.5), 0.0, 1.0);
    return col;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float topInset = min(TopInset, 0.45 * res.y);
    // frame box: separate top inset, center shifted down accordingly
    vec2 maxHs = vec2(0.5 * res.x - inset, 0.5 * (res.y - inset - topInset));
    vec2 hs = maxHs;
    if (SwHaveImage) {
        vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
        hs = texRes * min(maxHs.x / texRes.x, maxHs.y / texRes.y);
    }
    vec2 c = vec2(0.5 * res.x, inset + maxHs.y);
    vec2 p = fragCoord - c;
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float leftX  = c.x - hs.x;
    float rightX = c.x + hs.x;
    float botY   = c.y - hs.y;
    float topY   = c.y + hs.y;

    vec4 bg = vec4(Background * Opacity, Opacity);
    float flameH = max(FlameHeight * topInset, 10.0);   // flames scale with the room above the top line
    float t = mod(iTime * Speed, 500.0);

    float d0 = EwSdRoundedBox2D(p, hs, rad);
    float innerMask = InnerOpacity > 0.001 ? EwFillAA(d0, 0.75) : 0.0;
    if (innerMask > 0.0) {
        vec3 innerCol = vec3(0.0);
        float innerA = InnerOpacity * innerMask;
        if (SwHaveImage) {
            vec2 imgUv = clamp((p + hs) / max(2.0 * hs, vec2(1.0)), 0.0, 1.0);
            vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
            float imgLod = max(0.0, log2(max(texRes.x / max(2.0 * hs.x, 1.0), texRes.y / max(2.0 * hs.y, 1.0))));
            vec4 img = textureLod(iChannel0, imgUv, imgLod);
            if (SwTransparentImage) {
                if (img.a < 0.999) {
                    float sy = max(hs.y, 1.0);
                    vec3 bgScene = volcanicScene(p / sy, t, sy, fragCoord);
                    innerCol = mix(bgScene, img.rgb, img.a);
                } else {
                    innerCol = img.rgb;
                }
            } else {
                innerCol = img.rgb;
                innerA *= img.a;
            }
        } else {
            float sy = max(hs.y, 1.0);
            innerCol = volcanicScene(p / sy, t, sy, fragCoord);
        }
        bg = vec4(innerCol * innerA + bg.rgb * (1.0 - innerA), innerA + bg.a * (1.0 - innerA));
    }
    float reach = max(inset, flameH) + 60.0;
    float fall = min(EmberFall, SwHaveImage ? min(0.6 * res.y, 2.0 * hs.y) : 0.6 * res.y);
    float outerReach = 3.2 * flameH + Wind * flameH * 2.0 + reach + 80.0;
    bool inFlame = d0 > -reach && (!SwHaveImage || d0 < outerReach);
    bool hasEmbers = EmberAmount > 0.0 && fragCoord.x > leftX && fragCoord.x < rightX;
    bool inSp1 = hasEmbers && fragCoord.y > topY - 75.0 && (!SwHaveImage || fragCoord.y < topY + topInset * 0.9 + 80.0);
    bool inSp2 = hasEmbers && fragCoord.y > botY - 75.0 && fragCoord.y < botY + 5.0 + flameH * 2.5 + 75.0;
    bool inSp3 = hasEmbers && fall > 0.0 && fragCoord.y > topY - fall && fragCoord.y < topY + 75.0;
    if (!inFlame && !inSp1 && !inSp2 && !inSp3) { fragColor = bg; return; }

    vec2 eb2 = min(fragCoord, res - fragCoord);
    float eb = min(min(eb2.x, fragCoord.y) / (inset * 0.9), (res.y - fragCoord.y) / (topInset * 0.5 + 4.0));
    float edgeF = smoothstep(0.0, 1.0, eb);

    vec3 col = vec3(0.0);
    if (inFlame) {
        // turbulence scrolling upward (domain warped), stretched vertically like flames
        vec2 q = fragCoord * (1.0 / 55.0);
        float wv = 0.0, n = 0.0, nf = 0.0;
        for (int b = ZERO; b < 3; b++) {
            vec2 bp = b == 0 ? q * vec2(1.3, 0.5) + vec2(0.0, -t * 1.2)
                    : b == 1 ? q * vec2(2.2, 0.8) + vec2(wv * 0.9, -t * 2.6)
                             : q * vec2(5.0, 2.0) + vec2(n * 0.5, -t * 4.5);
            float bn = boltNoise(bp);
            if (b == 0) wv = bn; else if (b == 1) n = bn; else nf = bn;
        }
        // slow gusts along the frame: some stretches burn high, others low (less repetitive)
        float gust = fastNoise2D(vec2((fragCoord.x + fragCoord.y * 0.7) * (1.0 / 260.0), t * 0.23 + 3.7));
        gust = 0.35 + 0.9 * gust * gust * (3.0 - 2.0 * gust);
        // stronger toward the middle of each frame edge
        float mid;
        if (SwHaveImage) {
            vec2 fuv = clamp(abs(p) / max(hs, vec2(1.0)), 0.0, 1.0);
            float sideW = smoothstep(-80.0, 80.0, (1.0 - fuv.y) * (2.0 * hs.y) - (1.0 - fuv.x) * (2.0 * hs.x));
            mid = mix(1.0 - fuv.x * fuv.x, 1.0 - fuv.y * fuv.y, sideW);
        } else {
            vec2 cuv = abs(fragCoord / res * 2.0 - 1.0);
            // smooth switch between edge orientations (no seam on the diagonal)
            float sideW = smoothstep(-80.0, 80.0, (1.0 - cuv.y) * res.y - (1.0 - cuv.x) * res.x);
            mid = mix(1.0 - cuv.x * cuv.x, 1.0 - cuv.y * cuv.y, sideW);
        }
        mid = mix(1.0, mid, MiddleBoost);
        float hLocal = flameH * gust * (0.6 + 1.1 * (0.5 + 0.5 * wv)) * (0.55 + 0.75 * mid);
        // wind: slow global gusts + waves travelling along the frame, lean grows with height
        float windT = sin(t * 0.37) * 0.6 + (EwValueNoise1D(t * 0.5) - 0.5) * 1.2;
        float wave = sin(fragCoord.x * (1.0 / 95.0) - t * 2.3 + wv * 1.5) + 0.5 * sin(fragCoord.x * (1.0 / 37.0) + t * 3.1);
        float xw = (wv * 0.6 + nf * 0.4) * 12.0 + (windT * Wind + wave * 0.35) * hLocal * 0.55;
        float stepH = hLocal * (1.0 / 19.0);
        float w = max(3.0 + 0.06 * inset, stepH * 1.1);
        float invW = 1.0 / w;
        vec2 hsMinusRad = hs - rad;
        // search downward for the burning frame line: flames rise and lean with the wind
        float acc = 0.0, hAcc = 0.0;
        float jit = EwHash2To1(fragCoord + fract(t * 7.0) * 61.0) - 0.5;   // per pixel jitter: no stripe banding
        for (int k = ZERO; k < 20; k++) {
            float hs01 = clamp((float(k) + jit) * (1.0 / 19.0), 0.0, 1.0);
            vec2 spPos = p - vec2(xw * hs01 * (0.3 + hs01), hs01 * hLocal);
            vec2 sq = abs(spPos) - hsMinusRad;
            float dk = length(max(sq, 0.0)) + min(max(sq.x, sq.y), 0.0) - rad;
            float v = exp(-abs(dk) * invW) * (1.0 - hs01);
            acc += v; hAcc += v * hs01;
        }
        // vertical walls: every sample hits the line -> tame them, let the noise break them up
        vec2 apd = abs(p) - hs;
        float sideF = smoothstep(-20.0, 20.0, apd.x - apd.y) * smoothstep(rad + 10.0, rad + 80.0, hs.y - abs(p.y));
        float nn = 0.5 + 0.5 * n;
        acc *= mix(1.0, 0.45 * (0.6 + 0.8 * nn), sideF);
        float best = clamp(acc * stepH * (0.5 * invW), 0.0, 1.0);
        float hBest = hAcc / max(acc, 1e-4);
        // uneven fuel along the line: hot spots and darker gaps, slowly changing
        float fuel = fastNoise2D(vec2((fragCoord.x - fragCoord.y) * (1.0 / 90.0), t * 0.35));
        fuel = (0.45 + 0.6 * fuel) * (0.75 + 0.5 * mid);
        float f = best * fuel * (0.75 + 0.35 * nn) - (0.05 + 0.6 * hBest) * nn - 0.15 * nf * hBest;
        f = clamp(f * 1.2, 0.0, 1.0);
        // blackbody-like ramp: dark red -> FlameColor -> yellow -> white core
        col = vec3(0.35, 0.03, 0.0) * smoothstep(0.0, 0.15, f);
        col = mix(col, FlameColor, smoothstep(0.08, 0.4, f));
        col = mix(col, vec3(1.0, 0.72, 0.22), smoothstep(0.35, 0.75, f));
        col = mix(col, vec3(1.0, 0.96, 0.85), smoothstep(0.75, 1.0, f));
        col *= 1.6 * f + 0.4 * f * f;
        // glowing coal bed along the frame line: flickering hot spots
        float coalN = fastNoise2D(fragCoord * (1.0 / 7.0) + vec2(t * 0.4, -t * 0.25));
        float coal = (coalN * coalN * coalN) * (0.6 + 0.4 * sin(t * 3.0 + coalN * 12.0));
        float absD0 = abs(d0);
        col += (FlameColor * 0.5 + vec3(0.5, 0.15, 0.0)) * coal * exp(-absD0 * invW * (1.0 / 0.9)) * 0.9;
        col += FlameColor * 0.08 * exp(-absD0 * invW * 0.25);
        col *= Intensity * edgeF * smoothstep(-reach, -reach * 0.45, d0);
        col = max(col - 0.004, 0.0);           // cut far tails: empty areas exactly transparent
    }

    // embers: sparks rising from the top + bottom lines, embers dropping from the top
    float sp = 0.0;
    if (inSp1 || inSp2 || inSp3) {
        for (int layer = ZERO; layer < 3; layer++) {
            if (layer == 0 && !inSp1) continue;
            if (layer == 1 && !inSp2) continue;
            if (layer == 2 && !inSp3) continue;
            float baseY = layer == 0 ? topY + 5.0 : layer == 1 ? botY + 5.0 : topY - 4.0;
            float dir   = layer == 2 ? -1.0 : 1.0;
            float trav  = layer == 0 ? topInset * 0.9 : layer == 1 ? flameH * 2.5 : fall;
            float rate  = layer == 0 ? 0.35 : layer == 1 ? 0.3 : 0.18;
            float sv = sparks(fragCoord, baseY, dir, trav, rate, t, float(layer + 1));
            if (layer == 2) sv *= smoothstep(topY - fall, topY - fall * 0.5, fragCoord.y);
            sp += sv;
        }
    }
    if (sp > 0.0) {
        float xFade = min(40.0, hs.x * 0.5);
        float xm = smoothstep(leftX, leftX + xFade, fragCoord.x) * smoothstep(rightX, rightX - xFade, fragCoord.x);
        col += vec3(1.0, 0.45, 0.08) * sp * xm * EmberAmount * edgeF;
    } else if (!inFlame) {
        fragColor = bg;
        return;
    }

    col *= 1.0 + Punch;                    // exposure boost
    col = 1.0 - exp(-col);                 // tone map to [0,1]
    // saturation / contrast punch (keeps white-hot cores, deepens the colored glow)
    float lum = dot(col, vec3(0.2126, 0.7152, 0.0722));
    col = clamp(mix(vec3(lum), col, 1.0 + 0.6 * Punch), 0.0, 1.0);
    // dither against banding (gamma-space 8 bit step), alpha dithered alike
    float mc = max(col.r, max(col.g, col.b));
    // premultiplied mix of blend + additive: dense hot flame body occludes (alpha ~ color),
    // thin glow / wisps / sparks are mostly additive (alpha < color)
    float a = mc * mix(GlowAdd, 1.0, smoothstep(0.3, 0.85, mc));
    if (mc > 0.0) {
        float dz = EwDither(fragCoord, iTime, 2.0) * smoothstep(0.0, 0.02, mc);
        a = clamp(a + dz * a / max(mc, 1e-3), 0.0, 1.0);
        col = clamp(col + dz, 0.0, 1.0);
    }
    // hard guarantee: fully transparent outermost pixels (no edge artifacts)
    float em = smoothstep(1.0, 6.0, min(eb2.x, eb2.y));
    col *= em; a *= em;
    a *= 1.0 - Additive;
    fragColor = vec4(min(col + bg.rgb * (1.0 - a), 1.0), a + bg.a * (1.0 - a));
}
