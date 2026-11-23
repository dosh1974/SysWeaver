//  media: image none
// =============================================================================
//  Caribbean Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Wind | Strength of the breeze in palms, leaves and grass | 0.4 | 0 .. 2 | {0}x
//  iMouse.w = Fireflies | Glow of the fireflies above the grass | 0.4 | 0 .. 2 | {0}x

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Wind animation speed (palms, leaves, grass, coconuts)", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.1405, 0.7188, 0.6971); // var: { "type": "col", "name": "Inner Color", "desc": "Base turquoise color of the sea and inner backdrop" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the sea and sky inside the frame", "scale": 100, "unit": "%" }
const vec3 SkyColor = vec3(0.20, 0.56, 0.94); // var: { "type": "col", "name": "Sky Color", "desc": "Upper sky gradient color inside the frame" }
const float SkyClouds = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Sky Clouds", "desc": "Amount of soft drifting clouds in the background sky", "scale": 100, "unit": "%" }
const float Horizon = 0.38; // var: { "min": 0.15, "max": 0.65, "step": 0.01, "name": "Horizon", "desc": "Vertical position of the sea horizon inside the frame", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }
const float Scale = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Scale", "desc": "Size of palms, leaves, stones and flowers (follows the screen size and grows with the inset)", "unit": "x" }
const float Lean = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Palm Lean", "desc": "How far the palms bend in towards the middle", "unit": "x" }
const float CloudDensity = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Cloud Density", "desc": "Thickness and height of the fluffy white clouds along the top", "unit": "x" }
#define Canopy CloudDensity
const bool BackPalms = true; // var: { "name": "Back Palms", "desc": "Smaller, darker palms behind the side palms and trunks, for depth" }
const vec3 LeafColor = vec3(0.24, 0.50, 0.14); // var: { "type": "col", "name": "Leaf Color", "desc": "Green of palm fronds and grass" }
const vec3 TrunkColor = vec3(0.62, 0.48, 0.34); // var: { "type": "col", "name": "Trunk Color", "desc": "Color of the palm trunks" }
const vec3 SandColor = vec3(0.90, 0.80, 0.60); // var: { "type": "col", "name": "Sand Color", "desc": "Color of the sandy ground" }
const vec3 FlowerColor = vec3(0.95, 0.12, 0.22); // var: { "type": "col", "name": "Flower Color", "desc": "Main hibiscus color (some blooms turn golden)" }
const vec3 SunColor = vec3(1.0, 0.88, 0.70); // var: { "type": "col", "name": "Sun Color", "desc": "Color of the sunlight and dappled highlights" }
const float Exposure = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float Shadow = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow", "desc": "Strength of the cast shadows of leaves and trunks", "scale": 100, "unit": "%" }
const float GrassHeight = 1.0; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Grass Height", "desc": "Height of the grass and flower stems", "unit": "x" }
const float FlowerAmount = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Flowers", "desc": "How many hibiscus flowers grow along the bottom", "scale": 100, "unit": "%" }
const float CoconutInterval = 10.0; // var: { "min": 3.0, "max": 60.0, "step": 1.0, "name": "Coconut Interval", "desc": "Time slot for a possible falling coconut", "unit": "s" }
const float CoconutChance = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Coconut Chance", "desc": "Chance that a coconut falls in each time slot", "scale": 100, "unit": "%" }
const int Quality = 1; // 0 fast, 1 default, 2 high (cloud march steps 8 / 12 / 18)

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define Wind mix(0.0, 2.0, iMouse.z)
#define Fireflies mix(0.0, 2.0, iMouse.w)

const vec3 LDIR = vec3(-0.4811317, 0.5512967, 0.6816039);   // normalized sun from upper left

// crown fronds of the left palm, back to front (precomputed cos/sin of angles; right palm flips x)
const vec2 CrownDir[10] = vec2[10](
    vec2( 0.1205028,  0.9927129), vec2(-0.4614492,  0.8871666),
    vec2( 0.5403023,  0.8414710), vec2(-0.8568888,  0.5155014),
    vec2( 0.8775826,  0.4794255), vec2(-0.9958083,  0.0914653),
    vec2( 0.9968017,  0.0799147), vec2(-0.9175206, -0.3976883),
    vec2( 0.9004471, -0.4349655), vec2( 0.4975710, -0.8674232)
);
const float CrownLen[10] = float[10](0.70, 0.68, 0.80, 0.74, 0.95, 0.76, 1.00, 0.70, 0.92, 0.75);
const vec2 NutOff[5] = vec2[5](vec2(0.35, -0.35), vec2(-0.05, -0.45), vec2(0.20, -0.75), vec2(-0.25, -0.70), vec2(0.55, -0.62));

flat varying vec4 vFrame0;  // (gS, gT, gWind, gRad)
flat varying vec4 vFrame1;  // (gHS.x, gHS.y, gLip, yCeil)
flat varying vec4 vFrame2;  // (fillTop, xMinG, xMaxG, yBaseRel)
flat varying vec4 vPalmL;   // (bL.x, bL.y, CL.x, CL.y)
flat varying vec4 vPalmR;   // (bR.x, bR.y, CR.x, CR.y)
flat varying vec4 vFall;    // (pos.x, pos.y, rot, fade)
flat varying vec4 vBackL0;  // (tb.x, tb.y, C.x, C.y)
flat varying vec4 vBackL1;  // (tb.x, tb.y, C.x, C.y)
flat varying vec4 vBackR0;  // (tb.x, tb.y, C.x, C.y)
flat varying vec4 vBackR1;  // (tb.x, tb.y, C.x, C.y)
flat varying vec4 vWindC;   // (wCL, wCR, wBL0, wBL1)
flat varying vec2 vWindBR;  // (wBR0, wBR1)

float gS, gT, gWind, gRad;
vec2 gHS;
int gZero = 0;   // min(iFrame, 0): keeps the compiler from unrolling the big loops
vec2 gHalf;      // half the screen size
float gLip;      // bound of the ground line's slope |groundY'|
float gDap = 0.0;                 // dappled light noise at the pixel (fronds)
vec4 gStCxA, gStCxB, gStRxA, gStRxB;   // cached stones near the pixel
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
vec4 lay(vec3 c, float a) { return vec4(c * a, a); }
vec3 lin(vec3 c) { return c * c; }
vec3 hazeCol() { return lin(vec3(0.52, 0.66, 0.62)); }
// SIMD vec4 value noise (algebraically identical to EwValueNoise2D / 2DDerivatives / 3D)
float vnoise2(vec2 p)
{
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec4 q = fract(vec4(c, c + 1.0) * 0.1031);
    vec4 px = q.xzxz, py = q.yyww;
    vec4 d = px * (2.0 * py + px + 66.66) + 33.33 * py;
    vec4 h = fract((px + py + 2.0 * d) * (px + d));
    vec2 hy = mix(h.xy, h.zw, u.y);
    return mix(hy.x, hy.y, u.x);
}

vec3 vnoise2D(vec2 p)
{
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 du = 6.0 * f * (1.0 - f);
    vec4 q = fract(vec4(c, c + 1.0) * 0.1031);
    vec4 px = q.xzxz, py = q.yyww;
    vec4 d = px * (2.0 * py + px + 66.66) + 33.33 * py;
    vec4 h = fract((px + py + 2.0 * d) * (px + d));
    float k1 = h.y - h.x;
    float k2 = h.z - h.x;
    float k4 = h.x - h.y - h.z + h.w;
    return vec3(h.x + k1 * u.x + k2 * u.y + k4 * u.x * u.y,
                du * (vec2(k1, k2) + k4 * u.yx));
}

float vnoise3(vec3 p)
{
    vec3 c = floor(p);
    vec3 f = fract(p);
    vec3 u = f * f * (3.0 - 2.0 * f);
    vec3 a = fract(c * 0.1031);
    vec3 b = fract((c + 1.0) * 0.1031);
    vec4 px = vec4(a.x, b.x, a.x, b.x);
    vec4 py = vec4(a.y, a.y, b.y, b.y);
    vec4 pyTerm = py * (py + 31.32);
    vec4 pxpy = px + py;
    vec4 d0 = px * (a.z + 31.32) + pyTerm + a.z * (px + 31.32);
    vec4 h0 = fract((pxpy + 2.0 * d0) * (a.z + d0));
    vec4 d1 = px * (b.z + 31.32) + pyTerm + b.z * (px + 31.32);
    vec4 h1 = fract((pxpy + 2.0 * d1) * (b.z + d1));
    vec4 h = mix(h0, h1, u.z);
    vec2 hy = mix(h.xy, h.zw, u.y);
    return mix(hy.x, hy.y, u.x);
}


// breeze towards +x with gusts travelling across the screen
float windAt(float x)
{
    float g = EwValueNoise1D(gT * 0.45 - x * 0.0015);
    return gWind * (0.35 + 0.65 * g) + gWind * 0.12 * sin(gT * 2.1 + x * 0.004);
}


// sand surface: raised above the frame line, mounds in the corners, gentle dunes
float groundY(float x)
{
    float e = gHS.x - abs(x);
    float cr = max(gRad, 2.0 * gS);
    float mound = 1.8 * gS * exp(-e * e / (cr * cr * 1.5));
    float dunes = gS * 0.45 * (EwValueNoise1D(x / gS * 0.3 + 3.0) - 0.5)
                + gS * 0.12 * (EwValueNoise1D(x / gS * 1.4 + 9.0) - 0.5)
                + gS * 0.05 * (EwValueNoise1D(x / gS * 3.0) - 0.5);
    return -gHS.y + 0.9 * gS + mound + dunes;
}

float trunkSway(float sd)
{
    float w = windAt(sd * gHS.x);
    return gS * (0.5 * w + 0.12 * (0.3 + gWind) * sin(gT * 1.1 + sd * 1.7));
}

void palmGeom(float sd, out vec2 b, out vec2 c)
{
    float bx = sd * (gHS.x - 0.8 * gS);
    b = vec2(bx, groundY(bx) - 0.5 * gS);
    c = vec2(sd * (gHS.x - (0.8 + 2.6 * Lean) * gS) + trunkSway(sd), gHS.y - 2.2 * gS - 0.3 * gRad);
}
// x of a palm trunk's center line at height y
float trunkX(vec2 b, vec2 c, float y)
{
    float t = clamp((y - b.y) / max(c.y - b.y, 1.0), 0.0, 1.0);
    return b.x + (c.x - b.x) * pow(t, 1.7);
}

// curved palm trunk from base b to crown c
vec4 palmTrunk(vec2 q, vec2 b, vec2 c, float w, bool full)
{
    float H = max(c.y - b.y, 1.0);
    float t = (q.y - b.y) / H;
    if (t < 0.0 || t > 1.02) return vec4(0.0);
    float tc = min(t, 1.0);
    float dx = c.x - b.x;
    float cx = b.x + dx * pow(tc, 1.7);
    float slope = dx * 1.7 * pow(max(tc, 1e-3), 0.7) / H;
    float off = (q.x - cx) * inversesqrt(1.0 + slope * slope);
    float tw = w * (1.0 - 0.35 * tc) * (1.0 + 0.35 * smoothstep(0.12, 0.0, tc));
    float a = smoothstep(tw + 1.0, tw - 1.0, abs(off)) * smoothstep(1.02, 0.96, t);
    if (!full || a < 0.003) return vec4(0.0, 0.0, 0.0, a);
    float n = clamp(off / tw, -1.0, 1.0);
    vec2 tg = normalize(vec2(slope, 1.0));
    float nz = sqrt(max(1.0 - n * n, 0.0));
    vec3 N = vec3(vec2(tg.y, -tg.x) * n, nz);
    float diff = max(dot(N, LDIR), 0.0);
    float u = (q.y - b.y) + (q.x - cx) * slope * 0.8;
    float rv = u / (0.24 * gS) + 0.3 * EwValueNoise1D(u / gS * 0.8 + b.x * 0.01) + n * n * 0.25;
    float rid = floor(rv);
    float rr = rv - rid;
    float groove = mix(0.4, 1.0, smoothstep(0.0, 0.2, rr)) * (0.8 + 0.2 * smoothstep(1.0, 0.4, rr));
    float ringH = EwHash1To1(rid + floor(b.x));
    float fib = EwValueNoise2D(vec2(n * 7.0 + rid * 3.1, u / gS * 12.0));
    vec3 bark = lin(TrunkColor) * mix(vec3(0.82, 0.84, 0.86), vec3(1.0), smoothstep(0.0, 0.6, tc));
    bark *= (0.75 + 0.4 * fib) * (0.85 + 0.3 * ringH) * groove;
    bark = mix(bark, lin(vec3(0.33, 0.25, 0.15)), 0.6 * smoothstep(0.82, 1.0, tc));
    float ao = mix(0.5, 1.0, smoothstep(0.0, 0.15, tc)) * mix(1.0, 0.55, smoothstep(0.85, 1.0, tc));
    vec3 col = bark * (0.2 + 1.2 * diff * lin(SunColor)) * ao;
    col += bark * 0.35 * (1.0 - nz) * (1.0 - nz) * lin(vec3(0.6, 0.8, 1.0)) * ao;
    return lay(col, a);
}

