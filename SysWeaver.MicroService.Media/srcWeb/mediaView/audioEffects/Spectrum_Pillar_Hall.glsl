//  media: audio2 stock:YuE2_00056.flac
//  Neon pillar hall: every pillar listens to one of 32 spectrum bands.
//  iMouse.x = Haze | Amount of glowing haze around the pillars | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Reactivity | How strongly the pillars flare with their band | 0.5 | 0.0 .. 3.0 | x
//  iMouse.z = Palette Shift | Rotates the spectrum colors around the hue circle | 0.0 | 0.0 .. 1.0
//  iMouse.w = Fog Distance | How far you see before the fog of war swallows the hall | 0.4 | 8.0 .. 60.0 | m

#define Haze mix(0.0, 2.0, iMouse.x)
#define Reactivity mix(0.0, 3.0, iMouse.y)
#define PaletteShift iMouse.z
#define FogDistance mix(8.0, 60.0, iMouse.w)

// Camera
const float Speed = 1.2; // var: { "min": 0.0, "max": 6.0, "step": 0.05, "name": "Forward Speed", "desc": "How fast the camera glides forward", "unit": "m/s" }
const float LookSpeed = 0.35; // var: { "min": 0.0, "max": 2.0, "step": 0.01, "name": "Look Speed", "desc": "How fast the camera looks around", "unit": "x" }
const float LookAmount = 0.6; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Look Amount", "desc": "How far the camera turns left / right", "unit": "x" }
// Room
const float RoomHeight = 4.5; // var: { "min": 2.5, "max": 10.0, "step": 0.1, "name": "Room Height", "desc": "Floor to ceiling height", "unit": "m" }
const float PillarDensity = 0.65; // var: { "min": 0.1, "max": 1.0, "step": 0.01, "name": "Pillar Density", "desc": "Share of grid cells holding a pillar", "scale": 100, "unit": "%" }
const float PillarRadius = 0.28; // var: { "min": 0.05, "max": 0.8, "step": 0.01, "name": "Pillar Radius", "desc": "Thickness of the neon columns", "unit": "m" }
const vec3 FloorColor = vec3(0.08, 0.08, 0.09); // var: { "type": "col", "name": "Floor Color", "desc": "Satin floor tiles" }
const vec3 CeilingColor = vec3(0.36, 0.35, 0.33); // var: { "type": "col", "name": "Ceiling Color", "desc": "Concrete ceiling panels" }
const float FloorGloss = 0.3; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Floor Gloss", "desc": "Strength of the floor reflections", "unit": "x" }
const float FloorRoughness = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Floor Roughness", "desc": "Blur of the reflections (grows with distance), higher = more diffuse floor" }
// Light & audio
const float GlowWidth = 0.2; // var: { "min": 0.05, "max": 1.5, "step": 0.01, "name": "Glow Width", "desc": "Width of the haze around each pillar", "unit": "m" }
const float BaseGlow = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Idle Glow", "desc": "Pillar brightness in silence" }
const float PillarSheen = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.01, "name": "Pillar Sheen", "desc": "Brightness of the unlit glass / metal caps (what idle pillars show without band light), 0 = black", "unit": "x" }
const vec3 PillarTint = vec3(0.275, 0.275, 0.275); // var: { "type": "col", "name": "Pillar Tint", "desc": "Color of the unlit glass / metal sheen of the pillars" }
const float Sensitivity = 0.4; // var: { "min": 0.0, "max": 0.8, "step": 0.01, "name": "Noise Floor", "desc": "Band level below which pillars stay idle (raise for loud music)" }
const vec3 PalettePhase = vec3(0.0, 0.33, 0.67); // var: { "name": "Palette Phase", "desc": "Cosine palette phase per channel (spectrum colors)" }
const float Exposure = 1.1; // var: { "min": 0.2, "max": 4.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
// Fog
const vec3 FogColor = vec3(0.01, 0.008, 0.02); // var: { "type": "col", "name": "Fog Color", "desc": "Color of the far distance (when not transparent)" }
const float Transparency = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Transparency", "desc": "0 = fade to Fog Color, 1 = fade to transparent", "scale": 100, "unit": "%" }
// Post
const float DitherNoise = 3.0; // var: { "min": 0.0, "max": 8.0, "step": 0.1, "name": "Dither Noise", "desc": "Fine per pixel noise against banding, in 8-bit steps (1 = minimum, 0 = off)", "unit": "steps" }
const float FilmGrain = 0.25; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Film Grain", "desc": "Visible animated grain, stronger in the dark areas", "scale": 100, "unit": "%" }

