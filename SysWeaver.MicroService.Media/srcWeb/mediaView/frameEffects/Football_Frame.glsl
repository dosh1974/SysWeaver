//  media: image none
// =============================================================================
//  Football Frame
// =============================================================================
//  A photo real soccer goal framing the screen: brushed aluminium posts and
//  crossbar, a 3D net box with sun shadows, a ball on the turf and an out of
//  focus stadium seen through the net. Transparent outside the goal (overlay).
//
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.z = Wind | Sway of the grass and billow of the net | 0.5 | 0 .. 2 | {0:2}x
//  iMouse.w = Grass Height | Height of the grass blades in front of the goal | 0.5 | 0.4 .. 1.6 | {0:2}x

// --- Frame (shared) ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Net and grass wind animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0); // var: { "type": "col", "name": "Inner Color", "desc": "Fill color inside the frame (blended with the stadium and goal floor by Scenery)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the fill inside the frame (0 = transparent, 100 = opaque)", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

// --- Stadium scenery ---
const float Scenery = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Scenery", "desc": "Inner fill: 0 = flat Inner Color, 100 = stadium and goal floor (pitch, boards, crowd, sky)", "scale": 100, "unit": "%" }
const vec3 SkyColor = vec3(0.22, 0.42, 0.78); // var: { "type": "col", "name": "Sky Color", "desc": "Color of the blurred sky behind the stadium (keep it darker than the net)" }
const vec3 FanColor = vec3(0.55, 0.12, 0.10); // var: { "type": "col", "name": "Fan Color", "desc": "Color of the shirt / flag patches in the blurred crowd" }
const float Floodlights = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Floodlights", "desc": "Brightness of the floodlight bokeh above the stands (0 = off)", "unit": "x" }
const vec3 FloodlightColor = vec3(1.0, 0.93, 0.8); // var: { "type": "col", "name": "Floodlight Color", "desc": "Color of the floodlight bokeh" }
const float CloudAmount = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Clouds", "desc": "How much the grey clouds cover the sky", "scale": 100, "unit": "%" }
const float BoardSpeed = 0.04; // var: { "min": 0.0, "max": 0.3, "step": 0.005, "name": "Board Speed", "desc": "Scroll speed of the LED boards behind the pitch" }
const float BoardBrightness = 0.85; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Board Brightness", "desc": "Brightness of the LED boards", "unit": "x" }
const float SceneryBlur = 0.025; // var: { "min": 0.002, "max": 0.08, "step": 0.001, "name": "Scenery Blur", "desc": "Defocus of the stadium bands (lower = sharper edges between pitch, boards, crowd and sky)" }

// --- Grass ---
const vec3 GrassColor = vec3(0.18, 0.56, 0.14); // var: { "type": "col", "name": "Grass Color", "desc": "Color of the pitch and the grass blades" }
const float StripeWidth = 120.0; // var: { "min": 10.0, "max": 200.0, "step": 1.0, "name": "Stripe Width", "desc": "Width of the mown stripes on the goal floor", "unit": "px" }
const vec3 GoalLineColor = vec3(0.86, 0.87, 0.84); // var: { "type": "col", "name": "Goal Line Color", "desc": "Color of the painted goal line under the posts" }

// --- Net and goal ---
const vec3 NetColor = vec3(0.95, 0.97, 1.0); // var: { "type": "col", "name": "Net Color", "desc": "Color of the goal net" }
const float NetCell = 18.0; // var: { "min": 6.0, "max": 40.0, "step": 0.5, "name": "Net Mesh", "desc": "Size of the net mesh", "unit": "px" }
const float NetOpacity = 0.75; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Net Opacity", "desc": "Visibility of the net inside the goal", "scale": 100, "unit": "%" }
const float GoalDepth = 0.22; // var: { "min": 0.05, "max": 0.6, "step": 0.01, "name": "Goal Depth", "desc": "How far back the net reaches on the ground", "unit": "x" }
const float ShadowStrength = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadows", "desc": "Strength of the sun shadows of posts, net and ball", "scale": 100, "unit": "%" }

// --- Aluminium ---
const vec3 PostColor = vec3(0.88, 0.89, 0.91); // var: { "type": "col", "name": "Post Color", "desc": "Tint of the aluminium posts, crossbar and supports" }
const float PostWidth = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Post Width", "desc": "Thickness of the posts and crossbar (0 hides them)", "unit": "x" }
const float SupportWidth = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Support Width", "desc": "Thickness of the net supports and ground frame relative to the posts (0 hides them)", "unit": "x" }
const float Anisotropy = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Brushed Metal", "desc": "Brushed (anisotropic) streaks and broad sheen of the aluminium", "scale": 100, "unit": "%" }
const float MetalShine = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Metal Shine", "desc": "Strength of the sun highlights on the aluminium", "unit": "x" }
const float MetalReflection = 0.75; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Metal Reflection", "desc": "How strongly the aluminium reflects sky, stands and grass" }

