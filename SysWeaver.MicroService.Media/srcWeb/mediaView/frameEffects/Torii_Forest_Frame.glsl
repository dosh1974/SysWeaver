//  media: image none
// =============================================================================
//  Torii Forest Frame
// =============================================================================
//  A weathered vermilion torii gate as a screen frame: stone threshold, crossbeam
//  with plaque, swinging paper lanterns, perched crows, jorou spiders, corner webs
//  and, inside the opening, a misty moonlit path lined with glowing stone lanterns
//  and jizo statues. Everything is procedural; the gate uses normal-mapped PBR-style
//  shading, the night scene is simple flat moonlight; the middle stays calm.
//
//  Live controls:
//  iMouse.x = Inset | Distance of the frame center line from the screen edges (gate size) | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Lantern Glow | Brightness of the paper lanterns, the stone lanterns on the path and their warm light | 0.4 | 0 .. 3 | x
//  iMouse.w = Mist | Fog density of the moonlit path behind the gate | 0.4 | 0 .. 1

// --- General ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Lantern, crow, spider and firefly animation speed", "unit": "x" }
const float Exposure = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float SunStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Sun", "desc": "Strength of the warm key light on the gate (lower for a night-lit gate)", "unit": "x" }

// --- Gate ---
const vec3 GateColor = vec3(0.45, 0.045, 0.02); // var: { "type": "col", "name": "Gate Color", "desc": "Vermilion lacquer of the pillars and beams" }
const float Weathering = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Weathering", "desc": "Peeling paint, grime streaks, cracks and lichen", "scale": 100, "unit": "%" }
const float MossAmount = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Moss", "desc": "Moss on the stone threshold", "scale": 100, "unit": "%" }
const float GateShadow = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Gate Shadow", "desc": "Darkness of the soft shadow the gate casts onto the path scene", "scale": 100, "unit": "%" }
const float WebStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Webs", "desc": "Visibility of the spider webs in the top corners", "unit": "x" }

// --- Lanterns ---
const vec3 LanternColor = vec3(1.0, 0.45, 0.14); // var: { "type": "col", "name": "Lantern Color", "desc": "Color of the lantern light" }
const float HaloStrength = 0.4; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Halo Strength", "desc": "Brightness of the soft glow around the lanterns" }
const float HaloSize = 0.7; // var: { "min": 0.2, "max": 1.5, "step": 0.01, "name": "Halo Size", "desc": "Spread of the lantern halo (gate heights)" }
const float FlickerAmount = 0.12; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Flicker", "desc": "How much the lantern flames flicker", "scale": 100, "unit": "%" }
const float LanternSwing = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Lantern Swing", "desc": "How far the lanterns sway on their hooks", "unit": "x" }

// --- Moonlit path (inside the gate) ---
const float ForestOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Path Scene", "desc": "Visibility of the moonlit path over the inner color (0 = solid inner color)", "scale": 100, "unit": "%" }
const float FocusBlur = 0.008; // var: { "min": 0.0, "max": 0.03, "step": 0.001, "name": "Focus Blur", "desc": "Depth of field: how out of focus the scene behind the gate is (0 = sharp)", "scale": 100, "unit": "%" }
const vec3 FogColor = vec3(0.025, 0.035, 0.065); // var: { "type": "col", "name": "Night Color", "desc": "Color of the night sky and the mist" }
const vec3 MoonColor = vec3(0.7, 0.78, 0.95); // var: { "type": "col", "name": "Moon Color", "desc": "Color of the moon and its light on the path" }
const float MoonBrightness = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Moonlight", "desc": "Brightness of the moon and the moonlit ground / stones", "unit": "x" }
const vec2 MoonPosition = vec2(-0.32, 0.24); // var: { "min": -0.8, "max": 0.8, "step": 0.01, "name": "Moon Position", "desc": "Moon position (x across, y height; 0, 0 = screen center, 0.5 = half the screen height)" }
const float StarDensity = 0.1; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Stars", "desc": "Share of sky cells holding a star: more = a richer starry sky", "scale": 100, "unit": "%" }
const float MoonSize = 1.0; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Moon Size", "desc": "Size of the moon disc and its halo", "unit": "x" }
const vec3 PathLightColor = vec3(1.0, 0.55, 0.2); // var: { "type": "col", "name": "Path Light Color", "desc": "Color of the stone lantern windows and their light pools" }
const float PathLightGlow = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Path Lights", "desc": "Brightness of the stone lanterns along the path (also follows Lantern Glow)", "unit": "x" }
const float SculptureStart = 2.2; // var: { "min": 1.2, "max": 5.0, "step": 0.1, "name": "First Sculpture", "desc": "Distance of the nearest stone lantern / statue: lower = bigger, closer" }
const float SculptureSpacing = 2.6; // var: { "min": 1.2, "max": 5.0, "step": 0.1, "name": "Sculpture Spacing", "desc": "Distance between the rows of stone lanterns and statues" }
const vec3 BibColor = vec3(0.3, 0.025, 0.02); // var: { "type": "col", "name": "Bib Color", "desc": "Color of the cloth bibs on the jizo statues" }
const int FireflyCount = 8; // var: { "min": 0, "max": 30, "step": 1, "name": "Fireflies", "desc": "Number of softly blinking fireflies" }
const vec3 FireflyColor = vec3(0.55, 0.8, 0.25); // var: { "type": "col", "name": "Firefly Color", "desc": "Color of the fireflies" }
const float GroundMist = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Ground Mist", "desc": "Strength of the low mist band along the horizon (scaled by Mist)", "scale": 100, "unit": "%" }

// --- Creatures ---
const bool ShowCreatures = true; // var: { "name": "Creatures", "desc": "Show the crows and spiders" }

// --- Compositing ---
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0); // var: { "type": "col", "name": "Inner Color", "desc": "Fill color inside the frame" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner scene and fill inside the frame (0 = transparent overlay)", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define LanternGlow mix(0.0, 3.0, iMouse.z)
#define Mist mix(0.0, 1.0, iMouse.w)
// non-constant zero loop start: keeps the compiler from unrolling heavy loops (much faster compile)
#define ZERO min(iFrame, 0)

// value noise fbm, same math as EwFbm2D (gain 0.5, rotated octaves) but with a loop the compiler
// does not unroll: one inlined noise per call site instead of one per octave
float fbmL(vec2 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < octaves; i++) {
        sum += amp * EwValueNoise2D(p);
        p = mat2(1.6, 1.2, -1.2, 1.6) * p;
        amp *= 0.5;
    }
    return sum;
}

// Voronoi F1 / F2 / cell value, same as EwVoronoi2D but with a non-unrolled 3x3 search
vec3 voronoiL(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);
    float nearest = 8.0;
    float second = 8.0;
    vec2 nearestCell = cell;
    for (int k = ZERO; k < 9; k++) {
        vec2 nb = vec2(float(k % 3 - 1), float(k / 3 - 1));
        vec2 r = nb + EwHash2To2(cell + nb) - frac;
        float pd = dot(r, r);
        if (pd < nearest) {
            second = nearest;
            nearest = pd;
            nearestCell = cell + nb;
        } else if (pd < second) {
            second = pd;
        }
    }
    return vec3(sqrt(nearest), sqrt(second), EwHash2To1(nearestCell));
}
// premultiplied "over" compositing: top layer over base
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

// per pixel globals shared with the shading: lantern centers (px), precomputed light dirs/atts, gate unit size H (px)
vec2 gLan[2];
vec3 gLanDir[2];
float gLanAtt[2];
float gLanI;
float gH;

// ---------------------------------------------------------------- lighting
// rough PBR-ish: hemisphere ambient, wrapped diffuse for rough / dusty surfaces,
// GGX specular with Fresnel (f0 0.02-0.05 for lacquer / stone, colored for metal)
vec3 shadeM(vec3 alb, vec3 n, float ao, float sh, vec3 f0, float rough)
{
    const vec3 L = vec3(-0.55030275, 0.60033027, 0.58031926);
    const vec3 hv = vec3(-0.3095382, 0.3376781, 0.8889092);
    vec3 sunC = vec3(1.0, 0.85, 0.66) * (2.1 * SunStrength);
    float ndl = dot(n, L);
    float dif = clamp(ndl, 0.0, 1.0);
    float difR = mix(dif, clamp((ndl + 0.3) * (1.0 / 1.3), 0.0, 1.0) * 0.85, rough * 0.6);
    vec3 amb = mix(vec3(0.09, 0.09, 0.055), vec3(0.24, 0.28, 0.32), 0.5 + 0.5 * n.y);
    vec3 col = alb * (amb * ao + sunC * difR * sh * (0.35 + 0.65 * ao));
    float nh = max(dot(n, hv), 0.0);
    float a = rough * rough;
    float a2 = a * a;
    float dd = nh * nh * (a2 - 1.0) + 1.0;
    float D = a2 / (3.14159 * dd * dd);
    vec3 F = f0 + (1.0 - f0) * 1.69156e-5;
    col += sunC * F * D * 0.25 * dif * sh * ao;
    float fn = 1.0 - max(n.z, 0.0);
    float fn2 = fn * fn;
    vec3 Fv = f0 + (1.0 - f0) * (fn2 * fn2 * fn);
    col += amb * Fv * (1.0 - rough) * 1.2 * ao;
    float lanL = gLanAtt[0] * (0.25 + 0.75 * max(dot(n, gLanDir[0]), 0.0)) +
                 gLanAtt[1] * (0.25 + 0.75 * max(dot(n, gLanDir[1]), 0.0));
    col += alb * LanternColor * lanL * ao;
    return col;
}

vec3 shade(vec3 alb, vec3 n, float ao, float spec, float shin)
{
    return shadeM(alb, n, ao, 1.0, vec3(0.02 + 0.1 * spec), clamp(sqrt(2.0 / (shin + 2.0)) * 1.6, 0.25, 0.95));
}

void pillarAt(float y, float hsx, float H, float yT, float yS0, out float cx, out float w)
{
    float tt = clamp((y - yT) / max(yS0 - yT, 1.0), 0.0, 1.0);
    cx = hsx - (0.85 + 0.08 * tt) * H;
    w = mix(0.8, 0.7, tt) * H;
}

