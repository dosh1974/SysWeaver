// =============================================================================
//  EwLinearToSrgbFast - Color: linear -> sRGB (gamma 2.2 approximation)
// =============================================================================
//  pow(c, 1 / 2.2). Cheap approximation of the sRGB curve.
//
//  c       : linear rgb, negative values are clamped to 0
//  returns : gamma encoded rgb
//
//  Requires: -
//  Docs    : EwLinearToSrgbFast.md
// =============================================================================
vec3 EwLinearToSrgbFast(vec3 c)
{
    return pow(max(c, 0.0), vec3(1.0 / 2.2));
}
