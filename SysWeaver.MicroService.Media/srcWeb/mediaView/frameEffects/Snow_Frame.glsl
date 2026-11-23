//  media: image none
// =============================================================================
//  Snow Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Glitter | Strength of the twinkling ice sparkles | 0.5 | 0 .. 2 | {0}x
//  iMouse.w = Snowfall | Share of flake slots dropping flakes off the frame | 0.3 | 0 .. 100 | {0} %

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Twinkle and snowfall animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the image or snowy landscape inside the frame", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const vec3 SnowColor = vec3(0.96, 0.98, 1.0); // var: { "type": "col", "name": "Snow Color", "desc": "Color of the lit snow" }
const vec3 ShadowColor = vec3(0.30, 0.42, 0.75); // var: { "type": "col", "name": "Shadow Color", "desc": "Tint of the shaded hollows and thin edges" }
const vec3 GlitterTint = vec3(0.8, 0.92, 1.0); // var: { "type": "col", "name": "Glitter Tint", "desc": "Color of the sparkles" }
const float Brightness = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Brightness", "desc": "Overall brightness of the snow", "unit": "x" }
const float BandWidth = 0.5; // var: { "min": 0.3, "max": 0.95, "step": 0.01, "name": "Band Width", "desc": "Thickness of the snow band relative to the inset", "scale": 100, "unit": "%" }
const float Lumpiness = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Lumpiness", "desc": "How uneven the snow edges are", "scale": 100, "unit": "%" }
const float LumpSize = 1.7; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Lump Size", "desc": "Size of the snow lumps", "unit": "x" }
const float GlitterDensity = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Glitter Density", "desc": "How many sparkles sit in the snow", "scale": 100, "unit": "%" }
const float FlakeSize = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Flake Size", "desc": "Size of the falling flakes", "unit": "x" }
const float FallDistance = 0.5; // var: { "min": 0.1, "max": 1.0, "step": 0.01, "name": "Fall Distance", "desc": "How far flakes fall before melting away (share of the screen height)", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define Glitter mix(0.0, 2.0, iMouse.z)
#define Snowfall (mix(0.0, 100.0, iMouse.w) * 0.01)

#define ZERO min(iFrame, 0)

flat varying vec4 vRoGround;
varying vec3 vRayDir;
flat varying vec4 vFrameA;
flat varying vec4 vFrameB;
flat varying vec3 vSnowAlb;
flat varying vec3 vSkyFillZenith;
flat varying vec3 vGlitterTintLin;
varying vec2 vImgUV;
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// Vectorized 2D value noise + non-unrolled 4-octave fbm for the snow frame
float valueNoise2DFast(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 cx = fract((cell.x + vec2(0.0, 1.0)) * 0.1031);
    vec2 cy = fract((cell.y + vec2(0.0, 1.0)) * 0.1031);
    vec4 px = cx.xyxy;
    vec4 py = cy.xxyy;
    vec4 d = px * (2.0 * py + px + 66.66) + py * 33.33;
    vec4 h = fract((px + py + 2.0 * d) * (px + d));
    vec2 b = mix(h.xz, h.yw, u.x);
    return mix(b.x, b.y, u.y);
}

float fbm4Fast(vec2 p)
{
    const mat2 M = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < 4; i++) {
        sum += amp * valueNoise2DFast(p);
        p = M * p;
        amp *= 0.5;
    }
    return sum;
}

// snow "thickness" field: > 0 inside the snow band, in pixels
float snowField(vec2 p, vec2 hs, float rad, float hw, float sc)
{
    float d = EwSdRoundedBox2D(p, hs, rad);
    float n = fbm4Fast(p / sc + 17.3);
    float halfW = hw * (1.0 + Lumpiness * 1.3 * (n - 0.47));
    return halfW - sqrt(d * d + 0.16 * hw * hw) + 0.08 * hw;   // soft ridge, no crease
}

// height of the snow surface (pixels) from an already-computed snowField value
float snowHeightFromField(float f, vec2 p, float hw)
{
    float x = clamp(f / (hw * 1.3), 0.0, 1.0);
    float m = 1.0 - (1.0 - x) * (1.0 - x);
    return m * hw * 0.8 + valueNoise2DFast(p * 0.45) * 0.8;
}
// one layer of flakes falling off the frame
vec4 flakeLayer(vec2 fc, vec2 res, vec2 hs, float hw, float t, float layer)
{
    float C = 70.0 + 23.0 * layer;
    float off = layer * 31.0;
    float colX = floor((fc.x + off) / C);
    vec4 h = EwHash2To4(vec2(colX, layer * 7.1 + 3.0));
    if (h.x >= Snowfall) return vec4(0.0);

    float v = 70.0 * (0.7 + 0.6 * h.z) * (0.6 + 0.4 * FlakeSize);
    float fallLen = FallDistance * res.y * (0.6 + 0.4 * h.y);
    float P = fallLen / v + 1.5 + 4.0 * h.w;
    float tt = t + h.w * 37.0;
    float cycle = floor(tt / P);
    float lt = tt - cycle * P;
    vec3 h2 = EwHash2To3(vec2(colX + cycle * 13.1, layer + 5.3));

    float cx = (colX + 0.5) * C - off + (h2.x - 0.5) * C * 0.3;
    vec2 fMin = 0.5 * res - hs;
    vec2 fMax = 0.5 * res + hs;
    if (SwHaveImage && (cx < fMin.x - hw || cx > fMax.x + hw)) return vec4(0.0);
    float y0;
    if (cx < fMin.x + hw || cx > fMax.x - hw)
        y0 = mix(fMin.y + hw, fMax.y - hw, h2.y);
    else
        y0 = fMax.y - hw * (0.5 + 0.4 * h2.y);
    float tf = max(lt - 0.5, 0.0);
    const float tau = 0.6;
    float fall = v * (tf - tau * (1.0 - exp(-tf * (1.0 / tau))));
    float a = smoothstep(0.0, 0.5, lt) * (1.0 - smoothstep(0.6, 1.0, fall / fallLen));
    if (a <= 0.001) return vec4(0.0);

    float sway = C * 0.12 * sin(tf * (1.5 + h2.z) + h2.z * 6.2832) * min(tf, 1.0);
    vec2 pos = vec2(cx + sway, y0 - fall);
    float r = FlakeSize * (2.5 + 3.0 * h2.z);
    vec2 dp = fc - pos;
    float dist2 = dot(dp, dp);
    float r2 = r * r;
    if (dist2 > r2 * 22.0) return vec4(0.0);

    float dist = sqrt(dist2);
    float core = smoothstep(r, r * 0.2, dist);
    float glow = exp(-dist2 / (r2 * 2.5)) * 0.45;
    float tws = 0.5 + 0.5 * sin(t * 6.0 + h2.y * 40.0);
    float tw2 = tws * tws;
    float tw5 = tw2 * tw2 * tws;
    float tw = tw5 * tw5;
    float fa = clamp(core + glow, 0.0, 1.0) * a;
    vec3 c = SnowColor * (0.9 + 0.5 * tw) + GlitterTint * tw * Glitter * 0.6 * core;
    return vec4(c * fa, fa);
}

