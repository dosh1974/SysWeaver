// CRT Screen
// Shows the picture on a curved cathode ray tube: barrel warped glass,
// rolling dark scanlines and a crisp (or soft) rounded screen border.
//
// Live controls:
//  iMouse.x = Curvature | Bulge of the curved glass | 0.375 | 0.0 .. 2.0 | x
//  iMouse.y = Scanlines | Darkness of the scanlines | 0.375 | 0.0 .. 2.0 | x
//  iMouse.z = Scanline Density | Scanline frequency (higher = finer lines) | 0.5 | 0.5 .. 1.5 | x
//  iMouse.w = Brightness | Picture brightness | 0.5 | 0.0 .. 2.0 | x

uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;

uniform sampler2D tex;

// --- Screen shape ---
const float CurveX = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Curve X", "desc": "Horizontal bulge of the glass (left / right edges bow out)" }
const float CurveY = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Curve Y", "desc": "Vertical bulge of the glass (top / bottom edges bow out)" }
const float Edge = 0.3; // var: { "min": 0.01, "max": 1.0, "step": 0.01, "name": "Edge Sharpness", "desc": "Sharpness of the screen border: low = soft fade, high = crisp cut" }

// --- Scanlines ---
const float ScanSpeed = 1.0; // var: { "min": 0.0, "max": 10.0, "step": 0.1, "name": "Scan Speed", "desc": "How fast the scanlines roll" }
const vec3 ScanColor = vec3(0.0); // var: { "type": "col", "name": "Scanline Color", "desc": "Color the scanlines darken towards" }

// --- Bezel ---
const vec3 BezelColor = vec3(0.0); // var: { "type": "col", "name": "Bezel Color", "desc": "Color outside the curved screen" }
const float BezelOpacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Bezel Opacity", "desc": "Opacity outside the screen (0 = transparent)", "scale": 100, "unit": "%" }

// Live controls
#define Warp mix(0.0, 2.0, iMouse.x)
#define Scan mix(0.0, 2.0, iMouse.y)
#define ScanDensity mix(0.5, 1.5, iMouse.z)
#define Brightness mix(0.0, 2.0, iMouse.w)

void main()
{
    float edgeSize = (resolution.x + resolution.y) * Edge;

    // squared distance from the center
    vec2 uv = gl_FragCoord.xy / resolution.xy;
    vec2 dc = abs(0.5 - uv);
    dc *= dc;

    // barrel warp: each axis stretches with the squared distance along the other
    uv = (uv - 0.5) * (1.0 + dc.yx * vec2(CurveX, CurveY) * Warp) + 0.5;

    // rolling scanlines (phase wrapped for precision)
    float apply = abs(sin(gl_FragCoord.y * ScanDensity + mod(time * ScanSpeed, 6.2831853)) * 0.5 * Scan);

    // sample the picture, darken by the scanline, premultiply
    vec4 color = texture2D(tex, uv);
    color.rgb = min(mix(color.rgb, ScanColor, apply) * Brightness, 1.0) * color.a;

    // screen border: fade over 1 / edgeSize at the edges, full bezel outside
    vec2 d = min(uv, 1.0 - uv);
    float edgeFade = clamp(1.0 - min(d.x, d.y) * edgeSize, 0.0, 1.0);
    color = mix(color, vec4(BezelColor * BezelOpacity, BezelOpacity), edgeFade);

    gl_FragColor = color;
}
