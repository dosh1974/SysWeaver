//  media: audio stock:YuE2_00056.flac
//  Hifi VU Meters: the brushed aluminium front of a vintage amplifier with two backlit analog
//  VU meters behind glass. Needles follow the left / right level (+ kick), wandering room lights
//  pulse with bass / mids, the power lamp with the beat. Calm in silence.
//
//  iMouse.x = Backlight | Brightness of the meter lamps | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Gain | Needle sensitivity to the music | 0.333 | 0.5 .. 2.0 | x
//  iMouse.z = Glass | Strength of the reflections on the meter glass | 0.5 | 0.0 .. 1.0
//  iMouse.w = Warmth | Lamp color, cool white to warm amber | 0.7 | 0.0 .. 1.0

#define Backlight mix(0.0, 2.0, iMouse.x)
#define Gain mix(0.5, 2.0, iMouse.y)
#define GlassAmount mix(0.0, 1.0, iMouse.z)
#define Warmth mix(0.0, 1.0, iMouse.w)

// --- Panel ---
const vec3 PanelColor = vec3(0.62, 0.62, 0.64); // var: { "type": "col", "name": "Panel Color", "desc": "Tint of the brushed aluminium front" }
const float BrushStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Brush Strength", "desc": "Depth of the brushed grain", "unit": "x" }
const float GrainScale = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Grain Scale", "desc": "Fineness of the brushing: higher = finer, denser grooves", "unit": "x" }
const float ScratchAmount = 0.6; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Scratches", "desc": "Strength of the occasional deeper scratches" }
const float SmudgeAmount = 0.08; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Smudges", "desc": "Large soft brightness variation (fingerprints, wear)" }

// --- Lighting ---
const float RoomLights = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Room Lights", "desc": "Brightness of the two wandering colored lights reflected in metal and glass (they pulse with bass / mids)", "unit": "x" }
const vec3 RoomLightA = vec3(1.0, 0.55, 0.25); // var: { "type": "col", "name": "Room Light A", "desc": "Color of the bass driven wandering light" }
const vec3 RoomLightB = vec3(0.35, 0.6, 1.0); // var: { "type": "col", "name": "Room Light B", "desc": "Color of the mid driven wandering light" }
const float SheenSpeed = 0.03; // var: { "min": 0.0, "max": 0.3, "step": 0.005, "name": "Sheen Speed", "desc": "How fast the colored room lights wander across the metal", "unit": "Hz" }
const float LightReact = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Light Reaction", "desc": "How strongly the room lights pulse with bass, kicks and mids", "unit": "x" }
const float KeyLight = 0.42; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Key Light", "desc": "Main studio light (top left): the bright streaks on the metal" }
const vec3 KeyLightColor = vec3(1.0, 0.97, 0.92); // var: { "type": "col", "name": "Key Light Color", "desc": "Color of the main studio light" }
const float FillLight = 0.16; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Fill Light", "desc": "Soft cool light from the right" }
const float Exposure = 1.25; // var: { "min": 0.5, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float Vignette = 0.18; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Vignette", "desc": "Darkening toward the screen edges" }

