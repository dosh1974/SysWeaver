// Morning Coffee - photoreal procedural porcelain cup of steaming hot coffee on a transparent background
// A glossy white porcelain cup on a decorated saucer in a studio product shot: dark coffee with a
// swirled crema and soft ripples that mirror the cup wall, self shadowed wisps of steam curling up,
// studio softbox reflections on the glaze; the cup slowly swings back and forth like on a turntable.
// Raymarched SDF scene with analytic anti aliasing (edges, waterline, pattern, crema bubbles) and a
// few extra reflection rays only on silhouette pixels.
// Output: premultiplied alpha (transparent around the cup, soft contact shadow, translucent steam).
//  iMouse.x = Steam | Density of the rising steam | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Coffee Level | Fill height of the coffee (0 low, 1 near the rim) | 0.7 | 0.0 .. 1.0
//  iMouse.z = Crema | Light brown crema on the coffee: 0 black coffee, 1 full espresso crema | 0.7 | 0.0 .. 1.0
//  iMouse.w = Key Light | Brightness of the main softbox light | 0.5 | 0.4 .. 2.0 | x
//  media: image none

// --- Colors ---
const vec3 CoffeeColor = vec3(0.07, 0.03, 0.012); // var: { "type": "col", "name": "Coffee Color", "desc": "Color of the coffee (linear): very dark brown for black coffee" }
const vec3 CremaColor = vec3(0.42, 0.22, 0.09); // var: { "type": "col", "name": "Crema Color", "desc": "Color of the light brown crema on the coffee" }
const vec3 PorcelainColor = vec3(0.86, 0.85, 0.82); // var: { "type": "col", "name": "Porcelain Color", "desc": "Color of the cup and saucer glaze" }
const vec3 LightColor = vec3(1.0, 0.95, 0.88); // var: { "type": "col", "name": "Light Color", "desc": "Color of the main softbox light" }
const vec3 ReflectionTint = vec3(0.95, 0.97, 1.0); // var: { "type": "col", "name": "Reflection Tint", "desc": "Color of the studio lights reflected on the glaze and the coffee" }
const vec3 SteamColor = vec3(0.93, 0.94, 0.96); // var: { "type": "col", "name": "Steam Color", "desc": "Color of the steam" }

// --- Cup and camera ---
const float CupSize = 1.0; // var: { "min": 0.5, "max": 1.4, "step": 0.01, "name": "Cup Size", "desc": "Size of the cup on screen", "unit": "x" }
const float CameraHeight = 24.0; // var: { "min": 5.0, "max": 55.0, "step": 0.5, "name": "Camera Height", "desc": "How much the camera looks down into the cup", "unit": "°" }
const float HandleAngle = 25.0; // var: { "min": -180.0, "max": 180.0, "step": 1.0, "name": "Handle Angle", "desc": "Direction of the handle (0 = to the right)", "unit": "°" }
const float SwingAmount = 30.0; // var: { "min": 0.0, "max": 90.0, "step": 1.0, "name": "Swing", "desc": "How far the cup turns back and forth", "unit": "°" }
const float SwingSpeed = 0.15; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Swing Speed", "desc": "Speed of the turning swing", "unit": "rad/s" }

// --- Lighting ---
const float Exposure = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float ReflectionStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Reflections", "desc": "Strength of the studio reflections on glaze and coffee", "unit": "x" }
const float SoftboxLight = 14.0; // var: { "min": 0.0, "max": 30.0, "step": 0.5, "name": "Softbox Reflection", "desc": "Brightness of the wide reflected softbox on the left" }
const float StripLight = 22.0; // var: { "min": 0.0, "max": 40.0, "step": 0.5, "name": "Strip Light", "desc": "Brightness of the thin reflected light strip on the right" }
const float TopLight = 1.2; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Top Light", "desc": "Brightness of the overhead light (reflections on coffee and saucer)" }
const float RimLight = 0.5; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Rim Light", "desc": "Light from behind on the right, also makes the steam glow" }
const float RoomLight = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Room Light", "desc": "Dim light of the surrounding room seen in the reflections: higher = lighter, less contrasty glaze", "unit": "x" }
const float Translucency = 0.5; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Translucency", "desc": "Warm soft light scattering in the porcelain" }
const float GlazeGloss = 0.045; // var: { "min": 0.02, "max": 0.12, "step": 0.005, "name": "Glaze Reflectance", "desc": "How strongly the porcelain glaze reflects when seen head on (edges always reflect strongly)" }
const float ReflectionDrift = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Reflection Drift", "desc": "How far the reflections slowly sway back and forth", "unit": "x" }

// --- Steam ---
const float SteamSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Steam Speed", "desc": "Rising speed of the steam", "unit": "x" }
const float SteamHeight = 1.0; // var: { "min": 0.4, "max": 1.6, "step": 0.05, "name": "Steam Height", "desc": "How high the steam rises before it vanishes", "unit": "x" }
const float SteamWidth = 1.0; // var: { "min": 0.5, "max": 1.6, "step": 0.05, "name": "Steam Width", "desc": "Width of the steam plume", "unit": "x" }
const float SteamCurl = 1.0; // var: { "min": 0.0, "max": 2.5, "step": 0.05, "name": "Steam Curl", "desc": "How much the steam wisps swirl and curl", "unit": "x" }
const float SteamSway = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Steam Sway", "desc": "How far the whole plume sways sideways while rising", "unit": "x" }
const float SteamFadeIn = 0.3; // var: { "min": 0.05, "max": 0.6, "step": 0.01, "name": "Steam Fade In", "desc": "Share of the height over which the steam builds up above the coffee" }
const float SteamFadeOut = 1.1; // var: { "min": 0.5, "max": 3.0, "step": 0.05, "name": "Steam Fade Out", "desc": "How quickly the steam thins out with height: lower keeps it dense longer" }
const float SteamSelfShadow = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Steam Shading", "desc": "Self shadowing inside the steam: higher = more volume and contrast", "unit": "x" }
const int Quality = 1; // 0 fast, 1 default, 2 high

// --- Coffee ---
const float RippleStrength = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Ripples", "desc": "Gentle ripples on the coffee surface", "unit": "x" }
const float RippleSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Ripple Speed", "desc": "How fast the ripples change", "unit": "x" }
const float CremaBubbles = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Crema Bubbles", "desc": "Amount of tiny bubbles in the crema", "unit": "x" }
const float CremaBubbleSize = 1.0; // var: { "min": 0.5, "max": 2.5, "step": 0.05, "name": "Crema Bubble Size", "desc": "Size of the bubbles in the crema", "unit": "x" }
const float Meniscus = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Meniscus", "desc": "Bright curved edge where the coffee climbs the porcelain", "unit": "x" }
const float CoffeeStain = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Coffee Stain", "desc": "Faint brown line on the inner wall just above the coffee", "scale": 100, "unit": "%" }