const float CELL = 4.0;
#define ZERO min(iFrame, 0)
uint gL[32];               // band lights, rgb shared exponent (see encodeLight)
vec3 bandLight(int b) {
    uint u = gL[b];
    return vec3(uvec3(u, u >> 9, u >> 18) & 511u) * (exp2(float(int(u >> 27) - 20)) / 511.0);
}
vec3 gAmbient = vec3(0.0);

float pathX(float z) { return 5.0 * sin(z * 0.06) + 2.5 * sin(z * 0.13 + 1.0); }
float pathDX(float z) { return 0.3 * cos(z * 0.06) + 0.325 * cos(z * 0.13 + 1.0); }

float fogAt(float t) { float x = t / FogDistance; return exp(-2.5 * x * x); }

// candidate pillar of a cell (cheap hash only)
bool cellPillar(vec2 c, out vec2 pos, out int band) {
    vec4 h = EwHash2To4(c + 0.37);
    pos = (c + 0.5 + (h.xy - 0.5) * 0.5) * CELL;
    band = min(int(h.z * 32.0), 31);
    return h.w < PillarDensity;
}
// pillars keep the camera path free
bool pathFree(vec2 pos) { return abs(pos.x - pathX(pos.y)) > 2.2 + PillarRadius; }

bool getPillar(vec2 c, out vec2 pos, out int band) {
    return cellPillar(c, pos, band) && pathFree(pos);
}

// light of nearby pillars on a floor / ceiling point (3x3 cells)
vec3 lightAt(vec2 p) {
    vec2 c0 = floor(p / CELL);
    vec3 s = gAmbient;
    for (int j = -1; j <= ZERO + 1; j++)
    for (int i = -1; i <= ZERO + 1; i++) {
        vec2 pos; int b;
        if (!cellPillar(c0 + vec2(i, j), pos, b)) continue;
        float d = max(length(p - pos) - PillarRadius, 0.0);
        if (d >= 3.2 || !pathFree(pos)) continue;
        s += bandLight(b) * (1.0 / (1.0 + d * d * 2.0)) * smoothstep(3.2, 1.0, d);
    }
    return s;
}
// shaded pillar surface (direct view and floor reflection): dark glass, neon by band light
// bw = vertical blur width (rough reflections soften caps / rings)
vec3 pillarShade(int b, float facing, float y, float H, float bw) {
    vec3 L = bandLight(b);
    float m = max(max(L.r, L.g), L.b);
    float f2 = facing * facing, f4 = f2 * f2;
    vec3 base = PillarTint * PillarSheen;       // unlit glass / metal shading (no band light)
    vec3 neon = 0.008 * base + L * (0.55 + 0.6 * facing) + (m * 0.35 + 0.02 * base) * f4;
    float e = 0.015 + bw;
    float cap = max(smoothstep(0.315 + e, 0.315 - e, y), smoothstep(H - 0.315 - e, H - 0.315 + e, y));
    float rw = 0.03 + bw;
    float ring = smoothstep(rw, 0.0, abs(abs(y - 0.5 * H) - (0.5 * H - 0.4))) * (0.03 / rw);
    vec3 metal = 0.02 * base + L * 0.12 * (1.0 - facing) + 0.05 * base * (f4 * f4);
    return mix(neon, metal, cap) + L * ring * 0.8;
}

vec3 ceilingShade(vec2 xz) {
    return CeilingColor * 0.75 * lightAt(xz) * 0.8;
}