// --- Meters ---
const vec3 DialColor = vec3(0.93, 0.85, 0.66); // var: { "type": "col", "name": "Dial Color", "desc": "Color of the meter paper" }
const vec3 LampColor = vec3(1.0, 0.58, 0.20); // var: { "type": "col", "name": "Lamp Color", "desc": "Warm lamp color (Warmth = 1)" }
const vec3 CoolLampColor = vec3(0.85, 0.92, 1.0); // var: { "type": "col", "name": "Cool Lamp Color", "desc": "Cool lamp color (Warmth = 0)" }
const vec3 ScaleRed = vec3(0.72, 0.05, 0.03); // var: { "type": "col", "name": "Red Zone", "desc": "Color of the overload zone" }
const vec3 PeakColor = vec3(1.0, 0.07, 0.02); // var: { "type": "col", "name": "Peak LED Color", "desc": "Color of the peak LEDs" }
const float NeedleSwing = 45.0; // var: { "min": 30.0, "max": 55.0, "step": 1.0, "name": "Needle Swing", "desc": "Half angle of the meter scale", "unit": "°" }
const float NeedleResponse = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Needle Response", "desc": "0 = slow, smooth VU ballistics, 1 = fast, jittery needles" }
const float NeedleKick = 0.05; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Needle Kick", "desc": "How far the needles jump on kick drums" }
const float MeterSize = 1.2; // var: { "min": 0.9, "max": 1.25, "step": 0.01, "name": "Meter Size", "desc": "Size of the meter windows", "unit": "x" }
const float MeterSpacing = 0.45; // var: { "min": 0.42, "max": 0.5, "step": 0.005, "name": "Meter Spacing", "desc": "Distance of the meters from the center" }
const float RecessShadow = 0.92; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Recess Shadow", "desc": "Darkness of the ambient occlusion at the dial's edges" }
// --- Knobs ---
const bool ShowKnobs = true; // var: { "name": "Knobs", "desc": "Show the knob row and power lamp" }
const vec3 PowerLampColor = vec3(1.0, 0.45, 0.08); // var: { "type": "col", "name": "Power Lamp Color", "desc": "Color of the power lamp" }
const float BeatPulse = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Beat Pulse", "desc": "How much the power lamp pulses with the beat" }

// fixed geometry: VU scale marks (dB), meter bevel width, viewer distance for reflections
const float DBS[11] = float[11](-20.0, -10.0, -7.0, -5.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0);
const float BevelW = 0.03;
const float CamZ = 2.4;

float gPx = 0.001;              // pixel size in the current space (AA)
vec3 gLP[4];                    // light positions
vec3 gLC[4];                    // light colors
float gLS[4];                   // light sizes

// meter window shape (local meter space)
float winSd(vec2 q) { return EwSdRoundedBox2D(q, vec2(0.33, 0.245), 0.03); }

// the room seen in the metal: bright ceiling / window left, dark floor, the viewer's dark silhouette
vec3 envLight(vec3 R)
{
    vec3 c = mix(vec3(0.06, 0.058, 0.06), vec3(0.46, 0.46, 0.47), smoothstep(-0.3, 0.45, R.y));
    c += vec3(0.2, 0.19, 0.17) * smoothstep(0.2, 0.9, -R.x) * smoothstep(-0.4, 0.3, R.y);
    c *= 1.0 - 0.45 * exp(-R.x * R.x * 55.0 - R.y * R.y * 2.0);
    return c;
}

