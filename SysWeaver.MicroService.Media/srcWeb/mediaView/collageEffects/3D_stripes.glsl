
// Each stripe shows one column of the collage atlas as upright (rotated) rectangles;
// neighbouring stripes move in opposite directions at different speeds,
// the screen edges fade out (vignette).
//
//  iMouse.x = Slant | Tilt of the stripes and pictures (0 = vertical) | 0.5 | -40 .. 80 | °
//  iMouse.y = Picture Size | Zoom of the pictures and stripes | 0.5 | 0.5 .. 1.5 | x
//  iMouse.z = Vignette | Edge fade exponent (0 = off, higher = stronger fade) | 0.5 | 0.0 .. 2.4
//  iMouse.w = Brightness | Picture brightness | 0.5 | 0.0 .. 2.0 | x

precision highp float;

uniform sampler2D tex;
uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;

// --- Collage layout (set by the editor from the chosen pictures) ---
const float TileCountX = 6.0;   //var:
const float TileCountY = 3.0;   //var:
const float TileWidth = 168.0;  //var:
const float TileHeight = 98.0;  //var:

// --- Stripes ---
const float StripeWidthScale = 1.0; // var: { "min": 0.25, "max": 4.0, "step": 0.05, "name": "Stripe Width", "desc": "Stripe width relative to one atlas tile: lower = more, narrower stripes", "unit": "x" }

// --- Motion ---
const float ScrollSpeed = 1.0;      // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Scroll Speed", "desc": "Overall speed of the up / down sliding stripes (0 = frozen)", "unit": "x" }
const float SpeedVariation = 0.1;   // var: { "min": 0.0, "max": 0.4, "step": 0.005, "name": "Speed Variation", "desc": "How much the speed differs from stripe to stripe" }
const float BaseSpeed = 0.025;      // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Base Speed", "desc": "Minimum speed every stripe has" }
const float SpeedSeed = 13.0;       // var: { "min": 1.0, "max": 50.0, "step": 0.1, "name": "Speed Seed", "desc": "Reshuffles which stripes are fast and which are slow" }

// --- Vignette ---
const float VignetteStrength = 20.0; // var: { "min": 4.0, "max": 40.0, "step": 0.5, "name": "Vignette Strength", "desc": "Size of the bright center: higher = brighter, wider center before the edges fade" }

// --- Perspective plane ---
const float TiltAngle = 55.0;   // var: { "min": -80.0, "max": 80.0, "step": 0.5, "name": "Tilt", "desc": "Pitch of the picture plane: positive leans the top away (floor view), negative the bottom (ceiling view), 0 = flat", "unit": "°" }
const float YawAngle = 0.0;     // var: { "min": -70.0, "max": 70.0, "step": 0.5, "name": "Yaw", "desc": "Turns the plane left / right around the vertical axis", "unit": "°" }
const float FieldOfView = 60.0; // var: { "min": 20.0, "max": 120.0, "step": 1.0, "name": "Field of View", "desc": "Camera lens angle: wider = stronger perspective", "unit": "°" }
const float DepthFade = 0.35;   // var: { "min": 0.0, "max": 2.0, "step": 0.01, "name": "Depth Fade", "desc": "How fast the plane fades out towards the horizon (0 = no fade)" }

// Live controls
#define SlantAngle     mix(-40.0, 80.0, iMouse.x)  // stripe tilt in degrees within the plane (0 = vertical stripes)
#define PictureSize    mix(0.5, 1.5, iMouse.y)     // zoom of the pictures (and stripe width)
#define VignettePower  mix(0.0, 2.4, iMouse.z)     // edge fade exponent (0 = off)
#define Brightness     mix(0.0, 2.0, iMouse.w)     // picture brightness

// Edge darkening / fade to transparent: 0 at the screen border, ~1 inside
float Vignette(vec2 uv, float power)
{
    uv *= 1.0 - uv.yx;                     // u*(1-v), v*(1-u): 0 at every edge (thanks FabriceNeyret)
    float vig = uv.x * uv.y * VignetteStrength;
    return pow(max(vig, 1e-6), power);     // max: avoids pow(0, 0) at the border
}

// Atlas lookup, wrapped, straight -> premultiplied alpha
vec4 Image(vec2 uv)
{
    vec4 t = texture2D(tex, fract(uv));
    t.xyz *= t.a;
    return t;
}

// rotate a vector around the y axis (yaw)
vec3 RotY(vec3 v, float a)
{
    float c = cos(a), s = sin(a);
    return vec3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
}

void main(void)
{
    vec2 fragCoord = gl_FragCoord.xy;
    // on screen size of one atlas tile (same scale on both axes = undistorted pictures)
    float scale = StripeWidthScale * PictureSize;
    float tileW = TileWidth * scale;
    // repeat length of one atlas column (all its rows) along the stripe, in pixels
    float columnH = TileCountY * TileHeight * scale;

    // --- perspective plane ---
    // camera at the origin looking down +z; the plane passes through (0, 0, f),
    // so with 0 tilt / yaw one plane unit = one screen unit (same look as flat)
    vec2 sp = (fragCoord - 0.5 * resolution.xy) / resolution.y;
    float f = 0.5 / tan(radians(clamp(FieldOfView, 10.0, 150.0)) * 0.5);
    vec3 rd = vec3(sp, f);
    vec3 center = vec3(0.0, 0.0, f);
    float pitch = radians(TiltAngle), yaw = radians(YawAngle);
    vec3 ex = RotY(vec3(1.0, 0.0, 0.0), yaw);
    vec3 ey = RotY(vec3(0.0, cos(pitch), sin(pitch)), yaw);   // positive tilt: top leans away
    vec3 n = cross(ex, ey);
    float denom = dot(rd, n);
    float t = abs(denom) > 1e-6 ? dot(center, n) / denom : -1.0;
    if (t <= 0.0) { gl_FragColor = vec4(0.0); return; }        // above the horizon: empty
    vec3 hit = rd * t - center;
    vec2 p = vec2(dot(hit, ex), dot(hit, ey)) * resolution.y;  // plane coords in pixels
    float depthFade = exp(-max(t - 1.0, 0.0) * DepthFade);     // fade into the distance (hides moire)

    // rotated frame in the plane: 'across' runs perpendicular to the stripes, 'along' with them,
    // so the pictures are rotated rectangles (90 degree corners), not sheared
    float ang = radians(SlantAngle);
    vec2 acrossDir = vec2(cos(ang), sin(ang));
    vec2 alongDir = vec2(-acrossDir.y, acrossDir.x);

    // stripe index + position across the stripe (0..1)
    float screenStripe = dot(p, acrossDir) / tileW;
    float sourceU = fract(screenStripe);
    screenStripe = floor(screenStripe);

    // alternate direction per stripe, pseudo random speed per stripe
    float flip = mod(screenStripe, 2.0) - 0.5;
    float speedV = (abs(sin(screenStripe * SpeedSeed)) * SpeedVariation + BaseSpeed) * flip;
    float dv = (time * ScrollSpeed) * speedV;

    // stripe -> atlas column; v scrolls through the column along the stripe
    float u = (mod(screenStripe, TileCountX) + sourceU) / TileCountX;
    float v = dot(p, alongDir) / columnH + dv;

    vec4 col = Image(vec2(u, v));
    col.rgb = min(col.rgb * Brightness, col.a);   // keep premultiplied rgb <= alpha

    vec2 uv = fragCoord / resolution.xy;
    gl_FragColor = col * (Vignette(uv, VignettePower) * depthFade);
}
