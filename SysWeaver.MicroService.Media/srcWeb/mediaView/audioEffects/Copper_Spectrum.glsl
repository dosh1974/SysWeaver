//  media: audio2 stock:YuE2_00004.flac

// Copper Spectrum: polished copper rods driven by the music spectrum, standing on a glossy white tile floor.
//  iMouse.x = Rod Height | Height gain of the spectrum rods | 0.5 | 0.2 .. 1.8 | x
//  iMouse.y = Roughness | Copper micro surface: mirror polished .. satin brushed | 0.3 | 0.04 .. 0.6
//  iMouse.z = Floor Gloss | Strength and sharpness of the floor reflections | 0.6 | 0 .. 1
//  iMouse.w = Light Angle | Key light direction around the rods | 0.7 | -90 .. 90 | °

#define HeightGain  mix(0.2, 1.8, iMouse.x)
#define CopperRough mix(0.04, 0.6, iMouse.y)
#define FloorGloss  mix(0.0, 1.0, iMouse.z)
#define LightAngle  mix(-90.0, 90.0, iMouse.w)

// Copper
const vec3 CopperColor = vec3(0.95, 0.5, 0.3); // var: { "type": "col", "name": "Copper Color", "desc": "Metal reflectance color (F0) of the rods" }
const float Anisotropy = 0.9; // var: { "min": 0.0, "max": 0.95, "step": 0.01, "name": "Anisotropy", "desc": "Brushed look: stretches the highlights along the rods", "scale": 100, "unit": "%" }
const float Tarnish = 0.35; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Tarnish", "desc": "Blotchy darker, rougher patches on the copper", "scale": 100, "unit": "%" }
const float MaxHeight = 1.5; // var: { "min": 0.5, "max": 4.0, "step": 0.05, "name": "Max Height", "desc": "Rod height at full spectrum level" }
const float Smoothing = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Smoothing", "desc": "Calmer rods: blends each band with its time smoothed value and its neighbours (less jitter, less punch)", "scale": 100, "unit": "%" }
const float RodRadius = 0.08; // var: { "min": 0.02, "max": 0.085, "step": 0.005, "name": "Rod Radius", "desc": "Thickness of the rods" }
// Floor
const vec3 FloorColor = vec3(0.93, 0.92, 0.9); // var: { "type": "col", "name": "Tile Color", "desc": "Color of the glazed tiles" }
const vec3 GroutColor = vec3(0.6, 0.6, 0.6); // var: { "type": "col", "name":   "Grout Color", "desc": "Color of the joints between the tiles" }
const float TileSize = 0.6; // var: { "min": 0.2, "max": 2.0, "step": 0.05, "name": "Tile Size", "desc": "Size of one floor tile" }
// Light and scene
const vec3 BackgroundColor = vec3(1.0, 0.985, 0.96); // var: { "type": "col", "name": "Background", "desc": "Color of the bright void the floor fades into" }
const float KeyIntensity = 2.4; // var: { "min": 0.0, "max": 6.0, "step": 0.1, "name": "Key Light", "desc": "Strength of the warm key light (shadows)" }
const float RimIntensity = 1.2; // var: { "min": 0.0, "max": 4.0, "step": 0.1, "name": "Fill Light", "desc": "Strength of the cool fill light on the other side" }
const float ShadowSharpness = 15.0; // var: { "min": 2.0, "max": 32.0, "step": 0.5, "name": "Shadow Sharpness", "desc": "Low: soft wide penumbra, high: crisp shadows" }
const float FogDensity = 0.09; // var: { "min": 0.0, "max": 0.4, "step": 0.005, "name": "Fade", "desc": "How fast the tiles fade into the white background" }
const float Exposure = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness" }
// Camera
const float CameraMotion = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Camera Motion", "desc": "Amount of the slow camera drift" }
const float CameraSpeed = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Camera Speed", "desc": "Speed of the camera drift" }
// Woofers and waveform
const float WooferSize = 0.9; // var: { "min": 0.4, "max": 1.6, "step": 0.05, "name": "Woofer Size", "desc": "Radius of the two background speakers" }
const float WooferOpacity = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Woofer Strength", "desc": "How visible the dim speakers are (0 hides them)", "scale": 100, "unit": "%" }
const float WooferBeatGrow = 0.25; // var: { "min": 0.0, "max": 0.3, "step": 0.01, "name": "Beat Grow", "desc": "How much the speakers swell in size on bass hits", "scale": 100, "unit": "%" }
const float BassGain = 1.0; // var: { "min": 0.25, "max": 3.0, "step": 0.05, "name": "Bass Gain", "desc": "Scales the bass level driving the woofers: higher reacts stronger / to quieter bass", "unit": "x" }
const float BassThreshold = 0.45; // var: { "min": 0.0, "max": 0.9, "step": 0.01, "name": "Bass Threshold", "desc": "Bass level where the woofers start to react: lower reacts to more, higher only to strong kicks" }
const float WooferPump = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Bass Pump", "desc": "How strongly the speaker cones push and ripple with the bass" }
const vec3 WaveColor = vec3(0.45, 0.22, 0.12); // var: { "type": "col", "name": "Wave Color", "desc": "Color of the floating waveform line" }
const float WaveOpacity = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Wave Strength", "desc": "Opacity of the waveform line (0 hides it)", "scale": 100, "unit": "%" }
const float WaveHeight = 0.9; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Wave Height", "desc": "Amplitude of the waveform curve" }
const float WaveWidth = 0.02; // var: { "min": 0.004, "max": 0.05, "step": 0.001, "name": "Line Width", "desc": "Thickness of the waveform line" }
// Quality
const int AA = 2; // var: { "min": 1, "max": 3, "step": 1, "name": "Anti-Aliasing", "desc": "Samples per pixel axis: 1 fast (jagged), 2 smooth edges (4x cost), 3 very smooth (9x cost)", "format": "{0}x{0}" }

