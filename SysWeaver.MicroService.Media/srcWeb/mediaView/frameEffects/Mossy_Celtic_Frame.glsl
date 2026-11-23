//  media: image none
// =============================================================================
//  Mossy Celtic Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Wind | Sway of the hanging moss | 0.4 | 0 .. 2 | {0}x
//  iMouse.w = Wetness | Shine, darkening and glints of the wet stone | 0.9 | 0 .. 1

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Wind, drip and glint animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0538, 0.0563, 0.0522); // var: { "type": "col", "name": "Inner Color", "desc": "Color modifier for the inner stone background" }
const vec3 InnerMossColor = vec3(0.0561, 0.0813, 0.0336); // var: { "type": "col", "name": "Inner Moss Color", "desc": "Color of the moss on the inner stone background" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner stone background", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const vec3 StoneColor = vec3(0.45, 0.42, 0.36); // var: { "type": "col", "name": "Stone Color", "desc": "Base color of the dry stone" }
const vec3 MossColor = vec3(0.12, 0.21, 0.04); // var: { "type": "col", "name": "Moss Color", "desc": "Color of the moss patches and hanging moss" }
const float BandWidth = 0.8; // var: { "min": 0.4, "max": 0.95, "step": 0.01, "name": "Band Width", "desc": "Width of the stone band relative to the inset", "scale": 100, "unit": "%" }
const float FeatureSize = 1.6; // var: { "min": 0.5, "max": 3.0, "step": 0.05, "name": "Feature Size", "desc": "Scale of the details: knotwork, cracks, chips, moss, ivy leaves, drops", "unit": "x" }
const float Decay = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Decay", "desc": "How chipped, cracked and eroded the old stone is", "scale": 100, "unit": "%" }
const float Relief = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Relief", "desc": "Depth of the carved knotwork", "unit": "x" }
const float MossAmount = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Moss Amount", "desc": "How much moss grows and hangs", "scale": 100, "unit": "%" }
const float MossLength = 1.0; // var: { "min": 0.2, "max": 2.0, "step": 0.05, "name": "Moss Length", "desc": "Length of the hanging moss strands", "unit": "x" }
const float VineAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Vines", "desc": "How much ivy crawls over the stone", "scale": 100, "unit": "%" }
const vec3 VineColor = vec3(0.05, 0.12, 0.025); // var: { "type": "col", "name": "Vine Color", "desc": "Color of the ivy leaves" }
const float DropletAmount = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Droplets", "desc": "Number of water droplets on the stone", "scale": 100, "unit": "%" }
const float Glints = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Glints", "desc": "Strength of the twinkling wet sparkles", "unit": "x" }
const float Exposure = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define Wind mix(0.0, 2.0, iMouse.z)
#define Wetness mix(0.0, 1.0, iMouse.w)

const float TAU = 6.2831853;
const vec3 LDIR = vec3(-0.4706, 0.6471, 0.5999); // normalized key light (top left)
const vec3 HDIR = vec3(-0.263078, 0.361747, 0.894388); // normalize(LDIR + (0,0,1))
#define ZERO min(iFrame, 0)

varying vec2 vP;
flat varying vec4 vHsImg; // hs.xy, imgHs.xy
flat varying vec4 vGeo1;  // hw, fs, rad, invFs
flat varying vec4 vGeo2;  // rp, L, N, R
flat varying vec4 vCcTR;  // cc.xy, T, reach

float g_mask = 1.0, g_chip = 0.0;

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
// 4-corner vectorized Hoskins hash (identical to 4x EwHash2To1, 4x smaller IR & SIMD4)
vec4 hash4(vec2 cell) {
    vec4 xy = fract(vec4(cell, cell + 1.0) * 0.1031);
    vec4 hx = xy.xzxz;
    vec4 hy = xy.yyww;
    vec4 s = hx * (2.0 * hy + hx + 66.66) + hy * 33.33;
    return fract((hx + hy + 2.0 * s) * (hx + s));
}
float vNoise2D(vec2 p) {
    vec2 cell = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec4 h = hash4(cell);
    vec2 m = mix(h.xz, h.yw, u.x);
    return mix(m.x, m.y, u.y);
}
// same sum as EwFbm2D, loop kept rolled (ZERO bound) so each call compiles to one noise body
float fbm(vec2 p, int octaves) {
    float sum = 0.0, amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * vNoise2D(p);
        p = mat2(1.6, 1.2, -1.2, 1.6) * p;
        amp *= 0.5;
    }
    return sum;
}
// same result as EwVoronoi2D (F1, F2, cell hash), single-level rolled 3x3 loop with squared dist
vec3 voronoi(vec2 p) {
    vec2 cell = floor(p), fr = fract(p);
    float f1 = 8.0, f2 = 8.0;
    vec2 nc = cell;
    int x = -1, y = -1;
    for (int k = ZERO; k < 9; k++) {
        vec2 nb = vec2(float(x), float(y));
        vec2 diff = nb + EwHash2To2(cell + nb) - fr;
        float dd = dot(diff, diff);
        if (dd < f1) { f2 = f1; f1 = dd; nc = cell + nb; }
        else if (dd < f2) f2 = dd;
        if (++x > 1) { x = -1; y++; }
    }
    return vec3(sqrt(f1), sqrt(f2), EwHash2To1(nc));
}
float sq(float x) { return x * x; }

// position along the frame (pixels), continuous around the whole perimeter
float perimPos(vec2 p) {
    vec2 hs = vHsImg.xy;
    float rp = vGeo2.x;
    vec2 b = hs - rp;
    vec2 q = abs(p);
    float Lq = b.x + b.y + 1.5707963 * rp;
    float s;
    if (q.x > b.x && q.y > b.y) s = b.x + rp * atan(q.x - b.x, q.y - b.y);
    else if (q.x - b.x < q.y - b.y) s = q.x;
    else s = b.x + 1.5707963 * rp + (b.y - q.y);
    if (p.x >= 0.0) s = (p.y >= 0.0) ? s : 2.0 * Lq - s;
    else s = (p.y < 0.0) ? 2.0 * Lq + s : 4.0 * Lq - s;
    return s;
}

// Celtic plait band: q.x along (period 1), q.y across (turns at +-1); returns strand height 0..1
float plait(vec2 q, float px) {
    const float w = 0.17;
    float ay = abs(q.y);
    if (ay > 0.75) {
        vec2 P = vec2(q.x - (floor(q.x) + 0.5), ay - 1.0);
        vec2 u1 = vec2(0.70710678, -0.70710678), u2 = vec2(-0.70710678, -0.70710678);
        float dd = min(length(P - u1 * max(dot(P, u1), 0.0)), length(P - u2 * max(dot(P, u2), 0.0)));
        return smoothstep(w, w - px, dd) * (0.55 + 0.45 * sqrt(max(0.0, 1.0 - sq(dd / w))));
    }
    float a = q.x + q.y, b = q.x - q.y;
    float da = abs(fract(a) - 0.5) * 0.70710678;
    float db = abs(fract(b) - 0.5) * 0.70710678;
    bool topA = mod(floor(a) + floor(b), 2.0) < 0.5;
    float dt = topA ? da : db;
    float du = topA ? db : da;
    float ht = smoothstep(w, w - px, dt) * (0.55 + 0.45 * sqrt(max(0.0, 1.0 - sq(dt / w))));
    float gap = smoothstep(w + 0.05, w + 0.05 + px, dt);
    float hu = smoothstep(w, w - px, du) * gap
             * (0.5 + 0.45 * sqrt(max(0.0, 1.0 - sq(du / w))) - 0.2 * (1.0 - smoothstep(0.0, 0.5, dt)));
    return max(ht, hu);
}

