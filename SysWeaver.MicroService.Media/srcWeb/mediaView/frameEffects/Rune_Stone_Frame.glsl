//  media: image none
// =============================================================================
//  Rune Stone Frame
//  A carved granite frame around the screen: an Elder Futhark inscription runs
//  around the band, sun wheels sit in the corners, and tufts of grass on top
//  of the frame and on the inner bottom ledge bend in gusts of wind.
//  The inside (and outside) is transparent: use it as an overlay / border.
// =============================================================================
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Wind | Strength of the wind in the grass and the blown seeds | 0.4 | 0 .. 2 | {0}x
//  iMouse.w = Grass | Coverage of the grass patches on top and on the bottom ledge | 0.6 | 0 .. 1

// --- Layout & compositing ---
const float TopSpace = 110.0; // var: { "min": 0.0, "max": 300.0, "step": 1.0, "name": "Top Inset", "desc": "Extra space above the frame for the grass on top", "unit": "px" }
const float BandWidth = 0.8; // var: { "min": 0.4, "max": 0.95, "step": 0.01, "name": "Band Width", "desc": "Width of the stone band relative to the inset", "scale": 100, "unit": "%" }
const float Exposure = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.0443, 0.0462, 0.05); // var: { "type": "col", "name": "Cliff Color", "desc": "Base color of the rugged cliff stone inside" }
const vec3 CrackColor = vec3(0.15, 0.1442, 0.1384); // var: { "type": "col", "name": "Crevice Color", "desc": "Color inside the cracks and deep fissures" }
const float CliffContrast = 0.65; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Cliff Contrast", "desc": "Contrast and darkness of crevices and shadows", "unit": "x" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the cliff inside (0 = transparent overlay)", "scale": 100, "unit": "%" }
const float CliffRuggedness = 0.55; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Cliff Ruggedness", "desc": "Cragginess and 3D relief of the cliff rock face", "unit": "x" }
const float CliffScale = 0.7; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Cliff Scale", "desc": "Size of the rock slabs, crags and crevices", "unit": "x" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

// --- Stone ---
const vec3 StoneColor = vec3(0.26, 0.265, 0.275); // var: { "type": "col", "name": "Stone Color", "desc": "Base color of the granite" }
const float Decay = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Decay", "desc": "How chipped, cracked and eroded the old stone is", "scale": 100, "unit": "%" }
const float Relief = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Relief", "desc": "Strength of the 3D shading of the carving", "unit": "x" }
const float FeatureSize = 1.6; // var: { "min": 0.5, "max": 3.0, "step": 0.05, "name": "Feature Size", "desc": "Scale of the details: cracks, chips, lichen, grass blades", "unit": "x" }
const float MicaAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Dark Grains", "desc": "Darkness of the black mica grains in the granite", "scale": 100, "unit": "%" }
const float QuartzAmount = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Light Grains", "desc": "Strength of the pale quartz grains in the granite", "scale": 100, "unit": "%" }
const vec3 QuartzColor = vec3(0.58, 0.58, 0.57); // var: { "type": "col", "name": "Light Grain Color", "desc": "Color of the quartz grains" }
const float Lichen = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Lichen", "desc": "Dry lichen spots on the stone", "scale": 100, "unit": "%" }
const vec3 LichenColor = vec3(0.62, 0.62, 0.56); // var: { "type": "col", "name": "Lichen Color", "desc": "Color of the common pale lichen" }
const vec3 RustLichenColor = vec3(0.55, 0.36, 0.14); // var: { "type": "col", "name": "Rust Lichen", "desc": "Color of the rarer rusty lichen" }

// --- Runes ---
const vec3 RunePaint = vec3(0.45, 0.07, 0.04); // var: { "type": "col", "name": "Rune Paint", "desc": "Color of the old paint in the carved runes" }
const float RunePaintAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Paint Amount", "desc": "How much of the rune paint is left", "scale": 100, "unit": "%" }
const float RuneSize = 0.85; // var: { "min": 0.5, "max": 1.1, "step": 0.01, "name": "Rune Size", "desc": "Height of the runes relative to the band", "scale": 100, "unit": "%" }
const float RuneStroke = 0.075; // var: { "min": 0.03, "max": 0.14, "step": 0.005, "name": "Rune Stroke", "desc": "Width of the carved rune strokes" }
const float RuneDepth = 0.17; // var: { "min": 0.0, "max": 0.35, "step": 0.01, "name": "Rune Depth", "desc": "How deep the runes and sun wheels are cut" }
const float SeparatorChance = 0.16; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Word Breaks", "desc": "Share of word separators (:) between the runes", "scale": 100, "unit": "%" }

// --- Grass & wind ---
const vec3 GrassColor = vec3(0.085, 0.15, 0.03); // var: { "type": "col", "name": "Grass Color", "desc": "Color of the fresh grass" }
const vec3 DryGrassColor = vec3(0.33, 0.26, 0.11); // var: { "type": "col", "name": "Dry Grass", "desc": "Color of the dry blades, tips and seeds" }
const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Wind animation speed", "unit": "x" }
const float GrassHeight = 1.0; // var: { "min": 0.3, "max": 2.5, "step": 0.05, "name": "Grass Height", "desc": "Length of the grass blades", "unit": "x" }
const float GrassDensity = 1.0; // var: { "min": 0.4, "max": 2.0, "step": 0.05, "name": "Grass Density", "desc": "How close together the blades grow", "unit": "x" }
const float DryAmount = 1.0; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Dry Blades", "desc": "How many blades are dry and yellowed", "unit": "x" }
const float BladeWidth = 0.4; // var: { "min": 0.2, "max": 0.8, "step": 0.01, "name": "Blade Width", "desc": "Width of a blade at its base" }
const float TuftSpread = 0.28; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Tuft Spread", "desc": "How far the blades of a tuft fan out" }
const float Gustiness = 0.2; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Gusts", "desc": "Strength of the gusts rolling through the grass" }
const float Flutter = 0.05; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Flutter", "desc": "Fast trembling of the single blades" }
const float SeedAmount = 1.0; // var: { "min": 0.0, "max": 2.5, "step": 0.05, "name": "Seeds", "desc": "Amount of seeds and chaff blown by the wind", "unit": "x" }
const float SeedSpeed = 1.8; // var: { "min": 0.5, "max": 4.0, "step": 0.05, "name": "Seed Speed", "desc": "How fast the seeds fly past", "unit": "x" }
const vec3 SoilColor = vec3(0.075, 0.055, 0.035); // var: { "type": "col", "name": "Soil Color", "desc": "Color of the soil mounds under the grass" }

