//  media: collage
//  Arcane Fortune Reels - a magic 5 reel slot machine spinning the collage pictures.
//  Reels start one by one, race at random speeds, stop one by one with a spring bounce, pause, repeat;
//  every spin lands on a new combination. Bulbs, flashes, ticker reflection and orb follow the reel cycle.
//  iMouse.x = Glow | Brightness of the bulbs, the crystal orb and the magic glow | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Magic Hue | Hue shift of cabinet, lights and magic colors | 0.5 | -180 .. 180 | °
//  iMouse.z = Glass | Strength of the glass reflections over the reels | 0.5 | 0.0 .. 1.0
//  iMouse.w = Sparkle | Background nebula and star sparkle intensity | 0.5 | 0.0 .. 2.0 | x

#define GlowAmount mix(0.0, 2.0, iMouse.x)
#define MagicHue radians(mix(-180.0, 180.0, iMouse.y))
#define GlassAmount mix(0.0, 1.0, iMouse.z)
#define SparkleAmount mix(0.0, 2.0, iMouse.w)

// ---- Collage layout (written by the editor) ----
const float TileCountX = 4.0; // var: { "min": 1.0, "max": 16.0, "step": 1.0, "name": "Tile Count X", "desc": "Atlas columns (set by the editor)" }
const float TileCountY = 3.0; // var: { "min": 1.0, "max": 16.0, "step": 1.0, "name": "Tile Count Y", "desc": "Atlas rows (set by the editor)" }
const float TileCount = 12.0; // var: { "min": 1.0, "max": 256.0, "step": 1.0, "name": "Tile Count", "desc": "Number of tiles (set by the editor)" }
const float UniqueCount = 11.0; // var: { "min": 1.0, "max": 256.0, "step": 1.0, "name": "Unique Count", "desc": "Number of different pictures (set by the editor)" }
const float TileWidth = 384.0; // var: { "min": 16.0, "max": 4096.0, "step": 1.0, "name": "Tile Width", "desc": "Tile width (set by the editor)", "unit": "px" }
const float TileHeight = 216.0; // var: { "min": 16.0, "max": 4096.0, "step": 1.0, "name": "Tile Height", "desc": "Tile height (set by the editor)", "unit": "px" }
const float ImageWidth = 380.0; // var: { "min": 16.0, "max": 4096.0, "step": 1.0, "name": "Image Width", "desc": "Picture width inside a tile (set by the editor)", "unit": "px" }
const float ImageHeight = 212.0; // var: { "min": 16.0, "max": 4096.0, "step": 1.0, "name": "Image Height", "desc": "Picture height inside a tile (set by the editor)", "unit": "px" }

// ---- Reel timing ----
const float SpinTime = 2.0; // var: { "min": 0.5, "max": 6.0, "step": 0.1, "name": "Spin Time", "desc": "Full speed time before the first reel stops", "unit": "s" }
const float StartDelay = 0.15; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Start Delay", "desc": "Delay between the reels starting", "unit": "s" }
const float StopGap = 0.4; // var: { "min": 0.1, "max": 1.0, "step": 0.05, "name": "Stop Gap", "desc": "Time between the reels stopping one by one", "unit": "s" }
const float PauseTime = 3.0; // var: { "min": 0.5, "max": 10.0, "step": 0.1, "name": "Pause", "desc": "Pause after all reels stopped before the next spin", "unit": "s" }
const float MinSpeed = 9.0; // var: { "min": 2.0, "max": 30.0, "step": 0.5, "name": "Min Speed", "desc": "Slowest random reel speed", "unit": "sym/s" }
const float MaxSpeed = 15.0; // var: { "min": 2.0, "max": 30.0, "step": 0.5, "name": "Max Speed", "desc": "Fastest random reel speed", "unit": "sym/s" }
const float Bounce = 0.2; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Bounce", "desc": "Spring overshoot when a reel lands", "scale": 100.0, "unit": "%" }
const float BounceFreq = 3.5; // var: { "min": 1.0, "max": 8.0, "step": 0.1, "name": "Bounce Frequency", "desc": "Wobble rate of the landing spring", "unit": "Hz" }

// ---- Look ----
const float MotionBlur = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Motion Blur", "desc": "Blur of the spinning symbols", "unit": "x" }
const float ReelCurve = 0.75; // var: { "min": 0.3, "max": 1.4, "step": 0.05, "name": "Reel Curvature", "desc": "How round the reel drums look" }
const float VisibleRows = 3.6; // var: { "min": 3.0, "max": 4.5, "step": 0.05, "name": "Visible Rows", "desc": "Symbols visible per reel: 3 full rows plus partial ones at top and bottom", "unit": "rows" }
const float TickerSpeed = 0.12; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Ticker Speed", "desc": "Scroll speed of the LED rune ticker" }
const vec3 CabinetColor = vec3(0.20, 0.05, 0.32); // var: { "type": "col", "name": "Cabinet Color", "desc": "Lacquer of the cabinet and the crest" }
const vec3 BulbColorA = vec3(1.0, 0.72, 0.28); // var: { "type": "col", "name": "Bulb Color A", "desc": "Even bulbs, win glow" }
const vec3 BulbColorB = vec3(0.85, 0.30, 1.0); // var: { "type": "col", "name": "Bulb Color B", "desc": "Odd bulbs" }
const vec3 MagicColor = vec3(0.35, 0.65, 1.0); // var: { "type": "col", "name": "Magic Color", "desc": "Crystal orb, nebula and flashes" }
const vec3 CardFrameColor = vec3(1.0, 0.76, 0.33); // var: { "type": "col", "name": "Card Frame Color", "desc": "Gold frames around the pictures" }
const vec3 TrimColor = vec3(1.05, 0.78, 0.4); // var: { "type": "col", "name": "Trim Tint", "desc": "Tint of the thin gold trim around the reel window" }

