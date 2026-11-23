//  media: image none
// =============================================================================
//  Junkyard Sculpture Frame
// =============================================================================
//  A screen frame built from a pile of retro car parts (fenders, chrome bumpers,
//  mirrors, light bulbs, horns, headlights, a whitewall tire), shaded like a
//  small 3D sculpture with cast shadows and ambient occlusion, tangled with
//  swaying, sporadically sparking cables, around a defocused scrap yard.
//  Overlay: premultiplied alpha, the outermost pixels are always transparent.
//
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Light Angle | Turns the key light around the pile, the shadows follow | 0.5 | -90 .. 90 | {0}°
//  iMouse.w = Bulb Glow | Brightness of the lit light bulbs and their halo | 0.5 | 0 .. 2 | {0:2}x

// --- Frame (shared) ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Light sway, wire sway, sparks and bulb flicker animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner image or defocused scrap yard inside the frame (0 = transparent opening)", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

// --- Pile ---
const float PartSize = 1.0; // var: { "min": 0.6, "max": 1.5, "step": 0.05, "name": "Part Size", "desc": "Size of the car parts relative to the frame band", "unit": "x" }
const float Seed = 0.0; // var: { "min": 0.0, "max": 100.0, "step": 1.0, "name": "Seed", "desc": "Rearranges the pile, the wires and the yard" }
const bool ShowTire = true; // var: { "name": "Tire", "desc": "Old whitewall tire at the bottom center" }
const float Corrosion = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Corrosion", "desc": "Share of rusty / patinated parts (also the cars in the yard)", "scale": 100, "unit": "%" }
const vec3 BulbColor = vec3(1.0, 0.6, 0.25); // var: { "type": "col", "name": "Bulb Color", "desc": "Color of the glowing light bulbs" }

// --- Light ---
const float LightIntensity = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Key Light", "desc": "Brightness of the key light", "unit": "x" }
const vec3 KeyColor = vec3(1.0, 0.86, 0.7); // var: { "type": "col", "name": "Key Color", "desc": "Color of the key light on the parts and cables" }
const float Exposure = 1.2; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float ShadowStrength = 0.85; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadows", "desc": "Darkness of the cast shadows", "scale": 100, "unit": "%" }
const float AOStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Ambient Occlusion", "desc": "Darkening in the crevices and deep inside the pile", "unit": "x" }
const float AmbientLight = 0.45; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Ambient Light", "desc": "Fill light in the shadows of the pile (lower = deeper, moodier shadows)" }
const float LightSway = 20.0; // var: { "min": 0.0, "max": 60.0, "step": 1.0, "name": "Light Sway", "desc": "How far the key light swings back and forth", "unit": "°" }
const float LightSwaySpeed = 0.25; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Sway Speed", "desc": "How fast the key light swings", "unit": "x" }
const float LightElevation = 0.7; // var: { "min": 0.3, "max": 1.3, "step": 0.01, "name": "Light Height", "desc": "Elevation of the key light: low = long shadows, high = short shadows", "unit": "rad" }

// --- Yard ---
const vec3 SkyColor = vec3(0.95, 0.62, 0.38); // var: { "type": "col", "name": "Yard Sky", "desc": "Dusk sky color of the scrap yard (also reflected by the chrome)" }
const float ContactShadow = 0.65; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Contact Shadow", "desc": "Darkening of the yard right next to the pile", "scale": 100, "unit": "%" }
const float YardVignette = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Yard Vignette", "desc": "Darkening of the yard towards the frame", "scale": 100, "unit": "%" }
const float BokehAmount = 0.1; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Bokeh", "desc": "Brightness of the blurred light spots in the yard" }

// --- Wires ---
const float WireDensity = 1.0; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Wires", "desc": "Amount of cables woven through the pile and hanging at the sides (0 = none)", "unit": "x" }
const float WireLength = 1.0; // var: { "min": 0.5, "max": 1.5, "step": 0.05, "name": "Wire Length", "desc": "How long the side cables hang and how far their loops sag inward", "unit": "x" }
const float WireThickness = 1.0; // var: { "min": 0.5, "max": 2.5, "step": 0.05, "name": "Wire Thickness", "desc": "Thickness of the cables", "unit": "x" }
const float Wind = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Wind", "desc": "Strength of the wind swaying the side cables", "unit": "x" }
const float GustSpeed = 0.35; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Gust Speed", "desc": "How fast the wind gusts travel across the cables", "unit": "x" }

// --- Sparks ---
const float SparkShare = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Live Wires", "desc": "Share of dangling wires that spark", "scale": 100, "unit": "%" }
const float SparkRate = 1.0; // var: { "min": 0.2, "max": 3.0, "step": 0.05, "name": "Spark Rate", "desc": "How often the live wires spark", "unit": "x" }
const float SparkIntensity = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Spark Brightness", "desc": "Brightness of the sparks and their flash", "unit": "x" }
const vec3 ArcColor = vec3(0.75, 0.88, 1.0); // var: { "type": "col", "name": "Arc Color", "desc": "Color of the electric arc at the wire tip" }
const vec3 SparkColor = vec3(1.0, 0.5, 0.12); // var: { "type": "col", "name": "Spark Color", "desc": "Color of the flying sparks (the hottest ones turn white)" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define LightAngle mix(-90.0, 90.0, iMouse.z)
#define BulbGlow mix(0.0, 2.0, iMouse.w)

// premultiplied "over" compositing
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
// loop start the compiler can't fold: keeps big loops rolled (much faster compile on ANGLE / D3D)
#define ZERO min(iFrame, 0)

// frame / scene globals (set once in mainImage)
vec2 gHs, gHb, gTireC, gFC;
float gRad, gQ, gN, gCell, gW, gT, gRt, gResY, gInset, gReach, gWR;
vec3 gL;
struct Hit { float h; float hx; float hy; float sd; float type; float sub; vec2 q; vec4 rnd; float glow; float fl; float bs; };

// perimeter position (px, counter clockwise from the right middle) and signed distance
vec2 frameCoord(vec2 p)
{
    vec2 a = abs(p);
    float A = 1.5707963 * gRad;
    float t;
    if (a.x > gHb.x && a.y > gHb.y) t = gHb.y + gRad * atan(a.y - gHb.y, a.x - gHb.x);
    else if (a.x > gHb.x || (a.y <= gHb.y && gHs.x - a.x < gHs.y - a.y)) t = a.y;
    else t = gHb.y + A + (gHb.x - a.x);
    float s;
    if (p.x >= 0.0 && p.y >= 0.0) s = t;
    else if (p.x < 0.0 && p.y >= 0.0) s = 2.0 * gQ - t;
    else if (p.x < 0.0) s = 2.0 * gQ + t;
    else s = 4.0 * gQ - t;
    return vec2(s, EwSdRoundedBox2D(p, gHs, gRad));
}

void addShape(float s2, float h2, float sub2, inout float sd, inout float h, inout float sub)
{
    if (s2 < 0.0 && h2 > h) { h = h2; sub = sub2; }
    sd = min(sd, s2);
}

