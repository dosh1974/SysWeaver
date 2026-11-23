//  media: image none
// =============================================================================
//  Pharaoh Frame
// =============================================================================
//  An ancient, cracked Egyptian sandstone frame along the screen edges: carved
//  and painted hieroglyphs, glittering gold moldings and inlays, pulsing magic
//  gems, and dust, sand and debris crumbling off the top. Transparent overlay
//  (premultiplied alpha), pixel based geometry, empty middle skipped early.
//
//  iMouse.x = Inset | Distance of the frame center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px
//  iMouse.z = Gem Magic | Glow strength of the magic gems | 0.5 | 0 .. 2 | {0}x
//  iMouse.w = Dust | Amount of dust, sand and debris falling from the top | 0.5 | 0 .. 2 | {0}x

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Falling dust and gem pulse animation speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.0); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const vec3 InnerColor = vec3(0.6625, 0.4922, 0.1385); // var: { "type": "col", "name": "Inner Color", "desc": "Color modifier of the inner sandstone blocks" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the inner sandstone blocks", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

// --- Frame look ---
const float BandWidth = 0.92; // var: { "min": 0.5, "max": 1.0, "step": 0.01, "name": "Band Width", "desc": "Frame thickness relative to the inset", "scale": 100, "unit": "%" }
const float Brightness = 1.0; // var: { "min": 0.5, "max": 2.5, "step": 0.05, "name": "Brightness", "desc": "Exposure of the frame", "unit": "x" }
const float Age = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Age", "desc": "Cracks, chipped edges, grime and uneven stone", "scale": 100, "unit": "%" }
const float Wear = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Wear", "desc": "Weathering: how much paint has flaked off", "scale": 100, "unit": "%" }
const float PaintAmount = 0.85; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Paint", "desc": "Strength of the colored paint on stone and glyphs", "scale": 100, "unit": "%" }

// --- Colors ---
const vec3 SandColor = vec3(0.80, 0.60, 0.38); // var: { "type": "col", "name": "Sandstone", "desc": "Color of the carved sandstone" }
const vec3 GoldColor = vec3(1.0, 0.70, 0.22); // var: { "type": "col", "name": "Gold", "desc": "Color of the gold moldings and gem settings" }
const vec3 PaintBlue = vec3(0.12, 0.27, 0.62); // var: { "type": "col", "name": "Paint Blue", "desc": "Lapis blue of the paintings" }
const vec3 PaintRed = vec3(0.72, 0.22, 0.12); // var: { "type": "col", "name": "Paint Red", "desc": "Red ochre of the paintings" }
const vec3 PaintGreen = vec3(0.10, 0.52, 0.45); // var: { "type": "col", "name": "Paint Green", "desc": "Malachite green of the paintings" }
const vec3 PaintYellow = vec3(0.92, 0.70, 0.25); // var: { "type": "col", "name": "Paint Yellow", "desc": "Yellow ochre of the chevrons and cartouche inlays" }
const vec3 PaintCream = vec3(0.93, 0.86, 0.68); // var: { "type": "col", "name": "Paint Cream", "desc": "Light ground color of the painted bands" }

// --- Gold ---
const float GoldShare = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Gilded Glyphs", "desc": "Share of hieroglyphs inlaid with gold", "scale": 100, "unit": "%" }
const float GoldGlitter = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Glitter", "desc": "Twinkling sparkles on the gold", "unit": "x" }
const float GlitterDensity = 0.2; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Glitter Density", "desc": "How many tiny sparks twinkle on the gold", "scale": 100, "unit": "%" }
const float GoldShine = 2.2; // var: { "min": 0.0, "max": 5.0, "step": 0.1, "name": "Gold Shine", "desc": "Strength of the specular highlights on the gold", "unit": "x" }

// --- Gems ---
const int GemSpacing = 4; // var: { "min": 1, "max": 12, "step": 1, "name": "Gem Spacing", "desc": "Hieroglyphs between two gems" }
const float GemPulseSpeed = 1.8; // var: { "min": 0.0, "max": 6.0, "step": 0.1, "name": "Gem Pulse", "desc": "How fast the gems pulse (times Speed)", "unit": "rad/s" }
const float HaloStrength = 0.9; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Halo Strength", "desc": "Brightness of the gem light spilling onto the stone", "unit": "x" }
const float HaloFalloff = 2.6; // var: { "min": 1.0, "max": 8.0, "step": 0.1, "name": "Halo Falloff", "desc": "Higher keeps the gem light tight around the gem, lower spreads it wide" }

// --- Dust ---
const float FallHeight = 0.45; // var: { "min": 0.1, "max": 0.9, "step": 0.01, "name": "Fall Height", "desc": "How far debris and sand fall (share of the screen height, at most 560 px)", "scale": 100, "unit": "%" }
const float HazeAmount = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Haze", "desc": "Dust haze hanging below the top edge", "scale": 100, "unit": "%" }
const float DebrisSize = 2.6; // var: { "min": 0.0, "max": 8.0, "step": 0.1, "name": "Debris Size", "desc": "Size range of the falling stone chunks", "unit": "px" }
const float TrickleAmount = 0.45; // var: { "min": 0.0, "max": 0.9, "step": 0.01, "name": "Sand Trickles", "desc": "How many thin streams of sand run down", "scale": 100, "unit": "%" }

// --- Lighting & aging details ---
const vec3 LightDir = vec3(-0.45, 0.6, 0.7); // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Light Direction", "desc": "Direction of the light (x right, y up, z toward the viewer)" }
const float Ambient = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Ambient", "desc": "Light in the shadows; higher flattens the carving" }
const vec3 CrackColor = vec3(0.30, 0.20, 0.13); // var: { "type": "col", "name": "Crack Color", "desc": "Color of the cracks in the stone" }
const float CrackSize = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Crack Size", "desc": "Width and prominence of the cracks", "unit": "x" }
const float CrackScale = 0.8; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Crack Scale", "desc": "Spacing of the crack network relative to the band", "unit": "x" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)
#define GemMagic mix(0.0, 2.0, iMouse.z)
#define ZERO min(iFrame, 0)
#define DustAmount mix(0.0, 2.0, iMouse.w)

