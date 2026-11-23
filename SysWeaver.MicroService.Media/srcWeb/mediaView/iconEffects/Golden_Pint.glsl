// Golden Pint - photoreal procedural beer glass on a transparent background
// A backlit nonic pint in a studio product shot: amber beer with rising bubbles and
// nucleation streams, a lumpy foam head, a thick clear glass base, condensation droplets
// and Fresnel studio reflections; slowly turning like on a turntable.
// Output: premultiplied alpha (transparent around the glass, soft contact shadow).
//  iMouse.x = Bubbles | Amount of rising carbonation bubbles | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Foam Height | Thickness of the foam head | 0.5 | 0.02 .. 0.16
//  iMouse.z = Condensation | Cold droplets and mist on the glass | 0.6 | 0.0 .. 1.0
//  iMouse.w = Backlight | Light glowing through the beer | 0.5 | 0.4 .. 2.0 | x
//  media: image none

// --- Colors ---
const vec3 BeerColor = vec3(0.58, 0.21, 0.025); // var: { "type": "col", "name": "Beer Color", "desc": "Beer tint at full depth: lighter for lager / pilsner, darker for amber ale" }
const vec3 LightColor = vec3(1.0, 0.92, 0.78); // var: { "type": "col", "name": "Light Color", "desc": "Color of the backlight shining through the beer" }
const vec3 FoamColor = vec3(0.97, 0.93, 0.85); // var: { "type": "col", "name": "Foam Color", "desc": "Color of the foam head" }
const vec3 ReflectionTint = vec3(0.95, 0.97, 1.0); // var: { "type": "col", "name": "Reflection Tint", "desc": "Color of the studio lights reflected on the glass" }

// --- Glass and camera ---
const float GlassSize = 1.0; // var: { "min": 0.5, "max": 1.2, "step": 0.01, "name": "Glass Size", "desc": "Size of the glass on screen", "unit": "x" }
const float Perspective = 0.1; // var: { "min": 0.02, "max": 0.25, "step": 0.005, "name": "Camera Height", "desc": "How much the camera looks down onto the glass (rim and foam ellipses)" }
const float TurnSpeed = 0.0; // var: { "min": -0.3, "max": 0.3, "step": 0.005, "name": "Turntable Speed", "desc": "Slow rotation of the glass (droplets, foam, bubbles)", "unit": "rad/s" }

// --- Lighting ---
const float Exposure = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }
const float ReflectionStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Reflections", "desc": "Strength of all glass reflections, the rim highlight and the light sweep", "unit": "x" }
const float SoftboxLight = 9.0; // var: { "min": 0.0, "max": 20.0, "step": 0.5, "name": "Softbox Light", "desc": "Brightness of the wide reflected light strip on the left side of the glass" }
const float StripLight = 14.0; // var: { "min": 0.0, "max": 30.0, "step": 0.5, "name": "Strip Light", "desc": "Brightness of the thin reflected light line on the right side of the glass" }
const float ReflectionDrift = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Reflection Drift", "desc": "How far the reflections slowly sway back and forth", "unit": "x" }
const float SweepStrength = 0.07; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Light Sweep", "desc": "Brightness of the diagonal light sweep gliding across the glass" }
const float SweepPeriod = 11.0; // var: { "min": 3.0, "max": 30.0, "step": 0.5, "name": "Sweep Period", "desc": "Time between two light sweeps", "unit": "s" }

// --- Beer ---
const float BeerGlow = 2.0; // var: { "min": 0.5, "max": 4.0, "step": 0.05, "name": "Center Glow", "desc": "How strongly the backlight glows through the middle of the beer" }
const float BeerOpacity = 0.86; // var: { "min": 0.5, "max": 1.0, "step": 0.01, "name": "Beer Opacity", "desc": "Opacity of the beer: lower lets the page behind shine through more", "scale": 100, "unit": "%" }
const float BeerHaze = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.02, "name": "Haze", "desc": "Cloudy, slowly drifting density variation in the beer (unfiltered look)" }

// --- Bubbles ---
const float BubbleSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Bubble Speed", "desc": "Rising speed of the bubbles", "unit": "x" }
const float BubbleSize = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Bubble Size", "desc": "Size of the rising bubbles and the stream bubbles", "unit": "x" }

// --- Foam ---
const float FoamLumpiness = 0.008; // var: { "min": 0.0, "max": 0.02, "step": 0.0005, "name": "Foam Lumpiness", "desc": "Height of the lumps on top of the foam head" }
const float FoamBigBubbles = 1.0; // var: { "min": 0.0, "max": 1.4, "step": 0.05, "name": "Foam Big Bubbles", "desc": "Amount of large bubbles visible in the side of the foam", "unit": "x" }
const float FoamWobble = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Foam Line Wobble", "desc": "Unevenness of the line between beer and foam", "unit": "x" }

// --- Condensation ---
const float DropSize = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Drop Size", "desc": "Size of the condensation droplets", "unit": "x" }
const float MistAmount = 0.25; // var: { "min": 0.0, "max": 0.8, "step": 0.01, "name": "Mist", "desc": "Frosty haze of the cold glass behind the droplets" }

// --- Shadow ---
const bool ContactShadow = true; // var: { "name": "Contact Shadow", "desc": "Soft shadow under the glass (for compositing)" }
const float ShadowOpacity = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow Opacity", "desc": "Darkness of the contact shadow", "scale": 100, "unit": "%" }

// --- Etched logo (optional image) ---
const float LogoSize = 1.4; // var: { "min": 0.3, "max": 1.6, "step": 0.02, "name": "Logo Size", "desc": "Size of the etched logo on the glass", "unit": "x" }
const float LogoHeight = 0.03; // var: { "min": -0.25, "max": 0.25, "step": 0.01, "name": "Logo Height", "desc": "Vertical position of the etched logo on the glass" }
const float LogoAngle = 10.0; // var: { "min": -180.0, "max": 180.0, "step": 1.0, "name": "Logo Angle", "desc": "Starting angle of the logo around the glass", "unit": "°" }
const float EtchStrength = 1.0; // var: { "min": 0.0, "max": 1.5, "step": 0.02, "name": "Etch Frost", "desc": "Strength of the frosted glass etching", "scale": 100, "unit": "%" }
const float LogoColorMix = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.02, "name": "Fixed Color Mix", "desc": "Mix from original image colors (0%) to the fixed Logo Color (100%)", "scale": 100, "unit": "%" }
const vec3 LogoColor = vec3(1.0, 1.0, 1.0); // var: { "type": "col", "name": "Logo Color", "desc": "Fixed tint color for the etched logo (white for classic frosted glass)" }

#define BubbleAmount mix(0.0, 2.0, iMouse.x)
#define FoamHeight mix(0.02, 0.16, iMouse.y)
#define Condensation mix(0.0, 1.0, iMouse.z)
#define Backlight mix(0.4, 2.0, iMouse.w)

// glass geometry (structural): rim height, bottom height, wall thickness, thick base,
// radius used for the unrolled surface coordinates
const float GTop = 0.44;
const float GBot = -0.46;
const float Wall = 0.0065;
const float BaseH = 0.07;
const float R0 = 0.18;

#define ZERO min(iFrame, 0)

// half width of the nonic pint at height y (taper + bulge below the rim)
float glassHalfWidth(float y)
{
    float t = clamp((y - GBot) / (GTop - GBot), 0.0, 1.0);
    float b = (t - 0.8) / 0.09;
    return mix(0.148, 0.2, t) + 0.012 * exp(-b * b);
}

