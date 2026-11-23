//  media: audio
//  iMouse.x = Reactivity | How strongly the lines bend with the spectrum | 0.5 | 0.0 .. 1.6
//  iMouse.y = Glow | Intensity of the neon glow | 0.5 | 0.0 .. 2.0
//  iMouse.z = Hue Shift | Rotates the palette | 0.0 | 0.0 .. 1.0
//  iMouse.w = Grid Scale | Zoom of the grid (line density) | 0.5 | 0.6 .. 1.6
#define Reactivity mix(0.0, 1.6, iMouse.x)
#define GlowAmount mix(0.0, 2.0, iMouse.y)
#define HueShift mix(0.0, 1.0, iMouse.z)
#define GridScale mix(0.6, 1.6, iMouse.w)

const float Speed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Speed", "desc": "Speed of the calm base motion and the drift" }
const float GridDensity = 6.0; // var: { "min": 3.0, "max": 12.0, "step": 0.5, "name": "Grid Density", "desc": "Lines per screen height (before Grid Scale)" }
const float LineWidth = 1.2; // var: { "min": 0.4, "max": 4.0, "step": 0.1, "name": "Line Width", "desc": "Core width of the lines", "unit": "px" }
const float Exposure = 1.0; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness" }

// The spectrum at f on the log frequency axis (the 128 band row of the audio texture)
float spec(float f)
{
    return EwAudioGetValue(iChannel0, clamp(f, 0.0, 1.0), EwAudioFrequency128v, false);
}

// The log frequency of an axis position (0 = center: ~40 Hz .. 1 = edge: ~10 kHz)
float bandF(float a)
{
    return 0.1 + 0.8 * a;
}

float shape(float v)
{
    v = clamp((v - 0.3) / 0.55, 0.0, 1.0);
    return v * v * (3.0 - 2.0 * v);
}

// band energy around a node on the axis (5 tap smoothing of the spectrum)
float bandNode(float k)
{
    float a = k * 0.1;
    float v = 0.0;
    for (int j = -2; j <= 2; j++)
    {
        float w = (j == 0) ? 0.36 : ((j == 1 || j == -1) ? 0.22 : 0.10);
        v += w * spec(bandF(abs(a + float(j) * 0.035)));
    }
    return v;
}

// spectrum level at axis position a (0 = center / bass, 1 = edge / highs):
// smooth interpolation between wide band nodes so the lines stay smooth
float lvl(float a)
{
    float u = clamp(a, 0.0, 1.0) * 10.0;
    float k = min(floor(u), 9.0);
    float s = smoothstep(0.0, 1.0, u - k);
    return shape(mix(bandNode(k), bandNode(k + 1.0), s));
}
// pixel distance to the nearest line of a warped coordinate, anti aliased
float lineDist(float g)
{
    float d = abs(fract(g + 0.5) - 0.5);
    float fw = max(fwidth(g), 1e-4);
    return d / fw * (1080.0 / iResolution.y);
}

vec3 pal(float t)
{
    return EwCosinePalette(t + HueShift, vec3(0.5), vec3(0.5), vec3(1.0), vec3(0.55, 0.70, 0.85));
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    float t = mod(iTime * Speed, 600.0);

    float bass = EwAudioGetBassEnergy(iChannel0);
    bass = smoothstep(0.35, 0.95, bass);

    vec2 p = uv * 2.0 / GridScale;
    p *= 1.0 - 0.03 * bass;
    float maxX = 1.8 / GridScale;
    float maxY = 1.0 / GridScale;
    float ax = clamp(abs(p.x) / maxX, 0.0, 1.0);
    float ay = clamp(abs(p.y) / maxY, 0.0, 1.0);

    // spectrum along each axis (bass in the middle, highs outwards) + calm base motion
    float lx = min(lvl(ax) + 0.10 * (0.5 + 0.5 * sin(ax * 7.0 - t * 1.3)), 1.0);
    float ly = min(lvl(ay) + 0.10 * (0.5 + 0.5 * sin(ay * 7.0 - t * 1.1 + 2.0)), 1.0);
    float A = 0.25 * Reactivity;

    // warped coordinates (smooth, never folding)
    float yw = p.y - A * lx * sin(p.y * 2.2 + t * 0.5 + p.x * 0.6);
    float xw = p.x - A * ly * sin(p.x * 2.2 - t * 0.4 + p.y * 0.6);

    float D = GridDensity;
    float gy = yw * D * 0.5 + t * 0.03;
    float gx = xw * D * 0.5 + t * 0.02;

    float dH = lineDist(gy);
    float dV = lineDist(gx);

    // each line listens to its own frequency band
    float idH = abs(floor(gy + 0.5));
    float idV = abs(floor(gx + 0.5));
    float bH = lvl(clamp(idH / (D * 0.5 / GridScale + 0.001), 0.0, 1.0));
    float bV = lvl(clamp(idV / (D * 0.5 * 1.8 / GridScale + 0.001), 0.0, 1.0));

    float inH = 0.3 + 1.0 * bH + 0.8 * lx;
    float inV = 0.3 + 1.0 * bV + 0.8 * ly;

    float w = LineWidth;
    float coreH = 1.0 - smoothstep(w * 0.5 - 0.5, w * 0.5 + 0.5, dH);
    float coreV = 1.0 - smoothstep(w * 0.5 - 0.5, w * 0.5 + 0.5, dV);
    float glowH = exp(-dH * 0.12) * 0.4 + exp(-dH * 0.035) * 0.07;
    float glowV = exp(-dV * 0.12) * 0.4 + exp(-dV * 0.035) * 0.07;

    vec3 cH = pal(0.00 + ax * 0.35 + p.y * 0.08 + t * 0.02);
    vec3 cV = pal(0.35 + ay * 0.35 + p.x * 0.08 + t * 0.02);

    vec3 col = vec3(0.0);
    col += cH * (coreH * 1.1 + glowH * GlowAmount) * inH;
    col += cV * (coreV * 1.1 + glowV * GlowAmount) * inV;
    // hot white cores where lines are loud
    col += vec3(coreH * lx * lx + coreV * ly * ly) * 0.8;

    // dark backdrop with soft bass glow in the center
    vec3 bg = mix(vec3(0.004, 0.006, 0.016), vec3(0.02, 0.012, 0.045), 1.0 - clamp(length(uv) * 1.2, 0.0, 1.0));
    bg += pal(0.2) * 0.06 * bass * exp(-dot(uv, uv) * 3.0);
    col += bg;

    col *= Exposure;
    col = EwTonemapAces(col);
    col *= EwVignette(fragCoord / iResolution.xy, 16.0, 0.2, 0.7);
    col = EwLinearToSrgb(col);
    fragColor = vec4(col, 1.0);
}
