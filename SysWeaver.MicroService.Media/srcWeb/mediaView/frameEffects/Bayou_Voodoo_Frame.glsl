//  media: image none
// =============================================================================
//  Bayou Voodoo Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Swamp Mist | Amount of green bayou mist creeping along the bottom | 0.5 | 0 .. 2 | x
//  iMouse.w = Candle Glow | Brightness of the candles, will-o'-wisps and glowing doll eyes | 0.5 | 0 .. 2 | x

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Moss, doll, candle and mist animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0875, 0.0689, 0.0565); // var: { "type": "col", "name": "Inner Color", "desc": "Base color of the dark wood planks inside the frame" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner wood planks (0% = transparent)", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const float BandWidth = 0.85; // var: { "min": 0.4, "max": 1.0, "step": 0.01, "name": "Band Width", "desc": "Width of the plank band relative to the inset", "scale": 100, "unit": "%" }
const vec3 WoodColor = vec3(0.36, 0.30, 0.24); // var: { "type": "col", "name": "Wood Color", "desc": "Color of the weathered cypress planks" }
const vec3 PaintColorA = vec3(0.30, 0.18, 0.36); // var: { "type": "col", "name": "Paint Color A", "desc": "First peeling paint color of the planks" }
const vec3 PaintColorB = vec3(0.17, 0.33, 0.29); // var: { "type": "col", "name": "Paint Color B", "desc": "Second peeling paint color of the planks" }
const float PaintAmount = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Paint Amount", "desc": "How much old paint is left on the planks", "scale": 100, "unit": "%" }
const vec3 MossColor = vec3(0.46, 0.50, 0.40); // var: { "type": "col", "name": "Moss Color", "desc": "Color of the hanging Spanish moss" }
const float MossLength = 1.0; // var: { "min": 0.0, "max": 2.5, "step": 0.05, "name": "Moss Length", "desc": "How far the moss hangs down from the top", "unit": "x" }
const int DollCount = 3; // var: { "min": 0, "max": 3, "step": 1, "name": "Dolls", "desc": "Number of voodoo dolls hanging from the top" }
const float DollSize = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Doll Size", "desc": "Size of the voodoo dolls", "unit": "x" }
const float SwingAmount = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Swing", "desc": "How far the dolls swing on their strings", "unit": "x" }
const vec3 FlameColor = vec3(1.0, 0.52, 0.16); // var: { "type": "col", "name": "Flame Color", "desc": "Color of the candle flames and their light" }
const vec3 MistColor = vec3(0.30, 0.80, 0.42); // var: { "type": "col", "name": "Mist Color", "desc": "Color of the swamp mist, wisps and doll eyes" }
const int WispCount = 6; // var: { "min": 0, "max": 12, "step": 1, "name": "Wisps", "desc": "Number of will-o'-wisps floating in the mist" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define SwampMist mix(0.0, 2.0, iMouse.z)
#define CandleGlow mix(0.0, 2.0, iMouse.w)

varying vec2 vP;
flat varying vec4 vF0;
flat varying vec4 vF1;
flat varying vec4 vSp;
flat varying vec4 vCnd;
flat varying vec4 vW0;
flat varying vec4 vW1;
flat varying vec4 vW2;
flat varying vec4 vW3;
flat varying vec4 vW4;
flat varying vec4 vW5;
#define ZERO min(iFrame, 0)

float fbm2(vec2 p, int octaves) {
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * EwValueNoise2D(p);
        p = m * p;
        amp *= 0.5;
    }
    return sum;
}
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
vec4 layer(vec3 c, float a, vec4 base) { return over(vec4(c * a, a), base); }
float fillPx(float dpx) { return clamp(0.5 - dpx, 0.0, 1.0); }

vec3 tonemapSoft(vec3 x) {
    vec3 k = 0.7 + 0.3 * (1.0 - exp(-(x - 0.7) / 0.3));
    return mix(x, k, step(0.7, x));
}

// Arc length position along the rounded rectangle (clockwise from top center), total length out
float perimPos(vec2 p, vec2 hs, float rad, out float total) {
    vec2 b = max(hs - rad, 0.0);
    float Q = b.x + b.y + rad * EW_HALF_PI;
    total = 4.0 * Q;
    vec2 a = abs(p);
    float u;
    if (a.x > b.x && a.y > b.y) {
        vec2 c = a - b;
        u = b.x + rad * atan(c.x, max(c.y, 1e-6));
    } else if (hs.x - a.x < hs.y - a.y) {
        u = b.x + rad * EW_HALF_PI + (b.y - a.y);
    } else {
        u = a.x;
    }
    if (p.x >= 0.0) return p.y >= 0.0 ? u : 2.0 * Q - u;
    return p.y < 0.0 ? 2.0 * Q + u : 4.0 * Q - u;
}

// Weathered plank band: s = perimeter position, t = -1 inner edge .. 1 outer edge
vec3 planks(float s, float L, float t, float hw) {
    float invHw = 1.0 / hw;
    float nPl = max(4.0, floor(L * (invHw / 7.0) + 0.5));
    float plen = L / nPl;
    float row = step(0.0, t);
    float ct = clamp(t + 1.0 - row, 0.0, 1.0);
    float sl = s + row * 0.5 * plen;
    float id = mod(floor(sl / plen), nPl) + row * 37.0;
    float lu = mod(sl, plen);
    float luHw = lu * invHw;
    vec3 h = EwHash1To3(id + 3.1);
    // grain running along the board
    vec2 gp = vec2(luHw * 0.35, ct * 2.2) + h.xy * 40.0;
    float warp = fbm2(gp * vec2(1.0, 1.5), 3);
    float rings = 0.5 + 0.5 * sin((ct * 9.0 + warp * 6.0 + h.z * 6.0) * 3.14159);
    float fine = EwValueNoise2D(vec2(luHw * 0.8, ct * 28.0) + h.yz * 50.0);
    float grain = 0.55 * rings + 0.45 * fine;
    vec3 wood = WoodColor * (0.5 + 0.65 * grain) * (0.75 + 0.5 * h.x);
    // rot and water stains
    float rot = fbm2(vec2(luHw * 0.6, ct * 1.2) + h.zx * 30.0 + 7.0, 4);
    wood *= mix(1.0, 0.4, smoothstep(0.5, 0.75, rot));
    // peeling paint
    vec3 paint = mix(PaintColorA, PaintColorB, step(0.5, h.y)) * (0.75 + 0.35 * fine);
    float pn = fbm2(vec2(luHw * 1.1, ct * 1.8) + h.xy * 20.0 + 3.0, 5);
    float th = 0.75 - PaintAmount * 0.45;
    float pm = smoothstep(th, th + 0.015, pn);
    float pedge = smoothstep(th - 0.035, th, pn) * (1.0 - pm);
    vec3 col = mix(wood, paint, pm);
    col *= 1.0 - 0.55 * pedge;
    // gaps between boards, worn bevels
    float eCross = min(ct, 1.0 - ct) * hw;
    float eAlong = min(lu, plen - lu);
    float gap = min(eCross, eAlong);
    float px = max(1.0, hw * (1.0 / 30.0));
    col *= 0.2 + 0.8 * smoothstep(0.3 * px, 3.5 * px, gap);
    // rusty nails
    float nx = min(abs(lu - 0.5 * hw), abs(lu - (plen - 0.5 * hw)));
    float nr = 0.1 * hw;
    if (nx < nr * 3.2 + 1.0) {
        vec2 nv = vec2(nx, (ct - 0.5) * hw);
        float dn = length(nv);
        float rust = smoothstep(nr * 3.2, nr, dn) * (0.4 + 0.5 * fine);
        col = mix(col, vec3(0.38, 0.15, 0.05), rust);
        float head = smoothstep(nr + 0.7, nr - 0.7, dn);
        if (head > 0.0) {
            vec3 nail = vec3(0.14, 0.12, 0.11) + 0.22 * smoothstep(nr, 0.0, length(nv - vec2(0.0, 0.35 * nr)));
            col = mix(col, nail, head);
        }
    }
    return col;
}