// one palm frond: q pixel, o base, e unit direction, L length, wO = windAt(o.x)
vec4 frond(vec2 q, vec2 o, vec2 e, float L, float seed, float shade, bool full, float wO)
{
    vec2 r = q - o;
    float rl2 = dot(r, r);
    if (rl2 > L * L * 1.1) return vec4(0.0);
    float s0 = sqrt(rl2) / L;
    float side = e.x >= 0.0 ? 1.0 : -1.0;
    r.x -= (0.16 * wO + 0.05 * (0.3 + gWind) * sin(gT * 2.3 + seed * 6.1)) * L * s0 * s0;
    float bend = (0.15 + 0.75 * smoothstep(-0.8, 0.6, e.y)) * s0;   // arching under gravity
    r = EwRotate2D(side * bend) * r;
    float x = dot(r, e);
    float s = x / L;
    if (s <= 0.0 || s >= 1.0) return vec4(0.0);
    float W = 0.2 * L;
    float y = e.x * r.y - e.y * r.x;
    y -= W * 0.22 * (0.25 + gWind) * s * s * sin(s * 6.0 - gT * 4.0 + seed * 9.0);
    float at = abs(y);
    bool down = y * side < 0.0;
    float oms = 1.0 - s;
    float env = W * 4.6 * s * (oms * sqrt(sqrt(oms))) * (down ? 1.1 : 0.8);
    if (!full) return vec4(0.0, 0.0, 0.0, 0.85 * smoothstep(env + 0.1 * W, env - 0.3 * W, at));

    float spineW = max(W * 0.05 * (1.0 - 0.7 * s), 0.7);
    if (at >= max(env + 1.0, spineW + 0.8)) return vec4(0.0);

    float sp = L / 31.0;
    float k = (x - 0.9 * at - 0.6 * at * at / W) / sp;
    float id = floor(k);
    float f = k - id;
    float hl = EwHash1To1(id + seed * 37.0 + (down ? 0.0 : 101.0));
    float envL = env * (0.8 + 0.2 * hl) * (hl < 0.05 ? 0.5 : 1.0);
    float rel = at / max(envL, 1e-3);
    float halfW = 0.5 * (0.92 - 0.5 * rel);
    float aaF = 1.0 / sp;
    float leaflet = smoothstep(halfW + aaF, halfW - aaF, abs(f - 0.5));
    leaflet = mix(leaflet, 0.8, smoothstep(5.0, 2.0, sp));
    float edge = smoothstep(envL + 1.0, envL - 1.0, at);
    float spine = smoothstep(spineW + 0.8, spineW - 0.8, at);
    float a = max(leaflet * edge, spine);
    if (a < 0.003) return vec4(0.0);

    // per frond hue: some cool blue green, some fresh, some older and yellower
    float fh = fract(seed * 0.618 + 0.3);
    vec3 ftint = fh < 0.4 ? mix(vec3(0.78, 0.98, 1.12), vec3(1.0), fh / 0.4)
                          : mix(vec3(1.0), vec3(1.25, 1.08, 0.6), (fh - 0.4) / 0.6 * 0.7);
    vec3 base = lin(LeafColor) * ftint * mix(vec3(1.0), vec3(1.12, 1.04, 0.75), hl * 0.5);
    // leaflet as a folded strip: its normal tilts across the leaflet and along the frond
    float u = (f - 0.5) / max(halfW, 0.05);                       // -1 .. 1 across the leaflet
    vec2 dir2 = normalize(vec2(e.x, e.y) + vec2(-e.y, e.x) * sign(y) * 0.9);
    vec3 Nl = normalize(vec3(dir2 * u * 0.8 + vec2(0.0, down ? -0.35 : 0.35), 0.75));
    float dl = max(dot(Nl, LDIR), 0.0);
    float fold = clamp(1.0 - abs(u), 0.0, 1.0);
    vec3 c = base * (0.3 + 0.95 * dl) * (0.75 + 0.5 * hl);
    c *= mix(0.5, 1.0, smoothstep(0.0, 0.35, rel));      // darker at the midrib
    c *= mix(0.45, 1.0, smoothstep(0.0, 0.35, s));       // darker near the crown
    // translucent glow where the sun shines through the thin outer leaflets
    c += base * vec3(1.3, 1.25, 0.45) * 0.4 * rel * rel * (1.0 - dl) * smoothstep(0.2, 0.8, s);
    if (down) c *= vec3(0.78, 0.86, 0.95);
    c = mix(c, lin(vec3(0.46, 0.5, 0.26)) * (0.5 + 0.4 * s), spine);
    float dap = gDap;
    c *= (0.6 + 0.65 * dap) * shade;
    c += lin(SunColor) * base * 0.6 * smoothstep(0.6, 0.9, dap) * shade * fold * dl;
    // depth cue: darker / cooler fronds look further back
    c = mix(c, lin(vec3(0.2, 0.36, 0.32)) * 0.5, 0.2 * smoothstep(0.75, 0.45, shade));
    return lay(c, a);
}

// shorten fronds pointing outwards so their tips stay on screen
float capLen(vec2 o, float cx, float L)
{
    if (cx * o.x <= 0.0) return L;
    float room = gHalf.x - 0.6 * gS - abs(o.x);
    return min(L, max(room, 0.8 * gS) / max(abs(cx) * 0.85, 0.3));
}

vec4 nut(vec2 q, vec2 c, float r, float seed, float rot);   // coconut, defined below

// All palm parts (back palms, main trunk + crown shadow, main crowns + coconuts)
// in ONE 2-deep loop so palmTrunk, frond, and nut each inline only once.
vec4 renderPalms(vec2 p, vec2 qSh, vec4 acc, bool backZone, bool trunkZone, bool crownZone,
                 vec2 CL, vec2 CR, vec2 bS, vec2 cS, float Lc, float sd)
{
    vec4 rT = vec4(0.0);
    float shC = 0.0;
    bool needSh = false;
    // items:
    //  0, 1: back palms (farther, nearer; geometry precomputed in vertexMain)
    //  2   : main trunk
    //  3, 4: crown shadow on main trunk (left, right; only if trunk hit & crownZone)
    //  5, 6: main crown fronds (left, right)
    //  7, 8: main crown coconuts (left, right)
    for (int item = gZero; item < 9; item++) {
        vec2 pq = p;
        bool full = true;
        vec2 C = vec2(0.0);
        float isd = sd;
        int nf = 0, nn = 0;
        bool drawTrunk = false;
        vec2 tb = vec2(0.0), tc = vec2(0.0);
        float tw = 0.0;
        float fk = 0.0, f = 0.0, Lb = 0.0, wC = 0.0;
        bool use = false;

        if (item < 2) {
            if (backZone) {
                fk = 1.0 - float(item);
                f = fk < 0.5 ? 0.66 : 0.45;
                vec4 bp = sd < 0.0 ? (item == 0 ? vBackL0 : vBackL1) : (item == 0 ? vBackR0 : vBackR1);
                wC = sd < 0.0 ? (item == 0 ? vWindC.z : vWindC.w) : (item == 0 ? vWindBR.x : vWindBR.y);
                tb = bp.xy;
                C = bp.zw;
                Lb = Lc * mix(0.55, 0.42, fk);
                bool nearCrown = length(p - C) < Lb * 1.1;
                bool nearTrunk = p.x > min(tb.x, C.x) - 0.6 * gS - 2.0 && p.x < max(tb.x, C.x) + 0.6 * gS + 2.0 && p.y < C.y + 0.2 * gS;
                use = nearCrown || nearTrunk;
                drawTrunk = nearTrunk;
                tc = C;
                tw = 0.45 * gS * mix(0.72, 0.58, fk);
                if (nearCrown) { nf = 10; nn = 3; }
            }
        } else if (item == 2) {
            if (trunkZone) {
                use = true;
                drawTrunk = true;
                tb = bS;
                tc = cS;
                tw = 0.45 * gS;
            }
        } else if (item < 5) {
            if (needSh) {
                int s = item - 3;
                pq = qSh;
                full = false;
                isd = s == 0 ? -1.0 : 1.0;
                C = s == 0 ? CL : CR;
                wC = s == 0 ? vWindC.x : vWindC.y;
                use = length(qSh - C) <= Lc * 1.1;
                nf = 10;
            }
        } else {
            if (crownZone) {
                int s = (item == 5 || item == 7) ? 0 : 1;
                isd = s == 0 ? -1.0 : 1.0;
                C = s == 0 ? CL : CR;
                wC = s == 0 ? vWindC.x : vWindC.y;
                if (item < 7) { use = length(p - C) <= Lc * 1.1; nf = 10; }
                else { use = length(p - C) <= 2.0 * gS; nn = 5; }
            }
        }

        vec4 pa = vec4(0.0);
        if (use) {
            if (drawTrunk) {
                pa = palmTrunk(pq, tb, tc, tw, full);
            }
            if (nf + nn > 0) {
                for (int e = gZero; e < 13; e++) {
                    if (e >= nf + nn) break;
                    if (e < nf) {
                        vec2 eDir = CrownDir[e];
                        if (isd > 0.0) eDir.x = -eDir.x;
                        float L, seed, shade;
                        if (item >= 3) {
                            L = capLen(C, eDir.x, Lc * CrownLen[e]);
                            seed = float(e) * 1.73 + isd * 5.1;
                            shade = 0.6 + 0.045 * float(e);
                        } else {
                            L = capLen(C, eDir.x, Lb * CrownLen[e]);
                            seed = float(e) * 2.11 + sd * 3.7 + fk * 13.0;
                            shade = 0.6 + 0.04 * float(e);
                        }
                        pa = over(frond(pq, C, eDir, L, seed, shade, full, wC), pa);
                    } else {
                        int m = e - nf;
                        float fm = float(m);
                        vec2 nc;
                        float nr, nseed;
                        if (item >= 7) {
                            nc = C + 1.2 * gS * vec2(-isd * NutOff[m].x, NutOff[m].y + 0.1);
                            nr = 0.48 * gS;
                            nseed = fm + isd * 3.0;
                        } else {
                            nc = C + 1.4 * gS * f * vec2(-sd * NutOff[m].x, NutOff[m].y + 0.1);
                            nr = 0.55 * gS * f;
                            nseed = fm + sd * 5.0 + fk * 11.0;
                        }
                        pa = over(nut(pq, nc, nr, nseed, 0.0), pa);
                    }
                }
            }
        }

        if (item < 2) {
            if (pa.a >= 0.003) {
                vec3 cc = pa.rgb / pa.a;
                cc = mix(cc * mix(0.62, 0.48, fk), hazeCol() * 0.32, mix(0.25, 0.4, fk));
                acc = over(lay(cc, pa.a), acc);
            }
        } else if (item == 2) {
            rT = pa;
            needSh = rT.a > 0.003 && crownZone && Shadow > 0.001;
        } else if (item < 5) {
            shC = pa.a + shC * (1.0 - pa.a);
            if (item == 4 && rT.a > 0.0) {
                rT.rgb *= 1.0 - 0.5 * Shadow * shC;
                acc = over(rT, acc);
            }
        } else {
            acc = over(pa, acc);
        }
    }
    return acc;
}

vec4 nut(vec2 q, vec2 c, float r, float seed, float rot)
{
    vec2 v = (q - c) / r;
    float l2 = dot(v, v);
    if (l2 > 1.3) return vec4(0.0);
    float l = sqrt(l2);
    float a = smoothstep(1.0 + 1.0 / r, 1.0 - 1.0 / r, l);
    vec3 N = vec3(v, sqrt(max(1.0 - l2, 0.0)));
    float h = EwHash1To1(seed * 13.7 + 3.0);
    vec3 base = mix(lin(vec3(0.42, 0.50, 0.14)), lin(vec3(0.46, 0.30, 0.13)), h);
    float diff = max(dot(N, LDIR), 0.0);
    float sp = max(dot(N, normalize(LDIR + vec3(0.0, 0.0, 1.0))), 0.0);
    float sp2 = sp * sp; float sp4 = sp2 * sp2; float sp8 = sp4 * sp4;
    float spec = sp8 * sp8 * sp8;
    vec2 rv = EwRotate2D(rot + h * 6.28) * v;
    float eye = smoothstep(0.24, 0.12, length(rv - vec2(0.0, 0.55)));
    vec3 col = base * (0.85 + 0.15 * sin((rv.x + rv.y * 0.3) * 18.0)) * (1.0 - 0.65 * eye);
    col = col * (0.12 + 1.0 * diff * lin(SunColor)) * mix(0.6, 1.0, N.z) + 0.25 * spec * lin(SunColor);
    return lay(col, a);
}