// one car part in normalized local coords u = q / R; returns sd/R, height/R above its base, sub material
void part(vec2 u, float type, vec4 rnd, out float sd, out float h, out float sub)
{
    sd = 1e5; h = -1.0; sub = 0.0;
    if (type < 0.5) {            // light bulb
        vec2 c = u - vec2(0.0, 0.18);
        float c2 = dot(c, c);
        addShape(sqrt(c2) - 0.42, sqrt(max(0.1764 - c2, 0.0)), 0.0, sd, h, sub);
        vec2 b = u - vec2(0.0, -0.36);
        addShape(EwSdBox2D(b, vec2(0.16, 0.2)), sqrt(max(0.0256 - b.x * b.x, 0.0)) * (0.82 + 0.18 * sin(b.y * 70.0)), 1.0, sd, h, sub);
        vec2 tq = u - vec2(0.0, -0.58);
        float t2 = dot(tq, tq);
        addShape(sqrt(t2) - 0.08, sqrt(max(0.0064 - t2, 0.0)), 2.0, sd, h, sub);
    } else if (type < 1.5) {     // rear view mirror
        vec2 m = u - vec2(0.0, 0.12);
        float sM = EwSdRoundedBox2D(m, vec2(0.6, 0.33), 0.28);
        float x = clamp(-sM * 12.5, 0.0, 1.0);
        float hM = -sM > 0.08 ? 0.13 : 0.1 + 0.2 * sqrt(x * (1.0 - x));
        addShape(sM, hM, -sM > 0.08 ? 0.0 : 1.0, sd, h, sub);
        float da = EwSdSegment2D(u, vec2(0.0, -0.18), vec2(0.15, -0.72));
        addShape(da - 0.06, sqrt(max(0.0036 - da * da, 0.0)), 1.0, sd, h, sub);
        vec2 bq = u - vec2(0.15, -0.72);
        float b2 = dot(bq, bq);
        addShape(sqrt(b2) - 0.1, sqrt(max(0.01 - b2, 0.0)), 1.0, sd, h, sub);
    } else if (type < 2.5) {     // fender
        vec2 f = vec2(abs(u.x), u.y + 0.5);
        vec2 sc = vec2(0.949, 0.3153); // sin / cos of 1.25 rad
        float lf = length(f);
        float dc = (sc.y * f.x > sc.x * f.y) ? length(f - sc * 0.68) : abs(lf - 0.68);
        float hF = sqrt(max(0.0625 - dc * dc, 0.0)) * 0.7 + 0.05;
        float trim = abs(lf - 0.68) < 0.035 ? 1.0 : 0.0;
        sd = dc - 0.25; h = hF + trim * 0.02; sub = trim;
    } else if (type < 3.5) {     // old horn
        float t = clamp((u.y + 0.7) * 0.89285714, 0.0, 1.0);
        float rr = 0.06 + 0.3 * t * t * t;
        addShape(max(abs(u.x) - rr, max(-0.7 - u.y, u.y - 0.42)), sqrt(max(rr * rr - u.x * u.x, 0.0)), 0.0, sd, h, sub);
        vec2 mq = (u - vec2(0.0, 0.42)) * vec2(2.7777778, 11.111111);
        float lm = length(mq);
        addShape((lm - 1.0) * 0.09, sqrt(max(0.1296 - u.x * u.x, 0.0)) * (lm < 0.72 ? 0.7 : 1.0) + 0.02, lm < 0.72 ? 2.0 : 1.0, sd, h, sub);
        vec2 dq = u - vec2(0.0, -0.7);
        float d2 = dot(dq, dq);
        addShape(sqrt(d2) - 0.17, 0.6 * sqrt(max(0.0289 - d2, 0.0)) + 0.04, 1.0, sd, h, sub);
    } else if (type < 4.5) {     // headlight / hubcap
        float l = length(u);
        float lr = l * 2.0;
        float hL = 0.08 + 0.3 * sqrt(max(1.0 - lr * lr, 0.0));
        float s = 0.0;
        if (0.5 - l < 0.09) { s = 1.0; hL += 0.03; }
        else if (rnd.y < 0.55) hL += 0.008 * sin(u.x * 45.0) * sin(u.y * 45.0);
        else { s = 1.0; hL += 0.025 * cos(atan(u.y, u.x) * 8.0) * smoothstep(0.1, 0.3, l); }
        sd = l - 0.5; h = hL; sub = s;
    } else {                     // bumper with overriders
        float x2 = u.x * u.x;
        float yy = u.y - 0.14 * x2;
        float th = 0.17 * (1.0 - 0.25 * x2);
        float bar = max(EwSdBox2D(vec2(u.x, yy), vec2(1.05, th)), length(vec2(u.x * 0.952381, yy)) - 1.071);
        float yth = yy / th;
        float hb = 0.06 + 0.16 * sqrt(max(1.0 - yth * yth, 0.0));
        float strip = (rnd.w < 0.5 && abs(yy) < 0.035) ? 2.0 : 0.0;
        addShape(bar, hb - (strip > 1.0 ? 0.03 : 0.0), strip, sd, h, sub);
        vec2 oq = vec2(abs(u.x) - 0.45, u.y - 0.03);
        float od = length(vec2(oq.x, oq.y - clamp(oq.y, -0.28, 0.2)));
        addShape(od - 0.075, 0.2 + sqrt(max(0.005625 - od * od, 0.0)), 1.0, sd, h, sub);
    }
}

// point on the frame center line at perimeter position s: xy = position, zw = outward normal
vec4 framePoint(float s)
{
    s = mod(s, 4.0 * gQ);
    float t; vec2 sg;
    if (s < gQ) { t = s; sg = vec2(1.0, 1.0); }
    else if (s < 2.0 * gQ) { t = 2.0 * gQ - s; sg = vec2(-1.0, 1.0); }
    else if (s < 3.0 * gQ) { t = s - 2.0 * gQ; sg = vec2(-1.0, -1.0); }
    else { t = 4.0 * gQ - s; sg = vec2(1.0, -1.0); }
    float A = 1.5707963 * gRad;
    vec2 pos, n;
    if (t < gHb.y) { pos = vec2(gHs.x, t); n = vec2(1.0, 0.0); }
    else if (t < gHb.y + A) {
        float an = (t - gHb.y) / max(gRad, 1e-3);
        n = vec2(cos(an), sin(an));
        pos = gHb + n * gRad;
    } else { pos = vec2(gHb.x - (t - gHb.y - A), gHs.y); n = vec2(0.0, 1.0); }
    return vec4(pos * sg, n * sg);
}

// max distance of a part's outline from its origin, in units of R
float partExtent(float type)
{
    return type < 0.5 ? 0.68 : type < 1.5 ? 0.86 : type < 2.5 ? 0.98 : type < 3.5 ? 0.88 : type < 4.5 ? 0.53 : 1.1;
}

// max height above base of a part, in units of R
float partMaxH(float type)
{
    return type < 0.5 ? 0.42 : type < 1.5 ? 0.205 : type < 2.5 ? 0.25 : type < 3.5 ? 0.385 : type < 4.5 ? 0.415 : 0.28;
}

