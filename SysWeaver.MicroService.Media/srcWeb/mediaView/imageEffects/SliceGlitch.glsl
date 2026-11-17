// Slice Glitch - digital video glitch for one picture
// The picture shakes, horizontal slices tear sideways with a chromatic RGB
// split, CRT scanlines run across and the frame occasionally jumps.
//
// Live controls:
//  iMouse.x = Glitch Amount | How far glitched slices shift sideways | 0.36 | 0.0 .. 0.5
//  iMouse.y = Glitch Density | Share of slices glitched at once | 0.3 | 0.0 .. 1.0 | {0:2}
//  iMouse.z = RGB Split | Base chromatic separation of the channels | 0.2 | 0.0 .. 0.02
//  iMouse.w = Shake | Strength of the whole frame jitter | 0.2 | 0.0 .. 0.03

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

// --- Live controls ---
#define GlitchAmount mix(0.0, 0.5, iMouse.x)
#define GlitchDensity mix(0.0, 1.0, iMouse.y)
#define RgbSplit mix(0.0, 0.02, iMouse.z)
#define Shake mix(0.0, 0.03, iMouse.w)

// --- Glitch ---
const float BlockSize = 25.0;           // var: { "min": 4.0, "max": 100.0, "step": 1.0, "name": "Slice Count", "desc": "Number of horizontal slices over the picture height: more = thinner slices" }
const float NoiseSpeed = 10.0;          // var: { "min": 1.0, "max": 30.0, "step": 0.5, "name": "Glitch Rate", "desc": "How often per second the glitched slices are re-rolled", "unit": "/s" }
const float RgbBoost = 0.03;            // var: { "min": 0.0, "max": 0.1, "step": 0.002, "name": "Glitch RGB Boost", "desc": "Extra chromatic split on glitched slices" }

// --- Scanlines ---
const float ScanlineStrength = 0.04;    // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Scanline Strength", "desc": "Darkness of the CRT scanlines" }
const float ScanlineDensity = 800.0;    // var: { "min": 100.0, "max": 2000.0, "step": 10.0, "name": "Scanline Density", "desc": "Scanline frequency over the picture height: higher = finer lines" }

// --- Frame tear ---
const float TearProbability = 0.05;     // var: { "min": 0.0, "max": 0.3, "step": 0.01, "name": "Tear Probability", "desc": "Chance that the whole frame jumps in a tear step", "scale": 100, "unit": "%" }
const float TearRate = 5.0;             // var: { "min": 1.0, "max": 20.0, "step": 0.5, "name": "Tear Rate", "desc": "Tear steps per second (how often a jump can happen and how long it lasts)", "unit": "/s" }
const float TearOffset = 0.05;          // var: { "min": -0.3, "max": 0.3, "step": 0.01, "name": "Tear Offset", "desc": "Vertical jump of the picture during a tear (share of its height)" }

// Sine hash: float -> [0, 1)
float rand(float n) {
    return fract(sin(n) * 43758.5453123);
}

// Slice glitch: shaken picture, torn into horizontal slices with RGB split,
// scanlines and occasional full frame vertical jumps.
void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / resolution.xy;
    float t = time;

    // Whole frame camera shake: a new random offset every frame
    uv.x += (rand(t) - 0.5) * Shake;
    uv.y += (rand(t * 2.0) - 0.5) * Shake;

    // Horizontal slice index
    float blockY = floor(uv.y * BlockSize);

    // Is this slice glitched in the current glitch step? (re-rolled NoiseSpeed times / s)
    float trigger = step(1.0 - GlitchDensity, rand(blockY + floor(t * NoiseSpeed)));

    // Random sideways displacement of glitched slices
    float offset = (rand(blockY + t) - 0.5) * GlitchAmount * trigger;

    vec2 uvGlitch = uv;
    uvGlitch.x += offset;

    // Chromatic split: base separation, stronger on glitched slices
    float shift = RgbSplit + trigger * RgbBoost;

    vec2 rUV = uvGlitch + vec2( shift, 0.0);
    vec2 gUV = uvGlitch;
    vec2 bUV = uvGlitch - vec2( shift, 0.0);

    vec4 color = texture2D(tex, gUV);
    color.r = texture2D(tex, rUV).r;
    color.b = texture2D(tex, bUV).b;

    // CRT style scanlines
    float scan = sin(uv.y * ScanlineDensity) * ScanlineStrength;
    color.xyz -= scan;

    // Rare full frame tear: the plain picture jumps vertically for one tear step
    float tear = step(1.0 - TearProbability, rand(floor(t * TearRate)));
    if (tear > 0.0) {
        color = texture2D(tex, uv + vec2(0.0, TearOffset));
    }

    // Straight -> premultiplied alpha
    color.rgb *= color.a;
    fragColor = color;
}