// one rod per band of the 32 band audio2 spectrum
#define NR 32
const float Spacing = 0.2;

float gH[NR];
float gMaxH = 0.0;
vec3 gKeyDir = vec3(0.0, 1.0, 0.0);
vec3 gRimDir = vec3(0.0, 1.0, 0.0);
float gKeyAz = 0.0;
const vec3 KeyColor = vec3(1.0, 0.94, 0.86);
float gBass = 0.0;
float gTime = 0.0;
// layer depths behind the rods and the height of the waveform line
const float WooferZ = 3.6;
const float WaveZ = 1.1;
const float WaveBase = 1.15;
const vec3 RimColor = vec3(0.8, 0.9, 1.1);

float rodCenter(int i) { return -(float(i) + 0.5 - float(NR) * 0.5) * Spacing; }

float rodDist(vec3 p, int i) {
    vec3 q = p - vec3(rodCenter(i), 0.0, 0.0);
    float top = max(gH[i] - RodRadius, 0.0);
    q.y -= clamp(q.y, -1.0, top);
    return length(q) - RodRadius;
}

int rodIndex(float x) {
    // the camera looks along +z, screen right = -x: bass on the left
    return int(clamp(floor(-x / Spacing + float(NR) * 0.5), 0.0, float(NR - 1)));
}

float mapRods(vec3 p) {
    int i = rodIndex(p.x);
    int j = clamp(i + (p.x > rodCenter(i) ? -1 : 1), 0, NR - 1);
    return min(rodDist(p, i), rodDist(p, j));
}

float map(vec3 p) { return min(mapRods(p), p.y); }
#define EW_SDF(p) map(p)

vec3 dirFromAngles(float az, float el) {
    return vec3(sin(az) * cos(el), sin(el), cos(az) * cos(el));
}

// background the floor fades into (also what the floor mirrors above the horizon)
vec3 bgColor(vec3 rd) {
    return BackgroundColor * mix(1.6, 1.15, smoothstep(0.0, 0.6, rd.y));
}