// ---- Lights ----
const float ChaseSpeed = 16.0; // var: { "min": 2.0, "max": 40.0, "step": 0.5, "name": "Chase Speed", "desc": "How fast the bulb chase runs while the reels spin", "unit": "bulbs/s" }
const float IdleBlinkRate = 1.5; // var: { "min": 0.25, "max": 6.0, "step": 0.05, "name": "Pause Blink Rate", "desc": "Bulb blinking during the pause (no win)", "unit": "Hz" }
const float WinBlinkRate = 4.0; // var: { "min": 0.5, "max": 10.0, "step": 0.1, "name": "Win Blink Rate", "desc": "Bulb blinking during the pause after a win", "unit": "Hz" }
const float FlashDecay = 4.0; // var: { "min": 1.0, "max": 12.0, "step": 0.1, "name": "Flash Decay", "desc": "How fast the landing flashes fade; lower = longer flashes", "unit": "1/s" }
const float HaloStrength = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Bulb Halo", "desc": "Glow halos around the bulbs", "unit": "x" }
const float HaloFalloff = 65.0; // var: { "min": 20.0, "max": 150.0, "step": 1.0, "name": "Halo Falloff", "desc": "Size of the bulb halos; higher = tighter" }
const float BulbSpacing = 0.05; // var: { "min": 0.03, "max": 0.1, "step": 0.002, "name": "Bulb Spacing", "desc": "Distance between the bulbs; lower = more bulbs" }

// ---- Reflections ----
const float TickerGlass = 0.45; // var: { "min": 0.0, "max": 1.5, "step": 0.01, "name": "Ticker on Glass", "desc": "Brightness of the mirrored LED ticker on the reel glass" }
const float TickerChrome = 2.5; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Ticker on Chrome", "desc": "Brightness of the LED ticker reflected in the chrome" }
const float GlintStrength = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Chrome Glints", "desc": "Star glints twinkling on the chrome rims", "unit": "x" }

// ---- Cabinet ----
const float OrbSize = 0.042; // var: { "min": 0.025, "max": 0.06, "step": 0.001, "name": "Orb Size", "desc": "Radius of the crystal orb on the crest (the machine shrinks a bit to fit it)" }
const float OrbSwirlSpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Orb Swirl Speed", "desc": "Speed of the magic swirl inside the orb", "unit": "x" }
const float CrestRaySpeed = 0.8; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Crest Ray Speed", "desc": "Rotation speed of the light rays on the crest", "unit": "rad/s" }
const float FlakeStrength = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Flake Sparkle", "desc": "Brightness of the metal flakes in the cabinet lacquer", "unit": "x" }
const float FlakeSize = 0.0032; // var: { "min": 0.0015, "max": 0.01, "step": 0.0001, "name": "Flake Size", "desc": "Size of the metal flakes (never smaller than about 3 pixels)" }
const float FlakeSharpness = 90.0; // var: { "min": 10.0, "max": 300.0, "step": 1.0, "name": "Flake Sharpness", "desc": "Higher = fewer, crisper flake sparkles" }
const float LatticeStrength = 0.045; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Lattice Strength", "desc": "Gold diamond lattice pattern on the cabinet face" }
const float LatticeScale = 18.0; // var: { "min": 6.0, "max": 40.0, "step": 0.5, "name": "Lattice Density", "desc": "Diamonds per unit; higher = finer lattice" }

// ---- Background ----
const float BackLights = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Casino Lights", "desc": "Blinking blurred lights of other machines (also in the glass)", "unit": "x" }
const float StarDensity = 28.0; // var: { "min": 10.0, "max": 60.0, "step": 1.0, "name": "Star Density", "desc": "Grid density of the twinkling stars" }
const float NebulaSpeed = 1.0; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Nebula Drift", "desc": "Drift speed of the nebula clouds", "unit": "x" }
const float Vignette = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Vignette", "desc": "Darkening of the screen corners" }

const float CycleLead = 0.35;
const float SpinUp = 0.6;
const float StopDur = 0.32;
const float SettleTime = 0.8;

vec3 gCab, gBulbA, gBulbB, gMagic;
float gN, gCyc, gTc, gTime, gEnergy, gFlash, gPause, gWin;
float gWinR[5];

float cycleLength() { return CycleLead + SpinUp + SpinTime + 4.0 * (StartDelay + StopGap) + StopDur + SettleTime + PauseTime; }
float reelStartT(float r) { return CycleLead + r * StartDelay; }
float reelStopT(float r) { return CycleLead + SpinUp + SpinTime + r * (StartDelay + StopGap); }
float reelLandT(float r) { return reelStopT(r) + StopDur; }

float rawTarget(float cyc, float r) {
    float stepR = 0.381966 + fract(r * 0.618034) * 0.3;
    return floor(fract(cyc * stepR + r * 0.2357 + 0.137) * gN);
}
float reelTarget(float cyc, float r) {
    float a = rawTarget(cyc, r);
    float b = rawTarget(cyc - 1.0, r);
    return (a == b) ? mod(a + 1.0, gN) : a;
}
float symbolImage(float k, float r) { return mod(k + floor(r * gN * 0.37 + 0.5), gN); }

