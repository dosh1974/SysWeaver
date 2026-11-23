//  media: image none
// Hot Pepperoni Slice - photoreal procedural pizza slice on a small plate, steaming hot
// Raymarched studio food shot: a slice with a puffy, lumpy crust, melted blistered cheese,
// cupped pepperoni with charred edges and grease, draped over a glazed ceramic plate.
// Physically based shading (GGX specular, Fresnel, wrapped subsurface light in the cheese),
// soft shadows, ambient occlusion and volumetric steam; the plate slowly turns back and forth.
// Output: premultiplied alpha (fully transparent background, no table shadow).
// Live controls:
//  iMouse.x = Steam | Amount of steam rising from the hot slice | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Swing | How far the plate turns back and forth | 0.5 | 0 .. 70 | °
//  iMouse.z = Browning | How toasted crust, cheese and pepperoni are | 0.5 | 0.0 .. 1.0
//  iMouse.w = Light Angle | Direction of the key light around the plate | 0.8 | -90 .. 90 | °

// --- Colors (linear) ---
const vec3 CheeseColor = vec3(0.90, 0.62, 0.22); // var: { "type": "col", "name": "Cheese Color", "desc": "Color of the melted mozzarella" }
const vec3 CrustColor = vec3(0.80, 0.46, 0.17); // var: { "type": "col", "name": "Crust Color", "desc": "Golden color of the baked crust" }
const vec3 PepperoniColor = vec3(0.30, 0.03, 0.014); // var: { "type": "col", "name": "Pepperoni Color", "desc": "Color of the pepperoni slices" }
const vec3 PlateColor = vec3(0.70, 0.70, 0.68); // var: { "type": "col", "name": "Plate Color", "desc": "Color of the glazed ceramic plate" }
const vec3 KeyColor = vec3(1.0, 0.86, 0.70); // var: { "type": "col", "name": "Key Light Color", "desc": "Color of the warm main light" }
const vec3 BlisterColor = vec3(0.42, 0.18, 0.04); // var: { "type": "col", "name": "Blister Color", "desc": "Color of the browned tops of the cheese bubbles" }
const vec3 GreaseColor = vec3(0.9, 0.36, 0.05); // var: { "type": "col", "name": "Grease Color", "desc": "Color of the orange pepperoni grease on the cheese" }
const vec3 CharColor = vec3(0.06, 0.03, 0.015); // var: { "type": "col", "name": "Char Color", "desc": "Color of the charred spots on the crust" }
const vec3 SauceColor = vec3(0.50, 0.05, 0.02); // var: { "type": "col", "name": "Sauce Color", "desc": "Tomato sauce visible at the cut sides" }
const vec3 OreganoColor = vec3(0.05, 0.07, 0.02); // var: { "type": "col", "name": "Oregano Color", "desc": "Color of the herb flecks on the cheese" }

// --- Motion and camera ---
const float SwingPeriod = 14.0; // var: { "min": 4.0, "max": 40.0, "step": 0.5, "name": "Swing Period", "desc": "Duration of one back and forth turn", "unit": "s" }
const float BaseAngle = 75.0; // var: { "min": -180.0, "max": 180.0, "step": 1.0, "name": "Base Angle", "desc": "Middle orientation of the slice", "unit": "°" }
const float Zoom = 1.15; // var: { "min": 0.6, "max": 1.8, "step": 0.01, "name": "Zoom", "desc": "Size of the plate on screen (above ~1.1 the plate gets cropped)", "unit": "x" }
const float CameraElevation = 40.0; // var: { "min": 10.0, "max": 80.0, "step": 1.0, "name": "Camera Elevation", "desc": "How steeply the camera looks down on the plate", "unit": "°" }

// --- Lighting ---
const float Exposure = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float KeyLight = 4.0; // var: { "min": 0.0, "max": 10.0, "step": 0.1, "name": "Key Light", "desc": "Brightness of the main light from behind (shadows, steam glow)" }
const float FillLight = 0.8; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Fill Light", "desc": "Soft studio light from the room and the front" }
const float ShadowSharpness = 10.0; // var: { "min": 2.0, "max": 32.0, "step": 0.5, "name": "Shadow Sharpness", "desc": "Edge of the shadows: low = soft, high = crisp" }
const float AoStrength = 0.5625; // var: { "min": 0.0, "max": 1.0, "step": 0.0025, "name": "Ambient Occlusion", "desc": "Darkening in the creases and under the pepperoni", "scale": 100, "unit": "%" }
const float SubsurfaceStrength = 0.9; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Subsurface Glow", "desc": "Warm light scattering through cheese and pepperoni on the shadow side" }

// --- Steam ---
const float SteamSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Steam Speed", "desc": "Rising speed of the steam", "unit": "x" }
const float SteamHeight = 0.35; // var: { "min": 0.3, "max": 1.2, "step": 0.01, "name": "Steam Height", "desc": "How high the steam rises before it vanishes" }
const float SteamSpread = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Steam Spread", "desc": "How much the steam widens while rising: low = thin wisps above the slice" }
const float SteamGlow = 0.15; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Steam Glow", "desc": "How brightly the steam lights up against the key light" }