// studio environment seen in the copper: bright horizon, dark ceiling, two soft boxes, lit floor
vec3 env(vec3 r, float rough) {
    float w = 0.04 + rough * 0.7;
    float az = atan(r.x, r.z);
    vec3 sky = BackgroundColor * mix(0.75, 0.05, smoothstep(0.0, 0.4, r.y));
    vec3 lower = mix(BackgroundColor * 0.6, FloorColor * 0.3, smoothstep(0.0, 0.25, -r.y));
    vec3 c = mix(lower, sky, smoothstep(-w * 0.3, w * 0.3, r.y));
    // dark flags left / right (negative fill) give the metal contrast
    c *= mix(1.0, 0.25, smoothstep(0.35, 0.85, abs(r.x)) * (1.0 - smoothstep(0.5, 0.9, r.y)));
    // tall soft boxes standing on the floor: warm key, cool rim
    float d1 = abs(mod(az - gKeyAz + EW_PI, EW_TAU) - EW_PI);
    float sb1 = (1.0 - smoothstep(0.28 - w, 0.28 + w, d1))
              * smoothstep(-0.3 - w, -0.3 + w, r.y) * (1.0 - smoothstep(0.75 - w, 0.75 + w, r.y));
    float d2 = abs(mod(az - gKeyAz - 2.7 + EW_PI, EW_TAU) - EW_PI);
    float sb2 = (1.0 - smoothstep(0.14 - w, 0.14 + w, d2))
              * smoothstep(-0.2 - w, -0.2 + w, r.y) * (1.0 - smoothstep(0.9 - w, 0.9 + w, r.y));
    // thin overhead strip light
    float st = (1.0 - smoothstep(0.03, 0.03 + w, abs(r.z + 0.25))) * smoothstep(0.75 - w, 0.85, r.y);
    float soft = 1.0 / (1.0 + 4.0 * w);   // blurred boxes spread their energy
    c += (KeyColor * (6.0 * sb1 * KeyIntensity / 2.4) + RimColor * (4.0 * sb2 * RimIntensity / 1.2) + vec3(1.5) * st) * soft;
    return c;
}

// anisotropic GGX specular of one light (radiance * BRDF * cos)
vec3 specLight(vec3 n, vec3 v, vec3 t, vec3 b, vec3 L, float at, float ab, vec3 f0) {
    float nl = dot(n, L);
    if (nl <= 0.0) return vec3(0.0);
    float nv = max(dot(n, v), 1e-3);
    vec3 h = normalize(L + v);
    float th = dot(h, t) / at, bh = dot(h, b) / ab, nh = dot(h, n);
    float dd = th * th + bh * bh + nh * nh;
    float D = 1.0 / (EW_PI * at * ab * dd * dd);
    float k = sqrt(at * ab) * 0.5;
    float G = nl / (nl * (1.0 - k) + k) * nv / (nv * (1.0 - k) + k);
    vec3 F = EwFresnelSchlick(max(dot(v, h), 0.0), f0);
    return min(D * G * F / (4.0 * nv), vec3(60.0));
}