Hit scene(vec2 p, bool full, float hMin, float hStop, out vec2 outFc)
{
    Hit H;
    H.h = full ? -1e5 : hMin; H.hx = -1e5; H.hy = -1e5; H.sd = 1e5; H.type = -1.0; H.sub = 0.0; H.q = vec2(0.0); H.rnd = vec4(0.0); H.glow = 0.0; H.fl = 0.0; H.bs = 1e5;
    vec2 fc = frameCoord(p);
    outFc = fc;
    float s = fc.x, d = fc.y;
    if (d < -gReach - 0.8 * gW) return H;                     // deep in the opening: only wires there
    vec2 a = abs(p);
    float margin = (full ? 3.7 : 2.7) * gCell;
    bool straightV = a.y < gHb.y - margin && (a.x > gHb.x || gHs.x - a.x < gHs.y - a.y);
    bool straightH = !straightV && a.x < gHb.x - margin && (a.y > gHb.y || gHs.y - a.y <= gHs.x - a.x);
    bool straightSafe = straightV || straightH;
    vec2 sN = straightV ? vec2(p.x >= 0.0 ? 1.0 : -1.0, 0.0) : vec2(0.0, p.y >= 0.0 ? 1.0 : -1.0);
    bool twoPass = false;
    float s2 = 0.0;
    if (!straightSafe) {
        // near a corner the parts of the other edge are searched too (no seam on the diagonal)
        float A = 1.5707963 * gRad;
        float tR = min(a.y, gHb.y);
        float tT = gHb.y + A + max(gHb.x - a.x, 0.0);
        float tO = (gHs.x - a.x < gHs.y - a.y) ? tT : tR;
        s2 = (p.x >= 0.0 && p.y >= 0.0) ? tO : (p.x < 0.0 && p.y >= 0.0) ? 2.0 * gQ - tO : (p.x < 0.0) ? 2.0 * gQ + tO : 4.0 * gQ - tO;
        twoPass = !(a.x > gHb.x && a.y > gHb.y) && max(gHs.x - a.x, gHs.y - a.y) < gReach;
    }
    float maxOut = gInset - 3.0;
    int jHalf = full ? 3 : 2;
    int jSpan = 2 * jHalf + 1;
    int nJ = twoPass ? jSpan * 2 : jSpan;
    float invCell = 1.0 / gCell;
    for (int k = 3; k >= ZERO; k--) {
        float fk = float(k);
        float baseH = gW * (0.05 + 0.25 * fk);
        float layerScale = gW * PartSize * (1.0 - 0.08 * fk);
        if (!full && H.h >= baseH + 0.47 * layerScale) break;
        float shift = fk * 0.37;
        float ci1 = floor(s * invCell - shift);
        float ci2 = twoPass ? floor(s2 * invCell - shift) : 0.0;
        for (int idx = ZERO; idx < nJ; idx++) {
            bool isPass1 = idx >= jSpan;
            float cj = (isPass1 ? ci2 : ci1) + float(idx - (isPass1 ? jSpan : 0) - jHalf);
            if (isPass1 && abs(cj - ci1) < float(jHalf) + 0.5) continue;
            float id = mod(cj, gN);
            vec4 r = EwHash2To4(vec2(id * 1.13 + 0.5, fk * 17.3 + Seed * 3.7 + 1.0));
            float cu = (cj + 0.5 + (r.y - 0.5) * 0.35 + shift) * gCell;
            float rScale = layerScale * (0.8 + 0.4 * r.x);
            if (!full) {
                if (baseH + 0.395 * rScale <= H.h) continue;
                if (straightSafe && abs(s - cu) > 1.28 * rScale + 1.0) continue;
            }
            if (ShowTire && k > 0 && abs(mod(cu, 4.0 * gQ) - 3.0 * gQ) < 0.75 * gRt) continue;
            vec4 r2 = EwHash2To4(vec2(id * 0.71 + 41.0, fk * 5.9 + Seed * 1.3 + 9.0));
            float type = r2.y < 0.13 ? 0.0 : r2.y < 0.25 ? 1.0 : r2.y < 0.52 ? 2.0 : r2.y < 0.63 ? 3.0 : r2.y < 0.74 ? 4.0 : 5.0;
            float tsz = type < 0.5 ? 0.75 : type < 1.5 ? 0.9 : type < 2.5 ? 1.3 : type < 4.5 ? 0.95 : 1.15;
            float R = rScale * tsz;
            float maxOh = partMaxH(type) * R;
            if (!full && baseH + maxOh <= H.h) continue;
            float ext = partExtent(type) * R + 1.0;
            // keep every part inside the screen: outermost reach <= inset - 3 px
            float cv = min(gW * (r.z - 0.5) * 0.9 - 0.15 * gW, maxOut - ext);
            vec2 lq, fN;
            float v2;
            if (straightSafe) {
                lq = vec2(s - cu, cv - d);
                v2 = dot(lq, lq);
                fN = sN;
            } else {
                vec4 fp = framePoint(cu);
                vec2 v = p - (fp.xy + fp.zw * cv);
                v2 = dot(v, v);
                fN = fp.zw;
                vec2 T = vec2(-fN.y, fN.x);
                lq = vec2(dot(v, T), -dot(v, fN));
            }
            if (full) {
                if (H.sd > -0.75 || Additive > 0.0) {
                    float bsLim = min(H.bs, 0.7 * gW) + 0.75 * ext;
                    if (v2 < bsLim * bsLim) H.bs = min(H.bs, sqrt(v2) - 0.75 * ext);
                }
                if (H.sd <= -0.75 && type >= 0.5 && baseH + maxOh <= min(H.h - 4.01, min(H.hx, H.hy))) continue;
                float glowPad = type < 0.5 ? 0.9 * R : 0.0;
                float lim = ext + glowPad + 2.0;
                if (v2 > lim * lim) continue;
            } else {
                if (v2 > ext * ext) continue;
            }
            float ang = type > 4.5 ? (r2.x - 0.5) * 0.9 + (r2.x > 0.5 ? 3.1416 : 0.0) : r2.x * 6.2832;
            mat2 rot = EwRotate2D(ang);
            float invR = 1.0 / R;
            vec2 u = (rot * lq) * invR;
            vec4 m = vec4(r.w, r2.z, r2.w, fract(r.y * 13.1 + r2.x));
            vec2 dux = full ? (rot * vec2(-fN.y, -fN.x)) * invR : vec2(0.0);
            vec2 duy = vec2(-dux.y, dux.x);
            for (int dn = ZERO; dn < 3; dn++) {
                vec2 ud = dn == 0 ? u : dn == 1 ? u + dux : u + duy;
                float osd, oh, osub;
                part(ud, type, m, osd, oh, osub);
                osd *= R;
                float ht = baseH + oh * R;
                if (dn == 0) {
                    if (full) {
                        H.sd = min(H.sd, osd);
                        float fl = 0.0;
                        if (type < 0.5 && m.y < 0.45) {
                            fl = (0.85 + 0.15 * sin(gT * (6.0 + 8.0 * m.w) + id * 3.0))
                               * (0.3 + 0.7 * smoothstep(0.0, 0.03, abs(fract(gT * 0.13 + m.w) - 0.5)));
                            float dg = max(length(u - vec2(0.0, 0.18)) - 0.42, 0.0);
                            H.glow += fl * smoothstep(0.9, 0.0, dg) / (1.0 + dg * dg * 25.0);
                        }
                        if (osd < 0.0 && ht > H.h) {
                            H.h = ht; H.type = type; H.sub = osub; H.q = u; H.rnd = m; H.fl = fl;
                        }
                        if (osd >= 2.0) break;
                    } else {
                        if (osd < 0.0 && ht > H.h) {
                            H.h = ht;
                            if (H.h >= hStop) return H;
                        }
                        break;
                    }
                } else if (osd < 0.0) {
                    if (dn == 1) H.hx = max(H.hx, ht);
                    else H.hy = max(H.hy, ht);
                }
            }
        }
    }
    // halo never reaches the screen edge
    if (full) H.glow *= smoothstep(maxOut - 0.1 * gW, maxOut - 0.6 * gW, d);
    if (ShowTire && (full || H.h < 0.24 * gRt + 0.05 * gW)) {
        vec2 tp0 = p - gTireC;
        float Rt = gRt;
        float l2 = dot(tp0, tp0);
        float maxLim = full ? (Rt + 2.0) : Rt;
        if (full && l2 >= maxLim * maxLim) {
            float l = sqrt(l2);
            H.sd = min(H.sd, l - Rt);
            H.bs = min(H.bs, l - 0.9 * Rt);
        } else if (l2 < maxLim * maxLim) {
            for (int dn = ZERO; dn < 3; dn++) {
                vec2 tp = dn == 0 ? tp0 : dn == 1 ? tp0 + vec2(1.0, 0.0) : tp0 + vec2(0.0, 1.0);
                float l = length(tp);
                float sdT = l - Rt;
                if (sdT < 0.0) {
                    float ht, sb;
                    if (l > 0.48 * Rt) {
                        float dc = l - 0.74 * Rt, th = 0.26 * Rt;
                        ht = sqrt(max(th * th - dc * dc, 0.0)) * 0.8;
                        float gro = abs(fract(atan(tp.y, tp.x) / 6.2832 * 36.0) - 0.5);
                        if (dc > 0.13 * Rt) ht -= 0.05 * Rt * smoothstep(0.12, 0.22, gro);
                        sb = (dc > -0.17 * Rt && dc < -0.07 * Rt) ? 1.0 : 0.0;
                    } else {
                        float lr = l / (0.48 * Rt);
                        ht = 0.08 * Rt + 0.14 * Rt * sqrt(max(1.0 - lr * lr, 0.0)) + 0.012 * Rt * cos(atan(tp.y, tp.x) * 10.0) * smoothstep(0.2, 0.5, lr);
                        sb = 2.0;
                    }
                    ht += gW * 0.05;
                    if (dn == 0) {
                        if (ht > H.h) {
                            H.h = ht;
                            if (full) { H.type = 6.0; H.sub = sb; H.q = tp / Rt; H.rnd = vec4(0.3, 0.5, 0.2, 0.7); H.fl = 0.0; }
                        }
                    } else if (dn == 1) {
                        H.hx = max(H.hx, ht);
                    } else {
                        H.hy = max(H.hy, ht);
                    }
                }
                if (dn == 0) {
                    if (full) {
                        H.sd = min(H.sd, sdT);
                        H.bs = min(H.bs, l - 0.9 * Rt);
                        if (sdT >= 2.0) break;
                    } else {
                        break;
                    }
                }
            }
        }
    }
    return H;
}

