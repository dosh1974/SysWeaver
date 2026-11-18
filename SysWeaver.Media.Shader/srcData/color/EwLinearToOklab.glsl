// =============================================================================
//  EwLinearToOklab - Color: linear sRGB -> Oklab
// =============================================================================
//  Björn Ottosson's Oklab: a perceptual color space, so a mix / gradient in
//  it keeps an even brightness and hue (no muddy or too dark middle as with
//  a mix in rgb). Mix in Oklab, convert back with EwOklabToLinear.
//
//  c       : linear rgb (EwSrgbToLinear for an sRGB color)
//  returns : x = L (lightness, 0 = black, 1 = white), y = a (green .. red),
//            z = b (blue .. yellow); a and b are about -0.4 .. 0.4
//
//  Requires: -
//  Docs    : EwLinearToOklab.md
// =============================================================================
vec3 EwLinearToOklab(vec3 c)
{
    // Linear sRGB -> cone responses (LMS)
    vec3 lms = vec3(
        dot(c, vec3(0.4122214708, 0.5363325363, 0.0514459929)),
        dot(c, vec3(0.2119034982, 0.6806995451, 0.1073969566)),
        dot(c, vec3(0.0883024619, 0.2817188376, 0.6299787005)));
    // Cube root (sign kept: out of gamut colors can be negative)
    lms = sign(lms) * pow(abs(lms), vec3(1.0 / 3.0));
    return vec3(
        dot(lms, vec3(0.2104542553, 0.7936177850, -0.0040720468)),
        dot(lms, vec3(1.9779984951, -2.4285922050, 0.4505937099)),
        dot(lms, vec3(0.0259040371, 0.7827717662, -0.8086757660)));
}