vec3 shadeCopper(vec3 p, vec3 n, vec3 v, float sh, float ao, float id) {
    // tangent frame: t along the rod axis, b around it
    vec3 b = cross(n, vec3(0.0, 1.0, 0.0));
    float bl = length(b);
    b = bl > 1e-3 ? b / bl : vec3(1.0, 0.0, 0.0);
    vec3 t = cross(b, n);
    // micro surface: tarnish blotches + fine lathe rings modulate roughness and tint
    float ang = atan(n.x, n.z);
    float blot = EwValueNoise2D(vec2(ang * 1.5 + id * 7.31, p.y * 5.0 + id * 3.7));
    blot = smoothstep(0.45, 0.85, blot) * Tarnish;
    float rings = EwValueNoise2D(vec2(p.y * 70.0, id * 1.7));
    float r = clamp(CopperRough * (0.85 + 0.25 * rings) + blot * 0.25, 0.02, 1.0);
    float a = r * r;
    float at = max(a * (1.0 + 4.0 * Anisotropy), 0.002);
    float ab = max(a * (1.0 - 0.75 * Anisotropy), 0.002);
    vec3 f0 = mix(CopperColor, CopperColor * vec3(0.7, 0.5, 0.42), blot);

    vec3 col = specLight(n, v, t, b, gKeyDir, at, ab, f0) * KeyColor * KeyIntensity * sh;
    col += specLight(n, v, t, b, gRimDir, at, ab, f0) * RimColor * RimIntensity;

    // anisotropic environment: 3 taps stretched along the rod axis
    vec3 R = reflect(-v, n);
    float s = (at - ab) * 1.5;
    vec3 e = env(R, r) * 0.4
           + (env(normalize(R + t * s), r) + env(normalize(R - t * s), r)) * 0.3;
    float nv = max(dot(n, v), 0.0);
    vec3 Fe = EwFresnelSchlick(nv, f0);
    // multiple scattering between the micro facets: rougher copper keeps more (and more saturated) energy
    Fe *= 1.0 + f0 * r * 0.8;
    // specular occlusion: the key soft box is partly blocked where the direct light is shadowed
    float occ = mix(ao, 1.0, 0.35) * mix(1.0, sh, 0.35);
    col += e * Fe * occ;
    return col;
}

vec2 boxHit(vec3 ro, vec3 rd, vec3 bmin, vec3 bmax) {
    vec3 d = mix(rd, vec3(1e-6), lessThan(abs(rd), vec3(1e-6)));
    vec3 inv = 1.0 / d;
    vec3 t0 = (bmin - ro) * inv, t1 = (bmax - ro) * inv;
    vec3 tn = min(t0, t1), tf = max(t0, t1);
    return vec2(max(max(tn.x, tn.y), tn.z), min(min(tf.x, tf.y), tf.z));
}

float fogAt(float t) { return 1.0 - exp(-max(t - 8.5, 0.0) * FogDensity); }

// two dim subwoofers floating in the back, cones pumping with the bass (straight alpha)
vec4 wooferLayer(vec3 ro, vec3 rd, float tMax, float pix) {
    if (rd.z <= 1e-4 || WooferOpacity <= 0.0) return vec4(0.0);
    float t = (WooferZ - ro.z) / rd.z;
    if (t <= 0.0 || t > tMax) return vec4(0.0);
    vec2 q = (ro + rd * t).xy;
    float side = q.x < 0.0 ? -1.0 : 1.0;
    // center from the base size, the speaker itself swells with the beat
    vec2 c = vec2(side * (1.1 + 1.25 * WooferSize), 0.25 + 1.05 * WooferSize);
    float R = WooferSize * (1.0 + WooferBeatGrow * gBass);
    vec2 d = (q - c) / R;
    float r = length(d);
    float aa = pix * t / R * 1.2 + 1e-4;
    if (r > 1.0 + 3.0 * aa) return vec4(0.0);
    float pump = gBass * WooferPump;
    // the cone moves out with the bass: rings inside expand a little
    float rc = r / (1.0 + 0.07 * pump);
    // surround (rubber ring), lit from above
    float shade = 0.45 + 0.2 * d.y / max(r, 1e-3) * smoothstep(0.82, 0.92, r);
    // cone: darker towards the rim, faint pressure ripples on beats
    float cone = mix(0.62, 0.32, smoothstep(0.3, 0.84, rc));
    cone += 0.08 * pump * cos(rc * 26.0 - gTime * 9.0) * smoothstep(0.3, 0.45, rc) * (1.0 - smoothstep(0.7, 0.84, rc));
    shade = mix(cone, shade, smoothstep(0.83, 0.85 + aa, r));
    // dust cap: bulges brighter when pushed
    float cap = 1.0 - smoothstep(0.28 - aa, 0.28 + aa, rc);
    shade = mix(shade, 0.7 + 0.25 * pump - 0.25 * length(d - vec2(-0.08, 0.1)), cap);
    // fine dark grooves between the parts
    float g1 = 1.0 - smoothstep(0.0, 0.012 + aa, abs(r - 0.84));
    float g2 = 1.0 - smoothstep(0.0, 0.012 + aa, abs(rc - 0.29));
    shade *= 1.0 - 0.35 * max(g1, g2);
    vec3 colr = BackgroundColor * shade * (1.0 + 0.25 * pump * cap);
    float a = (1.0 - smoothstep(1.0 - aa, 1.0 + aa, r)) * WooferOpacity;
    return vec4(mix(colr, bgColor(rd), fogAt(t)), a);
}