// Dark weathered wooden wall planks inside the frame
vec3 innerPlanks(vec2 p, vec2 hs, float U) {
    float invU = 1.0 / U;
    float ph = 115.0 * U;
    float invPh = 1.0 / ph;
    float seamWave = (EwValueNoise2D(vec2(p.x * (invU / 240.0), p.y * (invPh / 1.3) + 1.5)) - 0.5) * 3.5 * U;
    float py = p.y + seamWave;
    float pyPh = py * invPh;
    float row = floor(pyPh);
    float ct = pyPh - row;

    // staggered board lengths per row
    vec3 rh = EwHash1To3(row + 17.3);
    float plen = (540.0 + 360.0 * rh.x) * U;
    float invPlen = 1.0 / plen;
    float pxShift = p.x + rh.y * plen + row * 193.0 * U;
    float colIdx = floor(pxShift * invPlen);
    float lu = pxShift - colIdx * plen;
    float un = lu * invPlen;

    vec4 h = EwHash2To4(vec2(colIdx, row) + vec2(3.1, 7.7));

    // occasional wood knot deflecting the grain
    float knotDeflect = 0.0;
    float knotDark = 1.0;
    if (h.w >= 0.45) {
        vec2 kPos = vec2(0.2 + 0.6 * fract(h.x * 7.3), 0.25 + 0.5 * fract(h.y * 5.1));
        vec2 kd = vec2((un - kPos.x) * (plen * invPh) * 0.45, (ct - kPos.y) * 1.4);
        float kr2 = dot(kd, kd);
        if (kr2 < 1.2) {
            knotDeflect = exp(-kr2 * 14.0) * 1.6;
            knotDark = 1.0 - 0.42 * exp(-kr2 * 22.0);
        }
    }

    // horizontal wood grain (warped rings + fine fibers)
    float luPh = lu * invPh;
    vec2 gp = vec2(luPh * 0.55, ct * 2.4) + h.xy * 37.0;
    float warp = fbm2(gp * vec2(1.0, 1.4), 3);
    float ringFreq = 6.5 + 4.5 * h.z;
    float rings = 0.5 + 0.5 * sin((ct * ringFreq + warp * 5.5 + knotDeflect + h.z * 6.283) * 3.14159);
    rings = smoothstep(0.08, 0.92, rings);
    float fine = EwValueNoise2D(vec2(luPh * 1.6, ct * 34.0) + h.yz * 53.0);
    float fine2 = EwValueNoise2D(vec2(luPh * 0.4, ct * 14.0) + h.zw * 29.0);
    float grain = 0.50 * rings + 0.30 * fine + 0.20 * fine2;

    // per-plank tone & subtle warm/cool variation around InnerColor
    vec3 tint = InnerColor * (0.76 + 0.44 * h.x);
    tint *= mix(vec3(1.04, 0.99, 0.94), vec3(0.94, 0.98, 1.03), h.y);
    vec3 wood = tint * (0.48 + 0.72 * grain) * knotDark;

    // large-scale bayou dampness / age stains crossing boards
    float stain = fbm2(p * (invU / 320.0) + vec2(4.2, 1.9), 3);
    wood *= mix(1.0, 0.62, smoothstep(0.42, 0.78, stain));

    // subtle 3D plank bevel relief (dim top catch-light, deeper bottom lip)
    float bevelTop = smoothstep(0.88, 0.96, ct) * smoothstep(1.0, 0.97, ct);
    float bevelBot = smoothstep(0.14, 0.02, ct);
    wood *= 1.0 + 0.16 * bevelTop - 0.26 * bevelBot;

    // gaps & worn chamfers between planks
    float eCross = min(ct, 1.0 - ct) * ph;
    float eAlong = min(lu, plen - lu);
    float px = max(U, 0.75);
    float crack = smoothstep(0.35 * U, 0.35 * U + 1.4 * px, min(eCross, eAlong));
    float gapCross = smoothstep(0.6 * U, 4.5 * U + px, eCross);
    float gapAlong = smoothstep(0.5 * U, 3.2 * U + px, eAlong);
    wood *= (0.12 + 0.88 * crack) * (0.58 + 0.42 * gapCross) * (0.68 + 0.32 * gapAlong);

    // weathered dark iron nail heads near vertical joints (2 per end)
    float nx = min(abs(lu - 16.0 * U), abs(lu - (plen - 16.0 * U)));
    float nr = 2.8 * U;
    if (nx < nr * 2.2 + 1.0) {
        float nailY = ct < 0.5 ? 0.26 : 0.74;
        float dy = (ct - nailY) * ph;
        float streak = smoothstep(nr * 2.2, 0.0, nx)
                     * smoothstep(0.5 * nr, -0.8 * nr, dy)
                     * smoothstep(-10.0 * nr, -1.5 * nr, dy);
        wood *= 1.0 - 0.32 * streak;
        float dn = length(vec2(nx, dy));
        float nailHead = fillPx(dn - nr);
        if (nailHead > 0.0) {
            vec3 nailCol = InnerColor * (0.18 + 0.26 * smoothstep(nr, 0.0, length(vec2(nx, dy - 0.8 * U))));
            wood = mix(wood, nailCol, nailHead);
        }
    }

    // gentle interior ambient vignette so the wall feels recessed behind the frame
    vec2 vn = p / max(hs, vec2(1.0));
    float vig = 1.0 - 0.32 * dot(vn, vn);
    wood *= clamp(vig, 0.58, 1.0);

    return wood;
}
// Hanging Spanish moss; md = depth below its anchor line in px
vec4 moss(vec2 p, float md, float lenMul, float U, float T) {
    float maxPossibleLen = MossLength * 195.0 * U * lenMul;
    if (md > 1.35 * 1.2 * maxPossibleLen + 32.0 * U) return vec4(0.0);

    float invU = 1.0 / U;
    // Jagged top attachment profile so it doesn't look like a straight horizontal cut
    float topJag = 24.0 * U * fbm2(vec2(p.x * (invU / 45.0), 3.2), 3);
    float topJagFine = 8.0 * U * EwValueNoise1D(p.x * (invU / 12.0));
    float effectiveMd = md - (topJag + topJagFine);
    if (effectiveMd < 0.0) return vec4(0.0);

    float len = maxPossibleLen * (0.25 + 0.95 * smoothstep(0.2, 0.75, fbm2(vec2(p.x * (invU / 110.0), 1.7), 3)));
    if (effectiveMd > 1.35 * len) return vec4(0.0);
    float sway = sin(T * 0.8 + p.x * (invU / 300.0)) * effectiveMd * 0.06 + sin(T * 1.7 + p.x * (invU / 70.0)) * effectiveMd * 0.02;
    float xs = p.x - sway;
    float tip = effectiveMd / max(len * (0.45 + 0.7 * EwValueNoise1D(xs * (invU / 7.0))), 1.0);
    float w = fbm2(vec2(xs * (invU / 18.0), effectiveMd * (invU / 30.0)), 3);
    float n1 = EwValueNoise2D(vec2((xs + w * 22.0 * U) * (invU / 2.8), effectiveMd * (invU / 10.0)));
    float n2 = fbm2(vec2(xs * (invU / 12.0), effectiveMd * (invU / 14.0)) + w * 1.5, 3);
    float v = 0.45 * n1 + 0.6 * n2 + 0.6 * (1.0 - tip);
    float a = smoothstep(0.88, 1.05, v) * 0.92 + 0.18 * smoothstep(0.7, 0.95, v);
    a = min(a, 0.95);
    vec3 c = MossColor * (0.4 + 0.8 * n1) * (0.55 + 0.45 * smoothstep(0.0, 0.6, tip));
    return vec4(c * a, a);
}