// premultiplied "over" compositing
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }
// approximate ellipse distance (good enough near the outline)
float sdEll(vec2 p, vec2 r) { return (length(p / r) - 1.0) * min(r.x, r.y); }
// non-unrolled 2D FBM (identical to EwFbm2D, prevents loop unrolling bloat)
float fbm2(vec2 p, int oct) {
    const mat2 m = mat2(1.6, 1.2, -1.2, 1.6);
    float s = 0.0, a = 0.5;
    for (int i = ZERO; i < oct; i++) {
        s += a * EwValueNoise2D(p);
        p = m * p;
        a *= 0.5;
    }
    return s;
}
// non-unrolled 2D Voronoi F2-F1 edge distance (identical to EwVoronoi2D(p).y - .x)
float voronoiEdge(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    float d1 = 8.0, d2 = 8.0;
    for (int y = -1 + ZERO; y <= 1; y++) {
        for (int x = -1 + ZERO; x <= 1; x++) {
            vec2 nb = vec2(float(x), float(y));
            vec2 r = nb + EwHash2To2(cell + nb) - f;
            float d = dot(r, r);
            if (d < d1) { d2 = d1; d1 = d; }
            else if (d < d2) { d2 = d; }
        }
    }
    return sqrt(d2) - sqrt(d1);
}
// twinkling glitter on gold: fine sparks and occasional star flares (pixel space)
float glitter(vec2 fc, float tm) {
    vec2 c = floor(fc / 3.0);
    vec3 h = EwHash2To3(c);
    float g = 0.0;
    if (h.x <= GlitterDensity) {
        float s0 = max(sin(tm * (1.5 + 3.0 * h.y) + h.z * 6.283), 0.0);
        float s2 = s0 * s0; float s4 = s2 * s2; float s8 = s4 * s4; float s16 = s8 * s8;
        g = s16 * s8 * s4 * s2 * 1.5;
    }
    vec2 c2 = floor(fc / 14.0);
    vec4 h2 = EwHash2To4(c2 + 31.0);
    if (h2.x < 0.25) {
        float t0 = max(sin(tm * (1.0 + 2.0 * h2.w) + h2.y * 40.0), 0.0);
        if (t0 > 0.5) {
            float t2 = t0 * t0; float t4 = t2 * t2; float t8 = t4 * t4;
            float tw = t8 * t8;
            vec2 lp = fc - (c2 + 0.25 + 0.5 * h2.yz) * 14.0;
            float star = 2.0 * exp(-dot(lp, lp) * 0.35)
                       + 0.8 * (exp(-abs(lp.x) * 1.2 - lp.y * lp.y * 0.9) + exp(-abs(lp.y) * 1.2 - lp.x * lp.x * 0.9));
            g += star * tw * 3.0;
        }
    }
    return g;
}

// arc length position along the frame center line (clockwise from the top left)
float perimPos(vec2 p, vec2 b, float rad, out float L) {
    float R = rad * 1.5707963;
    L = 4.0 * b.x + 4.0 * b.y + 4.0 * R;
    float dx = abs(p.x) - b.x, dy = abs(p.y) - b.y;
    if (dx > 0.0 && dy > 0.0) {
        float th = atan(dy, dx);
        if (p.x > 0.0 && p.y > 0.0) return 2.0 * b.x + rad * (1.5707963 - th);
        if (p.x > 0.0) return 2.0 * b.x + 2.0 * b.y + R + rad * th;
        if (p.y < 0.0) return 4.0 * b.x + 2.0 * b.y + 2.0 * R + rad * (1.5707963 - th);
        return 4.0 * b.x + 4.0 * b.y + 3.0 * R + rad * th;
    }
    if (dy > dx) {
        if (p.y > 0.0) return p.x + b.x;
        return 2.0 * b.x + 2.0 * b.y + 2.0 * R + (b.x - p.x);
    }
    if (p.x > 0.0) return 2.0 * b.x + R + (b.y - p.y);
    return 4.0 * b.x + 2.0 * b.y + 3.0 * R + (p.y + b.y);
}

// cross section height (units of the half band width), t = -1 inner .. 1 outer
float profileH(float t, float ws) {
    float uo = clamp((t - 0.9) / 0.1, -1.0, 1.0);
    float ui = clamp((t + 0.89) / 0.11, -1.0, 1.0);
    float h = 0.0;
    h = mix(h, 0.08, smoothstep(0.52 - ws, 0.52 + ws, t));
    h = mix(h, 0.08 + 0.16 * sqrt(1.0 - uo * uo), smoothstep(0.80 - ws, 0.80 + ws, t));
    h = mix(h, 0.08, smoothstep(-0.48 + ws, -0.48 - ws, t));
    h = mix(h, 0.08 + 0.14 * sqrt(1.0 - ui * ui), smoothstep(-0.78 + ws, -0.78 - ws, t));
    return h;
}

// hieroglyphs, cell height 1, negative = carved
// axis-aligned segment distances (compact & fast)
float sdHSeg(vec2 q, float a, float y) { return length(vec2(max(abs(q.x) - a, 0.0), q.y - y)); }
float sdVSeg(vec2 q, float y0, float y1) { return length(vec2(q.x, q.y - clamp(q.y, y0, y1))); }

// hieroglyphs, cell height 1, negative = carved
float glyph(vec2 q, int id) {
    float w = 0.03;
    float g = 1e3;
    if (id == 0) {            // ankh
        g = abs(sdEll(q - vec2(0.0, 0.2), vec2(0.09, 0.14))) - w;
        g = min(g, sdHSeg(q, 0.19, 0.03) - w);
        g = min(g, sdVSeg(q, -0.37, 0.03) - w);
    } else if (id == 1) {     // eye of Horus
        vec2 e = q - vec2(0.0, 0.1);
        float al = max(length(e - vec2(0.0, -0.2)) - 0.28, length(e - vec2(0.0, 0.2)) - 0.28);
        g = abs(al) - w * 0.8;
        g = min(g, length(e) - 0.06);
        g = min(g, EwSdSegment2D(q, vec2(-0.24, 0.26), vec2(0.22, 0.28)) - w * 0.8);
        g = min(g, EwSdSegment2D(q, vec2(-0.03, 0.03), vec2(-0.07, -0.3)) - w * 0.8);
        g = min(g, EwSdSegment2D(q, vec2(0.1, 0.06), vec2(0.2, -0.17)) - w * 0.7);
        vec2 c = q - vec2(0.13, -0.17);
        g = min(g, max(abs(length(c) - 0.07) - w * 0.7, c.y));
    } else if (id == 2) {     // water
        float yv = q.y - 0.07 * abs(fract(q.x * 7.0) - 0.5);
        float y0 = clamp(floor(yv * 5.0 + 0.5), -1.0, 1.0) * 0.2;
        g = max(abs(yv - y0) * 0.8 - w * 0.7, abs(q.x) - 0.28);
    } else if (id == 3) {     // sun over the horizon
        float dl = length(q - vec2(0.0, 0.08));
        g = min(abs(dl - 0.17) - w, dl - 0.05);
        g = min(g, sdHSeg(q, 0.28, -0.25) - w);
        g = min(g, sdHSeg(q, 0.2, -0.34) - w);
    } else if (id == 4) {     // feather of Maat
        g = sdVSeg(q, -0.36, 0.34) - w * 0.7;
        g = min(g, sdEll(q - vec2(0.07, 0.14), vec2(0.07, 0.2)));
        g = min(g, sdHSeg(q, 0.12, -0.36) - w);
    } else if (id == 5) {     // ibis
        vec2 b = mat2(0.95, -0.3, 0.3, 0.95) * (q - vec2(-0.02, -0.02));
        g = sdEll(b, vec2(0.19, 0.1));
        g = min(g, length(q - vec2(0.15, 0.17)) - 0.055);
        g = min(g, EwSdSegment2D(q, vec2(0.18, 0.15), vec2(0.28, -0.06)) - w * 0.6);
        g = min(g, EwSdSegment2D(q, vec2(0.11, 0.04), vec2(0.15, 0.15)) - w);
        g = min(g, EwSdSegment2D(q, vec2(-0.02, -0.1), vec2(-0.04, -0.37)) - w * 0.6);
        g = min(g, EwSdSegment2D(q, vec2(0.05, -0.1), vec2(0.07, -0.37)) - w * 0.6);
        g = min(g, EwSdSegment2D(q, vec2(-0.18, -0.06), vec2(-0.3, -0.16)) - w * 0.8);
    } else if (id == 6) {     // djed pillar
        g = EwSdRoundedBox2D(q - vec2(0.0, -0.03), vec2(0.06, 0.34), 0.02);
        float y = 0.1 + 0.075 * clamp(floor((q.y - 0.1) / 0.075 + 0.5), 0.0, 3.0);
        g = min(g, sdHSeg(q, 0.16, y) - w * 0.7);
        g = min(g, sdHSeg(q, 0.14, -0.37) - w);
    } else {                  // scarab with sun
        vec2 a = vec2(abs(q.x), q.y);
        g = sdEll(q - vec2(0.0, -0.08), vec2(0.14, 0.19));
        g = min(g, sdEll(q - vec2(0.0, 0.15), vec2(0.09, 0.05)));
        g = min(g, EwSdSegment2D(a, vec2(0.1, 0.02), vec2(0.23, 0.12)) - w * 0.6);
        g = min(g, EwSdSegment2D(a, vec2(0.12, -0.1), vec2(0.25, -0.12)) - w * 0.6);
        g = min(g, EwSdSegment2D(a, vec2(0.1, -0.2), vec2(0.21, -0.33)) - w * 0.6);
        g = min(g, abs(length(q - vec2(0.0, 0.3)) - 0.07) - w * 0.7);
    }
    return g;
}