// --- Saucer ---
const bool ShowSaucer = true; // var: { "name": "Saucer", "desc": "Show the saucer under the cup" }
const bool ShowPattern = true; // var: { "name": "Brim Pattern", "desc": "Decorative band of lines, scallops and dots on the saucer brim" }
const vec3 PatternColor = vec3(0.05, 0.12, 0.42); // var: { "type": "col", "name": "Pattern Color", "desc": "Color of the brim decoration (cobalt blue by default)" }
const float PatternRepeats = 64.0; // var: { "min": 24.0, "max": 120.0, "step": 4.0, "name": "Pattern Repeats", "desc": "Number of scallops around the brim: lower = larger motifs" }

// --- Logo (when image is set) ---
const vec3 LogoColor = vec3(0.16, 0.14, 0.12); // var: { "type": "col", "name": "Logo Color", "desc": "Fixed etch color for the logo on the cup" }
const float LogoColorMix = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Logo Color Mix", "desc": "Blend from the image's original colors (0%) to the fixed Logo Color (100%)", "scale": 100, "unit": "%" }
const float LogoOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Logo Opacity", "desc": "Opacity of the etched logo on the cup", "scale": 100, "unit": "%" }
const float LogoRelief = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Logo Relief", "desc": "Subtle etched relief lighting on the logo edges", "unit": "x" }
const float LogoSize = 1.3; // var: { "min": 0.3, "max": 1.5, "step": 0.02, "name": "Logo Size", "desc": "Size of the logo on the cup", "unit": "x" }
const float LogoHeight = 0.0; // var: { "min": -0.15, "max": 0.15, "step": 0.005, "name": "Logo Height", "desc": "Vertical position of the logo on the cup wall" }
const float LogoAngle = 10.0; // var: { "min": -180.0, "max": 180.0, "step": 1.0, "name": "Logo Angle", "desc": "Horizontal rotation of the logo around the cup", "unit": "°" }

// --- Shadow ---
const bool ContactShadow = true; // var: { "name": "Contact Shadow", "desc": "Soft shadow under the saucer / cup (for compositing)" }
const float ShadowOpacity = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow Opacity", "desc": "Darkness of the contact shadow", "scale": 100, "unit": "%" }

#define SteamAmount mix(0.0, 2.0, iMouse.x)
#define CoffeeFill mix(0.0, 1.0, iMouse.y)
#define Crema mix(0.0, 1.0, iMouse.z)
#define KeyLight mix(0.4, 2.0, iMouse.w)

// cup profile (r, y): wall = quadratic Bezier A-B-C, half wall thickness
const vec2 CA = vec2(0.24, 0.045);
const vec2 CB = vec2(0.50, 0.045);
const vec2 CC = vec2(0.47, 0.62);
const float CWall = 0.016;
// saucer profile
const vec2 SA = vec2(0.0, -0.01);
const vec2 SB = vec2(0.46, -0.016);
const vec2 SC = vec2(0.80, 0.075);
const float SWall = 0.012;
const vec3 HandleC = vec3(0.535, 0.355, 0.0);
// light directions (world): key softbox front left, rim light back right
const vec3 LkDir = vec3(-0.61, 0.52, 0.59);
const vec3 LrDir = vec3(0.74, 0.39, -0.54);

// per frame state
mat2 gRotM = mat2(1.0);
float gLevel = 0.53;
vec3 gRo = vec3(0.0);       // camera position (pixel footprint for analytic AA in shade())
float gPixA = 0.001;        // pixel angle
float gSkipCoffee = 0.0;   // 1: map() without the coffee (porcelain normal at the waterline)
float gRIn = 0.45;
float gLogoYc = 0.36;
float gLogoUc = 0.74;
float gLogoR = 0.483;
vec2 gLogoSize = vec2(0.32);
vec2 gLogoTexSz = vec2(1.0);

vec4 over(vec4 dst, vec4 src) { return src + dst * (1.0 - src.a); }

#define ZERO min(iFrame, 0)

// Exact-matching Newton-Raphson distance to our smooth convex cup / saucer Beziers (no pow/acos/cos/sin/branch)
float sdQuadBezierNR(vec2 q, vec2 a, vec2 l, vec2 c2, float t)
{
    vec2 ao = a - q;
    for (int i = 0; i < 3; i++) {
        vec2 v = l + c2 * t;
        vec2 d = ao + (2.0 * l + c2 * t) * t;
        float g = dot(d, v);
        float gp = 2.0 * dot(v, v) + dot(d, c2);
        t = clamp(t - g / max(gp, 0.05), 0.0, 1.0);
    }
    vec2 d = ao + (2.0 * l + c2 * t) * t;
    return length(d);
}

// cup (body + foot + handle) in object space
float cupDist(vec3 p, vec2 q)
{
    float t0 = clamp(max(sqrt(max(q.y - CA.y, 0.0) * (1.0 / (CC.y - CA.y))), (q.x - CA.x) * 1.2), 0.0, 1.0);
    float dCup = sdQuadBezierNR(q, CA, CB - CA, CA - 2.0 * CB + CC, t0) - CWall;
    dCup = EwSmoothMin(dCup, EwSdSegment2D(q, vec2(0.0, CA.y), CA) - CWall, 0.02);
    dCup = EwSmoothMin(dCup, length(q - vec2(0.2, 0.02)) - 0.017, 0.02);   // foot ring
    // handle: an upright elliptic ring, flattened cross section
    vec3 h = p - HandleC;
    float ring = length(vec2(h.x, h.y * 0.85)) - 0.12;
    float dH = length(vec2(ring, h.z * 0.7)) - 0.024;
    dH = max(dH, 0.462 - p.x);
    return EwSmoothMin(dCup, dH * 0.85, 0.025);
}
// coffee: thin slab under the liquid level, its rim hidden in the wall (object space)
float coffeeDist(vec3 p)
{
    return max(abs(p.y - gLevel + 0.015) - 0.015, length(p.xz) - gRIn);
}