// half width and analytic dy-slope together (1 exp instead of 5)
vec2 glassProfile(float y)
{
    float t = clamp((y - GBot) / (GTop - GBot), 0.0, 1.0);
    float b = (t - 0.8) / 0.09;
    float eb = 0.012 * exp(-b * b);
    float w = mix(0.148, 0.2, t) + eb;
    float inR = step(GBot, y) * step(y, GTop);
    float dw = inR * ((0.2 - 0.148) - eb * (2.0 * b / 0.09)) / (GTop - GBot);
    return vec2(w, dw);
}

// premultiplied "over" compositing
vec4 over(vec4 dst, vec4 src) { return src + dst * (1.0 - src.a); }

// non-unrolled 2D FBM matching EwFbm2D bit-for-bit (ZERO prevents FXC unrolling)
float fbm2(vec2 p, int octaves)
{
    const mat2 octT = mat2(1.6, 1.2, -1.2, 1.6);
    float sum = 0.0;
    float amp = 0.5;
    for (int i = ZERO; i < 4; i++) {
        if (i >= octaves) break;
        sum += amp * EwValueNoise2D(p);
        p = octT * p;
        amp *= 0.5;
    }
    return sum;
}

// non-unrolled 2D Voronoi matching EwVoronoi2D (ZERO stops 9x unroll; squared dist saves 7 sqrt/call)
vec3 voronoi2(vec2 p)
{
    vec2 cell = floor(p);
    vec2 fr = fract(p);
    float d1 = 9.0;
    float d2 = 9.0;
    vec2 best = cell;
    for (int k = ZERO; k < 9; k++) {
        int ky = k / 3;
        vec2 nb = vec2(float(k - ky * 3 - 1), float(ky - 1));
        vec2 r = nb + EwHash2To2(cell + nb) - fr;
        float d = dot(r, r);
        if (d < d1) {
            d2 = d1;
            d1 = d;
            best = cell + nb;
        } else if (d < d2) {
            d2 = d;
        }
    }
    return vec3(sqrt(d1), sqrt(d2), EwHash2To1(best));
}

// a shaded bubble (dark rim, highlight + caustic) blended into col
void drawBubble(inout vec3 col, vec2 p, vec2 c, float r, float px, float fade, vec3 lightCol)
{
    vec2 d = p - c;
    float rp = r + px;
    float d2 = dot(d, d);
    if (d2 >= rp * rp) return;
    float cov = 1.0 - smoothstep(r - px, rp, sqrt(d2));
    if (cov <= 0.0) return;
    float invR = 1.0 / max(r, 1e-5);
    vec2 n = d * invR;
    float r2 = min(d2 * invR * invR, 1.0);
    float rim = smoothstep(0.3, 1.0, r2);
    vec2 hs = n - vec2(-0.35, 0.4);
    vec2 hc = n - vec2(0.1, -0.4);
    vec3 bc = col * (1.6 - 1.25 * rim)
            + lightCol * (1.6 * exp(-dot(hs, hs) * 14.0) + 0.3 * exp(-dot(hc, hc) * 5.0));
    col = mix(col, bc, cov * fade);
}

// free rising bubbles on a cylinder shell (k = fraction of the inner radius)
void bubbleLayer(inout vec3 col, vec2 p, float wi, float k, float cell, float speed, float seed,
                 float density, float rot, float t, float px, vec3 lightCol, float ybi, float ytop)
{
    float xs = p.x / (k * wi);
    if (abs(xs) >= 1.0 || density <= 0.0) return;
    float a = (asin(xs) - rot) * k * R0;
    vec2 g = vec2(a, p.y - t * speed) / cell;
    vec2 id = floor(g);
    vec4 h = EwHash2To4(id + seed);
    if (h.x > density) return;
    float yc = (id.y + 0.5 + (h.z - 0.5) * 0.4) * cell + t * speed;
    float rise = clamp((yc - ybi) / max(ytop - ybi, 1e-3), 0.0, 1.0);
    float r = cell * mix(0.07, 0.17, h.w) * (0.7 + 0.6 * rise) * BubbleSize; // bubbles grow while rising
    if (abs(p.y - yc) >= r + px) return;
    float ac = (id.x + 0.5 + (h.y - 0.5) * 0.4) * cell;
    float ph = ac / (k * R0) + rot + 0.015 * sin(t * 3.0 + h.w * 40.0);
    float ca = cos(ph);
    if (ca <= 0.0) return;
    vec2 c = vec2(k * wi * sin(ph), yc);
    drawBubble(col, p, c, r, px, (0.35 + 0.65 * k) * smoothstep(0.0, 0.3, ca), lightCol);
}

// condensation droplets: xy = drop normal (in drop radii), z = coverage
vec3 dropLayer(vec2 q, float cell, float amount, float seed, float px)
{
    vec2 g = q / cell;
    vec2 id = floor(g);
    vec2 f = fract(g) - 0.5;
    vec4 h = EwHash2To4(id + seed);
    if (h.x > amount) return vec3(0.0);
    vec2 c = (h.yz - 0.5) * 0.3;
    float r = mix(0.12, 0.3, h.w);
    vec2 d = (f - c) * vec2(1.0, 0.85);
    d.y *= d.y < 0.0 ? 0.85 : 1.15; // heavier at the bottom
    float aa = px / cell;
    float ra = r + aa;
    float d2 = dot(d, d);
    if (d2 >= ra * ra) return vec3(0.0);
    float cov = 1.0 - smoothstep(r - aa, ra, sqrt(d2));
    return vec3(d / r, cov);
}

// foam head height above the liquid level hf, q = (x, depth z) on the top disk
float foamH(vec2 q, float invWi2, vec2 rotCS, float tOff)
{
    float r2 = min(dot(q, q) * invWi2, 1.0);
    vec2 rq = vec2(rotCS.x * q.x - rotCS.y * q.y, rotCS.y * q.x + rotCS.x * q.y);
    float n = fbm2(rq * 16.0 + vec2(3.1, tOff), 4);
    // dome + lumps + a slightly raised edge where the foam climbs the glass
    return 0.006 * (1.0 - r2) + FoamLumpiness * n + 0.002 * smoothstep(0.75, 1.0, r2);
}

// studio environment: dark room, a wide strip softbox left, a thin strip right, back light
float studioEnv(vec3 R, float drift)
{
    float az = atan(R.x, R.z);
    float el = asin(clamp(R.y, -1.0, 1.0));
    float vert = smoothstep(-0.5, -0.3, el) * (1.0 - smoothstep(0.5, 0.7, el)) * (1.0 + 1.5 * max(el + 0.1, 0.0));
    float a1 = az + 1.1 + drift;
    float e = SoftboxLight * vert * (1.0 - smoothstep(0.17, 0.2, abs(a1))) * (0.45 + 0.55 * (1.0 - smoothstep(-0.2, 0.2, a1)));
    float a2 = az - 2.05 - 0.5 * drift;
    e += StripLight * vert * (1.0 - smoothstep(0.035, 0.07, abs(a2)));
    e += 1.5 * smoothstep(2.4, 3.0, abs(az)) * smoothstep(-0.6, -0.1, el);
    return e + 0.04;
}
// wrap an angle into [-pi, pi]
float wrapAngle(float a) { return mod(a + EW_PI, EW_TAU) - EW_PI; }

