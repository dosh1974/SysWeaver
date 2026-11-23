//  media: image none
// =============================================================================
//  Bamboo Frame
// =============================================================================
//  An overlay frame of two crooked, weathered bamboo poles lashed together with
//  twisted grass rope. Knotted rope ends hang down and swing in a gusty wind,
//  caterpillars crawl from hole to hole along the outer pole and butterflies
//  flutter along just inside the frame. Transparent middle, premultiplied alpha.
//
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Wind | Strength of the breeze swinging the hanging rope ends | 0.5 | 0 .. 2 | {0}x
//  iMouse.w = Weathering | Cracks, stains, speckles and scars on the bamboo | 0.5 | 0 .. 1 | {0}

// --- Shared frame parameters ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Wind, caterpillar and butterfly animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.6125, 0.5902, 0.5345); // var: { "type": "col", "name": "Inner Color", "desc": "Color of the zen garden sand inside the frame" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the zen sand inside the frame", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

// --- Zen sand ---
const int SandPattern = 0; // 0 Ripples & Flow, 1 Flowing Waves, 2 Seigaiha, 3 Concentric, 4 Basketweave
const float SandScale = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Sand Scale", "desc": "Spacing of the raked sand grooves", "unit": "x" }
const float SandDepth = 0.75; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Sand Depth", "desc": "Relief depth of the raked sand grooves", "scale": 100, "unit": "%" }
const float SandGrain = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Sand Grain", "desc": "Fine granular sand texture and mineral flecks", "scale": 100, "unit": "%" }

// --- Colors ---
const vec3 BambooColor = vec3(0.72, 0.70, 0.36); // var: { "type": "col", "name": "Bamboo Color", "desc": "Color of the bamboo poles" }
const vec3 NodeColor = vec3(0.45, 0.38, 0.16); // var: { "type": "col", "name": "Node Color", "desc": "Color of the node rings and spots" }
const vec3 RopeColor = vec3(0.78, 0.66, 0.38); // var: { "type": "col", "name": "Rope Color", "desc": "Color of the grass rope" }

// --- Bamboo and rope shape ---
const float Thickness = 0.85; // var: { "min": 0.4, "max": 1.0, "step": 0.01, "name": "Thickness", "desc": "Width of the bamboo band relative to the inset", "scale": 100, "unit": "%" }
const float Crookedness = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Crookedness", "desc": "How much the bamboo poles bend and kink at their nodes", "scale": 100, "unit": "%" }
const float RopeLength = 1.0; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Rope Length", "desc": "Length of the hanging rope ends", "unit": "x" }
const float BindingWidth = 0.48; // var: { "min": 0.25, "max": 0.8, "step": 0.01, "name": "Binding Width", "desc": "Width of the rope bindings relative to the bamboo band", "unit": "x" }
const bool SideRope = true; // var: { "name": "Side Rope", "desc": "A rope winding around the pole along the left side" }

// --- Lighting ---
const float ShadowAmount = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow", "desc": "Soft drop shadow of the frame", "scale": 100, "unit": "%" }
const float InnerShade = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Shade", "desc": "Darkens the inner pole for depth, most on the light facing side", "scale": 100, "unit": "%" }
const float Brightness = 1.0; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Brightness", "desc": "Light intensity", "unit": "x" }
const float LightAngle = 135.0; // var: { "min": 0.0, "max": 360.0, "step": 1.0, "name": "Light Angle", "desc": "Direction the light comes from (0 = right, 90 = top)", "unit": "°" }
const vec3 SunColor = vec3(1.0, 0.93, 0.82); // var: { "type": "col", "name": "Sun Color", "desc": "Color of the light (warm by default)" }
const float Exposure = 0.9; // var: { "min": 0.4, "max": 2.0, "step": 0.05, "name": "Exposure", "desc": "Overall exposure before tone mapping: lower = darker, more contrast", "unit": "x" }

// --- Caterpillars ---
const int Caterpillars = 3; // var: { "min": 0, "max": 6, "step": 1, "name": "Caterpillars", "desc": "Number of caterpillars crawling along the outer pole" }
const float CaterpillarSize = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Caterpillar Size", "desc": "Size of the caterpillars", "unit": "x" }
const float CaterpillarSpeed = 1.2; // var: { "min": 0.2, "max": 4.0, "step": 0.05, "name": "Caterpillar Speed", "desc": "How fast the caterpillars inch along", "unit": "x" }
const vec3 CaterpillarColor = vec3(0.38, 0.62, 0.12); // var: { "type": "col", "name": "Caterpillar Color", "desc": "Body color of the caterpillars" }

// --- Butterflies ---
const int Butterflies = 3; // var: { "min": 0, "max": 8, "step": 1, "name": "Butterflies", "desc": "Number of butterflies fluttering along the frame" }
const float ButterflySize = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Butterfly Size", "desc": "Size of the butterflies", "unit": "x" }
const float ButterflySpeed = 1.0; // var: { "min": 0.2, "max": 3.0, "step": 0.05, "name": "Butterfly Speed", "desc": "How fast the butterflies travel along the frame", "unit": "x" }
const float FlapRate = 2.2; // var: { "min": 0.5, "max": 6.0, "step": 0.1, "name": "Flap Rate", "desc": "Wing beats per second", "unit": "Hz" }
const vec3 ButterflyColor = vec3(1.0, 0.5, 0.08); // var: { "type": "col", "name": "Butterfly Color", "desc": "Wing color of the first kind of butterfly" }
const vec3 ButterflyColor2 = vec3(0.2, 0.45, 1.0); // var: { "type": "col", "name": "Butterfly Color 2", "desc": "Wing color of the second kind of butterfly" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define Wind mix(0.0, 2.0, iMouse.z)
#define Weathering mix(0.0, 1.0, iMouse.w)

// premultiplied "over" compositing
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// ---- compile-time lighting & color constants ----
const float cLA = radians(LightAngle);
const vec3 cL = normalize(vec3(cos(cLA), sin(cLA), 1.25));
const vec3 cH = normalize(cL + vec3(0.0, 0.0, 1.0));
const vec3 cSun = SunColor * 1.3 * Brightness;
const vec2 cOffS = cL.xy / cL.z;
const vec2 cL2 = normalize(cL.xy + 1e-4);
const vec3 cLs = normalize(vec3(cL.xy, 0.76));
const vec3 cHs = normalize(cLs + vec3(0.0, 0.0, 1.0));

const vec3 cBamboo = pow(max(BambooColor, vec3(0.0)), vec3(2.2));
const vec3 cNode = pow(max(NodeColor, vec3(0.0)), vec3(2.2));
const vec3 cRope = pow(max(RopeColor, vec3(0.0)), vec3(2.2));
const vec3 cInner = pow(max(InnerColor, vec3(0.0)), vec3(2.2));
const vec3 cCat = pow(max(CaterpillarColor, vec3(0.0)), vec3(2.2));
const vec3 cBf1 = pow(max(ButterflyColor, vec3(0.0)), vec3(2.2));
const vec3 cBf2 = pow(max(ButterflyColor2, vec3(0.0)), vec3(2.2));

// ---- vertex-to-fragment varyings (14 slots) ----
varying vec2 vP;
flat varying vec4 vFrame;   // hs.x, hs.y, rad, H
flat varying vec4 vFrame2;  // Q, c1, reachFrame, reachAll
flat varying vec4 vTime;    // gT, gWindAmt, bx, spPitch
flat varying vec4 vHoles0;  // holePos 0..3
flat varying vec4 vHoles1;  // holePos 4..7
flat varying vec4 vSand;    // pitch, borderW, f1, grainPx
flat varying vec4 vSand2;   // span.x, span.y, R1, R2
flat varying vec4 vBf0;     // butterfly 0: bp.xy, ang, phi
flat varying vec4 vBf1;     // butterfly 1: bp.xy, ang, phi
flat varying vec4 vBf2;     // butterfly 2: bp.xy, ang, phi
flat varying vec4 vBf3;     // butterfly 3: bp.xy, ang, phi
flat varying vec4 vBfRoll;  // butterfly 0..3 roll
flat varying vec4 vBfHgt;   // butterfly 0..3 shadow height

// stops the compiler from unrolling loops (smaller, faster compiling code)
#define ZERO min(iFrame, 0)

// smooth wind with gusts, about -1 .. 1
float windAt(float ph)
{
    float t = vTime.x;
    float n = EwValueNoise1D(t * 0.4 + ph * 1.3) * 2.0 - 1.0;
    float gust = smoothstep(0.45, 0.9, EwValueNoise1D(t * 0.23 + ph * 0.2 + 5.0));
    return (0.4 * sin(t * 0.9 + ph * 2.1) + 0.2 * sin(t * 1.9 + ph * 3.3 + 1.0) + 0.5 * n) * (0.7 + 0.8 * gust) + 0.35 * gust;
}

// hemisphere ambient + sun diffuse + glossy specular + rim (linear)
vec3 lit(vec3 alb, vec3 N, float specAmt, float gloss)
{
    float dif = max(dot(N, cL), 0.0);
    vec3 amb = mix(vec3(0.15, 0.12, 0.09), vec3(0.32, 0.38, 0.48), 0.5 + 0.5 * N.y) * (0.5 + 0.5 * N.z);
    float nh = max(dot(N, cH), 0.0);
    float nh2 = nh * nh;
    float sp = specAmt * (pow(nh, gloss) + 0.06 * nh2 * nh2 * nh);
    float f = 1.0 - clamp(N.z, 0.0, 1.0);
    float fr = (f * f) * (f * f);
    return alb * (amb + cSun * dif) + cSun * sp + vec3(0.45, 0.55, 0.7) * fr * 0.3 * specAmt;
}