// scene: x = distance, y = material (1 cup, 2 saucer, 3 coffee)
vec2 map(vec3 pw)
{
    vec3 p = pw;
    p.xz = gRotM * p.xz;
    float r = length(pw.xz);
    vec2 q = vec2(r, pw.y);
    vec2 res = vec2(cupDist(p, q), 1.0);
    if (gSkipCoffee < 0.5) {
        float dCof = max(abs(pw.y - gLevel + 0.015) - 0.015, r - gRIn);
        if (dCof < res.x) res = vec2(dCof, 3.0);
    }
    if (ShowSaucer) {
        float dBoxS = max(r - 0.82, max(pw.y - 0.105, -0.045 - pw.y));
        if (dBoxS < res.x) {
            float ts0 = clamp(q.x * 1.25, 0.0, 1.0);
            float dS = sdQuadBezierNR(q, SA, SB - SA, SA - 2.0 * SB + SC, ts0) - SWall;
            dS = EwSmoothMin(dS, length(q - vec2(0.36, -0.022)) - 0.012, 0.02);    // foot ring
            // raised ridge around the cup's seat, just outside the visible base of the cup, with a
            // soft cove on its inner side (the saucer well)
            dS = EwSmoothMin(dS, length(q - vec2(0.405, 0.021)) - 0.0105, 0.009);
            dS = EwSmoothMin(dS, length(q - vec2(0.43, 0.019)) - 0.006, 0.01);
            // fine bead where the well turns into the brim
            dS = EwSmoothMin(dS, length(q - vec2(0.6, 0.036)) - 0.0055, 0.008);
            // rolled, slightly thickened lip
            dS = EwSmoothMin(dS, length(q - vec2(0.793, 0.078)) - 0.0145, 0.012);
            if (dS < res.x) res = vec2(dS, 2.0);
        }
    }
    return res;
}

// studio environment: dark room, wide softbox front left, thin strip back right, top light
vec3 studioEnv(vec3 R, float drift)
{
    float az = atan(R.x, R.z);
    float el = asin(clamp(R.y, -1.0, 1.0));
    float a1 = az + 0.8 + drift;
    // tall softboxes: they reach below the horizon, so the upright walls (seen from above) catch them
    float e1 = SoftboxLight * (1.0 - smoothstep(0.26, 0.36, abs(a1))) * (1.0 - smoothstep(0.5, 0.62, abs(el - 0.05)))
             * (0.7 + 0.3 * (1.0 - abs(el - 0.05) / 0.62));
    float a2 = az - 2.2 - 0.5 * drift;
    float e2 = StripLight * (1.0 - smoothstep(0.035, 0.075, abs(a2))) * smoothstep(-0.8, -0.6, el) * (1.0 - smoothstep(0.9, 1.1, el));
    float e3 = TopLight * smoothstep(1.0, 1.35, el);
    float room = (0.05 + 0.07 * smoothstep(-0.3, 0.5, el)) * RoomLight;
    return LightColor * KeyLight * e1 + vec3(e2 + e3 + room);
}

// SDF normal / AO / shadow are unified into single map() loops to minimize compile time

vec2 rayBox(vec3 ro, vec3 rd, vec3 bmin, vec3 bmax)
{
    vec3 safeRd = mix(rd, vec3(1e-6), lessThan(abs(rd), vec3(1e-6)));
    vec3 invRd = 1.0 / safeRd;
    vec3 t0 = (bmin - ro) * invRd;
    vec3 t1 = (bmax - ro) * invRd;
    vec3 tmin = min(t0, t1);
    vec3 tmax = max(t0, t1);
    return vec2(max(max(tmin.x, tmin.y), tmin.z), min(min(tmax.x, tmax.y), tmax.z));
}

// Voronoi F1 + cell hash (squared distance in loop, single sqrt)
vec2 voronoiF1(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);
    float nearestSq = 64.0;
    vec2 nearestCell = cell;
    for (int i = ZERO; i < 9; i++) {
        vec2 nb = vec2(float(i % 3 - 1), float(i / 3 - 1));
        vec2 toPt = nb + EwHash2To2(cell + nb) - frac;
        float d2 = dot(toPt, toPt);
        if (d2 < nearestSq) {
            nearestSq = d2;
            nearestCell = cell + nb;
        }
    }
    return vec2(sqrt(nearestSq), EwHash2To1(nearestCell));
}

// 3D fBm with ZERO loop bound so FXC does not unroll EwValueNoise3D
float fbm3(vec3 p, int oct)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < 3; i++) {
        if (i >= oct) break;
        sum += amp * EwValueNoise3D(p);
        p = p * 2.02 + vec3(17.0, 31.0, 47.0);
        amp *= 0.5;
    }
    return sum;
}
float cupProfileSpeed(float u)
{
    return length(2.0 * mix(CB - CA, CC - CB, u));
}

float cupLogoUV(vec3 po, float r, out vec2 uv)
{
    float u = sqrt(clamp((po.y - CA.y) / (CC.y - CA.y), 0.0, 1.0));
    vec2 Pw = mix(mix(CA, CB, u), mix(CB, CC, u), u);
    float rOut = Pw.x + CWall;
    float wallMask = smoothstep(0.075, 0.115, po.y) * (1.0 - smoothstep(0.575, 0.61, po.y))
                   * (1.0 - smoothstep(0.008, 0.022, r - rOut));
    float ly = ((u - gLogoUc) / 6.0) * (cupProfileSpeed(gLogoUc) + 4.0 * cupProfileSpeed(0.5 * (gLogoUc + u)) + cupProfileSpeed(u));
    float ang = atan(po.x, po.z);
    float dAng = mod(ang - radians(LogoAngle) + EW_PI, EW_TAU) - EW_PI;
    float lx = dAng * gLogoR;
    uv = vec2(lx, ly) / gLogoSize + 0.5;
    return wallMask;
}

float cupLogoMask(vec2 uv, vec2 duvB, float lod)
{
    vec2 bx = smoothstep(vec2(0.0), duvB, uv) * (1.0 - smoothstep(1.0 - duvB, vec2(1.0), uv));
    float inB = bx.x * bx.y;
    if (inB <= 0.001) return 0.0;
    return clamp(textureLod(iChannel0, clamp(uv, 0.0, 1.0), lod).a, 0.0, 1.0) * inB;
}