// reel position (symbols) and velocity (symbols / s)
vec2 reelPos(float r) {
    float t0 = reelStartT(r);
    float tc0 = t0 + SpinUp;
    float ts = reelStopT(r);
    float te = ts + StopDur;
    float e = Bounce;
    float Fu = SpinUp * 0.5;
    float Fc = ts - tc0;
    float Fd = StopDur * (e + (1.0 - e) / 3.0);
    float Ft = Fu + Fc + Fd;
    float vr = mix(MinSpeed, MaxSpeed, EwHash2To1(vec2(gCyc + 3.7, r * 7.1 + 1.3)));
    float S0 = reelTarget(gCyc - 1.0, r);
    float S1 = reelTarget(gCyc, r);
    float Dn = ceil(vr * Ft);
    float D = Dn + mod(S1 - S0 - Dn, gN);
    float v = D / Ft;
    float t = gTc;
    float f = 0.0, fv = 0.0, off = 0.0, offv = 0.0;
    if (t < t0) {
        float s = clamp((t - t0 + 0.3) / 0.3, 0.0, 1.0);
        float sn = sin(EW_PI * s);
        off = -0.1 * sn * sn;
    } else if (t < tc0) {
        float s = (t - t0) / SpinUp;
        f = SpinUp * (s * s * s - 0.5 * s * s * s * s);
        fv = s * s * (3.0 - 2.0 * s);
    } else if (t < ts) {
        f = Fu + t - tc0;
        fv = 1.0;
    } else if (t < te) {
        float s = (t - ts) / StopDur;
        float is = 1.0 - s;
        f = Fu + Fc + StopDur * (e * s + (1.0 - e) * (1.0 - is * is * is) / 3.0);
        fv = e + (1.0 - e) * is * is;
    } else {
        f = Ft;
        float tb = t - te;
        float w = EW_TAU * BounceFreq;
        float dmp = exp(-tb * 5.0);
        off = v * e / w * dmp * sin(w * tb);
        offv = v * e * dmp * (cos(w * tb) - 5.0 / w * sin(w * tb));
    }
    return vec2(S0 + v * f + off, v * fv + offv);
}

// picture idx from the collage atlas, q in [0,1]^2 of the picture (clamped inside its tile), dq = uv footprint for mip / blur
vec3 sampleTile(float idx, vec2 q, vec2 dq) {
    float c = mod(idx, TileCountX);
    float rr = floor(idx / TileCountX);
    vec2 sc = vec2(ImageWidth, ImageHeight) / vec2(TileWidth, TileHeight);
    vec2 inset = 0.5 * (1.0 - sc);
    q = clamp(q, 0.003, 0.997);
    vec2 t = inset + q * sc;
    vec2 uv = vec2((c + t.x) / TileCountX, 1.0 - (rr + 1.0 - t.y) / TileCountY);
    vec2 g = dq * sc / vec2(TileCountX, TileCountY);
    return textureGrad(iChannel0, uv, vec2(g.x, 0.0), vec2(0.0, g.y)).rgb;
}
float gPX, gPY;

// LED rune ticker hanging in the room (only seen as a reflection)
// g: x in LED cells (scrolling), y in LED rows (0..6); soft: LED blur (0 sharp .. 1 very soft)
vec3 tickerLight(vec2 g, float soft) {
    g.x += gTime * TickerSpeed / 0.0098;
    vec2 cell = floor(g);
    vec2 f = g - cell - 0.5;
    float on = 0.0;
    float ch = floor(cell.x / 6.0);
    float c6 = cell.x - ch * 6.0;
    float row = cell.y;
    float word = mod(ch, 9.0);
    if (row >= 0.0 && row <= 6.0 && c6 < 5.0) {
        if (word < 6.0) {
            float mc = min(c6, 4.0 - c6);
            on = step(0.5, EwHash2To1(vec2(ch * 7.0 + mc, row + 13.0)));
            if (mc == 2.0 && EwHash1To1(ch) > 0.5) on = 1.0;
        } else if (word < 7.0) {
            vec2 sq = abs(vec2(c6 - 2.0, row - 3.0));
            on = step(sq.x + sq.y, 3.0) * step(min(sq.x, sq.y), 0.5);
        }
    }
    float wave = 0.5 + 0.5 * sin(cell.x * 0.07 - gTime * 2.0);
    vec3 lc = mix(gBulbA, gMagic * 1.3, wave);
    float boost = 0.85 + 0.6 * min(gFlash, 1.0) + 0.3 * gPause * gWin * step(0.5, fract(gTc * 4.0));
    float led = mix(smoothstep(0.45, 0.2, length(f)), 0.55, soft);
    float band = smoothstep(-0.5, 0.0, g.y) * smoothstep(7.5, 7.0, g.y);
    return lc * (led * on * boost + 0.03 * band);
}