// classic car paints
vec3 paintColor(float x)
{
    const vec3 cols[11] = vec3[11](
        vec3(0.6, 0.025, 0.02),    // fire red
        vec3(0.03, 0.08, 0.4),     // royal blue
        vec3(0.012, 0.012, 0.014), // black
        vec3(0.8, 0.77, 0.66),     // cream white
        vec3(0.35, 0.68, 0.58),    // mint
        vec3(0.03, 0.4, 0.45),     // turquoise
        vec3(0.8, 0.55, 0.03),     // mustard yellow
        vec3(0.03, 0.18, 0.08),    // racing green
        vec3(0.28, 0.015, 0.03),   // maroon
        vec3(0.3, 0.5, 0.78),      // baby blue
        vec3(0.75, 0.25, 0.03)     // orange
    );
    int idx = int(step(0.15, x) + step(0.27, x) + step(0.39, x) + step(0.48, x) + step(0.57, x)
                + step(0.65, x) + step(0.73, x) + step(0.81, x) + step(0.89, x) + step(0.95, x));
    return cols[idx];
}

vec3 envMap(vec3 r)
{
    float y = r.y + 0.25 * r.z;
    vec3 sky = mix(SkyColor * 1.3, vec3(0.35, 0.45, 0.65), smoothstep(0.1, 0.9, y));
    vec3 gnd = mix(vec3(0.2, 0.11, 0.06), vec3(0.04, 0.035, 0.03), smoothstep(0.0, -0.8, y));
    vec3 e = mix(gnd, sky, smoothstep(-0.04, 0.04, y + 0.1 * sin(r.x * 9.0)));
    float d = max(dot(r, gL), 0.0);
    float d2 = d * d;
    float d4 = d2 * d2;
    float d8 = d4 * d4;
    float d16 = d8 * d8;
    e += vec3(1.0, 0.85, 0.7) * (d16 * d16 * d8) * 3.0 * LightIntensity;
    return e;
}

// one blurred junked car silhouette (u = q / sc, bln = bl / sc), returns rgb + coverage
vec4 yardCar(vec2 u, float bln, vec4 r)
{
    u.x *= r.z < 0.5 ? -1.0 : 1.0;
    // fins / long hood, rounded cabin
    float body = EwSdRoundedBox2D(u, vec2(0.2, 0.042), 0.03);
    float cab = EwSdRoundedBox2D(u - vec2(-0.025, 0.07), vec2(0.095, 0.04), 0.035);
    float m = smoothstep(bln, -bln, min(body, cab));
    // wheels
    float w = 0.0;
    if (r.x >= 0.2) {
        float wl = min(length(u - vec2(-0.125, -0.04)), length(u - vec2(0.125, -0.04)));
        w = smoothstep(bln, -bln, wl - 0.04);
    }
    float alpha = max(m, w);
    if (alpha <= 0.0) return vec4(0.0);
    vec3 pc = paintColor(r.w);
    if (r.y <= Corrosion + 0.15) {
        float rust = smoothstep(0.4, 0.75, EwValueNoise2D(u * 6.0 + r.xy * 50.0));
        pc = mix(pc, vec3(0.22, 0.09, 0.035), rust);
    }
    float shade = 0.25 + 0.75 * smoothstep(-0.06, 0.12, u.y);
    vec3 c = pc * shade * (0.45 + 0.6 * SkyColor);
    // windows
    float win = smoothstep(bln, -bln, EwSdRoundedBox2D(u - vec2(-0.025, 0.075), vec2(0.08, 0.026), 0.022));
    c = mix(c, SkyColor * 0.3 + 0.02, win * 0.7);
    // chrome bumpers at both ends (soft highlights)
    float bum = smoothstep(bln, -bln, EwSdRoundedBox2D(vec2(abs(u.x) - 0.2, u.y + 0.018), vec2(0.01, 0.014), 0.008));
    c = mix(c, vec3(0.9, 0.8, 0.7), bum * 0.6);
    c = mix(c, vec3(0.015), w);
    return vec4(c, alpha);
}