float cellSdf(vec2 q, int id, bool gem) {
    if (gem) return abs(EwSdRoundedBox2D(q, vec2(0.34, 0.44), 0.17)) - 0.018;
    return glyph(q, id);
}

vec3 gemPalette(float h) {
    if (h < 0.25) return vec3(0.05, 0.85, 0.75);  // turquoise
    if (h < 0.5) return vec3(0.15, 0.3, 1.0);     // lapis
    if (h < 0.75) return vec3(1.0, 0.22, 0.06);   // carnelian
    return vec3(0.25, 1.0, 0.3);                  // emerald
}

// falling dust, sand trickles and debris below the top band (premultiplied, linear)
vec4 dustLayer(vec2 p, float yy, float fallH, float tm, float amt) {
    vec3 sandL = EwSquare(SandColor);
    // dust haze
    vec4 Lr = vec4(0.0);
    float hazeEnv = exp(-max(yy, 0.0) / 70.0) * HazeAmount * min(amt, 1.5) * smoothstep(-6.0, 8.0, yy);
    if (hazeEnv > 0.001) {
        float hz = fbm2(vec2(p.x / 160.0 + tm * 0.04, yy / 90.0 - tm * 0.12), 3);
        float hazeA = smoothstep(0.3, 0.75, hz) * hazeEnv;
        Lr = vec4(sandL * 0.9 * hazeA, hazeA);
    }
    // sand trickles
    float W3 = 110.0;
    float c3 = floor(p.x / W3);
    vec3 h3 = EwHash2To3(vec2(c3, 17.0));
    if (h3.y < min(TrickleAmount * amt, 0.9)) {
        float sx0 = (c3 + 0.2 + 0.6 * h3.x) * W3;
        if (abs(p.x - sx0) < 3.2) {
            float sx = sx0 + 1.5 * sin(yy * 0.03 + tm * 2.0);
            float dx = abs(p.x - sx);
            if (dx < 1.6) {
                float on = smoothstep(0.3, 0.8, sin(tm * (0.25 + 0.2 * h3.z) + h3.x * 6.283));
                if (on > 0.0) {
                    float n = EwValueNoise2D(vec2(p.x * 0.4, yy * 0.08 - tm * 20.0));
                    float line = smoothstep(1.6, 0.3, dx) * smoothstep(0.3, 0.65, n) * exp(-max(yy, 0.0) / (fallH * 0.35)) * on;
                    float la = 0.75 * line;
                    Lr = over(vec4(sandL * 1.1 * la, la), Lr);
                }
            }
        }
    }
    // debris chunks
    float W = 28.0;
    float cx = floor(p.x / W);
    float maxDebrisBound = (1.0 + DebrisSize) * 1.3 + 1.0;
    for (int k = ZERO; k < 2; k++) {
        vec4 h = EwHash2To4(vec2(cx, float(k) * 7.31 + 3.0));
        if (h.x > 0.3 * amt) continue;
        float per = 2.2 + 3.0 * h.y;
        float ph = tm / per + h.z;
        float cyc = floor(ph);
        float tt = fract(ph) * per;
        vec4 hc = EwHash2To4(vec2(cx * 1.7 + cyc, float(k) + 11.0));
        float fallT = 1.4 + 0.5 * hc.w;
        float g = 2.0 * fallH / (fallT * fallT);
        float py = 0.5 * g * tt * tt;
        if (py > fallH || abs(yy - py) > maxDebrisBound) continue;
        float r = 1.0 + DebrisSize * hc.y * hc.y;
        float px = (cx + 0.5 + (hc.x - 0.5) * 0.4) * W + (hc.z - 0.5) * 4.0 * tt;
        vec2 lq = vec2(p.x - px, yy - py);
        float bnd = r * 1.3 + 1.0;
        if (abs(lq.x) > bnd || abs(lq.y) > bnd) continue;
        float ang = tt * (3.0 + 6.0 * hc.z) + hc.w * 6.283;
        vec2 rq = EwRotateVec2(lq, -ang);                // tumbling chunk
        float sd = max(abs(rq.x) - r, abs(rq.y) - r * 0.7);
        float m = smoothstep(0.7, -0.7, sd) * (1.0 - smoothstep(0.65 * fallH, fallH, py));
        float shade = 0.6 + 0.35 * clamp(-lq.y / max(r, 0.5), -1.0, 1.0);
        Lr = over(vec4(sandL * shade * m, m), Lr);
    }
    // fine dust motes
    float W2 = 16.0;
    float cx2 = floor(p.x / W2);
    for (int k = ZERO; k < 2; k++) {
        vec4 h = EwHash2To4(vec2(cx2, float(k) * 5.1 + 40.0));
        if (h.x > 0.4 * amt) continue;
        float per = 4.0 + 4.0 * h.y;
        float ph = tm / per + h.z;
        float cyc = floor(ph);
        float tt = fract(ph) * per;
        vec4 hc = EwHash2To4(vec2(cx2 * 1.3 + cyc, float(k) + 50.0));
        float py = (25.0 + 45.0 * hc.y) * tt + 4.0 * tt * tt - 2.0;
        if (py < -2.0 || py > fallH || abs(yy - py) > 2.6) continue;
        float px = (cx2 + 0.5 + (hc.x - 0.5) * 0.2) * W2 + 2.5 * sin(tt * 1.5 + hc.z * 6.283);
        float dx = p.x - px;
        if (abs(dx) > 2.6) continue;
        float r = 0.6 + 0.9 * hc.w;
        float dd = length(vec2(dx, yy - py));
        float m = 0.6 * smoothstep(r + 1.0, r - 0.3, dd) * smoothstep(fallH, fallH * 0.4, py) * smoothstep(-2.0, 6.0, py);
        Lr = over(vec4(sandL * 1.3 * m, m), Lr);
    }
    return Lr;
}

