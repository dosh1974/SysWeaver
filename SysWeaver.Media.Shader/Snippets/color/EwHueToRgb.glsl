// =============================================================================
//  EwHueToRgb - Color: hue -> fully saturated RGB
// =============================================================================
//  Piecewise linear hue ramp (same as EwHsvToRgb with s = v = 1).
//
//  hue     : hue, 0..1 is one full turn (wraps)
//  returns : rgb in [0, 1], fully saturated
//
//  Requires: -
//  Docs    : EwHueToRgb.md
// =============================================================================
vec3 EwHueToRgb(float hue)
{
    return clamp(abs(mod(hue * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}
