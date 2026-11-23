//  media: image none
// =============================================================================
//  Temple Portal Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Sun Angle | Direction of the sunlight (negative = from the left) | 0.3 | -60 .. 60 | {0} °
//  iMouse.w = Weathering | Erosion: rugged edges, pits, cracks and rough surface | 0.5 | 0 .. 100 | {0} %

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Wind, falling debris, gold glint and sun drift animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the image or 3D Egyptian crypt inside the frame", "scale": 100, "unit": "%" }
const bool Torches = true; // var: { "name": "Torches", "desc": "Animated flickering fire torches along the crypt walls" }
const vec3 TorchColor = vec3(1.0, 0.52, 0.15); // var: { "type": "col", "name": "Torch Color", "desc": "Warm firelight color of the crypt torches" }
const float CryptFog = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Crypt Haze", "desc": "Dusty atmospheric haze in the depth of the crypt", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const vec3 StoneColor = vec3(0.88, 0.70, 0.47); // var: { "type": "col", "name": "Stone Color", "desc": "Main sandstone color" }
const vec3 StrataColor = vec3(0.68, 0.45, 0.28); // var: { "type": "col", "name": "Strata Color", "desc": "Color of the darker sandstone layers" }
const vec3 GoldColor = vec3(1.0, 0.76, 0.32); // var: { "type": "col", "name": "Gold Color", "desc": "Gilding of the Eye of Horus" }
const vec3 PaintColor = vec3(0.13, 0.27, 0.62); // var: { "type": "col", "name": "Paint Color", "desc": "Lapis blue of the paintings (wings, medallion, frieze)" }
const float PaintAmount = 0.85; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Paint", "desc": "Strength of the colored paintings on the top block", "scale": 100, "unit": "%" }
const bool GoldEye = true; // var: { "name": "Golden Eye", "desc": "Gilded, glistening Eye of Horus (off = painted)" }
const bool Glyphs = true; // var: { "name": "Hieroglyphs", "desc": "Carved hieroglyphs on the obelisks" }
const bool Vines = true; // var: { "name": "Vines", "desc": "Vines with leaves curling around the stone" }
const vec3 LeafColor = vec3(0.26, 0.48, 0.13); // var: { "type": "col", "name": "Leaf Color", "desc": "Color of the green leaves on the vines" }
const bool Grass = true; // var: { "name": "Grass", "desc": "Dry swaying grass at the obelisk bases" }
const vec3 GrassColor = vec3(0.72, 0.6, 0.33); // var: { "type": "col", "name": "Grass Color", "desc": "Color of the dry grass" }
const float WindAmount = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Wind", "desc": "How strongly leaves, grass and dust sway in the wind", "unit": "x" }
const float DebrisAmount = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Debris", "desc": "Amount of small stones and dust falling from the top", "scale": 100, "unit": "%" }
const vec3 VineColor = vec3(0.34, 0.27, 0.2); // var: { "type": "col", "name": "Vine Color", "desc": "Color of the dead vines" }
const float Sunlight = 1.0; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Sunlight", "desc": "Brightness of the sunlight", "unit": "x" }
const float ShadowAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadows", "desc": "Strength of the cast shadows", "scale": 100, "unit": "%" }
const float GrainScale = 1.0; // var: { "min": 0.5, "max": 3.0, "step": 0.05, "name": "Grain Scale", "desc": "Size of the stone texture and layers", "unit": "x" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define SunAngle mix(-60.0, 60.0, iMouse.z)
#define Weathering mix(0.0, 1.0, iMouse.w)


const vec3 RedOchre = vec3(0.62, 0.2, 0.1);
const vec3 Turquoise = vec3(0.1, 0.5, 0.45);
const vec3 YellowOchre = vec3(0.85, 0.6, 0.2);
const vec3 InkColor = vec3(0.08, 0.06, 0.05);
float S = 1.0;      // inset (unit size)
vec2 HS = vec2(1.0);
float RAD = 0.0;
float W = 0.5;
vec2 E = vec2(1.0); // half screen
float MG = 10.0;    // margin of the stone to the screen edge
float RADO = 0.0;   // outer corner radius
vec2 L2 = vec2(0.0, 1.0);
vec3 L3 = vec3(0.0, 0.6, 0.8);

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

float fbm2Rolled(vec2 p, int octaves) {
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amp = 0.5;
    int Z = min(iFrame, 0);
    for (int i = Z; i < octaves; i++) {
        sum += amp * EwValueNoise2D(p);
        p = m * p;
        amp *= 0.5;
    }
    return sum;
}

// ---------------------------------------------------------------- pieces (px)
// broken, chipped outer outline (>= 0: eats into the stone)
float outerClip(vec2 p) {
    float sz = max(S, 24.0);
    vec2 u = p / sz;
    float n1 = EwValueNoise2D(u * 1.6 + 11.3);
    float n2 = 1.0 - abs(2.0 * EwValueNoise2D(u * 4.7 + 2.1) - 1.0);
    float n3 = EwValueNoise2D(u * 12.0 + 7.7);
    float chip = smoothstep(0.55, 0.75, EwValueNoise2D(u * 0.8 + 5.7));
    float jag = S * ((0.05 + 0.16 * W) * n1 + (0.03 + 0.09 * W) * n2 + (0.01 + 0.04 * W) * n3 + 0.3 * W * chip);
    jag *= mix(0.3, 1.0, smoothstep(-E.y + 0.6 * S, -E.y + 1.6 * S, p.y));   // calmer bottom
    return EwSdRoundedBox2D(p, E - MG, RADO) + jag;
}

float sdLintel(vec2 p) {
    float yb = HS.y - 0.75 * S;
    float rug = (EwValueNoise2D(vec2(p.x / S * 1.1, 3.7)) - 0.5) * 0.45 * S * W
              + (1.0 - abs(2.0 * EwValueNoise2D(vec2(p.x / S * 4.3, 8.1)) - 1.0) - 0.5) * 0.12 * S * (0.3 + W);
    return yb + rug - p.y;
}

float sdThreshold(vec2 p) {
    float yt = -HS.y + 0.45 * S;
    float rug = (1.0 - abs(2.0 * EwValueNoise2D(vec2(p.x / S * 2.3, 1.9)) - 1.0) - 0.5) * 0.035 * S * (0.3 + W);
    return p.y - yt + rug;
}

float sdObelisk(vec2 p) {
    float side = p.x < 0.0 ? -1.0 : 1.0;
    float lx = p.x - side * HS.x;
    float y0 = -HS.y + 0.45 * S;
    float yP = y0 + 0.35 * S;
    float yS = HS.y - 0.45 * S;
    float yT = yS + 0.55 * S;
    float plinth = EwSdBox2D(vec2(lx, p.y - 0.5 * (y0 + yP)), vec2(0.8 * S, 0.5 * (yP - y0)));
    float t = clamp((p.y - yP) / max(yS - yP, 1.0), 0.0, 1.0);
    float hw = mix(0.64, 0.47, t) * S;
    float shaft = max(abs(lx) - hw, max(yP - 0.02 * S - p.y, p.y - yS));
    float b = 0.52 * S;
    float h = yT - yS;
    float tri = (abs(lx) * h + (p.y - yS) * b - b * h) / sqrt(h * h + b * b);
    float pyr = max(tri, yS - 0.01 * S - p.y);
    float d = min(min(plinth, shaft), pyr);
    d += (1.0 - abs(2.0 * EwValueNoise2D(p / max(S, 24.0) * 3.1 + 4.4) - 1.0) - 0.5) * 0.05 * S * W;
    return d;
}

float JMAX = 1.0;   // upper bound of the outline jag (set in mainImage)

vec3 allBase(vec2 p) { return vec3(sdLintel(p), sdThreshold(p), sdObelisk(p)); }

float baseSd(int id, vec2 p) {
    if (id == 1) return sdLintel(p);
    if (id == 2) return sdThreshold(p);
    return sdObelisk(p);
}

// unified piece distance (lazy: skips outerClip where b >= 0.5 or rb + JMAX < max(b, 0.0))
float pieceSd(int id, vec2 p, bool lazy) {
    float b = baseSd(id, p);
    if (lazy && b >= 0.5) return b;
    float rb = EwSdRoundedBox2D(p, E - MG, RADO) + JMAX;
    if (lazy ? (rb < max(b, 0.0)) : (rb <= b)) return b;
    return max(b, outerClip(p));
}

// ---------------------------------------------------------------- carvings
float glyphSd(vec2 q, float h) {
    float k = floor(h * 6.0);
    float d;
    if (k < 1.0) {          // sun ring
        d = abs(length(q) - 0.2) - 0.06;
        d = min(d, length(q) - 0.05);
    } else if (k < 2.0) {   // ankh
        d = abs(length((q - vec2(0.0, 0.2)) * vec2(1.0, 0.8)) - 0.11) - 0.05;
        d = min(d, EwSdSegment2D(q, vec2(0.0, 0.06), vec2(0.0, -0.38)) - 0.055);
        d = min(d, EwSdSegment2D(q, vec2(-0.21, 0.02), vec2(0.21, 0.02)) - 0.05);
    } else if (k < 3.0) {   // water
        float zz = abs(fract(q.x * 4.0) - 0.5) * 0.18;
        d = min(abs(q.y - 0.1 - zz), abs(q.y + 0.12 - zz)) - 0.045;
        d = max(d, abs(q.x) - 0.36);
    } else if (k < 4.0) {   // eye
        d = abs(length(q * vec2(1.0, 2.0)) - 0.3) / 1.6 - 0.04;
        d = min(d, length(q) - 0.08);
        d = min(d, EwSdSegment2D(q, vec2(0.0, -0.15), vec2(-0.12, -0.36)) - 0.04);
    } else if (k < 5.0) {   // feather
        d = abs(length(q * vec2(2.5, 1.0)) - 0.38) / 1.7 - 0.04;
        d = min(d, EwSdSegment2D(q, vec2(0.0, -0.4), vec2(0.0, 0.3)) - 0.035);
    } else {                // offering bars
        d = min(EwSdBox2D(q - vec2(0.0, 0.14), vec2(0.3, 0.06)),
                EwSdBox2D(q + vec2(0.04, 0.14), vec2(0.18, 0.06)));
        d = min(d, length(q - vec2(0.2, -0.14)) - 0.07);
    }
    return d;
}

// winged medallion on the lintel (px, raised)
float sdSunRelief(vec2 p) {
    vec2 r = p - vec2(0.0, HS.y - 0.04 * S);
    float disc = length(r) - 0.59 * S;
    float ax = abs(r.x);
    float wx = clamp((ax - 0.59 * S) / (2.7 * S), 0.0, 1.0);
    float top = 0.26 * S - 0.18 * S * wx * wx;
    float bot = top - 0.6 * S * pow(1.0 - wx, 0.7) - 0.06 * S;
    float wing = max(max(r.y - top, bot - r.y), max(0.3 * S - ax, ax - 3.3 * S));
    return min(disc, wing);
}

// Eye of Horus (wedjat), q in eye units (the eye is ~1.7 units wide)
float sdHorus(vec2 q) {
    float w = 0.5, h = 0.19;
    float r = (w * w + h * h) / (2.0 * h);
    float c = r - h;
    float alm = max(length(q - vec2(0.0, -c)) - r, length(q - vec2(0.0, c)) - r);
    float d = abs(alm) - 0.045;
    d = min(d, length(q) - 0.15);                                                         // pupil
    d = min(d, max(abs(length(q - vec2(0.0, -0.62)) - 0.97) - 0.055, abs(q.x - 0.03) - 0.6)); // brow
    d = min(d, EwSdSegment2D(q, vec2(0.48, -0.01), vec2(1.02, -0.01)) - 0.045);             // tail
    d = min(d, EwSdSegment2D(q, vec2(-0.12, -0.19), vec2(-0.12, -0.6)) - 0.045);            // tear
    d = min(d, EwSdSegment2D(q, vec2(-0.12, -0.6), vec2(-0.24, -0.64)) - 0.04);
    d = min(d, EwSdSegment2D(q, vec2(0.12, -0.18), vec2(0.42, -0.5)) - 0.04);               // curl
    d = min(d, abs(length(q - vec2(0.3, -0.58)) - 0.11) - 0.035);
    return d;
}

// ---------------------------------------------------------------- wind, vines, grass, debris
float TW = 0.0;     // wrapped animation time (set in mainImage)

float wind(float x) {
    return 0.55 * sin(TW * 1.3 + x * 0.004) + 0.3 * sin(TW * 2.7 + x * 0.011 + 1.7) + 0.15 * sin(TW * 5.3 + x * 0.023);
}

// distance to a wavy strand x = x0 + A sin(w y + ph), y in [ya, yb]; .y = cos of the phase (> 0 = front)
vec2 strandY(vec2 p, float x0, float ya, float yb, float A, float w, float ph) {
    float y = clamp(p.y, ya, yb);
    float s = sin(w * y + ph), c = cos(w * y + ph);
    float dx = abs(p.x - x0 - A * s) / sqrt(1.0 + A * A * w * w * c * c);
    return vec2(length(vec2(dx, p.y - y)), c);
}

// radius of the obelisk (plinth / shaft) at height y, for the wrapping vines
float obeliskA(float y) {
    float y0 = -HS.y + 0.45 * S;
    float yP = y0 + 0.35 * S;
    float yS = HS.y - 0.45 * S;
    float tq = clamp((y - yP) / max(yS - yP, 1.0), 0.0, 1.0);
    return (mix(0.8, mix(0.64, 0.47, tq), smoothstep(yP - 0.08 * S, yP + 0.08 * S, y)) + 0.03) * S;
}

float sdLeaf(vec2 q, float len, float wid) {
    float t = clamp(q.y / len, 0.0, 1.0);
    vec2 e = vec2(q.x / (wid * (1.15 - 0.75 * t)), (q.y - 0.5 * len) / (0.5 * len));
    return (length(e) - 1.0) * wid * 0.8;
}

// one vine (strand + leaves) in local coords q (q.y along the vine); A0 < 0: obelisk radius
void vineWrap(vec2 q, float ya, float yb, float A0, float w, float ph, float th0, float seed, float leaves, float wrap, inout vec3 best) {
    float A = A0 > 0.0 ? A0 : obeliskA(q.y);
    vec2 sc = strandY(q, 0.0, ya, yb, A, w, ph);
    float tt = clamp((q.y - ya) / max(yb - ya, 1.0), 0.0, 1.0);
    float th = th0 * (1.0 - 0.6 * tt) + 0.6;
    float dd = sc.x - th + wrap * smoothstep(0.15, -0.2, sc.y) * (th + 0.1 * S);   // hidden behind the stone
    if (dd < best.x) best = vec3(dd, th, 0.0);
    float cs = 0.2 * S + 4.0;
    for (int j = min(iFrame, 0); j < 3; j++) {
        float ci = floor(q.y / cs) + float(j) - 1.0;
        float yc = (ci + 0.5) * cs;
        float h = EwHash2To1(vec2(ci, seed));
        if (h > leaves || yc < ya + 0.1 * S || yc > yb) continue;
        float ang0 = w * yc + ph;
        if (wrap > 0.5 && cos(ang0) < 0.2) continue;
        float Ac = A0 > 0.0 ? A0 : obeliskA(yc);
        vec2 a = vec2(Ac * sin(ang0), yc);
        float ang = (fract(h * 7.1) > 0.5 ? 1.0 : -1.0) * (0.7 + 1.2 * fract(h * 3.3));
        ang += WindAmount * (0.3 * wind(seed * 37.0 + yc) + 0.12 * sin(TW * 5.0 + h * 40.0));
        vec2 dir = vec2(sin(ang), cos(ang));
        vec2 lq = q - a;
        lq = vec2(dot(lq, vec2(dir.y, -dir.x)), dot(lq, dir));
        float len = (0.13 + 0.09 * fract(h * 5.9)) * S + 3.0;
        float dl = sdLeaf(lq, len, 0.38 * len);
        if (dl < best.x) best = vec3(dl, 0.38 * len, fract(h * 9.7) > 0.3 ? 2.0 : 1.0);
    }
}

