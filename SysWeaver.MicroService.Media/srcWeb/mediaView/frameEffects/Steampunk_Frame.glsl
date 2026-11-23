//  media: image none
// =============================================================================
//  Steampunk Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the pipe center line from the screen edges | 0.2 | 24 .. 160 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 160 | {0} px

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Flow speed of the liquid and wheel turning", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.3, 0.0886363636, 0.0886363636); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerBackground = vec3(0.0172727273, 0.1, 0.065875); // var: { "type": "col", "name": "Inner Background", "desc": "Background color inside the pipe frame (shown by Inner Opacity)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the background inside the pipe frame", "scale": 100, "unit": "%" }
const vec3 CopperColor = vec3(0.78, 0.38, 0.2); // var: { "type": "col", "name": "Copper", "desc": "Color of the copper pipes" }
const float PipeWave = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Pipe Wave", "desc": "How strongly the pipes bend", "unit": "x" }
const vec3 ValveColor = vec3(0.65, 0.04, 0.03); // var: { "type": "col", "name": "Valve Color", "desc": "Paint of the valve wheels" }
const vec3 LiquidColor = vec3(0.25, 1.0, 0.2); // var: { "type": "col", "name": "Liquid", "desc": "Glowing liquid in the glass tube" }
const float Rust = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Rust", "desc": "Amount of rust and chipped paint", "scale": 100, "unit": "%" }
const float SteamAmount = 0.8; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Steam", "desc": "Density of the steam puffs", "scale": 100, "unit": "%" }
const float Glow = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Glow", "desc": "Brightness of the liquid", "unit": "x" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

#define Inset mix(24.0, 160.0, iMouse.x)
#define CornerRadius mix(0.0, 160.0, iMouse.y)
#define ZERO min(iFrame, 0)

flat varying vec4 vFrame0;     // I, rad, hs.x, hs.y
flat varying vec4 vFrame1;     // t, ts, P, open
flat varying vec4 vValvesH;    // bTop.xy, bBot.xy
flat varying vec4 vValvesV;    // bLeft.xy, bRight.xy
flat varying vec4 vGaugeVent;  // gc.xy, vtc.xy
flat varying vec4 vGaugeData;  // nd.xy, sin(lang), cos(lang)
flat varying vec4 vBayM2;      // lamp(-2).xy, flk(-2), fireT(-2)
flat varying vec4 vBayM1;      // lamp(-1).xy, flk(-1), fireT(-1)
flat varying vec4 vBay0;       // lamp(0).xy,  flk(0),  fireT(0)
flat varying vec4 vBayP1;      // lamp(1).xy,  flk(1),  fireT(1)
flat varying vec4 vBayP2;      // lamp(2).xy,  flk(2),  fireT(2)
flat varying vec4 vColors;     // ambTint.xyz, facInvH

float sat(float x) { return clamp(x, 0.0, 1.0); }

// non-unrolled fbm matching EwFbm2D bit-for-bit without compile-time unrolling bloat
float fbm2(vec2 p, int octaves)
{
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0, amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * EwValueNoise2D(p);
        p = m * p;
        amp *= 0.5;
    }
    return sum;
}

// arc length position along a rounded box outline (clockwise from top center), total = 4 * quarter
float perimPos(vec2 p, vec2 hs, float r, out float quarter)
{
    vec2 b = hs - r;
    float arc = 1.5707963 * r;
    quarter = b.x + arc + b.y;
    vec2 a = abs(p);
    float u;
    if (a.x < b.x) u = a.x;
    else if (a.y < b.y) u = b.x + arc + (b.y - a.y);
    else u = b.x + atan(a.x - b.x, a.y - b.y) * r;
    if (p.x >= 0.0) return p.y >= 0.0 ? u : 2.0 * quarter - u;
    return p.y < 0.0 ? 2.0 * quarter + u : 4.0 * quarter - u;
}

// outward direction of the rounded box (gradient of the sdf)
vec2 boxGrad(vec2 p, vec2 hs, float r)
{
    vec2 sg = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
    vec2 q = abs(p) - (hs - r);
    vec2 g = (q.x > 0.0 && q.y > 0.0) ? normalize(q) : (q.x > q.y ? vec2(1.0, 0.0) : vec2(0.0, 1.0));
    return g * sg;
}

const vec3 LDir  = vec3(-0.5, 0.65, 0.58);
const vec3 LNorm = vec3(-0.49776253, 0.64709129, 0.57740454); // normalize(LDir)
const vec3 LHalf = vec3(-0.27973995, 0.36366193, 0.88649272); // normalize(LNorm + (0,0,1))
const vec3 FNorm = vec3(0.68376346, -0.56980288, 0.45584231); // normalize(vec3(0.6, -0.5, 0.4))

// metal / paint shading (linear): warm key, cool fill, rim, env
vec3 shade(vec3 N, vec3 alb, float metal, float rough)
{
    float dif = sat(dot(N, LNorm));
    float ndh = sat(dot(N, LHalf));
    float sp = pow(ndh, mix(120.0, 8.0, rough));
    vec3 spc = mix(vec3(0.04), alb, metal);
    float fil = sat(dot(N, FNorm));
    float r1 = 1.0 - sat(N.z);
    float rim = r1 * r1 * r1;
    vec3 env = mix(vec3(0.005, 0.004, 0.003), vec3(0.3, 0.22, 0.15), smoothstep(-0.1, 0.95, N.y));
    const vec3 key = vec3(0.85, 0.68, 0.493);
    vec3 c = alb * 0.02;
    c += alb * dif * key * (1.0 - 0.75 * metal);
    c += spc * pow(ndh, mix(14.0, 3.0, rough)) * key * 0.35 * metal;
    c += alb * fil * vec3(0.12, 0.16, 0.24) * (1.0 - 0.5 * metal);
    c += spc * sp * key * 3.0 * (1.0 - 0.75 * rough);
    c += metal * alb * env * (1.0 - 0.8 * rough);
    c += spc * rim * vec3(0.125, 0.15, 0.2) * smoothstep(-0.2, 0.3, -N.x);
    return c;
}

vec3 tubeN(vec2 g, float y, vec2 t, float bev)
{
    y = clamp(y, -1.0, 1.0);
    return normalize(vec3(g * y + t * bev, sqrt(max(1.0 - y * y, 0.0)) + 0.05));
}

vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// vent cycle (8 s at Speed 1): pressure builds, vent opens, pressure drops
float ventOpen(float x)
{
    float ph = fract(x / 8.0);
    return smoothstep(0.70, 0.75, ph) * (1.0 - smoothstep(0.90, 0.95, ph));
}
float pressure(float x)
{
    float ph = fract(x / 8.0);
    float pr = ph < 0.72 ? mix(0.2, 0.9, ph / 0.72) : mix(0.9, 0.2, smoothstep(0.72, 0.93, ph));
    return pr + 0.025 * sin(x * 23.0) * sin(x * 7.3) + 0.05 * smoothstep(0.6, 0.72, ph) * sin(x * 41.0);
}

// wavy pipe offset along the loop (integer wave counts: seamless)
float wav(float s, float P, float I, float amp, float ph)
{
    float n = max(floor(P / (I * 8.0)), 3.0);
    float k = 6.2831853 * s / P;
    return amp * I * (0.7 * sin(n * k + ph) + 0.3 * sin((2.0 * n + 1.0) * k + 2.0 * ph));
}

// steam plume, q in inset units; mode 0: periodic pulse, 1: vent bursts (time ts, lag along the plume)
float steam(vec2 q, vec2 dir, float seed, float t, float ts, float mode, float len)
{
    float a = dot(q, dir);
    float c = dot(q, vec2(-dir.y, dir.x));
    if (a < -0.3 || a > len) return 0.0;
    float w = 0.2 + 0.4 * max(a, 0.0);
    if (abs(c) > 2.5 * w) return 0.0;
    float pulse;
    if (mode < 0.5) {
        float gate = smoothstep(0.3, 0.8, sin(t * 0.4 + seed * 3.1) * 0.5 + 0.5);
        if (gate <= 0.001) return 0.0;
        float puff = smoothstep(0.1, 0.9, sin(a * 1.8 - t * 2.2 + seed) * 0.5 + 0.5);
        pulse = (0.3 + 0.7 * puff) * gate;
    } else {
        pulse = 3.0 * ventOpen(ts - a * 0.35);
        if (pulse <= 0.001) return 0.0;
    }
    c += 0.3 * a * sin(a * 1.3 - t * 0.9 + seed);
    float env = exp(-c * c / (w * w)) * smoothstep(-0.3, 0.3, a) * exp(-0.5 * a) * smoothstep(len, len * 0.6, a);
    float n = fbm2(vec2(c * 1.6, a * 1.4 - t * 1.8) + seed * 7.3, 4);
    return sat(env * smoothstep(0.25, 0.75, n) * pulse * 1.7);
}
// =============================================================================
//  Dark Factory Inner Background
// =============================================================================

float sdGear(vec2 q, float r, float teeth, float toothH, float rimW, float hubR, float spokes, float ang)
{
    float rMax = r + toothH + 0.02;
    float len2 = dot(q, q);
    if (len2 > rMax * rMax) return 1e5;
    float len = sqrt(len2);
    float a = atan(q.y, q.x) + ang;
    float sec = 6.2831853 / teeth;
    float la = a - floor(a / sec + 0.5) * sec;
    vec2 tp = vec2(cos(la), abs(sin(la))) * len;
    float tw = sec * r * 0.24;
    float dTooth = EwSdBox2D(tp - vec2(r + toothH * 0.35, 0.0), vec2(toothH * 0.65, tw)) - toothH * 0.10;
    float dOuter = min(len - r, dTooth);
    float dRing = max(dOuter, (r - rimW) - len);
    float sSec = 6.2831853 / spokes;
    float sa = a - floor(a / sSec + 0.5) * sSec;
    vec2 sp = vec2(cos(sa), abs(sin(sa))) * len;
    float spokeW = max(rimW * 0.44, 0.0045);
    float dSpoke = EwSdBox2D(sp - vec2((r - rimW * 0.5) * 0.5, 0.0), vec2((r - rimW * 0.5) * 0.5, spokeW));
    float dBody = min(min(dRing, len - hubR), dSpoke);
    return max(dBody, hubR * 0.34 - len);
}

// lightweight spoked flywheel (no gear teeth) for cranks & pulleys
float sdFlywheel(vec2 q, float r, float rimW, float hubR, float spokes, float ang)
{
    float rMax = r + 0.02;
    float len2 = dot(q, q);
    if (len2 > rMax * rMax) return 1e5;
    float len = sqrt(len2);
    float dRing = abs(len - (r - rimW * 0.5)) - rimW * 0.5;
    float a = atan(q.y, q.x) + ang;
    float sSec = 6.2831853 / spokes;
    float sa = a - floor(a / sSec + 0.5) * sSec;
    vec2 sp = vec2(cos(sa), abs(sin(sa))) * len;
    float dSpoke = EwSdBox2D(sp - vec2((r - rimW * 0.5) * 0.5, 0.0), vec2((r - rimW * 0.5) * 0.5, rimW * 0.42));
    return max(min(min(dRing, len - hubR), dSpoke), hubR * 0.34 - len);
}