// defocused scrap yard (screen space, linear)
vec3 yard(vec2 p)
{
    vec2 uv = p / gResY;
    float hor = 0.1 + 0.05 * EwValueNoise2D(vec2(uv.x * 1.5 + Seed * 1.7, 0.5));
    float gy = hor - 0.02;
    vec3 c = vec3(0.0);
    if (uv.y >= gy - 0.03) {
        c = mix(SkyColor * SkyColor * 0.9, vec3(0.1, 0.14, 0.3), smoothstep(hor - 0.05, hor + 0.5, uv.y));
        c += SkyColor * 0.5 * exp(-4.0 * length((uv - vec2(0.3, hor + 0.03)) * vec2(0.8, 2.2)));
        // distant junk heaps
        if (uv.y < hor + 0.105) {
            float heap = hor + 0.12 * EwFbm2D(vec2(uv.x * 2.0 + Seed, 1.0), 2) - 0.02;
            float hm = smoothstep(0.03, -0.03, uv.y - heap);
            if (hm > 0.0) {
                vec3 heapC = mix(vec3(0.07, 0.045, 0.04), vec3(0.22, 0.12, 0.08), EwValueNoise2D(uv * vec2(4.0, 6.0) + Seed));
                c = mix(c, mix(heapC, SkyColor * 0.35, 0.4), hm);
            }
        }
    }
    // dirt ground, soft patches (single copy of EwValueNoise2D)
    if (uv.y < gy + 0.03) {
        float dn = EwValueNoise2D(uv * vec2(2.5, 5.0) + Seed * 0.3);
        vec3 ground = mix(vec3(0.12, 0.075, 0.05), vec3(0.03, 0.022, 0.018), smoothstep(gy, -0.5, uv.y)) * (0.75 + 0.5 * dn);
        c = mix(c, ground, smoothstep(0.03, -0.03, uv.y - gy));
    }
    // three rows of junked cars (far to near), strongly defocused, some stacked
    for (int row = ZERO; row < 3; row++) {
        float fr = float(row);
        float sc = row == 0 ? 0.5 : row == 1 ? 0.9 : 1.7;
        float invSc = 1.0 / sc;
        float baseY = row == 0 ? gy - 0.005 : row == 1 ? -0.17 : -0.56;
        float bl = row == 0 ? 0.03 : row == 1 ? 0.05 : 0.1;
        float bln = bl * invSc;
        float maxR = 0.25 * sc + bl;
        if (uv.y < baseY + 0.08 * sc - maxR || uv.y > baseY + 0.195 * sc + maxR) continue;
        float maxR2 = maxR * maxR;
        float cw = 0.5 * sc;
        float ci = floor(uv.x / cw);
        float minRz = row == 2 ? 0.45 : 0.15;
        float fog = row == 0 ? 0.45 : row == 1 ? 0.2 : 0.05;
        vec4 r = vec4(0.0);
        for (int js = ZERO; js < 6; js++) {
            int j = (js >> 1) - 1;
            int st = js & 1;
            float id = ci + float(j);
            if (st == 0) r = EwHash2To4(vec2(id, fr * 7.0 + Seed * 2.3 + 3.0));
            if (r.z < minRz || (st > 0 && r.w < 0.45)) continue;
            float fs = float(st);
            vec4 r2 = EwHash2To4(vec2(id * 1.7 + fs * 11.0, fr * 3.0 + Seed + 19.0));
            vec2 cc = vec2((id + 0.5 + (r.x - 0.5) * 0.3) * cw + 0.08 * sc * (r2.x - 0.5) * fs,
                           baseY + 0.08 * sc + fs * 0.115 * sc);
            vec2 dv = uv - cc;
            if (dot(dv, dv) > maxR2) continue;
            vec2 u = (EwRotate2D((r2.y - 0.5) * (0.06 + 0.16 * fs)) * dv) * invSc;
            vec4 car = yardCar(u, bln, r2);
            if (car.a > 0.0) {
                vec3 cr = mix(car.rgb, SkyColor * 0.3, fog);
                c = mix(c, cr, car.a);
            }
        }
    }
    // soft bokeh highlights
    if (BokehAmount > 0.0) {
        vec2 g = uv / 0.15;
        vec2 gi = floor(g);
        for (int k = ZERO; k < 9; k++) {
            int j = k / 3;
            int i = k - j * 3;
            vec2 cid = gi + vec2(float(i - 1), float(j - 1));
            vec4 r = EwHash2To4(cid + Seed * 7.0 + 0.5);
            if (r.z < 0.72) continue;
            vec2 cp = cid + 0.5 + (r.xy - 0.5) * 0.8;
            float rad = 0.12 + 0.22 * r.w;
            vec2 dv = g - cp;
            float d2 = dot(dv, dv);
            if (d2 >= rad * rad) continue;
            float disc = smoothstep(rad, rad - 0.12, sqrt(d2));
            vec3 bc = mix(vec3(1.0, 0.7, 0.4), vec3(0.6, 0.8, 1.0), step(0.92, r.z));
            float tw = 0.7 + 0.3 * sin(gT * (0.6 + r.x) + r.y * 30.0);
            c += bc * disc * BokehAmount * tw;
        }
    }
    return c;
}

// soft highlight roll-off for lit images: exact up to 0.85 linear, smooth HDR shoulder above
vec3 imageToneMap(vec3 c)
{
    vec3 t = max(c - 0.85, 0.0);
    return min(c, 0.85) + 0.15 * t / (t + 0.15);
}

// premultiplied layer below the pile: background + inner image / yard (output space)
vec4 baseLayer(vec2 p, float d, float shade, float castSh, float bulbGl, vec3 sparkLight)
{
    vec4 bg = vec4(Background * Opacity, Opacity);
    // soft edge under the pile (never a hard line in the gaps between parts)
    float inner = smoothstep(0.25 * gW, -0.35 * gW, d);
    float aY = InnerOpacity * inner;
    vec4 b = bg;
    if (aY > 0.0) {
        vec3 outLin = vec3(0.0);
        float aOut = aY;
        // execute the scrap yard only when there is no image, or behind a transparent image
        if (!SwHaveImage || SwTransparentImage) {
            vec3 yc = yard(p) * shade;
            vec2 e = gHs - abs(p);                                        // vignette towards the pile
            yc *= 1.0 - YardVignette + YardVignette * smoothstep(0.0, 3.0 * gW, e.x) * smoothstep(0.0, 3.0 * gW, e.y);
            outLin = EwTonemapAces(yc * Exposure);
        }
        if (SwHaveImage) {
            vec2 imgUV = p / (2.0 * gHs) + 0.5;
            vec4 tex = texture(iChannel0, imgUV);
            float shImg = 1.0 - ShadowStrength * (1.0 - castSh);
            vec3 keyLight = KeyColor * LightIntensity * gL.z * 1.25;
            vec3 ambLight = vec3(0.42, 0.44, 0.50) * AmbientLight;
            vec3 localLight = BulbColor * (bulbGl * 0.8) + min(sparkLight, vec3(3.0)) * 0.4;
            vec3 imgLight = ((keyLight * shImg + ambLight) * shade + localLight) * (Exposure / 1.2);
            vec3 imgLin = imageToneMap(EwSrgbToLinear(tex.rgb) * imgLight);
            if (SwTransparentImage) {
                outLin = mix(outLin, imgLin, tex.a);
            } else {
                outLin = imgLin;
                aOut = aY * tex.a;
            }
        }
        if (aOut > 0.0) {
            vec3 col = clamp(EwLinearToSrgb(outLin) + EwDither(gFC, iTime, 2.0), 0.0, 1.0);
            b = over(vec4(col * aOut, aOut), bg);
        }
    }
    return b;
}

// ---- electrical wires -------------------------------------------------------
vec3 wireColor(float x)
{
    const vec3 cols[7] = vec3[7](
        vec3(0.015),                 // black rubber
        vec3(0.5, 0.03, 0.02),       // red
        vec3(0.6, 0.42, 0.03),       // yellow
        vec3(0.03, 0.1, 0.45),       // blue
        vec3(0.04, 0.28, 0.06),      // green
        vec3(0.55, 0.53, 0.48),      // grey white
        vec3(0.18, 0.1, 0.05)        // old cloth braid
    );
    int idx = int(step(0.22, x) + step(0.40, x) + step(0.52, x) + step(0.64, x) + step(0.74, x) + step(0.86, x));
    return cols[idx];
}

// wind: a slow sway per wire plus gusts travelling across the screen
float windSway(float id, float xpos)
{
    float gust = EwValueNoise1D(gT * GustSpeed - xpos / gResY * 0.8 + 11.0);
    return Wind * (0.3 * sin(gT * (1.2 + 0.6 * fract(id * 0.618)) + id * 2.1) + 1.1 * (gust - 0.45));
}

// sporadic spark burst at a bare wire tip (linear light)
vec3 sparkAt(vec2 p, vec2 tip, float id)
{
    float slotLen = 1.2 / SparkRate;
    float x = gT / slotLen + fract(id * 0.371);
    float slot = mod(floor(x), 4096.0);
    vec4 h = EwHash2To4(vec2(slot + 0.5, id * 3.17 + 5.0));
    if (h.x > 0.6) return vec3(0.0);
    float dur = 0.6;
    float u = fract(x) * slotLen - h.y * max(slotLen - dur, 0.0);
    if (u < 0.0 || u > dur) return vec3(0.0);
    vec2 v = p - tip;
    float r2 = dot(v, v);
    float sc = gW;
    if (r2 > 7.0 * sc * sc) return vec3(0.0);
    float env = 1.0 - u / dur;
    float fl = 0.35 + 0.65 * step(0.3, EwHash2To1(vec2(mod(floor(gT * 37.0), 4096.0), id)));
    float invSc2 = 1.0 / (sc * sc);
    // blue white arc core, halo and a flash lighting the surroundings
    vec3 c = ArcColor * env * fl * (14.0 * exp(-r2 * (83.33333 * invSc2)) + 2.0 * exp(-r2 * (8.333333 * invSc2)));
    c += vec3(0.45, 0.6, 1.0) * env * env * fl / (1.0 + r2 * (1.666667 * invSc2));
    float pw = max(1.0, 0.022 * sc);
    for (int k = ZERO; k < 8; k++) {
        vec4 r = EwHash3To4(vec3(slot + 0.5, id, float(k) + 0.5));
        float ang = -1.5708 + (r.x - 0.5) * 4.6;
        vec2 dir = vec2(cos(ang), sin(ang));
        float spd = sc * (1.0 + 2.2 * r.y);
        float tt = u * (0.7 + 0.6 * r.z);
        vec2 pa = tip + dir * spd * tt - vec2(0.0, 3.0 * sc) * tt * tt;
        float t0 = max(tt - 0.06, 0.0);
        vec2 pb = tip + dir * spd * t0 - vec2(0.0, 3.0 * sc) * t0 * t0 + vec2(1e-3);
        float ds = EwSdSegment2D(p, pa, pb);
        float b = max(1.0 - tt / (dur * 1.3), 0.0);
        b *= b;
        c += mix(SparkColor, vec3(1.0, 0.9, 0.7), r.w) * b * (5.0 * smoothstep(pw + 0.8, pw - 0.5, ds) + 0.6 * exp(-ds * ds * (250.0 * invSc2)));
    }
    return c;
}

