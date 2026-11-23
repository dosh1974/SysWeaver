//  media: image none
// =============================================================================
//  Christmas Garland Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Lights | Brightness and glow of the fairy lights | 0.5 | 0 .. 2 | x
//  iMouse.w = Swing | How far the hanging baubles swing | 0.5 | 0 .. 0.4 | rad

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Branch sway, bauble swing and twinkle animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0845170455, 0.15625, 0.0943803267); // var: { "type": "col", "name": "Inner Color", "desc": "Color tint of the inner pine forest" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner pine scene", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const vec3 PineColor = vec3(0.12, 0.40, 0.18); // var: { "type": "col", "name": "Pine Color", "desc": "Color of the fir needles" }
const vec3 RibbonColor = vec3(0.78, 0.04, 0.07); // var: { "type": "col", "name": "Ribbon Color", "desc": "Color of the corner bows" }
const vec3 BaubleColor1 = vec3(0.80, 0.05, 0.08); // var: { "type": "col", "name": "Bauble Color 1", "desc": "First bauble color" }
const vec3 BaubleColor2 = vec3(0.95, 0.68, 0.22); // var: { "type": "col", "name": "Bauble Color 2", "desc": "Second bauble color" }
const vec3 BaubleColor3 = vec3(0.12, 0.32, 0.85); // var: { "type": "col", "name": "Bauble Color 3", "desc": "Third bauble color" }
const float Thickness = 0.9; // var: { "min": 0.5, "max": 1.0, "step": 0.01, "name": "Thickness", "desc": "Garland thickness relative to the inset", "scale": 100, "unit": "%" }
const float BaubleSize = 1.0; // var: { "min": 0.6, "max": 1.4, "step": 0.01, "name": "Bauble Size", "desc": "Size of the baubles", "unit": "x" }
const float Twinkle = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Twinkle", "desc": "How strongly the fairy lights twinkle", "scale": 100, "unit": "%" }
const bool ShowLights = true; // var: { "name": "Lights", "desc": "Show the string of fairy lights" }
const bool ShowCandy = true; // var: { "name": "Candy", "desc": "Show candy canes and peppermints" }
const bool ShowBows = true; // var: { "name": "Bows", "desc": "Show the ribbon bows at the corners" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define LightAmount mix(0.0, 2.0, iMouse.z)
#define SwingAmount mix(0.0, 0.4, iMouse.w)
#define ZERO min(iFrame, 0)

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
vec4 layer(vec4 acc, vec3 c, float a) { return over(vec4(c * a, a), acc); }
float cover(float dist) { return clamp(0.5 - dist, 0.0, 1.0); }   // 1 px AA, dist in px
vec3 knee(vec3 c) { vec3 e = max(c - 0.8, 0.0); return min(c, 0.8) + 0.2 * (1.0 - exp(-e / 0.2)); }

// arc length position s along the rounded rectangle path + outward normal
float perim(vec2 p, vec2 hs, float rad, out vec2 N, out float Q) {
    vec2 b = hs - rad;
    Q = b.x + b.y + rad * EW_HALF_PI;
    vec2 q = abs(p);
    vec2 e = q - b;
    float sq; vec2 n;
    if (e.x > 0.0 && e.y > 0.0) {
        float ang = atan(e.y, e.x);
        sq = b.y + rad * ang;
        n = vec2(cos(ang), sin(ang));
    } else if (e.x > e.y) {
        sq = min(q.y, b.y); n = vec2(1.0, 0.0);
    } else {
        sq = Q - min(q.x, b.x); n = vec2(0.0, 1.0);
    }
    vec2 sg = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
    N = n * sg;
    if (sg.y > 0.0) return sg.x > 0.0 ? sq : 2.0 * Q - sq;
    return sg.x < 0.0 ? 2.0 * Q + sq : 4.0 * Q - sq;
}

float cornerDist(float s, float Q, float sc) {
    float sm = mod(s, 2.0 * Q);
    return min(min(abs(sm - sc), abs(sm - 2.0 * Q + sc)), min(sm + sc, 2.0 * Q + sc - sm));
}

