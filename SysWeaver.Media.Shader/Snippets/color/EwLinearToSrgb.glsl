// =============================================================================
//  EwLinearToSrgb - Color: linear -> sRGB (exact)
// =============================================================================
//  Exact piecewise sRGB transfer function (IEC 61966-2-1).
//
//  c       : linear rgb, negative values are clamped to 0
//  returns : sRGB encoded rgb (inputs above 1 stay above 1)
//
//  Requires: -
//  Docs    : EwLinearToSrgb.md
// =============================================================================
vec3 EwLinearToSrgb(vec3 c)
{
    c = max(c, 0.0);
    vec3 linearPart = 12.92 * c;
    vec3 powerPart = 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055;
    // Select per channel: linear segment near black, power curve above
    return mix(powerPart, linearPart, vec3(lessThanEqual(c, vec3(0.0031308))));
}