// --- Ball ---
const vec3 BallColor = vec3(0.97, 0.97, 0.94); // var: { "type": "col", "name": "Ball Color", "desc": "Color of the ball's hexagons" }
const vec3 PatchColor = vec3(0.05, 0.05, 0.07); // var: { "type": "col", "name": "Patch Color", "desc": "Color of the ball's pentagons" }
const float BallSize = 1.0; // var: { "min": 0.0, "max": 2.5, "step": 0.05, "name": "Ball Size", "desc": "Size of the ball resting inside the goal (0 hides it)", "unit": "x" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define Wind mix(0.0, 2.0, iMouse.z)
#define GrassHeight mix(0.4, 1.6, iMouse.w)

#define ZERO min(iFrame, 0)

const vec3 LD = normalize(vec3(-0.45, 0.8, -0.45));   // sun direction (from the front left, high)
const vec3 HALF_LV = normalize(LD + vec3(0.0, 0.0, -1.0));
const vec3 BALL_L = normalize(vec3(-0.45, 0.65, 0.6));
const vec3 BALL_V[7] = vec3[7](
    vec3(0.0, 1.0, 1.618034) * 0.525731, vec3(1.0, 1.618034, 0.0) * 0.525731, vec3(1.618034, 0.0, 1.0) * 0.525731,
    vec3(0.57735), vec3(0.0, 1.618034, 0.618034) * 0.57735, vec3(1.618034, 0.618034, 0.0) * 0.57735, vec3(0.618034, 0.0, 1.618034) * 0.57735
);
const float NET_INV_CELL = 0.70710678 / NetCell;
const float NET_TH = max(0.03, 0.6 / NetCell);

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// one layer of grass blades rising from the screen bottom; returns premultiplied color
vec4 grassLayer(vec2 q, float layer, float H, float t, float wind)
{
    float layerScale = 1.0 - 0.15 * layer;
    if (q.y < 0.0 || q.y > H * layerScale) return vec4(0.0);
    float cw = 6.0 + 1.5 * layer;
    float cell = floor(q.x / cw);
    float cov = 0.0, shade = 0.0;
    float t1 = t * 1.9, t2 = t * 0.8;
    float wFac = wind * 0.12;
    for (int k = -3 + ZERO; k <= 3; k++)
    {
        float id = cell + float(k);
        vec3 h = EwHash1To3(id * 1.37 + layer * 91.7);
        float hb = H * (0.5 + 0.5 * h.y * h.y) * layerScale;
        if (q.y > hb) continue;
        float s = q.y / hb;
        float base = (id + h.x) * cw;
        float sway = (0.6 * sin(t1 + base * 0.013 + h.z * 6.28) + sin(t2 + base * 0.004)) * wFac * hb;
        float lean = (h.z - 0.5) * hb * 0.35;
        float x = base + (sway + lean) * s * s;
        float w = (1.8 + 1.8 * h.z) * (1.0 - s) + 0.25;
        float c = clamp(0.5 - (abs(q.x - x) - w), 0.0, 1.0);
        if (c > cov) { cov = c; shade = mix(0.45, 1.2, s) * (0.8 + 0.35 * h.x); }
    }
    float tint = 0.75 + 0.2 * layer;
    vec3 col = GrassColor * shade * tint + vec3(0.06, 0.05, 0.0) * (shade * shade);
    return vec4(col * cov, cov);
}

// soccer ball, premultiplied; u = position / radius (static roll = 0.4, tilt = 0.6)
vec4 soccerBall(vec2 u, float Rpx)
{
    float r2 = dot(u, u);
    float r = sqrt(r2);
    float cov = smoothstep(1.0, 1.0 - 1.5 / Rpx, r);
    if (cov <= 0.0) return vec4(0.0);
    vec3 n = vec3(u, sqrt(max(0.0, 1.0 - r2)));
    vec3 s = n;
    s.xy = EwRotate2D(0.4) * s.xy;
    s = EwRotateX(0.6) * s;
    vec3 q = abs(s);
    float m1 = 10.0, m2 = 10.0; int id = 0;
    for (int i = ZERO; i < 7; i++)
    {
        float ang = acos(clamp(dot(q, BALL_V[i]), -1.0, 1.0)) - (i < 3 ? 0.2876 : 0.3653);
        if (ang < m1) { m2 = m1; m1 = ang; id = i; }
        else if (ang < m2) m2 = ang;
    }
    float e = m2 - m1;
    float fw = max(fwidth(e), 1e-4);
    float seam = 1.0 - smoothstep(0.018, 0.018 + fw * 1.2, e);
    vec3 alb = id < 3 ? PatchColor : BallColor;
    alb = mix(alb, PatchColor * 0.6 + 0.05, seam * (id < 3 ? 0.3 : 0.75));
    float ndl = dot(n, BALL_L);
    float dif = max(ndl, 0.0);
    float spec = pow(max(2.0 * ndl * n.z - BALL_L.z, 0.0), 30.0);
    float occl = mix(0.55, 1.0, smoothstep(-0.9, 0.2, u.y));
    vec3 col = alb * (0.22 + 0.85 * dif) * occl + vec3(0.024, 0.054, 0.012) * (1.0 - n.y) + spec * 0.45;
    return vec4(col * cov, cov);
}

