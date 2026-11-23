//  media: image none
// =============================================================================
//  Bulb Frame - a string of light bulbs running chase patterns
// =============================================================================
//  iMouse.x = Inset | Distance of the bulb string from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px

const float StepRate = 4.0; // var: { "min": 0.5, "max": 20.0, "step": 0.5, "name": "Step Rate", "desc": "Pattern steps per second", "unit": "/s" }
const float FadeTime = 0.16; // var: { "min": 0.01, "max": 0.5, "step": 0.01, "name": "Fade Time", "desc": "Bulb on / off fade duration", "unit": "s" }
const float Wave = 10.0; // var: { "min": 0.0, "max": 60.0, "step": 0.5, "name": "Wave", "desc": "Waviness of the cord and bulbs", "unit": "px" }
const float WaveLength = 400.0; // var: { "min": 50.0, "max": 1500.0, "step": 10.0, "name": "Wave Length", "desc": "Length of one wave", "unit": "px" }
const float Sag = 5.0; // var: { "min": 0.0, "max": 30.0, "step": 0.5, "name": "Cord Sag", "desc": "How much the cord hangs between bulbs", "unit": "px" }
const float SwaySpeed = 0.6; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Sway Speed", "desc": "Speed of the slow wave motion", "unit": "x" }
const float PatternTime = 7.0; // var: { "min": 2.0, "max": 30.0, "step": 0.5, "name": "Pattern Time", "desc": "Duration of each running light pattern", "unit": "s" }
const float Spacing = 46.0; // var: { "min": 20.0, "max": 150.0, "step": 1.0, "name": "Spacing", "desc": "Distance between bulbs", "unit": "px" }
const float BulbSize = 8.0; // var: { "min": 3.0, "max": 25.0, "step": 0.5, "name": "Bulb Size", "desc": "Bulb radius", "unit": "px" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 TapestryColor = vec3(0.22, 0.02, 0.03); // var: { "type": "col", "name": "Tapestry Color", "desc": "Dark red base color of the tapestry" }
const vec3 GoldColor = vec3(0.62, 0.44, 0.17); // var: { "type": "col", "name": "Thread Color", "desc": "Color of the gold ornament thread" }
const float PatternSize = 150.0; // var: { "min": 40.0, "max": 500.0, "step": 1.0, "name": "Pattern Size", "desc": "Width of one ornament tile", "unit": "px" }
const float TapestryBrightness = 0.5; // var: { "min": 0.1, "max": 2.0, "step": 0.05, "name": "Tapestry Brightness", "desc": "Brightness of the tapestry", "unit": "x" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner fill (tapestry or image)", "scale": 100, "unit": "%" }
const float BulbLight = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Bulb Light", "desc": "How much the bulbs light up the inner fill / image", "unit": "x" }
const float ShadowAmount = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow", "desc": "Shadow of the wire and bulbs on the inner fill / image", "scale": 100, "unit": "%" }
const vec3 BulbColor = vec3(1.0, 0.72, 0.38); // var: { "type": "col", "name": "Bulb Color", "desc": "Warm color of the bulbs" }
const float Multicolor = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Multicolor", "desc": "Blend to red / green / blue / yellow party bulbs", "scale": 100, "unit": "%" }
const float Intensity = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Intensity", "desc": "Brightness of the bulbs", "unit": "x" }
const float GlowAmount = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Glow", "desc": "Halo strength around lit bulbs", "unit": "x" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = glow alpha blended, 1 = pure additive glow", "scale": 100, "unit": "%" }
const bool ShowWire = true; // var: { "name": "Wire", "desc": "Draw the cable and bulb sockets" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)