// 3D volumetric cumulus clouds along the top of the frame
float cloudNoise3(vec3 p, int octaves, float m)
{
    float v = 0.0;
    float a = 0.55;
    float norm = 0.0;
    float reqS = 1.0 - m / 0.66;
    for (int i = gZero; i < 4; i++) {
        if (i >= octaves) break;
        float n = vnoise3(p);
        // Mix smooth value noise with rounded cauliflower billows (broad domes, soft clefts)
        float t = 2.0 * n - 1.0;
        float billow = sqrt(t * t + 0.06);
        v += a * mix(n, billow, i == 0 ? 0.42 : 0.32);
        norm += a;
        // Exact early-out: if remaining octaves cannot lift shape above 0, skip them
        if (octaves == 4 && m < 0.66 && i < 3) {
            float rem = i == 0 ? 0.4561 : (i == 1 ? 0.1904 : 0.0618);
            if (smoothstep(0.22, 0.76, (v + rem) * 0.99846) <= reqS) return 0.0;
        }
        p.xy = mat2(0.8, 0.6, -0.6, 0.8) * p.xy;
        p.yz = mat2(0.8, 0.6, -0.6, 0.8) * p.yz;
        p = p * 2.06 + vec3(13.7, 29.1, 17.3);
        a *= 0.48;
    }
    return v / max(norm, 1e-4);
}
// 3D macro volume evaluated at 4 sample points simultaneously via vec4 SIMD
vec4 cloudMacro4(vec4 Px, vec4 Py, vec4 Pz, float xMinG, float xMaxG, float yBaseRel, float spanG, int nPerRow, int total, float fnPerRow, float cellW, int activeMask)
{
    vec4 valid = vec4(lessThanEqual(Py, vec4(-0.02)))
               * vec4(greaterThanEqual(Px, vec4(xMinG - 3.6)))
               * vec4(lessThanEqual(Px, vec4(xMaxG + 3.6)));
    if (dot(valid, valid) < 0.5) return vec4(0.0);

    float cd = CloudDensity;
    vec4 macro = vec4(0.0);

    // Continuous undulating 3D cumulus cloud-street core anchored along fillTop (rounded 3D capsule ends behind palms)
    float xMid = 0.5 * (xMinG + xMaxG);
    float halfCore = max(0.5 * (xMaxG - xMinG) - 1.5, 0.0);
    vec4 dxB = max(abs(Px - xMid) - halfCore, 0.0) / 1.55;
    if (any(lessThan(dxB, vec4(1.0)))) {
        vec4 yUnd = 0.16 * sin(Px * 0.42 - gT * 0.18) + 0.11 * sin(Px * 0.88 - gT * 0.30 + 1.7);
        vec4 zUnd = 0.20 * sin(Px * 0.33 - gT * 0.15 + 0.9);
        float ryBelt = 0.58 + 0.24 * cd;
        vec4 dyB = (Py - (yBaseRel + yUnd)) / ryBelt;
        dyB *= mix(vec4(1.0), vec4(1.15), lessThan(dyB, vec4(0.0)));
        vec4 dzB = (Pz - zUnd) / 1.05;
        vec4 eBelt = clamp(1.0 - (dxB * dxB + dyB * dyB + dzB * dzB), 0.0, 1.0);
        eBelt = smoothstep(0.0, 0.62, eBelt);
        macro = eBelt * 0.96 * vec4(lessThan(dxB, vec4(1.0)));
    }

    if (activeMask == 0) return macro * valid;

    for (int i = gZero; i < 16; i++) {
        if (i >= total || (activeMask >> i) == 0) break;
        if ((activeMask & (1 << i)) == 0) continue;
        int row = i / nPerRow;
        float fi = float(i - row * nPerRow);
        float frow = float(row);
        vec4 h = EwHash1To4(fi * 7.31 + frow * 41.7 + 3.0);
        vec4 h2 = EwHash1To4(fi * 13.17 + frow * 29.3 + 11.0);

        // Varied static drift speeds per cloud & depth row (natural parallax, not a rigid belt)
        float spd = mix(0.20, 0.36, frow) * (0.85 + 0.30 * h.y);
        float phase = (fi + 0.48 * frow + 0.25 + 0.26 * (h.x - 0.5)) / fnPerRow;
        float u = fract(phase + gT * spd / spanG);
        float cx = mix(xMinG, xMaxG, u);

        float rxBase = max(cellW * 0.68, 2.5) * (0.92 + 0.28 * h.z);
        if (all(greaterThan(abs(Px - cx), vec4(rxBase + 2.4)))) continue;

        // Condense organically behind the left palm crown & evaporate behind the right palm crown
        float life = smoothstep(0.0, 0.08, u) * smoothstep(1.0, 0.92, u);
        if (life <= 0.001) continue;
        float sc = 0.38 + 0.62 * life;
        float cz = mix(-0.46, 0.36, frow) + (h2.y - 0.5) * 0.30;
        if (all(greaterThan(Pz, vec4(cz + 1.54 * sc))) || all(lessThan(Pz, vec4(cz - 1.36 * sc)))) continue;
        float lifeVis = smoothstep(0.0, 0.26, life);

        // Smooth breeze tilt (offset only: never scales time by live iMouse.z)
        float leanX = 0.32 * gWind * (1.0 + 0.20 * sin(gT * 0.40 + fi * 1.9));

        // Active convective morphing: individual domes rise, swell, and roll independently
        float ph1 = gT * (0.32 + 0.14 * h2.z) + fi * 2.5 + frow * 3.7;
        float ph2 = gT * (0.27 + 0.12 * h.w)  + fi * 3.1 + frow * 1.4;
        float swell1 = 0.82 + 0.18 * sin(ph1);
        float swell2 = 0.82 + 0.18 * cos(ph2);
        float driftTop = 0.40 * sin(ph1 * 0.7);

        // 1. Broad dense cumulus base body (bridges fillTop with generous overlap)
        float rx = rxBase * sc;
        float ry = (1.16 + 0.34 * h.w) * (0.65 + 0.35 * cd) * sc;
        float rz = (1.08 + 0.26 * h2.x) * sc;
        float cy = yBaseRel - (0.04 + 0.30 * h.y) * cd - 0.16 * frow;
        cy = min(cy, -ry - 0.18);

        vec4 d1x = (Px - cx) / rx;
        vec4 d1y = (Py - cy) / ry;
        d1y *= mix(vec4(1.0), vec4(1.32), lessThan(d1y, vec4(0.0)));
        vec4 d1z = (Pz - cz) / rz;
        vec4 e1 = clamp(1.0 - (d1x * d1x + d1y * d1y + d1z * d1z), 0.0, 1.0);

        // 2. Main towering cauliflower head (varied peak heights -> real sky valleys!)
        float isTall = step(0.34, h2.w);
        float peakClear = mix(0.66 + 0.30 * h.y, 0.06 + 0.18 * h2.w, isTall);
        float rx2 = max(1.50 + 0.62 * h2.z, rxBase * 0.58) * sc;
        float ry2 = (1.30 + 0.58 * h2.w) * (0.78 + 0.22 * cd) * sc * swell1;
        float rz2 = (0.96 + 0.26 * h2.z) * sc;
        float cx2 = cx + (h2.x - 0.5) * rx * 0.50 + leanX + driftTop;
        float cy2 = min(cy + ry * 0.80 + (0.38 + 0.48 * isTall), -ry2 - peakClear);
        float cz2 = cz + (h2.z - 0.5) * 0.26;

        vec4 d2x = (Px - cx2) / rx2;
        vec4 d2y = (Py - cy2) / ry2;
        vec4 d2z = (Pz - cz2) / rz2;
        vec4 e2 = clamp(1.0 - (d2x * d2x + d2y * d2y + d2z * d2z), 0.0, 1.0);

        // 3. Asymmetric side turret puff
        float sideSign = h2.x < 0.5 ? 1.0 : -1.0;
        float rx3 = max(1.24 + 0.46 * h.x, rxBase * 0.48) * sc;
        float ry3 = (1.04 + 0.42 * h.y) * (0.75 + 0.25 * cd) * sc * swell2;
        float cx3 = cx + sideSign * rx * 0.50 + leanX * 0.7 - driftTop * 0.6;
        float cy3 = min(cy + ry * 0.48 + 0.16 * h.y, -ry3 - peakClear - 0.20);
        vec4 d3x = (Px - cx3) / rx3;
        vec4 d3y = (Py - cy3) / ry3;
        vec4 d3z = (Pz - (cz + 0.20 * (h.w - 0.5))) / (rz * 0.84);
        vec4 e3 = clamp(1.0 - (d3x * d3x + d3y * d3y + d3z * d3z), 0.0, 1.0);

        // 4. Foreground lower puff in +z (fluffy billowing undersides hanging over the inner fill)
        float rx4 = max(1.28 + 0.48 * h2.y, rxBase * 0.48) * sc;
        float ry4 = (0.94 + 0.36 * h.z) * (0.62 + 0.38 * cd) * sc * swell2;
        float cx4 = cx - sideSign * rx * 0.26 + leanX * 0.4;
        float cy4 = min(cy - ry * 0.16 - 0.22 * h.y * cd, -ry4 - 0.52);
        float cz4 = cz + 0.48 * sc;
        vec4 d4x = (Px - cx4) / rx4;
        vec4 d4y = (Py - cy4) / ry4;
        d4y *= mix(vec4(1.0), vec4(1.18), lessThan(d4y, vec4(0.0)));
        vec4 d4z = (Pz - cz4) / (rz * 0.78);
        vec4 e4 = clamp(1.0 - (d4x * d4x + d4y * d4y + d4z * d4z), 0.0, 1.0);

        vec4 e = 1.0 - (1.0 - e1) * (1.0 - e2) * (1.0 - e3) * (1.0 - e4);
        macro = 1.0 - (1.0 - macro) * (1.0 - e * lifeVis);
    }

    return macro * valid;
}

float cloudDensPrimary(vec3 P, float m, out vec2 warp)
{
    vec3 np = P * 1.05;
    np.x -= gT * 0.22 + P.y * 0.30 + 0.22 * gWind;
    np.y -= gT * 0.24;
    np.z += gT * 0.16;

    vec3 wp = np * 0.55;
    warp = (vec2(vnoise3(wp + vec3(4.1, 1.7, 2.3)),
                 vnoise3(wp + vec3(9.3, 6.2, 5.1))) - 0.5) * 0.52;
    np.xy += warp;

    float n = cloudNoise3(np, 4, m);
    float shape = m - (1.0 - smoothstep(0.22, 0.76, n)) * 0.66;
    if (shape <= 0.0) return 0.0;

    if (shape < 0.38) {
        float fine = vnoise3(np * 3.6 + vec3(0.0, -gT * 0.35, gT * 0.25));
        shape -= (1.0 - fine) * 0.20 * (1.0 - shape / 0.38);
    }
    return max(shape * 4.4, 0.0);
}

float cloudDensShadow(vec3 P, float m, int octaves, vec2 warp)
{
    vec3 np = P * 1.05;
    np.x -= gT * 0.22 + P.y * 0.30 + 0.22 * gWind;
    np.y -= gT * 0.24;
    np.z += gT * 0.16;
    np.xy += warp;
    float n;
    if (octaves == 1) {
        float n0 = vnoise3(np);
        float t = 2.0 * n0 - 1.0;
        n = mix(n0, sqrt(t * t + 0.06), 0.42);
    } else {
        n = cloudNoise3(np, 2, m);
    }
    return max((m - (1.0 - smoothstep(0.22, 0.76, n)) * 0.66) * 4.4, 0.0);
}