// what a reflected ray sees: the cup / saucer / coffee (simple shading) or the studio
vec3 reflection(vec3 pos, vec3 R, float drift)
{
    vec3 key = LightColor * KeyLight;
    vec3 bmin = ShowSaucer ? vec3(-0.84, -0.06, -0.84) : vec3(-0.71, -0.02, -0.71);
    vec3 bmax = ShowSaucer ? vec3(0.84, 0.66, 0.84) : vec3(0.71, 0.66, 0.71);
    vec2 rb = rayBox(pos, R, bmin, bmax);
    float tt = max(0.01, rb.x);
    float tEnd = min(1.6, rb.y);
    float hitMat = 0.0;
    vec3 hp = pos;
    vec3 n = vec3(0.0);
    int nStep = -1;
    if (tt < tEnd) {
        for (int i = ZERO; i < 44; i++) {
            vec3 te = vec3(0.0);
            vec3 mp;
            if (nStep < 0) {
                mp = pos + R * tt;
            } else {
                te = 0.5773 * (2.0 * vec3(float(((nStep + 3) >> 1) & 1), float((nStep >> 1) & 1), float(nStep & 1)) - 1.0);
                mp = hp + te * 0.002;
            }
            vec2 h = map(mp);
            if (nStep < 0) {
                if (h.x < 0.0015) {
                    hitMat = h.y;
                    if (hitMat > 2.5) break;
                    hp = mp;
                    nStep = 0;
                } else {
                    tt += h.x;
                    if (tt > tEnd || i >= 39) break;
                }
            } else {
                n += te * h.x;
                if (++nStep == 4) { n = normalize(n); break; }
            }
        }
    }
    if (hitMat > 2.5) return CoffeeColor * 0.4;
    if (hitMat > 0.5) {
        vec3 alb = PorcelainColor;
        if (SwHaveImage && hitMat < 1.5 && LogoOpacity > 0.0) {
            vec3 po = hp;
            po.xz = gRotM * po.xz;
            vec2 no2 = gRotM * n.xz;
            if (dot(no2, po.xz) > 0.0 && po.y > 0.07 && po.y < 0.615) {
                vec2 luv;
                float wm = cupLogoUV(po, length(po.xz), luv);
                if (wm > 0.001 && luv.x > 0.0 && luv.x < 1.0 && luv.y > 0.0 && luv.y < 1.0) {
                    vec4 tex = textureLod(iChannel0, luv, 2.0);
                    float mask = clamp(tex.a, 0.0, 1.0) * wm * LogoOpacity;
                    vec3 logoCol = mix(EwSrgbToLinear(tex.rgb), LogoColor, LogoColorMix);
                    alb = mix(alb, logoCol, mask);
                }
            }
        }
        float w = clamp((dot(n, normalize(LkDir)) + 0.25) / 1.25, 0.0, 1.0);
        return alb * (key * w * 0.7 + mix(vec3(0.06), vec3(0.2), 0.5 + 0.5 * n.y));
    }
    return studioEnv(R, drift);
}

// ripple height of the coffee surface (object space xz)
float rippleH(vec2 xz, float t)
{
    float tr = t * RippleSpeed;
    return EwValueNoise3D(vec3(xz * 11.0, tr * 0.35)) + 0.5 * EwValueNoise3D(vec3(xz * 23.0 + 5.0, tr * 0.5));
}

