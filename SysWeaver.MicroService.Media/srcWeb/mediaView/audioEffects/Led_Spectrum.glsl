//  media: audio stock:YuE2_00010.flac
/*
2D LED Spectrum - Visualiser
A classic LED-matrix spectrum analyser: the music's frequency spectrum drives a grid of
square LEDs, bass on the left, highs on the right. Lit LEDs fade from green (low) to red (top),
unlit LEDs stay as a dim ghost grid.

Live controls (iMouse, each 0..1) are listed below the credits.

Based on Led Spectrum Analyser by: simesgreen - 27th February, 2013 https://www.shadertoy.com/view/Msl3zr
2D LED Spectrum by: uNiversal - 27th May, 2015
Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
*/

//  iMouse.x = Sensitivity | How high the bars rise for a given loudness | 0.5 | 0.25 .. 1.75 | x
//  iMouse.y = Brightness | Overall LED brightness | 1.0 | 0.0 .. 2.0 | x
//  iMouse.z = Ghost LEDs | Brightness of the unlit LEDs | 0.1 | 0.0 .. 0.2
//  iMouse.w = Color Shift | Moves the green-to-red transition down (-) or up (+) | 0.0 | -0.5 .. 0.5
#define Sensitivity mix(0.25, 1.75, iMouse.x)
#define Brightness mix(0.0, 2.0, iMouse.y)
#define GhostLevel mix(0.0, 0.2, iMouse.z)
#define ColorShift mix(-0.5, 0.5, iMouse.w)


// --- Colors ---
const float HueBottom = 123.0; // var: { "min": -720.0, "max": 720.0, "step": 1.0, "name": "Hue Bottom", "desc": "HSV hue of the LEDs at the bottom of the bars (0 red, 120 green, 240 blue; beyond 0..360 wraps, a wide gap to Hue Top gives a rainbow)", "unit": "°" }
const float HueTop = -145.0; // var: { "min": -720.0, "max": 720.0, "step": 1.0, "name": "Hue Top", "desc": "HSV hue of the LEDs at the top of the bars", "unit": "°" }
const float HueShift = 0.0; // var: { "min": -720.0, "max": 720.0, "step": 1.0, "name": "Hue Shift", "desc": "Hue offset added across the bars: 0 at the left bar, the full shift at the right bar", "unit": "°" }
const float HueDrift = 0.0; // var: { "min": -180.0, "max": 180.0, "step": 1.0, "name": "Hue Drift", "desc": "Hue change over time, added to all hues", "unit": "°/s" }
const float Saturation = 0.9; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Saturation", "desc": "HSV saturation of the LEDs", "scale": 100, "unit": "%" }
const float LedIntensity = 0.55; // var: { "min": 0.5, "max": 3.0, "step": 0.05, "name": "LED Intensity", "desc": "Base brightness of the lit LEDs (above 1 the colors burn out and look more saturated and wider)" }
const float ColorCurve = 0.5; // var: { "min": 0.2, "max": 2.0, "step": 0.05, "name": "Color Curve", "desc": "Gradient bias: below 1 reaches the high color early, above 1 keeps the low color longer" }

