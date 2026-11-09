// =============================================================================
//  EwSaturation - Color: saturation adjust
// =============================================================================
//  Mixes between the luminance grey and the color (extrapolates above 1).
//
//  color   : rgb color
//  amount  : 0 = grey, 1 = unchanged, > 1 = boosted saturation
//  returns : adjusted rgb (may leave [0, 1] for amount > 1)
//
//  Requires: EwLuminance
//  Docs    : EwSaturation.md
// =============================================================================
vec3 EwSaturation(vec3 color, float amount)
{
    return mix(vec3(EwLuminance(color)), color, amount);
}