// perimeter position (clockwise from top center), outward normal
float frameCoords(vec2 p, vec2 hs, float rad, float Q, out vec2 n2)
{
    vec2 b = hs - rad;
    vec2 q = abs(p);
    vec2 v = q - b;
    float s1;
    vec2 n;
    if (v.x > 0.0 && v.y > 0.0) {
        s1 = b.x + rad * atan(v.x, v.y);
        n = normalize(v);
    } else if (v.y >= v.x) {
        s1 = q.x; n = vec2(0.0, 1.0);
    } else {
        s1 = b.x + rad * 1.5707963 + (b.y - q.y); n = vec2(1.0, 0.0);
    }
    n2 = n * vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
    if (p.x >= 0.0) return p.y >= 0.0 ? s1 : 2.0 * Q - s1;
    return p.y < 0.0 ? 2.0 * Q + s1 : 4.0 * Q - s1;
}

// crooked pole: center offset (px), kinks at the nodes + a slight bow per segment
float poleOff(float s, float seg, float n, float seed, float amp)
{
    float sp = s / seg + seed;
    float i = floor(sp);
    float f = sp - i;
    float a = EwHash1To1(mod(i, n) + seed * 31.0 + 0.5) - 0.5;
    float b = EwHash1To1(mod(i + 1.0, n) + seed * 31.0 + 0.5) - 0.5;
    float bow = (EwHash1To1(mod(i, n) + seed * 57.0 + 3.0) - 0.5) * 4.0 * f * (1.0 - f);
    return amp * (mix(a, b, f) + 0.5 * bow);
}

// one bamboo pole: x across (px, + outward), s along (px)
// one bamboo pole: x across (px, + outward), s along (px)
vec4 pole(float x, float r, float s, float seg, float seed, vec2 n2, vec2 tg, float wear)
{
    float sp = s / seg + seed;
    float id = floor(sp);
    float u = sp - id;
    float nd = min(u, 1.0 - u) * seg;
    float ridge = exp(-nd * nd / (0.012 * r * r + 0.5));
    float re = r * (1.0 + 0.07 * ridge + 0.015 * sin(6.2832 * u));
    float cov = clamp(re - abs(x) + 0.5, 0.0, 1.0);
    if (cov <= 0.0) return vec4(0.0);
    float t = clamp(x / re, -1.0, 1.0);
    float ct = sqrt(max(1.0 - t * t, 0.0));
    float sid = mod(id, 997.0) + seed * 13.0;
    float slope = (u < 0.5 ? -1.0 : 1.0) * ridge * 0.9;
    float dent = (EwValueNoise2D(vec2(s / (0.5 * r + 1.0), t * 2.5 + seed * 9.0)) - 0.5) * wear;
    vec3 N = normalize(vec3(n2 * (t + 0.25 * dent) + tg * (slope + 0.3 * dent) * ct, ct));

    float h = EwHash1To1(sid);
    vec3 alb = cBamboo * (0.85 + 0.3 * h);
    alb = mix(alb, alb * vec3(0.8, 1.05, 0.7), 0.5 * EwHash1To1(sid + 1.7));
    float fib = 0.6 * EwValueNoise1D(t * 28.0 + sid * 7.3) + 0.4 * EwValueNoise1D(t * 70.0 + sid * 3.1);
    alb *= 0.88 + 0.24 * fib;
    float mot = EwValueNoise1D(s / (0.5 * r + 1.0) + sid * 3.1 + t * 0.4) * EwValueNoise1D(s / (1.3 * r + 1.0) - sid);
    alb = mix(alb, cNode, 0.7 * smoothstep(0.3, 0.7, mot) * h);
    alb = mix(alb, cNode, ridge * 0.6);
    float powder = smoothstep(0.01, 0.03, u) * smoothstep(0.16, 0.05, u);
    alb = mix(alb, vec3(0.7549, 0.7549, 0.5469) * 0.7, powder * 0.35);

    // ---- imperfections ----
    float st = EwValueNoise2D(vec2(s / (1.4 * r + 1.0), t * 1.3 + seed * 7.0)) * EwValueNoise2D(vec2(s / (0.4 * r + 1.0) + 3.1, t * 3.5));
    float stain = smoothstep(0.15, 0.4, st) * wear;
    alb = mix(alb, alb * vec3(0.5, 0.47, 0.42) + vec3(0.015, 0.014, 0.012), stain * 0.8);
    alb *= 1.0 - 0.35 * wear * exp(-nd / (0.15 * r + 1.0)) * (1.0 - ridge);
    float bl = smoothstep(0.55, 0.8, EwValueNoise2D(vec2(s / (2.5 * r + 1.0) + 11.0, t * 0.8 + seed)));
    alb = mix(alb, vec3(dot(alb, vec3(0.33))) * 1.15, 0.6 * bl * wear);
    // speckles / short streaks
    vec2 cc = vec2(s / (0.22 * r + 1.0), x / (0.07 * r + 1.0));
    vec2 cf = fract(cc) - 0.5;
    float hp = EwHash2To1(floor(cc) + seed * 41.0);
    float spk = hp < 0.06 * wear ? smoothstep(0.45, 0.15, length(cf * vec2(1.0, 1.6) + (hp - 0.03) * 4.0)) : 0.0;
    alb *= 1.0 - 0.45 * spk;
    // half moon bud scar just after some nodes
    if (EwHash1To1(sid * 2.3 + 0.9) < 0.1 + 0.35 * wear) {
        float ts = (EwHash1To1(sid * 3.9) - 0.5) * 0.9;
        vec2 e = vec2(u * seg - 0.04 * r, (t - ts) * re) / vec2(0.2 * r + 1.0, 0.3 * r + 1.0);
        float le = length(e);
        float scar = smoothstep(1.0, 0.8, le) * step(0.0, e.x);
        alb = mix(alb, mix(cNode, alb, 0.4) * 0.8, scar * 0.6);
        alb *= 1.0 - 0.45 * smoothstep(0.12, 0.0, abs(le - 0.85)) * scar;
        N = normalize(N + vec3(tg * 0.5 * scar * smoothstep(0.3, 0.9, le), 0.0));
    }
    // a lengthwise split crack in some segments
    float sh = 1.0;
    if (EwHash1To1(sid * 1.91 + 0.3) < 0.6 * wear) {
        float t0 = (EwHash1To1(sid * 2.7 + 1.1) - 0.5) * 1.1;
        float ua = 0.1 + 0.35 * EwHash1To1(sid * 3.3);
        float ub = min(ua + 0.25 + 0.45 * EwHash1To1(sid * 4.1), 0.95);
        float cw = smoothstep(ua, ua + 0.12, u) * smoothstep(ub, ub - 0.15, u);
        float cx = t0 + 0.05 * (EwValueNoise1D(u * seg / (0.25 * r + 1.0) + sid) - 0.5);
        float dcx = (t - cx) * re;
        float wc = (0.35 + 0.03 * r) * cw;
        float crack = cw > 0.01 ? smoothstep(wc + 0.7, wc * 0.3, abs(dcx)) : 0.0;
        float lip = smoothstep(wc + 2.0 + 0.05 * r, wc, abs(dcx)) * (1.0 - crack) * cw;
        alb = mix(alb, alb * 1.1 + 0.03, lip * 0.5);
        sh = 1.0 - 0.9 * crack;
    }

    float groove = 1.0 - smoothstep(0.0, max(0.7, 0.035 * r), nd);
    vec3 col = lit(alb, N, 0.9 * (1.0 - 0.6 * stain), 70.0);
    col *= (1.0 - 0.7 * groove) * (0.55 + 0.45 * ct) * sh;
    return vec4(col * cov, cov);
}

// rope tube with a 2-ply twist: sd signed px across its center line, gS screen direction of +sd
vec4 ropeTube(float sd, float w, vec2 gS, float along, float seed)
{
    float cov = clamp(w - abs(sd) + 0.5, 0.0, 1.0);
    if (cov <= 0.0) return vec4(0.0);
    float t = clamp(sd / w, -1.0, 1.0);
    float ct = sqrt(max(1.0 - t * t, 0.0));
    vec2 aS = vec2(-gS.y, gS.x);
    float ph = along / (1.3 * w) + t * 0.55 + seed;
    float cb = 2.0 * fract(ph) - 1.0;
    float sb = sqrt(max(1.0 - cb * cb, 0.0));
    vec3 N = normalize(vec3(gS * t, ct) * (0.4 + 0.6 * sb) + vec3(aS * cb * 0.6, 0.0));
    float fib = EwValueNoise1D(cb * 6.0 + floor(ph) * 7.0 + seed * 3.0);
    float strands = 0.85 + 0.15 * sin((cb * 0.8 + t * 3.0) * 9.0);
    vec3 alb = cRope * (0.8 + 0.3 * fib) * strands;
    vec3 col = lit(alb, N, 0.25, 18.0);
    col *= (0.3 + 0.7 * sb) * (0.45 + 0.55 * ct);
    return vec4(col * cov, cov);
}