// one fir sprig: stem along +x (0..len), needles angled forward on both sides
vec4 sprig(vec2 lp, float len, float U, float seed, vec3 pine) {
    vec2 csAngle = vec2(0.6216, 0.7833);   // needle angle 0.9 rad
    float sp = 0.075 * U;
    float ay = abs(lp.y);
    float side = lp.y < 0.0 ? 0.8 : 1.0;
    vec4 acc = vec4(0.0);
    float xs = clamp(lp.x, 0.0, len);
    vec2 stemD = vec2(lp.x - xs, lp.y);
    float stemR = max(0.03 * U, 0.5);
    float stemCut = stemR + 0.5;
    float d2Stem = dot(stemD, stemD);
    if (d2Stem < stemCut * stemCut) {
        acc = layer(acc, vec3(0.20, 0.13, 0.07), clamp(stemCut - sqrt(d2Stem), 0.0, 1.0));
    }
    float kc = floor((lp.x - ay * 0.79356568) / sp + 0.5);
    for (int i = ZERO - 1; i <= 1; i++) {
        float k = kc + float(i);
        float bx = k * sp;
        if (k < 1.0 || bx > len) continue;
        float f = bx / len;
        float nl = 0.36 * U * (1.0 - 0.55 * f * f);
        vec2 r = vec2(lp.x - bx, ay);
        float tt = clamp(dot(r, csAngle), 0.0, nl);
        vec2 rDiff = r - csAngle * tt;
        float d2 = dot(rDiff, rDiff);
        float tn = tt / nl;
        float cut = max(0.026 * U * (1.0 - 0.5 * tn), 0.45) + 0.5;
        if (d2 >= cut * cut) continue;
        float cv = clamp(cut - sqrt(d2), 0.0, 1.0);
        float hn = EwHash2To1(vec2(seed, k + (lp.y < 0.0 ? 0.5 : 0.0)));
        vec3 c = pine * (0.45 + 0.75 * tn) * (0.75 + 0.5 * hn) * side;
        c = mix(c, vec3(0.75, 0.9, 0.8), 0.15 * tn * tn);
        acc = layer(acc, c, cv);
    }
    return acc;
}
vec3 varyPineColor(vec3 baseHsv, float seed, float depth);
vec4 pineBranch(vec2 dp, float ang, float len, float U, float seed, vec3 baseHsv, float depth) {
    float ca = cos(ang), sa = sin(ang);
    vec2 bp = vec2(dp.x * ca + dp.y * sa, -dp.x * sa + dp.y * ca);
    if (bp.x < -0.4 * U || bp.x > len + 1.1 * U || abs(bp.y) > 1.1 * U) return vec4(0.0);
    vec3 pineCol = varyPineColor(baseHsv, seed, depth);   // only for branches that can cover the pixel

    vec4 acc = vec4(0.0);
    float xs = clamp(bp.x, 0.0, len);
    float curve = 0.04 * len * sin(xs / max(len, 1.0) * EW_PI);
    float stemR = max(0.038 * U * (1.0 - 0.4 * (xs / len)), 0.65);
    float dStem = length(vec2(bp.x - xs, bp.y - curve)) - stemR;
    vec3 barkCol = mix(vec3(0.20, 0.12, 0.06), pineCol * 0.25, 0.35);
    acc = layer(acc, barkCol, cover(dStem));

    const float lengths[5] = float[5](0.54, 0.58, 0.54, 0.52, 0.58);
    const vec2 sca_ssa[5] = vec2[5](
        vec2(0.83645568, 0.54803507),
        vec2(0.85252452, -0.52268723),
        vec2(0.88701083, 0.46177933),
        vec2(0.90044710, -0.43496553),
        vec2(0.99920011, 0.03998933)
    );
    const vec2 s_off[5] = vec2[5](
        vec2(0.20, 0.02351141),
        vec2(0.40, 0.03804226),
        vec2(0.60, 0.03804226),
        vec2(0.78, 0.02554556),
        vec2(0.95, 0.00625738)
    );

    for (int i = ZERO; i < 5; i++) {
        vec2 v = bp - s_off[i] * len;
        vec2 slp = vec2(v.x * sca_ssa[i].x + v.y * sca_ssa[i].y, -v.x * sca_ssa[i].y + v.y * sca_ssa[i].x);
        float slen = lengths[i] * U;
        if (slp.x >= -0.2 * U && slp.x <= slen + 0.45 * U && abs(slp.y) <= 0.48 * U) {
            acc = over(sprig(slp, slen, U, seed + float(i) * 3.7 + 1.0, pineCol), acc);
        }
    }
    return acc;
}