// anisotropic (Ward) metal: grooves along T, alpha = roughness (along T, across T)
vec3 metalShade(vec3 P, vec3 N, vec3 T, vec2 alpha, vec3 F0)
{
    vec3 V = normalize(vec3(0.0, 0.0, CamZ) - P);
    vec3 B = normalize(cross(N, T));
    T = cross(B, N);
    float NV = max(dot(N, V), 0.05);
    vec3 R = reflect(-V, N);
    // anisotropic blur of the environment across the grooves
    vec3 env = envLight(R) * 0.6 + envLight(normalize(R + B * alpha.y)) * 0.2 + envLight(normalize(R - B * alpha.y)) * 0.2;
    vec3 col = env * F0;
    alpha = max(alpha, vec2(0.01));
    for (int i = 0; i < 4; i++)
    {
        vec3 L = normalize(gLP[i] - P);
        float NL = dot(N, L);
        if (NL <= 0.0) continue;
        vec3 H = normalize(L + V);
        float ax = alpha.x + gLS[i], ay = alpha.y + gLS[i];
        float hx = dot(H, T) / ax, hy = dot(H, B) / ay, hn = max(dot(H, N), 0.05);
        float w = exp(-(hx * hx + hy * hy) / (hn * hn)) / (12.566 * ax * ay * sqrt(max(NL, 0.05) * NV));
        col += gLC[i] * F0 * w * NL;
    }
    return col;
}
// one shading sample of a domed LED / lamp lens of radius R (local position lq, view dir V, world position P)
// in a black rounded bezel: tinted translucent plastic with depth (refracted reflector cup + die, focused
// light at the far edge), Fresnel room reflection and hard highlights of all lights;
// lit 0..1 blends from offCol to the glowing onCol; aa = edge feather
vec3 ledSample(vec3 bg, vec2 lq, float R, vec3 V, vec3 P, vec3 offCol, vec3 onCol, float lit, vec3 amb, float aa)
{
    float r = length(lq);
    float Rb = R * 1.28;

    // bezel ring: black glossy plastic with a rounded top
    vec2 rd = lq / max(r, 1e-6);
    float tr = clamp((r - R) / (Rb - R), 0.0, 1.0);
    vec3 nb = normalize(vec3(rd * (tr * 2.0 - 1.0) * 1.2, 1.0));
    vec3 ring = vec3(0.01) + envLight(reflect(-V, nb)) * 0.06;
    for (int i = 0; i < 4; i++)
    {
        vec3 H = normalize(normalize(gLP[i] - P) + V);
        ring += gLC[i] * pow(max(dot(nb, H), 0.0), 150.0) * 1.5;
    }
    vec3 col = mix(bg, ring, EwFillAA(r - Rb, aa));
    // dark gap between bezel and lens
    col = mix(col, vec3(0.003), EwFillAA(abs(r - R) - R * 0.07, aa));
    if (r > R) return col;

    // lens dome
    vec2 s = lq / R;
    float s2 = min(dot(s, s), 1.0);
    vec3 n = normalize(vec3(s * 0.85, sqrt(max(1.0 - s2 * 0.7225, 0.0))));
    // interior seen through the dome: refraction magnifies / pulls toward the center
    vec2 ri = (s - n.xy * 0.45) * 1.3;
    float rr = length(ri);
    float cup = smoothstep(0.62, 0.5, rr) * smoothstep(0.22, 0.4, rr);
    float die = smoothstep(0.17, 0.11, max(abs(ri.x), abs(ri.y)));
    vec2 cq = s - vec2(0.35, -0.5);
    float caustic = exp(-dot(cq, cq) * 5.0);
    // unlit: tinted plastic, thicker (darker) in the middle, light gathered at the lower right inside edge
    vec3 offBody = offCol * (amb * (0.25 + 0.6 * s2 + 2.2 * caustic + 0.5 * cup) + 0.01);
    offBody *= 1.0 - 0.7 * die;
    // lit: glowing body, hot die, bright reflector cup
    vec3 onBody = onCol * (0.55 + 3.5 * exp(-rr * rr * 5.0) + 0.9 * cup + 0.6 * caustic);
    vec3 body = mix(offBody, onBody, lit);

    // Fresnel room reflection, hard highlights of the (moving) lights, bright internal rim
    float nv = max(dot(n, V), 0.0);
    float fr = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
    vec3 refl = envLight(reflect(-V, n)) * (0.15 + fr);
    for (int i = 0; i < 4; i++)
    {
        vec3 H = normalize(normalize(gLP[i] - P) + V);
        float nh = max(dot(n, H), 0.0);
        refl += gLC[i] * (pow(nh, 700.0) * 22.0 + pow(nh, 90.0) * 0.8);
    }
    vec3 rimCol = mix(offCol * (amb + 0.05), onCol, lit);
    body += rimCol * smoothstep(0.7, 0.97, sqrt(s2)) * 0.6;
    vec3 lens = body * (1.0 - fr) + refl;
    return mix(col, lens, EwFillAA(r - R * 0.93, aa));
}

