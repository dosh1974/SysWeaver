//  media: image none
// =============================================================================
//  Ornate Gold Frame (baroque, static, with glints, flares and a sweeping sheen)
// =============================================================================
//  iMouse.x = Inset | Half width of the frame (center line distance from the screen edges) | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px

const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.5190625, 0.75625, 0.7473554688); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.62, 0.48, 0.32); // var: { "type": "col", "name": "Paper Tint", "desc": "Color modifier for the inner wrinkled paper" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the paper inside the frame", "scale": 100, "unit": "%" }
const float WrinkleDepth = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.02, "name": "Wrinkle Depth", "desc": "Depth of wrinkles and folds in the paper", "unit": "x" }
const vec3 GoldColor = vec3(1.0, 0.72, 0.32); // var: { "type": "col", "name": "Gold Color", "desc": "Base color of the gilding" }
const float Ornament = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.01, "name": "Ornament Depth", "desc": "Relief depth of the carvings", "unit": "x" }
const float FeatureSize = 2.0; // var: { "min": 0.5, "max": 4.0, "step": 0.05, "name": "Feature Size", "desc": "Size of the ornaments along the frame (larger = less detail)", "unit": "x" }
const float Roughness = 0.5; // var: { "min": 0.05, "max": 1.0, "step": 0.01, "name": "Roughness", "desc": "Surface roughness of the gold (low = mirror polish)" }
const float MicroFacets = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Micro Facets", "desc": "Strength of the noisy micro facet surface", "unit": "x" }
const float GlintAmount = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Glints", "desc": "Strength of the small twinkling glints", "unit": "x" }
const float GlimmerAmount = 0.5; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Glimmer", "desc": "Strength of the dense pixel-sized glimmer", "unit": "x" }
const float FlareAmount = 2.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Flares", "desc": "Strength of the larger random star flares", "unit": "x" }
const float SweepInterval = 10.0; // var: { "min": 2.0, "max": 30.0, "step": 0.5, "name": "Sweep Interval", "desc": "Time between highlight sweeps", "unit": "s" }
const float SweepGlow = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Sweep Glow", "desc": "Brightness of the sweeping highlight", "unit": "x" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define ZERO min(iFrame, 0)

