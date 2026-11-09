// =============================================================================
//  EwHsvToRgb - Color: HSV -> RGB
// =============================================================================
//  Branchless HSV to RGB (Sam Hocevar). Hue wraps, so any hue value works.
//
//  hsv     : x = hue [0, 1] (wraps), y = saturation [0, 1], z = value [0, 1]
//  returns : rgb in [0, 1] (for inputs in range)
//
//  Requires: -
//  Docs    : EwHsvToRgb.md
// =============================================================================
vec3 EwHsvToRgb(vec3 hsv)
{
    const vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    // Triangle waves per channel, offset by 0, 1/3, 2/3 of the hue circle
    vec3 p = abs(fract(hsv.xxx + K.xyz) * 6.0 - K.www);
    return hsv.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), hsv.y);
}
