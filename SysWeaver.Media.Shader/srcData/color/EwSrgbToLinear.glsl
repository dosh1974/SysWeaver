// =============================================================================
//  EwSrgbToLinear - Color: sRGB -> linear (exact)
// =============================================================================
//  Exact piecewise sRGB transfer function (IEC 61966-2-1), the inverse of
//  EwLinearToSrgb. Use it on colors picked in sRGB (constants, color
//  variables, textures) before mixing / lighting in linear space.
//
//  c       : sRGB encoded rgb, negative values are clamped to 0
//  returns : linear rgb (inputs above 1 stay above 1)
//
//  Requires: -
//  Docs    : EwSrgbToLinear.md
// =============================================================================
vec3 EwSrgbToLinear(vec3 c)
{
    c = max(c, 0.0);
    vec3 linearPart = c / 12.92;
    vec3 powerPart = pow((c + 0.055) / 1.055, vec3(2.4));
    // Select per channel: linear segment near black, power curve above
    return mix(powerPart, linearPart, vec3(lessThanEqual(c, vec3(0.04045))));
}
