

// Glitch - digital signal corruption over a picture.
// Several times per second random horizontal slices of the image are torn
// sideways, and one color channel (R, G or B) is shifted against the others,
// like a broken video feed or a corrupted JPEG.
//
// Live controls:
//  iMouse.x = Glitch Amount | Overall strength: number of torn slices, tear distance, color split | 0.2 | 0 .. 1
//  iMouse.y = Tear Distance | Multiplier for how far slices shift sideways | 0.5 | 0 .. 2 | {0}x
//  iMouse.z = Color Split | Multiplier for the RGB channel offset | 0.5 | 0 .. 2 | {0}x
//  iMouse.w = Slice Height | Maximum height of a torn slice (share of the screen) | 0.5 | 0 .. 0.5

uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;

uniform sampler2D tex;

// --- Glitch ---
const float GlitchRate = 36.0; // var: { "min": 1.0, "max": 60.0, "step": 1.0, "name": "Glitch Rate", "desc": "New random glitch patterns per second (low = slow stutter, high = frantic flicker)", "unit": "/s" }
const float SliceDensity = 10.0; // var: { "min": 0.0, "max": 20.0, "step": 1.0, "name": "Slice Density", "desc": "Torn slices at full Glitch Amount (more = busier)" }
const float TearScale = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Tear Scale", "desc": "Maximum sideways slice shift per unit of Glitch Amount (share of the width)" }
const float SplitScale = 0.1666667; // var: { "min": 0.0, "max": 0.5, "step": 0.005, "name": "Split Scale", "desc": "Maximum color channel offset per unit of Glitch Amount (share of the screen)" }

// --- Fine tuning ---
const float Seed = 0.0; // var: { "min": 0.0, "max": 100.0, "step": 1.0, "name": "Seed", "desc": "Picks a different random sequence of glitches" }

// Live controls
#define Amount      mix(0.0, 1.0, iMouse.x)
#define TearAmount  mix(0.0, 2.0, iMouse.y)
#define SplitAmount mix(0.0, 2.0, iMouse.z)
#define SliceHeight mix(0.0, 0.5, iMouse.w)

const float MAX_SLICES = 20.0; // loop bound = max of SliceDensity
#define MAX_SLICES 20.0 // loop bound = max of SliceDensity
// Sine hash, vec2 -> [0, 1) (own constants: keeps the original glitch sequence)
float random2d(vec2 n) {
    return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

// Random value in [lo, hi)
float randomRange(vec2 seed, float lo, float hi) {
    return lo + random2d(seed) * (hi - lo);
}

void main()
{
    // Glitch frame index: changes GlitchRate times per second (time wrapped for precision)
    float tt = floor(mod(time, 3600.0) * GlitchRate) + Seed;
    vec2 uv = gl_FragCoord.xy / resolution.xy;

    vec4 outCol = texture2D(tex, uv);

    // Randomly offset horizontal slices (a slice crossing the top edge is skipped)
    float maxOffset = Amount * TearScale * TearAmount;
    float sliceCount = SliceDensity * Amount;
    float found = -1.0;
    for (float i = 0.0; i < MAX_SLICES; i += 1.0) {
        if (i >= sliceCount) break;
        float sliceY = random2d(vec2(tt, 2345.0 + i));
        float sliceH = random2d(vec2(tt, 9035.0 + i)) * SliceHeight;
        float top = fract(sliceY + sliceH);
        if (step(sliceY, uv.y) - step(top, uv.y) == 1.0) found = i; // last slice wins
    }
    // One fetch for the winning slice instead of one per slice
    if (found >= 0.0) {
        float hOffset = randomRange(vec2(tt, 9625.0 + found), -maxOffset, maxOffset);
        outCol = texture2D(tex, uv + vec2(hOffset, 0.0));
    }

    // Shift one whole color channel (chosen at random) against the others
    float maxColOffset = Amount * SplitScale * SplitAmount;
    float rnd = random2d(vec2(tt, 9545.0));
    vec2 colOffset = vec2(randomRange(vec2(tt, 9545.0), -maxColOffset, maxColOffset),
                          randomRange(vec2(tt, 7205.0), -maxColOffset, maxColOffset));
    vec4 shifted = texture2D(tex, uv + colOffset);
    if (rnd < 0.33)      outCol.r = shifted.r;
    else if (rnd < 0.66) outCol.g = shifted.g;
    else                 outCol.b = shifted.b;

    // Premultiplied alpha output
    outCol.rgb *= outCol.a;
    gl_FragColor = outCol;
}
