// =============================================================================
//  EwDither - Post: animated screen space dither
// =============================================================================
//  Adds +-0.5 / 255 * strength of white noise to break up 8-bit banding.
//
//  fragCoord : pixel coordinate (fragCoord)
//  time      : iTime for animated dither, 0.0 for static dither
//  strength  : 1.0 = +-0.5 LSB of an 8-bit output
//  returns   : offset to add to the final color
//
//  Requires: EwHash2To1
//  Docs    : EwDither.md
// =============================================================================
float EwDither(vec2 fragCoord, float time, float strength)
{
    // fract(time) * 91.7 shifts the pattern every frame without precision loss
    return (EwHash2To1(fragCoord + fract(time) * 91.7) - 0.5) * strength / 255.0;
}