vec3 varyPineColor(vec3 baseHsv, float seed, float depth) {
    vec3 h = EwHash1To3(seed);
    float hueShift = (h.x - 0.48) * 0.10;
    vec3 hsv;
    hsv.x = fract(baseHsv.x + hueShift);
    hsv.y = clamp(baseHsv.y * (0.75 + 0.50 * h.y), 0.15, 1.0);
    float depthMult = mix(0.20, 1.05, depth);
    hsv.z = clamp(baseHsv.z * depthMult * (0.80 + 0.40 * h.z), 0.01, 1.0);
    return EwHsvToRgb(hsv);
}

vec3 innerPineScene(vec2 p, vec2 res, float t, vec3 innerCol, float U, float inset) {
    float sU = max(U, 28.0);
    vec3 deepShadow = innerCol * 0.15;
    vec3 darkForest = innerCol * 0.35;
    float yNorm = p.y / (0.5 * res.y);
    vec3 skyGrad = mix(deepShadow, darkForest, clamp(0.5 + 0.45 * yNorm, 0.0, 1.0));
    vec2 uvNorm = p / (0.5 * res);
    float rVig = dot(uvNorm, uvNorm);
    vec3 baseCol = skyGrad * mix(1.05, 0.55, clamp(rVig * 0.6, 0.0, 1.0));
    vec4 acc = vec4(baseCol, 1.0);
    vec3 baseHsv = EwRgbToHsv(innerCol);

    vec2 cellSize = vec2(sU * 1.85, sU * 1.65);
    float sU2 = 1.21 * sU * sU;

    const vec2 shifts[3] = vec2[3](vec2(0.0), vec2(0.35, 0.65), vec2(0.70, 0.35));
    // per layer and branch (A, B): index = layer * 2 + branch
    const vec2 h_offs[6] = vec2[6](vec2(17.3, 31.7), vec2(73.1, 91.5), vec2(41.3, 67.9), vec2(83.7, 107.1), vec2(53.1, 89.9), vec2(113.7, 149.3));
    const vec2 bpos_offs[6] = vec2[6](vec2(0.26, 0.32), vec2(0.74, 0.74), vec2(0.30, 0.60), vec2(0.76, 0.24), vec2(0.32, 0.68), vec2(0.80, 0.30));
    const vec4 ang_mult_base[6] = vec4[6](vec4(1.1, 0.04, 0.85, 0.0), vec4(1.1, 0.04, 0.90, 1.2), vec4(1.0, 0.05, 1.0, 0.7), vec4(1.0, 0.05, 1.05, 2.4), vec4(0.95, 0.05, 1.1, 2.1), vec4(0.95, 0.05, 1.15, 3.3));
    const float lens[3] = float[3](0.64, 0.66, 0.68);
    const vec4 seeds_depths[6] = vec4[6](vec4(71.0, 31.0, 0.0, 0.15), vec4(97.0, 43.0, 137.0, 0.22), vec4(53.0, 79.0, 300.0, 0.50), vec4(61.0, 83.0, 500.0, 0.58), vec4(79.0, 97.0, 700.0, 0.88), vec4(89.0, 103.0, 900.0, 0.98));

    for (int lyr = ZERO; lyr < 3; lyr++) {
        vec2 shift = shifts[lyr] * cellSize;
        vec2 c = floor((p - shift) / cellSize);
        // 3x3 cells x 2 branches, same order as before (cell by cell, A then B)
        // per layer constants, read once (no dynamic array indexing inside the cell loop)
        vec2 hoA = h_offs[lyr * 2], hoB = h_offs[lyr * 2 + 1];
        vec2 boA = bpos_offs[lyr * 2], boB = bpos_offs[lyr * 2 + 1];
        vec4 amA = ang_mult_base[lyr * 2], amB = ang_mult_base[lyr * 2 + 1];
        vec4 sdA = seeds_depths[lyr * 2], sdB = seeds_depths[lyr * 2 + 1];
        float lenL = lens[lyr];
        for (int cell = ZERO; cell < 9; cell++) {
            int cy = cell / 3;
            vec2 cid = c + vec2(float(cell - cy * 3 - 1), float(cy - 1));
            for (int b = 0; b < 2; b++) {
                bool isA = b == 0;
                vec2 bo = isA ? boA : boB;
                // cull before hashing: the root lies within ±0.1 cell of its nominal spot,
                // the branch reaches <= 1.0 sU above and <= 2.21 sU sideways of it
                vec2 dn = p - ((cid + bo) * cellSize + shift);
                if (dn.y > 0.1 * cellSize.y + sU || abs(dn.x) > 0.1 * cellSize.x + 2.22 * sU) continue;
                vec4 h1 = EwHash2To4(cid + (isA ? hoA : hoB));
                vec2 bpos = (cid + bo + (h1.xy - 0.5) * 0.20) * cellSize + shift;
                vec2 dp = p - bpos;
                float len = sU * (lenL + 0.14 * h1.x);
                float rr = len + 1.1 * sU;
                // branches point down (angle -90° ± 0.6 rad): nothing reaches more than 0.95 sU above the root
                if (dp.y > sU || dot(dp, dp) > rr * rr + sU2) continue;
                vec4 am = isA ? amA : amB;
                float ang = -1.57 + (h1.z - 0.5) * am.x + am.y * sin(t * am.z + h1.w * EW_TAU + am.w);
                vec4 sd = isA ? sdA : sdB;
                float seed = cid.x * sd.x + cid.y * sd.y + sd.z;
                acc = over(pineBranch(dp, ang, len, sU, seed, baseHsv, sd.w), acc);
            }
        }
    }

    return acc.rgb;
}