// grass rope wrapped around a pole: x across the pole, ds along the frame (px)
vec4 ropeWrap(float x, float r, float ds, float wB, float slant, float seed, vec2 n2, vec2 tg, float H)
{
    float R = r + 0.09 * H;
    float covX = clamp(R - abs(x) + 0.5, 0.0, 1.0);
    if (covX <= 0.0) return vec4(0.0);
    float ragged = 0.04 * H * (EwValueNoise1D(x / (0.08 * H + 1.0) + seed * 5.0) - 0.5);
    float cov = covX * clamp(wB + ragged - abs(ds) + 0.5, 0.0, 1.0);
    if (cov <= 0.0) return vec4(0.0);
    float t = clamp(x / R, -1.0, 1.0);
    float ct = sqrt(max(1.0 - t * t, 0.0));
    float pitch = 2.0 * wB / 5.0;
    float ph = (ds + wB + slant * pitch * 0.35 * asin(t)) / pitch;
    float id = floor(ph);
    float c = 2.0 * fract(ph) - 1.0;
    float sc = sqrt(max(1.0 - c * c, 0.0));
    vec2 g = normalize(tg + n2 * slant * pitch * 0.35 / (R * max(ct, 0.25)));
    vec3 Ncyl = vec3(n2 * t, ct);
    float tw = fract(x / (0.16 * H + 1.0) + c * 0.5 + EwHash1To1(id + seed));
    float tws = sin(3.1416 * tw);
    vec3 N = normalize(Ncyl * sc + vec3(g * c * 0.9, 0.0) + vec3(n2 * cos(3.1416 * tw) * 0.3, 0.0));
    float hId = EwHash1To1(id * 1.7 + seed * 9.0);
    float fib = EwValueNoise1D(c * 7.0 + id * 11.0 + tw * 3.0);
    float strands = 0.85 + 0.15 * sin((tw + c * 0.3) * 18.0);
    vec3 alb = cRope * (0.75 + 0.4 * hId) * (0.85 + 0.3 * fib) * strands;
    vec3 col = lit(alb, N, 0.25, 18.0);
    col *= (0.25 + 0.75 * sc) * (0.7 + 0.3 * tws) * (0.5 + 0.5 * ct);
    return vec4(col * cov, cov);
}

// geometry of the free rope tubes
float tubeGeo(int e, vec2 q, float xS, float sS, float R, float pitch, float H, float len, float ph, float seed,
              vec2 n2, vec2 tg, out float sd, out float w, out vec2 gS, out float along, out float ts, out float shade)
{
    sd = 1e4; w = 1.0; gS = vec2(1.0, 0.0); along = 0.0; ts = seed; shade = 1.0;
    if (e == 0) {
        w = 0.08 * H + 0.4;
        if (abs(xS) > R + w) return 0.0;
        float t = clamp(xS / R, -1.0, 1.0);
        float th = asin(t);
        float ct = cos(th);
        float f = sS / pitch - th / 6.2831853;
        float k = floor(f + 0.5);
        float tp = pitch / 6.2831853;
        float rc = R * max(ct, 0.2);
        sd = (f - k) * pitch * rc / sqrt(tp * tp + rc * rc);
        gS = normalize(tg * rc - n2 * tp);
        along = th * R; ts = k * 1.7; shade = 0.45 + 0.55 * ct;
        return clamp(R + 0.3 * w - abs(xS) + 0.5, 0.0, 1.0);
    }
    if (e < 3) {
        float y = -q.y;
        w = 0.068 * H + 0.4;
        float side = e == 1 ? -1.0 : 1.0;
        float L = len * (0.8 + 0.35 * EwHash1To1(seed * 3.1 + side));
        if (y < -w || y > L + w) return 0.0;
        float u = clamp(y / L, 0.0, 1.0);
        float F = windAt(ph + side * 0.3) * vTime.y;
        float S = L * 0.7 * F;
        float A = L * 0.07 * (0.2 + vTime.y);
        float wa = u * 4.0 - vTime.x * 2.4 + side * 1.3 + ph * 3.0;
        float swa = sin(wa);
        float xc = side * (0.05 * H + 0.1 * y) + S * u * u + A * u * swa;
        float slope = side * 0.1 + 2.0 * S * u / L + A / L * (swa + 4.0 * u * cos(wa));
        float sl = sqrt(1.0 + slope * slope);
        w *= 1.0 - 0.2 * u;
        float fray = smoothstep(0.78, 0.9, u);
        sd = (q.x - xc) / sl / (1.0 + 0.6 * fray);
        float tt = clamp(sd / w, -1.0, 1.0) * 0.5 + 0.5;
        float uEnd = 0.86 + 0.14 * EwHash1To1(floor(tt * 4.0) + seed * 7.0 + side);
        float strand = smoothstep(0.48, 0.3, abs(fract(tt * 4.0) - 0.5));
        gS = vec2(1.0, slope) / sl;
        along = y * sl; ts = side * 0.3 + seed;
        return mix(1.0, strand, fray) * clamp((uEnd * L - y) + 0.5, 0.0, 1.0);
    }
    vec2 kv = vec2(q.x, -q.y);
    if (e < 5) {
        vec2 v = kv - ((e == 3) ? vec2(-0.075 * H, 0.0) : vec2(0.075 * H, 0.02 * H));
        float lv = length(v);
        sd = lv - 0.1 * H; w = 0.062 * H + 0.4;
        vec2 gl = lv > 1e-3 ? v / lv : vec2(1.0, 0.0);
        gS = vec2(gl.x, -gl.y);
        along = atan(v.y, v.x) * 0.1 * H; ts = seed + float(e - 3);
        return 1.0;
    }
    // constant direction ab = vec2(0.41, 0.17) * H, |ab| = 0.44384682 * H
    vec2 ab = vec2(0.41, 0.17) * H;
    vec2 av = kv - vec2(-0.21, -0.08) * H;
    float hh = clamp(dot(av, ab) / (0.1970 * H * H), 0.0, 1.0);
    vec2 cl = av - ab * hh;
    const vec2 nrm = vec2(-0.38301502, 0.92374212);
    sd = (dot(cl, nrm) < 0.0 ? -1.0 : 1.0) * length(cl);
    w = 0.068 * H + 0.4;
    gS = vec2(nrm.x, -nrm.y);
    along = hh * (0.44384682 * H); ts = seed + 5.0;
    return 1.0;
}


// inching crawl progress (monotonic, pauses between steps)
float crawl(float t) { return t - sin(6.2831853 * t) / 6.2831853; }

// vertex-stage binding/hole layout helpers (compiled only into vertex shader)
float bindPosV(int m, float Q, float c1)
{
    int j = m / 2;
    if (m % 2 == 0) return float(j) * Q;
    return (j == 0) ? c1 : (j == 1) ? 2.0 * Q - c1 : (j == 2) ? 2.0 * Q + c1 : 4.0 * Q - c1;
}

float holePosV(int m, float Q, float c1)
{
    float a = bindPosV(m, Q, c1), b = bindPosV(m + 1, Q, c1);
    float f = 0.5 + 0.36 * (EwHash1To1(float(m) * 3.1 + 0.4) - 0.5);
    return a + f * (b - a);
}

float getHole(int idx)
{
    vec4 h = (idx < 4) ? vHoles0 : vHoles1;
    int k = idx & 3;
    return (k == 0) ? h.x : (k == 1) ? h.y : (k == 2) ? h.z : h.w;
}

// a chewed hole in the bamboo: l = (along, across) px from its center
vec4 holeLayer(vec2 l, float Rh, float id, vec2 n2, vec2 tg)
{
    float ang = atan(l.y, l.x);
    float rag = 1.0 + 0.12 * (EwValueNoise1D(ang * 2.5 + id * 7.0) - 0.5) + 0.06 * (EwValueNoise1D(ang * 9.0 + id) - 0.5);
    float r = length(l / vec2(1.0, 0.9)) / (Rh * rag);
    vec2 dirS = normalize(l.x * tg + l.y * n2 + 1e-4);
    float L2 = dot(dirS, cL2);
    float e = 0.6 / max(Rh, 1.0);
    float rim = smoothstep(1.4, 1.1, r) * smoothstep(1.0 - e, 1.0 + e, r);
    float ao = smoothstep(1.9, 1.05, r);
    float inside = smoothstep(1.0 + e, 1.0 - e, r);
    vec3 rimAlb = cBamboo * vec3(1.15, 1.08, 0.85) * (0.75 + 0.35 * EwValueNoise1D(ang * 14.0 + id));
    vec3 rimN = normalize(vec3(dirS * (r < 1.18 ? -0.8 : 0.6), 1.0));
    vec4 res = vec4(0.0, 0.0, 0.0, 0.35 * ao);
    res = over(vec4(lit(rimAlb, rimN, 0.3, 30.0) * rim, rim), res);
    vec3 ic = vec3(0.004, 0.003, 0.002) + cBamboo * 0.3 * cSun * smoothstep(0.45, 0.98, r) * max(-L2, 0.0);
    res = over(vec4(ic * inside, inside), res);
    return res;
}

// a caterpillar's current trip from one hole to the next: false when pixel s is not on its path
bool catSetup(float id, float s, float rr, float bodyLen, float C, float Lp,
              out float tc, out float dir, out float D, out float stride, out float a, out float wig)
{
    float T = vTime.x * CaterpillarSpeed * (0.85 + 0.3 * EwHash1To1(id * 3.7 + 0.2)) + id * C * 0.37;
    float cyc = floor(T / C);
    tc = T - cyc * C;
    int i0 = clamp(int(floor(EwHash2To1(vec2(id * 7.3 + 0.1, cyc)) * 8.0)), 0, 7);
    dir = EwHash2To1(vec2(cyc, id * 3.1 + 5.0)) < 0.5 ? 1.0 : -1.0;
    int i1 = (i0 + (dir > 0.0 ? 1 : 7)) & 7;
    float s0 = getHole(i0), s1 = getHole(i1);
    D = max(mod(dir * (s1 - s0), Lp), 1.0);
    stride = (D + bodyLen) / (C - 6.0);
    a = mod(dir * (s - s0) + 3.0 * rr, Lp) - 3.0 * rr;
    wig = 0.3 * (EwHash1To1(cyc + id * 9.0) - 0.5);
    return a <= D + 3.0 * rr;
}