// (distance px, half width px, type 0 stem / 1 dry leaf / 2 green leaf)
vec3 vineSd(vec2 p) {
    vec3 best = vec3(1e5, 1.0, 0.0);
    float side = p.x < 0.0 ? -1.0 : 1.0;
    vec2 lp = vec2(p.x - side * HS.x, p.y);
    float ya0 = -HS.y + 0.5 * S;
    float yS = HS.y - 0.45 * S;
    float lb = HS.y - 0.75 * S, lt = E.y - MG;
    int Z = min(iFrame, 0);
    for (int i = Z; i < 4; i++) {
        vec2 q;
        float ya, yb, A0, w, ph, th0, seed, leaves, wrap;
        if (i < 2) {
            if (abs(lp.x) >= 1.2 * S) continue;
            float fi = float(i);
            q = lp;
            ya = ya0;
            yb = mix(ya0, yS, (side < 0.0 ? 0.85 : 0.6) * (1.0 - 0.3 * fi));
            A0 = -1.0;
            w = 4.6 / S * (1.0 + 0.2 * fi) * (fi > 0.5 ? -1.0 : 1.0);
            ph = side * 1.3 + fi * 2.6;
            th0 = (0.042 - 0.012 * fi) * S;
            seed = side * 3.7 + fi * 11.0;
            leaves = 0.8;
            wrap = 1.0;
        } else if (i == 2) {
            if (!(p.y > lb - 0.4 * S && p.x < -0.2 * HS.x)) continue;
            q = vec2(p.y - 0.5 * (lb + lt), p.x);
            ya = -HS.x + 0.7 * S;
            yb = -0.3 * HS.x;
            A0 = 0.5 * (lt - lb) - 0.05 * S;
            w = 2.3 / S;
            ph = 0.7;
            th0 = 0.04 * S;
            seed = 21.0;
            leaves = 0.6;
            wrap = 1.0;
        } else {
            if (!(p.y < -HS.y + 0.9 * S && p.x > 0.2 * HS.x)) continue;
            q = vec2(p.y - (-HS.y + 0.47 * S), -p.x);
            ya = -(HS.x - 0.6 * S);
            yb = -0.35 * HS.x;
            A0 = 0.06 * S;
            w = 2.2 / S;
            ph = 1.0;
            th0 = 0.035 * S;
            seed = 33.0;
            leaves = 0.5;
            wrap = 0.0;
        }
        vineWrap(q, ya, yb, A0, w, ph, th0, seed, leaves, wrap, best);
    }
    return best;
}

// swaying dry grass tufts at the obelisk bases (premultiplied), layers l0 .. l1 - 1 (0 = behind the stone)
vec4 grass(vec2 p, int l0, int l1) {
    vec4 res = vec4(0.0);
    float side = p.x < 0.0 ? -1.0 : 1.0;
    float lx = p.x - side * HS.x;
    float g0 = -HS.y + 0.45 * S;      // ground line (threshold top)
    float v = p.y - g0;
    if (abs(lx) > 2.0 * S || v > 1.75 * S || v < -0.08 * S) return res;
    float cw = 0.1 * S + 3.0;
    float kw = WindAmount * 0.3;
    // shift the cell search by the average lean + wind bend at this height
    float leanC = clamp(lx / S, -1.0, 1.0) * 0.45;
    float xs = p.x - (leanC + kw * wind(p.x)) * v * v / (0.85 * S);
    int Z = min(iFrame, 0);
    for (int L = l0 + Z; L < l1; L++) {
        float fl = float(L);
        for (int j = Z; j < 9; j++) {
            float ci = floor(xs / cw - 0.37 * fl) + float(j) - 4.0;
            vec3 h3 = EwHash2To3(vec2(ci, 3.1 + fl * 7.0));
            float h = h3.x;
            float x0 = (ci + 0.37 * fl + 0.1 + 0.8 * h) * cw;
            float dens = (1.0 - smoothstep(0.5 * S, 1.6 * S, abs(x0 - side * HS.x)))
                       * smoothstep(E.x - MG, E.x - MG - 0.25 * S, abs(x0));
            if (h3.y > dens * 0.97) continue;
            float H = (0.6 + 0.95 * h3.z) * S * (0.55 + 0.45 * dens) * (L == 0 ? 0.85 : 1.0);
            if (v > H + 0.1 * S) continue;
            float u = clamp(v / H, 0.0, 1.0);
            // lean away from the tuft center + wind sway + flutter, curving more toward the tip
            float lean = clamp((x0 - side * HS.x) / S, -1.0, 1.0) * 0.45 + (fract(h * 3.9) - 0.5) * 0.35;
            float sway = kw * (wind(x0) + 0.3 * sin(TW * 3.1 + h * 30.0));
            float bend = (lean + sway) * H;
            float bx = x0 + bend * u * u;
            float slope = 2.0 * bend * u / H;
            float hwid = (0.026 * S + 1.0) * (1.0 - 0.85 * u) + 0.3;
            float dd = abs(p.x - bx) / sqrt(1.0 + slope * slope) - hwid;
            dd = max(dd, v - H);
            // seed head on some blades
            bool seedy = fract(h * 23.1) > 0.6;
            if (seedy) {
                vec2 tip = vec2(x0 + bend, g0 + H);
                vec2 dir = normalize(vec2(slope, 1.0) + vec2(0.0, 0.001));
                vec2 q = p - tip;
                q = vec2(dot(q, vec2(dir.y, -dir.x)), dot(q, dir));
                float sl = 0.09 * S + 3.0;
                float ds = (length(q / vec2(0.022 * S + 1.0, sl)) - 1.0) * (0.022 * S + 1.0);
                dd = min(dd, ds);
            }
            // fade in over the lowest 8 % of the blade (rooted in the ground)
            float cov = clamp(0.5 - dd, 0.0, 1.0) * smoothstep(0.0, 0.08, v / H);
            if (cov > 0.0) {
                vec3 alb = mix(GrassColor * 1.1, GrassColor * vec3(0.8, 0.72, 0.6), fract(h * 21.3));
                alb = mix(alb, LeafColor, 0.25 * fract(h * 17.1) * (1.0 - u) * (1.0 - u));
                // rounded blade normal, lit like the stone
                float s = clamp((p.x - bx) / max(hwid, 0.5), -1.0, 1.0);
                vec3 n = normalize(vec3(s * 0.7 - slope * 0.25, 0.35, 0.8));
                float diff = max(dot(n, L3), 0.0);
                float ao = mix(0.45, 1.0, smoothstep(0.0, 0.5, u)) * (L == 0 ? 0.7 : (L == 1 ? 0.88 : 1.0));
                vec3 c = alb * (vec3(0.6, 0.62, 0.72) * 0.5 + vec3(1.0, 0.88, 0.7) * 1.6 * Sunlight * (0.15 + 0.85 * diff)) * ao;
                float c2 = cov * (2.0 - cov);
                res = over(vec4(c, 1.0) * c2, res);
            }
        }
    }
    return res;
}

// small stones and dust falling from the lintel (premultiplied)
vec4 debris(vec2 p) {
    vec4 res = vec4(0.0);
    float lb = HS.y - 0.75 * S;
    float fallH = 2.3 * S;
    if (p.y > lb + 0.1 * S || p.y < lb - fallH - 0.2 * S || abs(p.x) > HS.x - 0.6 * S) return res;
    int Z = min(iFrame, 0);
    for (int L = Z; L < 4; L++) {
        float fl = float(L);
        bool dust = L >= 2;
        float cw = (dust ? 0.55 + 0.2 * (fl - 2.0) : 0.35 + 0.15 * fl) * S + 4.0;
        float ci = floor(p.x / cw);
        vec3 h = EwHash2To3(vec2(ci, fl * 5.1 + 2.0));
        if (h.z > DebrisAmount * (dust ? 1.1 : 0.9)) continue;
        float per = (dust ? 6.0 : 3.5) + 5.0 * h.y;
        float ph = fract(TW / per + h.x);
        float act = dust ? 0.7 : 0.35;
        if (ph > act) continue;
        float u = ph / act;
        float fade = smoothstep(0.0, 0.08, u) * (1.0 - smoothstep(0.7, 1.0, u));
        float xs = (ci + 0.4 + 0.2 * fract(h.x * 7.3)) * cw;
        if (dust) {
            // a small cloud of grains, spreading while it falls
            vec2 c = vec2(xs + 0.05 * S * sin(u * 7.0 + h.y * 6.0) + 0.07 * S * u * WindAmount * wind(xs), lb + 0.02 * S - fallH * u);
            float r = (0.04 + 0.06 * u) * S + 2.0;
            vec2 dq = p - c;
            float blob = exp(-dot(dq, dq) / (r * r));
            float grains = smoothstep(0.55, 0.85, EwValueNoise2D(dq / (0.012 * S + 1.0) + h.xy * 50.0));
            float a = blob * (0.12 + 0.6 * grains) * (1.0 - 0.5 * u) * fade;
            res = over(vec4(StoneColor * 1.35 * Sunlight, 1.0) * a, res);
        } else {
            vec2 c = vec2(xs, lb + 0.02 * S - fallH * (0.7 * u * u + 0.3 * u));
            float r = (0.018 + 0.022 * h.y) * S + 1.0;
            float an = u * (8.0 + 10.0 * h.y) + h.x * 6.0;
            vec2 q = mat2(cos(an), sin(an), -sin(an), cos(an)) * (p - c);
            float dd = EwSdBox2D(q, vec2(r, r * 0.7)) - 0.2 * r;
            float a = clamp(0.5 - dd, 0.0, 1.0) * fade;
            float fl2 = 0.5 + 0.5 * sin(an * 2.0);
            vec4 chip = vec4(mix(StrataColor, StoneColor, 0.5) * (0.35 + 1.2 * fl2 * Sunlight), 1.0) * a;
            // dust trail
            vec2 tq = p - c;
            float tr = exp(-tq.x * tq.x / (r * r * 3.0)) * smoothstep(0.0, 0.1 * S, tq.y) * (1.0 - smoothstep(0.15 * S, 0.7 * S, tq.y)) * 0.22 * fade;
            res = over(chip, over(vec4(StoneColor * 1.3 * Sunlight, 1.0) * tr, res));
        }
    }
    return res;
}

