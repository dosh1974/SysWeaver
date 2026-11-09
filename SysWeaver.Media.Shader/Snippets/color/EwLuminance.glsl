// =============================================================================
//  EwLuminance - Color: relative luminance (Rec.709)
// =============================================================================
//  Rec.709 / sRGB luminance weights. Meant for linear rgb.
//
//  c       : rgb color (linear for correct luminance)
//  returns : luminance, 1.0 for white
//
//  Requires: -
//  Docs    : EwLuminance.md
// =============================================================================
float EwLuminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}