// studio environment seen by chrome: dark floor, bright horizon / softbox bands, bulb glow, ticker (tx: its x), key light
vec3 envMap(vec3 R, float tx) {
    float y = R.y;
    vec3 ground = mix(vec3(0.015, 0.01, 0.03), gCab * 0.7 + 0.03, smoothstep(-1.0, -0.1, y));
    vec3 skyC = mix(vec3(0.8, 0.82, 0.95), vec3(0.22, 0.24, 0.36), smoothstep(0.1, 0.9, y));
    vec3 c = mix(ground, skyC, smoothstep(-0.02, 0.05, y));
    c += vec3(1.3) * smoothstep(0.03, 0.0, abs(y - 0.33) - 0.06);
    c += vec3(1.0, 0.92, 0.85) * 0.8 * smoothstep(0.02, 0.0, abs(y - 0.72) - 0.03);
    c += gBulbA * 0.4 * smoothstep(0.1, 0.0, abs(y + 0.45) - 0.05) * (0.5 + 0.5 * gEnergy);
    c *= 0.8 + 0.2 * R.x;
    c += tickerLight(vec2((-tx * 0.8 + R.x * 0.25) / 0.012, (R.y - 0.4) / 0.035), 0.15) * TickerChrome;
    c += pow(max(dot(R, normalize(vec3(-0.4, 0.6, 0.7))), 0.0), 80.0) * 2.0;
    return c;
}
// chrome tube cross section: u = -1 .. 1 across the tube, dir = outward direction in the screen plane.
// The reflection is taken in tube space: every tube (also the vertical ones, which would only see
// the flat horizon) sweeps the studio bands like the top tube; bottom tubes see it mirrored (floor outside).
vec3 chrome(vec2 dir, float u) {
    u = clamp(u, -0.97, 0.97);
    float z = sqrt(1.0 - u * u);
    float m = 2.0 * z * u;                          // reflection component along dir
    float g = dir.y + 1.0 - abs(dir.y);             // 1 top / sides, -1 bottom
    vec3 R = vec3(0.0, m * g, 2.0 * z * z - 1.0);
    float tx = gPX * dir.y * dir.y - gPY * dir.x * dir.x;   // ticker runs along the tube
    return envMap(R, tx) * vec3(0.92, 0.93, 1.0) * (0.45 + 0.55 * z);
}
// chrome tube: 4 sub pixel samples across the tube (the reflection changes fast near the edges)
vec3 rim(vec3 col, float d, float w, vec2 dir, float px, vec3 tint) {
    col *= 1.0 - 0.75 * EwFillAA(abs(d) - w - 1.5 * px, px);
    if (abs(d) < w + 2.0 * px) {
        vec3 ch = vec3(0.0);
        for (int i = 0; i < 4; i++) ch += chrome(dir, (d + (float(i) - 1.5) * 0.25 * px) / w);
        col = mix(col, ch * 0.25 * tint, EwFillAA(abs(d) - w, px));
    }
    return col;
}
// unit gradient of a rounded box SDF (the chrome tube normal direction)
vec2 sdGrad(vec2 p, vec2 h, float rad) {
    vec2 e = vec2(0.002, 0.0);
    vec2 g = vec2(EwSdRoundedBox2D(p + e.xy, h, rad) - EwSdRoundedBox2D(p - e.xy, h, rad),
                  EwSdRoundedBox2D(p + e.yx, h, rad) - EwSdRoundedBox2D(p - e.yx, h, rad));
    return g / max(length(g), 1e-6);
}

// ---- layout (set in mainImage) ----
vec2 gCabH, gWinH, gBulbH;
float gCabCY, gCabTop, gWinCY, gSymH, gK, gSpin, gIdle;
float gLand[5];
vec3 gHalo;

float reelX(float r) { return -gWinH.x + (r + 0.5) * 0.4 * gWinH.x; }

// metallic flakes in the lacquer: tiny tilted mirrors catching a slowly orbiting light
float flakes(vec2 p, float px) {
    float s = max(FlakeSize, px * 3.5);
    vec2 g = p / s;
    vec2 id = floor(g);
    vec2 h = EwHash2To2(id + 5.0);
    vec2 f = g - id - 0.5 - (h - 0.5) * 0.4;
    vec3 n = normalize(vec3((h - 0.5) * 1.2, 1.0));
    vec3 L = normalize(vec3(0.6 * sin(gTime * 0.35), 0.5 + 0.3 * cos(gTime * 0.27), 1.0));
    vec3 H = normalize(L + vec3(0.0, 0.0, 1.0));
    float sp = pow(max(dot(n, H), 0.0), FlakeSharpness);
    return sp * smoothstep(0.38, 0.05, length(f));
}

// blinking blurred lights of other machines
vec3 bokeh(vec2 p, float cs, float spd, float seed) {
    vec2 g = (p + vec2(gTime * spd, 0.0)) / cs;
    vec2 id = floor(g);
    vec2 h = EwHash2To2(id + seed * 13.1);
    vec2 f = g - id - 0.5 - (h - 0.5) * 0.35;
    float r = 0.2 + 0.15 * h.y;
    float d = length(f);
    float disc = smoothstep(r, r - 0.07, d) * (0.7 + 0.3 * smoothstep(r * 0.4, r, d));
    float tt = gTime * (1.0 + 3.0 * h.x) + h.y * 17.0;
    float st = floor(tt);
    float key = id.x * 3.1 + id.y * 17.7 + seed;
    float b0 = step(0.45, EwHash2To1(vec2(st, key)));
    float b1 = step(0.45, EwHash2To1(vec2(st + 1.0, key)));
    float blink = mix(b0, b1, smoothstep(0.75, 1.0, fract(tt)));
    float hs = fract(h.x * 7.3 + h.y * 3.1);
    vec3 c = hs < 0.25 ? gBulbA : (hs < 0.5 ? gBulbB : (hs < 0.75 ? gMagic : vec3(1.0, 0.25, 0.3)));
    return c * disc * (0.08 + 0.92 * blink) * step(0.3, h.y + 0.2 * h.x);
}