// ---------------------------------------------------------------- shading
vec3 shadePiece(int id, vec2 p, float sd, vec2 g) {
    float bev = 0.16 * S + 1.0;
    float bw = 1.0 - smoothstep(0.0, bev, -sd);

    // sandstone texture
    float seed = float(id) * 13.7;
    vec2 q = p / (max(S, 40.0) * GrainScale);
    float f = fbm2Rolled(q * vec2(0.7, 1.8) + seed, 5);
    float strata = 0.5 + 0.5 * sin(q.y * 5.0 + f * 7.0 + seed);
    vec3 alb = mix(StrataColor, StoneColor, 0.3 + 0.7 * smoothstep(0.1, 0.9, strata));
    alb *= 0.88 + 0.24 * f;
    alb *= 0.95 + 0.1 * EwHash2To1(floor(p));
    float pit = smoothstep(0.74, 0.92, EwValueNoise2D(q * 11.0 + seed) * 0.6 + EwValueNoise2D(q * 29.0 + seed) * 0.4) * W;
    alb *= 1.0 - 0.45 * pit;
    float sc = 12.0 / (max(S, 40.0) * GrainScale);
    float hb0 = EwValueNoise2D(p * sc + seed);
    vec2 tilt = vec2(EwValueNoise2D((p + vec2(1.0, 0.0)) * sc + seed) - hb0,
                     EwValueNoise2D((p + vec2(0.0, 1.0)) * sc + seed) - hb0) / sc;
    tilt *= (0.2 + 0.8 * W) * 0.12;

    vec3 n0 = vec3(0.0, 0.0, 1.0);
    float carve = 0.0;      // groove amount
    float gold = 0.0;       // gilding mask
    float glint = 0.0;
    vec3 pc = vec3(0.0);    // paint color
    float pa = 0.0;         // paint amount
    float spark = 0.0;
    float ao = 1.0;
    float offL = max(0.04 * S, 1.2);
    int Z = min(iFrame, 0);

    // Unified glyph evaluation state (1 glyphSd call site for both lintel frieze & obelisk shaft)
    int gPass = 0; // 1 = lintel frieze, 2 = obelisk shaft
    vec2 gPos = vec2(0.0);
    float gHash = 0.0, gScale = 1.0, gFm = 0.0;
    float friezeLine = 0.0;

    if (id == 1) {
        // big rugged block: cracks, painted frieze, winged medallion with the Eye of Horus
        float cn = EwValueNoise2D(q * vec2(0.9, 1.7) + 4.2);
        float crack = (1.0 - smoothstep(0.0, 0.025, abs(cn - 0.5))) * smoothstep(0.45, 0.7, EwValueNoise2D(q * 0.8 + 9.0));
        ao *= 1.0 - 0.55 * crack * W;
        vec2 r = p - vec2(0.0, HS.y - 0.04 * S);
        float ax = abs(r.x);
        float by = abs(r.y);
        if (ax > 3.45 * S) {
            // frieze: colored block borders + painted symbols
            float bord = smoothstep(0.275 * S, 0.285 * S, by) * (1.0 - smoothstep(0.355 * S, 0.365 * S, by));
            float bx = r.x / (0.13 * S);
            float bi = mod(floor(bx), 4.0);
            vec3 bc = bi < 1.0 ? RedOchre : (bi < 2.0 ? PaintColor : (bi < 3.0 ? Turquoise : YellowOchre));
            float sep = smoothstep(0.0, 0.1, fract(bx)) * smoothstep(1.0, 0.9, fract(bx));
            pc = mix(InkColor, bc, sep);
            pa = bord;
            float w = 0.6 * S;
            float kc = floor(r.x / w);
            float cxk = (kc + 0.5) * w;
            if (abs(cxk) > 3.8 * S && by < 0.27 * S) {
                gHash = EwHash2To1(vec2(kc, 5.3));
                gPos = vec2(r.x - cxk, r.y) / (0.5 * S);
                gScale = 0.5 * S;
                gPass = 1;
            }
            float line = max(exp(-pow((by - 0.27 * S) / (0.008 * S + 0.5), 2.0)), exp(-pow((by - 0.37 * S) / (0.008 * S + 0.5), 2.0)));
            line = max(line, exp(-pow((ax - 3.48 * S) / (0.008 * S + 0.5), 2.0)) * step(by, 0.37 * S));
            friezeLine = line;
        } else {
            float r0 = 0.0, r1 = 0.0, r2 = 0.0, r3 = 0.0;
            for (int i = Z; i < 4; i++) {
                vec2 o = i == 1 ? L2 : (i == 2 ? -L2 : (i == 3 ? 1.5 * L2 : vec2(0.0)));
                float rv = sdSunRelief(p + o * offL);
                if (i == 0) { r0 = rv; if (r0 >= 0.1 * S) break; }
                else if (i == 1) r1 = rv;
                else if (i == 2) r2 = rv;
                else r3 = rv;
            }
            if (r0 < 0.1 * S) {
                float m = smoothstep(0.6, -0.6, r0);
                float lit = m * smoothstep(-0.5, 0.5, r1);
                float dark = m * smoothstep(-0.5, 0.5, r2);
                float castS = (1.0 - m) * smoothstep(0.5, -0.5, r3);
                ao *= 1.0 + 0.35 * lit - 0.3 * dark - 0.35 * castS;
                float rl = length(r);
                float disc = smoothstep(0.6, -0.6, rl - 0.59 * S);
                // wings: rows of colored feathers
                float wx = clamp((ax - 0.59 * S) / (2.7 * S), 0.0, 1.0);
                float top = 0.26 * S - 0.18 * S * wx * wx;
                float bot = top - 0.6 * S * pow(1.0 - wx, 0.7) - 0.06 * S;
                float v = clamp((top - r.y) / max(top - bot, 1.0), 0.0, 1.0);
                float per = v < 0.3 ? 0.08 : (v < 0.58 ? 0.11 : 0.18);
                vec3 wc = v < 0.3 ? RedOchre : (v < 0.58 ? Turquoise : PaintColor);
                float ff = fract(ax / (per * S));
                float fsep = smoothstep(0.0, 0.14, ff) * smoothstep(1.0, 0.86, ff);
                float rowl = min(abs(v - 0.3), abs(v - 0.58)) * (top - bot);
                fsep *= smoothstep(0.0, 0.02 * S + 0.8, rowl);
                ao *= 1.0 - 0.25 * (1.0 - fsep) * m * (1.0 - disc);
                pc = mix(InkColor, wc, fsep);
                pa = m * (1.0 - disc);
                // medallion: lapis disc, gold rim, gold Eye of Horus
                float rim = smoothstep(0.6, -0.6, abs(rl - 0.555 * S) - 0.035 * S);
                float eu = 0.47 * S;
                vec2 eq = r / eu + vec2(0.22, -0.17);
                float h0 = 0.0, h1 = 0.0, h2 = 0.0, h3 = 0.0;
                for (int i = Z; i < 4; i++) {
                    vec2 o = i == 1 ? L2 : (i == 2 ? -L2 : (i == 3 ? 1.5 * L2 : vec2(0.0)));
                    float hv = sdHorus(eq + o * offL / eu) * eu;
                    if (i == 0) h0 = hv;
                    else if (i == 1) h1 = hv;
                    else if (i == 2) h2 = hv;
                    else h3 = hv;
                }
                float em = smoothstep(0.6, -0.6, h0) * disc;
                float elit = em * smoothstep(-0.5, 0.5, h1);
                float edark = em * smoothstep(-0.5, 0.5, h2);
                float ecast = (1.0 - em) * disc * smoothstep(0.5, -0.5, h3);
                ao *= 1.0 + 0.3 * elit - 0.35 * edark - 0.45 * ecast;
                n0 = normalize(n0 + vec3((elit - edark) * L2 * 0.8, 0.0));
                pc = mix(pc, PaintColor * 0.75, disc);
                pa = max(pa, disc);
                float gm = max(em, rim);
                if (GoldEye) {
                    gold = gm;
                    float ph = fract(iTime * Speed * 0.15);
                    glint = exp(-pow((r.x + 0.6 * r.y - mix(-0.9, 0.9, ph) * S) / (0.1 * S), 2.0));
                    float cz = 0.08 * S + 2.0;
                    vec2 cid = floor(p / cz);
                    vec2 hh = EwHash2To2(cid + 17.0);
                    float tw = pow(max(sin(mod(iTime * Speed * 2.0, 6.2831853 * 16.0) + hh.x * 60.0), 0.0), 30.0) * step(0.5, hh.y);
                    vec2 fq = p - (cid + 0.25 + 0.5 * hh) * cz;
                    float star = exp(-length(fq) / (0.006 * S + 0.6))
                               + 0.6 * exp(-abs(fq.x) / (0.004 * S + 0.4) - abs(fq.y) / (0.03 * S + 1.0))
                               + 0.6 * exp(-abs(fq.y) / (0.004 * S + 0.4) - abs(fq.x) / (0.03 * S + 1.0));
                    spark = tw * star;
                } else {
                    pc = mix(pc, YellowOchre, gm);
                }
            }
        }
    } else if (id == 2) {
        // two stepped threshold courses
        float yb = -HS.y;
        float y0 = yb + 0.45 * S;
        float yl = yb - 0.05 * S;
        float stripU = smoothstep(y0 - 0.15 * S, y0 - 0.12 * S, p.y);
        float stripL = smoothstep(yl - 0.15 * S, yl - 0.12 * S, p.y) * (1.0 - smoothstep(yl - 0.01 * S, yl + 0.01 * S, p.y));
        float strip = max(stripU, stripL);
        n0 = normalize(mix(vec3(0.0, 0.0, 1.0), vec3(0.0, 0.85, 0.5), strip));
        float crease = exp(-pow((p.y - (y0 - 0.135 * S)) / (0.025 * S + 0.5), 2.0)) + exp(-pow((p.y - yl) / (0.03 * S + 0.5), 2.0));
        ao *= 1.0 - 0.4 * crease;
        // block joints
        float course = p.y > yl ? 1.0 : 0.0;
        float c = 2.4 * S;
        float jx = p.x + course * 1.1 * S + 0.3 * S;
        float jd = (0.5 - abs(fract(jx / c) - 0.5)) * c;
        ao *= 0.5 + 0.5 * smoothstep(0.5, 1.6, jd);
    } else {
        // obelisk
        float side = p.x < 0.0 ? -1.0 : 1.0;
        float inner = -side;
        float lx = p.x - side * HS.x;
        float u = lx * inner;
        float y0 = -HS.y + 0.45 * S;
        float yP = y0 + 0.35 * S;
        float yS = HS.y - 0.45 * S;
        float yT = yS + 0.55 * S;
        if (p.y < yP) {
            float fm = smoothstep(-0.7, 0.7, u - 0.7 * S);
            n0 = normalize(mix(vec3(0.0, 0.0, 1.0), vec3(inner * 0.75, 0.0, 0.66), fm));
            float top = smoothstep(yP - 0.1 * S, yP - 0.08 * S, p.y);
            n0 = normalize(mix(n0, vec3(0.0, 0.85, 0.5), top));
            ao *= 0.85 + 0.15 * smoothstep(y0, y0 + 0.15 * S, p.y);
        } else if (p.y < yS) {
            float t = clamp((p.y - yP) / max(yS - yP, 1.0), 0.0, 1.0);
            float hw = mix(0.64, 0.47, t) * S;
            float sw = 0.15 * hw;
            float fm = smoothstep(-0.7, 0.7, u - (hw - sw));
            n0 = normalize(mix(vec3(0.0, 0.0, 1.0), vec3(inner * 0.75, 0.0, 0.66), fm));
            ao *= 0.8 + 0.2 * smoothstep(yP, yP + 0.4 * S, p.y);
            if (Glyphs) {
                float c = 0.5 * S;
                float ys = yP + 0.3 * S;
                float nc = floor((yS - 0.2 * S - ys) / c);
                float ci = floor((p.y - ys) / c);
                if (ci >= 0.0 && ci < nc) {
                    gHash = EwHash2To1(vec2(ci, side * 7.0 + 3.0));
                    gPos = vec2((lx + 0.5 * sw * inner) * 1.15, p.y - ys - (ci + 0.5) * c) / c;
                    gScale = c;
                    gFm = fm;
                    gPass = 2;
                }
            }
        } else {
            float b = 0.52 * S * clamp(1.0 - (p.y - yS) / max(yT - yS, 1.0), 0.0, 1.0);
            float fm = smoothstep(-0.7, 0.7, u - 0.7 * b);
            n0 = normalize(mix(vec3(0.0, 0.5, 0.87), vec3(inner * 0.6, 0.45, 0.66), fm));
        }
    }

    if (gPass > 0) {
        float gd0 = 0.0, gd1 = 0.0, gd2 = 0.0;
        int nG = gPass == 2 ? 3 : 1;
        for (int i = Z; i < 3; i++) {
            if (i >= nG) break;
            vec2 o = i == 1 ? L2 * offL / gScale : (i == 2 ? -L2 * offL / gScale : vec2(0.0));
            float gv = glyphSd(gPos + o, gHash) * gScale;
            if (i == 0) gd0 = gv;
            else if (i == 1) gd1 = gv;
            else gd2 = gv;
        }
        if (gPass == 1) {
            float gm = smoothstep(0.6, -0.6, gd0);
            float hc = fract(gHash * 17.3);
            vec3 mc = hc < 0.25 ? RedOchre : (hc < 0.5 ? PaintColor : (hc < 0.75 ? Turquoise : YellowOchre));
            float ol = smoothstep(0.6, -0.6, abs(gd0) - (0.01 * S + 0.4));
            pc = mix(mc, InkColor, ol);
            pa = max(gm, ol);
        } else {
            float m = smoothstep(0.6, -0.6, gd0) * (1.0 - gFm);
            float sh = m * smoothstep(-0.5, 0.5, gd1);
            float li = m * smoothstep(-0.5, 0.5, gd2);
            carve = m * 0.3 + sh * 0.45 - li * 0.3;
        }
    }
    if (friezeLine > 0.0) {
        pc = mix(pc, InkColor, friezeLine);
        pa = max(pa, friezeLine);
    }

    vec3 n = normalize(n0 + vec3(g * bw * 1.4 - tilt * (1.0 - 0.7 * gold), 0.0));
    float diff = max(dot(n, L3), 0.0);
    vec3 sun = vec3(1.0, 0.88, 0.7) * 1.6 * Sunlight;
    vec3 sky = vec3(0.6, 0.62, 0.72) * 0.45;

    float fade = 1.0 - 0.8 * W * smoothstep(0.35, 0.75, EwValueNoise2D(q * 7.0 + 3.0));
    alb = mix(alb, pc * (0.8 + 0.35 * f), clamp(pa * PaintAmount * fade, 0.0, 1.0));
    vec3 stone = alb * (sky + sun * diff);
    vec3 gcol = GoldColor * (0.18 + 1.1 * diff * Sunlight);
    float spec = pow(max(dot(reflect(-L3, n), vec3(0.0, 0.0, 1.0)), 0.0), 18.0);
    gcol += vec3(1.0, 0.95, 0.8) * spec * 1.2 * Sunlight;
    gcol *= 0.85 + 0.25 * f;
    vec3 col = mix(stone, gcol, gold);
    col += GoldColor * glint * gold * 1.6;
    col += vec3(1.0, 0.95, 0.8) * spark * gold * 2.5;
    col *= ao * (1.0 - carve);
    return col;
}
// side wall of a piece (depth t 0 = front edge .. 1 = back), pp = point on the front outline
vec3 shadeSide(int id, vec2 pp, float t, vec2 g) {
    float seed = float(id) * 13.7;
    vec2 q = pp / (max(S, 40.0) * GrainScale);
    float f = fbm2Rolled(q * vec2(0.7, 1.8) + seed + t * 0.6, 4);
    float strata = 0.5 + 0.5 * sin(q.y * 5.0 + f * 7.0 + seed);
    vec3 alb = mix(StrataColor, StoneColor, 0.3 + 0.7 * smoothstep(0.1, 0.9, strata));
    alb *= 0.85 + 0.3 * f;
    alb *= 1.0 - 0.35 * smoothstep(0.74, 0.92, EwValueNoise2D(q * 11.0 + seed + t * 3.0)) * W;
    // rough wall: tilt the normal a little with noise
    vec3 n = normalize(vec3(g, 0.0) + 0.25 * vec3(f - 0.45, EwValueNoise2D(q * 9.0 + t * 4.0) - 0.5, 0.15));
    float diff = max(dot(n, L3), 0.0);
    vec3 sun = vec3(1.0, 0.88, 0.7) * 1.6 * Sunlight;
    vec3 sky = vec3(0.6, 0.62, 0.72) * 0.32;
    float ao = mix(1.0, 0.35, t * t) * (0.8 + 0.2 * smoothstep(0.0, 0.15, t));
    vec3 col = alb * (sky + sun * diff);
    return col * ao;
}

// ---------------------------------------------------------------- 3D Egyptian crypt
const float CRYPT_BAY = 4.2;
const float CRYPT_PER = 67.2;

vec2 cryptPath(float z) {
    float a = z * (6.2831853 / CRYPT_PER);
    float x = 0.85 * sin(a) + 0.34 * sin(a * 2.0 + 1.1);
    float y = -0.095 * z + 0.14 * sin(a * 2.0 + 0.4);
    return vec2(x, y);
}

// sparse alternating torch placement: bay 1 left, bay 2 right, bay 3 dark, bay 4 left...
float cryptTorchSide(float bay) {
    float m = mod(bay, 3.0);
    return m < 0.5 ? 0.0 : (m < 1.5 ? -1.0 : 1.0);
}

float cryptFlicker(float seed) {
    return 0.84 + 0.11 * sin(TW * 7.3 + seed)
                + 0.07 * sin(TW * 13.7 + seed * 2.1)
                + 0.04 * sin(TW * 23.1 + seed * 3.7);
}

// 10 authentic Egyptian hieroglyphs for the crypt walls
float cryptGlyphSd(vec2 q, float h) {
    float k = floor(h * 10.0);
    float d;
    if (k < 1.0) {          // Ra sun ring
        d = abs(length(q) - 0.23) - 0.055;
        d = min(d, length(q) - 0.065);
    } else if (k < 2.0) {   // Ankh
        d = abs(length((q - vec2(0.0, 0.21)) * vec2(1.0, 0.78)) - 0.115) - 0.048;
        d = min(d, EwSdSegment2D(q, vec2(0.0, 0.06), vec2(0.0, -0.39)) - 0.050);
        d = min(d, EwSdSegment2D(q, vec2(-0.23, 0.03), vec2(0.23, 0.03)) - 0.046);
    } else if (k < 3.0) {   // Djed pillar (stability)
        d = EwSdBox2D(q - vec2(0.0, -0.10), vec2(0.085, 0.30));
        d = min(d, EwSdBox2D(q - vec2(0.0, -0.36), vec2(0.18, 0.045)));
        float gy = clamp(round((q.y - 0.12) / 0.10), -1.0, 2.0);
        d = min(d, EwSdBox2D(q - vec2(0.0, 0.12 + gy * 0.10), vec2(0.22 - 0.015 * gy, 0.028)));
    } else if (k < 4.0) {   // triple water ripples
        float zz = abs(fract(q.x * 4.5) - 0.5) * 0.15;
        d = min(abs(q.y - 0.18 - zz), min(abs(q.y - zz), abs(q.y + 0.18 - zz))) - 0.036;
        d = max(d, abs(q.x) - 0.38);
    } else if (k < 5.0) {   // Wedjat eye
        d = abs(length(q * vec2(1.0, 2.0)) - 0.30) / 1.6 - 0.038;
        d = min(d, length(q) - 0.075);
        d = min(d, EwSdSegment2D(q, vec2(0.0, -0.15), vec2(-0.12, -0.37)) - 0.038);
        d = min(d, EwSdSegment2D(q, vec2(0.10, -0.16), vec2(0.24, -0.34)) - 0.034);
    } else if (k < 6.0) {   // Feather of Ma'at
        d = abs(length((q - vec2(0.03, 0.02)) * vec2(2.4, 1.0)) - 0.36) / 1.65 - 0.038;
        d = min(d, EwSdSegment2D(q, vec2(-0.04, -0.40), vec2(-0.02, 0.34)) - 0.036);
    } else if (k < 7.0) {   // Akhet (horizon & rising sun)
        float mtn = EwSdBox2D(q - vec2(0.0, -0.22), vec2(0.36, 0.14));
        float dip = length(q - vec2(0.0, -0.02)) - 0.18;
        d = max(mtn, -dip);
        d = min(d, length(q - vec2(0.0, 0.06)) - 0.12);
    } else if (k < 8.0) {   // Scarab / winged emblem
        d = length(q * vec2(1.45, 1.0)) - 0.17;
        d = min(d, EwSdSegment2D(vec2(abs(q.x), q.y), vec2(0.12, 0.06), vec2(0.36, 0.16)) - 0.036);
        d = min(d, EwSdSegment2D(vec2(abs(q.x), q.y), vec2(0.12, -0.04), vec2(0.34, -0.14)) - 0.032);
        d = min(d, length(q - vec2(0.0, 0.28)) - 0.07);
    } else if (k < 9.0) {   // Was scepter
        d = EwSdSegment2D(q, vec2(-0.02, -0.38), vec2(-0.02, 0.28)) - 0.042;
        d = min(d, EwSdSegment2D(q, vec2(-0.02, 0.28), vec2(0.20, 0.22)) - 0.042);
        d = min(d, EwSdSegment2D(q, vec2(-0.02, -0.38), vec2(-0.15, -0.28)) - 0.036);
        d = min(d, EwSdSegment2D(q, vec2(-0.02, -0.38), vec2(0.12, -0.28)) - 0.036);
    } else {                // Offering bread & vessel
        d = min(EwSdBox2D(q - vec2(0.0, 0.16), vec2(0.32, 0.055)),
                EwSdBox2D(q - vec2(0.0, -0.14), vec2(0.24, 0.055)));
        d = min(d, length((q - vec2(0.0, 0.01)) * vec2(0.75, 1.3)) - 0.11);
        d = min(d, length(q - vec2(0.22, -0.28)) - 0.065);
    }
    return d;
}