// --- Grid ---
const float Bands = 30.0; // var: { "min": 8.0, "max": 120.0, "step": 1.0, "name": "Bands", "desc": "Number of frequency columns" }
const float Segments = 40.0; // var: { "min": 8.0, "max": 120.0, "step": 1.0, "name": "Segments", "desc": "Number of LEDs per column" }
const float FreqLow = 0.12; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Lowest Frequency", "desc": "Where the left column starts on the log frequency axis (0 = 20 Hz, 0.1 = 40 Hz, 0.23 = 100 Hz)" }
const float FreqHigh = 0.92; // var: { "min": 0.5, "max": 1.0, "step": 0.01, "name": "Highest Frequency", "desc": "Where the right column ends on the log frequency axis (0.77 = 4 kHz, 0.9 = 10 kHz, 1 = 20 kHz)" }
const float LedSize = 0.5; // var: { "min": 0.2, "max": 0.5, "step": 0.01, "name": "LED Size", "desc": "Size of the lit area inside each cell (smaller = more gap between LEDs)" }
const float LedSoftness = 0.15; // var: { "min": 0.01, "max": 0.3, "step": 0.01, "name": "LED Softness", "desc": "Softness of the LED edges" }
// --- Reflection ---
const float ReflectionHeight = 0.25; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Reflection Height", "desc": "Share of the screen height used by the mirrored reflection at the bottom (0 = no reflection)", "scale": 100, "unit": "%" }
const float ReflectionStrength = 0.18; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Reflection Strength", "desc": "Brightness of the reflection at the floor line", "scale": 100, "unit": "%" }
const float ReflectionFalloff = 1.0; // var: { "min": 0.25, "max": 4.0, "step": 0.05, "name": "Reflection Falloff", "desc": "How fast the reflection fades towards the bottom (higher = faster)" }
const float ReflectionBlur = 0.1; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Reflection Blur", "desc": "Diffuseness of the reflection: blur radius grows with the distance to the floor (0 = sharp mirror)" }
const int ReflectionSamples = 9; // var: { "min": 3, "max": 24, "step": 1, "name": "Reflection Samples", "desc": "Horizontal blur taps in the reflection (more = smoother, slower)" }

// The level of the spectrum at x (0 = left .. 1 = right column) on the log frequency axis (the 128 band row of the audio texture)
float Level(float x)
{
    return EwAudioGetValue(iChannel0, mix(FreqLow, FreqHigh, x), EwAudioFrequency128v, false);
}