// arc length position along the rounded rect (counter clockwise), P = perimeter
float perimS(vec2 p, vec2 hs, vec2 b, float L1, float L2, float A, float rad)
{
    vec2 q = clamp(p, -b, b);
    vec2 dv = p - q;
    if (dv.x != 0.0 && dv.y != 0.0) {            // corner arc
        float ang = atan(dv.y, dv.x);
        if (ang < 0.0) ang += 6.2831853;
        if (ang < 1.5707963) return L2 + rad * ang;
        if (ang < 3.1415927) return L2 + A + L1 + rad * (ang - 1.5707963);
        if (ang < 4.712389) return 2.0 * A + L1 + 2.0 * L2 + rad * (ang - 3.1415927);
        return 3.0 * A + 2.0 * L1 + 2.0 * L2 + rad * (ang - 4.712389);
    }
    vec2 e = hs - abs(p);
    bool sideX = (dv.x != 0.0) || (dv.y == 0.0 && e.x < e.y);
    if (sideX) {
        if (p.x > 0.0) return p.y + b.y;                                   // right, up
        return 2.0 * A + L1 + L2 + (b.y - p.y);                            // left, down
    }
    if (p.y > 0.0) return L2 + A + (b.x - p.x);                            // top, left
    return 3.0 * A + L1 + 2.0 * L2 + (p.x + b.x);                          // bottom, right
}

// on / off state of bulb i (of n) at step k for pattern id
float bulbState(float i, float n, float k, float id)
{
    if (id < 1.0) return step(mod(i + k, 3.0), 0.5);                       // chase 1 of 3
    if (id < 2.0) return step(mod(i - k, 5.0), 1.5);                       // reverse pairs
    if (id < 3.0) return mod(i + floor(k * 0.5), 2.0);                     // alternate swap
    if (id < 4.0) return step(mod(i, 8.0), mod(k, 10.0) - 1.0);            // fill groups of 8
    if (id < 5.0) return step(0.55, EwHash2To1(vec2(i, k)));               // twinkle
    if (id < 6.0) {                                                         // two comets
        float c = mod(k * 1.0, n);
        float d1 = mod(c - i, n), d2 = mod(i - (n - c), n);
        return step(min(d1, d2), 3.5);
    }
    if (id < 7.0) return step(mod(i - k, 10.0), 0.5) + step(mod(i + k, 10.0), 0.5) > 0.0 ? 1.0 : 0.0; // crossing
    return mod(floor(k * 0.5), 2.0);                                        // all blink
}

float bulbLevel(float i, float n, float k, float id, float idp, float blend)
{
    float cur = bulbState(i, n, k, id);
    if (blend >= 1.0) return cur;
    if (blend <= 0.0) return bulbState(i, n, k - 1.0, idp);
    return mix(bulbState(i, n, k - 1.0, idp), cur, blend);
}

// smooth wave of the whole string, periodic over the perimeter P (integer cycles)
float waveOff(float ph, float cyc, float cyc2_1, float sw1, float sw2)
{
    return Wave * (0.75 * sin(ph * cyc + sw1) + 0.25 * sin(ph * cyc2_1 - sw2));
}