// a domed LED / lamp lens with its bezel and contact shadow, 4x supersampled (rotated grid)
// so the tiny hard highlights and edges stay smooth
vec3 ledLens(vec3 bg, vec2 lq, float R, vec3 P, vec3 offCol, vec3 onCol, float lit, vec3 amb)
{
    float Rb = R * 1.28;
    if (length(lq) > Rb * 1.7) return bg;
    vec3 V = normalize(vec3(0.0, 0.0, CamZ) - P);
    // contact shadow of the bezel (key light from the top left)
    bg *= mix(0.4, 1.0, smoothstep(Rb * 0.85, Rb * 1.55, length(lq - vec2(0.25, -0.35) * R)));
    if (length(lq) > Rb + 2.0 * gPx) return bg;
    vec3 acc = vec3(0.0);
    for (int k = 0; k < 4; k++)
    {
        vec2 o = vec2(k == 0 ? -0.125 : k == 1 ? 0.375 : k == 2 ? -0.375 : 0.125, k == 0 ? 0.375 : k == 1 ? 0.125 : k == 2 ? -0.125 : -0.375);
        // clamp each sample to the tone curve's range so a hot highlight in one sample doesn't stay a hard pixel
        acc += min(ledSample(bg, lq + o * gPx, R, V, P, offCol, onCol, lit, amb, gPx * 0.5), vec3(3.0));
    }
    return acc * 0.25;
}

// brushed aluminium panel: horizontal grooves, scratches, smudges, anisotropic shading
vec3 panelShade(vec2 p, vec2 fragCoord)
{
    float f1 = 160.0 * GrainScale, f2 = 420.0 * GrainScale, f3 = 1100.0 * GrainScale;
    float w1 = smoothstep(0.6, 0.3, f1 * gPx);
    float w2 = smoothstep(0.6, 0.3, f2 * gPx);
    float w3 = smoothstep(0.6, 0.3, f3 * gPx);
    // grain: long fine grooves (stretched along x) fading out below pixel size, per pixel row streaks
    float g = ((EwValueNoise2D(vec2(p.x * 4.0, p.y * f1)) - 0.5) * w1 * 0.55
            + (EwValueNoise2D(vec2(p.x * 9.0 + 7.0, p.y * f2)) - 0.5) * w2 * 0.55
            + (EwValueNoise2D(vec2(p.x * 20.0 + 3.0, p.y * f3)) - 0.5) * w3 * 0.45
            + (EwValueNoise2D(vec2(p.x * 3.0, p.y * 55.0)) - 0.5) * 0.12
            + (EwHash2To1(vec2(floor(p.x * 6.0 + EwHash2To1(vec2(floor(fragCoord.y), 1.0)) * 6.0), floor(fragCoord.y))) - 0.5) * 0.3) * BrushStrength;
    // a few deeper scratches
    float scr = pow(EwValueNoise2D(vec2(p.x * 2.0 + 11.0, p.y * f2 * 0.5)), 6.0) * w2;
    g += scr * ScratchAmount * BrushStrength;
    float tilt = (EwValueNoise2D(vec2(p.x * 6.0, p.y * 90.0)) - 0.5) * BrushStrength;
    vec3 N = normalize(vec3(tilt * 0.03, g * 0.07, 1.0));
    vec2 alpha = vec2(0.05 * (1.0 + 0.6 * g), 0.35);
    float smudge = EwValueNoise2D(p * vec2(1.2, 2.5) + 3.0);
    vec3 F0 = PanelColor * (1.15 + 0.35 * g) * (1.0 - SmudgeAmount * 0.625 + SmudgeAmount * smudge);
    return metalShade(vec3(p, 0.0), N, vec3(1.0, 0.0, 0.0), alpha, F0);
}
// soft darkening of the panel toward a meter opening (d = window distance)
float meterOcclusion(float d) { return 1.0 - 0.18 * smoothstep(BevelW + 0.035, BevelW, d); }