// =============================================================================
//  Snowy glistening hilly landscape (inner background)
// =============================================================================

float valleyCenter(float z)
{
    return sin(z * 0.026) * 9.0 + sin(z * 0.012 + 1.3) * 5.0;
}

// Vectorized 4-corner hash (bit-equivalent to 4x EwHash2To1 on cell + {0,1}x{0,1})
vec4 hash2To1Quad(vec2 cell)
{
    vec2 cx = fract((cell.x + vec2(0.0, 1.0)) * 0.1031);
    vec2 cy = fract((cell.y + vec2(0.0, 1.0)) * 0.1031);
    vec4 px = cx.xyxy;
    vec4 py = cy.xxyy;
    vec4 d = px * (2.0 * py + px + 66.66) + py * 33.33;
    return fract((px + py + 2.0 * d) * (px + d));
}

float valueNoise2DQuinticFast(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec4 h = hash2To1Quad(cell);
    vec2 b = mix(h.xz, h.yw, u.x);
    return mix(b.x, b.y, u.y);
}

vec3 valueNoise2DQuinticD(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    vec2 f2 = f * f;
    vec2 u = f2 * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec2 du = 30.0 * f2 * (f * (f - 2.0) + 1.0);
    vec4 h = hash2To1Quad(cell);
    float k1 = h.y - h.x;
    float k2 = h.z - h.x;
    float k4 = h.x - h.y - h.z + h.w;
    return vec3(h.x + k1 * u.x + k2 * u.y + k4 * u.x * u.y,
                du * (vec2(k1, k2) + k4 * u.yx));
}

vec3 valueNoise2DD(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 du = 6.0 * f * (1.0 - f);
    vec4 h = hash2To1Quad(cell);
    float k1 = h.y - h.x;
    float k2 = h.z - h.x;
    float k4 = h.x - h.y - h.z + h.w;
    return vec3(h.x + k1 * u.x + k2 * u.y + k4 * u.x * u.y,
                du * (vec2(k1, k2) + k4 * u.yx));
}

// Lean 3-octave C2-smooth macro snow hills (used for raymarching, cast shadows, AO)
float snowTerrainMacro(vec2 p)
{
    const mat2 R1S = mat2(0.80, 0.60, -0.60, 0.80) * 0.058;
    const mat2 R2S = mat2(0.92 * 0.125, -0.39 * 0.09, 0.39 * 0.125, 0.92 * 0.09);

    vec2 q = p + vec2(sin(p.y * 0.031) * 5.2, cos(p.x * 0.027) * 4.5);

    float h = valueNoise2DQuinticFast(q * 0.027 + vec2(4.2, 1.7)) * 9.2;

    float n1 = valueNoise2DQuinticFast(R1S * q + vec2(11.3, 5.8)) * 2.0 - 1.0;
    h += (1.0 - sqrt(n1 * n1 + 0.12)) * 4.5;

    h += valueNoise2DQuinticFast(R2S * p + vec2(23.1, 14.2)) * 1.45;

    float dx = p.x - valleyCenter(p.y);
    float valleyCarve = exp(dx * dx * (-1.0 / 215.0));
    float sideHills = smoothstep(7.0, 46.0, abs(dx));
    h += sideHills * 4.8 - valleyCarve * 2.7;

    return h;
}

// Analytic macro height + gradient (returns x = height, yz = dh/dp)
vec3 snowTerrainMacroD(vec2 p)
{
    const mat2 R1S = mat2(0.80, 0.60, -0.60, 0.80) * 0.058;
    const mat2 R2S = mat2(0.92 * 0.125, -0.39 * 0.09, 0.39 * 0.125, 0.92 * 0.09);

    float py031 = p.y * 0.031;
    float px027 = p.x * 0.027;
    vec2 q = p + vec2(sin(py031) * 5.2, cos(px027) * 4.5);

    vec3 n0 = valueNoise2DQuinticD(q * 0.027 + vec2(4.2, 1.7));
    float h = n0.x * 9.2;
    vec2 gQ = n0.yz * (9.2 * 0.027);

    vec3 n1d = valueNoise2DQuinticD(R1S * q + vec2(11.3, 5.8));
    float n1 = n1d.x * 2.0 - 1.0;
    float sq = sqrt(n1 * n1 + 0.12);
    h += (1.0 - sq) * 4.5;
    gQ += (n1d.yz * R1S) * (-9.0 * n1 / sq);

    vec2 gP = vec2(gQ.x - gQ.y * (sin(px027) * 0.1215),
                   gQ.y + gQ.x * (cos(py031) * 0.1612));

    vec3 n2d = valueNoise2DQuinticD(R2S * p + vec2(23.1, 14.2));
    h += n2d.x * 1.45;
    gP += (n2d.yz * R2S) * 1.45;

    float py026 = p.y * 0.026;
    float py012 = p.y * 0.012 + 1.3;
    float vc = sin(py026) * 9.0 + sin(py012) * 5.0;
    float dvc = cos(py026) * 0.234 + cos(py012) * 0.06;
    float dx = p.x - vc;
    float valleyCarve = exp(dx * dx * (-1.0 / 215.0));
    float adx = abs(dx);
    float u = clamp((adx - 7.0) * (1.0 / 39.0), 0.0, 1.0);
    float sideHills = u * u * (3.0 - 2.0 * u);
    h += sideHills * 4.8 - valleyCarve * 2.7;

    float dVal = valleyCarve * dx * (5.4 / 215.0) + (4.8 * 6.0 / 39.0) * u * (1.0 - u) * sign(dx);
    gP.x += dVal;
    gP.y -= dVal * dvc;

    return vec3(h, gP);
}