// stone height field (0 = flat, ~0.75 = top of the carving); sets g_mask (silhouette) and g_chip
float stoneHeight(vec2 p, bool isCenter, bool doVo, out float voZ, out float voDf) {
    vec2 hs = vHsImg.xy;
    float hw = vGeo1.x, fs = vGeo1.y, rad = vGeo1.z, invFs = vGeo1.w;
    float L = vGeo2.y, N = vGeo2.z, R = vGeo2.w;
    vec2 cc = vCcTR.xy;
    voZ = 1.0; voDf = 1.0;

    vec2 pFs = p * invFs;
    vec2 w = vec2(vNoise2D(pFs * 0.6), vNoise2D(pFs * 0.6 + 5.2)) - 0.5
           + 0.4 * (vec2(vNoise2D(pFs * 1.7 + 2.1), vNoise2D(pFs * 1.7 + 8.4)) - 0.5);
    vec2 pw = p + w * (hw * (0.1 + 0.35 * Decay));
    float d = EwSdRoundedBox2D(pw, hs, rad);
    float v = -d / hw;
    float av = abs(v);
    vec2 lq = abs(pw) - cc;
    float r = length(lq);

    // Exact early-out when completely outside band and corner medallion
    if (isCenter && av >= 1.0 && r >= R) {
        g_mask = 0.0; g_chip = 0.0;
        return 0.0;
    }

    // Only evaluate erosion fbm where erosion can affect body / mask / medallion
    float ero = 0.0;
    if (Decay > 0.0 && ((av > 0.8 - 0.5 * Decay && av < 1.0) || r < R)) {
        float en = fbm(pFs * 1.6 + 4.0, 4);
        ero = Decay * 0.5 * smoothstep(0.35, 0.75, en);
    }
    float ae = av + ero;
    float body = smoothstep(1.0, 0.8, ae);
    float bead = sqrt(max(0.0, 1.0 - sq((av - 0.83) / 0.085)));
    float h = body * (0.22 + 0.45 * bead);
    float mask = clamp(0.5 - (ae - 0.97) * hw, 0.0, 1.0);
    if (av < 0.76 && body > 0.0) {
        vec2 q = vec2(perimPos(pw) * N / L, v / (0.6 * FeatureSize));
        h = max(h, body * (0.22 + 0.5 * plait(q, 1.0 / (0.6 * fs))));
    }
    // corner medallion with a triple spiral
    if (r < R) {
        float rr = r / R;
        float re = rr + ero * 0.4;
        float mb = sqrt(max(0.0, 1.0 - sq((rr - 0.88) / 0.08)));
        float hm = smoothstep(1.0, 0.9, re) * (0.22 + 0.5 * mb);
        float sp = cos(TAU * rr * max(1.3, 2.6 / FeatureSize) - 3.0 * atan(lq.y, lq.x));
        float spm = smoothstep(0.3, 0.6, sp) * smoothstep(0.78, 0.72, rr) * smoothstep(0.14, 0.22, rr);
        hm = max(hm, 0.22 + 0.45 * spm * (0.8 + 0.2 * sp));
        hm = max(hm, 0.3 + 0.45 * sqrt(max(0.0, 1.0 - sq(rr / 0.2))));
        h = mix(h, hm, smoothstep(1.0, 0.9, re));
        mask = max(mask, clamp(0.5 - (re - 0.97) * R, 0.0, 1.0));
    }
    if (isCenter && mask <= 0.0) {
        g_mask = 0.0; g_chip = 0.0;
        return 0.0;
    }
    // weathering: worn strands, erosion, chipped chunks, cracks, pitting
    if (h > 0.18 && Decay > 0.0) {
        float wear = vNoise2D(pFs * 1.1 + 13.0);
        h = mix(h, 0.18 + 0.4 * (h - 0.18), Decay * 0.8 * smoothstep(0.35, 0.7, wear));
    }
    if (h > 0.0) {
        float n = fbm(pFs * 2.2, 5);
        h += (n - 0.47) * (0.14 + 0.4 * Decay) * smoothstep(0.0, 0.1, h);
    }
    g_chip = 0.0;
    if (Decay > 0.0 && doVo) {
        vec3 vo = voronoi(pFs * 1.8 + 1.7);
        voZ = vo.z;
        voDf = vo.y - vo.x;
        float chipSel = step(vo.z, Decay * 0.75);
        if (chipSel > 0.0) {
            float chipLvl = 0.04 + 0.25 * fract(vo.z * 17.3) + 0.5 * vo.x;
            float hc = min(h, chipLvl);
            g_chip = smoothstep(0.0, 0.05, h - hc);
            h = hc;
        }
        float crackThr = 0.05 / FeatureSize;
        if (voDf < crackThr) {
            float crack = smoothstep(crackThr, 0.0, voDf) * step(0.4, vNoise2D(pFs * 1.2 + 9.0));
            h -= Decay * 0.55 * crack;
        }
    }
    h -= (0.05 + 0.12 * Decay) * smoothstep(0.65, 0.95, vNoise2D(pFs * 7.0 + 3.1));
    g_mask = mask * smoothstep(-0.05, 0.0, h);
    return h;
}

// y of the inner edge of the top band at x (or -1e5 if outside)
float innerTopY(float x) {
    float hw = vGeo1.x, rad = vGeo1.z;
    vec2 bi = vHsImg.xy - hw;
    float ri = max(rad - hw, 0.0);
    float bx = bi.x - ri;
    float ax = abs(x);
    if (ax > bi.x) return -1e5;
    return ax < bx ? bi.y : (bi.y - ri) + sqrt(max(ri * ri - sq(ax - bx), 0.0));
}