// --- Lighting ---
const vec3 LightColor = vec3(1.0, 0.97, 0.92); // var: { "type": "col", "name": "Light Color", "desc": "Color of the sun light from the top left" }
const vec3 SkyColor = vec3(0.14, 0.15, 0.17); // var: { "type": "col", "name": "Sky Color", "desc": "Ambient light on the upward facing stone" }
const float ShadowStrength = 0.65; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Drop Shadow", "desc": "Darkness of the shadow the frame casts", "scale": 100, "unit": "%" }
const float Sheen = 0.15; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Sheen", "desc": "Polished highlights on the granite" }
const float RimLight = 0.2; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Rim Light", "desc": "Brightening of the edges facing the light" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define Wind mix(0.0, 2.0, iMouse.z)
#define GrassAmount mix(0.0, 1.0, iMouse.w)

const vec3 LDIR = vec3(-0.4706, 0.6471, 0.5999); // normalized key light (top left)
const vec3 HDIR = vec3(-0.263078, 0.361747, 0.894388); // normalize(LDIR + vec3(0,0,1))
#define ZERO min(iFrame, 0)

// per pixel frame geometry, set once in mainImage
vec2 g_hs; vec2 g_cc;
float g_rad, g_hw, g_fs, g_rp, g_L, g_N, g_R;
float g_mask = 1.0, g_chip = 0.0, g_rune = 0.0;

// premultiplied "over" compositing
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
// same sum as EwFbm2D, loop kept rolled (ZERO bound) so each call compiles to one noise body
float fbm(vec2 p, int octaves) {
    float sum = 0.0, amp = 0.5;
    for (int i = ZERO; i < 8; i++) {
        if (i >= octaves) break;
        sum += amp * EwValueNoise2D(p);
        p = mat2(1.6, 1.2, -1.2, 1.6) * p;
        amp *= 0.5;
    }
    return sum;
}
// same result as EwVoronoi2D (F1, F2, cell hash), 3x3 search kept as one rolled loop
vec3 voronoi(vec2 p) {
    vec2 cell = floor(p), fr = fract(p);
    float f1 = 8.0, f2 = 8.0;
    vec2 nc = cell;
    for (int y = -1 + ZERO; y <= 1; y++)
    for (int x = -1; x <= 1; x++) {
        vec2 nb = vec2(float(x), float(y));
        float dd = length(nb + EwHash2To2(cell + nb) - fr);
        if (dd < f1) { f2 = f1; f1 = dd; nc = cell + nb; }
        else if (dd < f2) f2 = dd;
    }
    return vec3(f1, f2, EwHash2To1(nc));
}
// 3-octave value-noise fbm with exact analytic gradient (same sum as fbm(p, 3))
vec3 fbm3d(vec2 p) {
    const mat2 M = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0, amp = 0.5;
    vec2 grad = vec2(0.0);
    mat2 J = mat2(1.0);
    for (int i = ZERO; i < 3; i++) {
        vec3 n = EwValueNoise2DDerivatives(p);
        sum += amp * n.x;
        grad += amp * (n.yz * J);
        p = M * p;
        J = M * J;
        amp *= 0.5;
    }
    return vec3(sum, grad);
}
// 2D simplex noise with exact analytic gradient (identical value to EwSimplexNoise2D)
vec3 simplex2dDeriv(vec2 p) {
    const float K1 = 0.366025404;
    const float K2 = 0.211324865;
    vec2 cell = floor(p + (p.x + p.y) * K1);
    vec2 o0 = p - cell + (cell.x + cell.y) * K2;
    float lt = step(o0.y, o0.x);
    vec2 cs = vec2(lt, 1.0 - lt);
    vec2 o1 = o0 - cs + K2;
    vec2 o2 = o0 - 1.0 + 2.0 * K2;
    vec3 k = max(0.5 - vec3(dot(o0, o0), dot(o1, o1), dot(o2, o2)), 0.0);
    vec3 k2 = k * k;
    vec3 k3 = k2 * k;
    vec3 k4 = k2 * k2;
    vec2 g0 = EwHash2To2(cell) * 2.0 - 1.0;
    vec2 g1 = EwHash2To2(cell + cs) * 2.0 - 1.0;
    vec2 g2 = EwHash2To2(cell + 1.0) * 2.0 - 1.0;
    vec3 d = vec3(dot(o0, g0), dot(o1, g1), dot(o2, g2));
    vec3 t = -8.0 * k3 * d;
    vec2 grad = 70.0 * (k4.x * g0 + t.x * o0 + k4.y * g1 + t.y * o1 + k4.z * g2 + t.z * o2);
    return vec3(70.0 * dot(k4, d), grad);
}
// Voronoi returning vectors to the 2 closest feature points + their cell hashes
void voronoiTop2(vec2 p, out vec2 d1, out vec2 d2, out vec2 z12) {
    vec2 cell = floor(p), fr = fract(p);
    float f1 = 64.0, f2 = 64.0;
    vec2 v1 = vec2(8.0), v2 = vec2(8.0);
    vec2 c1 = cell, c2 = cell;
    for (int y = -1 + ZERO; y <= 1; y++)
    for (int x = -1; x <= 1; x++) {
        vec2 nb = vec2(float(x), float(y));
        vec2 c = cell + nb;
        vec2 diff = nb + EwHash2To2(c) - fr;
        float dd = dot(diff, diff);
        if (dd < f1) { f2 = f1; v2 = v1; c2 = c1; f1 = dd; v1 = diff; c1 = c; }
        else if (dd < f2) { f2 = dd; v2 = diff; c2 = c; }
    }
    d1 = v1; d2 = v2;
    z12 = vec2(EwHash2To1(c1), EwHash2To1(c2));
}