// aspect-ratio-preserving physical (width, height) of the optional etched logo on the glass
vec2 logoSpanSize()
{
    vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
    float imgAsp = texRes.x / texRes.y;
    vec2 box = vec2(0.27, 0.29) * LogoSize;
    float boxAsp = box.x / box.y;
    return imgAsp > boxAsp ? vec2(box.x, box.x / imgAsp) : vec2(box.y * imgAsp, box.y);
}

// sample optional transparent logo mapped onto the glass surface; returns rgba (a = etch mask)
// and outputs surface-space bevel gradient (only when lodBias <= 0.01)
vec4 sampleEtch(float phi, float ySurf, float foreshorten, float px, float lodBias, out vec2 grad)
{
    grad = vec2(0.0);
    if (EtchStrength <= 0.0 || LogoSize <= 0.0) return vec4(0.0);
    float dPhi = wrapAngle(phi - LogoAngle * (EW_PI / 180.0));
    if (abs(dPhi) > 1.45) return vec4(0.0);
    vec2 span = logoSpanSize();
    float wS = glassHalfWidth(ySurf);
    vec2 uv = vec2(dPhi * wS / span.x + 0.5, (ySurf - LogoHeight) / span.y + 0.5);
    vec2 duv = vec2(px / max(foreshorten * span.x, 1e-4), px / max(span.y, 1e-4));
    vec2 bm = smoothstep(vec2(0.0), duv, uv) * smoothstep(vec2(0.0), duv, 1.0 - uv);
    float borderM = bm.x * bm.y;
    if (borderM <= 0.0) return vec4(0.0);
    vec2 texRes = max(iChannelResolution[0].xy, vec2(1.0));
    vec2 texPx = duv * texRes;
    float lod = clamp(log2(max(max(texPx.x, texPx.y), 1.0)) - 0.35 + lodBias, 0.0, 5.0);
    vec4 tex = textureLod(iChannel0, uv, lod);
    tex.a *= borderM;
    if (lodBias <= 0.01) {
        vec2 st = max(duv * 1.15, 1.0 / texRes);
        float gLod = min(lod + 0.4, 5.0);
        float aR = 0.0, aL = 0.0, aU = 0.0, aD = 0.0;
        for (int gi = ZERO; gi < 4; gi++) {
            vec2 ofs = gi < 2 ? vec2(gi == 0 ? st.x : -st.x, 0.0) : vec2(0.0, gi == 2 ? st.y : -st.y);
            vec2 guv = uv + ofs;
            float ga = (guv.x > 0.0 && guv.x < 1.0 && guv.y > 0.0 && guv.y < 1.0)
                     ? textureLod(iChannel0, guv, gLod).a : 0.0;
            if (gi == 0) aR = ga;
            else if (gi == 1) aL = ga;
            else if (gi == 2) aU = ga;
            else aD = ga;
        }
        grad = vec2(aR - aL, aU - aD) * borderM;
    }
    return tex;
}

// blend original sRGB logo color (0) toward fixed LogoColor (1), then decode to linear light
// colorAmt is 0 for pure white fixed frosting and approaches 1 for original or tinted colors
vec3 logoPigment(vec3 texRgb, out float colorAmt)
{
    vec3 fixedCol = clamp(LogoColor, 0.0, 1.0);
    vec3 mixedSrgb = mix(clamp(texRgb, 0.0, 1.0), fixedCol, LogoColorMix);
    colorAmt = clamp(max(1.0 - LogoColorMix, 1.0 - min(fixedCol.r, min(fixedCol.g, fixedCol.b))), 0.0, 1.0);
    return EwSrgbToLinear(mixedSrgb);
}

// studio environment blurred for rough frosted / etched glass: x = broad specular, y = diffuse
vec2 roughStudioEnv(vec3 N, vec3 R, float drift)
{
    float az = atan(R.x, R.z);
    float el = asin(clamp(R.y, -1.0, 1.0));
    float vert = smoothstep(-0.7, -0.25, el) * (1.0 - smoothstep(0.45, 0.85, el)) * (1.0 + 0.9 * max(el + 0.1, 0.0));
    float a1 = wrapAngle(az + 1.1 + drift);
    float a2 = wrapAngle(az - 2.05 - 0.5 * drift);
    float spec = SoftboxLight * vert * 0.26 * exp(-a1 * a1 / 0.34)
               + StripLight   * vert * 0.11 * exp(-a2 * a2 / 0.22);
    float diffL = SoftboxLight * 0.05 * (0.35 + 0.65 * clamp(dot(N, normalize(vec3(-0.75, 0.2, 0.63))), 0.0, 1.0));
    float diffR = StripLight   * 0.02 * (0.25 + 0.75 * clamp(dot(N, normalize(vec3(0.85, 0.15, 0.5))), 0.0, 1.0));
    return vec2(spec, diffL + diffR + 0.16);
}


varying vec2 vP;
flat varying vec4 vFrameA; // t, rot, hf, drift
flat varying vec4 vFrameB; // px, sw, cos(rot), sin(rot)
flat varying vec4 vLightA; // L.rgb, foamTOff
flat varying vec3 vLightB; // key.rgb
flat varying vec4 vGeom;   // V.y, V.z, dA, dB
flat varying vec4 vStr0;   // nucleation streams 0..3: k * sin(ph), or 99.0 if inactive
flat varying vec4 vStr1;   // nucleation streams 4..7
flat varying vec2 vStr2;   // nucleation streams 8..9
flat varying int vStreamN; // active nucleation stream count (0..10)