// floor reflection: walks the pillar grid along the ray (xz DDA), front to back,
// analytic pillar hits with a roughness cone, analytic haze integral, ceiling at the end
vec3 reflectScene(vec3 o, vec3 r, float tMaxR, float tOff, float H) {
    float k = max(length(r.xz), 1e-4);
    vec2 u = r.xz / k;
    u = vec2(abs(u.x) < 1e-5 ? 1e-5 : u.x, abs(u.y) < 1e-5 ? 1e-5 : u.y);
    float Lh = tMaxR * k;                       // horizontal reach
    vec2 o2 = o.xz;
    vec2 cell = floor(o2 / CELL);
    vec2 st = sign(u);
    vec2 tDelta = abs(CELL / u);
    vec2 tNext = ((cell + max(st, 0.0)) * CELL - o2) / u;
    float rough = FloorRoughness * 0.25;
    float w = GlowWidth;
    vec3 acc = vec3(0.0);
    float A = 0.0;                              // accumulated opacity
    float tc0 = 0.0;
    float jr = 0.25 * CELL * (abs(u.x) + abs(u.y)) + 1e-3;   // max projected pillar jitter
    for (int i = ZERO; i < 14; i++) {
        if (tc0 > Lh || A > 0.98) break;
        float tExit = min(tNext.x, tNext.y);
        // 3x3 neighborhood: glow / blurred edges reach beyond the cells the ray crosses.
        // Each pillar is counted once: by the visited cell holding its closest point on the ray.
        // conservative pre-test (cell center ± max jitter) skips cells before hashing
        for (int j = -1; j <= ZERO + 1; j++)
        for (int m = -1; m <= ZERO + 1; m++) {
            vec2 c = cell + vec2(m, j);
            float tcC = dot((c + 0.5) * CELL - o2, u);
            if (clamp(tcC + jr, 0.0, Lh) < tc0 || clamp(tcC - jr, 0.0, Lh) >= tExit) continue;
            vec2 pos; int b;
            if (!cellPillar(c, pos, b)) continue;
            vec2 oc = pos - o2;
            float tc = dot(oc, u);
            float tcc = clamp(tc, 0.0, Lh);
            if (tcc < tc0 || tcc >= tExit || !pathFree(pos)) continue;
            float ap = abs(oc.x * u.y - oc.y * u.x);
            vec3 L = bandLight(b);
            // haze: integral of we^2 / (we^2 + dist^2) along [0, Lh]; width grows with the
            // reflection blur (energy kept: scaled by w / we)
            float bc = rough * max(tc, 0.0) / k;
            float we = sqrt(w * w + bc * bc);
            float dp = max(ap - PillarRadius, 0.0);
            float a = sqrt(we * we + dp * dp);
            float th = tc - sqrt(max(PillarRadius * PillarRadius - ap * ap, 0.0));
            float sEnd = min(Lh, max(th, 0.0) + 0.5 * PillarRadius) - tc;
            float hz = w * we / a * max(atan(sEnd / a) - atan(-tc / a), 0.0) / k;
            acc += (1.0 - A) * L * hz * Haze * 0.4 * fogAt(tOff + max(tc, 0.0) / k);
            // pillar body with a soft edge growing with distance (roughness)
            float thc = max(th, tc - PillarRadius);
            if (tc > 0.0 && thc < Lh) {
                float t3 = max(thc, 0.0) / k;
                float y = o.y + r.y * t3;
                float br = min(rough * t3 + 0.002, 0.6);
                float cov = smoothstep(PillarRadius + br, PillarRadius - br, ap) * step(y, H);
                if (cov > 0.0) {
                    vec2 hp = o2 + u * max(thc, 0.0);
                    vec2 n = normalize(hp - pos + 1e-5);
                    float facing = clamp(dot(n, -u), 0.0, 1.0);
                    vec3 c = pillarShade(b, facing, y, H, br);
                    acc += (1.0 - A) * cov * c * fogAt(tOff + t3);
                    A += (1.0 - A) * cov;
                }
            }
        }
        // next cell
        if (tNext.x < tNext.y) { tc0 = tNext.x; tNext.x += tDelta.x; cell.x += st.x; }
        else                   { tc0 = tNext.y; tNext.y += tDelta.y; cell.y += st.y; }
    }
    // ceiling seen in the floor when the ray reaches it
    vec3 ce = o + r * tMaxR;
    if (ce.y > H - 0.05) acc += (1.0 - A) * ceilingShade(ce.xz) * fogAt(tOff + tMaxR);
    return acc;
}