// single-pass rugged cliff evaluation: evaluates base height + derivatives & self-shadow offsets in one pass
vec3 cliffLinear(vec2 p) {
    float sc = max(g_hw * 3.2 * CliffScale, 24.0);
    float invSc = 1.0 / sc;
    float eps = max(g_hw * 0.035, 1.0);
    float de = eps * invSc;
    vec2 dlo = LDIR.xy * (4.0 * de);
    vec2 q = p * invSc;

    // 1. domain warping + Jacobian
    vec3 wx = fbm3d(q * 0.6);
    vec3 wy = fbm3d(q * 0.6 + 5.2);
    vec2 pw0 = q + (vec2(wx.x, wy.x) - 0.5) * 0.55;
    vec2 dpw_dx = vec2(1.0 + 0.33 * wx.y, 0.33 * wy.y);
    vec2 dpw_dy = vec2(0.33 * wx.z, 1.0 + 0.33 * wy.z);
    vec2 dpwX = dpw_dx * de;
    vec2 dpwY = dpw_dy * de;
    vec2 dpwL = dpw_dx * dlo.x + dpw_dy * dlo.y;

    // 2. massive rock slabs (Voronoi top-2 sites evaluated once, shifted for the 3 neighbor offsets)
    const vec2 sc1 = vec2(0.3, 0.65);
    const vec2 sc2 = vec2(0.8, 1.6);
    vec2 slabUV = pw0 * sc1;
    vec2 d1a, d2a, z12a;
    voronoiTop2(slabUV, d1a, d2a, z12a);
    vec2 d1b, d2b, z12b;
    voronoiTop2(pw0 * sc2 + 7.4, d1b, d2b, z12b);
    float slab0 = z12a.x;
    vec2 tilt1 = normalize(vec2(fract(z12a.x * 13.7) - 0.5, fract(z12a.x * 29.1) - 0.5) + 1e-3);
    vec2 tilt2 = normalize(vec2(fract(z12a.y * 13.7) - 0.5, fract(z12a.y * 29.1) - 0.5) + 1e-3);

    // 3. stepped rock strata
    vec3 stN = fbm3d(pw0 * 1.2);
    vec2 stGrad = vec2(0.26, 0.965) * 3.2 + 0.42 * stN.yz;
    float st0 = (pw0.y * 0.965 + pw0.x * 0.26) * 3.2 + 0.35 * stN.x;
    vec4 stCoord = vec4(st0, st0 + dot(stGrad, dpwX), st0 + dot(stGrad, dpwY), st0 + dot(stGrad, dpwL));
    vec4 stFrac = fract(stCoord);
    vec4 strata = smoothstep(0.0, 0.15, stFrac) * (1.0 - 0.5 * stFrac) * 0.28;

    // 4. sharp craggy ridges (4 octaves in one rolled loop -> 1 simplex copy)
    vec4 crags = vec4(0.0);
    for (int i = ZERO; i < 4; i++) {
        float freq = (i == 0) ? 1.1 : (i == 1) ? 2.4 : (i == 2) ? 5.2 : 11.0;
        vec2 off = (i == 0) ? vec2(0.0) : (i == 1) ? vec2(2.1, 4.3) : (i == 2) ? vec2(7.3, 1.9) : vec2(3.5, 8.2);
        vec3 sn = simplex2dDeriv(pw0 * freq + off);
        vec2 g = sn.yz * freq;
        vec4 v = 1.0 - abs(vec4(sn.x, sn.x + dot(g, dpwX), sn.x + dot(g, dpwY), sn.x + dot(g, dpwL)));
        if (i < 2) v *= v;
        float w = (i == 0) ? 0.42 : (i == 1) ? 0.25 : (i == 2) ? 0.14 : 0.06;
        crags += v * w;
    }

    // 5. fissure masks + fine grit
    vec3 fm1 = EwValueNoise2DDerivatives(pw0 * 0.9 + 3.1);
    vec2 fm1g = fm1.yz * 0.9;
    vec4 faultMask1 = smoothstep(0.38, 0.72, vec4(fm1.x, fm1.x + dot(fm1g, dpwX), fm1.x + dot(fm1g, dpwY), fm1.x + dot(fm1g, dpwL)));
    vec3 fm2 = EwValueNoise2DDerivatives(pw0 * 1.4 + 11.0);
    vec2 fm2g = fm2.yz * 1.4;
    vec4 faultMask2 = smoothstep(0.45, 0.75, vec4(fm2.x, fm2.x + dot(fm2g, dpwX), fm2.x + dot(fm2g, dpwY), fm2.x + dot(fm2g, dpwL)));
    vec3 grN = EwValueNoise2DDerivatives(pw0 * 22.0);
    vec2 grg = grN.yz * 22.0;
    vec4 grit = (vec4(grN.x, grN.x + dot(grg, dpwX), grN.x + dot(grg, dpwY), grN.x + dot(grg, dpwL)) - 0.5) * 0.035;

    // assemble slab + facet + cracks for the 4 offsets (0, X, Y, L)
    vec4 slabFacet = vec4(0.0);
    vec4 crack4 = vec4(0.0);
    for (int i = 0; i < 4; i++) {
        vec2 dpw = (i == 0) ? vec2(0.0) : (i == 1) ? dpwX : (i == 2) ? dpwY : dpwL;
        vec2 du1 = dpw * sc1;
        float l1a = length(d1a - du1), l2a = length(d2a - du1);
        float z = (l1a < l2a) ? z12a.x : z12a.y;
        vec2 tilt = (l1a < l2a) ? tilt1 : tilt2;
        float sf = (z - 0.5) * 0.35 + dot(fract(slabUV + du1) - 0.5, tilt) * 0.4;
        float fm1v = (i == 0) ? faultMask1.x : (i == 1) ? faultMask1.y : (i == 2) ? faultMask1.z : faultMask1.w;
        float c1 = smoothstep(0.05, 0.0, abs(l2a - l1a)) * fm1v;
        vec2 du2 = dpw * sc2;
        float l1b = length(d1b - du2), l2b = length(d2b - du2);
        float fm2v = (i == 0) ? faultMask2.x : (i == 1) ? faultMask2.y : (i == 2) ? faultMask2.z : faultMask2.w;
        float c2 = smoothstep(0.035, 0.0, abs(l2b - l1b)) * fm2v;
        float cr = max(c1, c2 * 0.8);
        if (i == 0) { slabFacet.x = sf; crack4.x = cr; }
        else if (i == 1) { slabFacet.y = sf; crack4.y = cr; }
        else if (i == 2) { slabFacet.z = sf; crack4.z = cr; }
        else { slabFacet.w = sf; crack4.w = cr; }
    }

    vec4 hs = slabFacet + strata + crags * 0.75 - crack4 * 0.45 + grit;
    float h0 = hs.x, hx = hs.y, hy = hs.z, hl = hs.w;
    float crack0 = crack4.x;

    float k = (g_hw * 0.45 / eps) * CliffRuggedness;
    vec3 n = normalize(vec3(-(hx - h0) * k, -(hy - h0) * k, 1.0));
    float selfSh = 1.0 - 0.7 * CliffContrast * smoothstep(0.015, 0.1, (hl - h0) * CliffRuggedness);
    // lighting: key light + sky ambient + deep crack ambient occlusion + specular facet glint
    float crackAmt = clamp(crack0 * CliffContrast, 0.0, 1.0);
    float diff = max(dot(n, LDIR), 0.0) * selfSh;
    vec3 amb = mix(vec3(0.02, 0.02, 0.025), SkyColor * 0.7, 0.5 + 0.5 * n.y);
    float ao = clamp(0.35 + 0.65 * (h0 + 0.2), 0.0, 1.0) * (1.0 - 0.5 * crackAmt);
    float nh = max(dot(n, HDIR), 0.0);
    float nh2 = nh * nh; float nh4 = nh2 * nh2; float nh8 = nh4 * nh4; float nh16 = nh8 * nh8;
    float spec = (nh16 * nh8 * nh4 * nh2) * 0.06 * (1.0 - crack0) * selfSh * CliffRuggedness;

    // rock albedo: customizable base color with geological variation across slabs
    float slabTint = fract(slab0 * 19.31) * 0.35 - 0.17;
    vec3 alb = InnerColor * (1.0 + slabTint);

    // fine grain grit and mineral noise
    float gr = EwValueNoise2D(p / (g_fs * 0.1));
    float gs = EwHash2To1(floor(p / 2.0));
    alb *= 0.88 + 0.24 * gr;
    alb *= 0.92 + 0.16 * gs;

    // tint and shade crevices with dedicated crack color and depth
    alb = mix(alb, CrackColor, crackAmt);

    // assemble linear color
    return alb * (amb + LightColor * 0.95 * diff) * ao + LightColor * spec;
}
// position along the frame (pixels), continuous around the whole perimeter
float perimPos(vec2 p) {
    vec2 b = g_hs - g_rp;
    vec2 q = abs(p);
    float Lq = b.x + b.y + EW_HALF_PI * g_rp;
    float s;
    if (q.x > b.x && q.y > b.y) s = b.x + g_rp * atan(q.x - b.x, q.y - b.y);
    else if (q.x - b.x < q.y - b.y) s = q.x;
    else s = b.x + EW_HALF_PI * g_rp + (b.y - q.y);
    if (p.x >= 0.0) s = (p.y >= 0.0) ? s : 2.0 * Lq - s;
    else s = (p.y < 0.0) ? 2.0 * Lq + s : 4.0 * Lq - s;
    return s;
}