varying vec3 vPDiag;           // xy = p, z = (diag - sp) * 5.0
flat varying vec4 vFrameGeom;  // hs.x, hs.y, rad, hw
flat varying vec4 vImgGeom;    // imgHs.x, imgHs.y, rabbet, invMinRes
flat varying vec4 vPerimFreq;  // n1/P, nb/P, ns/P, ne/P
flat varying vec4 vPerimBox;   // b.x, b.y, arc, Q
flat varying vec4 vFl0;        // flare 0..4: c.x, c.y, isz*cos(ang), isz*sin(ang)
flat varying vec4 vFl1;
flat varying vec4 vFl2;
flat varying vec4 vFl3;
flat varying vec4 vFl4;
flat varying vec4 vFlEnv03;    // env * FlareAmount for flares 0..3
flat varying vec4 vTimeFl4;    // nzOff.x, nzOff.y, env4 * FlareAmount, maxEnv

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float hw = inset * 0.62;
    vec2 hs = 0.5 * res - inset;
    float rabbet = max(hw * 0.88 - 1.5, 0.0);
    vec2 imgHs = max(hs - rabbet, vec2(1.0));
    if (SwHaveImage) {
        vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texRes.x / texRes.y;
        float availAspect = imgHs.x / imgHs.y;
        imgHs = (imgAspect > availAspect)
            ? vec2(imgHs.x, imgHs.x / imgAspect)
            : vec2(imgHs.y * imgAspect, imgHs.y);
        imgHs = max(imgHs, vec2(1.0));
        hs = imgHs + rabbet;
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float t = iTime;
    float cyc = mod(t, SweepInterval);
    float diag = (p.x + p.y * 0.6) / (0.5 * (res.x + res.y * 0.6));
    float sp = cyc * 1.2 - 1.6;
    vPDiag = vec3(p, (diag - sp) * 5.0);
    vFrameGeom = vec4(hs, rad, hw);
    vImgGeom = vec4(imgHs, rabbet, 1.0 / min(res.x, res.y));

    vec2 b = hs - rad;
    float arc = 1.5707963 * rad;
    float Q = b.x + arc + b.y;
    float P = 4.0 * Q;
    float invP = 1.0 / P;
    float hwF = hw * FeatureSize;
    float n1 = max(4.0 * floor(P / (hwF * 2.4)), 8.0);
    float nb = max(floor(P / (hwF * 0.22)), 4.0);
    float ns = max(floor(P / (hwF * 1.6)), 2.0);
    float ne = max(floor(P / (hwF * 0.35)), 4.0);
    vPerimFreq = vec4(n1, nb, ns, ne) * invP;
    vPerimBox = vec4(b, arc, Q);

    // Precompute the 5 star flares once per frame (no varying arrays)
    float invRad = 1.0 / max(rad, 1e-3);
    float cscale = 0.8 * hw / max(min(hs.x, hs.y), 1.0);
    vec4 fl0 = vec4(0.0), fl1 = vec4(0.0), fl2 = vec4(0.0), fl3 = vec4(0.0), fl4 = vec4(0.0);
    vec4 fenv03 = vec4(0.0);
    float fenv4 = 0.0;
    float maxEnv = 0.0;
    if (FlareAmount > 0.0) {
        for (int i = ZERO; i < 5; i++) {
            float fi = float(i);
            float per = 2.3 + fi * 0.7;
            float tt = t / per + fi * 0.37;
            float slot = floor(tt);
            float ph = fract(tt);
            float env = sin(clamp(ph * 5.0, 0.0, 1.0) * 3.14159);
            env *= env;
            if (env <= 0.0) continue;
            vec3 r = EwHash2To3(vec2(slot, fi * 7.1));
            float s = r.x * P;
            float qs = mod(s, Q);
            int qd = int(floor(s / Q));
            vec2 c;
            if (qs < b.x) c = vec2(qs, hs.y);
            else if (qs < b.x + arc) { float a = (qs - b.x) * invRad; c = b + rad * vec2(sin(a), cos(a)); }
            else c = vec2(hs.x, b.y - (qs - b.x - arc));
            if (qd == 1) c = vec2(c.x, -c.y);
            else if (qd == 2) c = -c;
            else if (qd == 3) c = vec2(-c.x, c.y);
            c *= 1.0 + (r.y - 0.5) * cscale;
            float isz = 1.0 / (hw * (0.8 + 1.2 * r.z));
            float ang = 0.4 + ph * 0.6;
            vec4 fv = vec4(c, isz * cos(ang), isz * sin(ang));
            float e = env * FlareAmount;
            maxEnv = max(maxEnv, e);
            if (i == 0)      { fl0 = fv; fenv03.x = e; }
            else if (i == 1) { fl1 = fv; fenv03.y = e; }
            else if (i == 2) { fl2 = fv; fenv03.z = e; }
            else if (i == 3) { fl3 = fv; fenv03.w = e; }
            else             { fl4 = fv; fenv4 = e; }
        }
    }
    vFl0 = fl0; vFl1 = fl1; vFl2 = fl2; vFl3 = fl3; vFl4 = fl4;
    vFlEnv03 = fenv03;
    vTimeFl4 = vec4(mod(t * 0.07, 100.0), mod(t * 0.05, 100.0), fenv4, maxEnv);
}

// vectorized 2D value noise matching EwValueNoise2D
float vnoise2D(vec2 p)
{
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 b = f * f * (3.0 - 2.0 * f);
    vec2 fx = fract(vec2(c.x, c.x + 1.0) * 0.1031);
    vec2 fy = fract(vec2(c.y, c.y + 1.0) * 0.1031);
    vec4 x = fx.xyxy;
    vec4 y = fy.xxyy;
    vec4 d = x * (2.0 * y + x + 66.66) + y * 33.33;
    vec4 h = fract((x + y + 2.0 * d) * (x + d));
    vec2 m = mix(h.xz, h.yw, b.x);
    return mix(m.x, m.y, b.y);
}

// arc length position along a rounded box outline using precomputed b, arc, Q
float perimPos(vec2 p, vec2 b, float arc, float Q, float r)
{
    vec2 a = abs(p);
    float u;
    if (a.x < b.x) u = a.x;
    else if (a.y < b.y) u = b.x + arc + (b.y - a.y);
    else u = b.x + atan(a.x - b.x, a.y - b.y) * max(r, 1e-3);
    if (p.x >= 0.0) return p.y >= 0.0 ? u : 2.0 * Q - u;
    return p.y < 0.0 ? 2.0 * Q + u : 4.0 * Q - u;
}