// returns vec3(signed distance, material: 0 stone/sand, 2 bronze sconce, sandMix [0..1])
vec3 cryptMap(vec3 p) {
    vec2 pc = cryptPath(p.z);
    vec3 q = vec3(p.x - pc.x, p.y - pc.y, p.z);
    float ax = abs(q.x);
    float side = q.x >= 0.0 ? 1.0 : -1.0;

    float bayP = floor((q.z + 0.5 * CRYPT_BAY) / CRYPT_BAY);
    float lzP = q.z - bayP * CRYPT_BAY;
    float bayM = floor(q.z / CRYPT_BAY);
    float lzM = q.z - (bayM + 0.5) * CRYPT_BAY;

    // subtle organic hand-hewn stone waviness (breaks mathematical straight lines)
    float wear3D = (sin(q.z * 3.1 + q.y * 2.7) * 0.55 + sin(q.z * 7.3 - q.y * 5.9 + q.x * 4.1) * 0.45) * 0.008 * (0.35 + 0.65 * W);

    // battered Egyptian walls (wider at floor, narrower at ceiling)
    float wWall = 1.92 - 0.13 * (q.y + 1.25);
    float dWall = (wWall - ax) * 0.99 + wear3D;

    // recessed carved wall panel (upper) & recessed papyrus dado zone (lower)
    float panMask = max(abs(lzM) - 1.38, abs(q.y - 0.18) - 0.66);
    dWall += 0.085 * smoothstep(0.05, -0.05, panMask);
    float dadoMask = max(abs(lzM) - 1.40, abs(q.y + 0.85) - 0.24);
    dWall += 0.045 * smoothstep(0.04, -0.04, dadoMask);

    // carved dado rail molding & curved Egyptian cavetto cornice + torus roll along upper wall
    float dDadoRail = max(abs(ax - (wWall - 0.03)) - 0.075, abs(q.y + 0.56) - 0.042) - 0.014;
    float cavT = clamp((q.y - 1.02) / 0.34, 0.0, 1.0);
    float cavOut = 0.05 + 0.16 * cavT * cavT;
    float dCavetto = max(abs(ax - (wWall - cavOut * 0.5)) - cavOut * 0.55, abs(q.y - 1.19) - 0.17) - 0.015;
    float dTorus = length(vec2(ax - (wWall - 0.075), q.y - 1.00)) - 0.042;
    dWall = min(dWall, min(dDadoRail, min(dCavetto, dTorus)));

    // wide worn descending stone terrace steps (2 steps per bay, chamfered risers + edge wear)
    float sf = fract(q.z / 2.1);
    float stepWave = sf - smoothstep(0.84, 1.0, sf);
    float yFloor = -1.25 + 0.20 * (stepWave - 0.42);
    float walkWear = -0.032 * exp(-q.x * q.x * 1.45) + wear3D * 0.6;
    float dFloor = (q.y - (yFloor + walkWear)) * 0.82;

    // 2-tier recessed coffered ceiling between heavy lintel beams
    float cofOuter = max(abs(lzM) - 1.48, ax - 1.24);
    float cofInner = max(abs(lzM) - 1.12, ax - 0.86);
    float ceilH = 1.42 + 0.12 * smoothstep(0.06, -0.06, cofOuter)
                       + 0.10 * smoothstep(0.06, -0.06, cofInner);
    float dCeil = ceilH - q.y + wear3D;

    float dRoom = min(dWall, min(dFloor, dCeil));

    // fluted lotus-bundle pilasters with tiered stepped plinth, torus neck & flared capital
    float flare = smoothstep(0.76, 1.16, q.y);
    float neck = exp(-pow((q.y - 0.74) / 0.045, 2.0)) * 0.032;
    float plinth1 = smoothstep(-0.74, -0.84, q.y) * 0.065;
    float plinth2 = smoothstep(-0.96, -1.04, q.y) * 0.055;
    float flute = smoothstep(-0.68, -0.58, q.y) * smoothstep(0.70, 0.60, q.y) * 0.014 * cos(lzP * 25.0);
    float pExt = 0.26 + flare * flare * 0.16 + neck + plinth1 + plinth2 + flute;
    float pZ = 0.26 + flare * 0.10 + neck * 0.9 + (plinth1 + plinth2) * 0.75;
    vec3 pilP = vec3(ax - (wWall - pExt * 0.5 + 0.06), q.y - 0.06, lzP);
    float dPil = EwSdBox3D(pilP, vec3(pExt * 0.5 + 0.06, 1.44, pZ)) - 0.020 + wear3D * 0.8;

    // stepped overhead ceiling lintel beam with slight weathered chamfer
    float dBeam = max(abs(q.y - 1.33) - 0.21, abs(lzP) - 0.28) - 0.022 + wear3D;
    float dBeamCap = max(abs(q.y - 1.46) - 0.09, abs(lzP) - 0.36) - 0.015;
    float dStone = min(dRoom, min(dPil, min(dBeam, dBeamCap)));

    // 3D fallen stone rubble blocks half-buried in the floor sand near walls
    if (q.y < -0.78 && ax > 0.62) {
        vec3 hr = EwHash2To3(vec2(mod(bayM, 16.0) * 2.0 + (side > 0.0 ? 1.0 : 0.0), 41.3));
        if (hr.z > 0.28) {
            vec3 rPos = vec3(wWall - 0.36 - 0.26 * hr.x, yFloor + 0.07, (hr.y - 0.5) * 2.2);
            vec3 rp = vec3(ax - rPos.x, q.y - rPos.y, lzM - rPos.z);
            float ra = hr.x * 6.28;
            rp.xz = EwRotate2D(ra) * rp.xz;
            rp.xy = EwRotate2D((hr.y - 0.5) * 0.55) * rp.xy;
            vec3 rSz = vec3(0.11 + 0.08 * hr.x, 0.065 + 0.045 * hr.y, 0.09 + 0.07 * hr.z);
            float dRub = EwSdBox3D(rp, rSz) - 0.018 + wear3D * 1.4;
            dStone = min(dStone, dRub);
        }
    }

    // drifted sand banks hugging the lower walls and spilling gently over step edges
    float wallDist = max(0.0, wWall - ax);
    float sandWave = 0.62 + 0.26 * sin(q.z * 0.75 + (q.x > 0.0 ? 0.0 : 2.3))
                          + 0.13 * sin(q.z * 2.1 + 1.1);
    float sandH = 0.28 * exp(-wallDist * 1.95) * sandWave;
    float ySand = yFloor + sandH - 0.068;
    float dSand = (q.y - ySand) * 0.88;

    float dBlend = EwSmoothMin(dStone, dSand, 0.075);
    float sandMix = smoothstep(0.085, -0.04, dSand - dStone);
    vec3 res = vec3(dBlend, 0.0, sandMix);

    // ornate 3D bronze torch sconce mounted ONLY on active torch pilasters
    if (Torches && abs(lzP) < 0.36 && abs(q.y - 0.12) < 0.52 && ax > 0.96) {
        if (cryptTorchSide(bayP) * side > 0.5) {
            vec3 tp = vec3(ax, q.y, lzP);
            // decorative backplate + collar rings + curved arm + fluted brazier cup
            float plt = EwSdBox3D(tp - vec3(1.46, 0.06, 0.0), vec3(0.026, 0.13, 0.062)) - 0.012;
            float brk1 = EwSdCapsule(tp, vec3(1.47, 0.11, 0.0), vec3(1.22, 0.13, 0.0), 0.018);
            float brk2 = EwSdCapsule(tp, vec3(1.47, -0.02, 0.0), vec3(1.25, 0.04, 0.0), 0.016);
            float rod = EwSdCapsule(tp, vec3(1.29, -0.14, 0.0), vec3(1.15, 0.25, 0.0), 0.026);
            float ring = length(vec2(length(tp.xz - vec2(1.19, 0.0)) - 0.036, tp.y - 0.14)) - 0.011;
            float cup = EwSdCapsule(tp, vec3(1.16, 0.21, 0.0), vec3(1.14, 0.30, 0.0), 0.060);
            float rim = length(vec2(length(tp.xz - vec2(1.14, 0.0)) - 0.056, tp.y - 0.30)) - 0.014;
            float dTorch = min(min(min(plt, brk1), min(brk2, rod)), min(min(ring, cup), rim));
            if (dTorch < res.x) res = vec3(dTorch, 2.0, 0.0);
        }
    }
    return res;
}