// Draws a grid of LEDs; each column lights up to the level of its frequency band.
void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    // create pixel coordinates
    vec2 uv = fragCoord.xy / iResolution.xy;

    // split the screen: bars above ReflectionHeight, their mirror image below it.
    // y = height inside the bar area (0 at the floor line, 1 at the top); the reflection
    // mirrors it at the same LED scale and fades out towards the bottom of the screen.
    float floorY = clamp(ReflectionHeight, 0.0, 0.9);
    float barH = 1.0 - floorY;
    float y = (uv.y - floorY) / barH;
    float reflFade = 1.0;
    bool isRefl = y < 0.0;
    if (isRefl) {
        y = -y;
        float depth = (floorY - uv.y) / max(floorY, 1e-4); // 0 at the floor line, 1 at the bottom
        reflFade = ReflectionStrength * pow(clamp(1.0 - depth, 0.0, 1.0), ReflectionFalloff);
    }

    // quantize coordinates
    vec2 p;
    p.x = floor(uv.x*Bands)/Bands;
    p.y = floor(y*Segments)/Segments;

    // led shape: rounded square inside each grid cell (separable x / y profiles)
    vec2 d = fract(vec2(uv.x, y) * vec2(Bands, Segments)) - 0.5;
    float ledX = smoothstep(LedSize, LedSize - LedSoftness, abs(d.x));
    float ledY = smoothstep(LedSize, LedSize - LedSoftness, abs(d.y));

    // led color: HSV hue gradient from HueBottom to HueTop along the height,
    // shifted per column (0 at the left bar, HueShift at the right bar) and drifting over time
    float t = clamp(pow(y, ColorCurve) + ColorShift, 0.0, 1.0);
    float drift = mod(iTime * HueDrift, 360.0);
    float hueY = mix(HueBottom, HueTop, t) + drift;
    float colNorm = 1.0 / max(Bands - 1.0, 1.0);

    vec3 col = vec3(0.0);

    if (!isRefl) {
        // read frequency data from first row of texture
        float fft = Level(p.x) * Sensitivity;
        // mask for bar graph: lit LEDs below the level, ghost LEDs above
        float mask = (p.y < fft) ? 1.0 : GhostLevel;
        float colX = p.x * Bands * colNorm;
        vec3 color = EwHsvToRgb(vec3((hueY + HueShift * colX) / 360.0, Saturation, 1.0));
        col = ledX * ledY * mask * color;
    } else {
        // Diffuse reflection: the blur radius grows with the distance to the floor (= mirrored height y).
        // Vertical: analytic box filter of the lit / ghost edge. Horizontal: a few weighted taps
        // across the blur width, each with its own band level, LED column shape and hue.
        float ry = ReflectionBlur * y;                                // in bar heights
        float rx = ry * barH * iResolution.y / iResolution.x;         // same screen distance in x
        float ryS = max(ry, 0.5 / Segments);

        // LED shape coverage, faded to its average where the blur (or tap spacing) exceeds a cell
        float avg = clamp(2.0 * LedSize - LedSoftness, 0.0, 1.0);
        float ky = smoothstep(0.0, 1.0, ry * Segments * 2.0);
        float shapeY = mix(ledY, avg, ky);
        // column gaps fade out once the blur radius reaches ~half a band (taps stay dense enough before)
        float kx = smoothstep(0.1, 0.6, rx * Bands);

        // loop invariants: inverse edge width, tap spacing; without HueShift the hue is the same for all taps
        float invEdge = 0.5 / ryS;
        float yEdge = 0.5 - y * invEdge;
        float sStep = 2.0 / float(ReflectionSamples - 1);
        vec3 baseColor = EwHsvToRgb(vec3(hueY / 360.0, Saturation, 1.0));
        float hueStep = HueShift * colNorm / 360.0;

        // A blur radius up to 1 band only reaches the centre band and its 2 neighbours:
        // fetch those 3 levels once (instead of one fetch per tap) and pick per tap.
        float bc = floor(uv.x * Bands);
        bool nearBands = rx * Bands <= 1.0;
        vec3 m3 = vec3(GhostLevel);
        if (nearBands) {
            for (int j = 0; j < 3; j++) {
                float bj = clamp(bc + float(j - 1), 0.0, Bands - 1.0);
                float top = ceil(Level(bj / Bands) * Sensitivity * Segments) / Segments;
                float lit = clamp(top * invEdge + yEdge, 0.0, 1.0);
                m3[j] = mix(GhostLevel, 1.0, lit * lit * (3.0 - 2.0 * lit));
            }
        }

        float wSum = 0.0;
        for (int i = 0; i < ReflectionSamples; i++) {
            float s = float(i) * sStep - 1.0;                               // -1 .. 1
            float w = exp(-2.5 * s * s);                                    // gaussian weights (constant folded)
            float xb = (uv.x + s * rx) * Bands;                             // tap position in bands
            float fb = floor(xb);
            float bi = clamp(fb, 0.0, Bands - 1.0);
            float mask;
            if (nearBands) {
                float off = fb - bc;                                        // -1, 0 or 1
                mask = off < -0.5 ? m3.x : (off > 0.5 ? m3.z : m3.y);
            } else {
                float top = ceil(Level(bi / Bands) * Sensitivity * Segments) / Segments;
                float lit = clamp(top * invEdge + yEdge, 0.0, 1.0);
                mask = mix(GhostLevel, 1.0, lit * lit * (3.0 - 2.0 * lit));
            }
            float lx = smoothstep(LedSize, LedSize - LedSoftness, abs(fract(xb) - 0.5));
            lx = mix(lx, avg, kx);
            float wm = w * mask * lx;
            if (HueShift == 0.0) {
                col.x += wm;                                                // scalar accumulate, color applied once
            } else {
                col += wm * EwHsvToRgb(vec3(hueY / 360.0 + hueStep * bi, Saturation, 1.0));
            }
            wSum += w;
        }
        if (HueShift == 0.0) col = col.x * baseColor;
        col *= shapeY / wSum;
    }

    vec3 ledColor = col*(LedIntensity*Brightness)*reflFade;

    // output final color
    fragColor = vec4(clamp(ledColor, 0.0, 1.0), 1.0);
}