// one body segment k (0 = head) of a caterpillar: x across the outer pole (px); shadow out
vec4 catSegment(float fi, float id, float tc, float dir, float D, float stride, float a, float wig,
                float x, float rr, float sp, float rA, vec2 n2, vec2 tg, vec2 offL, out float shadow)
{
    shadow = 0.0;
    float tk = tc + fi * 0.07;
    float lift = 1.0 - cos(6.2831853 * tk);
    float ak = stride * (crawl(tk) - fi * 0.07) - fi * sp;
    float vis = smoothstep(-0.3 * rr, 0.8 * rr, ak) * smoothstep(D + 0.3 * rr, D - 0.8 * rr, ak);
    if (vis < 0.02) return vec4(0.0);
    bool head = fi < 0.5;
    float ri = rr * vis * (1.0 + 0.08 * lift) * (head ? 1.08 : 1.0 - 0.025 * fi);
    float xi = -0.4 * rA + wig * rA * sin(3.14159 * clamp(ak / D, 0.0, 1.0)) + 0.18 * rr * sin(fi * 0.9 - tc * 3.0) * vis;
    vec2 l = vec2(a - ak, x - xi);
    vec2 ls = l + offL * vec2(dir, 1.0) * rr * (0.5 + 0.6 * lift);
    float limB = ri + 0.5;
    float limS = ri * 1.2;
    if (abs(l.x) > limB && abs(ls.x) > limS) return vec4(0.0);
    shadow = smoothstep(limS, ri * 0.6, length(ls));
    float cov = clamp(limB - length(l), 0.0, 1.0);
    if (cov <= 0.0) return vec4(0.0);
    vec2 u = l / ri;
    float luSq = dot(u, u);
    float z = sqrt(max(1.0 - luSq, 0.0));
    vec2 nS = u.x * dir * tg + u.y * n2;
    vec3 N = normalize(vec3(nS, z + 0.05));
    vec3 alb;
    float lu = sqrt(luSq);
    if (head) {
        alb = mix(cCat * 0.6, vec3(0.2686, 0.1726, 0.0290), 0.4);
        vec2 e1 = u - vec2(0.45, 0.38), e2 = u - vec2(0.45, -0.38);
        float eye = smoothstep(0.2, 0.12, min(length(e1), length(e2)));
        alb = mix(alb, vec3(0.002), eye);
    } else {
        alb = cCat * (0.9 + 0.2 * EwHash1To1(fi + id * 13.0));
        alb *= mix(1.0, 0.45, smoothstep(0.45, 0.9, abs(u.y)));
        alb = mix(alb, vec3(0.00137, 0.00084, 0.00018), smoothstep(0.1, 0.0, abs(abs(u.y) - 0.62)) * 0.8);
        float spot = smoothstep(0.3, 0.18, lu);
        alb = mix(alb, vec3(1.0, 0.4856, 0.00944), spot * 0.9);
        float ring = smoothstep(0.08, 0.0, abs(lu - 0.33));
        alb = mix(alb, vec3(0.00137, 0.00084, 0.00018), ring * 0.8);
    }
    vec3 col = lit(alb, N, head ? 0.15 : 0.35, 40.0);
    col += cSun * cCat * 0.12 * smoothstep(0.7, 1.0, lu) * (head ? 0.0 : 1.0);
    col *= 0.5 + 0.5 * z;
    return vec4(col * cov, cov);
}

// point on a rounded box path at perimeter position s (same layout as frameCoords), outward normal n
vec2 framePoint(float s, vec2 hs, float rad, float Q, out vec2 n)
{
    s = mod(s, 4.0 * Q);
    vec2 sg = vec2(1.0);
    float s1 = s;
    if (s >= 3.0 * Q) { s1 = 4.0 * Q - s; sg = vec2(-1.0, 1.0); }
    else if (s >= 2.0 * Q) { s1 = s - 2.0 * Q; sg = vec2(-1.0, -1.0); }
    else if (s >= Q) { s1 = 2.0 * Q - s; sg = vec2(1.0, -1.0); }
    vec2 b = hs - rad;
    float arc = rad * 1.5707963;
    vec2 pt, nn;
    if (s1 < b.x) { pt = vec2(s1, hs.y); nn = vec2(0.0, 1.0); }
    else if (s1 < b.x + arc) { float an = (s1 - b.x) / max(rad, 1e-3); nn = vec2(sin(an), cos(an)); pt = b + rad * nn; }
    else { pt = vec2(hs.x, b.y - (s1 - b.x - arc)); nn = vec2(1.0, 0.0); }
    n = nn * sg;
    return pt * sg;
}

// butterfly flight path: wandering along the frame, just inside it
vec2 butterflyPos(float id, float t, vec2 hs, float rad, float Q, float H, out vec2 n)
{
    float h1 = EwHash1To1(id * 5.1 + 0.3), h2 = EwHash1To1(id * 9.7 + 1.1);
    float dir = mod(id, 2.0) < 0.5 ? 1.0 : -1.0;
    float v = ButterflySpeed * (0.9 + 0.8 * h1) * max(H, 25.0);
    // along the frame: always forward (the speed swings, never reverses)
    float sB = h2 * 4.0 * Q + dir * (v * t + 1.4 * v * sin(t * 0.3 + id * 1.7) + 0.35 * v * sin(t * 0.83 + id * 4.1));
    vec2 pt = framePoint(sB, hs, rad, Q, n);
    float off = -1.0 * H - 0.6 * H * sin(t * 0.41 + id * 2.3) + 0.2 * H * sin(t * 1.1 + id * 0.7);
    return pt + n * off;
}

// wing outline distance (normalized wing coordinates w: x out from the body, y forward); fore: which wing
float wingDist(vec2 w, out vec2 aN, out vec2 bN, out bool fore, out float rel)
{
    aN = (mat2(0.88, -0.48, 0.48, 0.88) * (w - vec2(0.27, 0.10))) / vec2(0.30, 0.17);
    float laN = length(aN);
    float eF = (laN - 1.0) * 0.17;
    bN = (mat2(0.92, 0.39, -0.39, 0.92) * (w - vec2(0.19, -0.13))) / vec2(0.21, 0.15);
    float lbN = length(bN);
    float eH = (lbN - 1.0) * 0.15;
    fore = eF < eH;
    rel = fore ? laN : lbN;
    return min(eF, eH);
}

// project a screen point onto a wing tilted by angle a (toward the viewer), with perspective
vec2 wingCoords(vec2 lp, float a, out float c, out float sn)
{
    c = max(cos(a), 0.05);
    sn = sin(a);
    float xw = abs(lp.x) / c;
    float persp = 1.0 + 0.45 * sn * xw;
    return vec2(xw, lp.y) / persp;
}

// soft butterfly silhouette for its shadow (lp in butterfly units: x right, y forward)
float butterflyShadow(vec2 lp, float phi, float roll)
{
    float a = phi + (lp.x > 0.0 ? roll : -roll);
    float c = max(cos(a), 0.05);
    vec2 w = vec2(abs(lp.x) / c, lp.y);
    float r = min(length((w - vec2(0.24, 0.0)) / vec2(0.32, 0.27)), length(lp / vec2(0.05, 0.25)));
    return smoothstep(1.15, 0.7, r);
}

// butterfly seen from above: 3D wings + body parts in one loop (single compiled copy of lit)
vec4 butterfly(vec2 lp, float phi, float roll, float bsz, float id, vec2 lat, vec2 fwd)
{
    float sd = lp.x > 0.0 ? 1.0 : -1.0;
    float a = phi + sd * roll;
    float c, sn;
    vec2 w = wingCoords(lp, a, c, sn);
    vec2 aN, bN; bool fore; float rel;
    float e = wingDist(w, aN, bN, fore, rel);
    float covW = clamp(0.5 - e * bsz * c, 0.0, 1.0);
    vec2 tq = vec2(lp.x / 0.055, (lp.y - 0.05) / 0.11);
    vec2 aq = vec2(lp.x / 0.04, (lp.y + 0.15) / 0.17);
    vec2 hq = (lp - vec2(0.0, 0.19)) / 0.045;
    float lt = length(tq), la = length(aq), lh = length(hq);
    float covT = clamp(0.5 - (lt - 1.0) * 0.05 * bsz, 0.0, 1.0);
    float covAb = clamp(0.5 - (la - 1.0) * 0.04 * bsz, 0.0, 1.0);
    float covH = clamp(0.5 - (lh - 1.0) * 0.045 * bsz, 0.0, 1.0);
    vec2 aw = vec2(abs(lp.x), lp.y);
    vec2 a1 = vec2(0.09, 0.38);
    float dA = min(EwSdSegment2D(aw, vec2(0.015, 0.21), a1) - 0.005, length(aw - a1) - 0.014);
    float covA = clamp(0.5 - dA * bsz, 0.0, 1.0) * 0.9;
    if (max(max(covW, max(covT, covAb)), max(covH, covA)) <= 0.0) return vec4(0.0);

    vec3 wc = (EwHash1To1(id * 3.3 + 0.7) < 0.5 ? cBf1 : cBf2) * (fore ? 1.0 : 0.85);
    vec3 black = vec3(0.012, 0.01, 0.009);
    float lw = length(w);
    vec3 alb = wc * mix(0.45, 1.0, smoothstep(0.0, 0.3, lw));
    float angR = atan(w.y, w.x);
    float vein = smoothstep(0.08, 0.0, abs(fract(angR * 2.2) - 0.5)) * smoothstep(0.08, 0.3, lw);
    alb = mix(alb, black, vein * 0.7);
    float border = smoothstep(0.7, 0.8, rel);
    alb = mix(alb, black, border);
    alb = mix(alb, black, (fore ? 0.9 : 0.0) * smoothstep(0.5, 0.75, aN.x));
    float angW = atan(fore ? aN.y : bN.y, fore ? aN.x : bN.x);
    float dots = smoothstep(0.3, 0.12, length(vec2((fract(angW * 1.6) - 0.5) * 0.9, (rel - 0.88) * 5.0)));
    alb = mix(alb, vec3(0.85), dots * border);
    vec3 Nw = vec3(-lat * sd * sn, c);
    Nw += vec3(lat * sd * 0.35 * (w.x - 0.2) + fwd * 0.3 * (w.y + 0.05), 0.0) * c;
    Nw = normalize(Nw);
    vec3 wAmb = mix(vec3(0.15, 0.12, 0.09), vec3(0.32, 0.38, 0.48), 0.5 + 0.5 * Nw.y) * (0.5 + 0.5 * Nw.z);
    vec3 col = alb * (wAmb + cSun * max(dot(Nw, cL), 0.0));
    col += cSun * mix(vec3(1.0), wc, 0.5) * 0.12 * pow(max(dot(Nw, cH), 0.0), 18.0) * (1.0 - border);
    col += alb * cSun * 0.35 * max(-dot(Nw, cL), 0.0);
    col *= mix(0.45, 1.0, smoothstep(0.02, 0.14, w.x)) * mix(0.55, 1.0, smoothstep(0.08, 0.35, c));
    vec4 r = vec4(col * covW, covW);
    // unified body shading (single lit() call, no nested loop): head > thorax > abdomen
    float covB = max(covH, max(covT, covAb));
    if (covB > 0.0) {
        vec3 bodyA = vec3(0.03, 0.024, 0.018);
        bool isH = covH >= covT && covH >= covAb;
        bool isT = !isH && covT >= covAb;
        vec2 pq = isH ? hq : isT ? tq : aq;
        float pl = isH ? lh : isT ? lt : la;
        float fy = isH ? 1.0 : isT ? 0.5 : 0.4;
        float z = sqrt(max(1.0 - pl * pl, 0.0));
        vec3 Nb = normalize(vec3(lat * pq.x + fwd * pq.y * fy, z + 0.1));
        vec3 albB = isH ? bodyA
                  : isT ? bodyA * (1.3 + 0.6 * EwValueNoise1D(atan(tq.y, tq.x) * 9.0))
                  : bodyA * (0.75 + 0.25 * smoothstep(0.2, 0.5, abs(fract(aq.y * 3.5) - 0.5)));
        float spB = isH ? 0.6 : isT ? 0.2 : 0.35;
        float glB = isH ? 40.0 : isT ? 20.0 : 30.0;
        r = over(vec4(lit(albB, Nb, spB, glB) * covB, covB), r);
    }
    r = over(vec4(vec3(0.01) * covA, covA), r);
    return r;
}