// returns vec4(height, paintMask, goldMask, ao); outPaintCol = pigment, outExtra = (blockShade, soot/sand)
vec4 cryptSurfaceDetail(vec3 q, vec3 n, out vec3 outPaintCol, out vec2 outExtra) {
    outPaintCol = vec3(0.0);
    outExtra = vec2(1.0, 0.0);
    float side = q.x >= 0.0 ? 1.0 : -1.0;
    float bayP = floor((q.z + 0.5 * CRYPT_BAY) / CRYPT_BAY);
    float lzP = q.z - bayP * CRYPT_BAY;
    float bayM = floor(q.z / CRYPT_BAY);
    float lzM = q.z - (bayM + 0.5) * CRYPT_BAY;
    float bWrap = mod(bayM, 16.0);

    float h = 0.0;
    float paint = 0.0;
    float gold = 0.0;
    float ao = 1.0;
    // single unified glyph evaluation site (fast compile!)
    int glyphMode = 0; // 1 = pilaster, 2 = cartouche raised gold, 3 = panel sunken-relief column, 4 = pilaster front
    vec2 gq = vec2(0.0);
    float hg = 0.0;
    float gMask = 0.0;

    if (abs(n.x) > 0.52) {
        vec2 wp = vec2(q.z * side, q.y);
        // staggered ashlar sandstone courses with beveled block edges & per-block variation
        float row = floor(wp.y / 0.42);
        float bx = (wp.x + mod(row, 2.0) * 0.48) / 0.96;
        float blkH = EwHash2To1(vec2(floor(bx), row + side * 13.0));
        outExtra.x = 0.80 + 0.34 * blkH;
        float fy = fract(wp.y / 0.42);
        float fx = fract(bx);
        float mx = min(fx, 1.0 - fx) * 0.96;
        float my = min(fy, 1.0 - fy) * 0.42;
        float md = min(mx, my);
        float mortar = 1.0 - smoothstep(0.006, 0.025, md);
        float bevel = 1.0 - smoothstep(0.015, 0.055, md);
        h -= 0.28 * mortar + 0.06 * bevel;
        ao *= (1.0 - 0.38 * mortar) * (1.0 - 0.08 * bevel);

        // multi-scale tactile sandstone strata, chisel marks & weathered pits
        float g1 = EwValueNoise2D(wp * vec2(2.6, 7.2) * GrainScale + 3.1);
        float g2 = EwValueNoise2D(wp * 15.0 * GrainScale + 7.4);
        float g3 = EwValueNoise2D(wp * 38.0 * GrainScale + 1.9);
        float chisel = EwValueNoise2D(wp * vec2(26.0, 62.0) * GrainScale + 5.2);
        float pit = smoothstep(0.71, 0.90, g2 * 0.58 + g3 * 0.42) * (0.35 + 0.65 * W);
        h += (g1 * 0.46 + g2 * 0.30 + g3 * 0.14 + chisel * 0.10 - 0.5) * (0.18 + 0.24 * W) - 0.30 * pit;
        ao *= 1.0 - 0.35 * pit;

        // primary & branching hairline cracks
        float cn = EwValueNoise2D(wp * vec2(1.4, 2.4) + 11.2);
        float cn2 = EwValueNoise2D(wp * vec2(3.8, 5.2) + 21.7);
        float crack = (1.0 - smoothstep(0.0, 0.020, abs(cn - 0.5)))
                    * smoothstep(0.38, 0.70, EwValueNoise2D(wp * 0.9 + 5.7)) * (0.30 + 0.70 * W);
        float fineCrk = (1.0 - smoothstep(0.0, 0.014, abs(cn2 - 0.5)))
                      * smoothstep(0.58, 0.82, g1) * (0.20 + 0.65 * W);
        h -= 0.38 * crack + 0.18 * fineCrk;
        ao *= (1.0 - 0.55 * crack) * (1.0 - 0.28 * fineCrk);

        // subtle dark mineral drip streaks from upper cornice
        float drip = smoothstep(0.66, 0.92, EwValueNoise2D(vec2(wp.x * 5.2, wp.y * 0.7) + 4.1))
                   * smoothstep(0.35, 1.10, q.y) * (0.25 + 0.55 * W);
        outExtra.y = drip * 0.28;


        // upper cavetto cornice: traditional Egyptian reed-leaf vertical stripes
        if (q.y > 1.03 && q.y < 1.35) {
            float rz = (q.z * side) / 0.11;
            float ri = mod(floor(rz), 3.0);
            float rf = fract(rz);
            float rSep = smoothstep(0.08, 0.20, rf) * smoothstep(0.92, 0.80, rf);
            h -= 0.24 * (1.0 - rSep);
            ao *= 1.0 - 0.28 * (1.0 - rSep);
            outPaintCol = mix(InkColor, ri < 1.0 ? RedOchre : (ri < 2.0 ? PaintColor : Turquoise), rSep);
            paint = 0.82;
        } else if (abs(lzP) < 0.38) {
            // pilaster: torch soot ONLY if this pilaster has an active torch
            if (Torches && cryptTorchSide(bayP) * side > 0.5) {
                float soot = exp(-lzP * lzP * 20.0) * smoothstep(0.30, 0.78, q.y) * smoothstep(1.38, 0.82, q.y);
                soot *= 0.65 + 0.35 * EwValueNoise2D(wp * 4.5 + 2.0);
                outExtra.y = max(outExtra.y, soot * 0.78);
            }
            if (q.y > 0.74 && q.y < 1.04) {
                // painted lotus capital collar bands & vertical petal ribs
                float band = fract((q.y - 0.74) / 0.075);
                float groove = 1.0 - smoothstep(0.08, 0.22, min(band, 1.0 - band));
                float petal = 1.0 - smoothstep(0.06, 0.16, abs(fract(lzP / 0.085) - 0.5));
                h -= 0.30 * groove + 0.14 * petal;
                ao *= 1.0 - 0.34 * max(groove, petal * 0.6);
                float bi = floor((q.y - 0.74) / 0.075);
                outPaintCol = bi < 1.0 ? PaintColor : (bi < 2.0 ? RedOchre : (bi < 3.0 ? Turquoise : YellowOchre));
                paint = 0.88 * (1.0 - groove);
                if (bi >= 3.0) gold = 0.65 * (1.0 - groove);
            } else if (q.y > -0.72 && q.y < 0.70) {
                gMask = smoothstep(0.18, 0.13, abs(lzP));
                float bord = smoothstep(0.022, 0.0, abs(abs(lzP) - 0.175));
                h -= 0.26 * bord;
                ao *= 1.0 - 0.30 * bord;
                if (Glyphs && gMask > 0.1) {
                    float c = 0.26;
                    float ci = floor((q.y + 0.68) / c);
                    hg = EwHash2To1(vec2(ci, mod(bayP, 16.0) * 5.0 + side * 3.0));
                    gq = vec2(lzP * side * 1.15, fract((q.y + 0.68) / c) - 0.5) / 0.26;
                    glyphMode = 1;
                }
            }
        } else if (abs(lzM) < 1.36 && q.y > -1.08 && q.y < -0.60) {
            // lower carved dado: repeating stylized Egyptian lotus/papyrus stems & blossoms
            float cellW = 0.26;
            float dx = (lzM * side) / cellW;
            vec2 dq = vec2((fract(dx) - 0.5) * cellW, q.y + 0.84);
            float stem = smoothstep(0.018, 0.004, abs(dq.x)) * smoothstep(-0.22, -0.18, dq.y) * smoothstep(0.10, 0.06, dq.y);
            // flared lotus cup at top of stem
            float cupY = dq.y - 0.09;
            float cupW = 0.022 + 0.42 * clamp(cupY + 0.04, 0.0, 0.13);
            float cup = smoothstep(0.014, -0.004, max(abs(dq.x) - cupW, abs(cupY - 0.025) - 0.065));
            float leaf = smoothstep(0.012, -0.004, abs(abs(dq.x) - 0.055 * (1.0 - abs(dq.y + 0.14) * 4.5)) - 0.008) * step(dq.y, -0.04);
            float dadoCarve = max(max(stem, cup), leaf);
            h -= 0.30 * dadoCarve;
            ao *= 1.0 - 0.36 * dadoCarve;
            if (dadoCarve > 0.1) {
                outPaintCol = cup > 0.4 ? Turquoise : RedOchre;
                paint = max(paint, 0.55 * dadoCarve);
            }
        } else if (abs(lzM) < 1.34 && q.y > -0.48 && q.y < 0.84) {
            // upper carved wall panel: painted Egyptian block frieze + winged centerpiece + cartouche + sunken-relief glyphs
            if (q.y > 0.67 && q.y < 0.82) {
                float fz = (lzM * side + 1.35) / 0.13;
                float bi = mod(floor(fz), 4.0);
                float sep = smoothstep(0.06, 0.18, fract(fz)) * smoothstep(0.94, 0.82, fract(fz));
                outPaintCol = mix(InkColor, bi < 1.0 ? RedOchre : (bi < 2.0 ? PaintColor : (bi < 3.0 ? Turquoise : YellowOchre)), sep);
                paint = 0.92;
                h -= 0.22 * (1.0 - sep);
                ao *= 1.0 - 0.25 * (1.0 - sep);
            }
            float hLine = max(smoothstep(0.020, 0.0, abs(q.y - 0.66)),
                              max(smoothstep(0.020, 0.0, abs(q.y - 0.82)),
                                  smoothstep(0.020, 0.0, abs(q.y + 0.46))));
            h -= 0.30 * hLine;
            ao *= 1.0 - 0.42 * hLine;

            vec2 cp = vec2(lzM * side, q.y - 0.09);
            if (abs(cp.x) < 0.46 && abs(cp.y) < 0.54) {
                // braided double-ring royal cartouche
                float dCart = EwSdRoundedBox2D(cp, vec2(0.30, 0.40), 0.22);
                float braid = 0.5 + 0.5 * sin((cp.x + cp.y) * 58.0);
                float cRing = smoothstep(0.036, 0.008, abs(dCart));
                float cInnerRing = smoothstep(0.018, 0.004, abs(dCart + 0.048));
                float cBar = smoothstep(0.026, 0.006, EwSdBox2D(cp - vec2(0.0, -0.45), vec2(0.25, 0.020)));
                float cIn = smoothstep(0.015, -0.02, dCart + 0.04);
                h += (0.14 * braid - 0.46) * cRing - 0.28 * max(cInnerRing, cBar) - 0.12 * cIn;
                ao *= 1.0 - 0.44 * max(cRing, cBar);
                if (cIn > 0.05) {
                    outPaintCol = PaintColor * 0.72;
                    paint = max(paint, 0.76 * cIn);
                    if (mod(bWrap, 2.0) < 0.5) {
                        vec2 eq = cp / 0.25 + vec2(0.18, -0.14);
                        float hd = sdHorus(eq);
                        float em = smoothstep(0.042, -0.032, hd) * cIn;
                        h += 0.42 * em;
                        gold = max(gold, em * (GoldEye ? 0.98 : 0.0));
                        if (!GoldEye) { outPaintCol = mix(outPaintCol, YellowOchre, em); paint = max(paint, em); }
                    } else if (Glyphs) {
                        float gy = (cp.y + 0.33) / 0.22;
                        float gi = clamp(floor(gy), 0.0, 2.0);
                        gq = vec2(cp.x / 0.24, (cp.y - (-0.22 + gi * 0.22)) / 0.22);
                        hg = EwHash2To1(vec2(gi + 1.0, bWrap * 3.7 + side * 2.1));
                        gMask = cIn;
                        glyphMode = 2;
                    }
                }
                gold = max(gold, max(cRing * (0.45 + 0.35 * braid), cBar * 0.65));
            } else if (Glyphs && q.y > -0.44 && q.y < 0.63 && abs(lzM) > 0.46 && abs(lzM) < 1.30) {
                float gx = (lzM * side) / 0.26;
                float gy = (q.y + 0.44) / 0.25;
                float colDiv = smoothstep(0.055, 0.0, abs(fract(gx) - 0.5) - 0.445);
                h -= 0.24 * colDiv;
                ao *= 1.0 - 0.34 * colDiv;
                hg = EwHash2To1(vec2(floor(gx) + bWrap * 11.0, floor(gy) + side * 7.0));
                gq = vec2(fract(gx) - 0.5, fract(gy) - 0.5) * 1.06;
                gMask = 1.0;
                glyphMode = 3;
            }
        }
    } else if (n.y > 0.5) {
        // worn stone floor slabs with sand-filled mortar joints, slab cracks & chisel wear
        vec2 fp = vec2(q.x, q.z);
        float row = floor(fp.y / 0.70);
        float bx = (fp.x + mod(row, 2.0) * 0.42) / 0.84;
        outExtra.x = 0.78 + 0.36 * EwHash2To1(vec2(floor(bx), row + 5.0));
        float fx = fract(bx);
        float fy = fract(fp.y / 0.70);
        float md = min(min(fx, 1.0 - fx) * 0.84, min(fy, 1.0 - fy) * 0.70);
        float mortar = 1.0 - smoothstep(0.008, 0.036, md);
        float g1 = EwValueNoise2D(fp * 5.2 * GrainScale + 2.3);
        float g2 = EwValueNoise2D(fp * 17.5 * GrainScale + 6.1);
        float g3 = EwValueNoise2D(fp * 42.0 * GrainScale + 1.4);
        float pit = smoothstep(0.73, 0.91, g2 * 0.65 + g3 * 0.35) * (0.35 + 0.65 * W);
        float fCrk = (1.0 - smoothstep(0.0, 0.020, abs(EwValueNoise2D(fp * 2.2 + 7.7) - 0.5))) * (0.3 + 0.7 * W);
        h = (g1 * 0.5 + g2 * 0.32 + g3 * 0.18 - 0.5) * (0.18 + 0.22 * W) - 0.32 * mortar - 0.28 * pit - 0.30 * fCrk;
        ao *= (1.0 - 0.38 * mortar) * (1.0 - 0.28 * pit) * (1.0 - 0.42 * fCrk);
        outExtra.y = max(mortar * 0.62, fCrk * 0.35);
    } else if (abs(n.z) > 0.52) {
        // front-facing vertical faces of pilasters, stepped plinths & overhead lintel beams
        vec2 fp = vec2(q.x, q.y);
        float row = floor(fp.y / 0.42);
        float bx = (fp.x + mod(row, 2.0) * 0.40) / 0.80;
        outExtra.x = 0.80 + 0.34 * EwHash2To1(vec2(floor(bx), row + mod(bayP, 16.0) * 7.0));
        float fx = fract(bx);
        float fy = fract(fp.y / 0.42);
        float md = min(min(fx, 1.0 - fx) * 0.80, min(fy, 1.0 - fy) * 0.42);
        float mortar = 1.0 - smoothstep(0.006, 0.025, md);
        float g1 = EwValueNoise2D(fp * vec2(2.8, 7.0) * GrainScale + 3.7);
        float g2 = EwValueNoise2D(fp * 16.0 * GrainScale + 8.1);
        float g3 = EwValueNoise2D(fp * 38.0 * GrainScale + 2.3);
        float pit = smoothstep(0.71, 0.90, g2 * 0.6 + g3 * 0.4) * (0.35 + 0.65 * W);
        float cn = EwValueNoise2D(fp * vec2(1.8, 2.6) + 13.4);
        float crack = (1.0 - smoothstep(0.0, 0.020, abs(cn - 0.5))) * (0.30 + 0.70 * W);
        h = (g1 * 0.5 + g2 * 0.32 + g3 * 0.18 - 0.5) * (0.18 + 0.24 * W) - 0.28 * mortar - 0.28 * pit - 0.34 * crack;
        ao *= (1.0 - 0.36 * mortar) * (1.0 - 0.32 * pit) * (1.0 - 0.48 * crack);

        if (q.y > 1.12) {
            // painted Egyptian frieze & horizontal molding across front of overhead ceiling lintel beams
            float fz = q.x / 0.14;
            float bi = mod(floor(fz), 4.0);
            float sep = smoothstep(0.06, 0.18, fract(fz)) * smoothstep(0.94, 0.82, fract(fz));
            float fBand = smoothstep(1.16, 1.19, q.y) * smoothstep(1.36, 1.33, q.y);
            float bLine = max(smoothstep(0.018, 0.0, abs(q.y - 1.17)), smoothstep(0.018, 0.0, abs(q.y - 1.35)));
            outPaintCol = mix(InkColor, bi < 1.0 ? RedOchre : (bi < 2.0 ? PaintColor : (bi < 3.0 ? Turquoise : YellowOchre)), sep);
            paint = 0.88 * fBand;
            h -= 0.20 * (1.0 - sep) * fBand + 0.26 * bLine;
            ao *= 1.0 - 0.34 * bLine;
        } else if (q.y > 0.74 && q.y < 1.06) {
            // painted lotus capital collar bands on pilaster front
            float band = fract((q.y - 0.74) / 0.078);
            float groove = 1.0 - smoothstep(0.08, 0.22, min(band, 1.0 - band));
            h -= 0.30 * groove;
            ao *= 1.0 - 0.34 * groove;
            float bi = floor((q.y - 0.74) / 0.078);
            outPaintCol = bi < 1.0 ? PaintColor : (bi < 2.0 ? RedOchre : (bi < 3.0 ? Turquoise : YellowOchre));
            paint = 0.88 * (1.0 - groove);
            if (bi >= 3.0) gold = 0.65 * (1.0 - groove);
        } else if (q.y > -0.72 && q.y < 0.70) {
            // recessed vertical border & carved sunken-relief glyph strip on pilaster front
            float wWall = 1.92 - 0.13 * (q.y + 1.25);
            float px = abs(q.x) - (wWall - 0.14);
            float gMaskZ = smoothstep(0.11, 0.085, abs(px));
            float bord = smoothstep(0.018, 0.0, abs(abs(px) - 0.10));
            h -= 0.26 * bord;
            ao *= 1.0 - 0.32 * bord;
            if (Glyphs && gMaskZ > 0.1) {
                float c = 0.24;
                float ci = floor((q.y + 0.68) / c);
                hg = EwHash2To1(vec2(ci, mod(bayP, 16.0) * 9.0 + side * 5.0));
                gq = vec2(px * side / 0.19, (fract((q.y + 0.68) / c) - 0.5) * 1.15);
                gMask = gMaskZ;
                glyphMode = 4;
            }
        }
    } else {
        // ceiling: heavy carved lintel beams + Valley-of-the-Kings lapis blue starry recessed coffers
        vec2 cp = vec2(q.x, q.z);
        float bx = cp.x / 0.88;
        float by = cp.y / 0.94;
        outExtra.x = 0.78 + 0.32 * EwHash2To1(vec2(floor(bx), floor(by) + 9.0));
        float fx = fract(bx);
        float fy = fract(by);
        float seam = 1.0 - smoothstep(0.008, 0.032, min(min(fx, 1.0 - fx) * 0.88, min(fy, 1.0 - fy) * 0.94));
        float cn = EwValueNoise2D(cp * 2.0 + 9.4);
        float crack = (1.0 - smoothstep(0.0, 0.024, abs(cn - 0.5))) * (0.38 + 0.62 * W);
        float g = EwValueNoise2D(cp * 9.5 * GrainScale) * 0.65 + EwValueNoise2D(cp * 26.0 * GrainScale) * 0.35;
        h = (g - 0.5) * 0.22 - 0.28 * seam - 0.42 * crack;
        ao *= 1.0 - 0.38 * seam - 0.52 * crack;

        // recessed inner coffer painted deep lapis blue with carved golden 5-point pharaonic stars
        if (abs(lzM) < 1.12 && abs(q.x) < 0.86 && q.y > 1.54) {
            outPaintCol = PaintColor * 0.58;
            paint = 0.85;
            vec2 sp = vec2(q.x, lzM) / 0.36;
            vec2 sf = fract(sp) - 0.5;
            float ang = atan(sf.y, sf.x);
            float rad = length(sf);
            float starR = 0.16 + 0.11 * cos(ang * 5.0);
            float starM = smoothstep(0.03, -0.02, rad - starR);
            h += 0.20 * starM;
            gold = max(gold, starM * 0.85);
            outPaintCol = mix(outPaintCol, YellowOchre, starM * 0.7);
        } else if (abs(lzP) < 0.26) {
            // carved frieze strip along the underside of each lintel beam
            float fz = q.x / 0.15;
            float bi = mod(floor(fz), 4.0);
            float sep = smoothstep(0.08, 0.20, fract(fz)) * smoothstep(0.92, 0.80, fract(fz));
            float strip = smoothstep(0.22, 0.18, abs(lzP));
            outPaintCol = mix(InkColor, bi < 1.0 ? RedOchre : (bi < 2.0 ? PaintColor : (bi < 3.0 ? Turquoise : YellowOchre)), sep);
            paint = 0.72 * strip;
            h -= 0.18 * (1.0 - sep) * strip;
        }
    }

    if (glyphMode > 0) {
        float gsd = cryptGlyphSd(gq, hg);
        if (glyphMode == 4) {
            float gm = smoothstep(0.055, -0.035, gsd) * gMask;
            h -= 0.44 * gm;
            ao *= 1.0 - 0.44 * gm;
            if (gm > 0.1) {
                float hc = fract(hg * 13.7);
                outPaintCol = hc < 0.33 ? Turquoise : (hc < 0.66 ? RedOchre : YellowOchre);
                paint = max(paint, 0.45 * gm);
            }
        } else {
            float gm = smoothstep(0.052, -0.032, gsd) * gMask;
            if (glyphMode == 2) {
                h += 0.36 * gm;
                gold = max(gold, gm * 0.85);
            } else {
                // authentic Egyptian sunken relief: deeply incised groove with slightly domed inner island
                float trench = smoothstep(0.055, -0.015, abs(gsd)) * gMask;
                float innerDom = smoothstep(-0.02, -0.08, gsd) * gMask;
                h -= (glyphMode == 1 ? 0.44 : 0.52) * gm + 0.18 * trench - 0.16 * innerDom;
                ao *= 1.0 - (glyphMode == 1 ? 0.42 : 0.52) * gm;
                if (glyphMode == 3 && gm > 0.08) {
                    float hc = fract(hg * 13.7);
                    outPaintCol = hc < 0.28 ? RedOchre : (hc < 0.56 ? Turquoise : (hc < 0.82 ? YellowOchre : PaintColor));
                    paint = max(paint, 0.48 * gm);
                }
            }
        }
    }

    return vec4(h, paint, gold, ao);
}