// --- Toppings ---
const float PepperoniSize = 1.0; // var: { "min": 0.7, "max": 1.25, "step": 0.01, "name": "Pepperoni Size", "desc": "Size of the pepperoni slices", "unit": "x" }
const float BlisterHeight = 0.005; // var: { "min": 0.0, "max": 0.01, "step": 0.0005, "name": "Blister Height", "desc": "How high the cheese bubbles rise" }
const float BlisterScale = 22.0; // var: { "min": 12.0, "max": 40.0, "step": 0.5, "name": "Blister Density", "desc": "Number of cheese bubbles: higher = more, smaller bubbles" }
const float GreaseAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Grease", "desc": "Orange pepperoni grease pooling on the cheese", "scale": 100, "unit": "%" }
const float GreaseRoughness = 0.07; // var: { "min": 0.03, "max": 0.3, "step": 0.005, "name": "Grease Roughness", "desc": "Roughness of the grease in the pepperoni cups: low = sharp, glossy highlights" }
const float FatAmount = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Pepperoni Fat", "desc": "Visibility of the fat flecks in the pepperoni", "scale": 100, "unit": "%" }
const float OreganoDensity = 0.07; // var: { "min": 0.0, "max": 0.3, "step": 0.005, "name": "Oregano", "desc": "Amount of herb flecks on the cheese" }
const float FlourAmount = 0.35; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Flour Dusting", "desc": "Light flour spots on the crust", "scale": 100, "unit": "%" }

// --- Scene ---
const bool ShowPlate = true; // var: { "name": "Plate", "desc": "Show the small ceramic plate under the slice (without it the slice lies flat)" }
const int Quality = 1; // 0 fast (1 sample), 1 default (2 samples), 2 high (4 samples)

// --- Plate Logo ---
const vec3 LogoColor = vec3(0.28, 0.26, 0.24); // var: { "type": "col", "name": "Logo Color", "desc": "Fixed color for the etched logo when tinted" }
const float LogoColorMix = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Logo Color Mix", "desc": "Mix from original image colors (0%) to the fixed Logo Color (100%)", "scale": 100, "unit": "%" }
const float LogoOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Logo Opacity", "desc": "Strength of the etched logo blended onto the plate", "scale": 100, "unit": "%" }
const float LogoRelief = 0.7; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Logo Relief", "desc": "Depth of the subtle carved relief lighting on the porcelain" }
const float LogoSize = 1.4; // var: { "min": 0.4, "max": 1.4, "step": 0.02, "name": "Logo Size", "desc": "Scale of the etched logo on the plate", "unit": "x" }

#define SteamAmount mix(0.0, 2.0, iMouse.x)
#define Swing radians(mix(0.0, 70.0, iMouse.y))
#define Browning mix(0.0, 1.0, iMouse.z)
#define LightAngle radians(mix(-90.0, 90.0, iMouse.w))

// geometry (structural, scene units: plate radius 0.55)
const float SliceHalfAngle = 0.31;
// apex position in slice units; slice units -> scene units
const float SliceApexZ = 0.31;
const float SliceScale = 1.8;
const vec3 Pep[6] = vec3[6](            // pepperoni: slice coords (x, distance from tip), radius
    vec3( 0.003, 0.195, 0.042 * PepperoniSize),
    vec3(-0.045, 0.315, 0.046 * PepperoniSize),
    vec3( 0.055, 0.330, 0.045 * PepperoniSize),
    vec3( 0.000, 0.430, 0.046 * PepperoniSize),
    vec3(-0.085, 0.483, 0.046 * PepperoniSize),
    vec3( 0.085, 0.488, 0.045 * PepperoniSize));
#define ZERO min(iFrame, 0) // loop start the compiler can't unroll (fast compile)

// 2D Voronoi (same as EwVoronoi2D: x = F1, y = F2, z = cell random) with non unrolled loops (fast compile)
vec3 voronoi(vec2 p)
{
    vec2 cell = floor(p), fr = fract(p);
    float d1 = 8.0, d2 = 8.0;
    vec2 nc = cell;
    for (int y = ZERO - 1; y <= 1; y++)
    for (int x = ZERO - 1; x <= 1; x++) {
        vec2 nb = vec2(float(x), float(y));
        float d = length(nb + EwHash2To2(cell + nb) - fr);
        if (d < d1) { d2 = d1; d1 = d; nc = cell + nb; }
        else if (d < d2) d2 = d;
    }
    return vec3(d1, d2, EwHash2To1(nc));
}

// fbm over value noise (same as EwFbm2D) with a non unrolled loop (fast compile)
float fbm2(vec2 p, int octaves)
{
    float sum = 0.0, amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * EwValueNoise2D(p);
        p = mat2(1.6, 1.2, -1.2, 1.6) * p;
        amp *= 0.5;
    }
    return sum;
}
mat2 gRot = mat2(1.0); // world -> object rotation (turning plate)

// 2D distance d2 extruded to half height h around y = 0
float sdExtrude(float d2, float y, float h)
{
    vec2 w = vec2(d2, abs(y) - h);
    return min(max(w.x, w.y), 0.0) + length(max(w, 0.0));
}

// slice coordinates: tip at the origin, +y towards the crust
vec2 sliceQ(vec3 p) { return vec2(p.x, SliceApexZ - p.z); }

// signed distance to the two cut sides of the wedge (positive outside)
float wedgeSide(vec2 q)
{
    return abs(q.x) * cos(SliceHalfAngle) - q.y * sin(SliceHalfAngle);
}

// irregular outline radius of pepperoni i in direction dq
float pepR(int i, vec2 dq)
{
    float fi = float(i);
    float a = atan(dq.y, dq.x + 1e-7);
    return Pep[i].z * (1.0 + 0.035 * sin(a * 3.0 + fi * 1.7) + 0.02 * sin(a * 7.0 + fi * 4.1) + 0.01 * sin(a * 13.0 + fi * 2.3));
}