// Voodoo doll hanging on a string from anchor A
vec4 doll(vec2 p, vec2 A, float Ls, float S, float ang, float seed, float U, float T, inout vec3 emit) {
    vec2 q = EwRotate2D(-ang) * (p - A);
    vec4 acc = vec4(0.0);
    if (abs(q.x) < 0.7 * U + 1.0 && q.y > -Ls - 0.7 * U - 1.0 && q.y < 0.7 * U + 1.0) {
        float ds = length(vec2(q.x, q.y - clamp(q.y, -Ls, 0.0))) - 0.7 * U;
        acc = layer(vec3(0.10, 0.08, 0.06), fillPx(ds), acc);
    }
    vec2 l = (q - vec2(0.0, -Ls)) / S;
    l = EwRotate2D(0.18 * sin(T * 0.9 + seed * 5.0)) * l;
    // eerie eye glow
    float flick = 0.6 + 0.4 * EwValueNoise1D(T * 3.0 + seed * 17.0);
    vec2 le = l - vec2(-6.0, -16.0);
    float re2 = dot(le, le);
    emit += MistColor * CandleGlow * flick * (0.9 * exp(-re2 * (1.0 / 6.0)) + 0.12 / (1.0 + re2 * (1.0 / 40.0)));

    float pad = 1.0 / S;
    if (abs(l.x) < 33.0 + pad && l.y > -100.0 - pad && l.y < 4.0 + pad) {
        float dHead = length(l - vec2(0.0, -17.0)) - 15.0;
        float dBody = EwSdRoundedBox2D(l - vec2(0.0, -51.0), vec2(12.0, 20.0), 7.0);
        float dArms = EwSdSegment2D(l, vec2(-26.0, -45.0), vec2(26.0, -41.0)) - 5.5;
        float dLegs = min(EwSdSegment2D(l, vec2(-6.0, -66.0), vec2(-10.0, -92.0)),
                          EwSdSegment2D(l, vec2(6.0, -66.0), vec2(9.0, -93.0))) - 6.0;
        float dKnot = length(l - vec2(0.0, -1.5)) - 3.5;
        float dd = min(min(dHead, dBody), min(min(dArms, dLegs), dKnot));
        float fa = fillPx(dd * S);
        if (fa > 0.0) {
            vec3 tint = seed < 0.5 ? vec3(0.52, 0.40, 0.26) : (seed < 1.5 ? vec3(0.30, 0.25, 0.20) : vec3(0.62, 0.56, 0.46));
            float weave = sin(l.x * 1.6) * sin(l.y * 1.6);
            float blot = EwValueNoise2D(l * 0.12 + seed * 9.0);
            vec3 c = tint * (0.82 + 0.18 * weave) * (0.7 + 0.5 * blot);
            c *= 0.4 + 0.6 * smoothstep(0.0, 3.5, -dd);
            // twine around the neck and wrists
            float tw = min(abs(l.y + 32.5) - 1.8, max(abs(abs(l.x) - 20.0) - 1.5, abs(l.y + 43.0) - 6.5));
            c = mix(c, vec3(0.30, 0.05, 0.04) * (0.7 + 0.3 * sin(l.x * 3.0 + l.y * 3.0)), fillPx(tw * S));
            // heart patch
            vec2 hp = l - vec2(-3.0, -50.0);
            vec2 hr = EwRotate2D(0.785398) * (hp - vec2(0.0, -0.5));
            float dHeart = min(min(length(hp - vec2(-2.8, 2.0)), length(hp - vec2(2.8, 2.0))) - 3.6, max(abs(hr.x), abs(hr.y)) - 3.9);
            c = mix(c, vec3(0.55, 0.03, 0.05) * (0.8 + 0.2 * weave), fillPx(dHeart * S));
            // button eye with a glint
            float dEye = sqrt(re2) - 4.6;
            float holes = min(length(le - vec2(-1.3, 0.0)), length(le - vec2(1.3, 0.0))) - 0.75;
            vec3 btn = mix(vec3(0.03, 0.03, 0.035), vec3(0.18, 0.17, 0.16), fillPx(holes * S));
            c = mix(c, btn, fillPx(dEye * S));
            // stitched X eye
            vec2 eR = l - vec2(6.5, -16.0);
            float dX = min(EwSdSegment2D(eR, vec2(-3.2, -3.2), vec2(3.2, 3.2)), EwSdSegment2D(eR, vec2(-3.2, 3.2), vec2(3.2, -3.2))) - 0.9;
            c = mix(c, vec3(0.35, 0.03, 0.03), fillPx(dX * S));
            // stitched mouth
            float xt = clamp(floor(l.x * (1.0 / 2.6) + 0.5) * 2.6, -5.2, 5.2);
            float dM = min(EwSdSegment2D(l, vec2(-6.5, -25.5), vec2(6.5, -25.0)) - 0.5, EwSdSegment2D(l, vec2(xt, -27.5), vec2(xt, -23.0)) - 0.45);
            c = mix(c, vec3(0.04, 0.03, 0.03), fillPx(dM * S));
            acc = layer(c, fa, acc);
        }
        // pins
        if (abs(l.x) < 26.0 + pad && l.y > -81.0 - pad && l.y < 3.0 + pad) {
            for (int k = ZERO; k < 3; k++) {
                vec3 hk = EwHash1To3(seed * 7.0 + float(k) * 1.37 + 0.5);
                vec2 bp = mix(vec2(-9.0, -64.0), vec2(9.0, -14.0), hk.xy);
                vec2 dir = normalize(bp - vec2(0.0, -40.0) + vec2(0.001, 0.0));
                dir = EwRotate2D((hk.z - 0.5) * 1.2) * dir;
                vec2 hd = bp + dir * 13.0;
                float dShaft = EwSdSegment2D(l, bp, hd) - 0.45;
                acc = layer(vec3(0.62, 0.62, 0.60), fillPx(dShaft * S), acc);
                float dPin = length(l - hd) - 2.7;
                float pinA = fillPx(dPin * S);
                if (pinA > 0.0) {
                    vec3 pc = hk.z < 0.33 ? vec3(0.75, 0.05, 0.05) : (hk.z < 0.66 ? vec3(0.85, 0.65, 0.08) : vec3(0.45, 0.12, 0.62));
                    pc += 0.45 * smoothstep(1.6, 0.0, length(l - hd - vec2(-0.9, 0.9)));
                    acc = layer(pc, pinA, acc);
                }
            }
        }
    }
    return acc;
}