// night casino backdrop: drifting nebula, twinkling stars, two layers of blinking machine lights
vec3 background(vec2 p) {
    float t = gTime;
    vec3 col = mix(vec3(0.012, 0.008, 0.03), gCab * 0.45, smoothstep(1.2, 0.0, length(p * vec2(0.7, 1.0))));
    float n = EwFbm2D(p * 2.0 + vec2(t * 0.03, -t * 0.02) * NebulaSpeed, 4);
    col += (gMagic * 0.25 * smoothstep(0.4, 0.8, n) + gCab * 0.5 * smoothstep(0.3, 0.7, n)) * SparkleAmount;
    vec2 g = p * StarDensity;
    vec2 id = floor(g);
    vec2 h = EwHash2To2(id);
    float d = length(g - id - 0.2 - 0.6 * h);
    float tw = 0.5 + 0.5 * sin(t * (1.0 + 3.0 * h.x) + h.y * 30.0);
    col += vec3(0.9, 0.85, 1.0) * smoothstep(0.12, 0.0, d) * step(0.65, h.y) * tw * SparkleAmount;
    col += bokeh(p, 0.17, 0.012, 1.0) * 0.45 * BackLights;
    col += bokeh(p + 0.37, 0.1, -0.02, 7.0) * 0.25 * BackLights;
    return col;
}

// bulb brightness: chase while spinning, flash near landing reels, blink in the pause (faster on a win)
float bulbLevel(float idx, float bx) {
    float ch = fract((idx - gTime * ChaseSpeed) / 6.0);
    float lvChase = 0.12 + 0.95 * (1.0 - smoothstep(0.28, 0.4, ch));
    float blink = step(0.5, fract(gTc * (gWin > 0.5 ? WinBlinkRate : IdleBlinkRate) + mod(idx, 2.0) * 0.5));
    float lvPause = 0.2 + 0.9 * blink;
    float flash = 0.0;
    for (int i = 0; i < 5; i++) {
        float e = (bx - reelX(float(i))) / 0.16;
        flash += gLand[i] * exp(-e * e);
    }
    return clamp(gSpin * lvChase + gPause * lvPause + gIdle * 0.75 + flash * 1.2, 0.0, 1.6);
}

// nearest bulb (dist, idx, pos), accumulates the smooth halo of the neighbours in gHalo
vec4 bulbs(vec2 p) {
    vec2 h = gBulbH;
    float nx = max(floor(2.0 * h.x / BulbSpacing), 4.0);
    float ny = max(floor(2.0 * h.y / BulbSpacing), 3.0);
    float sx = 2.0 * h.x / nx, sy = 2.0 * h.y / ny;
    vec4 best = vec4(1e5, 0.0, 0.0, 0.0);
    gHalo = vec3(0.0);
    for (int s = 0; s < 4; s++) {
        bool hor = (s == 0 || s == 2);
        float n = hor ? nx : ny;
        float st = hor ? sx : sy;
        float off = s == 0 ? 0.0 : (s == 1 ? nx : (s == 2 ? nx + ny : 2.0 * nx + ny));
        vec2 o = s == 0 ? vec2(-h.x, h.y) : (s == 1 ? vec2(h.x, h.y) : (s == 2 ? vec2(h.x, -h.y) : vec2(-h.x, -h.y)));
        vec2 dir = s == 0 ? vec2(1.0, 0.0) : (s == 1 ? vec2(0.0, -1.0) : (s == 2 ? vec2(-1.0, 0.0) : vec2(0.0, 1.0)));
        float j0 = floor(dot(p - o, dir) / st + 0.5);
        for (int k = -1; k <= 1; k++) {
            float j = j0 + float(k);
            if (j < 0.0 || j > n - 0.5) continue;
            vec2 b = o + dir * j * st;
            float d = length(p - b);
            float idx = off + j;
            float lv = bulbLevel(idx, b.x);
            vec3 bc = mod(idx, 2.0) < 0.5 ? gBulbA : gBulbB;
            gHalo += bc * lv * exp(-max(d - 0.013, 0.0) * HaloFalloff);
            if (d < best.x) best = vec4(d, idx, b);
        }
    }
    return best;
}

// one card on the reel strip: dark backing, gold frame, picture (sc: strip position in symbols)
vec3 symbolAt(float sc, float cx, float r, float fw, vec2 dq) {
    float k = floor(sc + 0.5);
    float fy = sc - k;
    float d = EwSdRoundedBox2D(vec2(cx, fy), vec2(0.66, 0.43), 0.07);
    vec3 c = (vec3(0.06, 0.04, 0.09) + gCab * 0.45) * (0.8 + 0.2 * cos(fy * EW_TAU));
    c += gMagic * 0.05 * (0.5 + 0.5 * sin(cx * 9.0 + fy * 9.0));
    c *= mix(0.4, 1.0, smoothstep(-0.01, 0.08, d));
    vec3 gold = CardFrameColor * (0.45 + 0.75 * smoothstep(0.3, -0.45, fy - cx * 0.25));
    gold += vec3(0.6, 0.5, 0.35) * exp(-abs(fy + cx * 0.3 - 0.25) * 14.0);
    c = mix(c, gold, EwFillAA(d, fw));
    vec3 ic = sampleTile(symbolImage(k, r), vec2(0.5 + cx / (0.8 * 1.7778), 0.5 + fy / 0.8), dq);
    c = mix(c, ic, EwFillAA(d + 0.03, fw));
    return c;
}