vec2 getBlockPoint(vec2 c) {
    const float BLK_W = 195.0;
    const float BLK_H = 84.0;
    float rowStagger = fract(c.y * 0.5) * BLK_W;
    vec4 h = EwHash2To4(c * 3.17 + vec2(1.23, 7.89));
    
    // Varying block lengths (~120px to 270px) and slight course height variations
    float jx = (h.x - 0.5) * 0.60 * BLK_W;
    float jy = (h.y - 0.5) * 0.22 * BLK_H;
    
    // Gentle ancient wall sag
    float sag = sin(c.x * 0.45 + c.y * 1.3) * 5.0;
    
    return vec2(c.x * BLK_W + rowStagger + jx, c.y * BLK_H + jy + sag);
}
// Contact and directional cast shadows from the surrounding frame onto the interior
float innerFrameShadow(vec2 p, float d, float hw, vec2 hs, float rad) {
    float sScale = clamp(hw / 40.0, 0.4, 2.0);
    float distFromFrame = max(-(d + hw), 0.0);
    vec2 lightShift = LightDir.xy / max(abs(LightDir.z), 0.25);
    float sl = length(lightShift);
    float lLen = min(sl, 2.0);
    if (distFromFrame >= (26.0 * lLen + 24.0) * sScale) return 1.0;
    float frameAo = mix(0.62, 1.0, smoothstep(0.0, 22.0 * sScale, distFromFrame));
    vec2 lDir = sl > 2.0 ? lightShift * (2.0 / sl) : lightShift;
    float dS1 = EwSdRoundedBox2D(p + lDir * (11.0 * sScale), hs, rad);
    float dS2 = EwSdRoundedBox2D(p + lDir * (26.0 * sScale), hs, rad);
    float sh1 = smoothstep(-hw - 10.0 * sScale, -hw + 2.0 * sScale, dS1);
    float sh2 = smoothstep(-hw - 24.0 * sScale, -hw + 4.0 * sScale, dS2);
    float castW = smoothstep(0.0, 0.25, sl);
    float castShadow = 1.0 - castW * (0.40 * sh1 + 0.28 * sh2);
    return frameAo * castShadow;
}
// Darker ancient monumental sandstone blocks inside the frame
vec3 innerSandstoneTiles(vec2 p, vec2 fc, float d, float hw, vec2 hs, float rad) {
    const float BLK_W = 195.0;
    const float BLK_H = 84.0;
    // Subtle organic joint wobble (quarry cutting and millennia of weathering)
    vec2 pWobble = p + (vec2(
        EwValueNoise2D(p * 0.04 + 11.0) - 0.5,
        EwValueNoise2D(p * 0.04 + 67.0) - 0.5
    ) * 4.5);
    
    // Find nearest stone block center (row-staggered 3x3 search, non-unrolled)
    float rowY = floor(pWobble.y / BLK_H + 0.5);
    vec2 bestC = vec2(0.0);
    vec2 bestP = vec2(0.0);
    vec2 d1 = vec2(0.0);
    float minD2 = 1e20;
    for (int j = -1 + ZERO; j <= 1; j++) {
        float cy = rowY + float(j);
        float stagger = fract(cy * 0.5) * BLK_W;
        float rowX = floor((pWobble.x - stagger) / BLK_W + 0.5);
        for (int i = -1 + ZERO; i <= 1; i++) {
            vec2 c = vec2(rowX + float(i), cy);
            vec2 pt = getBlockPoint(c);
            vec2 diff = pt - pWobble;
            float d2 = dot(diff, diff);
            if (d2 < minD2) {
                minD2 = d2;
                bestC = c;
                bestP = pt;
                d1 = diff;
            }
        }
    }

    // Exact Euclidean distance to the nearest stone joint (Pass 2)
    float dSeam = 999.0;
    vec2 seamNormal = vec2(0.0, 1.0);
    for (int j = -1 + ZERO; j <= 1; j++) {
        float cy = rowY + float(j);
        float stagger = fract(cy * 0.5) * BLK_W;
        float rowX = floor((pWobble.x - stagger) / BLK_W + 0.5);
        for (int i = -1 + ZERO; i <= 1; i++) {
            vec2 c = vec2(rowX + float(i), cy);
            if (c == bestC) continue;
            vec2 dk = getBlockPoint(c) - pWobble;
            vec2 delta = dk - d1;
            float len2 = dot(delta, delta);
            if (len2 > 1e-8) {
                float invLen = inversesqrt(len2);
                float dist = dot(delta, dk + d1) * 0.5 * invLen;
                if (dist < dSeam) {
                    dSeam = dist;
                    seamNormal = -delta * invLen; // points away from joint towards block center
                }
            }
        }
    }
    // Block random properties from cell ID
    vec3 bHash = EwHash2To3(bestC * 2.371 + vec2(1.7, 4.3));
    vec3 bHash2 = EwHash2To3(bestC * 5.193 + vec2(9.1, 3.4));
    // Weathering: joint erosion and edge crumbling
    float edgeNoise1 = EwValueNoise2D(p * 0.06 + 12.0);
    float edgeNoise2 = dSeam < 15.0 ? EwValueNoise2D(p * 0.18 + 47.0) : 0.5;
    float jointErosion = edgeNoise1 * 1.5 + (edgeNoise2 - 0.5) * 0.8;
    
    // Spalls at broken corners and chipped edges
    float cornerChip = smoothstep(10.0, 1.0, dSeam) * step(0.50, bHash.z) * (2.0 + 3.0 * edgeNoise1);
    float edgeGouge = dSeam < 8.0 ? smoothstep(0.78, 0.94, EwValueNoise2D(p * 0.08 + 81.0)) * 2.5 * smoothstep(8.0, 0.0, dSeam) : 0.0;
    dSeam = max(dSeam - jointErosion - cornerChip - edgeGouge, 0.0);
    
    // Worn, rounded draft margins and rustic pillowed face (bossage)
    float marginWidth = 8.0 + 4.0 * bHash2.y;
    float marginRatio = clamp(dSeam / marginWidth, 0.0, 1.0);
    // Soft bevel lip
    float edgeRound = smoothstep(0.0, 1.0, marginRatio);
    vec2 marginGrad = seamNormal * (1.0 - edgeRound) * 0.45;
    
    // Pillowed rustic block face (bossage)
    vec2 toCenter = bestP - p;
    vec2 pillowGrad = normalize(toCenter + 1e-4) * smoothstep(marginWidth, 70.0, dSeam) * 0.12;
    
    // Block settlement tilt & proudness (differential settlement)
    vec2 blockTilt = (bHash.xy - 0.5) * 0.15;
    float proudness = (bHash.x - 0.5) * 0.35;
    
    // Surface texture: sandstone grain, ancient bronze adze/pick dressing, granular weathering pits
    vec3 sDeriv = EwValueNoise2DDerivatives(p * 0.024 + 7.3);
    vec3 sFine = EwValueNoise2DDerivatives(p * 0.090 + 25.1);
    vec2 bump = sDeriv.yz * 0.18 + sFine.yz * 0.08;
    
    // Chisel / adze dressing grooves across ancient dressed blocks
    float chiselAngle = (bHash2.z - 0.5) * 0.85;
    vec2 chiselDir = vec2(cos(chiselAngle), sin(chiselAngle));
    float chisel = sin(dot(p, chiselDir) * 0.42 + sDeriv.x * 2.8) * 0.035 * (1.0 - edgeNoise1 * 0.5);
    
    // Desert wind erosion cavities (tafoni honeycombing and wind scouring)
    float tafoni = smoothstep(0.68, 0.90, EwValueNoise2D(p * 0.032 + 92.0)) * 0.24;
    float fluting = sin(p.y * 0.055 + sDeriv.x * 2.2) * 0.03;
    
    vec3 N = normalize(vec3(marginGrad * 0.90 + pillowGrad + blockTilt + bump, 1.0));
    
    // Lighting
    vec3 Ls = normalize(LightDir);
    float dif = max(dot(N, Ls), 0.0);
    vec3 H = normalize(Ls + vec3(0.0, 0.0, 1.0));
    float nh = max(dot(N, H), 0.0);
    float nh2 = nh * nh; float nh4 = nh2 * nh2; float nh8 = nh4 * nh4;
    float spec = nh8 * nh8 * nh2 * 0.04;
    
    // Ambient occlusion in eroded dry-stone seams
    float seamAo = mix(0.55, 1.0, smoothstep(0.0, 3.5, dSeam));
    
    float shadow = innerFrameShadow(p, d, hw, hs, rad);
    
    // Rich block-to-block quarry batch variations
    float lumVar = 0.65 + 0.60 * bHash.x + proudness * 0.4;
    vec3 tintVar = vec3(1.0 + 0.24 * (bHash.y - 0.5), 1.0 + 0.08 * (bHash.z - 0.5), 1.0 - 0.26 * (bHash.y - 0.5));
    float grain = EwHash2To1(floor(fc));
    
    // Geological sandstone strata running continuously with fault displacements between blocks
    float strataShift = (bHash.x - 0.5) * 45.0;
    float strata1 = sin((p.y + strataShift) * 0.030 + p.x * 0.005 + sDeriv.x * 3.2);
    float strata2 = sin((p.y + strataShift) * 0.080 - p.x * 0.010 + sFine.x * 2.6);
    float strata = 0.5 + 0.35 * strata1 + 0.15 * strata2;
    
    // Ancient structural tension cracks cutting across blocks
    float crack = 0.0;
    if (CrackSize >= 0.01) {
        float crackMask = smoothstep(0.70, 0.88, EwValueNoise2D(p * 0.0035 + 12.0));
        if (crackMask > 0.0) {
            vec2 pCrack = p + (sDeriv.yz * 22.0 + sFine.yz * 10.0);
            float crackDist = voronoiEdge(pCrack * (0.0045 / max(CrackScale, 0.1)) + 31.0) * 55.0 * CrackScale;
            crack = smoothstep(2.4 * CrackSize, 0.2 * CrackSize, crackDist) * crackMask * 0.75;
        }
    }
    
    // Desert patina / dark mineral varnish accumulated in crevices, hollows, and weathered joints
    float patina = smoothstep(6.0, 0.5, dSeam) * 0.45 + tafoni * 0.50;
    
    // Color assembly
    vec3 baseTone = EwSquare(InnerColor);
    vec3 stone = baseTone * lumVar * tintVar;
    stone *= (0.80 + 0.28 * strata) * (0.92 + 0.16 * grain + chisel + fluting) * (1.0 - tafoni);
    stone *= (1.0 - 0.45 * patina);
    stone = mix(stone, EwSquare(CrackColor), crack);
    
    // Weathered mortar / joint recess with accumulated sand and crack tone
    vec3 jointSand = baseTone * 0.55 * (0.8 + 0.4 * grain);
    vec3 seamShadow = EwSquare(CrackColor) * (0.8 + 0.4 * grain);
    vec3 jointFill = mix(seamShadow, jointSand, smoothstep(0.0, 2.5, dSeam));
    stone = mix(jointFill, stone, smoothstep(0.3, 2.2, dSeam));
    return (stone * (Ambient + 0.95 * dif) * seamAo + vec3(spec)) * shadow;
}
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 res = iResolution.xy;
    vec2 p = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float hw = max(inset * BandWidth, 4.0);           // half band width (px)
    vec2 hs = 0.5 * res - inset;
    vec2 imgHs = max(hs - hw, vec2(1.0));
    if (SwHaveImage) {
        vec2 texSize = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = texSize.x / texSize.y;
        vec2 avail = max(hs - hw, vec2(0.0));
        if (avail.x > 0.0 && avail.y > 0.0) {
            float availAspect = avail.x / avail.y;
            imgHs = (imgAspect > availAspect)
                ? vec2(avail.x, avail.x / imgAspect)
                : vec2(avail.y * imgAspect, avail.y);
            hs = imgHs + hw;
        }
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    float d = EwSdRoundedBox2D(p, hs, rad);

    vec4 bg = vec4(Background * Opacity, Opacity);
    float inner = smoothstep(0.5, -0.5, d + 0.5 * hw);  // inside, under the frame body
    vec4 base = bg;
    if (InnerOpacity > 0.001 && inner > 0.0) {
        if (SwHaveImage) {
            vec2 imgUv = clamp((p / max(imgHs, vec2(1.0))) * 0.5 + 0.5, 0.0, 1.0);
            vec4 imgTex = texture(iChannel0, imgUv);
            vec3 imgSrgb = vec3(0.0);
            if (imgTex.a > 0.001) {
                vec3 LsImg = length(LightDir) > 1e-5 ? normalize(LightDir) : vec3(0.0, 0.0, 1.0);
                float imgDif = max(LsImg.z, 0.0);
                float imgSh = innerFrameShadow(p, d, hw, hs, rad);
                vec3 imgLin = EwSrgbToLinear(imgTex.rgb) * (Ambient + 0.95 * imgDif) * imgSh * Brightness;
                imgSrgb = EwLinearToSrgb(clamp(imgLin, 0.0, 1.0));
            }
            if (SwTransparentImage) {
                vec3 innerSrgb = imgSrgb;
                if (imgTex.a < 0.999) {
                    vec3 tileLinear = innerSandstoneTiles(p, fragCoord, d, hw, hs, rad);
                    vec3 tileSrgb = EwLinearToSrgbFast(EwTonemapAces(tileLinear * Brightness));
                    innerSrgb = mix(tileSrgb, imgSrgb, imgTex.a);
                }
                float inA = InnerOpacity * inner;
                base = over(vec4(innerSrgb * inA, inA), bg);
            } else {
                float inA = imgTex.a * InnerOpacity * inner;
                base = over(vec4(imgSrgb * inA, inA), bg);
            }
        } else {
            vec3 tileLinear = innerSandstoneTiles(p, fragCoord, d, hw, hs, rad);
            vec3 tileTonemapped = EwTonemapAces(tileLinear * Brightness);
            vec3 tileSrgb = EwLinearToSrgbFast(tileTonemapped);
            base = over(vec4(tileSrgb * (InnerOpacity * inner), InnerOpacity * inner), bg);
        }
    }
    float tm = mod(iTime * Speed, 3000.0);
    float amt = DustAmount;
    float yTop = hs.y - hw * 0.8;
    float yy = yTop - p.y;                             // px below the top band, downwards
    float frameH = SwHaveImage ? 2.0 * (hs.y + inset) : res.y;
    float fallH = min(FallHeight * frameH, 560.0);
    float xLim = hs.x - max(rad, hw) - 0.2 * hw;
    bool dustZone = amt > 0.001 && yy > -8.0 && yy < fallH && abs(p.x) < xLim + 16.0;
    float maxHaloD = (GemMagic > 0.001 && HaloStrength > 0.001)
        ? max(hw + 2.0, (0.02 + 9.0 / max(HaloFalloff, 0.5)) * hw)
        : (hw + 2.0);
    bool frameZone = d > -1.5 * hw && d < maxHaloD;
    if (!frameZone && !dustZone) { fragColor = base; return; }

    vec3 C = vec3(0.0);
    float A = 0.0;
    float edgeDist = min(min(fragCoord.x, res.x - fragCoord.x), min(fragCoord.y, res.y - fragCoord.y));
    float edgeFade = smoothstep(0.5, 10.0, edgeDist);

    if (frameZone) {
        float L;
        float s = perimPos(p, hs - rad, rad, L);
        float G = float(GemSpacing + 1);
        float nc = max(G, floor(L / (0.8 * hw) / G + 0.5) * G);
        float cw = L / nc;
        if (abs(d) < hw + 0.5) {
        float t = d / hw;
        float ws = 0.8 / hw;
        float chip = Age > 0.001 ? Age * hw * 0.2 * smoothstep(0.5, 0.85, fbm2(p / (hw * 0.3) + 3.1, 3)) : 0.0;
        float edgeIn = hw - abs(d) - chip;
        float band = smoothstep(-0.5, 0.5, edgeIn) * edgeFade;
        if (band > 0.0) {
        float chipRim = smoothstep(3.0, 0.0, edgeIn) * step(0.5, chip);

        // local frame: tangent along the frame, outward normal (analytic rounded-box gradient)
        vec2 qn = abs(p) - hs + rad;
        vec2 sg = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
        vec2 n2 = (qn.x > 0.0 || qn.y > 0.0)
            ? normalize(max(qn, 0.0)) * sg
            : (qn.x > qn.y ? vec2(sg.x, 0.0) : vec2(0.0, sg.y));
        vec2 tg = vec2(n2.y, -n2.x);
        vec3 Ls = normalize(LightDir);
        vec3 Ll = vec3(dot(Ls.xy, tg), dot(Ls.xy, n2), Ls.z);
        vec3 Hh = normalize(Ll + vec3(0.0, 0.0, 1.0));

        // profile normal
        float e2 = 1.0 / hw;
        float slope = (profileH(t + e2, ws) - profileH(t - e2, ws)) * hw * 0.5;
        vec3 N = vec3(0.0, -slope, 1.0);

        float mGoldO = smoothstep(0.80 - ws, 0.80 + ws, t);
        float mGoldI = smoothstep(-0.78 + ws, -0.78 - ws, t);
        float mBandO = smoothstep(0.52 - ws, 0.52 + ws, t);
        float mBandI = smoothstep(-0.48 + ws, -0.48 - ws, t);
        float mPaintO = mBandO * (1.0 - mGoldO);
        float mPaintI = mBandI * (1.0 - mGoldI);
        float mPanel = (1.0 - mBandO) * (1.0 - mBandI);

        // sandstone
        vec3 sandL = EwSquare(SandColor);
        float n1 = fbm2(p / 60.0, 4);
        float strata = 0.5 + 0.5 * sin(p.y * 0.08 + p.x * 0.012 + n1 * 9.0);
        float grain = EwHash2To1(floor(fragCoord));
        vec3 sand = sandL * (0.7 + 0.5 * n1) * (0.9 + 0.15 * strata) * (0.92 + 0.16 * grain);
        float pTex = 0.75 + 0.35 * n1;

        // age: uneven surface, grime, cracks
        vec3 bn1 = EwValueNoise2DDerivatives(p / (hw * 0.35) + 5.0);
        vec3 bn2 = EwValueNoise2DDerivatives(p / (hw * 0.09) + 9.0);
        vec2 bgr = (bn1.yz * 0.9 + bn2.yz * 0.4) * Age;
        vec2 bump = vec2(dot(bgr, tg), dot(bgr, n2));
        N.xy -= bump;
        float grime = fbm2(p / (hw * 0.5) + 13.0, 3);
        sand *= mix(1.0, 0.55 + 0.7 * grime, Age) * (1.0 - 0.15 * Age * bn2.x);
        float crack = 0.0;
        float crackHalo = 0.0;
        if (Age >= 0.01 && CrackSize >= 0.01) {
            vec2 pw = p + (vec2(bn1.x, bn2.x) - 0.5) * hw * 0.12;
            float cmask = smoothstep(0.68 - 0.4 * Age, 0.8 - 0.4 * Age, EwValueNoise2D(p / (hw * 0.7) + 40.0));
            if (cmask > 0.0) {
                float cpx = voronoiEdge(pw / (hw * CrackScale) + 2.3) * hw * 0.4;
                float cw0 = (0.4 + 1.4 * bn1.x) * Age * CrackSize;
                crack = smoothstep(cw0 + 0.8 * CrackSize, max(cw0 - 0.4 * CrackSize, 0.0), cpx) * cmask;
                crackHalo = smoothstep(cw0 + 3.5 * CrackSize, cw0, cpx) * cmask * Age;
            }
            float cmask2 = smoothstep(0.75 - 0.3 * Age, 0.85 - 0.3 * Age, EwValueNoise2D(p / (hw * 0.4) + 60.0));
            if (cmask2 > 0.0) {
                float cpx2 = voronoiEdge(pw / (hw * 0.28 * max(CrackScale, 0.1)) + 7.7) * hw * 0.14 * CrackScale;
                crack = max(crack, smoothstep(0.9 * CrackSize, 0.1 * CrackSize, cpx2) * cmask2 * 0.7);
            }
        }

        vec3 pal0 = EwSquare(PaintBlue), pal1 = EwSquare(PaintRed), pal2 = EwSquare(PaintGreen);
        vec3 pal3 = EwSquare(PaintYellow);
        vec3 cream = EwSquare(PaintCream);
        vec3 alb = sand;

        // panel: hieroglyph cells and gem cartouches
        float ci = floor(s / cw);
        bool isGem = mod(ci, G) < 0.5;
        vec2 q = vec2((s / cw - ci - 0.5) * cw / hw, t - 0.02);
        float pxq = 1.0 / hw;
        float wallW = max(0.025, 1.5 * pxq);
        float dep = 0.06 * hw;
        float c0 = 0.0;
        float carveSh = 0.0;
        if (mPanel > 0.001) {
            int gid = int(EwHash1To1(ci * 1.37 + 0.5) * 8.0);
            vec2 sampOff[4];
            sampOff[0] = vec2(0.0);
            sampOff[1] = vec2(pxq, 0.0);
            sampOff[2] = vec2(0.0, pxq);
            sampOff[3] = Ll.xy / Ll.z * dep * 0.7 * pxq;
            float gVal[4];
            gVal[0] = 1e3; gVal[1] = 1e3; gVal[2] = 1e3; gVal[3] = 1e3;
            float edgeLim = 0.5 * wallW + 4.0 * pxq;
            for (int k = ZERO; k < 4; k++) {
                if (k == 1 && gVal[0] > edgeLim) break;
                if ((k == 1 || k == 2) && gVal[0] < -edgeLim) continue;
                if (k == 3 && gVal[0] >= 0.5 * wallW) break;
                gVal[k] = cellSdf(q + sampOff[k], gid, isGem);
            }
            c0 = smoothstep(0.5 * wallW, -0.5 * wallW, gVal[0]);
            if (abs(gVal[0]) <= edgeLim) {
                float cxx = smoothstep(0.5 * wallW, -0.5 * wallW, gVal[1]);
                float cyy = smoothstep(0.5 * wallW, -0.5 * wallW, gVal[2]);
                N.xy += vec2(cxx - c0, cyy - c0) * dep * mPanel;
            }
            if (c0 > 0.0) carveSh = c0 * smoothstep(-0.01, 0.01, gVal[3]);
        }

        float paintK = 0.0;
        if (PaintAmount > 0.001 && (mPaintO > 0.001 || mPaintI > 0.001 || c0 * mPanel > 0.001)) {
            float wn = fbm2(p / (hw * 0.3) + 7.3, 3);
            paintK = PaintAmount * smoothstep(Wear * 0.6, Wear * 0.6 + 0.08, wn);
        }

        // outer painted band: colored blocks
        float mGxO = 0.0;
        if (mPaintO > 0.001) {
            float nb = max(4.0, floor(L / (hw * 0.32) / 4.0) * 4.0);
            float bl = L / nb;
            float fb = s / bl;
            float bi = mod(floor(fb), 4.0);
            vec3 bc = bi < 0.5 ? pal0 : bi < 1.5 ? pal3 : bi < 2.5 ? pal1 : pal2;
            float inBlk = smoothstep(1.0, -1.0, abs(d - 0.66 * hw) - 0.10 * hw);
            float fd = min(fract(fb), 1.0 - fract(fb)) * bl;
            float sepO = smoothstep(1.6, 0.5, fd) * inBlk;
            float lineO = smoothstep(1.3, 0.3, abs(abs(d - 0.66 * hw) - 0.10 * hw));
            vec3 paintO = mix(cream, bc, inBlk);
            paintO = mix(paintO, vec3(0.02), max(sepO, lineO));
            alb = mix(alb, mix(sand, paintO * pTex, paintK), mPaintO);
            float goldBlk = (bi > 0.5 && bi < 1.5) ? 1.0 : 0.0;
            mGxO = max(lineO, goldBlk * inBlk * (1.0 - sepO)) * mPaintO;
        }

        // inner painted band: chevrons
        float mGxI = 0.0;
        if (mPaintI > 0.001) {
            float nch = max(1.0, floor(L / (hw * 0.3)));
            float v = (t + 0.63) / 0.11;
            float ch = fract(s / L * nch + abs(v) * 0.5);
            float si = floor(ch * 3.0);
            vec3 cc = si < 0.5 ? pal0 : si < 1.5 ? pal3 : pal1;
            float cf = fract(ch * 3.0);
            float cedge = min(cf, 1.0 - cf) * L / (3.0 * nch);
            float inChv = smoothstep(1.0, -1.0, abs(v) * 0.11 * hw - 0.11 * hw);
            float sepI = smoothstep(1.3, 0.3, cedge) * inChv;
            float lineI = smoothstep(1.3, 0.3, abs(abs(d + 0.63 * hw) - 0.11 * hw));
            vec3 paintI = mix(cream, cc, inChv);
            paintI = mix(paintI, vec3(0.02), max(sepI, lineI));
            alb = mix(alb, mix(sand, paintI * pTex, paintK), mPaintI);
            mGxI = lineI * mPaintI;
        }

        float gilt = 0.0;
        if (c0 * mPanel > 0.001) {
            vec3 gh = EwHash1To3(ci * 2.1 + 0.3);
            vec3 gcol = isGem ? pal3 : (gh.x < 0.35 ? pal0 : gh.x < 0.6 ? pal1 : gh.x < 0.85 ? pal2 : vec3(0.015));
            alb = mix(alb, mix(sand * 0.8, gcol * pTex, paintK), c0 * mPanel);
            gilt = (!isGem && gh.y < GoldShare) ? 1.0 : 0.0;
        }

        // cast shadows of the raised bands onto the panel
        float lenO = 0.08 * hw * max(Ll.y, 0.0) / Ll.z;
        float lenI = 0.08 * hw * max(-Ll.y, 0.0) / Ll.z;
        float shO = smoothstep(lenO + 1.0, lenO - 1.0, 0.52 * hw - d);
        float shI = smoothstep(lenI + 1.0, lenI - 1.0, d + 0.48 * hw);
        float shadow = 1.0 - mPanel * max(0.55 * max(shO, shI), 0.6 * carveSh);

        vec3 Nn = normalize(N);
        float dif = max(dot(Nn, Ll), 0.0);
        vec3 col = alb * (Ambient + 0.95 * dif) * shadow;

        // gem setting and gem
        float mB = 0.0, mG = 0.0;
        vec3 nB = vec3(0.0, 0.0, 1.0);
        vec3 gemLit = vec3(0.0);
        if (isGem && mPanel > 0.001) {
            vec2 gr = vec2(0.19, 0.26);
            float bz = sdEll(q, gr + 0.025);
            float bring = abs(bz) - 0.035;
            mB = smoothstep(pxq, -pxq, bring) * mPanel;
            if (mB > 0.001) {
                vec2 gdir = normalize(q / ((gr + 0.025) * (gr + 0.025)) + 1e-5);
                float bu = clamp(bz / 0.035, -1.0, 1.0);
                nB = normalize(vec3(gdir * bu * 1.2, sqrt(max(1.0 - bu * bu, 0.0)) + 0.15));
            }
            float ge = sdEll(q, gr);
            mG = smoothstep(pxq, -pxq, ge) * mPanel;
            if (mG > 0.001) {
                vec2 gq = q / gr;
                float r = length(gq);
                float ang = atan(gq.y, gq.x);
                float fa = (floor(ang / 0.7853982) + 0.5) * 0.7853982;
                vec3 nG = r < 0.42 ? vec3(0.0, 0.0, 1.0) : normalize(vec3(vec2(cos(fa), sin(fa)) * min(r, 1.0) * 0.75, 0.8));
                vec3 gemC = gemPalette(EwHash1To3(ci * 3.7 + 1.1).x);
                float pulse = 0.5 + 0.5 * sin(tm * GemPulseSpeed + ci * 1.7);
                float sp0 = max(sin(tm * 2.3 + ci * 5.1), 0.0);
                float sp2 = sp0 * sp0; float sp4 = sp2 * sp2; float sp8 = sp4 * sp4;
                float spark = sp8 * sp8 * sp8;
                float gdf = max(dot(nG, Ll), 0.0);
                float gh0 = max(dot(nG, Hh), 0.0);
                float gh2 = gh0 * gh0; float gh4 = gh2 * gh2; float gh8 = gh4 * gh4; float gh16 = gh8 * gh8; float gh32 = gh16 * gh16;
                float gsp = gh32 * gh16 * gh8 * gh4;
                float rr = min(r * r, 1.0);
                gemLit = gemC * (0.06 + 0.3 * gdf) * (1.3 - rr)
                       + vec3(1.0) * gsp * (1.5 + 4.0 * spark)
                       + gemC * GemMagic * (0.6 + 2.4 * pulse) * (0.25 + 0.9 * (1.0 - rr));
            }
        }

        // gold: moldings, bezels, pinstripes, gilded blocks and glyphs
        float mGxRaw = max(max(mGxO, mGxI), gilt * c0 * mPanel);
        float mGx = 0.0;
        if (mGxRaw > 0.001) {
            float gWear = smoothstep(Wear * 0.5 - 0.06, Wear * 0.5 + 0.06, fbm2(p / (hw * 0.22) + 21.7, 3));
            mGx = mGxRaw * gWear;
        }
        float mGold = clamp(mGoldO + mGoldI + mB + mGx, 0.0, 1.0);
        if (mGold > 0.001) {
            vec3 goldL = EwSquare(GoldColor);
            vec3 Ng = normalize(mix(normalize(N + vec3(bump * 0.85, 0.0)), nB, mB));
            float gd = max(dot(Ng, Ll), 0.0);
            float gh0 = max(dot(Ng, Hh), 0.0);
            float gh2 = gh0 * gh0; float gh4 = gh2 * gh2; float gh8 = gh4 * gh4;
            float gs = gh8 * gh8 * gh8 * gh8 * gh8;
            float env = 0.5 + 0.5 * sin(Ng.y * 6.0 + Ng.x * 4.0 + 1.0);   // fake reflected surroundings
            vec3 gold = goldL * (0.08 + 0.55 * gd + 0.6 * env * env) + mix(goldL, vec3(1.0), 0.15) * gs * GoldShine;
            gold *= (0.8 + 0.35 * n1) * mix(1.0, 0.55 + 0.6 * grime, Age * 0.7);
            gold += mix(goldL, vec3(1.0), 0.45) * glitter(fragCoord, tm) * GoldGlitter * (0.35 + gd + 2.0 * gs);
            col = mix(col, gold, mGold);
        }
        col = mix(col, EwSquare(CrackColor), crack);
        col *= (1.0 - 0.25 * crackHalo) * (1.0 - 0.5 * chipRim);
        col = mix(col, gemLit, mG);

        C = col * band;
        A = band;
        }
        }

        // magic glow of the nearest gem
        if (GemMagic > 0.001 && HaloStrength > 0.001) {
            float kk = floor((s / cw - 0.5) / G + 0.5);
            float gci = mod(kk * G, nc);
            float ds = s - (kk * G + 0.5) * cw;
            ds -= L * floor(ds / L + 0.5);
            float hd = length(vec2(ds, d - 0.02 * hw)) / hw;
            vec3 hC = gemPalette(EwHash1To3(gci * 3.7 + 1.1).x);
            float hp = 0.5 + 0.5 * sin(tm * GemPulseSpeed + gci * 1.7);
            float halo = exp(-hd * HaloFalloff) * GemMagic * (0.3 + 0.7 * hp) * smoothstep(-1.5 * hw, -1.0 * hw, d) * edgeFade;
            vec3 glow = hC * halo * HaloStrength;
            C += glow;
            A += (1.0 - A) * clamp(EwMaxComponent(glow), 0.0, 1.0);
        }
    }

    if (dustZone) {
        vec4 dl = dustLayer(p, yy, fallH, tm, amt);
        dl *= smoothstep(xLim + 16.0, xLim - 8.0, abs(p.x));
        C = dl.rgb + C * (1.0 - dl.a);
        A = dl.a + A * (1.0 - dl.a);
    }

    vec3 col = vec3(0.0);
    float a = clamp(A, 0.0, 1.0);
    if (a > 1e-4) {
        vec3 c = EwTonemapAces(C / a * Brightness);
        col = EwLinearToSrgbFast(c) * a;
    }
    float dz = EwDither(fragCoord, iTime, 2.0);
    col += dz;
    a = clamp(a + dz, 0.0, 1.0);
    col = min(clamp(col, 0.0, 1.0), vec3(a));
    a *= 1.0 - Additive;
    fragColor = vec4(col + base.rgb * (1.0 - a), a + base.a * (1.0 - a));
}