vec4 cloudVolumetric(vec2 q, vec2 fragCoord, float yCeil)
{
    float xMinG = vFrame2.y;
    float xMaxG = vFrame2.z;
    float px = q.x / gS;
    float py = (q.y - yCeil) / gS;
    if (py > 0.0 || px < xMinG - 3.0 || px > xMaxG + 3.0) return vec4(0.0);

    float yBaseRel = vFrame2.w;
    float spanG = max(xMaxG - xMinG, 6.0);
    int nPerRow = clamp(int(ceil(spanG / 6.6)), 4, 8);
    int total = nPerRow * 2;
    float fnPerRow = float(nPerRow);
    float cellW = spanG / fnPerRow;
    float cd = CloudDensity;

    // Pre-cull which clouds can overlap this pixel's primary ray or its sun-shadow rays + tight z range
    float xMid = 0.5 * (xMinG + xMaxG);
    float halfCore = max(0.5 * (xMaxG - xMinG) - 1.5, 0.0);
    float dxB = max(abs(px - xMid) - halfCore, 0.0) / 1.55;
    bool primaryHit = false;
    float zHi = -2.0;
    float zLo = 2.0;
    if (dxB < 1.0) {
        float yUnd = 0.16 * sin(px * 0.42 - gT * 0.18) + 0.11 * sin(px * 0.88 - gT * 0.30 + 1.7);
        float ryBelt = 0.58 + 0.24 * cd;
        if (py + 0.12 > yBaseRel + yUnd - ryBelt / 1.15 && py - 0.12 < yBaseRel + yUnd + ryBelt) {
            primaryHit = true;
            float zUnd = 0.20 * sin(px * 0.33 - gT * 0.15 + 0.9);
            zHi = max(zHi, zUnd + 1.06);
            zLo = min(zLo, zUnd - 1.06);
        }
    }

    int activeMask = 0;
    for (int i = gZero; i < 16; i++) {
        if (i >= total) break;
        int row = i / nPerRow;
        float fi = float(i - row * nPerRow);
        float frow = float(row);
        vec4 h = EwHash1To4(fi * 7.31 + frow * 41.7 + 3.0);
        float spd = mix(0.20, 0.36, frow) * (0.85 + 0.30 * h.y);
        float phase = (fi + 0.48 * frow + 0.25 + 0.26 * (h.x - 0.5)) / fnPerRow;
        float u = fract(phase + gT * spd / spanG);
        float cx = mix(xMinG, xMaxG, u);
        float rxBase = max(cellW * 0.68, 2.5) * (0.92 + 0.28 * h.z);
        // LDIR.x ~ -0.48, so shadow samples shift P.x by up to -0.50
        if (px - cx <= rxBase + 2.4 + 0.55 && cx - px <= rxBase + 2.4 && u > 0.0001 && u < 0.9999) {
            float life = smoothstep(0.0, 0.08, u) * smoothstep(1.0, 0.92, u);
            float sc = 0.38 + 0.62 * life;
            float cy = yBaseRel - (0.04 + 0.30 * h.y) * cd - 0.16 * frow;
            float ry = (1.16 + 0.34 * h.w) * (0.65 + 0.35 * cd) * sc;
            float yMinC = cy - ry * 0.16 - 0.22 * h.y * cd - 1.32 * (0.62 + 0.38 * cd) * sc / 1.18;
            if (py + 0.72 >= yMinC) {
                activeMask |= (1 << i);
                if (abs(px - cx) <= rxBase + 2.4 && py + 0.12 >= yMinC) {
                    primaryHit = true;
                    float czC = mix(-0.46, 0.36, frow);
                    zHi = max(zHi, czC + 0.15 + 1.54 * sc);
                    zLo = min(zLo, czC - 0.15 - 1.36 * sc);
                }
            }
        }
    }
    if (!primaryHit) return vec4(0.0);

    const int STEPS = Quality <= 0 ? 8 : (Quality == 1 ? 12 : 18);   // march steps through the cloud volume
    float zStart = 1.45;
    float zEnd = -1.45;
    float ds = (zStart - zEnd) / float(STEPS);
    int sStart = clamp(int(floor((zStart - zHi) / ds)), 0, STEPS);
    int sEnd = clamp(int(ceil((zStart - zLo) / ds)), sStart, STEPS);

    // Interleaved gradient noise jitter for silky-smooth, banding-free volumetric marching
    float jit = fract(52.9829189 * fract(dot(fragCoord, vec2(0.06711056, 0.00583715))));

    // Crisp Caribbean daylight HDR palette (calibrated for ACES tonemapping):
    // blazing warm-white sunlit tops, clean airy sky-blue undersides, subtle sea bounce
    vec3 sunCol = lin(SunColor);
    vec3 colDirect = mix(vec3(1.0), sunCol, 0.24) * 2.65;
    vec3 colSkyTop = lin(vec3(0.68, 0.84, 1.00)) * 0.58;
    vec3 colSkyBot = lin(vec3(0.35, 0.56, 0.84)) * 0.30;
    vec3 colBounce = lin(vec3(0.22, 0.76, 0.80)) * 0.11;

    vec3 sumCol = vec3(0.0);
    float trans = 1.0;

    for (int sb = sStart + gZero; sb < STEPS; sb += 4) {
        if (sb >= sEnd || trans < 0.008) break;
        vec4 z4 = zStart - (vec4(float(sb), float(sb + 1), float(sb + 2), float(sb + 3)) + jit) * ds;
        vec4 py4 = py + 0.08 * z4;
        vec4 m4 = cloudMacro4(vec4(px), py4, z4, xMinG, xMaxG, yBaseRel, spanG, nPerRow, total, fnPerRow, cellW, activeMask);
        if (max(max(m4.x, m4.y), max(m4.z, m4.w)) <= 0.002) continue;

        // the 4 steps of the block in 2 pairs: one shadow macro lookup serves both steps of a pair
        for (int pr = gZero; pr < 2; pr++) {
            int s0 = sb + 2 * pr;
            if (s0 >= sEnd) break;
            vec2 mP = pr == 0 ? m4.xy : m4.zw;
            vec2 zP = pr == 0 ? z4.xy : z4.zw;
            if (s0 + 1 >= sEnd) mP.y = 0.0;      // beyond the end of the march
            if (max(mP.x, mP.y) <= 0.002) continue;

            // primary densities of both steps
            vec2 dP = vec2(0.0);
            vec4 wP = vec4(0.0);                  // domain warps (xy: 1st step, zw: 2nd step)
            for (int k = gZero; k < 2; k++) {
                float m = k == 0 ? mP.x : mP.y;
                if (m <= 0.002) continue;
                float z = k == 0 ? zP.x : zP.y;
                vec2 warp = vec2(0.0);
                float d = cloudDensPrimary(vec3(px, py + 0.08 * z, z), m, warp);
                if (k == 0) { dP.x = d; wP.xy = warp; } else { dP.y = d; wP.zw = warp; }
            }
            if (max(dP.x, dP.y) <= 0.003) continue;

            // both directional shadow macro lookups of both steps in one SIMD pass
            vec3 PA = vec3(px, py + 0.08 * zP.x, zP.x);
            vec3 PB = vec3(px, py + 0.08 * zP.y, zP.y);
            vec3 A1 = PA + LDIR * 0.36, A2 = PA + LDIR * 1.02;
            vec3 B1 = PB + LDIR * 0.36, B2 = PB + LDIR * 1.02;
            vec4 mSh = cloudMacro4(vec4(A1.x, A2.x, B1.x, B2.x), vec4(A1.y, A2.y, B1.y, B2.y), vec4(A1.z, A2.z, B1.z, B2.z),
                                   xMinG, xMaxG, yBaseRel, spanG, nPerRow, total, fnPerRow, cellW, activeMask);

            for (int k = gZero; k < 2; k++) {
                float dens = k == 0 ? dP.x : dP.y;
                if (dens <= 0.003) continue;
                vec3 P = k == 0 ? PA : PB;
                vec2 warp = k == 0 ? wP.xy : wP.zw;
                vec2 ms = k == 0 ? mSh.xy : mSh.zw;
                vec3 P1 = P + LDIR * 0.36;
                vec3 P2 = P + LDIR * 1.02;
                float dS1 = ms.x > 0.002 ? cloudDensShadow(P1, ms.x, 2, warp) : 0.0;
                float dS2 = ms.y > 0.002 ? cloudDensShadow(P2, ms.y, 1, warp) : 0.0;
                float tau = dS1 * 0.74 + dS2 * 1.28 + dens * 0.16;

                // Dual-lobe Beer-Lambert scattering: bright sunlit crests & sculptural sky-blue crevices
                float direct = max(exp(-tau * 2.5), 0.26 * exp(-tau * 0.54));
                float powder = 1.0 - 0.36 * exp(-dens * 1.8);
                float lit = direct * mix(0.84, 1.16, powder);

                float hGrad = smoothstep(yBaseRel - 1.4 * CloudDensity, -0.20, P.y);
                vec3 ambient = mix(colSkyBot, colSkyTop, hGrad) + colBounce * (1.0 - hGrad);

                vec3 stepCol = ambient * (0.76 + 0.24 * direct) + colDirect * lit;

                // Radiant sunlit silver-lining on thin wispy edges
                float thinEdge = exp(-dens * 1.0) * smoothstep(0.45, 0.92, direct);
                stepCol += colDirect * 0.45 * thinEdge;

                float stepAlpha = 1.0 - exp(-dens * ds * 4.2);
                sumCol += trans * stepCol * stepAlpha;
                trans *= (1.0 - stepAlpha);
                if (trans < 0.008) break;
            }
            if (trans < 0.008) break;
        }
    }

    float a = smoothstep(0.0, 0.992, clamp(1.0 - trans, 0.0, 1.0));
    if (a < 0.002) return vec4(0.0);
    return vec4(sumCol, a);
}

// stones near the current pixel, cached once per pixel (8 cells, centre x / radius; empty cells far away)
void stoneAt(float jj, out float cx, out float rx)
{
    float cw = 1.7 * gS;
    vec3 h = EwHash1To3(jj * 7.3 + 1.0);
    float h2 = EwHash1To1(jj * 3.7 + 2.0);
    rx = gS * (0.45 + 0.45 * h.y);
    cx = (jj + 0.5) * cw + (h2 - 0.5) * 0.5 * cw;
    if (h.x > 0.65) { cx = 1e6; rx = 1.0; }
}

void stoneCache(float x0)
{
    float cw = 1.7 * gS;
    float gwM = max(0.13 * gS * max(GrassHeight, 0.7), 2.0);   // widest blade spacing (front layer)
    float jS = floor((x0 - 7.0 * gwM - 1.2 * gS) / cw - 0.75);
    stoneAt(jS,       gStCxA.x, gStRxA.x);
    stoneAt(jS + 1.0, gStCxA.y, gStRxA.y);
    stoneAt(jS + 2.0, gStCxA.z, gStRxA.z);
    stoneAt(jS + 3.0, gStCxA.w, gStRxA.w);
    stoneAt(jS + 4.0, gStCxB.x, gStRxB.x);
    stoneAt(jS + 5.0, gStCxB.y, gStRxB.y);
    stoneAt(jS + 6.0, gStCxB.z, gStRxB.z);
    stoneAt(jS + 7.0, gStCxB.w, gStRxB.w);
}