// primary ray: sphere tracing the 2x2 nearest pillars (cached until the cell block changes),
// accumulating their haze
float trace(vec3 ro, vec3 rd, float tMax, float jit, out vec3 glow, out int band, out vec2 hitPos) {
    float k = max(length(rd.xz), 1e-3);
    float t = jit * 0.3;
    glow = vec3(0.0); band = -1; hitPos = vec2(0.0);
    float iw = 1.0 / GlowWidth;
    vec2 base = vec2(1e9);
    vec2 cp[4]; vec3 cl[4]; int cb[4]; bool cv[4];
    for (int i = 0; i < 4; i++) { cp[i] = vec2(0.0); cl[i] = vec3(0.0); cb[i] = 0; cv[i] = false; }
    for (int s = ZERO; s < 110; s++) {
        if (t >= tMax) break;
        vec2 p = ro.xz + rd.xz * t;
        vec2 nb = floor(p / CELL - 0.5);
        if (nb != base) {
            base = nb;
            for (int i = 0; i < 4; i++) {
                vec2 pos; int b;
                cv[i] = getPillar(base + vec2(i & 1, i >> 1), pos, b);
                cp[i] = pos; cb[i] = b; cl[i] = bandLight(b);
            }
        }
        float d = 1e5; int hb = 0; vec2 hp = vec2(0.0);
        vec3 g = vec3(0.0);
        for (int i = 0; i < 4; i++) {
            if (!cv[i]) continue;
            float di = length(p - cp[i]) - PillarRadius;
            if (di < d) { d = di; hb = cb[i]; hp = cp[i]; }
            float x = max(di, 0.0) * iw;
            g += cl[i] * (1.0 / (1.0 + x * x)) * smoothstep(2.6, 1.0, di);
        }
        if (d < 0.003) { band = hb; hitPos = hp; break; }
        float dt = clamp(d / k, 0.01, 0.7);
        glow += g * fogAt(t) * min(dt, tMax - t);
        t += dt;
    }
    return min(t, tMax);
}
// ---- vertex stage: the 32 band lights (same for every pixel), packed 4 per flat varying
flat varying uvec4 vL0, vL1, vL2, vL3, vL4, vL5, vL6, vL7;
flat varying vec3 vAmb;    // ambient (sum of the band lights)
flat varying vec3 vRo;     // camera position
varying vec3 vRay;         // camera * vec3(uv, 1.1), affine in fragCoord

// audio2: band i of the 32 band log spectrum (20 Hz .. 20 kHz), tilt lifts the quieter highs
float bandLevel(int i) {
    float fi = float(i);
    float v = textureLod(iChannel0, vec2(EwAudioGet32u(i), EwAudioFrequency32v), 0.0).r * (1.0 + 0.5 * fi / 31.0);
    float e = smoothstep(Sensitivity, Sensitivity + 0.6, v);
    return e * e;
}
// rgb with a shared exponent: 3 x 9 bit mantissa + 5 bit exponent
uint encodeLight(vec3 L) {
    float m = max(max(L.r, L.g), max(L.b, 1e-6));
    int e = clamp(int(ceil(log2(m) + 1e-4)), -20, 11);
    uvec3 q = uvec3(clamp(L * exp2(float(-e)) * 511.0 + 0.5, 0.0, 511.0));
    return q.r | (q.g << 9) | (q.b << 18) | (uint(e + 20) << 27);
}