// Elder Futhark glyphs: up to 5 strokes each (x -0.25..0.25, y -0.5..0.5), 24 = word separator
// packed: each stroke end coordinate as (c + 0.5) * 100 in 7 bits (x0, y0, x1, y1); -1 = no stroke
#define S(a, b, c, d) ((a) | ((b) << 7) | ((c) << 14) | ((d) << 21))
const int RN = -1;
const int RUNES[125] = int[125](
    S(35,0,35,100), S(35,65,70,95), S(35,35,70,65), RN, RN,                     // fehu
    S(35,0,35,100), S(35,100,70,65), S(70,65,70,0), RN, RN,                     // uruz
    S(35,0,35,100), S(35,78,65,50), S(65,50,35,22), RN, RN,                     // thurisaz
    S(35,0,35,100), S(35,100,70,70), S(35,65,70,35), RN, RN,                    // ansuz
    S(35,0,35,100), S(35,100,67,77), S(67,77,35,54), S(35,54,70,0), RN,         // raido
    S(70,95,35,50), S(35,50,70,5), RN, RN, RN,                                  // kaunan
    S(25,100,75,0), S(25,0,75,100), RN, RN, RN,                                 // gebo
    S(35,0,35,100), S(35,100,67,77), S(67,77,35,54), RN, RN,                    // wunjo
    S(30,0,30,100), S(70,0,70,100), S(30,70,70,30), RN, RN,                     // hagalaz
    S(50,0,50,100), S(30,75,70,45), RN, RN, RN,                                 // naudiz
    S(50,0,50,100), RN, RN, RN, RN,                                             // isa
    S(45,92,25,65), S(25,65,45,38), S(55,62,75,35), S(75,35,55,8), RN,          // jera
    S(50,0,50,100), S(50,100,70,80), S(50,0,30,20), RN, RN,                     // eihwaz
    S(35,0,35,100), S(35,100,55,75), S(55,75,72,95), S(35,0,55,25), S(55,25,72,5), // perthro
    S(50,0,50,100), S(50,55,25,95), S(50,55,75,95), RN, RN,                     // algiz
    S(65,100,30,67), S(30,67,70,35), S(70,35,35,0), RN, RN,                     // sowilo
    S(50,0,50,100), S(50,100,25,70), S(50,100,75,70), RN, RN,                   // tiwaz
    S(35,0,35,100), S(35,100,67,77), S(67,77,35,52), S(35,52,67,26), S(67,26,35,0), // berkanan
    S(30,0,30,100), S(70,0,70,100), S(30,100,50,75), S(50,75,70,100), RN,       // ehwaz
    S(30,0,30,100), S(70,0,70,100), S(30,100,70,60), S(70,100,30,60), RN,       // mannaz
    S(35,0,35,100), S(35,100,70,70), RN, RN, RN,                                // laguz
    S(50,80,70,50), S(70,50,50,20), S(50,20,30,50), S(30,50,50,80), RN,         // ingwaz
    S(25,0,25,100), S(75,0,75,100), S(25,100,75,0), S(25,0,75,100), RN,         // dagaz
    S(50,100,72,70), S(50,100,28,70), S(72,70,30,0), S(28,70,70,0), RN,         // othala
    S(47,70,53,70), S(47,30,53,30), RN, RN, RN                                  // separator (:)
);

// distance (glyph units) to the strokes of rune id
float runeDist(vec2 g, int id) {
    float dm = 9.0;
    for (int i = ZERO; i < 5; i++) {
        int c = RUNES[id * 5 + i];
        if (c < 0) break;
        vec4 s = vec4((ivec4(c, c >> 7, c >> 14, c >> 21) & 127)) * 0.01 - 0.5;
        dm = min(dm, EwSdSegment2D(g, s.xy, s.zw));
    }
    return dm;
}