// ivy crawling along the band (in perimeter coordinates, seamless around the frame)
vec4 vines(vec2 p, float d, float T, float wet, out float vao) {
    vao = 1.0;
    vec4 res = vec4(0.0);
    float hw = vGeo1.x, fs = vGeo1.y;
    float Lperim = vGeo2.y;
    float v = -d / hw;
    if (VineAmount <= 0.0 || abs(v) > 1.3) return res;
    float s = perimPos(p);
    float phi = s / Lperim * TAU;
    float K = max(1.0, floor(Lperim / (fs * 6.0) + 0.5));
    float K2 = K * 2.0 + 3.0;
    float minFsHw = min(fs, 1.6 * hw);
    float nl = max(4.0, floor(Lperim / (minFsHw * 0.3) + 0.5));
    float Pl = Lperim / nl;
    for (int i = ZERO; i < 2; i++) {
        float fi = float(i);
        float pres = sin(phi * floor(K * 0.23 + 1.0 + fi) + fi * 2.3) + 0.6 * sin(phi * floor(K * 0.51 + 2.0) + 1.7 + fi * 4.1);
        float on = smoothstep(1.2 - 2.4 * VineAmount, 1.6 - 2.4 * VineAmount, pres);
        if (on <= 0.0) continue;
        float a1 = phi * K + fi * 3.1, a2 = phi * K2 + fi * 1.3;
        float vc = 0.55 * sin(a1) + 0.2 * sin(a2);
        float dvc = (0.55 * cos(a1) * K + 0.2 * cos(a2) * K2) * TAU / Lperim * hw;
        float off = (v - vc) * hw / sqrt(1.0 + dvc * dvc);
        float tw = minFsHw * 0.04 * on;
        vao = min(vao, 1.0 - 0.6 * on * smoothstep(tw * 3.5 + 1.0, tw * 0.5, abs(off - 0.4 * tw)));
        // stem
        float sm = smoothstep(tw + 0.5, tw - 0.5, abs(off));
        if (sm > 0.0) {
            float u = clamp(off / max(tw, 0.5), -1.0, 1.0);
            float bark = vNoise2D(vec2(s / (fs * 0.04), fi * 13.0));
            vec3 sc = vec3(0.055, 0.042, 0.03) * (0.5 + 0.6 * bark) * (0.35 + 0.75 * sqrt(max(0.0, 1.0 - u * u)));
            float sp1 = max(0.0, 1.0 - abs(u - 0.45) * 3.0);
            float sp2 = sp1 * sp1;
            sc += wet * 0.15 * (sp2 * sp2);
            res = over(vec4(sc, 1.0) * sm, res);
        }
        // leaves
        float idx = floor(s / Pl);
        for (int k = -1 + ZERO; k <= 1; k++) {
            float id = idx + float(k);
            float idw = mod(id, nl);
            vec4 lh = EwHash1To4(idw * 1.37 + fi * 91.0);
            if (lh.x > 0.62) continue;
            float scn = (id + 0.5) * Pl + (lh.y - 0.5) * Pl * 0.5;
            float phc = scn / Lperim * TAU;
            float vcc = 0.55 * sin(phc * K + fi * 3.1) + 0.2 * sin(phc * K2 + fi * 1.3);
            vec2 q = vec2(s - scn, (v - vcc) * hw);
            float L = minFsHw * (0.2 + 0.14 * lh.w) * on;
            if (abs(q.x) > L || abs(q.y) > L) continue;
            float side = mod(idw, 2.0) < 1.0 ? 1.0 : -1.0;
            float ang = side * (0.9 + 0.6 * lh.z) + Wind * 0.25 * sin(T * 3.0 + idw * 1.7);
            vec2 dir = vec2(cos(ang), sin(ang));
            vec2 lq = vec2(dot(q, dir), dot(q, vec2(-dir.y, dir.x)));
            float x = lq.x / max(L, 1.0);
            if (x <= 0.0 || x >= 1.0) continue;
            float wdt = L * 0.45 * pow(sin(3.14159 * pow(x, 0.7)), 0.8);
            float e = abs(lq.y) - wdt;
            float lm = smoothstep(0.5, -0.5, e);
            if (lm <= 0.0) continue;
            float yy = lq.y / max(wdt, 0.5);
            vec3 lc = VineColor * (0.45 + 0.6 * lh.z) * (0.4 + 0.6 * (1.0 - yy * yy)) * (0.7 + 0.35 * yy * side);
            lc *= mix(vec3(1.0), vec3(1.9, 1.1, 0.45), lh.w * lh.w * 0.7);
            lc = mix(lc, lc * 1.35 + 0.01, smoothstep(0.1, 0.0, abs(yy)) * 0.5);
            float vein = abs(fract(x * 5.0 - abs(yy) * 1.5) - 0.5);
            lc *= 0.85 + 0.15 * smoothstep(0.0, 0.15, vein);
            lc *= 0.55 + 0.45 * smoothstep(0.0, -2.0, e);
            float lp1 = max(0.0, 1.0 - abs(yy + 0.35 * side) * 2.5);
            float lp2 = lp1 * lp1;
            lc += wet * 0.3 * (lp2 * lp2 * lp2) * (0.3 + 0.7 * x);
            res = over(vec4(lc, 1.0) * lm, res);
        }
    }
    return res;
}
// natural rugged cliff / bedrock relief (chiseled rock crags, angular facets, jagged fractures)
float rockRelief(vec2 p, float fs, float invFs, vec2 w1, bool doF1, bool doF2, out float crevice, out float ledge, out float vein, out float rockFacet, out float grain, out float grit, out float df1Out, out float df2Out) {
    // 2-level geological domain warp (w1 hoisted across 4 samples: 158px wavelength)
    vec2 p09 = p * (invFs * (1.0 / 0.9));
    vec2 w2 = vec2(vNoise2D(p09 + 14.7), vNoise2D(p09 + 22.3)) - 0.5;
    vec2 q = p + (w1 * 0.65 + w2 * 0.2) * fs;

    // Chiseled rock crags via squared ridged multifractal (sharp rock crests and angular facets)
    float n1 = vNoise2D(q * (invFs * 0.4));
    float r1 = 1.0 - abs(n1 * 2.0 - 1.0);
    r1 = r1 * r1;

    vec2 q2 = (q + vec2(r1, -r1) * (fs * 0.3)) * mat2(0.866, -0.5, 0.5, 0.866);
    float n2 = vNoise2D(q2 * (invFs * 0.8) + 3.8);
    float r2 = 1.0 - abs(n2 * 2.0 - 1.0);
    r2 = r2 * r2;

    vec2 q3 = (q2 + vec2(-r2, r2) * (fs * 0.18)) * mat2(0.707, 0.707, -0.707, 0.707);
    float n3 = vNoise2D(q3 * (invFs * (1.0 / 0.6)) + 8.2);
    float r3 = 1.0 - abs(n3 * 2.0 - 1.0);
    r3 = r3 * r3;

    float n4 = vNoise2D(q3 * (invFs * (1.0 / 0.28)) + 14.6);
    float r4 = 1.0 - abs(n4 * 2.0 - 1.0);

    float crag = r1 * 0.48 + r2 * 0.3 + r3 * 0.16 + r4 * 0.08;
    // Geological cleavage fractures: angular, brittle rock faults via warped Voronoi cell boundaries
    float fissure1 = 0.0;
    vein = 0.0;
    rockFacet = 0.0;
    df1Out = 1.0;
    if (doF1) {
        vec2 q05 = q * (invFs * 2.0);
        vec2 jw1 = vec2(vNoise2D(q05 + 5.1), vNoise2D(q05 + 9.3)) - 0.5;
        vec3 vf1 = voronoi((q + jw1 * (fs * 0.16)) * (invFs * (1.0 / 2.4)));
        float df1 = vf1.y - vf1.x;
        df1Out = df1;
        float f1Mask = step(0.62, vf1.z);
        fissure1 = smoothstep(0.025, 0.0, df1) * f1Mask;
        vein = smoothstep(0.028, 0.0, df1) * step(0.48, vf1.z) * (1.0 - f1Mask);
        rockFacet = vf1.z;
    }

    float fissure2 = 0.0;
    df2Out = 1.0;
    if (doF2) {
        vec2 q235 = q2 * (invFs * (1.0 / 0.35));
        vec2 jw2 = vec2(vNoise2D(q235 + 15.2), vNoise2D(q235 + 21.4)) - 0.5;
        vec3 vf2 = voronoi((q2 + jw2 * (fs * 0.12)) * (invFs * (1.0 / 1.5)));
        float df2 = vf2.y - vf2.x;
        df2Out = df2;
        fissure2 = smoothstep(0.02, 0.0, df2) * step(0.72, vf2.z);
    }

    // Rock shelves and ledges where water and moss pool
    ledge = smoothstep(0.55, 0.85, r2) * smoothstep(0.35, 0.75, r1);

    // Multi-octave rock grain and pitting
    grain = fbm(p * (invFs * (1.0 / 0.35)), 3);
    grit = vNoise2D(p * (invFs * (1.0 / 0.065)));
    float pit = smoothstep(0.78, 0.96, vNoise2D(p * (invFs * (1.0 / 0.14)) + 29.1)) * 0.16;

    crevice = clamp(1.0 - (fissure1 * 0.92 + fissure2 * 0.88 + (1.0 - crag) * 0.35), 0.0, 1.0);

    float h = crag * 0.68 
            + (grain - 0.5) * 0.2 
            + (grit - 0.5) * 0.06 
            - fissure1 * 0.34 
            - fissure2 * 0.26 
            - pit 
            + vein * 0.1;
    return h;
}
// dark mossy natural rock wall inside the frame
vec3 innerStone(vec2 p, vec2 res, float fs, float invFs, float wet, float d, float hw, float T) {
    // Hoist ultra-low-freq warp w1 (158px wavelength) across the 4 samples
    vec2 p28 = p * (invFs * (1.0 / 2.8));
    vec2 w1 = vec2(vNoise2D(p28 + 2.4), vNoise2D(p28 + 8.6)) - 0.5;

    // 4 elevation samples in one rolled loop (rockRelief inlined once)
    float eps = max(1.2, fs * 0.035);
    vec2 lo = LDIR.xy * min(fs, hw) * 0.15;
    float crevice = 0.0, ledge = 0.0, vein = 0.0, rockFacet = 0.0, grain = 0.0, micro = 0.0;
    float df1_0 = 0.0, df2_0 = 0.0;
    vec4 hr = vec4(0.0);
    for (int i = ZERO; i < 4; i++) {
        vec2 o = i == 0 ? vec2(0.0) : (i == 1 ? vec2(eps, 0.0) : (i == 2 ? vec2(0.0, eps) : lo));
        bool doF1 = (i == 0) || (df1_0 < (i < 3 ? 0.12 : 0.30));
        bool doF2 = (i == 0) || (df2_0 < (i < 3 ? 0.16 : 0.45));
        float cT, lT, vT, rfT, gT, grT, d1T, d2T;
        float hv = rockRelief(p + o, fs, invFs, w1, doF1, doF2, cT, lT, vT, rfT, gT, grT, d1T, d2T);
        if (i == 0) {
            hr.w = hv;
            crevice = cT; ledge = lT; vein = vT; rockFacet = rfT; grain = gT; micro = grT;
            df1_0 = d1T; df2_0 = d2T;
        } else if (i == 1) hr.x = hv;
        else if (i == 2) hr.y = hv;
        else hr.z = hv;
    }
    float hx = hr.x, hy = hr.y, hl = hr.z, h0 = hr.w;

    float invEps22 = 22.0 / eps;
    vec3 n = normalize(vec3(-(hx - h0) * invEps22, -(hy - h0) * invEps22, 1.0));
    float selfSh = 1.0 - 0.75 * smoothstep(0.02, 0.15, (hl - h0) * 1.8);

    // Water film & damp streaks flatten out the normal and pool in recesses
    float seep = smoothstep(0.45, 0.9, vNoise2D(vec2(p.x * (invFs * (1.0 / 0.12)), p.y * (invFs * (1.0 / 1.8))))) * (1.0 - crevice);
    float film = wet * max(smoothstep(0.35, 0.18, h0), seep * 0.65);
    n = normalize(mix(n, vec3(0.0, 0.0, 1.0), film * 0.8));

    // Organic mineral tone shifts across natural rock facets
    vec3 facetColor = mix(vec3(0.88, 0.92, 0.88), vec3(1.12, 1.06, 0.94), rockFacet);

    // Base rock albedo tinted by InnerColor
    vec3 alb = InnerColor * (0.65 + 0.6 * grain) * (0.85 + 0.3 * micro) * facetColor;

    // Mineral quartz / calcite vein with iron oxide staining halo
    float oxide = (1.0 - crevice) * (0.35 + 0.65 * grain);
    alb = mix(alb, alb * vec3(1.3, 0.86, 0.62), oxide * 0.45);
    alb = mix(alb, vec3(0.78, 0.83, 0.81) * (0.85 + 0.35 * micro) * (0.8 + 0.4 * grain), vein * 0.85);

    // Weathering grime & shadow in deep clefts
    alb *= mix(vec3(0.22, 0.26, 0.19), vec3(1.0), crevice);

    // Damp darkening from water seepages
    alb *= 1.0 - 0.42 * seep * wet;
    alb *= mix(1.0, 0.38, wet);

    // Pale crustose lichen colonies on dry, exposed rock facets (exact octave early-out for > 0.68)
    if (crevice > 0.0) {
        vec2 lp = p * (invFs * (1.0 / 0.45)) + 61.3;
        float ls = 0.5 * vNoise2D(lp);
        if (ls > 0.305) {
            lp = mat2(1.6, 1.2, -1.2, 1.6) * lp;
            ls += 0.25 * vNoise2D(lp);
            if (ls > 0.555) {
                lp = mat2(1.6, 1.2, -1.2, 1.6) * lp;
                ls += 0.125 * vNoise2D(lp);
                float lichenColony = smoothstep(0.68, 0.78, ls) * crevice * (1.0 - wet * 0.55);
                vec3 lichenCol = vec3(0.55, 0.63, 0.48) * (0.85 + 0.3 * micro);
                alb = mix(alb, lichenCol, lichenColony * 0.7);
            }
        }
    }

    // Natural, organic moss colonies: growing in natural clusters and damp hollows
    float m = 0.0;
    if (MossAmount > 0.001) {
        float mn2 = vNoise2D(p * (invFs * (1.0 / 0.75)) + 11.2);
        float baseSuit = mn2 * 0.25 
                       + 0.38 * (1.0 - crevice) 
                       + 0.4 * max(n.y, 0.0) 
                       + 0.28 * ledge 
                       + 0.25 * seep;
        float mThr = mix(0.86, 0.26, MossAmount);
        float mossSuitability = baseSuit;
        if (baseSuit < mThr + 0.2 && baseSuit + 0.703125 > mThr) {
            mossSuitability += fbm(p * (invFs * (1.0 / 1.5)) + 23.7, 4) * 0.75;
        }
        m = smoothstep(mThr, mThr + 0.14, mossSuitability);

        if (m > 0.0) {
            // Multi-tone velvety moss with micro-tufts and golden rim glow
            float mossDetail = fbm(p * (invFs * (1.0 / 0.12)), 3);
            float mossTuft = vNoise2D(p * (invFs * (1.0 / 0.045)));
            vec3 mossC = InnerMossColor * (0.5 + 0.75 * mossDetail + 0.25 * mossTuft) * mix(1.0, 0.72, wet);
            // Golden growing tips around cushion perimeters
            float mossRim = smoothstep(mThr, mThr + 0.08, mossSuitability) * (1.0 - smoothstep(mThr + 0.08, mThr + 0.2, mossSuitability));
            vec3 tipBoost = mix(vec3(1.4, 1.4, 1.2), vec3(1.4, 1.3, 0.6), clamp((InnerMossColor.g - max(InnerMossColor.r, InnerMossColor.b)) * 12.0, 0.0, 1.0));
            mossC = mix(mossC, mossC * tipBoost, mossRim * 0.55);
            // Darker damp roots
            mossC *= 0.7 + 0.3 * smoothstep(0.0, 0.3, m);
            alb = mix(alb, mossC, m);
        }
    }

    // Ambient occlusion with frame recess shadow and subtle corner vignette
    float ao = mix(0.24, 1.0, crevice) * (0.75 + 0.25 * grain);
    float recessAO = smoothstep(-hw * 1.8, -hw * 0.2, d);
    float vig = smoothstep(1.35, 0.4, length(p / (0.5 * res)));
    ao *= mix(0.65, 1.0, recessAO) * mix(0.78, 1.0, vig);

    // Key diffuse lighting with cast self-shadows under overhangs
    float diff = max(dot(n, LDIR), 0.0) * selfSh;
    vec3 col = alb * (0.16 + 0.9 * diff) * ao;

    // Specular reflections for wet rock, water film, and quartz vein
    vec3 H = HDIR;
    float shin = (1.0 - m * 0.8) * (0.55 + 0.45 * micro) + vein * 0.55;
    float specPow = mix(18.0, 150.0, wet);
    float spec = pow(max(dot(n, H), 0.0), specPow) * mix(0.015, 2.2, wet) * shin * (0.3 + 0.7 * selfSh) * ao;
    vec3 specCol = mix(vec3(0.9, 0.95, 1.0), vec3(1.0, 0.98, 0.92), vein);
    col += spec * specCol * 0.75;

    // Natural twinkling wet glints on crystalline minerals along highlights
    if (spec > 0.35 && Glints > 0.0) {
        float crystal = vNoise2D(p * (invFs * (1.0 / 0.022)) + 33.1);
        if (crystal >= 0.78) {
            float g1 = max(0.0, sin(T * 2.2 + crystal * 30.0));
            float g2 = g1 * g1; float g4 = g2 * g2; float g8 = g4 * g4;
            float glintSparkle = g8 * g8;
            float glintMask = smoothstep(0.35, 0.8, spec) * (1.0 - m * 0.9);
            col += glintSparkle * glintMask * Glints * wet * vec3(1.8, 2.0, 2.2) * 1.5;
        }
    }

    return col;
}
vec3 softShoulder(vec3 x) {
    const float k = 0.8;
    vec3 overK = max(x - k, 0.0);
    return min(x, k) + (1.0 - k) * (overK / (1.0 - k + overK));
}
// apply scene lighting (key light, frame recess AO, wet sheen, exposure) to the framed image
vec3 shadeImage(vec3 srgb, vec2 p, vec2 innerHs, float d, float hw, float wet, vec3 H) {
    vec3 lin = EwSrgbToLinear(srgb);
    vec2 u = p / max(innerHs, vec2(1.0));
    // gentle directional key light across the recessed plane (top-left LDIR)
    float keyGrad = 1.0 + 0.14 * dot(u, LDIR.xy);
    // recessed ambient occlusion from the carved stone walls + subtle corner vignette
    float inDist = max(-d - hw, 0.0);
    float recessAO = mix(0.68, 1.0, smoothstep(0.0, hw * 2.0, inDist));
    float vig = mix(0.86, 1.0, smoothstep(1.45, 0.35, length(u)));
    // subtle wet surface sheen catching the top-left key light
    vec3 n = normalize(vec3(u * 0.32, 1.0));
    float sheen = pow(max(dot(n, H), 0.0), 14.0) * wet * 0.075 * recessAO;
    vec3 col = lin * keyGrad * recessAO * vig + sheen * vec3(0.92, 0.96, 1.0);
    return softShoulder(col * Exposure);
}
void vertexMain(vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vP = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float hw = max(inset * BandWidth, 3.0);
    vec2 maxHs = 0.5 * res - inset;
    vec2 hs = maxHs;
    vec2 imgHs = max(hs - hw, vec2(1.0));
    if (SwHaveImage) {
        vec2 texSize = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texSize.x / texSize.y;
        float pad = hw * (0.85 - 0.6 * Decay);
        vec2 maxImgHs = max(maxHs - pad, vec2(1.0));
        imgHs = (maxImgHs.x / maxImgHs.y > imgAspect)
              ? vec2(maxImgHs.y * imgAspect, maxImgHs.y)
              : vec2(maxImgHs.x, maxImgHs.x / imgAspect);
        hs = min(max(imgHs + pad, vec2(hw + 1.0)), maxHs);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float fs = hw * FeatureSize;
    float rp = min(max(rad, hw * 1.05), min(hs.x, hs.y));
    float L = 4.0 * ((hs.x - rp) + (hs.y - rp) + 1.5707963 * rp);
    float N = max(4.0, floor(L / (0.6 * fs) + 0.5));
    float R = min(1.15 * hw, 0.95 * inset);
    vec2 cc = (hs - rad) + rad * 0.70710678;
    float T = mod(iTime * Speed, 3600.0);
    float reach = (2.0 * MossLength + 3.6) * hw;

    vHsImg = vec4(hs, imgHs);
    vGeo1 = vec4(hw, fs, rad, 1.0 / fs);
    vGeo2 = vec4(rp, L, N, R);
    vCcTR = vec4(cc, T, reach);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 p = vP;
    vec2 hs = vHsImg.xy;
    vec2 imgHs = vHsImg.zw;
    float hw = vGeo1.x, fs = vGeo1.y, rad = vGeo1.z, invFs = vGeo1.w;
    float R = vGeo2.w;
    vec2 cc = vCcTR.xy;
    float T = vCcTR.z, reach = vCcTR.w;

    float d = EwSdRoundedBox2D(p, hs, rad);
    vec3 H = HDIR;

    float yTop = hs.y - hw;
    float rMed = length(abs(p) - cc);
    bool nearTop = p.y > yTop - reach && p.y < hs.y + hw && abs(p.x) < hs.x + hw && MossAmount > 0.0;
    bool inFrameArea = abs(d) <= 1.55 * hw || nearTop || rMed <= 1.3 * R;

    vec3 col = vec3(0.0);
    float a = 0.0;
    if (inFrameArea) {
        float wet = Wetness;
        vec4 acc = vec4(0.0); // premultiplied linear
        // ---------------- stone ----------------
        // soft drop shadow of the frame (light from the top left)
        float dS = EwSdRoundedBox2D(p + vec2(-0.1, 0.16) * hw, hs, rad);
        acc = vec4(0.0, 0.0, 0.0, 0.6 * smoothstep(1.3 * hw, 0.7 * hw, abs(dS)));
        if (abs(d) < 1.3 * hw || rMed < 1.25 * R) {
            float vao = 1.0;
            vec4 vn = vines(p, d, T, wet, vao);
            if (vn.a < 1.0) {
                // 4 height samples in one loop (stoneHeight inlined once); center first for early-out
                vec4 hs4 = vec4(0.0);
                vec2 lo = LDIR.xy * min(fs, hw) * 0.1;
                float cover = 0.0, chip = 0.0;
                bool doVoOffset = true;
                for (int i = ZERO; i < 4; i++) {
                    vec2 o = i == 0 ? vec2(0.0) : (i == 1 ? vec2(1.0, 0.0) : (i == 2 ? vec2(0.0, 1.0) : lo));
                    bool doVo = (i == 0) || doVoOffset;
                    float voZ, voDf;
                    float hv = stoneHeight(p + o, i == 0, doVo, voZ, voDf);
                    if (i == 0) {
                        hs4.w = hv;
                        cover = g_mask;
                        chip = g_chip;
                        if (cover <= 0.0) break;
                        // if center cell is unchipped and far from cell border, offsets can't chip or crack
                        doVoOffset = (voZ <= Decay * 0.75) || (voDf < 0.25);
                    } else if (i == 1) hs4.x = hv; else if (i == 2) hs4.y = hv; else hs4.z = hv;
                }
                if (cover > 0.0) {
                    float hx = hs4.x, hy = hs4.y, hl = hs4.z, h0 = hs4.w;
                    float k = hw * 0.6 * Relief;
                    vec3 n = normalize(vec3(-(hx - h0) * k, -(hy - h0) * k, 1.0));
                    float selfSh = 1.0 - 0.75 * smoothstep(0.02, 0.14, (hl - h0) * Relief);
                    // wet streaks running down, water film in the recesses
                    float stk = smoothstep(0.5, 0.8, vNoise2D(vec2(p.x * (invFs * (1.0 / 0.07)), p.y * (invFs * (1.0 / 1.8))))) * wet;
                    float film = wet * max(smoothstep(0.3, 0.18, h0), stk * 0.7);
                    n = normalize(mix(n, vec3(0.0, 0.0, 1.0), film * 0.85));

                    vec2 pFs = p * invFs;
                    float cn = fbm(pFs * 0.9 + 11.0, 3);
                    float gr = vNoise2D(pFs * 22.0);
                    vec3 alb = StoneColor * (0.6 + 0.7 * cn) * (0.85 + 0.3 * gr);
                    alb *= mix(vec3(1.0), vec3(1.08, 1.0, 0.86), smoothstep(0.4, 0.7, cn));
                    // pale lichen spots
                    float li = smoothstep(0.72, 0.8, vNoise2D(pFs * 4.0 + 21.0)) * smoothstep(0.3, 0.6, h0);
                    alb = mix(alb, vec3(0.62, 0.6, 0.45), li * 0.6);
                    alb *= mix(1.0, 0.38, wet);
                    float ao = mix(0.25, 1.0, smoothstep(0.12, 0.55, h0));
                    // dark grime in the recesses
                    alb *= mix(vec3(0.55, 0.62, 0.5), vec3(1.0), smoothstep(0.18, 0.4, h0));
                    alb = mix(alb, alb * vec3(1.2, 1.08, 0.92), chip * 0.5);
                    alb *= 1.0 - 0.4 * stk;
                    // green algae stains
                    if (Decay > 0.0) {
                        alb = mix(alb, MossColor * 0.8, 0.4 * smoothstep(0.45, 0.75, fbm(pFs * 0.8 + 2.7, 3)) * Decay);
                    }

                    // moss patches (prefer recesses and upward faces)
                    float m = 0.0;
                    vec3 mossC = vec3(0.0);
                    if (MossAmount > 0.001) {
                        float mn = fbm(pFs * 1.3 + 7.3, 4);
                        float thr = mix(0.85, 0.38, MossAmount);
                        m = smoothstep(thr, thr + 0.07, mn + 0.18 * (1.0 - ao) + 0.08 * n.y);
                        if (m > 0.0) {
                            float fine = vNoise2D(pFs * 18.0);
                            mossC = MossColor * (0.4 + 0.9 * fine) * mix(1.0, 0.75, wet);
                        }
                    }

                    float diff = max(dot(n, LDIR), 0.0) * selfSh;
                    vec3 scol = mix(alb, mossC, m) * (0.1 + 0.95 * diff) * ao;

                    float shin = (1.0 - m * 0.8) * (0.6 + 0.4 * gr);
                    float ndh = max(dot(n, H), 0.0);
                    float spec = pow(ndh, mix(18.0, 160.0, wet)) * mix(0.03, 2.4, wet) * shin * (0.3 + 0.7 * selfSh);
                    vec3 Rr = reflect(vec3(0.0, 0.0, -1.0), n);
                    float e = smoothstep(0.45, 0.85, Rr.y * 0.8 + Rr.x * 0.35 + 0.25 * sin(T * 0.23 + p.x / hw * 0.15));
                    float f1 = 1.0 - clamp(n.z, 0.0, 1.0);
                    float f2 = f1 * f1;
                    float fres = 0.04 + 0.96 * (f2 * f2 * f1);
                    vec3 refl = vec3(0.42, 0.5, 0.6) * (0.02 + 0.7 * e) * (0.03 + fres + 0.3 * film) * wet * shin;
                    scol += spec * vec3(1.0, 0.96, 0.88) + refl;

                    // twinkling glints
                    if (ndh > 0.75 && h0 > 0.25 && Glints > 0.0) {
                        vec2 gc = floor(fragCoord / max(3.0 * FeatureSize, 2.0));
                        vec3 gh = EwHash2To3(gc);
                        if (gh.x >= 0.975) {
                            float tw1 = max(0.0, sin(T * (2.0 + 3.0 * gh.y) + gh.z * 60.0));
                            float tw2 = tw1 * tw1; float tw4 = tw2 * tw2; float tw = tw4 * tw4 * tw4;
                            float gl = tw * smoothstep(0.75, 0.97, ndh) * smoothstep(0.25, 0.4, h0);
                            scol += gl * Glints * wet * shin * vec3(2.5, 2.4, 2.2);
                        }
                    }
                    // water running down the wet streaks
                    if (stk > 0.0) {
                        float fl1 = fract(p.y * (invFs * (1.0 / 0.7)) + T * 0.45 + 7.0 * EwHash1To2(floor(p.x * (invFs * (1.0 / 0.07)))).x);
                        float fl2 = fl1 * fl1; float fl4 = fl2 * fl2; float flow = fl4 * fl4 * fl2;
                        scol += stk * flow * vec3(0.6, 0.65, 0.7) * 0.6 * shin;
                    }

                    // droplets resting on the stone
                    float cs = max(fs * 0.2, 5.0);
                    vec2 cid = floor(p / cs);
                    vec3 dh = EwHash2To3(cid + 31.0);
                    if (dh.x < DropletAmount * 0.5) {
                        float dr = cs * (0.12 + 0.16 * dh.y);
                        vec2 c = (cid + 0.5) * cs + (dh.yz - 0.5) * (cs - 2.0 * dr);
                        vec2 dq = (p - c) / dr;
                        float dl = length(dq);
                        if (dl < 1.0 + 2.0 / dr) {
                            float dm = smoothstep(1.0, 1.0 - 1.5 / dr, dl);
                            vec3 nd = vec3(dq, sqrt(max(0.0, 1.0 - dl * dl)));
                            vec3 dcol = scol * 0.55 + vec3(0.25, 0.3, 0.3) * smoothstep(0.2, 0.9, dot(dq, -LDIR.xy)) * 0.6;
                            dcol += pow(max(dot(nd, H), 0.0), 120.0) * 4.0 + smoothstep(0.6, 1.0, dl) * 0.06;
                            scol = mix(scol, dcol, dm);
                        } else {
                            float sh = smoothstep(1.6, 1.0, length(p - c + LDIR.xy * dr * 0.5) / dr);
                            scol *= 1.0 - 0.3 * sh;
                        }
                    }
                    scol *= vao;
                    acc = over(vec4(scol, 1.0) * cover, acc);
                }
            }
            acc = over(vn, acc);
        }

        // ---------------- hanging moss + drips ----------------
        if (nearTop) {
            float cw = max(fs * 0.09, 2.5);
            float j0 = floor(p.x / cw);
            float thrH = 1.0 - 0.85 * MossAmount;
            float drDrip = max(fs * 0.05, 1.6);
            float minDx = -(0.3 * Wind + 1.28) * cw;
            float maxDx = (2.3 * Wind + 1.28) * cw;
            float maxDripDx = 2.0 * Wind * cw + 1.5 * drDrip;
            int kMin = max(-5, -int(ceil(max(2.3 * Wind + 2.2, 2.0 * Wind + 1.5 * drDrip / cw + 1.0) + (SwHaveImage ? ceil(0.1 * hw / cw) : 0.0))));
            int kMax = min(5, int(ceil(0.3 * Wind + 2.2)));
            vec4 mossAcc = vec4(0.0);
            float mossSh = 0.0;
            for (int k = kMin + ZERO; k <= kMax; k++) {
                float j = j0 + float(k);
                vec4 h4 = EwHash1To4(j + 17.0);
                float x0 = (j + 0.5) * cw + (h4.y - 0.5) * cw * 0.8;
                float dx0 = p.x - x0;
                bool mayStrand = dx0 >= minDx && dx0 <= maxDx;
                bool mayDrip = h4.w > 0.78 && h4.x > 0.35 && Speed > 0.0 && dx0 >= -1.5 * drDrip && dx0 <= maxDripDx;
                bool mayShad = SwHaveImage && (dx0 - 0.1 * hw >= minDx - 2.5 && dx0 - 0.1 * hw <= maxDx + 2.5);
                if (!mayStrand && !mayDrip && !mayShad) continue;
                float clump = vNoise2D(vec2(j * cw * (invFs * (1.0 / 1.6)), 4.2));
                float str = smoothstep(thrH, thrH + 0.12, clump);
                if (str <= 0.0) continue;
                float yi = innerTopY(x0);
                if (yi < -1e4 || abs(x0) > hs.x - hw - cw) continue;
                float yA = yi + hw * 0.3;
                float len = hw * MossLength * (0.35 + 1.65 * h4.x * str) + hw * 0.3;
                float depth = yA - p.y;
                bool doStrand = mayStrand && depth > 0.0 && depth < len;
                float depthS = depth - 0.16 * hw;
                bool doShad = mayShad && depthS > 0.0 && depthS < len;
                if (!doStrand && !doShad && !mayDrip) continue;
                float gust = 0.6 + 0.4 * sin(T * 0.9 - x0 * 0.004 + h4.z * 2.0);
                // moss strand
                if (doStrand) {
                    float t = depth / len;
                    float sway = Wind * cw * 2.0 * (t * sqrt(t)) * gust
                               + Wind * cw * 0.3 * t * sin(T * 4.0 + depth * invFs * 3.0 + h4.w * 6.0)
                               + cw * 0.7 * t * sin(depth * (invFs * (1.0 / 0.22)) + h4.y * 6.0);
                    float dx = dx0 - sway;
                    float thBase = cw * 0.5 * (1.0 - 0.6 * t);
                    if (abs(dx) < thBase * 1.15) {
                        float fz = vNoise2D(vec2(depth / (cw * 0.9), j * 3.1));
                        float th = thBase * (0.35 + 0.8 * fz);
                        float mm = smoothstep(th, th - 1.0, abs(dx)) * smoothstep(len, len - cw * 2.0, depth);
                        if (mm > 0.0) {
                            float u = clamp(dx / max(th, 0.5), -1.0, 1.0);
                            float streak = vNoise2D(vec2(dx * 0.8, depth * 0.2 + j * 7.0));
                            vec3 mc = MossColor * (0.35 + 0.8 * streak) * (0.35 + 0.75 * (1.0 - u * u)) * (0.55 + 0.6 * (0.5 - 0.5 * u));
                            mc *= (0.45 + 0.4 * h4.z) * mix(vec3(1.0), vec3(1.25, 1.1, 0.7), h4.y * 0.6);
                            float sp1 = max(0.0, 1.0 - abs(u + 0.45) * 2.0);
                            float sp2 = sp1 * sp1;
                            mc += (sp2 * sp2 * sp2) * 0.25 * wet * streak;
                            mossAcc = over(vec4(mc, 1.0) * mm, mossAcc);
                        }
                    }
                }
                if (doShad) {
                    float ts = depthS / len;
                    float sways = Wind * cw * 2.0 * (ts * sqrt(ts)) * gust
                                + Wind * cw * 0.3 * ts * sin(T * 4.0 + depthS * invFs * 3.0 + h4.w * 6.0)
                                + cw * 0.7 * ts * sin(depthS * (invFs * (1.0 / 0.22)) + h4.y * 6.0);
                    float dxs = (dx0 - 0.1 * hw) - sways;
                    float ths = cw * 0.5 * (1.0 - 0.6 * ts) * 0.85;
                    float sm = smoothstep(ths + 2.5, ths * 0.2, abs(dxs)) * smoothstep(len, len - cw * 2.0, depthS);
                    mossSh = max(mossSh, sm * 0.38);
                }
                // water drip from the strand tip
                if (mayDrip) {
                    float P = 2.5 + 4.0 * h4.z;
                    float ph = fract(T / P + h4.x);
                    float tipSway = Wind * cw * 2.0 * gust;
                    float tipY = yA - len;
                    vec2 c; float rd; float stretch = 1.0; float fade = 1.0;
                    if (ph < 0.75) {
                        rd = drDrip * mix(0.35, 1.0, ph / 0.75);
                        c = vec2(x0 + tipSway, tipY - rd * 0.6);
                    } else {
                        float tf = (ph - 0.75) * P;
                        float fall = 0.5 * hw * 30.0 * tf * tf;
                        rd = drDrip;
                        stretch = 1.0 + min(hw * 30.0 * tf / (hw * 12.0), 1.2);
                        c = vec2(x0 + tipSway, tipY - rd * 0.6 - fall);
                        fade = smoothstep(3.2 * hw, 2.2 * hw, fall);
                    }
                    vec2 dq = (p - c) / vec2(rd, rd * stretch);
                    float dl2 = dot(dq, dq);
                    if (dl2 < 2.25 && fade > 0.0) {
                        float dl = sqrt(dl2);
                        float dm = smoothstep(1.0, 1.0 - 1.5 / rd, dl) * fade;
                        vec3 nd = vec3(dq, sqrt(max(0.0, 1.0 - dl2)));
                        vec3 dcol = vec3(0.05, 0.07, 0.08) + vec3(0.25, 0.32, 0.35) * smoothstep(0.1, 0.9, -dq.y);
                        dcol += pow(max(dot(nd, H), 0.0), 60.0) * 3.0;
                        mossAcc = over(vec4(dcol, 0.9) * dm, mossAcc);
                    }
                }
            }
            // moss mat draped over the inner edge (hides the strand roots)
            float yiP = innerTopY(p.x);
            if (yiP > -1e4 && abs(p.x) < hs.x - hw) {
                float strM = smoothstep(thrH - 0.15, thrH + 0.15, vNoise2D(vec2(p.x * (invFs * (1.0 / 1.6)), 4.2)));
                float yc = yiP + hw * 0.3;
                float dy = (p.y - yc) / (hw * 0.7 * max(strM, 0.02));
                if (strM >= 0.02 && abs(dy) < 1.7) {
                    float lump = fbm(p * (invFs * (1.0 / 0.16)) + 2.0, 4);
                    float fe = vNoise2D(vec2(p.x * (invFs * (1.0 / 0.03)), p.y * (invFs * (1.0 / 0.08))));
                    float field = lump * 2.6 - 0.95 - abs(dy) + 0.15 * fe;
                    float mat = clamp(field * hw * 0.2, 0.0, 1.0);
                    if (mat > 0.0) {
                        float fm = vNoise2D(p * (invFs * 20.0));
                        float lt = fbm((p + LDIR.xy * (fs * 0.04)) * (invFs * (1.0 / 0.16)) + 2.0, 4) - lump;
                        vec3 mc = MossColor * 0.7 * (0.3 + 0.7 * fm) * (0.3 + 0.9 * clamp(field * 1.5, 0.0, 1.0)) * (0.7 + 0.5 * fe);
                        mc *= clamp(0.85 - lt * 6.0, 0.3, 1.2);
                        mc += smoothstep(0.8, 0.95, fm) * 0.1 * wet;
                        mossAcc = over(vec4(mc, 1.0) * (mat * (2.0 - mat)), mossAcc);
                    }
                }
                if (SwHaveImage && strM > 0.02) {
                    float dyS = (p.y + 0.16 * hw - yc) / (hw * 0.7 * max(strM, 0.02));
                    float matSh = smoothstep(1.35, 0.35, abs(dyS)) * strM * 0.45;
                    mossSh = max(mossSh, matSh);
                }
            }
            if (SwHaveImage && mossSh > 0.0) acc = over(vec4(0.0, 0.0, 0.0, mossSh), acc);
            acc = over(mossAcc, acc);
        }

        // ---------------- frame output ----------------
        a = acc.a * smoothstep(0.5, 10.0, min(0.5 * res.x - abs(p.x), 0.5 * res.y - abs(p.y)));
        col = acc.rgb / max(acc.a, 1e-4);
        col = EwLinearToSrgbFast(EwTonemapAces(col * Exposure * 1.3));
        float dz = EwDither(fragCoord, iTime, 2.0);
        if (a > 0.0 && a < 1.0) a = clamp(a + dz, 0.0, 1.0);
        col = min(max(col + dz, 0.0) * a, vec3(a));
        a *= 1.0 - Additive;
    }

    if (a >= 1.0) {
        fragColor = vec4(col, 1.0);
        return;
    }

    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = smoothstep(0.5, -0.5, SwHaveImage ? d : (d - hw * 0.95));
    vec4 innerLayer = vec4(0.0);
    if (inner > 0.0 && InnerOpacity > 0.0) {
        vec3 dispLin = vec3(0.0);
        float imgAlpha = 1.0;
        if (!SwHaveImage || SwTransparentImage) {
            vec3 icLinear = innerStone(p, res, fs, invFs, Wetness, d, hw, T);
            dispLin = EwTonemapAces(icLinear * Exposure * 1.3);
        }
        if (SwHaveImage) {
            vec2 imgUv = p / (2.0 * imgHs) + 0.5;
            vec4 imgTex = texture(iChannel0, imgUv);
            vec3 imgDisp = shadeImage(imgTex.rgb, p, imgHs, d, hw, Wetness, H);
            if (SwTransparentImage) {
                dispLin = mix(dispLin, imgDisp, imgTex.a);
            } else {
                dispLin = imgDisp;
                imgAlpha = imgTex.a;
            }
        }
        vec3 icSrgb = EwLinearToSrgbFast(dispLin);
        float dz = EwDither(fragCoord, iTime, 2.0);
        icSrgb = clamp(icSrgb + dz, 0.0, 1.0);
        float aIn = InnerOpacity * inner * imgAlpha;
        innerLayer = vec4(icSrgb * aIn, aIn);
    }
    vec4 base = over(innerLayer, bg);
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