void vertexMain(vec2 fragCoord) {
    float T = mod(iTime, 3600.0);
    // audio: 32 bands, colors from a cosine palette
    // palette / idle phases advance by fixed angles per band: rotation recurrences instead of cos / sin
    const float dA = EW_TAU * 0.85 / 32.0;
    vec2 rot = vec2(cos(dA), sin(dA));
    vec3 ang = EW_TAU * (PaletteShift + PalettePhase);
    vec3 pc = cos(ang), ps = sin(ang);
    vec2 iRot = vec2(cos(1.7), sin(1.7));
    vec2 iv = vec2(sin(T * 0.6), cos(T * 0.6));              // (sin, cos) of the idle phase
    uint U[32];
    vec3 amb = vec3(0.0);
    for (int i = ZERO; i < 32; i++) {
        float e = bandLevel(i);
        vec3 c = 0.5 + 0.5 * pc;
        c = c * c * 1.3 + 0.02;
        float idle = BaseGlow * (0.75 + 0.25 * iv.x);
        // idle pillars keep their full band color (dim via Idle Glow); the energy adds brightness
        vec3 L = c * (idle + Reactivity * e * 1.2);
        U[i] = encodeLight(L);
        amb += L;
        // advance: cos(a + dA) / sin(a + dA), sin(b + 1.7)
        vec3 npc = pc * rot.x - ps * rot.y;
        ps = ps * rot.x + pc * rot.y;
        pc = npc;
        iv = vec2(iv.x * iRot.x + iv.y * iRot.y, iv.y * iRot.x - iv.x * iRot.y);
    }
    vAmb = amb * 0.002;
    vL0 = uvec4(U[0], U[1], U[2], U[3]);     vL1 = uvec4(U[4], U[5], U[6], U[7]);
    vL2 = uvec4(U[8], U[9], U[10], U[11]);   vL3 = uvec4(U[12], U[13], U[14], U[15]);
    vL4 = uvec4(U[16], U[17], U[18], U[19]); vL5 = uvec4(U[20], U[21], U[22], U[23]);
    vL6 = uvec4(U[24], U[25], U[26], U[27]); vL7 = uvec4(U[28], U[29], U[30], U[31]);

    // camera: forward along a winding free path, looking around
    float z = T * Speed;
    vec3 ro = vec3(pathX(z), 1.6, z);
    float yaw = atan(pathDX(z)) + LookAmount * (0.8 * sin(T * LookSpeed) + 0.35 * sin(T * LookSpeed * 2.3 + 1.3));
    float pitch = 0.08 * sin(T * LookSpeed * 0.7 + 0.5) - 0.04;
    vec3 fwd = vec3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch));
    mat3 cam = EwCameraLookAt(ro, ro + fwd, 0.03 * sin(T * LookSpeed * 0.9));
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    vRo = ro;
    vRay = cam * vec3(uv, 1.1);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    float T = mod(iTime, 3600.0);

    // ---- audio: 32 band lights (levels + palette computed in the vertex stage), 4 packed per flat varying
    uvec4 P[8] = uvec4[8](vL0, vL1, vL2, vL3, vL4, vL5, vL6, vL7);
    for (int i = 0; i < 8; i++) {
        gL[4 * i] = P[i].x; gL[4 * i + 1] = P[i].y; gL[4 * i + 2] = P[i].z; gL[4 * i + 3] = P[i].w;
    }
    gAmbient = vAmb;

    // ---- camera (vertex stage): position + unnormalized pinhole ray
    vec3 ro = vRo;
    vec3 rd = normalize(vRay);

    float H = RoomHeight;
    float tFloor = rd.y < 0.0 ? -ro.y / rd.y : 1e5;
    float tCeil = rd.y > 0.0 ? (H - ro.y) / rd.y : 1e5;
    float tFar = FogDistance * 1.25;
    float tMax = min(min(tFloor, tCeil), tFar);
    float jit = EwHash2To1(fragCoord);

    vec3 glow; int band; vec2 hpos;
    float t = trace(ro, rd, tMax, jit, glow, band, hpos);
    vec3 p = ro + rd * t;
    vec3 col = vec3(0.0);

    if (band >= 0) {
        // ---- neon pillar
        vec3 n = normalize(vec3(p.x - hpos.x, 0.0, p.z - hpos.y));
        col = pillarShade(band, clamp(dot(n, -rd), 0.0, 1.0), p.y, RoomHeight, 0.0);
    } else if (t < tFar - 1e-3) {
        float aa = clamp(t * 1.5 / (iResolution.y * max(abs(rd.y), 0.06)), 0.002, 0.25);
        vec3 La = lightAt(p.xz);
        if (p.y < 0.5 * H) {
            // ---- glossy tiled floor
            vec2 q = p.xz;
            vec2 tile = floor(q);
            vec2 f = fract(q) - 0.5;
            float grout = smoothstep(0.485 - aa, 0.485, max(abs(f.x), abs(f.y)));
            float tv = EwHash2To1(tile + 11.0);
            vec3 albedo = mix(FloorColor * (0.75 + 0.5 * tv), FloorColor * 0.35, grout);
            col = albedo * La;

            vec2 tilt = (EwHash2To2(tile) - 0.5) * 0.012 * (1.0 - grout);
            vec3 n = normalize(vec3(tilt.x, 1.0, tilt.y));
            vec3 rr = reflect(rd, n);
            rr.y = max(rr.y, 0.02);
            float tMaxR = min((H - 0.01) / rr.y, max(tFar - t, 0.0));
            // mirrored scene, analytic: pillars (soft edges = roughness cone), haze, ceiling
            vec3 refl = reflectScene(vec3(p.x, 0.0, p.z), rr, tMaxR, t, H);
            float fm = 1.0 - clamp(-rd.y, 0.0, 1.0);
            float fm2 = fm * fm;
            float F = 0.04 + 0.96 * fm2 * fm2 * fm;
            col += refl / max(fogAt(t), 1e-3) * FloorGloss * mix(0.25, 0.9, F) * (1.0 - 0.7 * grout);
        } else {
            // ---- concrete ceiling panels
            vec2 q = p.xz / 2.5;
            vec2 tile = floor(q);
            vec2 f = fract(q) - 0.5;
            float groove = smoothstep(0.48 - aa, 0.48, max(abs(f.x), abs(f.y)));
            float nz = 0.65 + 0.25 * EwValueNoise2D(p.xz * 2.3) + 0.15 * EwValueNoise2D(p.xz * 11.0);
            vec3 albedo = CeilingColor * nz * (0.85 + 0.25 * EwHash2To1(tile + 3.0));
            albedo *= 1.0 - 0.6 * groove;
            col = albedo * La * 0.8;
        }
    }

    float fg = fogAt(t);
    vec3 hdr = col * fg + glow * Haze * 0.4 + gAmbient * Haze * 0.3 * (1.0 - fg);
    vec3 rgb = EwLinearToSrgb(EwTonemapAces(hdr * Exposure));
    float opaque = 1.0 - Transparency;
    rgb += FogColor * (1.0 - fg) * opaque;
    float alpha = fg + (1.0 - fg) * opaque;
    // anti banding: per channel dither (in 8-bit steps) + soft film grain (more in the darks)
    rgb += vec3(EwDither(fragCoord, T, DitherNoise),
                EwDither(fragCoord + 71.3, T, DitherNoise),
                EwDither(fragCoord + 143.7, T, DitherNoise));
    float gr = EwFilmGrain(fragCoord, T, 1.0, 1.0) - 1.0;
    rgb += gr * FilmGrain * (0.25 + 0.75 * (1.0 - max(max(rgb.r, rgb.g), rgb.b))) * 0.1;
    rgb = clamp(rgb, 0.0, 1.0);
    alpha = clamp(max(alpha, max(max(rgb.r, rgb.g), rgb.b)), 0.0, 1.0);
    fragColor = vec4(rgb, alpha);
}
