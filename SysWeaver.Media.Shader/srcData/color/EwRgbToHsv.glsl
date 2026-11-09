// =============================================================================
//  EwRgbToHsv - Color: RGB -> HSV
// =============================================================================
//  Branchless RGB to HSV (Sam Hocevar). Inverse of EwHsvToRgb.
//
//  rgb     : color, components in [0, 1]
//  returns : x = hue [0, 1), y = saturation [0, 1], z = value [0, 1]
//
//  Requires: -
//  Docs    : EwRgbToHsv.md
// =============================================================================
vec3 EwRgbToHsv(vec3 rgb)
{
    const vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    // Sort channels without branches: p / q hold max, min and the hue sector
    vec4 p = mix(vec4(rgb.bg, K.wz), vec4(rgb.gb, K.xy), step(rgb.b, rgb.g));
    vec4 q = mix(vec4(p.xyw, rgb.r), vec4(rgb.r, p.yzx), step(p.x, rgb.r));
    float chroma = q.x - min(q.w, q.y);
    const float epsilon = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * chroma + epsilon)), chroma / (q.x + epsilon), q.x);
}