// Carved Jack-o'-lantern pumpkin on the bottom sill with candles inside
// Carved Jack-o'-lantern pumpkin on the bottom sill with candles inside
vec4 pumpkin(vec2 p, vec2 c, float U, float T, inout vec3 emit, inout vec3 light, float bf) {
    vec2 lp = p - c;
    float flick = vF1.w;
    float gl = CandleGlow * flick;

    // Pumpkin illumination onto surroundings
    float distP2 = dot(lp, lp) / (U * U);
    light += bf * gl * FlameColor * 2.2 / (1.0 + distP2 * (1.0 / 14000.0));
    emit += bf * gl * FlameColor * 0.65 / (1.0 + distP2 * (1.0 / 2600.0));

    if (abs(lp.x) > 84.0 * U + 1.0 || lp.y < -74.0 * U - 1.0 || lp.y > 86.0 * U + 1.0) return vec4(0.0);

    vec4 acc = vec4(0.0);
    float invU = 1.0 / U;

    // Pumpkin body SDF: large squashed sphere with organic ribbed lobes
    float W = 70.0 * U;
    float H = 54.0 * U;
    float r = length(vec2(lp.x * 0.8, lp.y + 0.05 * lp.x * lp.x / W));
    float cosRib = cos(lp.x * (invU / 8.5));
    float ribs = cosRib * (5.5 * U);
    float dBody = r - (54.0 * U + ribs);

    // Render stem behind if above body
    if (lp.y > 44.0 * U) {
        float dStem = EwSdSegment2D(lp, vec2(0.0, 46.0 * U), vec2(11.0 * U, 74.0 * U)) - mix(8.0 * U, 3.8 * U, clamp((lp.y - 46.0 * U) * (invU / 28.0), 0.0, 1.0));
        float sStem = fillPx(dStem);
        if (sStem > 0.0) {
            vec3 stemCol = vec3(0.24, 0.27, 0.13) * (0.8 + 0.2 * sin(lp.x * 1.5));
            acc = layer(stemCol, sStem, acc);
        }
    }

    // Sinister carved cutouts (scaled for large pumpkin)
    float sBody = fillPx(dBody);
    bool inCutBox = (abs(lp.x) < 40.0 * U && lp.y > -56.0 * U && lp.y < 36.0 * U);
    if (sBody > 0.0 || inCutBox) {
        // Eyes: triangular angled cutouts
        const float cE = 0.96105544, sE = 0.27635565;
        vec2 pL = lp - vec2(-24.0 * U, 14.0 * U);
        vec2 eL = vec2(cE * pL.x - sE * pL.y, sE * pL.x + cE * pL.y);
        float dEyeL = max(abs(eL.x) * 1.4 + eL.y, -eL.y - 11.0 * U) - 9.0 * U;
        vec2 pR = lp - vec2(24.0 * U, 14.0 * U);
        vec2 eR = vec2(cE * pR.x + sE * pR.y, -sE * pR.x + cE * pR.y);
        float dEyeR = max(abs(eR.x) * 1.4 + eR.y, -eR.y - 11.0 * U) - 9.0 * U;

        // Nose triangle
        vec2 nP = lp - vec2(0.0, 2.0 * U);
        float dNose = max(abs(nP.x) * 1.5 - nP.y, nP.y - 7.0 * U) - 5.5 * U;

        // Sinister grinning mouth with jagged square teeth
        vec2 mP = lp - vec2(0.0, -14.0 * U);
        float mSmile = mP.y + 0.025 * mP.x * mP.x * invU;
        float dMouthSlot = max(abs(mP.x) - 34.0 * U, abs(mSmile) - 6.5 * U);
        float teeth = step(0.0, sin(mP.x * (invU / 6.8)));
        float dTeeth = max(abs(mP.x) - 30.0 * U, abs(mSmile - (teeth * 2.0 - 1.0) * 3.8 * U) - 3.2 * U);
        float dMouth = min(dMouthSlot, dTeeth);

        float dCutouts = min(min(dEyeL, dEyeR), min(dNose, dMouth));
        float cutFill = fillPx(dCutouts);

        if (sBody > 0.0) {
            float nx = clamp(lp.x / W, -1.0, 1.0);
            float ny = clamp(lp.y / H, -1.0, 1.0);
            float nz = sqrt(max(1.0 - nx * nx - ny * ny, 0.0));
            vec3 norm = normalize(vec3(nx, ny, nz * 1.5));

            vec3 pCol = vec3(0.92, 0.42, 0.04);
            float ribGroove = 0.65 + 0.35 * (0.5 + 0.5 * cosRib);
            float vertGrad = 0.75 + 0.25 * smoothstep(-H, 0.0, lp.y) * smoothstep(H, 0.0, lp.y);
            const vec3 pkLightDir = normalize(vec3(-0.35, 0.55, 0.75));
            float diff = max(dot(norm, pkLightDir), 0.0);
            vec3 bodyCol = pCol * ribGroove * vertGrad * (0.45 + 0.55 * diff);
            bodyCol += FlameColor * gl * 0.22 * exp(-dBody * dBody / (90.0 * U * U));

            float rindMask = smoothstep(3.5 * U, 0.0, dCutouts);
            vec3 rindCol = vec3(0.98, 0.68, 0.15);
            bodyCol = mix(bodyCol, rindCol, rindMask * 0.85);

            bodyCol = mix(bodyCol, vec3(0.0), cutFill);
            acc = layer(bodyCol, sBody * (1.0 - cutFill), acc);
        }

        if (cutFill > 0.0) {
            vec3 flameInside = mix(FlameColor, vec3(1.0, 0.96, 0.75), smoothstep(0.0, -6.0 * U, dCutouts));
            flameInside *= gl * 1.8;
            emit += flameInside * cutFill;
        }
    }

    return acc;
}