vec4 candyCane(vec2 q, float U) {
    float R = 0.2 * U, th = max(0.075 * U, 1.0), y0 = -0.55 * U, y1 = 0.3 * U;
    float ys = clamp(q.y, y0, y1);
    float dd = length(vec2(q.x, q.y - ys));
    float l = ys - y0, c = q.x;
    vec2 hq = q - vec2(R, y1);
    float dA, lA, cA;
    if (hq.y > 0.0) { float r = length(hq); dA = abs(r - R); cA = R - r; lA = (y1 - y0) + R * atan(hq.y, -hq.x); }
    else { dA = length(hq - vec2(R, 0.0)); cA = 0.0; lA = (y1 - y0) + R * EW_PI; }
    if (dA < dd) { dd = dA; l = lA; c = cA; }
    float cv = cover(dd - th);
    if (cv <= 0.0) return vec4(0.0);
    float cn = clamp(c / th, -1.0, 1.0);
    float per = 0.22 * U;
    float st = fract((l + c * 0.9) / per);
    float aw = 1.0 / per;
    float wh = smoothstep(0.42 - aw, 0.42 + aw, st) * (1.0 - smoothstep(1.0 - 2.0 * aw, 1.0, st));
    vec3 col = mix(vec3(0.85, 0.05, 0.08), vec3(0.97, 0.95, 0.92), wh);
    col *= 0.45 + 0.6 * sqrt(1.0 - cn * cn);
    col += 0.45 * smoothstep(0.3, 0.0, abs(cn + 0.35));
    return vec4(col * cv, cv);
}

vec4 peppermint(vec2 q, float U) {
    float R = 0.24 * U;
    vec4 acc = vec4(0.0);
    float wx = abs(q.x) - 0.9 * R;
    float wing = max(abs(q.y) - (0.03 * U + 0.6 * max(wx, 0.0)), max(-wx, wx - 0.3 * U));
    float pleat = 0.3 + 0.15 * sin(atan(q.y, wx + 0.05 * U) * 14.0);
    acc = layer(acc, vec3(0.88, 0.94, 1.0), pleat * cover(wing));
    float r = length(q);
    float cv = cover(r - R);
    if (cv > 0.0) {
        float a = atan(q.y, q.x);
        float sw = sin(a * 6.0 + r / R * 2.5);
        vec3 col = mix(vec3(0.85, 0.05, 0.08), vec3(0.97, 0.96, 0.94), smoothstep(-0.2, 0.2, sw));
        col *= 0.7 + 0.3 * sqrt(max(0.0, 1.0 - r * r / (R * R)));
        col += 0.4 * smoothstep(0.45 * R, 0.0, length(q - vec2(-0.3, 0.35) * R));
        acc = layer(acc, col, cv);
    }
    return acc;
}

float ellipseD(vec2 q, vec2 r) {
    float k0 = length(q / r), k1 = length(q / (r * r));
    return k0 * (k0 - 1.0) / max(k1, 1e-5);
}