// silhouette-warped frame coordinate v (-1 inner .. 1 outer) and perimeter arc s
void frameVS(vec2 p, float d, out float v, out float s)
{
    v = d / vFrameGeom.w;
    s = perimPos(p, vPerimBox.xy, vPerimBox.z, vPerimBox.w, vFrameGeom.z);
    float s1 = s * vPerimFreq.x;
    float u2 = fract(s1 * 0.25) * 2.0 - 1.0;
    float sx = u2 * 2.5;
    float sx2 = 1.0 - sx * sx;
    float shell = sx2 > 0.0 ? sqrt(sx2) * (0.85 + 0.15 * abs(sin(u2 * 23.0))) : 0.0;
    if (v > 0.0) {
        float u1 = fract(s1) * 2.0 - 1.0;
        float scal = sqrt(max(1.0 - u1 * u1, 0.0));
        v /= (0.6 + 0.16 * scal + 0.28 * shell);
    } else {
        float u3 = fract(s1 * 2.0) * 2.0 - 1.0;
        v /= (0.88 + 0.08 * sqrt(max(1.0 - u3 * u3, 0.0)) + 0.06 * shell);
    }
}

// frame relief height from warped coordinate v and perimeter arc s
float frameH(float v, float s)
{
    float r1 = (v - 0.78) * 9.0;
    float r2 = (v - 0.55) * 7.0;
    float r3 = (v + 0.9) * 14.0;
    float h = 0.55 * smoothstep(1.02, 0.85, v)
            + 0.25 * exp(-r1 * r1)
            - 0.18 * exp(-r2 * r2)
            + 0.30 * smoothstep(0.45, 0.25, v) * smoothstep(-0.65, -0.45, v)
            - 0.25 * smoothstep(-0.70, -0.85, v)
            + 0.12 * exp(-r3 * r3);
    if (Ornament > 0.0) {
        if (abs(r1) < 0.75) {
            float bu = fract(s * vPerimFreq.y) - 0.5;
            float bead = length(vec2(bu * 1.6, r1));
            h += 0.10 * Ornament * smoothstep(0.75, 0.0, bead);
        }
        float band = smoothstep(0.5, 0.35, abs(v + 0.1));
        if (band > 0.0) {
            float sp = s * vPerimFreq.z;
            float su = fract(sp);
            float cell = floor(sp);
            float side = mod(cell, 2.0) * 2.0 - 1.0;
            vec2 q = vec2((su - 0.5) * 1.6, (v + 0.1) * 1.6 * side);
            vec2 d1 = q - vec2(-0.25, 0.08);
            float lr1 = length(d1);
            float lr2 = length(q - vec2(0.3, -0.12));
            float ang = atan(d1.y, d1.x);
            float spiral = abs(fract(lr1 * 14.0 - ang * 0.15915494) - 0.5);
            float sc = smoothstep(0.35, 0.05, spiral) * smoothstep(0.42, 0.1, lr1);
            float c2 = smoothstep(0.06, 0.0, abs(lr2 - 0.22)) * step(q.y, 0.05);
            float leaf = smoothstep(0.1, 0.0, abs(q.y + 0.35 * sin(q.x * 6.0 + 1.0))) * smoothstep(0.6, 0.2, abs(q.x));
            float leafV = 0.5 + 0.5 * sin(q.x * 30.0);
            h += Ornament * band * (0.22 * sc + 0.2 * c2 + 0.12 * leaf * leafV);
        }
        float ve = v + 0.62;
        if (abs(ve) < 0.15) {
            float eu = fract(s * vPerimFreq.w) - 0.5;
            float egg = length(vec2(eu * 1.2, ve * 6.0));
            h += 0.10 * Ornament * smoothstep(0.6, 0.1, egg)
               - 0.05 * Ornament * smoothstep(0.05, 0.0, abs(eu) - 0.42) * smoothstep(0.15, 0.0, abs(ve));
        }
    }
    return h;
}

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// 2D simplex noise returning vec3(value, d/dx, d/dy), bit-exact value to EwSimplexNoise2D
vec3 simplexNoise2DGrad(vec2 p)
{
    const float K1 = 0.366025404;
    const float K2 = 0.211324865;
    vec2 cell = floor(p + (p.x + p.y) * K1);
    vec2 o0 = p - cell + (cell.x + cell.y) * K2;
    float lower = step(o0.y, o0.x);
    vec2 cs = vec2(lower, 1.0 - lower);
    vec2 o1 = o0 - cs + K2;
    vec2 o2 = o0 - 1.0 + 2.0 * K2;
    vec3 k = max(0.5 - vec3(dot(o0, o0), dot(o1, o1), dot(o2, o2)), 0.0);
    vec3 k2 = k * k;
    vec3 k3 = k2 * k;
    vec3 k4 = k2 * k2;
    vec2 g0 = EwHash2To2(cell) * 2.0 - 1.0;
    vec2 g1 = EwHash2To2(cell + cs) * 2.0 - 1.0;
    vec2 g2 = EwHash2To2(cell + 1.0) * 2.0 - 1.0;
    vec3 gd = vec3(dot(o0, g0), dot(o1, g1), dot(o2, g2));
    float val = dot(k4, gd) * 70.0;
    vec2 grad = (k4.x * g0 - 8.0 * k3.x * gd.x * o0
               + k4.y * g1 - 8.0 * k3.y * gd.y * o1
               + k4.z * g2 - 8.0 * k3.z * gd.z * o2) * 70.0;
    return vec3(val, grad);
}

