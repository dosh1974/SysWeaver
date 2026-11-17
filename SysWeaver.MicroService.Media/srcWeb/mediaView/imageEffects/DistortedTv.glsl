uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

#define iTime time
#define iResolution resolution
#define iChannel0 tex
#define texture texture2D

// =============================================================================
//  Distorted TV - a picture on a failing analog television
// =============================================================================
//  Shows the chosen image as a badly tuned CRT signal: sideways wobble per row,
//  red / blue color fringes, scanlines, sparse white static specks and a picture
//  that now and then rolls or jumps vertically.
//
//  Live controls:
//  iMouse.x = RGB Split | Sideways shift of the red / blue fringes | 0.5 | 0.0 .. 0.02
//  iMouse.y = Static | Strength of the white static specks | 0.5 | 0.0 .. 2.0 | x
//  iMouse.z = Wobble | Sideways row fuzz / wave distortion | 0.5 | 0.0 .. 2.0 | x
//  iMouse.w = Scanlines | Darkness of the scanline rows | 0.5 | 0.0 .. 0.08
// =============================================================================

#define RgbSplit         mix(0.0, 0.02, iMouse.x)
#define StaticAmount     mix(0.0, 2.0, iMouse.y)
#define HorzFuzz         mix(0.0, 2.0, iMouse.z)
#define ScanlineStrength mix(0.0, 0.08, iMouse.w)

// --- Motion ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Speed", "desc": "Speed of all glitches (wobble, rolls, static)", "unit": "x" }
const float VertRoll = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Vertical Roll", "desc": "Amount of the occasional rolling scroll (0 = off)", "scale": 100, "unit": "%" }
const float VertJerk = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Vertical Jumps", "desc": "Amount of short vertical jumps (0 = off)", "scale": 100, "unit": "%" }
const float RollDistance = 4.0; // var: { "min": 0.0, "max": 8.0, "step": 0.1, "name": "Roll Distance", "desc": "How far the picture scrolls during a roll", "unit": "screens" }
const float JerkDistance = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Jump Distance", "desc": "Height of a vertical jump", "scale": 100, "unit": "% screen" }

// --- Static ---
const float StaticCutoff = 0.3; // var: { "min": 0.0, "max": 0.8, "step": 0.01, "name": "Static Sparsity", "desc": "Higher = fewer static specks, lower = denser snow" }

// --- Scanlines ---
const float ScanlineDensity = 800.0; // var: { "min": 200.0, "max": 2000.0, "step": 10.0, "name": "Scanline Density", "desc": "Frequency of the scanline rows (higher = finer lines)" }

// --- Wobble detail ---
const float FuzzFineScale = 80.0; // var: { "min": 10.0, "max": 200.0, "step": 1.0, "name": "Fine Fuzz Scale", "desc": "Row density of the fast fine jitter" }
const float FuzzWideScale = 25.0; // var: { "min": 2.0, "max": 80.0, "step": 1.0, "name": "Wide Wave Scale", "desc": "Row density of the slow wide waves" }
// Noise generation functions borrowed from: 
// https://github.com/ashima/webgl-noise/blob/master/src/noise2D.glsl

vec3 mod289(vec3 x) {
  return x - floor(x * (1.0 / 289.0)) * 289.0;
}

