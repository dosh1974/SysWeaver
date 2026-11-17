// =============================================================================
//  VHS Static - analog tape noise over a picture
// =============================================================================
//  Overlays the picture with flickering TV / VHS static. The static darkens the
//  image (subtractive grain), is re-seeded many times per second and its
//  strength slowly swells and fades between a floor and a peak.
//
//  Live controls:
//  iMouse.x = Static Strength | Peak darkness of the static when the pulse is high | 0.5 | 0.0 .. 1.0
//  iMouse.y = Static Floor | Minimum static that always remains between pulses | 0.25 | 0.0 .. 0.5
//  iMouse.z = Grain Size | Size of one static grain (blocky, low res noise when raised) | 0.0 | 1.0 .. 8.0 | px
//  iMouse.w = Color Noise | Blend from black and white static to colored RGB static | 0.0 | 0.0 .. 1.0
// =============================================================================

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

#define iTime time
#define iResolution resolution
#define iChannel0 tex
#define texture texture2D

// --- Static ---
const float GrainSpeed = 10.0; // var: { "min": 0.0, "max": 30.0, "step": 0.5, "name": "Grain Speed", "desc": "How fast the static pattern flickers (0 = frozen grain)" }
const vec3 StaticTint = vec3(1.0, 1.0, 1.0); // var: { "type": "col", "name": "Static Tint", "desc": "Channels the static darkens: white = neutral grey static, a color removes only those channels" }

// --- Pulse ---
const float PulseSpeed = 0.5; // var: { "min": 0.05, "max": 3.0, "step": 0.05, "name": "Pulse Speed", "desc": "How fast the static strength swells and fades between floor and peak" }

// --- Live controls ---
#define StaticStrength mix(0.0, 1.0, iMouse.x)
#define StaticFloor mix(0.0, 0.5, iMouse.y)
#define GrainSize mix(1.0, 8.0, iMouse.z)
#define ColorNoise mix(0.0, 1.0, iMouse.w)

// Full static sine hash (own constants, keeps the original grain pattern)
float random(vec2 noise)
{
    return fract(sin(dot(noise.xy, vec2(10.998, 98.233))) * 12433.14159265359);
}

void mainImage(out vec4 fragColour, in vec2 fragCoord)
{
    // Wrapped time: 400 pi is a multiple of both default periods (keeps precision)
    float t = mod(iTime, 400.0 * 3.14159265359);

    vec2 uv = fragCoord.xy / iResolution.xy;

    // Grain cell center (size 1 = the pixel itself)
    vec2 cell = (floor(fragCoord.xy / GrainSize) + 0.5) * GrainSize;
    // Re-seed the noise field each frame by scaling the coordinates randomly
    vec2 uv2 = fract(cell / iResolution.xy * fract(sin(t * GrainSpeed)));

    // Strength pulse: sine clamped between floor and peak
    float strength = clamp(sin(t * PulseSpeed), min(StaticFloor, StaticStrength), StaticStrength);

    // Black and white static, optionally blended to per channel RGB static
    vec3 noise = vec3(random(uv2));
    if (ColorNoise > 0.0)
    {
        vec3 rgb = vec3(noise.r, random(uv2 + vec2(0.137, 0.0)), random(uv2 + vec2(0.0, 0.291)));
        noise = mix(noise, rgb, ColorNoise);
    }
    vec3 colour = noise * StaticTint * strength;

    // Picture, darkened by the static (premultiplied output)
    vec4 background = texture(iChannel0, uv);
    fragColour = vec4(background.xyz - colour, background.w);
    fragColour.xyz *= background.w;
}