// shaded cable (premultiplied linear) from the nearest wire data
vec4 shadeWire(float best, vec4 info)
{
    if (best >= 1.5) return vec4(0.0);
    float wR = info.y, dd = info.w;
    float cv = smoothstep(0.75, -0.75, best);
    float dr = dd / wR;
    float hn = sqrt(max(1.0 - dr * dr, 0.0));
    float h2 = hn * hn;
    float h4 = h2 * h2;
    float h12 = h4 * h4 * h4;
    vec3 alb = wireColor(info.x);
    float spec = 0.3;
    if (info.z > 0.5) { alb = vec3(0.95, 0.45, 0.2); spec = 1.2; }   // bare copper tip
    vec3 c = alb * (0.1 + 1.6 * LightIntensity * (0.2 + 0.8 * hn)) + alb * SkyColor * 0.15
           + vec3(1.0, 0.9, 0.8) * spec * LightIntensity * h12 * 0.7;
    return vec4(c * cv, cv);
}

// cables: wc = hanging down along the left / right side (behind the pile),
// ww = woven through the pile on all edges, wh = its height (over / under the parts)
void wires(vec2 p, vec2 fc, out vec4 wc, out vec3 spark, out vec4 ww, out float wh)
{
    float best = 1e5, bestW = 1e5;
    vec4 info = vec4(0.0), infoW = vec4(0.0);
    spark = vec3(0.0);
    wc = vec4(0.0); ww = vec4(0.0); wh = 0.0;

    float ad = 0.5 * gW;
    float xA = gHs.x - ad;
    float sparkPad = (SparkShare > 0.0 && SparkIntensity > 0.0) ? 2.7 * gW : 0.0;
    float maxIn = max((2.2 * WireLength + 0.25 * Wind + 0.1) * gW, (1.55 + 0.4 * Wind) * gW + sparkPad) + 2.0 * gWR + 2.0;
    float maxOutSide = (0.4 * Wind) * gW + sparkPad + 2.0 * gWR + 2.0;
    float yLim = gHs.y - 0.3 * gRad - 1.3 * gW;

    bool doWoven = (fc.y > -gReach && fc.y < gInset + 2.0 * gWR + 2.0);
    bool doSide = (xA - abs(p.x) <= maxIn && abs(p.x) - xA <= maxOutSide && p.y <= yLim + sparkPad + 2.0 * gWR + 2.0);
    if (!doWoven && !doSide) return;

    float nW = max(3.0, floor(4.0 * gQ / (2.2 * gW / WireDensity)));
    float spW = 4.0 * gQ / nW;
    float ciW = floor(fc.x / spW);
    vec2 hr = gHs + gInset - 4.0 - 2.0 * gWR;

    float sp = 2.0 * gW / WireDensity;
    float sx = p.x < 0.0 ? -1.0 : 1.0;
    float cjS = floor(p.y / sp);

    int wStart = doWoven ? 0 : 3;
    int wEnd = doSide ? 9 : 3;
    float wirePad = 2.0 * gWR + 2.0;

    // unified loop: w=0..2 woven cables, w=3..8 side cables -> single EwSdQuadraticBezier call
    for (int w = wStart + ZERO; w < wEnd; w++) {
        bool isWoven = (w < 3);
        vec2 a, b, c;
        float wR, colSeed, idm = 0.0;
        bool dangling = false;
        if (isWoven) {
            float id = ciW + float(w - 2);
            idm = mod(id, nW);
            vec4 r = EwHash2To4(vec2(idm + 0.5, Seed * 3.3 + 123.0));
            if (r.x > 0.6) continue;
            float s0 = (id + 0.5 * r.y) * spW;
            float s1 = s0 + spW * (1.2 + 0.9 * r.z);
            vec4 f0 = vec4(0.0), f1 = vec4(0.0), fm = vec4(0.0);
            for (int fi = ZERO; fi < 3; fi++) {
                float sf = fi == 0 ? s0 : fi == 1 ? s1 : 0.5 * (s0 + s1);
                vec4 fp = framePoint(sf);
                if (fi == 0) f0 = fp;
                else if (fi == 1) f1 = fp;
                else fm = fp;
            }
            a = clamp(f0.xy + f0.zw * gW * (r.w - 0.65) * 0.9, -hr, hr);
            c = clamp(f1.xy + f1.zw * gW * (fract(r.w * 5.7) - 0.65) * 0.9, -hr, hr);
            b = clamp(2.0 * fm.xy - 0.5 * (a + c) + fm.zw * gW * (fract(r.y * 9.1) - 0.6) * 0.8, -hr, hr);
            wR = gWR * (1.0 + 0.6 * fract(r.z * 6.3));
            colSeed = fract(r.y * 3.91 + r.z);
        } else {
            float id = cjS + float(w - 4);
            vec4 r = EwHash2To4(vec2(id + 0.5, Seed * 2.7 + 31.0 + sx * 7.0));
            if (r.x > 0.75) continue;
            float y0 = (id + 0.5 + (r.y - 0.5) * 0.6) * sp;
            if (abs(y0) > yLim) continue;
            float sw = windSway(id + sx * 50.0, sx * xA);
            a = vec2(sx * xA, y0);
            bool loopW = r.z < 0.3;
            dangling = !loopW;
            if (loopW) {
                float D = gW * (1.4 + 1.2 * r.w);
                c = vec2(sx * (xA - 0.15 * gW * r.w), y0 - D);
                b = 0.5 * (a + c) + vec2(-sx * (1.6 + 0.6 * r.w) * gW * WireLength + sw * 0.2 * gW, -0.6 * gW);
            } else {
                float L = gW * (1.6 + 1.9 * r.w) * WireLength;
                c = a + vec2(-sx * (0.9 + 0.6 * r.z) * gW + sw * 0.35 * gW, -L);
                b = a + vec2(-sx * (1.0 + 0.4 * r.z) * gW + sw * 0.12 * gW, -0.4 * L);
            }
            if (c.y < -gHs.y + 0.9 * gW) continue;            // never reaches into the bottom pile
            colSeed = fract(r.y * 5.71 + r.z);
            float rz2 = fract(r.z * 3.3 + r.w);
            if (dangling && rz2 < SparkShare && SparkIntensity > 0.0) {
                vec2 sv = p - c;
                if (dot(sv, sv) <= 7.0 * gW * gW) {
                    spark += sparkAt(p, c, floor(r.x * 997.0) + colSeed * 13.0);
                }
            }
            wR = gWR * (0.75 + 0.5 * r.w);
        }
        vec2 lo = min(min(a, b), c) - wirePad, hi = max(max(a, b), c) + wirePad;
        if (any(lessThan(p, lo)) || any(greaterThan(p, hi))) continue;
        float dd = EwSdQuadraticBezier(p, a, b + vec2(0.37, 0.21), c);
        if (isWoven) {
            if (dd - wR < bestW) {
                bestW = dd - wR;
                infoW = vec4(colSeed, wR, 0.0, dd);
                wh = gW * (0.3 + 0.55 * (0.5 + 0.5 * sin(fc.x / gW * 2.1 + idm * 1.7)));
            }
        } else {
            if (dd - wR < best) {
                float tip = (dangling && length(p - c) < 0.1 * gW + wR) ? 1.0 : 0.0;
                best = dd - wR;
                info = vec4(colSeed, wR, tip, dd);
            }
        }
    }
    for (int si = ZERO; si < 2; si++) {
        vec4 swCol = shadeWire(si == 0 ? bestW : best, si == 0 ? infoW : info);
        if (si == 0) ww = swCol;
        else wc = swCol;
    }
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {
        vec2 imgRes = max(iChannelResolution[0].xy, vec2(1.0));
        float asp = imgRes.x / imgRes.y;
        float maxInset = min(0.5 * res.x / (asp + 1.0), 0.5 * res.y / (1.0 / asp + 1.0));
        inset = min(inset, maxInset);
        vec2 maxHs = 0.5 * res - inset;
        hs = (maxHs.x > maxHs.y * asp) ? vec2(maxHs.y * asp, maxHs.y) : vec2(maxHs.x, maxHs.x / asp);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);

    gW = inset * 1.5;
    gHs = hs; gRad = rad; gHb = hs - rad; gResY = res.y; gFC = fragCoord; gInset = inset;
    gT = mod(iTime, 3600.0) * Speed;
    // key light: base direction + live angle + slow sway
    float az = 2.3 + radians(LightAngle) + radians(LightSway) * sin(gT * LightSwaySpeed);
    float el = LightElevation;
    gL = normalize(vec3(cos(az) * cos(el), sin(az) * cos(el), sin(el)));

    float outerReach = max(gInset + 2.0, WireDensity > 0.0 ? 2.7 * gW : 0.0);
    if (d > outerReach) { fragColor = vec4(Background * Opacity, Opacity); return; }

    float pr = 1.53 * PartSize * gW;                                  // largest part radius
    gReach = max(0.6 * gW, pr - inset) + pr + 4.0;                    // deepest inward reach of the pile
    gRt = min(1.3 * gW, 0.8 * min(hs.x, hs.y) + 0.5 * inset);
    float shReach = max(gReach + 1.4 * gW, ShowTire ? 2.0 * gRt - inset + 1.4 * gW : 0.0);
    gWR = max(1.0, 0.05 * gW) * WireThickness;
    float wireReach = WireDensity > 0.0 ? 0.5 * gW + (1.6 + 0.4 * Wind) * gW * WireLength + 2.8 * gW : 0.0;
    float innerReach = max(SwHaveImage ? shReach : gReach + 0.8 * gW, wireReach);

    vec3 col = vec3(0.0);
    float a = 0.0;
    float contact = 1.0;
    float imgSh = 1.0;
    float gl = 0.0;
    vec3 spark = vec3(0.0);

    if (d >= -innerReach) {
        gQ = gHb.y + 1.5707963 * gRad + gHb.x;
        gN = max(4.0, floor(4.0 * gQ / (0.72 * gW * PartSize)));
        gCell = 4.0 * gQ / gN;
        gTireC = vec2(0.0, -hs.y - inset + 3.0 + gRt);

        Hit H;
        vec2 fc0 = vec2(0.0);
        vec2 ld = normalize(gL.xy);
        float slope = gL.z / max(length(gL.xy), 1e-3);
        float maxH = gW * (0.85 + 0.85 * PartSize);
        bool onPart = false;
        bool needImgSh = false;
        float h0 = -1e5;
        float hn0 = 0.0, hn1 = 0.0, sh = 1.0, occ = 0.0;
        int i1 = 7;

        // single scene() call site: primary hit + inline exact normals (i == -1), shadow (0..3), AO (4..6)
        for (int i = -1 + ZERO; i < i1; i++) {
            bool full = (i < 0);
            if (!full && i < 4 && (ShadowStrength <= 0.0 && !needImgSh || (!onPart || sh <= 0.0) && (!needImgSh || imgSh <= 0.0))) continue;
            vec2 sp = p;
            float hMin = -1e5;
            float hStop = 1e5;
            float t = 0.0;
            float pen = 0.0;
            if (!full) {
                if (i < 4) {
                    float fi = float(i);
                    t = gW * (0.1 + 0.1 * fi + 0.1 * fi * fi);
                    if (!onPart && t * slope > maxH) break;
                    sp = p + ld * t;
                    pen = 0.05 * gW + 0.25 * t;
                    float targetOn = h0 + t * slope;
                    float targetImg = t * slope;
                    if (onPart && needImgSh) {
                        hMin = min(targetOn, targetImg) - 0.5 * pen;
                        hStop = max(targetOn, targetImg) + 0.5 * pen;
                    } else if (onPart) {
                        hMin = targetOn - 0.5 * pen;
                        hStop = targetOn + 0.5 * pen;
                    } else {
                        hMin = targetImg - 0.5 * pen;
                        hStop = targetImg + 0.5 * pen;
                    }
                } else {
                    vec2 aoDir = i == 4 ? vec2(0.8775826, 0.4794255) : i == 5 ? vec2(-0.8537837, 0.5206279) : vec2(-0.0243866, -0.9997026);
                    sp = p + aoDir * 0.25 * gW;
                    hMin = h0;
                    hStop = h0 + 0.35 * gW;
                }
            }
            vec2 tmpFc;
            Hit S = scene(sp, full, hMin, hStop, tmpFc);
            if (full) {
                H = S;
                fc0 = tmpFc;
                h0 = H.h;
                hn0 = clamp(H.hx - h0, -4.0, 4.0);
                hn1 = clamp(H.hy - h0, -4.0, 4.0);
                onPart = H.sd < 1.0 && H.h > -1e4;
                needImgSh = SwHaveImage && InnerOpacity > 0.0 && ShadowStrength > 0.0 && d < 0.25 * gW && d > -shReach;
                if (needImgSh && !onPart) {
                    if (max(d, EwSdRoundedBox2D(p + ld * (1.3 * gW), hs, rad)) < -gReach - 0.8 * gW) needImgSh = false;
                    else if (H.bs < 1e4 && H.bs > (1.3 + 0.4 * PartSize) * gW + 2.0) needImgSh = false;
                }
                if (!onPart && !needImgSh) break;
                i1 = onPart ? (AOStrength > 0.0 ? 7 : 4) : 4;
                if (onPart && ShadowStrength <= 0.0 && !needImgSh && AOStrength <= 0.0) break;
            } else {
                float hh = S.h;
                if (i < 4) {
                    if (onPart) sh = min(sh, clamp(0.5 - (hh - (h0 + t * slope)) / pen, 0.0, 1.0));
                    if (needImgSh) imgSh = min(imgSh, clamp(0.5 - (hh - t * slope) / pen, 0.0, 1.0));
                } else {
                    occ += clamp((hh - h0) / (0.35 * gW), 0.0, 1.0);
                }
            }
        }

        if (needImgSh) imgSh = mix(1.0, imgSh, smoothstep(-shReach, -shReach + 0.5 * gW, d));

        float cov = smoothstep(0.75, -0.75, H.sd);
        vec3 objCol = vec3(0.02);
        if (onPart) {
            vec3 N = normalize(vec3(-hn0, -hn1, 1.0));
            sh = 1.0 - ShadowStrength * (1.0 - sh);
            float ao = 1.0 - 0.28 * occ * AOStrength;
            ao *= mix(1.0, 0.4 + 0.6 * smoothstep(0.0, 1.1 * gW, h0), clamp(AOStrength * 0.8, 0.0, 1.0));
            ao = clamp(ao, 0.0, 1.0);

            // material
            int t = int(H.type + 0.5);
            int sb = int(H.sub + 0.5);
            vec3 alb = vec3(0.5);
            float metal = 0.0, shin = 40.0, specK = 0.4, rustable = 1.0;
            bool brass = false, usePaint = false;
            vec3 emis = vec3(0.0);
            vec3 chrome = vec3(0.92, 0.93, 0.95);
            vec2 qn = H.q;
            if (t == 0) {
                if (sb == 0) {
                    rustable = 0.0;
                    alb = vec3(0.1); metal = 0.5; shin = 120.0; specK = 1.0;
                    if (H.fl > 0.0) {
                        vec2 c = qn - vec2(0.0, 0.18);
                        float core = exp(-dot(c, c) / 0.03);
                        float fil = smoothstep(0.03, 0.0, abs(c.y - 0.04 * sin(c.x * 60.0))) * step(abs(c.x), 0.14);
                        emis = BulbColor * BulbGlow * H.fl * (0.25 + 1.4 * core + 4.0 * fil);
                    }
                } else if (sb == 1) { alb = vec3(0.75, 0.68, 0.55); metal = 1.0; shin = 60.0; brass = true; }
                else { alb = vec3(0.03); shin = 20.0; rustable = 0.0; }
            } else if (t == 1) {
                if (sb == 0) { alb = vec3(0.32, 0.36, 0.4); metal = 1.0; shin = 200.0; rustable = 0.3; }
                else { alb = chrome; metal = 1.0; shin = 90.0; }
            } else if (t == 2) {
                if (sb == 1 || H.rnd.y < 0.2) { alb = chrome; metal = 1.0; shin = 90.0; }
                else { usePaint = true; shin = 80.0; specK = 0.8; }
            } else if (t == 3) {
                if (sb == 0) {
                    if (H.rnd.y > 0.6) { usePaint = true; shin = 50.0; specK = 0.7; }
                    else { alb = vec3(0.9, 0.62, 0.28); metal = 1.0; shin = 70.0; brass = true; }
                } else if (sb == 1) { alb = vec3(0.3); metal = 1.0; shin = 40.0; }
                else { alb = vec3(0.012); shin = 10.0; specK = 0.1; rustable = 0.0; }
            } else if (t == 4) {
                if (sb == 0) {
                    alb = mix(vec3(0.85, 0.9, 1.0), vec3(1.0, 0.45, 0.1), step(0.38, H.rnd.y)) * 0.6;
                    metal = 0.7; shin = 150.0; specK = 1.0; rustable = 0.0;
                } else { alb = chrome; metal = 1.0; shin = 90.0; }
            } else if (t == 5) {
                if (sb == 0) {
                    if (H.rnd.y < 0.3) { usePaint = true; shin = 80.0; specK = 0.8; }
                    else { alb = chrome; metal = 1.0; shin = 110.0; }
                } else if (sb == 1) { alb = chrome; metal = 1.0; shin = 90.0; }
                else { alb = vec3(0.02); shin = 15.0; specK = 0.15; rustable = 0.0; }
            } else if (t == 6) {
                rustable = 0.0;
                if (sb == 0) { alb = vec3(0.03); shin = 12.0; specK = 0.15; }
                else if (sb == 1) { alb = vec3(0.72, 0.69, 0.62); shin = 20.0; specK = 0.2; }
                else { alb = chrome; metal = 1.0; shin = 90.0; rustable = 0.6; }
            }
            if (usePaint) alb = paintColor(H.rnd.x);
            if (H.rnd.z < Corrosion && rustable > 0.0) {
                float rn = EwFbm2D(qn * 3.5 + H.rnd.xw * 37.0, 4);
                float rm = smoothstep(0.38, 0.55, rn + 0.25 * (Corrosion - H.rnd.z)) * rustable;
                float pit = EwValueNoise2D(qn * 22.0 + H.rnd.wx * 11.0);
                vec3 rc = brass ? mix(vec3(0.2, 0.45, 0.36), vec3(0.35, 0.6, 0.5), pit)
                                : mix(vec3(0.16, 0.06, 0.025), vec3(0.45, 0.2, 0.07), pit);
                alb = mix(alb, rc, rm);
                metal *= 1.0 - rm;
                shin = mix(shin, 8.0, rm);
                specK = mix(specK, 0.05, rm);
            }

            // lighting
            vec3 V = vec3(0.0, 0.0, 1.0);
            float dif = max(dot(N, gL), 0.0);
            vec3 Hv = normalize(gL + V);
            float sp = pow(max(dot(N, Hv), 0.0), shin) * (shin + 8.0) / 60.0;
            vec3 Rv = reflect(-V, N);
            vec3 env = envMap(Rv);
            float fm = 1.0 - max(N.z, 0.0);
            float fm2 = fm * fm;
            float fres = 0.04 + 0.96 * fm2 * fm2 * fm;
            vec3 keyC = KeyColor * 2.4 * LightIntensity;
            vec3 ambC = mix(vec3(0.16, 0.09, 0.06), vec3(0.28, 0.33, 0.45), N.y * 0.5 + 0.5) * AmbientLight;
            objCol = alb * (1.0 - metal) * (keyC * dif * sh + ambC * ao)
                   + metal * alb * env * ao * (0.3 + 0.7 * sh)
                   + (1.0 - metal) * fres * env * ao * specK
                   + keyC * sp * sh * specK * mix(vec3(1.0), alb, metal)
                   + emis;
        }

        // hanging side cables behind the pile, woven cables over / under the parts, sparks in front
        vec4 wc = vec4(0.0), ww = vec4(0.0);
        float wh = 0.0;
        if (WireDensity > 0.0) wires(p, fc0, wc, spark, ww, wh);
        col = objCol * cov + wc.rgb * (1.0 - cov);
        a = cov + wc.a * (1.0 - cov);
        // a woven cable is hidden where a part lies higher than the cable at this point
        float wf = H.h > wh ? 1.0 - cov : 1.0;
        // composite the woven cable (premultiplied)
        float wsh = ww.a * wf;
        col = ww.rgb * wf + col * (1.0 - wsh);
        a = wsh + a * (1.0 - wsh);
        gl = H.glow * BulbGlow;
        col += BulbColor * gl * 0.25;
        a += clamp(gl * 0.6, 0.0, 1.0) * (1.0 - a);
        // spark light falls off long before the screen edge (outer pixels stay transparent)
        float edgeD = min(0.5 * res.x - abs(p.x), 0.5 * res.y - abs(p.y));
        spark *= SparkIntensity * smoothstep(3.0, 3.0 + 0.6 * gW, edgeD);
        col += spark;
        a += clamp(dot(spark, vec3(0.35)), 0.0, 1.0) * (1.0 - a);

        // contact shadow of the pile on the inner surface (image or yard)
        contact = 1.0 - ContactShadow + ContactShadow * smoothstep(-0.15 * gW, 0.7 * gW, H.bs);

        if (a > 0.0) {
            vec3 un = col / max(a, 1e-4);
            un = EwLinearToSrgb(EwTonemapAces(un * Exposure));
            col = un * a;
            float dz = EwDither(fragCoord, iTime, 2.0) * step(0.004, a);       // empty pixels stay exactly 0
            col = clamp(col + dz, 0.0, 1.0);
            a = (a >= 1.0) ? 1.0 : clamp(a + dz, 0.0, 1.0);
            col = min(col, vec3(a));
            a *= 1.0 - Additive;
        }
    }

    if (a >= 1.0) {
        fragColor = vec4(col, 1.0);
    } else {
        vec4 base = baseLayer(p, d, contact, imgSh, gl, spark);
        fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
    }
}