vec3 bulbTint(float i)
{
    if (Multicolor <= 0.0) return BulbColor;
    float m = mod(i, 4.0);
    vec3 c = m < 1.0 ? vec3(1.0, 0.15, 0.1) : m < 2.0 ? vec3(0.15, 1.0, 0.25) : m < 3.0 ? vec3(0.2, 0.4, 1.0) : vec3(1.0, 0.8, 0.15);
    return mix(BulbColor, c, Multicolor);
}
// dark red ornate damask tapestry: diamond lattice, medallions, leaves, scrolls, gold thread (p in px)
vec3 tapestry(vec2 p)
{
    float ts = max(PatternSize, 30.0);
    float aa = 1.2 / ts;                                   // AA width in cell units
    vec2 q = p / ts;
    vec2 r = vec2(1.0, 1.4);                               // diamond cell (width, height)
    vec2 a = mod(q, r) - 0.5 * r;
    vec2 b = mod(q - 0.5 * r, r) - 0.5 * r;
    float ma = abs(a.x) / r.x + abs(a.y) / r.y;
    float mb = abs(b.x) / r.x + abs(b.y) / r.y;
    vec2 c = ma < mb ? a : b;                              // position in the nearest diamond
    float ed = (0.5 - min(ma, mb)) * 0.814;                // distance to the lattice
    vec2 u = vec2(abs(c.x), c.y);                          // mirrored (symmetric motif)

    // central medallion: 8 petal rose with a gold ring and center
    float rr = length(c);
    float an = atan(c.y, c.x);
    float med = smoothstep(aa, -aa, rr - (0.11 + 0.035 * cos(8.0 * an)));
    float ring = smoothstep(aa, -aa, abs(rr - 0.055) - 0.01);
    float cen = smoothstep(aa, -aa, rr - 0.022);
    // ring of beads
    float ab = mod(an + 0.3927, 0.7854) - 0.3927;
    float bead = smoothstep(aa, -aa, length(rr * vec2(cos(ab), sin(ab)) - vec2(0.19, 0.0)) - 0.016);
    // pointed leaves above / below, gold vein
    vec2 lp = vec2(u.x, abs(c.y) - 0.42);
    float leafD = length(lp + vec2(0.075, 0.0)) - 0.125;
    float leaf = smoothstep(aa, -aa, leafD);
    float vein = leaf * smoothstep(aa, -aa, lp.x - 0.005);
    // side scrolls (spiral curls) with stems from the medallion
    vec2 sp = u - vec2(0.27, 0.0);
    float sr = length(sp);
    float sa = atan(sp.y, sp.x) / 6.2831853;
    float s = sr / 0.032 - sa;
    float dl = (0.5 - abs(fract(s) - 0.5)) * 0.032;
    float scroll = smoothstep(aa, -aa, dl - 0.007) * smoothstep(0.085 + aa, 0.085 - aa, sr);
    float sd = abs(u.y - 0.035 * sin((u.x - 0.12) * 22.0));
    float stem = smoothstep(aa, -aa, sd - 0.007) * smoothstep(0.1, 0.12, u.x) * smoothstep(0.2, 0.18, u.x);
    // small leaves on the stem
    vec2 slp = vec2(u.x - 0.17, abs(c.y) - 0.06);
    float sleaf = smoothstep(aa, -aa, length(slp * vec2(1.6, 1.0)) - 0.03);
    // lattice: gold border line + inner red line, rosettes at the vertices
    float lat = smoothstep(aa, -aa, ed - 0.009);
    float lat2 = smoothstep(aa, -aa, abs(ed - 0.04) - 0.006);
    float vd = min(length(vec2(u.x - 0.5, c.y)), length(vec2(u.x, abs(c.y) - 0.7)));
    vec2 vv = abs(u.x - 0.5) < abs(abs(c.y) - 0.7) ? vec2(u.x - 0.5, c.y) : vec2(u.x, abs(c.y) - 0.7);
    float va = atan(vv.y, vv.x);
    float ros = smoothstep(aa, -aa, vd - (0.05 + 0.02 * cos(4.0 * va)));
    float rosC = smoothstep(aa, -aa, vd - 0.018);

    float damask = max(max(max(med, leaf), max(scroll, stem)), max(max(sleaf, lat2), ros));
    float gold = max(max(max(ring, cen), max(bead, vein)), max(lat, rosC));

    // woven texture: ground ribs horizontal, pattern ribs vertical (damask sheen)
    float gRib = 0.88 + 0.12 * (0.5 + 0.5 * sin(p.y * 1.4));
    float pRib = 0.88 + 0.12 * (0.5 + 0.5 * sin(p.x * 1.4));
    float thr = 0.9 + 0.2 * EwValueNoise2D(p * vec2(0.6, 0.04));   // thread irregularity
    vec3 ground = TapestryColor * gRib;
    vec3 pat = TapestryColor * vec3(2.1, 1.7, 1.7) * pRib;
    vec3 col = mix(ground, pat, damask) * thr;
    vec3 gc = GoldColor * (0.75 + 0.5 * EwValueNoise2D(p * 0.35)) * pRib;
    col = mix(col, gc, gold * 0.9);
    // hanging folds and age / wear
    float fold = sin(p.x / ts * 2.2 + 1.3 * sin(p.y / ts * 0.3));
    col *= 0.8 + 0.2 * fold + 0.08 * fold * fold * sign(fold);
    float wear = EwFbm2D(p * 0.005, 4);
    col *= 0.65 + 0.6 * wear;
    return clamp(col * TapestryBrightness, 0.0, 1.0);
}
// inner fill (premultiplied): the image (contained in the frame) or the tapestry; the unused one is never run
vec4 innerFill(vec2 p, vec2 hs)
{
    if (SwHaveImage) {
        vec2 isz = max(iChannelResolution[0].xy, vec2(1.0));
        vec2 uv = clamp(p / (2.0 * hs) + 0.5, 0.0, 1.0);
        float lod = log2(max(isz.x / (2.0 * hs.x), 1.0));   // uniform lod: mipmaps for big images
        vec4 im = textureLod(iChannel0, uv, lod);
        if (SwTransparentImage) return vec4(mix(tapestry(p), im.rgb, im.a), 1.0);
        return vec4(im.rgb * im.a, im.a);
    }
    return vec4(tapestry(p), 1.0);
}