// gate silhouette in pixels (negative inside), for the cast shadow
float gateSD(vec2 p, vec2 hs, float H, float yN, float tN, float yT, float xe)
{
    float ax = abs(p.x);
    float cv = 0.38 * H * pow(clamp(ax / xe, 0.0, 1.0), 5.0);
    float dBeam = max(max(hs.y - 0.62 * H + 0.6 * cv - p.y, p.y - (hs.y + 0.52 * H + cv)), ax - xe);
    float dP = max(abs(ax - hs.x + 0.89 * H) - 0.75 * H, max(yT - p.y, p.y - hs.y));
    float dN = max(abs(p.y - yN) - tN, ax - hs.x - 0.3 * H);
    float dG = max(ax - 0.52 * H, max(yN - p.y, p.y - hs.y));
    return min(min(dBeam, dP), min(dN, dG));
}

// sum of 3 octaves of value noise with analytic gradient: micro facets / pits (v = height)
vec2 microGrad(vec2 q, out float v)
{
    vec2 g = vec2(0.0);
    v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3 + min(iFrame, 0); i++) {
        vec3 nd = EwValueNoise2DDerivatives(q);
        g += a * nd.yz;
        v += a * nd.x;
        q = mat2(1.6, 1.2, -1.2, 1.6) * q + 3.1;
        a *= 0.6;
    }
    v /= 1.18;
    return g;
}
// three microGrad layers q_m = p * scl[m] + off[m] in one loop: a single inlined copy instead of three
void microGrad3(vec2 p, vec3 scl, vec3 off, out vec2 g0, out float v0, out vec2 g1, out float v1, out vec2 g2, out float v2)
{
    g0 = g1 = g2 = vec2(0.0);
    v0 = v1 = v2 = 0.0;
    for (int m = ZERO; m < 3; m++) {
        float sc = (m == 0) ? scl.x : ((m == 1) ? scl.y : scl.z);
        float of = (m == 0) ? off.x : ((m == 1) ? off.y : off.z);
        float v;
        vec2 g = microGrad(p * sc + of, v);
        if (m == 0) { g0 = g; v0 = v; }
        else if (m == 1) { g1 = g; v1 = v; }
        else { g2 = g; v2 = v; }
    }
}
// weathered lacquer over wood. q = (along grain, across) in H units, s = screen / H.
// wear: extra local wear (edges, splash zone). Outputs f0, roughness and a height gradient (q space).
vec3 paint(vec2 q, vec2 s, float seed, vec3 baseC, float wear, out float f0, out float rough, out vec2 bump)
{
    vec2 qa = q * vec2(0.9, 1.1) + seed;
    // 4-octave fbm value + analytic gradient of the first 2 octaves in one loop (replaces 4 unrolled EwFbm2D calls)
    const mat2 M = mat2(1.6, 1.2, -1.2, 1.6);
    float n = 0.0;
    vec2 gN = vec2(0.0);
    vec2 qp = qa;
    float amp = 0.5;
    for (int i = ZERO; i < 4; i++) {
        vec3 nd = EwValueNoise2DDerivatives(qp);
        n += amp * nd.x;
        if (i == 0) gN += amp * nd.yz;
        else if (i == 1) gN += amp * (nd.yz * M);
        qp = M * qp;
        amp *= 0.5;
    }
    float wth = clamp(Weathering * (0.8 + 0.7 * wear), 0.0, 1.0);
    float th = 0.76 - 0.2 * wth;
    float peel = smoothstep(th - 0.01, th + 0.01, n) * step(0.001, wth);
    float edgeS = smoothstep(th - 0.025, th + 0.025, n);
    float lip = smoothstep(th - 0.06, th - 0.01, n) * (1.0 - peel);
    // bare wood: grain lines along q.x, silvered by weather
    float gw = EwValueNoise2D(vec2(q.x * 0.6, q.y * 6.0) + seed);
    float ring = q.y * 26.0 + gw * 7.0;
    float grain = 0.5 + 0.5 * sin(ring);
    float fib = EwValueNoise2D(vec2(q.x * 3.0, q.y * 60.0) + seed);
    vec3 wood = mix(vec3(0.045, 0.032, 0.022), vec3(0.19, 0.14, 0.095), grain * 0.5 + fib * 0.5);
    wood = mix(wood, vec3(0.15, 0.14, 0.125) * (0.6 + 0.6 * fib), 0.45 * wth);
    // lacquer with craquelure (non-unrolled Voronoi)
    vec3 vor = voronoiL(q * vec2(6.0, 10.0) + seed * 1.3);
    float crkZone = smoothstep(0.35, 0.65, EwValueNoise2D(q * 1.2 + seed * 0.7) + 0.3 * wth);
    float crk = (1.0 - smoothstep(0.0, 0.035 + 0.03 * (1.0 - wth), vor.y - vor.x)) * smoothstep(0.2, 0.6, wth + 0.3 * n) * crkZone * 0.6;
    float tone = EwValueNoise2D(q * 1.7 + seed * 2.3);
    vec3 lac = baseC * (0.75 + 0.4 * tone) * (0.96 + 0.08 * vor.z);
    float bleach = fbmL(q * 0.35 + seed + 4.0, 3);
    lac = mix(lac, lac * 0.6 + vec3(0.075, 0.05, 0.042), wth * smoothstep(0.35, 0.75, bleach));
    vec3 alb = mix(lac, wood, peel);
    alb *= 1.0 - 0.35 * lip;
    alb *= 1.0 - 0.55 * crk * (1.0 - peel);
    // grime running down
    float streak = EwValueNoise2D(vec2(s.x * 5.0 + seed, s.y * 0.25)) * EwValueNoise2D(vec2(s.x * 13.0 + seed, s.y * 0.6));
    alb *= 1.0 - 0.75 * wth * smoothstep(0.15, 0.55, streak);
    // micro surface: brush strokes along the grain, pitting, dust specks (1 inlined microGrad loop)
    float mvA = 0.0, mvB = 0.0;
    vec2 mgA = vec2(0.0), mgB = vec2(0.0);
    for (int m = ZERO; m < 2; m++) {
        float mv;
        vec2 mq = (m == 0) ? (q * vec2(5.0, 34.0) + seed * 1.9) : (s * 42.0 + seed * 3.7);
        vec2 mg = microGrad(mq, mv);
        if (m == 0) { mgA = mg; mvA = mv; } else { mgB = mg; mvB = mv; }
    }
    float pit = smoothstep(0.66, 0.76, mvB);
    alb *= (0.9 + 0.2 * mvA) * (1.0 - 0.35 * pit);
    vec3 dh = EwHash2To3(floor(s * 55.0) + seed * 17.0);
    alb = mix(alb, vec3(0.16, 0.14, 0.12), step(0.96, dh.x) * 0.6 * wth);
    // height: chips are recessed, wood grain grooves
    bump = -gN * edgeS * (1.0 - edgeS) * 1.4;
    bump.y += peel * 0.18 * cos(ring);
    bump += (vec2(fib, tone) - 0.5) * 0.06;
    bump += mgA * mix(0.05, 0.16, peel) + mgB * (0.06 + 0.08 * wth);
    f0 = mix(0.045, 0.025, peel) * (1.0 - 0.4 * crk) * (1.0 - 0.5 * pit);
    rough = mix(mix(0.32, 0.65, wth) + 0.12 * bleach * wth, 0.9, peel) + 0.15 * (mvB - 0.5) + 0.2 * pit;
    rough = clamp(rough, 0.2, 1.0);
    return alb;
}

// ---------------------------------------------------------------- moonlit path (inside the gate)
// world: x lateral, y height above the ground, z depth; camera at height PATH_CAM_H looking down the path

// lateral center of the winding path at depth z
float pathX(float z) { return 0.9 * (sin(z * 0.16 + 0.8) - sin(0.8)); }

// stone lantern (toro) silhouette, o = (lateral, height) in world units, ground at o.y = 0
float sdToro(vec2 o)
{
    float d = EwSdBox2D(o - vec2(0.0, 0.04), vec2(0.17, 0.04));          // base
    d = min(d, EwSdBox2D(o - vec2(0.0, 0.25), vec2(0.055, 0.18)));       // shaft
    d = min(d, EwSdBox2D(o - vec2(0.0, 0.45), vec2(0.15, 0.03)));        // platform
    d = min(d, EwSdBox2D(o - vec2(0.0, 0.55), vec2(0.1, 0.07)));         // fire box
    float ry = o.y - 0.62;
    d = min(d, max(abs(ry - 0.05) - 0.05, (abs(o.x) - (0.25 - ry * 1.7)) * 0.5)); // roof
    d = min(d, length(o - vec2(0.0, 0.75)) - 0.035);                     // finial
    return d;
}

// jizo statue silhouette (pedestal, robed body, round head); bib = signed distance of the red bib
float sdJizo(vec2 o, out float bib)
{
    float ped = EwSdRoundedBox2D(o - vec2(0.0, 0.05), vec2(0.13, 0.05), 0.015);
    float body = max((length((o - vec2(0.0, 0.22)) / vec2(0.1, 0.15)) - 1.0) * 0.1, 0.1 - o.y);
    float head = length(o - vec2(0.0, 0.4)) - 0.065;
    vec2 bq = o - vec2(0.0, 0.335);
    bib = max(max(-bq.y - 0.13, bq.y), abs(bq.x) - 0.1 * (1.0 + bq.y / 0.13));
    return min(ped, min(body, head));
}

