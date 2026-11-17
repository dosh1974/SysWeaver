// Stripes - picture collage cut into tilted stripes that slide along their length
// Each stripe shows one column of the collage atlas as upright (rotated) rectangles;
// neighbouring stripes move in opposite directions at different speeds,
// the screen edges fade out (vignette).
//
//  iMouse.x = Slant | Tilt of the stripes and pictures (0 = vertical) | 0.5 | -40 .. 80 | °
//  iMouse.y = Picture Size | Zoom of the pictures and stripes | 0.5 | 0.5 .. 1.5 | x
//  iMouse.z = Vignette | Edge fade exponent (0 = off, higher = stronger fade) | 0.5 | 0.0 .. 2.4
//  iMouse.w = Brightness | Picture brightness | 0.5 | 0.0 .. 2.0 | x
//  media: collage stock:DemoCollage.svg

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
const float ImageWidth = 164.0; //var:
const float ImageHeight = 94.0; //var:

// --- Stripes ---
const float StripeWidthScale = 1.0; // var: { "min": 0.25, "max": 4.0, "step": 0.05, "name": "Stripe Width", "desc": "Stripe width relative to one atlas tile: lower = more, narrower stripes", "unit": "x" }

// --- Motion ---
const float ScrollSpeed = 1.0;      // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Scroll Speed", "desc": "Overall speed of the up / down sliding stripes (0 = frozen)", "unit": "x" }
const float SpeedVariation = 0.1;   // var: { "min": 0.0, "max": 0.4, "step": 0.005, "name": "Speed Variation", "desc": "How much the speed differs from stripe to stripe" }
const float BaseSpeed = 0.025;      // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Base Speed", "desc": "Minimum speed every stripe has" }
const float SpeedSeed = 13.0;       // var: { "min": 1.0, "max": 50.0, "step": 0.1, "name": "Speed Seed", "desc": "Reshuffles which stripes are fast and which are slow" }

// --- Vignette ---
const float VignetteStrength = 20.0; // var: { "min": 4.0, "max": 40.0, "step": 0.5, "name": "Vignette Strength", "desc": "Size of the bright center: higher = brighter, wider center before the edges fade" }

// Live controls
#define SlantAngle     mix(-40.0, 80.0, iMouse.x)  // stripe tilt in degrees (0 = vertical stripes)
#define PictureSize    mix(0.5, 1.5, iMouse.y)     // zoom of the pictures (and stripe width)
#define VignettePower  mix(0.0, 2.4, iMouse.z)     // edge fade exponent (0 = off)
#define Brightness     mix(0.0, 2.0, iMouse.w)    // picture brightness

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

void main(void)
{
    vec2 fragCoord = gl_FragCoord.xy;
    // on screen size of one atlas tile (same scale on both axes = undistorted pictures)
    float scale = StripeWidthScale * PictureSize;
    float tileW = TileWidth * scale;
    // repeat length of one atlas column (all its rows) along the stripe, in pixels
    float columnH = TileCountY * TileHeight * scale;

    // rotated frame: 'across' runs perpendicular to the stripes, 'along' with them,
    // so the pictures are rotated rectangles (90 degree corners), not sheared
    float ang = radians(SlantAngle);
    vec2 acrossDir = vec2(cos(ang), sin(ang));
    vec2 alongDir = vec2(-acrossDir.y, acrossDir.x);
    vec2 p = fragCoord - 0.5 * resolution.xy;   // rotate around the screen center

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

    // Mask to the picture area of the tile (analytic, anti-aliased): kills the
    // mipmap seams where fract() / the stripe change makes the uv jump
    // (huge derivatives -> tiniest mip = grey dotted lines) and bilinear bleeding
    vec2 local = vec2(sourceU * TileWidth, fract(v * TileCountY) * TileHeight);
    vec2 edge = min(local, vec2(TileWidth, TileHeight) - local)
              - 0.5 * (vec2(TileWidth, TileHeight) - vec2(ImageWidth, ImageHeight));
    vec2 aa = clamp(edge * scale + 0.5, 0.0, 1.0);   // texel -> screen pixels
    col *= aa.x * aa.y;
    col.rgb = min(col.rgb * Brightness, col.a);   // keep premultiplied rgb <= alpha

    vec2 uv = fragCoord / resolution.xy;
    gl_FragColor = col * Vignette(uv, VignettePower);
}
