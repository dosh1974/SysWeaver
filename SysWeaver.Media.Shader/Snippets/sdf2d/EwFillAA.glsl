// =============================================================================
//  EwFillAA - SDF 2D: anti-aliased fill mask
// =============================================================================
//  Converts a signed distance into a smooth coverage mask: 1 inside, 0
//  outside, a smooth ramp of width 2 * feather centered on the border.
//
//  d       : signed distance (< 0 inside)
//  feather : half width of the transition, > 0 (e.g. fwidth(d) or 1.5 / iResolution.y)
//  returns : coverage in [0, 1]
//
//  Requires: -
//  Docs    : EwFillAA.md
// =============================================================================
float EwFillAA(float d, float feather)
{
    return 1.0 - smoothstep(-feather, feather, d);
}