// Crawling or dangling bayou spider
vec4 spider(vec2 p, vec2 center, float angle, float U, float T, float seed) {
    vec2 lp = EwRotate2D(-angle) * (p - center);
    vec4 acc = vec4(0.0);
    
    float dAbd = length(vec2(lp.x * 1.25, lp.y - 3.5 * U)) - 4.2 * U;
    float dHead = length(vec2(lp.x * 1.1, lp.y + 2.5 * U)) - 2.6 * U;
    float dBody = min(dAbd, dHead);
    
    float dLegs = 1e5;
    for (int j = ZERO; j < 8; j++) {
        float fk = float(j >> 1);
        float sgn = (j & 1) == 0 ? -1.0 : 1.0;
        float twitch = 0.25 * sin(T * 6.0 + fk * 1.8 + sgn * 2.0 + seed);
        vec2 root = vec2(sgn * 2.0 * U, (1.0 - fk * 1.4) * U);
        vec2 knee = root + vec2(sgn * (6.0 + fk * 1.4) * U, (4.5 - fk * 3.2) * U + twitch * 2.2 * U);
        vec2 foot = knee + vec2(sgn * (4.5 + fk * 1.2) * U, (-6.0 - fk * 1.4) * U);
        float dL1 = EwSdSegment2D(lp, root, knee) - 0.45 * U;
        float dL2 = EwSdSegment2D(lp, knee, foot) - 0.35 * U;
        dLegs = min(dLegs, min(dL1, dL2));
    }
    acc = layer(vec3(0.0), 0.45 * fillPx(min(dBody, dLegs) + 1.8 * U), acc);
    acc = layer(vec3(0.08, 0.07, 0.07), fillPx(dLegs), acc);
    
    float bodyFill = fillPx(dBody);
    if (bodyFill > 0.0) {
        vec3 bCol = vec3(0.09, 0.08, 0.08);
        bCol += 0.25 * smoothstep(2.5 * U, 0.0, length(lp - vec2(-1.0, -2.5) * U));
        vec2 abP = lp - vec2(0.0, 3.5 * U);
        float dHour = max(abs(abP.y) - 1.8 * U, abs(abP.x) * 1.6 - abs(abP.y) - 0.2 * U);
        bCol = mix(bCol, vec3(0.85, 0.05, 0.04), fillPx(dHour));
        acc = layer(bCol, bodyFill, acc);
    }
    return acc;
}

// Shiny 3D bayou snake slithering along the perimeter of the frame
vec4 snake(float s, float L, float d, float U, float T, inout vec3 emit) {
    if (abs(d) > 35.0 * U + 1.0) return vec4(0.0);
    vec4 acc = vec4(0.0);
    float snakeLen = 380.0 * U;
    float headS = vW0.w;
    float ds = mod(headS - s + L, L);

    if (ds < snakeLen + 12.0 * U) {
        float invU = 1.0 / U;
        float phase = ds * (invU / 24.0) - T * 3.6;
        float wave = sin(phase) * (9.5 * U);
        float dSnakeCenter = d - wave;
        float uSnake = clamp(ds / snakeLen, 0.0, 1.0);

        float rSnake;
        if (uSnake < 0.10) {
            float hn = uSnake * 10.0;
            rSnake = mix(5.5 * U, 14.5 * U, sin(hn * 2.8));
        } else {
            rSnake = mix(12.0 * U, 2.0 * U, smoothstep(0.40, 1.0, uSnake));
        }

        float dSnake = (ds <= snakeLen)
            ? abs(dSnakeCenter) - rSnake
            : length(vec2(ds - snakeLen, dSnakeCenter)) - 1.5 * U;

        // Deep ambient drop shadow under the thick body onto the planks
        acc = layer(vec3(0.0), 0.65 * fillPx(dSnake + 4.5 * U), acc);
        acc = layer(vec3(0.0), 0.35 * fillPx(dSnake + 8.0 * U), acc);

        float sFill = fillPx(dSnake);
        if (sFill > 0.0) {
            // Cylindrical 3D normal vector
            float latNorm = clamp(dSnakeCenter / rSnake, -1.0, 1.0);
            float zNorm = sqrt(max(1.0 - latNorm * latNorm, 0.0));

            // Along-body tangent and normal slope from sinusoidal motion
            float slope = cos(phase) * (9.5 / 24.0);
            vec3 nVec = normalize(vec3(slope * 0.4, latNorm, zNorm));

            // Key directional light (cool moonlight / swamp light from top-left)
            const vec3 lightDir = normalize(vec3(-0.4, -0.6, 0.8));
            float diff = max(dot(nVec, lightDir), 0.0);

            // Secondary bounce light from swamp fog below
            float bounce = max(-nVec.y, 0.0) * 0.3;

            // Extreme wet gloss / specular sheen
            const vec3 halfVec = normalize(lightDir + vec3(0.0, 0.0, 1.0));
            float nh = max(dot(nVec, halfVec), 0.0);
            float nh2 = nh * nh;
            float nh4 = nh2 * nh2;
            float specBroad = nh4 * nh4;
            float specTight = pow(nh, 38.0);
            float fresnel = pow(1.0 - zNorm, 3.5);

            // Scales texture
            float scalePat = sin(ds * (invU / 3.2) + latNorm * 2.0) * cos(latNorm * 4.5);
            vec3 bellyCol = vec3(0.72, 0.68, 0.32);
            vec3 backCol = vec3(0.06, 0.36, 0.14);
            vec3 diamondCol = vec3(0.10, 0.08, 0.06);
            vec3 snakeBase = mix(backCol, diamondCol, step(0.25, scalePat));
            snakeBase = mix(snakeBase, bellyCol, smoothstep(-0.30, -0.85, latNorm));

            // Combine lighting & glossy 3D shading
            vec3 snakeCol = snakeBase * (0.22 + 0.78 * diff + bounce);
            vec3 glossCol = vec3(0.85, 0.98, 0.90);
            snakeCol += glossCol * (specTight * 1.25 + specBroad * 0.25 + fresnel * 0.45);

            // Menacing glowing yellow eyes with slit pupils
            if (uSnake < 0.09 && abs(latNorm) > 0.42 && abs(latNorm) < 0.88) {
                float dEyeDot = length(vec2(ds - 11.5 * U, abs(dSnakeCenter) - 6.0 * U)) - 2.2 * U;
                float eFill = fillPx(dEyeDot);
                if (eFill > 0.0) {
                    float dSlit = max(abs(ds - 11.5 * U) - 0.45 * U, abs(abs(dSnakeCenter) - 6.0 * U) - 1.8 * U);
                    vec3 eyeCol = mix(vec3(1.0, 0.88, 0.12), vec3(0.05, 0.02, 0.0), fillPx(dSlit));
                    snakeCol = mix(snakeCol, eyeCol, eFill);
                    emit += vec3(1.0, 0.75, 0.1) * eFill * 0.85;
                }
            }

            acc = layer(snakeCol, sFill, acc);
        }
    }
    return acc;
}
vec3 wispData(int i, float wispHalfX, float topEdge, float U, float T) {
    vec3 h = EwHash1To3(float(i) * 3.7 + 1.3);
    float wf = fract(h.x + T * 0.008 * (h.y - 0.5));
    float wx = (wf * 2.0 - 1.0) * wispHalfX;
    float wy = -topEdge + (30.0 + 120.0 * h.z) * U + sin(T * 0.7 + h.x * 20.0) * 14.0 * U;
    float pulse = smoothstep(0.2, 1.0, 0.5 + 0.5 * sin(T * (1.2 + h.y) + h.z * 30.0)) * sin(wf * 3.14159);
    return vec3(wx + sin(T * 0.5 + h.y * 30.0) * 20.0 * U, wy, pulse);
}