// 0..1: how close x is to the edges / foot of a stone (grass tufts grow there)
float stoneTuft(float x)
{
    vec4 eA = abs(abs(x - gStCxA) - 0.8 * gStRxA) / (0.45 * gStRxA);
    vec4 eB = abs(abs(x - gStCxB) - 0.8 * gStRxB) / (0.45 * gStRxB);
    vec4 m = max(smoothstep(1.0, 0.0, eA), smoothstep(1.0, 0.0, eB));
    return max(max(m.x, m.y), max(m.z, m.w));
}
// layer 0 = far back (hazy, cool), 1 = back, 2 = middle, 3 = front, 4 = front tufts rooted in the sand
vec4 grassLayer(vec2 q, float layer, vec4 acc, float gyq)
{
    float dF = layer * 0.25;                                   // 0 far .. 1 front
    float gw = max(mix(0.09, 0.13, dF) * gS * max(GrassHeight, 0.7), 2.0);
    float j0 = floor(q.x / gw);
    float hMul = layer < 0.5 ? 1.25 : layer < 1.5 ? 1.05 : layer < 2.5 ? 0.9 : layer < 3.5 ? 0.7 : 0.5;
    float root = layer < 0.5 ? 0.0 : layer < 1.5 ? -0.08 : layer < 2.5 ? -0.18 : layer < 3.5 ? -0.32 : -0.55;
    float thr = layer < 0.5 ? 0.0 : layer < 1.5 ? 0.08 : layer < 2.5 ? 0.15 : layer < 3.5 ? 0.3 : 0.45;
    // height above this layer's roots, with the ground at the pixel; blades root within gLip * |dx| of it
    float yRel = q.y - gyq - root * gS;
    float slackM = gLip * 7.0 * gw + 1.0;
    if (yRel + slackM < 0.0 || yRel - slackM > gS * GrassHeight * 1.15 * hMul * 1.3) return acc;
    float wr = 1.54 * (0.5 * gw + 1.0);                        // widest blade reach incl. its slope
    vec3 g = lin(LeafColor);
    vec3 dry = lin(vec3(0.66, 0.6, 0.32));
    vec3 sunF = 0.3 + 0.8 * lin(SunColor);
    vec3 layerMul = mix(0.5, 1.1, dF) * mix(vec3(0.85, 0.95, 1.1), vec3(1.05, 1.0, 0.9), dF) * sunF;
    vec3 sandTintLayer = mix(lin(SandColor) * vec3(0.5, 0.5, 0.36), lin(vec3(0.30, 0.30, 0.16)), 0.5) * sunF * mix(0.6, 1.0, dF);
    for (int k = -6 + gZero; k <= 3; k++) {
        float j = j0 + float(k);
        vec3 h = EwHash1To3(j * 1.37 + layer * 91.0);
        float bx = (j + 0.5 + 0.9 * (h.x - 0.5)) * gw;
        float dx = q.x - bx;
        float slack = gLip * abs(dx) + 1.0;
        float hgB = gS * GrassHeight * (0.35 + 0.8 * h.y) * hMul;
        float hgMax = hgB * 1.3;
        if (yRel + slack < 0.0 || yRel - slack > hgMax || dx > 0.585 * hgMax + wr || -dx > 0.27 * hgMax + wr) continue;
        float clump = EwValueNoise1D(bx / gS * 0.7 + layer * 7.3);
        if (layer > 1.5) clump = max(clump, stoneTuft(bx) * (layer > 2.5 ? 0.85 : 0.6));
        if (clump < thr) continue;
        float cl = (clump - thr) / (1.0 - thr);
        float hg = hgB * (0.4 + 0.9 * cl);
        // the blade bends at most -0.27 .. 0.585 hg sideways
        if (dx > 0.585 * hg + wr || -dx > 0.27 * hg + wr || yRel - slack > hg) continue;
        float by = groundY(bx) + root * gS;
        float v = (q.y - by) / hg;
        if (v < 0.0 || v > 1.0) continue;
        float bend = clamp(0.55 * windAt(bx) + 0.18 * (0.3 + gWind) * sin(gT * 3.1 + bx * 0.03 + h.z * 6.0) + (h.z - 0.5) * 0.9, -0.6, 1.3);
        float cx = bx + bend * hg * 0.45 * v * v;
        float slope = 0.9 * bend * v;
        float w = gw * 0.5 * pow(1.0 - v, 0.7) + 0.3;
        float sx = (q.x - cx) * inversesqrt(1.0 + slope * slope);
        float a = smoothstep(w + 0.7, w - 0.7, abs(sx));
        if (a < 0.003) continue;
        // per blade hue: bluish green, fresh green, olive / yellow, some dry
        vec3 tint = h.x < 0.33 ? vec3(0.8, 1.0, 1.15) : (h.x < 0.66 ? vec3(1.0) : vec3(1.2, 1.08, 0.65));
        vec3 bc = mix(g * tint, dry, step(0.9, h.z) * 0.8 + 0.15 * h.y * h.y);
        // folded blade lit from the upper left: one half bright, the other in shade
        float side = clamp(-sx / max(w, 0.5), -1.0, 1.0);
        float lit = 0.55 + 0.45 * side * (bend > 0.0 ? 1.0 : 0.7);
        float ao = mix(0.25, 1.0, smoothstep(0.0, 0.7, v));       // dark, dense base
        vec3 col = bc * ao * (0.7 + 0.5 * h.y) * (0.45 + 0.75 * lit);
        // translucent sunlit tips
        col += bc * vec3(1.2, 1.15, 0.5) * 0.45 * smoothstep(0.55, 1.0, v) * max(side, 0.0);
        // depth: back layers darker and cooler, front layers warmer and brighter
        col *= layerMul;
        // roots fade into the sand: no hard line between grass and ground
        col = mix(sandTintLayer, col, smoothstep(0.0, 0.35, v));
        if (layer > 3.5) a *= smoothstep(0.0, 0.25, v);
        if (layer < 0.5) col = mix(col, hazeCol() * 0.3, 0.35);
        acc = over(lay(col, a), acc);
    }
    return acc;
}

// stones as a layer: result = acc + M * below.rgb (alpha: over)
vec4 stones(vec2 q, inout float M, float gyq)
{
    float slack = gLip * (1.58 * gS + 1.0) + 1.0;
    float yb0 = q.y - gyq;
    if (yb0 > 0.74 * gS + slack || yb0 < -1.02 * gS - slack) return vec4(0.0);
    vec4 acc = vec4(0.0);
{
    float cw = 1.7 * gS;
    float j0 = floor(q.x / cw);
    for (int k = -1 + gZero; k <= 1; k++) {
        float j = j0 + float(k);
        vec3 h = EwHash1To3(j * 7.3 + 1.0);
        if (h.x > 0.65) continue;
        float h2 = EwHash1To1(j * 3.7 + 2.0);
        float rx = gS * (0.45 + 0.45 * h.y);
        float ry = rx * (0.6 + 0.25 * h.z);
        float cx = (j + 0.5) * cw + (h2 - 0.5) * 0.5 * cw;
        if (abs(q.x - cx) > 1.75 * rx + 1.0) continue;     // beyond the stone and its contact shadow
        float gy = groundY(cx);
        vec2 cc = vec2(cx, gy - 0.3 * ry);                    // half sunk into the ground
        // soft contact shadow / damp ring on the sand
        vec2 sv = (q - cc - vec2(0.25 * rx, -0.05 * ry)) / vec2(rx * 1.45, ry * 0.95);
        float sm = 1.0 - 0.35 * smoothstep(1.0, 0.25, length(sv));
        acc.rgb *= sm;
        M *= sm;
        vec2 v = (q - cc) / vec2(rx, ry);
        float l = length(v);
        if (l > 1.25) continue;
        // irregular, slightly flattened outline
        float ang = atan(v.y, v.x);
        float wob = 0.07 * (EwValueNoise1D(ang * 2.0 + j * 5.0) - 0.5) + 0.04 * (EwValueNoise1D(ang * 5.0 + j * 3.0) - 0.5);
        float lr = l * (1.0 + 0.12 * max(-v.y, 0.0)) - wob;
        float a = smoothstep(1.0, -1.0, (lr - 1.0) * ry);
        // buried part: fades into the soil instead of a hard bottom
        float yb = q.y - gy;
        a *= smoothstep(-0.32 * ry, 0.05 * ry, yb + 0.12 * gS * (EwValueNoise1D(q.x / gS * 4.0 + j) - 0.5));
        if (a < 0.003) continue;
        vec3 N = normalize(vec3(v * 0.85, sqrt(max(1.0 - lr * lr, 0.0)) + 0.1));
        float diff = max(dot(N, LDIR), 0.0);
        float sp = max(dot(N, normalize(LDIR + vec3(0.0, 0.0, 1.0))), 0.0);
        float sp2 = sp * sp; float sp4 = sp2 * sp2; float sp8 = sp4 * sp4;
        float spec = sp8 * sp8;
        vec3 col = mix(lin(vec3(0.52, 0.51, 0.48)), lin(vec3(0.64, 0.56, 0.45)), h2);
        float tex = EwValueNoise2D(q / (0.1 * gS) + j * 13.0);
        col *= 0.65 + 0.55 * tex;
        col *= 0.85 + 0.3 * EwValueNoise2D(q / (0.35 * gS) - j * 7.0);
        // moss on the top, dirt splashes at the bottom
        float moss = smoothstep(0.25, 0.85, N.y + 0.3 * (tex - 0.5)) * (0.3 + 0.7 * h.z);
        col = mix(col, lin(LeafColor) * vec3(0.75, 0.8, 0.55), 0.55 * moss);
        float low = smoothstep(0.35 * ry, -0.1 * ry, yb);
        col = mix(col, lin(vec3(0.30, 0.30, 0.16)) * (0.8 + 0.4 * tex), 0.6 * low);
        float ao = mix(0.45, 1.0, smoothstep(-0.1 * ry, 0.6 * ry, yb));
        col = col * (0.14 + 1.0 * diff * lin(SunColor)) * ao + 0.1 * spec * lin(SunColor) * (1.0 - low);
        acc = over(lay(col, a), acc);
        M *= 1.0 - a;
    }
    return acc;
}
}

vec4 flowers(vec2 q, vec4 acc, float gyq)
{
    float fw = 2.3 * gS;
    // widest horizontal reach of a flower (stem sway + leaves / bloom)
    float reach = gS * (0.28 * gWind + 0.08 * (0.3 + gWind) + 0.72) + 2.0;
    if (q.y < gyq - 0.2 * gS - gLip * reach - 1.0) return acc;
    float j0 = floor(q.x / fw);
    for (int k = -1 + gZero; k <= 1; k++) {
        float j = j0 + float(k);
        vec3 h = EwHash1To3(j * 3.1 + 5.0);
        if (h.x >= FlowerAmount) continue;
        float bx = (j + 0.5 + 0.5 * (h.y - 0.5)) * fw;
        if (abs(q.x - bx) > reach) continue;
        float stemH = gS * GrassHeight * (0.75 + 0.45 * h.z);
        float by = groundY(bx) - 0.2 * gS;
        float sway = gS * (0.25 * windAt(bx) + 0.08 * (0.3 + gWind) * sin(gT * 2.0 + h.y * 20.0));
        vec2 head = vec2(bx + sway, by + stemH);
        float R = gS * 0.45 * (0.8 + 0.4 * h.y);
        float v = (q.y - by) / stemH;
        if (v > 0.0 && v < 1.0) {
            float sx = bx + sway * v * v;
            float sw = max(0.045 * gS, 0.8);
            float a = smoothstep(sw + 0.7, sw - 0.7, abs(q.x - sx));
            acc = over(lay(lin(LeafColor) * 0.35, a), acc);
        }
        // two leaves on the stem
        for (int m = 0; m < 2; m++) {
            float lm = m == 0 ? 0.4 : 0.65;
            float ls = (m == 0 ? -1.0 : 1.0) * (h.z < 0.5 ? 1.0 : -1.0);
            vec2 lb = vec2(bx + sway * lm * lm, by + stemH * lm);
            vec2 ld = normalize(vec2(ls, 0.5 + 0.3 * h.z));
            vec2 lr = q - lb;
            float LL = 0.65 * gS;
            float lx = dot(lr, ld);
            if (lx < 0.0 || lx > LL) continue;
            float ly = dot(lr, vec2(-ld.y, ld.x));
            float lw = 0.2 * gS * sin(lx / LL * 3.14159);
            float la = smoothstep(lw + 0.8, lw - 0.8, abs(ly));
            if (la < 0.003) continue;
            float rib = smoothstep(1.2, 0.3, abs(ly));
            vec3 lc = lin(LeafColor) * 0.75 * (0.55 + 0.6 * smoothstep(-lw, lw, ly * ls)) * (1.0 - 0.35 * rib);
            acc = over(lay(lc * (0.3 + 0.8 * lin(SunColor)), la), acc);
        }
        // bloom
        vec2 r = q - head;
        float rl = length(r);
        if (rl > R * 1.05 + 1.5) continue;
        float th = atan(r.y, r.x) + h.z * 6.28 + 0.15 * sin(gT * 1.7 + h.y * 9.0);
        float pr = R * (0.62 + 0.38 * pow(abs(cos(2.5 * th)), 0.7));
        float a = smoothstep(pr + 1.0, pr - 1.0, rl);
        vec3 pc = lin(mix(FlowerColor, vec3(1.0, 0.75, 0.15), step(0.72, h.z)));
        vec3 col = mix(lin(vec3(0.45, 0.0, 0.06)), pc, smoothstep(0.08 * R, 0.45 * R, rl));
        col *= 0.85 + 0.15 * cos(th * 16.0);
        col *= 0.7 + 0.45 * smoothstep(0.3, 1.0, rl / pr) * (0.6 + 0.4 * dot(r / max(rl, 1e-3), normalize(LDIR.xy)));
        col *= 0.5 + 0.7 * lin(SunColor);
        vec2 sdir = vec2(cos(h.y * 6.28), sin(h.y * 6.28));
        float sl = clamp(dot(r, sdir), 0.0, 0.6 * R);
        float sdist = length(r - sdir * sl);
        float stw = max(0.05 * R, 0.8);
        float stamen = smoothstep(stw + 0.6, stw - 0.6, sdist);
        float tip = smoothstep(0.13 * R + 0.8, 0.13 * R - 0.8, length(r - sdir * 0.6 * R));
        col = mix(col, lin(vec3(0.9, 0.2, 0.25)), stamen);
        col = mix(col, lin(vec3(1.0, 0.85, 0.2)), tip);
        acc = over(lay(col, max(a, tip)), acc);
    }
    return acc;
}