// one meter in its local space q (wp = panel position): bevel, recess, dial, needle, glass
vec3 meter(vec2 q, vec2 wp, float val, float peak, vec3 panel, vec3 lamp)
{
    float d = winSd(q);
    const float bev = BevelW;
    const float Frame = 0.008;
    if (d > bev + 0.03) return panel;
    vec3 col = panel;

    // gradient of the window outline (points outwards)
    float e = 0.002;
    vec2 g = vec2(winSd(q + vec2(e, 0.0)) - winSd(q - vec2(e, 0.0)),
                  winSd(q + vec2(0.0, e)) - winSd(q - vec2(0.0, e)));
    g /= max(length(g), 1e-6);

    // soft occlusion of the panel toward the opening
    col *= meterOcclusion(d);

    // polished curved chamfer (flat at the outer edge, steep at the inner lip), machined along the edge
    float tb = clamp((bev - d) / bev, 0.0, 1.0);
    float slope = mix(0.12, 1.9, tb * tb);
    float cw = smoothstep(0.6, 0.3, 1500.0 * gPx);
    float cn = ((EwValueNoise2D(vec2(d * 1500.0, 1.0)) - 0.5) * cw + (EwValueNoise2D(vec2(d * 400.0, 5.0)) - 0.5) * 0.5) * BrushStrength;
    vec3 Nc = normalize(vec3(-g * slope * (1.0 + 0.1 * cn), 1.0));
    vec3 bevCol = metalShade(vec3(wp, -(bev - d) * 1.2), Nc, vec3(-g.y, g.x, 0.0), vec2(0.03, 0.12), PanelColor * (1.25 + 0.2 * cn));
    // ambient occlusion: the deep inner lip sees little of the room
    bevCol *= mix(0.3, 1.0, smoothstep(0.0, 0.012, d)) * mix(1.0, 0.8, tb);
    // outer seam: a hairline shadow, then the rounded edge catching light
    float seam = EwFillAA(abs(d - bev) - 0.0006, gPx);
    bevCol += PanelColor * 0.35 * EwFillAA(abs(d - bev + 0.0018) - 0.0008, gPx) * (0.5 + 0.5 * max(-g.y, 0.0));
    col = mix(col, bevCol, EwFillAA(d - bev, gPx));
    col *= 1.0 - 0.5 * seam;
    if (d > gPx * 2.0) return col;
    // dial geometry
    vec2 P = vec2(0.0, -0.355);
    vec2 v = q - P;
    float r = length(v);
    float ang = atan(v.x, v.y);
    float sw = radians(NeedleSwing);
    float u = ang / sw * 0.5 + 0.5;
    float al = 2.0 * sw * r;

    vec3 paper = DialColor * (0.95 + 0.1 * EwValueNoise2D(q * 260.0));
    float inkB = 0.0, inkR = 0.0;
    // main scale arc + red zone
    inkB = max(inkB, EwFillAA(max(abs(r - 0.40) - 0.0012, (abs(u - 0.3894) - 0.3186) * al), gPx));
    inkR = max(inkR, EwFillAA(max(abs(r - 0.4065) - 0.0065, (abs(u - 0.854) - 0.146) * al), gPx));
    // dB ticks
    for (int i = 0; i < 11; i++)
    {
        float db = DBS[i];
        float ut = 0.708 * pow(10.0, db / 20.0);
        bool major = (i == 0 || i == 1 || i == 3 || i == 7 || i == 10);
        float len = major ? 0.042 : 0.026;
        float dt = max(abs(u - ut) * al - (major ? 0.0018 : 0.0012), abs(r - 0.40 - len * 0.5) - len * 0.5);
        float m = EwFillAA(dt, gPx);
        if (db > 0.0) inkR = max(inkR, m); else inkB = max(inkB, m);
    }
    // percent scale
    inkB = max(inkB, EwFillAA(max(abs(r - 0.33) - 0.0008, (abs(u - 0.354) - 0.354) * al), gPx));
    float fu = u / 0.0708;
    float dp = max(abs(fract(fu + 0.5) - 0.5) * 0.0708 * al - 0.0009, abs(r - 0.324) - 0.006);
    inkB = max(inkB, EwFillAA(max(dp, (abs(u - 0.36) - 0.37) * al), gPx));
    // "VU" lettering
    float dl = EwSdSegment2D(q, vec2(-0.032, -0.05), vec2(-0.020, -0.08));
    dl = min(dl, EwSdSegment2D(q, vec2(-0.020, -0.08), vec2(-0.008, -0.05)));
    dl = min(dl, EwSdSegment2D(q, vec2(0.008, -0.05), vec2(0.008, -0.068)));
    dl = min(dl, EwSdSegment2D(q, vec2(0.032, -0.05), vec2(0.032, -0.068)));
    vec2 uc = q - vec2(0.02, -0.068);
    dl = min(dl, max(abs(length(uc) - 0.012), uc.y));
    inkB = max(inkB, EwFillAA(dl - 0.0025, gPx));

    // lamp light from below, needle shadow, bezel shadow
    vec2 l1 = q - vec2(-0.16, -0.29), l2 = q - vec2(0.16, -0.29);
    float glow = exp(-dot(l1, l1) * 16.0) + exp(-dot(l2, l2) * 16.0) + 0.2 * exp(-(q.y + 0.25) * 3.5);
    vec3 light = lamp * (0.02 + 1.9 * glow) + vec3(0.01);
    float a = (val * 2.0 - 1.0) * sw;
    vec2 dir = vec2(sin(a), cos(a));
    float dsh = EwSdSegment2D(v - vec2(0.004, 0.016), dir * 0.12, dir * 0.455);
    float shadow = smoothstep(0.0, 0.016, dsh);
    float dS = winSd(q + vec2(-0.012, 0.03));
    float bz = smoothstep(-0.015, 0.045, -dS);

    vec3 dial = mix(paper, vec3(0.03), inkB);
    dial = mix(dial, ScaleRed, inkR);
    dial *= light * mix(0.45, 1.0, shadow) * mix(0.25, 1.0, bz);

    // peak LED: a domed red lens in a black bezel, glowing onto the dial when lit
    vec2 lq = q - vec2(0.27, 0.155);
    float ld = length(lq);
    dial += PeakColor * peak * exp(-ld * ld * 900.0) * 0.6;
    dial = ledLens(dial, lq, 0.011, vec3(wp, -0.02), PeakColor * 0.6, PeakColor * 1.8, peak, light * 0.7 + 0.03);

    // needle
    float th = mix(0.0026, 0.0011, clamp((dot(v, dir) - 0.12) / 0.33, 0.0, 1.0));
    float dn = EwSdSegment2D(v, dir * 0.12, dir * 0.455) - th;
    dial = mix(dial, vec3(0.015) + light * 0.03, EwFillAA(dn, gPx));

    // pivot cover (black, a faint lamp rim)
    vec3 cover = vec3(0.012, 0.011, 0.01) + lamp * 0.18 * smoothstep(0.012, 0.0, 0.17 - r);
    dial = mix(dial, cover * mix(0.4, 1.0, bz), EwFillAA(r - 0.17, gPx));

    // ambient occlusion of the recess on the dial (deeper toward the top: the bezel overhang)
    float dIn = -d - Frame;
    float aoR = mix(0.022, 0.05, smoothstep(-0.1, 0.2, q.y));
    dial *= mix(1.0 - RecessShadow, 1.0, 1.0 - exp(-max(dIn, 0.0) / aoR));

    // black inner frame (recess wall): top edge catches a little room light, sides get warm lamp spill
    float wallT = smoothstep(-Frame, 0.0, d);
    vec3 frameCol = vec3(0.006) + vec3(0.07) * max(-g.y, 0.0) * wallT * wallT
                  + lamp * 0.08 * smoothstep(0.1, -0.24, q.y) * (1.0 - wallT);
    dial = mix(frameCol, dial, EwFillAA(d + Frame, gPx));

    // glass: env reflection, light glints, edge highlight
    vec3 Pg = vec3(wp, -0.015);
    vec3 V = normalize(vec3(0.0, 0.0, CamZ) - Pg);
    vec3 refl = envLight(vec3(-V.xy, V.z)) * 0.08 + vec3(0.9, 0.95, 1.0) * 0.05 * smoothstep(0.08, 0.0, abs(q.x * 0.5 + q.y - 0.12));
    for (int i = 0; i < 4; i++)
    {
        vec3 H = normalize(normalize(gLP[i] - Pg) + V);
        float hz = max(H.z, 0.0);
        refl += gLC[i] * (pow(hz, 3000.0) * 7.0 + pow(hz, 250.0) * 0.35);
    }
    float topEdge = EwFillAA(abs(d + 0.0025) - 0.0012, gPx) * smoothstep(-0.15, 0.25, q.y - q.x * 0.3);
    refl += vec3(0.5) * topEdge * 0.35;
    dial = dial * 0.94 + refl * GlassAmount * 1.6;

    return mix(col, dial, EwFillAA(d, gPx));
}

