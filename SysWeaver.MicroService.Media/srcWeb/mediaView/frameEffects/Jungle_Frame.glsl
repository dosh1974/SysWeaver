//  media: image none
// =============================================================================
//  Jungle Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Sway and falling speed", "unit": "x" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner foliage or image", "scale": 100, "unit": "%" }
const vec3 InnerColor = vec3(0.0297, 0.0687, 0.0368); // var: { "type": "col", "name": "Inner Foliage Color", "desc": "Color modifier for the inner foliage, leaves and vines" }
const vec3 VineColor = vec3(0.32, 0.36, 0.14); // var: { "type": "col", "name": "Vine Color", "desc": "Color of the vines" }
const vec3 LeafColor = vec3(0.16, 0.55, 0.12); // var: { "type": "col", "name": "Leaf Color", "desc": "Base color of the leaves" }
const vec3 FruitColor = vec3(1.0, 0.45, 0.08); // var: { "type": "col", "name": "Fruit Color", "desc": "Base color of the fruit" }
const int Vines = 4; // var: { "min": 1, "max": 5, "step": 1, "name": "Vines", "desc": "Number of vine strands" }
const float LeafDensity = 0.95; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Leaf Density", "desc": "Share of leaf slots filled", "scale": 100, "unit": "%" }
const float Branches = 0.25; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Branches", "desc": "Share of slots growing a leafy twig / sprout", "scale": 100, "unit": "%" }
const float LeafSize = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Leaf Size", "desc": "Leaf size", "unit": "x" }
const float FruitAmount = 0.25; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Fruit", "desc": "Share of slots carrying fruit", "scale": 100, "unit": "%" }
const float FallingLeaves = 0.35; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Falling Leaves", "desc": "Amount of falling leaves", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)

#define ZERO min(iFrame, 0)

// Vectorized 4-corner equivalent of EwValueNoise2D (identical Hoskins hash)
float fastValueNoise2D(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 b = f * f * (3.0 - 2.0 * f);
    vec4 cxy = fract(vec4(cell.x, cell.x + 1.0, cell.y, cell.y + 1.0) * 0.1031);
    vec4 x = cxy.xyxy;
    vec4 y = cxy.zzww;
    vec4 d = x * (2.0 * y + x + 66.66) + y * 33.33;
    vec4 xd = x + d;
    vec4 h = fract((xd + y + d) * xd);
    vec2 m = mix(h.xz, h.yw, b.x);
    return mix(m.x, m.y, b.y);
}

float vineNoise(vec2 p)
{
    float n = 0.0, a = 0.6;
    for (int i = ZERO; i < 3; i++) {
        n += a * (fastValueNoise2D(p) * 2.0 - 1.0);
        p = p * 2.03 + vec2(13.7, 7.1);
        a *= 0.5;
    }
    return n;
}

float vineNoiseOff(vec2 p, float fi, float amp)
{
    float n = clamp(vineNoise(p / 240.0 + vec2(fi * 17.3, fi * 5.1)) * 1.4, -1.0, 1.0);
    n = n > 0.0 ? n * 0.35 : n;   // outward (toward the screen edge) only a little: vines stay clear of the edge fade
    return n * amp;
}

float vineOff(vec2 p, float fi, float t, float amp)
{
    return vineNoiseOff(p, fi, amp) + 2.5 * sin(t * 0.7 + fi * 2.0 + (p.x + p.y) * 0.01);
}

vec2 boxNormal(vec2 p, vec2 hs, float rad)
{
    vec2 q = abs(p) - hs + rad;
    vec2 n = (q.x > 0.0 && q.y > 0.0) ? normalize(q) : (q.x > q.y ? vec2(1, 0) : vec2(0, 1));
    return n * vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
}

void under(inout vec3 rgb, inout float T, vec3 c, float a)
{
    rgb += c * (T * a);
    T *= 1.0 - a;
}

// leaf sdf in local coords (x along leaf 0..L, y across)
float leafSd(vec2 l, float L, float W)
{
    float xn = clamp(l.x / L, 0.0, 1.0);
    float w = W * 2.6 * sqrt(xn) * (1.0 - xn);
    return max(abs(l.y) - w, max(-l.x, l.x - L));
}