float waveAt(float u) { return texture(iChannel0, vec2(u, EwAudioWave512v)).r * 2.0 - 1.0; }

// mono waveform as a floating translucent curve line (straight alpha)
vec4 waveLayer(vec3 ro, vec3 rd, float tMax, float pix) {
    if (rd.z <= 1e-4 || WaveOpacity <= 0.0) return vec4(0.0);
    float t = (WaveZ - ro.z) / rd.z;
    if (t <= 0.0 || t > tMax) return vec4(0.0);
    vec2 q = (ro + rd * t).xy;
    float halfW = float(NR) * 0.5 * Spacing + 0.5;
    float u = 0.5 - q.x / (2.0 * halfW);          // screen left .. right
    if (u < 0.0 || u > 1.0) return vec4(0.0);
    float tu = mix(0.04, 0.96, u);
    const float e = 1.5 / 512.0;
    float w0 = waveAt(tu - e), w1 = waveAt(tu), w2 = waveAt(tu + e);
    float env = sqrt(sin(EW_PI * u));              // the line tapers to the ends
    float y0 = WaveBase + WaveHeight * env * (w0 + 2.0 * w1 + w2) * 0.25;
    float slope = WaveHeight * env * (w2 - w0) / (2.0 * e) * 0.92 / (2.0 * halfW);
    float dist = abs(q.y - y0) / sqrt(1.0 + slope * slope);
    float pw = pix * t;
    float hw = WaveWidth * 0.5 * (0.4 + 0.6 * env);
    float core = 1.0 - smoothstep(hw - pw, hw + pw, dist);
    float glow = exp(-dist / (WaveWidth * 3.0)) * 0.3;
    float a = WaveOpacity * max(core, glow) * smoothstep(0.0, 0.06, u) * smoothstep(1.0, 0.94, u);
    vec3 lc = mix(WaveColor * 1.4, WaveColor, core);
    return vec4(mix(lc, bgColor(rd), fogAt(t)), a);
}