// stone height field (0 = flat, ~0.75 = top of the carving); sets g_mask (silhouette) and g_chip
float stoneHeight(vec2 p) {
    float hw = g_hw;
    float fs = g_fs;
    vec2 w = vec2(EwValueNoise2D(p / fs * 0.6), EwValueNoise2D(p / fs * 0.6 + 5.2)) - 0.5
           + 0.4 * (vec2(EwValueNoise2D(p / fs * 1.7 + 2.1), EwValueNoise2D(p / fs * 1.7 + 8.4)) - 0.5);
    vec2 pw = p + w * hw * (0.1 + 0.35 * Decay);
    float d = EwSdRoundedBox2D(pw, g_hs, g_rad);
    float v = -d / hw;
    float av = abs(v);
    float en = fbm(p / fs * 1.6 + 4.0, 4);
    float ero = Decay * 0.5 * smoothstep(0.35, 0.75, en);
    float ae = av + ero;
    // rounded (pillow) cross section: flat top, quarter round shoulders
    float body = smoothstep(1.0, 0.94, ae);
    float shoulder = sqrt(max(0.0, 1.0 - EwSquare(max(ae - 0.4, 0.0) / 0.6)));
    float bead = sqrt(max(0.0, 1.0 - EwSquare((av - 0.8) / 0.07)));
    // runic inscription running clockwise around the band, tops pointing outwards
    g_rune = 0.0;
    if (av < 0.6) {
        float cx = perimPos(pw) * g_N / g_L;
        float ci = mod(floor(cx), g_N);
        vec2 g = vec2((fract(cx) - 0.5) * (g_L / g_N) / hw, -v) / RuneSize;
        g += (vec2(EwValueNoise2D(p / fs * 6.0), EwValueNoise2D(p / fs * 6.0 + 3.3)) - 0.5) * 0.05; // hand cut
        float rh = EwHash1To1(ci * 1.37 + 3.0);
        int id = rh < SeparatorChance ? 24 : int(min(floor(fract(rh * 7.13) * 24.0), 23.0));
        float rd = runeDist(g, id);
        g_rune = smoothstep(RuneStroke + 1.0 / (RuneSize * hw), 0.0, rd);
    }
    float h = body * (0.08 + 0.52 * shoulder + 0.1 * bead - RuneDepth * g_rune);
    float mask = clamp(0.5 - (ae - 0.97) * hw, 0.0, 1.0);
    // corner medallion with an eight armed carved sun wheel
    vec2 lq = abs(pw) - g_cc;
    float r = length(lq);
    if (r < g_R) {
        float rr = r / g_R;
        float re = rr + ero * 0.4;
        float mb = sqrt(max(0.0, 1.0 - EwSquare((rr - 0.86) / 0.07)));
        float dome = sqrt(max(0.0, 1.0 - EwSquare(max(rr - 0.3, 0.0) / 0.7)));
        float hm = smoothstep(1.0, 0.94, re) * (0.06 + 0.58 * dome + 0.1 * mb);
        // fold into one of 8 sectors: spoke, cross bar, hooked tip
        float a8 = floor(atan(lq.y, lq.x + 1e-6) / (EW_TAU / 8.0) + 0.5) * (EW_TAU / 8.0);
        vec2 sd = vec2(cos(a8), sin(a8));
        vec2 lr = vec2(dot(lq, sd), abs(dot(lq, vec2(-sd.y, sd.x)))) / g_R;
        float ds = EwSdSegment2D(lr, vec2(0.26, 0.0), vec2(0.68, 0.0));
        ds = min(ds, EwSdSegment2D(lr, vec2(0.5, 0.0), vec2(0.5, 0.09)));
        ds = min(ds, EwSdSegment2D(lr, vec2(0.68, 0.0), vec2(0.74, 0.07)));
        ds = min(ds, abs(rr - 0.2));
        float gm = smoothstep(0.035 + 1.0 / g_R, 0.0, ds) * smoothstep(0.8, 0.76, rr);
        hm -= RuneDepth * gm;
        float boss = sqrt(max(0.0, 1.0 - EwSquare(rr / 0.15)));
        hm = max(hm, (0.6 + 0.15 * boss) * step(0.0, 0.15 - rr));
        float mm = smoothstep(1.0, 0.9, re);
        h = mix(h, hm, mm);
        g_rune = mix(g_rune, gm, mm);
        mask = max(mask, clamp(0.5 - (re - 0.97) * g_R, 0.0, 1.0));
    }
    // weathering: worn strands, erosion, chipped chunks, cracks, pitting
    float wear = EwValueNoise2D(p / fs * 1.1 + 13.0);
    h = mix(h, 0.18 + 0.4 * (h - 0.18), Decay * 0.8 * smoothstep(0.35, 0.7, wear) * step(0.18, h));
    float n = fbm(p / fs * 2.2, 5);
    h += (n - 0.47) * (0.08 + 0.25 * Decay) * smoothstep(0.0, 0.1, h);
    vec3 vo = voronoi(p / fs * 1.8 + 1.7);
    float chipLvl = 0.04 + 0.25 * fract(vo.z * 17.3) + 0.5 * vo.x;
    float chipSel = step(vo.z, Decay * 0.5);
    float hc = min(h, chipLvl);
    g_chip = chipSel * smoothstep(0.0, 0.05, h - hc);
    h = mix(h, hc, chipSel);
    float crack = smoothstep(0.04 / FeatureSize, 0.0, vo.y - vo.x) * step(0.62, EwValueNoise2D(p / fs * 1.2 + 9.0));
    h -= Decay * 0.45 * crack;
    h -= (0.05 + 0.12 * Decay) * smoothstep(0.65, 0.95, EwValueNoise2D(p / fs * 7.0 + 3.1));
    g_mask = mask * smoothstep(-0.05, 0.0, h);
    return h;
}