// aggressive fog-of-war curve: near stays crisp, far corridor plunges into dark ancient gloom
float cryptFogFactor(float dist) {
    float fogEnd = mix(20.0, 8.2, CryptFog);
    float u = dist / fogEnd;
    return exp(-u * u * 2.55 - u * 0.68);
}

// lightweight light estimate for airborne falling sand & stones
vec3 cryptAirLight(vec3 p, float tDist) {
    vec3 lit = vec3(0.035, 0.028, 0.025) + vec3(0.95, 0.85, 0.68) * exp(-tDist * 0.34) * 0.95 * Sunlight;
    if (!Torches) return lit;
    // check bay 1 (left) and bay 2 (right)
    int Z = min(iFrame, 0);
    for (int k = Z; k < 2; k++) {
        float bay = 1.0 + float(k);
        float side = cryptTorchSide(bay);
        float zb = bay * CRYPT_BAY;
        vec2 pc = cryptPath(zb);
        vec3 lp = vec3(pc.x + side * 1.14, pc.y + 0.44, zb);
        float d2 = dot(lp - p, lp - p);
        float flk = cryptFlicker(bay * 7.3 + side * 3.1);
        lit += TorchColor * (flk * 1.65 / (0.28 + 0.46 * d2));
    }
    return lit;
}

