// =============================================================================
//  EwHueToRgbSmooth - Color: hue -> fully saturated RGB (smooth)
// =============================================================================
//  iq smooth HSV: the linear hue ramp shaped with a cubic smoothstep (C1).
//
//  hue     : hue, 0..1 is one full turn (wraps)
//  returns : rgb in [0, 1], fully saturated
//
//  Requires: -
//  Docs    : EwHueToRgbSmooth.md
// =============================================================================
vec3 EwHueToRgbSmooth(float hue)
{
    vec3 rgb = clamp(abs(mod(hue * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    // Cubic smoothstep removes the kinks of the linear ramp
    return rgb * rgb * (3.0 - 2.0 * rgb);
}