// melted cheese: x = surface height, y = blister dome (0..1), z = blister random, w = closeness to a pepperoni
vec4 cheeseInfo(vec2 q)
{
    float n = 0.5 * EwValueNoise2D(q * 9.0 + 1.0) + 0.3 * EwValueNoise2D(q * 23.0 + 3.0) + 0.2 * EwValueNoise2D(q * 57.0 + 9.0);
    float h = 0.0285 + 0.005 * n;
    vec3 v = voronoi(q * BlisterScale + 0.5);
    float br = mix(0.25, 0.55, fract(v.z * 7.31));
    float b = 1.0 - smoothstep(0.0, br, v.x);
    // fade to zero at the cell borders: no cliffs between neighbouring blisters
    b = b * b * (3.0 - 2.0 * b) * smoothstep(0.25, 0.35, v.z) * smoothstep(0.0, 0.25, v.y - v.x);
    h += BlisterHeight * b * (0.4 + 0.6 * fract(v.z * 13.7));
    h += 0.0014 * EwValueNoise2D(q * 140.0);   // fine surface ripples
    // flattened under the pepperoni (they sit on top), grease collects around them
    float fl = 1.0, near = 0.0;
    for (int i = ZERO; i < 6; i++) {
        float rho = length(q - Pep[i].xy);
        float r = Pep[i].z;
        fl = min(fl, smoothstep(r * 0.8, r * 1.07, rho));
        near = max(near, 1.0 - smoothstep(r, r + 0.03, rho));
    }
    h = mix(0.0293, h, fl);
    // melted cheese slumps down towards the cut edges (smooth silhouette and shadow)
    float edge = smoothstep(0.0, -0.03, wedgeSide(q));
    h = mix(0.0255, h, edge);
    fl *= edge;
    return vec4(h, b * fl, v.z, near);
}
// height of the plate surface the slice rests on (world units, radius in object space)
float plateTop(float r)
{
    if (!ShowPlate) return 0.0;
    float o = max(r - 0.55, 0.0);
    return 0.030 * smoothstep(0.40, 0.515, r) + 0.0045 * smoothstep(0.50, 0.55, r) - 1.5 * o * o;
}

// scene: x = distance, y = material (1 plate, 2 dough / crust, 3 cheese, 4 pepperoni)
vec2 map(vec3 pw)
{
    vec3 p = pw;
    p.xz = gRot * p.xz;
    vec2 res = vec2(1e5, 0.0);
    if (ShowPlate) {
        vec2 pr = vec2(length(p.xz), p.y);
        float d = EwSdSegment2D(pr, vec2(0.0, -0.006), vec2(0.42, -0.006));
        d = min(d, EwSdSegment2D(pr, vec2(0.42, -0.006), vec2(0.505, 0.022)));
        d = min(d, EwSdSegment2D(pr, vec2(0.505, 0.022), vec2(0.545, 0.027)));
        d = min(d, EwSdSegment2D(pr, vec2(0.25, -0.012), vec2(0.265, -0.030)));
        res = vec2(d - 0.006, 1.0);
    }
    // the slice drapes over the plate: rides up the rim, sags where it hangs over
    float rp = length(p.xz);
    p.y -= plateTop(rp);
    float bend = rp > 0.38 ? 0.9 : 1.0;   // the bend stretches distances a little
    p /= SliceScale;   // the slice is modelled in its own units
    vec2 q = sliceQ(p);
    float rq = length(q);
    float side = wedgeSide(q);
    float bb = max(max(side, rq - 0.66), max(p.y - 0.095, -q.y - 0.02));
    if (bb > 0.03) return vec2(min(res.x, bb * SliceScale), res.y);

    // dough base
    float dough = sdExtrude(max(side, rq - 0.58) + 0.004, p.y - 0.0105, 0.0075) - 0.004;
    // puffy outer crust: a lumpy elliptic ring, flat bottom, cut by the wedge sides
    float n1 = EwValueNoise2D(q * 17.0 + 5.0);
    vec2 er = vec2(0.036, 0.026 + 0.008 * n1);
    vec2 e = vec2(rq - 0.578 - 0.008 * (n1 - 0.5), p.y - 0.022);
    float crust = (length(e / er) - 1.0) * min(er.x, er.y);
    if (crust < 0.01) {   // surface detail only near the crust, in tube coordinates (no stretching)
        vec2 cr = vec2(atan(q.x, q.y) * 0.578, atan(p.y - 0.022, rq - 0.578) * 0.032);
        crust -= 0.0015 * EwValueNoise2D(cr * 60.0) + 0.0005 * EwValueNoise2D(cr * 170.0);
    }
    crust = max(crust, -p.y - 0.001);
    crust = EwSmoothMax(crust, side, 0.004);
    float pizza = EwSmoothMin(dough, crust, 0.02);
    // melted cheese layer (blistered height field, irregular edge at the cuts)
    float cb = max(max(side - 0.002, rq - 0.555), max(p.y - 0.042, 0.012 - p.y));   // cheap bound
    float ch = cb > 0.006 ? 0.04 : cheeseInfo(q).x;
    float cheese = EwSmoothMax(max(side - 0.002, rq - 0.555), (p.y - ch) * 0.7, 0.004);
    cheese = max(cheese, 0.012 - p.y);
    float piz = EwSmoothMin(pizza, cheese, 0.006);
    // cupped pepperoni slices: irregular outline, edges curl up, bumpy surface
    float dp = 1e5;
    if (p.y < 0.054) {
        for (int i = ZERO; i < 6; i++) {
            vec3 pp = Pep[i];
            vec2 dq = q - pp.xy;
            float rho = length(dq);
            if (rho > pp.z * 1.08 + 0.01) { dp = min(dp, rho - pp.z * 1.08); continue; }
            float rr = pepR(i, dq);
            float e = min(rho / rr, 1.3);
            float e2 = e * e;
            float yc = 0.0318 + 0.0035 * e2 + 0.0045 * e2 * e2 * e2     // rests on the cheese, rim curls up
                     + 0.0008 * (EwValueNoise2D(dq * 150.0 + float(i) * 9.0) - 0.5)
                     + 0.0003 * (EwValueNoise2D(dq * 420.0 + float(i) * 3.0) - 0.5);
            float sl = (0.007 * e + 0.027 * e2 * e2 * e) / rr;      // slope of the cup: true distance
            float dy = (p.y - yc) * inversesqrt(1.0 + sl * sl);
            float th = 0.0013 - 0.0004 * e2 * e2;                  // thinner, crisp rim
            dp = min(dp, sdExtrude(rho - rr + 0.0012, dy, th) - 0.0012);
        }
    }
    float dz = min(piz, dp) * SliceScale * bend;
    if (dz < res.x) {
        float id = dp < piz ? 4.0 : (cheese < pizza + 0.002 ? 3.0 : 2.0);
        res = vec2(dz, id);
    }
    return res;
}
#define EW_SDF(p) map(p).x