// Single-pass analytic surface normal + macro height
vec3 snowTerrainNormal(vec2 p, float dist, out float macroH)
{
    vec3 m = snowTerrainMacroD(p);
    macroH = m.x;
    vec2 grad = m.yz;

    float fMid = 1.0 - smoothstep(14.0, 52.0, dist);
    if (fMid > 0.001) {
        const mat2 RW = mat2(0.86, 0.51, -0.51, 0.86);
        vec2 wp = RW * p;
        vec2 gw = vec2(0.0);
        for (int i = ZERO; i < 3; i++) {
            vec2 freq = (i == 0) ? vec2(0.28, 0.14) : ((i == 1) ? vec2(0.78, 0.24) : vec2(2.05, 0.62));
            vec2 ofs  = (i == 0) ? vec2(3.7, 11.2)  : ((i == 1) ? vec2(9.1, 5.2)   : vec2(17.4, 3.6));
            float amp = (i == 0) ? 0.40             : ((i == 1) ? 0.17             : 0.044);
            gw += valueNoise2DQuinticD(wp * freq + ofs).yz * (freq * amp);
        }
        grad += (gw * RW) * fMid;
    }

    float fNear = 1.0 - smoothstep(4.0, 20.0, dist);
    if (fNear > 0.001) {
        vec3 g1 = valueNoise2DQuinticD(p * 5.2 + vec2(2.7, 8.3));
        vec3 g2 = valueNoise2DD(p * 13.5 + vec2(13.1, 4.9));
        grad += (g1.yz * (5.2 * 0.022) + g2.yz * (13.5 * 0.0075)) * fNear;
    }

    return normalize(vec3(-grad.x, 1.0, -grad.y));
}

// Soft powdery cast shadows across curved snowy hills (band-free IQ penumbra)
float snowCastShadow(vec3 ro, vec3 rd)
{
    float res = 1.0;
    float ph = 1e10;
    float s = 0.22;
    float sMax = min(56.0, (18.45 - ro.y) / rd.y);
    for (int i = ZERO; i < 16; i++) {
        if (s > sMax) break;
        vec3 pos = ro + rd * s;
        float d = pos.y - snowTerrainMacro(pos.xz);
        if (d < 0.002) return 0.0;
        float y = d * d / (2.0 * ph);
        float dd = sqrt(max(0.0, d * d - y * y));
        res = min(res, 6.2 * dd / max(0.001, s - y));
        if (res < 0.004) return 0.0;
        ph = d;
        s += clamp(d * 0.58, 0.22, 4.2);
    }
    res = clamp(res, 0.0, 1.0);
    return res * res * (3.0 - 2.0 * res);
}

// Fast 4-sample two-scale snow concavity AO + backlit crest height in one loop
float snowHorizonAOAndAhead(vec3 p, vec3 N, vec2 aheadOfs, out float aheadH)
{
    float hNear = 0.0;
    float hFar = 0.0;
    aheadH = p.y;
    for (int i = ZERO; i < 5; i++) {
        vec2 ofs;
        if (i == 4) ofs = aheadOfs;
        else {
            ofs = (i < 2) ? vec2(1.5, 0.8) : vec2(-5.5, 4.5);
            if ((i & 1) != 0) ofs = -ofs;
        }
        float h = snowTerrainMacro(p.xz + ofs);
        if (i < 2) hNear += h;
        else if (i < 4) hFar += h;
        else aheadH = h;
    }
    hNear = 0.5 * hNear - p.y;
    hFar = 0.5 * hFar - p.y;

    float occ = clamp(hNear * 0.36, 0.0, 0.65) + clamp(hFar * 0.12, 0.0, 0.65);
    float skyOpen = clamp(0.32 + 0.68 * N.y, 0.0, 1.0);
    return clamp((1.0 - occ) * (0.46 + 0.54 * skyOpen), 0.14, 1.0);
}

// Crisp clear winter blue sky (linear light, calibrated for ACES)
vec3 winterSky(vec3 rd, vec3 sunDir)
{
    float y = max(rd.y, 0.0);
    float sunAmt = max(dot(rd, sunDir), 0.0);

    const vec3 zenith  = vec3(0.006, 0.046, 0.34);
    const vec3 midSky  = vec3(0.028, 0.172, 0.66);
    const vec3 horizon = vec3(0.20,  0.45,  0.85);

    vec3 sky = mix(horizon, midSky, pow(y, 0.35));
    sky = mix(sky, zenith, smoothstep(0.12, 0.80, y));

    float antiSun = 1.0 - sunAmt;
    sky *= mix(vec3(1.0), vec3(0.74, 0.87, 1.08), antiSun * 0.48 * (1.0 - y * 0.4));

    float sa2 = sunAmt * sunAmt;
    float sa3 = sa2 * sunAmt;
    float sa6 = sa3 * sa3;
    float sa12 = sa6 * sa6;
    float sa24 = sa12 * sa12;
    float sa72 = sa24 * sa24 * sa24;
    sky += vec3(0.32, 0.35, 0.38) * sa12 * (0.35 + 0.65 * (1.0 - y));
    sky += vec3(0.95, 0.88, 0.74) * sa72 * 0.55;
    sky += vec3(2.4, 2.2, 1.85) * smoothstep(0.9988, 0.9997, sunAmt);

    return sky;
}