// the reel window: 5 curved drums with motion blurred cards, win glow, flashes, separators, glass
vec3 reels(vec2 p, float px, float dW) {
    float reelW = 0.4 * gWinH.x;
    float r = clamp(floor((p.x + gWinH.x) / reelW), 0.0, 4.0);
    int ri = int(r);
    float lxu = p.x - reelX(r);
    float yn = clamp(p.y / gWinH.y, -0.999, 0.999);
    float sA = sin(ReelCurve);
    float th = asin(yn * sA);
    float ct = max(cos(th), 0.05);
    float ys = th * gK;
    float cx = lxu / gSymH;
    float fwX = px / gSymH;
    float fwY = px / (gSymH * ct);
    float fw = max(fwX, fwY);
    vec2 pv = reelPos(r);
    float bl = clamp(abs(pv.y) * MotionBlur * 0.02, 0.0, 0.8);
    vec2 dq = vec2(fwX / (0.8 * 1.7778), fwY / 0.8 + bl / 4.8);
    int n = bl > 0.02 ? 7 : 1;
    vec3 acc = vec3(0.0);
    float wsum = 0.0;
    for (int i = 0; i < 7; i++) {
        if (i >= n) break;
        float o = n > 1 ? (float(i) / 6.0 - 0.5) * bl : 0.0;
        acc += symbolAt(ys + pv.x + o, cx, r, fw + bl * 0.1, dq);
        wsum += 1.0;
    }
    vec3 col = acc / wsum;

    // win glow around the middle card
    float k0 = floor(ys + pv.x + 0.5);
    float d0 = EwSdRoundedBox2D(vec2(cx, ys + pv.x - k0), vec2(0.66, 0.43), 0.07);
    float isMid = 1.0 - smoothstep(0.45, 0.6, abs(ys));
    float wg = gPause * gWinR[ri] * isMid * (0.6 + 0.4 * sin(gTime * 10.0));
    col += gBulbA * wg * (0.35 + GlowAmount) * exp(-abs(d0) * 20.0) * 0.8;
    col *= 1.0 + 0.25 * wg * step(d0, 0.0);

    // drum shading
    col *= mix(0.35, 1.0, ct);
    col += 0.12 * exp(-pow((yn - 0.45) * 4.0, 2.0));
    col *= 1.0 - 0.35 * pow(abs(lxu) / (0.5 * reelW), 6.0);

    // landing flash
    col += gMagic * (0.15 + 0.35 * GlowAmount) * gLand[ri] * (0.3 + pow(abs(lxu) / (0.5 * reelW), 4.0));

    // payline
    float pl = exp(-abs(p.y) / (px * 1.5)) * gPause * gWin * (0.6 + 0.4 * sin(gTime * 10.0));
    col += gBulbA * pl * (0.4 + 0.6 * GlowAmount);

    // chrome separators
    float u = (lxu - sign(lxu) * 0.5 * reelW) / 0.006;
    if (abs(u) < 1.0 + 3.0 * px / 0.006) {
        vec3 sep = vec3(0.0);
        for (int i = 0; i < 4; i++) sep += chrome(vec2(1.0, 0.0), u + (float(i) - 1.5) * 0.25 * px / 0.006);
        col = mix(col, sep * 0.25 * mix(0.4, 1.0, ct), EwFillAA(abs(u) - 1.0, px / 0.006));
    }

    // inner shadow of the bezel
    col *= mix(0.3, 1.0, smoothstep(0.0, 0.022, -dW));

    // glass: streaks + reflected room lights
    float g = GlassAmount;
    float b1 = smoothstep(0.06, 0.0, abs(p.y - 0.16 + 0.35 * p.x) - 0.03);
    float b2 = smoothstep(0.012, 0.0, abs(p.y - 0.045 + 0.35 * p.x) - 0.002);
    float fade = smoothstep(0.5, -0.6, p.x);
    col += vec3(0.9, 0.92, 1.0) * (0.14 * b1 + 0.1 * b2) * fade * g;
    col += vec3(0.8, 0.85, 1.0) * 0.06 * smoothstep(-0.1, 0.3, p.y) * g;
    // mirrored LED ticker from the room behind the viewer
    float ty = (p.y - (0.55 * gWinH.y - 0.06 * p.x)) / 0.0125 + 3.5;
    float tfade = 0.25 + 0.75 * smoothstep(1.0, -0.6, p.x / gWinH.x);
    col += tickerLight(vec2(-p.x / 0.0125, ty), 0.6) * TickerGlass * tfade * g;
    col += bokeh(vec2(-p.x, p.y) * 1.2 + vec2(3.1, 0.4), 0.2, 0.015, 3.0) * 0.18 * BackLights * g;
    return col;
}

// distance to the chrome rims of cabinet and window (glints only appear on chrome)
float rimDist(vec2 q) {
    float a = abs(EwSdRoundedBox2D(q - vec2(0.0, gCabCY), gCabH, 0.045) + 0.013) - 0.010;
    float b = abs(EwSdRoundedBox2D(q - vec2(0.0, gWinCY), gWinH, 0.015) - 0.016) - 0.013;
    return min(a, b);
}