vec3 shade(vec3 pos, vec3 rd, float mat, float drift, float t)
{
    vec3 key = LightColor * KeyLight;
    vec3 Lk = normalize(LkDir);
    vec3 Lr = normalize(LrDir);
    vec3 n = vec3(0.0);
    vec3 n2 = vec3(0.0);
    vec3 pos2 = pos;
    bool doDn = false;
    float occ = 0.0, aoW = 1.0;
    float sh = 1.0, shT = 0.01;
    float shMax = min(2.5, (1.05 - pos.y) / Lk.y);
    int phase = 0;
    int stp = 0;
    for (int it = ZERO; it < 77; it++) {
        vec3 mp;
        vec3 te = vec3(0.0);
        if (phase == 0 || phase == 2) {
            te = 0.5773 * (2.0 * vec3(float(((stp + 3) >> 1) & 1), float((stp >> 1) & 1), float(stp & 1)) - 1.0);
            mp = (phase == 0 ? pos : pos2) + te * 0.0007;
        } else if (phase == 1) {
            mp = pos + (0.01 + 0.03 * float(stp)) * n;
        } else {
            if (shT > shMax || sh < 0.004) break;
            mp = pos + n * 0.003 + Lk * shT;
        }
        float d = map(mp).x;
        if (phase == 0) {
            n += te * d;
            if (++stp == 4) {
                n = normalize(n);
                if (mat < 2.5) {
                    float cv = abs(dot(n, rd));
                    vec3 tdir = rd + n * cv;
                    float lt = length(tdir);
                    if (lt > 1e-3) {
                        float fpS = min(gPixA * length(pos - gRo) / max(cv, 0.08), 0.03);
                        pos2 = pos + tdir / lt * fpS;
                        doDn = true;
                    }
                }
                phase = 1; stp = 0;
            }
        } else if (phase == 1) {
            occ += (0.01 + 0.03 * float(stp) - d) * aoW;
            aoW *= 0.95;
            if (++stp == 5) {
                phase = doDn ? 2 : 3; stp = 0;
            }
        } else if (phase == 2) {
            n2 += te * d;
            if (++stp == 4) {
                phase = 3; stp = 0;
            }
        } else {
            sh = min(sh, 10.0 * d / shT);
            shT += clamp(d, 0.01, 0.2);
            if (++stp == 64) break;
        }
    }
    vec3 po = pos;
    po.xz = gRotM * po.xz;                     // object space
    float r = length(po.xz);
    float ao = clamp(1.0 - 3.0 * occ, 0.0, 1.0);
    sh = clamp(sh, 0.0, 1.0);
    sh = sh * sh * (3.0 - 2.0 * sh);
    vec3 dn = doDn ? (normalize(n2) - n) : vec3(0.0);
    int nS = (doDn && length(dn) > 0.025) ? 3 : 1;
    vec3 col = vec3(0.0);
    vec3 nw = n;
    float F = 0.0;
    if (mat > 2.5) {
        // ---- coffee
        float e = 0.004;
        float h0 = rippleH(po.xz, t);
        vec2 g = vec2(rippleH(po.xz + vec2(e, 0.0), t) - h0, rippleH(po.xz + vec2(0.0, e), t) - h0) / e;
        vec3 no = normalize(vec3(-g.x * 0.004 * RippleStrength, 1.0, -g.y * 0.004 * RippleStrength));
        // meniscus: the coffee climbs the porcelain at the wall
        float dE = max(gRIn - r, 0.0);
        vec2 radial = po.xz / max(r, 1e-4);
        no.xz -= radial * 0.9 * Meniscus * exp(-dE / 0.006);
        no = normalize(no);
        // crema: ring at the wall + swirled patches
        vec2 cq = po.xz;
        vec2 wq = vec2(EwFbm2D(cq * 5.0 + 1.3, 3), EwFbm2D(cq * 5.0 + 8.1, 3));
        float nz = EwFbm2D(cq * 7.0 + 2.5 * wq + vec2(0.0, t * 0.01), 4);
        float edge = exp(-dE / (0.015 + 0.08 * Crema));
        float m = edge + (nz - 0.45) * 1.4 + (Crema - 0.55) * 1.8;
        float crema = smoothstep(0.35, 0.7, m) * smoothstep(0.0, 0.05, Crema);
        vec3 cc = CremaColor * mix(0.65, 1.2, smoothstep(0.3, 0.7, EwFbm2D(cq * 30.0 + 3.0 * wq, 3)));
        cc *= mix(1.0, 0.8, edge * 0.5);
        // world space normal
        nw = no;
        nw.xz = no.xz * gRotM;                 // inverse rotation (transpose)
        // tiny bubbles in the crema, anti aliased with the projected pixel footprint (cells)
        float BubF = 110.0 / CremaBubbleSize;
        float fpc = gPixA * length(pos - gRo) / max(abs(dot(nw, rd)), 0.3) * BubF;
        float bFade = 1.0 - smoothstep(0.2, 0.45, fpc);   // too small on screen: average out
        float inB = 0.0;
        float bubDist = 0.0;
        float rb = 0.2;
        if (CremaBubbles > 0.0 && crema > 0.001 && bFade > 0.001) {
            vec2 vb = voronoiF1(cq * BubF);
            float sel = step(1.0 - 0.35 * CremaBubbles, vb.y) * crema;
            rb = 0.2 + 0.18 * fract(vb.y * 17.0);
            float aab = max(0.6 * fpc, 0.03);
            bubDist = vb.x;
            inB = (1.0 - smoothstep(rb - aab, rb + aab, bubDist)) * sel;
            cc = mix(cc, cc * 0.55, inB * smoothstep(rb * 0.3, rb + aab, bubDist) * bFade);
        }
        cc *= 1.0 - 0.06 * CremaBubbles * crema * (1.0 - bFade);   // averaged bubble darkening
        float ndl = max(dot(nw, Lk), 0.0);
        vec3 amb = vec3(0.12) * (0.4 + 0.6 * ao);
        vec3 body = CoffeeColor * (amb + key * 0.25 * ndl * sh);
        vec3 cremaC = cc * (amb * 1.3 + key * (0.25 + 0.75 * ndl) * sh);
        col = mix(body, cremaC, crema);
        F = EwFresnelSchlick(dot(nw, -rd), 0.02) * mix(1.0, 0.45, crema);
        // glints on the bubbles: width at least a pixel, energy kept constant
        if (inB > 0.001) {
            float gw = max(0.06, 0.7 * fpc);
            col += key * 0.25 * inB * exp(-EwSquare((bubDist - rb * 0.5) / gw)) * (0.06 / gw) * sh * bFade;
        }
    } else {
        // ---- porcelain
        vec3 alb = PorcelainColor;
        float etchMask = 0.0;
        vec2 no2 = (gRotM * n.xz);
        // etched logo on the outer cup wall (when an image is present)
        if (SwHaveImage && mat < 1.5 && LogoOpacity > 0.0 && dot(no2, po.xz) > 0.0 && po.y > 0.07 && po.y < 0.615) {
            float fpw = gPixA * length(pos - gRo) / max(abs(dot(n, rd)), 0.25);
            vec2 luv;
            float wallMask = cupLogoUV(po, r, luv);
            if (wallMask > 0.001 && luv.x > -0.05 && luv.x < 1.05 && luv.y > -0.05 && luv.y < 1.05) {
                vec2 duvB = max(vec2(0.75 * fpw) / gLogoSize, vec2(1e-4));
                vec2 bx = smoothstep(vec2(0.0), duvB, luv) * (1.0 - smoothstep(1.0 - duvB, vec2(1.0), luv));
                float inBox = bx.x * bx.y * wallMask;
                float physPerTex = gLogoSize.x / gLogoTexSz.x;
                float lod = max(log2(max(fpw / max(physPerTex, 1e-5), 1.0)) - 0.35, 0.0);
                if (inBox > 0.001) {
                    vec4 tex = textureLod(iChannel0, clamp(luv, 0.0, 1.0), lod);
                    float mask = clamp(tex.a, 0.0, 1.0) * inBox * LogoOpacity;
                    if (mask > 0.001) {
                        vec3 origLin = EwSrgbToLinear(tex.rgb);
                        vec3 logoCol = mix(origLin, LogoColor, LogoColorMix);
                        alb = mix(alb, logoCol, mask);
                        etchMask = mask;
                    }
                }
                // subtle etched relief lighting: recessed bevel normal perturbation
                if (LogoRelief > 0.0) {
                    float stepPhys = max(0.9 * fpw, 0.0012);
                    vec2 stepUV = vec2(stepPhys) / gLogoSize;
                    if (luv.x > -stepUV.x && luv.x < 1.0 + stepUV.x && luv.y > -stepUV.y && luv.y < 1.0 + stepUV.y) {
                        float lodB = max(lod, log2(max(stepPhys / max(physPerTex, 1e-5), 1.0)));
                        float hR = cupLogoMask(luv + vec2(stepUV.x, 0.0), duvB, lodB);
                        float hL = cupLogoMask(luv - vec2(stepUV.x, 0.0), duvB, lodB);
                        float hU = cupLogoMask(luv + vec2(0.0, stepUV.y), duvB, lodB);
                        float hD = cupLogoMask(luv - vec2(0.0, stepUV.y), duvB, lodB);
                        vec2 grad = vec2(hR - hL, hU - hD) * 0.5 * wallMask * LogoOpacity;
                        vec3 no = normalize(vec3(no2.x, n.y, no2.y));
                        vec3 tu_o = normalize(vec3(po.z, 0.0, -po.x));
                        vec3 tv_o = normalize(cross(no, tu_o));
                        vec3 tu = tu_o; tu.xz = tu_o.xz * gRotM;
                        vec3 tv = tv_o; tv.xz = tv_o.xz * gRotM;
                        float viewFade = smoothstep(0.05, 0.25, dot(n, -rd));
                        float reliefStr = 0.48 * LogoRelief * viewFade;
                        nw = normalize(n + (tu * grad.x + tv * grad.y) * reliefStr);
                    }
                }
            }
        }
        vec3 nSh = nw;
        float ndl = dot(nSh, Lk);
        float wrap = clamp((ndl + 0.2) / 1.2, 0.0, 1.0);
        vec3 dif = key * wrap * sh;
        // soft warm scattering around the terminator and in the shadow
        float sss = exp(-EwSquare(ndl / 0.45)) * (0.35 + 0.65 * sh) + 0.25 * (1.0 - sh) * max(ndl, 0.0);
        dif += key * Translucency * 0.3 * vec3(1.0, 0.8, 0.62) * sss;
        vec3 amb = mix(vec3(0.07), vec3(0.24), 0.5 + 0.5 * nSh.y) * ao;
        vec3 rim = vec3(RimLight) * max(dot(nSh, Lr), 0.0) * (0.5 + 0.5 * ao);
        // subtle cavity darkening inside the etched recess
        float cavity = 1.0 - 0.1 * clamp(LogoRelief, 0.0, 1.5) * etchMask;
        // faint coffee line on the inner wall just above the liquid
        if (mat < 1.5 && dot(no2, po.xz) < 0.0 && r < 0.5) {
            float cl = po.y - gLevel;
            alb = mix(alb, alb * vec3(0.72, 0.6, 0.5), CoffeeStain * exp(-max(cl, 0.0) / 0.006) * step(-0.004, cl));
        }
        // decorated saucer brim: two cobalt lines framing a band of scallops and dots,
        // anti aliased analytically with the projected pixel footprint
        if (mat > 1.5 && ShowPattern && r > 0.64) {
            float fpw = gPixA * length(pos - gRo) / max(abs(dot(n, rd)), 0.3);   // footprint (object units)
            float aa = 0.75 * fpw;
            float ang = atan(po.z, po.x);
            float cells = PatternRepeats;
            float cw = 6.2831853 * r / cells;                                     // cell width at r
            float u = (fract(ang / 6.2831853 * cells) - 0.5) * cw;
            // lines (inner thin, outer thicker near the lip)
            float d = min(abs(r - 0.668) - 0.0035, abs(r - 0.788) - 0.0055);
            // scallop arches opening outwards
            float dv = r - 0.69;
            float arc = abs(length(vec2(u, dv)) - 0.5 * cw) - 0.0028;
            arc = max(arc, -dv);
            arc = max(arc, dv - 0.5 * cw);
            d = min(d, arc);
            // a dot above each arch and a small diamond between arches
            d = min(d, length(vec2(u, r - 0.748)) - 0.0058);
            vec2 dq = abs(vec2(abs(u) - 0.5 * cw, r - 0.703));
            d = min(d, (dq.x + dq.y * 0.6) - 0.0045);
            float ink = 1.0 - smoothstep(-aa, aa, d);
            // far away / tiny on screen: fade to the average tint (no moire)
            ink = mix(ink, 0.22 * step(0.66, r) * step(r, 0.795), smoothstep(0.25, 0.6, fpw / cw));
            alb = mix(alb, PatternColor, ink);
        }
        col = alb * (dif + amb + rim) * cavity;
        // etched porcelain is slightly less glossy than untouched glaze, keeping reflections natural
        float etchGloss = mix(1.0, 1.0 - 0.35 * clamp(LogoRelief, 0.0, 1.5), etchMask);
        F = (0.5 + 0.5 * ao) * etchGloss;      // Fresnel per reflection sample below
    }
    // glossy reflection (one traced ray: cup wall in the coffee, saucer in the cup, studio).
    // Near silhouettes (handle, rim) the normal turns by more than a highlight's width within
    // one pixel and the thin reflected lights alias: measure that turn analytically (one extra
    // normal, one pixel footprint further towards the silhouette) and only there average
    // 3 reflection rays spread across the pixel.
    // dn and nS were already computed in the unified SDF loop above
    vec3 rsum = vec3(0.0);
    for (int s = ZERO; s < 3; s++) {
        if (s >= nS) break;
        vec3 ns = nS == 1 ? nw : normalize(nw + dn * (float(s) - 1.0) * 0.6);
        vec3 R = reflect(rd, ns);
        if (SwHaveImage && mat < 1.5 && LogoRelief > 0.0) {
            float rn = dot(R, n);
            if (rn < 0.0) R = normalize(R - n * rn * 1.05);
        }
        float Fs = mat > 2.5 ? F : EwFresnelSchlick(max(dot(ns, -rd), 0.0), GlazeGloss) * F;
        rsum += reflection(pos + n * 0.004, R, drift) * Fs;
    }
    col += ReflectionTint * rsum / float(nS) * ReflectionStrength;
    return col;
}