vec3 forest(vec2 uv, float t, float px, vec4 scPX0, vec4 scFg0, vec4 scPF1)
{
    // horizon height (screen units), camera height (world units)
    const float PATH_HOR = 0.04;
    const float PATH_CAM_H = 0.5;
    vec3 fogC = FogColor;
    float fogK = mix(0.02, 0.2, Mist);
    vec3 moonC = MoonColor * MoonBrightness;
    vec3 lightC = PathLightColor * PathLightGlow * LanternGlow * (1.0 / 1.2);
    float yy = uv.y - PATH_HOR;
    // depth of field: the scene behind the gate is out of focus; b = blur radius in screen units
    float b = max(px, FocusBlur);

    // night sky: gradient, stars, moon with halo
    vec3 col = mix(fogC * 1.2, fogC * 0.35, smoothstep(0.0, 0.5, yy));
    // stars: soft blurred dots (only above horizon)
    if (yy > 0.03 && StarDensity > 0.0) {
        vec2 sc = uv * 40.0;
        vec3 sh = EwHash2To3(floor(sc));
        if (sh.x >= 1.0 - StarDensity) {
            float sd = length(fract(sc) - 0.3 - 0.4 * sh.yz) * 0.025;
            float srad = clamp(0.7 * b, 1.5 * px, 0.0075);
            col += vec3(0.75, 0.8, 1.0) * smoothstep(srad, 0.2 * srad, sd) * (0.2 + 0.5 * sh.y) * smoothstep(0.03, 0.2, yy) * clamp(3.0 * px / srad, 0.4, 1.0);
        }
    }
    vec2 mq = uv - MoonPosition;
    float mr = 0.045 * MoonSize;
    float md = length(mq);
    col += moonC * 0.25 * exp(-md / (0.12 * MoonSize));
    if (md < mr + b) {
        float maria = EwValueNoise2D(mq / mr * 2.5 + 3.0);
        col = mix(col, moonC * (1.3 - 0.45 * smoothstep(0.45, 0.7, maria)), smoothstep(mr + b, mr - b, md));
    }

    // ground: dark grass, pale gravel path with stepping stones, warm light pools under the stone lanterns
    if (yy < 0.0) {
        float z = PATH_CAM_H / max(-yy, 1e-3);
        float wx = uv.x * z;
        float dp = wx - pathX(z);
        vec2 gp = vec2(wx, z);
        // blur radius on the ground in world units: across (x) and along the depth (z, foreshortened)
        float bx = b * z;
        float bz = b * z * z * (1.0 / PATH_CAM_H);
        float bm = max(bx, bz);
        float w1 = smoothstep(1.2, 0.3, bm * 2.5);
        float n1 = (w1 > 0.0) ? mix(0.5, fbmL(gp * 2.5, 3), w1) : 0.5;
        float w2 = smoothstep(0.6, 0.15, bm * 14.0);
        float n2 = (w2 > 0.0) ? mix(0.5, EwValueNoise2D(gp * 14.0), w2) : 0.5;
        float pathM = smoothstep(0.85 + bx, 0.6 - bx, abs(dp) + 0.3 * (n1 - 0.5));
        vec3 grass = vec3(0.012, 0.022, 0.016) * (0.6 + 0.8 * n1);
        vec3 gravel = vec3(0.07, 0.075, 0.085) * (0.6 + 0.6 * n2) * (0.8 + 0.4 * n1);
        vec3 alb = mix(grass, gravel, pathM);
        float sw = min(bx * (2.0 / 0.45) + bz * (1.6 / 0.36), 0.35);   // stone edge blur in st units
        float stoneFade = smoothstep(25.0, 8.0, z) * smoothstep(0.35, 0.2, sw);
        if (stoneFade > 0.0 && abs(dp) < 1.2) {
            float sz = z * 1.6;
            float h1 = EwHash1To1(floor(sz));
            vec2 sl = vec2((dp - (h1 - 0.5) * 0.3) * 2.0, fract(sz) - 0.5);
            float st = length(sl * vec2(1.0 / 0.45, 1.0 / 0.36)) + 0.25 * (n1 - 0.5);
            float stone = smoothstep(0.95 + sw, 0.9 - sw, st) * stoneFade;
            alb *= 1.0 - 0.5 * smoothstep(1.2 + sw, 0.95 - sw, st) * stoneFade * (1.0 - stone);
            alb = mix(alb, vec3(0.085, 0.09, 0.1) * (0.7 + 0.5 * n2), stone);
        }
        float pool = 0.0;
        for (int k = ZERO; k < 6; k++) {
            float kz = SculptureStart + float(k) * SculptureSpacing;
            float pxz = (k == 0) ? scPX0.x : ((k == 1) ? scPX0.y : ((k == 2) ? scPX0.z : ((k == 3) ? scPX0.w : ((k == 4) ? scPF1.x : scPF1.y))));
            float side = ((k & 1) == 0) ? -1.25 : 1.25;
            vec2 dl = gp - vec2(pxz + side, kz);
            float r2 = dot(dl, dl);
            if (r2 < 7.0) pool += exp(-r2 * 2.5);
        }
        vec3 g = alb * (moonC * 0.6 + lightC * 2.0 * pool);
        col = mix(g, col, 1.0 - exp(-z * fogK));
    }

    // tree trunks, far to near: flat silhouettes with a cool moonlit rim
    const float trPX[5] = float[5](
        0.9 * (sin(22.0 * 0.16 + 0.8) - sin(0.8)),
        0.9 * (sin(13.64 * 0.16 + 0.8) - sin(0.8)),
        0.9 * (sin(8.4568 * 0.16 + 0.8) - sin(0.8)),
        0.9 * (sin(5.243216 * 0.16 + 0.8) - sin(0.8)),
        0.9 * (sin(3.25079392 * 0.16 + 0.8) - sin(0.8))
    );
    float tz = 22.0;
    for (int i = ZERO; i < 5; i++) {
        float fi = float(i);
        float z = tz;
        tz *= 0.62;
        float wy = yy * z + PATH_CAM_H;
        float aa = b * z;   // blur radius in world units at this depth
        if (wy < -aa) continue;
        float off = fi * 13.7;
        float sx = uv.x * z + off;
        float id = floor(sx * 0.5);
        float h = EwHash2To1(vec2(id, fi));
        float cx = (id + 0.25 + 0.5 * h) * 2.0;
        if (abs(cx - (trPX[i] + off)) < 1.7) continue;
        float dx = sx - cx;
        if (abs(dx) >= 0.364 + aa) continue;
        float r = (0.13 + 0.15 * EwHash2To1(vec2(id, fi + 7.0))) * (1.0 + 0.3 * exp(-wy * 5.0));
        float cov = smoothstep(r + aa, r - aa, abs(dx)) * smoothstep(-aa, aa, wy);   // soft sides and soft foot
        if (cov <= 0.0) continue;
        float nx = clamp(dx / r, -1.0, 1.0);
        float bark = EwValueNoise2D(vec2(nx * 3.0 + id * 7.0, wy * 2.0 + fi));
        vec3 tc = vec3(0.012, 0.011, 0.01) * (0.6 + 0.8 * bark) + moonC * 0.05 * smoothstep(-0.3, -0.95, nx) * (0.5 + bark);
        tc = mix(tc, fogC, 1.0 - exp(-z * fogK));
        col = mix(col, tc, cov);
    }

    // roadside sculptures, far to near: stone lanterns with warm windows and jizo statues
    for (int kk = ZERO; kk < 6; kk++) {
        int k = 5 - kk;
        float z = SculptureStart + float(k) * SculptureSpacing;
        float pxz = (k == 0) ? scPX0.x : ((k == 1) ? scPX0.y : ((k == 2) ? scPX0.z : ((k == 3) ? scPX0.w : ((k == 4) ? scPF1.x : scPF1.y))));
        float fog = (k == 0) ? scFg0.x : ((k == 1) ? scFg0.y : ((k == 2) ? scFg0.z : ((k == 3) ? scFg0.w : ((k == 4) ? scPF1.z : scPF1.w))));
        float aa = b * z;   // blur radius in world units at this depth
        float oy = yy * z + PATH_CAM_H;
        float wx = uv.x * z - pxz;
        // halo from the one stone lantern in this row
        float sideT = ((k & 1) == 0) ? -1.25 : 1.25;
        vec2 woT = vec2(wx - sideT, oy - 0.55);
        float r2T = dot(woT, woT);
        if (r2T < 9.0) col += lightC * 0.12 * exp(-sqrt(r2T) * 5.0) * (1.0 - 0.7 * fog);
        if (oy < -0.02 - aa || oy > 0.8 + aa) continue;
        // at most one side (j = 0 at -1.25 or j = 1 at +1.25) can cover this pixel
        int j = (wx >= 0.0) ? 1 : 0;
        float ox = wx - ((j == 0) ? -1.25 : 1.25);
        if (abs(ox) > 0.3 + aa) continue;
        vec2 o = vec2(ox, oy);
        bool toro = ((k + j) & 1) == 0;
        vec2 wo = o - vec2(0.0, 0.55);
        vec2 rimD = vec2(-0.7, 0.7) * 0.025;    // toward the moon
        float bib = 1.0;
        // two samples in a loop (rim offset first, then the shape itself): one inlined copy of the SDFs
        float dR = 0.0, d = 0.0;
        for (int m = ZERO; m < 2; m++) {
            vec2 os = (m == 0) ? o + rimD : o;
            float ds = toro ? sdToro(os) : sdJizo(os, bib);
            if (m == 0) dR = ds; else d = ds;
        }
        float cov = smoothstep(aa, -aa, d);
        if (cov <= 0.0) continue;
        // stone texture: fine grain fades out when it is smaller than the blur
        float lod1 = smoothstep(0.6, 0.2, aa * 14.0);
        float lodS = smoothstep(0.6, 0.2, aa * 45.0);
        float n1 = (lod1 > 0.0) ? mix(0.5, EwValueNoise2D(o * 14.0 + float(k * 2 + j) * 7.0), lod1) : 0.5;
        float n2 = (lodS > 0.0) ? mix(0.5, EwValueNoise2D(o * 45.0), lodS) : 0.5;
        float n = n1 * 0.7 + 0.3 * n2;
        vec3 alb = vec3(0.1, 0.1, 0.095) * (0.7 + 0.5 * n);
        alb = mix(alb, vec3(0.035, 0.06, 0.02), smoothstep(0.12 + aa, -aa, o.y) * smoothstep(0.3, 0.7, n) * MossAmount * 1.6);
        if (!toro) alb = mix(alb, BibColor * (0.7 + 0.4 * n), smoothstep(aa, -aa, bib));
        float rim = smoothstep(-aa, 0.025 + aa, dR);
        vec3 sc2 = alb * (moonC * (0.35 + 1.2 * rim) + lightC * 0.6 * exp(-length(wo) * (toro ? 7.0 : 4.0)));
        if (toro) {
            float win = smoothstep(aa, -aa, EwSdBox2D(wo, vec2(0.06, 0.04)));
            sc2 = mix(sc2, lightC * (1.6 + 0.6 * smoothstep(0.04 + aa, 0.0, abs(wo.y))), win);
        }
        sc2 = mix(sc2, fogC, fog);
        col = mix(col, sc2, cov);
    }

    // low ground mist along the horizon
    col = mix(col, fogC * 1.6, Mist * GroundMist * exp(-abs(yy + 0.03) * 14.0));

    // fireflies: a few slow, softly blinking points of light, out of focus = soft bokeh discs
    float fr = max(2.0 * px, 1.2 * b);
    float fk = 0.35 + 1.15 * clamp(2.0 * px / fr, 0.0, 1.0);
    float fMaxR = max(fr, 0.12);
    float fMaxR2 = fMaxR * fMaxR;
    for (int i = ZERO; i < FireflyCount; i++) {
        float fi = float(i);
        float blink = smoothstep(0.2, 1.0, sin(t * 0.9 + fi * 4.0));
        if (blink <= 0.0) continue;
        vec2 h = EwHash1To2(fi * 3.7 + 1.3);
        vec2 fp = vec2((h.x - 0.5) * 1.7, -0.3 + 0.35 * h.y) + 0.06 * vec2(sin(t * 0.23 + fi * 2.1), sin(t * 0.31 + fi * 1.3));
        vec2 df = uv - fp;
        float fd2 = dot(df, df);
        if (fd2 >= fMaxR2) continue;
        float fd = sqrt(fd2);
        col += FireflyColor * blink * (smoothstep(fr, 0.4 * fr, fd) * fk + 0.12 * exp(-fd * 90.0));
    }
    return col;
}