// y of the top edge of the rounded box grown by off (or -1e5 if outside)
float bandEdgeY(float x, float off) {
    vec2 b = g_hs + off;
    float r = max(g_rad + off, 0.0);
    float bx = b.x - r;
    float ax = abs(x);
    if (ax > b.x) return -1e5;
    return ax < bx ? b.y : (b.y - r) + sqrt(max(r * r - EwSquare(ax - bx), 0.0));
}
// ground line of the grass: mode 0 = top of the frame, 1 = inner edge of the bottom band
float groundY(float x, int mode) {
    if (mode == 0) return bandEdgeY(x, 0.8 * g_hw);
    float e = bandEdgeY(x, -0.9 * g_hw);
    return e < -1e4 ? -1e5 : -e;
}

// wind bend of the grass at x: steady push plus gusts travelling to the right
float windBend(float x, float T) {
    return Wind * (0.25 + Gustiness * sin(T * 1.6 - x / (g_fs * 2.5)) + 0.5 * Gustiness * sin(T * 0.7 - x / (g_fs * 6.0) + 1.3));
}

// grass patch strength along a ground line (0 = bare stone)
float grassPatch(float x, int mode) {
    float sd = float(mode) * 17.0;
    float n = 0.7 * EwValueNoise2D(vec2(x / (g_fs * 1.5), 4.2 + sd)) + 0.3 * EwValueNoise2D(vec2(x / (g_fs * 0.45), 9.1 + sd));
    float thr = 1.0 - 0.9 * GrassAmount;
    float lim = smoothstep(g_cc.x - 0.5 * g_R, g_cc.x - 1.3 * g_R, abs(x));
    return smoothstep(thr - 0.08, thr + 0.12, n) * lim;
}