// exact meshing angle for gear B (n2 teeth) at world angle theta from gear A (n1 teeth, angA)
float meshAngle(float angA, float theta, float n1, float n2)
{
    return (3.14159265 - (theta + angA) * n1) / n2 - (theta + 3.14159265);
}

float sdTruss(vec2 q, float halfH, float pitch, float barW)
{
    if (abs(q.y) > halfH + barW + 0.035) return abs(q.y) - (halfH + barW);
    float dChords = min(abs(q.y - halfH), abs(q.y + halfH)) - barW;
    float cx = mod(q.x, pitch) - 0.5 * pitch;
    vec2 ac = abs(vec2(cx, q.y));
    float dDiag = EwSdSegment2D(ac, vec2(0.0), vec2(0.5 * pitch, halfH)) - barW * 0.62;
    float dVert = max(abs(ac.x - 0.5 * pitch) - barW * 0.72, ac.y - halfH);
    return min(dChords, min(dDiag, dVert));
}

float sdExhaustFan(vec2 q, float r, float blades, float ang)
{
    float len = length(q);
    if (len > r + 0.04) return len - r;
    float dRim = abs(len - r) - r * 0.11;
    float dHub = len - r * 0.25;
    float a = atan(q.y, q.x) + ang;
    float sec = 6.2831853 / blades;
    float la = a + len * 2.5;
    la = la - floor(la / sec + 0.5) * sec;
    float bw = mix(0.28, 0.18, len / max(r, 1e-4)) * sec * len;
    float dBlade = max(abs(sin(la) * len) - max(bw, 0.005), len - r * 0.94);
    return min(min(dRim, dHub), dBlade);
}

// vertical slider-crank steam piston centered at crank origin q = (0,0) (flywheel evaluated separately)
float sdPiston(vec2 q, float ang, float sc)
{
    if (abs(q.x) > 0.12 * sc || q.y < -0.11 * sc || q.y > 0.39 * sc) return 1.0;
    float rCrank = 0.050 * sc;
    float rodLen = 0.140 * sc;
    vec2 pin = vec2(cos(ang), sin(ang)) * rCrank;
    float py = pin.y + sqrt(max(rodLen * rodLen - pin.x * pin.x, 1e-5));
    float dArm = EwSdSegment2D(q, vec2(0.0), pin) - 0.0085 * sc;
    float dRod = EwSdSegment2D(q, pin, vec2(0.0, py)) - 0.008 * sc;
    float dHead = EwSdBox2D(q - vec2(0.0, py), vec2(0.018, 0.014) * sc) - 0.003 * sc;
    float dPRod = EwSdSegment2D(q, vec2(0.0, py), vec2(0.0, py + 0.10 * sc)) - 0.0065 * sc;
    float dGuides = max(abs(abs(q.x) - 0.030 * sc) - 0.0055 * sc, abs(q.y - 0.112 * sc) - 0.074 * sc);
    float dCyl = EwSdBox2D(q - vec2(0.0, 0.252 * sc), vec2(0.044, 0.068) * sc) - 0.004 * sc;
    float dCapT = EwSdBox2D(q - vec2(0.0, 0.324 * sc), vec2(0.056, 0.011) * sc) - 0.002 * sc;
    float dCapB = EwSdBox2D(q - vec2(0.0, 0.182 * sc), vec2(0.056, 0.010) * sc) - 0.002 * sc;
    float dNoz = EwSdBox2D(q - vec2(0.052 * sc, 0.285 * sc), vec2(0.016, 0.008) * sc);
    float d = min(dArm, min(dRod, min(dHead, dPRod)));
    return min(d, min(dGuides, min(min(dCyl, dNoz), min(dCapT, dCapB))));
}

// oscillating Victorian walking-beam steam engine centered at fulcrum base q = (0,0) (crank wheel evaluated separately)
float sdBeamEngine(vec2 q, float ang, float sc)
{
    if (abs(q.x) > 0.21 * sc || q.y < -0.26 * sc || q.y > 0.20 * sc) return 1.0;
    float rock = 0.23 * sin(ang);
    vec2 piv = vec2(0.0, 0.065 * sc);
    // central A-frame fulcrum support + bearing
    float dLegL = EwSdSegment2D(q, vec2(-0.042 * sc, -0.22 * sc), piv) - 0.010 * sc;
    float dLegR = EwSdSegment2D(q, vec2(0.042 * sc, -0.22 * sc), piv) - 0.010 * sc;
    float dCross = EwSdSegment2D(q, vec2(-0.025 * sc, -0.08 * sc), vec2(0.025 * sc, -0.08 * sc)) - 0.007 * sc;
    float dPiv = length(q - piv) - 0.020 * sc;
    // heavy tapered rocking beam
    mat2 rNeg = EwRotate2D(-rock);
    vec2 bq = rNeg * (q - piv);
    float halfW = mix(0.018, 0.008, clamp(abs(bq.x) / (0.125 * sc), 0.0, 1.0)) * sc;
    float dBeam = max(abs(bq.x) - 0.128 * sc, abs(bq.y) - halfW) - 0.003 * sc;
    // left pump rod & stationary pump cylinder
    vec2 beamDir = vec2(rNeg[0][0], -rNeg[0][1]) * (0.120 * sc); // EwRotate2D(rock) * (0.120*sc, 0)
    vec2 pL = piv - beamDir;
    float dPumpRod = EwSdSegment2D(q, pL, vec2(-0.120 * sc, -0.15 * sc)) - 0.0065 * sc;
    float dPumpCyl = EwSdBox2D(q - vec2(-0.120 * sc, -0.185 * sc), vec2(0.032, 0.052) * sc) - 0.004 * sc;
    // right connecting rod
    vec2 pR = piv + beamDir;
    vec2 cCrank = vec2(0.118 * sc, -0.135 * sc);
    vec2 pin = cCrank + vec2(cos(ang), sin(ang)) * (0.038 * sc);
    float dConn = EwSdSegment2D(q, pR, pin) - 0.007 * sc;
    float d = min(min(dLegL, dLegR), min(dCross, dPiv));
    d = min(d, min(dBeam, min(dPumpRod, dPumpCyl)));
    return min(d, dConn);
}

// organic electrical flickering for industrial tungsten bulbs
float bulbFlicker(float ts, float seed)
{
    float h = EwHash1To1(seed * 13.7 + 2.1);
    float hum = 0.93 + 0.07 * sin(ts * (1.7 + h * 1.9) + seed * 5.0);
    float winN = EwValueNoise2D(vec2(ts * (0.42 + 0.38 * h) + seed * 11.3, seed * 3.7));
    float thresh = mix(0.68, 0.42, step(0.40, h));
    float inWin = smoothstep(thresh, thresh + 0.13, winN);
    float f1 = EwHash1To1(floor(ts * 21.0 + seed * 41.0) + seed * 7.0);
    float f2 = EwHash1To1(floor(ts * 46.0 + seed * 19.0) + seed * 13.0);
    float sputter = mix(1.0, 0.14 + 0.90 * step(0.32, f1) * (0.45 + 0.55 * f2), inWin);
    float dipN = EwValueNoise2D(vec2(ts * 0.21 + seed * 17.0, seed * 9.1));
    float dip = 1.0 - 0.55 * smoothstep(0.76, 0.90, dipN) * step(0.32, h);
    return clamp(hum * sputter * dip, 0.08, 1.18);
}

vec4 evalBayData(float id, float ts)
{
    vec3 lh = EwHash2To3(vec2(id, 19.3));
    float sway = 0.005 * sin(ts * (0.65 + 0.35 * lh.y) + id * 3.7);
    vec2 lp = vec2(id * 0.56 - 0.04 + (lh.x - 0.5) * 0.12 + sway, 0.175 + 0.095 * lh.y);
    float flk = bulbFlicker(ts, id + 3.1);
    float fireT = 0.76 + 0.24 * EwValueNoise2D(vec2(ts * 2.8 + id * 5.3, id * 2.1))
                + 0.10 * sin(ts * 8.1 + id * 3.7);
    return vec4(lp, flk, fireT);
}

vec3 getLamp(float lid, float ts)
{
    if (lid > -2.5 && lid < 2.5) {
        if (lid < -1.5) return vBayM2.xyz;
        if (lid < -0.5) return vBayM1.xyz;
        if (lid <  0.5) return vBay0.xyz;
        if (lid <  1.5) return vBayP1.xyz;
        return vBayP2.xyz;
    }
    vec3 lh = EwHash2To3(vec2(lid, 19.3));
    float sway = 0.005 * sin(ts * (0.65 + 0.35 * lh.y) + lid * 3.7);
    vec2 lp = vec2(lid * 0.56 - 0.04 + (lh.x - 0.5) * 0.12 + sway, 0.175 + 0.095 * lh.y);
    return vec3(lp, bulbFlicker(ts, lid + 3.1));
}