// ---- zen garden raked sand ----
// Peaked sand ridge with gently flat-bottomed trough: 0 at integer x (trough), 1 at half-integer x (crest)
float sandRidge(float x, float f1, float f2)
{
    float c = cos(EW_TAU * x);
    return 0.5 - 0.5 * c * f1 - 0.13 * (1.0 - c * c) * f2;
}

// Smoothly rounded radial distance near r = 0 so concentric centers form a soft sand dome
float smoothRad(float rSq, float eSq)
{
    return sqrt(rSq + eSq);
}

float sandHeight(vec2 q, vec2 wob, vec2 hs, float rad, float H, float pitch, float f1, float f2,
                 float borderW, vec2 span, float dPre, bool deepInterior)
{
    vec2 pw = q + pitch * 0.16 * wob;
    float invPitch = 1.0 / pitch;
    float eSq = (0.16 * pitch) * (0.16 * pitch);

    float mIn = 1.0;
    float dipB = 1.0;
    float hBorder = 0.0;
    if (!deepInterior) {
        float dq = (dPre < 1e8) ? dPre : EwSdRoundedBox2D(q, hs, rad);
        float dIn = -(dq + 0.90 * H) + pitch * 0.11 * wob.x;
        mIn = smoothstep(borderW - 0.22 * pitch, borderW + 0.22 * pitch, dIn);
        dipB = smoothstep(borderW - 0.08 * pitch, borderW + 0.38 * pitch, dIn);
        if (mIn < 1.0) hBorder = sandRidge(dIn * invPitch, f1, f2);
        if (mIn <= 0.0) return hBorder;
    }

    float hInt = 0.5;
    if (SandPattern == 0) {
        bool horiz = span.x >= span.y;
        vec2 c1 = horiz ? vec2(-0.37 * span.x, -0.14 * span.y) : vec2(-0.14 * span.x, -0.35 * span.y);
        vec2 c2 = horiz ? vec2( 0.40 * span.x,  0.18 * span.y) : vec2( 0.18 * span.x,  0.37 * span.y);
        float R1 = vSand2.z, R2 = vSand2.w;
        float R1e = R1 + 0.45 * pitch, R2e = R2 + 0.45 * pitch;
        vec2 u1 = pw - c1, u2 = pw - c2;
        float r1Sq = dot(u1, u1), r2Sq = dot(u2, u2);
        float r1 = sqrt(r1Sq), r2 = sqrt(r2Sq);
        float m1 = smoothstep(R1 + 0.22 * pitch, R1 - 0.22 * pitch, r1);
        float m2 = smoothstep(R2 + 0.22 * pitch, R2 - 0.22 * pitch, r2);
        hInt = 0.0;
        if (m1 < 1.0 && m2 < 1.0) {
            float R1eSq = R1e * R1e, R2eSq = R2e * R2e;
            float defl = u1.y * R1eSq / max(r1Sq, R1eSq * 0.35) * 0.56
                       + u2.y * R2eSq / max(r2Sq, R2eSq * 0.35) * 0.56;
            float wave = 0.55 * pitch * sin(pw.x / max(span.x, pitch * 2.0) * 4.0 + 0.4);
            float dip1 = smoothstep(R1 - 0.08 * pitch, R1 + 0.40 * pitch, r1);
            float dip2 = smoothstep(R2 - 0.08 * pitch, R2 + 0.40 * pitch, r2);
            hInt = sandRidge((pw.y - defl + wave) * invPitch, f1, f2) * dip1 * dip2;
        }
        if (m1 > 0.0 && m2 < 1.0) hInt = mix(hInt, sandRidge(smoothRad(r1Sq, eSq) * invPitch + 0.5, f1, f2), m1);
        if (m2 > 0.0) hInt = mix(hInt, sandRidge(smoothRad(r2Sq, eSq) * invPitch + 0.5, f1, f2), m2);
    } else if (SandPattern == 1) {
        float wx = pw.x / max(span.x, pitch * 2.0);
        float wy = pw.y / max(span.y, pitch * 2.0);
        float wave = 1.15 * pitch * sin(wx * 4.2 + 0.5) + 0.45 * pitch * sin(wx * 2.1 - wy * 1.4);
        hInt = sandRidge((pw.y + wave) * invPitch, f1, f2);
    } else if (SandPattern == 2) {
        float R = 3.5 * pitch;
        float dx = 2.0 * R, dy = 0.5 * R;
        float iy0 = floor(pw.y / dy);
        hInt = 0.0;
        for (int dr = 1; dr >= -2; dr--) {
            float iy = iy0 + float(dr);
            float xOff = mod(iy, 2.0) * R;
            float ix = floor((pw.x - xOff) / dx + 0.5);
            vec2 c = vec2(ix * dx + xOff, iy * dy);
            vec2 u = pw - c;
            float rSq = dot(u, u);
            float r = sqrt(rSq);
            float m = smoothstep(R + 0.14 * pitch, R - 0.14 * pitch, r);
            if (m > 0.0) {
                float hc = sandRidge(smoothRad(rSq, eSq) * invPitch + 0.5, f1, f2);
                hInt *= smoothstep(R - 0.08 * pitch, R + 0.32 * pitch, r);
                hInt = mix(hInt, hc, m);
            }
        }
    } else if (SandPattern == 3) {
        hInt = sandRidge(smoothRad(dot(pw, pw), eSq) * invPitch + 0.5, f1, f2);
    } else {
        float cell = 6.0 * pitch;
        vec2 cid = floor(pw / cell);
        vec2 cf = pw - (cid + 0.5) * cell;
        float parity = mod(cid.x + cid.y, 2.0);
        float hTile = sandRidge((parity < 0.5 ? pw.y : pw.x) * invPitch, f1, f2);
        float edgeD = 0.5 * cell - max(abs(cf.x), abs(cf.y));
        hInt = hTile * (0.2 + 0.8 * smoothstep(0.0, 0.35 * pitch, edgeD));
    }

    return mix(hBorder, hInt * dipB, mIn);
}

