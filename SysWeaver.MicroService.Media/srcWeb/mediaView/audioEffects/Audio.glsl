// Audio - spectral cross pulse
// Concentric rings driven by the music spectrum (radius = frequency),
// crossed by a glowing diagonal X; colors drift with screen position and time.
// Calm in silence: the diagonal glow and color drift stay alive.
//
// Live controls:
//  iMouse.x = Brightness | Overall light intensity | 0.5 | 0.0 .. 6.0 | x
//  iMouse.y = Ring Density | Number of concentric ripples | 0.5 | 4.0 .. 28.0
//  iMouse.z = Spectrum Zoom | Radius range mapped to the spectrum (higher = more bass) | 0.5 | 4.0 .. 28.0
//  iMouse.w = Color Shift | Shifts the blue / yellow balance | 0.5 | -2.0 .. 2.0

// --- Colors ---
const float ColorPulseSpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Color Pulse Speed", "desc": "Speed of the blue / yellow color pulse", "unit": "x" }

// --- Spectrum ---
const float SpectrumBlur = 0.1; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Spectrum Blur", "desc": "Smoothing of the spectrum across radius (higher = softer rings)" }

// --- Diagonal glow ---
const float DiagonalGlow = 0.1; // var: { "min": 0.01, "max": 0.5, "step": 0.01, "name": "Diagonal Glow", "desc": "Strength of the glowing X across the screen" }

#define Brightness   mix(0.0, 6.0, iMouse.x)
#define RingDensity  mix(4.0, 28.0, iMouse.y)
#define SpectrumZoom mix(4.0, 28.0, iMouse.z)
#define ColorShift   mix(-2.0, 2.0, iMouse.w)

// Inverted logistic: 1 / (1 + e^x), maps any value to (0, 1)
float sigmoid(float x) { return 1. / (1. + exp(x)); }
vec3 sigmoid(vec3 v) { return 1. / (1. + exp(v)); }

// Spectrum level at radius-like coordinate f
float sample_at(float f)
{
    return texture(iChannel0, vec2(f / SpectrumZoom, 0.)).x;
}

// 5-tap box blur of the spectrum around f
float sample_multiple(float f)
{
    float delta = SpectrumBlur;
    return 0.2 * (sample_at(f - 2. * delta) + sample_at(f - delta) + sample_at(f) + sample_at(f + delta) + sample_at(f + 2. * delta));
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 uv = (fragCoord.xy - 0.5) / iResolution.xy;
    uv = 2. * uv - 1.;
    uv.x *= iResolution.x/iResolution.y;

    float d = length(uv);

    // Audio level at this radius pushes the rings outward
    float amplitude = sample_multiple(d * d);
    d -= amplitude;
    float weird = sigmoid(abs(uv.x) * abs(uv.y));
    float speed = 6. * amplitude * sin(iTime * weird * 0.005) * 0.001;
    // Glowing X along the diagonals, bent by the audio
    float dist_diagonal = abs(abs(uv.x) - abs(uv.y));
    dist_diagonal += d * amplitude;
    dist_diagonal *= dist_diagonal;
    amplitude += DiagonalGlow / (DiagonalGlow + smoothstep(1., 0.1, dist_diagonal));
    float brightness = Brightness * amplitude * sigmoid(sin(d * d * RingDensity - speed * iTime + 2. * speed * amplitude));

    // Position-based color with a pulsing blue channel
    vec3 col = sigmoid(vec3(uv, sin(iTime * ColorPulseSpeed) + ColorShift));

    fragColor = vec4(col * brightness, 1.0);
}