void vertexMain(vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vP = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    float hw = inset * BandWidth;
    if (SwHaveImage) {
        // contain: inner picture rectangle (hs - hw / 2) gets the picture's aspect, frame stays inside the view
        vec2 ts = max(iChannelResolution[0].xy, vec2(1.0));
        float A = ts.x / ts.y;
        vec2 mh = max(hs - 0.5 * hw, vec2(1.0));
        vec2 hi = (mh.x > mh.y * A) ? vec2(mh.y * A, mh.y) : vec2(mh.x, mh.x / A);
        hs = hi + 0.5 * hw;
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    // decoration scale: the frame height (= view height without a picture), never more than its width
    float U = SwHaveImage ? max(min(2.0 * (hs.y + inset), 2.0 * (hs.x + inset)), 64.0) / 1080.0 : res.y / 1080.0;
    float invU = 1.0 / U;
    float T = mod(iTime * Speed, 3600.0);
    float topEdge = hs.y - hw;
    float CU = 1.4 * U;
    vec2 b = max(hs - rad, 0.0);
    float L = 4.0 * (b.x + b.y + rad * EW_HALF_PI);
    float headS = mod(T * 50.0 * U, L);

    vF0 = vec4(hs, rad, hw);
    vF1 = vec4(U, invU, T, 0.8 + 0.4 * EwValueNoise1D(T * 8.5 + 41.2));
    vSp = vec4(
        -hs.x + hw * 1.8,
        topEdge - (30.0 + 18.0 * sin(T * 0.8)) * U,
        sin(T * 0.5) * (hs.y * 0.45),
        cos(T * 0.5) >= 0.0 ? 0.0 : 3.14159
    );
    vCnd = vec4(
        0.75 + 0.5 * EwValueNoise1D(T * 9.0),
        (EwValueNoise1D(T * 2.3) - 0.5) * 3.0 * CU,
        0.75 + 0.5 * EwValueNoise1D(T * 9.0 + 13.0),
        (EwValueNoise1D(T * 2.3 + 7.0) - 0.5) * 3.0 * CU
    );
    float wispHalfX = hs.x - hw - 40.0 * U;
    vW0 = vec4(wispData(0, wispHalfX, topEdge, U, T), headS);
    vW1 = vec4(wispData(1, wispHalfX, topEdge, U, T), L);
    vW2 = vec4(wispData(2, wispHalfX, topEdge, U, T), SwingAmount * (0.10 * sin(T * 1.3) + 0.04 * sin(T * 2.9)));
    vW3 = vec4(wispData(3, wispHalfX, topEdge, U, T), SwingAmount * (0.10 * sin(T * 1.3 + 4.0) + 0.04 * sin(T * 2.9 + 1.0)));
    vW4 = vec4(wispData(4, wispHalfX, topEdge, U, T), SwingAmount * (0.10 * sin(T * 1.3 + 8.0) + 0.04 * sin(T * 2.9 + 2.0)));
    vW5 = vec4(wispData(5, wispHalfX, topEdge, U, T), invU * invU);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 p = vP;
    vec2 hs = vF0.xy;
    float rad = vF0.z;
    float hw = vF0.w;
    float U = vF1.x;
    float invU = vF1.y;
    float T = vF1.z;
    float d = EwSdRoundedBox2D(p, hs, rad);
    float S = 1.8 * U * DollSize;
    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = smoothstep(0.5, -0.5, d + 0.5 * hw);
    vec3 innerCol = vec3(0.0);
    vec3 imgCol = vec3(0.0);
    float imgA = 0.0;
    vec4 plankPm = vec4(0.0);
    vec4 imgPm = vec4(0.0);
    if (inner > 0.0 && InnerOpacity > 0.0) {
        if (SwHaveImage) {
            // picture fitted to the inner rectangle (frame already has its aspect), straight alpha -> premultiplied
            vec2 hi = max(hs - 0.5 * hw, vec2(1.0));
            vec4 tx = texture(iChannel0, clamp((p + hi) / (2.0 * hi), 0.0, 1.0));
            // same recessed ambient shadow as the plank wall
            vec2 vn = p / max(hs, vec2(1.0));
            imgCol = tx.rgb * clamp(1.0 - 0.32 * dot(vn, vn), 0.58, 1.0);
            imgA = tx.a;
            imgPm = vec4(imgCol * imgA, imgA) * InnerOpacity;
        }
        // plank wall: without a picture, or behind a transparent one
        if (!SwHaveImage || SwTransparentImage) {
            innerCol = innerPlanks(p, hs, U);
            plankPm = vec4(tonemapSoft(innerCol) * InnerOpacity, InnerOpacity);
        }
    }
    vec4 base = over(over(imgPm, plankPm) * inner, bg);

    float topEdge = hs.y - hw;
    float reachTop = MossLength * 260.0 * U;
    if (DollCount > 0) reachTop = max(reachTop, 135.0 * U + 125.0 * S);
    float reachBot = 240.0 * U;
    bool inBand = d > -hw - max(24.0 * U, 1.0);
    bool inTop = p.y > topEdge - reachTop;
    bool inBot = p.y < -topEdge + reachBot;
    if (!(inBand || inTop || inBot)) {
        if (inner > 0.0 && InnerOpacity > 0.0) {
            float dz = EwDither(fragCoord, iTime, 2.0);
            base.rgb = clamp(base.rgb + dz * base.a, vec3(0.0), vec3(base.a));
        }
        fragColor = base;
        return;
    }

    vec4 acc = vec4(0.0);
    vec3 emit = vec3(0.0);
    vec3 light = vec3(0.0);
    float bf = 1.0 - smoothstep(hw + 0.45 * reachBot, hw + reachBot, p.y + hs.y);

    // candles standing on the bottom edge
    float cx = hs.x - hw - max(rad - hw, 0.0) * 0.6 - 55.0 * U;
    vec4 candles = vec4(0.0);
    if (inBot) {
        float CU = 1.4 * U;
        float invU2c = 1.0 / (U * U);
        for (int i = ZERO; i < 2; i++) {
            float fi = float(i);
            float side = -1.0;
            bool tall = (i == 0);
            float h = (tall ? 78.0 : 46.0) * CU;
            float w = (tall ? 11.0 : 9.5) * CU;
            vec2 cb = vec2(side * (cx - (tall ? 0.0 : 26.0 * CU)), -topEdge - 3.0 * U);
            vec2 lp = p - cb;
            float flick = tall ? vCnd.x : vCnd.z;
            float swayF = tall ? vCnd.y : vCnd.w;
            vec2 fc = cb + vec2(swayF, h + 11.0 * CU);
            vec2 fp = p - fc;
            float r2 = dot(fp, fp) * invU2c;
            float gl = CandleGlow * flick;
            light += bf * gl * FlameColor * 1.1 / (1.0 + r2 * (1.0 / 6000.0));
            emit += bf * gl * FlameColor * 0.32 / (1.0 + r2 * (1.0 / 1100.0));
            if (abs(lp.x) < 40.0 * CU && lp.y > -10.0 * CU && lp.y < h + 40.0 * CU) {
                // wax body with drips
                float dB = EwSdRoundedBox2D(lp - vec2(0.0, 0.5 * h), vec2(w, 0.5 * h), 2.5 * CU);
                float dripLen = (0.12 + 0.55 * EwValueNoise1D(lp.x / (3.2 * CU) + fi * 9.0)) * h;
                float drip = smoothstep(h - dripLen - 1.5 * CU, h - dripLen + 1.5 * CU, lp.y);
                vec3 wax = i == 0 ? vec3(0.08, 0.07, 0.07) : vec3(0.62, 0.06, 0.05);
                float lx = lp.x / w;
                float cyl = sqrt(max(1.0 - lx * lx, 0.0));
                vec3 wc = wax * (0.45 + 0.55 * cyl) * (1.0 + 0.35 * drip);
                wc += gl * FlameColor * 0.5 * smoothstep(h - 16.0 * CU, h, lp.y) * (0.5 + 0.5 * cyl);
                candles = layer(wc, fillPx(dB), candles);
                // wick
                float dW = EwSdSegment2D(lp, vec2(0.0, h - 1.0 * CU), vec2(swayF * 0.3, h + 5.0 * CU)) - 0.8 * CU;
                candles = layer(vec3(0.05), fillPx(dW), candles);
                // flame
                fp.x -= swayF * clamp(fp.y / (12.0 * CU), 0.0, 1.0);
                float e = length(vec2(fp.x / (4.4 * CU), fp.y / ((fp.y > 0.0 ? 12.0 * flick : 5.0) * CU)));
                vec3 fcol = mix(FlameColor, vec3(1.0, 0.95, 0.8), smoothstep(1.0, 0.25, e));
                emit += fcol * smoothstep(1.0, 0.7, e) * 1.3 * min(CandleGlow * 1.5, 1.0);
            }
        }
        vec2 pkCenter = vec2(cx - 15.0 * U, -topEdge + 46.0 * U);
        candles = over(pumpkin(p, pkCenter, U, T, emit, light, bf), candles);
    }

    // band shadow on the inside
    if (d < -hw) acc = layer(vec3(0.0), 0.55 * smoothstep(-hw - 22.0 * U, -hw, d), acc);

    if (inBand) {
        float L;
        float s = perimPos(p, hs, rad, L);

        // plank band
        float aB = fillPx(abs(d) - hw);
        if (aB > 0.0) {
            vec3 wc = planks(s, L, clamp(d / hw, -1.0, 1.0), hw);
            wc *= 0.85 + light;
            acc = layer(wc, aB, acc);
        }

        // Shiny slithering bayou snake
        acc = over(snake(s, L, d, U, T, emit), acc);
    }

    // Crawling spiders and web thread
    vec2 sp1 = vSp.xy;
    if (inTop && abs(p.x - sp1.x) < 2.0 * U + 1.0 && p.y > sp1.y - 2.0 * U - 1.0 && p.y < topEdge + 0.3 * hw + 2.0 * U + 1.0) {
        float dThread = EwSdSegment2D(p, vec2(sp1.x, topEdge + 0.3 * hw), sp1) - 0.35 * U;
        acc = layer(vec3(0.7, 0.75, 0.7), fillPx(dThread) * 0.65, acc);
    }
    for (int si = ZERO; si < 2; si++) {
        bool activeSp = (si == 0) ? inTop : (inBand && abs(p.x) > hs.x - hw * 1.6 && abs(p.y) < hs.y - hw - 30.0 * U);
        if (!activeSp) continue;
        vec2 spPos = (si == 0) ? sp1 : vec2(hs.x - 0.5 * hw, vSp.z);
        vec2 sdp = p - spPos;
        float spBound = 26.0 * U + 1.0;
        if (abs(sdp.x) < spBound && abs(sdp.y) < spBound) {
            float spAng = (si == 0) ? 0.0 : vSp.w;
            acc = over(spider(p, spPos, spAng, U, T, si == 0 ? 0.0 : 3.7), acc);
        }
    }

    // Spanish moss along the top
    if (inTop && MossLength > 0.0 && p.y > 0.0) {
        float md = -(d + 0.6 * hw);
        float xe = hs.x - hw - max(rad - hw, 0.0);
        float xf = 1.0 - smoothstep(xe - 140.0 * U, xe, abs(p.x));
        if (md > 0.0 && xf > 0.0) acc = over(moss(p, md, xf, U, T), acc);
    }

    // voodoo dolls
    if (inTop && DollCount > 0) {
        for (int i = ZERO; i < DollCount; i++) {
            float fi = float(i);
            float fx = (i == 0 ? -0.96 : (i == 1 ? -0.82 : 0.94));
            vec2 A = vec2(fx * (hs.x - hw), topEdge + 0.3 * hw);
            float Ls = (45.0 + 35.0 * EwHash1To1(fi + 0.7)) * U + 0.3 * hw;
            vec2 dA = p - A;
            float rMax = Ls + 130.0 * S;
            if (dot(dA, dA) > rMax * rMax) continue;
            float ang = (i == 0) ? vW2.w : (i == 1) ? vW3.w : vW4.w;
            acc = over(doll(p, A, Ls, S, ang, fi, U, T, emit), acc);
        }
    }

    acc = over(candles, acc);

    // swamp mist and will-o'-wisps
    if (inBot) {
        if (SwampMist > 0.0) {
            float yBot = p.y + hs.y - 0.2 * hw;
            if (yBot > -hw - 10.0 * U) {
                float xBorderDist = hs.x - abs(p.x);
                float edgeFade = smoothstep(15.0 * U, 65.0 * U, xBorderDist);
                if (edgeFade > 0.0) {
                    float cornerDrop = smoothstep(10.0 * U, rad + 1.2 * hw, xBorderDist);
                    float invU = 1.0 / U;
                    float billowDome = 0.5 + 0.35 * sin(p.x * (invU / 110.0) + T * 0.9) + 0.25 * sin(p.x * (invU / 55.0) - T * 1.3);
                    float maxReach = (75.0 + 45.0 * billowDome) * U * cornerDrop;
                    if (yBot < maxReach) {
                        float hNorm = clamp(yBot / max(maxReach, 1.0), 0.0, 1.0);
                        float domeFade = sqrt(max(1.0 - hNorm * hNorm, 0.0)) * smoothstep(1.0, 0.75, hNorm);

                        float flowT = T * 0.85;
                        vec2 uvA = vec2(p.x * (invU / 140.0) + flowT * 0.25, yBot * (invU / 45.0) - flowT * 0.12);
                        vec2 uvB = vec2(p.x * (invU / 80.0) - flowT * 0.35, yBot * (invU / 35.0) + flowT * 0.18);
                        float nA = fbm2(uvA, 3);
                        float nB = fbm2(uvB + nA * 1.3, 3);

                        float puffTurb = smoothstep(0.2, 0.8, 0.55 * nA + 0.55 * nB);
                        float dens = SwampMist * domeFade * puffTurb * smoothstep(-hw - 8.0 * U, 0.0, yBot) * edgeFade;
                        float fa = clamp(dens * 0.85, 0.0, 0.85);

                        vec3 mCol = MistColor * (0.35 + 0.65 * nB);
                        mCol += vec3(0.12, 0.50, 0.22) * (nA * nA) * CandleGlow;
                        acc = layer(mCol, fa, acc);
                    }
                }
            }
        }
        float invU2 = vW5.w;
        float wispHalfX = hs.x - hw - 40.0 * U;
        for (int i = ZERO; i < WispCount; i++) {
            vec3 wd;
            if (WispCount > 6 && i >= 6) {
                vec3 h = EwHash1To3(float(i) * 3.7 + 1.3);
                float wf = fract(h.x + T * 0.008 * (h.y - 0.5));
                float wx = (wf * 2.0 - 1.0) * wispHalfX;
                float wy = -topEdge + (30.0 + 120.0 * h.z) * U + sin(T * 0.7 + h.x * 20.0) * 14.0 * U;
                float pulse = smoothstep(0.2, 1.0, 0.5 + 0.5 * sin(T * (1.2 + h.y) + h.z * 30.0)) * sin(wf * 3.14159);
                wd = vec3(wx + sin(T * 0.5 + h.y * 30.0) * 20.0 * U, wy, pulse);
            } else {
                wd = (i == 0) ? vW0.xyz : (i == 1) ? vW1.xyz : (i == 2) ? vW2.xyz : (i == 3) ? vW3.xyz : (i == 4) ? vW4.xyz : vW5.xyz;
            }
            vec2 wv = p - wd.xy;
            float r2 = dot(wv, wv) * invU2;
            emit += CandleGlow * wd.z * (vec3(0.75, 1.0, 0.8) * exp(-r2 * (1.0 / 14.0)) + bf * MistColor * 0.4 / (1.0 + r2 * (1.0 / 220.0)));
        }
    }

    // compose: premultiplied, emission additive
    vec3 col = tonemapSoft(acc.rgb + emit);
    float a = max(acc.a, max(col.r, max(col.g, col.b)));
    vec2 eg = min(fragCoord, res - fragCoord);
    float edge = smoothstep(0.5, 10.0, min(eg.x, eg.y));
    // with a picture the frame may not fill the view: fade glows / mist outside the frame
    if (SwHaveImage) edge *= 1.0 - smoothstep(hw, hw + 40.0 * U, d);
    col *= edge;
    a *= edge;
    float dz = EwDither(fragCoord, iTime, 2.0);
    a = clamp(a + dz, 0.0, 1.0);
    col = clamp(col + dz, 0.0, 1.0);
    col = min(col, vec3(a));
    if (inner > 0.0 && InnerOpacity > 0.0 && inBot) {
        vec4 litPm = vec4(0.0);
        if (!SwHaveImage || SwTransparentImage) {
            vec3 litInner = clamp(tonemapSoft(innerCol * (1.0 + light * 0.55)) + dz, 0.0, 1.0);
            litPm = vec4(litInner * InnerOpacity, InnerOpacity);
        }
        if (SwHaveImage) {
            // candle / pumpkin / wisp light on the picture: multiplicative, soft clipped (identity without light)
            vec3 lit = 1.0 - (1.0 - imgCol) * exp(-imgCol * light * 0.55);
            lit = clamp(lit + dz, 0.0, 1.0);
            litPm = over(vec4(lit * imgA, imgA) * InnerOpacity, litPm);
        }
        base = over(litPm * inner, bg);
    } else if (inner > 0.0 && InnerOpacity > 0.0) {
        base.rgb = clamp(base.rgb + dz * base.a, vec3(0.0), vec3(base.a));
    }
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
