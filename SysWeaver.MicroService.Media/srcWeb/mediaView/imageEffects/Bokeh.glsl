//  Bokeh - waves of lens blur sweep across the picture
//
//  Soft diagonal waves travel over the image. At each crest the picture melts
//  into a bright bokeh blur (the brightest of a disc of samples, so highlights
//  bloom into round discs); between the crests it stays crisp.
//
//  iMouse.x = Blur Size | Bokeh radius at the wave crests | 0.5 | 0.0 .. 0.8
//  iMouse.y = Wave Sharpness | Higher = narrower blur bands, more sharp picture between them | 0.5 | 1.0 .. 15.0
//  iMouse.z = Wave Density | How many blur bands cross the screen (0 = whole picture pulses) | 0.5 | 0.0 .. 0.002
//  iMouse.w = Grain | Animated film grain amount | 0.25 | 0.0 .. 0.4

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

// --- Live controls ---
#define BlurSize mix(0.0, 0.8, iMouse.x)
#define WaveSharpness mix(1.0, 15.0, iMouse.y)
#define WaveDensity mix(0.0, 0.002, iMouse.z)
#define GrainAmount mix(0.0, 0.4, iMouse.w)

// --- Wave motion ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Speed", "desc": "How fast the blur waves travel across the picture", "unit": "x" }
const float DirX = -1.0; // var: { "min": -5.0, "max": 5.0, "step": 0.1, "name": "Direction X", "desc": "Horizontal part of the wave direction (larger magnitude = denser bands horizontally)" }
const float DirY = 1.0; // var: { "min": -5.0, "max": 5.0, "step": 0.1, "name": "Direction Y", "desc": "Vertical part of the wave direction (larger magnitude = denser bands vertically)" }

// --- Quality ---
const int Samples = 50; // var: { "min": 8, "max": 100, "step": 1, "name": "Samples", "desc": "Bokeh samples per pixel: more = larger, fuller discs, but slower" }

// --- Vignette ---
const float VingetteIntensity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Vignette Intensity", "desc": "Darkening towards the screen edges, 0 = off" }
const float VingetteSpread = 16.0; // var: { "min": 10.0, "max": 1000.0, "step": 10.0, "name": "Vignette Spread", "desc": "Higher = the darkening is pushed further out to the edges" }
const float VingettePow = 0.5; // var: { "min": 0.1, "max": 10.0, "step": 0.1, "name": "Vignette Power", "desc": "Falloff curve: lower = flatter center and sharper edge" }

// --- Corner opacity ---
const float TopLeftO = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Top Left Opacity", "desc": "Opacity of the output in the top left corner" }
const float TopRightO = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Top Right Opacity", "desc": "Opacity of the output in the top right corner" }
const float BottomRightO = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Bottom Right Opacity", "desc": "Opacity of the output in the bottom right corner" }
const float BottomLeftO = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Bottom Left Opacity", "desc": "Opacity of the output in the bottom left corner" }

// Grain, vignette and corner fades on the premultiplied color
vec4 PostProcess(vec4 color, vec2 fragCoord, vec2 uv)
{
    // Wrapped time keeps the grain hash precise in long sessions
    color.rgb *= EwFilmGrain(fragCoord * vec2(-13.0, 17.0), mod(iTime, 64.0), 1.0, GrainAmount);
    if (VingetteIntensity > 0.0)
        color *= EwVignette(uv, VingetteSpread, VingettePow, VingetteIntensity);
    color *= EwCornerOpacity(uv, vec4(BottomLeftO, BottomRightO, TopLeftO, TopRightO));
    color.rgb = clamp(color.rgb, 0.0, color.a);
    return color;
}

// Travelling blur wave: gather a spiral disc of samples, keep the brightest
void mainImage(out vec4 O, vec2 I)
{
    vec2 r = iResolution.xy;
    vec2 uv = I / r;

    // Wave along the direction: 0 = sharp, 1 = full blur at the crest
    float phase = mod(iTime * Speed, EW_TAU) + dot(I, vec2(DirX, DirY)) * WaveDensity;
    float wave = pow(0.5 - 0.5 * cos(phase), WaveSharpness);
    float dof = wave * BlurSize * (r.x + r.y);  // blur scale, relative to the view size

    // Largest disc offset in pixels; when tiny, one fetch gives the same result
    float maxOffset = dof * sqrt(float(Samples - 1)) / 600.0;
    if (maxOffset < 0.002)
    {
        O = texture(tex, uv);
    }
    else
    {
        O = vec4(0.0);
        vec2 i = vec2(0.0, 11.0);  // sin(i + a) = (sin a, ~cos a): a direction per sample
        for (int j = 0; j < Samples; ++j)
        {
            vec2 dP = sin(i + i.x * 1.4) / 6e2 * sqrt(i.x);  // spiral disc, radius grows with sqrt
            i += 1.0;
            // Brightest sample (L-infinity): highlights spread into bokeh discs
            O = max(O, texture(tex, (I + dof * dP) / r));
        }
    }

    O.rgb *= O.a;  // straight alpha picture -> premultiplied output
    O = PostProcess(O, I, uv);
}