// The whole scene query of a ray in ONE non unrolled loop: sphere tracing (up to 200 steps),
// then on a hit the normal (4 tetrahedral samples), ambient occlusion (5 samples along the
// normal, iq) and the soft shadow towards the key light (up to 64 steps). map() is inlined
// only once in the whole shader (fast compile).
// Returns the material id (-1 = miss); on a hit: tHit, pos, nao (xyz normal, w ambient
// visibility) and sh (key light visibility).
float traceScene(vec3 ro, vec3 rd, float tmin, float tmax, vec3 Ld,
                 out float tHit, out vec3 pos, out vec4 nao, out float sh)
{
    float id = -1.0, t = tmin;
    int phase = 0, j = 0;                  // 0 = tracing, 1 = normal / AO / shadow; j = step in phase
    vec3 n = vec3(0.0), nn = vec3(0.0, 1.0, 0.0), so = ro;
    float occ = 0.0, w = 1.0, res = 1.0, ts = 0.003, tMax = 1.5;
    bool lit = false;
    tHit = 1e5; pos = ro; nao = vec4(0.0, 1.0, 0.0, 1.0); sh = 0.0;
    for (int i = ZERO; i < 273; i++) {
        vec3 e = vec3(0.0);
        float h = 0.0;
        vec3 sp;
        if (phase == 0) {                  // sphere tracing
            if (j >= 200) break;
            sp = ro + rd * t;
        } else if (j < 4) {                // normal samples
            e = 0.5773 * (2.0 * vec3(float(((j + 3) >> 1) & 1), float((j >> 1) & 1), float(j & 1)) - 1.0);
            sp = pos + e * 0.0006;
        } else if (j < 9) {                // AO samples
            if (j == 4) nn = normalize(n);
            h = 0.01 + 0.12 * float(j - 4) / 4.0;
            sp = pos + h * nn;
        } else {                           // shadow march
            if (j == 9) {
                if (dot(nn, Ld) <= -0.3) break;          // facing away: fully shadowed
                lit = true;
                so = pos + nn * 0.0015;
                // march only until the ray leaves the thin slab the scene lives in
                tMax = Ld.y > 0.01 ? min((0.13 - pos.y) / Ld.y, 1.5) : 1.5;
            }
            sp = so + Ld * ts;
        }
        vec2 m = map(sp);
        float d = m.x;
        j++;
        if (phase == 0) {
            if (d < 0.00025 * t) { id = m.y; tHit = t; pos = sp; phase = 1; j = 0; continue; }
            t += d * 0.9;
            if (t > tmax) break;
        } else if (j <= 4) n += e * d;
        else if (j <= 9) { occ += (h - d) * w; w *= 0.95; }
        else {
            res = min(res, ShadowSharpness * d / ts);
            if (res < 0.002 || ts > tMax) break;
            ts += clamp(d * 0.8, 0.002, 0.05);
        }
    }
    if (id > 0.0) {
        nao = vec4(nn, clamp(1.0 - 3.0 * occ, 0.0, 1.0));
        if (lit) { sh = clamp(res, 0.0, 1.0); sh = sh * sh * (3.0 - 2.0 * sh); }
    }
    return id;
}

// studio environment: dim room, large warm softbox in the key direction, white card front right
vec3 envLight(vec3 R, vec3 Ld)
{
    vec3 room = vec3(0.9, 0.95, 1.0) * (0.25 + 0.3 * clamp(R.y + 0.2, 0.0, 1.0)) * FillLight;
    float box = smoothstep(0.82, 0.93, dot(R, Ld));
    float card = smoothstep(0.55, 0.85, dot(R, normalize(vec3(0.7, 0.35, 0.8))));
    return room + KeyColor * KeyLight * 1.2 * box + vec3(1.0) * FillLight * 1.1 * card;
}

// diffuse (blurred) studio lighting for a normal, also used for rough reflections
vec3 envDiffuse(vec3 n, vec3 Ld)
{
    return vec3(0.9, 0.95, 1.0) * FillLight * (0.35 + 0.25 * n.y)
         + KeyColor * KeyLight * 0.06 * clamp(0.5 + 0.5 * dot(n, Ld), 0.0, 1.0);
}

// sample the optional plate logo with smooth anti-aliased edge masking
vec4 samplePlateLogo(vec2 uv, float lod, vec2 edgePx)
{
    vec4 t = textureLod(iChannel0, clamp(uv, 0.0, 1.0), lod);
    vec2 m = smoothstep(vec2(0.0), edgePx, uv) * (1.0 - smoothstep(1.0 - edgePx, vec2(1.0), uv));
    t.a *= m.x * m.y;
    return t;
}