// ---- goal geometry shared by the helpers (world units = pixels on the goal mouth plane, Z into the screen)
float gXL, gXR, gYG, gYB, gZ1, gZ2, gPR, gFoc;
vec2 gC;

// exit of a ray from the goal volume (sides, roof, floor, sloped back net; optional front opening)
float goalExit(vec3 ro, vec3 rd, bool front, out int id)
{
    float tm = 1e9; id = -1;
    if (rd.x < 0.0) { float s = (gXL - ro.x) / rd.x; if (s < tm) { tm = s; id = 0; } }
    if (rd.x > 0.0) { float s = (gXR - ro.x) / rd.x; if (s < tm) { tm = s; id = 1; } }
    if (rd.y > 0.0) { float s = (gYB - ro.y) / rd.y; if (s < tm) { tm = s; id = 2; } }
    if (rd.y < 0.0) { float s = (gYG - ro.y) / rd.y; if (s < tm) { tm = s; id = 3; } }
    vec3 n = vec3(0.0, gZ2 - gZ1, gYB - gYG);
    float dn = dot(rd, n);
    if (dn > 0.0) { float s = dot(vec3(0.0, gYB, gZ1) - ro, n) / dn; if (s < tm) { tm = s; id = 4; } }
    if (front && rd.z < 0.0) { float s = -ro.z / rd.z; if (s < tm) { tm = s; id = 5; } }
    return tm;
}

vec2 netCoords(vec3 P, int id)
{
    if (id == 0 || id == 1) return vec2(P.z, P.y);
    if (id == 2) return vec2(P.x, P.z);
    float slope = length(vec2(1.0, (gZ2 - gZ1) / max(gYB - gYG, 1.0)));
    return vec2(P.x, (gYB - P.y) * slope);
}

// net mesh coverage, w = surface coords (px), blur = extra defocus
float netMesh(vec2 w, float blur)
{
    vec2 g = vec2(w.x + w.y, w.x - w.y) * NET_INV_CELL;
    vec2 f = 0.5 - abs(fract(g) - 0.5);
    vec2 fwg = min(fwidth(g), vec2(0.5)) + blur * 0.05;
    vec2 l = 1.0 - smoothstep(vec2(NET_TH) - fwg, vec2(NET_TH) + fwg, f);
    float m = max(l.x, l.y);
    float dens = max(fwg.x, fwg.y);
    return mix(m, 2.0 * NET_TH, smoothstep(0.15, 0.45, dens));
}

// shadow of the front posts / crossbar on a point behind the goal mouth
float frameShadow(vec3 P)
{
    if (P.z <= 0.0) return 0.0;
    vec2 Q = P.xy + LD.xy * (-P.z / LD.z);
    float a = Q.y < gYB ? min(abs(Q.x - gXL), abs(Q.x - gXR)) : 1e9;
    float b = (Q.x > gXL && Q.x < gXR) ? abs(Q.y - gYB) : 1e9;
    float soft = 0.3 * gPR + P.z * 0.01;
    return 1.0 - smoothstep(gPR - soft, gPR + soft, min(a, b));
}

// stadium environment seen in the aluminium
vec3 envMap(vec3 R)
{
    vec3 sky = mix(vec3(0.80, 0.84, 0.88), vec3(0.40, 0.56, 0.82), smoothstep(0.05, 0.7, R.y));
    vec3 stands = vec3(0.58, 0.58, 0.62) * (0.75 + 0.5 * EwValueNoise2D(vec2(R.x * 6.0, R.y * 20.0)));
    vec3 gnd = mix(vec3(0.30, 0.44, 0.20), vec3(0.18, 0.30, 0.12), smoothstep(0.0, -0.6, R.y));
    vec3 c = mix(gnd, stands, smoothstep(-0.03, 0.02, R.y));
    c = mix(c, sky, smoothstep(0.12, 0.25, R.y));
    float rl = max(dot(R, LD), 0.0);
    float rl2 = rl * rl;
    float rl4 = rl2 * rl2;
    c += vec3(0.8, 0.76, 0.68) * (rl4 * rl4 * rl4);
    return c;
}