// ---------------------------------------------------------------- creatures
vec4 crow(vec2 q, float ph, float t, float pxH)
{
    float cyc = mod(t + ph * 7.0, 9.0);
    float peck = smoothstep(0.0, 0.25, cyc - 6.0) * smoothstep(1.0, 0.75, cyc - 6.0);
    float look = sin(t * 0.7 + ph * 3.0);
    float fcy = mod(t + ph * 11.0, 13.0);
    float flap = smoothstep(0.0, 0.15, fcy - 10.0) * smoothstep(1.4, 1.0, fcy - 10.0) * (0.5 + 0.5 * sin(fcy * 18.0));
    vec2 hc = vec2(0.42, 0.86) + vec2(0.14, -0.32) * peck + vec2(0.0, 0.025 * look);
    vec2 b = EwRotate2D(-0.45) * (q - vec2(-0.05, 0.5));
    float dB = (length(b / vec2(0.5, 0.26)) - 1.0) * 0.26;
    float dHd = length(q - hc) - 0.18;
    float dN = EwSdSegment2D(q, vec2(0.2, 0.62), hc) - 0.14;
    float dT = EwSdSegment2D(q, vec2(-0.35, 0.4), vec2(-0.95, 0.12)) - 0.08;
    vec2 bd = normalize(vec2(1.0, -0.15 - 0.8 * peck));
    float dK = EwSdSegment2D(q, hc + bd * 0.1, hc + bd * 0.4) - 0.055 * (1.0 - clamp(dot(q - hc, bd) / 0.45, 0.0, 1.0)) - 0.01;
    float dLg = min(EwSdSegment2D(q, vec2(-0.03, 0.3), vec2(0.0, 0.0)), EwSdSegment2D(q, vec2(0.1, 0.3), vec2(0.14, 0.0))) - 0.025;
    float wa = 0.4 - flap * 1.9;
    vec2 wq = EwRotate2D(-wa) * (q - vec2(0.05, 0.62));
    float dWg = (length((wq - vec2(-0.45, 0.0)) / vec2(0.55, 0.15)) - 1.0) * 0.15;
    float dBody = min(min(dB, dHd), min(dN, dT));
    float d = min(min(dBody, dWg), min(dK, dLg));
    float cov = smoothstep(pxH, -pxH, d);
    if (cov <= 0.0) return vec4(0.0);
    float ins = clamp(-dBody / 0.15, 0.0, 1.0);
    vec3 col = vec3(0.012, 0.012, 0.017) * (0.6 + 0.9 * ins);
    float hl = exp(-pow((b.y - 0.13) / 0.07, 2.0)) * step(dB, 0.0);
    hl += exp(-pow(length(q - hc - vec2(-0.03, 0.09)) / 0.08, 2.0));
    hl += step(dWg, 0.0) * smoothstep(0.0, 0.1, wq.y + 0.05) * 0.5;
    col += vec3(0.09, 0.1, 0.14) * hl * 0.25 * (0.3 + SunStrength);
    col += vec3(0.008) * EwValueNoise2D(vec2(q.x * 12.0, q.y * 40.0));
    col = mix(col, vec3(0.03, 0.028, 0.026), step(dK, 0.0) * step(0.0, dHd));
    float eye = length(q - hc - vec2(0.07, 0.04));
    col = mix(col, vec3(0.06, 0.04, 0.02), smoothstep(0.04, 0.025, eye));
    col += vec3(0.9) * smoothstep(0.018, 0.008, length(q - hc - vec2(0.08, 0.055)));
    return vec4(col * cov, cov);
}

vec4 spider(vec2 q, float t, float ph, float pxH)
{
    vec2 ab = q - vec2(0.0, -0.17);
    float dA = (length(ab / vec2(0.09, 0.17)) - 1.0) * 0.09;
    vec2 ce = q - vec2(0.0, -0.38);
    float dC = length(ce / vec2(1.0, 1.2)) - 0.06;
    vec2 m = vec2(abs(ce.x), ce.y);
    float dL = 1e5;
    for (int k = ZERO; k < 4; k++) {
        float fk = float(k);
        float a = mix(-1.25, 1.0, fk / 3.0) + 0.12 * sin(t * 2.5 + fk * 1.7 + ph);
        float l1 = (k == 2) ? 0.17 : 0.26;
        vec2 knee = vec2(0.03, 0.0) + l1 * vec2(cos(a + 0.6), sin(a + 0.6));
        vec2 foot = knee + l1 * 1.15 * vec2(cos(a - 0.5), sin(a - 0.5));
        dL = min(dL, min(EwSdSegment2D(m, vec2(0.02, 0.0), knee), EwSdSegment2D(m, knee, foot)));
    }
    dL -= 0.012;
    float d = min(min(dA, dC), dL);
    float cov = smoothstep(pxH, -pxH, d - 0.3 * pxH);
    if (cov <= 0.0) return vec4(0.0);
    vec3 col = vec3(0.02, 0.018, 0.015);
    if (dA < 0.0) {
        float band = smoothstep(0.1, 0.5, sin(ab.y * 60.0 + 1.0));
        col = mix(vec3(0.03, 0.035, 0.02), vec3(0.55, 0.45, 0.06), band * 0.85);
        col *= 0.5 + 0.7 * clamp(-dA / 0.06, 0.0, 1.0);
        col += vec3(0.4) * exp(-dot(ab - vec2(-0.03, 0.05), ab - vec2(-0.03, 0.05)) / 0.001);
        col = mix(col, vec3(0.5, 0.06, 0.03), smoothstep(0.05, 0.03, length(ab - vec2(0.0, -0.13))));
    } else if (dL < dC) {
        col = mix(col, vec3(0.35, 0.3, 0.05), smoothstep(0.6, 0.9, sin(length(m) * 70.0)));
    }
    return vec4(col * SunStrength * 0.6 + col * 0.4, 1.0) * cov;
}