void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    vec2 hs = 0.5 * res - inset;
    if (SwHaveImage) {             // contain: the frame takes the image aspect, inside the inset area
        vec2 isz = max(iChannelResolution[0].xy, vec2(1.0));
        float ia = isz.x / isz.y;
        hs = hs.x / hs.y > ia ? vec2(hs.y * ia, hs.y) : vec2(hs.x, hs.x / ia);
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad); // px, 0 on the string

    vec4 bg = vec4(Background * Opacity, Opacity);
    float R = BulbSize;
    float reach = Spacing * 1.2 + R * 5.0 + abs(Wave) + Sag;
    if (d > reach) {
        fragColor = bg;
        return;
    }
    vec2 b = max(hs - rad, vec2(0.0));
    float L1 = 2.0 * b.x, L2 = 2.0 * b.y, A = rad * 1.5707963;
    float P = 2.0 * (L1 + L2) + 4.0 * A;
    float n = max(4.0, floor(P / Spacing + 0.5));
    float sp = P / n;

    float t = mod(iTime, 3600.0);
    float sw1 = t * SwaySpeed;
    float sw2 = sw1 * 1.7;
    float cyc = max(1.0, floor(P / max(WaveLength, 20.0) + 0.5));
    float cyc2_1 = cyc * 2.0 + 1.0;

    bool inside = (d < -reach);
    float s = 0.0, ic = 0.0, co = 0.0;
    if (!inside) {
        s = perimS(p, hs, b, L1, L2, A, rad);
        ic = floor(s / sp + 0.5);
        float x = fract(s / sp);
        float phCord = 6.2831853 * s / P;
        co = waveOff(phCord, cyc, cyc2_1, sw1, sw2) - Sag * 4.0 * x * (1.0 - x);
    }

    float aa = 1.0;
    float fm = inside ? InnerOpacity : (smoothstep(aa, -aa, d - co) * InnerOpacity);
    vec4 fill = fm > 0.0 ? innerFill(p, hs) * fm : vec4(0.0);
    if (inside) {
        fragColor = fill + bg * (1.0 - fill.a);
        return;
    }

    vec3 emit = vec3(0.0);
    vec3 light = vec3(0.0);
    vec4 glass = vec4(0.0);        // premultiplied alpha layer (wire, sockets, dark glass)
    float shad = 0.0;
    float so = R * 0.8;            // inward shadow offset
    if (ShowWire) {
        float wa = smoothstep(1.2 + aa, 1.2 - aa, abs(d - co)) * 0.9;
        glass = vec4(vec3(0.03, 0.035, 0.03) * wa, wa);
        shad = 0.7 * smoothstep(4.5, 0.5, abs(d - co + so * 0.6));
    }

    float fk = t * StepRate;
    float k = floor(fk);
    float stepsPer = max(2.0, floor(PatternTime * StepRate));
    float id  = floor(EwHash2To1(vec2(floor(k / stepsPer), 3.7)) * 8.0);
    float idp = floor(EwHash2To1(vec2(floor((k - 1.0) / stepsPer), 3.7)) * 8.0);
    float f = clamp(fract(fk) / (StepRate * FadeTime), 0.0, 1.0);
    float blend = f * f * (3.0 - 2.0 * f);

    float invReach06 = 1.0 / (reach * 0.6);
    float invSp045 = 1.0 / (sp * 0.45);
    float invRsq018 = 1.0 / (R * R * 0.18);
    float invRsq4 = 1.0 / (R * R * 4.0);
    float invRsq01 = 1.0 / (R * R * 0.1);
    float R12 = R * 1.2;
    float tau_n = 6.2831853 / n;

    for (int j = -1; j <= 1; j++) {
        float i = ic + float(j);
        float u = s - i * sp;
        float ii = mod(i, n);
        float lv = bulbLevel(ii, n, k, id, idp, blend);
        vec3 tint = bulbTint(ii);
        float db = d - waveOff(i * tau_n, cyc, cyc2_1, sw1, sw2);
        vec2 bp = vec2(u, db + R * 0.35);
        float r = length(bp * vec2(1.0, 0.85));
        float r2 = r * r;
        // socket (alpha)
        if (ShowWire && j == 0) {
            float sk = EwSdBox2D(vec2(u, db - R * 0.55), vec2(R * 0.5, R * 0.45)) - 1.0;
            float ska = smoothstep(aa, -aa, sk) * 0.95;
            glass = vec4(vec3(0.06, 0.06, 0.055) * ska, ska) + glass * (1.0 - ska);
        }
        // glass body (alpha): dim tinted when off
        float gd = r - R;
        float ga = smoothstep(aa, -aa, gd);
        if (ga > 0.0) {
            vec3 gc = tint * (0.12 + 0.1 * smoothstep(-R, R * 0.3, -length(bp + vec2(R * 0.35, R * 0.35))));
            float a = ga * 0.85;
            glass = vec4(gc * a, a) + glass * (1.0 - a);
        }
        // bulb light and shadow on the inner fill / image (shadow cast inward, frame floats above)
        float rs = length((bp + vec2(0.0, so)) * vec2(1.0, 0.85));
        shad = max(shad, smoothstep(R * 1.9, R * 0.5, rs));

        if (lv > 0.0) {
            float lvE = lv * lv * lv;   // perceptual: tone map compresses, so dim the ramp
            float body = ga * (0.9 + 1.4 * exp(-r2 * invRsq018));
            float halo = R12 / (r + R12) * exp(-r * invSp045);
            float bloom = exp(-r2 * invRsq4);
            emit += lvE * (tint * body * 1.6 + vec3(1.0, 0.95, 0.85) * 1.4 * exp(-r2 * invRsq01)
                           + tint * GlowAmount * (halo * 0.55 + bloom * 0.5));
            float lw = max(1.0 - r * invReach06, 0.0);
            light += lvE * tint * lw * lw;
        }
    }
    // inner fill: shadows + bulb light, then over the background
    if (fill.a > 0.0) {
        fill.rgb *= 1.0 - ShadowAmount * shad;
        fill.rgb = fill.rgb * (1.0 + BulbLight * Intensity * light * 1.6) + BulbLight * Intensity * light * 0.06 * fill.a;
        fill.rgb = min(fill.rgb, vec3(fill.a));
    }
    bg = fill + bg * (1.0 - fill.a);
    emit *= Intensity;
    emit = 1.0 - exp(-emit * 1.3);       // tone map
    vec2 eb2 = min(fragCoord, res - fragCoord);
    float edge = smoothstep(0.0, min(inset, 24.0) * 0.6 + 2.0, min(eb2.x, eb2.y));
    emit *= edge;

    float dz = EwDither(fragCoord, iTime, 2.0);
    emit = max(emit + dz, 0.0);
    // layer 1: glass / wire over the background
    vec4 base = glass + bg * (1.0 - glass.a);
    // layer 2: light, mixed alpha-blend / additive (premultiplied)
    float ea = clamp(max(emit.r, max(emit.g, emit.b)), 0.0, 1.0) * (1.0 - Additive);
    vec3 rgb = emit + base.rgb * (1.0 - ea);
    float a = ea + base.a * (1.0 - ea);
    fragColor = vec4(clamp(rgb, 0.0, 1.0), clamp(a, 0.0, 1.0));
}