// brushed aluminium tube: off = offset from the axis (screen px), T = axis direction (world)
vec4 metalTube(vec2 off, float r, vec3 T, float along, float y, float dim)
{
    float cov = clamp(r - length(off) + 0.5, 0.0, 1.0);
    if (cov <= 0.0) return vec4(0.0);
    vec2 t2 = T.xy / max(length(T.xy), 1e-4);
    vec2 pe = vec2(-t2.y, t2.x);
    float u = clamp(dot(off, pe) / max(r, 1e-3), -0.98, 0.98);
    vec3 n = vec3(pe * u, -sqrt(1.0 - u * u));
    float ndh = max(dot(n, HALF_LV), 0.0);
    float dif = max(dot(n, LD), 0.0);
    float brush = EwValueNoise2D(vec2(u * 14.0, along * 0.015)) * 0.6 + EwValueNoise2D(vec2(u * 45.0, along * 0.04)) * 0.4;
    vec3 env = envMap(vec3(-2.0 * n.z * n.xy, 1.0 - 2.0 * n.z * n.z));
    float f1 = 1.0 + n.z;
    float fres = 0.8 + 0.2 * f1 * f1 * f1;
    vec3 alb = PostColor * (0.93 + 0.14 * (brush - 0.5) * Anisotropy);
    float sharp = pow(ndh, 160.0) * 1.4;
    float broad = pow(ndh, mix(40.0, 7.0, Anisotropy)) * mix(0.25, 0.5, Anisotropy) * (0.5 + brush * Anisotropy);
    vec3 col = alb * (env * fres * MetalReflection + vec3(1.0, 0.97, 0.92) * (0.15 + 0.45 * dif)) + vec3(1.0, 0.98, 0.94) * (sharp + broad) * MetalShine;
    col *= mix(0.55, 1.0, smoothstep(gYG - 2.0, gYG + 6.0 * gPR, y));
    return vec4(col * dim * cov, cov);
}

// mown turf on the ground plane (G = world point, dGz = |dG.z/dy| in world px per screen px)
vec3 turf(vec3 G, float t, float wind, float dGz)
{
    vec2 w = G.xz;
    float fw = dGz / StripeWidth;
    float stripe = mix(smoothstep(-0.3, 0.3, sin(w.y * (3.14159 / StripeWidth))), 0.5, smoothstep(0.3, 0.9, fw));
    float n1 = EwValueNoise2D(w * vec2(0.015, 0.025));
    float n2 = EwValueNoise2D(w * vec2(0.3, 0.1));
    float n3 = EwValueNoise2D(w * vec2(1.2, 0.45));
    float gust = EwValueNoise2D(w * 0.008 + vec2(-t * 0.4, 0.0));
    vec3 c = GrassColor * (0.66 + 0.14 * stripe + 0.16 * (gust - 0.5) * (0.3 + wind) + 0.16 * (n2 - 0.5) + 0.12 * (n3 - 0.5));
    c = mix(c, c * vec3(1.18, 1.06, 0.7), 0.35 * n1);
    float fz = max(dGz, 1e-3);
    float gl = 1.0 - smoothstep(gPR - fz, gPR + fz, abs(G.z));
    c = mix(c, GoalLineColor * (0.9 + 0.2 * n3), gl * 0.9);
    return c;
}