vec4 fireflies(vec2 q, vec4 acc, float gyq)
{
    if (Fireflies <= 0.001) return acc;
    float r = max(0.05 * gS, 1.2);
    if (q.y < gyq + 0.2 * gS - (1.0 + gLip) * (10.0 * r + 0.5)) return acc;
    vec3 fc = lin(vec3(1.0, 0.85, 0.35));
    for (int i = gZero; i < 14; i++) {
        float fi = float(i);
        vec3 h = EwHash1To3(fi * 5.31 + 7.0);
        float x = (h.x * 2.0 - 1.0) * (gHS.x - gS) + 1.2 * gS * sin(gT * 0.31 * (0.6 + h.y) + fi);
        if (abs(q.x - x) > 10.0 * r + 0.5) continue;        // glow ends at 10 r
        float y = groundY(x) + gS * (0.6 + 1.4 * h.z) + 0.4 * gS * sin(gT * 0.47 * (0.5 + h.x) + fi * 2.3);
        vec2 v = q - vec2(x, y);
        float dd = dot(v, v);
        if (dd > 100.0 * r * r) continue;
        float blink = smoothstep(0.25, 0.95, 0.5 + 0.5 * sin(gT * (1.1 + h.y) + fi * 4.1));
        float g = (exp(-dd / (r * r)) + 0.25 * exp(-sqrt(dd) / (2.5 * r))) * blink * Fireflies;
        g = min(g, 1.0);
        acc = over(vec4(fc * g * 2.0, g * 0.7), acc);
    }
    return acc;
}


// sandH with its analytic gradient (x = height, yz = d/dq): one pass instead of 3 samples
vec3 sandHD(vec2 q)
{
    vec3 n = vnoise2D(q / (0.9 * gS) + 3.1);
    vec3 m = vnoise2D(q / (1.6 * gS) - 7.7);
    vec2 dm = m.yz / (1.6 * gS);
    float ph = (q.y + 0.12 * q.x + 0.5 * gS * m.x) / (0.13 * gS);
    vec2 dph = (vec2(0.12, 1.0) + 0.5 * gS * dm) / (0.13 * gS);
    float t = clamp((m.x - 0.25) / 0.5, 0.0, 1.0);
    float ss = t * t * (3.0 - 2.0 * t);
    float dss = (m.x > 0.25 && m.x < 0.75) ? 12.0 * t * (1.0 - t) : 0.0;
    float sp = sin(ph);
    float rip = sp * ss;
    vec2 drip = cos(ph) * dph * ss + sp * dss * dm;
    return vec3(0.9 * n.x + 0.07 * rip, 0.9 * n.yz / (0.9 * gS) + 0.07 * drip);
}

// sand + turf as one premultiplied layer (composited over the far grass)
vec4 sandLayer(vec2 q, float gyq)
{
    vec4 acc = vec4(0.0);
    {
        {
            float yb = q.y - gyq;
            float sa = smoothstep(1.0, -1.0, yb);
            if (sa > 0.0) {
                float clT = EwValueNoise1D(q.x / gS * 0.7 + 7.3);   // same clumps as the middle grass
                float depth = max(-yb, 0.0) / gS;
                // lit relief from the height field
                vec3 hd = sandHD(q);
                float h0 = hd.x;
                vec3 N = normalize(vec3(-gS * hd.yz, 1.2));
                // the surface tilts towards the viewer below the crest line
                N = normalize(N + vec3(0.0, 0.55 * smoothstep(0.0, 0.8, depth) - 0.15, 0.0));
                float diff = clamp(0.35 + 0.75 * dot(N, LDIR), 0.0, 1.2);     // wrapped light: soft, no black spots
                float n2 = vnoise2(q / max(0.04 * gS, 1.5));
                vec3 sc = lin(SandColor) * (0.85 + 0.25 * h0) * (0.88 + 0.24 * n2);
                // tiny grains / shell bits
                vec2 gc = q / max(0.09 * gS, 3.0);
                vec2 gi = floor(gc);
                vec3 gh = EwHash1To3(gi.x * 17.0 + gi.y * 131.0);
                float grain = smoothstep(0.32, 0.12, length(gc - gi - 0.2 - 0.6 * gh.xy)) * step(0.6, gh.z);
                sc *= 1.0 + grain * (gh.z > 0.85 ? 0.5 : -0.35);
                // lower dune ridges in front: their crests catch the sun, the hollows behind them are shaded
                float rv = depth + 0.6 * (EwValueNoise1D(q.x / gS * 0.45 + 21.0) - 0.5) + 0.25 * (EwValueNoise1D(q.x / gS * 1.3 + 5.0) - 0.5);
                float rt = fract((rv - 0.7) / 1.25);
                float inR = smoothstep(0.5, 0.8, rv);
                float hollow = smoothstep(0.4, 0.93, rt) * smoothstep(1.0, 0.95, rt) * inR;
                float crest = smoothstep(0.12, 0.0, rt) * inR * step(0.7, rv);
                // dune relief along the top line
                float light = 0.25 + 0.85 * diff;
                if (depth < 2.0) {          // the factor below is exactly 1 deeper down
                    float sl = 0.0;
                    for (int k = gZero; k < 2; k++) sl += (k == 0 ? 1.0 : -1.0) * groundY(q.x + (k == 0 ? 0.4 : -0.4) * gS);
                    sl /= 0.8 * gS;
                    light *= 1.0 + 0.35 * clamp(sl, -1.0, 1.0) * smoothstep(2.0, 0.0, depth);
                }
                light *= 1.0 - 0.45 * hollow;
                light += 0.35 * crest;
                float under = smoothstep(0.05, 0.6, clT);                 // grass above: shade + soil
                light *= mix(1.0, mix(0.4, 1.0, smoothstep(0.0, 0.9, depth)), 0.4 + 0.6 * under);
                sc = mix(sc, sc * vec3(0.65, 0.78, 0.45), (0.25 + 0.4 * under) * smoothstep(0.7, 0.0, depth));
                sc *= mix(1.0, 0.8, smoothstep(2.5, 5.0, depth));
                // warm sunlit tops, cool sky light in the shade
                vec3 lc = mix(lin(vec3(0.75, 0.8, 0.9)), lin(SunColor) * 1.05, smoothstep(0.3, 0.9, diff));
                sc *= light * lc;
                acc = over(lay(sc, sa), acc);
                // turf: soft irregular dark band where the grass is rooted
                float tn = EwValueNoise1D(q.x / gS * 2.3 + 1.7);
                float tm = smoothstep(0.1, 0.55, clT) * smoothstep(-(0.2 + 0.35 * tn) * gS, -0.02 * gS, yb);
                vec3 tc = mix(lin(vec3(0.30, 0.30, 0.16)), lin(LeafColor) * 0.45, 0.5 + 0.5 * tn);
                tc *= (0.8 + 0.4 * n2) * (0.3 + 0.85 * lin(SunColor));
                acc = over(lay(tc, tm * sa * 0.9), acc);
            }
        }
    }
    return acc;
}

vec4 groundLayer(vec2 q, vec4 fall, float gyq)
{
    // Only cache stones when pixel is within the vertical reach of grass layers 2..4
    float gwM = max(0.13 * gS * max(GrassHeight, 0.7), 2.0);
    float slackSt = gLip * 7.0 * gwM + 1.0;
    float ySt = q.y - gyq;
    if (ySt + slackSt >= -0.55 * gS && ySt - slackSt <= gS * GrassHeight * 1.15 * 0.9 * 1.3 - 0.18 * gS) {
        stoneCache(q.x);
    }
    // layers that don't depend on what is below: computed once, outside the grass loop
    vec4 sandL = sandLayer(q, gyq);
    float stM = 1.0;                     // stones darken what is below (contact shadows) ...
    vec4 stL = stones(q, stM, gyq);      // ... and lie over it
    vec4 flL = flowers(q, vec4(0.0), gyq);
    vec4 acc = vec4(0.0);
    // one loop over the 5 grass layers (one copy of the grass code); sand, stones, flowers in between
    for (int L = gZero; L < 5; L++) {
        if (L == 0 && sandL.a >= 0.999) continue; // sand completely covers far grass layer 0
        if (L == 1) acc = over(sandL, acc);
        acc = grassLayer(q, float(L), acc, gyq);
        if (L == 1) {
            acc = vec4(stL.rgb + stM * acc.rgb, stL.a + (1.0 - stL.a) * acc.a);
            acc = over(fall, acc);        // falling coconuts land behind the grass
        }
        if (L == 2) acc = over(flL, acc);
    }
    // the fireflies are layered twice (stronger glow)
    vec4 ff = fireflies(q, vec4(0.0), gyq);
    acc = over(ff, over(ff, acc));
    return acc;
}
// -----------------------------------------------------------------------------
//  Inner Caribbean vista: sky gradient, drifting background clouds & animated sea
// -----------------------------------------------------------------------------
float fbm2(vec2 p, int octaves)
{
    const mat2 octaveTransform = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = gZero; i < 5; i++) {
        if (i >= octaves) break;
        sum += amplitude * vnoise2(p);
        p = octaveTransform * p;
        amplitude *= 0.5;
    }
    return sum;
}

vec3 innerSkyBase(vec3 rd, vec3 sunDir, vec3 innerLin, vec3 skyLin, vec3 sunLin)
{
    float ey = clamp(rd.y, 0.0, 1.0);
    // Clean sunlit tropical horizon harmonized with InnerColor and SkyColor
    vec3 horCol = mix(lin(vec3(0.58, 0.84, 0.94)), innerLin * 0.75 + vec3(0.16, 0.22, 0.25), 0.35);
    vec3 midCol = mix(horCol, skyLin * 1.05, 0.66);
    vec3 zenCol = skyLin * vec3(0.50, 0.70, 0.98);

    vec3 col = mix(horCol, midCol, smoothstep(0.0, 0.16, ey));
    col = mix(col, zenCol, smoothstep(0.10, 0.52, ey));

    // Warm sunlit upper-left glow (focused so it doesn't wash out the blue sky)
    float s = max(dot(rd, sunDir), 0.0);
    float s2 = s * s;
    float s4 = s2 * s2;
    float s7 = s4 * s2 * s;
    float s14 = s7 * s7;
    float s28 = s14 * s14;
    float s42 = s28 * s14;
    float s8 = s4 * s4;
    float s16 = s8 * s8;
    float s32 = s16 * s16;
    float s64 = s32 * s32;
    float s128 = s64 * s64;
    float s260 = s128 * s128 * s4;
    col += sunLin * (0.08 * s7 * (0.5 + 0.5 * exp(-ey * 3.5))
                   + 0.28 * s42
                   + 0.85 * s260);
    return col;
}

float skyCloudField(vec2 p)
{
    vec2 warp = vec2(vnoise2(p * 0.85 + vec2(3.1, 7.4)),
                     vnoise2(p * 0.85 + vec2(9.2, 1.8))) - 0.5;
    return fbm2(p + warp * 0.55, 5) / 0.96875;
}

vec3 innerSky(vec3 rd, vec3 sunDir, vec3 innerLin, vec3 skyLin, vec3 sunLin)
{
    vec3 col = innerSkyBase(rd, sunDir, innerLin, skyLin, sunLin);
    if (SkyClouds <= 0.005) return col;

    // 1. Delicate high-altitude wispy cirrus streaks
    if (rd.y > 0.04) {
        vec2 cuvH = rd.xz / (rd.y + 0.30);
        vec2 cpH = cuvH * vec2(0.55, 1.45) + vec2(-gT * 0.014, gT * 0.004);
        float cirN = fbm2(cpH + vec2(vnoise2(cpH * 0.7) * 0.65, 0.0), 3) / 0.875;
        float cirA = smoothstep(0.62, 0.88, cirN) * 0.16 * SkyClouds * smoothstep(0.04, 0.24, rd.y);
        vec3 cirCol = mix(vec3(0.96, 0.98, 1.02), sunLin * 1.25, 0.30);
        col = mix(col, cirCol, cirA);
    }

    // 2. Sculpted distant trade-wind cumulus puffs hovering across the lower-mid sky
    float bandMask = smoothstep(0.008, 0.085, rd.y) * smoothstep(0.54, 0.18, rd.y);
    if (bandMask > 0.002) {
        vec2 cuv = rd.xz / (rd.y + 0.12);
        vec2 cp = cuv * vec2(0.82, 1.28) + vec2(-gT * 0.052, gT * 0.012);
        float n = skyCloudField(cp);
        float thr = mix(0.78, 0.44, SkyClouds);
        float d = smoothstep(thr, thr + 0.16, n) * bandMask;
        if (d > 0.002) {
            vec2 sunOff = normalize(sunDir.xz + vec2(1e-4)) * 0.16;
            float nSun = fbm2(cp + sunOff * vec2(0.82, 1.28), 3) / 0.875;
            float lit = clamp(0.54 + 2.8 * (n - nSun), 0.10, 1.20);
            float sunDot = max(dot(rd, sunDir), 0.0);
            float sd2 = sunDot * sunDot;
            float sd4 = sd2 * sd2;
            vec3 cShadow = mix(skyLin * 0.62 + vec3(0.18, 0.22, 0.28), vec3(0.42, 0.56, 0.76), 0.55);
            vec3 cLit = mix(vec3(1.18, 1.20, 1.22), sunLin * 1.48, 0.35);
            vec3 cloudCol = mix(cShadow, cLit, smoothstep(0.18, 0.92, lit));
            cloudCol += sunLin * 0.36 * (sd4 * sd4 * sd4) * (1.0 - 0.55 * d);
            // Subtle atmospheric haze near the distant horizon
            float haz = smoothstep(0.10, 0.008, rd.y);
            cloudCol = mix(cloudCol, col * 1.04, haz * 0.42);
            col = mix(col, cloudCol, d * 0.90);
        }
    }
    return col;
}

