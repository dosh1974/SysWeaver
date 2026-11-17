// Picture Grade - a clean, live-gradable presenter for one picture.
// Shows the chosen image full view (upright, filling the screen) and lets you grade it:
// exposure, contrast, saturation, hue, tint, gamma, zoom / pan, vignette and opacity.
// With the defaults the picture is shown exactly as it is (transparency kept).
//
// Live controls:
//  iMouse.x = Exposure | Brightness in photographic stops | 0.5 | -2.0 .. 2.0 | stops
//  iMouse.y = Contrast | Contrast around mid grey (0 = flat grey, 2 = punchy) | 0.5 | 0.0 .. 2.0 | x
//  iMouse.z = Saturation | Color intensity (0 = black and white, 2 = vivid) | 0.5 | 0.0 .. 2.0 | x
//  iMouse.w = Hue Shift | Rotates all colors around the color wheel | 0.5 | -180.0 .. 180.0 | °

uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;

uniform sampler2D tex;

// --- Color ---
const vec3 Tint = vec3(1.0, 1.0, 1.0); // var: { "type": "col", "name": "Tint", "desc": "Color filter multiplied over the picture (white = none)" }
const float Gamma = 1.0; // var: { "min": 0.4, "max": 2.5, "step": 0.01, "name": "Gamma", "desc": "Midtone brightness: above 1 brightens shadows / midtones, below 1 darkens them" }

// --- Framing ---
const float Zoom = 1.0; // var: { "min": 1.0, "max": 4.0, "step": 0.01, "name": "Zoom", "desc": "Magnifies the picture around the center", "unit": "x" }
const vec2 Pan = vec2(0.0, 0.0); // var: { "min": -0.5, "max": 0.5, "step": 0.01, "name": "Pan", "desc": "Moves the visible part of the picture (fractions of its size, useful when zoomed in)" }

// --- Finish ---
const float Vignette = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Vignette", "desc": "Darkens the edges of the view", "scale": 100.0, "unit": "%" }
const float VignetteFalloff = 0.25; // var: { "min": 0.05, "max": 1.0, "step": 0.01, "name": "Vignette Falloff", "desc": "Low = flat center with a sharp dark rim, high = gradual darkening from the center" }
const float Opacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Overall opacity of the picture (transparent output below 100 %)", "scale": 100.0, "unit": "%" }

// Live controls (defaults 0.5 = picture unchanged)
#define Exposure mix(-2.0, 2.0, iMouse.x)
#define Contrast mix(0.0, 2.0, iMouse.y)
#define Saturation mix(0.0, 2.0, iMouse.z)
#define HueShift mix(-180.0, 180.0, iMouse.w)

void main()
{
    vec2 screenUv = gl_FragCoord.xy / resolution.xy;

    // Zoom around the center, then pan; outside the picture stays transparent
    vec2 uv = (screenUv - 0.5) / Zoom + 0.5 + Pan;
    vec4 color = texture2D(tex, uv);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) color = vec4(0.0);

    // Grade the straight (non premultiplied) color
    vec3 c = color.rgb * exp2(Exposure);
    c = (c - 0.5) * Contrast + 0.5;
    c = EwSaturation(c, Saturation);
    c = EwHueRotate(c, radians(HueShift));
    c = clamp(c * Tint, 0.0, 1.0);
    c = pow(c, vec3(1.0 / Gamma));

    // Optional edge darkening (strength 0 = off)
    c *= EwVignette(screenUv, 16.0, VignetteFalloff, Vignette);

    // Premultiplied alpha output
    float a = color.a * Opacity;
    gl_FragColor = vec4(c * a, a);
}