vec3 leafShade(vec2 l, float sd, float L, vec3 h)
{
    vec3 c = LeafColor * (0.75 + 0.5 * h.x);
    c = mix(c, c * vec3(1.6, 1.3, 0.4), 0.35 * h.y * h.y);       // some yellowish
    c *= l.y > 0.0 ? 1.1 : 0.8;                                   // two halves lit differently
    c *= 0.65 + 0.35 * smoothstep(0.0, 4.0, -sd);                 // darker rim
    c = mix(c, c * 0.55, smoothstep(1.0, 0.3, abs(l.y)) * step(0.0, l.x) * (1.0 - l.x / L)); // midrib
    return c;
}

void drawLeafFTB(inout vec3 rgb, inout float T, vec2 p, vec2 A, vec2 dir, float L, float W, vec3 h)
{
    vec2 l = p - A;
    float r = L + 6.0;
    if (dot(l, l) > r * r) return;
    l = vec2(dot(l, dir), dot(l, vec2(-dir.y, dir.x)));
    if (l.x <= -5.5 || l.x >= L + 5.5 || abs(l.y) >= W * 1.001 + 5.5) return;
    float sd = leafSd(l, L, W);
    if (sd < 1.0) {
        float a = clamp(0.5 - sd, 0.0, 1.0);
        under(rgb, T, leafShade(l, sd, L, h), a);
        if (T <= 0.001) return;
    }
    if (sd < 5.5) {
        T *= 1.0 - 0.3 * smoothstep(4.0, -2.0, sd - 1.5); // soft shadow
    }
}

void drawInnerLeafFTB(inout vec3 rgb, inout float T, vec2 delta, float angBase, float swayAmp, float swayPhase, float L, float W, vec4 h, vec3 leafBase, vec3 leafLit)
{
    float r = L + 6.0;
    if (dot(delta, delta) > r * r) return;
    float ang = angBase + swayAmp * sin(swayPhase);
    vec2 dir = vec2(cos(ang), sin(ang));
    vec2 l = vec2(dot(delta, dir), dot(delta, vec2(-dir.y, dir.x)));
    if (l.x <= -6.8 || l.x >= L + 6.8 || abs(l.y) >= W * 1.001 + 6.8) return;
    float xn = clamp(l.x / L, 0.0, 1.0);
    float wRaw = W * 2.6 * sqrt(xn) * (1.0 - xn);
    float sd = max(abs(l.y) - wRaw, max(-l.x, l.x - L));
    if (sd < 1.0) {
        float w = max(wRaw, 0.1);
        float absY = abs(l.y);
        float absNormY = absY / w;
        float rib = smoothstep(1.2, 0.2, absY) * step(0.0, l.x) * (1.0 - xn);
        float vein = smoothstep(0.65, 1.0, sin(l.x * 0.5 - absY * 0.9)) * 0.25 * (1.0 - absNormY);
        float curvature = sqrt(max(1.0 - absNormY * absNormY, 0.0));
        float lit = (l.y > 0.0 ? 1.15 : 0.8) * (0.65 + 0.35 * curvature);
        vec3 col = mix(leafBase, leafLit, 0.35 * h.z + 0.18 * lit);
        col = mix(col, col * vec3(1.3, 1.25, 0.45), 0.22 * h.w * h.w); // slight yellowish tint
        col = mix(col, leafBase * 0.55, rib * 0.7); // midrib
        col += leafLit * vein * 0.35; // secondary veins
        col *= 0.65 + 0.35 * smoothstep(0.0, 3.5, -sd); // rim shade
        under(rgb, T, col, clamp(0.5 - sd, 0.0, 1.0));
        if (T <= 0.001) return;
    }
    // Soft shadow under leaf
    if (sd < 6.8) {
        T *= 1.0 - 0.42 * smoothstep(5.0, -1.0, sd - 1.8);
    }
}