// star glints running over the chrome
vec3 glints(vec2 p) {
    const float cs = 0.035;
    vec2 base = floor(p / cs);
    float acc = 0.0;
    for (int i = 0; i < 9; i++) {
        vec2 id = base + vec2(float(i % 3) - 1.0, float(i / 3) - 1.0);
        vec2 h = EwHash2To2(id + 17.0);
        vec2 sp = (id + 0.5 + (h - 0.5) * 0.8) * cs;
        if (rimDist(sp) > 0.0) continue;
        float tw = pow(max(0.0, sin(gTime * (0.7 + h.y) + h.x * 60.0)), 24.0);
        vec2 q = abs(p - sp);
        float s = exp(-q.x * 900.0 - q.y * 140.0) + exp(-q.y * 900.0 - q.x * 140.0) + exp(-length(q) * 260.0);
        acc += s * tw;
    }
    return vec3(1.0, 0.96, 0.9) * acc * GlintStrength * (0.4 + 0.6 * SparkleAmount) * (0.7 + 0.5 * gEnergy);
}

// cabinet face: vertical lacquer gradient, faint room reflection, gold lattice, sparkling flakes
vec3 lacquer(vec2 pc, vec2 p, float px) {
    vec3 base = gCab * (0.35 + 0.8 * smoothstep(-gCabH.y, gCabH.y, pc.y));
    vec3 n = normalize(vec3(pc / gCabH * vec2(0.15, 0.3), 1.0));
    base += envMap(reflect(vec3(0.0, 0.0, -1.0), n), gPX) * 0.06;
    vec2 fq = pc * LatticeScale;
    float lat = min(abs(fract(fq.x + fq.y) - 0.5), abs(fract(fq.x - fq.y) - 0.5));
    base += gBulbA * LatticeStrength * smoothstep(0.06, 0.0, lat);
    base += mix(gBulbA, vec3(1.0), 0.6) * flakes(p, px) * FlakeStrength * (0.8 + 0.6 * gEnergy);
    return base;
}

vec3 scene(vec2 p, float px) {
    gPX = p.x;
    gPY = p.y;
    vec3 col = background(p);

    vec2 pc = p - vec2(0.0, gCabCY);
    float dCab = EwSdRoundedBox2D(pc, gCabH, 0.045);
    col *= mix(0.3, 1.0, smoothstep(-0.02, 0.09, dCab));
    col += gMagic * 0.1 * GlowAmount * (0.5 + 0.5 * gEnergy) * exp(-max(dCab, 0.0) * 12.0);

    // crest behind the top rim
    vec2 cp = p - vec2(0.0, gCabTop);
    vec2 cr = vec2(0.19, 0.074);
    float dCr = (length(cp / cr) - 1.0) * 0.074;
    vec3 crestCol = gCab * (0.6 + 0.8 * clamp(cp.y / cr.y, 0.0, 1.0));
    crestCol += mix(gBulbA, vec3(1.0), 0.6) * flakes(p, px);
    float ang = atan(cp.x, cp.y);
    crestCol += gMagic * 0.25 * (0.3 + GlowAmount) * (0.5 + 0.5 * gEnergy) * pow(0.5 + 0.5 * cos(ang * 12.0 + gTime * CrestRaySpeed), 4.0) * smoothstep(0.0, 0.08, length(cp));
    col = mix(col, crestCol, EwFillAA(dCr, px));
    col = rim(col, dCr + 0.008, 0.008, normalize(cp / (cr * cr) + 1e-5), px, vec3(1.0));

    // cabinet body
    col = mix(col, lacquer(pc, p, px), EwFillAA(dCab, px));
    col = rim(col, dCab + 0.013, 0.013, sdGrad(pc, gCabH, 0.045), px, vec3(1.0));

    // window bezel
    vec2 pw = p - vec2(0.0, gWinCY);
    float dW = EwSdRoundedBox2D(pw, gWinH, 0.015);
    vec2 gW = sdGrad(pw, gWinH, 0.015);
    col *= mix(0.55, 1.0, smoothstep(0.03, 0.08, dW));
    col = rim(col, dW - 0.037, 0.0045, gW, px, TrimColor);
    col = rim(col, dW - 0.016, 0.016, gW, px, vec3(1.0));

    // bulbs
    vec4 nb = bulbs(pw);
    float lv = bulbLevel(nb.y, nb.z);
    vec3 bc = mod(nb.y, 2.0) < 0.5 ? gBulbA : gBulbB;
    vec2 bdir = normalize(pw - nb.zw + 1e-5);
    col = rim(col, nb.x - 0.0155, 0.0028, bdir, px, vec3(1.0));
    vec2 bq = (pw - nb.zw) / 0.0128;
    float bz = sqrt(max(0.0, 1.0 - dot(bq, bq)));
    vec3 bulbCol = bc * (0.08 + lv * (0.7 + 1.1 * bz)) + vec3(1.0) * lv * pow(bz, 6.0) * 0.7;
    bulbCol += vec3(0.7) * exp(-dot(bq - vec2(-0.35, 0.4), bq - vec2(-0.35, 0.4)) * 25.0);
    col = mix(col, bulbCol, EwFillAA(nb.x - 0.0128, px));

    // reels
    if (dW < px) col = mix(col, reels(pw, px, dW), EwFillAA(dW, px));
    col += gHalo * (0.06 + 0.3 * GlowAmount) * HaloStrength * mix(1.0, 0.25, smoothstep(0.0, -0.05, dW));
    col += glints(p);

    // crystal orb
    vec2 oc = p - vec2(0.0, gCabTop + 0.03);
    float od = length(oc);
    const float orbR = OrbSize;
    col = rim(col, od - orbR - 0.005, 0.006, oc / max(od, 1e-5), px, vec3(1.0));
    if (od < orbR + 2.0 * px) {
        vec2 on = oc / orbR;
        float z = sqrt(max(0.0, 1.0 - dot(on, on)));
        float sw = EwFbm2D(on * 2.0 + vec2(gTime * 0.3, -gTime * 0.2) * OrbSwirlSpeed, 4);
        vec3 oCol = gMagic * (0.15 + 2.2 * sw * sw) * (0.45 + 0.55 * gEnergy) * (0.4 + 0.6 * GlowAmount);
        oCol += gMagic * pow(1.0 - z, 2.0) * 0.8;
        oCol += vec3(1.0) * pow(max(dot(normalize(vec3(on, z)), normalize(vec3(-0.4, 0.6, 0.7))), 0.0), 40.0);
        col = mix(col, oCol, EwFillAA(od - orbR, px));
    }
    col += gMagic * GlowAmount * (0.12 + 0.3 * gEnergy) * exp(-max(od - orbR - 0.011, 0.0) * 22.0) * smoothstep(orbR + 0.004, orbR + 0.014, od);
    float ray = exp(-abs(oc.x) * 400.0 - abs(oc.y) * 9.0) + exp(-abs(oc.y) * 400.0 - abs(oc.x) * 9.0);
    col += vec3(1.0, 0.95, 1.0) * ray * min(gFlash + 0.25 * gPause * gWin, 1.5) * GlowAmount;
    return col;
}