vec3 zenSand(vec2 p, vec2 fragCoord, vec2 hs, float rad, float d, float H)
{
    float pitch = vSand.x, borderW = vSand.y, f1 = vSand.z, grainPx = vSand.w;
    float f2 = f1 * f1; f2 *= f2;
    vec2 span = vSand2.xy;
    float eps = max(1.0, 0.045 * pitch);
    float minDim = min(iResolution.x, iResolution.y);
    // low-frequency hand-raked wobble & ridge height drift
    vec2 wp = p / (pitch * 8.5);
    vec2 wob = vec2(EwValueNoise2D(wp + vec2(1.3, 4.7)), EwValueNoise2D(wp + vec2(9.1, 2.5))) - 0.5;
    float drift = 0.90 + 0.20 * EwValueNoise2D(p / (pitch * 5.0) + vec2(12.4, 6.1));
    // fine granular sand texture
    vec2 gp = p / grainPx;
    float g0 = EwValueNoise2D(gp);
    float gx = EwValueNoise2D(gp + vec2(0.65, 0.0));
    float gy = EwValueNoise2D(gp + vec2(0.0, 0.65));
    vec2 mp = p / (grainPx * 3.4) + vec2(11.4, 7.2);
    float medG = EwValueNoise2D(mp);
    float medX = EwValueNoise2D(mp + vec2(0.5, 0.0));
    float medY = EwValueNoise2D(mp + vec2(0.0, 0.5));
    float speck = EwValueNoise2D(gp * 1.25 + vec2(31.7, 19.2));
    float broad = EwValueNoise2D(p / max(minDim * 0.35, 1.0) + vec2(4.2, 8.7));

    bool deepInterior = -(d + 0.90 * H) > borderW + 0.50 * pitch + eps;

    // 3-tap height gradient in a non-unrolled loop (single compiled copy of sandHeight)
    float h0 = 0.0, hx = 0.0, hy = 0.0;
    for (int k = ZERO; k < 3; k++) {
        vec2 off = (k == 1) ? vec2(eps, 0.0) : (k == 2) ? vec2(0.0, eps) : vec2(0.0);
        float hk = sandHeight(p + off, wob, hs, rad, H, pitch, f1, f2, borderW, span, (k == 0) ? d : 1e9, deepInterior);
        if (k == 0) h0 = hk; else if (k == 1) hx = hk; else hy = hk;
    }
    vec2 grad = vec2(hx - h0, hy - h0) * (drift / eps);

    float amp = 0.45 * pitch * SandDepth;
    vec2 gGrad = (vec2(gx - g0, gy - g0) * 1.75 + vec2(medX - medG, medY - medG) * 0.55) * SandGrain;
    vec3 N = normalize(vec3(-grad * amp - gGrad, 1.0));

    vec3 alb = cInner;
    alb *= mix(0.93, 1.07, broad);
    alb *= 1.0 + SandGrain * ((g0 - 0.5) * 0.28 + (medG - 0.5) * 0.16);
    float darkFlecks = smoothstep(0.66, 0.86, speck);
    float lightFlecks = smoothstep(0.68, 0.86, g0);
    alb *= 1.0 - 0.24 * SandGrain * darkFlecks + 0.12 * SandGrain * lightFlecks;

    float dif = max(dot(N, cLs), 0.0);
    float hCl = clamp(h0 * drift, 0.0, 1.0);
    float ao = mix(1.0 - 0.30 * clamp(SandDepth, 0.0, 1.2), 1.0, hCl);
    float edgeAO = mix(0.75, 1.0, smoothstep(0.0, 0.9 * H, max(-(d + 0.85 * H), 0.0)));
    vec3 amb = mix(vec3(0.20, 0.18, 0.16), vec3(0.30, 0.35, 0.43), 0.5 + 0.5 * N.y) * ao;
    vec3 bounce = alb * cSun * (0.12 * (1.0 - hCl));
    float quartz = smoothstep(0.66, 0.88, g0) * pow(max(dot(N, cHs), 0.0), 22.0) * 0.22 * SandGrain;

    vec3 col = (alb * (amb + cSun * dif * 1.18) + bounce + cSun * quartz) * edgeAO;
    col = EwTonemapAces(col * Exposure);
    col = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
    return clamp(col + EwDither(fragCoord, iTime, 1.5), 0.0, 1.0);
}

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vP = fragCoord - 0.5 * res;
    float minDim = min(res.x, res.y);
    float inset = min(Inset, 0.25 * minDim);
    float H = inset * Thickness;
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {
        // contain: the inner opening (hs - 0.5 H) takes the image aspect, the frame stays on screen
        vec2 ts = vec2(textureSize(iChannel0, 0));
        float A = clamp(ts.x / max(ts.y, 1.0), 0.05, 20.0);
        vec2 iMax = max(hs - 0.5 * H, vec2(1.0));
        vec2 ih = iMax.x > iMax.y * A ? vec2(iMax.y * A, iMax.y) : vec2(iMax.x, iMax.x / A);
        hs = ih + 0.5 * H;
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));

    float ropeLen = 1.4 * H * RopeLength;
    float reachFrame = max(2.1 * H, 0.45 * H + 1.25 * ropeLen) + 6.0;
    float reachAll = max(1.6 * H, 0.45 * H + 1.25 * ropeLen) + 6.0;
    float bsz = ButterflySize * max(1.2 * H, 30.0);
    if (Butterflies > 0) reachAll = max(reachAll, 3.0 * H + 0.97 * bsz + 6.0);
    reachFrame = min(reachFrame, reachAll);

    float gT = mod(iTime, 3600.0) * Speed;
    float gWindAmt = Wind * 0.5 * (0.6 + 0.4 * EwValueNoise1D(gT * 0.15));

    vec2 b0 = hs - rad;
    float Q = b0.x + b0.y + rad * 1.5707963;
    float c1 = b0.x + rad * 0.7853982;

    float wB = BindingWidth * H;
    float sp0 = 3.0 * Q + 0.6 * wB;
    float sp1 = 4.0 * Q - c1 - 0.6 * wB;
    float spPitch = (sp1 - sp0) / max(2.0, floor((sp1 - sp0) / (0.65 * H)));

    float wE = 0.068 * H + 0.4;
    float mSl = 0.17 + 3.15 * gWindAmt;
    float Lmax = 1.15 * ropeLen;
    float bx = max(0.05 * H + Lmax * (0.12 + 1.5 * gWindAmt) + (wE + 0.5) * 1.6 * sqrt(1.0 + mSl * mSl), 0.36 * H)
             + length(cOffS) * 0.3 * H + 2.0;

    float pitch = max(6.0, (minDim / 38.0) * SandScale);
    float aa = 1.0 / pitch;
    float f1 = exp(-aa * aa * 1.5);
    float borderGrooves = min(3.0, max(1.0, floor(0.28 * min(hs.x, hs.y) / pitch)));
    float borderW = borderGrooves * pitch;
    vec2 span = max(hs - 0.90 * H - borderW, vec2(pitch * 2.0));
    float grainPx = max(1.35, minDim / 650.0);

    float R1 = 0.0, R2 = 0.0;
    if (SandPattern == 0 && (!SwHaveImage || SwTransparentImage)) {
        bool horiz = span.x >= span.y;
        vec2 c1s = horiz ? vec2(-0.37 * span.x, -0.14 * span.y) : vec2(-0.14 * span.x, -0.35 * span.y);
        vec2 c2s = horiz ? vec2( 0.40 * span.x,  0.18 * span.y) : vec2( 0.18 * span.x,  0.37 * span.y);
        float sMin = min(span.x, span.y);
        float dist12 = length(c2s - c1s);
        float maxSum = max(dist12 - 1.8 * pitch, 4.0 * pitch);
        float r1Raw = 0.48 * sMin;
        float r2Raw = 0.32 * sMin;
        float sc = min(1.0, maxSum / max(r1Raw + r2Raw, 1.0));
        R1 = (max(2.0, floor(r1Raw * sc / pitch)) + 0.5) * pitch;
        R2 = (max(2.0, floor(r2Raw * sc / pitch)) + 0.5) * pitch;
    }

    vFrame = vec4(hs, rad, H);
    vFrame2 = vec4(Q, c1, reachFrame, reachAll);
    vTime = vec4(gT, gWindAmt, bx, spPitch);
    vHoles0 = vec4(holePosV(0, Q, c1), holePosV(1, Q, c1), holePosV(2, Q, c1), holePosV(3, Q, c1));
    vHoles1 = vec4(holePosV(4, Q, c1), holePosV(5, Q, c1), holePosV(6, Q, c1), holePosV(7, Q, c1));
    vSand = vec4(pitch, borderW, f1, grainPx);
    vSand2 = vec4(span, R1, R2);

    vBf0 = vec4(0.0); vBf1 = vec4(0.0); vBf2 = vec4(0.0); vBf3 = vec4(0.0);
    vBfRoll = vec4(0.0); vBfHgt = vec4(0.0);
    if (Butterflies > 0) {
        float radP = min(max(rad, 2.5 * H), min(hs.x, hs.y));
        float QP = (hs.x - radP) + (hs.y - radP) + radP * 1.5707963;
        int nPre = min(Butterflies, 4);
        for (int bi = 0; bi < nPre; bi++) {
            float fi = float(bi);
            vec2 nP, nA, nC;
            vec2 pm = butterflyPos(fi, gT, hs, radP, QP, H, nP);
            vec2 pa = butterflyPos(fi, gT - 0.35, hs, radP, QP, H, nA);
            vec2 pc = butterflyPos(fi, gT + 0.35, hs, radP, QP, H, nC);
            vec2 tP = vec2(nP.y, -nP.x);
            vec2 bp = pm + nP * 0.07 * H * sin(gT * 4.7 + fi) + tP * 0.05 * H * sin(gT * 3.3 + fi * 2.0);
            float hf = EwHash1To1(fi * 6.1 + 0.5);
            float glide = smoothstep(0.55, 0.8, EwValueNoise1D(gT * 0.6 + fi * 7.0));
            float beat = sin(EW_TAU * gT * FlapRate * (1.0 + 0.8 / 2.2 * hf) + fi * 2.0);
            float phi = mix(0.55 + 0.75 * beat, 0.15 + 0.08 * beat, glide);
            float bob = 0.12 * bsz * (beat * (1.0 - glide));
            float hgt = (0.7 + 0.4 * EwHash1To1(fi + 4.4)) * H + 0.4 * bsz + bob;
            vec2 v = pc - pa;
            vec2 fwd = dot(v, v) > 1e-6 ? normalize(v) : vec2(0.0, 1.0);
            float yaw = 0.08 * sin(gT * 1.7 + fi * 2.0);
            fwd = EwRotateVec2(fwd, yaw);
            float ang = atan(fwd.y, fwd.x);
            vec2 v1 = pm - pa, v2 = pc - pm;
            float turn = (v1.x * v2.y - v1.y * v2.x) / max(dot(v1, v1) + dot(v2, v2), 1.0);
            float roll = clamp(turn * 6.0, -0.45, 0.45);
            vec4 packed = vec4(bp, ang, phi);
            if (bi == 0) { vBf0 = packed; vBfRoll.x = roll; vBfHgt.x = hgt; }
            else if (bi == 1) { vBf1 = packed; vBfRoll.y = roll; vBfHgt.y = hgt; }
            else if (bi == 2) { vBf2 = packed; vBfRoll.z = roll; vBfHgt.z = hgt; }
            else { vBf3 = packed; vBfRoll.w = roll; vBfHgt.w = hgt; }
        }
    }
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 p = vP;
    vec2 hs = vFrame.xy;
    float rad = vFrame.z;
    float H = vFrame.w;
    float d = EwSdRoundedBox2D(p, hs, rad);

    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = smoothstep(0.5, -0.5, d + 0.5 * H);
    vec4 base = bg;
    if (InnerOpacity > 0.0 && inner > 0.0) {
        vec4 innerCol;
        if (SwHaveImage) {
            // image fills the inner opening (frame sized to its aspect in vertexMain)
            vec2 ih = max(hs - 0.5 * H, vec2(1.0));
            vec2 iq = p / ih;
            vec4 im = texture(iChannel0, iq * 0.5 + 0.5);
            float ia = SwTransparentImage ? clamp(im.a, 0.0, 1.0) : 1.0;
            // lighting: sun color / brightness, soft gradient toward the light, occlusion near the frame
            float edgeAO = mix(0.75, 1.0, smoothstep(0.0, 0.9 * H, max(-(d + 0.85 * H), 0.0)));
            float lgrad = 0.93 + 0.07 * clamp(dot(iq, cL2), -1.0, 1.0);
            vec3 lin = pow(max(im.rgb, vec3(0.0)), vec3(2.2)) * SunColor * (Brightness * edgeAO * lgrad);
            vec3 ic = pow(clamp(lin, 0.0, 1.0), vec3(1.0 / 2.2)) + EwDither(fragCoord, iTime, 1.5);
            innerCol = vec4(clamp(ic, 0.0, 1.0) * ia, ia);
            if (SwTransparentImage && ia < 1.0)
                innerCol = over(innerCol, vec4(zenSand(p, fragCoord, hs, rad, d, H), 1.0));
        } else {
            innerCol = vec4(zenSand(p, fragCoord, hs, rad, d, H), 1.0);
        }
        base = over(innerCol * (InnerOpacity * inner), bg);
    }
    if (d < -vFrame2.w) { fragColor = base; return; }

    float Q = vFrame2.x;
    float c1 = vFrame2.y;
    float Lp = 4.0 * Q;
    float gT = vTime.x;
    float gWindAmt = vTime.y;
    float bx = vTime.z;
    float ropeLen = 1.4 * H * RopeLength;
    float wear = Weathering;

    vec4 acc = vec4(0.0);

    // ---- bamboo poles, bindings, side rope, hanging ropes, holes, caterpillars ----
    if (d >= -vFrame2.z) {
        vec2 n2;
        float s = frameCoords(p, hs, rad, Q, n2);
        vec2 tg = vec2(n2.y, -n2.x);
        float cA = 0.22 * H, rA = 0.62 * H, cB = -0.52 * H, rB = 0.42 * H;
        float nA = max(4.0, floor(Lp / (H * 4.6) + 0.5)), nB = max(4.0, floor(Lp / (H * 3.7) + 0.5));
        float segA = Lp / nA, segB = Lp / nB;
        float amp = Crookedness * 0.35 * H;
        float xA = d - cA - poleOff(s, segA, nA, 0.0, amp) + 0.3 * amp;
        float xB = d - cB - poleOff(s, segB, nB, 0.37, amp * 0.8);

        // ---- nearest binding (3 quadrant candidates, exact match to all 8) ----
        vec2 cP = (hs - rad) + rad * 0.70710678;
        vec2 pb = p - vec2(0.0, -0.5 * ropeLen);
        bool px = pb.x >= 0.0, py = pb.y >= 0.0;
        vec2 sg = vec2(px ? 1.0 : -1.0, py ? 1.0 : -1.0);
        vec2 P0 = vec2(0.0, sg.y * hs.y);
        vec2 P1 = vec2(sg.x * hs.x, 0.0);
        vec2 P2 = sg * cP;
        vec2 dv0 = pb - P0, dv1 = pb - P1, dv2 = pb - P2;
        float d0 = dot(dv0, dv0), d1 = dot(dv1, dv1), d2 = dot(dv2, dv2);
        vec2 P = P0, nK = vec2(0.0, sg.y);
        float bk = py ? 0.0 : 4.0, sK = py ? 0.0 : 2.0 * Q;
        float best = d0;
        if (d1 < best) { best = d1; P = P1; nK = vec2(sg.x, 0.0); bk = px ? 2.0 : 6.0; sK = px ? Q : 3.0 * Q; }
        if (d2 < best) {
            P = P2; nK = sg * 0.70710678;
            bk = px ? (py ? 1.0 : 3.0) : (py ? 7.0 : 5.0);
            sK = px ? (py ? c1 : 2.0 * Q - c1) : (py ? 4.0 * Q - c1 : 2.0 * Q + c1);
        }
        vec2 tgK = vec2(nK.y, -nK.x);
        float ds = dot(p - P, tgK);
        float ph = sK / Lp * 9.0;
        vec2 K = P - nK * 0.27 * H;
        vec2 q = p - K;

        // ---- shadows ----
        float shN = dot(cOffS, n2);
        float shAB = smoothstep(0.12 * H, -0.12 * H, abs(xA + shN * 0.45 * H) - rA);
        float hF = 0.6 * H;
        float fr = min(abs(xA + shN * hF) - rA, abs(xB + shN * hF) - rB);
        float dropF = smoothstep(0.35 * H, -0.25 * H, fr);
        float knotSh = smoothstep(0.32 * H, 0.05 * H, length(q + cOffS * 0.25 * H) - 0.12 * H);

        // ---- all free rope tubes in one loop (one inlined copy of the rope code) ----
        float wB = BindingWidth * H;
        float sp0 = 3.0 * Q + 0.6 * wB, sp1 = 4.0 * Q - c1 - 0.6 * wB, pitch = vTime.w;
        bool spOn = SideRope && sp1 > sp0 && s > sp0 - H && s < sp1 + H;
        float Rsp = rA + 0.05 * H;
        float clip = clamp(s - sp0 + 0.5, 0.0, 1.0) * clamp(sp1 - s + 0.5, 0.0, 1.0);
        float shv = 0.0, endSh = 0.0;
        vec4 spiral = vec4(0.0), hang = vec4(0.0);
        float Lmax = 1.15 * ropeLen;
        bool nearKnot = abs(q.x) < bx && -q.y > -bx && -q.y < Lmax + bx;
        int iEnd = nearKnot ? 9 : (spOn ? 4 : 0);
        for (int i = ZERO; i < iEnd; i++) {
            bool shadowPass = i < 3;
            int e = shadowPass ? i : i - 3;
            if (e == 0 ? !spOn : !nearKnot) continue;
            float sd, w, along, ts, shade;
            vec2 gS;
            float m = tubeGeo(e, shadowPass ? q + cOffS * 0.3 * H : q,
                              shadowPass ? xA + shN * 0.12 * H : xA,
                              shadowPass ? s - sp0 + dot(cOffS, tg) * 0.12 * H : s - sp0,
                              Rsp, pitch, H, ropeLen, ph, bk, n2, tg, sd, w, gS, along, ts, shade);
            float cov = clamp(w - abs(sd) + 0.5, 0.0, 1.0) * m;
            if (cov <= 0.0) continue;
            if (shadowPass) {
                if (e == 0) shv = cov * clip; else endSh = cov + endSh * (1.0 - cov);
                continue;
            }
            vec4 tb = ropeTube(sd, w, gS, along, ts);
            tb.rgb *= shade;
            tb *= m;
            if (e == 0) spiral = tb * clip; else hang = over(tb, hang);
        }
        acc = vec4(0.0, 0.0, 0.0, ShadowAmount * 0.75 * max(dropF, 0.85 * max(endSh, knotSh)));

        // ---- poles (one loop: outer first, inner only where it shows) ----
        vec4 pB = vec4(0.0), pA = vec4(0.0);
        for (int k = ZERO; k < 2; k++) {
            bool outer = k == 0;
            if (!outer && pA.a >= 1.0) break;
            vec4 pc = pole(outer ? xA : xB, outer ? rA : rB, s, outer ? segA : segB, outer ? 0.0 : 0.37, n2, tg, wear);
            if (outer) pA = pc; else pB = pc;
        }
        pB.rgb *= 1.0 - 0.55 * shAB;
        float faceL = max(dot(n2, cL2), 0.0);
        float aoB = smoothstep(0.35 * H, -0.05 * H, abs(xA) - rA);
        pB.rgb *= mix(1.0, 0.25, InnerShade) * (1.0 - 0.6 * InnerShade * faceL) * (1.0 - 0.4 * InnerShade * aoB);
        acc = over(pA, over(pB, acc));

        // ---- holes in the outer pole (positions precomputed in vertexMain) ----
        if (pA.a > 0.0) {
            float xh = -0.4 * rA;
            float Rh = 1.3 * 0.19 * CaterpillarSize * H;
            float ly = xA - xh;
            if (abs(ly) <= 2.0 * Rh) {
                vec4 dh0 = s - vHoles0; dh0 -= Lp * floor(dh0 / Lp + 0.5);
                vec4 dh1 = s - vHoles1; dh1 -= Lp * floor(dh1 / Lp + 0.5);
                float bestH = dh0.x, hId = 0.0;
                if (abs(dh0.y) < abs(bestH)) { bestH = dh0.y; hId = 1.0; }
                if (abs(dh0.z) < abs(bestH)) { bestH = dh0.z; hId = 2.0; }
                if (abs(dh0.w) < abs(bestH)) { bestH = dh0.w; hId = 3.0; }
                if (abs(dh1.x) < abs(bestH)) { bestH = dh1.x; hId = 4.0; }
                if (abs(dh1.y) < abs(bestH)) { bestH = dh1.y; hId = 5.0; }
                if (abs(dh1.z) < abs(bestH)) { bestH = dh1.z; hId = 6.0; }
                if (abs(dh1.w) < abs(bestH)) { bestH = dh1.w; hId = 7.0; }
                if (abs(bestH) <= 2.0 * Rh)
                    acc = over(holeLayer(vec2(bestH, ly), Rh, hId, n2, tg) * pA.a, acc);
            }
        }

        // ---- rope winding around the outer pole along the left side ----
        if (spOn) {
            acc.rgb *= 1.0 - 0.5 * shv * pA.a;
            acc = over(spiral, acc);
        }

        // ---- shadows of the hanging rope on the poles ----
        acc.rgb *= 1.0 - 0.5 * max(endSh, knotSh) * acc.a;

        // ---- rope wrap ----
        float onPole = step(min(abs(xA) - rA, abs(xB) - rB), 0.0);
        acc.rgb *= 1.0 - 0.4 * onPole * smoothstep(wB + 0.3 * H, wB, abs(ds)) * step(wB, abs(ds));
        float shWA = smoothstep(0.08 * H, -0.08 * H, abs(xA + shN * 0.45 * H) - (rA + 0.09 * H))
                   * smoothstep(0.1 * H, -0.1 * H, abs(ds + dot(cOffS, tgK) * 0.35 * H) - wB);
        acc.rgb *= 1.0 - 0.5 * shWA * pB.a * (1.0 - pA.a);
        float wrapSh = (1.0 - 0.65 * max(shAB, shWA)) * mix(1.0, 0.35, InnerShade) * (1.0 - 0.5 * InnerShade * faceL)
                     * (1.0 - 0.35 * InnerShade * aoB) * mix(0.6, 1.0, smoothstep(0.0, -0.6 * wB, abs(ds) - wB));
        if (abs(ds) < wB + 0.04 * H + 1.0) {
            for (int k = ZERO; k < 2; k++) {
                bool outer = k == 1;
                vec4 wr = ropeWrap(outer ? xA : xB, outer ? rA : rB, ds, wB, outer ? 1.0 : -1.0, bk * 3.0 + (outer ? 0.0 : 1.0), n2, tg, H);
                wr.rgb *= outer ? 1.0 : wrapSh;
                acc = over(wr, acc);
            }
        }

        // ---- caterpillars crawling along the inner side of the outer pole ----
        if (Caterpillars > 0 && abs(xA) < rA + 0.4 * H) {
            float shC = 0.0;
            vec4 cat = vec4(0.0);
            vec2 offL = vec2(dot(cOffS, tg), dot(cOffS, n2));
            float sz = CaterpillarSize * H;
            float rr = 0.19 * sz;
            float spC = 0.13 * sz;
            float bodyLen = 9.0 * spC + 2.0 * rr;
            float C = ceil((0.7 * Q + bodyLen) / (0.4 * sz)) + 6.0;
            float tc = 0.0, cdir = 1.0, D = 1.0, stride = 0.0, a = 0.0, wig = 0.0, cid = 0.0;
            for (int ci = ZERO; ci < Caterpillars; ci++) {
                cid = float(ci);
                if (!catSetup(cid, s, rr, bodyLen, C, Lp, tc, cdir, D, stride, a, wig)) continue;
                for (int k = 9 + ZERO; k >= 0; k--) {
                    float sh;
                    vec4 c = catSegment(float(k), cid, tc, cdir, D, stride, a, wig, xA, rr, spC, rA, n2, tg, offL, sh);
                    shC = max(shC, sh);
                    cat = over(c, cat);
                }
            }
            acc.rgb *= 1.0 - 0.55 * shC * acc.a;
            acc = over(cat, acc);
        }

        // ---- hanging rope ends + knot ----
        acc.rgb *= 1.0 - 0.45 * smoothstep(0.3 * H, 0.0, length(q) - 0.18 * H);
        acc = over(hang, acc);
    }

    // ---- butterflies fluttering close to the frame ----
    if (Butterflies > 0) {
        float bsz = ButterflySize * max(1.2 * H, 30.0);
        float shB = 0.0;
        vec4 bf = vec4(0.0);
        float rLim = 0.8 * bsz;
        float rLimSq = rLim * rLim;
        int nPre = min(Butterflies, 4);
        for (int bi = ZERO; bi < nPre; bi++) {
            vec4 b0 = (bi == 0) ? vBf0 : (bi == 1) ? vBf1 : (bi == 2) ? vBf2 : vBf3;
            float hgt = (bi == 0) ? vBfHgt.x : (bi == 1) ? vBfHgt.y : (bi == 2) ? vBfHgt.z : vBfHgt.w;
            vec2 bp = b0.xy;
            vec2 pS = p + cOffS * hgt;
            vec2 db = p - bp, ds2 = pS - bp;
            bool nearB = dot(db, db) < rLimSq;
            bool nearS = dot(ds2, ds2) < rLimSq;
            if (!nearB && !nearS) continue;
            float roll = (bi == 0) ? vBfRoll.x : (bi == 1) ? vBfRoll.y : (bi == 2) ? vBfRoll.z : vBfRoll.w;
            float ang = b0.z, phi = b0.w;
            vec2 fwd = vec2(cos(ang), sin(ang));
            vec2 lat = vec2(fwd.y, -fwd.x);
            if (nearS) shB = max(shB, butterflyShadow(vec2(dot(ds2, lat), dot(ds2, fwd)) / bsz, phi, roll));
            if (nearB) bf = over(butterfly(vec2(dot(db, lat), dot(db, fwd)) / bsz, phi, roll, bsz, float(bi), lat, fwd), bf);
        }
        if (Butterflies > 4) {
            float radP = min(max(rad, 2.5 * H), min(hs.x, hs.y));
            float QP = (hs.x - radP) + (hs.y - radP) + radP * 1.5707963;
            float rMax = rLim + 0.10 * H + length(cOffS) * (1.1 * H + 0.52 * bsz);
            float rMaxSq = rMax * rMax;
            vec2 pm = vec2(0.0), pa = vec2(0.0), bp = vec2(0.0), pS = vec2(0.0);
            float phi = 0.0;
            bool nearB = false, nearS = false;
            for (int i = ZERO + 12; i < Butterflies * 3; i++) {
                int bi = i / 3;
                int j = i - bi * 3;
                float fi = float(bi);
                vec2 nP;
                vec2 pj = butterflyPos(fi, gT + (j == 0 ? 0.0 : j == 1 ? -0.35 : 0.35), hs, radP, QP, H, nP);
                if (j == 0) {
                    pm = pj;
                    vec2 dp0 = p - pm;
                    if (dot(dp0, dp0) > rMaxSq) { i += 2; continue; }
                    vec2 tP = vec2(nP.y, -nP.x);
                    bp = pm + nP * 0.07 * H * sin(gT * 4.7 + fi) + tP * 0.05 * H * sin(gT * 3.3 + fi * 2.0);
                    float hf = EwHash1To1(fi * 6.1 + 0.5);
                    float glide = smoothstep(0.55, 0.8, EwValueNoise1D(gT * 0.6 + fi * 7.0));
                    float beat = sin(EW_TAU * gT * FlapRate * (1.0 + 0.8 / 2.2 * hf) + fi * 2.0);
                    phi = mix(0.55 + 0.75 * beat, 0.15 + 0.08 * beat, glide);
                    float bob = 0.12 * bsz * (beat * (1.0 - glide));
                    float hgt = (0.7 + 0.4 * EwHash1To1(fi + 4.4)) * H + 0.4 * bsz + bob;
                    pS = p + cOffS * hgt;
                    vec2 db = p - bp, ds2 = pS - bp;
                    nearB = dot(db, db) < rLimSq;
                    nearS = dot(ds2, ds2) < rLimSq;
                    if (!nearB && !nearS) i += 2;
                    continue;
                }
                if (j == 1) { pa = pj; continue; }
                vec2 pc = pj;
                vec2 v = pc - pa;
                vec2 fwd = dot(v, v) > 1e-6 ? normalize(v) : vec2(0.0, 1.0);
                float yaw = 0.08 * sin(gT * 1.7 + fi * 2.0);
                fwd = EwRotateVec2(fwd, yaw);
                vec2 lat = vec2(fwd.y, -fwd.x);
                vec2 v1 = pm - pa, v2 = pc - pm;
                float turn = (v1.x * v2.y - v1.y * v2.x) / max(dot(v1, v1) + dot(v2, v2), 1.0);
                float roll = clamp(turn * 6.0, -0.45, 0.45);
                if (nearS) {
                    vec2 d2 = pS - bp;
                    shB = max(shB, butterflyShadow(vec2(dot(d2, lat), dot(d2, fwd)) / bsz, phi, roll));
                }
                if (nearB) {
                    vec2 d2 = p - bp;
                    bf = over(butterfly(vec2(dot(d2, lat), dot(d2, fwd)) / bsz, phi, roll, bsz, fi, lat, fwd), bf);
                }
            }
        }
        if (shB > 0.0 || bf.a > 0.0) {
            acc.rgb *= 1.0 - 0.55 * shB * acc.a;
            acc = over(acc, vec4(0.0, 0.0, 0.0, 0.7 * ShadowAmount * shB));
            acc = over(bf, acc);
        }
    }

    if (acc.a <= 0.0) { fragColor = base; return; }

    // outermost pixels transparent
    vec2 eDist = 0.5 * iResolution.xy - abs(p);
    acc *= smoothstep(0.5, 10.0, min(eDist.x, eDist.y));

    float a = clamp(acc.a, 0.0, 1.0);
    vec3 c = a > 1e-4 ? acc.rgb / a : vec3(0.0);
    c = EwTonemapAces(c * Exposure);
    c = pow(c, vec3(1.0 / 2.2));
    float dz = EwDither(fragCoord, iTime, 2.0);
    vec3 col = c * a + dz;
    a = clamp(a + dz, 0.0, 1.0);
    col = clamp(min(col, vec3(a)), 0.0, 1.0);
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