vec3 shadeFloor(vec3 p, vec3 rd, float t, float pix) {
    // tiles
    vec2 g = p.xz / TileSize;
    vec2 f = abs(fract(g) - 0.5);
    float edge = max(f.x, f.y);
    float aa = pix * t / TileSize + 1e-4;
    float grout = smoothstep(0.485 - aa, 0.485 + aa, edge);
    float var = EwHash2To1(floor(g));
    vec3 alb = mix(FloorColor * (0.96 + 0.05 * var), GroutColor, grout);

    float nl = max(gKeyDir.y, 0.0);
    float sh = EwSoftShadow(p + vec3(0.0, 0.002, 0.0), gKeyDir, 0.02, 8.0, ShadowSharpness);
    sh = sh * sh * (3.0 - 2.0 * sh);
    float ao = EwAmbientOcclusion(p, vec3(0.0, 1.0, 0.0));
    float dr = mapRods(p);
    float sky = 0.45 * (0.4 + 0.6 * smoothstep(0.0, 0.5, dr)) * ao;
    vec3 diff = alb * (KeyColor * KeyIntensity * nl * sh + BackgroundColor * sky
                     + RimColor * RimIntensity * 0.25 * max(gRimDir.y, 0.0));
    // warm copper bounce light around the rod feet
    diff += alb * CopperColor * 0.35 * exp(-dr * 9.0) * (0.4 + 0.6 * sh);

    // blurry floor reflection: cone trace of the rods along the mirrored ray
    vec3 rr = reflect(rd, vec3(0.0, 1.0, 0.0));
    float cone = mix(0.35, 0.03, FloorGloss);
    float res = 1.0, bestT = 0.02, tt = 0.02;
    for (int k = 0; k < 40; k++) {
        vec3 q = p + rr * tt;
        float d = mapRods(q);
        float c = d / (cone * tt);
        if (c < res) { res = c; bestT = tt; }
        if (res < 0.0 || tt > 7.0 || q.y > gMaxH + 0.3) break;
        tt += clamp(d, 0.015, 0.4);
    }
    vec3 refl = bgColor(rr);
    // woofers and waveform line mirrored (blurred) in the tiles; rods in front are blended below
    float rpix = pix * (1.0 + 60.0 * cone);
    vec4 rw = wooferLayer(p, rr, 1e9, rpix);
    refl = mix(refl, rw.rgb, rw.a);
    vec4 rl = waveLayer(p, rr, 1e9, rpix);
    refl = mix(refl, rl.rgb, rl.a);
    float cov = clamp(1.0 - res, 0.0, 1.0);
    cov = cov * cov * (3.0 - 2.0 * cov);
    if (cov > 0.0) {
        vec3 q = p + rr * bestT;
        int i = rodIndex(q.x);
        vec3 c = vec3(rodCenter(i), clamp(q.y, 0.0, max(gH[i] - RodRadius, 0.0)), 0.0);
        vec3 rn = q - c;
        rn = length(rn) > 1e-4 ? normalize(rn) : vec3(0.0, 0.0, -1.0);
        vec3 rc = shadeCopper(q, rn, -rr, 1.0, 1.0, float(i));
        refl = mix(refl, rc, cov);
    }
    float F = EwFresnelSchlick(max(-rd.y, 0.0), 0.04);
    float ra = clamp(mix(0.04, 0.32, FloorGloss) + 0.4 * F, 0.0, 1.0) * (1.0 - 0.75 * grout);
    return mix(diff, refl, ra);
}

vec3 shadeRod(vec3 p, vec3 rd) {
    vec3 n = EwCalcNormal(p, 0.0006);
    float sh = EwSoftShadow(p + n * 0.003, gKeyDir, 0.02, 8.0, ShadowSharpness);
    sh = sh * sh * (3.0 - 2.0 * sh);
    float ao = EwAmbientOcclusion(p, n);
    return shadeCopper(p, n, -rd, sh, ao, float(rodIndex(p.x)));
}