vec3 renderCrypt(vec2 p, vec2 viewSize) {
    vec2 uv = p / max(viewSize.y, 1.0);
    int Z = min(iFrame, 0);

    float zCam = 0.65;
    vec2 pc0 = cryptPath(zCam);
    vec2 pc1 = cryptPath(zCam + 4.4);
    vec3 ro = vec3(pc0.x, pc0.y - 0.04, zCam);
    vec3 ta = vec3(pc1.x, pc1.y - 0.16, zCam + 4.4);

    vec3 fwd = normalize(ta - ro);
    vec3 camR = normalize(cross(vec3(0.0, 1.0, 0.0), fwd));
    vec3 camU = cross(fwd, camR);
    vec3 rd = normalize(fwd * 1.38 + camR * uv.x + camU * uv.y);

    // Unified raymarch + normal/AO + torch shadow march (single cryptMap call site!)
    float t = 0.05;
    float tMax = 21.5;
    float matId = -1.0;
    float sandMix = 0.0;
    vec3 pHit = ro;
    vec3 nSum = vec3(0.0);
    vec3 nGeom = vec3(0.0, 1.0, 0.0);
    float occ1 = 1.0, occ2 = 1.0;
    float shVal0 = 1.0, shVal1 = 1.0;

    vec3 tPos0 = vec3(0.0), tPos1 = vec3(0.0);
    float tFlk0 = 0.0, tFlk1 = 0.0;
    if (Torches) {
        vec2 tpc0 = cryptPath(CRYPT_BAY);
        tFlk0 = cryptFlicker(4.2);
        tPos0 = vec3(tpc0.x - 1.14 + 0.024 * sin(TW * 9.1 + 4.2),
                     tpc0.y + 0.44 + 0.030 * sin(TW * 11.3 + 4.2), CRYPT_BAY);
        vec2 tpc1 = cryptPath(2.0 * CRYPT_BAY);
        tFlk1 = cryptFlicker(17.7);
        tPos1 = vec3(tpc1.x + 1.14 + 0.024 * sin(TW * 9.1 + 17.7),
                     tpc1.y + 0.44 + 0.030 * sin(TW * 11.3 + 17.7), 2.0 * CRYPT_BAY);
    }

    int phase = 0; // 0 = march, 1 = normal+AO, 2 = shadow0, 3 = shadow1
    int sub = 0;
    float ts = 0.12, maxS = 0.0, shCur = 1.0;
    vec3 shDir = vec3(0.0, 1.0, 0.0);
    vec3 shOrig = vec3(0.0);

    for (int iter = Z; iter < 82; iter++) {
        vec3 sp;
        if (phase == 0) {
            sp = ro + rd * t;
        } else if (phase == 1) {
            vec3 e = sub == 0 ? vec3(1.0, -1.0, -1.0)
                   : (sub == 1 ? vec3(-1.0, -1.0, 1.0)
                   : (sub == 2 ? vec3(-1.0, 1.0, -1.0) : vec3(1.0, 1.0, 1.0)));
            float distAO = (sub == 4) ? 0.14 : 0.36;
            sp = pHit + ((sub < 4) ? (e * 0.0028) : (nGeom * distAO));
        } else {
            sp = shOrig + shDir * ts;
        }

        vec3 h = cryptMap(sp);

        if (phase == 0) {
            if (h.x < 0.0020 * (1.0 + 0.28 * t)) {
                matId = h.y;
                sandMix = h.z;
                pHit = sp;
                phase = 1;
                sub = 0;
            } else {
                t += h.x * 0.85;
                sub++;
                if (t > tMax || sub >= 64) break;
            }
        } else if (phase == 1) {
            if (sub < 4) {
                vec3 e = sub == 0 ? vec3(1.0, -1.0, -1.0)
                       : (sub == 1 ? vec3(-1.0, -1.0, 1.0)
                       : (sub == 2 ? vec3(-1.0, 1.0, -1.0) : vec3(1.0, 1.0, 1.0)));
                nSum += e * h.x;
                if (sub == 3) nGeom = normalize(nSum);
            } else {
                float distAO = (sub == 4) ? 0.14 : 0.36;
                float v = clamp(h.x / distAO, 0.0, 1.0);
                if (sub == 4) occ1 = v; else occ2 = v;
            }
            sub++;
            if (sub >= 6) {
                if (!Torches || t >= 11.5) break;
                shOrig = pHit + nGeom * 0.03;
                phase = 4;
                for (int tryK = 0; tryK < 2; tryK++) {
                    vec3 lp = tryK == 0 ? tPos0 : tPos1;
                    float dz = abs(pHit.z - lp.z);
                    vec3 ld = lp - pHit;
                    float d2 = dot(ld, ld);
                    if (dz <= CRYPT_BAY * 1.45 && d2 > 0.72 * 0.72 && d2 < 25.0) {
                        float distL = sqrt(d2);
                        vec3 L = ld / distL;
                        if (dot(nGeom, L) > -0.25) {
                            phase = 2 + tryK;
                            sub = 0;
                            ts = 0.12;
                            maxS = min(distL - 0.62, 4.0);
                            shCur = 1.0;
                            shDir = L;
                            break;
                        }
                    }
                }
                if (phase >= 4) break;
            }
        } else {
            shCur = min(shCur, clamp(3.2 * h.x / ts, 0.0, 1.0));
            ts += clamp(h.x, 0.12, 0.60);
            sub++;
            if (shCur < 0.08 || ts >= maxS || sub >= 6) {
                float shFinal = mix(0.28, 1.0, smoothstep(0.0, 1.0, shCur));
                if (phase == 2) {
                    shVal0 = shFinal;
                    float dz = abs(pHit.z - tPos1.z);
                    vec3 ld = tPos1 - pHit;
                    float d2 = dot(ld, ld);
                    if (dz <= CRYPT_BAY * 1.45 && d2 > 0.72 * 0.72 && d2 < 25.0) {
                        float distL = sqrt(d2);
                        vec3 L = ld / distL;
                        if (dot(nGeom, L) > -0.25) {
                            phase = 3;
                            sub = 0;
                            ts = 0.12;
                            maxS = min(distL - 0.62, 4.0);
                            shCur = 1.0;
                            shDir = L;
                            continue;
                        }
                    }
                    break;
                } else {
                    shVal1 = shFinal;
                    break;
                }
            }
        }
    }

    // deep abyssal fog-of-war gloom color
    vec3 fogCol = vec3(0.007, 0.005, 0.005);
    vec3 col = fogCol;
    float tHit = matId >= 0.0 ? t : tMax;

    if (matId >= 0.0) {
        vec2 pc = cryptPath(pHit.z);
        vec3 q = vec3(pHit.x - pc.x, pHit.y - pc.y, pHit.z);
        vec3 n = nGeom;

        vec3 alb = StoneColor;
        float ao = 0.28 + 0.72 * (0.55 * occ1 + 0.45 * occ2);
        float specAmt = 0.14;
        float specPow = 18.0;
        float goldMask = 0.0;
        float emberGlow = 0.0;

        if (matId < 1.5) {
            vec3 stoneAlb = StoneColor;
            vec3 stoneN = nGeom;
            float stoneAo = 1.0;
            float stoneSpec = 0.14;
            float stonePow = 18.0;

            if (sandMix < 0.99) {
                float eps = 0.0055 * (1.0 + 0.10 * t);
                vec3 tU = abs(nGeom.y) < 0.8 ? normalize(cross(vec3(0.0, 1.0, 0.0), nGeom)) : vec3(1.0, 0.0, 0.0);
                vec3 tV = cross(nGeom, tU);
                vec4 det = vec4(0.0);
                vec3 paintCol = vec3(0.0);
                vec2 extra = vec2(1.0, 0.0);
                float hU = 0.0, hV = 0.0;
                int nDet = t < 13.0 ? 3 : 1;
                for (int i = Z; i < 3; i++) {
                    if (i >= nDet) break;
                    vec3 qq = q + (i == 1 ? tU * eps : (i == 2 ? tV * eps : vec3(0.0)));
                    vec3 pTmp;
                    vec2 eTmp;
                    vec4 dTmp = cryptSurfaceDetail(qq, nGeom, pTmp, eTmp);
                    if (i == 0) { det = dTmp; paintCol = pTmp; extra = eTmp; }
                    else if (i == 1) hU = dTmp.x;
                    else hV = dTmp.x;
                }
                stoneAo = det.w;
                if (nDet > 1) {
                    float bump = 0.056 / (1.0 + 0.045 * t);
                    stoneN = normalize(nGeom - bump * (tU * (hU - det.x) + tV * (hV - det.x)) / eps);
                }

                vec2 tq = abs(nGeom.x) > 0.52 ? vec2(q.z, q.y)
                        : (abs(nGeom.z) > 0.52 ? vec2(q.x, q.y) : vec2(q.x, q.z));
                float f = fbm2Rolled(tq * vec2(0.6, 1.55) * GrainScale + 4.7, 4);
                float strata = 0.5 + 0.5 * sin(tq.y * 5.8 + f * 7.2);
                stoneAlb = mix(StrataColor, StoneColor, 0.22 + 0.78 * smoothstep(0.1, 0.9, strata));
                stoneAlb *= (0.76 + 0.32 * f) * extra.x;

                float pFade = 1.0 - 0.70 * W * smoothstep(0.32, 0.76, EwValueNoise2D(tq * 5.8 + 2.1));
                stoneAlb = mix(stoneAlb, paintCol * (0.74 + 0.36 * f), clamp(det.y * PaintAmount * pFade, 0.0, 1.0));
                goldMask = det.z * (0.58 + 0.42 * (1.0 - 0.5 * W)) * (1.0 - sandMix);
                stoneAlb = mix(stoneAlb, GoldColor * 0.95, goldMask);
                stoneSpec = mix(0.15, 0.96, goldMask);
                stonePow = mix(18.0, 38.0, goldMask);

                if (abs(nGeom.x) > 0.52 || abs(nGeom.z) > 0.52) {
                    stoneAlb *= 1.0 - extra.y * 0.76;
                } else if (nGeom.y > 0.5) {
                    stoneAlb = mix(stoneAlb, mix(StrataColor, StoneColor * 1.1, 0.72), extra.y);
                }
            }

            if (sandMix > 0.01) {
                vec2 sp = vec2(q.x, q.z);
                float nR = EwValueNoise2D(sp * 3.8);
                float rip = sin(sp.y * 16.0 + sp.x * 4.5 + nR * 4.5) * 0.5 + 0.5;
                float gr = EwValueNoise2D(sp * 42.0);
                // scattered tiny pebbles embedded in sand
                float peb = smoothstep(0.86, 0.94, EwValueNoise2D(sp * 22.0 + 9.1));
                vec3 sandAlb = mix(StrataColor, StoneColor * 1.06, 0.56 + 0.16 * rip + 0.14 * gr);
                sandAlb = mix(sandAlb, StrataColor * 0.72, peb * 0.55);
                float rSlope = cos(sp.y * 16.0 + sp.x * 4.5 + nR * 4.5);
                vec3 sandN = normalize(nGeom + vec3(rSlope * 0.07 + (gr - 0.5) * 0.18, 0.0, rSlope * 0.18));
                float sandSpec = 0.28 * smoothstep(0.68, 0.96, EwHash2To1(floor(sp * 78.0)));
                float sm = smoothstep(0.0, 1.0, clamp(sandMix + (nR - 0.5) * 0.34, 0.0, 1.0));
                alb = mix(stoneAlb, sandAlb, sm);
                n = normalize(mix(stoneN, sandN, sm));
                ao *= mix(stoneAo, 1.0 - 0.25 * peb, sm);
                specAmt = mix(stoneSpec, sandSpec, sm);
                specPow = mix(stonePow, 30.0, sm);
            } else {
                alb = stoneAlb;
                n = stoneN;
                ao *= stoneAo;
                specAmt = stoneSpec;
                specPow = stonePow;
            }
        } else {
            // dark forged bronze torch sconce + glowing hot embers inside brazier cup
            alb = vec3(0.20, 0.15, 0.11) * (0.75 + 0.38 * EwValueNoise2D(q.yz * 28.0));
            specAmt = 1.0;
            specPow = 36.0;
            float coalN = EwValueNoise2D(q.xz * 48.0 + TW * 0.4);
            emberGlow = smoothstep(0.24, 0.31, q.y) * smoothstep(0.05, 0.75, nGeom.y) * (0.65 + 0.55 * coalN);
        }

        // portal entrance daylight spill (fades rapidly into crypt gloom)
        float entSpill = exp(-t * 0.34) * Sunlight;
        vec3 sunDir = normalize(vec3(L2.x * 0.55, 0.45, -0.70));
        float sunDiff = max(dot(n, sunDir), 0.0);
        vec3 lightDiff = vec3(0.014, 0.011, 0.010)
                       + vec3(1.0, 0.88, 0.70) * entSpill * (0.16 + 0.95 * sunDiff);
        vec3 lightSpec = vec3(1.0, 0.90, 0.72) * entSpill * pow(max(dot(n, normalize(sunDir - rd)), 0.0), specPow) * specAmt * 0.40;

        // sparse flickering 3D torches (bay 1 left, bay 2 right, bay 4 far left) + precomputed soft 3D shadow
        if (Torches) {
            for (int k = Z; k < 3; k++) {
                float zb = (k == 0 ? 1.0 : (k == 1 ? 2.0 : 4.0)) * CRYPT_BAY;
                float dz = abs(q.z - zb);
                if (dz > CRYPT_BAY * 1.45) continue;
                float win = smoothstep(CRYPT_BAY * 1.45, CRYPT_BAY * 0.35, dz);
                vec3 lPos;
                float flk;
                if (k == 0) { lPos = tPos0; flk = tFlk0; }
                else if (k == 1) { lPos = tPos1; flk = tFlk1; }
                else {
                    vec2 tpc = cryptPath(zb);
                    float seed = 4.0 * 7.3 - 3.1;
                    flk = cryptFlicker(seed);
                    lPos = vec3(tpc.x - 1.14 + 0.024 * sin(TW * 9.1 + seed),
                                tpc.y + 0.44 + 0.030 * sin(TW * 11.3 + seed), zb);
                }
                vec3 ld = lPos - pHit;
                float d2 = dot(ld, ld);
                float distL = sqrt(max(d2, 1e-4));
                vec3 L = ld / distL;
                float diff = max(dot(n, L), 0.0);
                float sh = (k < 2 && diff > 0.03) ? (k == 0 ? shVal0 : shVal1) : 1.0;

                float att = flk * win / (0.28 + 0.46 * d2);
                vec3 H = normalize(L - rd);
                float spc = pow(max(dot(n, H), 0.0), specPow) * specAmt;
                lightDiff += TorchColor * (diff * 1.95 * sh + 0.09) * att;
                lightSpec += (TorchColor * 0.72 + vec3(0.35, 0.25, 0.12)) * spc * sh * att * 1.55;
            }
        } else {
            lightDiff += vec3(0.08, 0.07, 0.065) * (0.35 + 0.65 * max(n.y * 0.5 + 0.5, 0.0));
        }

        col = alb * lightDiff * ao + lightSpec * (0.40 + 0.60 * ao);
        col += GoldColor * goldMask * lightSpec * 0.65;
        col += TorchColor * vec3(1.6, 0.55, 0.12) * emberGlow * 2.4;

        // aggressive fog of war swallowing the distant crypt
        float fog = cryptFogFactor(t);
        col = mix(fogCol, col, fog);
    }

    // ---------------------------------------------------------------- volumetric fog-of-war wisps, falling sand/stones & torch flames
    // 1. Subtle creeping fog-of-war shadow mist drifting across the deep corridor & stairs
    if (CryptFog > 0.05) {
        float tMist = min(tHit, mix(11.0, 5.2, CryptFog));
        if (tHit > 3.2) {
            vec3 mp = ro + rd * tMist;
            vec2 mpc = cryptPath(mp.z);
            float my = mp.y - mpc.y;
            float nM1 = EwValueNoise2D(vec2(mp.x * 1.15 + TW * 0.14, my * 1.35 - TW * 0.06));
            float nM2 = EwValueNoise2D(vec2(mp.x * 2.4 - TW * 0.10, mp.z * 0.45 + TW * 0.08));
            float wisp = smoothstep(0.28, 0.82, nM1 * 0.62 + nM2 * 0.38);
            float depthM = smoothstep(3.0, mix(12.5, 6.2, CryptFog), tHit);
            float floorBias = 0.55 + 0.45 * smoothstep(0.5, -1.1, my);
            // faint warm backlit dust rim where torchlight hits the fog wall, dark veil behind
            vec3 mistCol = mix(fogCol, TorchColor * 0.08 + vec3(0.025, 0.020, 0.016), Torches ? 0.42 : 0.15);
            float mistA = wisp * depthM * floorBias * CryptFog * 0.52;
            col = mix(col, mistCol, clamp(mistA, 0.0, 0.65));
        }
    }

    // 2. Falling sand streams & tumbling angular stone chunks from ceiling cracks
    if (DebrisAmount > 0.005) {
        vec3 sandAdd = vec3(0.0);
        float sandTrans = 1.0;
        vec2 rd2 = rd.xz;
        float rd2Len2 = dot(rd2, rd2);

        if (rd2Len2 > 1e-6) {
            for (int i = Z; i < 6; i++) {
                float bay = float(i / 2);
                float j = float(i - (i / 2) * 2);
                vec3 h = EwHash2To3(vec2(bay * 2.0 + j, 7.3));
                if (h.z > DebrisAmount * 1.60) continue;
                float zc = (bay + 0.28 + 0.56 * h.x) * CRYPT_BAY;
                float xc = (h.y * 2.0 - 1.0) * 1.00;
                vec2 pcC = cryptPath(zc);
                vec2 crackXZ = vec2(pcC.x + xc, zc);
                float ts = dot(crackXZ - ro.xz, rd2) / rd2Len2;
                if (ts <= 1.95 || ts >= tHit) continue;
                vec3 rp = ro + rd * ts;
                float ry = rp.y - pcC.y;
                if (ry < -1.32 || ry > 1.58) continue;
                float fallT = clamp((1.56 - ry) / 2.84, 0.0, 1.0);
                vec2 sway = vec2(sin(ry * 2.4 + TW * 1.7 + h.x * 20.0),
                                 cos(ry * 1.9 + TW * 1.4 + h.y * 15.0)) * 0.024 * fallT;
                vec2 dXZ = rp.xz - (crackXZ + sway);
                float rDist = length(dXZ);
                float wid = mix(0.007, 0.024, fallT);
                if (rDist < wid * 4.2) {
                    float sx = dot(dXZ, vec2(camR.x, camR.z));
                    float pulse = 0.62 + 0.38 * sin(TW * (0.48 + 0.35 * h.y) + h.x * 30.0);
                    float core = exp(-(rDist * rDist) / (wid * wid));
                    float n1 = EwValueNoise2D(vec2(sx * 95.0 + h.x * 17.0, ry * 22.0 + TW * 7.2 + h.y * 31.0));
                    float n2 = EwValueNoise2D(vec2(sx * 190.0 + h.y * 29.0, ry * 48.0 + TW * 13.5 + h.x * 13.0));
                    float sparkle = smoothstep(0.74, 0.93, n2) * 0.85;
                    float grain = 0.30 + 0.88 * n1 * (0.40 + 0.80 * n2) + sparkle;
                    float topFade = smoothstep(1.58, 1.38, ry);
                    float botFade = smoothstep(-1.32, -1.06, ry);
                    float impact = exp(-(ry + 1.14) * (ry + 1.14) * 28.0)
                                 * exp(-(rDist * rDist) / (0.12 * 0.12)) * 0.55;
                    float dens = (core * grain * topFade * botFade + impact * (0.5 + 0.5 * n1))
                               * pulse * (0.55 + 0.55 * DebrisAmount);
                    float fog = cryptFogFactor(ts);
                    vec3 sLit = cryptAirLight(rp, ts);
                    vec3 sandCol = mix(StrataColor, StoneColor * 1.35, 0.72) * sLit;
                    sandAdd += sandCol * dens * 0.88 * fog;
                    sandTrans *= clamp(1.0 - dens * 0.38 * fog, 0.30, 1.0);
                }
            }
        }

        for (int i = Z; i < 6; i++) {
            float bay = float(i / 2);
            float j = float(i - (i / 2) * 2);
            vec3 hs = EwHash2To3(vec2(bay * 3.0 + j, 19.7));
            if (hs.z > DebrisAmount * 1.60) continue;
            float per = 2.3 + 2.9 * hs.y;
            float ph = fract(TW / per + hs.x);
            if (ph > 0.58) continue;
            float u = ph / 0.58;
            float zs = (bay + 0.20 + 0.62 * hs.x) * CRYPT_BAY;
            float xs = (hs.y * 2.0 - 1.0) * 1.06;
            vec2 pcs = cryptPath(zs);
            float ys = pcs.y + 1.50 - 2.82 * (0.82 * u * u + 0.18 * u);
            vec3 sp = vec3(pcs.x + xs + 0.05 * u * (hs.z - 0.5), ys, zs);
            float tS = dot(sp - ro, rd);
            if (tS <= 0.45 || tS >= tHit) continue;
            vec3 dp = (ro + rd * tS) - sp;
            float r0 = 0.017 + 0.020 * fract(hs.x * 11.3);
            float d2 = dot(dp, dp);
            float fadeU = smoothstep(0.0, 0.07, u) * smoothstep(1.0, 0.90, u);
            bool needRock = (d2 < r0 * r0 * 3.2);
            bool needTrail = (abs(dp.x) < 0.06 && abs(dp.z) < 0.06 && dp.y > 0.0 && dp.y < 0.42);
            if (needRock || needTrail) {
                vec3 airL = cryptAirLight(sp, tS);
                float fog = cryptFogFactor(tS);
                if (needRock) {
                    float ang = u * (8.0 + 9.0 * hs.y) + hs.x * 6.28;
                    vec2 p2 = EwRotate2D(ang) * vec2(dot(dp, camR), dot(dp, camU));
                    float chip = 0.14 * r0 * sin(p2.x / max(r0, 0.005) * 3.5 + hs.z * 6.0);
                    float sdRock = EwSdBox2D(p2, vec2(r0, r0 * (0.62 + 0.24 * hs.z))) - 0.18 * r0 + chip;
                    float pxW = max(tS * 0.0014, 0.0018);
                    float cov = smoothstep(pxW, -pxW, sdRock) * fadeU;
                    if (cov > 0.001) {
                        vec3 rn = normalize(vec3(p2 / max(r0, 0.005), 0.85));
                        float shade = 0.35 + 0.75 * max(dot(rn, normalize(vec3(0.4, 0.7, 0.6))), 0.0);
                        vec3 rCol = mix(StrataColor, StoneColor, 0.45 + 0.30 * sin(ang * 2.0)) * airL * shade;
                        col = mix(col, mix(fogCol, rCol, fog), cov);
                    }
                }
                if (needTrail) {
                    float tr = exp(-(dp.x * dp.x + dp.z * dp.z) / (0.020 * 0.020))
                             * smoothstep(0.0, 0.06, dp.y) * smoothstep(0.42, 0.06, dp.y) * fadeU;
                    sandAdd += StoneColor * airL * tr * 0.30 * fog;
                }
            }
        }
        col = col * sandTrans + sandAdd;
    }

    // 3. Sparse animated flickering 3D torch flames, smoke wisps, halos & rising sparks (only 3 active bays)
    if (Torches) {
        vec3 flameAdd = vec3(0.0);
        float smokeTrans = 1.0;
        for (int k = Z; k < 3; k++) {
            float bay = k == 0 ? 1.0 : (k == 1 ? 2.0 : 4.0);
            float side = cryptTorchSide(bay);
            float zb = bay * CRYPT_BAY;
            if (zb - zCam > tHit + 1.2) break;
            vec2 tpc = cryptPath(zb);
            vec3 fBase = vec3(tpc.x + side * 1.14, tpc.y + 0.31, zb);
            float tf = dot(fBase - ro, rd);
            if (tf <= 0.22 || tf > tHit + 0.16) continue;

            vec3 dp = (ro + rd * tf) - fBase;
            float fx = dot(dp, camR);
            float fy = dp.y;
            float hDist2 = fx * fx + (fy - 0.18) * (fy - 0.18);
            if (hDist2 > 1.1) continue;

            float seed = bay * 7.3 + side * 3.1;
            float flk = cryptFlicker(seed);
            float fog = cryptFogFactor(tf * 0.90);
            float vis = smoothstep(tHit + 0.12, tHit - 0.05, tf);

            // atmospheric torchlight bloom piercing the dusty air
            flameAdd += TorchColor * (exp(-hDist2 * 3.6) * 0.28 * flk * fog * vis);

            if (abs(fx) < 0.28 && fy > -0.05 && fy < 0.64) {
                float hNorm = clamp(fy / 0.48, 0.0, 1.0);
                float sway = (0.044 * sin(TW * 7.8 + fy * 8.5 + seed)
                            + 0.026 * sin(TW * 15.1 - fy * 13.0 + seed * 1.9)) * hNorm;
                float nx = fx - sway;
                float n1 = EwValueNoise2D(vec2(nx * 11.5 + seed, fy * 7.8 - TW * 6.8));
                float n2 = EwValueNoise2D(vec2(nx * 23.0 - seed, fy * 15.5 - TW * 12.2));
                float turb = (n1 * 0.62 + n2 * 0.38 - 0.46) * (0.12 + 0.26 * hNorm);
                float rad = 0.074 * sin(pow(clamp(hNorm, 0.001, 1.0), 0.50) * 3.1415926) * (0.85 + 0.22 * flk);
                float dFlame = abs(nx + turb * 0.62) - rad;
                float fMask = smoothstep(0.020, -0.018, dFlame)
                            * smoothstep(-0.03, 0.05, fy)
                            * smoothstep(0.54, 0.22, fy + turb * 0.55);
                float midMask = smoothstep(0.010, -0.024, dFlame + 0.014) * smoothstep(0.42, 0.10, fy + turb * 0.3);
                float coreMask = smoothstep(0.006, -0.028, dFlame + 0.028) * smoothstep(0.28, 0.04, fy);

                vec3 fireCol = mix(TorchColor * vec3(1.15, 0.20, 0.02), TorchColor * vec3(1.38, 0.74, 0.18), midMask);
                fireCol = mix(fireCol, vec3(1.0, 0.96, 0.82) * 2.2, coreMask);
                flameAdd += fireCol * fMask * 1.65 * fog * vis;
            }

            if (abs(fx) < 0.28 && fy > 0.26 && fy < 1.08) {
                float sh = clamp((fy - 0.30) / 0.72, 0.0, 1.0);
                float smW = 0.08 + 0.13 * sh;
                float smN = EwValueNoise2D(vec2(fx * 6.0 + seed, fy * 4.5 - TW * 2.4));
                float smk = exp(-(fx * fx) / (smW * smW)) * smoothstep(0.0, 0.25, sh) * smoothstep(1.0, 0.35, sh) * smN;
                smokeTrans *= 1.0 - smk * 0.34 * fog * vis;

                // three rising glowing embers
                for (int e = Z; e < 3; e++) {
                    float fe = float(e);
                    float ph = fract(TW * (0.52 + 0.24 * fe) + seed * 0.37 + fe * 0.33);
                    float ex = 0.09 * sin(ph * 11.0 + seed + fe * 4.1) * ph;
                    float ey = 0.26 + ph * 0.76;
                    float sp = exp(-((fx - ex) * (fx - ex) + (fy - ey) * (fy - ey)) / (0.010 * 0.010))
                             * smoothstep(0.0, 0.12, ph) * smoothstep(1.0, 0.35, ph);
                    flameAdd += vec3(1.0, 0.80, 0.30) * sp * 2.4 * fog * vis;
                }
            }
        }
        col = col * smokeTrans + flameAdd;
    }

    // 4. Delicate floating warm-lit dust motes drifting in the foreground air
    if (CryptFog > 0.05) {
        vec3 moteAdd = vec3(0.0);
        for (int m = Z; m < 2; m++) {
            float tm = 1.9 + float(m) * 2.6;
            if (tm >= tHit) break;
            vec3 mp = ro + rd * tm;
            vec2 mCell = floor(mp.xy * 3.8 + vec2(0.0, -TW * 0.12));
            vec3 mh = EwHash2To3(mCell + float(m) * 19.3 + floor(mp.z * 0.5) * 7.1);
            if (mh.z > 0.35) {
                vec2 mf = fract(mp.xy * 3.8 + vec2(0.0, -TW * 0.12)) - (0.2 + 0.6 * mh.xy);
                mf += 0.12 * vec2(sin(TW * 0.9 + mh.x * 20.0), cos(TW * 0.8 + mh.y * 20.0));
                float d2 = dot(mf, mf);
                float mote = exp(-d2 / (0.020 * 0.020)) * (0.4 + 0.6 * sin(TW * 2.2 + mh.z * 30.0));
                float mfog = cryptFogFactor(tm);
                moteAdd += mix(StoneColor, TorchColor, Torches ? 0.65 : 0.0) * mote * 0.34 * CryptFog * mfog;
            }
        }
        col += moteAdd;
    }

    // soft filmic tone map harmonized with the portal frame
    col = 1.0 - exp(-max(col, vec3(0.0)) * 1.38);
    col *= 0.86 + 0.14 * smoothstep(1.15, 0.15, length(uv));
    return clamp(col, 0.0, 1.0);
}
vec3 shadeImage(vec3 imgRgb, vec2 p, vec2 imgCenter, vec2 imgHalf) {
    vec3 imgLin = EwSrgbToLinear(clamp(imgRgb, 0.0, 1.0));
    vec2 dp = p - imgCenter;
    vec2 uImg = dp / max(imgHalf, vec2(1.0));
    // Directional sunlight raking across the recessed opening
    float sunGrad = clamp(dot(uImg, -L2), -1.0, 1.0);
    // Soft ambient occlusion & directional recessed shadow near the stone inner walls
    float wallDist = min(imgHalf.x - abs(dp.x), imgHalf.y - abs(dp.y));
    float edgeAO = 1.0 - 0.24 * ShadowAmount * (1.0 - smoothstep(0.0, 0.8 * S + 8.0, wallDist));
    float sideDist = L2.x < 0.0 ? (dp.x + imgHalf.x) : (imgHalf.x - dp.x);
    float topDist = imgHalf.y - dp.y;
    float recessSh = 1.0 - 0.24 * ShadowAmount * clamp(
        (1.0 - smoothstep(0.0, (0.5 + 1.5 * abs(L2.x)) * S + 10.0, sideDist)) * abs(L2.x) +
        (1.0 - smoothstep(0.0, (0.5 + 1.3 * max(L2.y, 0.0)) * S + 10.0, topDist)) * max(L2.y, 0.0),
        0.0, 1.0
    );
    // Balanced sky fill + directional sunlight (neutral at Sunlight = 1.0, responds across 0.3 .. 2.0)
    vec3 skyFill = vec3(0.86, 0.91, 1.0) * 0.22;
    vec3 sunLight = vec3(1.04, 0.99, 0.93) * 0.78 * Sunlight * (1.0 + 0.14 * sunGrad) * recessSh;
    vec3 lit = imgLin * (skyFill + sunLight) * edgeAO;
    vec3 hi = max(lit - 0.9, vec3(0.0));
    lit = min(lit, vec3(0.9)) + hi / (1.0 + hi * 4.0);
    return EwLinearToSrgb(clamp(lit, 0.0, 1.0));
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 halfRes = 0.5 * res;
    vec2 p = fragCoord - halfRes;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 frameE = halfRes;
    vec2 imgHalf = vec2(1.0);
    vec2 imgCenter = vec2(0.0);
    if (SwHaveImage) {
        vec2 imgRes = iChannelResolution[0].xy;
        float imgAspect = (imgRes.x > 0.5 && imgRes.y > 0.5) ? (imgRes.x / imgRes.y) : (res.x / max(res.y, 1.0));
        float bx = 1.46 * inset;
        float by = 1.47 * inset;
        vec2 maxOpen = max(halfRes - vec2(bx, by), vec2(1.0));
        vec2 openFront;
        if (maxOpen.x / maxOpen.y > imgAspect) {
            openFront.y = maxOpen.y;
            openFront.x = openFront.y * imgAspect;
        } else {
            openFront.x = maxOpen.x;
            openFront.y = openFront.x / max(imgAspect, 1e-4);
        }
        frameE = min(openFront + vec2(bx, by), halfRes);
        float minE = min(frameE.x, frameE.y);
        if (inset > 0.5 * minE) {
            inset = 0.5 * minE;
            bx = 1.46 * inset;
            by = 1.47 * inset;
            maxOpen = max(halfRes - vec2(bx, by), vec2(1.0));
            if (maxOpen.x / maxOpen.y > imgAspect) {
                openFront.y = maxOpen.y;
                openFront.x = openFront.y * imgAspect;
            } else {
                openFront.x = maxOpen.x;
                openFront.y = openFront.x / max(imgAspect, 1e-4);
            }
            frameE = min(openFront + vec2(bx, by), halfRes);
        }
        float kFrame = 0.5 * inset / max(frameE.x, frameE.y);
        imgHalf = openFront / (1.0 + kFrame);
        imgCenter = vec2(0.0, -0.05 * inset / (1.0 + kFrame));
    }

    vec2 hs = frameE - inset;
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);
    S = inset; HS = hs; RAD = rad; W = Weathering;
    E = frameE;
    MG = 10.0 + 0.06 * S;
    RADO = max(min(rad, min(E.x, E.y) - MG), 0.0);

    float sa = radians(SunAngle + 5.0 * sin(mod(iTime * Speed * 0.15, 6.2831853)));
    L2 = vec2(sin(sa), cos(sa));
    L3 = normalize(vec3(L2 * 0.75, 0.7));
    TW = mod(iTime * Speed, 2000.0);

    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = smoothstep(0.5, -0.5, d + 0.2 * inset);

    vec3 col = vec3(0.0);
    float a = 0.0;

    if (d >= -3.2 * inset - 4.0 && max(abs(p.x) - frameE.x, abs(p.y) - frameE.y) <= 1.2 * inset + 10.0) {
        vec2 so = L2 * 0.45 * S;
        float k = 0.5 * S / max(E.x, E.y);      // depth of the stone (perspective toward the center)
        float lp = length(p) * k;

        int Z = min(iFrame, 0);   // keeps the loops rolled (fast compile)
        JMAX = S * ((0.05 + 0.16 * W) + (0.03 + 0.09 * W) + (0.01 + 0.04 * W) + 0.3 * W) + 0.01;

        // deep in the opening only the falling debris can reach (stone, shadows, vines, grass end before)
        vec4 acc = vec4(0.0);
        if (d >= -2.6 * S - 4.0) {
            // exact piece distances (lintel, threshold, obelisk) at p, the shadow probe p + so and p + x / p + y (gradient);
            // the costly outline clip only where it can change the result
            vec3 sd0 = vec3(1e5), sd1 = vec3(1e5), sd2 = vec3(1e5), sd3 = vec3(1e5);
            for (int i = Z; i < 4; i++) {
                vec2 q = p + (i == 1 ? so : (i == 2 ? vec2(1.0, 0.0) : (i == 3 ? vec2(0.0, 1.0) : vec2(0.0))));
                vec3 b = allBase(q);
                float mb = min(b.x, min(b.y, b.z));
                vec3 v;
                if (i == 1 && mb >= 0.35 * S) {
                    v = b;     // shadow fully saturated either way
                } else {
                    v = (EwSdRoundedBox2D(q, E - MG, RADO) + JMAX <= mb) ? b : max(b, vec3(outerClip(q)));
                }
                if (i == 0) sd0 = v;
                else if (i == 1) {
                    sd1 = v;
                    if (min(sd0.x, min(sd0.y, sd0.z)) >= 0.5) break;   // no piece covers p: no gradient needed
                }
                else if (i == 2) sd2 = v;
                else sd3 = v;
            }

            // cast shadow of the whole portal onto the layer below
            vec3 sp = sd1;
            float occ = min(sp.x, min(sp.y, sp.z));
            float shA = (1.0 - smoothstep(-0.1 * S, 0.35 * S, occ)) * ShadowAmount * 0.6;
            acc = vec4(0.0, 0.0, 0.0, shA);

            // grass: back tufts (behind the stone), shadow probe and front blades in one loop
            vec4 g0 = vec4(0.0), g1 = vec4(0.0), g2 = vec4(0.0);
            float gv = p.y - (-HS.y + 0.45 * S);
            float gh = max(gv, 0.0);
            if (Grass) {
                for (int i = Z; i < 3; i++) {
                    vec4 gvRes = grass(i == 1 ? p + L2 * 0.3 * gh : p, i == 0 ? 0 : 1, i == 0 ? 1 : 3);
                    if (i == 0) g0 = gvRes;
                    else if (i == 1) g1 = gvRes;
                    else g2 = gvRes;
                }
            }
            acc = over(g0, acc);

            for (int id = 1 + Z; id < 4; id++) {
                float sd = id == 1 ? sd0.x : (id == 2 ? sd0.y : sd0.z);
                if (sd > lp + 2.0) continue;
                float sh = 1.0;
                if (id < 2) sh *= 1.0 - 0.7 * ShadowAmount * (1.0 - smoothstep(-0.1 * S, 0.25 * S, sd1.y));
                if (id < 3) sh *= 1.0 - 0.7 * ShadowAmount * (1.0 - smoothstep(-0.1 * S, 0.25 * S, sd1.z));
                float cov = clamp(0.5 - sd, 0.0, 1.0);
                // side walls seen through the opening: march into the depth, bisect, then 3-tap gradient (one pieceSd site)
                if (cov < 1.0) {
                    float t0 = 0.0, t1 = -1.0, dmin = 1e5;
                    float scov = 0.0;
                    vec2 pp = p;
                    float gs0 = 0.0, gs1 = 0.0, gs2 = 0.0;
                    int stage = 0;
                    int gIdx = 0;
                    for (int i = Z; i < 19; i++) {
                        bool search = t1 < 0.0;
                        if (stage == 0 && (i >= 16 || (search && i >= 12))) {
                            scov = clamp(0.5 - dmin, 0.0, 1.0);
                            if (scov <= 0.0) break;
                            if (t1 < 0.0) t1 = 1.0;
                            pp = p * (1.0 + t1 * k);
                            stage = 1;
                            gIdx = 0;
                        }
                        float t = search ? float(i + 1) / 12.0 : 0.5 * (t0 + t1);
                        vec2 q = stage == 0 ? p * (1.0 + t * k)
                                            : pp + (gIdx == 1 ? vec2(1.0, 0.0) : (gIdx == 2 ? vec2(0.0, 1.0) : vec2(0.0)));
                        float ds = pieceSd(id, q, stage == 0);
                        if (stage == 0) {
                            if (search) dmin = min(dmin, ds);
                            if (ds < 0.0) t1 = t; else t0 = t;
                        } else {
                            if (gIdx == 0) gs0 = ds;
                            else if (gIdx == 1) gs1 = ds;
                            else { gs2 = ds; break; }
                            gIdx++;
                        }
                    }
                    if (scov > 0.0) {
                        vec2 g2v = vec2(gs1 - gs0, gs2 - gs0);
                        g2v /= max(length(g2v), 1e-4);
                        vec3 cs = shadeSide(id, pp, t1, g2v) * sh;
                        acc = vec4(cs, 1.0) * scov + acc * (1.0 - scov);
                    }
                }
                if (cov > 0.0) {
                    float sdX = id == 1 ? sd2.x : (id == 2 ? sd2.y : sd2.z);
                    float sdY = id == 1 ? sd3.x : (id == 2 ? sd3.y : sd3.z);
                    vec2 g = vec2(sdX - sd, sdY - sd);
                    g /= max(length(g), 1e-4);
                    vec3 c = shadePiece(id, p, sd, g) * sh;
                    acc = vec4(c, 1.0) * cov + acc * (1.0 - cov);
                }
            }
            // vines with leaves, with a small shadow on the stone (center, shadow probe, gradient in one loop)
            if (Vines) {
                vec3 v0 = vec3(0.0), v1 = vec3(0.0), v2 = vec3(0.0), v3 = vec3(0.0);
                for (int i = Z; i < 4; i++) {
                    if (i == 2 && v0.x >= 0.5) break;
                    vec3 vr = vineSd(p + (i == 1 ? L2 * 0.1 * S : (i == 2 ? vec2(1.0, 0.0) : (i == 3 ? vec2(0.0, 1.0) : vec2(0.0)))));
                    if (i == 0) v0 = vr;
                    else if (i == 1) v1 = vr;
                    else if (i == 2) v2 = vr;
                    else v3 = vr;
                }
                vec3 vs = v0;
                float vshm = (1.0 - smoothstep(-1.0, 0.04 * S + 1.0, v1.x)) * ShadowAmount;
                acc.rgb *= 1.0 - 0.55 * vshm;
                acc.a = max(acc.a, 0.35 * vshm);
                float vc = clamp(0.5 - vs.x, 0.0, 1.0);
                if (vc > 0.0) {
                    vec2 gvn = vec2(v2.x - vs.x, v3.x - vs.x);
                    gvn /= max(length(gvn), 1e-4);
                    float ac = clamp((vs.x + vs.y) / vs.y, 0.0, 1.0);
                    if (vs.z > 0.5) ac *= 0.5;
                    vec3 nv = normalize(vec3(gvn * ac, sqrt(max(1.0 - ac * ac, 0.0)) + 0.05));
                    float dv = max(dot(nv, L3), 0.0);
                    float bark = 0.75 + 0.4 * EwValueNoise2D(p / max(S, 24.0) * 16.0);
                    vec3 vcol = VineColor * bark;
                    if (vs.z > 1.5) { vcol = LeafColor * (0.6 + 0.5 * bark); dv = 0.25 + 0.75 * dv; }
                    else if (vs.z > 0.5) vcol = VineColor * vec3(1.6, 1.3, 0.8) * bark;
                    vec3 vlit = vcol * (vec3(0.6, 0.62, 0.72) * 0.4 + vec3(1.0, 0.88, 0.7) * 1.6 * Sunlight * dv);
                    acc = vec4(vlit, 1.0) * vc + acc * (1.0 - vc);
                }
            }
            if (Grass) {
                // contact shadow of the tufts on the stone, then the front blades
                float side = p.x < 0.0 ? -1.0 : 1.0;
                float dens = 1.0 - smoothstep(0.6 * S, 1.6 * S, abs(p.x - side * HS.x));
                float gao = dens * exp(-gv * gv / (0.012 * S * S + 1.0)) * step(-0.03 * S, gv);
                // shadow offset grows with the height above the ground: 0 at the roots
                float gsh = g1.a * ShadowAmount * (1.0 - 0.35 * smoothstep(0.0, 1.5 * S, gh));
                acc.rgb *= (1.0 - 0.35 * gao) * (1.0 - 0.55 * gsh);
                acc.a = max(acc.a, 0.3 * gsh);
                acc = over(g2, acc);
            }
        }
        if (DebrisAmount > 0.0) acc = over(debris(p), acc);

        // tone map (soft shoulder) on the unpremultiplied color
        col = acc.a > 0.0 ? acc.rgb / acc.a : vec3(0.0);
        col = 1.0 - exp(-col * 1.4);
        a = acc.a;

        // transparent screen border (the stone stays inside it)
        float edge = smoothstep(0.5, 10.0, min(halfRes.x - abs(p.x), halfRes.y - abs(p.y)));
        a *= edge;

        col *= a;
        float dz = EwDither(fragCoord, iTime, 2.0);
        col += dz;
        a = clamp(a + dz, 0.0, 1.0);
        col = clamp(min(col, vec3(a)), 0.0, 1.0);

        a *= 1.0 - Additive;
    }

    vec4 base = bg;
    if (a < 0.999 && InnerOpacity > 0.001 && inner > 0.001) {
        vec4 img = vec4(0.0);
        vec3 imgCol = vec3(0.0);
        if (SwHaveImage) {
            vec2 imgUv = (p - imgCenter) / max(2.0 * imgHalf, vec2(1.0)) + 0.5;
            img = texture(iChannel0, clamp(imgUv, 0.0, 1.0));
            imgCol = shadeImage(img.rgb, p, imgCenter, imgHalf);
        }
        const bool allowCrypt = !SwHaveImage || SwTransparentImage;
        vec3 innerRgb = imgCol;
        if (allowCrypt && (!SwHaveImage || img.a < 0.999)) {
            vec3 cCol = renderCrypt(p, 2.0 * frameE);
            innerRgb = SwHaveImage ? mix(cCol, imgCol, img.a) : cCol;
        }
        innerRgb = clamp(innerRgb + EwDither(fragCoord, iTime, 1.5), 0.0, 1.0);
        float ia = (SwHaveImage && !SwTransparentImage) ? (img.a * InnerOpacity) : InnerOpacity;
        base = over(vec4(innerRgb * ia, ia) * inner, bg);
    }
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