// Crisp high-frequency snow glitter: 1 px diamond-dust facets + razor-sharp crystal pinpoints (zero blur)
vec3 snowCrystalSparkle(vec2 fragCoord, float gScale, float ndh, float dist, float t, float sunMask, out float facetDim)
{
    facetDim = 0.0;
    float gAmt = Glitter;
    if (gAmt <= 0.001 || sunMask <= 0.002) return vec3(0.0);
    float dMid = 1.0 - smoothstep(48.0, 128.0, dist);
    if (dMid <= 0.002) return vec3(0.0);
    float dNear = 1.0 - smoothstep(26.0, 82.0, dist);

    float specSweep = 0.55 + 1.85 * ndh * ndh * sqrt(ndh);
    float dens = clamp(GlitterDensity, 0.0, 1.0);
    vec2 fc = fragCoord / gScale;

    vec3 glitterTintLin = vGlitterTintLin;
    vec3 sparkle = vec3(0.0);
    float restFacet = 0.0;
    float ndhScale = 0.90 + 0.12 * ndh;

    // Layer 1: Razor-sharp 1 px high-frequency crystal dust (active micro-facets)
    vec2 id1 = floor(fc);
    vec3 h1 = EwHash2To3(id1 + 19.7);
    float p1 = clamp(0.14 + 0.28 * dens, 0.08, 0.46) * (0.55 + 0.45 * dNear);
    if (h1.x >= 1.0 - p1) {
        float w1 = clamp((0.5 + 0.5 * sin(t * (2.8 + 5.0 * h1.y) + h1.z * 628.318)) * ndhScale, 0.0, 1.0);
        float w1_2 = w1 * w1;
        float w1_4 = w1_2 * w1_2;
        float f1 = w1_4 * w1_4 * w1_4 * w1;
        restFacet = max(restFacet, (1.0 - smoothstep(0.04, 0.30, f1)) * dMid);
        vec3 prism1 = mix(vec3(1.0, 0.99, 0.97), glitterTintLin * 1.5, 0.30 + 0.40 * h1.y);
        prism1 += (h1 - 0.5) * 0.34;
        sparkle += prism1 * (f1 * 9.0 * dMid);
    }

    // Layer 2 & 3: Dense sub-pixel-jittered pinpoint crystals + needle-sharp diamond star-pinpoints
    if (dNear > 0.005) {
        const float cs2 = 3.0;
        vec2 c2 = floor(fc * (1.0 / cs2));
        vec2 f2 = fc - c2 * cs2;
        vec3 h2 = EwHash2To3(c2 + 137.3);
        float p2 = clamp(0.18 + 0.38 * dens, 0.10, 0.58) * dNear;
        if (h2.x > 1.0 - p2) {
            vec2 q2 = f2 - (0.8 + 1.4 * h2.yz);
            float d2 = dot(q2, q2);
            if (d2 < 3.5) {
                float w2 = clamp((0.5 + 0.5 * sin(t * (2.2 + 4.4 * h2.z) + (h2.x + h2.y) * 412.7)) * ndhScale, 0.0, 1.0);
                float w2_2 = w2 * w2;
                float w2_4 = w2_2 * w2_2;
                float f2Flash = w2_4 * w2_4 * w2_4 * w2_2 * w2;
                restFacet = max(restFacet, exp(-d2 * 5.8) * (1.0 - smoothstep(0.04, 0.28, f2Flash)) * 0.85 * dNear);
                if (f2Flash > 0.005) {
                    float pin2 = exp(-d2 * 4.4);
                    vec3 prism2 = mix(vec3(1.0, 0.995, 0.98), glitterTintLin * 1.55, 0.28 + 0.42 * h2.z);
                    prism2 += (h2 - 0.5) * 0.36;
                    sparkle += prism2 * (pin2 * f2Flash * 13.5 * dNear);
                }
            }
        }

        const float cs3 = 8.0;
        vec2 c3 = floor(fc * (1.0 / cs3));
        vec2 f3 = fc - c3 * cs3;
        vec3 h3 = EwHash2To3(c3 + 271.9);
        float p3 = clamp(0.15 + 0.35 * dens, 0.08, 0.52) * dNear;
        if (h3.x > 1.0 - p3) {
            vec2 q3 = f3 - (2.2 + 3.6 * h3.yz);
            float d3 = dot(q3, q3);
            if (d3 < 9.0) {
                float w3 = clamp((0.5 + 0.5 * sin(t * (1.8 + 3.6 * h3.y) + h3.z * 319.1)) * ndhScale, 0.0, 1.0);
                float w3_2 = w3 * w3;
                float w3_5 = w3_2 * w3_2 * w3;
                float w3_10 = w3_5 * w3_5;
                float f3Flash = w3_10 * w3_10;
                if (f3Flash > 0.008) {
                    float core3 = exp(-d3 * 5.2);
                    float len3 = sqrt(d3);
                    float needle3 = (exp(-abs(q3.x) * 3.8) + exp(-abs(q3.y) * 3.8)) * exp(-len3 * 0.95) * 0.44;
                    vec3 prism3 = mix(vec3(1.0, 0.995, 0.98), glitterTintLin * 1.6, 0.25 + 0.40 * h3.y);
                    prism3 += (h3 - 0.5) * 0.32;
                    sparkle += prism3 * ((core3 + needle3) * f3Flash * 18.0 * dNear);
                }
            }
        }
    }

    facetDim = restFacet * sunMask * min(gAmt, 1.35) * (0.65 + 0.35 * specSweep * 0.4);
    return sparkle * sunMask * specSweep * gAmt;
}