// one knob: engraved scale, contact shadow, black skirt, spun aluminium cap, pointer (a = pointer angle)
vec3 knob(vec2 q, vec2 wp, float a, vec3 col)
{
    const float R = 0.055;
    float r = length(q);
    if (r > R + 0.06) return col;
    // engraved scale dots
    float aU = atan(q.x, q.y);
    float k = clamp(floor((aU + 2.4) / 0.48 + 0.5), 0.0, 10.0);
    float aa = -2.4 + k * 0.48;
    float dd = length(q - (R + 0.02) * vec2(sin(aa), cos(aa))) - 0.0025;
    col = mix(col, col * 0.2, EwFillAA(dd, gPx));
    // soft contact shadow + ambient occlusion
    float ds = length(q - vec2(0.008, -0.014)) - R;
    col *= mix(0.25, 1.0, smoothstep(-0.012, 0.035, ds));
    col *= mix(0.55, 1.0, smoothstep(0.0, 0.012, r - R));
    // black skirt with a soft sheen
    vec3 skirt = vec3(0.012) + vec3(0.06) * smoothstep(-0.3, 1.0, q.y / R) * smoothstep(R, R - 0.01, r);
    col = mix(col, skirt, EwFillAA(r - R, gPx));
    // spun aluminium cap: circular grooves -> radial anisotropic highlight
    float Rc = R * 0.82;
    vec2 tq = r > 1e-5 ? vec2(-q.y, q.x) / r : vec2(1.0, 0.0);
    float wr = smoothstep(0.6, 0.3, 900.0 * gPx);
    float rings = (EwValueNoise2D(vec2(r * 900.0, 3.0)) - 0.5) * wr + (EwValueNoise2D(vec2(r * 240.0, 9.0)) - 0.5) * 0.6;
    rings *= BrushStrength;
    float rim = smoothstep(Rc - 0.0075, Rc - 0.0015, r);
    vec3 N = normalize(vec3(q / max(r, 1e-5) * (0.04 + 1.1 * rim), 1.0));
    vec3 cap = metalShade(vec3(wp, 0.04), N, vec3(tq, 0.0), vec2(0.035, 0.3) * (1.0 + 0.5 * rings), PanelColor * (1.12 + 0.25 * rings));
    cap *= mix(1.0, 0.75, rim);
    col = mix(col, cap, EwFillAA(r - Rc, gPx));
    // pointer groove
    vec2 dir = vec2(sin(a), cos(a));
    float dpt = EwSdSegment2D(q, dir * 0.012, dir * (Rc - 0.01)) - 0.0018;
    col = mix(col, col * 0.15, EwFillAA(dpt, gPx));
    return col;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    // fit the whole front: portrait / narrow screens zoom out
    float aspect = iResolution.x / iResolution.y;
    float sc = max(1.0, 1.86 / aspect);
    vec2 p = (fragCoord - 0.5 * iResolution.xy) / iResolution.y * sc;
    float pxW = sc / iResolution.y;
    gPx = pxW;
    float t = mod(iTime, 3600.0);

    // audio: stereo levels (smoothed + raw), kick, bass, mids, beat (faded by tempo confidence)
    vec2 lvlS = texture(iChannel0, vec2(EwAudioLevelU, EwAudioLevelV + EwAudioDiffuseAddV)).gb;
    vec2 lvlR = texture(iChannel0, vec2(EwAudioLevelU, EwAudioLevelV)).gb;
    float kick = EwAudioGetKickPulse(iChannel0);
    float bass = EwAudioGetBassEnergy(iChannel0, true);
    float mid = EwAudioGetMidEnergy(iChannel0, true);
    float beat = EwAudioGetBeatPulse(iChannel0) * smoothstep(0.1, 0.3, EwAudioGetTempoConfidence(iChannel0));

    // needle positions (0 = left stop, ~0.71 = 0 VU) and peak LEDs
    vec2 vu = clamp((mix(lvlS, lvlR, NeedleResponse) * Gain - 0.56) / 0.34 + kick * NeedleKick, 0.0, 1.06);
    vu += 0.004 * (1.0 + sin(t * vec2(1.3, 1.1)));
    vec2 pk = smoothstep(0.86, 0.96, (lvlR * Gain - 0.56) / 0.34 + kick * NeedleKick);

    vec3 lamp = mix(CoolLampColor, LampColor, Warmth) * Backlight * (1.0 + 0.08 * bass);

    // room lights: two fixed studio lights + two slowly wandering colored ones
    float lt = t * SheenSpeed * EW_TAU;
    float bassE = smoothstep(0.3, 0.8, bass);
    float midE = smoothstep(0.3, 0.8, mid);
    gLP[0] = vec3(-1.4, 1.8, 1.6); gLC[0] = KeyLightColor * KeyLight; gLS[0] = 0.10;
    gLP[1] = vec3(1.7, 1.1, 2.2);  gLC[1] = vec3(0.85, 0.92, 1.0) * FillLight; gLS[1] = 0.14;
    gLP[2] = vec3(sin(lt) * 1.7, -0.2 + 0.5 * sin(lt * 0.71 + 1.0), 1.3);
    gLC[2] = RoomLightA * (0.12 + (0.3 * bassE + 0.12 * kick) * LightReact) * RoomLights; gLS[2] = 0.035;
    gLP[3] = vec3(sin(lt * 1.37 + 2.5) * 1.9, 0.7 + 0.4 * cos(lt * 0.93), 1.5);
    gLC[3] = RoomLightB * (0.06 + 0.2 * midE * LightReact) * RoomLights; gLS[3] = 0.045;

    // brushed panel + two engraved lines
    vec3 col = panelShade(p, fragCoord);
    for (int i = 0; i < 2; i++)
    {
        float Y = i == 0 ? 0.468 : -0.268;
        col *= 1.0 - 0.6 * EwFillAA(abs(p.y - Y) - 0.0012, gPx);
        col += col * 0.6 * EwFillAA(abs(p.y - Y + 0.0028) - 0.0008, gPx);
    }

    // meters: only the nearer one is shaded; the other only darkens the panel softly near its opening
    gPx = pxW / MeterSize;
    float side = p.x < 0.0 ? -1.0 : 1.0;
    vec2 qo = (p - vec2(-side * MeterSpacing, 0.1)) / MeterSize;
    col *= meterOcclusion(winSd(qo));
    col = meter((p - vec2(side * MeterSpacing, 0.1)) / MeterSize, p, side < 0.0 ? vu.x : vu.y, side < 0.0 ? pk.x : pk.y, col, lamp);
    gPx = pxW;

    // knobs (only the nearest one) + power lamp
    if (ShowKnobs)
    {
        float ax = abs(p.x);
        float kx = ax > 0.5 ? 0.64 : 0.36;
        float ka = p.x < 0.0 ? (ax > 0.5 ? -1.2 : 0.3) : (ax > 0.5 ? 1.6 : -0.2);
        col = knob(p - vec2(side * kx, -0.395), p, ka, col);
        vec2 lq = p - vec2(0.0, -0.395);
        float ld = length(lq);
        float on = (1.0 - BeatPulse + BeatPulse * beat) * clamp(Backlight, 0.2, 1.5);
        col += PowerLampColor * on * exp(-ld * ld * 300.0) * 0.25;
        col = ledLens(col, lq, 0.015, vec3(p, 0.01), PowerLampColor * 0.3, PowerLampColor * on * 0.4, 1.0, vec3(0.3));
    }

    // gentle lens falloff
    vec2 vq = p / vec2(max(aspect, 1.0 / sc) * 0.5 * sc, 0.5 * sc);
    col *= 1.0 - Vignette * dot(vq, vq) * 0.5;

    // soft tone curve, sRGB, dither
    col = 1.0 - exp(-col * Exposure);
    col = EwLinearToSrgb(col) + (EwHash2To1(fragCoord) - 0.5) / 255.0;
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