// physically based shading of a surface hit (material id: 1 plate, 2 dough, 3 cheese, 4 pepperoni)
// nao = normal + ambient visibility, sh = key light visibility (from traceScene)
vec4 shade(vec3 pos, vec3 rd, float id, vec3 Ld, vec4 nao, float sh, float pxWorld)
{
    vec3 n = nao.xyz;
    vec3 po = pos;
    po.xz = gRot * po.xz;
    vec3 pPlate = po;
    po.y -= plateTop(length(po.xz));
    po /= SliceScale;
    vec2 q = sliceQ(po);
    float rq = length(q);
    float side = wedgeSide(q);
    float B = Browning;

    vec3 alb = PlateColor;
    float rough = 0.08;
    float sss = 0.0;
    vec3 sssCol = vec3(0.0);

    if (SwHaveImage && id < 1.5 && n.y > 0.3 && pPlate.y > -0.002) {
        // logo etched onto the porcelain plate in front of the slice, aligned with the front cut edge
        vec2 axisU = vec2(sin(SliceHalfAngle), cos(SliceHalfAngle));
        vec2 axisV = vec2(cos(SliceHalfAngle), -sin(SliceHalfAngle));
        vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texRes.x / texRes.y;
        vec2 maxBox = vec2(0.42, 0.198);
        vec2 logoDim = imgAspect > (maxBox.x / maxBox.y)
            ? vec2(maxBox.x, maxBox.x / imgAspect)
            : vec2(maxBox.y * imgAspect, maxBox.y);
        vec2 logoCenter = vec2(0.0, -0.302);
        // keep bottom corners inside the flat plate well (r <= 0.42) at default size
        vec2 halfD = 0.5 * logoDim;
        float cornerR = length(vec2(halfD.x, -logoCenter.y + halfD.y));
        if (cornerR > 0.42) {
            float aQ = dot(halfD, halfD);
            float bQ = -logoCenter.y * halfD.y;
            float cQ = logoCenter.y * logoCenter.y - 0.42 * 0.42;
            logoDim *= (-bQ + sqrt(max(bQ * bQ - aQ * cQ, 0.0))) / max(aQ, 1e-6);
        }
        logoDim *= LogoSize;
        vec2 uv = (vec2(dot(pPlate.xz, axisU), dot(pPlate.xz, axisV)) - logoCenter) / logoDim + 0.5;

        float pxPlate = pxWorld / sqrt(max(dot(n, -rd), 0.25));
        vec2 duv = max(vec2(1.0) / texRes, vec2(pxPlate) / logoDim * 0.85);
        if (uv.x > -duv.x * 2.0 && uv.x < 1.0 + duv.x * 2.0 && uv.y > -duv.y * 2.0 && uv.y < 1.0 + duv.y * 2.0) {
            float texelsPerPx = pxPlate * max(texRes.x / logoDim.x, texRes.y / logoDim.y);
            float lod = max(0.0, log2(max(texelsPerPx, 1e-4)) - 0.5);
            vec2 edgePx = max(vec2(pxPlate) / logoDim, vec2(1.0) / texRes);

            vec4 tex = samplePlateLogo(uv, lod, edgePx);
            float etchA = clamp(tex.a * LogoOpacity, 0.0, 1.0);
            if (LogoRelief > 0.0) {
                float hL = -samplePlateLogo(uv - vec2(duv.x, 0.0), lod, edgePx).a;
                float hR = -samplePlateLogo(uv + vec2(duv.x, 0.0), lod, edgePx).a;
                float hD = -samplePlateLogo(uv - vec2(0.0, duv.y), lod, edgePx).a;
                float hU = -samplePlateLogo(uv + vec2(0.0, duv.y), lod, edgePx).a;
                mat2 invRot = transpose(gRot);
                vec2 tU2 = invRot * axisU;
                vec2 tV2 = invRot * axisV;
                vec3 T = normalize(vec3(tU2.x, 0.0, tU2.y) - n * dot(n, vec3(tU2.x, 0.0, tU2.y)));
                vec3 Bv = normalize(vec3(tV2.x, 0.0, tV2.y) - n * dot(n, vec3(tV2.x, 0.0, tV2.y)));
                float bevel = LogoRelief * LogoOpacity * 0.9;
                n = normalize(n - ((hR - hL) * T + (hU - hD) * Bv) * bevel);
                nao.w *= 1.0 - 0.14 * etchA * clamp(LogoRelief, 0.0, 1.5);
            }
            vec3 imgCol = EwSrgbToLinear(tex.rgb);
            vec3 logoCol = mix(imgCol, LogoColor, LogoColorMix);
            alb = mix(alb, logoCol, etchA);
            rough = mix(rough, 0.36, etchA * etchA);
        }
    }

    if (id > 1.5 && id < 2.5) {
        // dough and crust: pale bottom, golden brown top, charred blisters, crumb at the cut
        float top = smoothstep(0.012, 0.042, po.y) * smoothstep(0.5, 0.56, rq);
        // crust tube coordinates: arc length along the crust x around its cross-section (no stretching)
        vec2 cq = vec2(atan(q.x, q.y) * 0.578, atan(po.y - 0.022, rq - 0.578) * 0.032);
        float n = fbm2(cq * 30.0, 6);
        float nf = EwValueNoise2D(cq * 260.0) * 0.6 + EwValueNoise2D(cq * 620.0) * 0.4;   // fine grain
        alb = mix(CrustColor * vec3(1.08, 1.12, 1.25), CrustColor * vec3(0.62, 0.48, 0.36), top * (0.55 + 0.6 * n + 0.3 * B));
        alb *= (0.78 + 0.44 * n) * (0.9 + 0.2 * nf);
        // small baked bubbles / pits with darker toasted rims
        // crust pits + crumb Voronoi through one call site (fast compile)
        vec2 kin[2] = vec2[2](cq * 85.0, vec2(rq, po.y) * 140.0);
        vec3 vk[2];
        for (int j = ZERO; j < 2; j++) vk[j] = voronoi(kin[j]);
        vec3 vb = vk[0];
        float pit = (1.0 - smoothstep(0.08, 0.2, vb.x)) * step(0.62, vb.z);
        float ring = smoothstep(0.12, 0.22, vb.x) * (1.0 - smoothstep(0.22, 0.34, vb.x)) * step(0.62, vb.z);
        alb *= 1.0 - 0.35 * pit * top - 0.25 * ring * top;
        // charred blisters (leopard spots)
        float charS = smoothstep(0.66 - 0.12 * B, 0.8 - 0.1 * B, n + 0.08 * nf) * top;
        alb = mix(alb, CharColor, charS * (0.35 + 0.65 * B));
        // a light dusting of flour on the crust
        alb = mix(alb, vec3(0.9, 0.85, 0.76), smoothstep(0.8, 0.95, EwValueNoise2D(cq * 140.0 + 3.0)) * top * FlourAmount);
        // flour dust on the bottom edge
        alb = mix(alb, vec3(0.85, 0.8, 0.7), smoothstep(0.012, 0.0, po.y) * 0.4);
        // cut faces: porous crumb, a thin sauce line under the cheese
        float cut = 1.0 - smoothstep(0.0008, 0.004, abs(side));
        vec3 v = vk[1];
        vec3 crumb = vec3(0.88, 0.72, 0.46) * (0.55 + 0.45 * smoothstep(0.08, 0.45, v.x));
        float sauce = smoothstep(0.016, 0.019, po.y) * (1.0 - smoothstep(0.024, 0.027, po.y)) * (1.0 - smoothstep(0.53, 0.56, rq));
        crumb = mix(crumb, SauceColor, sauce);
        alb = mix(alb, crumb, cut * (1.0 - top * 0.3));
        rough = mix(0.62, 0.8, cut);
        sss = 0.15;
        sssCol = vec3(1.0, 0.6, 0.3);
    } else if (id > 2.5 && id < 3.5) {
        // melted mozzarella: creamy base, leopard spotted blisters, pores, grease pools, oregano
        vec4 ci = cheeseInfo(q);
        float bn = fbm2(q * 13.0 + 2.0, 4);
        float fine = EwValueNoise2D(q * 150.0);
        // pores + oregano Voronoi through one call site (fast compile)
        vec2 cin[2] = vec2[2](q * 220.0, q * 70.0 + 13.0);
        vec3 vc[2];
        for (int j = ZERO; j < 2; j++) vc[j] = voronoi(cin[j]);
        float hn = clamp((ci.x - 0.0285) / 0.01, 0.0, 1.0);
        alb = CheeseColor * mix(vec3(0.95, 0.92, 0.9), vec3(0.88, 0.7, 0.45), bn);
        // browned blister tops with darker cores, each blister browned differently
        float bTop = smoothstep(0.05, 0.7, ci.y);
        float bAmt = smoothstep(0.3 - 0.3 * B, 0.8 - 0.2 * B, fract(ci.z * 5.13));
        float edgeB = smoothstep(0.75, 0.25, ci.y) * bTop;   // darker ring at the blister edge
        alb = mix(alb, BlisterColor, bTop * mix(0.45, 1.0, bAmt));
        alb = mix(alb, vec3(0.22, 0.08, 0.02), edgeB * bAmt * 0.6);
        alb = mix(alb, vec3(0.1, 0.035, 0.012), smoothstep(0.55, 1.0, ci.y) * bAmt * (0.4 + 0.6 * B) * smoothstep(0.3, 0.65, fine * 0.7 + 0.5 * bn));
        // larger golden-brown patches where the cheese is thin
        alb = mix(alb, vec3(0.58, 0.3, 0.07), smoothstep(0.45 - 0.15 * B, 0.75, bn) * 0.75);
        // tiny pores
        vec3 vp = vc[0];
        alb *= 1.0 - 0.35 * (1.0 - smoothstep(0.05, 0.18, vp.x)) * step(0.7, vp.z);
        alb *= 0.9 + 0.2 * fine;
        // orange pepperoni grease pooling in the low spots, mostly around the slices
        float low = 1.0 - smoothstep(0.0, 0.5, hn);
        float g = clamp(ci.w * (0.45 + 0.7 * low) + 0.6 * low * smoothstep(0.55, 0.8, EwValueNoise2D(q * 8.0 + 4.0)), 0.0, 1.0);
        alb = mix(alb, GreaseColor, g * GreaseAmount);
        // oregano flecks
        vec3 vo = vc[1];
        alb = mix(alb, OreganoColor, (1.0 - smoothstep(0.03, 0.07, vo.x)) * step(1.0 - OreganoDensity, vo.z));
        rough = mix(0.36, 0.09, g);
        rough = mix(rough, 0.5, bTop * bAmt);
        sss = 0.6;
        sssCol = vec3(1.0, 0.65, 0.25);
    } else if (id > 3.5) {
        // pepperoni: mottled meat, fat marbling, pepper specks, glossy grease cup, crispy rim
        float rel = 10.0;
        vec2 dl = vec2(0.0);
        float pid = 0.0;
        for (int i = ZERO; i < 6; i++) {
            vec2 d = q - Pep[i].xy;
            float r = length(d) / pepR(i, d);
            if (r < rel) { rel = r; dl = d / Pep[i].z; pid = float(i); }
        }
        float g1 = fbm2(dl * 5.0 + pid * 3.1, 4);
        float g2 = EwValueNoise2D(dl * 28.0 + pid * 11.0);
        float g3 = EwValueNoise2D(dl * 70.0 + pid * 2.0);
        alb = PepperoniColor * (0.6 + 0.6 * g1) * (0.85 + 0.3 * g2) * (0.9 + 0.2 * g3);
        alb = mix(alb, PepperoniColor * vec3(1.5, 0.95, 0.75), smoothstep(0.6, 0.85, g1) * 0.4);
        // rendered fat: irregular flecks of different sizes
        // the three Voronoi layers (fat, small fat, specks) through one call site (fast compile)
        vec2 vin[3] = vec2[3](dl * 8.0 + pid * 7.0 + 0.8 * vec2(g2, g1), dl * 18.0 + pid * 3.0 + 0.5 * g3, dl * 30.0 + pid * 5.0);
        vec3 vv[3];
        for (int j = ZERO; j < 3; j++) vv[j] = voronoi(vin[j]);
        vec3 v1 = vv[0], v2 = vv[1], v3 = vv[2];
        // rendered fat: irregular flecks of different sizes
        float fs = mix(0.07, 0.16, fract(v1.z * 17.0));
        float fat1 = (1.0 - smoothstep(fs * 0.6, fs, v1.x)) * step(0.5, v1.z);
        float fat2 = (1.0 - smoothstep(0.05, 0.11, v2.x)) * step(0.55, v2.z);
        alb = mix(alb, vec3(0.42, 0.14, 0.065), max(fat1, fat2 * 0.7) * FatAmount);
        // black pepper and paprika specks
        float sp = 1.0 - smoothstep(0.05, 0.12, v3.x);
        alb = mix(alb, vec3(0.03, 0.015, 0.01), sp * step(0.84, v3.z) * 0.85);
        alb = mix(alb, vec3(0.45, 0.09, 0.015), sp * step(0.66, v3.z) * (1.0 - step(0.84, v3.z)) * 0.5);
        // glossy grease pooled in the cup
        float pool = 1.0 - smoothstep(0.3, 0.65, rel + 0.2 * (g2 - 0.5));
        alb = mix(alb, alb * vec3(0.75, 0.5, 0.35) + vec3(0.05, 0.01, 0.0), pool * 0.6);
        // scorched band and crispy, charred, irregular rim; darker underside where it curls up
        float rn = EwValueNoise2D(dl * 9.0 + pid * 5.0);
        float rim = smoothstep(0.74 + 0.14 * rn, 1.0, rel);
        float band = smoothstep(0.55, 0.85, rel) * (1.0 - rim);
        alb = mix(alb, vec3(0.2, 0.045, 0.018), band * (0.25 + 0.3 * B));
        alb = mix(alb, vec3(0.085, 0.022, 0.01) * (0.7 + 0.6 * rn), rim * (0.55 + 0.45 * B));
        alb = mix(alb, PepperoniColor * 0.55, smoothstep(0.2, -0.4, n.y) * 0.5);
        rough = mix(0.34, GreaseRoughness, pool);
        rough = mix(rough, 0.6, rim);
        sss = 0.3;
        sssCol = vec3(1.0, 0.25, 0.1);
    }

    // PBR lighting: GGX key light with soft shadow, wrapped subsurface, studio environment
    vec3 V = -rd;
    float NoV = max(dot(n, V), 1e-3);
    vec3 H = normalize(Ld + V);
    float NoL = dot(n, Ld);
    float NoLc = max(NoL, 0.0);
    float NoH = max(dot(n, H), 0.0);
    float a = rough * rough;
    float a2 = a * a;
    float dd = NoH * NoH * (a2 - 1.0) + 1.0;
    float D = a2 / (3.14159 * dd * dd);
    float k = a * 0.5;
    float Vis = 0.25 / ((NoV * (1.0 - k) + k) * (NoLc * (1.0 - k) + k));
    float F = EwFresnelSchlick(max(dot(H, V), 0.0), 0.04);
    float ao = nao.w;
    ao = 1.0 - AoStrength * (1.0 - ao);
    vec3 key = KeyColor * KeyLight;
    vec3 col = (alb * (1.0 - F) / 3.14159 + D * Vis * F) * NoLc * key * sh;
    // wrapped light scattered inside cheese / dough / meat (subsurface)
    float wrap = clamp((NoL + 0.6) / 1.6, 0.0, 1.0);
    col += alb * sssCol * sss * key / 3.14159 * max(wrap - NoLc, 0.0) * SubsurfaceStrength * mix(0.35, 1.0, sh);
    // front fill (white card, no shadow)
    vec3 Lf = normalize(vec3(0.7, 0.35, 0.8));
    col += alb / 3.14159 * max(dot(n, Lf), 0.0) * FillLight * 1.3 * ao;
    // ambient diffuse + specular reflection of the studio
    col += alb * envDiffuse(n, Ld) * ao;
    vec3 R = reflect(rd, n);
    float Fe = 0.04 + (max(1.0 - rough, 0.04) - 0.04) * pow(1.0 - NoV, 5.0);  // roughness aware Fresnel
    vec3 envS = mix(envLight(R, Ld), envDiffuse(R, Ld) * 1.5, smoothstep(0.05, 0.7, rough));
    float specOcc = ao * ao * mix(1.0, sh, 0.5 * smoothstep(0.0, 0.3, R.y));
    col += Fe * envS * specOcc * (1.0 - 0.6 * rough);
    return vec4(col, 1.0);
}