// Animated multi-directional Caribbean sea wave height (x) + analytic world gradient (yz)
vec3 seaWaves(vec2 wp, float dist)
{
    float amp = 0.72 + 0.42 * gWind;   // amplitude responds to Wind; phase uses static gT only
    float h = 0.0;
    vec2 g = vec2(0.0);

    // 4 directional rolling swells with crisper trochoidal ridges & distance anti-aliasing
    const vec2 dirs[4] = vec2[4](
        vec2( 0.18, 0.984),
        vec2(-0.34, 0.940),
        vec2( 0.56, 0.828),
        vec2(-0.68, 0.733)
    );
    const float freqs[4] = float[4](1.05, 1.75, 2.90, 4.40);
    const float spds[4]  = float[4](1.35, 1.82, 2.35, 2.95);
    const float amps[4]  = float[4](0.088, 0.052, 0.028, 0.015);

    for (int i = gZero; i < 4; i++) {
        float freq = freqs[i];
        float att = 1.0 / (1.0 + dist * freq * 0.020);
        float a = amps[i] * att;
        float ph = dot(wp, dirs[i]) * freq + gT * spds[i];
        float s = sin(ph);
        float c = cos(ph);
        // Sharpened trochoidal-style wave crest: sin(ph) + 0.28 * sin(2*ph)
        h += a * (s + 0.56 * s * c);
        g += dirs[i] * (a * freq * (c + 0.56 * (2.0 * c * c - 1.0)));
    }

    // 3 rotated octaves of animated derivative noise in one loop (single inlined copy)
    const mat2 R2 = mat2(0.8, 0.6, -0.6, 0.8);
    const mat2 R3 = mat2(0.6, -0.8, 0.8, 0.6);
    for (int k = gZero; k < 3; k++) {
        vec2 p = k == 0 ? (wp * 0.52 + vec2(-gT * 0.30, gT * 0.50))
               : k == 1 ? (R2 * wp * 1.45 + vec2(gT * 0.58, gT * 0.84))
                        : (R3 * wp * 3.60 + vec2(-gT * 1.05, gT * 1.28));
        float att = 1.0 / (1.0 + dist * (k == 0 ? 0.014 : k == 1 ? 0.036 : 0.085));
        float wA = (k == 0 ? 0.17 : k == 1 ? 0.095 : 0.048) * att;
        vec3 n = vnoise2D(p);
        h += (n.x - 0.5) * wA;
        vec2 gn = k == 0 ? n.yz : k == 1 ? (transpose(R2) * n.yz) : (transpose(R3) * n.yz);
        g += gn * ((k == 0 ? 0.52 : k == 1 ? 1.45 : 3.60) * wA);
    }

    return vec3(h, g) * amp;
}

vec3 innerSea(vec3 rd, vec3 sunDir, vec3 innerLin, vec3 skyLin, vec3 sunLin)
{
    float dist = min(1.50 / max(-rd.y, 1e-4), 160.0);
    vec2 wp = rd.xz * dist;

    vec3 w = seaWaves(wp, dist);
    float slopeFade = 1.0 / (1.0 + dist * 0.0085);
    vec3 N = normalize(vec3(-w.y * slopeFade, 1.0, -w.z * slopeFade));

    // Vivid Caribbean depth gradient driven by InnerColor (luminous turquoise shallows -> rich marine blue-teal)
    float farT = 1.0 - exp(-dist * 0.042);
    vec3 shallowCol = innerLin * vec3(1.16, 1.24, 1.12) + lin(SandColor) * 0.035;
    vec3 midCol = innerLin * vec3(0.72, 0.92, 0.98);
    vec3 deepCol = mix(innerLin * vec3(0.14, 0.34, 0.58), skyLin * vec3(0.10, 0.26, 0.54), 0.36);
    vec3 bodyCol = mix(shallowCol, midCol, smoothstep(0.0, 0.36, farT));
    bodyCol = mix(bodyCol, deepCol, smoothstep(0.24, 0.88, farT));

    // Wave trough darkening vs crest translucency for crisp 3D swell definition
    float crest = clamp(w.x * 3.8 + 0.45, 0.0, 1.0);
    bodyCol *= mix(0.68, 1.18, crest);

    // Sunlit shallow-water caustic shimmer near the foreground beach
    float nearMask = exp(-dist * 0.13);
    if (nearMask > 0.003) {
        float caust = vnoise2(wp * 0.95 + vec2(w.y * 0.8, gT * 0.42 + w.z * 0.8));
        bodyCol += innerLin * vec3(0.55, 0.80, 0.72) * (caust * caust) * nearMask * 0.42;
    }

    // Translucent aqua subsurface glow in sunlit wave crests
    float sf = max(dot(rd, sunDir), 0.0);
    float sunFwd = sf * sf * sf;
    bodyCol += innerLin * vec3(0.45, 0.80, 0.68) * crest * (0.24 + 0.76 * sunFwd) * (1.0 - 0.60 * farT) * 0.52;
    bodyCol *= 0.56 + 0.44 * max(dot(N, sunDir), 0.0);

    // Physical Fresnel reflection of sky & subtle clouds (restrained so tropical water stays vibrant)
    vec3 R = reflect(rd, N);
    R.y = abs(R.y);
    float NdotV = max(dot(N, -rd), 0.0);
    float fresnel = EwFresnelSchlick(NdotV, 0.025);
    vec3 reflCol = innerSkyBase(R, sunDir, innerLin, skyLin, sunLin);
    if (SkyClouds > 0.005 && R.y > 0.01) {
        vec2 rcuv = R.xz / (R.y + 0.16) * vec2(0.82, 1.28) + vec2(-gT * 0.052, gT * 0.012);
        float rcn = fbm2(rcuv, 3) / 0.875;
        float rthr = mix(0.78, 0.44, SkyClouds);
        float rcd = smoothstep(rthr, rthr + 0.18, rcn) * smoothstep(0.01, 0.12, R.y) * 0.45;
        reflCol = mix(reflCol, mix(vec3(1.06, 1.10, 1.14), sunLin * 1.32, 0.30), rcd);
    }
    vec3 col = mix(bodyCol, reflCol, clamp(fresnel * 0.62, 0.03, 0.68));

    // Crisp sun glitter path: warm sheen + dancing diamond sun sparkles on water ripples
    float sp = max(dot(R, sunDir), 0.0);
    float sp2 = sp * sp;
    float sp4 = sp2 * sp2;
    float sp8 = sp4 * sp4;
    float sp16 = sp8 * sp8;
    float sp32 = sp16 * sp16;
    float sp48 = sp32 * sp16;
    float sp64 = sp32 * sp32;
    float sp128 = sp64 * sp64;
    float sp360 = sp128 * sp128 * sp64 * sp32 * sp8;
    float glints = (sp48 * 0.30 + sp360 * 3.80)
                 * (1.0 - 0.35 * smoothstep(55.0, 150.0, dist));
    col += sunLin * glints;

    // Delicate sea-foam flecks on steep crests & gentle near-shore surf ripples
    float slopeMag = length(w.yz);
    float crestFade = exp(-dist * 0.038);
    float shoreBand = exp(-dist * 0.21);
    if (crestFade > 0.004 || shoreBand > 0.002) {
        float foamN = EwValueNoise2D(wp * 2.4 + vec2(-gT * 0.45, gT * 0.65));
        float crestFoam = smoothstep(0.56, 0.96, w.x * 2.8 + slopeMag * 0.46 + (foamN - 0.5) * 0.40)
                        * (0.20 + 0.38 * gWind) * crestFade;
        float shoreFoam = 0.0;
        if (shoreBand > 0.002) {
            float shoreWave = sin(dist * 2.4 - gT * 1.8 + foamN * 2.5);
            shoreFoam = smoothstep(0.62, 0.96, shoreWave) * shoreBand * 0.38 * (0.45 + 0.55 * foamN);
        }
        float foam = clamp(crestFoam + shoreFoam, 0.0, 0.62);
        col = mix(col, mix(vec3(0.88, 0.96, 1.0), sunLin * 1.20, 0.30), foam);
    }

    // Subtle horizon atmospheric perspective (keeps the distant sea-sky horizon crisp and legible)
    vec3 horizFog = innerSkyBase(normalize(vec3(rd.x, 0.01, rd.z)), sunDir, innerLin, skyLin, sunLin);
    float fog = 1.0 - exp(-dist * 0.006);
    col = mix(col, horizFog, fog * 0.34);
    return col;
}

vec3 innerBackdrop(vec2 p, vec2 fragCoord, vec2 res, float yBot, float yTop)
{
    float yHor = mix(yBot, yTop, Horizon);
    float dy = (p.y - yHor) / res.y;
    float aa = 1.5 / res.y;
    float skyMix = smoothstep(-aa, aa, dy);
    vec3 sunDir = normalize(vec3(-0.38, 0.34, 0.86));
    vec3 innerLin = lin(InnerColor);
    vec3 skyLin = lin(SkyColor);
    vec3 sunLin = lin(SunColor);

    vec3 cSea = vec3(0.0);
    vec3 cSky = vec3(0.0);
    if (skyMix < 1.0) {
        cSea = innerSea(normalize(vec3(p.x / res.y, min(dy, -aa * 0.5), 1.05)), sunDir, innerLin, skyLin, sunLin);
    }
    if (skyMix > 0.0) {
        cSky = innerSky(normalize(vec3(p.x / res.y, max(dy, aa * 0.5), 1.05)), sunDir, innerLin, skyLin, sunLin);
    }
    vec3 col = mix(cSea, cSky, skyMix);
    col = EwLinearToSrgb(EwTonemapAces(col * Exposure));
    return clamp(col + EwDither(fragCoord, iTime, 1.5), 0.0, 1.0);
}

// -----------------------------------------------------------------------------
//  Picture inside the frame (image media)
// -----------------------------------------------------------------------------
// frame line half size hs and frame area half size halfA: the screen, or with a picture its aspect (contained)
void frameRect(vec2 res, out float inset, out vec2 hs, out vec2 halfA)
{
    inset = min(Inset, 0.25 * min(res.x, res.y));
    hs = 0.5 * res - inset;
    if (SwHaveImage) {
        vec2 ts = max(iChannelResolution[0].xy, vec2(1.0));
        float ar = ts.x / ts.y;
        hs = hs.x > hs.y * ar ? vec2(hs.y * ar, hs.y) : vec2(hs.x, hs.x / ar);
    }
    halfA = hs + inset;
}

// shadow of the nearest palm (trunk + crown) on the picture: it lies behind the frame, so the shadow falls further down right
float imageShadow(vec2 p, vec2 bL, vec2 CL, vec2 bR, vec2 CR)
{
    vec2 q = p - gS * vec2(0.8, -1.0);
    float sd = q.x < 0.0 ? -1.0 : 1.0;
    vec2 b = sd < 0.0 ? bL : bR;
    vec2 C = sd < 0.0 ? CL : CR;
    float sh = palmTrunk(q, b, C, 0.45 * gS, false).a;
    float Lc = 6.0 * gS;
    if (length(q - C) <= Lc * 1.1) {
        float wC = sd < 0.0 ? vWindC.x : vWindC.y;
        for (int e = gZero; e < 10; e++) {
            vec2 eDir = CrownDir[e];
            if (sd > 0.0) eDir.x = -eDir.x;
            float L = capLen(C, eDir.x, Lc * CrownLen[e]);
            float a = frond(q, C, eDir, L, float(e) * 1.73 + sd * 5.1, 1.0, false, wC).a;
            sh = a + sh * (1.0 - a);
        }
    }
    return sh;
}