vec4 bow(vec2 q, float B, vec3 rc) {
    vec4 acc = vec4(0.0);
    vec3 gold = vec3(0.95, 0.78, 0.4);
    float ew = max(0.03 * B, 0.7);
    // tails
    vec2 a = vec2(0.04, -0.05) * B, e = vec2(0.42, -0.95) * B;
    float la = length(e - a); vec2 ad = (e - a) / la;
    vec2 qa = q - a;
    float tt = dot(qa, ad) / la;
    float pp = dot(qa, vec2(-ad.y, ad.x));
    float hw = B * (0.09 + 0.04 * clamp(tt, 0.0, 1.0));
    float dT = max(abs(pp) - hw, max((tt - (0.88 + 0.12 * abs(pp) / hw)) * la, -tt * la));
    float cv = cover(dT);
    if (cv > 0.0) {
        vec3 c = rc * (0.6 + 0.3 * sin(tt * 6.0 + pp / hw * 1.2));
        c = mix(c, gold, cover(hw - abs(pp) - ew));
        acc = layer(acc, c, cv);
    }
    // loops
    vec2 lq = q - vec2(0.42, 0.16) * B;
    float ca = cos(0.35), sa = sin(0.35);
    lq = vec2(lq.x * ca + lq.y * sa, -lq.x * sa + lq.y * ca);
    vec2 lr = vec2(0.42, 0.25) * B;
    float dL = ellipseD(lq, lr);
    cv = cover(dL);
    if (cv > 0.0) {
        vec3 c = rc * (0.6 + 0.45 * smoothstep(-lr.y, lr.y, lq.y)) * (0.6 + 0.4 * smoothstep(-0.4 * B, 0.1 * B, lq.x));
        float hole = cover(ellipseD(lq - vec2(0.08, -0.02) * B, vec2(0.24, 0.1) * B));
        c = mix(c, rc * 0.3, hole);
        c = mix(c, gold, cover(-dL - ew));
        acc = layer(acc, c, cv);
    }
    // knot
    vec2 kq = abs(q - vec2(0.0, 0.07 * B)) - vec2(0.12, 0.14) * B + 0.05 * B;
    float dK = length(max(kq, 0.0)) + min(max(kq.x, kq.y), 0.0) - 0.05 * B;
    cv = cover(dK);
    if (cv > 0.0) {
        vec3 c = rc * (0.75 + 0.35 * smoothstep(-0.14 * B, 0.14 * B, q.y));
        c = mix(c, gold, cover(-dK - ew));
        acc = layer(acc, c, cv);
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {   // fit the frame to the picture's aspect (contain)
        vec2 ts = max(iChannelResolution[0].xy, vec2(1.0));
        float A = ts.x / ts.y;
        hs = hs.x > hs.y * A ? vec2(hs.y * A, hs.y) : vec2(hs.x, hs.x / A);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);

    float U = inset * Thickness;          // garland scale (px)
    float gc = 0.12 * U;                   // garland center, inward of the line
    vec4 bg = vec4(Background * Opacity, Opacity);
    float t = mod(iTime * Speed, 1256.6371);
    float inner = smoothstep(0.5, -0.5, d - 0.1 * U);
    vec4 base = bg;
    vec3 imgPm = vec3(0.0);   // premultiplied picture color (lit by the fairy lights later)
    if (inner > 0.001 && InnerOpacity > 0.001) {
        if (SwHaveImage) {
            vec4 under = bg;
            if (SwTransparentImage) {
                vec3 innerPine = innerPineScene(p, res, t, InnerColor, U, inset);
                under = over(vec4(innerPine * InnerOpacity, InnerOpacity) * inner, bg);
            }
            vec4 tx = texture(iChannel0, clamp(p / (2.0 * hs) + 0.5, 0.0, 1.0));
            // soft shadow of the garland, light from the upper left
            float ds = EwSdRoundedBox2D(p - vec2(0.15, -0.2) * U, hs, rad);
            float shade = 1.0 - 0.6 * smoothstep(-2.2 * U, -0.2 * U, ds);
            float ia = tx.a * InnerOpacity * inner;
            imgPm = tx.rgb * ia;
            base = over(vec4(imgPm * shade, ia), under);
        } else {
            vec3 innerPine = innerPineScene(p, res, t, InnerColor, U, inset);
            base = over(vec4(innerPine * InnerOpacity, InnerOpacity) * inner, bg);
        }
    }
    float v = -d;
    if (v > 2.7 * U + 4.0) { fragColor = base; return; }
    vec2 N; float Q;
    float radP = min(max(rad, 0.8 * U), min(hs.x, hs.y));   // smooth parameterization at sharp corners
    float s = perim(p, hs, radP, N, Q);
    float L = 4.0 * Q;
    vec2 T = vec2(-N.y, N.x);
    float sc = (hs.y - radP) + radP * 0.25 * EW_PI;
    vec2 sv = vec2(s, v);
    vec4 acc = vec4(0.0);
    vec3 glow = vec3(0.0);

    // dark garland body
    float fq = EW_TAU * max(1.0, floor(L / (2.7 * U) + 0.5)) / L;
    float nz = 0.10 * sin(s * fq) + 0.06 * sin(s * fq * 3.0 + 1.7);
    float bd = abs(v - gc - 0.05 * U) - (0.3 + nz) * U;
    acc = layer(acc, PineColor * 0.28, 0.92 * smoothstep(0.06 * U + 0.5, -0.06 * U - 0.5, bd));

    // fir sprigs: 3 layers (back, outer, inner), rooted in the garland body, swaying in a gentle breeze
    float nS = max(4.0, floor(L / (0.6 * U) + 0.5));
    float cw = L / nS;
    float c0 = floor(s / cw);
    for (int n = ZERO; n < 15; n++) {
        int m = n / 5;
        int k = n - m * 5 - 3;
        {
            float j = c0 + float(k);
            float jm = mod(j, nS);
            float id = jm * 3.0 + float(m);
            vec4 h = EwHash1To4(id + 13.0);
            float sg = m == 1 ? 1.0 : m == 2 ? -1.0 : (h.z < 0.5 ? 1.0 : -1.0);
            float bx = (j + h.x) * cw;
            // base on the opposite side of the direction the sprig points: tips stay on the band
            float by = gc + 0.05 * U - sg * (m == 0 ? 0.08 : 0.12 + 0.12 * h.y) * U;
            float ang = sg * (m == 0 ? 0.05 + 0.2 * fract(h.z * 7.0) : 0.12 + 0.3 * h.z);
            // about a third of the outer / inner sprigs stick out further (inward a bit more than outward)
            float stick = m > 0 && fract(h.x * 13.7 + h.w * 3.1) < 0.33 ? 1.0 : 0.0;
            float sh = fract(h.y * 9.3);
            ang = mix(ang, sg * (m == 2 ? 0.6 + 0.25 * sh : 0.45 + 0.15 * sh), stick);
            float len = U * (m == 0 ? 1.0 + 0.4 * h.w : 0.75 + 0.4 * h.w + stick * (m == 2 ? 0.25 : 0.1));
            vec2 dp = sv - vec2(bx, by);
            // conservative reject before the sway trig: sprig reach from its base
            float reach = len + 0.5 * U;
            if (dot(dp, dp) > reach * reach) continue;
            ang += (0.07 + 0.04 * h.w) * sin(t * (0.9 + 0.3 * h.y) - bx / U * 0.5 + h.z * EW_TAU)
                 + 0.03 * sin(t * 2.3 + h.w * EW_TAU);
            float ca = cos(ang), sa = sin(ang);
            vec2 lp = vec2(dp.x * ca + dp.y * sa, -dp.x * sa + dp.y * ca);
            if (lp.x < -0.1 * U || lp.x > len + 0.3 * U || abs(lp.y) > 0.4 * U) continue;
            float shade = m == 0 ? 0.55 : (0.75 + 0.4 * h.y);
            acc = over(sprig(lp, len, U, id, PineColor * shade), acc);
        }
    }

    // fairy lights
    if (ShowLights) {
        float nL = max(2.0, floor(L / (2.4 * U) + 0.5));
        float lw = L / nL;
        float wa = 0.3 * U, wk = EW_TAU / lw;
        float wv = gc + 0.05 * U + wa * sin(s * wk);
        float slope = wa * wk * cos(s * wk);
        acc = layer(acc, vec3(0.04, 0.09, 0.04), cover(abs(v - wv) / sqrt(1.0 + slope * slope) - max(0.02 * U, 0.4)));
        float hp = 0.5 * lw;
        float kb = floor(s / hp);
        float br = max(0.075 * U, 1.5);
        for (int k = ZERO - 1; k <= 1; k++) {
            float j = kb + float(k);
            float jm = mod(j, 2.0 * nL);
            float u = (j + 0.5) * hp;
            float sd = mod(j, 2.0) < 0.5 ? 1.0 : -1.0;
            vec2 e = vec2(s - u, (v - (gc + 0.05 * U + (wa + 0.12 * U) * sd)) * sd);
            vec2 sq = abs(e - vec2(0.0, -1.4 * br)) - vec2(0.6, 0.45) * br;
            float dsock = length(max(sq, 0.0)) + min(max(sq.x, sq.y), 0.0);
            float dbulb = length(e / vec2(1.0, 1.5)) - br;
            vec4 hb = EwHash1To4(jm + 5.0);
            int li = int(mod(jm, 5.0));
            vec3 lc = li == 0 ? vec3(1.0, 0.72, 0.28) : li == 1 ? vec3(1.0, 0.16, 0.12) : li == 2 ? vec3(0.2, 0.45, 1.0) : li == 3 ? vec3(1.0, 0.3, 0.75) : vec3(0.3, 1.0, 0.45);
            float tw = 0.5 + 0.5 * sin(t * (1.5 + 0.5 * floor(hb.x * 4.0)) + hb.y * EW_TAU);
            tw = mix(1.0, 0.15 + 0.85 * smoothstep(0.1, 0.9, tw), Twinkle);
            float I = LightAmount * tw;
            acc = layer(acc, vec3(0.05, 0.12, 0.05), cover(dsock));
            vec3 bcol = lc * (0.35 + 0.6 * I) + vec3(0.5) * I * smoothstep(br, 0.0, length(e - vec2(0.0, 0.2 * br)));
            acc = layer(acc, bcol, cover(dbulb));
            float gr = 0.45 * U;
            glow += lc * I * 0.5 * exp(-dot(e, e) / (gr * gr));
        }
    }

    float nC = max(4.0, floor(L / (2.6 * U) + 0.5));
    float cc = L / nC;

    // candy canes and peppermints
    if (ShowCandy) {
        float kc = floor(s / cc + 0.5);
        for (int k = ZERO - 1; k <= 1; k++) {
            float j = kc + float(k);
            float jm = mod(j, nC);
            vec4 h = EwHash1To4(jm + 41.0);
            float u = j * cc + (h.x - 0.5) * 0.4 * U;
            if (cornerDist(mod(u, L), Q, sc) < 1.9 * U) continue;
            vec2 dl = sv - vec2(u, gc + 0.05 * U + (h.y - 0.5) * 0.3 * U);
            if (dot(dl, dl) > U * U) continue;
            float ang = (h.w - 0.5) * 2.0 + (fract(h.z * 7.0) < 0.5 ? 0.0 : EW_PI);
            float ca = cos(ang), sa = sin(ang);
            vec2 q = vec2(dl.x * ca + dl.y * sa, -dl.x * sa + dl.y * ca);
            acc = over(h.z < 0.6 ? candyCane(q, U) : peppermint(q, U), acc);
        }
    }

    // baubles
    float kb2 = floor(s / cc);
    float hf = max(N.y, 0.0);   // hang on the top edge
    for (int k = ZERO - 1; k <= 1; k++) {
        float j = kb2 + float(k);
        float jm = mod(j, nC);
        vec4 h = EwHash1To4(jm + 97.0);
        float u = (j + 0.5) * cc;
        if (cornerDist(mod(u, L), Q, sc) < 1.9 * U) continue;
        float r = U * (0.30 + 0.14 * h.x) * BaubleSize;
        float sw = hf * SwingAmount * sin(t * (1.0 + 0.5 * floor(h.y * 3.0)) + h.z * EW_TAU);
        vec2 dir = vec2(sin(sw), cos(sw));
        vec2 pv = vec2(u, gc - 0.05 * U);
        float Lc = mix(0.3 * U, 0.35 * U + 0.5 * U * h.w + r, hf);
        vec2 dl = sv - (pv + dir * Lc);
        if (length(dl) > Lc + r + 2.0) continue;
        if (hf > 0.0) {
            vec2 ps = sv - pv;
            float tt = clamp(dot(ps, dir), 0.0, Lc);
            acc = layer(acc, vec3(0.85, 0.7, 0.35), hf * cover(length(ps - dir * tt) - max(0.012 * U, 0.35)));
        }
        vec2 cq = vec2(dot(dl, vec2(dir.y, -dir.x)), dot(dl, dir) + r + 0.03 * U);
        vec2 cb = abs(cq) - vec2(0.09, 0.06) * U * BaubleSize;
        float dcap = length(max(cb, 0.0)) + min(max(cb.x, cb.y), 0.0);
        vec3 capc = vec3(0.85, 0.68, 0.32) * (0.55 + 0.6 * smoothstep(0.09 * U, -0.09 * U, cq.x)) * (0.85 + 0.15 * sin(cq.x / U * 90.0));
        acc = layer(acc, capc, cover(dcap));
        vec2 so = dl.x * T - dl.y * N;
        float cv = cover(length(so) - r);
        if (cv > 0.0) {
            vec3 nr = vec3(so / r, sqrt(max(0.0, 1.0 - dot(so, so) / (r * r))));
            vec3 ld = normalize(vec3(-0.45, 0.6, 0.65));
            float df = max(dot(nr, ld), 0.0);
            float spc = pow(max(dot(reflect(-ld, nr), vec3(0.0, 0.0, 1.0)), 0.0), 36.0);
            float idx = floor(fract(h.x * 7.0 + h.y) * 3.0);
            vec3 bcol = idx < 1.0 ? BaubleColor1 : idx < 2.0 ? BaubleColor2 : BaubleColor3;
            vec3 axis = vec3(-(dir.x * T - dir.y * N), 0.0);
            float lat = dot(nr, axis);
            vec3 col = bcol * (0.18 + 0.95 * df);
            col += pow(1.0 - nr.z, 3.0) * 0.35 * mix(vec3(0.1, 0.25, 0.12), vec3(0.9, 0.85, 0.7), 0.5 + 0.5 * nr.y);
            float style = fract(h.z * 5.3);
            if (style < 0.5) {
                float band = smoothstep(0.15, 0.11, abs(lat - 0.05));
                float gl = EwHash2To1(floor(fragCoord / max(1.0, 0.03 * U)) + floor(t * 4.0));
                vec3 gcol = vec3(0.95, 0.8, 0.45) * (0.35 + 0.8 * df) + step(0.88, gl) * 0.7;
                col = mix(col, gcol, band);
            } else if (style < 0.8) {
                float ring = smoothstep(0.06, 0.03, abs(abs(lat) - 0.45));
                col = mix(col, vec3(0.95) * (0.4 + 0.7 * df), ring);
            }
            col += spc * 0.9;
            acc = layer(acc, col, cv);
        }
    }

    // corner bows
    if (ShowBows) {
        vec2 n45 = vec2(0.70710678);
        vec2 bcn = (hs - rad) + rad * n45 - n45 * (gc + 0.1 * U) * mix(1.4142, 1.0, clamp(rad / U, 0.0, 1.0));
        vec2 sg = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
        vec2 bp = p - sg * bcn;
        float B = 1.2 * U;
        if (length(bp) < 1.4 * B) acc = over(bow(vec2(abs(bp.x), bp.y), B, RibbonColor), acc);
    }

    // twinkling glints on the needles
    float nG = max(4.0, floor(L / (0.55 * U) + 0.5));
    float gw = L / nG;
    vec2 gcell = vec2(floor(s / gw), floor((v - gc + 0.5 * U) / (0.5 * U)));
    if (gcell.y >= 0.0 && gcell.y <= 1.0) {
        vec4 h = EwHash1To4(mod(gcell.x, nG) * 2.0 + gcell.y + 211.0);
        vec2 gpos = vec2((gcell.x + 0.3 + 0.4 * h.x) * gw, gc - 0.5 * U + (gcell.y + 0.3 + 0.4 * h.y) * 0.5 * U);
        vec2 dg = sv - gpos;
        float tw = pow(max(0.0, sin(t * (0.5 + 0.5 * floor(h.z * 4.0)) + h.w * EW_TAU)), 16.0);
        float k1 = 20.0 / U, k2 = 120.0 / U;
        float fl = exp(-abs(dg.x) * k1 - abs(dg.y) * k2) + exp(-abs(dg.y) * k1 - abs(dg.x) * k2);
        glow += vec3(1.0, 0.95, 0.8) * fl * tw * 0.8;
    }

    vec3 col = acc.rgb + glow;
    float a = acc.a;
    a = a + (1.0 - a) * clamp(max(glow.r, max(glow.g, glow.b)), 0.0, 1.0);
    col = knee(col);
    float edge = smoothstep(0.5, 10.0, min(0.5 * res.x - abs(p.x), 0.5 * res.y - abs(p.y)));
    col *= edge; a *= edge;
    float dz = EwDither(fragCoord, iTime, 2.0);
    a = clamp(a + dz, 0.0, 1.0);
    col = min(clamp(col + dz, 0.0, 1.0), vec3(a));
    a *= 1.0 - Additive;
    // fairy lights cast their colored glow onto the picture
    base.rgb = min(base.rgb + imgPm * glow * 1.5, vec3(base.a));
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