// ---- main: layout (machine fitted to the screen), cycle timing, win check, render, post ----
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    float halfW = 0.5 * iResolution.x / iResolution.y;
    float zoom = min(1.0, halfW / 0.8);
    vec2 p = uv / zoom;
    float px = 1.0 / (iResolution.y * zoom);
    float halfWp = halfW / zoom;

    float hue = MagicHue;
    gCab = max(EwHueRotate(CabinetColor, hue), 0.0);
    gBulbA = max(EwHueRotate(BulbColorA, hue), 0.0);
    gBulbB = max(EwHueRotate(BulbColorB, hue), 0.0);
    gMagic = max(EwHueRotate(MagicColor, hue), 0.0);

    // layout: reel window as large as possible, VisibleRows symbols tall
    // vertical fit: orb top 1 px below the screen top, cabinet bottom near the screen bottom
    gCabH.x = clamp(halfWp - 0.03, 0.75, 1.25);
    float topLim = 0.5 / zoom - px - (OrbSize + 0.041);   // highest cabinet top (orb + its chrome ring reach above it)
    float botLim = -0.5 / zoom + 0.006;
    float availH = 0.5 * (topLim - botLim);
    gCabH.y = min(availH, 0.6);
    gCabTop = topLim - (availH - gCabH.y);
    gCabCY = gCabTop - gCabH.y;
    gWinCY = gCabCY;
    gWinH.x = gCabH.x - 0.115;
    float hyMax = gCabH.y - 0.115;
    float sA = sin(ReelCurve);
    gSymH = min(hyMax * 2.0 * ReelCurve / (VisibleRows * sA), 0.2 * gWinH.x / 0.7);
    gWinH.y = min(hyMax, VisibleRows * gSymH * sA / (2.0 * ReelCurve));
    gK = gWinH.y / (gSymH * sA);
    gBulbH = gWinH + 0.064;

    gN = max(1.0, min(UniqueCount, TileCount));
    float cl = cycleLength();
    gCyc = floor(iTime / cl);
    gTc = iTime - gCyc * cl;
    gTime = mod(iTime, 600.0);

    // win check on the middle row
    float mids[5];
    for (int i = 0; i < 5; i++) mids[i] = symbolImage(reelTarget(gCyc, float(i)), float(i));
    gWin = 0.0;
    for (int i = 0; i < 5; i++) {
        float m = 0.0;
        for (int j = 0; j < 5; j++) if (j != i && mids[j] == mids[i]) m = 1.0;
        gWinR[i] = m;
        gWin = max(gWin, m);
    }

    float tEnd = reelLandT(4.0);
    gFlash = 0.0;
    for (int i = 0; i < 5; i++) {
        float tf = gTc - reelLandT(float(i));
        gLand[i] = tf > 0.0 ? exp(-tf * FlashDecay) : 0.0;
        gFlash += gLand[i];
    }
    gPause = smoothstep(tEnd + 0.2, tEnd + 0.5, gTc);
    gSpin = smoothstep(CycleLead - 0.1, CycleLead + 0.2, gTc) * (1.0 - smoothstep(tEnd, tEnd + 0.3, gTc));
    gIdle = 1.0 - max(gSpin, gPause);
    float spin = smoothstep(CycleLead, CycleLead + SpinUp, gTc) * (1.0 - smoothstep(reelStopT(0.0), tEnd, gTc));
    gEnergy = 0.5 * spin + gFlash + gPause * (gWin > 0.5 ? 0.7 + 0.3 * sin(gTc * 15.0) : 0.3);

    vec3 col = scene(p, px);

    vec2 suv = fragCoord / iResolution.xy;
    col *= EwVignette(suv, 16.0, 0.15, Vignette);
    col = col / (1.0 + max(col - 0.8, 0.0));
    col = clamp(col, 0.0, 1.0);
    col += EwDither(fragCoord, iTime, 1.0);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