vec3 render(vec3 ro, vec3 rd, float pix) {
    float tF = rd.y < -1e-4 ? -ro.y / rd.y : 1e5;
    float xr = float(NR) * 0.5 * Spacing + 0.1;
    vec2 bh = boxHit(ro, rd, vec3(-xr, 0.0, -RodRadius - 0.01), vec3(xr, gMaxH + 0.01, RodRadius + 0.01));
    bool hit = false;
    float t = 0.0;
    if (bh.x < bh.y && bh.y > 0.0) {
        t = max(bh.x, 0.0);
        float tEnd = min(bh.y, tF);
        for (int k = 0; k < 100; k++) {
            float d = mapRods(ro + rd * t);
            if (d < 0.0002 * t) { hit = true; break; }
            t += d;
            if (t > tEnd) break;
        }
    }
    vec3 col;
    float tHit = 1e9;
    if (hit) {
        col = shadeRod(ro + rd * t, rd);
        col = mix(col, bgColor(rd), 1.0 - exp(-max(t - 8.5, 0.0) * FogDensity));
        tHit = t;
    } else if (tF < 1e4) {
        vec3 p = ro + rd * tF;
        col = shadeFloor(p, rd, tF, pix);
        col = mix(col, bgColor(rd), 1.0 - exp(-max(tF - 8.5, 0.0) * FogDensity));
        tHit = tF;
    } else {
        col = bgColor(rd);
    }
    // floating layers behind the rods: woofers at the back, the waveform line in front of them
    vec4 wf = wooferLayer(ro, rd, tHit, pix);
    col = mix(col, wf.rgb, wf.a);
    vec4 wl = waveLayer(ro, rd, tHit, pix);
    col = mix(col, wl.rgb, wl.a);
    return col;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    float tm = mod(iTime, 1000.0);

    // spectrum -> rod levels: the 32 log bands of audio2, one band per rod (bass on the left)
    // Smoothing blends the raw band with its time smoothed value
    float lvl[NR];
    for (int i = 0; i < NR; i++) {
        float u = EwAudioGet32u(i);
        float raw = texture(iChannel0, vec2(u, EwAudioFrequency32v)).r;
        float calm = texture(iChannel0, vec2(u, EwAudioFrequency32v + EwAudioDiffuseAddV)).r;
        lvl[i] = mix(raw, calm, Smoothing);
    }
    // light smoothing across neighbouring rods (keeps each band readable)
    for (int i = 0; i < NR; i++) {
        float l = lvl[max(i - 1, 0)], r = lvl[min(i + 1, NR - 1)];
        float nb = (l + r) * 0.25 + lvl[i] * 0.5;
        float lv = mix(lvl[i], nb, Smoothing * 0.5);
        // response curve with a spectral tilt (music is loud in the bass, quiet in the highs):
        // the floor drops from the bass to the treble so all 32 bands get room to move
        float f = float(i) / float(NR - 1);
        float fl = mix(0.42, 0.08, sqrt(f));
        lv = clamp((lv - fl) / 0.6, 0.0, 1.0);
        lv = mix(lv, lv * lv * (3.0 - 2.0 * lv), 0.5);
        float idle = 0.05 * (0.5 + 0.5 * sin(tm * 1.3 + float(i) * 0.45));
        float h = 2.0 * RodRadius + 0.05 + idle + lv * MaxHeight * HeightGain;
        gH[i] = h;
        gMaxH = max(gMaxH, h);
    }
    // bass for the woofers: audio2 bass energy (20 .. 150 Hz), thresholded
    float bass = EwAudioGetBassEnergy(iChannel0);
    gBass = smoothstep(BassThreshold, BassThreshold + 0.5, bass * BassGain);
    gBass = smoothstep(BassThreshold, BassThreshold + 0.5, bass * BassGain);
    gTime = tm;

    // key from behind left: the shadows fall forward onto the tiles
    gKeyAz = 0.75 + radians(LightAngle);
    gKeyDir = dirFromAngles(gKeyAz, 0.55);
    gRimDir = dirFromAngles(gKeyAz + 2.7, 0.6);
    // camera: slow drift
    float cs = tm * CameraSpeed;
    vec3 ro = vec3(sin(cs * 0.7) * 0.5, 1.6 + 0.2 * sin(cs * 0.53), -7.2 + 0.4 * sin(cs * 0.37));
    ro = mix(vec3(0.0, 1.6, -7.2), ro, CameraMotion);
    vec3 ta = vec3(sin(cs * 0.41) * 0.25 * CameraMotion, 0.7, 0.0);
    mat3 cam = EwCameraLookAt(ro, ta, 0.012 * sin(cs * 0.6) * CameraMotion);
    const float focal = 1.9;
    float pix = 1.0 / (iResolution.y * focal);

    // supersampling: AA x AA rotated grid samples per pixel, tone mapped per sample
    // (bright copper highlights do not alias against the white floor)
    vec3 col = vec3(0.0);
    for (int sy = 0; sy < AA; sy++) {
        for (int sx = 0; sx < AA; sx++) {
            vec2 o = (vec2(float(sx), float(sy)) + 0.5) / float(AA) - 0.5;
            o = AA > 1 ? vec2(o.x * 0.9 + o.y * 0.45, o.y * 0.9 - o.x * 0.45) : vec2(0.0);
            vec2 suv = (fragCoord + o - 0.5 * iResolution.xy) / iResolution.y;
            vec3 rd = cam * normalize(vec3(suv, focal));
            col += EwTonemapAces(render(ro, rd, pix) * Exposure * 0.7);
        }
    }
    col /= float(AA * AA);

    col = EwLinearToSrgb(col);
    col *= EwVignette(fragCoord / iResolution.xy, 16.0, 0.2, 0.25);
    col += EwDither(fragCoord, iTime, 1.0);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