vec3 renderInnerFoliage(vec2 p, float t, vec3 tint)
{
    // Derive palette from color modifier
    vec3 baseDark = tint * 0.28;
    vec3 leafBase = tint * 1.1;
    vec3 leafLit  = tint * vec3(1.7, 1.9, 1.2);
    vec3 vineCol  = tint * vec3(1.4, 1.15, 0.65);

    vec3 rgb = vec3(0.0);
    float T = 1.0;

    // 1. Dense overlapping leaves in 3x3 cells (front-to-back, single drawInnerLeafFTB call site)
    float cs = 52.0;
    vec2 cell = floor(p / cs);
    for (int idx = 8 + ZERO; idx >= 0; idx--) {
        int dy = idx / 3;
        int dx = idx - dy * 3;
        vec2 c = cell + vec2(float(dx) - 1.0, float(dy) - 1.0);
        vec4 h2 = EwHash2To4(c + 91.83);
        vec4 h1 = vec4(0.0);

        for (int layer = 2 + ZERO; layer >= 0; layer--) {
            vec2 a;
            float angBase, swayAmp, swayPhase, L, W;
            vec4 lh;
            float scale;
            if (layer == 2) {
                a = (c + 0.5 + (h2.xy - 0.5) * 0.6) * cs;
                angBase = h2.z * 6.283185;
                swayAmp = 0.14;
                swayPhase = t * 1.5 + h2.y * 6.28;
                L = 34.0 + 28.0 * h2.w;
                W = 12.0 + 8.0 * h2.x;
                lh = h2;
                scale = 1.0;
            } else {
                if (layer == 1) {
                    h1 = EwHash2To4(c + 43.17);
                    if (h1.z <= 0.35) continue;
                    a = (c + 0.25 + 0.5 * h1.zw) * cs;
                    angBase = h2.w * 6.283185;
                    swayAmp = 0.15;
                    swayPhase = t * 1.6 + h1.w * 6.28;
                    L = 20.0 + 16.0 * h2.z;
                    W = 7.0 + 5.0 * h2.y;
                    lh = h1.wxyz;
                    scale = 0.85;
                } else {
                    a = (c + 0.15 + 0.7 * h1.xy) * cs;
                    angBase = h1.z * 6.283185;
                    swayAmp = 0.12;
                    swayPhase = t * 1.3 + h1.y * 6.28;
                    L = 32.0 + 26.0 * h1.w;
                    W = 11.0 + 7.0 * h1.x;
                    lh = h1;
                    scale = 0.9;
                }
            }
            drawInnerLeafFTB(rgb, T, p - a, angBase, swayAmp, swayPhase, L, W, lh, leafBase * scale, leafLit * scale);
            if (T <= 0.001) return rgb;
        }
    }

    // 2. Winding background vines (folded into 1 loop = 1 vineNoise call site, tightly bounded)
    float sdV = 1e5;
    for (int v = ZERO; v < 4; v++) {
        vec2 vp;
        float freq, phase, sAmp, midY, boundY, nBound, nFreq, nAmp, vw;
        vec2 nOff;
        if (v == 0) {
            vp = p; freq = 0.007; phase = 1.2; sAmp = 85.0; midY = -50.0; boundY = 143.0;
            nBound = 58.0; nFreq = 0.006; nOff = vec2(0.0); nAmp = 45.0; vw = 3.2;
        } else if (v == 1) {
            vp = vec2(p.x * 0.707 - p.y * 0.707, p.x * 0.707 + p.y * 0.707);
            freq = 0.008; phase = 3.5; sAmp = 80.0; midY = 0.0; boundY = 132.0;
            nBound = 52.0; nFreq = 0.007; nOff = vec2(12.0, 6.0); nAmp = 40.0; vw = 2.8;
        } else if (v == 2) {
            vp = p.yx; freq = 0.006; phase = -0.9 + 1.57079633; sAmp = 100.0; midY = 70.0; boundY = 163.0;
            nBound = 63.0; nFreq = 0.006; nOff = vec2(8.0, 21.0); nAmp = 50.0; vw = 3.0;
        } else {
            vp = vec2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8);
            freq = 0.007; phase = -2.1 + 1.57079633; sAmp = 90.0; midY = -20.0; boundY = 147.0;
            nBound = 57.5; nFreq = 0.006; nOff = vec2(19.0, 14.0); nAmp = 45.0; vw = 2.7;
        }
        if (abs(vp.y - midY) < boundY) {
            float base = sin(vp.x * freq + phase) * sAmp + midY;
            if (abs(vp.y - base) < nBound) {
                float yv = base + vineNoise(vp * nFreq + nOff) * nAmp;
                sdV = min(sdV, abs(vp.y - yv) - vw);
            }
        }
    }

    if (sdV < 1.0) {
        float bark = 0.85 + 0.15 * sin((p.x + p.y) * 0.4);
        vec3 c = vineCol * bark * (0.6 + 0.4 * smoothstep(1.0, -2.0, sdV));
        under(rgb, T, c, clamp(0.5 - sdV, 0.0, 1.0));
        if (T <= 0.001) return rgb;
    }
    if (sdV < 7.0) {
        T *= 1.0 - 0.45 * smoothstep(5.0, -1.0, sdV - 2.0);
    }

    // 3. Deep undergrowth & dappled canopy layer (non-unrolled Voronoi using squared dist)
    vec2 uvB = p * 0.02;
    vec2 vCell = floor(uvB);
    vec2 vFrac = fract(uvB);
    float f1Sq = 64.0, f2Sq = 64.0;
    vec2 nearestCell = vCell;
    for (int vi = ZERO; vi < 9; vi++) {
        int vy = vi / 3;
        int vx = vi - vy * 3;
        vec2 nb = vec2(float(vx) - 1.0, float(vy) - 1.0);
        vec2 toPt = nb + EwHash2To2(vCell + nb) - vFrac;
        float dSq = dot(toPt, toPt);
        if (dSq < f1Sq) {
            f2Sq = f1Sq;
            f1Sq = dSq;
            nearestCell = vCell + nb;
        } else if (dSq < f2Sq) {
            f2Sq = dSq;
        }
    }
    float vF1 = sqrt(f1Sq);
    float vF2 = sqrt(f2Sq);
    float clump = smoothstep(0.1, 0.85, 1.0 - vF1);
    float crevice = smoothstep(0.0, 0.2, vF2 - vF1);
    vec3 underCol = mix(baseDark, leafBase * 0.65, clump * crevice);
    underCol = mix(underCol, underCol * vec3(1.3, 1.2, 0.5), 0.25 * EwHash2To1(nearestCell));

    // Dappled sun filtering down through high canopy
    float dapple = fastValueNoise2D(p * 0.008 + vec2(t * 0.06, -t * 0.04));
    underCol += leafLit * 0.3 * (dapple * dapple * dapple);

    return rgb + T * underCol;
}
void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {
        vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
        float texAspect = texRes.x / texRes.y;
        float boxAspect = hs.x / max(hs.y, 1.0);
        if (texAspect > boxAspect) {
            hs.y = hs.x / texAspect;
        } else {
            hs.x = hs.y * texAspect;
        }
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);

    float amp = min(inset * 0.8, 0.5 * min(hs.x, hs.y));
    float t = mod(iTime * Speed, 1000.0);
    float ls = LeafSize;

    // Front-to-back accumulation: accRgb = premultiplied color, accT = remaining transmittance (1 - alpha)
    vec3 accRgb = vec3(0.0);
    float accT = 1.0;

    // 1. Falling leaves (front-to-back: layer 1 then layer 0)
    for (int k = 1 + ZERO; k >= 0; k--) {
        if (accT <= 0.001) break;
        float fk = float(k);
        float cs = (190.0 + 90.0 * fk) * ls;
        float v = 35.0 + 20.0 * fk;
        vec2 q = p + vec2(0.0, t * v);
        vec2 cell = floor(q / cs);
        vec4 h = EwHash2To4(cell + fk * 31.0);
        if (h.x > FallingLeaves) continue;
        vec2 ctr = (cell + 0.5 + (h.yz - 0.5) * 0.4) * cs + vec2(sin(t * 1.3 + h.y * 6.28) * cs * 0.15, 0.0);
        float L = (16.0 + 8.0 * fk) * ls;
        vec2 dCtr = q - ctr;
        float maxD = L + 20.0;
        if (dot(dCtr, dCtr) > maxD * maxD) continue;
        // fade the whole leaf out before it reaches the frame / screen edge
        vec2 scFrame = ctr - vec2(0.0, t * v);
        vec2 e2 = min(scFrame + 0.5 * res, 0.5 * res - scFrame);
        vec2 fe2 = (hs + inset) - abs(scFrame);
        vec2 edgeDist = min(e2, fe2);
        float fade = smoothstep(L, L + 90.0, min(edgeDist.x, edgeDist.y));
        if (fade <= 0.0) continue;
        float a = t * (h.z - 0.5) * 3.0 + h.w * 6.28;
        float ca = cos(a), sa = sin(a);
        mat2 rot = mat2(ca, -sa, sa, ca);
        float tum = cos(t * 2.0 + h.y * 12.0);
        float tumAbs = max(abs(tum), 0.2);
        float invTum = 1.0 / tumAbs;
        float W = (5.0 + 2.0 * fk) * ls;
        float layerFade = (0.75 + 0.25 * fk) * fade;
        // Leaf body first (front-to-back)
        vec2 l = rot * dCtr;
        l.y *= invTum;
        l.x += L * 0.5;
        float sd = leafSd(l, L, W) * tumAbs;
        if (sd < 1.0) {
            vec3 col = leafShade(l, sd, L, vec3(h.w, h.y, 0.0)) * (0.7 + 0.3 * abs(tum));
            under(accRgb, accT, col, clamp(0.5 - sd, 0.0, 1.0) * layerFade);
            if (accT <= 0.001) break;
        }
        // Soft offset shadow cast onto the inner area
        vec2 lSh = rot * (dCtr - vec2(5.0, -7.0));
        lSh.y *= invTum;
        lSh.x += L * 0.5;
        float sdSh = leafSd(lSh, L, W) * tumAbs;
        if (sdSh < 6.0) {
            accT *= 1.0 - 0.22 * smoothstep(5.0, -2.0, sdSh - 1.0) * layerFade;
        }
    }

    // 2. Frame: leaves/fruit/branches (front-to-back) then vines
    if (accT > 0.001 && d > -(amp + 58.0 * ls + 8.0) && d < 0.35 * amp + 48.0 * ls + 8.0) {
        // Leaves and fruit: 4x4 grid search centered on p (reverse order: front-to-back)
        float cs = 40.0 * ls;
        vec2 cb = floor(p / cs - 0.5);
        float dcMin = -(amp + 2.5 + 0.5 * cs);
        float dcMax = 0.35 * amp + 2.5 + 0.5 * cs;

        for (int j = 15 + ZERO; j >= 0; j--) {
            if (accT <= 0.001) break;
            int jy = j >> 2;
            int jx = j & 3;
            vec2 cell = cb + vec2(float(jx) - 1.0, float(jy) - 1.0);
            vec4 h = EwHash2To4(cell + 71.0);
            if (h.x > LeafDensity) continue;
            vec2 c = (cell + 0.3 + 0.4 * h.yz) * cs;
            float dc = EwSdRoundedBox2D(c, hs, rad);
            if (dc < dcMin || dc > dcMax) continue;

            // Compute h2 and exact per-cell max reach from A before expensive vine search
            vec3 h2 = EwHash2To3(cell + 13.0);
            bool isFruit = h2.z < FruitAmount;
            bool isBranch = !isFruit && (h2.z < FruitAmount + Branches);
            float br = fract(h2.z * 37.0);
            float brLen = (10.0 + 24.0 * br) * ls;
            float brLsz = (0.5 + 0.5 * br) * ls;
            float maxFromA = (38.0 + 5.0 * h.y) * ls + 12.0;
            if (isBranch) maxFromA = max(maxFromA, brLen + 20.0 * brLsz + 6.0);

            // Anisotropic cull along frame normal & tangent BEFORE vine noise search
            vec2 n = boxNormal(c, hs, rad);
            vec2 pc = p - c;
            float pcT = abs(dot(pc, vec2(-n.y, n.x)));
            if (pcT > maxFromA + 10.0) continue;
            float sMin = max(-amp - 2.5, dc - 0.5 * cs - 5.0) - dc;
            float sMax = min(0.35 * amp + 2.5, dc + 0.5 * cs + 5.0) - dc;
            float pcN = dot(pc, n);
            float tDist = max(pcT - 10.0, 0.0);
            float nDist = max(max(sMin - 6.0 - pcN, pcN - (sMax + 6.0)), 0.0);
            if (tDist * tDist + nDist * nDist > maxFromA * maxFromA) continue;

            // Nearest vine, start at a random one so overlapping vines all get leaves
            float fi = 0.0, best = 1e9, bestNoise = 0.0;
            int s0 = int(h.w * 7.0);
            float cxy = (c.x + c.y) * 0.01;
            for (int i = ZERO; i < Vines; i++) {
                float fv = float((i + s0) % Vines);
                float vn = vineNoiseOff(c, fv, amp);
                float e = abs(dc - (vn + 2.5 * sin(fv * 2.0 + cxy))); // static: choice never changes over time
                if (e < best - 4.0) {
                    best = e;
                    fi = fv;
                    bestNoise = vn;
                    if (best <= 4.0) break; // no subsequent vine can satisfy e < best - 4.0
                }
            }
            if (best > 0.5 * cs) continue;

            // Snap onto the vine (reuse bestNoise), then cull before refinement steps
            vec2 A = c - n * (dc - (bestNoise + 2.5 * sin(t * 0.7 + fi * 2.0 + cxy)));
            vec2 pa0 = p - A;
            float rPre = maxFromA + 10.0;
            if (dot(pa0, pa0) > rPre * rPre) continue;

            for (int r = ZERO; r < 2; r++) {
                n = boxNormal(A, hs, rad);
                float err = EwSdRoundedBox2D(A, hs, rad) - vineOff(A, fi, t, amp);
                A -= n * err;
                if (abs(err) < 1.2) break;
            }
            vec2 pa = p - A;
            float dA2 = dot(pa, pa);
            if (dA2 > maxFromA * maxFromA) continue;

            vec2 T = vec2(-n.y, n.x) * (h2.x < 0.5 ? -1.0 : 1.0);
            float sway = 0.15 * sin(t * 1.5 + h.y * 9.0);

            // Reverse order inside cell:
            // (a) Fruit or Branch (Main leaf handled in unified leaf loop below)
            if (isFruit) {
                float maxF = 28.0 * ls + 14.0;
                if (dA2 <= maxF * maxF) {
                    float r = (5.0 + 4.0 * h2.x) * ls;
                    vec2 F = A + vec2(sin(t * 1.2 + h.y * 6.0) * 2.0, -r - 3.0 * ls);
                    vec2 pf = p - F;
                    float sd = length(pf) - r;
                    if (sd < 1.0) {
                        vec2 v = pf / r;
                        vec3 fc = mix(FruitColor, FruitColor.gbr * 0.9 + vec3(0.6, 0.0, 0.1), step(0.6, h2.y) * 0.6);
                        float lz = sqrt(max(1.0 - dot(v, v), 0.0));
                        float dif = clamp(dot(vec3(v, lz), vec3(-0.50709255, 0.60851106, 0.60851106)), 0.0, 1.0);
                        float d2 = dif * dif;
                        float d4 = d2 * d2;
                        float d8 = d4 * d4;
                        vec3 col = fc * (0.3 + 0.8 * dif) + vec3(0.6) * (d8 * d8 * d8);
                        under(accRgb, accT, col, clamp(0.5 - sd, 0.0, 1.0));
                    }
                    if (accT > 0.001) {
                        vec2 pfSh = pf - vec2(2.0, -3.0);
                        float rSh = r + 6.0;
                        if (dot(pfSh, pfSh) < rSh * rSh) {
                            float sdSh = length(pfSh) - r;
                            accT *= 1.0 - 0.32 * smoothstep(5.0, -1.5, sdSh - 1.0);
                        }
                    }
                    if (accT > 0.001) {
                        float st = EwSdSegment2D(p, A, F) - 0.9;
                        if (st < 0.5) under(accRgb, accT, VineColor * 0.6, clamp(0.5 - st, 0.0, 1.0));
                    }
                }
            } else if (isBranch) {
                vec2 d1 = normalize(T * 0.6 - n * (0.6 + h.y) + vec2(sway * 0.5));
                float bend = (h2.x - 0.5) * 1.6 + sway;
                vec2 d2 = normalize(d1 + vec2(-d1.y, d1.x) * bend);
                vec2 B1 = A + d1 * brLen * 0.55, B2 = B1 + d2 * brLen * 0.45;
                // 3 branch leaves folded into one loop (front-to-back: B2 leaf 2, B2 leaf 1, B1 leaf)
                for (int bl = 2 + ZERO; bl >= 0; bl--) {
                    if (accT <= 0.001) break;
                    vec2 bA = bl == 0 ? B1 : B2;
                    float bL = (bl == 2 ? 18.0 : (bl == 1 ? 20.0 : 16.0)) * brLsz;
                    vec2 dp = p - bA;
                    float bR = bL + 6.0;
                    if (dot(dp, dp) > bR * bR) continue;
                    vec2 bDir = bl == 2 ? normalize(d2 - vec2(-d2.y, d2.x) * 0.7)
                              : (bl == 1 ? normalize(d2 + vec2(-d2.y, d2.x) * 0.7)
                                         : normalize(d1 + vec2(d1.y, -d1.x) * 1.2));
                    float bW = (bl == 2 ? 5.5 : (bl == 1 ? 6.0 : 5.0)) * brLsz;
                    vec3 bH = bl == 2 ? h2.yxz : (bl == 1 ? h2 : h2.zxy);
                    drawLeafFTB(accRgb, accT, p, bA, bDir, bL, bW, bH);
                }
                if (accT > 0.001) {
                    float th = 1.0 + 1.2 * br;
                    float sb = min(EwSdSegment2D(p, A, B1) - th, EwSdSegment2D(p, B1, B2) - th * 0.6);
                    if (sb < 0.5) under(accRgb, accT, VineColor * 0.75, clamp(0.5 - sb, 0.0, 1.0));
                    if (accT > 0.001 && sb < 4.0) accT *= 1.0 - 0.25 * smoothstep(3.0, -1.0, sb - 1.0);
                }
            }

            // (b) Unified leaf loop: m=3 main leaf, m=2/1 two A2 leaves, m=0 extra leaf
            float paT = dot(pa, T);
            float paN = abs(dot(pa, n));
            float off2 = (11.0 + 5.0 * h.y) * ls;
            for (int m = 3 + ZERO; m >= 0; m--) {
                if (accT <= 0.001) break;
                vec2 lA = A;
                vec2 lDir;
                float lL, lW;
                vec3 lH;
                if (m == 3) {
                    if (isFruit || isBranch) continue;
                    lL = (22.0 + 14.0 * h.y) * ls;
                    float rMain = lL + 6.0;
                    if (dA2 > rMain * rMain) continue;
                    float ang = (h2.y - 0.5) * 2.2 + sway;
                    lDir = normalize(T + n * tan(clamp(ang, -1.2, 1.2)));
                    lW = (6.0 + 3.0 * h.z) * ls;
                    lH = h2;
                } else if (m == 0) {
                    lL = (16.0 + 10.0 * h.z) * ls;
                    float rEx = lL + 6.0;
                    if (dA2 > rEx * rEx) continue;
                    lDir = normalize(-T + n * (h2.y - 0.3) + 0.3 * vec2(sway));
                    lW = (5.0 + 2.0 * h.y) * ls;
                    lH = h2.yzx;
                } else {
                    float origM = float(m - 1);
                    float sgn = m == 1 ? 1.0 : -1.0;
                    lH = fract(h2 * (3.7 + origM * 5.3) + h.zwy);
                    lL = (15.0 + 10.0 * lH.z) * ls;
                    // Exact tangent cull + generous normal cull before snapping A2
                    if (abs(paT - sgn * off2) > lL + 6.0 || paN > lL + off2 + 18.0) continue;
                    lA = A + T * (sgn * off2);
                    lA -= n * (EwSdRoundedBox2D(lA, hs, rad) - vineOff(lA, fi, t, amp));
                    vec2 dA2p = p - lA;
                    float r2 = lL + 6.0;
                    if (dot(dA2p, dA2p) > r2 * r2) continue;
                    float side = lH.x < 0.5 ? -1.0 : 1.0;
                    lDir = normalize(T * sgn * 0.8 + n * side * (0.5 + lH.y) + 0.3 * vec2(sway));
                    lW = (4.5 + 2.5 * lH.y) * ls;
                }
                drawLeafFTB(accRgb, accT, p, lA, lDir, lL, lW, lH);
            }
        }

        // Vines in reverse order (front-to-back), only in the vine elevation band
        if (accT > 0.001 && d > -(amp + 14.0) && d < 0.35 * amp + 14.0) {
            for (int i = Vines - 1 + ZERO; i >= 0; i--) {
                if (accT <= 0.001) break;
                float fi = float(i);
                float off = vineOff(p, fi, t, amp);
                float dd = d - off;
                float w = 2.2 + 1.3 * EwHash2To1(vec2(fi, 3.0));
                float sd = abs(dd) - w;
                if (sd < 1.0) {
                    float x = clamp(dd / w, -1.0, 1.0);
                    float bark = 0.85 + 0.15 * sin((p.x + p.y) * 0.45 + fi * 3.0 + 4.0 * x);
                    vec3 c = VineColor * (0.45 + 0.75 * sqrt(1.0 - x * x)) * bark;
                    under(accRgb, accT, c, clamp(0.5 - sd, 0.0, 1.0));
                    if (accT <= 0.001) break;
                }
                if (sd < 7.3) {
                    accT *= 1.0 - 0.32 * smoothstep(5.5, -1.0, sd - 1.8);
                }
            }
        }
    }

    vec4 acc = vec4(accRgb, 1.0 - accT);
    // guarantee: everything fades to fully transparent at the screen border
    vec2 eb2 = min(fragCoord, res - fragCoord);
    acc *= smoothstep(0.0, 3.0 + inset * 0.3, min(eb2.x, eb2.y));

    float innerMask = EwFillAA(d, 1.0);
    vec4 inner = vec4(0.0);
    if (InnerOpacity > 0.0 && innerMask > 0.001) {
        if (acc.a >= 0.9995) {
            // Frame completely covers inner rgb; only inner alpha can matter if Additive > 0
            if (Additive > 0.0) {
                float ia = InnerOpacity * innerMask;
                if (SwHaveImage && !SwTransparentImage) {
                    vec2 imgUV = clamp(p / max(2.0 * hs, vec2(1.0)) + 0.5, 0.0, 1.0);
                    ia *= texture(iChannel0, imgUV).a;
                }
                inner = vec4(0.0, 0.0, 0.0, ia);
            }
        } else {
            vec3 foliageCol = vec3(0.0);
            if (!SwHaveImage || SwTransparentImage) {
                foliageCol = renderInnerFoliage(p, t, InnerColor);
            }
            if (SwHaveImage) {
                vec2 imgUV = clamp(p / max(2.0 * hs, vec2(1.0)) + 0.5, 0.0, 1.0);
                vec4 img = texture(iChannel0, imgUV);
                if (SwTransparentImage) {
                    vec3 col = mix(foliageCol, img.rgb, img.a);
                    float ia = InnerOpacity * innerMask;
                    inner = vec4(col * ia, ia);
                } else {
                    float ia = img.a * InnerOpacity * innerMask;
                    inner = vec4(img.rgb * ia, ia);
                }
            } else {
                float ia = InnerOpacity * innerMask;
                inner = vec4(foliageCol * ia, ia);
            }
            // soft overhang shadow cast inward by the vine frame
            float borderShadow = 0.25 * smoothstep(-(inset * 0.85 + 16.0), 0.0, d);
            inner.rgb *= 1.0 - borderShadow;
        }
    }
    float a = acc.a * (1.0 - Additive);
    fragColor = vec4(acc.rgb + inner.rgb * (1.0 - acc.a), a + inner.a * (1.0 - a));
}