// out of focus stadium behind the goal (vertically band-culled, warp-coherent)
vec3 scenery(vec2 fc, float t)
{
    vec2 u = (fc - gC) / iResolution.y;
    float y = u.y;
    const float bl = SceneryBlur;
    float wBoard = smoothstep(-bl * 0.6, bl * 0.6, y - 0.005);
    float wCrowd = smoothstep(-bl, bl, y - 0.055);
    float wRoof  = smoothstep(-bl, bl, y - 0.30);
    float wSky   = smoothstep(-bl, bl, y - 0.345);

    vec3 col = vec3(0.0);
    float rem = 1.0;
    if (wSky > 0.0)
    {
        vec3 sky = mix(SkyColor * vec3(1.25, 1.12, 1.0), SkyColor * 0.75, smoothstep(0.35, 0.9, y));
        if (CloudAmount > 0.0)
        {
            float cl = EwValueNoise2D(u * vec2(2.0, 4.0) + vec2(t * 0.01, 0.0)) * 0.65
                     + EwValueNoise2D(u * vec2(4.5, 7.0) - vec2(t * 0.015, 0.0)) * 0.35;
            sky = mix(sky, mix(SkyColor, vec3(0.62, 0.66, 0.72), 0.6), smoothstep(0.5, 0.85, cl) * CloudAmount);
        }
        col += sky * wSky;
        rem *= 1.0 - wSky;
    }
    if (rem > 0.0 && wRoof > 0.0)
    {
        col += vec3(0.20, 0.21, 0.24) * (rem * wRoof);
        rem *= 1.0 - wRoof;
    }
    if (rem > 0.0 && wCrowd > 0.0)
    {
        float cr1 = EwValueNoise2D(u * vec2(20.0, 24.0));
        float cr2 = EwValueNoise2D(u * vec2(7.0, 6.0) + 3.1);
        vec3 crowd = mix(vec3(0.10, 0.11, 0.15), vec3(0.45, 0.43, 0.45), cr1 * 0.7 + 0.15);
        crowd = mix(crowd, FanColor, smoothstep(0.55, 0.8, cr2) * 0.6);
        crowd *= mix(0.7, 1.05, smoothstep(0.30, 0.08, y));
        col += crowd * (rem * wCrowd);
        rem *= 1.0 - wCrowd;
    }
    if (rem > 0.0 && wBoard > 0.0)
    {
        float bx = u.x * 2.2 - t * BoardSpeed;
        float pid = floor(bx);
        vec3 h0 = EwHash1To3(mod(pid - 1.0, 64.0) + 0.5), h1 = EwHash1To3(mod(pid, 64.0) + 0.5);
        vec3 b0 = mix(vec3(dot(h0, vec3(0.333))), h0, 1.7), b1 = mix(vec3(dot(h1, vec3(0.333))), h1, 1.7);
        vec3 board = clamp(mix(b0, b1, smoothstep(0.0, 0.1, fract(bx))), 0.05, 1.0) * BoardBrightness;
        col += board * (rem * wBoard);
        rem *= 1.0 - wBoard;
    }
    if (rem > 0.0)
    {
        float dz = max(-y, 0.0);
        float zz = 0.05 / max(dz, 0.003);
        float sAmt = smoothstep(0.004, 0.06, dz);
        vec3 pitch = GrassColor * (0.75 + 0.16 * mix(0.5, 0.5 + 0.5 * sin(zz * 2.0), sAmt)) * mix(1.15, 0.9, sAmt);
        pitch = mix(pitch, vec3(0.55, 0.65, 0.62), 0.25 * (1.0 - sAmt));
        col += pitch * rem;
    }
    if (Floodlights > 0.0 && y > 0.05)
    {
        float flSum = 0.0;
        for (int i = ZERO; i < 7; i++)
        {
            float fi = float(i);
            vec3 h = EwHash1To3(fi * 7.31 + 2.0);
            vec2 bp = vec2((fi - 3.0) * 0.32 + (h.x - 0.5) * 0.15, 0.315 + 0.03 * h.y);
            float r = 0.022 + 0.018 * h.z;
            float r2 = r * r;
            vec2 du = u - bp;
            float d2 = dot(du, du);
            if (d2 < r2 * 40.0)
            {
                float disc = d2 < r2 ? smoothstep(r, r * 0.8, sqrt(d2)) * 0.35 : 0.0;
                flSum += disc + 0.12 * exp(-d2 / (r2 * 4.0));
            }
        }
        col += FloodlightColor * (Floodlights * flSum);
    }
    return min(col, vec3(1.0));
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    float inset = min(Inset, 0.25 * min(res.x, res.y));

    float t = mod(iTime * Speed, 6283.185);
    float wind = Wind;
    vec2 fc = fragCoord;

    // ---- goal geometry
    gPR = max(3.5, inset * 0.2) * PostWidth;
    float pad = max(gPR, 3.5);
    float minXL = inset + pad;
    float maxXR = res.x - inset - pad;
    float minYG = max(inset, 6.0);
    float maxYB = res.y - inset - pad;
    if (SwHaveImage)
    {
        vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texRes.x / texRes.y;
        float maxVisW = max(maxXR - minXL - 2.0 * gPR, 2.0);
        float maxVisH = max(maxYB - gPR - minYG, 2.0);
        float visW = maxVisW;
        float visH = maxVisH;
        if (imgAspect > maxVisW / maxVisH)
            visH = max(maxVisW / imgAspect, 2.0);
        else
            visW = max(maxVisH * imgAspect, 2.0);
        float cx = 0.5 * (minXL + maxXR);
        float cy = 0.5 * (minYG + maxYB - gPR);
        gXL = max(cx - 0.5 * visW - gPR, minXL);
        gXR = min(cx + 0.5 * visW + gPR, maxXR);
        gYG = max(cy - 0.5 * visH, minYG);
        gYB = min(cy + 0.5 * visH + gPR, maxYB);
    }
    else
    {
        gXL = minXL; gXR = maxXR;
        gYG = minYG; gYB = maxYB;
    }
    float gh = gYB - gYG;
    gC = vec2(0.5 * (gXL + gXR), gYG + 0.45 * gh);
    gFoc = 0.62 * max(res.x, res.y);
    gZ2 = GoalDepth * 3.5 * gh;
    gZ1 = 0.38 * gZ2;

    float yRoot = max(gYG - 0.55 * minYG, 4.0);
    float bladeH = GrassHeight * (0.25 * inset + 12.0);
    float H = yRoot + bladeH;
    float ext = 2.0 * gPR + 0.3 * inset;
    float fadeW = 10.0 + 0.3 * inset;

    // ---- background and goal mouth mask
    vec4 bg = vec4(Background * Opacity, Opacity);
    vec2 rq = abs(fc - vec2(gC.x, 0.5 * (gYG + gYB))) - vec2(0.5 * (gXR - gXL), 0.5 * gh);
    float rm = max(rq.x, rq.y);
    float inner = clamp(0.5 - rm, 0.0, 1.0);
    bool inOpen = rm < 0.5;
    if (!inOpen && (fc.y > H + 2.0 || fc.y < yRoot - 8.0) && rm > gPR + 2.0) { fragColor = bg; return; }
    if (rq.x > ext + 2.0 * gPR) { fragColor = bg; return; }

    float hm = smoothstep(0.0, fadeW, min(fc.x - (gXL - ext), (gXR + ext) - fc.x));
    vec3 ro = vec3(gC, -gFoc);
    vec3 rd = vec3(fc - gC, gFoc);
    float dGz = gFoc * max(gC.y - gYG, 1.0) / max(rd.y * rd.y, 1.0);
    float dz = EwDither(fc, iTime, 2.0);
    vec4 acc = vec4(0.0);

    // ---- find support bar & front post hits upfront, then invoke metalTube() at ONE call site
    const bool drawInnerBg = !SwHaveImage || SwTransparentImage;
    bool doInner = inner > 0.0 && InnerOpacity > 0.0;
    bool doInnerBg = doInner && drawInnerBg;

    bool hitSupp = false;
    vec2 sOff = vec2(0.0); float sRr = 1.0, sAl = 0.0, sDim = 1.0; vec3 sT = vec3(1.0, 0.0, 0.0);
    float br = gPR * SupportWidth;
    if (doInnerBg && br > 0.4)
    {
        float s1 = gFoc / (gFoc + gZ1);
        float s2 = gFoc / (gFoc + gZ2);
        vec2 pRTL = gC + (vec2(gXL, gYB) - gC) * s1;
        float pRTRx = gC.x + (gXR - gC.x) * s1;
        vec2 pGBL = gC + (vec2(gXL, gYG) - gC) * s2;
        float pGBRx = gC.x + (gXR - gC.x) * s2;
        float margin = br + 2.0;
        if (fc.y < pGBL.y + margin || fc.x < pGBL.x + margin || fc.x > pGBRx - margin)
        {
            float bestD = 1e9;
            if (fc.y < pGBL.y + margin && fc.y > pGBL.y - margin)
            {
                float span = max(pGBRx - pGBL.x, 1e-4);
                float h = clamp((fc.x - pGBL.x) / span, 0.0, 1.0);
                vec2 off = fc - vec2(pGBL.x + span * h, pGBL.y);
                float rr = br * 0.8 * s2;
                float dd = length(off) - rr;
                if (dd < bestD) { bestD = dd; sOff = off; sRr = rr; sAl = h * span; sDim = 0.62; sT = vec3(1.0, 0.0, 0.0); }
            }
            if (fc.x < pGBL.x + margin || fc.x > pGBRx - margin)
            {
                bool isR = fc.x >= gC.x;
                float fx = isR ? gXR : gXL;
                float rx = isR ? pRTRx : pRTL.x;
                float gx = isR ? pGBRx : pGBL.x;
                for (int i = ZERO; i < 3; i++)
                {
                    vec2 A = i == 0 ? vec2(fx, gYG) : (i == 1 ? vec2(rx, pRTL.y) : vec2(fx, gYB));
                    vec2 B = i < 2 ? vec2(gx, pGBL.y) : vec2(rx, pRTL.y);
                    float zA = i == 1 ? gZ1 : 0.0;
                    float zB = i < 2 ? gZ2 : gZ1;
                    float rMul = i == 0 ? 0.8 : 1.0;
                    vec2 ab = B - A;
                    float ab2 = dot(ab, ab);
                    float h = clamp(dot(fc - A, ab) / max(ab2, 1e-4), 0.0, 1.0);
                    vec2 off = fc - (A + ab * h);
                    float rr = br * rMul * gFoc / (gFoc + mix(zA, zB, h));
                    float dd = length(off) - rr;
                    if (dd < bestD)
                    {
                        bestD = dd; sOff = off; sRr = rr; sAl = h * sqrt(ab2);
                        sDim = i == 0 ? 0.7 : (i == 1 ? 0.8 : 0.85);
                        sT = i == 1 ? vec3(0.0, -gh, gZ2 - gZ1) : vec3(0.0, 0.0, zB);
                    }
                }
            }
            hitSupp = bestD < 1.0;
        }
    }

    bool hitPost = false;
    vec2 pOff = vec2(0.0); vec3 pT = vec3(0.0, 1.0, 0.0); float pAl = 0.0;
    if (gPR > 0.5 && fc.y > gYG - 1.5 && rm > -gPR - 1.5)
    {
        float yCl = clamp(fc.y, gYG - gPR, gYB);
        vec2 off = fc - vec2(gXL, yCl);
        float best2 = dot(off, off); pT = vec3(0.0, 1.0, 0.0); pAl = fc.y;
        vec2 o2 = fc - vec2(clamp(fc.x, gXL, gXR), gYB);
        float d2 = dot(o2, o2);
        if (d2 < best2) { best2 = d2; off = o2; pT = vec3(1.0, 0.0, 0.0); pAl = fc.x; }
        o2 = fc - vec2(gXR, yCl);
        d2 = dot(o2, o2);
        if (d2 < best2) { best2 = d2; off = o2; pT = vec3(0.0, 1.0, 0.0); pAl = fc.y + 500.0; }
        float lim = gPR + 0.5;
        if (best2 < lim * lim) { hitPost = true; pOff = off; }
    }

    vec4 suppCol = vec4(0.0), postCol = vec4(0.0);
    if (hitSupp || hitPost)
    {
        int mStart = hitSupp ? 0 : 1;
        int mEnd = hitPost ? 2 : 1;
        for (int m = mStart + ZERO; m < mEnd; m++)
        {
            vec4 tc = metalTube(
                m == 0 ? sOff : pOff,
                m == 0 ? sRr  : gPR,
                m == 0 ? sT   : pT,
                m == 0 ? sAl  : pAl,
                fc.y,
                m == 0 ? sDim : 1.0);
            if (m == 0) suppCol = tc;
            else        postCol = tc;
        }
    }

    // ---- ray-goal intersection and shared turf evaluation (inlines turf() once)
    int hitId = -1;
    vec3 hitP = vec3(0.0);
    bool floorHit = false;
    if (doInnerBg)
    {
        float th = goalExit(ro, rd, false, hitId);
        hitP = ro + th * rd;
        floorHit = (hitId == 3);
    }

    bool needFloorTurf = doInnerBg && Scenery > 0.0 && floorHit;
    bool needFgTurf = fc.y < gYG + 1.5 && fc.y > yRoot - 8.0 && hm > 0.0 && rd.y < -1e-4;
    vec3 turfG = vec3(0.0);
    vec3 turfCol = vec3(0.0);
    float postAO = 1.0;
    if (needFloorTurf || needFgTurf)
    {
        turfG = needFloorTurf ? hitP : (ro + ((gYG - ro.y) / rd.y) * rd);
        turfCol = turf(turfG, t, wind, dGz);
        float fp = min(length(vec2(turfG.x - gXL, turfG.z)), length(vec2(turfG.x - gXR, turfG.z)));
        postAO = mix(0.5, 1.0, smoothstep(gPR, 4.0 * gPR, fp));
    }

    // ---- inner layer: stadium, floor, net, supports and ball, plus optional image
    vec4 base = bg;
    if (doInner)
    {
        vec3 bgFill = vec3(0.0);
        if (drawInnerBg)
        {
            float R = 0.032 * min(gh, 3.0 * (gXR - gXL)) * BallSize;
            vec3 Pb = vec3(gXR - gPR - 2.6 * R, gYG + R, 0.16 * gZ2 + R);
            vec3 Sb = Pb - LD * ((Pb.y - gYG) / LD.y);

            vec4 innerAcc = vec4(0.0);
            if (!floorHit)
            {
                vec2 w = netCoords(hitP, hitId);
                vec3 nS = hitId == 0 ? vec3(1.0, 0.0, 0.0) : (hitId == 1 ? vec3(-1.0, 0.0, 0.0) : (hitId == 2 ? vec3(0.0, -1.0, 0.0) : -normalize(vec3(0.0, gZ2 - gZ1, gh))));
                float anchor = smoothstep(0.0, 0.45 * gZ2, hitP.z) * smoothstep(0.0, 0.15 * gh, hitP.y - gYG);
                float amp = (1.5 + 0.02 * gh) * (0.25 + wind) * anchor;
                float wv = sin(w.y * 0.012 + t * 1.3 + sin(w.x * 0.009 + t * 0.7));
                float wu = cos(w.x * 0.011 - t * 1.1 + sin(w.y * 0.01 - t * 0.5));
                w += amp * vec2(wv, wu);
                float zf = clamp(hitP.z / max(gZ2, 1.0), 0.0, 1.0);
                float m = netMesh(w, zf);
                float lit = 0.62 + 0.45 * abs(dot(nS, LD));
                lit *= 1.0 - 0.6 * ShadowStrength * frameShadow(hitP);
                float sheen = 0.88 + 0.12 * wv * wu;
                float na = m * NetOpacity * mix(1.0, 0.85, zf);
                innerAcc = vec4(NetColor * lit * sheen * mix(1.0, 0.8, zf) * na, na);
            }

            bgFill = InnerColor;
            if (Scenery > 0.0)
            {
                vec3 innerBg;
                if (floorHit)
                {
                    vec3 G = turfG;
                    vec3 gc = turfCol;
                    float sh = frameShadow(G);
                    int id2;
                    float ts = goalExit(G, LD, true, id2);
                    if (id2 != 5) sh = max(sh, netMesh(netCoords(G + ts * LD, id2), 1.0) * NetOpacity * 0.9);
                    float zf = clamp(G.z / max(gZ2, 1.0), 0.0, 1.0);
                    float side = min(G.x - gXL, gXR - G.x);
                    float ao = mix(0.7, 1.0, smoothstep(0.0, 0.1 * gh, side)) * mix(1.0, 0.82, zf) * postAO;
                    if (R >= 1.0)
                    {
                        float bd = length((G.xz - Sb.xz) / vec2(R * 1.05, R * 1.35));
                        sh = max(sh, 0.9 * (1.0 - smoothstep(0.45, 1.15, bd)));
                        ao *= mix(0.55, 1.0, smoothstep(0.0, 1.6 * R, length(G.xz - Pb.xz)));
                    }
                    gc *= (0.42 + 0.58 * (1.0 - sh * ShadowStrength)) * ao;
                    innerBg = gc / (1.0 + 0.3 * max(gc - 0.75, 0.0));
                }
                else innerBg = scenery(fc, t);
                bgFill = clamp(mix(InnerColor, innerBg + dz, Scenery), 0.0, 1.0);
            }

            if (hitSupp) innerAcc = over(suppCol, innerAcc);

            if (R >= 1.0)
            {
                float focPb = gFoc / (gFoc + Pb.z);
                float Rs = R * focPb;
                vec2 bc = gC + (Pb.xy - gC) * focPb;
                vec2 bOff = fc - bc;
                float bLim = Rs + 2.0;
                if (dot(bOff, bOff) < bLim * bLim) innerAcc = over(soccerBall(bOff / Rs, Rs), innerAcc);
            }

            float ia = clamp(innerAcc.a, 0.0, 1.0);
            vec3 ic = innerAcc.rgb / (1.0 + 0.3 * max(innerAcc.rgb - 0.75, 0.0));
            ia = clamp(ia + dz, 0.0, 1.0);
            ic = clamp(min(ic + dz, vec3(ia)), 0.0, 1.0);
            bgFill = clamp(ic + bgFill * (1.0 - ia), 0.0, 1.0);
        }

        if (SwHaveImage)
        {
            vec2 imgUV = (fc - vec2(gXL + gPR, gYG)) / max(vec2((gXR - gXL) - 2.0 * gPR, gh - gPR), vec2(1.0));
            vec2 cUV = clamp(imgUV, 0.0, 1.0);
            vec4 img = texture(iChannel0, cUV);

            float sh = 0.0;
            float ao = 1.0;
            if (gPR > 0.5)
            {
                float zImg = min(gPR, 26.0);
                vec2 qImg = fc + LD.xy * (-zImg / LD.z);
                float qy = clamp(qImg.y, gYG, gYB);
                float dF = min(
                    length(qImg - vec2(clamp(qImg.x, gXL, gXR), gYB)),
                    min(length(qImg - vec2(gXL, qy)), length(qImg - vec2(gXR, qy))));
                float softF = 0.45 * gPR + zImg * 0.15;
                sh = (1.0 - smoothstep(gPR - softF, gPR + softF, dF)) * smoothstep(0.5, 1.5, gPR);

                float edgeDist = min(min(fc.x - (gXL + gPR), (gXR - gPR) - fc.x), (gYB - gPR) - fc.y);
                ao *= mix(0.76, 1.0, smoothstep(0.0, max(2.2 * gPR, 1.0), edgeDist));
            }
            ao *= mix(0.78, 1.0, smoothstep(0.0, 12.0 + 0.25 * bladeH, fc.y - gYG));
            float sunGrad = 1.0 + 0.05 * dot(cUV - 0.5, normalize(vec2(-0.45, 0.8)));
            vec3 shadowMul = mix(vec3(1.0), vec3(0.40, 0.44, 0.52), sh * ShadowStrength);
            vec3 lightMul = shadowMul * mix(1.0, sunGrad, 1.0 - sh * ShadowStrength * 0.7) * ao;
            vec3 imgLit = clamp(img.rgb * lightMul + dz, 0.0, 1.0);

            if (SwTransparentImage)
            {
                vec3 combined = mix(bgFill, imgLit, img.a);
                base = over(vec4(combined * InnerOpacity, InnerOpacity) * inner, bg);
            }
            else
            {
                float ia = img.a * InnerOpacity;
                base = over(vec4(imgLit * ia, ia) * inner, bg);
            }
        }
        else
        {
            base = over(vec4(bgFill * InnerOpacity, InnerOpacity) * inner, bg);
        }
    }

    // ---- foreground ground strip in front of the goal line
    if (needFgTurf)
    {
        vec3 gc = turfCol * postAO;
        float ta = hm * smoothstep(yRoot - 8.0, yRoot + 2.0, fc.y) * smoothstep(gYG + 1.5, gYG + 0.5, fc.y);
        acc = over(vec4(gc * ta, ta), acc);
    }

    // ---- front posts + crossbar: brushed aluminium
    if (hitPost)
    {
        vec4 tb = postCol * clamp(fc.y - (gYG - 1.5), 0.0, 1.0);
        acc = over(tb, acc);
    }

    // ---- short grass blades in front, fading out before the screen edges
    if (fc.y < H + 2.0 && fc.y > yRoot - 4.0 && hm > 0.0)
    {
        vec2 q = fc - vec2(0.0, yRoot);
        float bf = hm * smoothstep(yRoot - 3.0, yRoot + 3.0, fc.y);
        acc = over(grassLayer(q + vec2(3.7, 2.0), 1.0, bladeH * 0.8, t + 0.7, wind) * bf, acc);
        acc = over(grassLayer(q, 0.0, bladeH, t, wind) * bf, acc);
    }

    // outermost pixels transparent
    vec2 ed = min(fc, res - fc);
    acc *= smoothstep(0.5, 10.0, min(ed.x, ed.y));

    // tone map (soft shoulder), dither
    float a = clamp(acc.a, 0.0, 1.0);
    vec3 col = acc.rgb / (1.0 + 0.3 * max(acc.rgb - 0.75, 0.0));
    col += dz;
    a = clamp(a + dz, 0.0, 1.0);
    col = clamp(min(col, vec3(a)), 0.0, 1.0);
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