void vertexMain(vec2 fragCoord)
{
    float scl = max(1.0, 0.62 * iResolution.y / iResolution.x) / GlassSize;
    vP = (fragCoord - 0.5 * iResolution.xy) / iResolution.y * scl;
    float px = scl / iResolution.y;
    float t = mod(iTime, 3600.0);
    float rot = t * TurnSpeed;
    float hf = GTop - 0.007 + 0.0015 * sin(t * 0.7);
    float drift = ReflectionDrift * (0.04 * sin(t * 0.23) + 0.025 * sin(t * 0.11 + 2.0));
    float sw = fract(t / SweepPeriod) * 4.0 - 1.5;
    vFrameA = vec4(t, rot, hf, drift);
    vFrameB = vec4(px, sw, cos(rot), sin(rot));
    vec3 L = LightColor * Backlight;
    vLightA = vec4(L, 7.7 + t * 0.004);
    vLightB = mix(L, vec3(1.0), 0.5);
    vec3 V = normalize(vec3(0.0, -Perspective, -1.0));
    float fp = px / max(Perspective, 0.02);
    vGeom = vec4(V.y, V.z, 1.0 - smoothstep(0.4, 1.0, fp * 55.0), 1.0 - smoothstep(0.4, 1.0, fp * 110.0));

    float maxStreams = BubbleAmount * 5.0;
    vStreamN = clamp(int(ceil(maxStreams)), 0, 10);
    float strK[10];
    for (int i = ZERO; i < 10; i++) {
        float fi = float(i);
        float ks = 99.0;
        if (fi < maxStreams) {
            vec3 h = EwHash1To3(fi * 7.31 + 1.7);
            float ph = (h.x - 0.5) * 2.9 + rot;
            if (cos(ph) >= 0.1) ks = mix(0.3, 0.92, h.y) * sin(ph);
        }
        strK[i] = ks;
    }
    vStr0 = vec4(strK[0], strK[1], strK[2], strK[3]);
    vStr1 = vec4(strK[4], strK[5], strK[6], strK[7]);
    vStr2 = vec2(strK[8], strK[9]);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 p = vP;
    float t = vFrameA.x;
    float rot = vFrameA.y;
    float px = vFrameB.x;
    float E = Perspective;
    vec3 L = vLightA.xyz;
    vec3 key = vLightB;
    const float wt = 0.200086004;
    const float wb = 0.148;
    float ax = abs(p.x);
    vec4 acc = vec4(0.0); // premultiplied, linear

    // soft elliptic contact shadow (alpha only: darkens the page below the glass)
    if (ContactShadow) {
        vec2 sq = vec2(p.x / (wb * 1.25), (p.y - GBot) / (E * wb * 1.25 + 0.012));
        float sd2 = dot(sq, sq);
        if (sd2 < 6.5) acc.a = ShadowOpacity * exp(-sd2 * 1.6);
    }

    // ---- silhouette (bounding-box early out skips ~80% transparent background pixels)
    float yTopMax = GTop + E * wt;
    float yBotMin = GBot - E * wb;
    if (ax <= 0.216 + px && p.y >= yBotMin - px && p.y <= yTopMax + px) {
        vec2 wp = glassProfile(p.y);
        float w = wp.x;
        float dwdy = wp.y;
        float xt = p.x / wt;
        float xb = p.x / wb;
        float st = sqrt(max(1.0 - xt * xt, 0.0));
        float sb = sqrt(max(1.0 - xb * xb, 0.0));
        float yRimBack = GTop + E * wt * st;
        float yBotFront = GBot - E * wb * sb;
        // analytic coverage: distances divided by the slope of the ellipses (true pixel distance)
        float slT = E * abs(xt) / max(st, 0.05);
        float slBo = E * abs(xb) / max(sb, 0.05);
        float invDw = inversesqrt(1.0 + dwdy * dwdy);
        float invSlBo = inversesqrt(1.0 + slBo * slBo);
        float glassM = clamp((w - ax) * invDw / px + 0.5, 0.0, 1.0)
                     * clamp((yRimBack - p.y) * inversesqrt(1.0 + slT * slT) / px + 0.5, 0.0, 1.0)
                     * clamp((p.y - yBotFront) * invSlBo / px + 0.5, 0.0, 1.0);

        if (glassM > 0.0) {
            float nx = clamp(p.x / w, -1.0, 1.0);
            float s = sqrt(max(1.0 - nx * nx, 0.0));
            float wi = w - Wall;
            float xi = clamp(p.x / wi, -1.0, 1.0);
            float si = sqrt(max(1.0 - xi * xi, 0.0));
            float mIn = clamp((wi - ax) / px + 0.5, 0.0, 1.0);
            float mWall = 1.0 - mIn;
            float ybi = GBot + BaseH;
            float zm = wi * si;
            float eW = E * zm;
            float yBeerBot = ybi - eW;
            // skip asin(xi) and hl wobble noises on glass base pixels
            float asinXi = p.y >= yBeerBot - px ? asin(xi) : 0.0;
            float th = asinXi + rot;
            float u = th * R0;
            // liquid level under the foam (hl) wobbles slightly around the glass
            float hf = vFrameA.z;
            float hl = hf - FoamHeight;
            if (FoamWobble != 0.0 && p.y >= yBeerBot - px) {
                hl += FoamWobble * (0.007 * (EwValueNoise2D(vec2(th * 2.5, t * 0.25)) - 0.5)
                    + 0.004 * (EwValueNoise2D(vec2(th * 14.0, 3.0 + t * 0.1)) - 0.5));
            }
            float yFoamLine = hl - eW;

            vec2 rotCS = vFrameB.zw;
            float invWi2 = 1.0 / max(wi * wi, 1e-6);
            float foamTOff = vLightA.w;

            // foam top curves only need evaluating near the upper band (skips 2x 4-oct FBM on beer/base!)
            float yFoamTopF = max(hf - eW, yFoamLine);
            float yFoamTopB = max(hf + eW, yFoamTopF);
            float c3, c4;
            if (p.y < hf - eW - 0.022 - px) {
                c3 = 0.0;
                c4 = 0.0;
            } else if (p.y > hf + 0.0085 + FoamLumpiness + eW + px) {
                c3 = 1.0;
                c4 = 1.0;
            } else {
                bool needBack = p.y >= hf + eW - px;
                for (int i = ZERO; i < 2; i++) {
                    if (i == 1 && !needBack) break;
                    float zSign = i == 0 ? -zm : zm;
                    float fh = foamH(vec2(p.x, zSign), invWi2, rotCS, foamTOff);
                    if (i == 0) yFoamTopF = max(hf + fh - eW, yFoamLine);
                    else        yFoamTopB = max(hf + fh + eW, yFoamTopF);
                }
                c3 = clamp((p.y - yFoamTopF) / px + 0.5, 0.0, 1.0);
                c4 = needBack ? clamp((p.y - yFoamTopB) / px + 0.5, 0.0, 1.0) : 0.0;
            }

            // region weights: a partition of unity (no seams between beer / foam / top)
            float c1 = clamp((p.y - yBeerBot) / px + 0.5, 0.0, 1.0);
            float c2 = clamp((p.y - yFoamLine) / px + 0.5, 0.0, 1.0);
            float wBase = (1.0 - c1) * mIn;
            float wBeer = (c1 - c2) * mIn;
            float wFoamS = (c2 - c3) * mIn;
            float wFoamT = (c3 - c4) * mIn;
            float wEmpty = c4 * mIn;

            const vec3 bc = max(BeerColor, vec3(0.003));
            vec3 bcSqrt = sqrt(bc);
            vec3 bcLog2 = log2(bc);
            float drift = vFrameA.w;
            vec3 V = vec3(0.0, vGeom.xy);
            vec3 No = vec3(nx, -dwdy, s) * invDw;
            vec3 Ni = vec3(xi, -dwdy, si) * invDw;
            float Fi = EwFresnelSchlick(-dot(V, Ni), 0.04);
            vec4 body = vec4(0.0);

            // glass wall band (refracted contents, compressed and dark)
            if (mWall > 0.0) {
                float hTop = hf + 0.003;
                float fB = clamp((hl - p.y) / px + 0.5, 0.0, 1.0) * clamp((p.y - ybi) / px + 0.5, 0.0, 1.0);
                float fF = clamp((p.y - hl) / px + 0.5, 0.0, 1.0) * clamp((hTop - p.y) / px + 0.5, 0.0, 1.0);
                float fBase = clamp((ybi - p.y) / px + 0.5, 0.0, 1.0);
                float fE = clamp((p.y - hTop) / px + 0.5, 0.0, 1.0);
                vec3 Tw = pow(bc, vec3(1.3));
                vec4 wc = fB * vec4(L * Tw * 0.45 * 0.95, 0.95)
                        + fF * vec4(FoamColor * key * 0.75 * 0.95, 0.95)
                        + fBase * vec4(L * bcSqrt * 0.45 * 0.75, 0.75)
                        + fE * vec4(vec3(0.5) * 0.12, 0.12);
                body += wc * mWall;
            }

            // empty upper glass (back wall seen through, including optional etched logo on back)
            if (wEmpty > 0.0) {
                vec4 emptyC = vec4(vec3(0.45) * 0.07, 0.07);
                if (SwHaveImage) {
                    float phiB = EW_PI - asinXi + rot;
                    float ySurfB = p.y - eW;
                    float wallClipB = 1.0 - smoothstep(GTop - 0.014, GTop - 0.004, ySurfB);
                    vec2 unusedG;
                    vec4 backTex = sampleEtch(phiB, ySurfB, max(si, 0.12), px, 1.0, unusedG);
                    float backEtch = backTex.a * min(EtchStrength, 1.0) * wallClipB * smoothstep(0.05, 0.25, si);
                    if (backEtch > 0.0) {
                        float bColorAmt;
                        vec3 bPig = logoPigment(backTex.rgb, bColorAmt);
                        float aBE = backEtch * mix(0.32, 0.45, bColorAmt);
                        vec3 cPure = ReflectionTint * (0.28 + 0.18 * ReflectionStrength) + L * 0.08;
                        vec3 cCol = bPig * (ReflectionTint * (0.35 + 0.2 * ReflectionStrength) + L * 0.15);
                        vec3 cBE = mix(cPure, cCol, bColorAmt);
                        emptyC = over(emptyC, vec4(cBE * aBE, aBE));
                    }
                }
                body += emptyC * wEmpty;
            }

            // beer
            if (wBeer > 0.0) {
                float hy = clamp((p.y - ybi) / max(hl - ybi, 1e-3), 0.0, 1.0);
                vec3 Tc = exp2(bcLog2 * (0.4 + 0.85 * si));
                // backlight glow: brightest in the middle, a bit below the centre of the beer
                float hot = exp(-xi * xi * 2.2) * (0.6 + 0.5 * exp(-EwSquare(hy - 0.35) * 4.0));
                float haze = BeerHaze > 0.0 ? fbm2(vec2(u * 8.0, p.y * 4.0 - t * 0.06), 4) : 0.5;
                float streak = EwValueNoise2D(vec2(u * 45.0, p.y * 1.2 - t * 0.03));
                vec3 beer = L * Tc * (0.2 + BeerGlow * hot) * (1.0 + BeerHaze * (haze - 0.5)) * (0.88 + 0.3 * streak * streak);
                beer *= mix(0.7, 1.0, smoothstep(0.0, 0.35, hy));
                // refraction at the sides: darker, deeper amber + thin total internal reflection line
                float edgeF = 1.0 - smoothstep(0.04, 0.75, si);
                beer = mix(beer, L * (bc * bc) * 0.4, edgeF * 0.9);
                vec3 bc07 = pow(bc, vec3(0.7));
                if (si < 0.35) {
                    float tir = (si - 0.13) / 0.05;
                    beer += L * bc07 * 0.8 * exp(-tir * tir);
                }
                // studio reflections on the back wall, seen through the beer (Fb == Fi since V.x == 0)
                if (hy > 0.0) {
                    vec3 Nb = vec3(-xi, -dwdy, si) * invDw;
                    float envB = studioEnv(reflect(V, Nb), -drift);
                    beer += pow(bc, vec3(1.2)) * Fi * envB * 0.18 * smoothstep(0.0, 0.2, hy);
                }
                // light caught under the foam (liquid surface seen from below)
                if (p.y > yFoamLine - 0.1) {
                    float uf = exp(-max(yFoamLine - p.y, 0.0) / 0.01);
                    beer = mix(beer, L * pow(bc, vec3(0.45)) * 1.1, uf * 0.55);
                }
                // the glass floor seen through the beer: lit from below through the base,
                // its back edge a soft bright arc, darker refraction line just above it
                float yFB = ybi + eW;
                if (p.y < yFB + 0.025) {
                    float onFloor = 1.0 - smoothstep(yFB - 1.5 * px, yFB + 1.5 * px, p.y);
                    if (onFloor > 0.0) {
                        float fc = EwValueNoise2D(vec2(th * 2.0, (p.y - ybi) / max(E * wi, 1e-4) * 3.0 + t * 0.04));
                        vec3 floorC = L * pow(bc, vec3(0.75)) * (0.6 + 0.5 * fc) * (0.4 + 0.6 * si);
                        beer = mix(beer, floorC, onFloor * 0.35);
                    }
                    beer += L * bcSqrt * 0.22 * exp(-abs(p.y - yFB) / max(0.0015, px)) * si;
                    beer *= 1.0 - 0.25 * exp(-EwSquare((p.y - yFB - 0.005) / 0.004)) * si;
                }
                // droplets on the back of the glass, blurred through the beer
                if (Condensation > 0.0) {
                    vec2 bq = vec2((3.14159 - asinXi + rot) * R0, p.y);
                    vec3 db = dropLayer(bq, 0.024, 0.55 * Condensation, 5.0, px * 5.0);
                    float br2 = min(dot(db.xy, db.xy), 1.0);
                    beer *= 1.0 + db.z * (0.3 - 0.55 * smoothstep(0.4, 1.0, br2)) * smoothstep(0.1, 0.4, si);
                }
                // optional etched logo on the back of the glass, seen blurred & mirrored through the beer
                if (SwHaveImage) {
                    float phiB = EW_PI - asinXi + rot;
                    float ySurfB = p.y - eW;
                    vec2 unusedG;
                    vec4 backTex = sampleEtch(phiB, ySurfB, max(si, 0.12), px, 1.4, unusedG);
                    float backEtch = backTex.a * min(EtchStrength, 1.0) * smoothstep(0.08, 0.35, si);
                    if (backEtch > 0.0) {
                        float bColorAmt;
                        vec3 bPig = logoPigment(backTex.rgb, bColorAmt);
                        vec3 bFrost = bc07 * (L * 0.35 + ReflectionTint * ReflectionStrength * 0.22);
                        vec3 beerPure = beer * 0.85 + bFrost;
                        vec3 beerPig = beer * mix(0.45, 0.92, EwLuminance(bPig)) + bFrost * bPig * 0.65;
                        beer = mix(beer, mix(beerPure, beerPig, bColorAmt), backEtch * 0.55);
                    }
                }

                float bA = BubbleAmount;
                if (bA > 0.0) {
                    float bs = BubbleSpeed;
                    // 3 free rising bubble shells (loop with ZERO avoids 3x inlining)
                    for (int bi = ZERO; bi < 3; bi++) {
                        float k    = bi == 0 ? 0.40 : (bi == 1 ? 0.68 : 0.93);
                        float cell = bi == 0 ? 0.050 : (bi == 1 ? 0.038 : 0.030);
                        float spd  = (bi == 0 ? 0.07 : (bi == 1 ? 0.09 : 0.11)) * bs;
                        float seed = bi == 0 ? 11.0 : (bi == 1 ? 23.0 : 37.0);
                        float dens = (bi == 2 ? 0.25 : 0.30) * bA;
                        bubbleLayer(beer, p, wi, k, cell, spd, seed, dens, rot, t, px, L, ybi, hl);
                    }

                    // nucleation streams: SIMD early-out skips loop on ~80% of beer pixels
                    vec4 d0 = abs(p.x - vStr0 * wi);
                    vec4 d1 = abs(p.x - vStr1 * wi);
                    vec2 d2 = abs(p.x - vStr2 * wi);
                    float minD = min(min(min(d0.x, d0.y), min(d0.z, d0.w)),
                                     min(min(min(d1.x, d1.y), min(d1.z, d1.w)), min(d2.x, d2.y)));
                    if (minD <= 0.012) {
                        for (int i = ZERO; i < 10; i++) {
                            if (i >= vStreamN) break;
                            float kSin = i < 4 ? (i < 2 ? (i == 0 ? vStr0.x : vStr0.y) : (i == 2 ? vStr0.z : vStr0.w))
                                       : (i < 8 ? (i < 6 ? (i == 4 ? vStr1.x : vStr1.y) : (i == 6 ? vStr1.z : vStr1.w))
                                                : (i == 8 ? vStr2.x : vStr2.y));
                            float sx = kSin * wi;
                            if (abs(p.x - sx) > 0.012) continue;
                            float fi = float(i);
                            vec3 h = EwHash1To3(fi * 7.31 + 1.7);
                            float ph = (h.x - 0.5) * 2.9 + rot;
                            float ca = cos(ph);
                            float k = mix(0.3, 0.92, h.y);
                            float sp = mix(0.014, 0.026, h.z);
                            float v = bs * mix(0.06, 0.1, h.z);
                            float id = floor((p.y - ybi - t * v) / sp);
                            float yc = ybi + (id + 0.5) * sp + t * v;
                            float rise = clamp((yc - ybi) / max(hl - ybi, 1e-3), 0.0, 1.0);
                            float r = mix(0.0013, 0.0038, rise) * BubbleSize;
                            vec2 c = vec2(sx + 0.0012 * sin(yc * 80.0 + fi * 5.0), yc);
                            drawBubble(beer, p, c, r, px, (0.4 + 0.6 * k) * smoothstep(0.1, 0.4, ca), L);
                        }
                    }
                }
                float aBeer = mix(BeerOpacity, 0.97, edgeF);
                body += vec4(beer * aBeer, aBeer) * wBeer;
            }

            // foam, side view
            if (wFoamS > 0.0) {
                vec2 fq = vec2(u, p.y);
                fq += 0.0025 * (vec2(EwValueNoise2D(fq * 25.0 + t * 0.05), EwValueNoise2D(fq * 25.0 - t * 0.05 + 7.0)) - 0.5);
                float fy = clamp((p.y - yFoamLine) / max(FoamHeight, 1e-3), 0.0, 1.0);
                float lam = 0.6 + 0.4 * clamp(dot(vec2(xi, si), vec2(-0.45, 0.89)), 0.0, 1.0);
                // dense fine foam + larger bubbles pressed against the glass (1 Voronoi call site)
                float mott = fbm2(fq * vec2(70.0, 110.0) + 2.0, 3);
                vec3 vS = vec3(0.0), vL = vec3(0.0);
                for (int vi = ZERO; vi < 2; vi++) {
                    if (vi == 1 && FoamBigBubbles <= 0.0) break;
                    vec3 vv = voronoi2(vi == 0 ? fq * 170.0 + 3.7 : fq * 70.0);
                    if (vi == 0) vS = vv; else vL = vv;
                }
                vec3 foam = FoamColor * key * 1.1 * lam * (0.82 + 0.35 * mott);
                foam *= mix(0.9, 1.03, smoothstep(0.05, 0.5, vS.x));
                if (FoamBigBubbles > 0.0) {
                    float bigP = mix(0.7, 0.12, smoothstep(0.0, 0.7, fy)) * FoamBigBubbles;
                    float sel = step(1.0 - bigP, vL.z);
                    float rB = 0.45 * (0.55 + 0.45 * fract(vL.z * 13.7));
                    float inB = (1.0 - smoothstep(rB - 0.06, rB, vL.x)) * sel;
                    float ring = exp(-EwSquare((vL.x - rB + 0.05) / 0.04)) * sel;
                    vec3 bubIn = mix(L * bcSqrt * 0.9, FoamColor * key * 0.6 * lam, smoothstep(0.0, 0.6, fy));
                    foam = mix(foam, bubIn * (0.75 + 0.5 * vL.x / rB), inB * 0.85);
                    foam += key * 0.3 * ring;
                }
                // wet, beer tinted bottom of the head
                foam = mix(foam, L * pow(bc, vec3(0.35)) * 0.95, (1.0 - smoothstep(0.0, 0.18, fy)) * 0.55);
                foam *= 1.0 + 0.15 * smoothstep(-0.02, 0.0, p.y - yFoamTopF);
                body += vec4(foam, 1.0) * wFoamS;
            }

            // foam, top surface: lumpy domed height field, first hit of the view ray
            // unified loop (bracket + bisect + gradient) keeps a single foamH call site
            if (wFoamT > 0.0) {
                float za = -zm, zb = zm;
                float dz = 0.2 * zm;
                float h0 = 0.0, hx = 0.0, hz = 0.0;
                vec2 q = vec2(p.x, 0.0);
                int phase = 0;
                int stepIdx = 1;
                for (int iter = ZERO; iter < 18; iter++) {
                    vec2 qp;
                    if (phase == 0) {
                        qp = vec2(p.x, -zm + float(stepIdx) * dz);
                    } else if (phase == 1) {
                        qp = vec2(p.x, 0.5 * (za + zb));
                    } else {
                        qp = q + (stepIdx == 1 ? vec2(0.003, 0.0) : (stepIdx == 2 ? vec2(0.0, 0.003) : vec2(0.0)));
                    }
                    float fh = foamH(qp, invWi2, rotCS, foamTOff);
                    if (phase == 0) {
                        if (hf + fh + E * qp.y >= p.y) { zb = qp.y; phase = 1; stepIdx = 0; }
                        else { za = qp.y; if (++stepIdx > 10) { phase = 1; stepIdx = 0; } }
                    } else if (phase == 1) {
                        if (hf + fh + E * qp.y >= p.y) zb = qp.y; else za = qp.y;
                        if (++stepIdx >= 5) { q = vec2(p.x, 0.5 * (za + zb)); phase = 2; stepIdx = 0; }
                    } else {
                        if (stepIdx == 0) h0 = fh;
                        else if (stepIdx == 1) hx = fh;
                        else { hz = fh; break; }
                        stepIdx++;
                    }
                }
                float gx = (hx - h0) / 0.003;
                float gz = (hz - h0) / 0.003;
                vec3 n = normalize(vec3(-gx * 8.0, 1.0, -gz * 8.0));
                float rr = length(q) / max(wi, 1e-4);
                vec2 rq = vec2(rotCS.x * q.x - rotCS.y * q.y, rotCS.y * q.x + rotCS.x * q.y);
                float dA = vGeom.z;
                float dB = vGeom.w;
                vec3 vA = vec3(0.0), vB = vec3(0.0);
                for (int vi = ZERO; vi < 2; vi++) {
                    if (vi == 0 && dA <= 0.0) continue;
                    if (vi == 1 && dB <= 0.0) break;
                    vec3 vv = voronoi2(vi == 0 ? rq * 55.0 : rq * 110.0 + 4.1);
                    if (vi == 0) vA = vv; else vB = vv;
                }
                float big = smoothstep(0.55, 0.95, rr);
                vec3 Ld = normalize(vec3(-0.45, 0.8, 0.35));
                float dif = 0.25 + 0.85 * clamp(0.5 + 0.6 * dot(n, Ld), 0.0, 1.0);
                float ao = 0.82 + 0.25 * smoothstep(0.001, 0.005, h0 - 0.006 * (1.0 - rr * rr));
                float cellA = mix(0.8, 1.06, smoothstep(0.05, 0.5, vA.x)) + 0.08 * (1.0 - smoothstep(0.0, 0.08, vA.y - vA.x));
                float cellB = mix(0.88, 1.04, smoothstep(0.05, 0.5, vB.x));
                float tex = mix(1.0, cellA, dA * mix(0.6, 1.0, big)) * mix(1.0, cellB, dB * 0.7);
                float glint = smoothstep(0.88, 1.0, vA.z) * exp(-vA.x * vA.x * 30.0) * dA;
                vec3 top = FoamColor * key * 1.1 * dif * ao * tex;
                top += key * 0.6 * glint;
                top = mix(top, top * pow(bc, vec3(0.3)), 0.3 * smoothstep(0.85, 1.0, rr));
                body += vec4(top, 1.0) * wFoamT;
            }

            // thick clear glass base: a lens that shows the beer above it mirrored / compressed,
            // the back edge of the bottom surface, and a total internal reflection zone below it
            if (wBase > 0.0) {
                float yBB = GBot + E * wb * sb;                  // back edge of the bottom surface
                float upper = smoothstep(yBB - px, yBB + px, p.y);
                float lens = smoothstep(0.0, 0.9, sb);
                // concentric elliptic rings of the base disk (lensing caustics), slowly shimmering
                float qr = length(vec2(xb, (p.y - GBot) / max(E * wb, 1e-4)));
                float c1 = EwValueNoise2D(vec2(qr * 9.0, asin(clamp(xb, -1.0, 1.0)) * 1.2 + rot + t * 0.03));
                float c2 = 0.5 + 0.5 * sin(qr * 22.0 + 4.0 * c1);
                float cs = mix(c1, c2 * c2, 0.3 * smoothstep(0.25, 0.7, qr));
                // upper: looking through solid glass towards the back, beer refracted down by the floor
                float d = clamp((yBeerBot - p.y) / max(yBeerBot - yBB, 1e-3), 0.0, 1.0);
                float clearZ = smoothstep(0.35, 0.9, d);
                vec3 amber = L * pow(bc, vec3(0.9)) * (0.45 + 0.9 * cs) * (1.0 - 0.45 * d) * (0.6 + 0.6 * lens);
                vec3 clearC = L * pow(bc, vec3(0.3)) * (0.1 + 0.14 * lens + 0.15 * cs);
                vec3 up = mix(amber, clearC, clearZ * 0.8);
                up += L * 0.45 * exp(-EwSquare((d - 0.6) / 0.08)) * lens * lens;   // bright lens band
                float aUp = mix(0.88, 0.32, clearZ * (0.5 + 0.5 * lens));
                // lower: the bottom surface seen from inside (mirror of the beer + table)
                float d2 = clamp((yBB - p.y) / max(yBB - yBotFront, 1e-3), 0.0, 1.0);
                vec3 low = L * bc * (0.3 + 0.8 * cs) * (1.0 - 0.6 * d2) + L * 0.04;
                float aLow = 0.88;
                vec3 base = mix(low, up, upper);
                float aB = mix(aLow, aUp, upper);
                // sides: refraction compresses everything into darker, deeper amber
                float side = 1.0 - smoothstep(0.05, 0.45, si);
                base = mix(base, L * pow(bc, vec3(1.8)) * 0.35, side * 0.75);
                aB = mix(aB, 0.92, side);
                // edges of the base: front bottom rim, back bottom edge, the floor under the beer
                base += L * 0.9 * exp(-max(p.y - yBotFront, 0.0) / max(0.0025, px));
                base += L * 0.45 * exp(-abs(p.y - yBB) / max(0.002, px)) * lens;
                base += L * bcSqrt * 0.5 * exp(-max(yBeerBot - p.y, 0.0) / 0.003);
                body += vec4(base * aB, aB) * wBase;
            }

            float vfade = smoothstep(GBot - 0.02, GBot + 0.1, p.y) * (1.0 - smoothstep(GTop - 0.03, GTop + 0.02, p.y));
            float sw = vFrameB.y;
            float phiF = SwHaveImage ? asin(nx) + rot : 0.0;

            // optional frosted / etched logo on the outer glass surface
            float etchReflMask = 0.0;
            float etchInnerOccl = 0.0;
            if (SwHaveImage) {
                float ySurfF = p.y + E * w * s;
                float wallClip = smoothstep(GBot + 0.005, GBot + 0.018, ySurfF)
                               * (1.0 - smoothstep(GTop - 0.014, GTop - 0.004, ySurfF));
                vec2 etchGrad;
                vec4 logoTex = sampleEtch(phiF, ySurfF, max(s, 0.08), px, 0.0, etchGrad);
                float etchCov = logoTex.a * wallClip;
                if (etchCov > 0.0 || dot(etchGrad, etchGrad) > 1e-6) {
                    float etch = clamp(etchCov * EtchStrength, 0.0, 1.0);
                    float graze = pow(1.0 - s, 1.5);
                    float colorAmt;
                    vec3 pigment = logoPigment(logoTex.rgb, colorAmt);
                    // resolution-filtered fine sandblasted grain
                    vec2 gq = vec2(phiF * R0, ySurfF);
                    float gF1 = 1.0 - smoothstep(0.35, 1.0, px * 420.0);
                    float gF2 = 1.0 - smoothstep(0.35, 1.0, px * 850.0);
                    float grainAmp = mix(1.0, 0.72, colorAmt);
                    float frostGrain = 1.0 + grainAmp * (0.16 * gF1 * (EwValueNoise2D(gq * 420.0) - 0.5)
                                                       + 0.09 * gF2 * (EwValueNoise2D(gq * 850.0 + 13.7) - 0.5));
                    // forward-scattered light from the beer / foam / glass behind the etching
                    vec3 bgCol = body.rgb / max(body.a, 1e-4);
                    float bgLum = EwLuminance(bgCol);
                    vec3 transmitted = mix(bgCol, vec3(bgLum) * mix(vec3(1.0), LightColor, 0.65), 0.38);
                    vec3 scatterGlow = mix(transmitted, transmitted * 0.85 + L * 0.24 * body.a, 0.45);
                    // broad studio lighting + diagonal sweep scattered by the frosted micro-facets
                    vec2 roughSD = roughStudioEnv(No, reflect(V, No), drift) * vfade;
                    float roughE = roughSD.x + roughSD.y;
                    float kkE = (nx * 0.8 + p.y * 1.2 - sw) / 0.12;
                    float sweepE = SweepStrength * exp(-kkE * kkE) * vfade;
                    // classic frosted glass appearance (white sandblasted silica)
                    vec3 studioFrost = ReflectionTint * ReflectionStrength
                                     * (roughE * mix(0.34, 0.95, graze) + sweepE * 3.4);
                    vec3 pureFrost = scatterGlow * 0.94 + studioFrost;
                    // colored / tinted frosted etching: curved studio diffuse + subtle backlight + Fresnel frosted sheen
                    vec3 frontIrrad = ReflectionTint * ReflectionStrength * (roughSD.y * 1.45 + sweepE * 2.2)
                                    + key * (0.26 + 0.16 * s);
                    vec3 coloredBase = pigment * (frontIrrad + scatterGlow * 0.22);
                    vec3 coloredSpec = ReflectionTint * ReflectionStrength
                                     * (roughSD.x * mix(0.055, 0.48, graze) + roughSD.y * 0.06 * graze + sweepE * 1.6);
                    vec3 coloredFrost = coloredBase + coloredSpec;
                    // recessed carved bevel: inward groove wall normal (+etchGrad) catches left/top softbox
                    float bevLit = dot(etchGrad, normalize(vec2(-0.75, 0.66)));
                    float bevBoost = 0.5 + 0.5 * clamp(roughE * 0.5, 0.0, 1.6);
                    vec3 bevelTint = mix(vec3(1.0), mix(vec3(0.30), pigment * 1.35, 0.65), colorAmt);
                    float bevelStr = mix(1.0, 0.55, colorAmt);
                    vec3 bevelCol = (ReflectionTint * ReflectionStrength * 0.65 * bevBoost + L * 0.25 * body.a)
                                  * bevelTint * bevelStr * max(bevLit, 0.0) * wallClip * min(EtchStrength, 1.2);
                    float bevelShadow = max(-bevLit, 0.0) * 0.28 * wallClip * min(EtchStrength, 1.2);
                    vec3 frostCol = mix(pureFrost, coloredFrost, colorAmt) * frostGrain;
                    frostCol = frostCol * (1.0 - bevelShadow) + bevelCol;
                    float pureA = clamp(etch * mix(0.48, 0.78, graze), 0.0, 0.88);
                    float colA = clamp(etchCov * clamp(EtchStrength * 1.15, 0.0, 0.98) * mix(0.90, 0.97, graze), 0.0, 0.96);
                    float frostA = mix(pureA, colA, colorAmt);
                    body = over(body, vec4(frostCol * frostA, frostA));
                    // crisp carved catchlight on the recessed edge transition (premultiplied-safe)
                    float edgeLine = clamp(length(etchGrad) * 0.75, 0.0, 1.0);
                    vec3 edgeCatch = bevelCol * edgeLine * 0.55 * (1.0 - frostA);
                    body.rgb += edgeCatch;
                    body.a += (1.0 - body.a) * clamp(EwLuminance(edgeCatch), 0.0, 1.0);
                    etchReflMask = clamp(etchCov * clamp(EtchStrength * 1.15, 0.0, 1.0), 0.0, 1.0);
                    etchInnerOccl = frostA;
                }
            }

            // condensation: mist + droplets on the cold part of the glass
            float coldTop = hl + 0.4 * FoamHeight;
            float cold = (1.0 - smoothstep(coldTop - 0.012, coldTop, p.y))
                       * smoothstep(ybi, ybi + 0.02, p.y) * smoothstep(0.03, 0.25, s) * Condensation;
            if (cold > 0.0) {
                if (!SwHaveImage) phiF = asin(nx) + rot;
                vec2 dq = vec2(phiF * R0, p.y);
                vec3 clearCol = body.rgb;
                if (MistAmount > 0.0) {
                    float mist = 0.55 + 0.45 * EwValueNoise2D(dq * 450.0);
                    body.rgb = mix(body.rgb, body.rgb * 0.88 + key * 0.03 * body.a, cold * mist * MistAmount);
                }
                // two droplet sizes: large drops, small drops in between (early break skips small drops on hits)
                vec3 d = vec3(0.0);
                for (int di = ZERO; di < 2; di++) {
                    d = di == 0 ? dropLayer(dq, 0.024 * DropSize, 0.55 * Condensation, 3.0, px)
                                : dropLayer(dq + 0.37, 0.009 * DropSize, 0.75 * Condensation, 9.0, px);
                    if (d.z > 0.01) break;
                }
                float dc = d.z * smoothstep(0.0, 0.3, cold / max(Condensation, 1e-3));
                if (dc > 0.0) {
                    float r2 = min(dot(d.xy, d.xy), 1.0);
                    vec2 hs = d.xy - vec2(-0.35, 0.45);
                    vec3 dcol = clearCol * (1.45 - 1.25 * smoothstep(0.35, 1.0, r2));
                    dcol += L * 0.35 * body.a * smoothstep(0.5, 1.0, r2) * smoothstep(0.2, 0.8, -d.y);
                    dcol += vec3(1.6) * exp(-dot(hs, hs) * 16.0);
                    body.rgb = mix(body.rgb, dcol, dc);
                    body.a = max(body.a, dc * 0.6);
                    // water droplets wet the frosted pits and create a smooth glossy meniscus
                    etchReflMask *= 1.0 - 0.75 * dc;
                }
            }

            acc = over(acc, body * glassM);

            // silhouette: refraction edge + bright inner line (at least ~1 px wide: no aliasing)
            float ed = min(w - ax, (p.y - yBotFront) * invSlBo);
            float edgeDark = exp(-max(ed, 0.0) / max(0.002, 1.2 * px));
            float edgeLit = exp(-abs(ed - 0.004) / max(0.001, 0.9 * px));
            acc = over(acc, vec4(vec3(0.02), 1.0) * 0.3 * edgeDark * glassM);

            // studio reflections: outer + inner surface of the thick glass (Fresnel)
            // frosted / colored logo breaks up the outer mirror reflection and occludes the inner wall behind it
            if (ReflectionStrength > 0.0) {
                float refl = 0.35 * edgeLit;
                if (vfade > 0.0) {
                    float envSum = 0.0;
                    for (int ri = ZERO; ri < 2; ri++) {
                        vec3 Nr = ri == 0 ? No : Ni;
                        float wsR = ri == 0 ? w * s : wi * si;
                        float Fr = ri == 0 ? EwFresnelSchlick(-dot(V, No), 0.04) : Fi;
                        float envR = mix(studioEnv(reflect(V, Nr), drift), 1.3, smoothstep(0.015, 0.08, px / max(wsR, 1e-5)));
                        float wRefl = ri == 0 ? mix(1.0, 0.10, etchReflMask) : 0.6 * (1.0 - etchInnerOccl);
                        envSum += Fr * envR * wRefl;
                    }
                    refl += envSum * vfade;
                    // light sweep: a soft diagonal highlight crossing the glass every SweepPeriod seconds
                    float kk = (nx * 0.8 + p.y * 1.2 - sw) / 0.1;
                    refl += SweepStrength * exp(-kk * kk) * vfade * mix(1.0, 0.35, etchReflMask);
                }
                refl *= ReflectionStrength * glassM;
                acc.rgb += ReflectionTint * refl;
                acc.a += (1.0 - acc.a) * clamp(refl, 0.0, 1.0);
            }
        }
    }

    // rim of the glass (bounding-box early out)
    if (ReflectionStrength > 0.0 && ax <= wt + 0.003 + 2.0 * px && abs(p.y - GTop) <= E * wt + 0.003 + 2.0 * px) {
        vec2 rq = vec2(p.x / wt, (p.y - GTop) / (E * wt));
        float g = length(rq);
        vec2 gr = vec2(p.x / (wt * wt), (p.y - GTop) / (E * E * wt * wt)) / max(g, 1e-4);
        float rd = abs(g - 1.0) / max(length(gr), 1e-4);
        float rim = 1.0 - smoothstep(0.0012, 0.0012 + 1.5 * px, rd);
        float rimI = rim * mix(0.5, 1.0, step(p.y, GTop)) * ReflectionStrength;
        acc.rgb += vec3(1.0) * rimI * 1.2;
        acc.a += (1.0 - acc.a) * clamp(rimI, 0.0, 1.0);
    }

    if (acc.a <= 0.0) {
        fragColor = vec4(0.0);
        return;
    }
    float A = clamp(acc.a, 0.0, 1.0);
    vec3 col = acc.rgb / max(A, 1e-4);
    col = EwTonemapAces(col * Exposure);
    col = clamp(EwLinearToSrgb(col), 0.0, 1.0);
    fragColor = vec4(col * A, A);
}