// Voronoi F2-F1 with analytic gradient, vec3 row hashing, and exact row-bound pruning
vec3 voronoiF2F1Grad(vec2 q, bool needId, out float cellId)
{
    vec2 cell = floor(q);
    vec2 frac = fract(q);
    vec3 px1 = fract((cell.x + vec3(-1.0, 0.0, 1.0)) * 0.1031);
    vec3 px3 = fract((cell.x + vec3(-1.0, 0.0, 1.0)) * 0.0973);
    float sy = frac.y < 0.5 ? -1.0 : 1.0;
    float dNear = min(frac.y, 1.0 - frac.y);
    float dFar = max(frac.y, 1.0 - frac.y);
    float d1 = 8.0, d2 = 8.0;
    vec2 r1 = vec2(0.0), r2 = vec2(0.0);
    vec2 nearCell = cell;
    for (int ri = ZERO; ri < 3; ri++) {
        float minDy = (ri == 0) ? 0.0 : (ri == 1) ? dNear : dFar;
        if (minDy * minDy >= d2) break;
        float fy = (ri == 0) ? 0.0 : (ri == 1) ? sy : -sy;
        float py = fract((cell.y + fy) * 0.1030);
        vec3 dd = px1 * (py + 33.33) + py * (px3 + 33.33) + px3 * (px1 + 33.33);
        vec3 hx = fract((px1 + py + 2.0 * dd) * (px3 + dd));
        vec3 hy = fract((px1 + px3 + 2.0 * dd) * (py + dd));
        vec3 rx = vec3(-1.0, 0.0, 1.0) + hx - frac.x;
        vec3 ry = vec3(fy) + hy - frac.y;
        vec3 dists = rx * rx + ry * ry;
        if (min(dists.x, min(dists.y, dists.z)) < d2) {
            if (dists.x < d1) { d2 = d1; r2 = r1; d1 = dists.x; r1 = vec2(rx.x, ry.x); nearCell = cell + vec2(-1.0, fy); }
            else if (dists.x < d2) { d2 = dists.x; r2 = vec2(rx.x, ry.x); }
            if (dists.y < d1) { d2 = d1; r2 = r1; d1 = dists.y; r1 = vec2(rx.y, ry.y); nearCell = cell + vec2(0.0, fy); }
            else if (dists.y < d2) { d2 = dists.y; r2 = vec2(rx.y, ry.y); }
            if (dists.z < d1) { d2 = d1; r2 = r1; d1 = dists.z; r1 = vec2(rx.z, ry.z); nearCell = cell + vec2(1.0, fy); }
            else if (dists.z < d2) { d2 = dists.z; r2 = vec2(rx.z, ry.z); }
        }
    }
    float inv1 = inversesqrt(max(d1, 1e-10));
    float inv2 = inversesqrt(max(d2, 1e-10));
    cellId = needId ? EwHash2To1(nearCell) : 1.0;
    return vec3(d2 * inv2 - d1 * inv1, r1 * inv1 - r2 * inv2);
}