// rising steam: wispy domain warped noise in a cylinder above the coffee
vec4 steam(vec3 ro, vec3 rd, float tMax, float t, float jit)
{
    float amt = SteamAmount;
    if (amt <= 0.0) return vec4(0.0);
    float y0 = gLevel;
    float y1 = gLevel + 1.0 * SteamHeight;
    float rc = 0.75;
    float a = dot(rd.xz, rd.xz);
    float b = dot(ro.xz, rd.xz);
    float c = dot(ro.xz, ro.xz) - rc * rc;
    float disc = b * b - a * c;
    if (disc <= 0.0 || a < 1e-6) return vec4(0.0);
    float sq = sqrt(disc);
    float t0 = (-b - sq) / a;
    float t1 = (-b + sq) / a;
    float ry = abs(rd.y) < 1e-4 ? 1e-4 : rd.y;
    float ta = (y0 - ro.y) / ry;
    float tb = (y1 - ro.y) / ry;
    t0 = max(max(t0, min(ta, tb)), 0.0);
    t1 = min(min(t1, max(ta, tb)), tMax);
    if (t1 <= t0) return vec4(0.0);
    int N = Quality == 0 ? 18 : (Quality == 1 ? 30 : 48);
    float dt = (t1 - t0) / float(N);
    vec3 Lr = normalize(LrDir);
    vec3 Lk = normalize(LkDir);
    vec3 key = LightColor * KeyLight;
    float ts = t * SteamSpeed;
    // forward scattering: steam glows when the rim light is behind it
    float fwd = pow(max(dot(rd, Lr), 0.0), 3.0) * RimLight * 2.5;
    // probe offset towards the key light, in noise space (self shadowing)
    vec3 lOff = Lk * vec3(4.2, 2.6, 4.2) * 0.06;
    float invH = 1.0 / (y1 - y0);
    float swayScale = SteamSway * SteamCurl;
    float dt11 = dt * 11.0;
    float selfShadowK = 2.2 * SteamSelfShadow;
    vec3 keyHalf = key * 0.5;
    vec3 fwdVec = vec3(fwd);
    vec3 p = ro + rd * (t0 + jit * dt);
    vec3 dp = rd * dt;
    vec4 acc = vec4(0.0);
    for (int i = ZERO; i < 48; i++) {
        if (i >= N || acc.a > 0.97) break;
        float h = clamp((p.y - y0) * invH, 0.0, 1.0);
        vec3 q = p;
        // the plume sways while rising
        q.xz -= swayScale * h * vec2(0.1 * sin(p.y * 2.6 - ts * 0.6 + 1.0), 0.07 * cos(p.y * 2.1 - ts * 0.45));
        float r = length(q.xz);
        float rad = gRIn * (0.6 + 0.55 * h) * SteamWidth;
        if (r < rad) {
            // soft start over the coffee, then a long, gradual thinning all the way up:
            // opacity ~ (1 - h)^SteamFadeOut, reaching exactly zero at the top of the volume
            float hIn = smoothstep(0.0, SteamFadeIn, h);
            float hOut = pow(1.0 - h, SteamFadeOut) * (1.0 - smoothstep(0.85, 1.0, h));
            float env = (1.0 - smoothstep(rad * 0.3, rad, r)) * hIn * hIn * mix(1.0, hOut, smoothstep(0.0, 0.3, h));
            if (env > 0.002) {
                // vertically stretched filaments, curling more the higher they rise
                vec3 sp = vec3(q.x * 4.2, q.y * 2.6 - ts * 0.55, q.z * 4.2);
                vec3 wp = vec3(q.x * 1.8, q.y * 1.5 - ts * 0.35, q.z * 1.8 + ts * 0.04);
                vec2 w = vec2(EwValueNoise3D(wp + 3.1), EwValueNoise3D(wp + 11.7)) - 0.5;
                float curl = SteamCurl * (1.2 + 3.5 * h);
                sp.xz += curl * w;
                sp.y += curl * 0.7 * w.y;
                float n = fbm3(sp, 3) * (1.0 / 0.875);
                float ridge = 1.0 - abs(2.0 * n - 1.0);
                float dens = (smoothstep(0.72, 0.97, ridge) + 0.08 * smoothstep(0.4, 0.9, ridge)) * env * amt;
                if (dens > 0.0) {
                    float al = 1.0 - exp(-dens * dt11);
                    // self shadow: density a little towards the key light (coarse probe)
                    float lit = 1.0;
                    if (SteamSelfShadow > 0.0) {
                        float nL = fbm3(sp + lOff, 2) * (1.0 / 0.75);
                        float shL = smoothstep(0.6, 0.95, 1.0 - abs(2.0 * nL - 1.0)) * env * amt;
                        lit = exp(-shL * selfShadowK);
                    }
                    // core of a filament (ridge peak) is denser: darker, its fringes catch more light
                    float core = smoothstep(0.88, 1.0, ridge);
                    vec3 amb = mix(vec3(0.07, 0.075, 0.09), vec3(0.17), h * 0.5 + 0.5);
                    vec3 sc = SteamColor * (amb * (1.0 - 0.35 * core)
                            + keyHalf * lit * (0.55 + 0.45 * (1.0 - core))
                            + fwdVec * (0.6 + 0.4 * (1.0 - core)));
                    acc += (1.0 - acc.a) * vec4(sc * al, al);
                }
            }
        }
        p += dp;
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    float scl = max(1.0, 1.05 * iResolution.y / iResolution.x) / CupSize;
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y * scl;
    float t = mod(iTime, 3600.0);
    float rot = radians(HandleAngle) + radians(SwingAmount) * sin(t * SwingSpeed);
    gRotM = EwRotate2D(-rot);
    gLevel = mix(0.38, 0.6, CoffeeFill);
    // inner radius of the cup at the coffee level (Bezier solved for y, wall offset horizontally)
    float tc = sqrt(clamp((gLevel - CA.y) / (CC.y - CA.y), 0.0, 1.0));
    vec2 P = mix(mix(CA, CB, tc), mix(CB, CC, tc), tc);
    vec2 T = 2.0 * mix(CB - CA, CC - CB, tc);
    gRIn = P.x - CWall * length(T) / max(abs(T.y), 1e-3) + 0.002;

    if (SwHaveImage) {
        gLogoYc = 0.39 + LogoHeight;
        gLogoUc = sqrt(clamp((gLogoYc - CA.y) / (CC.y - CA.y), 0.0, 1.0));
        vec2 Pc = mix(mix(CA, CB, gLogoUc), mix(CB, CC, gLogoUc), gLogoUc);
        gLogoR = Pc.x + CWall;
        gLogoTexSz = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAsp = gLogoTexSz.x / gLogoTexSz.y;
        float maxH = 0.33 * LogoSize;
        float maxW = 0.56 * LogoSize;
        float lh = min(maxH, maxW / imgAsp);
        gLogoSize = vec2(lh * imgAsp, lh);
    }
    float el = radians(CameraHeight);
    vec3 ta = vec3(0.0, 0.47, 0.0);
    vec3 ro = ta + 4.0 * vec3(0.0, sin(el), cos(el));
    const float focal = 1.95;
    mat3 cam = EwCameraLookAt(ro, ta, 0.0);
    vec3 rd = cam * normalize(vec3(uv, focal));
    float pixA = scl / iResolution.y / focal;
    gRo = ro;
    gPixA = pixA;
    float drift = ReflectionDrift * (0.05 * sin(t * 0.23) + 0.03 * sin(t * 0.11 + 2.0));

    // ---- raymarch cup / saucer / coffee with analytic edge detection: a pass of the ray closer
    // than one pixel footprint that leaves again is an occluding edge in front of whatever the ray
    // hits later (cup rim over the coffee, handle over the cup, silhouettes over the page)
    float hitR = 0.25;                                // hit threshold in pixel footprints
    float tHit = -1.0, mat = 0.0;
    float edgeR = 1.0, edgeT = 0.0, edgeM = 0.0;      // closest committed edge pass
    float candR = 1.0, candT = 0.0, candM = 0.0;      // closest pass of the current episode
    vec3 bmin = ShowSaucer ? vec3(-0.84, -0.06, -0.84) : vec3(-0.71, -0.02, -0.71);
    vec3 bmax = ShowSaucer ? vec3(0.84, 0.66, 0.84) : vec3(0.71, 0.66, 0.71);
    vec2 bs = rayBox(ro, rd, bmin, bmax);
    bool inBox = bs.y > max(bs.x, 0.0);
    if (inBox) {
        float tt = max(bs.x, 0.0);
        for (int i = ZERO; i < 160; i++) {
            vec2 h = map(ro + rd * tt);
            float ratio = h.x / (pixA * tt);
            if (ratio < hitR) { tHit = tt; mat = h.y; break; }
            if (ratio < candR) { candR = ratio; candT = tt; candM = h.y; }
            else if (ratio >= 1.0 && candR < 1.0) {   // the ray left the edge: commit it
                if (candR < edgeR) { edgeR = candR; edgeT = candT; edgeM = candM; }
                candR = 1.0;
            }
            tt += h.x * 0.9;
            if (tt > bs.y) break;
        }
    }
    if (tHit < 0.0 && candR < edgeR) { edgeR = candR; edgeT = candT; edgeM = candM; }

    // waterline (coffee meets porcelain, same depth, two materials): signed distance between the
    // two surfaces at the hit point, its screen derivative gives an analytic 1 px blend
    vec3 hp = ro + rd * max(tHit, 0.0);
    float creaseB = 0.0;
    bool doAlt = false;
    if (inBox) {
        vec3 ho = hp;
        ho.xz = gRotM * ho.xz;
        float sC = cupDist(ho, vec2(length(ho.xz), ho.y)) - coffeeDist(ho);   // > 0 coffee side, < 0 cup side
        float wC = min(fwidth(sC), 4.0 * pixA * max(tHit, 0.0));
        creaseB = clamp(0.5 + sC / max(wC, 1e-6), 0.0, 1.0);   // 1 = coffee
        doAlt = tHit > 0.0 && mat != 2.0 && creaseB > 0.0 && creaseB < 1.0;
    }
    bool doEdge = edgeR < 1.0;

    // shading jobs with one inlined shade() (compile time), extra jobs only on edge pixels:
    // 0 = the hit, 1 = the other material of the waterline, 2 = the occluding edge
    vec3 cHit = vec3(0.0), cAlt = vec3(0.0), cEdge = vec3(0.0);
    for (int k = ZERO; k < 3; k++) {
        bool on = k == 0 ? tHit > 0.0 : (k == 1 ? doAlt : doEdge);
        if (!on) continue;
        vec3 sp = k == 2 ? ro + rd * edgeT : hp;
        float sm = k == 0 ? mat : (k == 1 ? (mat > 2.5 ? 1.0 : 3.0) : edgeM);
        gSkipCoffee = (k == 1 && sm < 2.5) ? 1.0 : 0.0;   // porcelain normal under the coffee
        vec3 c = shade(sp, rd, sm, drift, t);
        if (k == 0) cHit = c; else if (k == 1) cAlt = c; else cEdge = c;
    }
    gSkipCoffee = 0.0;
    vec3 surf = cHit;
    if (doAlt) surf = mat > 2.5 ? mix(cAlt, cHit, creaseB) : mix(cHit, cAlt, creaseB);
    float cov = tHit > 0.0 ? 1.0 : 0.0;
    float ec = doEdge ? clamp((1.0 - edgeR) / (1.0 - hitR), 0.0, 1.0) : 0.0;

    vec4 acc = vec4(0.0);
    // ---- contact shadow on an invisible table (alpha only)
    if (ContactShadow && cov < 1.0 && rd.y < -1e-3) {
        float fy = ShowSaucer ? -0.034 : 0.004;
        vec3 pp = ro + rd * ((fy - ro.y) / rd.y);
        float rr = length(pp.xz);
        if (rr < 1.7) {
            vec3 Ls = normalize(normalize(LkDir) + vec3(0.0, 1.4, 0.0));
            float shMax = min(2.0, (1.05 - pp.y) / Ls.y);
            float aoR = ShowSaucer ? 0.95 : 0.83;
            bool doAo = rr < aoR;
            float s = 1.0, st = 0.01, occ = 0.0, aoW = 1.0;
            int ph = 0, stp = 0;
            for (int i = ZERO; i < 69; i++) {
                vec3 mp;
                if (ph == 0) {
                    if (st > shMax || s < 0.004) {
                        if (!doAo) break;
                        ph = 1; stp = 0;
                    }
                }
                if (ph == 0) {
                    mp = pp + vec3(0.0, 0.002, 0.0) + Ls * st;
                } else {
                    mp = pp + vec3(0.0, 0.01 + 0.03 * float(stp), 0.0);
                }
                float d = map(mp).x;
                if (ph == 0) {
                    s = min(s, 3.0 * d / st);
                    st += clamp(d, 0.01, 0.2);
                    if (++stp == 64) {
                        if (!doAo) break;
                        ph = 1; stp = 0;
                    }
                } else {
                    occ += (0.01 + 0.03 * float(stp) - d) * aoW;
                    aoW *= 0.95;
                    if (++stp == 5) break;
                }
            }
            s = clamp(s, 0.0, 1.0);
            float ao = doAo ? clamp(1.0 - 3.0 * occ, 0.0, 1.0) : 1.0;
            acc.a = ShadowOpacity * clamp(0.6 * (1.0 - s) + 0.7 * (1.0 - ao), 0.0, 1.0) * (1.0 - smoothstep(1.1, 1.7, rr));
        }
    }
    acc = over(acc, vec4(surf * cov, cov));
    acc = over(acc, vec4(cEdge * ec, ec));
    if (tHit < 0.0 && ec > 0.5) tHit = edgeT;
    // ---- steam (in front of the cup and above it)
    float jit = fract(52.9829189 * fract(dot(fragCoord, vec2(0.06711056, 0.00583715))));
    acc = over(acc, steam(ro, rd, tHit > 0.0 ? tHit : 1e3, t, jit));

    float A = clamp(acc.a, 0.0, 1.0);
    vec3 col = acc.rgb / max(A, 1e-4);
    col = EwTonemapAces(col * Exposure);
    col = clamp(EwLinearToSrgb(col), 0.0, 1.0);
    fragColor = vec4(col * A, A);
}