// Shade a snow surface point in linear HDR light (for both solid hits and AA silhouette crests)
vec3 shadeSnowSurface(vec3 ro, vec3 rd, float dist, vec3 sunDir, vec3 skyCol, vec2 fragCoord, float gScale, float t, float tMax)
{
    vec3 hitPos = ro + rd * dist;
    // Analytic macro height + detailed surface normal in one pass
    float macroH;
    vec3 N = snowTerrainNormal(hitPos.xz, dist, macroH);
    hitPos.y = macroH;
    float ndl = dot(N, sunDir);

    // Skip cast shadow ray on self-shadowed backsides
    float sh = (ndl > -0.14) ? snowCastShadow(hitPos + vec3(0.0, 0.06, 0.0) + N * 0.05, sunDir) : 0.0;
    float aheadH;
    float ao = snowHorizonAOAndAhead(hitPos, N, sunDir.xz * 1.4, aheadH);

    // Linear snow albedo & crisp warm winter sunlight
    vec3 snowAlb = vSnowAlb;
    const vec3 sunCol = vec3(1.18, 1.04, 0.88) * 2.02;

    float diff = max(ndl, 0.0);
    float wrap = clamp((ndl + 0.16) * (1.0 / 1.16), 0.0, 1.0);
    vec3 direct = sunCol * (diff * 0.88 + wrap * wrap * 0.12) * sh;

    // Subsurface scattering right at the lit-to-shadow boundary & soft cast-shadow penumbra
    float termBand = smoothstep(-0.18, 0.08, ndl) * (1.0 - smoothstep(0.04, 0.36, ndl));
    float penumbra = 4.0 * sh * (1.0 - sh);
    vec3 sssEdge = vec3(0.12, 0.36, 0.72) * (termBand * (0.14 + 0.18 * sh) + penumbra * 0.14 * diff) * ao;

    // Luminous sky-lit snow shadows
    vec3 skyFillZenith = vSkyFillZenith;
    const vec3 skyFillHoriz = vec3(0.13, 0.33, 0.72);
    float skyHemi = clamp(0.32 + 0.68 * N.y, 0.0, 1.0);
    vec3 skyFill = mix(skyFillHoriz, skyFillZenith, skyHemi) * 0.92;

    // Shaded slopes facing away from sun see deeper anti-solar winter sky
    float awaySun = clamp(-dot(N.xz, sunDir.xz) * 0.5 + 0.5, 0.0, 1.0);
    skyFill *= mix(vec3(1.0), vec3(0.66, 0.84, 1.15), awaySun * 0.62);

    // Multi-bounce snow AO: red absorbs faster than blue inside snow hollows
    vec3 snowAO = pow(vec3(ao), vec3(1.85, 1.25, 0.78));
    vec3 ambient = skyFill * snowAO;

    // Soft indirect radiosity bounce from opposite sunlit snow slopes into shadowed bowls
    float bounceMask = clamp(0.38 - 0.55 * ndl, 0.0, 1.0) * ao;
    vec3 snowBounce = vec3(0.10, 0.22, 0.46) * (bounceMask * 0.38);

    // Translucent ice-blue subsurface glow on thin backlit snow crests
    float thickness = max(aheadH - (hitPos.y + sunDir.y * 1.4), 0.0);
    float thinRidge = exp(-thickness * 1.55);
    float rds = clamp(dot(rd, sunDir), 0.0, 1.0);
    float fwdScatter = 0.25 + 0.75 * rds * rds * rds;
    vec3 crestSSS = vec3(0.24, 0.54, 0.88) * thinRidge * fwdScatter * (0.08 + 0.20 * sh) * skyHemi;

    // Grazing icy crust Fresnel sheen & broad sunlit powder sheen
    vec3 H = normalize(sunDir - rd);
    float fres = EwFresnelSchlick(max(dot(N, -rd), 0.0), 0.032);
    vec3 sheen = mix(skyFillHoriz * 0.45, sunCol * 0.16, sh * 0.75) * fres * ao;
    float ndh = max(dot(N, H), 0.0);
    float ndh2 = ndh * ndh;
    float ndh4 = ndh2 * ndh2;
    float ndh8 = ndh4 * ndh4;
    float broadSpec = (ndh8 * ndh8 * ndh4 * ndh2) * 0.24 * sh;

    // Crisp high-frequency snow glitter on sunlit snow
    float sunMask = smoothstep(0.08, 0.42, sh) * smoothstep(0.0, 0.22, ndl);
    float facetDim = 0.0;
    vec3 sparkle = snowCrystalSparkle(fragCoord, gScale, ndh, dist, t, sunMask, facetDim);

    vec3 facetTint = mix(vec3(1.0), vec3(0.56, 0.74, 0.98), clamp(facetDim * 0.50, 0.0, 0.56));

    vec3 col = snowAlb * facetTint * (direct + ambient + snowBounce + sssEdge + crestSSS)
             + vec3(1.0, 0.97, 0.92) * broadSpec
             + sheen
             + sparkle;

    // Crisp cold winter aerial perspective dissolving seamlessly into the clear horizon sky
    float fog = 1.0 - exp(-pow(dist * 0.0072, 1.48));
    fog = clamp(fog + smoothstep(tMax * 0.48, tMax * 0.90, dist), 0.0, 1.0);
    vec3 fogCol = mix(skyCol, vec3(0.24, 0.50, 0.88), 0.22 * (1.0 - clamp(rd.y * 4.0, 0.0, 1.0)));
    return mix(col, fogCol, fog);
}