// volumetric steam rising above the slice, up to the surface hit distance tEnd
vec4 steam(vec3 ro, vec3 rd, float tEnd, vec3 Ld, vec2 fragCoord, float st)
{
    vec4 acc = vec4(0.0);
    if (SteamAmount <= 0.0) return acc;
    float yTop = 0.06 + SteamHeight;
    float t0 = 0.0, t1 = tEnd;
    if (abs(rd.y) > 1e-4) {
        float ta = (0.03 - ro.y) / rd.y, tb = (yTop - ro.y) / rd.y;
        t0 = max(t0, min(ta, tb));
        t1 = min(t1, max(ta, tb));
    }
    float a = dot(rd.xz, rd.xz), b = dot(ro.xz, rd.xz), c = dot(ro.xz, ro.xz) - 0.85;
    float disc = b * b - a * c;
    if (disc <= 0.0 || a < 1e-6) return acc;
    float sq = sqrt(disc);
    t0 = max(t0, (-b - sq) / a);
    t1 = min(t1, (-b + sq) / a);
    if (t1 <= t0) return acc;
    int N = Quality == 0 ? 14 : 26;
    float dt = (t1 - t0) / float(N);
    float tt = t0 + dt * EwHash2To1(fragCoord + 17.0 * fract(iTime * 0.37));
    float T = 1.0;
    float ph = 0.5 + 2.5 * pow(max(dot(rd, Ld), 0.0), 4.0);   // forward scattering: glows against the light
    vec3 lc = KeyColor * KeyLight * SteamGlow * ph + vec3(0.92, 0.95, 1.0) * FillLight * 0.4;
    for (int i = ZERO; i < 26; i++) {
        if (i >= N) break;
        vec3 pw = ro + rd * tt;
        tt += dt;
        float h = pw.y - 0.03;
        // fades in above the slice and out towards SteamHeight
        float hf = smoothstep(0.04, 0.3, h) * (1.0 - smoothstep(0.3 * SteamHeight, SteamHeight, h));
        if (hf <= 0.0) continue;
        vec3 po = pw;
        po.xz = gRot * po.xz;
        vec2 q = sliceQ(po / SliceScale);
        // footprint of the slice, widening with height
        float spread = 0.04 + SteamSpread * h;
        float m = 1.0 - smoothstep(-0.1, spread, max(wedgeSide(q), max(length(q) - 0.56, 0.05 - q.y)));
        if (m <= 0.0) continue;
        // billowing base noise warps thin ridged wisps
        // 3 octave fbm (same as EwFbm3D) + the ridge sample in one loop: one inlined noise (fast compile)
        vec3 sp = pw * vec3(4.5, 2.6, 4.5) - vec3(0.0, st * 1.1, 0.0);
        float w = 0.0, amp = 0.5, ridge = 0.0;
        for (int o = ZERO; o < 4; o++) {
            if (o == 3) sp = pw * vec3(8.0, 4.0, 8.0) + vec3(w * 3.0, 0.0, w * 2.0) - vec3(0.0, st * 1.9, 0.0);
            float nv = EwValueNoise3D(sp);
            if (o < 3) { w += amp * nv; sp = sp * 2.02 + vec3(17.0, 31.0, 47.0); amp *= 0.5; }
            else ridge = 1.0 - abs(2.0 * nv - 1.0);
        }
        ridge *= ridge * ridge * ridge;
        float d = ridge * ridge * smoothstep(0.42, 0.72, w) * m * m * hf * SteamAmount * 2.0;
        float al = 1.0 - exp(-d * dt * 10.0);
        acc.rgb += T * al * lc;
        acc.a += T * al;
        T *= 1.0 - al;
        if (T < 0.02) break;
    }
    return acc;
}