// authentic crumpled paper heightfield + analytic gradient (single pass)
float paperHeightAndGrad(vec2 uv, out vec2 dh)
{
    vec2 p0 = uv * 1.05;
    vec3 wx = vec3(0.0), wy = vec3(0.0);
    for (int s = ZERO; s < 2; s++) {
        vec3 sn = simplexNoise2DGrad(p0 * 0.7 + float(s) * 3.7);
        if (s == 0) wx = sn; else wy = sn;
    }
    vec2 p = p0 + vec2(wx.x, wy.x) * 0.08;
    vec2 dp_dx = vec2(1.0 + wx.y * 0.056, wy.y * 0.056) * 1.05;
    vec2 dp_dy = vec2(wx.z * 0.056, 1.0 + wy.z * 0.056) * 1.05;

    float ax = p.x * 1.1 + 0.4;
    float ay = p.y * 1.3 - 0.3;
    float h = 0.32 * sin(ax) + 0.26 * cos(ay);
    vec2 gradP = vec2(0.352 * cos(ax), -0.338 * sin(ay));

    for (int k = ZERO; k < 4; k++) {
        float sc;
        vec2 off;
        float thresh, wPos, wNeg;
        if (k == 0)      { sc = 0.65; off = vec2(1.3, 2.7); thresh = 0.48; wPos = 1.60;  wNeg = -1.472;  }
        else if (k == 1) { sc = 1.45; off = vec2(5.1, 8.4); thresh = 0.50; wPos = 0.85;  wNeg = -0.7225; }
        else if (k == 2) { sc = 3.2;  off = vec2(3.7, 1.9); thresh = 0.52; wPos = 0.378; wNeg = -0.336;  }
        else             { sc = 7.0;  off = vec2(7.2, 4.3); thresh = -1.0; wPos = 0.20;  wNeg = 0.20;    }
        float cid;
        vec3 vg = voronoiF2F1Grad(p * sc + off, k < 3, cid);
        float w = cid > thresh ? wPos : wNeg;
        h += vg.x * w;
        gradP += vg.yz * (w * sc);
    }

    const vec2 fd1 = vec2(0.4763, 0.275);
    const vec2 fd2 = vec2(-0.3575, 0.54275);
    float u1 = fract(dot(p, fd1) + 0.25) - 0.5;
    float u2 = fract(dot(p, fd2) + 0.60) - 0.5;
    h += (abs(u1) * 2.0 - 0.5) * 0.55 - (abs(u2) * 2.0 - 0.5) * 0.48;
    gradP += (u1 >= 0.0 ? 1.10 : -1.10) * fd1 - (u2 >= 0.0 ? 0.96 : -0.96) * fd2;

    dh = vec2(dot(gradP, dp_dx), dot(gradP, dp_dy));
    return h;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = vPDiag.xy;
    vec2 hs = vFrameGeom.xy;
    float rad = vFrameGeom.z;
    float hw = vFrameGeom.w;
    vec2 imgHs = vImgGeom.xy;
    float rabbet = vImgGeom.z;
    float invMinRes = vImgGeom.w;

    vec4 bg = vec4(Background * Opacity, Opacity);
    float d0 = EwSdRoundedBox2D(p, hs, rad);
    vec4 acc = vec4(0.0);
    float t = iTime;
    float inner = d0 < 0.0 ? 1.0 : 0.0;
    float sw = exp(-vPDiag.z * vPDiag.z) * SweepGlow;

    const vec3 L0 = vec3(-0.4472136, 0.5366563, 0.7155418);
    const vec3 L1 = vec3( 0.6991158, -0.2996211, 0.6491790);
    const vec3 H0 = vec3(-0.2414842, 0.2897810, 0.9261614);
    const vec3 H1 = vec3( 0.3849830, -0.1649927, 0.9080643);
    const vec3 FACING_DIR = vec3(0.4574957, 0.4574957, 0.7624929);

    if (d0 > -hw * 1.1 - 14.0 && d0 < hw * 1.1 + 10.0) {
        float pv = 1.0 / hw;
        float v = 0.0;
        float frameA = 0.0;
        float h = 0.0, hx = 0.0, hy = 0.0;
        for (int i = ZERO; i < 3; i++) {
            vec2 off = (i == 1) ? vec2(1.0, 0.0) : ((i == 2) ? vec2(0.0, 1.0) : vec2(0.0));
            float di = (i == 0) ? d0 : EwSdRoundedBox2D(p + off, hs, rad);
            float tv, ts;
            frameVS(p + off, di, tv, ts);
            if (i == 0) {
                v = tv;
                inner = smoothstep(0.0, -0.5, v);
                frameA = smoothstep(-1.0 - pv, -1.0 + pv, v) * smoothstep(1.0 + pv, 1.0 - pv, v);
                if (frameA <= 0.0) break;
            }
            float ch = frameH(tv, ts);
            if (i == 0) h = ch;
            else if (i == 1) hx = ch;
            else hy = ch;
        }
        if (frameA > 0.0) {
            float k = hw * 0.12;
            vec3 n = normalize(vec3(-(hx - h) * k, -(hy - h) * k, 1.0));
            // Single non-unrolled loop for all frame value-noise samples (microfacets + roughness + glimmer patch)
            vec2 mq = fragCoord * (1.0 / 3.0);
            vec2 mq2 = mq * 0.25;
            vec2 mn = vec2(0.0), mn2 = vec2(0.0);
            float roughN = 0.0, nz = 0.0;
            int nCnt = (GlimmerAmount > 0.0) ? 6 : 5;
            for (int mi = ZERO; mi < nCnt; mi++) {
                if (MicroFacets <= 0.0 && mi < 4) continue;
                vec2 np = (mi == 0) ? mq
                        : (mi == 1) ? mq + 17.3
                        : (mi == 2) ? mq2 + 5.1
                        : (mi == 3) ? mq2 + 41.7
                        : (mi == 4) ? fragCoord * 0.025
                        : fragCoord * (1.0 / 60.0) + vTimeFl4.xy;
                float nv = vnoise2D(np);
                if (mi == 0) mn.x = nv;
                else if (mi == 1) mn.y = nv;
                else if (mi == 2) mn2.x = nv;
                else if (mi == 3) mn2.y = nv;
                else if (mi == 4) roughN = nv;
                else nz = nv;
            }
            if (MicroFacets > 0.0) {
                mn = mn * 2.0 - 1.0;
                mn2 = mn2 * 2.0 - 1.0;
                n = normalize(n + vec3(mn * 0.06 + mn2 * 0.05, 0.0) * MicroFacets);
            }
            float rough = clamp(Roughness * (0.75 + 0.5 * roughN), 0.04, 1.0);
            vec3 gold = GoldColor;
            float NV = max(n.z, 1e-3);
            float cav = clamp(0.4 + h * 1.2, 0.0, 1.0);
            float r2 = rough * rough;
            float a2 = r2 * r2;
            float r1 = rough + 1.0;
            float kk = r1 * r1 * 0.125;
            float Gv = NV / (NV * (1.0 - kk) + kk);

            vec3 col = vec3(0.0);
            float spec = 0.0, dif = 0.0;
            for (int li = ZERO; li < 2; li++) {
                vec3 L = (li == 0) ? L0 : L1;
                vec3 H = (li == 0) ? H0 : H1;
                float Li = (li == 0) ? 2.6 : 0.8;
                float NL = max(dot(n, L), 0.0);
                float NH = max(dot(n, H), 0.0);
                float dd = NH * NH * (a2 - 1.0) + 1.0;
                float D = a2 / (3.14159 * dd * dd + 1e-5);
                float G = (NL / (NL * (1.0 - kk) + kk)) * Gv;
                float hv = 1.0 - H.z;
                float hv2 = hv * hv;
                vec3 F = gold + (1.0 - gold) * (hv2 * hv2 * hv);
                col += (D * G * F / (4.0 * NL * NV + 1e-3)) * (NL * Li);
                if (li == 0) { spec = clamp(D * NL * 0.05, 0.0, 1.0); dif = NL; }
            }

            float envY = n.y * (1.0 - 0.5 * rough);
            float env = smoothstep(-0.6, 0.8, envY + 0.15 * n.x);
            float nv1 = 1.0 - NV;
            float nv2 = nv1 * nv1;
            vec3 Fe = gold + (1.0 - gold) * (nv2 * nv2 * nv1);
            col = (col + Fe * (0.06 + 0.32 * env)) * cav;

            if (SweepGlow > 0.0) {
                float fd = max(dot(n, FACING_DIR), 0.0);
                float fd2 = fd * fd;
                float facing = fd2 * fd2 * fd2;
                col += mix(gold, vec3(1.0, 0.95, 0.8), 0.5) * sw * (0.35 + 1.3 * facing);
            }

            if (GlintAmount > 0.0) {
                const float gs = 7.0;
                vec2 gc = floor(fragCoord * (1.0 / gs));
                vec2 gh = EwHash2To2(gc);
                vec2 gp = (gc + 0.2 + 0.6 * gh) * gs;
                vec2 gd = fragCoord - gp;
                float sn = max(sin(t * (1.5 + 2.0 * gh.x) + gh.y * 40.0), 0.0);
                float sn2 = sn * sn;
                float sn4 = sn2 * sn2;
                float tw = sn4 * sn4 * sn4;
                float gl = step(0.55, gh.y) * tw * exp(-dot(gd, gd) * 0.35) * (spec * 3.0 + 0.2 + sw * 2.0);
                col += vec3(1.0, 0.95, 0.8) * (gl * GlintAmount);
            }
            if (GlimmerAmount > 0.0) {
                vec3 ph3 = EwHash2To3(floor(fragCoord));
                float pr = 0.8 + 3.0 * ph3.x;
                float pt = fract(t * pr * 0.25 + ph3.y);
                float pk = max(1.0 - abs(pt - 0.5) * 12.0, 0.0);
                pk *= pk;
                float bright = clamp(spec * 2.0 + dif * 0.5 + sw, 0.0, 2.0);
                float patchM = smoothstep(0.45, 0.8, nz);
                float pg = step(0.6, ph3.z) * pk * (0.25 + bright) * patchM;
                col += vec3(1.0, 0.95, 0.85) * (pg * 0.8 * GlimmerAmount);
            }
            acc = vec4(col * frameA, frameA);
        }
        if (frameA < 1.0) {
            float sh = 0.45 * (smoothstep(-1.0 - 12.0 * pv, -1.0, v) * step(v, 0.0) + smoothstep(1.0 + 8.0 * pv, 1.0, v) * step(0.0, v)) * (1.0 - frameA);
            float sh2 = sh * (2.0 - sh);
            acc.a += sh2 * (1.0 - acc.a);
        }
    }

    // inner fill (under frame + shadow), skip if fully occluded by opaque frame
    float ia = InnerOpacity * inner;
    if (ia > 0.0 && acc.a < 1.0) {
        vec3 paperCol = vec3(0.0);
        if (!SwHaveImage || SwTransparentImage) {
            vec2 uv = p * invMinRes;
            vec2 dh;
            float h0 = paperHeightAndGrad(uv, dh);

            float bumpScale = 0.055 * WrinkleDepth;
            vec3 pNormal = normalize(vec3(-dh * bumpScale, 1.0));

            // Single non-unrolled loop for stains (4+3 octaves) + fiber (2 samples)
            vec2 sp = uv * 2.0 + vec2(2.1, 5.7);
            float stain1 = 0.0, stain2 = 0.0, fiber = 0.0, amp = 0.5;
            const mat2 octM = mat2(1.6, 1.2, -1.2, 1.6);
            for (int i = ZERO; i < 9; i++) {
                if (i == 4)      { sp = uv * 4.5 + vec2(8.3, 1.2); amp = 0.5; }
                else if (i == 7) { sp = fragCoord * 0.45; amp = 0.7; }
                else if (i == 8) { sp = fragCoord * 0.9 + 13.7; amp = 0.3; }
                float nv = amp * vnoise2D(sp);
                if (i < 4)      stain1 += nv;
                else if (i < 7) stain2 += nv;
                else            fiber += nv;
                sp = octM * sp;
                amp *= 0.5;
            }
            float stain = stain1 * 0.65 + stain2 * 0.35;
            float waterRing = smoothstep(0.04, 0.0, abs(stain1 - 0.44)) * 0.16;
            fiber = (fiber - 0.5) * 0.06;

            float edgeDist = max(-d0 - hw * 0.65, 0.0);
            float edgeVig = mix(0.45, 1.0, smoothstep(0.0, hw * 2.2, edgeDist));
            float foldShade = smoothstep(0.15, -0.60, h0) * 0.22;

            // exact foxing age freckles (checks only cells within 0.10)
            vec2 fp = uv * 18.0 + 7.3;
            vec2 fcell = floor(fp);
            vec2 ffrac = fract(fp);
            vec2 fstep = step(0.5, ffrac) * 2.0 - 1.0;
            bool bx = min(ffrac.x, 1.0 - ffrac.x) < 0.10;
            bool by = min(ffrac.y, 1.0 - ffrac.y) < 0.10;
            float minD2 = 0.01;
            vec2 nearCell = fcell;
            for (int i = ZERO; i < 4; i++) {
                if (i == 1 && !bx) continue;
                if (i == 2 && !by) continue;
                if (i == 3 && !(bx && by)) break;
                vec2 nb = (i == 0) ? vec2(0.0) : (i == 1) ? vec2(fstep.x, 0.0) : (i == 2) ? vec2(0.0, fstep.y) : fstep;
                vec2 r = nb + EwHash2To2(fcell + nb) - ffrac;
                float d2 = dot(r, r);
                if (d2 < minD2) { minD2 = d2; nearCell = fcell + nb; }
                if (i == 0 && !bx && !by) break;
            }
            float foxSpot = (minD2 < 0.01) ? smoothstep(0.10, 0.02, sqrt(minD2)) * step(0.85, EwHash2To1(nearCell)) * 0.25 : 0.0;

            float nl1 = max(dot(pNormal, L0), 0.0);
            float nl2 = max(dot(pNormal, L1), 0.0);
            float diff = (0.25 + 0.75 * nl1 + 0.18 * nl2) * clamp(0.45 + 0.55 * pNormal.z - foldShade, 0.20, 1.0);

            float shd = max(dot(pNormal, H0), 0.0);
            float shd2 = shd * shd;
            float sheen = shd2 * shd2 * shd2 * 0.05;

            vec3 paperBase = mix(vec3(0.70, 0.58, 0.42), vec3(0.44, 0.32, 0.20), stain);
            paperBase = (paperBase * (1.0 - foldShade - waterRing - foxSpot) + vec3(fiber)) * edgeVig;

            vec3 litPaper = paperBase * diff + sheen * vec3(0.95, 0.90, 0.82);
            paperCol = clamp(litPaper * (InnerColor / vec3(0.62, 0.48, 0.32)), 0.0, 1.0);
        }

        if (SwHaveImage) {
            vec2 imgUv = (p + imgHs) / (2.0 * imgHs);
            vec4 tex = texture(iChannel0, imgUv);
            float shSpan = min(hw * 0.9, min(imgHs.x, imgHs.y) * 0.5);
            float dSh = EwSdRoundedBox2D(p + vec2(-0.5, 0.6) * (shSpan * 0.5), hs, rad);
            float castSh = 1.0 - smoothstep(-rabbet - shSpan * 0.75, -rabbet + shSpan * 0.25, dSh);
            float edgeDist = max(-d0 - rabbet, 0.0);
            float recessAo = mix(0.84, 1.0, smoothstep(0.0, shSpan * 1.1, edgeDist));
            vec2 np = p / imgHs;
            float lightGrad = 1.0 + 0.08 * dot(np, vec2(-0.6401844, 0.7682213));
            vec3 imgCol = tex.rgb * mix(0.58, 1.0, castSh) * recessAo * lightGrad;
            vec3 sweepSheen = vec3(1.0, 0.98, 0.94) * (sw * 0.14);
            if (SwTransparentImage) {
                vec3 innerCol = mix(paperCol, imgCol, tex.a) + sweepSheen;
                acc = over(acc, vec4(innerCol * ia, ia));
            } else {
                imgCol += sweepSheen;
                float imgA = tex.a * ia;
                acc = over(acc, vec4(imgCol * imgA, imgA));
            }
        } else {
            acc = over(acc, vec4(paperCol * ia, ia));
        }
    }

    // large random star flares (precomputed in vertexMain, skipped completely if none active)
    if (vTimeFl4.w > 0.0) {
        vec3 fl = vec3(0.0);
        const float C45 = 0.70738827;
        const float S45 = 0.70682518;
        for (int i = ZERO; i < 5; i++) {
            float fenv = (i == 0) ? vFlEnv03.x : (i == 1) ? vFlEnv03.y : (i == 2) ? vFlEnv03.z : (i == 3) ? vFlEnv03.w : vTimeFl4.z;
            if (fenv <= 0.0) continue;
            vec4 fp = (i == 0) ? vFl0 : (i == 1) ? vFl1 : (i == 2) ? vFl2 : (i == 3) ? vFl3 : vFl4;
            vec2 d = p - fp.xy;
            vec2 rs = vec2(fp.z * d.x - fp.w * d.y, fp.w * d.x + fp.z * d.y);
            float rr2 = dot(rs, rs);
            if (rr2 > 20.0) continue;
            vec2 rq = abs(rs);
            vec2 rq2 = abs(vec2(C45 * rs.x - S45 * rs.y, S45 * rs.x + C45 * rs.y));
            float rays = exp(-rq.x * 60.0 - rq.y * 2.5) + exp(-rq.y * 60.0 - rq.x * 2.5);
            rays += 0.4 * (exp(-rq2.x * 80.0 - rq2.y * 5.0) + exp(-rq2.y * 80.0 - rq2.x * 5.0));
            float core = exp(-rr2 * 60.0) + 0.25 * exp(-sqrt(rr2) * 6.0);
            fl += vec3(1.0, 0.9, 0.7) * (fenv * (rays + core));
        }
        float fa = clamp(max(fl.r, max(fl.g, fl.b)), 0.0, 1.0);
        acc.rgb += fl;
        acc.a = max(acc.a, fa);
    }

    // tone map the color, keep premultiplied
    vec3 lin = acc.rgb;
    lin = lin / (1.0 + max(lin - 0.8, 0.0));
    vec2 eb2 = min(fragCoord, res - fragCoord);
    float eb = min(eb2.x, eb2.y);
    float edge = smoothstep(0.5, 10.0, eb);
    lin *= edge;
    float a = acc.a * edge;

    float dz = EwDither(fragCoord, t, 2.0);
    vec3 col = clamp(lin + dz, 0.0, 1.0);
    col = min(col, vec3(a));
    a *= 1.0 - Additive;
    fragColor = vec4(col + bg.rgb * (1.0 - a), a + bg.a * (1.0 - a));
}