float getFireT(float id, float ts)
{
    if (id > -2.5 && id < 2.5) {
        if (id < -1.5) return vBayM2.w;
        if (id < -0.5) return vBayM1.w;
        if (id <  0.5) return vBay0.w;
        if (id <  1.5) return vBayP1.w;
        return vBayP2.w;
    }
    return 0.76 + 0.24 * EwValueNoise2D(vec2(ts * 2.8 + id * 5.3, id * 2.1))
         + 0.10 * sin(ts * 8.1 + id * 3.7);
}

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    float I = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - I;
    if (SwHaveImage) {
        vec2 imgRes = iChannelResolution[0].xy;
        if (imgRes.x > 0.5 && imgRes.y > 0.5) {
            float imgAspect = imgRes.x / imgRes.y;
            vec2 halfRes = 0.5 * res;
            float maxI = imgAspect >= 1.0
                ? min(halfRes.y * 0.5, halfRes.x / (imgAspect + 1.0))
                : min(halfRes.x * 0.5, halfRes.y * imgAspect / (1.0 + imgAspect));
            I = min(Inset, maxI);
            vec2 maxHs = halfRes - I;
            if (imgAspect > maxHs.x / max(maxHs.y, 1.0)) {
                hs = vec2(maxHs.x, maxHs.x / imgAspect);
            } else {
                hs = vec2(maxHs.y * imgAspect, maxHs.y);
            }
        }
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float t = mod(iTime, 3600.0);
    float ts = mod(t * Speed, 2880.0);
    vec2 b = hs - rad;
    float P = 4.0 * (b.x + 1.5707963 * rad + b.y);
    float open = ventOpen(ts);

    vFrame0 = vec4(I, rad, hs);
    vFrame1 = vec4(t, ts, P, open);

    float straightX = max(hs.x - rad, 0.0), straightY = max(hs.y - rad, 0.0);
    float Qd;
    vec3 h0 = EwHash2To3(vec2(0.0, 5.3));
    vec2 bTop = vec2((h0.x - 0.5) * 0.5 * straightX, hs.y);
    bTop.y += sign(bTop.y) * wav(perimPos(bTop, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);

    vec3 h1 = EwHash2To3(vec2(1.0, 5.3));
    vec2 bBot = vec2((h1.x - 0.5) * 0.5 * straightX - 0.1 * straightX, -hs.y);
    bBot.y += sign(bBot.y) * wav(perimPos(bBot, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);

    vec3 h2 = EwHash2To3(vec2(2.0, 5.3));
    vec2 bLeft = vec2(-hs.x, (h2.x - 0.5) * 0.9 * straightY);
    bLeft.x += sign(bLeft.x) * wav(perimPos(bLeft, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);

    vec3 h3 = EwHash2To3(vec2(3.0, 5.3));
    vec2 bRight = vec2(hs.x, (h3.x - 0.5) * 0.9 * straightY);
    bRight.x += sign(bRight.x) * wav(perimPos(bRight, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);

    vValvesH = vec4(bTop, bBot);
    vValvesV = vec4(bLeft, bRight);

    vec2 gc = vec2(max(min(0.5 * hs.x, straightX - 0.7 * I), 0.0), hs.y);
    gc.y += wav(perimPos(gc, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);
    vec2 vtc = vec2(-max(min(0.55 * hs.x, straightX - 0.8 * I), 0.0), -hs.y);
    vtc.y -= wav(perimPos(vtc, hs, rad, Qd), P, I, 0.11 * PipeWave, 0.0);
    vGaugeVent = vec4(gc, vtc);

    float pr = pressure(ts);
    float na = mix(-2.356, 2.356, clamp(pr, 0.0, 1.05));
    float lang = open * 1.25;
    vGaugeData = vec4(sin(na), cos(na), sin(lang), cos(lang));

    vBayM2 = evalBayData(-2.0, ts);
    vBayM1 = evalBayData(-1.0, ts);
    vBay0  = evalBayData( 0.0, ts);
    vBayP1 = evalBayData( 1.0, ts);
    vBayP2 = evalBayData( 2.0, ts);

    vec3 ibLin = pow(clamp(InnerBackground, 0.0, 1.0), vec3(2.2));
    ibLin = ibLin / (1.0 + 4.0 * ibLin);
    vec3 ambTint = mix(vec3(0.0045, 0.0068, 0.0085), ibLin * 1.7 + vec3(0.0018, 0.0028, 0.0036), 0.78);
    vec2 facRes = SwHaveImage ? 2.0 * (hs + I) : res;
    vColors = vec4(ambTint, 1.0 / max(facRes.y, 1.0));
}

// dark steampunk factory background (returns display-space RGB in [0,1])
// dark steampunk factory background (returns display-space RGB in [0,1])
vec3 factoryBg(vec2 p, float ts, float dPipe, float I)
{
    float invH = vColors.w;
    vec2 uv = p * invH;
    float px = invH;

    // deep moody linear ambient tint precomputed in vertexMain
    vec3 ambTint = vColors.xyz;
    vec3 liqLin = pow(clamp(LiquidColor, 0.0, 1.0), vec3(2.2));

    // shared curling steam / haze fields (domain-warped for organic billows)
    float mistFar = fbm2(uv * vec2(2.3, 2.9) + vec2(-ts * 0.045, -ts * 0.085) + 4.1, 3);
    float mistMid = fbm2(uv * vec2(3.8, 4.5) + vec2(ts * 0.042, -ts * 0.155) + vec2(mistFar * 0.85, 1.7), 4);
    float wisp = smoothstep(0.28, 0.78, mistMid);
    float steamFac = 0.22 + 0.78 * SteamAmount;

    // ----- Layer 0: far factory hall, varied arched/porthole windows, furnaces & turbine duct -----
    float bayW = 0.64;
    float bayId = floor(uv.x / bayW + 0.5);
    float bx = uv.x - bayId * bayW;
    float bType = mod(bayId, 3.0);  // 0: center foundry, 1: right beam-engine, 2: left turbine
    vec3 bh = EwHash2To3(vec2(bayId, 8.4));

    // dark ribbed factory wall with subtle vertical depth gradient
    float rib = 0.84 + 0.16 * sin(uv.x * 165.0);
    vec3 col = ambTint * (0.24 + 0.14 * mistFar) * rib * (0.55 + 0.45 * smoothstep(-0.50, 0.35, uv.y));

    // bay-specific tall industrial windows (cathedral arch / twin narrow arches / circular rose vent)
    float winOpen = 0.0;
    if (uv.y > 0.01 && uv.y < 0.35) {
        if (bType < 0.5) {
            // Bay 0: tall cathedral arched window
            vec2 wq = vec2(bx, uv.y - 0.175);
            float dWin = EwSdRoundedBox2D(wq, vec2(0.115, 0.145), 0.105);
            if (dWin < 0.01) {
                float m = smoothstep(px * 1.5, -px * 1.5, dWin);
                float barV = abs(fract((bx + 0.115) / 0.0575 + 0.5) - 0.5) * 0.0575;
                float barH = abs(fract((uv.y - 0.05) / 0.068 + 0.5) - 0.5) * 0.068;
                float mullions = smoothstep(0.0045, 0.0075, min(barV, barH));
                winOpen = m * mullions;
            }
        } else if (bType < 1.5) {
            // Bay +1: circular industrial bull's-eye / rose window
            vec2 wq = vec2(bx + 0.04, uv.y - 0.185);
            float rW = length(wq);
            if (rW < 0.115) {
                float m = smoothstep(0.105 + px, 0.105 - px, rW);
                float a = atan(wq.y, wq.x);
                float spokes = smoothstep(0.005, 0.008, abs(sin(a * 4.0)) * rW);
                float ring = smoothstep(0.005, 0.008, abs(rW - 0.055));
                winOpen = m * spokes * ring;
            }
        } else {
            // Bay -1: twin tall narrow factory clerestory windows
            vec2 wq = vec2(abs(bx - 0.03) - 0.072, uv.y - 0.18);
            float dWin = EwSdRoundedBox2D(wq, vec2(0.048, 0.135), 0.045);
            if (dWin < 0.01) {
                float m = smoothstep(px * 1.5, -px * 1.5, dWin);
                float barH = abs(fract((uv.y - 0.06) / 0.064 + 0.5) - 0.5) * 0.064;
                float barV = abs(wq.x);
                float mullions = smoothstep(0.004, 0.007, min(barV, barH));
                winOpen = m * mullions;
            }
        }
    }
    if (winOpen > 0.001) {
        float grime = 0.48 + 0.52 * mistFar;
        vec3 winLight = (ambTint * 2.8 + vec3(0.008, 0.016, 0.022)) * (0.55 + 0.55 * smoothstep(0.03, 0.34, uv.y)) * grime;
        col = mix(col, winLight, winOpen * 0.92);
    }

    // faint diagonal moonlit / industrial haze shafts from the upper windows
    {
        float sx = uv.x - (uv.y - 0.24) * 0.50;
        float sbx = sx - floor(sx / bayW + 0.5) * bayW;
        float shaft = smoothstep(0.15, 0.01, abs(sbx)) * smoothstep(0.38, 0.15, uv.y) * smoothstep(-0.42, 0.12, uv.y);
        col += (ambTint * 0.85 + vec3(0.004, 0.008, 0.011)) * shaft * (0.35 + 0.85 * mistFar) * 0.50 * steamFac;
    }

    // furnace fireboxes & backlit spinning turbine duct:
    // any 3 consecutive bays {bayId-1, bayId, bayId+1} contain exactly one bay of each type (0, 1, 2)
    float id0 = floor((bayId + 1.0) / 3.0) * 3.0;
    float id1 = floor(bayId / 3.0) * 3.0 + 1.0;
    float id2 = floor((bayId - 1.0) / 3.0) * 3.0 + 2.0;
    float fireT0 = getFireT(id0, ts);
    float fireT1 = getFireT(id1, ts);
    float fireT2 = getFireT(id2, ts);

    vec2 fq0 = vec2(uv.x - id0 * bayW + 0.01, uv.y + 0.275);
    vec2 fq1 = vec2(uv.x - id1 * bayW - 0.04, uv.y + 0.295);
    vec2 fq2 = vec2(uv.x - id2 * bayW - 0.02, uv.y + 0.085);
    float fl2 = length(fq2);

    vec3 furnaceGlow = vec3(1.0, 0.24, 0.03) * exp(-length(fq0 * vec2(0.85, 1.15)) * 6.8) * 0.11 * fireT0
                     + vec3(1.0, 0.26, 0.035) * exp(-length(fq1 * vec2(0.85, 1.3)) * 7.5) * 0.075 * fireT1
                     + vec3(0.92, 0.28, 0.045) * exp(-fl2 * 7.2) * 0.065 * fireT2;

    // only the current bay can hit firebox / boiler / fan geometry (compiled once, no 3x unroll!)
    if (bType < 0.5) {
        if (abs(fq0.x) < 0.11 && abs(fq0.y) < 0.09) {
            float dArch = EwSdRoundedBox2D(fq0, vec2(0.095, 0.072), 0.042);
            if (dArch < 0.012) {
                float fm = smoothstep(px * 1.5, -px * 1.5, dArch);
                float frame = smoothstep(-0.009, -0.013, dArch);
                float barX = smoothstep(0.0045, 0.0075, abs(fract(fq0.x / 0.025 + 0.5) - 0.5) * 0.025);
                float barY = smoothstep(0.0038, 0.0065, abs(fq0.y + 0.005));
                float grate = barX * barY * frame;
                float heatY = smoothstep(0.068, -0.062, fq0.y);
                float coal = heatY * (0.60 + 0.65 * mistMid);
                vec3 ember = mix(vec3(0.32, 0.025, 0.004), vec3(1.25, 0.42, 0.045), sat(coal)) * fireT0;
                float hy = sat(heatY);
                ember += vec3(0.65, 0.32, 0.04) * (hy * hy * hy) * fireT0;
                col = mix(col, ember * grate + vec3(0.006, 0.003, 0.002) * (1.0 - grate), fm);
            }
        }
    } else if (bType < 1.5) {
        if (abs(fq1.x) < 0.095 && abs(fq1.y) < 0.045) {
            float dSlit = EwSdRoundedBox2D(fq1, vec2(0.085, 0.034), 0.010);
            if (dSlit < 0.008) {
                float fm = smoothstep(px * 1.5, -px * 1.5, dSlit);
                float bars = smoothstep(0.004, 0.007, abs(fract(fq1.x / 0.022 + 0.5) - 0.5) * 0.022);
                vec3 ember = vec3(0.95, 0.24, 0.03) * (0.6 + 0.6 * mistMid) * fireT1;
                col = mix(col, ember * bars + vec3(0.005, 0.002, 0.001) * (1.0 - bars), fm);
            }
        }
    } else {
        float rFan = 0.098;
        if (fl2 < rFan + 0.025) {
            float fm = smoothstep(rFan + px, rFan - px, fl2);
            vec3 ductLight = mix(vec3(0.75, 0.24, 0.04), ambTint * 3.0 + vec3(0.03, 0.06, 0.05), 0.22)
                           * (0.45 + 0.70 * (1.0 - fl2 / rFan)) * fireT2 * 0.42;
            float dFan = sdExhaustFan(fq2, rFan, 6.0, ts * 0.72 + id2 * 1.9);
            float fanMask = smoothstep(px * 1.2, -px * 1.2, dFan);
            col = mix(col, mix(ductLight, ambTint * 0.08, fanMask), fm);
        }
    }
    // subtle continuous foundry hearth warmth along very bottom floor
    furnaceGlow += vec3(0.85, 0.20, 0.025) * exp(-max(uv.y + 0.44, 0.0) * 7.5) * (0.55 + 0.45 * mistFar) * 0.055;
    col += furnaceGlow * 0.55;

    // ----- Layer 1: far architectural & machinery silhouettes (truss, catwalk, giant slow gears, boilers) -----
    float dFar = 1e5;
    // distant upper truss, catwalk deck + handrail, overhead pipes
    dFar = min(dFar, sdTruss(vec2(uv.x, uv.y - 0.345), 0.030, 0.082, 0.0042));
    dFar = min(dFar, abs(uv.y + 0.095) - 0.0055);
    dFar = min(dFar, abs(uv.y + 0.048) - 0.0020);
    dFar = min(dFar, max(abs(mod(uv.x, 0.072) - 0.036) - 0.0020, abs(uv.y + 0.072) - 0.024));
    dFar = min(dFar, abs(uv.y - 0.258) - 0.0075);
    dFar = min(dFar, abs(uv.y + 0.222) - 0.010);
    // distant giant slow flywheel + vertical boiler tank + swaying hoist chain (per bay, bounded inside bay)
    {
        float gDir = bh.x > 0.5 ? 1.0 : -1.0;
        vec2 gp = vec2(bx - (bh.x - 0.5) * 0.14, uv.y - mix(-0.12, 0.08, bh.y));
        dFar = min(dFar, sdFlywheel(gp, 0.19 + 0.04 * bh.z, 0.022, 0.038, 6.0, ts * 0.14 * gDir + bayId * 1.7));
        vec2 bp = vec2(bx + (bh.y > 0.5 ? -0.22 : 0.22), uv.y + 0.12);
        dFar = min(dFar, EwSdRoundedBox2D(bp, vec2(0.038, 0.23), 0.034));
        // swaying vertical chain & slowly moving counterweight
        float sway = sin(ts * 0.52 + bayId * 3.1) * 0.008 * smoothstep(0.45, -0.35, uv.y);
        float cx = bx + (bType < 0.5 ? -0.15 : (bType < 1.5 ? 0.08 : 0.18)) + sway;
        float hoistY = 0.05 + 0.12 * sin(ts * (0.26 + 0.10 * bh.x) + bayId * 2.4);
        dFar = min(dFar, max(abs(cx) - 0.0024, hoistY - uv.y));
        dFar = min(dFar, EwSdBox2D(vec2(cx, uv.y - hoistY), vec2(0.013, 0.022)) - 0.003);
    }
    float mFar = smoothstep(px * 1.6, -px * 1.6, dFar);
    col = mix(col, ambTint * 0.15 + furnaceGlow * 0.08, mFar * 0.90);

    // distant mist veil separating far & mid planes
    float fog1 = (0.06 + 0.22 * smoothstep(0.25, 0.78, mistFar)) * steamFac;
    col = mix(col, ambTint * 0.55 + furnaceGlow * 0.42, sat(fog1 * 0.48));

    // ----- Layer 2: mid-ground complex animated machinery (distinct per bay archetype!) -----
    float dMach = 1e5;
    vec3 pilotGlow = vec3(0.0);
    float pistonSteam = 0.0;

    // heavy vertical structural factory I-beam columns separating bays (tucks all bay machinery cleanly!)
    float dCol = abs(abs(bx) - bayW * 0.5) - 0.017;
    float dFlange = max(abs(abs(bx) - bayW * 0.5) - 0.024, abs(fract(uv.y * 3.5 + 0.2) - 0.5) - 0.032);
    dMach = min(dMach, min(dCol, dFlange));

    // overhead heavy foreground steel truss & lower floor steam main
    dMach = min(dMach, sdTruss(vec2(uv.x, uv.y - 0.412), 0.036, 0.105, 0.0055));
    dMach = min(dMach, abs(uv.y + 0.375) - 0.015);

    // shared single mid-ground flywheel + shared gear loop (sdGear & sdFlywheel inlined ONCE!)
    vec2 buv = vec2(bx, uv.y);
    vec2 fwPos; vec4 fwP; float fwAng;
    int nGears = 2;

    if (bType < 0.5) {
        // Bay 0 (Center): pumping vertical steam piston on right
        vec2 pOrig = vec2(0.185, -0.295);
        float sc = 0.94;
        float pAng = ts * 1.45 + 0.8 + bayId * 2.1;
        float exh = smoothstep(0.42, 0.95, sin(pAng));
        dMach = min(dMach, sdPiston(buv - pOrig, pAng, sc));
        fwPos = pOrig;
        fwP = vec4(0.050 * sc * 1.36, 0.013 * sc, 0.018 * sc, 5.0);
        fwAng = -pAng;

        vec2 sq = buv - (pOrig + vec2(0.060, 0.275));
        if (sq.y > -0.02 && sq.y < 0.28 && abs(sq.x - sq.y * 0.25) < 0.15) {
            float pw = 0.022 + 0.28 * max(sq.y, 0.0);
            float sx = sq.x - sq.y * 0.24 - 0.012 * sin(sq.y * 24.0 - ts * 4.5);
            float pEnv = exp(-(sx * sx) / (pw * pw)) * smoothstep(-0.01, 0.025, sq.y) * smoothstep(0.27, 0.05, sq.y);
            pistonSteam = max(pistonSteam, pEnv * (0.35 + 0.85 * exh) * (0.35 + 0.65 * wisp));
        }

        // tiny crisp indicator box mounted on left column
        vec2 jb = vec2(-0.285, 0.02);
        dMach = min(dMach, EwSdBox2D(buv - jb, vec2(0.016, 0.024)) - 0.002);
        float dl1 = length(buv - (jb + vec2(0.0, 0.008)));
        float dl2 = length(buv - (jb + vec2(0.0, -0.008)));
        pilotGlow += vec3(1.0, 0.18, 0.04) * (exp(-dl1 * 340.0) * 1.4 + exp(-dl1 * 85.0) * 0.06) * (0.6 + 0.4 * sin(ts * 2.1 + bayId));
        pilotGlow += vec3(0.20, 0.95, 0.42) * (exp(-dl2 * 360.0) * 1.1 + exp(-dl2 * 95.0) * 0.05) * (0.4 + 0.6 * sin(ts * 3.7 + 1.4) * 0.5 + 0.5);
    } else if (bType < 1.5) {
        // Bay +1 (Right): Victorian oscillating walking-beam steam engine
        vec2 bOrig = vec2(-0.01, -0.025);
        float sc = 1.02;
        float bAng = ts * 1.15 + 1.3 + bayId;
        dMach = min(dMach, sdBeamEngine(buv - bOrig, bAng, sc));
        fwPos = bOrig + vec2(0.118, -0.135) * sc;
        fwP = vec4(0.058 * sc, 0.011 * sc, 0.015 * sc, 5.0);
        fwAng = -bAng;

        // diagonal riveted steam conduit pipe across upper corner
        dMach = min(dMach, EwSdSegment2D(buv, vec2(-0.31, 0.24), vec2(-0.08, 0.39)) - 0.012);

        // crisp amber pilot lamp on right manifold
        vec2 jb = vec2(-0.275, -0.02);
        dMach = min(dMach, EwSdBox2D(buv - jb, vec2(0.015, 0.020)) - 0.002);
        float dl1 = length(buv - jb);
        pilotGlow += vec3(1.0, 0.58, 0.08) * (exp(-dl1 * 340.0) * 1.4 + exp(-dl1 * 80.0) * 0.06) * (0.65 + 0.35 * sin(ts * 2.9 + bayId));
    } else {
        // Bay -1 (Left): heavy interlocking 3-gear clockwork cluster + high pulley wheel & steam pipe
        nGears = 3;
        float pAng = -ts * 0.85;
        float pR = 0.048;
        fwPos = vec2(0.175, uv.y > -0.065 ? 0.175 : -0.305);
        fwP = vec4(pR, 0.010, 0.013, 4.0);
        fwAng = pAng;
        if (uv.y > -0.305 && uv.y < 0.175) {
            float chainDist = abs(abs(bx - 0.175) - (pR - 0.004)) - 0.0038;
            float sideDir = bx > 0.175 ? 1.0 : -1.0;
            float lugY = abs(fract((uv.y + sideDir * pAng * pR) * 14.0) - 0.5) / 14.0;
            float dLug = max(abs(abs(bx - 0.175) - (pR - 0.002)) - 0.0075, lugY - 0.005);
            dMach = min(dMach, min(chainDist, dLug));
        }
        dMach = min(dMach, EwSdSegment2D(buv, vec2(0.11, 0.39), vec2(0.31, 0.23)) - 0.012);

        // crisp ruby + amber pilot lamps on junction box
        vec2 jb = vec2(0.25, -0.05);
        dMach = min(dMach, EwSdBox2D(buv - jb, vec2(0.016, 0.024)) - 0.002);
        float dl1 = length(buv - (jb + vec2(0.0, 0.007)));
        float dl2 = length(buv - (jb + vec2(0.0, -0.007)));
        pilotGlow += vec3(1.0, 0.16, 0.04) * (exp(-dl1 * 340.0) * 1.3 + exp(-dl1 * 80.0) * 0.055) * (0.55 + 0.45 * sin(ts * 1.6 + 2.0));
        pilotGlow += vec3(1.0, 0.65, 0.10) * (exp(-dl2 * 350.0) * 1.1 + exp(-dl2 * 85.0) * 0.045) * (0.7 + 0.3 * sin(ts * 4.2));
    }

    dMach = min(dMach, sdFlywheel(buv - fwPos, fwP.x, fwP.y, fwP.z, fwP.w, fwAng));

    // evaluate bay gears via a non-unrolled loop so sdGear is inlined ONCE instead of 7x
    for (int gi = ZERO; gi < nGears; gi++) {
        vec2 gc_; float gr, gn, gth, grim, ghub, gsp, gang;
        if (bType < 0.5) {
            vec2 cA = vec2(-0.185, -0.225);
            float angA = ts * 0.36 + bayId * 1.1;
            if (gi == 0) {
                gc_ = cA; gr = 0.098; gn = 16.0; gth = 0.010; grim = 0.017; ghub = 0.025; gsp = 5.0; gang = angA;
            } else {
                gc_ = cA + vec2(0.65243747, 0.75784256) * (0.098 + 0.058 + 0.010 * 0.92);
                gr = 0.058; gn = 10.0; gth = 0.0088; grim = 0.012; ghub = 0.017; gsp = 4.0;
                gang = meshAngle(angA, 0.86, 16.0, 10.0);
            }
        } else if (bType < 1.5) {
            vec2 cA = vec2(-0.215, -0.258);
            float angA = -ts * 0.42 + bayId;
            if (gi == 0) {
                gc_ = cA; gr = 0.078; gn = 14.0; gth = 0.0088; grim = 0.014; ghub = 0.020; gsp = 5.0; gang = angA;
            } else {
                gc_ = cA + vec2(0.98006658, -0.19866933) * (0.078 + 0.048 + 0.0088 * 0.92);
                gr = 0.048; gn = 9.0; gth = 0.0088 * 0.85; grim = 0.010; ghub = 0.014; gsp = 4.0;
                gang = meshAngle(angA, -0.20, 14.0, 9.0);
            }
        } else {
            vec2 cA = vec2(-0.09, -0.245);
            float angA = ts * 0.32 + bayId * 1.7;
            vec2 cB = cA + vec2(0.97589745, 0.21822962) * (0.105 + 0.064 + 0.0105 * 0.92);
            float angB = meshAngle(angA, 0.22, 16.0, 10.0);
            if (gi == 0) {
                gc_ = cA; gr = 0.105; gn = 16.0; gth = 0.0105; grim = 0.018; ghub = 0.026; gsp = 6.0; gang = angA;
            } else if (gi == 1) {
                gc_ = cB; gr = 0.064; gn = 10.0; gth = 0.0105 * 0.9; grim = 0.013; ghub = 0.018; gsp = 4.0; gang = angB;
            } else {
                gc_ = cB + vec2(0.40848744, 0.91276394) * (0.064 + 0.044 + 0.0105 * 0.85);
                gr = 0.044; gn = 7.0; gth = 0.0105 * 0.8; grim = 0.0095; ghub = 0.013; gsp = 3.0;
                gang = meshAngle(angB, 1.15, 10.0, 7.0);
            }
        }
        dMach = min(dMach, sdGear(buv - gc_, gr, gn, gth, grim, ghub, gsp, gang));
    }

    // ----- Layer 3: hanging flickering Edison light bulbs + dusty golden cones -----
    float lampW = 0.56;
    float lx0 = uv.x + 0.04;
    float lampId = floor(lx0 / lampW + 0.5);
    vec3 bulbCone = vec3(0.0);
    vec3 bulbAmb = vec3(0.0);
    vec3 bulbEmit = vec3(0.0);
    float dLampSil = 1e5;
    float bulbShadeWarm = 0.0;

    for (int k = ZERO - 1; k <= 1; k++) {
        float lid = lampId + float(k);
        vec3 ld = getLamp(lid, ts);
        vec2 q = uv - ld.xy;
        float flk = ld.z;

        // cord & conical shade only within the current lamp cell (k == 0)
        if (k == 0) {
            float dCord = max(abs(q.x) - 0.0022, 0.028 - q.y);
            float shadeY = q.y - 0.022;
            float shadeW = mix(0.044, 0.011, clamp((shadeY + 0.013) / 0.028, 0.0, 1.0));
            float dShade = max(abs(q.x) - shadeW, abs(shadeY) - 0.014);
            dLampSil = min(dCord, dShade);
            if (abs(q.x) < 0.042 && abs(q.y - 0.008) < 0.006) {
                bulbShadeWarm = flk * smoothstep(0.042, 0.010, abs(q.x)) * smoothstep(0.006, 0.0, abs(q.y - 0.008));
            }
        }

        // distinct warm incandescent bulb & filament glow (golden amber, not washed white)
        float rBulb = length((q - vec2(0.0, -0.002)) * vec2(1.25, 0.85));
        vec3 warmGold = vec3(1.0, 0.56, 0.16);
        vec3 hotFil = vec3(1.0, 0.90, 0.62);
        bulbEmit += (hotFil * exp(-rBulb * 155.0) * 2.8 + warmGold * exp(-rBulb * 38.0) * 0.42) * flk;

        // smooth shade penumbra (NO hard horizontal line!)
        float belowShade = smoothstep(0.025, -0.020, q.y);
        float dy = max(-q.y, 0.0);
        float coneR = 0.032 + dy * 0.58;
        float inCone = smoothstep(coneR, coneR * 0.12, abs(q.x)) * belowShade;
        float att = exp(-dy * 2.55);
        // swirling steam/dust vividly illuminated inside the flickering light cone
        float dust = 0.25 + 1.35 * wisp;
        bulbCone += warmGold * inCone * att * dust * 0.20 * flk * steamFac;
        // soft smooth local light pool below the shade (for subtle machinery rim-lighting)
        bulbAmb += warmGold * exp(-length(q) * 4.8) * (0.12 + 0.88 * belowShade) * 0.055 * flk;
    }

    // composite mid-ground machinery silhouette with razor-thin directional warm rim-light
    float mMach = smoothstep(px * 1.4, -px * 1.4, dMach);
    vec2 machGrad = vec2(dFdx(dMach), dFdy(dMach));
    vec2 machN = machGrad / max(length(machGrad), 1e-5);
    float thinRim = smoothstep(-px * 3.2, -px * 0.3, dMach) * mMach;
    float upFace = sat(machN.y * 0.8 + 0.2);
    float dnFace = sat(-machN.y * 0.8 + 0.2);
    vec3 rimCol = (bulbCone * 1.25 + bulbAmb * 2.8) * upFace + furnaceGlow * 2.3 * dnFace + ambTint * 0.55 * upFace;
    vec3 machCol = ambTint * 0.06 + rimCol * thinRim * 1.05;
    col = mix(col, machCol, mMach);

    // crisp jewel pilot indicator lamps on machinery
    col += pilotGlow;

    // volumetric light cones & lower furnace haze (stronger in open air, soft atmospheric wrap over dark iron)
    col += bulbCone * (1.0 - 0.52 * mMach) + bulbAmb * (1.0 - 0.72 * mMach);
    col += furnaceGlow * (0.20 + 0.52 * wisp) * (1.0 - 0.65 * mMach) * steamFac;

    // ----- rising glowing foundry ember sparks (crisp isotropic pinpoints) -----
    {
        vec2 euv = uv * 22.0 + vec2(sin(uv.y * 7.5 + ts * 0.9) * 0.45, -ts * 1.15);
        vec2 eid = floor(euv);
        vec2 ef = fract(euv) - 0.5;
        vec3 eh = EwHash2To3(eid + vec2(11.3, 4.7));
        if (eh.z > 0.76) {
            vec2 ep = (eh.xy - 0.5) * 0.68;
            ep.x += 0.14 * sin(ts * 2.8 + eh.x * 20.0);
            float ed = length((ef - ep) * vec2(1.15, 0.85));
            float spark = smoothstep(0.065, 0.008, ed) * smoothstep(0.14, -0.34, uv.y);
            float tw = 0.45 + 0.55 * sin(ts * 9.0 + eh.y * 30.0);
            col += vec3(1.0, 0.46, 0.07) * spark * tw * 0.95 * (1.0 - 0.78 * mMach);
        }
    }

    // ----- foreground billowing steam clouds & piston exhaust puffs -----
    float rimDist = max(-dPipe, 0.0);
    if (SteamAmount > 0.001) {
        // curling rising steam columns + low floor vapor
        float lowSteam = smoothstep(0.28, -0.44, uv.y) * smoothstep(0.34, 0.80, mistMid) * 0.42;
        float driftWisp = smoothstep(0.46, 0.86, mistMid) * smoothstep(0.42, -0.15, uv.y) * 0.24;
        float totSteam = sat((lowSteam + driftWisp + pistonSteam * 0.78) * SteamAmount);
        vec3 stLit = ambTint * 0.75 + furnaceGlow * 1.25 + (bulbCone * 1.85 + bulbAmb * 2.2) + vec3(0.013, 0.016, 0.018);
        // subtle spill from the frame's glowing liquid tube onto nearby steam
        stLit += liqLin * Glow * 0.095 * exp(-rimDist / max(2.2 * I, 1.0));
        col = mix(col, stLit, totSteam * 0.65);
        col += stLit * totSteam * 0.38;
    }

    // hanging lamp cords, metal shades, and bright incandescent bulb cores on top of steam
    float mLamp = smoothstep(px * 1.3, -px * 1.3, dLampSil);
    col = mix(col, vec3(0.003, 0.0035, 0.004) + vec3(0.70, 0.36, 0.09) * bulbShadeWarm, mLamp);
    col += bulbEmit;

    // inner frame recession shadow & subtle green liquid bounce along the inner border
    float edgeAO = 0.52 + 0.48 * smoothstep(0.0, 1.45 * I, rimDist);
    col *= edgeAO;
    col += liqLin * Glow * 0.028 * exp(-rimDist / max(1.6 * I, 1.0));

    // filmic tone map + linear -> display sRGB
    col = EwTonemapAces(col * 1.25);
    col = pow(max(col, 0.0), vec3(1.0 / 2.2));
    return clamp(col, 0.0, 1.0);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float I = vFrame0.x;
    float rad = vFrame0.y;
    vec2 hs = vFrame0.zw;
    float t = vFrame1.x;
    float ts = vFrame1.y;
    float P = vFrame1.z;
    float open = vFrame1.w;

    float d0 = EwSdRoundedBox2D(p, hs, rad);
    vec4 bg = vec4(Background * Opacity, Opacity);

    float R  = 0.33 * I;               // main pipe radius
    float Rf = R * 1.22;               // flange radius
    float r2 = 0.2 * I;                // glass tube radius
    float dc = -(R + 0.2 * I + r2);    // glass tube center offset
    vec2 sOff = vec2(-0.09, 0.12) * I; // shadow offset (light top left)

    vec4 acc = vec4(0.0);
    vec3 addGlow = vec3(0.0);          // additive light (premultiplied: rgb without alpha)

    float wMax = 0.11 * PipeWave * I + 4.0;
    float d = d0;
    float haloMin = -(4.5 + 0.07 * PipeWave) * I;
    float haloMax = (3.0 + 0.07 * PipeWave) * I;
    bool inHaloBand = (d0 > haloMin && d0 < haloMax);
    bool inFrameBand = (d0 > -(1.25 + 0.11 * PipeWave) * I - 4.0 && d0 < (1.05 + 0.11 * PipeWave) * I + 4.0);

    // valve / gauge / vent centers (precomputed in vertexMain)
    vec2 bH = p.y < 0.0 ? vValvesH.zw : vValvesH.xy;
    vec2 bV = p.x < 0.0 ? vValvesV.xy : vValvesV.zw;
    vec2 dH = p - bH, dV = p - bV;
    bool useH = dot(dH, dH) < dot(dV, dV);
    vec2 vc = useH ? bH : bV;
    float eSel = useH ? (p.y < 0.0 ? 1.0 : 0.0) : (p.x < 0.0 ? 2.0 : 3.0);
    vec3 vh = EwHash2To3(vec2(eSel, 5.3));
    vec2 gc = vGaugeVent.xy;
    vec2 vtc = vGaugeVent.zw;
    vec2 vhs = vec2(0.62, 0.46) * I;

    if (inHaloBand) {
        float Q;
        float s = perimPos(p, hs, rad, Q);
        float wM = wav(s, P, I, 0.11 * PipeWave, 0.0);
        float wT = wav(s, P, I, 0.07 * PipeWave, 1.3);
        d = d0 - wM;                 // main pipe local offset
        float v2 = d0 - dc - wT;     // glass tube local offset
        float flow = 0.6 + 0.4 * sin(s / I * 0.6 - ts * 2.5) * sin(s / I * 0.23 + ts * 0.7);

        if (inFrameBand) {
            vec2 g = boxGrad(p, hs, rad);
            vec2 tg = vec2(-g.y, g.x);
            float Rw = (0.42 + 0.22 * vh.y) * I, rw = (0.06 + 0.025 * vh.z) * I;
            float Rg = 0.66 * I;

            // ---------- drop shadows ----------
            {
                vec2 ps = p + sOff;
                float ds = EwSdRoundedBox2D(ps, hs, rad) - wM;
                float sh = smoothstep(R + 0.16 * I, R - 0.08 * I, abs(ds));
                sh = max(sh, 0.7 * smoothstep(r2 + 0.12 * I, r2 - 0.05 * I, abs(ds + wM - dc - wT)));
                float rq = length(ps - vc);
                sh = max(sh, smoothstep(0.12 * I, -0.04 * I, abs(rq - Rw) - rw));
                sh = max(sh, smoothstep(0.12 * I, -0.04 * I, rq - 0.22 * I));
                sh = max(sh, smoothstep(0.15 * I, -0.05 * I, length(ps - gc) - Rg));
                sh = max(sh, smoothstep(0.15 * I, -0.05 * I, EwSdRoundedBox2D(ps - vtc, vhs, 0.08 * I)));
                acc = vec4(0.0, 0.0, 0.0, 0.6 * sh);
            }

            float nseg = max(floor(P / (I * 5.0)), 4.0);
            float sl = P / nseg;
            float lsf = (fract(s / sl + 0.5) - 0.5) * sl;     // 0 at flanges / clamps
            float fw = 0.17 * I;
            float dPipe = min(abs(d) - R, max(abs(d) - Rf, abs(lsf) - fw));

            // rust texture only where main pipe or glass-tube clamps actually exist
            float rustN = 0.0, rustM = 0.0;
            bool needRust = (dPipe < 1.0) || (abs(v2) < r2 * 1.3 + 0.5 && abs(lsf) < 0.08 * I + 0.5);
            if (needRust) {
                rustN = fbm2(p / I * 1.3 + 3.1, 5);
                rustM = smoothstep(0.62 - 0.35 * Rust, 0.75 - 0.3 * Rust, rustN + 0.1 * (-p.y / res.y)) * step(0.001, Rust);
            }

            // ---------- glass tube with glowing liquid ----------
            if (abs(v2) < r2 * 1.3 + 2.0) {
                float y = v2 / r2;
                float cov = sat(r2 - abs(v2) + 0.5);
                vec3 N = tubeN(g, y, tg, 0.0);
                float iw = 0.72 * r2;
                float inner = sat(iw - abs(v2) + 0.5);
                float yi = clamp(v2 / iw, -1.0, 1.0);
                float depth = sqrt(max(1.0 - yi * yi, 0.0));         // thickness through the liquid
                // swirling volumetric liquid
                float sf = s - ts * 1.2 * I;
                vec2 lq = vec2(sf / I * 2.0, yi * 1.2);
                float sw1 = fbm2(lq + vec2(ts * 0.3, 0.0), 4);
                float sw2 = fbm2(lq * 2.3 + sw1 * 2.0 - vec2(ts * 0.5, 0.0), 3);
                float dens = 0.5 + 0.8 * sw2;
                vec3 deep = LiquidColor * LiquidColor * 0.35;
                vec3 bright = mix(LiquidColor, vec3(0.85, 1.0, 0.6), 0.12);
                vec3 liq = mix(deep, bright, sat(dens * depth * (0.55 + 0.6 * flow)));
                liq *= 0.4 + 0.9 * depth;
                // caustic streaks
                float ca1 = 1.0 - abs(EwValueNoise2D(lq * vec2(3.0, 4.0) + vec2(-ts * 0.8, sw1 * 3.0)) * 2.0 - 1.0);
                float ca2 = ca1 * ca1;
                float ca = ca2 * ca2 * ca2;
                liq += bright * ca * 0.6 * depth;
                // bubbles (two layers): clear centre, bright refracting rim, highlight dot
                float bubB = 0.0, bubR = 0.0, bubH = 0.0;
                for (int k = ZERO; k < 2; k++) {
                    float fk = float(k);
                    float cs = (0.26 + 0.18 * fk) * I;
                    float sb = mod(s - ts * (1.4 - 0.5 * fk) * I, P);
                    float cid = floor(sb / cs);
                    vec3 h = EwHash2To3(vec2(mod(cid, 997.0), 1.7 + fk * 3.1));
                    float br = (0.14 + 0.2 * h.z) * iw;
                    vec2 bp = vec2((h.x * 0.6 + 0.2) * cs, (h.y - 0.5) * 1.5 * (iw - br));
                    bp.y += 0.15 * iw * sin(ts * 3.0 + h.x * 20.0);
                    vec2 bq = vec2(sb - cid * cs, v2) - bp;
                    float bl = length(bq) / br;
                    float aaB = 1.5 / br;                                  // ~1.5 px soft edge
                    float has = step(0.25, h.z) * (1.0 - 0.35 * fk);
                    float bMask = smoothstep(1.0 + aaB, 1.0 - aaB, bl) * has;
                    bubB += bMask;
                    bubR += smoothstep(0.6, 0.92, bl) * bMask;
                    bubH += smoothstep(0.32, 0.12, length(bq / br - vec2(-0.35, 0.38))) * has;
                }
                bubB = sat(bubB); bubR = sat(bubR); bubH = sat(bubH);
                liq = mix(liq, liq * 0.45, bubB);                         // gas inside is darker
                liq += bright * 1.3 * bubR + vec3(0.95, 1.0, 0.9) * bubH * 1.6;
                liq *= Glow;
                // ----- round glass tube, built as premultiplied layers -----
                float ay = min(abs(y), 1.0);
                const vec2 Ld2 = vec2(-0.60971076, 0.79262399);           // normalize(LDir.xy)
                float gLd2 = dot(g, Ld2);
                float side = gLd2 * yi;                                   // +1 lit side, -1 shadow side (liquid)
                // liquid: cylindrical shading + focused caustic band on the far side (lens effect)
                liq *= 0.6 + 0.5 * side * depth;
                float sb1 = (side + 0.62) / 0.16;
                liq += bright * Glow * 0.4 * exp(-sb1 * sb1) * depth;
                liq *= LiquidColor;                                       // saturate (no white wash)
                vec3 liqPm = (vec3(0.005, 0.02, 0.01) + liq * 0.45) * inner;
                addGlow += liq * 0.15 * inner * cov;
                // glass wall thickness along the view ray (smooth, no cusp)
                float k = iw / r2;
                float tOut = sqrt(max(1.0 - ay * ay, 0.0));
                float tIn = sqrt(max(k * k - ay * ay, 0.0) + 0.012) - 0.11;
                float path = max(tOut - max(tIn, 0.0), 0.0);
                float ga = sat(0.06 + 0.55 * path) * cov;                 // glass absorption / scattering
                vec3 tint = mix(vec3(0.35, 0.45, 0.4), LiquidColor * Glow, 0.4) * (0.1 + 0.35 * path);
                vec4 pm;
                pm.rgb = liqPm * (1.0 - 0.2 * ga) + tint * ga;
                pm.a = 1.0 - (1.0 - inner) * (1.0 - ga);
                // reflections: fresnel environment + highlights
                float f1 = 1.0 - sat(N.z);
                float f2 = f1 * f1;
                float fres = 0.04 + 0.96 * f2 * f2;
                float envY = N.y * 0.5 + 0.5;
                vec3 envC = mix(vec3(0.05, 0.045, 0.04), vec3(0.8, 0.68, 0.52), smoothstep(0.3, 0.9, envY));
                envC += vec3(0.9, 0.95, 1.0) * smoothstep(0.84, 0.92, envY) * (1.0 - smoothstep(0.96, 1.0, envY));
                float ndh = sat(dot(N, LHalf));
                float sp = pow(ndh, 400.0);
                float spW = pow(ndh, 40.0);
                float sp2 = pow(sat(dot(N, vec3(0.3940552, -0.47286624, 0.7881104))), 120.0);
                // reflection of the light on the inner back wall (second, dimmer streak on the shadow side)
                float spb = (gLd2 * y + 0.8) / 0.05;
                float spBack = exp(-spb * spb) * 0.5;
                float streak = 0.75 + 0.25 * EwValueNoise2D(vec2(s / I * 3.0, 0.5));
                vec3 spec = vec3(1.0, 0.97, 0.9) * (sp * 3.0 + spW * 0.2 + spBack) * streak + vec3(0.6, 0.8, 1.0) * sp2 * 0.7;
                float dust = smoothstep(0.85, 0.95, EwValueNoise2D(vec2(s, v2) / I * 40.0));
                vec3 refl = envC * fres * 0.9 + spec * 0.6 + vec3(0.12) * dust * (0.3 + fres);
                pm.rgb += refl * cov;
                pm.a = sat(pm.a + (fres * 0.6 + sat(sp * 3.0)) * cov);
                addGlow += spec * 0.5 * cov;
                float a = pm.a;
                vec3 col = pm.rgb / max(a, 1e-3);
                // copper clamps with a rivet
                float cl = sat(0.08 * I - abs(lsf) + 0.5) * sat(r2 * 1.3 - abs(v2) + 0.5);
                if (cl > 0.0) {
                    vec3 Nc = tubeN(g, v2 / (r2 * 1.3), tg, clamp(lsf / (0.08 * I), -1.0, 1.0) * 0.6);
                    vec3 cc = shade(Nc, mix(CopperColor, vec3(0.25, 0.1, 0.04), rustM), mix(1.0, 0.2, rustM), mix(0.35, 0.9, rustM));
                    float nz2 = sat(Nc.z); nz2 *= nz2;
                    cc += CopperColor * LiquidColor * Glow * 0.6 * (nz2 * nz2) * flow;
                    float rv = length(vec2(lsf, v2)) - 0.04 * I;
                    if (rv < 0.5) {
                        cc = mix(cc, shade(normalize(vec3(g * v2 + tg * lsf, 0.04 * I)), vec3(0.5, 0.45, 0.38), 0.9, 0.4), sat(0.5 - rv));
                    }
                    col = mix(col, cc, cl);
                    addGlow *= 1.0 - cl;
                    a = max(a, cl);
                }
                acc = over(vec4(col * a, a), acc);
            }

            // halo
            {
                float od = max(abs(v2) - r2, 0.0);
                float gl = (exp(-od / (0.07 * I)) * 0.18 + exp(-od / (0.3 * I)) * 0.025) * Glow * flow;
                addGlow += LiquidColor * gl * smoothstep(0.75 * r2, 1.05 * r2, abs(v2));
            }

            // ---------- main copper pipe with flanges ----------
            if (dPipe < 1.0) {
                float cov = sat(0.5 - dPipe);
                bool fl = abs(lsf) < fw;
                float rr = fl ? Rf : R;
                float bev = fl ? smoothstep(0.4 * fw, fw, abs(lsf)) * sign(lsf) : 0.0;
                // slope of the wave tilts the normal a little
                float dw = (wav(s + 2.0, P, I, 0.11 * PipeWave, 0.0) - wM) * 0.5;
                vec3 N = tubeN(g, d / rr, tg, bev * 0.8 - dw * 0.6 * (1.0 - abs(d / rr)));
                float pat = fbm2(vec2(s * 0.015, d / I * 4.0), 4);
                float scr = EwValueNoise2D(vec2(s * 0.004, d / I * 30.0));
                // high frequency: hammered dents, fine brushed scratches, pits in rust
                vec2 hp = vec2(s, d) / I;
                float hm = EwValueNoise2D(hp * 9.0) - 0.5;
                float hm2 = EwValueNoise2D(hp * 9.0 + vec2(0.07, 0.0)) - 0.5;
                float hm3 = EwValueNoise2D(hp * 9.0 + vec2(0.0, 0.07)) - 0.5;
                float brs = EwValueNoise2D(vec2(s / I * 1.5, d / I * 80.0));
                float pit = smoothstep(0.62, 0.8, EwValueNoise2D(hp * 28.0)) * (0.3 + rustM);
                N = normalize(N + vec3(tg * (hm2 - hm) + g * (hm3 - hm), 0.0) * 4.0 * (0.4 + rustM));
                vec3 alb = CopperColor * (0.5 + 0.5 * pat) * (0.8 + 0.3 * scr) * (0.85 + 0.3 * brs);
                alb = mix(alb, vec3(0.22, 0.08, 0.03) * (0.6 + 0.7 * rustN), rustM);
                alb = mix(alb, vec3(0.12, 0.38, 0.3), smoothstep(0.7, 0.8, rustN) * 0.45 * Rust);
                alb *= 1.0 - 0.6 * pit;
                vec3 col = shade(N, alb, mix(1.0, 0.15, rustM), mix(0.25 + 0.15 * brs, 0.95, rustM));
                // green glow from the tube: diffuse bounce + glossy reflection (inner side of the pipe)
                vec3 Lg = normalize(vec3(-g, 0.35));
                float gdiff = sat(dot(N, Lg));
                float gspec = pow(sat(dot(N, normalize(Lg + vec3(0.0, 0.0, 1.0)))), mix(70.0, 20.0, rustM));
                float gfl = Glow * flow * (0.7 + 0.3 * sin(s / I * 2.0 - ts * 3.0));
                col += alb * LiquidColor * 0.12 * gfl * gdiff * gdiff;
                col += mix(alb, vec3(0.3), 0.2) * LiquidColor * 1.6 * gfl * gspec * (1.0 - 0.85 * rustM);
                // AO near flanges and at the pipe underside
                if (!fl) col *= 0.45 + 0.55 * smoothstep(0.0, 0.3 * I, abs(lsf) - fw);
                else col *= 0.85 + 0.15 * (1.0 - abs(bev));
                // seam line
                if (!fl) col *= 1.0 - 0.35 * sat(1.0 - abs(d - 0.45 * R) / 1.2);
                // nearest hex bolt on flanges (spaced by 0.68 * Rf, non-overlapping)
                if (fl) {
                    float kb = clamp(floor(d / (0.68 * Rf) + 0.5), -1.0, 1.0);
                    vec2 bq = vec2(lsf, d - kb * 0.68 * Rf);
                    float br = 0.065 * I;
                    vec2 bq2 = abs(bq);
                    float bd = max(bq2.x * 0.866 + bq2.y * 0.5, bq2.y) - br;
                    if (bd < 1.0) {
                        float m = sat(0.5 - bd);
                        vec2 bn = bq / br;
                        vec3 Nb = normalize(vec3(g * bn.y + tg * bn.x, 1.4));
                        col = mix(col, shade(Nb, vec3(0.4, 0.36, 0.3), 0.9, 0.45), m);
                    }
                }
                acc = over(vec4(col * cov, cov), acc);
                addGlow *= 1.0 - 0.75 * cov;
            }
            float aMid = acc.a;

            // ---------- vent (louvers open / close) ----------
            {
                vec2 q = p - vtc;
                float bd = EwSdRoundedBox2D(q, vhs, 0.08 * I);
                if (bd < 1.0) {
                    float m = sat(0.5 - bd);
                    vec2 e = vhs - abs(q);
                    float edge = min(e.x, e.y);
                    vec2 en = vec2(e.x < e.y ? sign(q.x) : 0.0, e.x < e.y ? 0.0 : sign(q.y));
                    vec3 N = normalize(vec3(en * (1.0 - smoothstep(0.0, 0.1 * I, edge)) * 1.5, 1.0));
                    float n = fbm2(q / I * 6.0 + 9.0, 4);
                    vec3 iron = mix(vec3(0.07, 0.065, 0.06), vec3(0.25, 0.09, 0.03), smoothstep(0.45, 0.65, n) * Rust);
                    vec3 col = shade(N, iron * (0.8 + 0.4 * n), 0.7, 0.6);
                    // rivets
                    vec2 rq = abs(q) - (vhs - 0.09 * I);
                    float rv = length(rq) - 0.035 * I;
                    if (rv < 0.5) {
                        col = mix(col, shade(normalize(vec3(rq / (0.035 * I), 1.0)), vec3(0.4, 0.35, 0.3), 0.9, 0.4), sat(0.5 - rv));
                    }
                    // opening with louvers
                    vec2 ih = vhs - vec2(0.15, 0.13) * I;
                    float od = EwSdRoundedBox2D(q, ih, 0.02 * I);
                    if (od < 1.0) {
                        float om = sat(0.5 - od);
                        float hc = 2.0 * ih.y / 3.0;
                        float ly = mod(q.y + ih.y, hc) - 0.5 * hc;
                        float sAng = vGaugeData.z, cAng = vGaugeData.w;
                        float half_ = 0.5 * hc * max(cAng, 0.05);
                        float sm = sat(half_ - abs(ly) + 0.5);
                        float s9 = sin(ts * 9.0);
                        vec3 hole = vec3(0.01) + vec3(0.25, 0.08, 0.02) * open * (0.5 + 0.5 * s9) * 0.6;
                        vec3 Ns = normalize(vec3(0.0, -sAng * 0.9 + (ly / max(half_, 1.0)) * 0.3, cAng));
                        vec3 sc = shade(Ns, vec3(0.16, 0.14, 0.12), 0.8, 0.5);
                        sc *= 0.6 + 0.4 * smoothstep(0.0, 0.3 * ih.x, ih.x - abs(q.x));
                        vec3 ic = mix(hole, sc, sm);
                        ic *= 1.0 - 0.6 * sat(1.0 - abs(od) / (0.04 * I)) * step(od, 0.0);
                        col = mix(col, ic, om);
                        addGlow += vec3(1.0, 0.35, 0.08) * open * (1.0 - sm) * om * 0.5 * (0.7 + 0.3 * s9);
                    }
                    acc = over(vec4(col * m, m), acc);
                }
            }

            // ---------- red valve wheel ----------
            {
                vec2 q = p - vc;
                float rq = length(q);
                if (rq < Rw + rw + 2.0) {
                    float vid = vh.x * 6.2831853;
                    float dirn = vh.z > 0.5 ? 1.0 : -1.0;
                    float k = ts * (0.15 + 0.15 * vh.y) + vh.z * 5.0;
                    float fk = fract(k);
                    float rot = vid + dirn * (floor(k) + smoothstep(0.0, 0.35, fk)) * (1.0 + vh.x) + 0.04 * sin(ts * 13.0) * smoothstep(0.35, 0.0, fk);
                    vec2 qd = rq > 1e-3 ? q / rq : vec2(0.0, 1.0);
                    mat2 rotM = EwRotate2D(rot);
                    vec2 qr = rotM * q;
                    vec2 qr0 = qr / I;                                    // texture rotates with the wheel
                    float cn = fbm2(qr0 * 5.0 + vid, 4) * (0.6 + 0.6 * Rust);
                    float chip = smoothstep(0.62, 0.68, cn);
                    float chipEdge = smoothstep(0.58, 0.62, cn) * (1.0 - chip);
                    float grain = EwValueNoise2D(qr0 * 60.0) * 0.6 + EwValueNoise2D(qr0 * 140.0) * 0.4;
                    float drip = EwValueNoise2D(vec2(qr0.x * 25.0, qr0.y * 3.0));
                    vec3 metalBare = vec3(0.2, 0.1, 0.06) * (0.6 + 0.8 * EwValueNoise2D(qr0 * 35.0));
                    vec3 paint = ValveColor * (0.8 + 0.4 * vh.y) * (0.75 + 0.35 * grain) * (0.85 + 0.25 * drip);
                    paint = mix(paint, vec3(0.75, 0.6, 0.45), chipEdge * 0.35);  // paint flake edge
                    paint = mix(paint, metalBare, chip);
                    float met = mix(0.2, 0.6, chip), rgh = mix(0.3 + 0.2 * grain, 0.85, chip);
                    float qAng = atan(q.y, q.x);
                    float bon = rq - 0.3 * I;
                    if (bon < 1.0) {
                        float m = sat(0.5 - bon);
                        vec3 N = normalize(vec3(qd * (rq / (0.3 * I)) * 0.8, 0.6));
                        vec3 c = shade(N, vec3(0.75, 0.55, 0.25), 1.0, 0.35);
                        c *= 0.75 + 0.25 * cos(qAng * 6.0);               // hex nut facets
                        acc = over(vec4(c * m, m), acc);
                    }
                    float ns = 3.0 + floor(vh.y * 3.0);
                    float ang = qAng + rot;
                    float sec = 6.2831853 / ns;
                    float la = mod(ang, sec) - 0.5 * sec;
                    float sw = 0.045 * I;
                    float sinLa = sin(la);
                    float sd = abs(rq * sinLa) - sw * (1.0 + 0.6 * (1.0 - rq / Rw));
                    sd = max(sd, max(0.15 * I - rq, rq - Rw));
                    if (sd < 1.0 && cos(la) > 0.0) {
                        float m = sat(0.5 - sd);
                        float sa = qAng - la;
                        vec2 pr = vec2(-sin(sa), cos(sa));
                        vec3 N = tubeN(pr, rq * sinLa / (sw * 1.3), vec2(0.0), 0.0);
                        acc = over(vec4(shade(N, paint, met, rgh) * m, m), acc);
                    }
                    float rd = abs(rq - Rw) - rw;
                    if (rd < 1.0) {
                        float m = sat(0.5 - rd);
                        vec3 N = tubeN(qd, (rq - Rw) / rw, vec2(0.0), 0.0);
                        float nk = 8.0 + 2.0 * ns;
                        N = normalize(N + vec3(vec2(-qd.y, qd.x) * sin(ang * nk) * 0.3, 0.0));
                        vec3 c = shade(N, paint * (0.85 + 0.15 * cos(ang * nk)), met, rgh);
                        acc = over(vec4(c * m, m), acc);
                    }
                    float hd = rq - 0.16 * I;
                    if (hd < 1.0) {
                        float m = sat(0.5 - hd);
                        vec3 N = tubeN(qd, rq / (0.16 * I), vec2(0.0), 0.0);
                        vec3 c = shade(N, paint, met, rgh);
                        float nut = max(abs(qr.x) * 0.866 + abs(qr.y) * 0.5, abs(qr.y)) - 0.065 * I;
                        if (nut < 0.5) {
                            c = mix(c, shade(normalize(vec3(qr / (0.065 * I) * 0.5, 1.0)), vec3(0.45, 0.42, 0.38), 0.9, 0.3), sat(0.5 - nut));
                        }
                        acc = over(vec4(c * m, m), acc);
                    }
                }
            }

            // ---------- pressure gauge ----------
            {
                vec2 q = p - gc;
                float rq = length(q);
                if (rq < Rg + 2.0) {
                    vec2 qd = rq > 1e-3 ? q / rq : vec2(0.0, 1.0);
                    float m = sat(Rg - rq + 0.5);
                    float Rb = 0.17 * I;                       // bezel width
                    float fr = Rg - Rb;
                    // brass bezel
                    vec3 N = tubeN(qd, (rq - (Rg - 0.5 * Rb)) / (0.5 * Rb), vec2(0.0), 0.0);
                    vec3 brass = vec3(0.8, 0.58, 0.26) * (0.75 + 0.35 * fbm2(q / I * 8.0, 3));
                    vec3 col = shade(N, brass, 1.0, 0.3);
                    float a = atan(q.x, q.y);                  // 0 up, clockwise
                    col *= 0.8 + 0.2 * smoothstep(0.3, 0.7, abs(fract(a / 6.2831853 * 6.0) - 0.5) * 2.0 + 0.6);
                    // face
                    if (rq < fr + 1.0) {
                        float fm = sat(fr - rq + 0.5);
                        float rfr = rq / fr;
                        vec3 face = vec3(0.62, 0.55, 0.42) * (1.0 - 0.45 * rfr * rfr * rfr);
                        face *= 0.85 + 0.15 * fbm2(q / I * 10.0, 3);      // aged paper
                        float ua = (a + 2.356) / 4.712;          // 0..1 over the dial
                        float onDial = step(0.0, ua) * step(ua, 1.0);
                        // red zone
                        face = mix(face, vec3(0.5, 0.03, 0.02), onDial * step(0.78, ua) * step(fr * 0.72, rq) * step(rq, fr * 0.88));
                        // ticks
                        float tk = abs(fract(ua * 10.0 + 0.5) - 0.5) * 4.712 / 10.0 * rq;
                        float tm = onDial * sat(1.2 - tk / max(fr * 0.012, 0.6)) * step(fr * 0.72, rq) * step(rq, fr * 0.9);
                        float tk2 = abs(fract(ua * 50.0 + 0.5) - 0.5) * 4.712 / 50.0 * rq;
                        tm = max(tm, onDial * sat(1.0 - tk2 / 0.6) * step(fr * 0.8, rq) * step(rq, fr * 0.9) * 0.7);
                        face = mix(face, vec3(0.03), tm);
                        // needle (direction precomputed in vertexMain)
                        vec2 nd = vGaugeData.xy;
                        vec2 na0 = -nd * fr * 0.18, na1 = nd * fr * 0.82;
                        float nl = EwSdSegment2D(q, na0, na1) - max(0.02 * I * (1.0 - 0.7 * dot(q, nd) / fr), 0.6);
                        float nsd = EwSdSegment2D(q + vec2(-0.03, 0.04) * I, na0, na1) - 0.03 * I;
                        face *= 1.0 - 0.45 * sat(0.5 - nsd / 2.0);
                        face = mix(face, vec3(0.08, 0.02, 0.01), sat(0.5 - nl));
                        float cap = rq - 0.07 * I;
                        if (cap < 0.5) {
                            face = mix(face, shade(tubeN(qd, rq / (0.07 * I), vec2(0.0), 0.0), vec3(0.7, 0.5, 0.25), 1.0, 0.3), sat(0.5 - cap));
                        }
                        // inner shadow from bezel + glass reflection
                        face *= 0.55 + 0.45 * smoothstep(fr, fr - 0.12 * I, rq + dot(qd, vec2(-0.7, 0.7)) * 0.05 * I);
                        vec2 gq = q / fr - vec2(-0.35, 0.4);
                        face += vec3(0.25) * smoothstep(0.25, 0.0, length(gq * vec2(1.0, 1.8)) - 0.2) * 0.6;
                        col = mix(col, face, fm);
                    }
                    acc = over(vec4(col * m, m), acc);
                    addGlow *= 1.0 - m;          // gauge fully covers the glass glow / highlights
                }
            }
            // vent / valve / gauge cover the additive glow below them
            addGlow *= 1.0 - sat((acc.a - aMid) / max(1.0 - aMid, 1e-3));
        } else {
            // outside frame band, still inside halo band: only halo contributes
            float od = max(abs(v2) - r2, 0.0);
            float gl = (exp(-od / (0.07 * I)) * 0.18 + exp(-od / (0.3 * I)) * 0.025) * Glow * flow;
            addGlow += LiquidColor * gl * smoothstep(0.75 * r2, 1.05 * r2, abs(v2));
        }
    }

    // ---------- steam (loop prevents 3x inlining of steam + fbm2) ----------
    if (SteamAmount > 0.0) {
        vec2 sg = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
        float st = 0.0;
        for (int si = ZERO; si < 3; si++) {
            vec2 sq, sdir;
            float sseed, stime, smode, slen, smul;
            if (si == 0) {
                vec2 cp = sg * (hs - rad + rad * 0.7071);
                sq = (p - cp) / I;
                sdir = normalize(-sg + vec2(0.0, 1.4));
                sseed = dot(sg, vec2(1.7, 4.3));
                stime = t; smode = 0.0; slen = 4.0; smul = 0.7;
            } else if (si == 1) {
                sq = (p - vc) / I;
                sdir = normalize(-vc / max(length(vc), 1.0) + vec2(0.0, 0.8));
                sseed = vh.x * 20.0;
                stime = t; smode = 0.0; slen = 3.0; smul = 0.5;
            } else {
                sq = (p - vtc - vec2(0.0, 0.3 * I)) / I;
                sdir = vec2(0.14834045, 0.98893635); // normalize(vec2(0.15, 1.0))
                sseed = 3.3;
                stime = t * 1.4; smode = 1.0; slen = 7.0; smul = 1.0;
            }
            st = max(st, steam(sq, sdir, sseed, stime, ts, smode, slen) * smul);
        }
        if (st > 0.0001) {
            st *= SteamAmount;
            vec3 sc = vec3(0.75, 0.74, 0.72) * (0.6 + 0.4 * sat(0.5 + p.y / res.y));
            sc += LiquidColor * 0.25 * Glow * exp(-abs(d0 - dc) / I);   // green light in the steam
            acc = over(vec4(sc * st * 0.85, st * 0.85), acc);
            addGlow *= 1.0 - 0.5 * st;
        }
    }

    // linear -> display (skip pow when empty - ~70% of pixels!)
    if (acc.a > 0.0) {
        acc.rgb = acc.rgb / (1.0 + 0.4 * acc.rgb);
        acc.rgb = pow(max(acc.rgb, 0.0), vec3(1.0 / 2.2)) * pow(max(acc.a, 1e-4), 1.0 - 1.0 / 2.2);
    }
    vec3 tot = acc.rgb;
    if (addGlow.r + addGlow.g + addGlow.b > 0.0) {
        vec3 ag = addGlow / (1.0 + addGlow);
        ag = pow(max(ag, 0.0), vec3(1.0 / 2.2));
        tot += ag * (1.0 - 0.5 * acc.rgb);
    }

    vec2 eb2 = min(fragCoord, res - fragCoord);
    float eb = min(eb2.x, eb2.y);
    float fade = smoothstep(1.0, 12.0, eb);
    tot *= fade;
    float a = clamp(acc.a * fade, 0.0, 1.0);

    float dz = EwDither(fragCoord, iTime, 2.0);
    vec3 col = clamp(tot + dz * a, 0.0, 1.0);
    a *= 1.0 - Additive;
    // background: image (when available) or dark steampunk factory inside the pipe loop (affected by InnerOpacity), Opacity / Background outside
    float inMask = smoothstep(0.5, -0.5, d);
    vec4 inBg = vec4(0.0);
    if (InnerOpacity > 0.001 && inMask > 0.001 && a < 0.999) {
        vec3 facCol = vec3(0.0);
        if (!SwHaveImage || SwTransparentImage) {
            facCol = factoryBg(p, ts, d, I);
            facCol = clamp(facCol + dz, 0.0, 1.0);
        }
        if (SwHaveImage) {
            vec2 imgUV = clamp(p / max(2.0 * hs, vec2(1.0)) + 0.5, 0.0, 1.0);
            vec2 texelScale = iChannelResolution[0].xy / max(2.0 * hs, vec2(1.0));
            float imgLod = max(0.0, log2(max(texelScale.x, texelScale.y)) - 0.25);
            vec4 imgTex = textureLod(iChannel0, imgUV, imgLod);
            vec3 imgLin = pow(clamp(imgTex.rgb, 0.0, 1.0), vec3(2.2));

            // inner frame recession shadow (AO)
            float edgeAO = 0.52 + 0.48 * smoothstep(0.0, 1.45 * I, max(-d0, 0.0));
            imgLin *= edgeAO;

            // glowing liquid bounce light from the glass tube
            float tubeDist = max(abs(d0 - dc) - r2, 0.0);
            vec3 liqLin = pow(clamp(LiquidColor, 0.0, 1.0), vec3(2.2));
            float liqPulse = 0.78 + 0.14 * sin(ts * 1.4);
            float liqSpill = exp(-tubeDist / max(1.25 * I, 1.0)) * Glow * liqPulse;
            vec3 imgLight = liqLin * liqSpill;

            // warm firelight from the vent when open
            if (open > 0.001) {
                float vDist = max(length(p - vtc) - 0.5 * I, 0.0) / max(I, 1.0);
                float vGlow = open * exp(-vDist * 0.55) * (0.7 + 0.3 * sin(ts * 9.0));
                imgLight += vec3(1.0, 0.35, 0.08) * vGlow;
            }

            imgLin += (imgLin * 0.65 + 0.025) * imgLight * (1.0 - 0.5 * imgLin);
            vec3 imgCol = pow(clamp(imgLin, 0.0, 1.0), vec3(1.0 / 2.2));
            imgCol = clamp(imgCol + dz, 0.0, 1.0);

            if (SwTransparentImage) {
                vec3 comb = mix(facCol, imgCol, imgTex.a);
                inBg = vec4(comb * InnerOpacity, InnerOpacity);
            } else {
                float imgA = imgTex.a * InnerOpacity;
                inBg = vec4(imgCol * imgA, imgA);
            }
        } else {
            inBg = vec4(facCol * InnerOpacity, InnerOpacity);
        }
    }
    vec4 bgSel = mix(bg, inBg, inMask);
    fragColor = vec4(clamp(col + bgSel.rgb * (1.0 - a), 0.0, 1.0), clamp(a + bgSel.a * (1.0 - a), 0.0, 1.0));
}