const vec2 AA4[4] = vec2[4](vec2(-0.125, -0.375), vec2(0.375, -0.125), vec2(0.125, 0.375), vec2(-0.375, 0.125));

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    float ph = mod(iTime, SwingPeriod) / SwingPeriod * 6.2831853;
    gRot = EwRotate2D(radians(BaseAngle) + Swing * sin(ph));
    float st = mod(iTime, 1000.0) * SteamSpeed * 0.35;

    vec3 Ld = normalize(vec3(-0.6, 0.52, -0.55));
    Ld.xz = EwRotate2D(LightAngle) * Ld.xz;

    float el = radians(CameraElevation);
    vec3 ta = vec3(0.0, 0.03, 0.0);
    vec3 ro = ta + (2.05 / Zoom) * vec3(0.0, sin(el), cos(el));
    mat3 cam = EwCameraLookAt(ro, ta, 0.0);
    float uvs = max(1.0, 1.05 * iResolution.y / iResolution.x);
    const float FL = 1.8;

    int ns = Quality == 0 ? 1 : (Quality == 1 ? 2 : 4);
    vec4 acc = vec4(0.0);
    float tPrim = 1e5;
    for (int s = ZERO; s < 4; s++) {
        if (s >= ns) break;
        vec2 off = ns == 1 ? vec2(0.0) : (ns == 2 ? (s == 0 ? vec2(-0.25, 0.25) : vec2(0.25, -0.25)) : AA4[s]);
        vec2 uv = (fragCoord + off - 0.5 * iResolution.xy) / iResolution.y * uvs;
        vec3 rd = cam * normalize(vec3(uv, FL));
        vec4 c = vec4(0.0);
        float tHit = 1e5;
        float id = -1.0;
        vec3 pos = vec3(0.0);
        // bounds: sphere around plate + slice, clipped to the slab the scene occupies
        vec3 oc = ro - vec3(0.0, 0.02, 0.0);
        float b = dot(oc, rd);
        float h = b * b - (dot(oc, oc) - 0.82 * 0.82);
        if (h > 0.0) {
            float sq = sqrt(h);
            float t0 = max(-b - sq, 0.0), t1 = -b + sq;
            if (rd.y < -1e-4) {
                t0 = max(t0, (0.13 - ro.y) / rd.y);
                t1 = min(t1, (-0.045 - ro.y) / rd.y);
            }
            if (t1 > t0) {
                vec4 nao; float sh;
                id = traceScene(ro, rd, t0, t1, Ld, tHit, pos, nao, sh);
                if (id > 0.0) c = shade(pos, rd, id, Ld, nao, sh, tHit * uvs / (iResolution.y * FL));
            }
        }
        if (s == 0) tPrim = tHit;
        acc += c;
    }
    acc /= float(ns);

    // steam in front of the slice
    vec2 uv0 = (fragCoord - 0.5 * iResolution.xy) / iResolution.y * uvs;
    vec3 rd0 = cam * normalize(vec3(uv0, FL));
    vec4 stm = steam(ro, rd0, min(tPrim, 10.0), Ld, fragCoord, st);
    acc = stm + acc * (1.0 - stm.a);

    float A = clamp(acc.a, 0.0, 1.0);
    vec3 col = acc.rgb / max(A, 1e-4);
    col = EwTonemapAces(col * Exposure);
    col = EwLinearToSrgb(col) + EwDither(fragCoord, iTime, 1.0);
    col = clamp(col, 0.0, 1.0);
    fragColor = vec4(col * A, A);
}