// ---------------------------------------------------------------- vertex + main
varying vec2 vP;
flat varying vec4 vGate0;   // hs.x, hs.y, H, rad
flat varying vec4 vGate1;   // yN, tN, yS0, yT
flat varying vec4 vGate2;   // xe, yBot, rowW, invFh
flat varying vec4 vOpenT;   // openH.x, openH.y, t, gLanI
flat varying vec4 vLan0;    // gLan[0].xy, lanPiv[0].xy
flat varying vec4 vLan1;    // gLan[1].xy, lanPiv[1].xy
flat varying vec4 vLanA;    // ang[0], ang[1], flick[0], flick[1]
flat varying vec4 vScPX0;   // pathX(z0..3)
flat varying vec4 vScFg0;   // fog(z0..3)
flat varying vec4 vScPF1;   // pathX(z4), pathX(z5), fog(z4), fog(z5)

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vP = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    vec2 openH = hs;
    if (SwHaveImage) {
        vec2 imgRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = imgRes.x / imgRes.y;
        float kMin = max(2.8, 2.0 / imgAspect);
        inset = min(inset, min(0.5 * res.x / (imgAspect * kMin + 2.7115),
                               0.5 * res.y / (kMin + 1.609)));
        float H0 = inset * 1.05;
        vec2 pad = vec2(1.63, 0.58) * H0;
        vec2 maxOpen = max(0.5 * res - inset - pad, vec2(1.0));
        if (imgAspect > maxOpen.x / maxOpen.y) {
            openH = vec2(maxOpen.x, maxOpen.x / imgAspect);
        } else {
            openH = vec2(maxOpen.y * imgAspect, maxOpen.y);
        }
        hs = openH + pad;
    }
    float t = mod(iTime * Speed, 1200.0);
    float H = inset * 1.05;
    float yN = hs.y - 2.45 * H;
    float tN = 0.34 * H;
    float yS0 = hs.y - 0.62 * H;
    float yT = -hs.y + 0.5 * H;
    float xe = hs.x + inset - max(0.05 * inset, 3.0);
    float mrgB = max(0.2 * inset, 7.0);
    float yBot = -hs.y - inset + mrgB;
    float rowW = 2.0 * (hs.x + inset - mrgB);
    float fh = SwHaveImage ? (2.0 * openH.y) : res.y;
    float rad = min(CornerRadius, min(hs.x, hs.y));

    float yP = min(max(mix(yT, yN - tN, 0.62), yT + 3.9 * H), yN - tN - 0.3 * H);
    float cxP, wP;
    pillarAt(yP, hs.x, H, yT, yS0, cxP, wP);
    vec2 piv0 = vec2(-cxP, yP);
    vec2 piv1 = vec2(cxP, yP);
    float ang0 = LanternSwing * (0.05 * sin(t * 1.1) + 0.02 * sin(t * 2.3));
    float ang1 = LanternSwing * (0.05 * sin(t * 1.1 + 2.0) + 0.02 * sin(t * 2.3 + 4.0));
    vec2 lan0 = piv0 + EwRotate2D(ang0) * vec2(0.0, -1.48) * H;
    vec2 lan1 = piv1 + EwRotate2D(ang1) * vec2(0.0, -1.48) * H;
    float flk0 = 1.0 - FlickerAmount + FlickerAmount * EwValueNoise2D(vec2(t * 7.0, 0.0));
    float flk1 = 1.0 - FlickerAmount + FlickerAmount * EwValueNoise2D(vec2(t * 7.0, 9.0));
    float lanI = LanternGlow * 0.5 * (flk0 + flk1) * 0.5;

    vGate0 = vec4(hs, H, rad);
    vGate1 = vec4(yN, tN, yS0, yT);
    vGate2 = vec4(xe, yBot, rowW, 1.0 / fh);
    vOpenT = vec4(openH, t, lanI);
    vLan0 = vec4(lan0, piv0);
    vLan1 = vec4(lan1, piv1);
    vLanA = vec4(ang0, ang1, flk0, flk1);

    float fogK = mix(0.02, 0.2, Mist);
    float z0 = SculptureStart;
    float z1 = z0 + SculptureSpacing;
    float z2 = z1 + SculptureSpacing;
    float z3 = z2 + SculptureSpacing;
    float z4 = z3 + SculptureSpacing;
    float z5 = z4 + SculptureSpacing;
    vScPX0 = vec4(pathX(z0), pathX(z1), pathX(z2), pathX(z3));
    vScFg0 = 1.0 - exp(-vec4(z0, z1, z2, z3) * fogK);
    vScPF1 = vec4(pathX(z4), pathX(z5), 1.0 - exp(-z4 * fogK), 1.0 - exp(-z5 * fogK));
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = vP;
    vec2 hs = vGate0.xy;
    float H = vGate0.z;
    float rad = vGate0.w;
    float yN = vGate1.x;
    float tN = vGate1.y;
    float yS0 = vGate1.z;
    float yT = vGate1.w;
    float xe = vGate2.x;
    float yBot = vGate2.y;
    float rowW = vGate2.z;
    float invFh = vGate2.w;
    vec2 openH = vOpenT.xy;
    float t = vOpenT.z;
    gH = H;
    gLanI = vOpenT.w;

    vec4 bg = vec4(Background * Opacity, Opacity);
    if (SwHaveImage && (abs(p.x) > max(xe + 4.0, hs.x + 2.2 * H) || p.y > hs.y + H + 4.0 || p.y < yBot - 0.5 * H)) {
        fragColor = bg;
        return;
    }

    float d = EwSdRoundedBox2D(p, hs, rad);
    gLan[0] = vLan0.xy;
    gLan[1] = vLan1.xy;
    vec2 lanPiv[2];
    lanPiv[0] = vLan0.zw;
    lanPiv[1] = vLan1.zw;
    float ang[2];
    ang[0] = vLanA.x;
    ang[1] = vLanA.y;
    float flick[2];
    flick[0] = vLanA.z;
    flick[1] = vLanA.w;
    vec3 col = vec3(0.0);
    float a = 0.0;
    if (!(d < -4.0 * H && p.y < yN - tN - 0.3 * H)) {
        for (int i = ZERO; i < 2; i++) {
            vec2 dl = (gLan[i] - p) / gH;
            float r2 = dot(dl, dl);
            gLanDir[i] = vec3(dl, 1.2) * inversesqrt(r2 + 1.44);
            gLanAtt[i] = gLanI / (1.0 + r2 * 0.4);
        }
        vec4 acc = vec4(0.0);
        vec2 s = p / H;
        float pxH = 1.0 / H;

    // corner spider webs
    for (int i = ZERO; i < 2; i++) {
        float sx = float(i) * 2.0 - 1.0;
        vec2 cp = vec2(sx * (hs.x - 1.72 * H), yS0 - 0.42 * H);
        vec2 w = (p - cp) * vec2(-sx, -1.0);
        float r = length(w);
        if (w.x > -1.0 && w.y > -1.0 && r < 3.2 * H) {
            float a = atan(w.y, max(w.x, 1e-3)) / 1.5708;
            float sp = abs(fract(a * 6.0 + 0.5) - 0.5) / 6.0 * 1.5708 * r;
            float rs = 0.24 * H;
            float rr = r * (1.0 + 0.05 * cos(a * 37.7));
            float rg = abs(fract(rr / rs) - 0.5) * rs;
            float torn = step(0.3, EwHash2To1(vec2(floor(rr / rs), floor(a * 6.0) + sx * 9.0)));
            float web = max(smoothstep(0.9, 0.0, sp), smoothstep(0.9, 0.0, rg) * step(0.4 * H, r) * torn);
            web *= smoothstep(3.2 * H, 2.4 * H, r) * (0.25 + 0.5 * SunStrength * smoothstep(0.7, 1.0, sin(r / H * 3.0 - t * 0.5 + a * 4.0)));
            web *= WebStrength;
            acc = over(vec4(vec3(0.75, 0.78, 0.8) * web, web * 0.6), acc);
        }
    }

    // ================= gate structure, back to front =================
    float ax = abs(p.x);
    float sgx = (p.x < 0.0) ? -1.0 : 1.0;
    float ux = clamp(ax / xe, 0.0, 1.0);
    float ux2 = ux * ux;
    float cv = 0.38 * H * ux2 * ux2 * ux;
    float xP = hs.x - 0.85 * H;
    float W = Weathering;

    // stone threshold: a row of separate, fully visible blocks (rounded, chipped silhouettes),
    // seen slightly from above: top face, rounded front edge, front face, soft contact shadow
    if (p.y < yT + 0.3 * H && p.y > yBot - 0.4 * H && abs(p.x) < 0.5 * rowW + 0.4 * H) {
        // whole blocks only: the row is split evenly across the gate width (no cut off stones)
        float NB = max(1.0, floor(rowW / (3.1 * H) + 0.5));
        float bwP = rowW / NB;
        float bx = clamp((p.x + 0.5 * rowW) / bwP, 0.0, NB - 0.001);
        float bid = floor(bx);
        float hb = EwHash1To1(bid + 3.0);
        float yTop = yT + (0.12 + (hb - 0.5) * 0.12) * H;
        float hgt = yTop - yBot;
        float yF = yTop - min(0.42 * H, 0.38 * hgt);
        vec2 hsB = vec2(0.5 * bwP - 0.05 * H - 0.03 * H * hb, 0.5 * hgt);
        float rB = min(0.2 * H, 0.45 * hsB.y);
        vec2 lp = vec2(p.x + 0.5 * rowW - (bid + 0.5) * bwP, p.y - 0.5 * (yTop + yBot));
        vec2 sq = s + vec2(bid * 5.3, 0.0);
        // soft contact shadow in the joints (fixed row shape, fades out above / below the blocks)
        float yMid = 0.5 * (yT + 0.12 * H + yBot);
        float hRow = 0.5 * (yT + 0.12 * H - yBot);
        float dRow = EwSdRoundedBox2D(vec2(p.x, p.y - yMid), vec2(0.5 * rowW - 0.1 * H, hRow - 0.18 * H), 0.3 * H);
        float shA = 0.8 * smoothstep(0.12 * H, -0.2 * H, dRow);
        acc = over(vec4(vec3(0.006, 0.007, 0.005) * shA, shA), acc);
        float dR = EwSdRoundedBox2D(lp, hsB, rB);
        float chipN = fbmL(sq * 2.6 + 17.0, 4);
        float chip = smoothstep(0.42, 0.72, chipN) * (0.35 + 0.65 * W);
        float dB = dR + (chipN - 0.4) * 0.16 * H * (0.4 + 0.6 * W);
        float cov = smoothstep(0.75, -0.75, dB);
        if (cov > 0.0) {
            vec2 gR = vec2(EwSdRoundedBox2D(lp + vec2(1.0, 0.0), hsB, rB), EwSdRoundedBox2D(lp + vec2(0.0, 1.0), hsB, rB)) - dR;
            gR /= max(length(gR), 1e-4);
            // three micro relief layers (base relief, pitted grain, fine grain) in one loop
            float n1, mvA, mvB;
            vec2 gb, mgA, mgB;
            microGrad3(sq, vec3(1.6, 13.0, 46.0), vec3(0.0, bid, bid * 2.0), gb, n1, mgA, mvA, mgB, mvB);
            gb *= 1.6;
            n1 = n1 * 0.95;
            float n2 = EwValueNoise2D(sq * 9.0);
            vec3 gh = EwHash2To3(floor(sq * 38.0) + bid * 31.0);
            float glint = step(0.9, gh.z);
            // shape: front face, rounded front edge, top face, pillowed bevel along the outline
            float bvW = (0.14 + 0.22 * chip) * H;
            float kb = 1.0 - clamp(-dB / bvW, 0.0, 1.0);
            float tf = smoothstep(yF - 0.07 * H, yF + 0.07 * H, p.y);
            vec3 nb = normalize(mix(vec3(0.0, -0.12, 1.0), vec3(0.0, 0.95, 0.3), tf));
            vec3 n = normalize(nb + vec3(gR * kb * kb * 1.7, 0.0));
            float rough = mix(0.85, 0.6, kb * chip);
            n = normalize(n + vec3(gb * (0.12 + 0.2 * chip) + mgA * 0.3 + mgB * 0.2 + (gh.xy - 0.5) * 0.6 * glint, 0.0));
            // albedo: granite tone, speckles, pits, lighter fresh chips, cracks, lichen, moss
            vec3 alb = vec3(0.16, 0.156, 0.145) * (0.45 + 0.85 * n1) * (0.82 + 0.3 * hb) * (0.85 + 0.3 * n2);
            alb *= 0.75 + 0.5 * mvB;
            alb *= 1.0 - 0.5 * smoothstep(0.62, 0.74, mvA);
            alb = mix(alb, vec3(0.04, 0.038, 0.035), step(0.93, gh.y) * 0.7);
            alb *= mix(1.0, 1.15, tf) * (1.0 + 0.35 * chip * kb);
            float cr = abs(EwValueNoise2D(sq * vec2(1.1, 1.7) + 3.1) - 0.5);
            float crack = smoothstep(0.025, 0.0, cr) * smoothstep(0.42, 0.6, n1) * W;
            float lich = smoothstep(0.74, 0.79, EwValueNoise2D(sq * 3.5 + 40.0) + 0.05 * mvB) * W;
            alb = mix(alb, vec3(0.32, 0.32, 0.24) * (0.8 + 0.4 * n2), lich * 0.55);
            alb *= 1.0 - 0.8 * crack;
            float mv = fbmL(sq * 1.2 + 11.0, 4) + 0.6 * MossAmount - 0.4 + 0.35 * kb + 0.08 * tf - 0.12 * (p.y - yF) / H;
            float fuzz = fbmL(sq * 7.0 + 5.0, 4) * 1.07;
            float moss = smoothstep(0.55, 0.68, mv + 0.35 * (fuzz - 0.47) + 0.1 * (mvB - 0.5)) * step(0.001, MossAmount);
            alb = mix(alb, mix(vec3(0.012, 0.028, 0.007), vec3(0.045, 0.075, 0.018), fuzz) * (0.7 + 0.6 * mvB), moss);
            alb *= mix(0.8, 1.0, tf);
            n = normalize(mix(n, normalize(vec3(mgB * 0.8 + (fuzz - 0.5), 1.0) + vec3(0.0, 0.4, 0.0)), moss * 0.6));
            vec3 F0 = vec3(mix(0.03, 0.2, glint * (1.0 - moss)));
            rough = mix(rough, 0.3, glint * (1.0 - moss));
            rough = mix(rough, 0.95, moss);
            float ao = mix(0.35, 1.0, smoothstep(0.0, 0.35 * H, -dB));
            ao = mix(ao, 1.0, 0.5 * tf);
            ao *= 1.0 - 0.3 * crack;
            ao *= 1.0 - 0.35 * smoothstep(0.55, 0.75, mvA);
            ao *= mix(0.6, 1.0, smoothstep(yBot, yBot + 0.5 * H, p.y));
            float kp = length(vec2((ax - xP) / 0.95, max(p.y - yT, 0.0) / 0.6) / H);
            ao *= mix(0.35, 1.0, smoothstep(0.9, 1.5, kp));
            float rel = (p.x - sgx * xP) / H;
            float shd = 1.0 - 0.75 * tf * smoothstep(0.85, 1.05, rel) * smoothstep(2.4, 1.6, rel);
            acc = over(vec4(shadeM(alb, n, ao, shd, F0, rough) * cov, cov), acc);
        }
    }

    // painted wooden parts, back to front: cheap SDF pass finds the 2 frontmost part IDs (no 44-float structs)
    float cxN, wN;
    pillarAt(yN, hs.x, H, yT, yS0, cxN, wN);
    float cxT = hs.x - 0.93 * H;
    float wD = 0.79 * H;
    float xNe = min(cxN + wN + 0.45 * H, xe - 0.05 * H);
    float cxH, wH;
    pillarAt(p.y, hs.x, H, yT, yS0, cxH, wH);
    float yD0 = yS0 - 0.42 * H;
    float ySb = yS0 + 0.6 * cv;
    float ySt = hs.y + 0.02 * H + 0.6 * cv;
    float xSe = xe - 0.32 * H;
    float yKt = ySt + 0.5 * H + 0.4 * cv;
    float xEnd = xe - 0.45 * clamp(yKt - p.y, 0.0, H);

    int id1 = -1;
    float minSD = 1e5;
    {
        float s0 = max(abs(p.y - yN) - tN, ax - xNe);
        if (s0 < 0.5) { if (s0 < 0.0 || id1 < 0) id1 = 0; minSD = min(minSD, s0); }
        float wx1 = ax - (cxN + wN + 0.17 * H);
        float wy1 = (p.y - yN) / (tN * 1.3);
        float s1 = max(abs(wx1) - 0.1 * H * (1.0 - 0.25 * wy1), abs(p.y - yN) - tN * 1.3);
        if (s1 < 0.5) { if (s1 < 0.0 || id1 < 0) id1 = 1; minSD = min(minSD, s1); }
        float s2 = max(ax - 0.17 * H, max(yN + tN - 2.0 - p.y, p.y - yS0 - 2.0));
        if (s2 < 0.5) { if (s2 < 0.0 || id1 < 0) id1 = 2; minSD = min(minSD, s2); }
        float s3 = max(abs(ax - cxH) - wH, max(yT - 0.2 * H - p.y, p.y - yS0 - 0.3 * H));
        if (s3 < 0.5) { if (s3 < 0.0 || id1 < 0) id1 = 3; minSD = min(minSD, s3); }
        float s4 = max(abs(ax - cxT) - wD, max(yD0 - p.y, p.y - yS0 - 0.05 * H));
        if (s4 < 0.5) { if (s4 < 0.0 || id1 < 0) id1 = 4; minSD = min(minSD, s4); }
        float s5 = max(max(ySb - p.y, p.y - ySt), ax - xSe);
        if (s5 < 0.5) { if (s5 < 0.0 || id1 < 0) id1 = 5; minSD = min(minSD, s5); }
        float s6 = max(max(ySt - p.y, p.y - yKt), (ax - xEnd) * 0.91);
        if (s6 < 0.5) { if (s6 < 0.0 || id1 < 0) id1 = 6; minSD = min(minSD, s6); }
    }
    // shade the frontmost wood part with exact union silhouette coverage: 1 paint() instance, no outer loop
    if (id1 >= 0) {
        {
            int k = id1;
            float sdE = minSD;
            vec2 q = vec2(0.0);
            float seed = 0.0;
            vec3 baseC = GateColor;
            float wear = 0.0;
            bool vert = false;
            vec3 n0 = vec3(0.0, 0.0, 1.0);
            float ao = 1.0;
            float shd = 1.0;
            float dust = 0.0, ringM = 0.0, ry = 0.0, topF = 0.0, aux = 0.0;
            if (k == 0) {            // nuki: lower crossbeam through the pillars
                float vN = clamp((p.y - yN) / tN, -1.0, 1.0);
                float kx = smoothstep(xNe - 0.15 * H, xNe, ax) * sgx * 0.6;
                n0 = vec3(kx, smoothstep(0.6, 1.0, abs(vN)) * sign(vN) * 0.7, 1.0);
                q = vec2(s.x, vN * tN / H) * 0.7;
                seed = 11.0;
                wear = 0.6 * smoothstep(0.5, 1.0, abs(vN)) + abs(kx);
                dust = smoothstep(0.55, 1.0, vN);
                ao = mix(0.35, 1.0, smoothstep(0.0, 0.45 * H, abs(ax - cxN) - wN));
                ao *= mix(1.0, mix(0.4, 1.0, smoothstep(0.17 * H, 0.5 * H, ax)), smoothstep(0.0, 1.0, vN));
                ao *= 0.8 + 0.2 * smoothstep(-1.0, 0.3, vN);
            } else if (k == 1) {     // kusabi wedges outside the pillars
                float wx = ax - (cxN + wN + 0.17 * H);
                float wy = (p.y - yN) / (tN * 1.3);
                n0 = vec3(clamp(wx / (0.1 * H), -1.0, 1.0) * 0.5 * sgx, 0.6 * smoothstep(0.75, 1.0, wy) - 0.6 * smoothstep(-0.75, -1.0, wy), 1.0);
                q = vec2(p.y / H, wx / H) * 0.8;
                seed = 17.0;
                baseC = vec3(0.03, 0.022, 0.016);
                wear = 0.6;
                vert = true;
                ao = 0.8;
            } else if (k == 2) {     // gakuzuka: short post behind the plaque
                float c = clamp(p.x / (0.17 * H), -1.0, 1.0);
                n0 = vec3(c * 0.9, 0.0, 1.0);
                q = vec2(s.y * 0.6, c * 0.2);
                seed = 21.0;
                wear = 0.2;
                vert = true;
                ao = mix(0.5, 1.0, smoothstep(yS0, yS0 - 0.4 * H, p.y)) * mix(0.5, 1.0, smoothstep(yN + tN, yN + tN + 0.4 * H, p.y));
            } else if (k == 3) {     // hashira: tapered pillars leaning inward, black base, brass ring
                float dx = ax - cxH;
                float c = clamp(dx / wH, -0.995, 0.995);
                aux = asin(c) * wH / H;
                n0 = vec3(sgx * c, 0.0, sqrt(1.0 - c * c));
                float yNem = yT + 1.6 * H;
                baseC = mix(GateColor, vec3(0.018, 0.016, 0.015), smoothstep(yNem + 1.0, yNem - 1.0, p.y));
                wear = 0.35 * smoothstep(yT + 2.5 * H, yT, p.y) + 0.25 * smoothstep(0.0, -0.8, n0.x);
                q = vec2(s.y * 0.6, aux * 0.9);
                seed = sgx * 7.0 + 1.0;
                vert = true;
                ry = (p.y - yNem) / (0.1 * H);
                ringM = clamp(0.1 * H - abs(p.y - yNem) + 0.5, 0.0, 1.0);
                ao = mix(0.4, 1.0, smoothstep(yT + 0.25 * H, yT + 1.1 * H, p.y));
                ao *= mix(0.45, 1.0, smoothstep(yS0 - 0.35 * H, yS0 - 0.9 * H, p.y));
                shd = 1.0 - 0.65 * smoothstep(yS0 - 0.75 * H, yS0 - 0.5 * H, p.y);
                shd *= 1.0 - 0.5 * smoothstep(yNem - 0.28 * H, yNem - 0.12 * H, p.y) * step(p.y, yNem - 0.1 * H);
            } else if (k == 4) {     // daiwa: black ring where the pillars meet the beams
                float dx = ax - cxT;
                float c = clamp(dx / wD, -0.995, 0.995);
                n0 = vec3(sgx * c, -0.7 * smoothstep(yD0 + 0.12 * H, yD0, p.y), sqrt(1.0 - c * c));
                q = vec2(asin(c) * 0.6, p.y / H * 0.6);
                seed = sgx * 13.0;
                baseC = vec3(0.018, 0.016, 0.015);
                wear = 0.3;
                ao = mix(0.55, 1.0, smoothstep(yS0, yS0 - 0.2 * H, p.y));
            } else if (k == 5) {     // shimaki: red lower top beam
                float v = clamp((p.y - ySb) / (ySt - ySb), 0.0, 1.0);
                float kx = sgx * 0.65 * smoothstep(xSe - 0.15 * H, xSe, ax);
                n0 = vec3(kx, -0.65 * smoothstep(0.15, 0.0, v), 1.0);
                q = vec2(s.x * 0.7, v * 0.45);
                seed = 3.0;
                wear = 0.5 * smoothstep(0.2, 0.0, v) + abs(kx);
                ao = mix(0.45, 1.0, smoothstep(1.0, 0.7, v));
                ao *= 1.0 - 0.45 * smoothstep(0.3 * H, 0.0, abs(ax - cxT) - wD) * smoothstep(0.45, 0.0, v);
                shd = 1.0 - 0.8 * smoothstep(0.68, 0.76, v + 0.05 * EwValueNoise2D(vec2(s.x * 3.0, 1.0)));
            } else {                 // kasagi: black top beam with upswept ends, copper roof
                float v = clamp((p.y - ySt) / (yKt - ySt), 0.0, 1.0);
                topF = smoothstep(0.6, 0.68, v);
                float kx = sgx * 0.7 * smoothstep(xEnd - 0.12 * H, xEnd, ax);
                n0 = vec3(kx, mix(-0.6 * smoothstep(0.14, 0.0, v), 1.3, topF), 1.0);
                q = vec2(s.x * 0.7, v * 0.35);
                seed = 5.0;
                baseC = vec3(0.02, 0.018, 0.017);
                wear = 0.4 * smoothstep(0.2, 0.0, v) + abs(kx);
                ao = mix(0.6, 1.0, smoothstep(0.0, 0.25, v));
                aux = v;
            }
            float cov = clamp(0.5 - sdE, 0.0, 1.0);
            float f0, rough;
            vec2 bp;
            vec3 alb = paint(q, s, seed, baseC, wear, f0, rough, bp);
            alb = mix(alb, vec3(0.2, 0.18, 0.15), 0.45 * W * dust);
            vec2 bs = vert ? vec2(sgx * bp.y, bp.x) : bp;
            vec3 F0 = vec3(f0);
            vec3 nb = normalize(n0);
            if (topF > 0.0) {
                float pn = fbmL(s * vec2(1.5, 5.0) + 30.0, 4);
                float seam = smoothstep(0.03, 0.0, abs(fract(s.x * 1.3) - 0.5) - 0.44);
                vec3 cu = mix(vec3(0.06, 0.09, 0.075), vec3(0.2, 0.3, 0.25), pn) * (1.0 - 0.35 * seam);
                cu = mix(vec3(0.09, 0.05, 0.03) * (0.6 + 0.6 * pn), cu, 0.3 + 0.7 * W);
                cu *= 1.0 - 0.4 * smoothstep(0.55, 0.85, EwValueNoise2D(vec2(s.x * 6.0, aux * 2.0)));
                alb = mix(alb, cu, topF);
                F0 = mix(F0, vec3(0.04), topF);
                rough = mix(rough, 0.75, topF);
                bs *= 1.0 - topF;
            }
            vec3 n = normalize(nb + vec3(bs, 0.0) * max(nb.z, 0.3));
            if (ringM > 0.0) {
                float bn = EwValueNoise2D(vec2(aux * 9.0, ry) + sgx * 3.0);
                vec3 brass = vec3(0.42, 0.29, 0.1) * (0.6 + 0.5 * bn);
                float pat = W * smoothstep(0.45, 0.7, EwValueNoise2D(vec2(aux * 5.0, ry * 0.5) + 9.0));
                alb = mix(alb, mix(brass * 0.15, vec3(0.1, 0.2, 0.16), pat), ringM);
                F0 = mix(F0, mix(brass, vec3(0.03), pat), ringM);
                rough = mix(rough, mix(0.35, 0.8, pat), ringM);
                n = normalize(mix(n, normalize(nb + vec3(0.0, clamp(ry, -1.0, 1.0) * 0.8, 0.0)), ringM));
            }
            acc = over(vec4(shadeM(alb, n, ao, shd, F0, rough) * cov, cov), acc);
        }
    }
    // gaku plaque
    {
        vec2 gc = vec2(0.0, 0.5 * (yS0 + yN + tN));
        float gh = 0.5 * (yS0 - yN - tN) / H - 0.12;
        vec2 gq = (p - gc) / H;
        float db = EwSdRoundedBox2D(gq, vec2(0.52, gh), 0.06);
        float cov = smoothstep(pxH, -pxH, db);
        if (cov > 0.0) {
            float rimM = smoothstep(-0.1 - pxH, -0.1 + pxH, db);
            vec3 alb = vec3(0.03, 0.025, 0.02) * (0.7 + 0.6 * EwValueNoise2D(vec2(gq.x * 3.0, gq.y * 30.0)));
            float sp = 0.15;
            vec2 ga = vec2(gq.x / 0.3, gq.y / max(gh - 0.2, 0.1));
            float ci = step(ga.y, 0.0);
            vec2 cu = vec2(ga.x, ga.y * 2.0 + (ci * 2.0 - 1.0)) * 0.5 + 0.5;
            if (abs(ga.x) < 1.0 && abs(ga.y) < 1.0) {
                vec2 fg = floor(cu * 4.0);
                vec2 fr = fract(cu * 4.0);
                float h = EwHash2To1(fg + ci * 17.0 + 3.0);
                float stroke = (h > 0.5) ? step(abs(fr.y - 0.5), 0.16) : ((h < 0.28) ? step(abs(fr.x - 0.5), 0.16) : 0.0);
                rimM = max(rimM, stroke * 0.9);
            }
            alb = mix(alb, vec3(0.55, 0.38, 0.1) * (0.6 + 0.5 * EwValueNoise2D(gq * 12.0)), rimM);
            sp = mix(sp, 0.7 * (1.0 - 0.5 * Weathering), rimM);
            vec3 n = normalize(vec3(-0.3 * smoothstep(-0.05, 0.0, db) * normalize(gq + 1e-4), 1.0));
            acc = over(vec4(shade(alb, n, 0.85, sp, 30.0) * cov, cov), acc);
        }
    }

    // kamebara: rounded stone foot of each pillar
    {
        vec2 e = vec2(ax - xP, p.y - yT) / vec2(0.95 * H, 0.6 * H);
        float dK = max((length(e) - 1.0) * 0.6 * H, yT - 0.15 * H - p.y);
        if (dK < 1.0) {
            float cov = clamp(0.5 - dK, 0.0, 1.0);
            float r2 = dot(e, e);
            vec2 kq = p / H;
            float n1, mvA, mvB;
            vec2 gK, mgA, mgB;
            microGrad3(kq, vec3(3.0, 14.0, 48.0), vec3(7.0, 3.0, 9.0), gK, n1, mgA, mvA, mgB, mvB);
            gK *= 3.0;
            float n2 = EwValueNoise2D(kq * 16.0);
            vec3 n = normalize(vec3(sgx * e.x, max(e.y, 0.0), sqrt(max(1.0 - r2, 0.0)) + 0.08));
            n = normalize(n + vec3(gK * 0.12 + mgA * 0.3 + mgB * 0.2, 0.0));
            vec3 alb = vec3(0.23, 0.22, 0.2) * (0.55 + 0.7 * n1) * (0.8 + 0.3 * n2) * (0.8 + 0.4 * mvB);
            alb *= 1.0 - 0.45 * smoothstep(0.62, 0.72, mvA);
            float streak = EwValueNoise2D(vec2(kq.x * 9.0, kq.y * 0.8));
            alb *= 1.0 - 0.5 * W * smoothstep(0.55, 0.85, streak) * smoothstep(0.9, 0.2, e.y);
            float lich = smoothstep(0.72, 0.77, EwValueNoise2D(kq * 5.0 + 20.0)) * W;
            alb = mix(alb, vec3(0.34, 0.33, 0.24), lich * 0.5);
            float mv = n1 + 0.6 * MossAmount - 0.25 - 0.45 * e.y;
            float moss = smoothstep(0.5, 0.68, mv + 0.15 * (n2 - 0.5)) * step(0.001, MossAmount);
            alb = mix(alb, mix(vec3(0.02, 0.045, 0.01), vec3(0.08, 0.12, 0.03), n2), moss);
            float ao = mix(0.45, 1.0, smoothstep(0.0, 0.35, e.y));
            ao *= mix(1.0, mix(0.5, 1.0, smoothstep(0.0, 0.3, abs(ax - xP) / H - 0.78)), smoothstep(0.4, 0.8, e.y));
            float rel = (p.x - sgx * xP) / H;
            float shd = 1.0 - 0.55 * smoothstep(0.78, 0.92, rel) * smoothstep(0.2, 0.6, e.y);
            acc = over(vec4(shadeM(alb, n, ao, shd, vec3(0.035), 0.85) * cov, cov), acc);
        }
    }


    // lanterns
    for (int i = ZERO; i < 2; i++) {
        float fi = float(i);
        vec2 piv = lanPiv[i];
        // soft shadow of the lantern on the pillar, then the iron bracket it hangs from
        {
            vec2 sq2 = (p - gLan[i] - vec2(0.3, -0.26) * H) / (vec2(0.62, 0.92) * H);
            acc.rgb *= 1.0 - 0.5 * smoothstep(1.3, 0.55, length(sq2));
            vec2 bq = (p - piv) / H;
            float dPl = EwSdRoundedBox2D(bq - vec2(0.0, 0.3), vec2(0.13, 0.18), 0.04);
            float dHook = abs(length(bq - vec2(0.0, 0.07)) - 0.07) - 0.024;
            float dArm = EwSdSegment2D(bq, vec2(0.0, 0.14), vec2(0.0, 0.2)) - 0.035;
            float dBr = min(dPl, min(dHook, dArm));
            float cbr = smoothstep(pxH, -pxH, dBr);
            if (cbr > 0.0) {
                vec2 pq = bq - vec2(0.0, 0.3);
                vec3 n = (dPl < min(dHook, dArm)) ? normalize(vec3(smoothstep(-0.04, 0.0, dPl) * normalize(pq + 1e-4) * 0.9, 1.0)) : normalize(vec3(bq.x * 8.0, 0.3, 1.0));
                float rust = W * smoothstep(0.45, 0.75, EwValueNoise2D(bq * 30.0 + float(i) * 7.0));
                float rivet = smoothstep(0.03, 0.015, length(vec2(pq.x, abs(pq.y) - 0.1)));
                vec3 alb = mix(vec3(0.025, 0.024, 0.022), vec3(0.12, 0.05, 0.02), rust) * (1.0 + 2.0 * rivet);
                acc = over(vec4(shadeM(alb, n, 0.85, 1.0, mix(vec3(0.15, 0.14, 0.13), vec3(0.03), rust), mix(0.45, 0.85, rust)) * cbr, cbr), acc);
            }
        }
        vec2 q = EwRotate2D(-ang[i]) * (p - piv) / H;
        if (abs(q.x) > 0.8 || q.y > 0.2 || q.y < -3.0) continue;
        float dCord = EwSdSegment2D(q, vec2(0.0, 0.05), vec2(0.0, -0.45)) - 0.025;
        float dCap = min(EwSdRoundedBox2D(q - vec2(0.0, -0.54), vec2(0.32, 0.09), 0.03),
                         EwSdRoundedBox2D(q - vec2(0.0, -2.42), vec2(0.3, 0.08), 0.03));
        float tsw = 0.06 * sin(t * 1.7 + fi);
        float dTas = EwSdSegment2D(q, vec2(0.0, -2.5), vec2(tsw, -2.9)) - mix(0.03, 0.08, clamp((-2.5 - q.y) / 0.4, 0.0, 1.0));
        vec2 e = (q - vec2(0.0, -1.48)) / vec2(0.6, 0.88);
        float rr = length(e);
        float dBody = (rr - 1.0) * 0.6;
        // body
        float cb = smoothstep(pxH, -pxH, dBody);
        if (cb > 0.0) {
            float ex = clamp(e.x, -1.0, 1.0);
            vec3 n = vec3(ex, 0.25 * e.y, sqrt(max(1.0 - ex * ex, 0.0)) + 0.1);
            n = normalize(n);
            float rib = smoothstep(0.12, 0.0, abs(fract(e.y * 6.0) - 0.5)) * 0.8;
            float dG = min(min(EwSdSegment2D(e, vec2(-0.48, 0.28), vec2(0.48, 0.28)), EwSdSegment2D(e, vec2(0.0, 0.62), vec2(0.0, 0.25))),
                           min(EwSdSegment2D(e, vec2(0.0, 0.26), vec2(-0.45, -0.52)), EwSdSegment2D(e, vec2(0.0, 0.26), vec2(0.42, -0.5)))) - 0.065;
            float glyph = smoothstep(0.02, -0.02, dG);
            float stain = fbmL(e * 3.0 + fi * 5.0, 4);
            vec3 alb = vec3(0.6, 0.07, 0.03) * (0.7 + 0.5 * stain) * (1.0 - 0.6 * rib);
            alb = mix(alb, vec3(0.015), glyph);
            vec3 lc = shade(alb, n, 1.0, 0.1, 20.0);
            vec3 em = LanternColor * vec3(1.0, 0.5, 0.35) * LanternGlow * flick[i] * (0.12 + 0.6 * (1.0 - rr * rr)) * (1.0 - 0.7 * rib) * (1.0 - 0.95 * glyph) * (0.8 + 0.3 * stain);
            acc = over(vec4((lc + em) * cb, cb), acc);
        }
        float cc = smoothstep(pxH, -pxH, min(dCap, dCord));
        if (cc > 0.0) {
            vec3 n = normalize(vec3(0.0, 0.3 * sign(q.y + 1.48), 1.0));
            vec3 alb = (dCord < dCap) ? vec3(0.08, 0.06, 0.04) : vec3(0.02);
            acc = over(vec4(shade(alb, n, 0.9, 0.3, 40.0) * cc, cc), acc);
        }
        float ct = smoothstep(pxH, -pxH, dTas);
        if (ct > 0.0) {
            vec3 alb = vec3(0.4, 0.05, 0.03) * (0.6 + 0.5 * EwValueNoise2D(vec2(q.x * 60.0, 0.0)));
            acc = over(vec4(shade(alb, vec3(0.0, 0.0, 1.0), 0.8, 0.0, 8.0) * ct, ct), acc);
        }
    }

    if (ShowCreatures) {
        // crows perched on the crossbeam
        for (int i = ZERO; i < 2; i++) {
            float fi = float(i);
            float dir = (i == 0) ? 1.0 : -1.0;
            vec2 feet = vec2(((i == 0) ? -0.22 : 0.3) * hs.x, yN + tN);
            vec2 q = (p - feet) / H;
            q.x *= dir;
            if (abs(q.x) < 1.2 && q.y > -0.1 && q.y < 1.3) {
                vec4 c = crow(q, fi, t, pxH);
                acc = over(c, acc);
            }
        }
        // spiders on threads
        for (int i = ZERO; i < 3; i++) {
            float fi = float(i);
            // threads hang just inside the pillars, so the middle stays free
            float sgs = (i == 1) ? 1.0 : -1.0;
            float offS = (i == 0) ? 0.35 : ((i == 1) ? 0.45 : 0.9);
            vec2 at = vec2(sgs * (hs.x - 1.68 * H - offS * H), yN - tN);
            float maxL = min(4.5 * H, max(0.0, (yN - tN) - (yT + 1.5 * H)));
            float L = min(0.4 * H, maxL) + maxL * smoothstep(0.15, 0.85, 0.5 + 0.5 * sin(t * 0.25 + fi * 2.1));
            vec2 sp = at + vec2(0.08 * H * sin(t * 0.7 + fi * 3.0), -L);
            float dTh = EwSdSegment2D(p, at, sp);
            float th = smoothstep(0.9, 0.0, dTh) * (0.18 + 0.4 * SunStrength * smoothstep(0.6, 1.0, sin(p.y / H * 2.0 + t * 1.5 + fi)));
            acc = over(vec4(vec3(0.8) * th, th * 0.7), acc);
            vec2 q = EwRotate2D(0.15 * sin(t * 0.9 + fi)) * (p - sp) / H;
            if (abs(q.x) < 0.6 && q.y < 0.15 && q.y > -0.95) acc = over(spider(q, t, fi, pxH), acc);
        }
    }

    // lantern glow (additive halo)
    for (int i = ZERO; i < 2; i++) {
        float dist = length(p - gLan[i]) / H;
        float G = LanternGlow * flick[i] * HaloStrength * exp(-dist / HaloSize) * smoothstep(3.0, 2.0, dist);
        acc.rgb += LanternColor * G;
        acc.a += G * 0.4 * (1.0 - acc.a);
    }

        // tone map, dither
        float edge = min(min(fragCoord.x, res.x - fragCoord.x), min(fragCoord.y, res.y - fragCoord.y));
        a = clamp(acc.a, 0.0, 1.0) * smoothstep(0.5, 10.0, edge);
        vec3 cl = EwLinearToSrgb(EwTonemapAces(acc.rgb / max(acc.a, 1e-4) * Exposure));
        float dz = EwDither(fragCoord, iTime, 2.0) * step(0.002, a);
        col = clamp(cl * a + dz, 0.0, 1.0);
        a = clamp(a + dz, 0.0, 1.0);
        col = min(col, vec3(a));
        a *= 1.0 - Additive;
        if (a >= 1.0) {
            fragColor = vec4(col, 1.0);
            return;
        }
    }

    // soft cast shadow of the gate (and swinging lanterns onto the image)
    float inner = smoothstep(0.5, -0.5, d + 0.3 * H);   // reaches under the pillars, beams and threshold
    float shc = 0.0;
    if (inner > 0.0 && !(d < -2.6 * H && p.y < yN - tN - 0.9 * H)) {
        float gs = gateSD(p - vec2(0.3, -0.42) * H, hs, H, yN, tN, yT, xe);
        shc = smoothstep(0.45 * H, -0.25 * H, gs);
        if (SwHaveImage) {
            for (int i = ZERO; i < 2; i++) {
                vec2 lsq = (p - gLan[i] - vec2(0.3, -0.42) * H) / (vec2(0.62, 0.92) * H);
                shc = max(shc, smoothstep(1.3, 0.55, length(lsq)) * 0.8);
            }
        }
    }

    // background, inner fill / forest / image
    vec4 base = over(vec4(0.0, 0.0, 0.0, GateShadow * shc * inner), bg);
    if (InnerOpacity > 0.0 && inner > 0.0) {
        vec3 ic = vec3(0.0);
        if (!SwHaveImage || SwTransparentImage) {
            ic = InnerColor;
            if (ForestOpacity > 0.0) {
                vec3 fc = forest(p * invFh, t, invFh, vScPX0, vScFg0, vScPF1);
                fc = EwLinearToSrgb(EwTonemapAces(fc * Exposure));
                ic = mix(ic, fc, ForestOpacity);
            }
            ic *= 1.0 - GateShadow * shc;
        }
        if (SwHaveImage) {
            vec2 imgUV = p / (2.0 * openH) + 0.5;
            vec4 tex = texture(iChannel0, imgUV);
            vec3 imgLin = EwSrgbToLinear(tex.rgb);
            float shLin = pow(max(1.0 - GateShadow * shc, 0.0), 1.8);
            vec3 imgAmb = vec3(0.24, 0.26, 0.30);
            vec3 imgSun = vec3(0.76, 0.74, 0.70) * SunStrength;
            vec3 imgLight = imgAmb * mix(0.2, 1.0, shLin) + imgSun * shLin;
            for (int i = ZERO; i < 2; i++) {
                vec2 dl = (gLan[i] - p) / H;
                float r2 = dot(dl, dl);
                float att = LanternGlow * flick[i] / (1.0 + r2 * 0.14);
                float ndl = 1.6 * inversesqrt(r2 + 2.56);
                imgLight += LanternColor * att * ndl * 0.5;
            }
            vec3 imgLit = imgLin * imgLight * Exposure;
            vec3 overHi = max(imgLit - 0.85, 0.0);
            imgLit = min(imgLit, 0.85) + 0.15 * tanh(min(overHi / 0.15, vec3(10.0)));
            vec3 imgCol = EwLinearToSrgb(imgLit);
            ic = SwTransparentImage ? mix(ic, imgCol, tex.a) : imgCol;
        }
        float ia = InnerOpacity * inner;
        base = over(vec4(ic * ia, ia), base);
    }

    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