// No overloaded functions in effects: the shader minifier of the build (shader_minifier 1.6.0) renamed snoise(vec2) to
// the minified name of this overload, two functions with the same signature, the built effect failed to compile.
vec2 mod289v2(vec2 x) {
  return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec3 permute(vec3 x) {
  return mod289(((x*34.0)+1.0)*x);
}

float snoise(vec2 v)
  {
  const vec4 C = vec4(0.211324865405187,  // (3.0-sqrt(3.0))/6.0
                      0.366025403784439,  // 0.5*(sqrt(3.0)-1.0)
                     -0.577350269189626,  // -1.0 + 2.0 * C.x
                      0.024390243902439); // 1.0 / 41.0
// First corner
  vec2 i  = floor(v + dot(v, C.yy) );
  vec2 x0 = v -   i + dot(i, C.xx);

// Other corners
  vec2 i1;
  //i1.x = step( x0.y, x0.x ); // x0.x > x0.y ? 1.0 : 0.0
  //i1.y = 1.0 - i1.x;
  i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
  // x0 = x0 - 0.0 + 0.0 * C.xx ;
  // x1 = x0 - i1 + 1.0 * C.xx ;
  // x2 = x0 - 1.0 + 2.0 * C.xx ;
  vec4 x12 = x0.xyxy + C.xxzz;
  x12.xy -= i1;

// Permutations
  i = mod289v2(i); // Avoid truncation effects in permutation
  vec3 p = permute( permute( i.y + vec3(0.0, i1.y, 1.0 ))
		+ i.x + vec3(0.0, i1.x, 1.0 ));

  vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
  m = m*m ;
  m = m*m ;

// Gradients: 41 points uniformly over a line, mapped onto a diamond.
// The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

  vec3 x = 2.0 * fract(p * C.www) - 1.0;
  vec3 h = abs(x) - 0.5;
  vec3 ox = floor(x + 0.5);
  vec3 a0 = x - ox;

// Normalise gradients implicitly by scaling m
// Approximation of: m *= inversesqrt( a0*a0 + h*h );
  m *= 1.79284291400159 - 0.85373472095314 * ( a0*a0 + h*h );

// Compute final noise value at P
  vec3 g;
  g.x  = a0.x  * x0.x  + h.x  * x0.y;
  g.yz = a0.yz * x12.xz + h.yz * x12.yw;
  return 130.0 * dot(m, g);
}

// Bright static specks of one row: a noise band that drifts through the picture,
// thresholded to sparse white flecks; height, coverage and strength wander over time.
float staticV(vec2 uv, float t) {
    float staticHeight = snoise(vec2(9.0, t * 1.2 + 3.0)) * 0.3 + 5.0;
    float staticAmount = snoise(vec2(1.0, t * 1.2 - 6.0)) * 0.1 + StaticCutoff;
    float staticStrength = snoise(vec2(-9.75, t * 0.6 - 3.0)) * 2.0 + 2.0;
    // pow(t, 2.0) as in the original (t >= 0); the band position grows with t * t
    return (1.0 - step(snoise(vec2(5.0 * pow(t, 2.0) + pow(uv.x * 7.0, 1.2),
                                   pow((mod(t, 100.0) + 100.0) * uv.y * 0.3 + 3.0, staticHeight))),
                       staticAmount)) * staticStrength;
}


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    float t = iTime * Speed;

    // Horizontal wobble: fast fine fuzz + slower wide waves, per picture row
    float fuzzOffset = snoise(vec2(t * 15.0, uv.y * FuzzFineScale)) * 0.003;
    float largeFuzzOffset = snoise(vec2(t * 1.0, uv.y * FuzzWideScale)) * 0.004;
    float xOffset = (fuzzOffset + largeFuzzOffset) * HorzFuzz;

    // Vertical roll: random noise gates switch a rolling scroll and short jumps on / off
    float vertMovementOn = (1.0 - step(snoise(vec2(t * 0.2, 8.0)), 0.4)) * VertRoll;
    float vertJerk = (1.0 - step(snoise(vec2(t * 1.5, 5.0)), 0.6)) * VertJerk;
    float vertJerk2 = (1.0 - step(snoise(vec2(t * 5.5, 5.0)), 0.2)) * VertJerk;
    float yOffset = abs(sin(t) * RollDistance) * vertMovementOn + vertJerk * vertJerk2 * JerkDistance;
    float y = mod(uv.y + yOffset, 1.0);

    // Static: 3 rows blended with a tent weight for slightly soft streaks
    float staticVal = 0.0;
    for (float sy = -1.0; sy <= 1.0; sy += 1.0) {
        float maxDist = 5.0 / 200.0;
        float dist = sy / 200.0;
        staticVal += staticV(vec2(uv.x, uv.y + dist), t) * (maxDist - abs(dist)) * 1.5;
    }
    staticVal *= StaticAmount;

    // Picture with red / blue fringes shifted sideways (chromatic split)
    vec4 image = texture(iChannel0, vec2(uv.x + xOffset, y));
    float red   = texture(iChannel0, vec2(uv.x + xOffset - RgbSplit, y)).r + staticVal;
    float green = image.g + staticVal;
    float blue  = texture(iChannel0, vec2(uv.x + xOffset + RgbSplit, y)).b + staticVal;

    // Scanlines darken / brighten fixed rows
    vec3 color = vec3(red, green, blue);
    color -= sin(uv.y * ScanlineDensity) * ScanlineStrength;

    // Premultiplied alpha (transparent pictures stay transparent)
    fragColor = vec4(color * image.w, image.w);
}