vec3 renderSnowLandscape(vec2 fragCoord, vec2 res, float t)
{
    vec3 ro = vRoGround.xyz;
    float groundHere = vRoGround.w;

    float invViewLen = inversesqrt(dot(vRayDir, vRayDir));
    vec3 rd = vRayDir * invViewLen;
    // Exact perspective world-space pixel height at unit distance
    float pxScale = invViewLen / res.y;

    // Low side-raking upper-left winter sun for dramatic sculpted snow shadows
    const vec3 sunDir = normalize(vec3(0.80, 0.24, 0.55));
    vec3 skyCol = winterSky(rd, sunDir);

    // Fast skip for clear upper sky
    if (rd.y > 0.18) {
        vec3 col = EwTonemapAces(skyCol * 0.92);
        return clamp(EwLinearToSrgbFast(col), 0.0, 1.0);
    }

    // Single-ray heightfield march with analytical cone-grazing silhouette AA
    const float tMax = 210.0;
    float f0 = ro.y - groundHere;
    float dist = max(0.25, f0 * 0.35);
    float tStop = (rd.y > 0.001) ? min(tMax, (18.6 - ro.y) / rd.y) : tMax;
    float s0 = 0.0;
    float s1 = 0.0;
    float f1 = f0;
    float lastAbove = 0.0;
    float firstBelow = 0.0;
    bool hasBelow = false;
    bool hit = false;

    float edgeDist0 = 0.0, edgeCov0 = 0.0;
    float edgeDist1 = 0.0, edgeCov1 = 0.0;

    for (int i = ZERO; i < 60; i++) {
        if (dist > tStop) break;
        vec3 pos = ro + rd * dist;

        float h = snowTerrainMacro(pos.xz);
        float f = pos.y - h;
        float pxW = dist * pxScale;
        float band = 1.45 * pxW;

        if (f > 0.0) {
            if (!hasBelow) lastAbove = dist;
        } else if (!hasBelow) {
            firstBelow = dist;
            hasBelow = true;
        }

        // Deep hit: pixel is 100% covered by this hill
        if (f < -band) {
            hit = true;
            break;
        }

        // Local minimum at previous step s1: ray grazed or barely nicked a hill crest
        float band1 = 1.45 * s1 * pxScale;
        if (i > 0 && f1 < f0 && f1 <= f && f1 < band1) {
            float d1 = s1 - s0;
            float d2 = dist - s1;
            float y0 = f0 - f1;
            float y2 = f - f1;
            float denom = y0 * d2 + y2 * d1;
            float sMin = s1;
            float fMin = f1;
            float sCand = s1;
            bool tryCand = denom > 1e-7;
            if (tryCand) {
                float xMin = 0.5 * (y0 * d2 * d2 - y2 * d1 * d1) / denom;
                sCand = s1 + clamp(xMin, -d1, d2);
            }

            // Combined parabolic refinement (j=0) + finite-diff ridge normal (j=1,2) with single macro call site
            vec3 pMin = ro + rd * sMin;
            float me = max(0.05, 0.0025 * sMin);
            float hMin = pMin.y - fMin;
            vec2 dh = vec2(0.0);
            for (int j = ZERO; j < 3; j++) {
                if (j == 0 && !tryCand) continue;
                vec2 qxz = (j == 0) ? (ro.xz + rd.xz * sCand)
                                    : (pMin.xz + ((j == 1) ? vec2(me, 0.0) : vec2(0.0, me)));
                float hj = snowTerrainMacro(qxz);
                if (j == 0) {
                    float fCand = (ro.y + rd.y * sCand) - hj;
                    if (fCand < fMin) {
                        sMin = sCand;
                        fMin = fCand;
                        pMin = ro + rd * sMin;
                        me = max(0.05, 0.0025 * sMin);
                        hMin = hj;
                    }
                } else if (j == 1) {
                    dh.x = (hj - hMin) / me;
                } else {
                    dh.y = (hj - hMin) / me;
                }
            }
            float macroNy = inversesqrt(1.0 + dot(dh, dh));

            float sdPx = (fMin * macroNy) / max(sMin * pxScale, 1e-6);
            float cov = smoothstep(0.65, -0.65, sdPx);

            if (cov >= 0.995) {
                if (!hasBelow) {
                    lastAbove = s0;
                    firstBelow = sMin;
                }
                hit = true;
                break;
            } else if (cov > 0.005) {
                if (edgeCov0 <= 0.0) {
                    edgeDist0 = sMin;
                    edgeCov0 = cov;
                } else if (edgeCov1 <= 0.0) {
                    edgeDist1 = sMin;
                    edgeCov1 = cov;
                }
            }
        }

        // Emerged back into air after a shallow ridge nick: reset bracket for farther hills
        if (f > 0.0 && hasBelow) {
            hasBelow = false;
            lastAbove = dist;
        }

        s0 = s1; f0 = f1;
        s1 = dist; f1 = f;

        float minStep = min(0.015 * dist + 0.00018 * dist * dist, 3.0) + 0.04;
        if (f < band * 3.0) minStep *= 0.6;
        dist += max(minStep, f * 0.60);
    }
    // Bisect solid background hit if present
    float hitDist = 0.0;
    if (hit) {
        float lo = lastAbove;
        float hi = firstBelow;
        for (int i = ZERO; i < 4; i++) {
            float mid = 0.5 * (lo + hi);
            vec3 pos = ro + rd * mid;
            if (pos.y <= snowTerrainMacro(pos.xz)) hi = mid;
            else lo = mid;
        }
        hitDist = 0.5 * (lo + hi);
    }

    // Composite solid background + up to 2 anti-aliased grazing hill crests in linear HDR light
    vec3 col = skyCol;
    if (hit || edgeCov0 > 0.0) {
        float gScale = vFrameB.w;
        for (int k = ZERO; k < 3; k++) {
            float dLayer = (k == 0) ? hitDist : ((k == 1) ? edgeDist1 : edgeDist0);
            float aLayer = (k == 0) ? (hit ? 1.0 : 0.0) : ((k == 1) ? edgeCov1 : edgeCov0);
            if (aLayer > 0.0) {
                vec3 layerCol = shadeSnowSurface(ro, rd, dLayer, sunDir, skyCol, fragCoord, gScale, t, tMax);
                col = mix(col, layerCol, aLayer);
            }
            if (k == 0 && edgeCov0 <= 0.0) break;
        }
    }
    col = EwTonemapAces(col * 0.92);
    return clamp(EwLinearToSrgbFast(col), 0.0, 1.0);
}
void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float hw = max(inset * BandWidth, 4.0);
    vec2 hs = 0.5 * res - inset;
    vec2 imgUV = vec2(0.0);
    if (SwHaveImage) {
        vec2 texSize = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texSize.x / texSize.y;
        vec2 innerMax = max(hs - 0.5 * hw, vec2(1.0));
        float boxAspect = innerMax.x / innerMax.y;
        vec2 imgHalf = (imgAspect > boxAspect)
            ? vec2(innerMax.x, innerMax.x / imgAspect)
            : vec2(innerMax.y * imgAspect, innerMax.y);
        hs = imgHalf + 0.5 * hw;
        imgUV = (fragCoord - 0.5 * res) / (2.0 * imgHalf) + 0.5;
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float t = mod(iTime * Speed, 3600.0);
    float sc = max(hw, 8.0) * 0.7 * LumpSize;
    float gScale = max(1.0, floor(res.y / 1080.0 + 0.5));

    vFrameA = vec4(inset, rad, hw, sc);
    vFrameB = vec4(hs, t, gScale);
    vImgUV = imgUV;

    if (!SwHaveImage || SwTransparentImage) {
        const float camZ = 0.0;
        float camX = valleyCenter(camZ);
        float camXNext = valleyCenter(camZ + 24.0);
        float groundHere = snowTerrainMacro(vec2(camX, camZ));
        float groundAhead = snowTerrainMacro(vec2(valleyCenter(camZ + 7.0), camZ + 7.0));
        float camY = max(groundHere, 0.5 * (groundHere + groundAhead)) + 3.6;

        vec3 ro = vec3(camX, camY, camZ);
        vec3 ta = vec3(camXNext * 0.72 + camX * 0.28, camY - 1.15, camZ + 26.0);
        mat3 cam = EwCameraLookAt(ro, ta, 0.0);

        vec2 uv = (fragCoord - 0.5 * res) / res.y;
        vRoGround = vec4(ro, groundHere);
        vRayDir = cam * vec3(uv, 1.35);

        vSnowAlb = mix(vec3(0.86, 0.90, 0.96), EwSrgbToLinear(SnowColor) * 0.92, 0.30);
        vSkyFillZenith = mix(vec3(0.028, 0.125, 0.50), EwSrgbToLinear(ShadowColor) * 0.92, 0.25);
        vGlitterTintLin = EwSrgbToLinear(GlitterTint);
    } else {
        vRoGround = vec4(0.0);
        vRayDir = vec3(0.0);
        vSnowAlb = vec3(0.0);
        vSkyFillZenith = vec3(0.0);
        vGlitterTintLin = vec3(0.0);
    }
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float rad = vFrameA.y;
    float hw = vFrameA.z;
    float sc = vFrameA.w;
    vec2 hs = vFrameB.xy;
    float t = vFrameB.z;
    float d = EwSdRoundedBox2D(p, hs, rad);

    float dz1 = EwDither(fragCoord, iTime, 1.0);
    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = 1.0 - smoothstep(-0.5, 0.5, d + hw * 0.5);

    // falling flakes (cheap, everywhere)
    vec4 fl = vec4(0.0);
    if (Snowfall > 0.0) {
        for (int i = ZERO; i < 2; i++) {
            float ti = (i == 0) ? t : (t * 0.93 + 11.0);
            fl = over(fl, flakeLayer(fragCoord, res, hs, hw, ti, float(i)));
        }
    }

    float dz = dz1 * 2.0;
    float bandMargin = SwHaveImage ? max(10.0, hw * 1.0) : 10.0;
    bool inBand = (d >= -hw * (1.0 + 0.7 * Lumpiness) - bandMargin);

    float field = 0.0;
    float cover = 0.0;
    float edgeFade = 0.0;
    vec3 imgShadow = vec3(1.0);
    if (inBand) {
        field = snowField(p, hs, rad, hw, sc);
        cover = smoothstep(-0.8, 0.8, field);
        vec2 sd = min(fragCoord, res - fragCoord);
        edgeFade = smoothstep(0.5, 10.0, min(sd.x, sd.y));
        cover *= edgeFade;

        if (SwHaveImage && InnerOpacity > 0.001 && inner > 0.001 && (cover < 0.999 || Additive > 0.001)) {
            float shField = snowField(p + vec2(-0.5, 0.7) * (hw * 0.55), hs, rad, hw, sc);
            float castSh = smoothstep(-hw * 0.35, hw * 0.25, shField);
            float contactAO = smoothstep(-hw * 0.45, 0.0, field);
            float sh = clamp(castSh * 0.44 + contactAO * 0.22, 0.0, 0.54) * edgeFade;
            vec3 shTint = mix(vec3(0.28), ShadowColor * 0.65 + 0.12, 0.38);
            imgShadow = mix(vec3(1.0), shTint, sh);
        }
    }

    vec4 innerLayer = vec4(0.0);
    if (InnerOpacity > 0.001 && inner > 0.001) {
        if (SwHaveImage) {
            vec4 img = texture(iChannel0, vImgUV);
            vec3 imgCol = img.rgb * imgShadow;
            if (SwTransparentImage) {
                vec3 innerCol = imgCol;
                if (img.a < 0.999) {
                    vec3 landCol = renderSnowLandscape(fragCoord, res, t);
                    innerCol = mix(landCol, imgCol, img.a);
                }
                innerCol = clamp(innerCol + dz1, 0.0, 1.0);
                float ia = InnerOpacity * inner;
                innerLayer = vec4(innerCol * ia, ia);
            } else {
                imgCol = clamp(imgCol + dz1, 0.0, 1.0);
                float ia = InnerOpacity * inner * img.a;
                innerLayer = vec4(imgCol * ia, ia);
            }
        } else {
            vec3 landCol = renderSnowLandscape(fragCoord, res, t);
            landCol = clamp(landCol + dz1, 0.0, 1.0);
            float ia = InnerOpacity * inner;
            innerLayer = vec4(landCol * ia, ia);
        }
    }
    vec4 base = over(innerLayer, bg);

    if (!inBand) {
        vec4 f = vec4(fl.rgb + dz, fl.a + dz);
        f = clamp(f, 0.0, 1.0);
        f.rgb = min(f.rgb, vec3(f.a));
        f.a *= 1.0 - Additive;
        fragColor = vec4(f.rgb + base.rgb * (1.0 - f.a), f.a + base.a * (1.0 - f.a));
        return;
    }
    vec3 col = vec3(0.0);
    float a = 0.0;
    if (field > -8.0 && edgeFade > 0.0) {
        const float e = 1.5;
        float h0 = snowHeightFromField(field, p, hw);
        vec2 hGrad = vec2(0.0);
        for (int i = ZERO; i < 2; i++) {
            vec2 ep = p + ((i == 0) ? vec2(e, 0.0) : vec2(0.0, e));
            float hi = snowHeightFromField(snowField(ep, hs, rad, hw, sc), ep, hw);
            if (i == 0) hGrad.x = hi - h0;
            else hGrad.y = hi - h0;
        }
        vec3 N = normalize(vec3(-hGrad * (1.0 / e), 1.0));
        const vec3 L = normalize(vec3(-0.5, 0.7, 0.6));
        const vec3 HFrame = normalize(L + vec3(0.0, 0.0, 1.0));
        float ndl = dot(N, L);
        float wrap = clamp(0.5 + 0.5 * ndl, 0.0, 1.0);
        vec3 snow = mix(ShadowColor * 0.35, SnowColor, smoothstep(0.25, 0.85, wrap));
        float nh = max(dot(N, HFrame), 0.0);
        float nh3 = nh * nh * nh;
        float nh6 = nh3 * nh3;
        float nh15 = nh6 * nh6 * nh3;
        float spec = nh15 * nh15 * 0.3;
        snow += spec;
        // translucent thin edges
        float thin = 1.0 - smoothstep(0.0, hw * 0.35, field);
        snow = mix(snow, ShadowColor * 0.6, thin * 0.5);
        snow *= Brightness;

        // sparkles: star glints
        vec3 glint = vec3(0.0);
        if (Glitter > 0.001 && GlitterDensity > 0.001) {
            const float cs = 18.0;
            vec2 cell = floor(fragCoord * (1.0 / cs));
            for (int j = ZERO - 1; j <= 1; j++)
            for (int i = ZERO - 1; i <= 1; i++) {
                vec2 c = cell + vec2(float(i), float(j));
                vec3 g = EwHash2To3(c + 31.7);
                if (g.x >= GlitterDensity * 0.25) continue;
                vec2 gp = (c + 0.2 + 0.6 * g.yz) * cs;
                float s = max(sin(t * (1.0 + 2.5 * g.y) + g.z * 40.0), 0.0);
                float s2 = s * s; s2 *= s2; s2 *= s2;
                float tw = s2 * s2;
                if (tw < 0.002) continue;
                vec2 q = fragCoord - gp;
                float len = length(q);
                float coreG = exp(-dot(q, q) * 0.35);
                float rays = (exp(-abs(q.x) * 1.1) + exp(-abs(q.y) * 1.1)) * exp(-len * 0.2)
                           + 0.5 * (exp(-abs(q.x + q.y) * 0.9) + exp(-abs(q.x - q.y) * 0.9)) * exp(-len * 0.4);
                vec3 tint = GlitterTint * (0.8 + 0.4 * vec3(g.z, g.y, 1.0 - g.y));
                glint += tint * (coreG * 4.0 + rays * 1.5) * tw;
            }
        }
        // fine pixel glitter: 1 px and 2 px grains, each with its own twinkle phase
        vec2 g1 = EwHash2To2(floor(fragCoord) + 7.0);
        float m1 = step(1.0 - 0.12 * GlitterDensity, g1.x);
        float sv1 = 0.5 + 0.5 * sin(t * (3.0 + 4.0 * g1.y) + g1.y * 900.0);
        float sv1_3 = sv1 * sv1 * sv1;
        float s1 = sv1_3 * sv1_3;
        vec2 g2 = EwHash2To2(floor(fragCoord * 0.5) + 91.0);
        float m2 = step(1.0 - 0.07 * GlitterDensity, g2.x);
        float sv2 = 0.5 + 0.5 * sin(t * (2.0 + 3.0 * g2.y) + g2.y * 700.0);
        float sv2_2 = sv2 * sv2; sv2_2 *= sv2_2;
        float s2 = sv2_2 * sv2_2;
        float dust = m1 * s1 * 2.5 + m2 * s2 * 3.5;
        float facet = clamp(m1 * (1.0 - s1) + m2 * (1.0 - s2), 0.0, 1.0);
        glint *= smoothstep(-8.0, 1.0, field) * edgeFade;
        glint += GlitterTint * dust * (0.5 + 0.5 * wrap) * cover;

        snow *= 1.0 - 0.35 * facet * min(Glitter, 1.0);
        vec3 sn = (1.0 - exp(-snow * 2.5)) * cover;
        vec3 gl = glint * Glitter * 0.65;
        gl = gl / (1.0 + gl);
        col = sn * (1.0 - 0.6 * gl) + gl;
        a = max(cover, max(gl.r, max(gl.g, gl.b)));
    }

    // flakes in front
    vec4 s = over(fl, vec4(col, a));
    col = s.rgb; a = s.a;

    col += dz; a += dz;
    a = clamp(a, 0.0, 1.0);
    col = clamp(col, 0.0, 1.0);
    col = min(col, vec3(a));
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