// the picture (premultiplied; opaque unless SwTransparentImage), lit by the sun and shaded by palms, clouds and grass
vec4 imageLayer(vec2 p, vec2 hs, float fillTop, vec2 bL, vec2 CL, vec2 bR, vec2 CR)
{
    vec2 uv = clamp((p + hs) / (2.0 * hs), 0.0, 1.0);
    vec4 t = texture(iChannel0, uv);
    vec3 c = t.rgb;
    float a = SwTransparentImage ? t.a : 1.0;

    // cast shadows of the palms, broken up by dappled light through the fronds
    float sh = 0.0;
    if (Shadow > 0.001) {
        sh = imageShadow(p, bL, CL, bR, CR);
        if (sh > 0.003) {
            float dap = EwValueNoise2D(p / (2.5 * gS) + vec2(gT * 0.25, gT * 0.1));
            sh *= 1.0 - 0.45 * smoothstep(0.55, 0.9, dap);
        }
    }
    // soft occlusion below the cloud belt and above the grass
    float occ = 0.45 * smoothstep(fillTop - 2.5 * gS, fillTop + 0.3 * gS, p.y)
              + 0.35 * smoothstep(-hs.y + 5.0 * gS, -hs.y + 1.5 * gS, p.y);
    float shade = clamp(Shadow * (1.3 * sh + occ), 0.0, 0.85);
    c = mix(c, c * vec3(0.40, 0.48, 0.62), shade);   // cool, sky lit shadow

    // warm sunlight from the upper left (screen blend: never clips, keeps the picture's contrast)
    vec2 sp = (p - hs * vec2(-0.8, 0.95)) / max(1.2 * min(hs.x, hs.y), 1.0);
    float glow = 0.42 * exp(-dot(sp, sp)) + 0.06 * clamp(0.5 - 0.5 * dot(p / hs, vec2(0.6, 0.8)), 0.0, 1.0);
    vec3 lightC = SunColor * glow * (1.0 - 0.8 * shade);
    c = 1.0 - (1.0 - c) * (1.0 - lightC);
    return vec4(c * a, a);
}

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    float inset;
    vec2 hs, halfA;
    frameRect(res, inset, hs, halfA);
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float S = max(Scale * (2.0 * min(halfA.x, halfA.y) / 1080.0) * (24.0 + 0.45 * inset), 4.0);
    gS = S; gT = mod(iTime * Speed, 3600.0); gWind = Wind;
    gHS = hs; gRad = rad; gHalf = halfA;
    gLip = 0.7 + 1.27 * S / max(rad, 2.0 * S);

    vec2 bL, CL, bR, CR;
    palmGeom(-1.0, bL, CL);
    palmGeom(1.0, bR, CR);
    float yCeil = gHalf.y - max(14.0, 0.32 * S);
    float fillTop = min(hs.y - 0.3 * S, yCeil - 1.85 * S);
    float xMinG = max(CL.x - 1.5 * S, -gHalf.x + 1.1 * S) / S;
    float xMaxG = min(CR.x + 1.5 * S,  gHalf.x - 1.1 * S) / S;
    float yBaseRel = (fillTop - yCeil) / S;

    vFrame0 = vec4(S, gT, gWind, rad);
    vFrame1 = vec4(hs.x, hs.y, gLip, yCeil);
    vFrame2 = vec4(fillTop, xMinG, xMaxG, yBaseRel);
    vPalmL = vec4(bL, CL);
    vPalmR = vec4(bR, CR);

    // Precompute all 4 back palms (2 left, 2 right) once per frame
    float HmL = CL.y - bL.y;
    float HmR = CR.y - bR.y;
    float swL = trunkSway(-1.0);
    float swR = trunkSway(1.0);
    for (int s = 0; s < 2; s++) {
        float sd = s == 0 ? -1.0 : 1.0;
        float Hm = s == 0 ? HmL : HmR;
        float sw = s == 0 ? swL : swR;
        for (int item = 0; item < 2; item++) {
            float fk = 1.0 - float(item);
            vec3 h = EwHash1To3(fk * 7.0 + sd * 3.0 + 2.0);
            float f = fk < 0.5 ? 0.66 : 0.45;
            float bx = sd * (hs.x - (2.2 + 2.6 * fk + 0.8 * h.x) * S);
            vec2 tb = vec2(bx, groundY(bx) - 0.3 * S);
            float lean = fk < 0.5 ? (1.0 + 0.8 * h.y) : -(0.3 + 0.5 * h.y);
            vec2 C = vec2(bx - sd * lean * S * f * 1.5 + sw * f, tb.y + f * Hm);
            float wC = windAt(C.x);
            if (s == 0 && item == 0) { vBackL0 = vec4(tb, C); vWindC.z = wC; }
            else if (s == 0 && item == 1) { vBackL1 = vec4(tb, C); vWindC.w = wC; }
            else if (s == 1 && item == 0) { vBackR0 = vec4(tb, C); vWindBR.x = wC; }
            else { vBackR1 = vec4(tb, C); vWindBR.y = wC; }
        }
    }
    vWindC.x = windAt(CL.x);
    vWindC.y = windAt(CR.x);

    // Precompute falling coconut trajectory once per frame
    vFall = vec4(0.0);
    float P = max(CoconutInterval, 1.0);
    float cyc = floor(gT / P);
    float tc = gT - cyc * P;
    vec2 hc = EwHash1To2(cyc + 17.0);
    if (hc.x < CoconutChance && tc <= 6.0) {
        float fsd = hc.y < 0.5 ? -1.0 : 1.0;
        vec2 C = fsd < 0.0 ? CL : CR;
        vec2 st = C + S * vec2(-fsd * 0.3, -0.8);
        float r = 0.38 * S;
        float sink = 0.15;
        float yg = groundY(st.x) + r * sink;
        float H = max(st.y - yg, 1.0);
        float tf = 1.1;
        float g = 2.0 * H / (tf * tf);
        vec2 pos = st;
        float rot = 0.0;
        if (tc < tf) {
            pos.y = st.y - 0.5 * g * tc * tc;
        } else {
            float tb = tc - tf;
            float vb = sqrt(2.0 * g * 0.05 * H);
            float hop = max(vb * tb - 0.5 * g * tb * tb, 0.0);
            float dist = 1.6 * S * (1.0 - exp(-1.5 * tb));
            pos.x = st.x - fsd * dist;
            pos.y = groundY(pos.x) + r * sink + hop;
            rot = fsd * dist / r;
        }
        float fade = smoothstep(0.0, 0.15, tc) * smoothstep(6.0, 3.8, tc);
        vFall = vec4(pos, rot, fade);
    }
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    gZero = min(iFrame, 0);

    float S = vFrame0.x;
    gS = S; gT = vFrame0.y; gWind = vFrame0.z; gRad = vFrame0.w;
    vec2 hs = vFrame1.xy;
    float insetF;
    vec2 hsF, halfF;
    frameRect(res, insetF, hsF, halfF);
    gHS = hs; gLip = vFrame1.z; gHalf = halfF;
    float yCeil = vFrame1.w;
    float fillTop = vFrame2.x;
    vec2 bL = vPalmL.xy, CL = vPalmL.zw;
    vec2 bR = vPalmR.xy, CR = vPalmR.zw;

    float px = abs(p.x);
    float sd = p.x < 0.0 ? -1.0 : 1.0;
    float Lc = 6.0 * S;

    bool crownZone = min(length(p - CL), length(p - CR)) < Lc * 1.15 + S;
    bool topZone = p.y > fillTop - (1.15 + 1.10 * CloudDensity) * S;
    bool sideZone = px > hs.x - max(7.5, 5.5 + 2.6 * Lean) * S;
    float botTop = (2.8 * GrassHeight + 1.0) * S;
    float gyq = 0.0;
    bool botZone = false;
    if (p.y < -hs.y + 3.02 * S + botTop + 1.0) {   // the ground never rises above -hs.y + 3.01 S
        gyq = groundY(p.x);
        botZone = p.y < gyq + botTop;
    }
    bool backZone = BackPalms && px > hs.x - 9.5 * S;

    vec4 acc = vec4(0.0);
    bool anyFg = crownZone || topZone || sideZone || botZone || backZone;
    if (anyFg) {
        // dappled light on the fronds: the same for every frond at this pixel
        if (crownZone || backZone) gDap = EwValueNoise2D(p / (2.5 * gS) + vec2(gT * 0.25, gT * 0.1));

        vec2 off = S * vec2(0.35, -0.45);
        vec2 q = p - off;   // shadow lookup
        bool trunkZone = sideZone || crownZone;
        vec2 bS = sd < 0.0 ? bL : bR;
        vec2 cS = sd < 0.0 ? CL : CR;

        // 1. clouds along the top (behind the palms!)
        if (topZone) {
            acc = over(cloudVolumetric(p, fragCoord, yCeil), acc);
        }
        // 2..4. back palms, main trunk (+ crown shadow on trunk), corner crowns + coconuts
        if (backZone || trunkZone || crownZone) {
            acc = renderPalms(p, q, acc, backZone, trunkZone, crownZone, CL, CR, bS, cS, Lc, sd);
        }
        vec4 fall = vec4(0.0);
        if (vFall.w > 0.001) {
            float fr = 0.38 * S;
            if (length(p - vFall.xy) <= fr * 1.2) {
                fall = nut(p, vFall.xy, fr, 9.0, vFall.z) * vFall.w;
            }
        }
        if (botZone) {
            // irregular, soft fade of the ground towards the screen edges (rounded corners)
            float er = min(2.5 * S, min(gHalf.x, gHalf.y));
            float dE = -EwSdRoundedBox2D(p, gHalf, er);
            float n = EwValueNoise2D(p / (1.3 * S) + 4.0) + 0.5 * EwValueNoise2D(p / (0.5 * S) + 9.0) - 0.75;
            float gm = smoothstep(0.05 * S, 0.95 * S, dE + 0.55 * S * n);
            acc = over(groundLayer(p, fall, gyq) * gm, acc);
        } else {
            acc = over(fall, acc);
        }
    }

    vec3 col = vec3(0.0);
    float a = 0.0;
    float aPreDither = 0.0;
    if (anyFg) {
        a = clamp(acc.a, 0.0, 1.0);
        col = acc.rgb / max(acc.a, 1e-4);
        col = EwLinearToSrgb(EwTonemapAces(col * Exposure)) * a;
        float edge = min(gHalf.x - abs(p.x), gHalf.y - abs(p.y));   // edges of the frame area (the screen without a picture)
        float em = smoothstep(0.5, 10.0, edge);
        col *= em;
        a *= em;
        aPreDither = a * (1.0 - Additive);
        float dz = EwDither(fragCoord, iTime, 2.0);
        col = max(col + dz, 0.0);
        a = clamp(a + dz, 0.0, 1.0);
        col = min(col, vec3(a));
        a *= 1.0 - Additive;
    }

    // inner fill: only evaluate when not completely occluded by the foreground
    vec4 bg = vec4(Background * Opacity, Opacity);
    vec4 base = bg;
    if (aPreDither < 0.999 && InnerOpacity > 0.001) {
        // the inner area: inside the frame line, between the trunks, below the cloud belt
        float d = EwSdRoundedBox2D(p, hs, gRad);
        float xL = trunkX(bL, CL, p.y);
        float xR = trunkX(bR, CR, p.y);
        float inner = smoothstep(0.5, -0.5, d + 0.3 * S) * smoothstep(-0.5, 0.5, p.x - xL) * smoothstep(0.5, -0.5, p.x - xR)
                    * smoothstep(0.10 * S, -0.10 * S, p.y - fillTop);
        if (inner > 0.001) {
            if (SwHaveImage) {
                // the picture (its aspect, contained in the frame), clipped to the inner area; sea / sky only below a transparent picture
                vec4 inn = imageLayer(p, hs, fillTop, bL, CL, bR, CR);
                if (SwTransparentImage && inn.a < 0.999) {
                    inn = over(inn, vec4(innerBackdrop(p, fragCoord, res, -hs.y + 0.8 * S, fillTop), 1.0));
                }
                base = over(inn * (InnerOpacity * inner), bg);
            } else {
                vec3 innerCol = innerBackdrop(p, fragCoord, res, -hs.y + 0.8 * S, fillTop);
                base = over(vec4(innerCol * InnerOpacity, InnerOpacity) * inner, bg);
            }
        }
    }

    if (!anyFg) { fragColor = base; return; }
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