// soil mound + 3 layers of wind blown blades growing up from a ground line
vec4 grassBand(vec2 p, int mode, float T) {
    vec4 acc = vec4(0.0);
    float hw = g_hw, fs = g_fs;
    float Hn = hw * 1.6 * GrassHeight;
    float sd = float(mode) * 53.0;
    float yg = groundY(p.x, mode);
    if (yg < -1e4) return acc;
    // soil mound (behind the blades), small shadow below it
    float paP = grassPatch(p.x, mode);
    if (paP > 0.01) {
        float ln = fbm(vec2(p.x / (fs * 0.18), 3.7 + sd), 3);
        float yTopS = yg + hw * (0.08 + 0.3 * paP * (0.4 + ln));
        float yBotS = yg - hw * 0.14 * paP * (0.5 + EwValueNoise2D(vec2(p.x / (fs * 0.1), 8.3 + sd)));
        float sm = clamp(yTopS - p.y + 0.5, 0.0, 1.0) * clamp(p.y - yBotS + 0.5, 0.0, 1.0);
        float sh = smoothstep(yBotS - hw * 0.3, yBotS, p.y) * step(p.y, yBotS + 0.5) * paP;
        acc = over(vec4(0.0, 0.0, 0.0, 0.5 * sh * (1.0 - sm)), acc);
        if (sm > 0.0) {
            float fn = EwValueNoise2D(p / (fs * 0.035));
            float pb = EwValueNoise2D(p / (fs * 0.09) + 4.0);
            vec3 sc = SoilColor * (0.55 + 0.7 * fn);
            sc = mix(sc, vec3(0.2, 0.2, 0.2) * (0.6 + 0.6 * fn), smoothstep(0.72, 0.8, pb)); // pebbles
            float up = smoothstep(yBotS, yTopS, p.y);
            sc *= 0.5 + 0.7 * up;
            sc = mix(sc, DryGrassColor * (0.35 + 0.5 * fn), smoothstep(0.5, 1.0, up) * 0.6); // thatch
            acc = over(vec4(sc, 1.0) * sm, acc);
        }
    }
    // blades, searched around the wind sheared position
    float cwB = max(fs * 0.09 / GrassDensity, 3.0);
    float tuft = fs * 0.45;
    float dyg = max(p.y - yg - hw * 0.15, 0.0);
    float windP = windBend(p.x, T);
    float dyg2 = dyg * dyg / Hn;
    float baseWb = max(cwB * BladeWidth, 0.9);
    for (int l = ZERO; l < 3; l++) {
        float fl = float(l);
        float lean0 = (fl - 1.0) * 0.12;
        float xg = p.x - (windP + lean0) * dyg2;
        float j0 = floor(xg / cwB + fl * 0.33);
        for (int k = -5 + ZERO; k <= 5; k++) {
            float j = j0 + float(k);
            vec4 bh = EwHash1To4(j * 1.71 + fl * 37.0 + 5.0 + sd);
            float x0 = (j + 0.5 + (bh.x - 0.5) * 0.9 - fl * 0.33) * cwB;
            float pa = grassPatch(x0, mode);
            if (bh.y > pa * 1.3) continue;
            float yi = groundY(x0, mode);
            if (yi < -1e4) continue;
            // blades fan out of tufts: lean away from the tuft center, tallest in the middle
            float tf = fract(x0 / tuft + 0.37 * fl + sd * 0.1) * 2.0 - 1.0;
            float yr = yi + hw * (0.08 + 0.15 * bh.z);
            float dy = p.y - yr;
            float Hb = Hn * (0.25 + 0.75 * bh.w) * (0.55 + 0.45 * pa) * (1.0 - 0.45 * tf * tf);
            if (dy < 0.0 || dy > Hb) continue;
            float cb = windBend(x0, T) + lean0 + tf * TuftSpread + (bh.z - 0.5) * 0.12 + Wind * Flutter * sin(T * 7.0 + bh.x * 40.0);
            Hb /= 1.0 + 0.5 * cb * cb * Hb / Hn;   // bent blades get shorter (keeps their length)
            if (dy > Hb) continue;
            // parabolic blade: x offset grows with height squared
            float xc = x0 + cb * dy * dy / Hn;
            float slope = 2.0 * cb * dy / Hn;
            float sdx = (p.x - xc) * inversesqrt(1.0 + slope * slope);
            float t = dy / Hb;
            float om = sqrt(max(1.0 - t, 0.0));
            float wb = baseWb * om * sqrt(om);
            float m = clamp(wb - abs(sdx) + 0.5, 0.0, 1.0);
            if (m <= 0.0) continue;
            float u = clamp(sdx / max(wb, 0.5), -1.0, 1.0);
            vec3 gc = mix(GrassColor, DryGrassColor, clamp(bh.y * bh.y * 0.9 * DryAmount + t * t * 0.35, 0.0, 1.0));
            gc *= (0.6 + 0.2 * fl) * (0.8 + 0.4 * bh.z);
            gc *= (0.8 - 0.3 * u) * (0.3 + 0.7 * smoothstep(0.0, 0.35, t));
            float sp = max(0.0, 1.0 - abs(u + 0.3) * 2.5);
            sp *= sp; sp *= sp;
            gc += vec3(0.05, 0.06, 0.02) * sp * t;
            acc = over(vec4(gc, 1.0) * m, acc);
        }
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 ps = fragCoord - 0.5 * res;               // screen centered
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float top = clamp(TopSpace, 0.0, 0.3 * res.y);  // extra room above the frame for the grass
    float hw = max(inset * BandWidth, 3.0);
    vec2 maxHs = 0.5 * res - inset - vec2(0.0, 0.5 * top);

    vec2 hs = maxHs;
    vec2 innerHs = maxHs;
    vec2 p = ps + vec2(0.0, 0.5 * top);             // frame centered
    if (SwHaveImage) {
        vec2 pad = min(vec2(hw * mix(0.95, 0.50, Decay)), maxHs * 0.99);
        vec2 maxInner = maxHs - pad;
        vec2 imgRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = imgRes.x / imgRes.y;
        float maxAspect = maxInner.x / maxInner.y;
        innerHs = (imgAspect > maxAspect)
            ? vec2(maxInner.x, maxInner.x / imgAspect)
            : vec2(maxInner.y * imgAspect, maxInner.y);
        hs = innerHs + pad;
        // center vertically while still keeping inset + top clearance above the frame for grass
        float topShift = max(0.0, hs.y + inset + top - 0.5 * res.y);
        p = ps + vec2(0.0, topShift);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);

    float fs = hw * FeatureSize;
    g_hs = hs; g_rad = rad; g_hw = hw; g_fs = fs;
    g_rp = min(max(rad, hw * 1.05), min(hs.x, hs.y));
    g_L = 4.0 * ((hs.x - g_rp) + (hs.y - g_rp) + EW_HALF_PI * g_rp);
    g_N = max(4.0, floor(g_L / (0.62 * hw) + 0.5));
    g_R = min(1.15 * hw, 0.95 * inset);
    g_cc = (hs - rad) + rad * 0.70710678;

    vec4 bg = vec4(Background * Opacity, Opacity);
    // fill up to the band's center line: the stone always covers it there, even where the
    // inner edge is warped, eroded or chipped (no gap between fill and stone)
    float inner = smoothstep(0.5, -0.5, d);
    vec4 innerFill = vec4(0.0);
    if (inner > 0.001 && InnerOpacity > 0.001) {
        if (SwHaveImage) {
            vec2 imgUV = p / (2.0 * innerHs) + 0.5;
            vec2 imgRes = max(iChannelResolution[0].xy, vec2(1.0));
            float texelsPerPx = max(imgRes.x / (2.0 * innerHs.x), imgRes.y / (2.0 * innerHs.y));
            vec4 tex = textureLod(iChannel0, imgUV, max(log2(texelsPerPx), 0.0));
            vec3 imgAmb = mix(vec3(0.03, 0.03, 0.035), SkyColor, 0.5);
            vec3 imgLin = EwSrgbToLinear(tex.rgb) * (imgAmb + LightColor * (0.95 * LDIR.z));
            vec3 lin;
            float imgA;
            if (SwTransparentImage) {
                vec3 cLin = tex.a < 0.999 ? cliffLinear(p) : vec3(0.0);
                lin = mix(cLin, imgLin, tex.a);
                imgA = 1.0;
            } else {
                lin = imgLin;
                imgA = tex.a;
            }
            vec3 col = EwLinearToSrgbFast(EwTonemapAces(lin * Exposure * 1.3));
            col = clamp(col + EwDither(fragCoord, 0.0, 1.0), 0.0, 1.0);
            innerFill = vec4(col * imgA, imgA) * (InnerOpacity * inner);
        } else {
            vec3 col = cliffLinear(p);
            col = EwLinearToSrgbFast(EwTonemapAces(col * Exposure * 1.3));
            col = clamp(col + EwDither(fragCoord, 0.0, 1.0), 0.0, 1.0);
            innerFill = vec4(col * InnerOpacity, InnerOpacity) * inner;
        }
    }
    vec4 base = over(innerFill, bg);

    float T = mod(iTime * Speed, 3600.0);
    float Hmax = hw * 1.6 * GrassHeight * 1.05 + hw * 0.6;
    float rMed = length(abs(p) - g_cc);
    float topYMax = hs.y + hw + Hmax * 1.35;
    bool topG = GrassAmount > 0.0 && p.y > hs.y + 0.5 * hw && (!SwHaveImage || (p.y < topYMax && p.x > -hs.x - 1.5 * hw && p.x < hs.x + 3.5 * hw));
    bool botG = GrassAmount > 0.0 && p.y > -hs.y + 0.6 * hw && p.y < -hs.y + hw + Hmax && (!SwHaveImage || abs(p.x) < hs.x + hw);
    if (abs(d) > 1.9 * hw && !topG && !botG && rMed > g_R + 0.9 * hw) { fragColor = base; return; }

    vec3 H = normalize(LDIR + vec3(0.0, 0.0, 1.0));
    vec4 acc = vec4(0.0); // premultiplied linear

    // ---------------- stone ----------------
    // soft drop shadow of the frame and corner medallions (light from the top left)
    vec2 pS = p + vec2(-0.22, 0.32) * hw;
    float dS = EwSdRoundedBox2D(pS, hs, rad);
    float shBox = smoothstep(1.5 * hw, 0.6 * hw, abs(dS));
    float shMed = smoothstep(g_R + 0.5 * hw, g_R - 0.4 * hw, length(abs(pS) - g_cc));
    acc = vec4(0.0, 0.0, 0.0, ShadowStrength * max(shBox, shMed));
    if (abs(d) < 1.3 * hw || rMed < 1.25 * g_R) {
        // 4 height samples in one loop (stoneHeight inlined once); last = center, keeps g_mask / g_chip
        vec4 hs4 = vec4(0.0);
        vec2 lo = LDIR.xy * min(fs, hw) * 0.12;
        for (int i = ZERO; i < 4; i++) {
            vec2 o = i == 0 ? vec2(1.0, 0.0) : (i == 1 ? vec2(0.0, 1.0) : (i == 2 ? lo : vec2(0.0)));
            float hv = stoneHeight(p + o);
            if (i == 0) hs4.x = hv; else if (i == 1) hs4.y = hv; else if (i == 2) hs4.z = hv; else hs4.w = hv;
        }
        float hx = hs4.x, hy = hs4.y, hl = hs4.z, h0 = hs4.w;
        float cover = g_mask, chip = g_chip, cut = g_rune;
        float k = hw * 0.9 * Relief;
        vec3 n = normalize(vec3(-(hx - h0) * k, -(hy - h0) * k, 1.0));
        float selfSh = 1.0 - 0.8 * smoothstep(0.02, 0.12, (hl - h0) * Relief);

        // granite: grey base, black mica, white quartz / feldspar grains, fine salt and pepper
        float cn = fbm(p / fs * 0.9 + 11.0, 3);
        float gr = EwValueNoise2D(p / fs * 22.0);
        vec3 alb = StoneColor * (0.8 + 0.35 * cn) * (0.9 + 0.2 * gr);
        float g1 = EwValueNoise2D(p / fs * 32.0 + 40.0);
        float g2 = EwValueNoise2D(p / fs * 26.0 + 77.0);
        float gs = EwHash2To1(floor(p / max(fs * 0.015, 1.0)));
        alb *= 1.0 - MicaAmount * smoothstep(0.66, 0.76, g1);                // black mica / hornblende
        alb = mix(alb, QuartzColor, QuartzAmount * smoothstep(0.68, 0.78, g2)); // quartz
        alb *= 0.88 + 0.24 * gs;
        alb = mix(alb, vec3(0.05), 0.5 * step(0.95, gs));                   // pepper
        // dry lichen: pale grey rosettes and a few rusty ones
        float lt = EwValueNoise2D(p / fs * 4.0 + 21.0);
        float li = smoothstep(0.8 - 0.12 * Lichen, 0.86 - 0.12 * Lichen, lt) * smoothstep(0.3, 0.5, h0) * step(0.001, Lichen);
        vec3 lc = mix(LichenColor, RustLichenColor, step(0.78, EwValueNoise2D(p / fs * 1.3 + 5.0)));
        alb = mix(alb, lc, li * 0.6);
        // dust in the recesses, fresh stone in the chips, darker grooves
        alb *= mix(0.6, 1.0, smoothstep(0.1, 0.35, h0));
        alb = mix(alb, alb * 1.15 + 0.02, chip * 0.5);
        alb *= 1.0 - 0.35 * cut;
        // faded red ochre paint left in the carved runes
        float paint = RunePaintAmount * cut * smoothstep(0.3, 0.55, fbm(p / fs * 2.5 + 31.0, 3) + 0.25 * RunePaintAmount);
        alb = mix(alb, RunePaint * (0.7 + 0.5 * cn), paint);

        float ao = mix(0.45, 1.0, smoothstep(0.02, 0.4, h0)) * (1.0 - 0.3 * cut);
        float diff = max(dot(n, LDIR), 0.0) * selfSh;
        vec3 amb = mix(vec3(0.03, 0.03, 0.035), SkyColor, 0.5 + 0.5 * n.y);
        vec3 col = alb * (amb + LightColor * 0.95 * diff) * ao;
        // polished granite sheen + crisp rim on the edges facing the light
        col += pow(max(dot(n, H), 0.0), 40.0) * Sheen * (0.4 + 0.6 * gr) * selfSh * LightColor;
        col += alb * RimLight * smoothstep(0.35, 0.8, dot(n.xy, LDIR.xy)) * selfSh;
        // composited twice on purpose: firmer, slightly crisper silhouette on the soft edges
        acc = over(vec4(col, 1.0) * cover, acc);
        acc = over(vec4(col, 1.0) * cover, acc);
    }

    // ---------------- grass on top of the frame and on the inner bottom ledge ----------------
    // one grassBand call site (each one compiles a full copy); the zones only overlap on tiny
    // screens where the frame interior collapses, the front (bottom) band wins there
    if (topG || botG) acc = over(grassBand(p, botG ? 1 : 0, T), acc);

    // seeds and chaff blown along by the wind (fade in from a light breeze)
    float vis = smoothstep(0.05, 0.6, Wind) * step(0.001, GrassAmount);
    if (vis > 0.0 && (topG || botG)) {
        float zone = topG ? smoothstep(hs.y + 0.8 * hw, hs.y + 1.3 * hw, p.y)
                          : smoothstep(-hs.y + hw, -hs.y + 1.3 * hw, p.y) * smoothstep(-hs.y + hw + Hmax, -hs.y + hw + 0.5 * Hmax, p.y);
        if (SwHaveImage) {
            zone *= topG ? smoothstep(topYMax, hs.y + hw + Hmax * 0.6, p.y)
                         * smoothstep(-hs.x - 0.5 * hw, -hs.x + 0.5 * hw, p.x)
                         * smoothstep(hs.x + 3.0 * hw, hs.x + 0.5 * hw, p.x)
                         : smoothstep(hs.x - 0.3 * hw, hs.x - 1.1 * hw, abs(p.x));
        }
        float G = max(fs * 0.9, 12.0);
        vec2 pp = p - vec2(T * fs * SeedSpeed, 0.0);
        vec2 cell = floor(pp / G);
        vec3 sh3 = EwHash2To3(cell + 71.0);
        if (sh3.x < (0.1 + 0.3 * GrassAmount) * SeedAmount) {
            vec2 c = (cell + 0.5) * G + (sh3.yz - 0.5) * G * 0.5 + vec2(0.0, sin(T * 3.0 + sh3.y * 20.0) * G * 0.15);
            vec2 dq = (pp - c) / vec2(max(fs * 0.05, 2.0), max(fs * 0.018, 0.9));
            float dm = smoothstep(1.0, 0.6, length(dq)) * vis * zone;
            acc = over(vec4(DryGrassColor * 1.4, 1.0) * dm * 0.85, acc);
        }
    }

    // ---------------- output ----------------
    float a = acc.a * smoothstep(0.5, 10.0, min(0.5 * res.x - abs(ps.x), 0.5 * res.y - abs(ps.y)));
    vec3 col = acc.rgb / max(acc.a, 1e-4);
    col = EwLinearToSrgbFast(EwTonemapAces(col * Exposure * 1.3));
    float dz = EwDither(fragCoord, iTime, 2.0);
    a = clamp(a + dz, 0.0, 1.0);
    col = min(max(col + dz, 0.0) * a, vec3(a));
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
